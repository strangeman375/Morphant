// Compiled integration scenario: auto flattening
#nullable enable
#pragma warning disable CS1591

using System;
using System.Diagnostics.CodeAnalysis;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Flattening_9a31c001
{
    public sealed class Source
    {
        public string CustomerAddressCity { get; init; } = "root";

        public Customer? Customer { get; init; }
    }

    public sealed class NestedSource
    {
        public Customer? Customer { get; init; }
    }

    public sealed class Customer
    {
        public Address? Address { get; init; }
    }

    public sealed class Address
    {
        public string? City { get; init; }

        public string Code = string.Empty;

        public int Score { get; init; }
    }

    public sealed class Destination
    {
        public Destination(string customerAddressCity)
        {
            CustomerAddressCity = customerAddressCity;
        }

        public string CustomerAddressCity { get; }

        public int? CustomerAddressScore { get; set; }

        public int CustomerAddressRequiredScore { get; set; } = 41;

        public string CustomerAddressRequiredCity { get; set; } = "initial";

        public string? CustomerAddressCode { get; set; }
    }

    public sealed class FlattenedDestination
    {
        public string? CustomerAddressCity { get; set; }

        public int? CustomerAddressScore { get; set; }
    }

    public sealed class DisabledDestination
    {
        public string? CustomerAddressCity { get; set; }
    }

    public sealed class DisabledConstructorDestination
    {
        public DisabledConstructorDestination(
            string? customerAddressCity = "initial")
        {
            CustomerAddressCity = customerAddressCity;
        }

        public string? CustomerAddressCity { get; }
    }

    public sealed class NonNullableDestination
    {
        public string CustomerAddressCity { get; set; } = "initial";

        public int CustomerAddressScore { get; set; } = 41;
    }

    public sealed class ExplicitDestination
    {
        public string? CustomerAddressCity { get; set; }
    }

    public sealed class ExplicitConstructorDestination
    {
        public ExplicitConstructorDestination(string? customerAddressCity)
        {
            CustomerAddressCity = customerAddressCity;
        }

        public string? CustomerAddressCity { get; }
    }

    public sealed class ExplicitSelectionConstructorDestination
    {
        public ExplicitSelectionConstructorDestination(
            string? customerAddressCity)
        {
            CustomerAddressCity = customerAddressCity;
        }

        public string? CustomerAddressCity { get; }
    }

    public sealed class StructuredConstructorDestination
    {
        public StructuredConstructorDestination(string? customerAddressCity)
        {
            CustomerAddressCity = customerAddressCity;
        }

        public string? CustomerAddressCity { get; }
    }

    public sealed class AllowNullConstructorDestination
    {
        public AllowNullConstructorDestination(
            [AllowNull] string customerAddressCity)
        {
            CustomerAddressCity = customerAddressCity;
        }

        public string? CustomerAddressCity { get; }
    }

    public sealed class DisallowNullConstructorDestination
    {
        public DisallowNullConstructorDestination(
            [DisallowNull] string? customerAddressCity = "initial")
        {
            CustomerAddressCity = customerAddressCity;
        }

        public string? CustomerAddressCity { get; }
    }

#nullable disable annotations
    public sealed class ObliviousConstructorDestination
    {
        public ObliviousConstructorDestination(string customerAddressCity)
        {
            CustomerAddressCity = customerAddressCity;
        }

        public string CustomerAddressCity { get; }
    }
#nullable enable

    public sealed class TieredSource
    {
        public NumericDetails Details { get; init; } = new NumericDetails();

        public Profile Profile { get; init; } = new Profile();
    }

    public sealed class NumericDetails
    {
        public int Name { get; init; }
    }

    public sealed class Profile
    {
        public TextDetails Details { get; init; } = new TextDetails();
    }

    public sealed class TextDetails
    {
        public string Name { get; init; } = string.Empty;
    }

    public sealed class TieredDestination
    {
        public string DetailsName { get; set; } = string.Empty;
    }

    public sealed class DirectClaimSource
    {
        public int DetailsName { get; init; }

        public DirectClaimDetails Details { get; init; } =
            new DirectClaimDetails();
    }

    public sealed class DirectClaimDetails
    {
        public string Name { get; init; } = string.Empty;
    }

    public sealed class DirectClaimDestination
    {
        public string DetailsName { get; set; } = "initial";
    }

    public sealed class CaseFallbackSource
    {
        public NumericCaseCustomer customer { get; init; } =
            new NumericCaseCustomer();

        public TextCaseCustomer Customer { get; init; } =
            new TextCaseCustomer();
    }

    public sealed class NumericCaseCustomer
    {
        public int AddressCity { get; init; }
    }

    public sealed class TextCaseCustomer
    {
        public CaseAddress Address { get; init; } = new CaseAddress();
    }

    public sealed class CaseAddress
    {
        public string City { get; init; } = string.Empty;
    }

    public sealed class CaseFallbackDestination
    {
        public CaseFallbackDestination(string customerAddressCity)
        {
            CustomerAddressCity = customerAddressCity;
        }

        public string CustomerAddressCity { get; }
    }

    public sealed class CountingSource
    {
        private readonly CountingCustomer? _customer;

        public CountingSource(CountingCustomer? customer)
        {
            _customer = customer;
        }

        public int CustomerReads { get; private set; }

        public CountingCustomer? Customer
        {
            get
            {
                CustomerReads++;
                return _customer;
            }
        }
    }

    public sealed class CountingCustomer
    {
        private readonly CountingAddress? _address;

        public CountingCustomer(CountingAddress? address)
        {
            _address = address;
        }

        public int AddressReads { get; private set; }

        public CountingAddress? Address
        {
            get
            {
                AddressReads++;
                return _address;
            }
        }
    }

    public sealed class CountingAddress
    {
        public string City { get; init; } = string.Empty;
    }

    public sealed class CountingDestination
    {
        public string? CustomerAddressCity { get; set; }
    }

    public sealed class OutputNullabilitySource
    {
        [MaybeNull]
        public OutputCustomer MaybeCustomer { get; init; }

        [NotNull]
        public OutputCustomer? CertainCustomer { get; init; } = new();

        [NotNull]
        public OutputMetrics? CertainMetrics { get; init; } = new();
    }

    public sealed class OutputCustomer
    {
        public string Name { get; init; } = string.Empty;
    }

    public struct OutputMetrics
    {
        public int Count { get; init; }
    }

    public sealed class OutputNullabilityDestination
    {
        public string? MaybeCustomerName { get; set; }

        public string CertainCustomerName { get; set; } = string.Empty;

        public int CertainMetricsCount { get; set; }
    }

    public sealed class OutputNullabilityIncludedDestination
    {
        public string? Name { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>();

            builder.Map<NestedSource, FlattenedDestination>();

            builder.Map<NestedSource, DisabledDestination>()
                .Flattening(Flattening.None);

            builder.Map<NestedSource, DisabledConstructorDestination>()
                .Flattening(Flattening.None);

            builder.Map<NestedSource, NonNullableDestination>();

            builder.Map<NestedSource, ExplicitDestination>()
                .MemberSelection(MemberSelection.Explicit)
                .Members(source => new()
                {
                    CustomerAddressCity = Auto()
                });

            builder.Map<NestedSource, ExplicitConstructorDestination>()
                .Construct(_ => new(Auto()));

            builder.Map<NestedSource,
                    ExplicitSelectionConstructorDestination>()
                .MemberSelection(MemberSelection.Explicit);

            builder.Map<NestedSource, StructuredConstructorDestination>()
                .Construct(source => source.Customer is null
                    ? new("fallback")
                    : new(Auto()));

            builder.Map<NestedSource, AllowNullConstructorDestination>();

            builder.Map<NestedSource, DisallowNullConstructorDestination>();

            builder.Map<NestedSource, ObliviousConstructorDestination>();

            builder.Map<TieredSource, TieredDestination>()
                .IncludeMembers(source => source.Profile);

            builder.Map<DirectClaimSource, DirectClaimDestination>();

            builder.Map<CaseFallbackSource, CaseFallbackDestination>();

            builder.Map<CountingSource, CountingDestination>();

            builder.Map<OutputNullabilitySource,
                    OutputNullabilityDestination>();

            builder.Map<OutputNullabilitySource,
                    OutputNullabilityIncludedDestination>()
                .IncludeMembers(source => source.MaybeCustomer);
        }
    }

    [MorphantMapper]
    public partial class MapperDefaultMapper : TypeMapper<MapperDefaultMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Flattening(Flattening.None);

            builder.Map<NestedSource, DisabledDestination>()
                .Flattening(Flattening.Default);
            builder.Map<NestedSource, FlattenedDestination>()
                .Flattening(Flattening.Auto);
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source
            {
                CustomerAddressCity = "direct",
                Customer = new Customer
                {
                    Address = new Address
                    {
                        City = "nested",
                        Code = "field",
                        Score = 17
                    }
                }
            };

            var direct = ((ITypeMapper<Source, Destination>)mapper)
                .Create(source, default(MappingContext));

            if (direct.CustomerAddressCity != "direct" ||
                direct.CustomerAddressScore != 17 ||
                direct.CustomerAddressRequiredScore != 41 ||
                direct.CustomerAddressRequiredCity != "initial" ||
                direct.CustomerAddressCode != "field")
            {
                throw new InvalidOperationException(
                    "Direct precedence or nullable flattening is incorrect.");
            }

            var automatic =
                (ITypeMapper<NestedSource, FlattenedDestination>)mapper;
            var nestedSource = new NestedSource
            {
                Customer = source.Customer
            };
            var created = automatic.Create(
                nestedSource,
                default(MappingContext));
            var missing = automatic.Create(
                new NestedSource(),
                default(MappingContext));
            var previous = new FlattenedDestination
            {
                CustomerAddressCity = "old",
                CustomerAddressScore = 23
            };
            var updated = automatic.Update(
                nestedSource,
                previous,
                default(MappingContext));

            if (created.CustomerAddressCity != "nested" ||
                created.CustomerAddressScore != 17 ||
                missing.CustomerAddressCity is not null ||
                missing.CustomerAddressScore is not null ||
                !ReferenceEquals(updated, previous) ||
                updated.CustomerAddressCity != "nested" ||
                updated.CustomerAddressScore != 17)
            {
                throw new InvalidOperationException(
                    "Create or Update flattening is incorrect.");
            }

            var disabled =
                ((ITypeMapper<NestedSource, DisabledDestination>)mapper)
                .Create(nestedSource, default(MappingContext));

            if (disabled.CustomerAddressCity is not null)
            {
                throw new InvalidOperationException(
                    "Flattening.None did not disable nested conventions.");
            }

            var disabledConstructor =
                ((ITypeMapper<NestedSource,
                    DisabledConstructorDestination>)mapper).Create(
                    nestedSource,
                    default(MappingContext));

            if (disabledConstructor.CustomerAddressCity != "initial")
            {
                throw new InvalidOperationException(
                    "Flattening.None did not disable constructor " +
                    "flattening.");
            }

            var nonNullable =
                ((ITypeMapper<NestedSource, NonNullableDestination>)mapper)
                .Create(nestedSource, default(MappingContext));

            if (nonNullable.CustomerAddressCity != "initial" ||
                nonNullable.CustomerAddressScore != 41)
            {
                throw new InvalidOperationException(
                    "A nullable flattened path mapped to a non-nullable " +
                    "destination.");
            }

            var explicitResult =
                ((ITypeMapper<NestedSource, ExplicitDestination>)mapper)
                .Create(nestedSource, default(MappingContext));

            if (explicitResult.CustomerAddressCity != "nested")
            {
                throw new InvalidOperationException(
                    "Explicit Auto did not use convention resolution.");
            }

            var explicitConstructor =
                ((ITypeMapper<NestedSource,
                    ExplicitConstructorDestination>)mapper).Create(
                    nestedSource,
                    default(MappingContext));

            if (explicitConstructor.CustomerAddressCity != "nested")
            {
                throw new InvalidOperationException(
                    "Explicit constructor Auto did not use flattening.");
            }

            var explicitSelectionConstructor =
                ((ITypeMapper<NestedSource,
                    ExplicitSelectionConstructorDestination>)mapper).Create(
                    nestedSource,
                    default(MappingContext));

            if (explicitSelectionConstructor.CustomerAddressCity != "nested")
            {
                throw new InvalidOperationException(
                    "Explicit member selection disabled constructor " +
                    "flattening.");
            }

            var structuredMapper =
                (ITypeMapper<NestedSource,
                    StructuredConstructorDestination>)mapper;
            var structuredAutomatic = structuredMapper.Create(
                nestedSource,
                default(MappingContext));
            var structuredFallback = structuredMapper.Create(
                new NestedSource(),
                default(MappingContext));

            if (structuredAutomatic.CustomerAddressCity != "nested" ||
                structuredFallback.CustomerAddressCity != "fallback")
            {
                throw new InvalidOperationException(
                    "Structured constructor Auto did not use flattening.");
            }

            var allowNull =
                (ITypeMapper<NestedSource,
                    AllowNullConstructorDestination>)mapper;
            var allowedValue = allowNull.Create(
                nestedSource,
                default(MappingContext));
            var allowedMissing = allowNull.Create(
                new NestedSource(),
                default(MappingContext));
            var disallowNull =
                ((ITypeMapper<NestedSource,
                    DisallowNullConstructorDestination>)mapper).Create(
                    nestedSource,
                    default(MappingContext));
            var oblivious =
                (ITypeMapper<NestedSource,
                    ObliviousConstructorDestination>)mapper;
            var obliviousValue = oblivious.Create(
                nestedSource,
                default(MappingContext));
            var obliviousMissing = oblivious.Create(
                new NestedSource(),
                default(MappingContext));

            if (allowedValue.CustomerAddressCity != "nested" ||
                allowedMissing.CustomerAddressCity is not null ||
                disallowNull.CustomerAddressCity != "initial" ||
                obliviousValue.CustomerAddressCity != "nested" ||
                obliviousMissing.CustomerAddressCity is not null)
            {
                throw new InvalidOperationException(
                    "Constructor nullability attributes were ignored.");
            }

            var tiered =
                ((ITypeMapper<TieredSource, TieredDestination>)mapper)
                .Create(
                    new TieredSource
                    {
                        Details = new NumericDetails { Name = 29 },
                        Profile = new Profile
                        {
                            Details = new TextDetails { Name = "included" }
                        }
                    },
                    default(MappingContext));

            if (tiered.DetailsName != "included")
            {
                throw new InvalidOperationException(
                    "An incompatible root flattened path blocked a " +
                    "compatible included path.");
            }

            var directClaim =
                ((ITypeMapper<DirectClaimSource,
                    DirectClaimDestination>)mapper).Create(
                    new DirectClaimSource
                    {
                        DetailsName = 29,
                        Details = new DirectClaimDetails
                        {
                            Name = "nested"
                        }
                    },
                    default(MappingContext));

            if (directClaim.DetailsName != "initial")
            {
                throw new InvalidOperationException(
                    "An incompatible direct member did not reserve the " +
                    "destination name.");
            }

            var caseFallback =
                ((ITypeMapper<CaseFallbackSource,
                    CaseFallbackDestination>)mapper).Create(
                    new CaseFallbackSource
                    {
                        customer = new NumericCaseCustomer
                        {
                            AddressCity = 29
                        },
                        Customer = new TextCaseCustomer
                        {
                            Address = new CaseAddress
                            {
                                City = "case-insensitive"
                            }
                        }
                    },
                    default(MappingContext));

            if (caseFallback.CustomerAddressCity != "case-insensitive")
            {
                throw new InvalidOperationException(
                    "An incompatible exact-case flattened path blocked a " +
                    "compatible constructor fallback.");
            }

            var countingAddress = new CountingAddress { City = "counted" };
            var countingCustomer = new CountingCustomer(countingAddress);
            var countingSource = new CountingSource(countingCustomer);
            var countingMapper =
                (ITypeMapper<CountingSource, CountingDestination>)mapper;
            var counted = countingMapper.Create(
                countingSource,
                default(MappingContext));
            var missingCustomer = new CountingSource(null);
            var missingCustomerResult = countingMapper.Create(
                missingCustomer,
                default(MappingContext));
            var customerWithoutAddress = new CountingCustomer(null);
            var missingAddressSource =
                new CountingSource(customerWithoutAddress);
            var missingAddressResult = countingMapper.Create(
                missingAddressSource,
                default(MappingContext));

            if (counted.CustomerAddressCity != "counted" ||
                countingSource.CustomerReads != 1 ||
                countingCustomer.AddressReads != 1 ||
                missingCustomerResult.CustomerAddressCity is not null ||
                missingCustomer.CustomerReads != 1 ||
                missingAddressResult.CustomerAddressCity is not null ||
                missingAddressSource.CustomerReads != 1 ||
                customerWithoutAddress.AddressReads != 1)
            {
                throw new InvalidOperationException(
                    "Null-conditional flattening repeated a getter or did " +
                    "not short-circuit a missing path.");
            }

            var outputNullability =
                ((ITypeMapper<OutputNullabilitySource,
                    OutputNullabilityDestination>)mapper).Create(
                    new OutputNullabilitySource
                    {
                        MaybeCustomer = null!,
                        CertainCustomer = new OutputCustomer
                        {
                            Name = "certain"
                        },
                        CertainMetrics = new OutputMetrics { Count = 19 }
                    },
                    default(MappingContext));

            if (outputNullability.MaybeCustomerName is not null ||
                outputNullability.CertainCustomerName != "certain" ||
                outputNullability.CertainMetricsCount != 19)
            {
                throw new InvalidOperationException(
                    "Output nullability attributes were ignored by " +
                    "flattening.");
            }

            var includedOutputNullability =
                ((ITypeMapper<OutputNullabilitySource,
                    OutputNullabilityIncludedDestination>)mapper).Create(
                    new OutputNullabilitySource
                    {
                        MaybeCustomer = null!
                    },
                    default(MappingContext));

            if (includedOutputNullability.Name is not null)
            {
                throw new InvalidOperationException(
                    "IncludeMembers ignored output nullability.");
            }

            var mapperDefault = new MapperDefaultMapper();
            var inheritedNone =
                ((ITypeMapper<NestedSource, DisabledDestination>)
                    mapperDefault).Create(
                    nestedSource,
                    default(MappingContext));
            var pairOverride =
                ((ITypeMapper<NestedSource, FlattenedDestination>)
                    mapperDefault).Create(
                    nestedSource,
                    default(MappingContext));

            if (inheritedNone.CustomerAddressCity is not null ||
                pairOverride.CustomerAddressCity != "nested" ||
                pairOverride.CustomerAddressScore != 17)
            {
                throw new InvalidOperationException(
                    "Mapper-level flattening inheritance or the pair " +
                    "override is incorrect.");
            }
        }
    }
}
