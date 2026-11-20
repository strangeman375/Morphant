using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9
{
    public sealed class Customer
    {
        public string Name { get; set; } = string.Empty;
    }

    public sealed class CustomerDto
    {
        public string Name { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public sealed partial class CSharp9Mapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Customer, CustomerDto>();
    }
}
