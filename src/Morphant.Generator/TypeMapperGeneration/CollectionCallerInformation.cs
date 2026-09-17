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
            var elementLines = original.Expressions[index].GetLocation().GetLineSpan();
            var argumentTypes = (elementLines.StartLinePosition.Line == elementLines.EndLinePosition.Line ? "S" : "M") +
                string.Join("\u001f", Arguments(original.Expressions[index]).Select(argument =>
                typeName(semantic.GetTypeInfo(argument).ConvertedType ?? semantic.GetTypeInfo(argument).Type!)));
            var placeholders = Enumerable.Range(0, arguments.Count)
                .Select(i => ArgumentPrefix + i.ToString(CultureInfo.InvariantCulture)).ToList();
            if (operation.Initializers[index] is not IInvocationOperation call)
            {
                metadata.Add(string.Empty);
                metadata.Add(Receiver + ".Add(" + string.Join(", ", placeholders) + ")");
                metadata.Add(argumentTypes);
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
            var lastCaller = call.Arguments.Where(IsCaller).Select(argument => argument.Parameter!.Ordinal)
                .DefaultIfEmpty(-1).Max();
            var defaults = call.Arguments.Where(argument => argument.ArgumentKind == ArgumentKind.DefaultValue &&
                    argument.Parameter!.Ordinal <= lastCaller).OrderBy(argument => argument.Parameter!.Ordinal).ToArray();
            // Metadata defaults such as DateTimeConstant are compiler operations,
            // not source literals. An explicit Add can leave those arguments omitted.
            metadata.Add((defaults.Any(argument => !HasConstant(argument.Value)) ? "!" : "") + MethodKey(method, typeName));
            metadata.Add(target + "." + name + "(" + string.Join(", ", placeholders) + ")");
            metadata.Add(argumentTypes);
            foreach (var argument in defaults)
                arguments = arguments.Add(SyntaxFactory.ParseExpression(Constant(argument, typeName)));

            if (lastCaller >= 0)
            {
                var updated = element is InitializerExpressionSyntax complex
                    ? complex.WithExpressions(arguments)
                    : SyntaxFactory.InitializerExpression(SyntaxKind.ComplexElementInitializerExpression,
                        arguments.Replace(arguments[0], arguments[0].WithoutTrivia())).WithTriviaFrom(element);
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
            var fields = data.Split('\0');
            rewritten = rewritten.ReplaceTrivia(rewritten.DescendantTrivia().Where(trivia => trivia.ToString() == marker), (_, _) => default);
            rewritten = rewritten.ReplaceTokens(rewritten.DescendantTokens(), (token, _) => token
                .WithLeadingTrivia(TrimLineEnds(token.LeadingTrivia)).WithTrailingTrivia(TrimLineEnds(token.TrailingTrivia)));
            rewritten = rewritten.ReplaceNodes(rewritten.Expressions.OfType<InitializerExpressionSyntax>(), (_, element) => element
                .WithOpenBraceToken(element.OpenBraceToken.TrailingTrivia.Count == 0
                    ? element.OpenBraceToken.WithTrailingTrivia(SyntaxFactory.Space) : element.OpenBraceToken)
                .WithCloseBraceToken(element.CloseBraceToken.LeadingTrivia.Count == 0
                    ? element.CloseBraceToken.WithLeadingTrivia(SyntaxFactory.Space) : element.CloseBraceToken));
            // Roslyn versions disagree about line breaks in complex elements.
            // Keep originally single-line values and synthesized caller arguments
            // together without reflowing multiline user expressions.
            var expressions = rewritten.Expressions;
            for (var index = 0; index < expressions.Count; index++)
                if (fields[index * 3 + 2][0] == 'S' && expressions[index] is InitializerExpressionSyntax element)
                    expressions = expressions.Replace(element, element.NormalizeWhitespace(indentation: "", eol: " ").WithTriviaFrom(element));
            rewritten = rewritten.WithExpressions(expressions);
            return rewritten.WithAdditionalAnnotations(new SyntaxAnnotation(Annotation, data));
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
            if (metadata[index * 3].Length == 0) continue;
            if (semantic.GetCollectionInitializerSymbolInfo(initializer.Expressions[index], token).Symbol is not IMethodSymbol method ||
                MethodKey(method, TypeMapperMappingTypePolicy.GetGeneratedTypeName) != metadata[index * 3]) return false;
        }
        return true;
    }

    internal static string[] Metadata(InitializerExpressionSyntax initializer) =>
        initializer.GetAnnotations(Annotation).Single().Data!.Split('\0');

    private static SyntaxTriviaList TrimLineEnds(SyntaxTriviaList trivia) => SyntaxFactory.TriviaList(trivia.Where((item, index) =>
        !item.IsKind(SyntaxKind.WhitespaceTrivia) || index + 1 == trivia.Count || !trivia[index + 1].IsKind(SyntaxKind.EndOfLineTrivia)));

    internal static SeparatedSyntaxList<ExpressionSyntax> Arguments(ExpressionSyntax element) =>
        element is InitializerExpressionSyntax complex ? complex.Expressions : SyntaxFactory.SingletonSeparatedList(element);

    private static bool IsCaller(IArgumentOperation argument) =>
        argument.ArgumentKind == ArgumentKind.DefaultValue && argument.Parameter is { } parameter &&
        ConstructExpressionRewriter.HasCallerInfoAttribute(parameter);

    private static bool HasConstant(IOperation value) => value is IConversionOperation conversion
        ? HasConstant(conversion.Operand) : value is IDefaultValueOperation || value.ConstantValue.HasValue &&
            value.ConstantValue.Value is null or string or bool or char or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;

    private static string MethodKey(IMethodSymbol method, Func<ITypeSymbol, string> typeName)
    {
        if (method.ReducedFrom is { } reduced)
            method = method.IsGenericMethod ? reduced.ConstructedFrom.Construct(method.TypeArguments, method.TypeArgumentNullableAnnotations) : reduced;
        return typeName(method.ContainingType) + "." + method.MetadataName + "<" +
            string.Join(",", method.TypeArguments.Select(typeName)) + ">(" +
            string.Join(",", method.Parameters.Select(parameter => parameter.RefKind + ":" + typeName(parameter.Type))) + ")|" +
            method.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat
                .WithMemberOptions(SymbolDisplayMemberOptions.IncludeContainingType | SymbolDisplayMemberOptions.IncludeParameters)
                .WithParameterOptions(SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeParamsRefOut));
    }

    private static string Constant(IArgumentOperation argument, Func<ITypeSymbol, string> typeName)
    {
        var value = argument.Value;
        while (value is IConversionOperation conversion) value = conversion.Operand;
        var type = argument.Parameter!.Type;
        if (!HasConstant(value) || !value.ConstantValue.HasValue || value.ConstantValue.Value is null)
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
            float number => number.ToString("R", CultureInfo.InvariantCulture) + "F",
            double number => number.ToString("R", CultureInfo.InvariantCulture) + "D",
            decimal number => number.ToString(CultureInfo.InvariantCulture) + "M",
            long number => number.ToString(CultureInfo.InvariantCulture) + "L",
            ulong number => number.ToString(CultureInfo.InvariantCulture) + "UL",
            uint number => number.ToString(CultureInfo.InvariantCulture) + "U",
            _ => SymbolDisplay.FormatPrimitive(constant, quoteStrings: true, useHexadecimalNumbers: false)
        };
        var literalType = constant switch
        {
            string => SpecialType.System_String, int => SpecialType.System_Int32,
            bool => SpecialType.System_Boolean, char => SpecialType.System_Char,
            float => SpecialType.System_Single, double => SpecialType.System_Double,
            decimal => SpecialType.System_Decimal, long => SpecialType.System_Int64,
            ulong => SpecialType.System_UInt64, uint => SpecialType.System_UInt32,
            _ => SpecialType.None
        };
        return literalType == type.SpecialType && literalType != SpecialType.None ? text : "(" + typeName(type) + ")" + text;
    }

    private static string Escape(string name) => SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ? "@" + name : name;
}
