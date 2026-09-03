// Compiled integration scenario: CompositionDiagnosticsTests::Executes_every_result_policy_with_Members_and_an_imported_boundary
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.CompositionPositive_5c0f0000
{
    public sealed class Source
    {
        public int Value { get; set; }
    }

    public sealed class ConstructDestination
    {
        public int Value { get; set; }
    }

    public sealed class ResolveDestination
    {
        public int Value { get; set; }
    }

    public sealed class ConstructUsingDestination
    {
        public int Origin { get; set; }
        public int Value { get; set; }
    }

    public sealed class ResolveUsingDestination
    {
        public int Origin { get; set; }
        public int Value { get; set; }
    }

    public class BaseSource
    {
        public int Value { get; set; }
    }

    public sealed class DerivedSource : BaseSource { }

    public class BaseDestination
    {
        public int Value { get; set; }
    }

    public sealed class DerivedDestination : BaseDestination { }

    public abstract class BaseMapper<TMapper> : TypeMapper<TMapper>
        where TMapper : BaseMapper<TMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<BaseSource, BaseDestination>()
                .Members(source => new() { Value = source.Value + 100 });
    }

    [MorphantMapper]
    public partial class TestMapper : BaseMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);

            builder.Map<Source, ConstructDestination>()
                .Construct(source => new())
                .Members(source => new() { Value = source.Value + 1 });

            builder.Map<Source, ResolveDestination>()
                .Resolve((source, previous) => new())
                .Members(source => new() { Value = source.Value + 2 });

            builder.Map<Source, ConstructUsingDestination>()
                .ConstructUsing(source =>
                    new ConstructUsingDestination { Origin = 3 })
                .Members(source => new() { Value = source.Value + 3 });

            builder.Map<Source, ResolveUsingDestination>()
                .ResolveUsing((source, previous) =>
                    new ResolveUsingDestination { Origin = 4 })
                .Members(source => new() { Value = source.Value + 4 });

            builder.Map<DerivedSource, DerivedDestination>()
                .Convert(source => new DerivedDestination
                {
                    Value = source!.Value + 50
                })
                .IncludeBase<BaseSource, BaseDestination>();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source { Value = 10 };

            VerifyDeclarative<ConstructDestination>(
                mapper,
                source,
                11,
                reuseExisting: true,
                readValue: destination => destination.Value);
            VerifyDeclarative<ResolveDestination>(
                mapper,
                source,
                12,
                reuseExisting: false,
                readValue: destination => destination.Value);

            var constructUsing =
                (ITypeMapper<Source, ConstructUsingDestination>)mapper;
            var constructed = constructUsing.Create(
                source,
                default(MappingContext));
            var existingConstruct = new ConstructUsingDestination
            {
                Origin = 30
            };
            var updatedConstruct = constructUsing.Update(
                source,
                existingConstruct,
                default(MappingContext));

            if (constructed.Origin != 3 || constructed.Value != 13 ||
                !ReferenceEquals(existingConstruct, updatedConstruct) ||
                updatedConstruct.Origin != 30 || updatedConstruct.Value != 13)
            {
                throw new InvalidOperationException(
                    "ConstructUsing and Members did not compose.");
            }

            var resolveUsing =
                (ITypeMapper<Source, ResolveUsingDestination>)mapper;
            var resolvedCreate = resolveUsing.Create(
                source,
                default(MappingContext));
            var existingResolve = new ResolveUsingDestination
            {
                Origin = 40
            };
            var resolvedUpdate = resolveUsing.Update(
                source,
                existingResolve,
                default(MappingContext));

            if (resolvedCreate.Origin != 4 || resolvedCreate.Value != 14 ||
                ReferenceEquals(existingResolve, resolvedUpdate) ||
                resolvedUpdate.Origin != 4 || resolvedUpdate.Value != 14)
            {
                throw new InvalidOperationException(
                    "ResolveUsing and Members did not compose.");
            }

            var importedBoundary =
                (ITypeMapper<DerivedSource, DerivedDestination>)mapper;
            var importedResult = importedBoundary.Create(
                new DerivedSource { Value = 5 },
                default(MappingContext));

            if (importedResult.Value != 55)
            {
                throw new InvalidOperationException(
                    "An imported Members plan conflicted with local Convert.");
            }
        }

        private static void VerifyDeclarative<TDestination>(
            TestMapper mapper,
            Source source,
            int expected,
            bool reuseExisting,
            Func<TDestination, int> readValue)
            where TDestination : new()
        {
            var contract = (ITypeMapper<Source, TDestination>)mapper;
            var created = contract.Create(source, default(MappingContext));
            var existing = new TDestination();
            var updated = contract.Update(
                source,
                existing,
                default(MappingContext));

            if (readValue(created) != expected ||
                ReferenceEquals(existing, updated) != reuseExisting ||
                readValue(updated) != expected)
            {
                throw new InvalidOperationException(
                    "A structured result policy did not compose with Members.");
            }
        }
    }
}
