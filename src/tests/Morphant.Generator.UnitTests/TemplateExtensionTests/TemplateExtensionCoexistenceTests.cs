using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TemplateExtensionTests;

[TestFixture]
internal sealed class TemplateExtensionCoexistenceTests
{
    [Test]
    public async Task Generates_independent_extensions_for_distinct_destinations()
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

    public sealed class ClassDestination
    {
    }

    public sealed class GenericDestination<T>
    {
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, ClassDestination>()
                .Template(static _ => new());

            builder.Map<Source, GenericDestination<int>>()
                .Template(static _ => new());

            builder.Map<Source, GenericDestination<int?>>()
                .Template(static _ => new());

            builder.Map<Source, GenericDestination<string>>()
                .Template(static _ => new());

            builder.Map<Source, int>()
                .Template(static _ => 0);

            builder.Map<Source, int?>()
                .Template(static _ => null);
        }
    }
}

namespace TestCase.Morphant.Generated
{
    internal sealed record ClassDestinationMorphantTemplate;

    internal sealed record GenericDestinationMorphantTemplate<T>;
}
""";

        const string classDestination =
            "global::TestCase.ClassDestination";

        const string intGenericDestination =
            "global::TestCase.GenericDestination<int>";

        const string nullableIntGenericDestination =
            "global::TestCase.GenericDestination<int?>";

        const string stringGenericDestination =
            "global::TestCase.GenericDestination<string>";

        await TemplateExtensionGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            ExpectedGeneratedExtension(
                "TestCase.ClassDestination",
                classDestination,
                "global::TestCase.Morphant.Generated." +
                "ClassDestinationMorphantTemplate"),
            ExpectedGeneratedExtension(
                "TestCase.GenericDestination`1<int>",
                intGenericDestination,
                "global::TestCase.Morphant.Generated." +
                "GenericDestinationMorphantTemplate<int>"),
            ExpectedGeneratedExtension(
                "TestCase.GenericDestination`1<int?>",
                nullableIntGenericDestination,
                "global::TestCase.Morphant.Generated." +
                "GenericDestinationMorphantTemplate<int?>"),
            ExpectedGeneratedExtension(
                "TestCase.GenericDestination`1<string>",
                stringGenericDestination,
                "global::TestCase.Morphant.Generated." +
                "GenericDestinationMorphantTemplate<string>"),
            ExpectedDirectExtension(
                "System.Int32",
                "int",
                "int"),
            ExpectedDirectExtension(
                "System.Nullable`1<int>",
                "int?",
                "int?"));
    }

    [Test]
    public async Task Does_not_let_destinations_without_template_surface_affect_supported_destinations()
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

    public sealed class GeneratedDestination
    {
    }

    public delegate void DelegateDestination();

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, (int Id, string Name)>();

            builder.Map<Source, GeneratedDestination>()
                .Template(static _ => new());

            builder.Map<Source, GeneratedDestination[]>();

            builder.Map<Source, int>()
                .Template(static _ => 0);

            builder.Map<Source, DelegateDestination>();
        }
    }
}

namespace TestCase.Morphant.Generated
{
    internal sealed record GeneratedDestinationMorphantTemplate;
}
""";

        const string generatedDestination =
            "global::TestCase.GeneratedDestination";

        await TemplateExtensionGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            ExpectedGeneratedExtension(
                "TestCase.GeneratedDestination",
                generatedDestination,
                "global::TestCase.Morphant.Generated." +
                "GeneratedDestinationMorphantTemplate"),
            ExpectedDirectExtension(
                "System.Int32",
                "int",
                "int"));
    }

    [Test]
    public async Task Generates_distinct_extensions_for_same_simple_name_in_different_namespaces()
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
    public sealed class Destination
    {
    }
}

namespace Second
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
        builder.Map<Source, First.Destination>()
            .Template(static _ => new());

        builder.Map<Source, Second.Destination>()
            .Template(static _ => new());
    }
}

namespace First.Morphant.Generated
{
    internal sealed record DestinationMorphantTemplate;
}

namespace Second.Morphant.Generated
{
    internal sealed record DestinationMorphantTemplate;
}
""";

        const string firstDestination =
            "global::First.Destination";

        const string secondDestination =
            "global::Second.Destination";

        await TemplateExtensionGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            ExpectedGeneratedExtension(
                "First.Destination",
                firstDestination,
                "global::First.Morphant.Generated." +
                "DestinationMorphantTemplate"),
            ExpectedGeneratedExtension(
                "Second.Destination",
                secondDestination,
                "global::Second.Morphant.Generated." +
                "DestinationMorphantTemplate"));
    }

    [Test]
    public async Task Generates_one_extension_for_repeated_usages_across_sources_and_mappers()
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

    public sealed class Destination
    {
    }

    [MorphantMapper]
    public partial class FirstMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<FirstSource, Destination>(MappingMode.MapNew)
                .Template(static _ => new());

            builder.Map<FirstSource, Destination>();
        }
    }

    [MorphantMapper]
    public partial class SecondMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<SecondSource, Destination>(MappingMode.MapExisting)
                .Template(static _ => new());
        }
    }
}

namespace TestCase.Morphant.Generated
{
    internal sealed record DestinationMorphantTemplate;
}
""";

        const string destination = "global::TestCase.Destination";

        await TemplateExtensionGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            ExpectedGeneratedExtension(
                "TestCase.Destination",
                destination,
                "global::TestCase.Morphant.Generated." +
                "DestinationMorphantTemplate"));
    }

    [Test]
    public async Task Deduplicates_top_level_signature_equivalents_independently_of_usage_order()
    {
        // lang=c#
        const string forwardMapStatements =
"""
            builder.Map<FirstSource, Destination>();
            builder.Map<SecondSource, Destination?>();
            builder.Map<FirstSource, string>();
            builder.Map<SecondSource, string?>();
            builder.Map<FirstSource, object>();
            builder.Map<SecondSource, dynamic>();
""";

        // lang=c#
        const string reverseMapStatements =
"""
            builder.Map<SecondSource, dynamic>();
            builder.Map<FirstSource, object>();
            builder.Map<SecondSource, string?>();
            builder.Map<FirstSource, string>();
            builder.Map<SecondSource, Destination?>();
            builder.Map<FirstSource, Destination>();
""";

        const string destination = "global::TestCase.Destination";

        var expectedSources = new[]
        {
            ExpectedGeneratedExtension(
                "TestCase.Destination",
                destination,
                "global::TestCase.Morphant.Generated." +
                "DestinationMorphantTemplate"),
            ExpectedDirectExtension(
                "System.Object",
                "object",
                "object?"),
            ExpectedDirectExtension(
                "System.String",
                "string",
                "string?")
        };

        await TemplateExtensionGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            BuildTopLevelEquivalentSource(forwardMapStatements),
            expectedSources);

        await TemplateExtensionGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            BuildTopLevelEquivalentSource(reverseMapStatements),
            expectedSources);
    }

    [Test]
    public async Task Deduplicates_constructed_signature_equivalents_independently_of_usage_order()
    {
        // lang=c#
        const string forwardMapStatements =
"""
            builder.Map<Source, AliasDestination<int>>();
            builder.Map<Source, AliasDestination<global::System.Int32>>();
            builder.Map<Source, DynamicDestination<object>>();
            builder.Map<Source, DynamicDestination<dynamic>>();
            builder.Map<Source, NativeDestination<nint>>();
            builder.Map<Source, NativeDestination<global::System.IntPtr>>();
            builder.Map<Source, NullableDestination<string>>();
            builder.Map<Source, NullableDestination<string?>>();
            builder.Map<Source, TupleDestination<(int, string)>>();
            builder.Map<Source, TupleDestination<(int Id, string Name)>>();
            builder.Map<Source, ValueTupleDestination<(int, string)>>();
            builder.Map<Source, ValueTupleDestination<global::System.ValueTuple<int, string>>>();
            builder.Map<Source, NestedDestination<Wrapper<object>>>();
            builder.Map<Source, NestedDestination<Wrapper<dynamic>>>();
            builder.Map<Source, ArrayDestination<string[]>>();
            builder.Map<Source, ArrayDestination<string?[]>>();
            builder.Map<Source, Outer<string>.ContainedDestination>();
            builder.Map<Source, Outer<string?>.ContainedDestination>();
""";

        // lang=c#
        const string reverseMapStatements =
"""
            builder.Map<Source, Outer<string?>.ContainedDestination>();
            builder.Map<Source, Outer<string>.ContainedDestination>();
            builder.Map<Source, ArrayDestination<string?[]>>();
            builder.Map<Source, ArrayDestination<string[]>>();
            builder.Map<Source, NestedDestination<Wrapper<dynamic>>>();
            builder.Map<Source, NestedDestination<Wrapper<object>>>();
            builder.Map<Source, ValueTupleDestination<global::System.ValueTuple<int, string>>>();
            builder.Map<Source, ValueTupleDestination<(int, string)>>();
            builder.Map<Source, TupleDestination<(int Id, string Name)>>();
            builder.Map<Source, TupleDestination<(int, string)>>();
            builder.Map<Source, NullableDestination<string?>>();
            builder.Map<Source, NullableDestination<string>>();
            builder.Map<Source, NativeDestination<global::System.IntPtr>>();
            builder.Map<Source, NativeDestination<nint>>();
            builder.Map<Source, DynamicDestination<dynamic>>();
            builder.Map<Source, DynamicDestination<object>>();
            builder.Map<Source, AliasDestination<global::System.Int32>>();
            builder.Map<Source, AliasDestination<int>>();
""";

        var expectedSources = new[]
        {
            ExpectedConstructedExtension(
                "AliasDestination",
                "int"),
            ExpectedConstructedExtension(
                "ArrayDestination",
                "string[]"),
            ExpectedConstructedExtension(
                "DynamicDestination",
                "object"),
            ExpectedConstructedExtension(
                "NestedDestination",
                "global::TestCase.Wrapper<object>"),
            ExpectedConstructedExtension(
                "NativeDestination",
                "nint"),
            ExpectedConstructedExtension(
                "NullableDestination",
                "string"),
            ExpectedConstructedExtension(
                "TupleDestination",
                "(int, string)"),
            ExpectedConstructedExtension(
                "ValueTupleDestination",
                "(int, string)"),
            ExpectedGeneratedExtension(
                "TestCase.Outer`1+ContainedDestination<string>",
                "global::TestCase.Outer<string>.ContainedDestination",
                "global::TestCase.Morphant.Generated.Outer1Scope." +
                "ContainedDestinationMorphantTemplate<string>")
        };

        await TemplateExtensionGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            BuildConstructedEquivalentSource(forwardMapStatements),
            expectedSources);

        await TemplateExtensionGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            BuildConstructedEquivalentSource(reverseMapStatements),
            expectedSources);
    }

    [Test]
    public async Task Uses_unique_hint_names_for_sanitized_identity_collisions()
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
    public sealed class B_C
    {
    }
}

namespace A_B
{
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

namespace A.Morphant.Generated
{
    internal sealed record B_CMorphantTemplate;
}

namespace A_B.Morphant.Generated
{
    internal sealed record CMorphantTemplate;
}
""";

        await TemplateExtensionGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            ExpectedGeneratedExtension(
                "A.B_C",
                "global::A.B_C",
                "global::A.Morphant.Generated.B_CMorphantTemplate"),
            ExpectedGeneratedExtension(
                "A_B.C",
                "global::A_B.C",
                "global::A_B.Morphant.Generated.CMorphantTemplate"));
    }

    [Test]
    public async Task Uses_unique_hint_names_for_case_insensitive_collisions()
    {
        // lang=c#
        const string forwardMapStatements =
"""
            builder.Map<Source, Destination>();
            builder.Map<Source, destination>();
""";

        // lang=c#
        const string reverseMapStatements =
"""
            builder.Map<Source, destination>();
            builder.Map<Source, Destination>();
""";

        const string firstDestination =
            "global::TestCase.Destination";

        const string secondDestination =
            "global::TestCase.destination";

        var expectedSources = new[]
        {
            ExpectedGeneratedExtension(
                "TestCase.Destination",
                firstDestination,
                "global::TestCase.Morphant.Generated." +
                "DestinationMorphantTemplate"),
            ExpectedGeneratedExtension(
                "TestCase.destination",
                secondDestination,
                "global::TestCase.Morphant.Generated." +
                "destinationMorphantTemplate",
                "TestCase_destination__c52cc9889f9bc467")
        };

        await TemplateExtensionGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            BuildCaseInsensitiveCollisionSource(forwardMapStatements),
            expectedSources);

        await TemplateExtensionGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            BuildCaseInsensitiveCollisionSource(reverseMapStatements),
            expectedSources);
    }

    private static string BuildTopLevelEquivalentSource(
        string mapStatements)
    {
        // lang=c#
        return $$"""
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

                     public sealed class Destination
                     {
                     }

                     [MorphantMapper]
                     public partial class TestMapper : TypeMapper
                     {
                         protected override void Configure(MapperBuilder builder)
                         {
                 {{mapStatements}}
                         }
                     }
                 }

                 namespace TestCase.Morphant.Generated
                 {
                     internal sealed record DestinationMorphantTemplate;
                 }
                 """;
    }

    private static string BuildConstructedEquivalentSource(
        string mapStatements)
    {
        // lang=c#
        return $$"""
                 #pragma warning disable CS1591
                 #nullable enable

                 using Morphant;

                 namespace TestCase
                 {
                     public sealed class Source
                     {
                     }

                     public sealed class AliasDestination<T>
                     {
                     }

                     public sealed class Wrapper<T>
                     {
                     }

                     public sealed class ArrayDestination<T>
                     {
                     }

                     public sealed class DynamicDestination<T>
                     {
                     }

                     public sealed class NestedDestination<T>
                     {
                     }

                     public sealed class NativeDestination<T>
                     {
                     }

                     public sealed class NullableDestination<T>
                     {
                     }

                     public sealed class TupleDestination<T>
                     {
                     }

                     public sealed class ValueTupleDestination<T>
                     {
                     }

                     public sealed class Outer<T>
                     {
                         public sealed class ContainedDestination
                         {
                         }
                     }

                     [MorphantMapper]
                     public partial class TestMapper : TypeMapper
                     {
                         protected override void Configure(MapperBuilder builder)
                         {
                 {{mapStatements}}
                         }
                     }
                 }

                 namespace TestCase.Morphant.Generated
                 {
                     internal sealed record AliasDestinationMorphantTemplate<T>;

                     internal sealed record ArrayDestinationMorphantTemplate<T>;

                     internal sealed record DynamicDestinationMorphantTemplate<T>;

                     internal sealed record NestedDestinationMorphantTemplate<T>;

                     internal sealed record NativeDestinationMorphantTemplate<T>;

                     internal sealed record NullableDestinationMorphantTemplate<T>;

                     internal sealed record TupleDestinationMorphantTemplate<T>;

                     internal sealed record ValueTupleDestinationMorphantTemplate<T>;
                 }

                 namespace TestCase.Morphant.Generated.Outer1Scope
                 {
                     internal sealed record ContainedDestinationMorphantTemplate<T>;
                 }
                 """;
    }

    private static string BuildCaseInsensitiveCollisionSource(
        string mapStatements)
    {
        // lang=c#
        return $$"""
                 #pragma warning disable CS1591
                 #nullable enable

                 using Morphant;

                 namespace TestCase
                 {
                     public sealed class Source
                     {
                     }

                     public sealed class Destination
                     {
                     }

                     public sealed class destination
                     {
                     }

                     [MorphantMapper]
                     public partial class TestMapper : TypeMapper
                     {
                         protected override void Configure(MapperBuilder builder)
                         {
                 {{mapStatements}}
                         }
                     }
                 }

                 namespace TestCase.Morphant.Generated
                 {
                     internal sealed record DestinationMorphantTemplate;

                     internal sealed record destinationMorphantTemplate;
                 }
                 """;
    }

    private static (string FileName, string Content)
        ExpectedConstructedExtension(
            string destinationName,
            string typeArgument)
    {
        var usageIdentity =
            $"TestCase.{destinationName}`1<{typeArgument}>";

        var destinationType =
            $"global::TestCase.{destinationName}<{typeArgument}>";

        var templateType =
            "global::TestCase.Morphant.Generated." +
            $"{destinationName}MorphantTemplate<{typeArgument}>";

        return ExpectedGeneratedExtension(
            usageIdentity,
            destinationType,
            templateType);
    }

    private static (string FileName, string Content)
        ExpectedGeneratedExtension(
            string usageIdentity,
            string destinationType,
            string templateResultType,
            string? hintNamePart = null)
    {
        return ExpectedExtension(
            usageIdentity,
            destinationType,
            destinationType + "?",
            templateResultType,
            hintNamePart);
    }

    private static (string FileName, string Content)
        ExpectedDirectExtension(
            string usageIdentity,
            string destinationType,
            string existingDestinationType)
    {
        return ExpectedExtension(
            usageIdentity,
            destinationType,
            existingDestinationType,
            destinationType);
    }

    private static string ExpectedHintNamePart(string usageIdentity)
    {
        // Literal contract: do not derive these values with generator code.
        return usageIdentity switch
        {
            "A.B_C" =>
                "A_B_C",
            "A_B.C" =>
                "A_B_C__a143b4740e1429ca",
            "First.Destination" =>
                "First_Destination",
            "Second.Destination" =>
                "Second_Destination",
            "System.Int32" =>
                "System_Int32",
            "System.Nullable`1<int>" =>
                "System_Nullable_1_int_",
            "System.Object" =>
                "System_Object",
            "System.String" =>
                "System_String",
            "TestCase.AliasDestination`1<int>" =>
                "TestCase_AliasDestination_1_int_",
            "TestCase.ArrayDestination`1<string[]>" =>
                "TestCase_ArrayDestination_1_string___",
            "TestCase.ClassDestination" =>
                "TestCase_ClassDestination",
            "TestCase.Destination" =>
                "TestCase_Destination",
            "TestCase.DynamicDestination`1<object>" =>
                "TestCase_DynamicDestination_1_object_",
            "TestCase.GeneratedDestination" =>
                "TestCase_GeneratedDestination",
            "TestCase.GenericDestination`1<int>" =>
                "TestCase_GenericDestination_1_int_",
            "TestCase.GenericDestination`1<int?>" =>
                "TestCase_GenericDestination_1_int__",
            "TestCase.GenericDestination`1<string>" =>
                "TestCase_GenericDestination_1_string_",
            "TestCase.NativeDestination`1<nint>" =>
                "TestCase_NativeDestination_1_nint_",
            "TestCase.NestedDestination`1<global::TestCase.Wrapper<object>>" =>
                "TestCase_NestedDestination_1_global__TestCase_Wrapper_object__",
            "TestCase.NullableDestination`1<string>" =>
                "TestCase_NullableDestination_1_string_",
            "TestCase.Outer`1+ContainedDestination<string>" =>
                "TestCase_Outer_1_ContainedDestination_string_",
            "TestCase.TupleDestination`1<(int, string)>" =>
                "TestCase_TupleDestination_1__int__string__",
            "TestCase.ValueTupleDestination`1<(int, string)>" =>
                "TestCase_ValueTupleDestination_1__int__string__",
            "TestCase.destination" =>
                "TestCase_destination",
            _ => throw new ArgumentOutOfRangeException(
                nameof(usageIdentity),
                usageIdentity,
                "Unexpected usage identity.")
        };
    }

    private static (string FileName, string Content) ExpectedExtension(
        string usageIdentity,
        string destinationType,
        string existingDestinationType,
        string templateResultType,
        string? hintNamePart = null)
    {
        hintNamePart ??=
            ExpectedHintNamePart(usageIdentity);

        var fileName =
            $"Morphant.Generated.TemplateExtension.{hintNamePart}.g.cs";

        // lang=c#
        var content = $$"""
                        // <auto-generated />
                        #nullable enable

                        namespace Morphant
                        {
                            internal static partial class MorphantGeneratedTemplateExtensions
                            {
                                /// <summary>
                                /// Configures a mapping template.
                                /// </summary>
                                /// <typeparam name="TSource">The source type.</typeparam>
                                /// <param name="builder">The mapping builder to configure.</param>
                                /// <param name="template">
                                /// A lambda expression that receives the source value and describes the mapping.
                                /// </param>
                                /// <returns>The <paramref name="builder"/> instance.</returns>
                                public static global::Morphant.MapperBuilder<TSource, {{destinationType}}> Template<TSource>(
                                    this global::Morphant.MapperBuilder<TSource, {{destinationType}}> builder,
                                    global::System.Func<TSource, {{templateResultType}}> template)
                                    => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

                                /// <summary>
                                /// Configures a mapping template that depends on the destination's previous state.
                                /// </summary>
                                /// <typeparam name="TSource">The source type.</typeparam>
                                /// <param name="builder">The mapping builder to configure.</param>
                                /// <param name="template">
                                /// A lambda expression that receives the source value and the previous destination value
                                /// and describes the mapping.
                                /// </param>
                                /// <returns>The <paramref name="builder"/> instance.</returns>
                                public static global::Morphant.MapperBuilder<TSource, {{destinationType}}> Template<TSource>(
                                    this global::Morphant.MapperBuilder<TSource, {{destinationType}}> builder,
                                    global::System.Func<TSource, {{existingDestinationType}}, {{templateResultType}}> template)
                                    => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();
                            }
                        }
                        """;

        return (fileName, content);
    }
}
