// Compiled integration scenario: TypeMapperNullHandlingTests::Resolves_pair_included_mapper_base_and_library_precedence
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;
using Morphant.Context;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.NullHandlingPrecedence_9d7a0307
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

    public abstract class IncludedBaseMapper<TMapper> : TypeMapper<TMapper>
        where TMapper : IncludedBaseMapper<TMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .NullSourceHandling(NullSourceHandling.ReturnNull)
                .NullDestinationHandling(
                    NullDestinationHandling.Create);
    }

    [MorphantMapper]
    public partial class IncludedMapper : IncludedBaseMapper<IncludedMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.NullSourceHandling(NullSourceHandling.Throw);
            builder.NullDestinationHandling(
                NullDestinationHandling.Throw);

            builder.Map<IncludedSource, IncludedDestination>()
                .IncludeBase<Source, Destination>();
            builder.Map<LocalSource, LocalDestination>()
                .IncludeBase<Source, Destination>()
                .NullSourceHandling(NullSourceHandling.Throw)
                .NullDestinationHandling(
                    NullDestinationHandling.Throw);
        }
    }

    public abstract class RootBaseMapper<TMapper> : TypeMapper<TMapper>
        where TMapper : RootBaseMapper<TMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.NullSourceHandling(NullSourceHandling.ReturnNull);
            builder.NullSourceHandling(
                NullSourceHandling.ReturnDestination);
            builder.NullDestinationHandling(
                NullDestinationHandling.Create);
            builder.NullDestinationHandling(
                NullDestinationHandling.Throw);
        }
    }

    [MorphantMapper]
    public partial class BaseRootMapper : RootBaseMapper<BaseRootMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Source, BaseRootDestination>();
        }
    }

    [MorphantMapper]
    public partial class CurrentRootMapper : RootBaseMapper<CurrentRootMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.NullSourceHandling(NullSourceHandling.ReturnNull);
            builder.NullDestinationHandling(
                NullDestinationHandling.Create);
            builder.Map<Source, CurrentRootDestination>();
        }
    }

    [MorphantMapper]
    public partial class DefaultPairMapper : TypeMapper<DefaultPairMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.NullSourceHandling(NullSourceHandling.Throw);
            builder.NullDestinationHandling(
                NullDestinationHandling.Throw);
            builder.Map<Source, DefaultPairDestination>()
                .NullSourceHandling(NullSourceHandling.Default)
                .NullDestinationHandling(
                    NullDestinationHandling.Default);
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            VerifyIncludedAndLocalPairs();
            VerifyConnectedAndCurrentRoots();
            VerifyDefaultContinuation();
        }

        private static void VerifyIncludedAndLocalPairs()
        {
            var mapper = new IncludedMapper();
            var included = (ITypeMapper<
                IncludedSource,
                IncludedDestination>)mapper;

            if (included.Create(null) is not null)
            {
                throw new InvalidOperationException(
                    "Included NullSourceHandling did not override the " +
                    "current mapper.");
            }

            var createdByUpdate = included.Update(
                new IncludedSource { Value = 11 },
                null);

            if (createdByUpdate.Value != 11)
            {
                throw new InvalidOperationException(
                    "Included NullDestinationHandling did not create a " +
                    "replacement.");
            }

            var local =
                (ITypeMapper<LocalSource, LocalDestination>)mapper;
            Expect<NullSourceException>(
                () => local.Create(null),
                MappingOperation.Create,
                typeof(LocalSource),
                typeof(LocalDestination));
            Expect<NullDestinationException>(
                () => local.Update(new LocalSource(), null),
                MappingOperation.Update,
                typeof(LocalSource),
                typeof(LocalDestination));
        }

        private static void VerifyConnectedAndCurrentRoots()
        {
            var fromBase = (ITypeMapper<Source, BaseRootDestination>)
                new BaseRootMapper();
            var previous = new BaseRootDestination { Value = 13 };

            if (!ReferenceEquals(
                    previous,
                    fromBase.Update(null, previous)))
            {
                throw new InvalidOperationException(
                    "The connected base NullSourceHandling was not used.");
            }

            Expect<NullDestinationException>(
                () => fromBase.Update(new Source(), null),
                MappingOperation.Update,
                typeof(Source),
                typeof(BaseRootDestination));

            var current = (ITypeMapper<Source, CurrentRootDestination>)
                new CurrentRootMapper();

            if (current.Create(null) is not null ||
                current.Update(
                    new Source { Value = 17 },
                    null).Value != 17)
            {
                throw new InvalidOperationException(
                    "The current mapper null policies did not override the " +
                    "connected base mapper.");
            }
        }

        private static void VerifyDefaultContinuation()
        {
            var mapper = (ITypeMapper<Source, DefaultPairDestination>)
                new DefaultPairMapper();

            Expect<NullSourceException>(
                () => mapper.Create(null),
                MappingOperation.Create,
                typeof(Source),
                typeof(DefaultPairDestination));
            Expect<NullDestinationException>(
                () => mapper.Update(new Source(), null),
                MappingOperation.Update,
                typeof(Source),
                typeof(DefaultPairDestination));
        }

        private static void Expect<TException>(
            Action action,
            MappingOperation operation,
            Type sourceType,
            Type destinationType)
            where TException : MappingException
        {
            try
            {
                action();
            }
            catch (TException exception)
                when (exception.Operation == operation &&
                      exception.SourceType == sourceType)
            {
                if (exception.DestinationType == destinationType)
                {
                    return;
                }
            }

            throw new InvalidOperationException(
                $"Expected {typeof(TException).Name} for {operation}.");
        }
    }
}
