using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.IntegrationTests.TestUtils;

namespace Morphant.Generator.IntegrationTests.TypeMapperDeclarativeControlFlowTests;

[TestFixture]
internal sealed class UnsupportedFormsTests
{
    private static IEnumerable<TestCaseData> Cases()
    {
        yield return Case(
            "uninitialized local and assignment",
            """
            int value;
            value = source.Value;
            return new() { Value = value };
            """);
        yield return Case(
            "increment",
            """
            var value = source.Value;
            value++;
            return new() { Value = value };
            """);
        yield return Case(
            "loop",
            """
            var value = 0;
            for (var index = 0; index < source.Value; index++)
            {
                value += index;
            }

            return new() { Value = value };
            """);
        yield return Case(
            "standalone side effect",
            """
            Observe(source.Value);
            return new() { Value = source.Value };
            """);
        yield return Case(
            "local function",
            """
            int Read() => source.Value;
            return new() { Value = Read() };
            """);
        yield return Case(
            "try catch",
            """
            try
            {
                return new() { Value = source.Value };
            }
            catch (Exception)
            {
                return new() { Value = -1 };
            }
            """);
        yield return Case(
            "using local",
            """
            using var stream = new MemoryStream();
            return new() { Value = source.Value };
            """);
        yield return Case(
            "lock",
            """
            lock (source)
            {
                return new() { Value = source.Value };
            }
            """);
        yield return Case(
            "label and goto",
            """
            goto Selected;

            Selected:
            return new() { Value = source.Value };
            """);
    }

    [TestCaseSource(nameof(Cases))]
    public void Keeps_mutation_oriented_statement_form_as_invalid(
        string body)
    {
        var source = BuildSource(body);

        ProductionGeneratorIntegrationTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }

    private static TestCaseData Case(string name, string body) =>
        new TestCaseData(body).SetName(
            "Keeps_" + name.Replace(' ', '_') + "_as_invalid");

    private static string BuildSource(string body) =>
$$"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;
using System.IO;

namespace TestCase
{
    public sealed class Source
    {
        public int Value { get; init; }
    }

    public sealed class Destination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members((source, _) =>
                {
{{body}}
                });

        private static void Observe(int value)
        {
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<Source, Destination>)
                new TestMapper();

            try
            {
                mapper.Create(
                    new Source { Value = 3 },
                    default(MappingContext));
            }
            catch (NotSupportedException exception)
                when (exception.Message.Contains(
                    "Declarative plan",
                    StringComparison.Ordinal))
            {
                return;
            }

            throw new InvalidOperationException(
                "Unsupported declarative grammar was executed.");
        }
    }
}
""";
}
