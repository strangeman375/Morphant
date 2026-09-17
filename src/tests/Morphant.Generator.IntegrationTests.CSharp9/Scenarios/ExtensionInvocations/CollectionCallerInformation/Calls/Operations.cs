namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.CollectionCallerInformation.Calls
{
    public static class Operations
    {
        public static int Twice(this int value)
        {
            Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.CollectionCallerInformation.Trace.Events.Add("value:" + value);
            return value * 2;
        }
    }
}
