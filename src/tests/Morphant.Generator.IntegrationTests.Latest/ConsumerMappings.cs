namespace Morphant.Generator.IntegrationTests.Latest;

public sealed class Customer
{
    public string Name { get; set; } = string.Empty;
}

public sealed class CustomerDto
{
    public string Name { get; set; } = string.Empty;
}

public interface ILabelFormatter
{
    string Format(int value);
}

public sealed class Order
{
    public int Number { get; set; }

    public Customer Customer { get; set; } = new();
}

public sealed class OrderDto
{
    public OrderDto(int number, string label)
    {
        Number = number;
        Label = label;
    }

    public int Number { get; }

    public string Label { get; }

    public required CustomerDto Customer { get; set; }
}

public sealed class Summary
{
    public Customer Customer { get; set; } = new();
}

public sealed record SummaryDto(string CustomerName);

public sealed record GenericSource<T>(T Value);

public sealed record GenericDestination<T>(T Value);

public sealed class NullableNumber
{
    public int Value { get; set; }
}

[MorphantMapper]
public sealed partial class LatestMapper : TypeMapper<LatestMapper>
{
    private readonly ILabelFormatter _formatter;

    public LatestMapper(ILabelFormatter formatter)
    {
        _formatter = formatter;
    }

    protected override void Configure(MapperBuilder builder)
    {
        builder.Map<Customer, CustomerDto>();

        builder.Map<Order, OrderDto>()
            .Construct(source =>
                new(source.Number, _formatter.Format(source.Number)))
            .Members((source, _) => new()
            {
                Customer = Map<CustomerDto>(source.Customer)
            });

        builder.Map<Summary, SummaryDto>()
            .Convert((source, _, context) =>
                new(context.Mapper
                    .Map<Customer, CustomerDto>(source!.Customer)
                    .Name));

        builder
            .Map<GenericSource<int>, GenericDestination<string>>()
            .Construct(source =>
                new(_formatter.Format(source.Value)));

        builder.Map<NullableNumber, int?>()
            .ConstructUsing(source => source.Value);
    }
}
