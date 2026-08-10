namespace Morphant.Generator.IntegrationTests;

[TestFixture]
internal sealed class TypeMapperExpressionTransferTests
{
    [Test]
    public void Preserves_caller_information_in_all_structured_surfaces()
    {
        global::Morphant.Generator.IntegrationTests.Latest.Scenarios.CallerInfo_a11ce005.Scenario.Verify();
    }

    [Test]
    public void Rejects_file_local_helpers_in_all_structured_surfaces()
    {
        global::Morphant.Generator.IntegrationTests.CSharp11.Scenarios.FileLocal_a11ce006.Scenario.Verify();
    }
}
