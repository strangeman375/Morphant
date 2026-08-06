using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TypeMapperConvertTests;

[TestFixture]
internal sealed class BodyTests
{
    [Test]
    public void Executes_expression_and_arbitrary_synchronous_block_bodies()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;
using Morphant.Context;

namespace TestCase
{
    public sealed record Source(int Value);

    public sealed record Destination(int Value);

    public sealed record ComplexSource(
        int Value,
        bool ReturnEarly = false,
        bool Recover = false,
        bool Fail = false);

    public sealed record ComplexDestination(int Value)
    {
        public int Mutable { get; set; }
    }

    public sealed class RecoverableException : Exception
    {
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        private readonly int _offset = 3;

        public static int FactoryCalls { get; private set; }

        public static int FinallyCalls { get; private set; }

        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>()
                .Convert(static (source, _, _) =>
                    CreateDestination(source?.Value ?? -1));

            builder.Map<ComplexSource, ComplexDestination?>()
                .Convert((source, previous, _) =>
                {
                    if (source is null)
                    {
                        return null;
                    }

                    static int Sum(int value)
                    {
                        var sum = 0;

                        for (var index = 0; index < value; index++)
                        {
                            sum += index;
                        }

                        return sum;
                    }

                    var result = previous.TryGetValue(out var destination)
                        ? destination
                        : CreateComplexDestination(source.Value);

                    try
                    {
                        result.Mutable += Sum(source.Value);

                        if (source.Recover)
                        {
                            throw new RecoverableException();
                        }

                        if (source.Fail)
                        {
                            throw new InvalidOperationException("failed");
                        }
                    }
                    catch (RecoverableException)
                    {
                        result = result with
                        {
                            Value = result.Value + 10
                        };
                    }
                    finally
                    {
                        FinallyCalls++;
                    }

                    if (source.ReturnEarly)
                    {
                        return result;
                    }

                    return result with
                    {
                        Value = result.Value + _offset
                    };
                });
        }

        private static Destination CreateDestination(int value) =>
            new(value + 1);

        private static ComplexDestination CreateComplexDestination(
            int value)
        {
            FactoryCalls++;
            return new(value);
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var generated = new TestMapper();
            var context = default(MappingContext);
            var expression =
                (ITypeMapper<Source, Destination>)generated;
            var complex =
                (ITypeMapper<ComplexSource, ComplexDestination?>)generated;
            var expressionResult = expression.Create(new Source(4), context);
            var nullResult = complex.Create(null, context);
            var early = complex.Create(
                new ComplexSource(4, ReturnEarly: true),
                context);
            var previous = new ComplexDestination(5);
            var recovered = complex.Update(
                new ComplexSource(3, Recover: true),
                previous,
                context);

            if (expressionResult.Value != 5 ||
                nullResult is not null ||
                early?.Value != 4 ||
                early.Mutable != 6 ||
                ReferenceEquals(previous, recovered) ||
                previous.Mutable != 3 ||
                recovered?.Value != 18 ||
                recovered.Mutable != 3 ||
                TestMapper.FactoryCalls != 1 ||
                TestMapper.FinallyCalls != 2)
            {
                throw new InvalidOperationException(
                    "A manual C# body changed semantics.");
            }

            try
            {
                complex.Create(
                    new ComplexSource(2, Fail: true),
                    context);
            }
            catch (InvalidOperationException exception)
                when (exception.Message == "failed")
            {
                if (TestMapper.FinallyCalls != 3)
                {
                    throw new InvalidOperationException(
                        "The manual finally block did not execute.");
                }

                return;
            }

            throw new InvalidOperationException(
                "A manual exception was swallowed.");
        }
    }
}
""";

        StructuredConstructTypeMapperGeneratorTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }
}
