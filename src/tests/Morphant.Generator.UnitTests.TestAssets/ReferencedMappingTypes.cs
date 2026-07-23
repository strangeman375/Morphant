namespace Morphant.Generator.UnitTests.TestAssets;

public class ReferencedMappingSource
{
    public int PublicProperty { get; set; }

    public int PublicField;

    internal int InternalProperty { get; set; }

    internal int InternalField = 0;

    protected internal int ProtectedInternalProperty { get; set; }
}

public class ReferencedMappingDestination
{
    public int PublicProperty { get; set; }

    public int PublicField;

    internal int InternalProperty { get; set; }

    internal int InternalField = 0;

    protected internal int ProtectedInternalProperty { get; set; }
}
