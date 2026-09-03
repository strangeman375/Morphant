// Compiled integration scenario: unknown runtime source handling
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.RuntimePolymorphismUnknown_b82d0003
{
    public interface IRoot { }
    public interface IKnown : IRoot { }
    public sealed class Known : IKnown { }
    public sealed class Unknown : IRoot { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<IRoot, object>()
                .ForDerived<IKnown, string>()
                .UnknownDerivedTypeHandling(
                    UnknownDerivedTypeHandling.Throw)
                .Convert(source => source is null ? "null" : "base");
            builder.Map<IKnown, string>()
                .Convert(_ => "known");
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<IRoot, object>)new TestMapper();

            if (!Equals(mapper.Create(new Known()), "known") ||
                !Equals(mapper.Create(null), "null"))
            {
                throw new InvalidOperationException(
                    "Known or null dispatch is incorrect.");
            }

            try
            {
                mapper.Create(new Unknown());
                throw new InvalidOperationException(
                    "An unknown derived source was accepted.");
            }
            catch (UnmatchedPolymorphicMappingException exception)
            {
                if (exception.SourceType != typeof(IRoot) ||
                    exception.DestinationType != typeof(object) ||
                    exception.ActualSourceType != typeof(Unknown))
                {
                    throw new InvalidOperationException(
                        "The unmatched exception lost runtime details.");
                }
            }
        }
    }
}
