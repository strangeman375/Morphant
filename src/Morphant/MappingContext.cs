namespace Morphant;

public abstract class MappingContext
{
    private protected MappingContext()
    {
    }

    public IContextualMapper Mapper { get; internal set; } = null!;
}
