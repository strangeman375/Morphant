#nullable enable
using System;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp11.Scenarios.ValueTypeConstruction
{
    public sealed class Source { public int Id { get; set; } }
    public struct ExplicitDestination
    {
        public ExplicitDestination() { Stamp = 73; Id = 0; }
        public int Stamp { get; set; }
        public int Id { get; set; }
    }
    public readonly record struct ReadonlyDestination(int Id);

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, ExplicitDestination>();
            builder.Map<Source, ExplicitDestination?>();
            builder.Map<Source, ReadonlyDestination>();
        }
    }

    public static class Scenario
    {
        public static void VerifyExplicitConstructor(string operation)
        {
            var concrete = new TestMapper();
            var mapper = (ITypeMapper<Source, ExplicitDestination>)concrete;
            var source = new Source { Id = 11 };
            var result = operation switch
            {
                "Create" => mapper.Create(source),
                "CreateNull" => mapper.Create(null),
                "UpdateDefault" => mapper.Update(source, default),
                "UpdateNullableNull" => ((ITypeMapper<Source, ExplicitDestination?>)concrete)
                    .Update(source, null) ?? throw new InvalidOperationException("Update must construct a destination."),
                _ => throw new ArgumentOutOfRangeException(nameof(operation))
            };
            var expectedStamp = operation is "Create" or "UpdateNullableNull" ? 73 : 0;
            var expectedId = operation == "CreateNull" ? 0 : 11;
            if (result.Stamp != expectedStamp || result.Id != expectedId)
            {
                throw new InvalidOperationException(
                    $"{operation}: expected Stamp={expectedStamp}, Id={expectedId}; got {result}.");
            }
        }

        public static void VerifyReadonlyRecord(bool update)
        {
            ITypeMapper<Source, ReadonlyDestination> mapper = new TestMapper();
            var source = new Source { Id = 11 };
            var result = update ? mapper.Update(source, new ReadonlyDestination(19)) : mapper.Create(source);
            if (result.Id != (update ? 19 : 11))
            {
                throw new InvalidOperationException("Create must bind the record constructor; Update must preserve its readonly member.");
            }
        }
    }
}
