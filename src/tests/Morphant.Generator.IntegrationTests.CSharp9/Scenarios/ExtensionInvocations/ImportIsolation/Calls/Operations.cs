#nullable enable
using System;
using System.Collections.Generic;
using Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.ImportIsolation.Calls;
using Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.ImportIsolation.SafeCalls;
using Morphant;
namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.ImportIsolation.Calls
{
    public static class Operations
    {
        public static int Extra(this string value) => value.Length;
        public static IEnumerable<int> Select(this int[] value, Func<int, int> selector) => new[] { 99 };
        public static int Describe(this string value) => 99;
        public static int Count(this string value) => 99;
    }
}
