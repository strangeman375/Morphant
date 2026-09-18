using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Morphant.Generator.UnitTests.GeneratorFailureDiagnosticsTests;

// Instrument an isolated copy of the production assembly. All pipeline wiring,
// guards and caches remain real; the shipped generator has no test hooks.
internal sealed class FaultInjectingGenerator : IIncrementalGenerator, IDisposable
{
    private readonly AssemblyLoadContext _context = new("generator-failure-test", isCollectible: true);
    private readonly IIncrementalGenerator _generator;

    public int Calls { get; private set; }
    public int Failures { get; private set; }
    public bool Armed { get; set; } = true;

    public FaultInjectingGenerator(string point, int skip = 0, CancellationTokenSource? cancellation = null)
    {
        using var module = ModuleDefinition.ReadModule(typeof(MorphantGenerator).Assembly.Location);
        var types = AllTypes(module.Types).ToArray();
        var generatorType = types.Single(type => type.FullName == "Morphant.Generator.MorphantGenerator");
        var hook = new FieldDefinition("TestFault", Mono.Cecil.FieldAttributes.Public | Mono.Cecil.FieldAttributes.Static,
            module.ImportReference(typeof(Action)));
        generatorType.Fields.Add(hook);

        var method = types.SelectMany(type => type.Methods)
            .Where(method => method.HasBody && Matches(method, point)).Single();
        var il = method.Body.GetILProcessor();
        var first = method.Body.Instructions[0];
        il.InsertBefore(first, il.Create(OpCodes.Ldsfld, hook));
        il.InsertBefore(first, il.Create(OpCodes.Callvirt, module.ImportReference(typeof(Action).GetMethod("Invoke")!)));

        using var bytes = new MemoryStream();
        module.Write(bytes);
        bytes.Position = 0;
        var assembly = _context.LoadFromStream(bytes);
        var type = assembly.GetType("Morphant.Generator.MorphantGenerator", throwOnError: true)!;
        type.GetField("TestFault", BindingFlags.Static | BindingFlags.Public)!.SetValue(null, (Action)(() =>
        {
            var call = Calls++;
            if (!Armed || call != skip) return;
            Failures++;
            if (cancellation is not null)
            {
                cancellation.Cancel();
                throw new OperationCanceledException(cancellation.Token);
            }
            throw new InjectedFailure();
        }));
        _generator = (IIncrementalGenerator)Activator.CreateInstance(type, nonPublic: true)!;
    }

    public void Initialize(IncrementalGeneratorInitializationContext context) => _generator.Initialize(context);
    public void Dispose() => _context.Unload();

    private static bool Matches(MethodDefinition method, string point) => point switch
    {
        "GlobalExtensionCache" => method.DeclaringType.FullName ==
            "Morphant.Generator.Incrementality.ExtensionLookupDependencies/Index" && method.Name == "BuildGlobalTypes",
        "TypeContractCache" => method.DeclaringType.FullName ==
            "Morphant.Generator.Incrementality.TypeContractDependencies" && method.Name == "BuildTypeDependencies",
        "SemanticModel" => method.DeclaringType.FullName ==
            "Morphant.Generator.TypeMapperGeneration.GeneratedMapperSyntax" && method.Name == "get_SemanticModel",
        "SharedConstruction" => method.DeclaringType.FullName ==
            "Morphant.Generator.TypeMapperGeneration.SharedConstructionLowerer" && method.Name == "Bind",
        "LocalNames" => method.DeclaringType.FullName ==
            "Morphant.Generator.TypeMapperGeneration.TransferredLocalNames" && method.Name == "Restore",
        "ExtensionCalls" => method.DeclaringType.FullName ==
            "Morphant.Generator.TypeMapperGeneration.ExtensionInvocationSimplifier" && method.Name == "Simplify",
        "FinalizeSource" => method.DeclaringType.DeclaringType?.FullName ==
            "Morphant.Generator.TypeMapperGeneration.TypeMapperModelBuilder" &&
            method.Name.Contains("g__FinalizeSource|", StringComparison.Ordinal),
        "Output" => method.DeclaringType.DeclaringType?.FullName ==
            "Morphant.Generator.TypeMapperGeneration.TypeMapperPipeline" &&
            method.Body.Instructions.Any(instruction => instruction.Operand is MethodReference
                { Name: "AddSource", DeclaringType.FullName: "Microsoft.CodeAnalysis.SourceProductionContext" }),
        "Initialize" => method.DeclaringType.FullName == "Morphant.Generator.MorphantGenerator" &&
            method.Name == "InitializeCore",
        _ => throw new ArgumentOutOfRangeException(nameof(point), point, "Unknown production failure point.")
    };

    private static IEnumerable<TypeDefinition> AllTypes(IEnumerable<TypeDefinition> types) =>
        types.SelectMany(type => new[] { type }.Concat(AllTypes(type.NestedTypes)));

    private sealed class InjectedFailure() : Exception("deliberate production failure")
    {
        public override string ToString() => "InjectedFailure: deliberate production failure";
    }
}
