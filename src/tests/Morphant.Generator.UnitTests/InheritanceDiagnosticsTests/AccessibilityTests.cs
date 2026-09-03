namespace Morphant.Generator.UnitTests.InheritanceDiagnosticsTests;

[TestFixture]
internal sealed class AccessibilityTests
{
    [Test]
    public void Reports_each_effective_inaccessible_callback_family()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source1 { public string Value { get; init; } = ""; }
    public sealed class Source2 { public string Value { get; init; } = ""; }
    public sealed class Source3 { public string Value { get; init; } = ""; }
    public sealed class Source4 { public string Value { get; init; } = ""; }
    public sealed class Source5 { public string Value { get; init; } = ""; }
    public sealed class Source6 { public string Value { get; init; } = ""; }

    public sealed class Destination1
    {
        public Destination1(string value) => Value = value;
        public string Value { get; }
    }

    public sealed class Destination2
    {
        public Destination2(string value) => Value = value;
        public string Value { get; }
    }

    public sealed class Destination3
    {
        public Destination3(string value) => Value = value;
        public string Value { get; }
    }

    public sealed class Destination4
    {
        public Destination4(string value) => Value = value;
        public string Value { get; }
    }

    public sealed class Destination5
    {
        public string Value { get; set; } = "";
    }

    public sealed class Destination6
    {
        public Destination6(string value) => Value = value;
        public string Value { get; }
    }

    public abstract class BaseMapper<TMapper> : TypeMapper<TMapper>
        where TMapper : BaseMapper<TMapper>
    {
        private static string Secret(string value) => "secret:" + value;

        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source1, Destination1>()
                .Construct(source => new(Secret(source.Value)));
            builder.Map<Source2, Destination2>()
                .Resolve((source, _) => new(Secret(source.Value)));
            builder.Map<Source3, Destination3>()
                .ConstructUsing(source =>
                    new Destination3(Secret(source.Value)));
            builder.Map<Source4, Destination4>()
                .ResolveUsing((source, _) =>
                    new Destination4(Secret(source.Value)));
            builder.Map<Source5, Destination5>()
                .Members(source => new()
                {
                    Value = Secret(source.Value)
                });
            builder.Map<Source6, Destination6>()
                .Convert(source =>
                    new Destination6(Secret(source!.Value)));
        }
    }

    [MorphantMapper]
    public partial class DerivedMapper : BaseMapper<DerivedMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Source1, Destination1>()
                .IncludeBase<Source1, Destination1>();
            builder.Map<Source2, Destination2>()
                .IncludeBase<Source2, Destination2>();
            builder.Map<Source3, Destination3>()
                .IncludeBase<Source3, Destination3>();
            builder.Map<Source4, Destination4>()
                .IncludeBase<Source4, Destination4>();
            builder.Map<Source5, Destination5>()
                .IncludeBase<Source5, Destination5>();
            builder.Map<Source6, Destination6>()
                .IncludeBase<Source6, Destination6>();
        }
    }
}
""";

        var result = InheritanceDiagnosticsGeneratorTest.Run(source);
        var diagnostics = result.EffectiveDiagnostics;

        Assert.Multiple(() =>
        {
            Assert.That(
                diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(Enumerable.Repeat("MORPH0028", 6)));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    diagnostic.GetMessage().Split(' ')[2]),
                Is.EqualTo(new[]
                {
                    "Construct",
                    "Resolve",
                    "ConstructUsing",
                    "ResolveUsing",
                    "Members",
                    "Convert"
                }));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    InheritanceDiagnosticsGeneratorTest.SourceText(
                        diagnostic.Location)),
                Has.All.EqualTo("IncludeBase"));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    diagnostic.AdditionalLocations.Count),
                Has.All.EqualTo(2));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    InheritanceDiagnosticsGeneratorTest.SourceText(
                        diagnostic.AdditionalLocations[0])),
                Is.EqualTo(new[]
                {
                    "Construct",
                    "Resolve",
                    "ConstructUsing",
                    "ResolveUsing",
                    "Members",
                    "Convert"
                }));
            Assert.That(
                diagnostics.Select(static diagnostic =>
                    InheritanceDiagnosticsGeneratorTest.SourceText(
                        diagnostic.AdditionalLocations[1])),
                Has.All.EqualTo("Secret"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Checks_accessibility_only_after_local_model_precedence()
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
        public string Value { get; init; } = "";
    }

    public sealed class Destination
    {
        public Destination(string value) => Value = value;
        public string Value { get; set; }
    }

    public sealed class ManualSource
    {
    }

    public sealed class ManualDestination
    {
        public int Value { get; set; }
    }

    public sealed class ConvertedSource
    {
        public string Value { get; init; } = "";
    }

    public sealed class ConvertedDestination
    {
        public string Value { get; set; } = "";
    }

    public abstract class BaseMapper<TMapper> : TypeMapper<TMapper>
        where TMapper : BaseMapper<TMapper>
    {
        private static string Secret(string value) => "secret:" + value;
        protected static string Visible(string value) => "visible:" + value;

        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>()
                .Construct(source => new(Secret(source.Value)))
                .Members(source => new()
                {
                    Value = Secret(source.Value)
                });
            builder.Map<ManualSource, ManualDestination>()
                .Convert(_ => new ManualDestination
                {
                    Value = Secret("manual").Length
                });
            builder.Map<ConvertedSource, ConvertedDestination>()
                .Members(source => new()
                {
                    Value = Secret(source.Value)
                });
        }
    }

    [MorphantMapper]
    public partial class DerivedMapper : BaseMapper<DerivedMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Source, Destination>()
                .IncludeBase<Source, Destination>()
                .Construct(source => new(Visible(source.Value)))
                .Members(source => new()
                {
                    Value = Visible(source.Value)
                });
            builder.Map<ManualSource, ManualDestination>()
                .IncludeBase<ManualSource, ManualDestination>()
                .Members(_ => new());
            builder.Map<ConvertedSource, ConvertedDestination>()
                .IncludeBase<ConvertedSource, ConvertedDestination>()
                .Convert(_ => new ConvertedDestination());
        }
    }
}
""";

        var result = InheritanceDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Groups_all_inaccessible_references_of_one_callback()
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
        public string First { get; init; } = "";
        public string Second { get; init; } = "";
    }

    public sealed class Destination
    {
        public string First { get; set; } = "";
        public string Second { get; set; } = "";
    }

    public abstract class SupportMapper<TMapper> : TypeMapper<TMapper>
        where TMapper : SupportMapper<TMapper>
    {
        protected string Decorate(string value) => value;

        protected override void Configure(MapperBuilder builder)
        {
        }
    }

    public abstract class BaseMapper<TMapper> : SupportMapper<TMapper>
        where TMapper : BaseMapper<TMapper>
    {
        private static string Secret(string value) => value;

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members(source => new()
                {
                    First = Secret(source.First),
                    Second = base.Decorate(source.Second)
                });
    }

    [MorphantMapper]
    public partial class DerivedMapper : BaseMapper<DerivedMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Source, Destination>()
                .IncludeBase<Source, Destination>();
        }
    }
}
""";

        var result = InheritanceDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(result.EffectiveDiagnostics.Length, Is.EqualTo(1));
        });

        var diagnostic = result.EffectiveDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0028"));
            Assert.That(
                diagnostic.AdditionalLocations.Select(static location =>
                    InheritanceDiagnosticsGeneratorTest.SourceText(location)),
                Is.EqualTo(new[] { "Members", "Secret", "base" }));
        });
    }

    [Test]
    public void Accepts_public_internal_and_protected_inherited_helpers()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source1 { public string Value { get; init; } = ""; }
    public sealed class Source2 { public string Value { get; init; } = ""; }
    public sealed class Source3 { public string Value { get; init; } = ""; }
    public sealed class Destination1 { public string Value { get; set; } = ""; }
    public sealed class Destination2 { public string Value { get; set; } = ""; }
    public sealed class Destination3 { public string Value { get; set; } = ""; }

    public abstract class BaseMapper<TMapper> : TypeMapper<TMapper>
        where TMapper : BaseMapper<TMapper>
    {
        public static string Public(string value) => value;
        internal static string Internal(string value) => value;
        protected static string Protected(string value) => value;

        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source1, Destination1>()
                .Members(source => new()
                {
                    Value = Public(source.Value)
                });
            builder.Map<Source2, Destination2>()
                .Members(source => new()
                {
                    Value = Internal(source.Value)
                });
            builder.Map<Source3, Destination3>()
                .Members(source => new()
                {
                    Value = Protected(source.Value)
                });
        }
    }

    [MorphantMapper]
    public partial class DerivedMapper : BaseMapper<DerivedMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Source1, Destination1>()
                .IncludeBase<Source1, Destination1>();
            builder.Map<Source2, Destination2>()
                .IncludeBase<Source2, Destination2>();
            builder.Map<Source3, Destination3>()
                .IncludeBase<Source3, Destination3>();
        }
    }
}
""";

        var result = InheritanceDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Orders_multiple_callback_origins_from_nearest_to_farthest()
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
        public string First { get; init; } = "";
        public string Second { get; init; } = "";
    }

    public sealed class Destination
    {
        public string First { get; set; } = "";
        public string Second { get; set; } = "";
    }

    public abstract class FarMapper<TMapper> : TypeMapper<TMapper>
        where TMapper : FarMapper<TMapper>
    {
        private static string FarSecret(string value) => value;

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members(source => new()
                {
                    First = FarSecret(source.First)
                });
    }

    public abstract class NearMapper<TMapper> : FarMapper<TMapper>
        where TMapper : NearMapper<TMapper>
    {
        private static string NearSecret(string value) => value;

        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Source, Destination>()
                .IncludeBase<Source, Destination>()
                .Members(source => new()
                {
                    Second = NearSecret(source.Second)
                });
        }
    }

    [MorphantMapper]
    public partial class DerivedMapper : NearMapper<DerivedMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Source, Destination>()
                .IncludeBase<Source, Destination>();
        }
    }
}
""";

        var result = InheritanceDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.EffectiveDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0028", "MORPH0028" }));
            Assert.That(
                result.EffectiveDiagnostics.Select(static diagnostic =>
                    InheritanceDiagnosticsGeneratorTest.SourceText(
                        diagnostic.AdditionalLocations[1])),
                Is.EqualTo(new[] { "NearSecret", "FarSecret" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
