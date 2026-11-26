#nullable enable
using System;
using Morphant.Context;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.HandwrittenStandaloneDispatch
{
    public static class Scenario
    {
        public static void Verify()
        {
            ITypeMapper<object, string> mapper = new ManualMapper();
            try
            {
                mapper.Create<object, string>("source");
            }
            catch (MappingNotFoundException error) when (
                error.Operation == MappingOperation.Update &&
                error.SourceType == typeof(string) &&
                error.DestinationType == typeof(string))
            {
                return;
            }

            throw new InvalidOperationException("Nested lookup accepted a pair through source variance.");
        }

        private sealed class ManualMapper : ITypeMapper<object, string>
        {
            public string Create(object? source, MappingContext context) =>
                context.Mapper.Map<string, string>("nested", "existing");

            public string Update(object? source, string? destination, MappingContext context) =>
                destination ?? string.Empty;
        }
    }
}
