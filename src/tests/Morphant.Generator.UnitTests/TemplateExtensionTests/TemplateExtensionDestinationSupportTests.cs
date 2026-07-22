using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TemplateExtensionTests;

[TestFixture]
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
    [TestCase("global::System.Nullable<int>", "int?", "System.Nullable`1<int>")]
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

    [TestCase("dynamic", "object")]
    [TestCase("dynamic?", "object?")]
    public async Task Reuses_object_extension_for_dynamic_destination(
        string destinationType,
        string expectedType)
    {
        await RunDirectTemplateDestination(
            destinationType,
            expectedType,
            "System.Object",
            "object?");
    }

    [TestCase("(int Id, string Name)")]
    [TestCase("global::System.ValueTuple<int, string>")]
    public async Task Does_not_generate_extension_for_tuple_destination(
        string destinationType)
    {
        await RunWithoutExtension(destinationType);
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

    [TestCase("Destination", "public delegate void Destination();")]
    [TestCase("Destination?", "public delegate void Destination();")]
    [TestCase("Destination<int>", "public delegate void Destination<T>();")]
    [TestCase("Destination<int>?", "public delegate void Destination<T>();")]
    public async Task Does_not_generate_extension_for_delegate_destination(
        string destinationType,
        string destinationTypeDeclaration)
    {
        await RunWithoutExtension(
            destinationType,
            "    " + destinationTypeDeclaration);
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
                ExpectedHintNamePart(
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
    public async Task Generates_extension_for_destination_in_global_namespace()
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

namespace Morphant.Generated
{
    internal sealed record DestinationMorphantTemplate;
}
""";

        await TemplateExtensionGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            (
                "Morphant.TemplateExtensions.Destination.g.cs",
                BuildExpectedGeneratedExtension(
                    "global::Destination",
                    "global::Morphant.Generated." +
                    "DestinationMorphantTemplate",
                    "global::Destination?")
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
    public async Task Generates_extension_for_accessible_nested_destination(
        string containerDeclaration,
        string destinationAccessibility)
    {
        var destinationDeclaration = $$"""
                                           {{containerDeclaration}}
                                           {
                                               {{destinationAccessibility}} sealed class Destination
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
                ExpectedHintNamePart(
                    "TestCase.Container+Destination") +
                ".g.cs",
                BuildExpectedGeneratedExtension(
                    "global::TestCase.Container.Destination",
                    "global::TestCase.Morphant.Generated." +
                    "ContainerScope.DestinationMorphantTemplate",
                    "global::TestCase.Container.Destination?")
            ));
    }

    [TestCase("private")]
    [TestCase("protected")]
    [TestCase("private protected")]
    public async Task Does_not_generate_extension_for_inaccessible_nested_destination(
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

        await TemplateExtensionGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source);
    }

    [Test]
    public async Task Does_not_generate_extension_when_containing_type_is_inaccessible()
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

        await TemplateExtensionGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source);
    }

    [TestCase("Destination<Hidden>")]
    [TestCase("Destination<Wrapper<Hidden>>")]
    [TestCase("Destination<Hidden[]>")]
    [TestCase("Outer<Hidden>.Destination<int>")]
    [TestCase("HiddenEnum?")]
    public async Task Does_not_generate_extension_when_destination_contains_inaccessible_type(
        string destinationType)
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

                           public sealed class Destination<T>
                           {
                           }

                           public sealed class Wrapper<T>
                           {
                           }

                           public sealed class Outer<TOuter>
                           {
                               public sealed class Destination<TValue>
                               {
                               }
                           }

                           [MorphantMapper]
                           public partial class TestMapper : TypeMapper
                           {
                               private sealed class Hidden
                               {
                               }

                               private enum HiddenEnum
                               {
                                   None
                               }

                               protected override void Configure(MapperBuilder builder)
                               {
                                   builder.Map<Source, {{destinationType}}>();
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
    public async Task Does_not_generate_extension_for_destination_nested_in_file_local_type()
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

        await TemplateExtensionGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp11,
            source);
    }

    [TestCase("Destination<Hidden>")]
    [TestCase("Destination<Wrapper<Hidden>>")]
    [TestCase("Destination<Hidden[]>")]
    [TestCase("Outer<Hidden>.Destination<int>")]
    [TestCase("HiddenEnum?")]
    public async Task Does_not_generate_extension_when_constructed_destination_contains_file_local_type(
        string destinationType)
    {
        // lang=c#
        const string destinationDeclaration =
"""
    file sealed class Hidden
    {
    }

    file enum HiddenEnum
    {
        None
    }

    public sealed class Destination<T>
    {
    }

    public sealed class Wrapper<T>
    {
    }

    public sealed class Outer<TOuter>
    {
        public sealed class Destination<TValue>
        {
        }
    }
""";

        await RunWithoutExtension(
            destinationType,
            destinationDeclaration,
            LanguageVersion.CSharp11);
    }

    [Test]
    public async Task Generates_extension_for_non_generic_destination_from_referenced_assembly()
    {
        // lang=c#
        const string templateStub =
"""
namespace Morphant.Generator.UnitTests.TestAssets.Morphant.Generated
{
    internal sealed record ReferencedDestinationMorphantTemplate;
}
""";

        const string destinationType =
            "global::Morphant.Generator.UnitTests.TestAssets." +
            "ReferencedDestination";

        const string templateType =
            "global::Morphant.Generator.UnitTests.TestAssets." +
            "Morphant.Generated." +
            "ReferencedDestinationMorphantTemplate";

        const string usageIdentity =
            "Morphant.Generator.UnitTests.TestAssets." +
            "ReferencedDestination";

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
                        .ReferencedDestination).Assembly
            },
            (
                "Morphant.TemplateExtensions." +
                ExpectedHintNamePart(usageIdentity) +
                ".g.cs",
                BuildExpectedGeneratedExtension(
                    destinationType,
                    templateType,
                    destinationType + "?")
            ));
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

    [TestCase("dynamic", "dynamic")]
    [TestCase("global::System.Int32", "int")]
    [TestCase("global::System.Nullable<int>", "int?")]
    [TestCase("string?[]", "string?[]")]
    [TestCase("(int Id, string Name)", "(int Id, string Name)")]
    [TestCase(
        "global::System.ValueTuple<int, string>",
        "(int, string)")]
    [TestCase("global::System.Action", "global::System.Action")]
    [TestCase(
        "global::System.Collections.Generic.Dictionary<string, int?>",
        "global::System.Collections.Generic.Dictionary<string, int?>")]
    public async Task Generates_extension_for_supported_closed_generic_argument(
        string destinationTypeArgument,
        string expectedTypeArgument)
    {
        // lang=c#
        const string destinationDeclaration =
"""
    public sealed class Destination<T>
    {
    }
""";

        var destinationType =
            $"global::TestCase.Destination<{expectedTypeArgument}>";

        var usageIdentity =
            $"TestCase.Destination`1<{expectedTypeArgument}>";

        await TemplateExtensionGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            BuildSource(
                destinationDeclaration,
                $"builder.Map<Source, Destination<{destinationTypeArgument}>>();",
                GenericTemplateStub),
            (
                "Morphant.TemplateExtensions." +
                ExpectedHintNamePart(usageIdentity) +
                ".g.cs",
                BuildExpectedGeneratedExtension(
                    destinationType,
                    "global::TestCase.Morphant.Generated." +
                    "DestinationMorphantTemplate<" +
                    expectedTypeArgument + ">",
                    destinationType + "?")
            ));
    }

    [Test]
    public async Task Preserves_top_level_nullable_annotation_on_constructed_generic_destination()
    {
        // lang=c#
        const string destinationDeclaration =
"""
    public sealed class Destination<T>
    {
    }
""";

        const string destinationType =
            "global::TestCase.Destination<int>?";

        await TemplateExtensionGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            BuildSource(
                destinationDeclaration,
                "builder.Map<Source, Destination<int>?>();",
                GenericTemplateStub),
            (
                "Morphant.TemplateExtensions." +
                ExpectedHintNamePart(
                    "TestCase.Destination`1<int>") +
                ".g.cs",
                BuildExpectedGeneratedExtension(
                    destinationType,
                    "global::TestCase.Morphant.Generated." +
                    "DestinationMorphantTemplate<int>?",
                    destinationType)
            ));
    }

    [Test]
    public async Task Generates_extension_for_nullable_generic_custom_struct_destination()
    {
        // lang=c#
        const string destinationDeclaration =
"""
    public struct Destination<T>
    {
    }
""";

        const string destinationType =
            "global::TestCase.Destination<int>?";

        await TemplateExtensionGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            BuildSource(
                destinationDeclaration,
                "builder.Map<Source, Destination<int>?>();",
                GenericTemplateStub),
            (
                "Morphant.TemplateExtensions." +
                ExpectedHintNamePart(
                    "System.Nullable`1<" +
                    "global::TestCase.Destination<int>>") +
                ".g.cs",
                BuildExpectedGeneratedExtension(
                    destinationType,
                    "global::TestCase.Morphant.Generated." +
                    "DestinationMorphantTemplate<int>?",
                    destinationType)
            ));
    }

    [TestCase("public sealed class Destination<T>", true, LanguageVersion.CSharp9)]
    [TestCase("public struct Destination<T>", false, LanguageVersion.CSharp9)]
    [TestCase("public readonly struct Destination<T>", false, LanguageVersion.CSharp9)]
    [TestCase("public sealed record Destination<T>", true, LanguageVersion.CSharp9)]
    [TestCase("public abstract class Destination<T>", true, LanguageVersion.CSharp9)]
    [TestCase("public interface Destination<T>", true, LanguageVersion.CSharp9)]
    [TestCase("public record struct Destination<T>", false, LanguageVersion.CSharp10)]
    [TestCase("public readonly record struct Destination<T>", false, LanguageVersion.CSharp10)]
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
                ExpectedHintNamePart(usageIdentity) +
                ".g.cs",
                BuildExpectedGeneratedExtension(
                    destinationType,
                    templateType,
                    destinationType + "?")
            ));
    }

    [TestCase("Destination<T>")]
    [TestCase("Destination<Wrapper<T>>")]
    [TestCase("Destination<T[]>")]
    [TestCase("Destination<(T Item, int Count)>")]
    [TestCase("Outer<T>.Destination<int>")]
    public async Task Does_not_generate_extension_for_open_constructed_destination(
        string destinationType)
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

                           public sealed class Destination<TValue>
                           {
                           }

                           public sealed class Wrapper<TValue>
                           {
                           }

                           public sealed class Outer<TOuter>
                           {
                               public sealed class Destination<TValue>
                               {
                               }
                           }

                           [MorphantMapper]
                           public partial class TestMapper<T> : TypeMapper
                           {
                               protected override void Configure(MapperBuilder builder)
                               {
                                   builder.Map<Source, {{destinationType}}>();
                               }
                           }
                       }
                       """;

        await TemplateExtensionGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source);
    }

    [TestCase("TDestination", "")]
    [TestCase("TDestination", "where TDestination : class")]
    [TestCase("TDestination?", "where TDestination : class")]
    [TestCase("TDestination", "where TDestination : struct")]
    [TestCase("TDestination?", "where TDestination : struct")]
    public async Task Does_not_generate_extension_for_type_parameter_destination(
        string destinationType,
        string constraint)
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

                           [MorphantMapper]
                           public partial class TestMapper<TDestination> : TypeMapper
                               {{constraint}}
                           {
                               protected override void Configure(MapperBuilder builder)
                               {
                                   builder.Map<Source, {{destinationType}}>();
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

    private static string ExpectedHintNamePart(string usageIdentity)
    {
        // Literal contract: do not derive these values with generator code.
        return usageIdentity switch
        {
            "Morphant.Generator.UnitTests.TestAssets.ReferencedDestination" =>
                "Morphant_Generator_UnitTests_TestAssets_ReferencedDestination",
            "Morphant.Generator.UnitTests.TestAssets.ReferencedGenericDestination`1<string>" =>
                "Morphant_Generator_UnitTests_TestAssets_ReferencedGenericDestination_1_string___d8d840c16ce06d75",
            "System.Boolean" =>
                "System_Boolean",
            "System.Byte" =>
                "System_Byte",
            "System.Char" =>
                "System_Char",
            "System.DateOnly" =>
                "System_DateOnly",
            "System.DateTime" =>
                "System_DateTime",
            "System.DateTimeOffset" =>
                "System_DateTimeOffset",
            "System.Decimal" =>
                "System_Decimal",
            "System.Double" =>
                "System_Double",
            "System.Guid" =>
                "System_Guid",
            "System.Half" =>
                "System_Half",
            "System.Index" =>
                "System_Index",
            "System.Int128" =>
                "System_Int128",
            "System.Int16" =>
                "System_Int16",
            "System.Int32" =>
                "System_Int32",
            "System.Int64" =>
                "System_Int64",
            "System.IntPtr" =>
                "System_IntPtr",
            "System.Nullable`1<bool>" =>
                "System_Nullable_1_bool___4df6b1bcb377cf3e",
            "System.Nullable`1<byte>" =>
                "System_Nullable_1_byte___bd82412e67f076e8",
            "System.Nullable`1<char>" =>
                "System_Nullable_1_char___7639ecb5376dbbe2",
            "System.Nullable`1<decimal>" =>
                "System_Nullable_1_decimal___a19b0d4f7fe22c9f",
            "System.Nullable`1<double>" =>
                "System_Nullable_1_double___0ea1965ff8720a8d",
            "System.Nullable`1<float>" =>
                "System_Nullable_1_float___ced16b7b3299ead0",
            "System.Nullable`1<global::System.DateOnly>" =>
                "System_Nullable_1_global__System_DateOnly___9366ff1ee268685c",
            "System.Nullable`1<global::System.DateTime>" =>
                "System_Nullable_1_global__System_DateTime___9d17bf23ad831f5d",
            "System.Nullable`1<global::System.DateTimeOffset>" =>
                "System_Nullable_1_global__System_DateTimeOffset___87a81c9caab3f4f0",
            "System.Nullable`1<global::System.Guid>" =>
                "System_Nullable_1_global__System_Guid___bbf6f8313e5a607d",
            "System.Nullable`1<global::System.Half>" =>
                "System_Nullable_1_global__System_Half___ae31267332346df1",
            "System.Nullable`1<global::System.Index>" =>
                "System_Nullable_1_global__System_Index___9c9e151f7bbc19b2",
            "System.Nullable`1<global::System.Int128>" =>
                "System_Nullable_1_global__System_Int128___09da207b976fff58",
            "System.Nullable`1<global::System.Numerics.BigInteger>" =>
                "System_Nullable_1_global__System_Numerics_BigInteger___98a17ac6005dfd8c",
            "System.Nullable`1<global::System.Numerics.Complex>" =>
                "System_Nullable_1_global__System_Numerics_Complex___62269ba80b3273b0",
            "System.Nullable`1<global::System.Range>" =>
                "System_Nullable_1_global__System_Range___7d2ca1569bdf6e8d",
            "System.Nullable`1<global::System.Text.Rune>" =>
                "System_Nullable_1_global__System_Text_Rune___ab5c22a4797f9c6b",
            "System.Nullable`1<global::System.TimeOnly>" =>
                "System_Nullable_1_global__System_TimeOnly___b819988b3624a363",
            "System.Nullable`1<global::System.TimeSpan>" =>
                "System_Nullable_1_global__System_TimeSpan___4a20d129bcbc8975",
            "System.Nullable`1<global::System.UInt128>" =>
                "System_Nullable_1_global__System_UInt128___34541e2275456c89",
            "System.Nullable`1<global::TestCase.Destination<int>>" =>
                "System_Nullable_1_global__TestCase_Destination_int____6efe5daae75cd750",
            "System.Nullable`1<global::TestCase.Destination>" =>
                "System_Nullable_1_global__TestCase_Destination___19e6d3705ef85e11",
            "System.Nullable`1<int>" =>
                "System_Nullable_1_int___7d45e0b10f64f4d1",
            "System.Nullable`1<long>" =>
                "System_Nullable_1_long___f5f9a2e31fa2375c",
            "System.Nullable`1<nint>" =>
                "System_Nullable_1_nint___798d651cdd512a3b",
            "System.Nullable`1<nuint>" =>
                "System_Nullable_1_nuint___513079d293d515ac",
            "System.Nullable`1<sbyte>" =>
                "System_Nullable_1_sbyte___32da8cbe2dbcdc6b",
            "System.Nullable`1<short>" =>
                "System_Nullable_1_short___3d107169614015b8",
            "System.Nullable`1<uint>" =>
                "System_Nullable_1_uint___9a5d5d570fd42f1e",
            "System.Nullable`1<ulong>" =>
                "System_Nullable_1_ulong___444552ddee1b3661",
            "System.Nullable`1<ushort>" =>
                "System_Nullable_1_ushort___e130dbe80b6dcb5f",
            "System.Numerics.BigInteger" =>
                "System_Numerics_BigInteger",
            "System.Numerics.Complex" =>
                "System_Numerics_Complex",
            "System.Object" =>
                "System_Object",
            "System.Range" =>
                "System_Range",
            "System.SByte" =>
                "System_SByte",
            "System.Single" =>
                "System_Single",
            "System.String" =>
                "System_String",
            "System.Text.Rune" =>
                "System_Text_Rune",
            "System.TimeOnly" =>
                "System_TimeOnly",
            "System.TimeSpan" =>
                "System_TimeSpan",
            "System.UInt128" =>
                "System_UInt128",
            "System.UInt16" =>
                "System_UInt16",
            "System.UInt32" =>
                "System_UInt32",
            "System.UInt64" =>
                "System_UInt64",
            "System.UIntPtr" =>
                "System_UIntPtr",
            "System.Uri" =>
                "System_Uri",
            "System.Version" =>
                "System_Version",
            "TestCase.Container+Destination" =>
                "TestCase_Container_Destination__ed07600340fa8c3b",
            "TestCase.Destination" =>
                "TestCase_Destination",
            "TestCase.Destination`1<(int Id, string Name)>" =>
                "TestCase_Destination_1__int_Id__string_Name____b1410dab23b9cb4d",
            "TestCase.Destination`1<(int, string)>" =>
                "TestCase_Destination_1__int__string____347961e6881fe97f",
            "TestCase.Destination`1<dynamic>" =>
                "TestCase_Destination_1_dynamic___46185ec8cd0035cd",
            "TestCase.Destination`1<global::System.Action>" =>
                "TestCase_Destination_1_global__System_Action___16040dabb2c7fd34",
            "TestCase.Destination`1<global::System.Collections.Generic.Dictionary<string, int?>>" =>
                "TestCase_Destination_1_global__System_Collections_Generic_Dictionary_string__int_____01318f645d99328b",
            "TestCase.Destination`1<int>" =>
                "TestCase_Destination_1_int___a212525a5607429d",
            "TestCase.Destination`1<int?>" =>
                "TestCase_Destination_1_int____6c6110802e53283c",
            "TestCase.Destination`1<string?[]>" =>
                "TestCase_Destination_1_string______1a0ca624e6ea6ba4",
            _ => throw new ArgumentOutOfRangeException(
                nameof(usageIdentity),
                usageIdentity,
                "Unexpected usage identity.")
        };
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
                ExpectedHintNamePart(usageIdentity) +
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
                         /// <summary>
                         /// Configures a mapping template.
                         /// </summary>
                         /// <typeparam name="TSource">The source type.</typeparam>
                         /// <param name="builder">The mapping builder to configure.</param>
                         /// <param name="template">
                         /// A lambda expression that receives the source value and describes the mapping.
                         /// </param>
                         /// <returns>The <paramref name="builder"/> instance.</returns>
                         public static global::Morphant.MapperBuilder<TSource, {{type}}> Template<TSource>(
                             this global::Morphant.MapperBuilder<TSource, {{type}}> builder,
                             global::System.Func<TSource, {{type}}> template)
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
                             global::System.Func<TSource, {{templateType}}> template)
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
        /// <summary>
        /// Configures a mapping template.
        /// </summary>
        /// <typeparam name="TSource">The source type.</typeparam>
        /// <param name="builder">The mapping builder to configure.</param>
        /// <param name="template">
        /// A lambda expression that receives the source value and describes the mapping.
        /// </param>
        /// <returns>The <paramref name="builder"/> instance.</returns>
        public static global::Morphant.MapperBuilder<TSource, global::TestCase.Destination<global::TestCase.User?>> Template<TSource>(
            this global::Morphant.MapperBuilder<TSource, global::TestCase.Destination<global::TestCase.User?>> builder,
            global::System.Func<TSource, global::TestCase.Morphant.Generated.DestinationMorphantTemplate<global::TestCase.User?>> template)
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
        /// <summary>
        /// Configures a mapping template.
        /// </summary>
        /// <typeparam name="TSource">The source type.</typeparam>
        /// <param name="builder">The mapping builder to configure.</param>
        /// <param name="template">
        /// A lambda expression that receives the source value and describes the mapping.
        /// </param>
        /// <returns>The <paramref name="builder"/> instance.</returns>
        public static global::Morphant.MapperBuilder<TSource, global::TestCase.Destination<int>> Template<TSource>(
            this global::Morphant.MapperBuilder<TSource, global::TestCase.Destination<int>> builder,
            global::System.Func<TSource, global::TestCase.Morphant.Generated.DestinationMorphantTemplate<int>> template)
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
        /// <summary>
        /// Configures a mapping template.
        /// </summary>
        /// <typeparam name="TSource">The source type.</typeparam>
        /// <param name="builder">The mapping builder to configure.</param>
        /// <param name="template">
        /// A lambda expression that receives the source value and describes the mapping.
        /// </param>
        /// <returns>The <paramref name="builder"/> instance.</returns>
        public static global::Morphant.MapperBuilder<TSource, global::TestCase.Destination<string>> Template<TSource>(
            this global::Morphant.MapperBuilder<TSource, global::TestCase.Destination<string>> builder,
            global::System.Func<TSource, global::TestCase.Morphant.Generated.DestinationMorphantTemplate<string>> template)
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
        /// <summary>
        /// Configures a mapping template.
        /// </summary>
        /// <typeparam name="TSource">The source type.</typeparam>
        /// <param name="builder">The mapping builder to configure.</param>
        /// <param name="template">
        /// A lambda expression that receives the source value and describes the mapping.
        /// </param>
        /// <returns>The <paramref name="builder"/> instance.</returns>
        public static global::Morphant.MapperBuilder<TSource, global::TestCase.Outer<int>.Destination<string?>> Template<TSource>(
            this global::Morphant.MapperBuilder<TSource, global::TestCase.Outer<int>.Destination<string?>> builder,
            global::System.Func<TSource, global::TestCase.Morphant.Generated.Outer1Scope.DestinationMorphantTemplate<int, string?>> template)
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
        public static global::Morphant.MapperBuilder<TSource, global::TestCase.Outer<int>.Destination<string?>> Template<TSource>(
            this global::Morphant.MapperBuilder<TSource, global::TestCase.Outer<int>.Destination<string?>> builder,
            global::System.Func<TSource, global::TestCase.Outer<int>.Destination<string?>?, global::TestCase.Morphant.Generated.Outer1Scope.DestinationMorphantTemplate<int, string?>> template)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();
    }
}
""";
}
