using Microsoft.CodeAnalysis;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.CallbackDiagnosticsTests;

[TestFixture]
internal sealed class PrecedenceAndActualizationTests
{
    private const string InvalidSource =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;

namespace TestCase
{
    public sealed class Source { }

    public sealed class Destination
    {
        public Destination(int value) => Value = value;

        public int Value { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            var offset = Environment.TickCount;

            builder.Map<Source, Destination>()
                .ConstructUsing(source => new(offset));
        }
    }
}
""";

    [Test]
    public void Earlier_composition_failure_owns_the_pair()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;

namespace TestCase
{
    public sealed class Source { }

    public sealed class Destination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            var runtime = Environment.TickCount;

            builder.Map<Source, Destination>()
                .Members(source => new() { Value = runtime })
                .Members(source =>
                {
                    for (var index = 0; index < 1; index++) { }
                    return new();
                });
        }
    }
}
""";

        var result = CallbackDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.EffectiveDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0019" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Compiler_binding_error_is_not_duplicated_by_morphant()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source { }

    public sealed class Destination
    {
        public Destination(int value) { }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(source => new(MissingValue));
    }
}
""";

        var result = CallbackDiagnosticsGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.EffectiveDiagnostics.Where(static diagnostic =>
                    diagnostic.Id is
                        "MORPH0029" or
                        "MORPH0030" or
                        "MORPH0031" or
                        "MORPH0032" or
                        "MORPH0033"),
                Is.Empty);
            Assert.That(
                result.CompilerWarningsAndErrors.Select(static diagnostic =>
                    diagnostic.Id),
                Does.Contain("CS0103"));
        });
    }

    [Test]
    public void Suppression_and_severity_do_not_change_recovery_artifacts()
    {
        var visible = CallbackDiagnosticsGeneratorTest.Run(InvalidSource);
        var suppressed = CallbackDiagnosticsGeneratorTest.Run(
            InvalidSource,
            diagnosticOptions: new Dictionary<string, ReportDiagnostic>
            {
                ["MORPH0030"] = ReportDiagnostic.Suppress
            });
        var warning = CallbackDiagnosticsGeneratorTest.Run(
            InvalidSource,
            diagnosticOptions: new Dictionary<string, ReportDiagnostic>
            {
                ["MORPH0030"] = ReportDiagnostic.Warn
            });
        var visibleSources = Sources(visible);
        var suppressedSources = Sources(suppressed);
        var warningSources = Sources(warning);

        Assert.Multiple(() =>
        {
            Assert.That(
                visible.EffectiveDiagnostics.Single().Id,
                Is.EqualTo("MORPH0030"));
            Assert.That(suppressed.EffectiveDiagnostics, Is.Empty);
            Assert.That(
                warning.EffectiveDiagnostics.Single().Severity,
                Is.EqualTo(DiagnosticSeverity.Warning));
            Assert.That(suppressedSources, Is.EqualTo(visibleSources));
            Assert.That(warningSources, Is.EqualTo(visibleSources));
            Assert.That(
                visibleSources.Select(static source => source.HintName),
                Is.EqualTo(new[]
                {
                    "Morphant.Generated.Construction.Destination__17fbb67411e1e232593b768778eabb50.g.cs",
                    "Morphant.Generated.MappingExtension.TestMapper.SourceToDestination__045c8ddc6d3bd2389959dca1e52fe4d8.g.cs",
                    "Morphant.Generated.TypeMapper.TestMapper__3716cfd39450e4dd725a4e5fa8513843.g.cs"
                }));
            Assert.That(
                visibleSources.Single(static source =>
                        source.HintName.Contains(
                            ".TypeMapper.",
                            StringComparison.Ordinal))
                    .Source,
                Does.Contain(
                    "throw new global::Morphant.Exceptions." +
                    "MappingConfigurationException("));
            Assert.That(visible.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(suppressed.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(warning.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Actualizes_capture_and_recovery_on_one_driver()
    {
        // lang=c#
        const string validSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source { }

    public sealed class Destination
    {
        public Destination(int value) => Value = value;

        public int Value { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        private int Offset => 17;

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .ConstructUsing(source => new(Offset));
    }
}
""";

        // lang=c#
        const string expectedSource0 =
"""
// <auto-generated />
#nullable enable

namespace Morphant.Generated.N_17fbb67411e1e232593b768778eabb50
{
    /// <summary>
    /// Maps constructor arguments for <see cref="global::TestCase.Destination"/>.
    /// </summary>
    internal sealed class DestinationConstructorParameters
    {
        /// <summary>
        /// Maps the <c>value</c> argument.
        /// </summary>
        public global::Morphant.Members.ConstructorParameter<int> value = null!;
    }

    /// <summary>
    /// Defines construction of <see cref="global::TestCase.Destination"/>.
    /// </summary>
    internal sealed class DestinationConstruction
    {
        /// <summary>
        /// Uses convention-based construction.
        /// </summary>
        /// <param name="marker">The convention marker.</param>
        /// <param name="parameters">Optional argument mappings.</param>
        public DestinationConstruction(
            global::Morphant.Markers.ByConventionMarker marker,
            DestinationConstructorParameters? parameters = null)
        {
        }

        /// <summary>
        /// Uses the corresponding destination constructor.
        /// </summary>
        /// <param name="value">Maps the <c>value</c> argument.</param>
        public DestinationConstruction(global::Morphant.Members.ConstructorParameter<int> value)
        {
        }

        /// <summary>
        /// Uses the existing destination as the result.
        /// </summary>
        /// <param name="previous">The existing destination.</param>
        public static implicit operator DestinationConstruction(
            global::TestCase.Destination previous) =>
            throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();
    }
}
""";

        // lang=c#
        const string expectedSource1 =
"""
// <auto-generated />
#nullable enable

namespace Morphant
{
    internal static partial class MorphantGeneratedMappingExtensions
    {
        /// <summary>
        /// Supplies constructor arguments when no destination exists.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="construct">Inline lambda returning a construction expression.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/construct.md"/>
        public static global::Morphant.MappingBuilder<global::TestCase.TestMapper, global::TestCase.Source, global::TestCase.Destination> Construct(
            this global::Morphant.MappingBuilder<global::TestCase.TestMapper, global::TestCase.Source, global::TestCase.Destination> builder,
            global::Morphant.Delegates.Construct<global::TestCase.Source, global::Morphant.Generated.N_17fbb67411e1e232593b768778eabb50.DestinationConstruction> construct)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Supplies constructor arguments when no destination exists.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="construct">Inline lambda returning a construction expression.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/construct.md"/>
        public static global::Morphant.MappingBuilder<global::TestCase.TestMapper, global::TestCase.Source, global::TestCase.Destination> Construct(
            this global::Morphant.MappingBuilder<global::TestCase.TestMapper, global::TestCase.Source, global::TestCase.Destination> builder,
            global::Morphant.Delegates.Construct<global::TestCase.Source, global::Morphant.Context.MappingContextMarker, global::Morphant.Generated.N_17fbb67411e1e232593b768778eabb50.DestinationConstruction> construct)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Chooses reuse or construction on Create and Update.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="resolve">Inline lambda returning the existing destination value or a construction expression.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/resolve.md"/>
        public static global::Morphant.MappingBuilder<global::TestCase.TestMapper, global::TestCase.Source, global::TestCase.Destination> Resolve(
            this global::Morphant.MappingBuilder<global::TestCase.TestMapper, global::TestCase.Source, global::TestCase.Destination> builder,
            global::Morphant.Delegates.Resolve<global::TestCase.Source, global::TestCase.Destination, global::Morphant.Generated.N_17fbb67411e1e232593b768778eabb50.DestinationConstruction> resolve)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Chooses reuse or construction on Create and Update.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="resolve">Inline lambda returning the existing destination value or a construction expression.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/resolve.md"/>
        public static global::Morphant.MappingBuilder<global::TestCase.TestMapper, global::TestCase.Source, global::TestCase.Destination> Resolve(
            this global::Morphant.MappingBuilder<global::TestCase.TestMapper, global::TestCase.Source, global::TestCase.Destination> builder,
            global::Morphant.Delegates.Resolve<global::TestCase.Source, global::TestCase.Destination, global::Morphant.Context.MappingContextMarker, global::Morphant.Generated.N_17fbb67411e1e232593b768778eabb50.DestinationConstruction> resolve)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Creates a destination through a callback only when none exists.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="construct">Callback returning the destination; null ends the mapping.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/construct-using.md"/>
        public static global::Morphant.MappingBuilder<global::TestCase.TestMapper, global::TestCase.Source, global::TestCase.Destination> ConstructUsing(
            this global::Morphant.MappingBuilder<global::TestCase.TestMapper, global::TestCase.Source, global::TestCase.Destination> builder,
            global::Morphant.Delegates.ConstructUsing<global::TestCase.Source, global::TestCase.Destination> construct)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Creates a destination through a callback only when none exists.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="construct">Callback returning the destination; null ends the mapping.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/construct-using.md"/>
        public static global::Morphant.MappingBuilder<global::TestCase.TestMapper, global::TestCase.Source, global::TestCase.Destination> ConstructUsing(
            this global::Morphant.MappingBuilder<global::TestCase.TestMapper, global::TestCase.Source, global::TestCase.Destination> builder,
            global::Morphant.Delegates.ConstructUsing<global::TestCase.Source, global::Morphant.Context.MappingContext, global::TestCase.Destination> construct)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Chooses the destination through a callback on Create and Update.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="resolve">Callback returning the destination; null ends the mapping.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/resolve-using.md"/>
        public static global::Morphant.MappingBuilder<global::TestCase.TestMapper, global::TestCase.Source, global::TestCase.Destination> ResolveUsing(
            this global::Morphant.MappingBuilder<global::TestCase.TestMapper, global::TestCase.Source, global::TestCase.Destination> builder,
            global::Morphant.Delegates.ResolveUsing<global::TestCase.Source, global::TestCase.Destination, global::TestCase.Destination> resolve)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Chooses the destination through a callback on Create and Update.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="resolve">Callback returning the destination; null ends the mapping.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/resolve-using.md"/>
        public static global::Morphant.MappingBuilder<global::TestCase.TestMapper, global::TestCase.Source, global::TestCase.Destination> ResolveUsing(
            this global::Morphant.MappingBuilder<global::TestCase.TestMapper, global::TestCase.Source, global::TestCase.Destination> builder,
            global::Morphant.Delegates.ResolveUsing<global::TestCase.Source, global::TestCase.Destination, global::Morphant.Context.MappingContext, global::TestCase.Destination> resolve)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Maps with ordinary C#, bypassing null policies and member rules.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="mapping">Lambda, method group, or delegate returning the final result.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/convert.md"/>
        public static global::Morphant.MappingBuilder<global::TestCase.TestMapper, global::TestCase.Source, global::TestCase.Destination> Convert(
            this global::Morphant.MappingBuilder<global::TestCase.TestMapper, global::TestCase.Source, global::TestCase.Destination> builder,
            global::Morphant.Delegates.Convert<global::TestCase.Source?, global::TestCase.Destination> mapping)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Maps with ordinary C#, bypassing null policies and member rules.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="mapping">Lambda, method group, or delegate returning the final result.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/convert.md"/>
        public static global::Morphant.MappingBuilder<global::TestCase.TestMapper, global::TestCase.Source, global::TestCase.Destination> Convert(
            this global::Morphant.MappingBuilder<global::TestCase.TestMapper, global::TestCase.Source, global::TestCase.Destination> builder,
            global::Morphant.Delegates.Convert<global::TestCase.Source?, global::TestCase.Destination, global::TestCase.Destination> mapping)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Maps with ordinary C#, bypassing null policies and member rules.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="mapping">Lambda, method group, or delegate returning the final result.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/convert.md"/>
        public static global::Morphant.MappingBuilder<global::TestCase.TestMapper, global::TestCase.Source, global::TestCase.Destination> Convert(
            this global::Morphant.MappingBuilder<global::TestCase.TestMapper, global::TestCase.Source, global::TestCase.Destination> builder,
            global::Morphant.Delegates.Convert<global::TestCase.Source?, global::TestCase.Destination, global::Morphant.Context.MappingContext, global::TestCase.Destination> mapping)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();
    }
}
""";

        // lang=c#
        const string expectedSource2 =
"""
// <auto-generated />
#nullable enable

namespace TestCase
{
    public partial class TestMapper :
        global::Morphant.ITypeMapper<global::TestCase.Source, global::TestCase.Destination>
    {
        /// <inheritdoc/>
        protected override bool Supports(
            global::System.Type sourceType,
            global::System.Type destinationType) =>
                (sourceType == typeof(global::TestCase.Source) &&
                    destinationType == typeof(global::TestCase.Destination)) ||
                base.Supports(sourceType, destinationType);

        /// <inheritdoc/>
        global::TestCase.Destination global::Morphant.ITypeMapper<global::TestCase.Source, global::TestCase.Destination>.Create(
            global::TestCase.Source? source,
            global::Morphant.Context.MappingContext context)
        {
            if (source is null)
            {
                return default!;
            }

            return __Create();
        }

        /// <inheritdoc/>
        global::TestCase.Destination global::Morphant.ITypeMapper<global::TestCase.Source, global::TestCase.Destination>.Update(
            global::TestCase.Source? source,
            global::TestCase.Destination? destination,
            global::Morphant.Context.MappingContext context)
        {
            if (source is null)
            {
                return default!;
            }

            if (destination is null)
            {
                return __Create();
            }

            return __Update(destination);
        }

        private global::TestCase.Destination __Create()
        {
            return __ConstructUsing();
        }

        private global::TestCase.Destination __Update(
            global::TestCase.Destination destination)
        {
            return destination;
        }

        private global::TestCase.Destination __ConstructUsing() => new(this.Offset);
    }
}
""";

        // lang=c#
        const string expectedInvalidMapper =
"""
// <auto-generated />
#nullable enable

namespace TestCase
{
    public partial class TestMapper :
        global::Morphant.ITypeMapper<global::TestCase.Source, global::TestCase.Destination>
    {
        /// <inheritdoc/>
        protected override bool Supports(
            global::System.Type sourceType,
            global::System.Type destinationType) =>
                (sourceType == typeof(global::TestCase.Source) &&
                    destinationType == typeof(global::TestCase.Destination)) ||
                base.Supports(sourceType, destinationType);

        /// <inheritdoc/>
        global::TestCase.Destination global::Morphant.ITypeMapper<global::TestCase.Source, global::TestCase.Destination>.Create(
            global::TestCase.Source? source,
            global::Morphant.Context.MappingContext context)
        {
            if (source is null)
            {
                return default!;
            }

            return __Create(global::Morphant.Context.MappingOperation.Create);
        }

        /// <inheritdoc/>
        global::TestCase.Destination global::Morphant.ITypeMapper<global::TestCase.Source, global::TestCase.Destination>.Update(
            global::TestCase.Source? source,
            global::TestCase.Destination? destination,
            global::Morphant.Context.MappingContext context)
        {
            if (source is null)
            {
                return default!;
            }

            if (destination is null)
            {
                return __Create(global::Morphant.Context.MappingOperation.Update);
            }

            return __Update(destination);
        }

        private global::TestCase.Destination __Create(
            global::Morphant.Context.MappingOperation operation)
        {
            throw new global::Morphant.Exceptions.MappingConfigurationException(
                operation,
                typeof(global::TestCase.Source),
                typeof(global::TestCase.Destination),
                "This ConstructUsing or ResolveUsing function is not supported.");
        }

        private global::TestCase.Destination __Update(
            global::TestCase.Destination destination)
        {
            return destination;
        }
    }
}
""";

        (string HintName, string Source)[] expectedSources =
        [
            ("Morphant.Generated.Construction.Destination__17fbb67411e1e232593b768778eabb50.g.cs", GeneratedSourceText.Normalize(expectedSource0)),
            ("Morphant.Generated.MappingExtension.TestMapper.SourceToDestination__045c8ddc6d3bd2389959dca1e52fe4d8.g.cs", GeneratedSourceText.Normalize(expectedSource1)),
            ("Morphant.Generated.TypeMapper.TestMapper__3716cfd39450e4dd725a4e5fa8513843.g.cs", GeneratedSourceText.Normalize(expectedSource2))
        ];

        var invalid = CallbackDiagnosticsGeneratorTest.Run(InvalidSource);
        var valid = CallbackDiagnosticsGeneratorTest.Run(
            validSource,
            driver: invalid.Driver);
        var invalidAgain = CallbackDiagnosticsGeneratorTest.Run(
            InvalidSource,
            driver: valid.Driver);

        Assert.Multiple(() =>
        {
            Assert.That(
                invalid.EffectiveDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0030" }));
            Assert.That(valid.EffectiveDiagnostics, Is.Empty);
            Assert.That(
                invalidAgain.EffectiveDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0030" }));
            Assert.That(Sources(valid), Is.EquivalentTo(expectedSources));
            var expectedInvalidSources = expectedSources.Select(item =>
                item.HintName == "Morphant.Generated.TypeMapper.TestMapper__3716cfd39450e4dd725a4e5fa8513843.g.cs"
                    ? (item.HintName, GeneratedSourceText.Normalize(expectedInvalidMapper))
                    : item).ToArray();
            Assert.That(Sources(invalid), Is.EquivalentTo(expectedInvalidSources));
            Assert.That(Sources(invalidAgain), Is.EquivalentTo(expectedInvalidSources));
            Assert.That(valid.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(invalidAgain.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    private static (string HintName, string Source)[] Sources(
        CallbackDiagnosticsGeneratorResult result) =>
        result.GeneratedSources
            .Select(static source =>
                (source.HintName, source.SourceText.ToString()))
            .ToArray();


}
