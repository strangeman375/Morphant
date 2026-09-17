#nullable enable
using Morphant;
using System.Runtime.CompilerServices;
using Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.CallerInformation.Calls;
namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.CallerInformation.Calls
{
    public static class Operations
    {
        public static string Describe(this int value, int added = 7,
            [CallerMemberName] string member = "", [CallerFilePath] string file = "",
            [CallerLineNumber] int line = 0,
            [CallerArgumentExpression("value")] string expression = "") =>
            value + ":" + added + ":" + member + ":" + file + ":" + line + ":" + expression;
    }
}
