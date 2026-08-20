using Microsoft.CodeAnalysis.CSharp;

namespace Morphant.Generator.UnitTests.FlatteningTests;

[TestFixture]
internal sealed class DiagnosticTests
{
    [Test]
    public void Reports_all_compatible_member_paths_without_guessing()
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
        public Customer Customer { get; init; } = new();
    }

    public sealed class Customer
    {
        public Address Address { get; init; } = new();

        public string AddressCity { get; init; } = string.Empty;
    }

    public sealed class Address
    {
        public string City { get; init; } = string.Empty;
    }

    public sealed class Destination
    {
        public string CustomerAddressCity { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }
}
""";

        var result = FlatteningGeneratorTest.Run(source);
        var diagnostic = result.FlatteningDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(
                FlatteningGeneratorTest.SourceText(diagnostic.Location),
                Is.EqualTo("CustomerAddressCity"));
            Assert.That(
                diagnostic.GetMessage(),
                Is.EqualTo(
                    "Auto flattening is ambiguous for mapping " +
                    "'TestCase.Source -> TestCase.Destination' in mapper " +
                    "'TestCase.TestMapper': target 'CustomerAddressCity' " +
                    "matches 'Customer.Address.City', " +
                    "'Customer.AddressCity'. Configure the target " +
                    "explicitly."));
            Assert.That(
                result.EffectiveDiagnostics.Where(static candidate =>
                    candidate.Severity == Microsoft.CodeAnalysis
                        .DiagnosticSeverity.Error),
                Has.Exactly(1).Matches<Microsoft.CodeAnalysis.Diagnostic>(
                    static candidate => candidate.Id == "MORPH0051"));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Reports_constructor_ambiguity_without_a_secondary_construction_error()
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
        public Customer Customer { get; init; } = new();
    }

    public sealed class Customer
    {
        public Address Address { get; init; } = new();

        public string AddressCity { get; init; } = string.Empty;
    }

    public sealed class Address
    {
        public string City { get; init; } = string.Empty;
    }

    public sealed class Destination
    {
        public Destination(string customerAddressCity)
        {
            CustomerAddressCity = customerAddressCity;
        }

        public string CustomerAddressCity { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }
}
""";

        var result = FlatteningGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.FlatteningDiagnostics,
                Has.Exactly(1).Items);
            Assert.That(
                result.EffectiveDiagnostics.Any(static diagnostic =>
                    diagnostic.Id is "MORPH0036" or "MORPH0037"),
                Is.False);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Explicit_member_rule_resolves_the_ambiguity()
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
        public Customer Customer { get; init; } = new();
    }

    public sealed class Customer
    {
        public Address Address { get; init; } = new();

        public string AddressCity { get; init; } = string.Empty;
    }

    public sealed class Address
    {
        public string City { get; init; } = string.Empty;
    }

    public sealed class Destination
    {
        public string CustomerAddressCity { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members(source => new()
                {
                    CustomerAddressCity = source.Customer.Address.City
                });
    }
}
""";

        var result = FlatteningGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.FlatteningDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Explicit_Auto_requests_flattening_and_keeps_ambiguity_visible()
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
        public Customer Customer { get; init; } = new();
    }

    public sealed class Customer
    {
        public Address Address { get; init; } = new();

        public string AddressCity { get; init; } = string.Empty;
    }

    public sealed class Address
    {
        public string City { get; init; } = string.Empty;
    }

    public sealed class Destination
    {
        public string CustomerAddressCity { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .Members(source => new()
                {
                    CustomerAddressCity = Auto()
                });
    }
}
""";

        var result = FlatteningGeneratorTest.Run(source);
        var diagnostic = result.FlatteningDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(
                FlatteningGeneratorTest.SourceText(diagnostic.Location),
                Is.EqualTo("Auto()"));
            Assert.That(
                result.EffectiveDiagnostics.Any(static candidate =>
                    candidate.Id == "MORPH0041"),
                Is.False);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Ambiguity_is_the_only_error_for_a_required_target()
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
        public Customer Customer { get; init; } = new();
    }

    public sealed class Customer
    {
        public Address Address { get; init; } = new();

        public string AddressCity { get; init; } = string.Empty;
    }

    public sealed class Address
    {
        public string City { get; init; } = string.Empty;
    }

    public sealed class Destination
    {
        public required string CustomerAddressCity { get; init; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }
}
""";

        var result = FlatteningGeneratorTest.Run(
            source,
            languageVersion: LanguageVersion.CSharp11);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.EffectiveDiagnostics
                    .Where(static diagnostic =>
                        diagnostic.Id.StartsWith("MORPH"))
                    .Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0051" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Ambiguity_does_not_cascade_into_strict_completeness_warnings()
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
        public Customer Customer { get; init; } = new();
    }

    public sealed class Customer
    {
        public Address Address { get; init; } = new();

        public string AddressCity { get; init; } = string.Empty;
    }

    public sealed class Address
    {
        public string City { get; init; } = string.Empty;
    }

    public sealed class Destination
    {
        public string CustomerAddressCity { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .UnmappedMemberValidation(UnmappedMemberValidation.Strict);
    }
}
""";

        var result = FlatteningGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.EffectiveDiagnostics
                    .Where(static diagnostic =>
                        diagnostic.Id.StartsWith("MORPH"))
                    .Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0051" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Successful_flattening_satisfies_strict_completeness()
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
        public Customer Customer { get; init; } = new();
    }

    public sealed class Customer
    {
        public string Name { get; init; } = string.Empty;
    }

    public sealed class Destination
    {
        public string CustomerName { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .UnmappedMemberValidation(UnmappedMemberValidation.Strict);
    }
}
""";

        var result = FlatteningGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.EffectiveDiagnostics.Where(static diagnostic =>
                    diagnostic.Id.StartsWith("MORPH")),
                Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Strict_completeness_still_reports_independent_members()
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
        public Customer Customer { get; init; } = new();

        public string SourceOnly { get; init; } = string.Empty;
    }

    public sealed class Customer
    {
        public Address Address { get; init; } = new();

        public string AddressCity { get; init; } = string.Empty;
    }

    public sealed class Address
    {
        public string City { get; init; } = string.Empty;
    }

    public sealed class Destination
    {
        public string CustomerAddressCity { get; set; } = string.Empty;

        public string DestinationOnly { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .UnmappedMemberValidation(UnmappedMemberValidation.Strict);
    }
}
""";

        var result = FlatteningGeneratorTest.Run(source);
        var diagnosticIds = result.EffectiveDiagnostics
            .Where(static diagnostic =>
                diagnostic.Id.StartsWith("MORPH"))
            .Select(static diagnostic => diagnostic.Id)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(
                diagnosticIds,
                Is.EquivalentTo(new[]
                {
                    "MORPH0047",
                    "MORPH0048",
                    "MORPH0051"
                }));
            Assert.That(diagnosticIds, Has.Length.EqualTo(3));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Greediest_keeps_an_ambiguity_shared_by_every_constructor()
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
        public Customer Customer { get; init; } = new();
    }

    public sealed class Customer
    {
        public Address Address { get; init; } = new();

        public string AddressCity { get; init; } = string.Empty;
    }

    public sealed class Address
    {
        public string City { get; init; } = string.Empty;
    }

    public sealed class Destination
    {
        public Destination(string customerAddressCity)
        {
            CustomerAddressCity = customerAddressCity;
        }

        public Destination(
            string customerAddressCity,
            int optional = 0)
        {
            CustomerAddressCity = customerAddressCity;
        }

        public string CustomerAddressCity { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .ConstructorSelection(ConstructorSelection.Greediest);
    }
}
""";

        var result = FlatteningGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.EffectiveDiagnostics
                    .Where(static diagnostic =>
                        diagnostic.Id.StartsWith("MORPH"))
                    .Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0051" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Greediest_does_not_promote_a_candidate_specific_ambiguity()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace TestCase
{
    public sealed class Source
    {
        public Customer Customer { get; init; } = new();
    }

    public sealed class Customer
    {
        public Address Address { get; init; } = new();

        public string AddressCity { get; init; } = string.Empty;
    }

    public sealed class Address
    {
        public string City { get; init; } = string.Empty;
    }

    public sealed class Destination
    {
        public Destination(string customerAddressCity)
        {
        }

        public Destination(Guid missing)
        {
        }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .ConstructorSelection(ConstructorSelection.Greediest);
    }
}
""";

        var result = FlatteningGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(result.FlatteningDiagnostics, Is.Empty);
            Assert.That(
                result.EffectiveDiagnostics
                    .Where(static diagnostic =>
                        diagnostic.Id.StartsWith("MORPH"))
                    .Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0036" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }

    [Test]
    public void Structured_Greediest_keeps_a_shared_ambiguity()
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
        public Customer Customer { get; init; } = new();
    }

    public sealed class Customer
    {
        public Address Address { get; init; } = new();

        public string AddressCity { get; init; } = string.Empty;
    }

    public sealed class Address
    {
        public string City { get; init; } = string.Empty;
    }

    public sealed class Destination
    {
        public Destination(string customerAddressCity)
        {
            CustomerAddressCity = customerAddressCity;
        }

        public Destination(
            string customerAddressCity,
            int optional = 0)
        {
            CustomerAddressCity = customerAddressCity;
        }

        public string CustomerAddressCity { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .ConstructorSelection(ConstructorSelection.Greediest)
                .Construct(source => new(ByConvention()));
    }
}
""";

        var result = FlatteningGeneratorTest.Run(source);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.EffectiveDiagnostics
                    .Where(static diagnostic =>
                        diagnostic.Id.StartsWith("MORPH"))
                    .Select(static diagnostic => diagnostic.Id),
                Is.EqualTo(new[] { "MORPH0051" }));
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });
    }
}
