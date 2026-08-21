using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.TypeMapperGeneration;

internal sealed class GeneratedLocalNameAllocator
{
    private readonly HashSet<string> _usedNames =
        new(StringComparer.Ordinal);

    public GeneratedLocalNameAllocator(
        INamedTypeSymbol mapperType,
        params string[] methodParameterNames)
    {
        for (var type = mapperType;
             type is not null;
             type = type.ContainingType)
        {
            foreach (var typeParameter in type.TypeParameters)
            {
                Reserve(typeParameter.Name);
            }
        }

        foreach (var parameterName in methodParameterNames)
        {
            Reserve(parameterName);
        }
    }

    public void Reserve(string? name)
    {
        if (name is not null)
        {
            _usedNames.Add(name);
        }
    }

    public void ReserveExpressionDeclarations(string? expression)
    {
        if (expression is null)
        {
            return;
        }

        ReserveDeclarations(SyntaxFactory.ParseExpression(expression));
    }

    public void ReserveSwitchLabelDeclarations(string label)
    {
        ReserveDeclarations(
            SyntaxFactory.ParseStatement(
                "switch (value) { " + label + " break; }"));
    }

    public string AllocateForSourcePathSegment(string segmentName)
    {
        var preferredName = BuildPreferredName(segmentName);
        return UserResultMappingPlanner.AllocateName(
            preferredName,
            _usedNames);
    }

    public string Allocate(string preferredName)
    {
        return UserResultMappingPlanner.AllocateName(
            preferredName,
            _usedNames);
    }

    private void ReserveDeclarations(SyntaxNode syntax)
    {
        foreach (var node in syntax.DescendantNodesAndSelf())
        {
            var name = node switch
            {
                VariableDeclaratorSyntax variable =>
                    variable.Identifier.ValueText,
                SingleVariableDesignationSyntax designation =>
                    designation.Identifier.ValueText,
                ParameterSyntax parameter =>
                    parameter.Identifier.ValueText,
                TypeParameterSyntax typeParameter =>
                    typeParameter.Identifier.ValueText,
                ForEachStatementSyntax forEach =>
                    forEach.Identifier.ValueText,
                CatchDeclarationSyntax catchDeclaration
                    when catchDeclaration.Identifier.RawKind != 0 =>
                    catchDeclaration.Identifier.ValueText,
                LocalFunctionStatementSyntax localFunction =>
                    localFunction.Identifier.ValueText,
                FromClauseSyntax from => from.Identifier.ValueText,
                LetClauseSyntax let => let.Identifier.ValueText,
                JoinClauseSyntax join => join.Identifier.ValueText,
                JoinIntoClauseSyntax joinInto =>
                    joinInto.Identifier.ValueText,
                QueryContinuationSyntax continuation =>
                    continuation.Identifier.ValueText,
                _ => null
            };

            Reserve(name);
        }
    }

    private static string BuildPreferredName(string segmentName)
    {
        var preferredName = char.ToLowerInvariant(segmentName[0]) +
                            segmentName.Substring(1);

        if (preferredName == "_" ||
            !(SyntaxFacts.IsValidIdentifier(preferredName) ||
              SyntaxFacts.GetKeywordKind(preferredName) !=
                  SyntaxKind.None ||
              SyntaxFacts.GetContextualKeywordKind(preferredName) !=
                  SyntaxKind.None))
        {
            return "value";
        }

        return preferredName;
    }
}
