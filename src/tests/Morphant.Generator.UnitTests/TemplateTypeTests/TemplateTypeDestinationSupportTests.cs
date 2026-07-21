using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TemplateTypeTests;

[TestFixture]
internal sealed class TemplateTypeDestinationSupportTests
{
    [Test]
    public async Task Generates_template_for_public_class()
    {
        await RunSupportedDestinationWithParameterlessConstructor(
            "public class Destination");
    }

    [Test]
    public async Task Generates_template_for_internal_class()
    {
        await RunSupportedDestinationWithParameterlessConstructor(
            "internal sealed class Destination");
    }

    [Test]
    public async Task Generates_template_for_partial_class()
    {
        await RunSupportedDestinationWithParameterlessConstructor(
            "public sealed partial class Destination");
    }

    [Test]
    public async Task Generates_template_for_abstract_class()
    {
        await TemplateTypeTestHarness.RunAndAssert(
            constructors: string.Empty,
            constructorMembers: string.Empty,
            expectedConstructors: string.Empty,
            destinationDeclaration: "public abstract class Destination",
            canConstructDestination: false);
    }

    [Test]
    public async Task Generates_template_for_positional_record_class()
    {
        await TemplateTypeTestHarness.RunAndAssert(
            constructors: string.Empty,
            PositionalRecordConstructorMembers,
            PositionalRecordClassConstructors,
            destinationDeclaration:
                "public sealed record Destination(int Id, string Name)",
            expectedMembers: PositionalRecordMembers,
            destinationDocumentation: PositionalRecordDocumentation);
    }

    [Test]
    public async Task Generates_template_for_struct()
    {
        await RunSupportedDestinationWithParameterlessConstructor(
            "public struct Destination");
    }

    [Test]
    public async Task Generates_template_for_readonly_struct()
    {
        await RunSupportedDestinationWithParameterlessConstructor(
            "public readonly struct Destination");
    }

    [Test]
    public async Task Generates_template_for_positional_record_struct()
    {
        await TemplateTypeTestHarness.RunAndAssert(
            constructors: string.Empty,
            PositionalRecordConstructorMembers,
            PositionalRecordStructConstructors,
            destinationDeclaration:
                "public record struct Destination(int Id, string Name)",
            expectedMembers: PositionalRecordMembers,
            destinationDocumentation: PositionalRecordDocumentation,
            languageVersion: LanguageVersion.CSharp10);
    }

    [Test]
    public async Task Generates_template_for_readonly_record_struct()
    {
        await RunSupportedDestinationWithParameterlessConstructor(
            "public readonly record struct Destination",
            LanguageVersion.CSharp10);
    }

    [Test]
    public async Task Generates_template_for_interface()
    {
        await TemplateTypeTestHarness.RunAndAssert(
            constructors: string.Empty,
            constructorMembers: string.Empty,
            expectedConstructors: string.Empty,
            destinationDeclaration: "public interface Destination",
            canConstructDestination: false);
    }

    [Test]
    public async Task Generates_template_for_nullable_reference_destination()
    {
        await TemplateTypeTestHarness.RunAndAssert(
            constructors: string.Empty,
            constructorMembers: string.Empty,
            ExpectedParameterlessConstructor,
            destinationDeclaration: "public sealed class Destination",
            mappedDestinationType: "Destination?");
    }

    [TestCase(
        "public static class Container",
        "public")]
    [TestCase(
        "internal static class Container",
        "public")]
    [TestCase(
        "public static class Container",
        "internal")]
    [TestCase(
        "public class Container",
        "protected internal")]
    public async Task Generates_template_for_accessible_nested_destination(
        string containerDeclaration,
        string destinationAccessibility)
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

                           {{containerDeclaration}}
                           {
                               /// <summary>
                               /// Represents a destination model.
                               /// </summary>
                               {{destinationAccessibility}} sealed class Destination
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

        await TemplateTypeGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            "Morphant.TemplateType." +
            "TestCase_Container_Destination__ed07600340fa8c3b.g.cs",
            ExpectedNestedDestinationTemplate);
    }

    [Test]
    public async Task Generates_template_for_destination_from_referenced_assembly()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591

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
            builder.Map<Source, ReferencedDestination>();
        }
    }
}
""";

        // lang=c#
        const string expected =
"""
// <auto-generated />
#nullable enable

namespace Morphant.Generator.UnitTests.TestAssets.Morphant.Generated
{
    /// <summary>
    /// Represents the Morphant mapping template for <see cref="global::Morphant.Generator.UnitTests.TestAssets.ReferencedDestination"/>.
    /// </summary>
    internal sealed record ReferencedDestinationMorphantTemplate
    {
        /// <summary>
        /// Creates a destination instance using convention-based mapping.
        /// </summary>
        /// <param name="marker">Selects convention-based mapping.</param>
        public ReferencedDestinationMorphantTemplate(global::Morphant.Markers.ByConventionMarker marker)
        {
        }

        /// <summary>
        /// Creates a destination instance using factory-based destination construction.
        /// </summary>
        /// <param name="marker">Selects factory-based construction.</param>
        public ReferencedDestinationMorphantTemplate(global::Morphant.Markers.ByFactoryMarker<global::Morphant.Generator.UnitTests.TestAssets.ReferencedDestination> marker)
        {
        }

        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        public ReferencedDestinationMorphantTemplate()
        {
        }

        /// <summary>
        /// Configures mapping for <see cref="global::Morphant.Generator.UnitTests.TestAssets.ReferencedDestination.PublicProperty"/>.
        /// </summary>
        public global::Morphant.Members.Member<int> PublicProperty
        {
            get => null!;
            set { }
        }

        public bool Equals(ReferencedDestinationMorphantTemplate? other) => false;

        public override int GetHashCode() => 0;

        public override string ToString() => string.Empty;

        private bool PrintMembers(global::System.Text.StringBuilder builder) => false;
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
                        .ReferencedDestination).Assembly
            },
            (
                "Morphant.TemplateType." +
                "Morphant_Generator_UnitTests_TestAssets_" +
                "ReferencedDestination.g.cs",
                expected
            ));
    }

    [TestCase("object")]
    [TestCase("object?")]
    [TestCase("string")]
    [TestCase("string?")]
    [TestCase("bool")]
    [TestCase("bool?")]
    [TestCase("char")]
    [TestCase("char?")]
    [TestCase("sbyte")]
    [TestCase("sbyte?")]
    [TestCase("byte")]
    [TestCase("byte?")]
    [TestCase("short")]
    [TestCase("short?")]
    [TestCase("ushort")]
    [TestCase("ushort?")]
    [TestCase("int")]
    [TestCase("int?")]
    [TestCase("uint")]
    [TestCase("uint?")]
    [TestCase("long")]
    [TestCase("long?")]
    [TestCase("ulong")]
    [TestCase("ulong?")]
    [TestCase("nint")]
    [TestCase("nint?")]
    [TestCase("nuint")]
    [TestCase("nuint?")]
    [TestCase("float")]
    [TestCase("float?")]
    [TestCase("double")]
    [TestCase("double?")]
    [TestCase("decimal")]
    [TestCase("decimal?")]
    public async Task Does_not_generate_template_type_for_direct_predefined_destination(
        string destinationType)
    {
        await RunUnsupportedDestination(destinationType);
    }

    [TestCase("global::System.Guid")]
    [TestCase("global::System.Guid?")]
    [TestCase("global::System.DateTime")]
    [TestCase("global::System.DateTime?")]
    [TestCase("global::System.DateTimeOffset")]
    [TestCase("global::System.DateTimeOffset?")]
    [TestCase("global::System.DateOnly")]
    [TestCase("global::System.DateOnly?")]
    [TestCase("global::System.TimeOnly")]
    [TestCase("global::System.TimeOnly?")]
    [TestCase("global::System.TimeSpan")]
    [TestCase("global::System.TimeSpan?")]
    [TestCase("global::System.Half")]
    [TestCase("global::System.Half?")]
    [TestCase("global::System.Int128")]
    [TestCase("global::System.Int128?")]
    [TestCase("global::System.UInt128")]
    [TestCase("global::System.UInt128?")]
    [TestCase("global::System.Uri")]
    [TestCase("global::System.Uri?")]
    [TestCase("global::System.Version")]
    [TestCase("global::System.Version?")]
    [TestCase("global::System.Numerics.BigInteger")]
    [TestCase("global::System.Numerics.BigInteger?")]
    [TestCase("global::System.Numerics.Complex")]
    [TestCase("global::System.Numerics.Complex?")]
    [TestCase("global::System.Text.Rune")]
    [TestCase("global::System.Text.Rune?")]
    [TestCase("global::System.Index")]
    [TestCase("global::System.Index?")]
    [TestCase("global::System.Range")]
    [TestCase("global::System.Range?")]
    public async Task Does_not_generate_template_type_for_direct_bcl_destination(
        string destinationType)
    {
        await RunUnsupportedDestination(destinationType);
    }

    [Test]
    public async Task Does_not_generate_template_for_tuple_destination()
    {
        await RunUnsupportedDestination(
            "(int Id, string Name)");
    }

    [Test]
    public async Task Does_not_generate_template_for_array_destination()
    {
        // lang=c#
        const string destinationDeclaration =
"""
    public sealed class Destination
    {
    }
""";

        await RunUnsupportedDestination(
            "Destination[]",
            destinationDeclaration);
    }

    [Test]
    public async Task Does_not_generate_template_for_multidimensional_array_destination()
    {
        // lang=c#
        const string destinationDeclaration =
"""
    public sealed class Destination
    {
    }
""";

        await RunUnsupportedDestination(
            "Destination[,]",
            destinationDeclaration);
    }

    [Test]
    public async Task Does_not_generate_template_for_jagged_array_destination()
    {
        // lang=c#
        const string destinationDeclaration =
"""
    public sealed class Destination
    {
    }
""";

        await RunUnsupportedDestination(
            "Destination[][]",
            destinationDeclaration);
    }

    [TestCase("Destination")]
    [TestCase("Destination?")]
    public async Task Does_not_generate_template_type_for_direct_enum_destination(
        string destinationType)
    {
        // lang=c#
        const string destinationDeclaration =
"""
    public enum Destination
    {
        None
    }
""";

        await RunUnsupportedDestination(
            destinationType,
            destinationDeclaration);
    }

    [Test]
    public async Task Does_not_generate_template_for_delegate_destination()
    {
        // lang=c#
        const string destinationDeclaration =
"""
    public delegate void Destination();
""";

        await RunUnsupportedDestination(
            "Destination",
            destinationDeclaration);
    }

    [Test]
    public async Task Does_not_generate_template_for_nullable_custom_struct_destination()
    {
        // lang=c#
        const string destinationDeclaration =
"""
    public struct Destination
    {
        public int Value { get; set; }
    }
""";

        await RunUnsupportedDestination(
            "Destination?",
            destinationDeclaration);
    }

    [Test]
    public async Task Does_not_generate_template_for_dynamic_destination()
    {
        await RunUnsupportedDestination("dynamic");
    }

    [Test]
    public async Task Does_not_generate_template_for_type_parameter_destination()
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

    [MorphantMapper]
    public partial class TestMapper<TDestination> : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, TDestination>();
        }
    }
}
""";

        await TemplateTypeGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source);
    }

    [Test]
    public async Task Does_not_generate_template_for_private_nested_destination()
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
        private sealed class Destination
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
}
""";

        await TemplateTypeGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source);
    }

    [Test]
    public async Task Does_not_generate_template_for_protected_nested_destination()
    {
        await RunInaccessibleNestedDestination("protected");
    }

    [Test]
    public async Task Does_not_generate_template_for_private_protected_nested_destination()
    {
        await RunInaccessibleNestedDestination("private protected");
    }

    [Test]
    public async Task Does_not_generate_template_when_containing_type_is_inaccessible()
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
        private static class Container
        {
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
    }
}
""";

        await TemplateTypeGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source);
    }

    [Test]
    public async Task Does_not_generate_template_for_file_local_destination()
    {
        // lang=c#
        const string destinationDeclaration =
"""
    file sealed class Destination
    {
    }
""";

        await RunUnsupportedDestination(
            "Destination",
            destinationDeclaration,
            LanguageVersion.CSharp11);
    }

    [Test]
    public async Task Does_not_generate_template_for_destination_nested_in_file_local_type()
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

    file static class Container
    {
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
}
""";

        await TemplateTypeGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp11,
            source);
    }

    private static Task RunSupportedDestinationWithParameterlessConstructor(
        string destinationDeclaration,
        LanguageVersion languageVersion = LanguageVersion.CSharp9)
    {
        return TemplateTypeTestHarness.RunAndAssert(
            constructors: string.Empty,
            constructorMembers: string.Empty,
            ExpectedParameterlessConstructor,
            destinationDeclaration: destinationDeclaration,
            languageVersion: languageVersion);
    }

    private static Task RunUnsupportedDestination(
        string destinationType,
        string destinationDeclaration = "",
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

                           [MorphantMapper]
                           public partial class TestMapper : TypeMapper
                           {
                               protected override void Configure(MapperBuilder builder)
                               {
                                   builder.Map<Source, {{destinationType}}>();
                               }
                           }
                       }
                       """;

        return TemplateTypeGeneratorTest.RunAndAssert(
            languageVersion,
            source);
    }

    private static Task RunInaccessibleNestedDestination(
        string destinationAccessibility)
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

                           public class Container
                           {
                               {{destinationAccessibility}} sealed class Destination
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
                       }
                       """;

        return TemplateTypeGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source);
    }

    // lang=c#
    private const string ExpectedParameterlessConstructor =
"""
        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        public DestinationMorphantTemplate()
        {
        }
""";

    // lang=c#
    private const string PositionalRecordConstructorMembers =
"""
    /// <summary>
    /// Contains mappings for constructor arguments of <see cref="global::TestCase.Destination"/>.
    /// </summary>
    internal sealed class DestinationMorphantTemplateConstructorMembers
    {
        /// <summary>
        /// Configures the <c>Id</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<int> Id = null!;

        /// <summary>
        /// Configures the <c>Name</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<string> Name = null!;
    }
""";

    // lang=c#
    private const string PositionalRecordClassConstructors =
"""
        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="Id">Configures the <c>Id</c> constructor argument.</param>
        /// <param name="Name">Configures the <c>Name</c> constructor argument.</param>
        public DestinationMorphantTemplate(
            global::Morphant.Members.ConstructorMember<int> Id,
            global::Morphant.Members.ConstructorMember<string> Name)
        {
        }
""";

    // lang=c#
    private const string PositionalRecordStructConstructors =
"""
        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="Id">Configures the <c>Id</c> constructor argument.</param>
        /// <param name="Name">Configures the <c>Name</c> constructor argument.</param>
        public DestinationMorphantTemplate(
            global::Morphant.Members.ConstructorMember<int> Id,
            global::Morphant.Members.ConstructorMember<string> Name)
        {
        }

        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        public DestinationMorphantTemplate()
        {
        }
""";

    // lang=c#
    private const string PositionalRecordMembers =
"""
        /// <inheritdoc cref="global::TestCase.Destination.Id"/>
        public global::Morphant.Members.Member<int> Id
        {
            get => null!;
            set { }
        }

        /// <inheritdoc cref="global::TestCase.Destination.Name"/>
        public global::Morphant.Members.Member<string> Name
        {
            get => null!;
            set { }
        }
""";

    // lang=c#
    private const string PositionalRecordDocumentation =
"""
    /// <summary>
    /// Represents a destination model.
    /// </summary>
    /// <param name="Id">The identifier.</param>
    /// <param name="Name">The name.</param>
""";

    // lang=c#
    private const string ExpectedNestedDestinationTemplate =
"""
// <auto-generated />
#nullable enable

namespace TestCase.Morphant.Generated.ContainerScope
{
    /// <inheritdoc cref="global::TestCase.Container.Destination"/>
    internal sealed record DestinationMorphantTemplate
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
        public DestinationMorphantTemplate(global::Morphant.Markers.ByFactoryMarker<global::TestCase.Container.Destination> marker)
        {
        }

        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        public DestinationMorphantTemplate()
        {
        }

        public bool Equals(DestinationMorphantTemplate? other) => false;

        public override int GetHashCode() => 0;

        public override string ToString() => string.Empty;

        private bool PrintMembers(global::System.Text.StringBuilder builder) => false;
    }
}
""";

}
