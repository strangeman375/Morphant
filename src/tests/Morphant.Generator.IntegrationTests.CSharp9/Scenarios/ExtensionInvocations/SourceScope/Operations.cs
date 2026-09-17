#nullable enable
using Morphant;
namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.SourceScope
{
    public static class CloserOperations
    {
        public static int Describe(this string value) => 99;
    }

}
