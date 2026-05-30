namespace Morphant;

public abstract class MappingContext
{
    private protected MappingContext()
    {
    }

    public IMapper Mapper { get; internal set; } = null!;
}
