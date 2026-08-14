namespace Morphant.Generator.IntegrationTests;

internal static class IntegrationTestEnvironment
{
    public static string RepositoryRoot { get; } = FindRepositoryRoot();

    public static string BuildConfiguration { get; } = GetBuildConfiguration();

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(
                 TestContext.CurrentContext.TestDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(
                    directory.FullName,
                    "src",
                    "Morphant.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException(
            "Could not locate the Morphant repository root.");
    }

    private static string GetBuildConfiguration()
    {
        const string configurationAttributeName =
            "System.Reflection.AssemblyConfigurationAttribute";
        var configuration = typeof(IntegrationTestEnvironment).Assembly
            .GetCustomAttributesData()
            .Single(attribute =>
                attribute.AttributeType.FullName ==
                    configurationAttributeName)
            .ConstructorArguments.Single().Value as string;

        return configuration ?? throw new InvalidOperationException(
            "Could not determine the integration-test build configuration.");
    }
}
