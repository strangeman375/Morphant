using System.Reflection;

namespace Morphant.Generator.UnitTests.PublicContractTests;

[TestFixture]
internal sealed class MappingContextContractTests
{
    [Test]
    public void MappingOperation_declares_exact_single_operation_values()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                Enum.GetNames<MappingOperation>(),
                Is.EqualTo(new[]
                {
                    nameof(MappingOperation.Create),
                    nameof(MappingOperation.Update)
                }));
            Assert.That((int)MappingOperation.Create, Is.Zero);
            Assert.That((int)MappingOperation.Update, Is.EqualTo(1));
            Assert.That(default(MappingOperation), Is.EqualTo(MappingOperation.Create));
            Assert.That(
                typeof(MappingOperation).IsDefined(
                    typeof(FlagsAttribute),
                    inherit: false),
                Is.False);
        });
    }

    [Test]
    public void MappingContext_is_an_immutable_value_type_frame()
    {
        var type = typeof(MappingContext);
        var properties = type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(static property => property.Name, StringComparer.Ordinal)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(type.IsValueType, Is.True);
            Assert.That(
                type.CustomAttributes.Any(static attribute =>
                    attribute.AttributeType.FullName ==
                    "System.Runtime.CompilerServices.IsReadOnlyAttribute"),
                Is.True);
            Assert.That(type.GetConstructors(), Is.Empty);
            Assert.That(
                properties.Select(static property => property.Name),
                Is.EqualTo(new[]
                {
                    nameof(MappingContext.Mapper),
                    nameof(MappingContext.Operation)
                }));
            Assert.That(
                properties.Single(static property =>
                    property.Name == nameof(MappingContext.Operation))
                    .PropertyType,
                Is.EqualTo(typeof(MappingOperation)));
            Assert.That(
                properties.Single(static property =>
                    property.Name == nameof(MappingContext.Mapper))
                    .PropertyType,
                Is.EqualTo(typeof(IMapper)));
            Assert.That(
                properties.Select(static property => property.SetMethod),
                Is.All.Null);
        });
    }
}
