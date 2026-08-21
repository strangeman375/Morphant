using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.RuntimePolymorphismTests;

[TestFixture]
internal sealed class ValueTypeTests
{
    [Test]
    public void Supports_boxed_sources_destinations_and_nullable_values()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;
using Morphant.Exceptions;

namespace TestCase
{
    public interface IValueSource
    {
        int Value { get; }
    }

    public readonly struct ValueSource : IValueSource
    {
        public ValueSource(int value) => Value = value;
        public int Value { get; }
    }

    public readonly struct OptionalSource : IValueSource
    {
        public OptionalSource(int value) => Value = value;
        public int Value { get; }
    }

    public sealed class UnknownSource : IValueSource
    {
        public int Value => -1;
    }

    public readonly struct ValueDestination
    {
        public ValueDestination(int value) => Value = value;
        public int Value { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<IValueSource, object?>()
                .ForDerived<ValueSource, ValueDestination>()
                .ForDerived<OptionalSource, ValueDestination?>()
                .Convert(_ => new object());
            builder.Map<ValueSource, ValueDestination>()
                .Convert(source => new ValueDestination(source.Value));
            builder.Map<OptionalSource, ValueDestination?>()
                .Convert(source => new ValueDestination(source.Value));
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<IValueSource, object?>)new TestMapper();
            IValueSource source = new ValueSource(7);
            var created = mapper.Create(source);
            var updated = mapper.Update(
                source,
                (object)new ValueDestination(1));
            IValueSource optional = new OptionalSource(11);
            var nullableFromNull = mapper.Update(optional, null);
            var nullableFromBox = mapper.Update(
                optional,
                (object)new ValueDestination(2));
            var fallback = mapper.Create(new UnknownSource());

            if (created is not ValueDestination { Value: 7 } ||
                updated is not ValueDestination { Value: 7 } ||
                nullableFromNull is not ValueDestination { Value: 11 } ||
                nullableFromBox is not ValueDestination { Value: 11 } ||
                fallback is null ||
                fallback.GetType() != typeof(object))
            {
                throw new InvalidOperationException(
                    "Value-type polymorphic mapping is incorrect.");
            }

            try
            {
                mapper.Update(source, null);
                throw new InvalidOperationException(
                    "A null non-nullable value destination was accepted.");
            }
            catch (PolymorphicDestinationTypeMismatchException exception)
            {
                if (exception.ExpectedDestinationType !=
                        typeof(ValueDestination) ||
                    exception.ActualDestinationType is not null)
                {
                    throw new InvalidOperationException(
                        "The value mismatch exception is incorrect.");
                }
            }
        }
    }
}
""";

        var result = GeneratorTestDriver.Run(
            "RuntimePolymorphismValues",
            source,
            LanguageVersion.CSharp9);

        Assert.Multiple(() =>
        {
            Assert.That(result.EffectiveDiagnostics, Is.Empty);
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
        });

        GeneratedCodeExecution.AssertScenario(
            "runtime polymorphism values",
            result.OutputCompilation,
            "TestCase.Scenario");
    }
}
