using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TemplateTypeTests.DestinationSupport;

internal sealed class TemplateTypeGenericDestinationSupportTests
{
    private const string GenericDestinationHintName =
        "Morphant.TemplateType." +
        "TestCase_Destination_1.g.cs";

    [Test]
    public async Task Generates_generic_template_definition_from_constructed_class_destination()
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

    public sealed class Destination<T>
        where T : class
    {
        public Destination(T value)
        {
        }

        public T Value { get; set; } = default!;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination<string>>();
        }
    }
}
""";

        await TemplateTypeGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            GenericDestinationHintName,
            ExpectedRichGenericTemplate);
    }

    [TestCase("public struct Destination<T>")]
    [TestCase("public sealed record Destination<T>")]
    public async Task Generates_template_for_constructible_generic_destination_kind(
        string destinationDeclaration)
    {
        await RunMinimalGenericDestination(
            destinationDeclaration,
            ExpectedMinimalConstructibleGenericTemplate);
    }

    [TestCase("public abstract class Destination<T>")]
    [TestCase("public interface Destination<T>")]
    public async Task Generates_template_for_non_constructible_generic_destination_kind(
        string destinationDeclaration)
    {
        await RunMinimalGenericDestination(
            destinationDeclaration,
            ExpectedMinimalNonConstructibleGenericTemplate);
    }

    [Test]
    public async Task Generates_template_for_generic_record_struct()
    {
        await RunMinimalGenericDestination(
            "public record struct Destination<T>",
            ExpectedMinimalConstructibleGenericTemplate,
            LanguageVersion.CSharp10);
    }

    [Test]
    public async Task Generates_generic_template_for_nullable_custom_struct_destination()
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

    public struct Destination<T>
    {
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination<int>?>();
        }
    }
}
""";

        await TemplateTypeGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            GenericDestinationHintName,
            ExpectedMinimalConstructibleGenericTemplate);
    }

    [Test]
    public async Task Copies_all_generic_parameter_constraints()
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

    public interface IContract
    {
    }

    public class Base
    {
    }

    public sealed class Constructed : Base, IContract
    {
        public Constructed()
        {
        }
    }

    public sealed class Destination<
        TClass,
        TNullable,
        TNotNull,
        TValue,
        TUnmanaged,
        TNullableBase,
        TNullableInterface,
        TOther,
        TNullableTypeParameter,
        TConstructed>
        where TClass : class
        where TNullable : class?
        where TNotNull : notnull
        where TValue : struct
        where TUnmanaged : unmanaged
        where TNullableBase : Base?
        where TNullableInterface : IContract?
        where TOther : class?
        where TNullableTypeParameter : TOther?
        where TConstructed : Base, IContract, new()
    {
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<
                Source,
                Destination<
                    string,
                    string?,
                    string,
                    int,
                    int,
                    Base?,
                    IContract?,
                    string?,
                    string?,
                    Constructed>>();
        }
    }
}
""";

        await TemplateTypeGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            "Morphant.TemplateType." +
            "TestCase_Destination_10.g.cs",
            ExpectedConstrainedGenericTemplate);
    }

    [Test]
    public async Task Includes_containing_type_parameters_in_nested_generic_template()
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

    public sealed class Outer<TOuter>
        where TOuter : notnull
    {
        public sealed class Destination<TValue>
            where TValue : TOuter
        {
            public TValue Value { get; set; } = default!;
        }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Outer<object>.Destination<string>>();
        }
    }
}
""";

        await TemplateTypeGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            "Morphant.TemplateType." +
            "TestCase_Outer_1_Destination_1.g.cs",
            ExpectedNestedGenericTemplate);
    }

    [Test]
    public async Task Reuses_one_template_definition_for_multiple_constructed_destinations()
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

    public sealed class Destination<T>
    {
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination<int>>();
            builder.Map<Source, Destination<string?>>();
            builder.Map<Source, Destination<int>>();
        }
    }
}
""";

        await TemplateTypeGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            GenericDestinationHintName,
            ExpectedMinimalConstructibleGenericTemplate);
    }

    [Test]
    public async Task Separates_nested_templates_with_colliding_total_arities()
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

    public sealed class Outer<TOuter>
    {
        public sealed class Destination<TValue>
        {
        }
    }

    public sealed class Outer<TFirst, TSecond>
    {
        public sealed class Destination
        {
        }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<
                Source,
                Outer<int>.Destination<string>>();

            builder.Map<
                Source,
                Outer<int, string>.Destination>();
        }
    }
}
""";

        await TemplateTypeGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            (
                "Morphant.TemplateType." +
                "TestCase_Outer_1_Destination_1.g.cs",
                ExpectedNestedArityOneTemplate
            ),
            (
                "Morphant.TemplateType." +
                "TestCase_Outer_2_Destination.g.cs",
                ExpectedNestedArityTwoTemplate
            ));
    }

    [Test]
    public async Task Generates_generic_template_for_destination_from_referenced_assembly()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591
#nullable enable

using Morphant;
using Morphant.Generator.UnitTests.TestAssets;

namespace TestCase
{
    public sealed class Source
    {
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<
                Source,
                ReferencedGenericDestination<string>>();
        }
    }
}
""";

        await TemplateTypeGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            new[]
            {
                typeof(
                    Morphant.Generator.UnitTests.TestAssets
                        .ReferencedGenericDestination<>).Assembly
            },
            (
                "Morphant.TemplateType." +
                "Morphant_Generator_UnitTests_TestAssets_" +
                "ReferencedGenericDestination_1.g.cs",
                ExpectedReferencedGenericTemplate
            ));
    }

    [Test]
    public async Task Generates_template_for_open_constructed_generic_destination()
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

    public sealed class Destination<T>
    {
    }

    [MorphantMapper]
    public partial class TestMapper<T> : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination<T>>();
        }
    }
}
""";

        await TemplateTypeGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            GenericDestinationHintName,
            ExpectedMinimalConstructibleGenericTemplate);
    }

    [Test]
    public async Task Does_not_generate_template_for_shadowed_containing_type_parameter()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS0693
#pragma warning disable CS1591
#nullable enable

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
    }

    public sealed class Outer<T>
    {
        public sealed class Destination<T>
        {
        }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<
                Source,
                Outer<int>.Destination<string>>();
        }
    }
}
""";

        await TemplateTypeGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source);
    }

    private static Task RunMinimalGenericDestination(
        string destinationDeclaration,
        string expected,
        LanguageVersion languageVersion = LanguageVersion.CSharp9)
    {
        var source = $$"""
                       #pragma warning disable CS1591
                       #nullable enable

                       using Morphant;

                       namespace TestCase
                       {
                           public sealed class Source
                           {
                           }

                           {{destinationDeclaration}}
                           {
                           }

                           [MorphantMapper]
                           public partial class TestMapper : TypeMapper
                           {
                               protected override void Configure(MapperBuilder builder)
                               {
                                   builder.Map<Source, Destination<int>>();
                               }
                           }
                       }
                       """;

        return TemplateTypeGeneratorTest.RunAndAssert(
            languageVersion,
            source,
            GenericDestinationHintName,
            expected);
    }

    // lang=c#
    private const string ExpectedRichGenericTemplate =
"""
// <auto-generated />
#nullable enable

namespace TestCase.Morphant.Generated
{
    /// <summary>
    /// Contains mappings for constructor arguments of <see cref="global::TestCase.Destination&lt;T&gt;"/>.
    /// </summary>
    internal sealed class DestinationMorphantTemplateConstructorMembers<T>
        where T : class
    {
        /// <summary>
        /// Configures the <c>value</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<T> value = null!;
    }

    /// <summary>
    /// Represents the Morphant mapping template for <see cref="global::TestCase.Destination&lt;T&gt;"/>.
    /// </summary>
    internal sealed record DestinationMorphantTemplate<T>
        where T : class
    {
        /// <summary>
        /// Creates a destination instance using convention-based mapping.
        /// </summary>
        /// <param name="marker">Selects convention-based mapping.</param>
        /// <param name="members">Specifies optional mappings for constructor arguments.</param>
        public DestinationMorphantTemplate(
            global::Morphant.Markers.ByConventionMarker marker,
            DestinationMorphantTemplateConstructorMembers<T>? members = null)
        {
        }

        /// <summary>
        /// Creates a destination instance using factory-based destination construction.
        /// </summary>
        /// <param name="marker">Selects factory-based construction.</param>
        public DestinationMorphantTemplate(global::Morphant.Markers.ByFactoryMarker<global::TestCase.Destination<T>> marker)
        {
        }

        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="value">Configures the <c>value</c> constructor argument.</param>
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorMember<T> value)
        {
        }

        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination&lt;T&gt;.Value"/>.
        /// </summary>
        public global::Morphant.Members.Member<T> Value
        {
            get => null!;
            set { }
        }

        public bool Equals(DestinationMorphantTemplate<T>? other) => false;

        public override int GetHashCode() => 0;

        public override string ToString() => string.Empty;

        private bool PrintMembers(global::System.Text.StringBuilder builder) => false;
    }
}
""";

    // lang=c#
    private const string ExpectedMinimalConstructibleGenericTemplate =
"""
// <auto-generated />
#nullable enable

namespace TestCase.Morphant.Generated
{
    /// <summary>
    /// Represents the Morphant mapping template for <see cref="global::TestCase.Destination&lt;T&gt;"/>.
    /// </summary>
    internal sealed record DestinationMorphantTemplate<T>
    {
        /// <summary>
        /// Creates a destination instance using convention-based mapping.
        /// </summary>
        /// <param name="marker">Selects convention-based mapping.</param>
        public DestinationMorphantTemplate(global::Morphant.Markers.ByConventionMarker marker)
        {
        }

        /// <summary>
        /// Creates a destination instance using factory-based destination construction.
        /// </summary>
        /// <param name="marker">Selects factory-based construction.</param>
        public DestinationMorphantTemplate(global::Morphant.Markers.ByFactoryMarker<global::TestCase.Destination<T>> marker)
        {
        }

        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        public DestinationMorphantTemplate()
        {
        }

        public bool Equals(DestinationMorphantTemplate<T>? other) => false;

        public override int GetHashCode() => 0;

        public override string ToString() => string.Empty;

        private bool PrintMembers(global::System.Text.StringBuilder builder) => false;
    }
}
""";

    // lang=c#
    private const string ExpectedMinimalNonConstructibleGenericTemplate =
"""
// <auto-generated />
#nullable enable

namespace TestCase.Morphant.Generated
{
    /// <summary>
    /// Represents the Morphant mapping template for <see cref="global::TestCase.Destination&lt;T&gt;"/>.
    /// </summary>
    internal sealed record DestinationMorphantTemplate<T>
    {
        /// <summary>
        /// Configures convention-based mapping without selecting a destination constructor.
        /// </summary>
        /// <param name="marker">Selects convention-based mapping.</param>
        public DestinationMorphantTemplate(global::Morphant.Markers.ByConventionMarker marker)
        {
        }

        /// <summary>
        /// Creates a destination instance using factory-based destination construction.
        /// </summary>
        /// <param name="marker">Selects factory-based construction.</param>
        public DestinationMorphantTemplate(global::Morphant.Markers.ByFactoryMarker<global::TestCase.Destination<T>> marker)
        {
        }

        public bool Equals(DestinationMorphantTemplate<T>? other) => false;

        public override int GetHashCode() => 0;

        public override string ToString() => string.Empty;

        private bool PrintMembers(global::System.Text.StringBuilder builder) => false;
    }
}
""";

    // lang=c#
    private const string ExpectedConstrainedGenericTemplate =
"""
// <auto-generated />
#nullable enable

namespace TestCase.Morphant.Generated
{
    /// <summary>
    /// Represents the Morphant mapping template for <see cref="global::TestCase.Destination&lt;TClass, TNullable, TNotNull, TValue, TUnmanaged, TNullableBase, TNullableInterface, TOther, TNullableTypeParameter, TConstructed&gt;"/>.
    /// </summary>
    internal sealed record DestinationMorphantTemplate<TClass, TNullable, TNotNull, TValue, TUnmanaged, TNullableBase, TNullableInterface, TOther, TNullableTypeParameter, TConstructed>
        where TClass : class
        where TNullable : class?
        where TNotNull : notnull
        where TValue : struct
        where TUnmanaged : unmanaged
        where TNullableBase : global::TestCase.Base?
        where TNullableInterface : global::TestCase.IContract?
        where TOther : class?
        where TNullableTypeParameter : TOther?
        where TConstructed : global::TestCase.Base, global::TestCase.IContract, new()
    {
        /// <summary>
        /// Creates a destination instance using convention-based mapping.
        /// </summary>
        /// <param name="marker">Selects convention-based mapping.</param>
        public DestinationMorphantTemplate(global::Morphant.Markers.ByConventionMarker marker)
        {
        }

        /// <summary>
        /// Creates a destination instance using factory-based destination construction.
        /// </summary>
        /// <param name="marker">Selects factory-based construction.</param>
        public DestinationMorphantTemplate(global::Morphant.Markers.ByFactoryMarker<global::TestCase.Destination<TClass, TNullable, TNotNull, TValue, TUnmanaged, TNullableBase, TNullableInterface, TOther, TNullableTypeParameter, TConstructed>> marker)
        {
        }

        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        public DestinationMorphantTemplate()
        {
        }

        public bool Equals(DestinationMorphantTemplate<TClass, TNullable, TNotNull, TValue, TUnmanaged, TNullableBase, TNullableInterface, TOther, TNullableTypeParameter, TConstructed>? other) => false;

        public override int GetHashCode() => 0;

        public override string ToString() => string.Empty;

        private bool PrintMembers(global::System.Text.StringBuilder builder) => false;
    }
}
""";

    // lang=c#
    private const string ExpectedNestedGenericTemplate =
"""
// <auto-generated />
#nullable enable

namespace TestCase.Morphant.Generated.Outer1Scope
{
    /// <summary>
    /// Represents the Morphant mapping template for <see cref="global::TestCase.Outer&lt;TOuter&gt;.Destination&lt;TValue&gt;"/>.
    /// </summary>
    internal sealed record DestinationMorphantTemplate<TOuter, TValue>
        where TOuter : notnull
        where TValue : TOuter
    {
        /// <summary>
        /// Creates a destination instance using convention-based mapping.
        /// </summary>
        /// <param name="marker">Selects convention-based mapping.</param>
        public DestinationMorphantTemplate(global::Morphant.Markers.ByConventionMarker marker)
        {
        }

        /// <summary>
        /// Creates a destination instance using factory-based destination construction.
        /// </summary>
        /// <param name="marker">Selects factory-based construction.</param>
        public DestinationMorphantTemplate(global::Morphant.Markers.ByFactoryMarker<global::TestCase.Outer<TOuter>.Destination<TValue>> marker)
        {
        }

        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        public DestinationMorphantTemplate()
        {
        }

        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Outer&lt;TOuter&gt;.Destination&lt;TValue&gt;.Value"/>.
        /// </summary>
        public global::Morphant.Members.Member<TValue> Value
        {
            get => null!;
            set { }
        }

        public bool Equals(DestinationMorphantTemplate<TOuter, TValue>? other) => false;

        public override int GetHashCode() => 0;

        public override string ToString() => string.Empty;

        private bool PrintMembers(global::System.Text.StringBuilder builder) => false;
    }
}
""";

    // lang=c#
    private const string ExpectedReferencedGenericTemplate =
"""
// <auto-generated />
#nullable enable

namespace Morphant.Generator.UnitTests.TestAssets.Morphant.Generated
{
    /// <summary>
    /// Represents the Morphant mapping template for <see cref="global::Morphant.Generator.UnitTests.TestAssets.ReferencedGenericDestination&lt;T&gt;"/>.
    /// </summary>
    internal sealed record ReferencedGenericDestinationMorphantTemplate<T>
        where T : notnull
    {
        /// <summary>
        /// Creates a destination instance using convention-based mapping.
        /// </summary>
        /// <param name="marker">Selects convention-based mapping.</param>
        public ReferencedGenericDestinationMorphantTemplate(global::Morphant.Markers.ByConventionMarker marker)
        {
        }

        /// <summary>
        /// Creates a destination instance using factory-based destination construction.
        /// </summary>
        /// <param name="marker">Selects factory-based construction.</param>
        public ReferencedGenericDestinationMorphantTemplate(global::Morphant.Markers.ByFactoryMarker<global::Morphant.Generator.UnitTests.TestAssets.ReferencedGenericDestination<T>> marker)
        {
        }

        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        public ReferencedGenericDestinationMorphantTemplate()
        {
        }

        /// <summary>
        /// Configures mapping for <see cref="global::Morphant.Generator.UnitTests.TestAssets.ReferencedGenericDestination&lt;T&gt;.Value"/>.
        /// </summary>
        public global::Morphant.Members.Member<T> Value
        {
            get => null!;
            set { }
        }

        public bool Equals(ReferencedGenericDestinationMorphantTemplate<T>? other) => false;

        public override int GetHashCode() => 0;

        public override string ToString() => string.Empty;

        private bool PrintMembers(global::System.Text.StringBuilder builder) => false;
    }
}
""";

    // lang=c#
    private const string ExpectedNestedArityOneTemplate =
"""
// <auto-generated />
#nullable enable

namespace TestCase.Morphant.Generated.Outer1Scope
{
    /// <summary>
    /// Represents the Morphant mapping template for <see cref="global::TestCase.Outer&lt;TOuter&gt;.Destination&lt;TValue&gt;"/>.
    /// </summary>
    internal sealed record DestinationMorphantTemplate<TOuter, TValue>
    {
        /// <summary>
        /// Creates a destination instance using convention-based mapping.
        /// </summary>
        /// <param name="marker">Selects convention-based mapping.</param>
        public DestinationMorphantTemplate(global::Morphant.Markers.ByConventionMarker marker)
        {
        }

        /// <summary>
        /// Creates a destination instance using factory-based destination construction.
        /// </summary>
        /// <param name="marker">Selects factory-based construction.</param>
        public DestinationMorphantTemplate(global::Morphant.Markers.ByFactoryMarker<global::TestCase.Outer<TOuter>.Destination<TValue>> marker)
        {
        }

        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        public DestinationMorphantTemplate()
        {
        }

        public bool Equals(DestinationMorphantTemplate<TOuter, TValue>? other) => false;

        public override int GetHashCode() => 0;

        public override string ToString() => string.Empty;

        private bool PrintMembers(global::System.Text.StringBuilder builder) => false;
    }
}
""";

    // lang=c#
    private const string ExpectedNestedArityTwoTemplate =
"""
// <auto-generated />
#nullable enable

namespace TestCase.Morphant.Generated.Outer2Scope
{
    /// <summary>
    /// Represents the Morphant mapping template for <see cref="global::TestCase.Outer&lt;TFirst, TSecond&gt;.Destination"/>.
    /// </summary>
    internal sealed record DestinationMorphantTemplate<TFirst, TSecond>
    {
        /// <summary>
        /// Creates a destination instance using convention-based mapping.
        /// </summary>
        /// <param name="marker">Selects convention-based mapping.</param>
        public DestinationMorphantTemplate(global::Morphant.Markers.ByConventionMarker marker)
        {
        }

        /// <summary>
        /// Creates a destination instance using factory-based destination construction.
        /// </summary>
        /// <param name="marker">Selects factory-based construction.</param>
        public DestinationMorphantTemplate(global::Morphant.Markers.ByFactoryMarker<global::TestCase.Outer<TFirst, TSecond>.Destination> marker)
        {
        }

        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        public DestinationMorphantTemplate()
        {
        }

        public bool Equals(DestinationMorphantTemplate<TFirst, TSecond>? other) => false;

        public override int GetHashCode() => 0;

        public override string ToString() => string.Empty;

        private bool PrintMembers(global::System.Text.StringBuilder builder) => false;
    }
}
""";
}
