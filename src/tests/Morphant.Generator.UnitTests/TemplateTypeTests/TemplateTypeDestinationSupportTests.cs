using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TemplateTypeTests;

[TestFixture]
internal sealed class TemplateTypeDestinationSupportTests
{
    [Test]
    public async Task Generates_template_for_internal_destination()
    {
        // lang=c#
        const string constructors =
"""
        public Destination()
        {
        }
""";

        // lang=c#
        const string expectedConstructors =
"""
        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        public DestinationMorphantTemplate()
        {
        }
""";

        await TemplateTypeTestHarness.RunAndAssert(
            constructors,
            constructorMembers: string.Empty,
            expectedConstructors,
            destinationDeclaration: "internal sealed class Destination");
    }

    [Test]
    public async Task Generates_template_for_positional_record_struct()
    {
        // lang=c#
        const string constructorMembers =
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
        const string expectedConstructors =
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
        const string expectedMembers =
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
        const string destinationDocumentation =
"""
    /// <summary>
    /// Represents a destination model.
    /// </summary>
    /// <param name="Id">The identifier.</param>
    /// <param name="Name">The name.</param>
""";

        await TemplateTypeTestHarness.RunAndAssert(
            constructors: string.Empty,
            constructorMembers,
            expectedConstructors,
            destinationDeclaration:
                "public record struct Destination(int Id, string Name)",
            expectedMembers: expectedMembers,
            destinationDocumentation: destinationDocumentation,
            languageVersion: LanguageVersion.CSharp10);
    }

    [Test]
    public async Task Does_not_generate_template_for_generic_destination()
    {
        // lang=c#
        const string destinationDeclaration =
"""
    public sealed class Destination<T>
    {
    }
""";

        await RunUnsupportedDestination(
            "Destination<int>",
            destinationDeclaration);
    }

    [Test]
    public async Task Does_not_generate_template_for_destination_nested_in_generic_type()
    {
        // lang=c#
        const string destinationDeclaration =
"""
    public sealed class Container<T>
    {
        public sealed class Destination
        {
        }
    }
""";

        await RunUnsupportedDestination(
            "Container<int>.Destination",
            destinationDeclaration);
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
    public async Task Does_not_generate_template_for_enum_destination()
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
            "Destination",
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
    public async Task Does_not_generate_template_for_nullable_value_type_destination()
    {
        await RunUnsupportedDestination("int?");
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
}
