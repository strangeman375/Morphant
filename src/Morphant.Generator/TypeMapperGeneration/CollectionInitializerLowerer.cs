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
    private const string Declaration = "Morphant.CollectionVariableDeclaration";
    private const string IndexArguments = "Morphant.CollectionIndexArguments";
    private readonly SemanticModel _semantic;
    private readonly HashSet<InitializerExpressionSyntax> _rejected;
    private readonly CancellationToken _cancellationToken;
    private readonly Stack<Scope> _scopes = new();
    private readonly List<(Scope Scope, string Name)> _names = new();
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
                NeedsLowering(creation) && _semantic.GetDeclaredSymbol(local.Declaration.Variables[0], _cancellationToken) is ILocalSymbol variableSymbol &&
                SymbolEqualityComparer.Default.Equals(variableSymbol.Type, _semantic.GetTypeInfo(creation, _cancellationToken).Type))
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
                statements.AddRange(Populate(receiver, remaining, scope, Indentation(statement)));
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

    public override SyntaxNode? VisitDeclarationExpression(DeclarationExpressionSyntax node) =>
        base.VisitDeclarationExpression(node)!.WithAdditionalAnnotations(new SyntaxAnnotation(Declaration,
            node.SpanStart.ToString(CultureInfo.InvariantCulture)));

    public override SyntaxNode? VisitImplicitElementAccess(ImplicitElementAccessSyntax node)
    {
        var property = _semantic.GetSymbolInfo(node, _cancellationToken).Symbol as IPropertySymbol;
        var data = node.ArgumentList.Arguments.SelectMany((argument, index) => new[]
        {
            TypeMapperMappingTypePolicy.GetGeneratedTypeName(_semantic.GetTypeInfo(argument.Expression, _cancellationToken).ConvertedType!),
            argument.NameColon?.Name.Identifier.ValueText ?? (property is not null && index < property.Parameters.Length ? property.Parameters[index].Name : "index")
        });
        return base.VisitImplicitElementAccess(node)!.WithAdditionalAnnotations(new SyntaxAnnotation(IndexArguments, string.Join("\0", data)));
    }

    private ExpressionSyntax RewriteCreation(BaseObjectCreationExpressionSyntax original, BaseObjectCreationExpressionSyntax rewritten)
    {
        if (original == _directCreation) return rewritten.WithAdditionalAnnotations(new SyntaxAnnotation(DirectCreation));
        if (!NeedsLowering(original)) return rewritten;
        var scope = _scopes.Peek();
        var type = _semantic.GetTypeInfo(original, _cancellationToken).Type!;
        var typeName = TypeMapperMappingTypePolicy.GetGeneratedTypeName(type);
        var indentation = Indentation(scope.Owner) + "    ";
        var overflow = original.Ancestors().TakeWhile(node => node is not (AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax or MethodDeclarationSyntax))
            .FirstOrDefault(node => node is CheckedExpressionSyntax or CheckedStatementSyntax);
        var isAsync = original.DescendantNodes(node => node is not (AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax))
            .OfType<AwaitExpressionSyntax>().Any();
        if (isAsync && original.Initializer!.IsKind(SyntaxKind.CollectionInitializerExpression))
            return RewriteAwaitedCollection(original, rewritten, scope, typeName, indentation);
        var functionName = Allocate(scope, "Create" + type.Name);
        var receiverName = ReceiverName(original, type.Name);
        var innerIndentation = indentation + (overflow is null ? "    " : "        ");
        var receiver = SyntaxFactory.IdentifierName(receiverName);
        var (allocation, remaining) = Split(original, rewritten);
        allocation = ExplicitAllocation(allocation, typeName);
        var statements = new List<StatementSyntax>
        {
            Line(SyntaxFactory.LocalDeclarationStatement(SyntaxFactory.VariableDeclaration(SyntaxFactory.IdentifierName("var"))
                .AddVariables(SyntaxFactory.VariableDeclarator(receiverName).WithInitializer(SyntaxFactory.EqualsValueClause(allocation)))), innerIndentation)
        };
        statements.AddRange(Populate(receiver, remaining, new Scope(original, isolated: true), innerIndentation));
        statements.Add(Line(SyntaxFactory.ReturnStatement(receiver), innerIndentation));
        var body = Block(statements, indentation);
        if (overflow is not null)
        {
            var isChecked = overflow.IsKind(SyntaxKind.CheckedExpression) || overflow.IsKind(SyntaxKind.CheckedStatement);
            var check = SyntaxFactory.CheckedStatement(isChecked ? SyntaxKind.CheckedStatement : SyntaxKind.UncheckedStatement,
                Block(statements, indentation + "    "))
                .WithKeyword(SyntaxFactory.Token(isChecked ? SyntaxKind.CheckedKeyword : SyntaxKind.UncheckedKeyword)
                    .WithLeadingTrivia(SyntaxFactory.Whitespace(indentation + "    ")).WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed));
            body = Block(new[] { check }, indentation);
        }
        var (parameters, arguments, declarations) = ScopedVariables(original, scope);
        body = body.ReplaceNodes(body.GetAnnotatedNodes(Declaration).OfType<DeclarationExpressionSyntax>()
            .Where(declaration => declarations.Contains(declaration.GetAnnotations(Declaration).Single().Data!)),
            (declaration, _) => SyntaxFactory.IdentifierName(((SingleVariableDesignationSyntax)declaration.Designation).Identifier).WithTriviaFrom(declaration));
        // A new async boundary would isolate AsyncLocal mutations and add a
        // continuation-context capture. Complex object initializers that still
        // require moving await are rejected by the ordinary transfer validator.
        var helper = SyntaxFactory.LocalFunctionStatement(SyntaxFactory.ParseTypeName(typeName), functionName)
            .WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(parameters)).NormalizeWhitespace()).WithBody(body);
        // Format the signature alone. The body contains user expressions with
        // their original multiline layout and literal token contents.
        helper = helper.WithReturnType(helper.ReturnType.WithTrailingTrivia(SyntaxFactory.Space))
            .WithParameterList(helper.ParameterList.WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed))
            .WithLeadingTrivia(SyntaxFactory.CarriageReturnLineFeed, SyntaxFactory.Whitespace(indentation));
        scope.Helpers.Add(helper);
        ExpressionSyntax invocation = SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName(functionName),
            SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(arguments)).NormalizeWhitespace());
        return CreationTrivia(invocation, original);
    }

    private ExpressionSyntax RewriteAwaitedCollection(BaseObjectCreationExpressionSyntax original,
        BaseObjectCreationExpressionSyntax rewritten, Scope scope, string typeName, string indentation)
    {
        ExpressionSyntax result = ExplicitAllocation(WithoutInitializer(rewritten), typeName);
        var initializer = rewritten.Initializer!;
        var metadata = CollectionCallerInformation.Metadata(initializer);
        for (var index = 0; index < initializer.Expressions.Count; index++)
        {
            var types = metadata[index * 3 + 2].Substring(1).Split('\u001f');
            var template = metadata[index * 3 + 1];
            var key = typeName + "\0" + template + "\0" + metadata[index * 3 + 2].Substring(1);
            if (!scope.ElementHelpers.TryGetValue(key, out var name))
            {
                name = Allocate(scope, "Add" + _semantic.GetTypeInfo(original, _cancellationToken).Type!.Name + "Item");
                scope.ElementHelpers.Add(key, name);
                var parameters = new List<ParameterSyntax> { SyntaxFactory.Parameter(SyntaxFactory.Identifier("collection")).WithType(SyntaxFactory.ParseTypeName(typeName)) };
                var values = new List<ExpressionSyntax>();
                for (var ordinal = 0; ordinal < types.Length; ordinal++)
                {
                    var value = types.Length == 1 ? "value" : "value" + (ordinal + 1).ToString(CultureInfo.InvariantCulture);
                    parameters.Add(SyntaxFactory.Parameter(SyntaxFactory.Identifier(value)).WithType(SyntaxFactory.ParseTypeName(types[ordinal])));
                    values.Add(SyntaxFactory.IdentifierName(value));
                }
                var receiver = SyntaxFactory.IdentifierName("collection");
                var body = Block(new StatementSyntax[]
                {
                    Line(SyntaxFactory.ExpressionStatement(AddCall(template, receiver, values)), indentation + "    "),
                    Line(SyntaxFactory.ReturnStatement(receiver), indentation + "    ")
                }, indentation);
                var helper = SyntaxFactory.LocalFunctionStatement(SyntaxFactory.ParseTypeName(typeName).WithTrailingTrivia(SyntaxFactory.Space), name)
                    .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.StaticKeyword).WithTrailingTrivia(SyntaxFactory.Space)))
                    .WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(parameters)).NormalizeWhitespace()
                        .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed)).WithBody(body)
                    .WithLeadingTrivia(SyntaxFactory.CarriageReturnLineFeed, SyntaxFactory.Whitespace(indentation));
                scope.Helpers.Add(helper);
            }
            var arguments = new[] { SyntaxFactory.Argument(result.WithoutTrivia()) }.Concat(
                CollectionCallerInformation.Arguments(initializer.Expressions[index]).Take(types.Length).Select(SyntaxFactory.Argument));
            result = SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName(name), SyntaxFactory.ArgumentList(
                SyntaxFactory.SeparatedList(arguments, Enumerable.Repeat(SyntaxFactory.Token(SyntaxKind.CommaToken)
                    .WithTrailingTrivia(SyntaxFactory.Space), types.Length))));
        }
        return CreationTrivia(result, original);
    }

    private static ExpressionSyntax CreationTrivia(ExpressionSyntax expression, BaseObjectCreationExpressionSyntax original)
    {
        expression = expression.WithTriviaFrom(original);
        return original.GetLastToken().GetNextToken().IsKind(SyntaxKind.CloseParenToken) &&
            original.GetTrailingTrivia().All(trivia => trivia.IsKind(SyntaxKind.WhitespaceTrivia))
                ? expression.WithoutTrailingTrivia() : expression;
    }

    private static ExpressionSyntax AddCall(string template, ExpressionSyntax receiver, IReadOnlyList<ExpressionSyntax> arguments)
    {
        var call = SyntaxFactory.ParseExpression(template);
        var substitutions = call.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()
            .Where(name => name.Identifier.ValueText == CollectionCallerInformation.Receiver ||
                name.Identifier.ValueText.StartsWith(CollectionCallerInformation.ArgumentPrefix, StringComparison.Ordinal)).ToArray();
        return call.ReplaceNodes(substitutions, (name, _) => name.Identifier.ValueText == CollectionCallerInformation.Receiver
            ? receiver : arguments[int.Parse(name.Identifier.ValueText.Substring(CollectionCallerInformation.ArgumentPrefix.Length), CultureInfo.InvariantCulture)]);
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

    private (List<ParameterSyntax> Parameters, List<ArgumentSyntax> Arguments, HashSet<string> Declarations) ScopedVariables(SyntaxNode creation, Scope scope)
    {
        var parameters = new List<ParameterSyntax>();
        var arguments = new List<ArgumentSyntax>();
        var seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var flow = _semantic.AnalyzeDataFlow(creation);
        foreach (var name in creation.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            var symbol = _semantic.GetSymbolInfo(name, _cancellationToken).Symbol;
            if (symbol is not (ILocalSymbol or IParameterSymbol or IRangeVariableSymbol) ||
                !seen.Add(symbol) || symbol.DeclaringSyntaxReferences.Any(reference => creation.Span.Contains(reference.Span))) continue;
            var type = symbol switch
            {
                IParameterSymbol parameterSymbol => parameterSymbol.Type,
                ILocalSymbol local => local.Type,
                _ => _semantic.GetTypeInfo(name, _cancellationToken).Type!
            };
            if (!type.IsRefLikeType && symbol is not IParameterSymbol { RefKind: not RefKind.None } && symbol is not ILocalSymbol { RefKind: not RefKind.None } &&
                _semantic.LookupSymbols(ScopePosition(scope.Owner), name: symbol.Name).Any(visible => SymbolEqualityComparer.Default.Equals(visible, symbol))) continue;
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
        var declarations = new HashSet<string>(StringComparer.Ordinal);
        foreach (var declaration in creation.DescendantNodes().OfType<DeclarationExpressionSyntax>())
        {
            if (declaration.Designation is not SingleVariableDesignationSyntax designation ||
                _semantic.GetDeclaredSymbol(designation, _cancellationToken) is not ILocalSymbol local ||
                !scope.Owner.DescendantNodes().OfType<IdentifierNameSyntax>().Any(name =>
                    !creation.Span.Contains(name.Span) && SymbolEqualityComparer.Default.Equals(local,
                        _semantic.GetSymbolInfo(name, _cancellationToken).Symbol))) continue;
            declarations.Add(declaration.SpanStart.ToString(CultureInfo.InvariantCulture));
            parameters.Add(SyntaxFactory.Parameter(designation.Identifier).WithType(SyntaxFactory.ParseTypeName(
                    TypeMapperMappingTypePolicy.GetGeneratedTypeName(local.Type)))
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.OutKeyword))));
            arguments.Add(SyntaxFactory.Argument(SyntaxFactory.DeclarationExpression(SyntaxFactory.IdentifierName("var"), designation))
                .WithRefKindKeyword(SyntaxFactory.Token(SyntaxKind.OutKeyword)));
        }
        return (parameters, arguments, declarations);
    }

    private IEnumerable<StatementSyntax> Populate(ExpressionSyntax receiver, InitializerExpressionSyntax initializer, Scope scope, string indentation)
    {
        if (initializer.IsKind(SyntaxKind.CollectionInitializerExpression))
        {
            var metadata = CollectionCallerInformation.Metadata(initializer);
            for (var index = 0; index < initializer.Expressions.Count; index++)
            {
                var element = initializer.Expressions[index];
                var arguments = CollectionCallerInformation.Arguments(element);
                var call = AddCall(metadata[index * 3 + 1], receiver, arguments.ToArray());
                var leading = element.GetLeadingTrivia();
                if (index == 0) leading = initializer.OpenBraceToken.TrailingTrivia.AddRange(leading);
                var trailing = element.GetTrailingTrivia();
                if (index < initializer.Expressions.SeparatorCount) trailing = trailing.AddRange(initializer.Expressions.GetSeparator(index).TrailingTrivia);
                if (index == initializer.Expressions.Count - 1) trailing = trailing.AddRange(initializer.CloseBraceToken.LeadingTrivia);
                yield return Line(SyntaxFactory.ExpressionStatement(call).WithLeadingTrivia(leading).WithTrailingTrivia(trailing), indentation);
            }
            yield break;
        }

        foreach (var assignment in initializer.Expressions.Cast<AssignmentExpressionSyntax>())
        {
            ExpressionSyntax member;
            if (assignment.Left is ImplicitElementAccessSyntax index)
            {
                var arguments = index.ArgumentList.Arguments;
                if (assignment.Right is InitializerExpressionSyntax indexed && PopulationCount(indexed) > 1)
                {
                    var data = index.GetAnnotations(IndexArguments).Single().Data!.Split('\0');
                    for (var ordinal = 0; ordinal < arguments.Count; ordinal++)
                    {
                        var argument = arguments[ordinal];
                        var name = Allocate(scope, data[ordinal * 2 + 1]);
                        yield return Line(SyntaxFactory.LocalDeclarationStatement(SyntaxFactory.VariableDeclaration(SyntaxFactory.ParseTypeName(data[ordinal * 2]))
                            .AddVariables(SyntaxFactory.VariableDeclarator(name).WithInitializer(SyntaxFactory.EqualsValueClause(argument.Expression)))), indentation);
                        arguments = arguments.Replace(argument, argument.WithExpression(SyntaxFactory.IdentifierName(name)));
                    }
                }
                member = SyntaxFactory.ElementAccessExpression(receiver, index.ArgumentList.WithArguments(arguments));
            }
            else member = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, receiver, (SimpleNameSyntax)assignment.Left);
            if (assignment.Right is InitializerExpressionSyntax nested)
            {
                foreach (var statement in Populate(member, nested, scope, indentation)) yield return statement;
            }
            else yield return Line(SyntaxFactory.ExpressionStatement(assignment.WithLeft(member)), indentation);
        }
    }

    private static int PopulationCount(InitializerExpressionSyntax initializer) => initializer.IsKind(SyntaxKind.CollectionInitializerExpression)
        ? initializer.Expressions.Count : initializer.Expressions.Cast<AssignmentExpressionSyntax>()
            .Sum(assignment => assignment.Right is InitializerExpressionSyntax nested ? PopulationCount(nested) : 1);

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
        var function = FunctionScope(scope.Owner);
        if (!scope.Isolated)
            used.UnionWith(_semantic.LookupSymbols(ScopePosition(scope.Owner)).Where(symbol => symbol.DeclaringSyntaxReferences
                .Any(reference => FunctionScope(reference.GetSyntax(_cancellationToken)) == function)).Select(symbol => symbol.Name));
        used.UnionWith(_names.Where(item => ReferenceEquals(item.Scope, scope) || !scope.Isolated && !item.Scope.Isolated &&
            FunctionScope(item.Scope.Owner) == function && (item.Scope.Owner.FullSpan.Contains(scope.Owner.FullSpan) ||
                scope.Owner.FullSpan.Contains(item.Scope.Owner.FullSpan))).Select(item => item.Name));
        var name = UserResultMappingPlanner.AllocateName(preferred, used);
        _names.Add((scope, name));
        return Escape(name);
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
        foreach (var addition in Populate(receiver, remaining, scope, indentation)) yield return addition;
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
        return Escape(UserResultMappingPlanner.AllocateName(char.ToLowerInvariant(typeName[0]) + typeName.Substring(1), used));
    }

    private static string Escape(string name) => SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None || name == "await" ? "@" + name : name;

    private static SyntaxNode? FunctionScope(SyntaxNode node) => node.AncestorsAndSelf()
        .FirstOrDefault(ancestor => ancestor is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax);

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
        var formatted = UserExpressionLayout.Restore(original, original.NormalizeWhitespace(indentation: "    ", eol: "\r\n"))
            .WithLeadingTrivia(SyntaxFactory.Whitespace(indentation)).WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);
        if (HasContent(statement.GetLeadingTrivia()))
            formatted = formatted.WithLeadingTrivia(LayoutTrivia(statement.GetLeadingTrivia(), indentation, true));
        if (HasContent(statement.GetTrailingTrivia()))
            formatted = formatted.WithTrailingTrivia(LayoutTrivia(statement.GetTrailingTrivia(), indentation, false));
        return formatted;
    }

    private static SyntaxTriviaList LayoutTrivia(SyntaxTriviaList trivia, string indentation, bool leading)
    {
        var lines = trivia.ToFullString().Trim().Replace("\r\n", "\n").Split('\n');
        var text = string.Join("\r\n", lines.Select((line, index) =>
            (index == 0 && !leading ? " " : indentation) + line.TrimStart())) + "\r\n";
        if (leading) text += indentation;
        return SyntaxFactory.ParseLeadingTrivia(text);
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
        public Scope(SyntaxNode owner, bool isolated = false) { Owner = owner; Isolated = isolated; }
        public SyntaxNode Owner { get; }
        public bool Isolated { get; }
        public List<LocalFunctionStatementSyntax> Helpers { get; } = new();
        public Dictionary<string, string> ElementHelpers { get; } = new(StringComparer.Ordinal);
    }
}
