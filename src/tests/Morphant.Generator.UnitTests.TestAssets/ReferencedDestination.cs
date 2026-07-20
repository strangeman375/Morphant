namespace Morphant.Generator.UnitTests.TestAssets;

public class ReferencedDestination
{
    public int PublicProperty { get; set; }

    internal int InternalProperty { get; set; }

    protected internal int ProtectedInternalProperty { get; set; }

    public int PropertyWithPrivateSetter { get; private set; }

    private int PrivateProperty { get; set; }
}
