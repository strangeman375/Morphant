// Compiled integration scenario: Create and strict Update dispatch
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.RuntimePolymorphismDispatch_b82d0005
{
    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<object, object>()
                .ForDerived<string, string>()
                .Convert(source => source!);
            builder.Map<string, string>()
                .Convert(source => source!);
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<object, object>)new TestMapper();
            object source = "derived";
            var created = mapper.Create(source);
            var updated = mapper.Update(source, "previous");
            var updatedFromNull = mapper.Update(source, null);

            if (created is not string ||
                updated is not string ||
                updatedFromNull is not string)
            {
                throw new InvalidOperationException(
                    "The derived string mapping was not selected.");
            }

            try
            {
                mapper.Update(source, new object());
                throw new InvalidOperationException(
                    "An incompatible destination was accepted.");
            }
            catch (PolymorphicDestinationTypeMismatchException exception)
            {
                if (exception.SourceType != typeof(object) ||
                    exception.DestinationType != typeof(object) ||
                    exception.ActualSourceType != typeof(string) ||
                    exception.BranchSourceType != typeof(string) ||
                    exception.ExpectedDestinationType != typeof(string) ||
                    exception.ActualDestinationType != typeof(object))
                {
                    throw new InvalidOperationException(
                        "The mismatch exception lost its branch details.");
                }
            }
        }
    }
}
