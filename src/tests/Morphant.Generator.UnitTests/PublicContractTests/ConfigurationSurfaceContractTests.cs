using System.Reflection;
using Morphant.Markers;
using Morphant.Members;

namespace Morphant.Generator.UnitTests.PublicContractTests;

[TestFixture]
internal sealed class ConfigurationSurfaceContractTests
{
    [Test]
    public void MappingMode_declares_exact_flags_and_default()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                Enum.GetNames<MappingMode>(),
                Is.EqualTo(new[]
                {
                    nameof(MappingMode.Default),
                    nameof(MappingMode.Create),
                    nameof(MappingMode.Update),
                    nameof(MappingMode.CreateAndUpdate)
                }));
            Assert.That((int)MappingMode.Default, Is.Zero);
            Assert.That((int)MappingMode.Create, Is.EqualTo(1));
            Assert.That((int)MappingMode.Update, Is.EqualTo(2));
            Assert.That((int)MappingMode.CreateAndUpdate, Is.EqualTo(3));
            Assert.That(
                MappingMode.CreateAndUpdate,
                Is.EqualTo(MappingMode.Create | MappingMode.Update));
            Assert.That(
                typeof(MappingMode).IsDefined(
                    typeof(FlagsAttribute),
                    inherit: false),
                Is.True);
        });
    }

    [Test]
    public void Renamed_settings_declare_exact_values()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                Enum.GetNames<NullDestinationHandling>(),
                Is.EqualTo(new[]
                {
                    nameof(NullDestinationHandling.Default),
                    nameof(NullDestinationHandling.Create),
                    nameof(NullDestinationHandling.Throw)
                }));
            Assert.That(
                Enum.GetValues<NullDestinationHandling>()
                    .Select(static value => (int)value),
                Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(
                Enum.GetNames<MemberSelection>(),
                Is.EqualTo(new[]
                {
                    nameof(MemberSelection.Default),
                    nameof(MemberSelection.Auto),
                    nameof(MemberSelection.Explicit)
                }));
            Assert.That(
                Enum.GetValues<MemberSelection>()
                    .Select(static value => (int)value),
                Is.EqualTo(new[] { 0, 1, 2 }));
        });
    }

    [Test]
    public void Builders_expose_only_current_setting_names()
    {
        var rootMethods = typeof(MapperBuilder)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(static method => method.DeclaringType != typeof(object))
            .Select(static method => method.Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
        var pairMethods = typeof(MapperBuilder<,>)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(static method => method.Name)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(
                rootMethods,
                Does.Contain(nameof(MapperBuilder.MappingMode)));
            Assert.That(rootMethods, Does.Contain("MemberSelection"));
            Assert.That(rootMethods, Does.Not.Contain("MemberMatching"));
            Assert.That(rootMethods, Does.Not.Contain("TemplateMode"));
            Assert.That(
                rootMethods,
                Does.Not.Contain("NullabilityMismatchValidation"));
            Assert.That(pairMethods, Does.Not.Contain("Construct"));
            Assert.That(pairMethods, Does.Not.Contain("Members"));
            Assert.That(pairMethods, Does.Not.Contain("Convert"));

            var mapMethod = typeof(MapperBuilder)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Single(static method => method.Name == nameof(MapperBuilder.Map));
            Assert.That(
                mapMethod.GetParameters().Single().DefaultValue,
                Is.EqualTo(MappingMode.Default));
        });
    }

    [Test]
    public void TypeMapper_exposes_the_six_final_marker_families()
    {
        var methods = typeof(TypeMapper)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(static method => method.IsFamily)
            .OrderBy(static method => method.Name, StringComparer.Ordinal)
            .ThenBy(static method => method.GetGenericArguments().Length)
            .ThenBy(static method => method.GetParameters().Length)
            .ToArray();
        var nullability = new NullabilityInfoContext();
        var auto = methods.Where(static method => method.Name == "Auto").ToArray();
        var ignore = methods.Where(static method => method.Name == "Ignore").ToArray();
        var map = methods.Where(static method => method.Name == "Map").ToArray();
        var byFactory = methods.Single(static method =>
            method.Name == "ByFactory");

        Assert.Multiple(() =>
        {
            Assert.That(
                methods.Select(static method => method.Name),
                Is.EqualTo(new[]
                {
                    "Auto",
                    "Auto",
                    "ByConvention",
                    "ByFactory",
                    "Ignore",
                    "Ignore",
                    "Map",
                    "Map",
                    "Map",
                    "Map"
                }));
            Assert.That(
                methods.Count(static method => method.Name == "Map" &&
                    method.GetParameters().Length == 1),
                Is.EqualTo(2));
            Assert.That(
                methods.Count(static method => method.Name == "Map" &&
                    method.GetParameters().Length == 2),
                Is.EqualTo(2));
            Assert.That(
                methods.Where(static method => method.Name == "Map")
                    .Any(static method => method.GetParameters().Length == 0),
                Is.False);
            Assert.That(
                methods.Single(static method =>
                    method.Name == "ByConvention").ReturnType,
                Is.EqualTo(typeof(ByConventionMarker)));
            Assert.That(
                methods.Single(static method =>
                    method.Name == "ByFactory").ReturnType
                    .GetGenericTypeDefinition(),
                Is.EqualTo(typeof(IByFactoryMarker<>)));
            Assert.That(
                byFactory.GetParameters().Single().ParameterType
                    .GetGenericTypeDefinition(),
                Is.EqualTo(typeof(Func<>)));
            Assert.That(
                auto.Single(static method => !method.IsGenericMethod)
                    .ReturnType,
                Is.EqualTo(typeof(AutoMarker)));
            Assert.That(
                auto.Single(static method => method.IsGenericMethod)
                    .ReturnType.GetGenericTypeDefinition(),
                Is.EqualTo(typeof(AutoMarker<>)));
            Assert.That(
                ignore.Single(static method => !method.IsGenericMethod)
                    .ReturnType,
                Is.EqualTo(typeof(IgnoreMarker)));
            Assert.That(
                ignore.Single(static method => method.IsGenericMethod)
                    .ReturnType.GetGenericTypeDefinition(),
                Is.EqualTo(typeof(IgnoreMarker<>)));
            Assert.That(
                map.Where(static method => !method.IsGenericMethod)
                    .Select(static method => method.ReturnType),
                Is.All.EqualTo(typeof(MapMarker)));
            Assert.That(
                map.Where(static method => method.IsGenericMethod)
                    .Select(static method =>
                        method.ReturnType.GetGenericTypeDefinition()),
                Is.All.EqualTo(typeof(MapMarker<>)));
            Assert.That(
                map.SelectMany(static method => method.GetParameters())
                    .Select(static parameter => parameter.ParameterType),
                Is.All.EqualTo(typeof(object)));
            Assert.That(
                map.SelectMany(static method => method.GetParameters())
                    .Select(parameter =>
                        nullability.Create(parameter).ReadState),
                Is.All.EqualTo(NullabilityState.Nullable));
        });
    }

    [Test]
    public void ConstructorParameter_accepts_values_and_all_member_markers()
    {
        var sources = typeof(ConstructorParameter<int>)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(static method => method.Name == "op_Implicit")
            .Select(static method => method.GetParameters().Single().ParameterType)
            .OrderBy(static type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        Assert.That(
            sources,
            Is.EqualTo(new[]
            {
                typeof(AutoMarker),
                typeof(AutoMarker<int>),
                typeof(IgnoreMarker),
                typeof(IgnoreMarker<int>),
                typeof(MapMarker),
                typeof(MapMarker<int>),
                typeof(int)
            }.OrderBy(static type => type.FullName, StringComparer.Ordinal)));
    }

    [Test]
    public void Removed_runtime_types_are_absent_from_the_public_contract()
    {
        var assembly = typeof(IMapper).Assembly;

        Assert.Multiple(() =>
        {
            Assert.That(assembly.GetType("Morphant.TemplateMode"), Is.Null);
            Assert.That(assembly.GetType("Morphant.IContextualMapper"), Is.Null);
            Assert.That(assembly.GetType("Morphant.MemberMatching"), Is.Null);
            Assert.That(
                assembly.GetType("Morphant.NullabilityMismatchValidation"),
                Is.Null);
            Assert.That(
                assembly.GetType("Morphant.Members.ConstructorMember`1"),
                Is.Null);
        });
    }
}
