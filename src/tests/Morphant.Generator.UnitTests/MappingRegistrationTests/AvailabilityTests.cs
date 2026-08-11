using Microsoft.CodeAnalysis.CSharp;

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
    public partial class TestMapper : TypeMapper
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
                    "'global::TestCase.Container.PrivateSource?' is " +
                    "unavailable to Morphant-generated code.",
                    "The destination type " +
                    "'global::TestCase.Container.PrivateDestination?' is " +
                    "unavailable to Morphant-generated code."
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
    public partial class TestMapper : TypeMapper
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
public partial class TestMapper : TypeMapper
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
    public partial class TestMapper : TypeMapper
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
    public class TestMapper : TypeMapper
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
public unsafe partial class TestMapper : TypeMapper
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
