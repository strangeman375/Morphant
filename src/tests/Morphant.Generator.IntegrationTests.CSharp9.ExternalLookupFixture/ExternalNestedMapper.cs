using System;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.ExternalLookupFixture
{
    public interface IExternalNestedSource
    {
        int Value { get; }
    }

    public sealed class ExternalNestedSource : IExternalNestedSource
    {
        public ExternalNestedSource(int value)
        {
            Value = value;
        }

        public int Value { get; }
    }

    public sealed class ExternalNestedDestination
    {
        public ExternalNestedDestination(int value)
        {
            Value = value;
        }

        public int Value { get; }
    }

    public sealed class ExternalNestedMapper :
        ITypeMapper<IExternalNestedSource, ExternalNestedDestination>
    {
        public int Calls { get; private set; }

        public ExternalNestedDestination Create(
            IExternalNestedSource? source,
            MappingContext context)
        {
            Calls++;
            return new ExternalNestedDestination(source?.Value + 10 ?? -1);
        }

        public ExternalNestedDestination Update(
            IExternalNestedSource? source,
            ExternalNestedDestination? destination,
            MappingContext context) =>
            throw new NotSupportedException();
    }
}
