using static Morphant.Generator.UnitTests.TestUtils.TemplateTypeNamingTestHarness;

namespace Morphant.Generator.UnitTests.TemplateTypeTests.Naming;

[TestFixture]
internal sealed class TemplateTypeNamingTests
{
    [Test]
    public async Task Places_template_in_destination_generated_namespace()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591
#nullable enable

using Morphant;

namespace Company.Product.Models
{
    public sealed class Source
    {
    }

    /// <summary>
    /// Represents a destination model.
    /// </summary>
    public sealed class Destination
    {
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>();
        }
    }
}
""";

        await RunAndAssert(
            source,
            ExpectedTemplate(
                "Company_Product_Models_Destination",
                "Company.Product.Models.Morphant.Generated",
                "DestinationMorphantTemplate",
                "global::Company.Product.Models.Destination"));
    }

    [Test]
    public async Task Places_global_namespace_template_in_Morphant_Generated()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591
#nullable enable

using Morphant;

public sealed class Source
{
}

/// <summary>
/// Represents a destination model.
/// </summary>
public sealed class Destination
{
}

[MorphantMapper]
public partial class TestMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<Source, Destination>();
    }
}
""";

        await RunAndAssert(
            source,
            ExpectedTemplate(
                "Destination",
                "Morphant.Generated",
                "DestinationMorphantTemplate",
                "global::Destination"));
    }

    [Test]
    public async Task Uses_nested_destination_identity_in_generated_references()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591
#nullable enable

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
    }

    public static class Container
    {
        /// <summary>
        /// Represents a nested destination model.
        /// </summary>
        public sealed class Destination
        {
        }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Container.Destination>();
        }
    }
}
""";

        await RunAndAssert(
            source,
            ExpectedTemplate(
                "TestCase_Container_Destination__a83cf1bb",
                "TestCase.Morphant.Generated",
                "DestinationMorphantTemplate",
                "global::TestCase.Container.Destination"));
    }

    [Test]
    public async Task Generates_templates_for_same_simple_name_in_different_namespaces()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591
#nullable enable

using Morphant;

public sealed class Source
{
}

namespace First
{
    /// <summary>
    /// Represents the first destination model.
    /// </summary>
    public sealed class Destination
    {
    }
}

namespace Second
{
    /// <summary>
    /// Represents the second destination model.
    /// </summary>
    public sealed class Destination
    {
    }
}

[MorphantMapper]
public partial class TestMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<Source, First.Destination>();
        builder.Map<Source, Second.Destination>();
    }
}
""";

        await RunAndAssert(
            source,
            ExpectedTemplate(
                "First_Destination",
                "First.Morphant.Generated",
                "DestinationMorphantTemplate",
                "global::First.Destination"),
            ExpectedTemplate(
                "Second_Destination",
                "Second.Morphant.Generated",
                "DestinationMorphantTemplate",
                "global::Second.Destination"));
    }

    [Test]
    public async Task Escapes_keyword_identifiers_in_generated_references()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591
#nullable enable

using Morphant;

namespace @namespace
{
    public sealed class Source
    {
    }

    /// <summary>
    /// Represents a destination with keyword identifiers.
    /// </summary>
    public sealed class @class
    {
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, @class>();
        }
    }
}
""";

        await RunAndAssert(
            source,
            ExpectedTemplate(
                "namespace_class",
                "@namespace.Morphant.Generated",
                "classMorphantTemplate",
                "global::@namespace.@class"));
    }

    [Test]
    public async Task Uses_unique_hint_names_for_colliding_sanitized_metadata_names()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591
#nullable enable

using Morphant;

public sealed class Source
{
}

namespace A
{
    /// <summary>
    /// Represents a destination whose type name contains an underscore.
    /// </summary>
    public sealed class B_C
    {
    }
}

namespace A_B
{
    /// <summary>
    /// Represents a destination whose namespace contains an underscore.
    /// </summary>
    public sealed class C
    {
    }
}

[MorphantMapper]
public partial class TestMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<Source, A.B_C>();
        builder.Map<Source, A_B.C>();
    }
}
""";

        await RunAndAssert(
            source,
            ExpectedTemplate(
                "A_B_C__185d251e",
                "A.Morphant.Generated",
                "B_CMorphantTemplate",
                "global::A.B_C"),
            ExpectedTemplate(
                "A_B_C__20b1220a",
                "A_B.Morphant.Generated",
                "CMorphantTemplate",
                "global::A_B.C"));
    }

    [Test]
    public async Task Generates_one_template_for_repeated_destination_across_mappers()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591
#nullable enable

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
    }

    /// <summary>
    /// Represents a destination model.
    /// </summary>
    public sealed class Destination
    {
    }

    [MorphantMapper]
    public partial class FirstMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>();
            builder.Map<Source, Destination>();
        }
    }

    [MorphantMapper]
    public partial class SecondMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>();
        }
    }
}
""";

        await RunAndAssert(
            source,
            ExpectedTemplate(
                "TestCase_Destination",
                "TestCase.Morphant.Generated",
                "DestinationMorphantTemplate",
                "global::TestCase.Destination"));
    }
}
