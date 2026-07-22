using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TemplateExtensionTests;

internal sealed class TemplateExtensionDestinationSupportTests
{
    [TestCase("object", "object", "System.Object")]
    [TestCase("object?", "object?", "System.Object")]
    [TestCase("string", "string", "System.String")]
    [TestCase("string?", "string?", "System.String")]
    [TestCase("bool", "bool", "System.Boolean")]
    [TestCase("bool?", "bool?", "System.Nullable`1<bool>")]
    [TestCase("char", "char", "System.Char")]
    [TestCase("char?", "char?", "System.Nullable`1<char>")]
    [TestCase("sbyte", "sbyte", "System.SByte")]
    [TestCase("sbyte?", "sbyte?", "System.Nullable`1<sbyte>")]
    [TestCase("byte", "byte", "System.Byte")]
    [TestCase("byte?", "byte?", "System.Nullable`1<byte>")]
    [TestCase("short", "short", "System.Int16")]
    [TestCase("short?", "short?", "System.Nullable`1<short>")]
    [TestCase("ushort", "ushort", "System.UInt16")]
    [TestCase("ushort?", "ushort?", "System.Nullable`1<ushort>")]
    [TestCase("int", "int", "System.Int32")]
    [TestCase("int?", "int?", "System.Nullable`1<int>")]
    [TestCase("uint", "uint", "System.UInt32")]
    [TestCase("uint?", "uint?", "System.Nullable`1<uint>")]
    [TestCase("long", "long", "System.Int64")]
    [TestCase("long?", "long?", "System.Nullable`1<long>")]
    [TestCase("ulong", "ulong", "System.UInt64")]
    [TestCase("ulong?", "ulong?", "System.Nullable`1<ulong>")]
    [TestCase("nint", "nint", "System.IntPtr")]
    [TestCase("nint?", "nint?", "System.Nullable`1<nint>")]
    [TestCase("nuint", "nuint", "System.UIntPtr")]
    [TestCase("nuint?", "nuint?", "System.Nullable`1<nuint>")]
    [TestCase("float", "float", "System.Single")]
    [TestCase("float?", "float?", "System.Nullable`1<float>")]
    [TestCase("double", "double", "System.Double")]
    [TestCase("double?", "double?", "System.Nullable`1<double>")]
    [TestCase("decimal", "decimal", "System.Decimal")]
    [TestCase("decimal?", "decimal?", "System.Nullable`1<decimal>")]
    public async Task Generates_direct_extension_for_predefined_destination(
        string destinationType,
        string expectedType,
        string usageIdentity)
    {
        var expectedExistingDestinationType = expectedType switch
        {
            "object" => "object?",
            "string" => "string?",
            _ => expectedType
        };

        await RunDirectTemplateDestination(
            destinationType,
            expectedType,
            usageIdentity,
            expectedExistingDestinationType);
    }

    [TestCase("global::System.Guid", "global::System.Guid", "System.Guid")]
    [TestCase("global::System.Guid?", "global::System.Guid?", "System.Nullable`1<global::System.Guid>")]
    [TestCase("global::System.DateTime", "global::System.DateTime", "System.DateTime")]
    [TestCase("global::System.DateTime?", "global::System.DateTime?", "System.Nullable`1<global::System.DateTime>")]
    [TestCase("global::System.DateTimeOffset", "global::System.DateTimeOffset", "System.DateTimeOffset")]
    [TestCase("global::System.DateTimeOffset?", "global::System.DateTimeOffset?", "System.Nullable`1<global::System.DateTimeOffset>")]
    [TestCase("global::System.DateOnly", "global::System.DateOnly", "System.DateOnly")]
    [TestCase("global::System.DateOnly?", "global::System.DateOnly?", "System.Nullable`1<global::System.DateOnly>")]
    [TestCase("global::System.TimeOnly", "global::System.TimeOnly", "System.TimeOnly")]
    [TestCase("global::System.TimeOnly?", "global::System.TimeOnly?", "System.Nullable`1<global::System.TimeOnly>")]
    [TestCase("global::System.TimeSpan", "global::System.TimeSpan", "System.TimeSpan")]
    [TestCase("global::System.TimeSpan?", "global::System.TimeSpan?", "System.Nullable`1<global::System.TimeSpan>")]
    [TestCase("global::System.Half", "global::System.Half", "System.Half")]
    [TestCase("global::System.Half?", "global::System.Half?", "System.Nullable`1<global::System.Half>")]
    [TestCase("global::System.Int128", "global::System.Int128", "System.Int128")]
    [TestCase("global::System.Int128?", "global::System.Int128?", "System.Nullable`1<global::System.Int128>")]
    [TestCase("global::System.UInt128", "global::System.UInt128", "System.UInt128")]
    [TestCase("global::System.UInt128?", "global::System.UInt128?", "System.Nullable`1<global::System.UInt128>")]
    [TestCase("global::System.Uri", "global::System.Uri", "System.Uri")]
    [TestCase("global::System.Uri?", "global::System.Uri?", "System.Uri")]
    [TestCase("global::System.Version", "global::System.Version", "System.Version")]
    [TestCase("global::System.Version?", "global::System.Version?", "System.Version")]
    [TestCase("global::System.Numerics.BigInteger", "global::System.Numerics.BigInteger", "System.Numerics.BigInteger")]
    [TestCase("global::System.Numerics.BigInteger?", "global::System.Numerics.BigInteger?", "System.Nullable`1<global::System.Numerics.BigInteger>")]
    [TestCase("global::System.Numerics.Complex", "global::System.Numerics.Complex", "System.Numerics.Complex")]
    [TestCase("global::System.Numerics.Complex?", "global::System.Numerics.Complex?", "System.Nullable`1<global::System.Numerics.Complex>")]
    [TestCase("global::System.Text.Rune", "global::System.Text.Rune", "System.Text.Rune")]
    [TestCase("global::System.Text.Rune?", "global::System.Text.Rune?", "System.Nullable`1<global::System.Text.Rune>")]
    [TestCase("global::System.Index", "global::System.Index", "System.Index")]
    [TestCase("global::System.Index?", "global::System.Index?", "System.Nullable`1<global::System.Index>")]
    [TestCase("global::System.Range", "global::System.Range", "System.Range")]
    [TestCase("global::System.Range?", "global::System.Range?", "System.Nullable`1<global::System.Range>")]
    public async Task Generates_direct_extension_for_supported_bcl_destination(
        string destinationType,
        string expectedType,
        string usageIdentity)
    {
        var expectedExistingDestinationType = expectedType switch
        {
            "global::System.Uri" => "global::System.Uri?",
            "global::System.Version" => "global::System.Version?",
            _ => expectedType
        };

        await RunDirectTemplateDestination(
            destinationType,
            expectedType,
            usageIdentity,
            expectedExistingDestinationType);
    }

    [TestCase("Destination", "global::TestCase.Destination", "TestCase.Destination")]
    [TestCase("Destination?", "global::TestCase.Destination?", "System.Nullable`1<global::TestCase.Destination>")]
    public async Task Generates_direct_extension_for_enum_destination(
        string destinationType,
        string expectedType,
        string usageIdentity)
    {
        // lang=c#
        const string destinationDeclaration =
"""
    public enum Destination
    {
        None
    }
""";

        await RunDirectTemplateDestination(
            destinationType,
            expectedType,
            usageIdentity,
            expectedType,
            destinationDeclaration);
    }

    [Test]
    public async Task Reuses_object_extension_for_dynamic_destination()
    {
        await RunDirectTemplateDestination(
            "dynamic",
            "object",
            "System.Object",
            "object?");
    }

    [Test]
    public async Task Does_not_generate_extension_for_tuple_destination()
    {
        await RunWithoutExtension("(int Id, string Name)");
    }

    [TestCase("Destination[]")]
    [TestCase("Destination[,]")]
    [TestCase("Destination[][]")]
    public async Task Does_not_generate_extension_for_array_destination(
        string destinationType)
    {
        // lang=c#
        const string destinationDeclaration =
"""
    public sealed class Destination
    {
    }
""";

        await RunWithoutExtension(
            destinationType,
            destinationDeclaration);
    }

    [Test]
    public async Task Does_not_generate_extension_for_delegate_destination()
    {
        await RunWithoutExtension(
            "Destination",
            "    public delegate void Destination();");
    }

    [Test]
    public async Task Generates_extension_for_nullable_custom_struct_destination()
    {
        // lang=c#
        const string destinationDeclaration =
"""
    public struct Destination
    {
    }
""";

        await TemplateExtensionGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            BuildSource(
                destinationDeclaration,
                "builder.Map<Source, Destination?>();",
                NonGenericTemplateStub),
            (
                "Morphant.TemplateExtensions." +
                HintNameHelper.ToHintNamePart(
                    "System.Nullable`1<global::TestCase.Destination>") +
                ".g.cs",
                BuildExpectedGeneratedExtension(
                    "global::TestCase.Destination?",
                    "global::TestCase.Morphant.Generated." +
                    "DestinationMorphantTemplate?",
                    "global::TestCase.Destination?")
            ));
    }

    [TestCase("public class Destination", true, LanguageVersion.CSharp9)]
    [TestCase("internal sealed class Destination", true, LanguageVersion.CSharp9)]
    [TestCase("public sealed partial class Destination", true, LanguageVersion.CSharp9)]
    [TestCase("public abstract class Destination", true, LanguageVersion.CSharp9)]
    [TestCase("public sealed record Destination", true, LanguageVersion.CSharp9)]
    [TestCase("public struct Destination", false, LanguageVersion.CSharp9)]
    [TestCase("public readonly struct Destination", false, LanguageVersion.CSharp9)]
    [TestCase("public interface Destination", true, LanguageVersion.CSharp9)]
    [TestCase("public record struct Destination", false, LanguageVersion.CSharp10)]
    [TestCase("public readonly record struct Destination", false, LanguageVersion.CSharp10)]
    public async Task Generates_extension_for_non_generic_destination_kind(
        string destinationTypeDeclaration,
        bool isReferenceType,
        LanguageVersion languageVersion)
    {
        var destinationDeclaration = $$"""
                                           {{destinationTypeDeclaration}}
                                           {
                                           }
                                       """;

        await TemplateExtensionGeneratorTest.RunAndAssert(
            languageVersion,
            BuildSource(
                destinationDeclaration,
                "builder.Map<Source, Destination>();",
                NonGenericTemplateStub),
            (
                "Morphant.TemplateExtensions." +
                "TestCase_Destination.g.cs",
                BuildExpectedGeneratedExtension(
                    "global::TestCase.Destination",
                    "global::TestCase.Morphant.Generated." +
                    "DestinationMorphantTemplate",
                    isReferenceType
                        ? "global::TestCase.Destination?"
                        : "global::TestCase.Destination")
            ));
    }

    [Test]
    public async Task Preserves_nullable_annotation_on_generated_destination()
    {
        // lang=c#
        const string destinationDeclaration =
"""
    public sealed class Destination
    {
    }
""";

        await TemplateExtensionGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            BuildSource(
                destinationDeclaration,
                "builder.Map<Source, Destination?>();",
                NonGenericTemplateStub),
            (
                "Morphant.TemplateExtensions." +
                "TestCase_Destination.g.cs",
                BuildExpectedGeneratedExtension(
                    "global::TestCase.Destination?",
                    "global::TestCase.Morphant.Generated." +
                    "DestinationMorphantTemplate?",
                    "global::TestCase.Destination?")
            ));
    }

    [Test]
    public async Task Generates_extension_for_accessible_nested_destination()
    {
        // lang=c#
        const string destinationDeclaration =
"""
    public static class Container
    {
        public sealed class Destination
        {
        }
    }
""";

        // lang=c#
        const string templateStub =
"""
namespace TestCase.Morphant.Generated.ContainerScope
{
    internal sealed record DestinationMorphantTemplate;
}
""";

        await TemplateExtensionGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            BuildSource(
                destinationDeclaration,
                "builder.Map<Source, Container.Destination>();",
                templateStub),
            (
                "Morphant.TemplateExtensions." +
                HintNameHelper.ToHintNamePart(
                    "TestCase.Container+Destination") +
                ".g.cs",
                BuildExpectedGeneratedExtension(
                    "global::TestCase.Container.Destination",
                    "global::TestCase.Morphant.Generated." +
                    "ContainerScope.DestinationMorphantTemplate",
                    "global::TestCase.Container.Destination?")
            ));
    }

    [Test]
    public async Task Does_not_generate_extension_for_inaccessible_nested_destination()
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

        await TemplateExtensionGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source);
    }

    [Test]
    public async Task Does_not_generate_extension_for_file_local_destination()
    {
        // lang=c#
        const string destinationDeclaration =
"""
    file sealed class Destination
    {
    }
""";

        await RunWithoutExtension(
            "Destination",
            destinationDeclaration,
            LanguageVersion.CSharp11);
    }

    [Test]
    public async Task Preserves_nullable_arguments_in_constructed_generic_extension()
    {
        // lang=c#
        const string destinationDeclaration =
"""
    public sealed class User
    {
    }

    public sealed class Destination<T>
    {
    }
""";

        await TemplateExtensionGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            BuildSource(
                destinationDeclaration,
                "builder.Map<Source, Destination<User?>>();",
                GenericTemplateStub),
            (
                "Morphant.TemplateExtensions." +
                "TestCase_Destination_1_global__TestCase_User____" +
                "e161f976f3b0adf9.g.cs",
                ExpectedNullableGenericExtension
            ));
    }

    [TestCase("public sealed class Destination<T>", true, LanguageVersion.CSharp9)]
    [TestCase("public struct Destination<T>", false, LanguageVersion.CSharp9)]
    [TestCase("public sealed record Destination<T>", true, LanguageVersion.CSharp9)]
    [TestCase("public abstract class Destination<T>", true, LanguageVersion.CSharp9)]
    [TestCase("public interface Destination<T>", true, LanguageVersion.CSharp9)]
    [TestCase("public record struct Destination<T>", false, LanguageVersion.CSharp10)]
    public async Task Generates_extension_for_generic_destination_kind(
        string destinationTypeDeclaration,
        bool isReferenceType,
        LanguageVersion languageVersion)
    {
        var destinationDeclaration = $$"""
                                           {{destinationTypeDeclaration}}
                                           {
                                           }
                                       """;

        await TemplateExtensionGeneratorTest.RunAndAssert(
            languageVersion,
            BuildSource(
                destinationDeclaration,
                "builder.Map<Source, Destination<int>>();",
                GenericTemplateStub),
            (
                "Morphant.TemplateExtensions." +
                "TestCase_Destination_1_int___" +
                "a212525a5607429d.g.cs",
                BuildExpectedGeneratedExtension(
                    "global::TestCase.Destination<int>",
                    "global::TestCase.Morphant.Generated." +
                    "DestinationMorphantTemplate<int>",
                    isReferenceType
                        ? "global::TestCase.Destination<int>?"
                        : "global::TestCase.Destination<int>")
            ));
    }

    [Test]
    public async Task Generates_one_extension_per_constructed_generic_usage()
    {
        // lang=c#
        const string destinationDeclaration =
"""
    public sealed class Destination<T>
    {
    }
""";

        // lang=c#
        const string mapStatements =
"""
            builder.Map<Source, Destination<int>>();
            builder.Map<Source, Destination<string>>();
            builder.Map<Source, Destination<int>>();
""";

        await TemplateExtensionGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            BuildSource(
                destinationDeclaration,
                mapStatements,
                GenericTemplateStub),
            (
                "Morphant.TemplateExtensions." +
                "TestCase_Destination_1_int___" +
                "a212525a5607429d.g.cs",
                ExpectedIntGenericExtension
            ),
            (
                "Morphant.TemplateExtensions." +
                "TestCase_Destination_1_string___" +
                "887c5e6840177255.g.cs",
                ExpectedStringGenericExtension
            ));
    }

    [Test]
    public async Task Includes_containing_and_nested_generic_arguments()
    {
        // lang=c#
        const string destinationDeclaration =
"""
    public sealed class Outer<TOuter>
    {
        public sealed class Destination<TValue>
        {
        }
    }
""";

        await TemplateExtensionGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            BuildSource(
                destinationDeclaration,
                "builder.Map<Source, " +
                "Outer<int>.Destination<string?>>();",
                NestedGenericTemplateStub),
            (
                "Morphant.TemplateExtensions." +
                "TestCase_Outer_1_Destination_1_int__string____" +
                "1f963160e176dad2.g.cs",
                ExpectedNestedGenericExtension
            ));
    }

    [Test]
    public async Task Generates_generic_extension_for_destination_from_referenced_assembly()
    {
        // lang=c#
        const string templateStub =
"""
namespace Morphant.Generator.UnitTests.TestAssets.Morphant.Generated
{
    internal sealed record ReferencedGenericDestinationMorphantTemplate<T>;
}
""";

        const string destinationType =
            "global::Morphant.Generator.UnitTests.TestAssets." +
            "ReferencedGenericDestination<string>";

        const string templateType =
            "global::Morphant.Generator.UnitTests.TestAssets." +
            "Morphant.Generated." +
            "ReferencedGenericDestinationMorphantTemplate<string>";

        const string usageIdentity =
            "Morphant.Generator.UnitTests.TestAssets." +
            "ReferencedGenericDestination`1<string>";

        await TemplateExtensionGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            BuildSource(
                destinationDeclaration: string.Empty,
                $"builder.Map<Source, {destinationType}>();",
                templateStub),
            new[]
            {
                typeof(
                    Morphant.Generator.UnitTests.TestAssets
                        .ReferencedGenericDestination<>).Assembly
            },
            (
                "Morphant.TemplateExtensions." +
                HintNameHelper.ToHintNamePart(usageIdentity) +
                ".g.cs",
                BuildExpectedGeneratedExtension(
                    destinationType,
                    templateType,
                    destinationType + "?")
            ));
    }

    [Test]
    public async Task Does_not_generate_extension_for_open_constructed_destination()
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

        await TemplateExtensionGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source);
    }

    [Test]
    public async Task Does_not_generate_extension_for_type_parameter_destination()
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

        await TemplateExtensionGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source);
    }

    [Test]
    public async Task Does_not_generate_extension_for_shadowed_type_parameter()
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

        await TemplateExtensionGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source);
    }

    private static Task RunDirectTemplateDestination(
        string destinationType,
        string expectedType,
        string usageIdentity,
        string expectedExistingDestinationType,
        string destinationDeclaration = "")
    {
        var mapStatements = $$"""
                                    builder.Map<Source, {{destinationType}}>()
                                        .Template(static _ => default!);

                                    builder.Map<Source, {{destinationType}}>()
                                        .Template(static (_, destination) => destination!);
                               """;

        return TemplateExtensionGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            BuildSource(
                destinationDeclaration,
                mapStatements),
            (
                "Morphant.TemplateExtensions." +
                HintNameHelper.ToHintNamePart(usageIdentity) +
                ".g.cs",
                BuildExpectedDirectExtension(
                    expectedType,
                    expectedExistingDestinationType)
            ));
    }

    private static Task RunWithoutExtension(
        string destinationType,
        string destinationDeclaration = "",
        LanguageVersion languageVersion = LanguageVersion.CSharp9)
    {
        return TemplateExtensionGeneratorTest.RunAndAssert(
            languageVersion,
            BuildSource(
                destinationDeclaration,
                $"builder.Map<Source, {destinationType}>();"));
    }

    private static string BuildExpectedDirectExtension(
        string type,
        string existingDestinationType)
    {
        return $$"""
                 // <auto-generated />
                 #nullable enable

                 namespace Morphant
                 {
                     internal static partial class MorphantGeneratedTemplateExtensions
                     {
                         public static global::Morphant.MapperBuilder<TSource, {{type}}> Template<TSource>(
                             this global::Morphant.MapperBuilder<TSource, {{type}}> builder,
                             global::System.Func<TSource, {{type}}> template)
                             => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

                         public static global::Morphant.MapperBuilder<TSource, {{type}}> Template<TSource>(
                             this global::Morphant.MapperBuilder<TSource, {{type}}> builder,
                             global::System.Func<TSource, {{existingDestinationType}}, {{type}}> template)
                             => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();
                     }
                 }
                 """;
    }

    private static string BuildExpectedGeneratedExtension(
        string destinationType,
        string templateType,
        string existingDestinationType)
    {
        return $$"""
                 // <auto-generated />
                 #nullable enable

                 namespace Morphant
                 {
                     internal static partial class MorphantGeneratedTemplateExtensions
                     {
                         public static global::Morphant.MapperBuilder<TSource, {{destinationType}}> Template<TSource>(
                             this global::Morphant.MapperBuilder<TSource, {{destinationType}}> builder,
                             global::System.Func<TSource, {{templateType}}> template)
                             => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

                         public static global::Morphant.MapperBuilder<TSource, {{destinationType}}> Template<TSource>(
                             this global::Morphant.MapperBuilder<TSource, {{destinationType}}> builder,
                             global::System.Func<TSource, {{existingDestinationType}}, {{templateType}}> template)
                             => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();
                     }
                 }
                 """;
    }

    private static string BuildSource(
        string destinationDeclaration,
        string mapStatements,
        string additionalSource = "")
    {
        return $$"""
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
                 {{mapStatements}}
                         }
                     }
                 }

                 {{additionalSource}}
                 """;
    }

    // lang=c#
    private const string NonGenericTemplateStub =
"""
namespace TestCase.Morphant.Generated
{
    internal sealed record DestinationMorphantTemplate;
}
""";

    // lang=c#
    private const string GenericTemplateStub =
"""
namespace TestCase.Morphant.Generated
{
    internal sealed record DestinationMorphantTemplate<T>;
}
""";

    // lang=c#
    private const string NestedGenericTemplateStub =
"""
namespace TestCase.Morphant.Generated.Outer1Scope
{
    internal sealed record DestinationMorphantTemplate<TOuter, TValue>;
}
""";

    // lang=c#
    private const string ExpectedNullableGenericExtension =
"""
// <auto-generated />
#nullable enable

namespace Morphant
{
    internal static partial class MorphantGeneratedTemplateExtensions
    {
        public static global::Morphant.MapperBuilder<TSource, global::TestCase.Destination<global::TestCase.User?>> Template<TSource>(
            this global::Morphant.MapperBuilder<TSource, global::TestCase.Destination<global::TestCase.User?>> builder,
            global::System.Func<TSource, global::TestCase.Morphant.Generated.DestinationMorphantTemplate<global::TestCase.User?>> template)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        public static global::Morphant.MapperBuilder<TSource, global::TestCase.Destination<global::TestCase.User?>> Template<TSource>(
            this global::Morphant.MapperBuilder<TSource, global::TestCase.Destination<global::TestCase.User?>> builder,
            global::System.Func<TSource, global::TestCase.Destination<global::TestCase.User?>?, global::TestCase.Morphant.Generated.DestinationMorphantTemplate<global::TestCase.User?>> template)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();
    }
}
""";

    // lang=c#
    private const string ExpectedIntGenericExtension =
"""
// <auto-generated />
#nullable enable

namespace Morphant
{
    internal static partial class MorphantGeneratedTemplateExtensions
    {
        public static global::Morphant.MapperBuilder<TSource, global::TestCase.Destination<int>> Template<TSource>(
            this global::Morphant.MapperBuilder<TSource, global::TestCase.Destination<int>> builder,
            global::System.Func<TSource, global::TestCase.Morphant.Generated.DestinationMorphantTemplate<int>> template)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        public static global::Morphant.MapperBuilder<TSource, global::TestCase.Destination<int>> Template<TSource>(
            this global::Morphant.MapperBuilder<TSource, global::TestCase.Destination<int>> builder,
            global::System.Func<TSource, global::TestCase.Destination<int>?, global::TestCase.Morphant.Generated.DestinationMorphantTemplate<int>> template)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();
    }
}
""";

    // lang=c#
    private const string ExpectedStringGenericExtension =
"""
// <auto-generated />
#nullable enable

namespace Morphant
{
    internal static partial class MorphantGeneratedTemplateExtensions
    {
        public static global::Morphant.MapperBuilder<TSource, global::TestCase.Destination<string>> Template<TSource>(
            this global::Morphant.MapperBuilder<TSource, global::TestCase.Destination<string>> builder,
            global::System.Func<TSource, global::TestCase.Morphant.Generated.DestinationMorphantTemplate<string>> template)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        public static global::Morphant.MapperBuilder<TSource, global::TestCase.Destination<string>> Template<TSource>(
            this global::Morphant.MapperBuilder<TSource, global::TestCase.Destination<string>> builder,
            global::System.Func<TSource, global::TestCase.Destination<string>?, global::TestCase.Morphant.Generated.DestinationMorphantTemplate<string>> template)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();
    }
}
""";

    // lang=c#
    private const string ExpectedNestedGenericExtension =
"""
// <auto-generated />
#nullable enable

namespace Morphant
{
    internal static partial class MorphantGeneratedTemplateExtensions
    {
        public static global::Morphant.MapperBuilder<TSource, global::TestCase.Outer<int>.Destination<string?>> Template<TSource>(
            this global::Morphant.MapperBuilder<TSource, global::TestCase.Outer<int>.Destination<string?>> builder,
            global::System.Func<TSource, global::TestCase.Morphant.Generated.Outer1Scope.DestinationMorphantTemplate<int, string?>> template)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        public static global::Morphant.MapperBuilder<TSource, global::TestCase.Outer<int>.Destination<string?>> Template<TSource>(
            this global::Morphant.MapperBuilder<TSource, global::TestCase.Outer<int>.Destination<string?>> builder,
            global::System.Func<TSource, global::TestCase.Outer<int>.Destination<string?>?, global::TestCase.Morphant.Generated.Outer1Scope.DestinationMorphantTemplate<int, string?>> template)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();
    }
}
""";
}
