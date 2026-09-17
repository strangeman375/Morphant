#nullable enable
using System;
using Morphant;
using Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.Chains.Calls;
namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.Chains.Calls
{
    public static class Operations
    {
        public static T Identity<T>(this T value) => value;
        public static string Echo(this string value) => value;
        public static int Add(this int value) => value + 1;
        public static int Touches;
        public static void Touch(this string value) { Touches++; }
    }
}
