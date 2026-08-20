using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp;

namespace Morphant.Generator.TypeMapperGeneration;

internal sealed class ConventionSourceValueExpressionModel
    : IEquatable<ConventionSourceValueExpressionModel>
{
    public ConventionSourceValueExpressionModel(
        string receiverExpression,
        ImmutableArray<ConventionSourceValuePathSegmentModel> path,
        string memberName,
        string memberTypeName,
        bool requiresTypedMissingBranch)
    {
        ReceiverExpression = receiverExpression;
        Path = path;
        MemberName = memberName;
        MemberTypeName = memberTypeName;
        RequiresTypedMissingBranch = requiresTypedMissingBranch;
    }

    public string ReceiverExpression { get; }

    public ImmutableArray<ConventionSourceValuePathSegmentModel> Path
    {
        get;
    }

    public string MemberName { get; }

    public string MemberTypeName { get; }

    public bool RequiresTypedMissingBranch { get; }

    public string Render(GeneratedLocalNameAllocator localNames)
    {
        return RequiresTypedMissingBranch
            ? RenderGuardedExpression(localNames)
            : RenderConditionalAccessExpression();
    }

    public bool Equals(ConventionSourceValueExpressionModel? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null ||
            !StringComparer.Ordinal.Equals(
                ReceiverExpression,
                other.ReceiverExpression) ||
            !StringComparer.Ordinal.Equals(
                MemberName,
                other.MemberName) ||
            !StringComparer.Ordinal.Equals(
                MemberTypeName,
                other.MemberTypeName) ||
            RequiresTypedMissingBranch !=
                other.RequiresTypedMissingBranch ||
            Path.Length != other.Path.Length)
        {
            return false;
        }

        for (var index = 0; index < Path.Length; index++)
        {
            if (Path[index] != other.Path[index])
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) =>
        Equals(obj as ConventionSourceValueExpressionModel);

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = StringComparer.Ordinal.GetHashCode(
                ReceiverExpression);
            hashCode = hashCode * 397 ^
                       StringComparer.Ordinal.GetHashCode(MemberName);
            hashCode = hashCode * 397 ^
                       StringComparer.Ordinal.GetHashCode(MemberTypeName);
            hashCode = hashCode * 397 ^
                       RequiresTypedMissingBranch.GetHashCode();

            foreach (var segment in Path)
            {
                hashCode = hashCode * 397 ^ segment.GetHashCode();
            }

            return hashCode;
        }
    }

    private string RenderConditionalAccessExpression()
    {
        var expression = ReceiverExpression;
        var accessOperator = ".";

        foreach (var segment in Path)
        {
            expression += accessOperator + Identifier(segment.Name);
            accessOperator = segment.RequiresGuard ? "?." : ".";

            if (segment.SuppressesNull)
            {
                expression += "!";
            }

            if (segment.UnwrapsNullableValue)
            {
                expression += ".Value";
            }
        }

        return expression + accessOperator + Identifier(MemberName);
    }

    private string RenderGuardedExpression(
        GeneratedLocalNameAllocator localNames)
    {
        var receiver = ReceiverExpression;
        var conditions = new List<string>();

        foreach (var segment in Path)
        {
            var access = receiver + "." + Identifier(segment.Name);

            if (!segment.RequiresGuard)
            {
                receiver = segment.SuppressesNull
                    ? access + "!"
                    : access;

                if (segment.UnwrapsNullableValue)
                {
                    receiver += ".Value";
                }

                continue;
            }

            var local = localNames.AllocateForSourcePathSegment(
                segment.Name);
            conditions.Add(
                access + " is { } " + Identifier(local));
            receiver = Identifier(local);
        }

        var value = receiver + "." + Identifier(MemberName);

        return "(" + string.Join(" && ", conditions) +
               " ? " + value + " : default(" + MemberTypeName + "))";
    }

    private static string Identifier(string value) =>
        SyntaxFacts.GetKeywordKind(value) != SyntaxKind.None ||
        SyntaxFacts.GetContextualKeywordKind(value) != SyntaxKind.None
            ? "@" + value
            : value;
}

internal readonly record struct ConventionSourceValuePathSegmentModel(
    string Name,
    bool SuppressesNull,
    bool RequiresGuard,
    bool UnwrapsNullableValue);
