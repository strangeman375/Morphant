using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Morphant.Generator.TypeMapperGeneration;

// Caller values belong to the source initializer. Preserve them before any
// callback relocation or extension-syntax rewriting. Text emission carries
// only binding identities and call templates; user expressions stay in syntax.
internal static class CollectionCallerInformation
{
    internal const string Annotation = "Morphant.CollectionCallerInformation";
    internal const string Receiver = "__collectionReceiver";
    internal const string ArgumentPrefix = "__collectionArgument";

    public static InitializerExpressionSyntax Mark(InitializerExpressionSyntax original,
        InitializerExpressionSyntax rewritten, SemanticModel semantic, Func<ITypeSymbol, string> typeName)
    {
        if (!original.IsKind(SyntaxKind.CollectionInitializerExpression) ||
            semantic.GetOperation(original) is not IObjectOrCollectionInitializerOperation operation)
            return rewritten;
        var creation = original.Ancestors().OfType<BaseObjectCreationExpressionSyntax>().FirstOrDefault();
        if (!operation.Initializers.OfType<IInvocationOperation>().Any(call => call.Arguments.Any(IsCaller)) &&
            (creation is null || semantic.GetOperation(creation) is not { } creationOperation ||
             !creationOperation.DescendantsAndSelf().OfType<IObjectOrCollectionInitializerOperation>()
                 .Any(initializer => initializer.Initializers.OfType<IInvocationOperation>().Any(call => call.Arguments.Any(IsCaller)))))
            return rewritten;

        var metadata = new List<string>();
        var elements = rewritten.Expressions;
        for (var index = 0; index < operation.Initializers.Length; index++)
        {
            var element = elements[index];
            var arguments = Arguments(element);
            var placeholders = Enumerable.Range(0, arguments.Count)
                .Select(i => ArgumentPrefix + i.ToString(CultureInfo.InvariantCulture)).ToList();
            if (operation.Initializers[index] is not IInvocationOperation call)
            {
                metadata.Add(string.Empty);
                metadata.Add(Receiver + ".Add(" + string.Join(", ", placeholders) + ")");
                continue;
            }

            var method = call.TargetMethod;
            var name = method.Name + (method.IsGenericMethod
                ? "<" + string.Join(", ", method.TypeArguments.Select(typeName)) + ">" : "");
            var target = method.IsExtensionMethod ? typeName(method.ContainingType) : Receiver;
            if (method.IsExtensionMethod)
                placeholders.Insert(0, (method.Parameters[0].RefKind == RefKind.Ref ? "ref " : "") + Receiver);
            placeholders.AddRange(call.Arguments.Where(IsCaller).Select(argument =>
                Escape(argument.Parameter!.Name) + ": " + Constant(argument, typeName)));
            metadata.Add(MethodKey(method, typeName));
            metadata.Add(target + "." + name + "(" + string.Join(", ", placeholders) + ")");

            var lastCaller = call.Arguments.Where(IsCaller).Select(argument => argument.Parameter!.Ordinal)
                .DefaultIfEmpty(-1).Max();
            foreach (var argument in call.Arguments.Where(argument => argument.ArgumentKind == ArgumentKind.DefaultValue &&
                         argument.Parameter!.Ordinal <= lastCaller).OrderBy(argument => argument.Parameter!.Ordinal))
                arguments = arguments.Add(SyntaxFactory.ParseExpression(Constant(argument, typeName)));

            if (lastCaller >= 0)
            {
                var updated = element is InitializerExpressionSyntax complex
                    ? complex.WithExpressions(arguments)
                    : SyntaxFactory.InitializerExpression(SyntaxKind.ComplexElementInitializerExpression, arguments).WithTriviaFrom(element);
                elements = elements.Replace(element, updated);
            }
        }
        return rewritten.WithExpressions(elements).WithOpenBraceToken(
            ExtensionInvocationSimplifier.MarkCollection(rewritten.OpenBraceToken, metadata.ToArray(), semantic.Compilation));
    }

    public static string Restore(string source, CSharpCompilation compilation,
        CSharpParseOptions? options, CancellationToken cancellationToken)
    {
        if (source.IndexOf("/*Morphant.Extension", StringComparison.Ordinal) < 0) return source;
        var prefix = ExtensionInvocationSimplifier.MarkerScopePrefix(compilation) + "Collection:";
        if (source.IndexOf(prefix, StringComparison.Ordinal) < 0) return source;
        var root = CSharpSyntaxTree.ParseText(source, options, cancellationToken: cancellationToken)
            .GetCompilationUnitRoot(cancellationToken);
        var marked = root.DescendantNodes().OfType<InitializerExpressionSyntax>()
            .Where(node => node.IsKind(SyntaxKind.CollectionInitializerExpression))
            .Select(node => (Node: node, Marker: node.OpenBraceToken.TrailingTrivia
                .AddRange(node.Expressions.FirstOrDefault()?.GetLeadingTrivia() ?? default)
                .FirstOrDefault(trivia => trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) &&
                    trivia.ToString().StartsWith(prefix, StringComparison.Ordinal))))
            .Where(item => item.Marker != default).ToDictionary(item => item.Node, item => item.Marker);
        root = root.ReplaceNodes(marked.Keys, (original, rewritten) =>
        {
            var marker = marked[original].ToString();
            var data = Encoding.UTF8.GetString(Convert.FromBase64String(marker.Substring(prefix.Length, marker.Length - prefix.Length - 2)));
            return rewritten.ReplaceTrivia(rewritten.DescendantTrivia().Where(trivia => trivia.ToString() == marker),
                    (_, _) => default)
                .WithAdditionalAnnotations(new SyntaxAnnotation(Annotation, data));
        });
        var tree = CSharpSyntaxTree.Create(root, options);
        root = tree.GetCompilationUnitRoot(cancellationToken);
        var semantic = compilation.AddSyntaxTrees(tree).GetSemanticModel(tree);
        var rejected = new HashSet<InitializerExpressionSyntax>(root.GetAnnotatedNodes(Annotation).OfType<InitializerExpressionSyntax>()
            .Where(initializer => !Matches(initializer, semantic, cancellationToken)));
        return rejected.Count == 0 ? root.ToFullString() :
            new CollectionInitializerLowerer(semantic, rejected, cancellationToken).Visit(root)!.ToFullString();
    }

    private static bool Matches(InitializerExpressionSyntax initializer, SemanticModel semantic, CancellationToken token)
    {
        var metadata = Metadata(initializer);
        for (var index = 0; index < initializer.Expressions.Count; index++)
        {
            if (metadata[index * 2].Length == 0) continue;
            if (semantic.GetCollectionInitializerSymbolInfo(initializer.Expressions[index], token).Symbol is not IMethodSymbol method ||
                MethodKey(method, TypeMapperMappingTypePolicy.GetGeneratedTypeName) != metadata[index * 2]) return false;
        }
        return true;
    }

    internal static string[] Metadata(InitializerExpressionSyntax initializer) =>
        initializer.GetAnnotations(Annotation).Single().Data!.Split('\0');

    internal static SeparatedSyntaxList<ExpressionSyntax> Arguments(ExpressionSyntax element) =>
        element is InitializerExpressionSyntax complex ? complex.Expressions : SyntaxFactory.SingletonSeparatedList(element);

    private static bool IsCaller(IArgumentOperation argument) =>
        argument.ArgumentKind == ArgumentKind.DefaultValue && argument.Parameter is { } parameter &&
        ConstructExpressionRewriter.HasCallerInfoAttribute(parameter);

    private static string MethodKey(IMethodSymbol method, Func<ITypeSymbol, string> typeName)
    {
        if (method.ReducedFrom is { } reduced)
            method = method.IsGenericMethod ? reduced.ConstructedFrom.Construct(method.TypeArguments, method.TypeArgumentNullableAnnotations) : reduced;
        return typeName(method.ContainingType) + "." + method.MetadataName + "<" +
            string.Join(",", method.TypeArguments.Select(typeName)) + ">(" +
            string.Join(",", method.Parameters.Select(parameter => parameter.RefKind + ":" + typeName(parameter.Type))) + ")";
    }

    private static string Constant(IArgumentOperation argument, Func<ITypeSymbol, string> typeName)
    {
        var value = argument.Value;
        while (value is IConversionOperation conversion) value = conversion.Operand;
        var type = argument.Parameter!.Type;
        if (!value.ConstantValue.HasValue || value.ConstantValue.Value is null)
            return "default(" + typeName(type) + ")!";
        var constant = value.ConstantValue.Value;
        var text = constant switch
        {
            float number when float.IsNaN(number) => "float.NaN",
            float number when float.IsPositiveInfinity(number) => "float.PositiveInfinity",
            float number when float.IsNegativeInfinity(number) => "float.NegativeInfinity",
            double number when double.IsNaN(number) => "double.NaN",
            double number when double.IsPositiveInfinity(number) => "double.PositiveInfinity",
            double number when double.IsNegativeInfinity(number) => "double.NegativeInfinity",
            _ => SymbolDisplay.FormatPrimitive(constant, quoteStrings: true, useHexadecimalNumbers: false)
        };
        return SymbolEqualityComparer.Default.Equals(value.Type, type) ? text : "(" + typeName(type) + ")" + text;
    }

    private static string Escape(string name) => SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ? "@" + name : name;
}
