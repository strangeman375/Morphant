#nullable enable
using System;
using System.Collections.Generic;
using Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.ImportIsolation.Calls;
using Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.ImportIsolation.SafeCalls;
using Morphant;
namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.ImportIsolation.SafeCalls
{
    public static class SafeOperations
    {
        public static int Twice(this int value) => value * 2;
    }
}
