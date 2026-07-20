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
    public async Task Places_nested_template_in_containing_type_scope()
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
                "TestCase_Container_Destination__ed07600340fa8c3b",
                "TestCase.Morphant.Generated.ContainerScope",
                "DestinationMorphantTemplate",
                "global::TestCase.Container.Destination"));
    }

    [Test]
    public async Task Generates_same_named_nested_templates_in_distinct_containing_type_scopes()
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

    public static class First
    {
        /// <summary>
        /// Represents the first nested destination model.
        /// </summary>
        public sealed class Destination
        {
        }
    }

    public static class Second
    {
        /// <summary>
        /// Represents the second nested destination model.
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
}
""";

        await RunAndAssert(
            source,
            ExpectedTemplate(
                "TestCase_First_Destination__2836210ee2ea978a",
                "TestCase.Morphant.Generated.FirstScope",
                "DestinationMorphantTemplate",
                "global::TestCase.First.Destination"),
            ExpectedTemplate(
                "TestCase_Second_Destination__a43439fdb43fe60e",
                "TestCase.Morphant.Generated.SecondScope",
                "DestinationMorphantTemplate",
                "global::TestCase.Second.Destination"));
    }

    [Test]
    public async Task Preserves_entire_containing_type_chain_in_template_namespace()
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

    public static class Outer
    {
        public static class Middle
        {
            /// <summary>
            /// Represents a deeply nested destination model.
            /// </summary>
            public sealed class Destination
            {
            }
        }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Outer.Middle.Destination>();
        }
    }
}
""";

        await RunAndAssert(
            source,
            ExpectedTemplate(
                "TestCase_Outer_Middle_Destination__3130940fbd323f13",
                "TestCase.Morphant.Generated.OuterScope.MiddleScope",
                "DestinationMorphantTemplate",
                "global::TestCase.Outer.Middle.Destination"));
    }

    [Test]
    public async Task Places_global_namespace_nested_template_in_generated_scope()
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
""";

        await RunAndAssert(
            source,
            ExpectedTemplate(
                "Container_Destination__d13a8b79fbc90443",
                "Morphant.Generated.ContainerScope",
                "DestinationMorphantTemplate",
                "global::Container.Destination"));
    }

    [Test]
    public async Task Creates_valid_scope_for_keyword_containing_type()
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

    public static class @namespace
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
            builder.Map<Source, @namespace.Destination>();
        }
    }
}
""";

        await RunAndAssert(
            source,
            ExpectedTemplate(
                "TestCase_namespace_Destination__b1d50cce1ebc1363",
                "TestCase.Morphant.Generated.namespaceScope",
                "DestinationMorphantTemplate",
                "global::TestCase.@namespace.Destination"));
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
                "A_B_C__dcf052f10671af9e",
                "A.Morphant.Generated",
                "B_CMorphantTemplate",
                "global::A.B_C"),
            ExpectedTemplate(
                "A_B_C__a143b4740e1429ca",
                "A_B.Morphant.Generated",
                "CMorphantTemplate",
                "global::A_B.C"));
    }

    [Test]
    public async Task Distinguishes_nested_and_top_level_metadata_names_with_same_sanitized_form()
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

public static class A_B
{
    /// <summary>
    /// Represents a nested destination model.
    /// </summary>
    public sealed class Destination
    {
    }
}

namespace A.B
{
    /// <summary>
    /// Represents a top-level destination model.
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
        builder.Map<Source, A_B.Destination>();
        builder.Map<Source, A.B.Destination>();
    }
}
""";

        await RunAndAssert(
            source,
            ExpectedTemplate(
                "A_B_Destination",
                "A.B.Morphant.Generated",
                "DestinationMorphantTemplate",
                "global::A.B.Destination"),
            ExpectedTemplate(
                "A_B_Destination__1ab147f89ef927d2",
                "Morphant.Generated.A_BScope",
                "DestinationMorphantTemplate",
                "global::A_B.Destination"));
    }

    [Test]
    public async Task Keeps_disambiguated_hint_names_stable_across_mapping_order()
    {
        // lang=c#
        const string firstOrderSource =
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
    /// Represents the first destination model.
    /// </summary>
    public sealed class B_C
    {
    }
}

namespace A_B
{
    /// <summary>
    /// Represents the second destination model.
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

        // lang=c#
        const string secondOrderSource =
"""
#pragma warning disable CS1591
#nullable enable

using Morphant;

public sealed class Source
{
}

namespace A_B
{
    /// <summary>
    /// Represents the second destination model.
    /// </summary>
    public sealed class C
    {
    }
}

namespace A
{
    /// <summary>
    /// Represents the first destination model.
    /// </summary>
    public sealed class B_C
    {
    }
}

[MorphantMapper]
public partial class TestMapper : TypeMapper
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<Source, A_B.C>();
        builder.Map<Source, A.B_C>();
    }
}
""";

        var expectedTemplates = new[]
        {
            ExpectedTemplate(
                "A_B_C__dcf052f10671af9e",
                "A.Morphant.Generated",
                "B_CMorphantTemplate",
                "global::A.B_C"),
            ExpectedTemplate(
                "A_B_C__a143b4740e1429ca",
                "A_B.Morphant.Generated",
                "CMorphantTemplate",
                "global::A_B.C")
        };

        await RunAndAssert(firstOrderSource, expectedTemplates);
        await RunAndAssert(secondOrderSource, expectedTemplates);
    }

    [Test]
    public async Task Generates_one_template_for_destination_mapped_from_multiple_source_types()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591
#nullable enable

using Morphant;

namespace TestCase
{
    public sealed class FirstSource
    {
    }

    public sealed class SecondSource
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
            builder.Map<FirstSource, Destination>();
            builder.Map<SecondSource, Destination>();
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

    [Test]
    public async Task Generates_one_template_for_nullable_and_non_nullable_destination_references()
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
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>();
            builder.Map<Source, Destination?>();
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

    [Test]
    public async Task Generates_one_template_for_aliased_and_direct_destination_references()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591
#nullable enable

using Morphant;
using DestinationAlias = TestCase.Destination;

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
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>();
            builder.Map<Source, DestinationAlias>();
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
