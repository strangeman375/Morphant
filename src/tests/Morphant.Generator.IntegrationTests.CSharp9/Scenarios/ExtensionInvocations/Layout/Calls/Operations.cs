#nullable enable
using System;
using Morphant;
using Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.Layout.Calls;
namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.Layout.Calls
{
    public static class Operations
    {
        public static string Echo(this string value, int number) => value + number;
        public static int Touches;
        public static void Touch(this string value) { Touches++; }
    }
}
