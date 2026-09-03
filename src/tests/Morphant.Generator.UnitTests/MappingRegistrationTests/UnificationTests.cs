namespace Morphant.Generator.UnitTests.MappingRegistrationTests;

[TestFixture]
internal sealed class UnificationTests
{
    [Test]
    public void Reports_one_conflict_per_unordered_contract_pair()
    {
        // lang=c#
        const string source =
"""
using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class Envelope<T> { }
public sealed class Destination { }

[MorphantMapper]
public partial class TestMapper<T> : TypeMapper<TestMapper<T>>
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<Envelope<T>, Destination>();
        builder.Map<Envelope<int>, Destination>();
    }
}
""";

        var result = MappingRegistrationGeneratorTest.Run(source);
        var diagnostic = result.Diagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Id, Is.EqualTo("MORPH0014"));
            Assert.That(
                diagnostic.GetMessage(),
                Is.EqualTo(
                    "Mappings 'TestCase.Envelope<T> -> " +
                    "TestCase.Destination' and 'TestCase.Envelope<int> -> " +
                    "TestCase.Destination' may become identical for some " +
                    "generic type arguments in mapper " +
                    "'TestCase.TestMapper<T>'."));
            Assert.That(
                MappingRegistrationGeneratorTest.SourceText(
                    diagnostic.Location),
                Is.EqualTo("Map"));
            Assert.That(
                MappingRegistrationGeneratorTest.SourceText(
                    diagnostic.AdditionalLocations.Single()),
                Is.EqualTo("Map"));
            Assert.That(
                diagnostic.Location.GetLineSpan().StartLinePosition.Line,
                Is.EqualTo(15));
            Assert.That(
                diagnostic.AdditionalLocations.Single()
                    .GetLineSpan().StartLinePosition.Line,
                Is.EqualTo(14));
        });
    }

    [Test]
    public void Three_pairwise_unifiable_contracts_report_three_diagnostics()
    {
        // lang=c#
        const string source =
"""
using Morphant;

#pragma warning disable CS1591

namespace TestCase;

[MorphantMapper]
public partial class TestMapper<T, U> : TypeMapper<TestMapper<T, U>>
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<T, U>();
        builder.Map<int, U>();
        builder.Map<T, string>();
    }
}
""";

        var result = MappingRegistrationGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Diagnostics.Count(static diagnostic =>
                    diagnostic.Id == "MORPH0014"),
                Is.EqualTo(3));
            Assert.That(
                result.Diagnostics
                    .Where(static diagnostic => diagnostic.Id == "MORPH0014")
                    .Select(static diagnostic =>
                        diagnostic.AdditionalLocations.Count),
                Is.All.EqualTo(1));
        });
    }

    [Test]
    public void Occurs_check_rejects_a_self_containing_substitution()
    {
        // lang=c#
        const string source =
"""
using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class Envelope<T> { }
public sealed class Destination { }

[MorphantMapper]
public partial class TestMapper<T> : TypeMapper<TestMapper<T>>
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<T, Destination>();
        builder.Map<Envelope<T>, Destination>();
    }
}
""";

        var result = MappingRegistrationGeneratorTest.Run(source);

        Assert.That(
            result.Diagnostics.Select(static diagnostic => diagnostic.Id),
            Is.EqualTo(new[] { "MORPH0012" }));
    }

    [Test]
    public void Generic_constraints_do_not_disprove_unification()
    {
        // lang=c#
        const string source =
"""
using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class ClassSource { }
public sealed class Destination { }

[MorphantMapper]
public partial class TestMapper<T> : TypeMapper<TestMapper<T>>
    where T : struct
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<T, Destination>();
        builder.Map<ClassSource, Destination>();
    }
}
""";

        var result = MappingRegistrationGeneratorTest.Run(source);

        Assert.That(
            result.Diagnostics.Select(static diagnostic => diagnostic.Id),
            Is.EqualTo(new[] { "MORPH0012", "MORPH0014" }));
    }

    [Test]
    public void Unsupported_root_participates_and_structural_recovery_wins()
    {
        // lang=c#
        const string source =
"""
using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class Destination { }

[MorphantMapper]
public partial class TestMapper<T> : TypeMapper<TestMapper<T>>
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<T, Destination>();
        builder.Map<int, Destination>();
    }
}
""";

        var result = MappingRegistrationGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0012", "MORPH0014" }));
        });
    }

    [Test]
    public void Declared_contract_conflict_excludes_pair_from_unification()
    {
        // lang=c#
        const string source =
"""
using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class Envelope<T> { }
public sealed class Destination { }

[MorphantMapper]
public abstract partial class TestMapper<T> : TypeMapper<TestMapper<T>>,
    ITypeMapper<Envelope<T>, Destination>
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<Envelope<T>, Destination>();
        builder.Map<Envelope<int>, Destination>();
    }
}
""";

        var result = MappingRegistrationGeneratorTest.Run(source);
        Assert.Multiple(() =>
        {
            Assert.That(
                result.Diagnostics.Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0009", "MORPH0010" }));
            Assert.That(
                result.Diagnostics.Any(static diagnostic =>
                    diagnostic.Id == "MORPH0014"),
                Is.False);
        });
    }

    [Test]
    public void Unavailable_pair_is_not_used_for_unification()
    {
        // lang=c#
        const string source =
"""
using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class Envelope<T> { }
public sealed class Destination { }

public partial class Container
{
    private sealed class Hidden { }

    [MorphantMapper]
    public partial class TestMapper<T> : TypeMapper<TestMapper<T>>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Envelope<Hidden>, Destination>();
            builder.Map<Envelope<T>, Destination>();
        }
    }
}
""";

        var result = MappingRegistrationGeneratorTest.Run(source);

        Assert.That(
            result.Diagnostics.Select(static diagnostic => diagnostic.Id),
            Is.EqualTo(new[] { "MORPH0011" }));
    }

    [Test]
    public void Exact_duplicate_does_not_unify_with_itself()
    {
        // lang=c#
        const string source =
"""
using Morphant;

#pragma warning disable CS1591

namespace TestCase;

public sealed class Destination { }

[MorphantMapper]
public partial class TestMapper<T> : TypeMapper<TestMapper<T>>
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<T, Destination>();
        builder.Map<T, Destination>();
    }
}
""";

        var result = MappingRegistrationGeneratorTest.Run(source);

        Assert.That(
            result.Diagnostics.Select(static diagnostic => diagnostic.Id),
            Is.EqualTo(new[] { "MORPH0012", "MORPH0013" }));
    }
}
