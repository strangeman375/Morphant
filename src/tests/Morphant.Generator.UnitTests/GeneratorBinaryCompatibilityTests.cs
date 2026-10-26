namespace Morphant.Generator.UnitTests;

public class GeneratorBinaryCompatibilityTests
{
    [TestCase("Microsoft.CodeAnalysis")]
    [TestCase("Microsoft.CodeAnalysis.CSharp")]
    public void Generator_references_the_minimum_supported_Roslyn(string assemblyName)
    {
        var reference = typeof(MorphantGenerator).Assembly.GetReferencedAssemblies()
            .Single(assembly => assembly.Name == assemblyName);

        Assert.That(reference.Version, Is.EqualTo(new Version(4, 4, 0, 0)));
    }
}
