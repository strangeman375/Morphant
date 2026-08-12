// Compiled integration scenario: TypeMapperDeclarativeValueTests::Rejects_mismatched_and_runtime_intrinsics_fail_closed
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0033

using System;
using Morphant;
using Morphant.Context;
using Morphant.Markers;
using Morphant.Members;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.DeclarativeValueFailures_4d90a103
{
    public sealed class Source
    {
        public int Number { get; init; }
    }

    public sealed class BadValueMember
    {
        public object Value { get; set; } = new();
    }

    public sealed class BadAutoMember
    {
        public object Value { get; set; } = new();
    }

    public sealed class BadIgnoreMember
    {
        public object Value { get; set; } = new();
    }

    public sealed class BadValueConstructor
    {
        public BadValueConstructor(object value)
        {
        }
    }

    public sealed class BadNullableValueMember
    {
        public string? Value { get; set; }
    }

    public sealed class BadHelperValueMember
    {
        public object Value { get; set; } = new();
    }

    public sealed class BadConsumedValueMember
    {
        public int Value { get; set; }
    }

    public sealed class BadHelperMarkerMember
    {
        public int Value { get; set; }
    }

    public sealed class BadRuntimeValue
    {
    }

    public sealed class BadRuntimeMethodGroup
    {
    }

    public sealed class BadRuntimeIndirectMethodGroup
    {
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, BadValueMember>()
                .Members((source, _) => new()
                {
                    Value = (object)Value<int>(source.Number)
                });

            builder.Map<Source, BadAutoMember>()
                .Members((_, __) => new()
                {
                    Value = (object)Auto<int>()
                });

            builder.Map<Source, BadIgnoreMember>()
                .Members((_, __) => new()
                {
                    Value = (object)Ignore<int>()
                });

            builder.Map<Source, BadValueConstructor>()
                .Construct(source => new(
                    (object)Value<int>(source.Number)));

            builder.Map<Source, BadNullableValueMember>()
                .Members((_, __) => new()
                {
#pragma warning disable CS8619
                    Value = (Member<string?>)
                        Value<string>("non-null")
#pragma warning restore CS8619
                });

            builder.Map<Source, BadHelperValueMember>()
                .Members((source, _) => new()
                {
                    Value = (object)Pin(source.Number)
                });

            builder.Map<Source, BadConsumedValueMember>()
                .Members((source, _) => new()
                {
                    Value = Consume(Value<int>(source.Number))
                });

            builder.Map<Source, BadHelperMarkerMember>()
                .Members((_, __) => new()
                {
                    Value = Automatic()
                });

            builder.Map<Source, BadRuntimeValue>()
                .ConstructUsing(_ =>
                    (BadRuntimeValue)(object)Value(
                        new BadRuntimeValue()));

            builder.Map<Source, BadRuntimeMethodGroup>()
                .ConstructUsing(_ => Build(Value<int>));

            builder.Map<Source, BadRuntimeIndirectMethodGroup>()
                .ConstructUsing(_ => BuildIndirect(ValueFactory));
        }

        private static Func<int, ValueMarker<int>> ValueFactory =>
            Value<int>;

        private static ValueMarker<int> Pin(int value) => Value(value);

        private static int Consume(ValueMarker<int> value)
        {
            _ = value;
            return 0;
        }

        private static AutoMarker<int> Automatic() => Auto<int>();

        private static BadRuntimeMethodGroup Build(
            Func<int, ValueMarker<int>> value)
        {
            _ = value;
            return new BadRuntimeMethodGroup();
        }

        private static BadRuntimeIndirectMethodGroup BuildIndirect(
            Func<int, ValueMarker<int>> value)
        {
            _ = value;
            return new BadRuntimeIndirectMethodGroup();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source { Number = 7 };

            AssertConfigurationFailure<BadValueMember>(mapper, source);
            AssertConfigurationFailure<BadAutoMember>(mapper, source);
            AssertConfigurationFailure<BadIgnoreMember>(mapper, source);
            AssertConfigurationFailure<BadValueConstructor>(mapper, source);
            AssertConfigurationFailure<BadNullableValueMember>(
                mapper,
                source);
            AssertConfigurationFailure<BadHelperValueMember>(mapper, source);
            AssertConfigurationFailure<BadConsumedValueMember>(
                mapper,
                source);
            AssertConfigurationFailure<BadHelperMarkerMember>(
                mapper,
                source);
            AssertConfigurationFailure<BadRuntimeValue>(mapper, source);
            AssertConfigurationFailure<BadRuntimeMethodGroup>(
                mapper,
                source);
            AssertConfigurationFailure<BadRuntimeIndirectMethodGroup>(
                mapper,
                source);
        }

        private static void AssertConfigurationFailure<TDestination>(
            TestMapper mapper,
            Source source)
        {
            try
            {
                ((ITypeMapper<Source, TDestination>)mapper).Create(
                    source,
                    default(MappingContext));
            }
            catch (global::Morphant.Exceptions.MappingConfigurationException)
            {
                return;
            }
            catch (global::Morphant.Exceptions.RuntimeInvocationNotSupportedException
                   exception)
            {
                throw new InvalidOperationException(
                    "A compile-time intrinsic leaked into runtime code.",
                    exception);
            }

            throw new InvalidOperationException(
                "An invalid declarative intrinsic was accepted.");
        }
    }
}
