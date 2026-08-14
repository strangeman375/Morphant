// Compiled integration scenario: TypeMapperCSharpSemanticsTests::Preserves_caller_information_in_all_structured_surfaces
#nullable enable
#pragma warning disable CS1591

using System;
using System.Runtime.CompilerServices;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.Latest.Scenarios.CallerInfo_a11ce005;

public sealed class Source
{
    public int Value { get; init; }
}

public sealed class CallerInfo
{
    public CallerInfo(
        int value,
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0,
        [CallerArgumentExpression("value")] string expression = "")
    {
        Value = value;
        Member = member;
        File = file;
        Line = line;
        Expression = expression;
    }

    public int Value { get; }

    public string Member { get; }

    public string File { get; }

    public int Line { get; }

    public string Expression { get; }
}

public sealed class ConstructDestination(CallerInfo info)
{
    public CallerInfo Info { get; } = info;
}

public sealed class ResolveDestination(CallerInfo info)
{
    public CallerInfo Info { get; } = info;
}

public sealed class MembersDestination
{
    public CallerInfo Info { get; set; } =
        new CallerInfo(-1, "", "", -1, "");
}

[MorphantMapper]
public partial class TestMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder)
    {
#line 4100 "Construct.dsl.cs"
        builder.Map<Source, ConstructDestination>().Construct(input => new(Capture(input.Value)));
#line 4200 "Resolve.dsl.cs"
        builder.Map<Source, ResolveDestination>().Resolve((input, _) => new(new CallerInfo(input.Value)));
#line 4300 "Members.dsl.cs"
        builder.Map<Source, MembersDestination>().Members(input => new() { Info = Capture(input.Value) });
#line default
    }

    private static CallerInfo Capture(
        int value,
        [CallerMemberName] string member = "",
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0,
        [CallerArgumentExpression("value")] string expression = "") =>
        new(value, member, file, line, expression);
}

public static class Scenario
{
    public static void Verify()
    {
        var mapper = new TestMapper();
        var source = new Source { Value = 7 };

        var construct =
            ((ITypeMapper<Source, ConstructDestination>)mapper).Create(
                source,
                default(MappingContext));
        var resolve =
            ((ITypeMapper<Source, ResolveDestination>)mapper).Create(
                source,
                default(MappingContext));
        var members =
            ((ITypeMapper<Source, MembersDestination>)mapper).Create(
                source,
                default(MappingContext));

        AssertInfo(
            construct.Info,
            "Construct.dsl.cs",
            4100);
        AssertInfo(
            resolve.Info,
            "Resolve.dsl.cs",
            4200);
        AssertInfo(
            members.Info,
            "Members.dsl.cs",
            4300);
    }

    private static void AssertInfo(
        CallerInfo info,
        string file,
        int line)
    {
        if (info.Value != 7 ||
            info.Member != "Configure" ||
            !info.File.EndsWith(file, StringComparison.Ordinal) ||
            info.Line != line ||
            info.Expression != "input.Value")
        {
            throw new InvalidOperationException(
                "Caller information changed after expression transfer: " +
                info.Value + "|" + info.Member + "|" + info.File + "|" +
                info.Line + "|" + info.Expression);
        }
    }
}
