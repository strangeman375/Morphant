using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TemplateExtensionTests;

[TestFixture]
internal sealed class TemplateExtensionUsageTests
{
    [Test]
    public async Task Resolves_generated_template_overloads_for_reference_destination()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591
#nullable enable

using Morphant;
using TestCase.Morphant.Generated;

namespace TestCase
{
    public sealed class Source
    {
        public int Value { get; }
    }

    public sealed class Destination
    {
        public int Value { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder
                .Map<Source, Destination>()
                .NullSourceHandling(
                    NullSourceHandling.Throw)
                .Template(static (Source source) =>
                    new(source.Value))
                .MemberSelection(
                    MemberSelection.Explicit)
                .Template(static (Source source, Destination destination) =>
                    new(
                        source.Value,
                        destination.Value))
                .UnmappedMemberValidation(
                    UnmappedMemberValidation.None);
        }
    }
}

namespace TestCase.Morphant.Generated
{
    internal sealed record DestinationMorphantTemplate(
        int SourceValue,
        int? ExistingValue = null);
}
""";

        await RunGeneratedDestination(source, isReferenceType: true);
    }

    [Test]
    public async Task Resolves_generated_template_overloads_for_value_destination()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591
#nullable enable

using Morphant;
using TestCase.Morphant.Generated;

namespace TestCase
{
    public sealed class Source
    {
        public int Value { get; }
    }

    public struct Destination
    {
        public int Value { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder
                .Map<Source, Destination>()
                .Template(static (Source source) =>
                    new(source.Value))
                .Template(static (Source source, Destination destination) =>
                    new(
                        source.Value,
                        destination.Value))
                .MemberSelection(
                    MemberSelection.Explicit);
        }
    }
}

namespace TestCase.Morphant.Generated
{
    internal sealed record DestinationMorphantTemplate(
        int SourceValue,
        int ExistingValue = 0);
}
""";

        await RunGeneratedDestination(source, isReferenceType: false);
    }

    [Test]
    public async Task Resolves_direct_template_overloads_for_reference_destination()
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
        public string Value { get; } = string.Empty;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder
                .Map<Source, string>()
                .Template(static source => source.Value)
                .Template(static (source, destination) =>
                    destination ?? source.Value)
                .UnmappedMemberValidation(
                    UnmappedMemberValidation.None);
        }
    }
}
""";

        await TemplateExtensionGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            (
                "Morphant.Generated.TemplateExtension.System_String.g.cs",
                BuildExpectedExtension(
                    "string",
                    "string?",
                    "string")
            ));
    }

    [Test]
    public async Task Resolves_direct_template_overloads_for_value_destination()
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
        public int Value { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder
                .Map<Source, int>()
                .Template(static source => source.Value)
                .Template(static (source, destination) =>
                    source.Value + destination)
                .ConstructorSelection(
                    ConstructorSelection.Explicit);
        }
    }
}
""";

        await TemplateExtensionGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            (
                "Morphant.Generated.TemplateExtension.System_Int32.g.cs",
                BuildExpectedExtension(
                    "int",
                    "int",
                    "int")
            ));
    }

    [Test]
    public async Task Resolves_overloads_for_block_bodied_lambdas()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591
#nullable enable

using Morphant;
using TestCase.Morphant.Generated;

namespace TestCase
{
    public sealed class Source
    {
        public int Value { get; }
    }

    public sealed class Destination
    {
        public int Value { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>()
                .Template(source =>
                {
                    var value = source.Value;

                    return new(value);
                })
                .Template((source, destination) =>
                {
                    var value =
                        source.Value +
                        destination.Value;

                    return new(value);
                });
        }
    }
}

namespace TestCase.Morphant.Generated
{
    internal sealed record DestinationMorphantTemplate(int Value);
}
""";

        await RunGeneratedDestination(source, isReferenceType: true);
    }

    [Test]
    public async Task Infers_source_type_from_receiver_for_supported_source_shapes()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591
#nullable enable

using Morphant;
using TestCase.Morphant.Generated;

namespace TestCase
{
    public sealed class ReferenceSource
    {
        public int Value { get; }
    }

    public struct ValueSource
    {
        public int Value { get; }
    }

    public sealed class GenericSource<T>
    {
        public T Value { get; } = default!;
    }

    public sealed class Destination
    {
        public int Value { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<ReferenceSource, Destination>()
                .Template(static source =>
                    new(source.Value));

            builder.Map<ReferenceSource?, Destination>()
                .Template(static source =>
                    new(source?.Value ?? 0));

            builder.Map<ValueSource, Destination>()
                .Template(static source =>
                    new(source.Value));

            builder.Map<ValueSource?, Destination>()
                .Template(static source =>
                    new(source?.Value ?? 0));

            builder.Map<GenericSource<string?>, Destination>()
                .Template(static source =>
                    new(
                        source.Value?.Length ?? 0));

            builder.Map<dynamic, Destination>()
                .Template(static source =>
                    new(source is null ? 0 : 1));
        }
    }
}

namespace TestCase.Morphant.Generated
{
    internal sealed record DestinationMorphantTemplate(int Value);
}
""";

        await RunGeneratedDestination(source, isReferenceType: true);
    }

    [Test]
    public async Task Infers_open_source_type_parameter_from_generic_mapper_receiver()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591
#nullable enable

using Morphant;
using TestCase.Morphant.Generated;

namespace TestCase
{
    public sealed class Destination
    {
        public int Value { get; }
    }

    [MorphantMapper]
    public partial class TestMapper<TSource> : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder
                .Map<TSource, Destination>()
                .Template(static source =>
                    new(GetValue(source)))
                .Template(static (source, destination) =>
                    new(
                        GetValue(source) +
                        destination.Value));
        }

        private static int GetValue(TSource source) => 0;
    }
}

namespace TestCase.Morphant.Generated
{
    internal sealed record DestinationMorphantTemplate(int Value);
}
""";

        await RunGeneratedDestination(source, isReferenceType: true);
    }

    private static Task RunGeneratedDestination(
        string source,
        bool isReferenceType)
    {
        const string destinationType = "global::TestCase.Destination";

        return TemplateExtensionGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            (
                "Morphant.Generated.TemplateExtension.TestCase_Destination.g.cs",
                BuildExpectedExtension(
                    destinationType,
                    isReferenceType
                        ? destinationType + "?"
                        : destinationType,
                    "global::TestCase.Morphant.Generated." +
                    "DestinationMorphantTemplate")
            ));
    }

    private static string BuildExpectedExtension(
        string destinationType,
        string existingDestinationType,
        string templateResultType)
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
                         /// A lambda expression that receives the non-null source value and describes the mapping.
                         /// </param>
                         /// <returns>The <paramref name="builder"/> instance.</returns>
                         public static global::Morphant.MapperBuilder<TSource, {{destinationType}}> Template<TSource>(
                             this global::Morphant.MapperBuilder<TSource, {{destinationType}}> builder,
                             global::System.Func<TSource, {{templateResultType}}> template)
                             => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

                         /// <summary>
                         /// Configures a mapping template for an existing destination.
                         /// </summary>
                         /// <typeparam name="TSource">The source type.</typeparam>
                         /// <param name="builder">The mapping builder to configure.</param>
                         /// <param name="template">
                         /// A lambda expression that receives the non-null source value and the non-null
                         /// existing destination and describes the mapping.
                         /// </param>
                         /// <returns>The <paramref name="builder"/> instance.</returns>
                         public static global::Morphant.MapperBuilder<TSource, {{destinationType}}> Template<TSource>(
                             this global::Morphant.MapperBuilder<TSource, {{destinationType}}> builder,
                             global::System.Func<TSource, {{existingDestinationType.TrimEnd('?')}}, {{templateResultType}}> template)
                             => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();
                     }
                 }
                 """;
    }
}
