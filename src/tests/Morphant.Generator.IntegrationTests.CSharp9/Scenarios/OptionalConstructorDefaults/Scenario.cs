#nullable enable
using System;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.OptionalConstructorDefaults
{
    public sealed class Source { }
    public sealed class Destination
    {
        public Destination(decimal amount = 1.25m, DayOfWeek day = DayOfWeek.Friday, params string[] tags)
        {
            Amount = amount;
            Day = day;
            Tags = tags;
        }
        public decimal Amount { get; }
        public DayOfWeek Day { get; }
        public string[] Tags { get; }
    }
    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Source, Destination>();
    }
    public static class Scenario
    {
        public static void Verify(bool update)
        {
            ITypeMapper<Source, Destination> mapper = new TestMapper();
            var source = new Source();
            var result = update ? mapper.Update(source, null) : mapper.Create(source);
            if (result.Amount != 1.25m || result.Day != DayOfWeek.Friday || result.Tags.Length != 0)
            {
                throw new InvalidOperationException("Construction must preserve decimal and enum defaults and supply an empty params array.");
            }
        }
    }
}
