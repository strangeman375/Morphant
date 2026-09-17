#nullable enable
namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.Surfaces.Extensions
{
    public static class TextOperations
    {
        public static string Echo(this string value, int number) => value + number;
    }
}
