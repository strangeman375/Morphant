using Morphant.Context;

namespace Morphant.Generator.UnitTests.TestAssets;

public interface IReferencedNestedSource
{
    int Value { get; }
}

public sealed class ReferencedNestedSource : IReferencedNestedSource
{
    public ReferencedNestedSource(int value)
    {
        Value = value;
    }

    public int Value { get; }
}

public sealed class ReferencedNestedDestination
{
    public ReferencedNestedDestination(int value)
    {
        Value = value;
    }

    public int Value { get; }
}

public sealed class ReferencedNestedMapper :
    ITypeMapper<IReferencedNestedSource, ReferencedNestedDestination>
{
    public int Calls { get; private set; }

    public ReferencedNestedDestination Map(
        IReferencedNestedSource? source,
        MappingContext context)
    {
        Calls++;
        return new ReferencedNestedDestination(source?.Value + 10 ?? -1);
    }

    public ReferencedNestedDestination Map(
        IReferencedNestedSource? source,
        ReferencedNestedDestination? destination,
        MappingContext context) =>
        throw new NotSupportedException();
}
