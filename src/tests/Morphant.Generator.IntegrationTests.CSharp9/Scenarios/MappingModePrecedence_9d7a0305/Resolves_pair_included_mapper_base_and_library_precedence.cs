// Compiled integration scenario: TypeMapperMappingModeTests::Resolves_pair_included_mapper_base_and_library_precedence
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;
using Morphant.Context;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.MappingModePrecedence_9d7a0305
{
    public class Source
    {
        public int Value { get; init; }
    }

    public class Destination
    {
        public int Value { get; set; }
    }

    public sealed class IncludedSource : Source
    {
    }

    public sealed class IncludedDestination : Destination
    {
    }

    public sealed class LocalSource : Source
    {
    }

    public sealed class LocalDestination : Destination
    {
    }

    public sealed class BaseRootDestination : Destination
    {
    }

    public sealed class CurrentRootDestination : Destination
    {
    }

    public sealed class DefaultPairDestination : Destination
    {
    }

    public abstract class IncludedBaseMapper : TypeMapper<IncludedBaseMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>(MappingMode.Create);
    }

    [MorphantMapper]
    public partial class IncludedMapper : IncludedBaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.MappingMode(MappingMode.Update);

            builder.Map<IncludedSource, IncludedDestination>()
                .IncludeBase<Source, Destination>();
            builder.Map<LocalSource, LocalDestination>(MappingMode.Update)
                .IncludeBase<Source, Destination>();
        }
    }

    public abstract class RootBaseMapper : TypeMapper<RootBaseMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.MappingMode(MappingMode.Create);
            builder.MappingMode(MappingMode.Update);
        }
    }

    [MorphantMapper]
    public partial class BaseRootMapper : RootBaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Source, BaseRootDestination>();
        }
    }

    [MorphantMapper]
    public partial class CurrentRootMapper : RootBaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.MappingMode(MappingMode.Create);
            builder.Map<Source, CurrentRootDestination>();
        }
    }

    [MorphantMapper]
    public partial class DefaultPairMapper : TypeMapper<DefaultPairMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.MappingMode(MappingMode.Update);
            builder.Map<Source, DefaultPairDestination>(MappingMode.Default);
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            VerifyIncludedAndLocalPair();
            VerifyConnectedAndCurrentRoots();
            VerifyDefaultContinuation();
        }

        private static void VerifyIncludedAndLocalPair()
        {
            var mapper = new IncludedMapper();
            var source = new IncludedSource { Value = 11 };
            var included =
                (ITypeMapper<IncludedSource, IncludedDestination>)mapper;
            var created = included.Create(source, default(MappingContext));

            if (created.Value != 11)
            {
                throw new InvalidOperationException(
                    "An included pair mode did not override the current " +
                    "mapper mode.");
            }

            ExpectNotSupported(
                () => included.Update(source, created),
                MappingOperation.Update,
                MappingMode.Create);

            var local =
                (ITypeMapper<LocalSource, LocalDestination>)mapper;
            var previous = new LocalDestination { Value = -1 };
            var updated = local.Update(
                new LocalSource { Value = 13 },
                previous);

            if (!ReferenceEquals(previous, updated) || updated.Value != 13)
            {
                throw new InvalidOperationException(
                    "A local pair mode did not override its included pair.");
            }

            ExpectNotSupported(
                () => local.Create(new LocalSource()),
                MappingOperation.Create,
                MappingMode.Update);
        }

        private static void VerifyConnectedAndCurrentRoots()
        {
            var source = new Source { Value = 17 };
            var fromBase = (ITypeMapper<Source, BaseRootDestination>)
                new BaseRootMapper();
            var basePrevious = new BaseRootDestination();
            var baseUpdated = fromBase.Update(source, basePrevious);

            if (!ReferenceEquals(basePrevious, baseUpdated) ||
                baseUpdated.Value != 17)
            {
                throw new InvalidOperationException(
                    "The nearest connected base mapper mode was not used.");
            }

            ExpectNotSupported(
                () => fromBase.Create(source),
                MappingOperation.Create,
                MappingMode.Update);

            var current = (ITypeMapper<Source, CurrentRootDestination>)
                new CurrentRootMapper();
            var created = current.Create(source);

            if (created.Value != 17)
            {
                throw new InvalidOperationException(
                    "The current mapper mode did not override its base.");
            }

            ExpectNotSupported(
                () => current.Update(source, created),
                MappingOperation.Update,
                MappingMode.Create);
        }

        private static void VerifyDefaultContinuation()
        {
            var mapper = (ITypeMapper<Source, DefaultPairDestination>)
                new DefaultPairMapper();
            var previous = new DefaultPairDestination();
            var updated = mapper.Update(
                new Source { Value = 19 },
                previous);

            if (!ReferenceEquals(previous, updated) || updated.Value != 19)
            {
                throw new InvalidOperationException(
                    "MappingMode.Default did not continue to the mapper " +
                    "setting.");
            }

            ExpectNotSupported(
                () => mapper.Create(new Source()),
                MappingOperation.Create,
                MappingMode.Update);
        }

        private static void ExpectNotSupported(
            Action action,
            MappingOperation operation,
            MappingMode mode)
        {
            try
            {
                action();
            }
            catch (MappingOperationNotSupportedException exception)
                when (exception.Operation == operation &&
                      exception.EffectiveMappingMode == mode)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Expected {operation} to be disabled by {mode}.");
        }
    }
}
