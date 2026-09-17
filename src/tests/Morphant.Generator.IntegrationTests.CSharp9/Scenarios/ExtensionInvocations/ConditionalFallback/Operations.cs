#nullable enable
using Morphant;
namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.ConditionalFallback
{
    public static class Closer
    {
        public static string Echo(this string value) => "different";
    }

}
