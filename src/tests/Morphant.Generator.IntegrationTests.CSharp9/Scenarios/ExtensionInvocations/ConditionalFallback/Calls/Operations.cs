#nullable enable
using Morphant;
namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.ConditionalFallback.Calls
{
    public static class Operations
    {
        public static string Echo(this object value) => "original";
        public static int LengthPlus(this string value) => value.Length + 1;
    }
}
