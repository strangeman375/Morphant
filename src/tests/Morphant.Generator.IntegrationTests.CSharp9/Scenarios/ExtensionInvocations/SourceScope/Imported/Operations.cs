#nullable enable
using Morphant;
namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.SourceScope.Imported
{
    public static class Operations
    {
        public static int Describe(this object value) => 11;
        public static int Independent(this int value) => value * 2;
    }
}
