namespace Morphant.Generator.UnitTests.NestedMappingDiagnosticsTests;

[TestFixture]
internal sealed class ResultConversionTests
{
    [Test]
    public void Accepts_warning_free_standard_and_user_defined_conversions()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System.Collections.Generic;
using Morphant;

namespace TestCase
{
    public class BaseValue { }
    public sealed class DerivedValue : BaseValue, IValue { }
    public interface IValue { }

    public readonly struct UserSource { }
    public readonly struct UserTarget
    {
        public static implicit operator UserTarget(UserSource value) =>
            new();
    }

    public sealed class Source
    {
        public int Value { get; set; }
    }

    public sealed class Destination
    {
        public BaseValue? Reference { get; set; }
        public IValue? Interface { get; set; }
        public long Numeric { get; set; }
        public long? Lifted { get; set; }
        public object? Boxed { get; set; }
        public IEnumerable<object>? Variant { get; set; }
        public (long First, long Second) Tuple { get; set; }
        public UserTarget UserDefined { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>(MappingMode.Create)
                .MemberSelection(MemberSelection.Explicit)
                .Members(source => new()
                {
                    Reference = Map<DerivedValue>(source.Value),
                    Interface = Map<DerivedValue>(source.Value),
                    Numeric = Map<int>(source.Value),
                    Lifted = Map<int>(source.Value),
                    Boxed = Map<int>(source.Value),
                    Variant = Map<IEnumerable<string>>(source.Value),
                    Tuple = Map<(int, int)>(source.Value),
                    UserDefined = Map<UserSource>(source.Value)
                });
    }
}
""";

        var result = NestedMappingDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.NestedMappingDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Reports_each_non_implicit_or_warning_producing_conversion()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591
#pragma warning disable CS8619

using Morphant;

namespace TestCase
{
    public class BaseValue { }
    public sealed class DerivedValue : BaseValue { }

    public readonly struct ExplicitSource
    {
        public static explicit operator ExplicitTarget(ExplicitSource value) =>
            new();
    }

    public readonly struct ExplicitTarget { }

    public sealed class Source
    {
        public long Value { get; set; }
    }

    public sealed class NarrowingDestination
    {
        public int Value { get; set; }
    }

    public sealed class DowncastDestination
    {
        public DerivedValue? Value { get; set; }
    }

    public sealed class UnboxingDestination
    {
        public int Value { get; set; }
    }

    public sealed class NullableDestination
    {
        public string Value { get; set; } = string.Empty;
    }

    public sealed class ExplicitDestination
    {
        public ExplicitTarget Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, NarrowingDestination>()
                .Members(source => new()
                {
                    Value = Map<long>(source.Value)
                });
            builder.Map<Source, DowncastDestination>()
                .Members(source => new()
                {
                    Value = Map<BaseValue>(source.Value)
                });
            builder.Map<Source, UnboxingDestination>()
                .Members(source => new()
                {
                    Value = Map<object>(source.Value)
                });
            builder.Map<Source, NullableDestination>()
                .Members(source => new()
                {
                    Value = Map<string?>(source.Value)
                });
            builder.Map<Source, ExplicitDestination>()
                .Members(source => new()
                {
                    Value = Map<ExplicitSource>(source.Value)
                });
        }
    }
}
""";

        var result = NestedMappingDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.NestedMappingDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[]
                {
                    "MORPH0045",
                    "MORPH0045",
                    "MORPH0045",
                    "MORPH0045",
                    "MORPH0045"
                }));
            Assert.That(
                result.NestedMappingDiagnostics.Select(diagnostic =>
                    NestedMappingDiagnosticsGeneratorTest.SourceText(
                        diagnostic.Location)),
                Is.EqualTo(new[]
                {
                    "BaseValue",
                    "ExplicitSource",
                    "long",
                    "string?",
                    "object"
                }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Reports_each_incompatible_terminal_use_of_one_local()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
        public int Value { get; set; }
    }

    public sealed class Destination
    {
        public string First { get; set; } = string.Empty;
        public bool Second { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>(MappingMode.Create)
                .Members(source =>
                {
                    var value = Map<int>(source.Value);
                    return new()
                    {
                        First = value,
                        Second = value
                    };
                });
    }
}
""";

        var result = NestedMappingDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.NestedMappingDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0045", "MORPH0045" }));
            Assert.That(
                result.NestedMappingDiagnostics.Select(static diagnostic =>
                    diagnostic.GetMessage()),
                Has.Exactly(1).Contains("assigned to 'string'") &
                Has.Exactly(1).Contains("assigned to 'bool'"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
