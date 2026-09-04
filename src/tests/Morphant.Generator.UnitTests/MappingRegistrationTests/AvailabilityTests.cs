using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.MappingRegistrationTests;

[TestFixture]
internal sealed class AvailabilityTests
{
    [Test]
    public void Reports_each_unavailable_role_at_the_full_type_argument()
    {
        // lang=c#
        const string source =
"""
#nullable enable

using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public partial class Container
{
    private sealed class PrivateSource { }
    private sealed class PrivateDestination { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<PrivateSource?, PrivateDestination?>();
    }
}
""";

        var result = MappingRegistrationGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0011", "MORPH0011" }));
            Assert.That(
                result.Diagnostics.Select(static diagnostic =>
                    diagnostic.GetMessage()),
                Is.EqualTo(new[]
                {
                    "The source type " +
                    "'TestCase.Container.PrivateSource?' is not accessible " +
                    "to the generated mapper.",
                    "The destination type " +
                    "'TestCase.Container.PrivateDestination?' is not " +
                    "accessible to the generated mapper."
                }));
            Assert.That(
                result.Diagnostics.Select(diagnostic =>
                    MappingRegistrationGeneratorTest.SourceText(
                        diagnostic.Location)),
                Is.EqualTo(new[]
                {
                    "PrivateSource?",
                    "PrivateDestination?"
                }));
            Assert.That(
                result.Diagnostics.SelectMany(static diagnostic =>
                    diagnostic.AdditionalLocations),
                Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Recursively_checks_containing_and_generic_argument_types()
    {
        // lang=c#
        const string source =
"""
using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class Envelope<T> { }

public partial class Container
{
    private sealed class Private
    {
        public sealed class Nested { }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Private.Nested, int>();
            builder.Map<int, Envelope<Private>>();
        }
    }
}
""";

        var result = MappingRegistrationGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0011", "MORPH0011" }));
            Assert.That(
                result.Diagnostics.Select(diagnostic =>
                    MappingRegistrationGeneratorTest.SourceText(
                        diagnostic.Location)),
                Is.EqualTo(new[]
                {
                    "Private.Nested",
                    "Envelope<Private>"
                }));
        });
    }

    [Test]
    public void File_local_pair_is_excluded_but_an_independent_pair_survives()
    {
        // lang=c#
        const string source =
"""
using Morphant;

#pragma warning disable CS1591

namespace TestCase;

file sealed class FileSource { }
public sealed class Source { }
public sealed class Destination { }

[MorphantMapper]
public partial class TestMapper : TypeMapper<TestMapper>
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<FileSource, Destination>();
        builder.Map<Source, Destination>();
    }
}
""";

        var result = MappingRegistrationGeneratorTest.Run(
            source,
            LanguageVersion.CSharp11);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0011" }));
            Assert.That(
                MappingRegistrationGeneratorTest.SourceText(
                    result.Diagnostics.Single().Location),
                Is.EqualTo("FileSource"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Extern_alias_only_pair_reports_unavailable_types_without_generation()
    {
        // lang=c#
        const string referencedSource =
"""
namespace ExternalModel
{
    public sealed class Source { }
    public sealed class Destination { }
}
""";
        // lang=c#
        const string source =
"""
extern alias Models;

using Morphant;

#pragma warning disable CS1591

namespace TestCase
{
    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<
                Models::ExternalModel.Source,
                Models::ExternalModel.Destination>();
    }
}
""";
        var reference = GeneratorTestDriver
            .CompileReference("ExternalModels", referencedSource)
            .WithProperties(
                MetadataReferenceProperties.Assembly.WithAliases(
                    ImmutableArray.Create("Models")));

        var result = GeneratorTestDriver.Run(
            "ExternAliasOnlyMapping",
            source,
            LanguageVersion.CSharp9,
            additionalReferences: [reference]);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.EffectiveDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0011", "MORPH0011" }));
            Assert.That(
                result.EffectiveDiagnostics.Select(diagnostic =>
                    GeneratorTestDriver.GetSourceText(
                        diagnostic.Location)),
                Is.EqualTo(new[]
                {
                    "Models::ExternalModel.Source",
                    "Models::ExternalModel.Destination"
                }));
            Assert.That(result.GeneratedSources, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Extern_alias_pair_remains_available_with_global_alias()
    {
        // lang=c#
        const string referencedSource =
"""
namespace ExternalModel
{
    public sealed class Source { }
    public sealed class Destination { }
}
""";
        // lang=c#
        const string source =
"""
extern alias Models;

using Morphant;

#pragma warning disable CS1591

namespace TestCase
{
    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<
                Models::ExternalModel.Source,
                Models::ExternalModel.Destination>();
    }
}
""";
        var reference = GeneratorTestDriver
            .CompileReference("ExternalModels", referencedSource)
            .WithProperties(
                MetadataReferenceProperties.Assembly.WithAliases(
                    ImmutableArray.Create("global", "Models")));

        var result = GeneratorTestDriver.Run(
            "ExternAndGlobalAliasMapping",
            source,
            LanguageVersion.CSharp9,
            additionalReferences: [reference]);

        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Ambiguous_global_name_behind_extern_alias_is_unavailable()
    {
        // lang=c#
        const string referencedSource =
"""
namespace ExternalModel
{
    public sealed class Source { }
    public sealed class Destination { }
}
""";
        // lang=c#
        const string source =
"""
extern alias First;
extern alias Second;

using Morphant;

#pragma warning disable CS1591

namespace TestCase
{
    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<
                First::ExternalModel.Source,
                First::ExternalModel.Destination>();
    }
}
""";
        var firstReference = GeneratorTestDriver
            .CompileReference("FirstExternalModels", referencedSource)
            .WithProperties(
                MetadataReferenceProperties.Assembly.WithAliases(
                    ImmutableArray.Create("global", "First")));
        var secondReference = GeneratorTestDriver
            .CompileReference("SecondExternalModels", referencedSource)
            .WithProperties(
                MetadataReferenceProperties.Assembly.WithAliases(
                    ImmutableArray.Create("global", "Second")));

        var result = GeneratorTestDriver.Run(
            "AmbiguousGlobalMapping",
            source,
            LanguageVersion.CSharp9,
            additionalReferences: [firstReference, secondReference]);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.EffectiveDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0011", "MORPH0011" }));
            Assert.That(
                result.EffectiveDiagnostics.Select(diagnostic =>
                    GeneratorTestDriver.GetSourceText(
                        diagnostic.Location)),
                Is.EqualTo(new[]
                {
                    "First::ExternalModel.Source",
                    "First::ExternalModel.Destination"
                }));
            Assert.That(result.GeneratedSources, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Internal_friend_type_with_one_global_name_remains_available()
    {
        // lang=c#
        const string referencedSource =
"""
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("FriendMapping")]

namespace ExternalModel
{
    internal sealed class Source { }
    internal sealed class Destination { }
}
""";
        // lang=c#
        const string source =
"""
using ExternalModel;
using Morphant;

#pragma warning disable CS1591

namespace TestCase
{
    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }
}
""";
        var reference = GeneratorTestDriver.CompileReference(
            "FriendModels",
            referencedSource);

        var result = GeneratorTestDriver.Run(
            "FriendMapping",
            source,
            LanguageVersion.CSharp9,
            additionalReferences: [reference]);

        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Extern_alias_only_constraint_reports_unavailable_pair()
    {
        // lang=c#
        const string referencedSource =
"""
namespace ExternalModel
{
    public interface IMarker { }
}
""";
        // lang=c#
        const string source =
"""
extern alias Models;

using Morphant;

#pragma warning disable CS1591

namespace TestCase
{
    public sealed class Source<T>
        where T : Models::ExternalModel.IMarker
    {
        public T Value { get; init; } = default!;
    }

    public sealed class Destination<T>
        where T : Models::ExternalModel.IMarker
    {
        public Destination(T value) => Value = value;

        public T Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper<T> : TypeMapper<TestMapper<T>>
        where T : Models::ExternalModel.IMarker
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source<T>, Destination<T>>();
    }
}
""";
        var reference = GeneratorTestDriver
            .CompileReference("ExternalModels", referencedSource)
            .WithProperties(
                MetadataReferenceProperties.Assembly.WithAliases(
                    ImmutableArray.Create("Models")));

        var result = GeneratorTestDriver.Run(
            "ExternAliasConstraintMapping",
            source,
            LanguageVersion.CSharp9,
            additionalReferences: [reference]);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.EffectiveDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0011", "MORPH0011" }));
            Assert.That(
                result.EffectiveDiagnostics.Select(diagnostic =>
                    GeneratorTestDriver.GetSourceText(
                        diagnostic.Location)),
                Is.EqualTo(new[]
                {
                    "Source<T>",
                    "Destination<T>"
                }));
            Assert.That(result.GeneratedSources, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Alias_only_destination_constraints_do_not_emit_broken_plans()
    {
        // lang=c#
        const string referencedSource =
"""
namespace ExternalModel
{
    public interface IMarker { }
}
""";
        // lang=c#
        const string source =
"""
extern alias Models;

using Morphant;

#pragma warning disable CS1591

namespace TestCase
{
    public sealed class Source { }

    public sealed class Value : Models::ExternalModel.IMarker { }

    public sealed class Destination<T>
        where T : Models::ExternalModel.IMarker
    {
        public Destination(T value) => Value = value;

        public T Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination<Value>>();
    }
}
""";
        var reference = GeneratorTestDriver
            .CompileReference("ExternalModels", referencedSource)
            .WithProperties(
                MetadataReferenceProperties.Assembly.WithAliases(
                    ImmutableArray.Create("Models")));

        var result = GeneratorTestDriver.Run(
            "AliasConstrainedDestinationMapping",
            source,
            LanguageVersion.CSharp9,
            additionalReferences: [reference]);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.EffectiveDiagnostics.Select(static diagnostic =>
                    diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0035" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Public_internal_and_protected_internal_graphs_are_available()
    {
        // lang=c#
        const string source =
"""
using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class PublicSource { }
internal sealed class InternalDestination { }

public partial class Container
{
    protected internal sealed class ProtectedInternalSource { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<PublicSource, InternalDestination>();
            builder.Map<ProtectedInternalSource, InternalDestination>();
        }
    }
}
""";

        var result = MappingRegistrationGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Structural_mapper_gate_suppresses_registration_diagnostics()
    {
        // lang=c#
        const string source =
"""
using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public partial class Container
{
    private sealed class PrivateSource { }
    public sealed class Destination { }

    [MorphantMapper]
    public class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<PrivateSource, Destination>();
    }
}
""";

        var result = MappingRegistrationGeneratorTest.Run(source);

        Assert.That(
            result.Diagnostics.Select(static diagnostic => diagnostic.Id),
            Is.EqualTo(new[] { "MORPH0006" }));
    }

    [Test]
    public void Compiler_owned_invalid_type_argument_is_not_duplicated()
    {
        // lang=c#
        const string source =
"""
using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class Destination { }

[MorphantMapper]
public unsafe partial class TestMapper : TypeMapper<TestMapper>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<int*, Destination>();
}
""";

        var result = MappingRegistrationGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(
                result.CompilerWarningsAndErrors.Select(
                    static diagnostic => diagnostic.Id),
                Does.Contain("CS0306"));
        });
    }
}
