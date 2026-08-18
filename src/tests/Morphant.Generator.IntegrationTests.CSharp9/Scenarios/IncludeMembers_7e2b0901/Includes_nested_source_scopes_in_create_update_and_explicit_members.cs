// Compiled integration scenario: IncludeMembers
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.IncludeMembers_7e2b0901
{
    public sealed class Source
    {
        public int Id { get; init; }

        public Customer? Customer { get; init; }

        public Envelope Envelope { get; init; } = null!;
    }

    public sealed class Customer
    {
        public string Name { get; init; } = string.Empty;

        public int Count { get; init; }
    }

    public sealed class Envelope
    {
        public Audit? Audit { get; init; }
    }

    public sealed class Audit
    {
        public string? Tag { get; init; }
    }

    public sealed class Destination
    {
        public Destination(int id, int count)
        {
            Id = id;
            Count = count;
        }

        public int Id { get; }

        public int Count { get; }

        public string? Name { get; set; }

        public string? Tag { get; set; }
    }

    public sealed class ExplicitDestination
    {
        public string? Name { get; set; }

        public string Untouched { get; set; } = "initial";
    }

    public sealed class AssertedDestination
    {
        public string Name { get; set; } = string.Empty;
    }

    public sealed class ExplicitConstructDestination
    {
        public ExplicitConstructDestination(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }

    public sealed class StructuredConventionDestination
    {
        public StructuredConventionDestination(int count)
        {
            Count = count;
        }

        public int Count { get; }
    }

    public class BaseSource
    {
        public Profile Profile { get; init; } = new Profile();
    }

    public sealed class DerivedSource : BaseSource
    {
        public Revision Revision = new Revision();
    }

    public sealed class Profile
    {
        public string Name { get; init; } = string.Empty;
    }

    public sealed class Revision
    {
        public long Number;
    }

    public class BaseDestination
    {
        public string Name { get; set; } = string.Empty;
    }

    public sealed class DerivedDestination : BaseDestination
    {
        public long Number { get; set; }
    }

    public class GenericSourceBase<T>
    {
        public GenericDetails<T> Details { get; init; } =
            new GenericDetails<T>();
    }

    public sealed class GenericDetails<T>
    {
        public T Value { get; init; } = default!;
    }

    public sealed class GenericSource : GenericSourceBase<string>
    {
    }

    public class GenericDestinationBase<T>
    {
        public T Value { get; set; } = default!;
    }

    public sealed class GenericDestination :
        GenericDestinationBase<string>
    {
    }

    public struct NullableValueSource
    {
        public ValueDetails Details { get; init; }
    }

    public sealed class ValueDetails
    {
        public string Text { get; init; } = string.Empty;
    }

    public sealed class NullableValueDestination
    {
        public NullableValueDestination(string text)
        {
            Text = text;
        }

        public string Text { get; }
    }

    public struct OptionalDetails
    {
        public int Score { get; init; }
    }

    public sealed class OptionalDetailsSource
    {
        public OptionalDetails? Details { get; init; }
    }

    public sealed class OptionalDetailsDestination
    {
        public int Score { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>()
                .IncludeMembers(source => source.Customer)
                .IncludeMembers(source => source.Envelope?.Audit);

            builder.Map<Source, ExplicitDestination>()
                .IncludeMembers(source => source.Customer)
                .MemberSelection(MemberSelection.Explicit)
                .Members(source => new()
                {
                    Name = Auto()
                });

            builder.Map<Source, AssertedDestination>()
                .IncludeMembers(source => source.Customer!);

            builder.Map<Source, ExplicitConstructDestination>()
                .IncludeMembers(source => source.Customer!)
                .Construct(_ => new(Auto()));

            builder.Map<Source, StructuredConventionDestination>()
                .IncludeMembers(source => source.Customer)
                .MemberSelection(MemberSelection.Explicit)
                .Construct(_ => new(ByConvention()));

            builder.Map<BaseSource, BaseDestination>()
                .IncludeMembers(source => source.Profile);
            builder.Map<DerivedSource, DerivedDestination>()
                .IncludeBase<BaseSource, BaseDestination>()
                .IncludeMembers(source => source.Revision);

            builder.Map<NullableValueSource?, NullableValueDestination>()
                .IncludeMembers(source => source!.Value.Details);

            builder.Map<OptionalDetailsSource,
                    OptionalDetailsDestination>()
                .IncludeMembers(source => source.Details!);
        }
    }

    public abstract class GenericMapper<T> : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<GenericSourceBase<T>, GenericDestinationBase<T>>()
                .IncludeMembers(source => source.Details);
    }

    [MorphantMapper]
    public sealed partial class ClosedGenericMapper : GenericMapper<string>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<GenericSource, GenericDestination>()
                .IncludeBase<
                    GenericSourceBase<string>,
                    GenericDestinationBase<string>>();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var automatic =
                (ITypeMapper<Source, Destination>)mapper;
            var source = new Source
            {
                Id = 11,
                Customer = new Customer
                {
                    Name = "Ada",
                    Count = 13
                },
                Envelope = new Envelope
                {
                    Audit = new Audit
                    {
                        Tag = "created"
                    }
                }
            };
            var created = automatic.Create(
                source,
                default(MappingContext));
            var previous = new Destination(17, 19)
            {
                Name = "old",
                Tag = "old"
            };
            var updated = automatic.Update(
                source,
                previous,
                default(MappingContext));
            var missing = automatic.Create(
                new Source { Id = 23 },
                default(MappingContext));

            if (created.Id != 11 ||
                created.Count != 13 ||
                created.Name != "Ada" ||
                created.Tag != "created" ||
                !ReferenceEquals(updated, previous) ||
                updated.Id != 17 ||
                updated.Count != 19 ||
                updated.Name != "Ada" ||
                updated.Tag != "created" ||
                missing.Id != 23 ||
                missing.Count != 0 ||
                missing.Name is not null ||
                missing.Tag is not null)
            {
                throw new InvalidOperationException(
                    "Included source scopes did not follow Create, Update " +
                    "or nullable-path semantics.");
            }

            var explicitMapper =
                (ITypeMapper<Source, ExplicitDestination>)mapper;
            var explicitCreated = explicitMapper.Create(
                source,
                default(MappingContext));
            var explicitPrevious = new ExplicitDestination
            {
                Name = "old",
                Untouched = "preserved"
            };
            var explicitUpdated = explicitMapper.Update(
                source,
                explicitPrevious,
                default(MappingContext));

            if (explicitCreated.Name != "Ada" ||
                explicitCreated.Untouched != "initial" ||
                !ReferenceEquals(explicitUpdated, explicitPrevious) ||
                explicitUpdated.Name != "Ada" ||
                explicitUpdated.Untouched != "preserved")
            {
                throw new InvalidOperationException(
                    "Explicit Auto did not use the included source scope.");
            }

            var assertedMapper =
                (ITypeMapper<Source, AssertedDestination>)mapper;
            var assertionThrew = false;

            try
            {
                _ = assertedMapper.Create(
                    new Source(),
                    default(MappingContext));
            }
            catch (NullReferenceException)
            {
                assertionThrew = true;
            }

            if (!assertionThrew)
            {
                throw new InvalidOperationException(
                    "The null-forgiving selector did not preserve the " +
                    "user's non-null assertion.");
            }

            var explicitConstruct =
                ((ITypeMapper<Source, ExplicitConstructDestination>)mapper)
                .Create(source, default(MappingContext));
            var structuredConvention =
                ((ITypeMapper<Source, StructuredConventionDestination>)mapper)
                .Create(source, default(MappingContext));

            if (explicitConstruct.Name != "Ada" ||
                structuredConvention.Count != 13)
            {
                throw new InvalidOperationException(
                    "Structured constructor rules did not use included " +
                    "source members.");
            }

            var inheritedMapper =
                (ITypeMapper<DerivedSource, DerivedDestination>)mapper;
            var inherited = inheritedMapper.Create(
                new DerivedSource
                {
                    Profile = new Profile { Name = "base" },
                    Revision = new Revision { Number = 29 }
                },
                default(MappingContext));

            if (inherited.Name != "base" || inherited.Number != 29)
            {
                throw new InvalidOperationException(
                    "IncludeBase did not compose included source scopes.");
            }

            var genericMapper =
                (ITypeMapper<GenericSource, GenericDestination>)
                new ClosedGenericMapper();
            var generic = genericMapper.Create(
                new GenericSource
                {
                    Details = new GenericDetails<string>
                    {
                        Value = "generic"
                    }
                },
                default(MappingContext));

            if (generic.Value != "generic")
            {
                throw new InvalidOperationException(
                    "A closed generic mapper did not rebind its included " +
                    "source scope.");
            }

            var nullableValueMapper =
                (ITypeMapper<NullableValueSource?,
                    NullableValueDestination>)mapper;
            var nullableValue = nullableValueMapper.Create(
                new NullableValueSource
                {
                    Details = new ValueDetails { Text = "value" }
                },
                default(MappingContext));

            if (nullableValue.Text != "value")
            {
                throw new InvalidOperationException(
                    "A nullable value source did not use its normalized " +
                    "included path.");
            }

            var optionalDetailsMapper =
                (ITypeMapper<OptionalDetailsSource,
                    OptionalDetailsDestination>)mapper;
            var optionalDetails = optionalDetailsMapper.Create(
                new OptionalDetailsSource
                {
                    Details = new OptionalDetails { Score = 37 }
                },
                default(MappingContext));
            var missingOptionalDetails = optionalDetailsMapper.Create(
                new OptionalDetailsSource(),
                default(MappingContext));

            if (optionalDetails.Score != 37 ||
                missingOptionalDetails.Score != 0)
            {
                throw new InvalidOperationException(
                    "The null-forgiving operator changed Nullable<T> " +
                    "runtime semantics.");
            }
        }
    }
}
