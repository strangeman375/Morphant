namespace Morphant.Exceptions;

/// <summary>
/// Represents construction of a mapper without a service provider.
/// </summary>
public sealed class MappingServiceProviderMissingException : MorphantException
{
    public MappingServiceProviderMissingException()
        : base("A service provider is required to create a Morphant mapper.")
    {
    }
}
