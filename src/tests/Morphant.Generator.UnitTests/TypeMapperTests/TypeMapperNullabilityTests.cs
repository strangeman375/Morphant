using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TypeMapperTests;

[TestFixture]
internal sealed class TypeMapperNullabilityTests
{
    [Test]
    public void Declares_nullable_interface_inputs_and_results()
    {
        var methods = typeof(ITypeMapper<,>)
            .GetMethods()
            .OrderBy(static method => method.GetParameters().Length)
            .ToArray();
        var context = new NullabilityInfoContext();

        Assert.Multiple(() =>
        {
            Assert.That(methods, Has.Length.EqualTo(2));

            AssertMethodNullability(
                context,
                methods[0],
                NullabilityState.Nullable,
                NullabilityState.NotNull);

            AssertMethodNullability(
                context,
                methods[1],
                NullabilityState.Nullable,
                NullabilityState.Nullable,
                NullabilityState.NotNull);
        });
    }

    [Test]
    public async Task Annotates_reference_mapping_inputs_and_results()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
    }

    public sealed record Destination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }
}
""";

        await TypeMapperGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            (
                "Morphant.Generated.TypeMapper.TestCase_TestMapper.g.cs",
                BuildExpectedCreatableMapper(
                    "global::TestCase.Source",
                    "global::TestCase.Source?",
                    "global::TestCase.Destination",
                    "global::TestCase.Destination?",
                    canMapExisting: true)
            ));
    }

    [Test]
    public async Task Keeps_non_nullable_value_mapping_types_unlifted()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<int, long>();
    }
}
""";

        await TypeMapperGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            (
                "Morphant.Generated.TypeMapper.TestCase_TestMapper.g.cs",
                BuildExpectedMapper(
                    "int",
                    "int",
                    "long",
                    "long")
            ));
    }

    [Test]
    public async Task Preserves_nullable_value_mapping_types()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<int?, long?>();
    }
}
""";

        await TypeMapperGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            (
                "Morphant.Generated.TypeMapper.TestCase_TestMapper.g.cs",
                BuildExpectedMapper(
                    "int?",
                    "int?",
                    "long?",
                    "long?")
            ));
    }

    [Test]
    public async Task Annotates_unconstrained_mapping_type_parameters()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    [MorphantMapper]
    public partial class TestMapper<TSource, TDestination> : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<TSource, TDestination>();
    }
}
""";

        await TypeMapperGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            (
                "Morphant.Generated.TypeMapper.TestCase_TestMapper_2.g.cs",
                BuildExpectedMapper(
                    "TSource",
                    "TSource?",
                    "TDestination",
                    "TDestination?",
                    "<TSource, TDestination>")
            ));
    }

    [Test]
    public async Task Keeps_value_constrained_mapping_type_parameters_unlifted()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    [MorphantMapper]
    public partial class TestMapper<TSource, TDestination> : TypeMapper
        where TSource : struct
        where TDestination : unmanaged
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<TSource, TDestination>();
    }
}
""";

        await TypeMapperGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            (
                "Morphant.Generated.TypeMapper.TestCase_TestMapper_2.g.cs",
                BuildExpectedCreatableMapper(
                    "TSource",
                    "TSource",
                    "TDestination",
                    "TDestination",
                    "<TSource, TDestination>",
                    canMapExisting: false)
            ));
    }

    private static void AssertMethodNullability(
        NullabilityInfoContext context,
        MethodInfo method,
        params NullabilityState[] parameterStates)
    {
        Assert.That(
            context.Create(method.ReturnParameter).ReadState,
            Is.EqualTo(NullabilityState.Nullable));

        var actualParameterStates = method
            .GetParameters()
            .Select(parameter => context.Create(parameter).ReadState);

        Assert.That(actualParameterStates, Is.EqualTo(parameterStates));
    }

    private static string BuildExpectedMapper(
        string sourceType,
        string sourceParameterType,
        string destinationType,
        string destinationMethodType,
        string typeParameterList = "")
    {
        var sourceCanBeNull =
            sourceParameterType.EndsWith(
                "?",
                StringComparison.Ordinal);
        var destinationCanBeNull =
            destinationMethodType.EndsWith(
                "?",
                StringComparison.Ordinal);
        const string mapNewStatement =
            "throw new global::System.NotImplementedException();";
        var mapNewImplementation = destinationCanBeNull
            ? BuildExpectedMapNewInvocation(
                sourceCanBeNull)
            : sourceCanBeNull
                ? """
                  {
                      if (source is null)
                      {
                          return default;
                      }

                      throw new global::System.NotImplementedException();
                  }
                  """
                : """
                  => throw new global::System.NotImplementedException();
                  """;
        var mapExistingImplementation =
            BuildExpectedMapExistingImplementation(
                sourceCanBeNull,
                destinationCanBeNull,
                destinationCanBeNull
                    ? "return MapNewImpl(source, context);"
                    : mapNewStatement,
                """
                throw new global::System.NotImplementedException();
                """);
        var mapNewImpl = destinationCanBeNull
            ? BuildExpectedMapNewImpl(
                sourceType,
                destinationMethodType,
                mapNewStatement)
            : string.Empty;

        // lang=c#
        return $$"""
                 // <auto-generated />
                 #nullable enable

                 namespace TestCase
                 {
                     public partial class TestMapper{{typeParameterList}} :
                         global::Morphant.ITypeMapper<{{sourceType}}, {{destinationType}}>
                     {
                         /// <inheritdoc/>
                         {{destinationMethodType}} global::Morphant.ITypeMapper<{{sourceType}}, {{destinationType}}>.Map(
                             {{sourceParameterType}} source,
                             global::Morphant.MappingContext context)
                 {{IndentGeneratedImplementation(mapNewImplementation)}}

                         /// <inheritdoc/>
                         {{destinationMethodType}} global::Morphant.ITypeMapper<{{sourceType}}, {{destinationType}}>.Map(
                             {{sourceParameterType}} source,
                             {{destinationMethodType}} destination,
                             global::Morphant.MappingContext context)
                 {{IndentGeneratedImplementation(mapExistingImplementation)}}{{mapNewImpl}}
                     }
                 }
                 """;
    }

    private static string BuildExpectedCreatableMapper(
        string sourceType,
        string sourceParameterType,
        string destinationType,
        string destinationMethodType,
        string typeParameterList = "",
        bool canMapExisting = false)
    {
        var sourceCanBeNull =
            sourceParameterType.EndsWith(
                "?",
                StringComparison.Ordinal);
        var destinationCanBeNull =
            destinationMethodType.EndsWith(
                "?",
                StringComparison.Ordinal);
        var mapNewStatement =
            $"return new {destinationType}();";
        var mapNewImplementation = destinationCanBeNull
            ? BuildExpectedMapNewInvocation(
                sourceCanBeNull)
            : sourceCanBeNull
                ? $$"""
                    {
                        if (source is null)
                        {
                            return default;
                        }

                        {{mapNewStatement}}
                    }
                    """
                : $$"""
                    {
                        {{mapNewStatement}}
                    }
                    """;
        var mapExistingImplementation =
            BuildExpectedMapExistingImplementation(
                sourceCanBeNull,
                destinationCanBeNull,
                destinationCanBeNull
                    ? "return MapNewImpl(source, context);"
                    : mapNewStatement,
                canMapExisting
                    ? """
                      return destination;
                      """
                    : """
                      throw new global::System.NotImplementedException();
                      """);
        var mapNewImpl = destinationCanBeNull
            ? BuildExpectedMapNewImpl(
                sourceType,
                destinationMethodType,
                mapNewStatement)
            : string.Empty;

        // lang=c#
        return $$"""
                 // <auto-generated />
                 #nullable enable

                 namespace TestCase
                 {
                     public partial class TestMapper{{typeParameterList}} :
                         global::Morphant.ITypeMapper<{{sourceType}}, {{destinationType}}>
                     {
                         /// <inheritdoc/>
                         {{destinationMethodType}} global::Morphant.ITypeMapper<{{sourceType}}, {{destinationType}}>.Map(
                             {{sourceParameterType}} source,
                             global::Morphant.MappingContext context)
                 {{IndentGeneratedImplementation(mapNewImplementation)}}

                         /// <inheritdoc/>
                         {{destinationMethodType}} global::Morphant.ITypeMapper<{{sourceType}}, {{destinationType}}>.Map(
                             {{sourceParameterType}} source,
                             {{destinationMethodType}} destination,
                             global::Morphant.MappingContext context)
                 {{IndentGeneratedImplementation(mapExistingImplementation)}}{{mapNewImpl}}
                     }
                 }
                 """;
    }

    private static string BuildExpectedMapNewInvocation(
        bool sourceCanBeNull)
    {
        return sourceCanBeNull
            ? """
              {
                  if (source is null)
                  {
                      return default;
                  }

                  return MapNewImpl(source, context);
              }
              """
            : "=> MapNewImpl(source, context);";
    }

    private static string BuildExpectedMapNewImpl(
        string sourceType,
        string destinationMethodType,
        string statement)
    {
        var lines = new List<string>
        {
            string.Empty,
            string.Empty,
            $"        private {destinationMethodType} MapNewImpl(",
            $"            {sourceType} source,",
            "            global::Morphant.MappingContext context)",
            "        {"
        };

        AddIndentedLines(
            lines,
            statement,
            "            ");
        lines.Add("        }");

        return string.Join(
            Environment.NewLine,
            lines);
    }

    private static string BuildExpectedMapExistingImplementation(
        bool sourceCanBeNull,
        bool destinationCanBeNull,
        string createNewStatement,
        string mapExistingStatement)
    {
        if (!sourceCanBeNull &&
            !destinationCanBeNull)
        {
            var statement = mapExistingStatement.Trim();

            if (statement.StartsWith(
                "return ",
                StringComparison.Ordinal))
            {
                return
                    "=> " +
                    statement.Substring(
                        "return ".Length);
            }

            return "=> " + statement;
        }

        var lines = new List<string>
        {
            "{"
        };

        if (sourceCanBeNull)
        {
            lines.Add("    if (source is null)");
            lines.Add("    {");
            lines.Add("        return default;");
            lines.Add("    }");
            lines.Add(string.Empty);
        }

        if (destinationCanBeNull)
        {
            lines.Add("    if (destination is null)");
            lines.Add("    {");
            AddIndentedLines(
                lines,
                createNewStatement,
                "        ");
            lines.Add("    }");
            lines.Add(string.Empty);
        }

        AddIndentedLines(
            lines,
            mapExistingStatement,
            "    ");
        lines.Add("}");

        return string.Join(
            Environment.NewLine,
            lines);
    }

    private static string IndentGeneratedImplementation(
        string implementation)
    {
        var normalized = implementation
            .Trim()
            .Replace("\r\n", "\n")
            .Replace('\r', '\n');
        var indentation = normalized.StartsWith(
            "=>",
            StringComparison.Ordinal)
                ? "            "
                : "        ";

        return string.Join(
            Environment.NewLine,
            normalized
                .Split('\n')
                .Select(line =>
                    line.Length == 0
                        ? string.Empty
                        : indentation + line));
    }

    private static void AddIndentedLines(
        ICollection<string> lines,
        string value,
        string indentation)
    {
        foreach (var line in value
                     .Trim()
                     .Replace("\r\n", "\n")
                     .Replace('\r', '\n')
                     .Split('\n'))
        {
            lines.Add(indentation + line);
        }
    }
}
