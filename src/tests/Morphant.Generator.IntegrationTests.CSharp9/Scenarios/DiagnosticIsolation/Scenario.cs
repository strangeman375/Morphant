#nullable enable
#pragma warning disable MORPH0019, MORPH0030, MORPH0052
using System;
using Morphant;
using Morphant.Context;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.DiagnosticIsolation
{
    public class Source { public int Value { get; set; } }
    public class First { public int Value { get; set; } }
    public class Second { public int Value { get; set; } }
    public class Third { public int Value { get; set; } }
    public class Valid { public int Value { get; set; } }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        public static int CallbackCalls { get; set; }

        protected override void Configure(MapperBuilder builder)
        {
            var captured = new Third();
            builder.Map<Source, First>().ForDerived<Source, First>();
            builder.Map<Source, Second>()
                .Convert(_ => MakeSecond())
                .Convert(_ => MakeSecond());
            builder.Map<Source, Third>().Convert(_ => captured);
            builder.Map<Source, Valid>();
        }

        private static Second MakeSecond()
        {
            CallbackCalls++;
            return new Second();
        }
    }

    public static class Scenario
    {
        public static void Verify(bool update)
        {
            TestMapper.CallbackCalls = 0;
            var mapper = new TestMapper();
            var source = new Source { Value = 42 };
            var first = new First { Value = 1 };
            var second = new Second { Value = 2 };
            var third = new Third { Value = 3 };
            var previous = new Valid { Value = 4 };

            VerifyFailure((ITypeMapper<Source, First>)mapper, source, first, update);
            VerifyFailure((ITypeMapper<Source, Second>)mapper, source, second, update);
            VerifyFailure((ITypeMapper<Source, Third>)mapper, source, third, update);

            var valid = (ITypeMapper<Source, Valid>)mapper;
            var result = update ? valid.Update(source, previous) : valid.Create(source);
            if (result.Value != 42 || (update && !ReferenceEquals(result, previous)) ||
                first.Value != 1 || second.Value != 2 || third.Value != 3 || TestMapper.CallbackCalls != 0)
            {
                throw new InvalidOperationException(
                    "Invalid pairs must fail without executing callbacks or mutating destinations, while the valid pair remains executable.");
            }
        }

        private static void VerifyFailure<TDestination>(ITypeMapper<Source, TDestination> mapper,
            Source source, TDestination previous, bool update)
        {
            try
            {
                _ = update ? mapper.Update(source, previous) : mapper.Create(source);
            }
            catch (MappingConfigurationException exception)
                when (exception.Operation == (update ? MappingOperation.Update : MappingOperation.Create) &&
                      exception.SourceType == typeof(Source) && exception.DestinationType == typeof(TDestination))
            {
                return;
            }

            throw new InvalidOperationException("A suppressed invalid pair lost its typed failure.");
        }
    }
}
