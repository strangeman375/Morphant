#nullable enable
using System;
using Morphant;
namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.ConditionalFallbackMembers.Calls
{
    public static class Operations
    {
        public static int Describe(this object value) => 11;
        public static int Independent(this int value) => value * 2;
        public static void Record(this object value) => Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.ConditionalFallbackMembers.Source.Records++;
    }
}
