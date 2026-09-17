using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.TypeMapperGeneration;

// An initializer cannot spell Add<T> or named arguments. When those are needed
// to retain its binding, keep the construction at its original evaluation site.
internal sealed class CollectionInitializerLowerer : CSharpSyntaxRewriter
{
    private const string DirectCreation = "Morphant.DirectCollectionCreation";
    private readonly SemanticModel _semantic;
    private readonly HashSet<InitializerExpressionSyntax> _rejected;
    private readonly CancellationToken _cancellationToken;
    private readonly Stack<Scope> _scopes = new();
    private readonly List<(SyntaxNode Scope, string Name)> _names = new();
    private SyntaxNode? _directCreation;

    public CollectionInitializerLowerer(SemanticModel semantic,
        HashSet<InitializerExpressionSyntax> rejected, CancellationToken cancellationToken)
    {
        _semantic = semantic;
        _rejected = rejected;
        _cancellationToken = cancellationToken;
    }

    public override SyntaxNode? VisitBlock(BlockSyntax node)
    {
        var scope = new Scope(node);
        _scopes.Push(scope);
        var statements = new List<StatementSyntax>();
        foreach (var statement in node.Statements)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (statement is LocalDeclarationStatementSyntax
                { UsingKeyword.RawKind: 0, Declaration.Variables.Count: 1 } local &&
                local.Declaration.Variables[0].Initializer?.Value is BaseObjectCreationExpressionSyntax creation &&
                NeedsLowering(creation))
            {
                var previous = _directCreation;
                _directCreation = creation;
                var rewritten = (LocalDeclarationStatementSyntax)Visit(local)!;
                _directCreation = previous;
                var variable = rewritten.Declaration.Variables[0];
                var rewrittenCreation = (BaseObjectCreationExpressionSyntax)variable.Initializer!.Value;
                var receiver = SyntaxFactory.IdentifierName(variable.Identifier);
                var (allocation, remaining) = Split(creation, rewrittenCreation);
                statements.Add(rewritten.WithDeclaration(rewritten.Declaration.WithVariables(
                    SyntaxFactory.SingletonSeparatedList(variable.WithInitializer(variable.Initializer.WithValue(allocation))))));
                statements.AddRange(Populate(receiver, remaining, Indentation(statement)));
            }
            else if (statement is ReturnStatementSyntax { Expression: { } result } && LeadingCreation(result) is { } first)
            {
                var previous = _directCreation;
                _directCreation = first;
                var rewritten = (ReturnStatementSyntax)Visit(statement)!;
                _directCreation = previous;
                statements.AddRange(ExpandResult(first, rewritten, scope, Indentation(statement)));
            }
            else if (statement is LocalDeclarationStatementSyntax
                { UsingKeyword.RawKind: 0, Declaration.Variables.Count: 1 } declaration &&
                declaration.Declaration.Variables[0].Initializer?.Value is { } value && LeadingCreation(value) is { } leading)
            {
                var previous = _directCreation;
                _directCreation = leading;
                var rewritten = (LocalDeclarationStatementSyntax)Visit(statement)!;
                _directCreation = previous;
                statements.AddRange(ExpandResult(leading, rewritten, scope, Indentation(statement)));
            }
            else statements.Add((StatementSyntax)Visit(statement)!);
        }
        statements.AddRange(scope.Helpers);
        _scopes.Pop();
        return node.WithStatements(SyntaxFactory.List(statements));
    }

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        if (node.ExpressionBody is null) return base.VisitMethodDeclaration(node);
        var scope = new Scope(node);
        _scopes.Push(scope);
        var previous = _directCreation;
        _directCreation = LeadingCreation(node.ExpressionBody.Expression);
        var rewritten = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node)!;
        _directCreation = previous;
        _scopes.Pop();
        return scope.Helpers.Count == 0 && !NeedsRootBody(node.ExpressionBody.Expression) ? rewritten : rewritten.WithExpressionBody(null)
            .WithParameterList(rewritten.ParameterList.WithTrailingTrivia(WithoutWhitespace(rewritten.ParameterList.GetTrailingTrivia())))
            .WithSemicolonToken(default).WithBody(FunctionBody(rewritten.ExpressionBody!.Expression,
                _semantic.GetDeclaredSymbol(node, _cancellationToken) is IMethodSymbol { ReturnsVoid: true }, scope, Indentation(node)));
    }

    public override SyntaxNode? VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
    {
        if (node.ExpressionBody is null) return base.VisitLocalFunctionStatement(node);
        var scope = new Scope(node);
        _scopes.Push(scope);
        var previous = _directCreation;
        _directCreation = LeadingCreation(node.ExpressionBody.Expression);
        var rewritten = (LocalFunctionStatementSyntax)base.VisitLocalFunctionStatement(node)!;
        _directCreation = previous;
        _scopes.Pop();
        return scope.Helpers.Count == 0 && !NeedsRootBody(node.ExpressionBody.Expression) ? rewritten : rewritten.WithExpressionBody(null)
            .WithParameterList(rewritten.ParameterList.WithTrailingTrivia(WithoutWhitespace(rewritten.ParameterList.GetTrailingTrivia())))
            .WithSemicolonToken(default).WithBody(FunctionBody(rewritten.ExpressionBody!.Expression,
                _semantic.GetDeclaredSymbol(node, _cancellationToken) is IMethodSymbol { ReturnsVoid: true }, scope, Indentation(node)));
    }

    public override SyntaxNode? VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node) => RewriteLambda(node);
    public override SyntaxNode? VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node) => RewriteLambda(node);

    private LambdaExpressionSyntax RewriteLambda(LambdaExpressionSyntax node)
    {
        if (node.Body is BlockSyntax) return node is SimpleLambdaExpressionSyntax simple
            ? (LambdaExpressionSyntax)base.VisitSimpleLambdaExpression(simple)!
            : (LambdaExpressionSyntax)base.VisitParenthesizedLambdaExpression((ParenthesizedLambdaExpressionSyntax)node)!;
        var scope = new Scope(node);
        _scopes.Push(scope);
        var previous = _directCreation;
        _directCreation = LeadingCreation((ExpressionSyntax)node.Body);
        var rewritten = node is SimpleLambdaExpressionSyntax single
            ? (LambdaExpressionSyntax)base.VisitSimpleLambdaExpression(single)!
            : (LambdaExpressionSyntax)base.VisitParenthesizedLambdaExpression((ParenthesizedLambdaExpressionSyntax)node)!;
        _directCreation = previous;
        _scopes.Pop();
        var returnsVoid = (_semantic.GetTypeInfo(node, _cancellationToken).ConvertedType as INamedTypeSymbol)?.DelegateInvokeMethod?.ReturnsVoid == true;
        return scope.Helpers.Count == 0 && !NeedsRootBody((ExpressionSyntax)node.Body) ? rewritten : rewritten
            .WithArrowToken(rewritten.ArrowToken.WithTrailingTrivia(WithoutWhitespace(rewritten.ArrowToken.TrailingTrivia)))
            .WithBody(FunctionBody((ExpressionSyntax)rewritten.Body, returnsVoid, scope, Indentation(node)).WithoutTrailingTrivia());
    }

    public override SyntaxNode? VisitObjectCreationExpression(ObjectCreationExpressionSyntax node) =>
        RewriteCreation(node, (BaseObjectCreationExpressionSyntax)base.VisitObjectCreationExpression(node)!);

    public override SyntaxNode? VisitImplicitObjectCreationExpression(ImplicitObjectCreationExpressionSyntax node) =>
        RewriteCreation(node, (BaseObjectCreationExpressionSyntax)base.VisitImplicitObjectCreationExpression(node)!);

    private ExpressionSyntax RewriteCreation(BaseObjectCreationExpressionSyntax original, BaseObjectCreationExpressionSyntax rewritten)
    {
        if (original == _directCreation) return rewritten.WithAdditionalAnnotations(new SyntaxAnnotation(DirectCreation));
        if (!NeedsLowering(original)) return rewritten;
        var scope = _scopes.Peek();
        var type = _semantic.GetTypeInfo(original, _cancellationToken).Type!;
        var typeName = TypeMapperMappingTypePolicy.GetGeneratedTypeName(type);
        var functionName = Allocate(scope, "Create" + type.Name);
        var receiverName = ReceiverName(original, type.Name);
        var indentation = Indentation(scope.Owner) + "    ";
        var innerIndentation = indentation + "    ";
        var receiver = SyntaxFactory.IdentifierName(receiverName);
        var (allocation, remaining) = Split(original, rewritten);
        allocation = ExplicitAllocation(allocation, typeName);
        var statements = new List<StatementSyntax>
        {
            Line(SyntaxFactory.LocalDeclarationStatement(SyntaxFactory.VariableDeclaration(SyntaxFactory.IdentifierName("var"))
                .AddVariables(SyntaxFactory.VariableDeclarator(receiverName).WithInitializer(SyntaxFactory.EqualsValueClause(allocation)))), innerIndentation)
        };
        statements.AddRange(Populate(receiver, remaining, innerIndentation));
        statements.Add(Line(SyntaxFactory.ReturnStatement(receiver), innerIndentation));
        var body = Block(statements, indentation);
        var (parameters, arguments) = ScopedVariables(original, scope);
        var helper = SyntaxFactory.LocalFunctionStatement(SyntaxFactory.ParseTypeName(typeName), functionName)
            .WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(parameters)).NormalizeWhitespace()).WithBody(body);
        // Format the signature alone. The body contains user expressions with
        // their original multiline layout and literal token contents.
        helper = helper.WithReturnType(helper.ReturnType.WithTrailingTrivia(SyntaxFactory.Space))
            .WithParameterList(helper.ParameterList.WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed))
            .WithLeadingTrivia(SyntaxFactory.CarriageReturnLineFeed, SyntaxFactory.Whitespace(indentation));
        scope.Helpers.Add(helper);
        return SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName(functionName),
            SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(arguments)).NormalizeWhitespace()).WithTriviaFrom(original);
    }

    private bool NeedsLowering(BaseObjectCreationExpressionSyntax creation) => creation.Initializer is { } initializer &&
        initializer.DescendantNodesAndSelf().OfType<InitializerExpressionSyntax>().Any(node =>
            _rejected.Contains(node) && node.Ancestors().OfType<BaseObjectCreationExpressionSyntax>().FirstOrDefault() == creation);

    private (BaseObjectCreationExpressionSyntax Allocation, InitializerExpressionSyntax Remaining) Split(
        BaseObjectCreationExpressionSyntax original, BaseObjectCreationExpressionSyntax rewritten)
    {
        var initializer = rewritten.Initializer!;
        var prefix = original.Initializer!.IsKind(SyntaxKind.ObjectInitializerExpression)
            ? original.Initializer.Expressions.TakeWhile(expression => !expression.DescendantNodesAndSelf()
                .OfType<InitializerExpressionSyntax>().Any(node => _rejected.Contains(node) &&
                    node.Ancestors().OfType<BaseObjectCreationExpressionSyntax>().FirstOrDefault() == original)).Count()
            : 0;
        if (prefix == 0) return (WithoutInitializer(rewritten), initializer);
        var retained = initializer.WithExpressions(SyntaxFactory.SeparatedList(initializer.Expressions.Take(prefix)));
        if (initializer.ToFullString().Contains('\n'))
            retained = retained.WithExpressions(retained.Expressions.Replace(retained.Expressions.Last(),
                retained.Expressions.Last().WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed)));
        BaseObjectCreationExpressionSyntax allocation = rewritten switch
        {
            ObjectCreationExpressionSyntax creation => creation.WithInitializer(retained),
            ImplicitObjectCreationExpressionSyntax creation => creation.WithInitializer(retained),
            _ => throw new InvalidOperationException()
        };
        return (allocation, initializer.WithExpressions(SyntaxFactory.SeparatedList(initializer.Expressions.Skip(prefix))));
    }

    private (List<ParameterSyntax> Parameters, List<ArgumentSyntax> Arguments) ScopedVariables(SyntaxNode creation, Scope scope)
    {
        var parameters = new List<ParameterSyntax>();
        var arguments = new List<ArgumentSyntax>();
        var seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var flow = _semantic.AnalyzeDataFlow(creation);
        foreach (var name in creation.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            var symbol = _semantic.GetSymbolInfo(name, _cancellationToken).Symbol;
            if (symbol is not (ILocalSymbol or IParameterSymbol or IRangeVariableSymbol) ||
                !seen.Add(symbol) || symbol.DeclaringSyntaxReferences.Any(reference => creation.Span.Contains(reference.Span)) ||
                (symbol is not IParameterSymbol { RefKind: not RefKind.None } && symbol is not ILocalSymbol { RefKind: not RefKind.None } &&
                 _semantic.LookupSymbols(ScopePosition(scope.Owner), name: symbol.Name).Any(visible => SymbolEqualityComparer.Default.Equals(visible, symbol)))) continue;
            var type = symbol switch
            {
                IParameterSymbol parameterSymbol => parameterSymbol.Type,
                ILocalSymbol local => local.Type,
                _ => _semantic.GetTypeInfo(name, _cancellationToken).Type!
            };
            var byRef = symbol is IParameterSymbol { RefKind: RefKind.Ref or RefKind.Out } or ILocalSymbol { RefKind: RefKind.Ref } ||
                flow?.WrittenInside.Contains(symbol, SymbolEqualityComparer.Default) == true;
            var parameter = SyntaxFactory.Parameter(name.Identifier).WithType(SyntaxFactory.ParseTypeName(
                TypeMapperMappingTypePolicy.GetGeneratedTypeName(type)));
            var argument = SyntaxFactory.Argument(name.WithoutTrivia());
            if (byRef)
            {
                parameter = parameter.WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.RefKeyword)));
                argument = argument.WithRefKindKeyword(SyntaxFactory.Token(SyntaxKind.RefKeyword));
            }
            parameters.Add(parameter);
            arguments.Add(argument);
        }
        return (parameters, arguments);
    }

    private IEnumerable<StatementSyntax> Populate(ExpressionSyntax receiver, InitializerExpressionSyntax initializer, string indentation)
    {
        if (initializer.IsKind(SyntaxKind.CollectionInitializerExpression))
        {
            var metadata = CollectionCallerInformation.Metadata(initializer);
            for (var index = 0; index < initializer.Expressions.Count; index++)
            {
                var element = initializer.Expressions[index];
                var arguments = CollectionCallerInformation.Arguments(element);
                var call = SyntaxFactory.ParseExpression(metadata[index * 2 + 1]);
                var substitutions = call.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()
                    .Where(name => name.Identifier.ValueText == CollectionCallerInformation.Receiver ||
                        name.Identifier.ValueText.StartsWith(CollectionCallerInformation.ArgumentPrefix, StringComparison.Ordinal)).ToArray();
                call = call.ReplaceNodes(substitutions, (name, _) => name.Identifier.ValueText == CollectionCallerInformation.Receiver
                    ? receiver : arguments[int.Parse(name.Identifier.ValueText.Substring(CollectionCallerInformation.ArgumentPrefix.Length), CultureInfo.InvariantCulture)]);
                yield return Line(SyntaxFactory.ExpressionStatement(call), indentation);
            }
            yield break;
        }

        foreach (var assignment in initializer.Expressions.Cast<AssignmentExpressionSyntax>())
        {
            ExpressionSyntax member = assignment.Left is ImplicitElementAccessSyntax index
                ? SyntaxFactory.ElementAccessExpression(receiver, index.ArgumentList)
                : SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, receiver, (SimpleNameSyntax)assignment.Left);
            if (assignment.Right is InitializerExpressionSyntax nested)
            {
                foreach (var statement in Populate(member, nested, indentation)) yield return statement;
            }
            else yield return Line(SyntaxFactory.ExpressionStatement(assignment.WithLeft(member)), indentation);
        }
    }

    private static BaseObjectCreationExpressionSyntax WithoutInitializer(BaseObjectCreationExpressionSyntax creation) => creation switch
    {
        ObjectCreationExpressionSyntax explicitCreation => explicitCreation.WithInitializer(null)
            .WithType(explicitCreation.Type.WithTrailingTrivia(WithoutWhitespace(explicitCreation.Type.GetTrailingTrivia())))
            .WithArgumentList((explicitCreation.ArgumentList ?? SyntaxFactory.ArgumentList()).WithTrailingTrivia(
                WithoutWhitespace(explicitCreation.ArgumentList?.GetTrailingTrivia() ?? default))),
        ImplicitObjectCreationExpressionSyntax implicitCreation => implicitCreation.WithInitializer(null)
            .WithArgumentList(implicitCreation.ArgumentList.WithTrailingTrivia(WithoutWhitespace(implicitCreation.ArgumentList.GetTrailingTrivia()))),
        _ => throw new InvalidOperationException()
    };

    private string Allocate(Scope scope, string preferred)
    {
        var used = new HashSet<string>(scope.Owner.DescendantTokens().Where(token => token.IsKind(SyntaxKind.IdentifierToken))
            .Select(token => token.ValueText), StringComparer.Ordinal);
        used.UnionWith(_semantic.LookupSymbols(ScopePosition(scope.Owner)).Select(symbol => symbol.Name));
        used.UnionWith(_names.Where(item => item.Scope.FullSpan.Contains(scope.Owner.FullSpan) ||
            scope.Owner.FullSpan.Contains(item.Scope.FullSpan)).Select(item => item.Name));
        var name = UserResultMappingPlanner.AllocateName(preferred, used);
        _names.Add((scope.Owner, name));
        return name;
    }

    private BaseObjectCreationExpressionSyntax? LeadingCreation(ExpressionSyntax expression) => expression switch
    {
        BaseObjectCreationExpressionSyntax creation when NeedsLowering(creation) => creation,
        MemberAccessExpressionSyntax member => LeadingCreation(member.Expression),
        ParenthesizedExpressionSyntax parenthesized => LeadingCreation(parenthesized.Expression),
        ObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: > 0 } creation => LeadingCreation(creation.ArgumentList.Arguments[0].Expression),
        ImplicitObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: > 0 } creation => LeadingCreation(creation.ArgumentList.Arguments[0].Expression),
        _ => null
    };

    private bool NeedsRootBody(ExpressionSyntax expression) => LeadingCreation(expression) is not null;

    private IEnumerable<StatementSyntax> ExpandResult(BaseObjectCreationExpressionSyntax original, StatementSyntax statement, Scope scope, string indentation)
    {
        var rewritten = (BaseObjectCreationExpressionSyntax)statement.GetAnnotatedNodes(DirectCreation).Single();
        var type = _semantic.GetTypeInfo(original, _cancellationToken).Type!;
        var name = Allocate(scope, char.ToLowerInvariant(type.Name[0]) + type.Name.Substring(1));
        var receiver = SyntaxFactory.IdentifierName(name);
        var (allocation, remaining) = Split(original, rewritten);
        var declaration = Line(SyntaxFactory.LocalDeclarationStatement(SyntaxFactory.VariableDeclaration(SyntaxFactory.IdentifierName("var"))
            .AddVariables(SyntaxFactory.VariableDeclarator(name).WithInitializer(SyntaxFactory.EqualsValueClause(
                ExplicitAllocation(allocation, TypeMapperMappingTypePolicy.GetGeneratedTypeName(type)))))), indentation);
        yield return HasContent(statement.GetLeadingTrivia()) ? declaration.WithLeadingTrivia(statement.GetLeadingTrivia()) : declaration;
        foreach (var addition in Populate(receiver, remaining, indentation)) yield return addition;
        yield return Line(statement.ReplaceNode(rewritten, receiver.WithTriviaFrom(rewritten)).WithoutLeadingTrivia(), indentation);
    }

    private BlockSyntax FunctionBody(ExpressionSyntax expression, bool returnsVoid, Scope scope, string indentation)
    {
        var original = scope.Owner switch
        {
            MethodDeclarationSyntax method => method.ExpressionBody!.Expression,
            LocalFunctionStatementSyntax function => function.ExpressionBody!.Expression,
            LambdaExpressionSyntax lambda => (ExpressionSyntax)lambda.Body,
            _ => expression
        };
        if (LeadingCreation(original) is { } creation)
        {
            StatementSyntax resultStatement = returnsVoid ? SyntaxFactory.ExpressionStatement(expression) : SyntaxFactory.ReturnStatement(expression);
            var statements = ExpandResult(creation, resultStatement, scope, indentation + "    ").ToList();
            statements.AddRange(scope.Helpers);
            return Block(statements, indentation).WithLeadingTrivia(SyntaxFactory.CarriageReturnLineFeed, SyntaxFactory.Whitespace(indentation));
        }
        StatementSyntax result = returnsVoid ? SyntaxFactory.ExpressionStatement(expression) : SyntaxFactory.ReturnStatement(expression);
        return Block(new[] { Line(result, indentation + "    ") }.Concat(scope.Helpers), indentation)
            .WithLeadingTrivia(SyntaxFactory.CarriageReturnLineFeed, SyntaxFactory.Whitespace(indentation));
    }

    private string ReceiverName(SyntaxNode original, string typeName)
    {
        var used = new HashSet<string>(original.DescendantTokens().Where(token => token.IsKind(SyntaxKind.IdentifierToken))
            .Select(token => token.ValueText), StringComparer.Ordinal);
        used.UnionWith(_semantic.LookupSymbols(original.SpanStart).Select(symbol => symbol.Name));
        return UserResultMappingPlanner.AllocateName(char.ToLowerInvariant(typeName[0]) + typeName.Substring(1), used);
    }

    private static BaseObjectCreationExpressionSyntax ExplicitAllocation(BaseObjectCreationExpressionSyntax creation, string typeName) =>
        creation is ImplicitObjectCreationExpressionSyntax implicitCreation
            ? SyntaxFactory.ObjectCreationExpression(SyntaxFactory.ParseTypeName(typeName)).WithArgumentList(implicitCreation.ArgumentList)
                .WithInitializer(implicitCreation.Initializer)
            : creation;

    private static SyntaxTriviaList WithoutWhitespace(SyntaxTriviaList trivia) =>
        trivia.All(item => item.IsKind(SyntaxKind.WhitespaceTrivia) || item.IsKind(SyntaxKind.EndOfLineTrivia)) ? default : trivia;

    private static bool HasContent(SyntaxTriviaList trivia) => WithoutWhitespace(trivia).Count != 0;

    private static int ScopePosition(SyntaxNode scope) => scope switch
    {
        BlockSyntax block => block.OpenBraceToken.Span.End,
        MethodDeclarationSyntax method => method.ExpressionBody!.Expression.SpanStart,
        LocalFunctionStatementSyntax function => function.ExpressionBody!.Expression.SpanStart,
        LambdaExpressionSyntax lambda => lambda.Body.SpanStart,
        _ => scope.SpanStart
    };

    private static BlockSyntax Block(IEnumerable<StatementSyntax> statements, string indentation) =>
        SyntaxFactory.Block(statements).WithOpenBraceToken(SyntaxFactory.Token(SyntaxKind.OpenBraceToken)
                .WithLeadingTrivia(SyntaxFactory.Whitespace(indentation)).WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed))
            .WithCloseBraceToken(SyntaxFactory.Token(SyntaxKind.CloseBraceToken)
                .WithLeadingTrivia(SyntaxFactory.Whitespace(indentation)).WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed));

    private static T Line<T>(T statement, string indentation) where T : StatementSyntax
    {
        var original = statement.ReplaceNodes(statement.DescendantNodes().OfType<ExpressionSyntax>(),
            (source, rewritten) => (ExpressionSyntax)UserExpressionLayout.Preserve(source, rewritten));
        return UserExpressionLayout.Restore(original, original.NormalizeWhitespace(indentation: "    ", eol: "\r\n"))
            .WithLeadingTrivia(SyntaxFactory.Whitespace(indentation)).WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);
    }

    private static string Indentation(SyntaxNode node)
    {
        var text = node.SyntaxTree.GetText();
        var line = text.Lines.GetLineFromPosition(node.SpanStart);
        return new string(text.ToString(Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(line.Start, node.SpanStart))
            .TakeWhile(character => character is ' ' or '\t').ToArray());
    }

    private sealed class Scope
    {
        public Scope(SyntaxNode owner) => Owner = owner;
        public SyntaxNode Owner { get; }
        public List<LocalFunctionStatementSyntax> Helpers { get; } = new();
    }
}
