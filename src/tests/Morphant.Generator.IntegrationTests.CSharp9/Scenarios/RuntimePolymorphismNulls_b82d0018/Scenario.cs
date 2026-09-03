// Compiled integration scenario: null source handling precedes dispatch
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;
using Morphant.Context;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.RuntimePolymorphismNulls_b82d0018
{
    public interface IReturnNullSource { }
    public sealed class ReturnNullSource : IReturnNullSource { }
    public interface IReturnDestinationSource { }
    public sealed class ReturnDestinationSource : IReturnDestinationSource { }
    public interface IThrowSource { }
    public sealed class ThrowSource : IThrowSource { }

    public class BaseDto { }
    public sealed class DerivedDto : BaseDto { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<IReturnNullSource, BaseDto?>()
                .ForDerived<ReturnNullSource, DerivedDto>()
                .UnknownDerivedTypeHandling(
                    UnknownDerivedTypeHandling.Throw);
            builder.Map<ReturnNullSource, DerivedDto>();

            builder.Map<IReturnDestinationSource, BaseDto>()
                .ForDerived<ReturnDestinationSource, DerivedDto>()
                .NullSourceHandling(NullSourceHandling.ReturnDestination)
                .UnknownDerivedTypeHandling(
                    UnknownDerivedTypeHandling.Throw);
            builder.Map<ReturnDestinationSource, DerivedDto>();

            builder.Map<IThrowSource, BaseDto>()
                .ForDerived<ThrowSource, DerivedDto>()
                .NullSourceHandling(NullSourceHandling.Throw)
                .UnknownDerivedTypeHandling(
                    UnknownDerivedTypeHandling.Throw);
            builder.Map<ThrowSource, DerivedDto>();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var returnNull =
                (ITypeMapper<IReturnNullSource, BaseDto?>)mapper;
            var previous = new BaseDto();

            if (returnNull.Create(null) is not null ||
                returnNull.Update(null, previous) is not null)
            {
                throw new InvalidOperationException(
                    "ReturnNull did not run before polymorphic dispatch.");
            }

            var returnDestination =
                (ITypeMapper<IReturnDestinationSource, BaseDto>)mapper;

            if (returnDestination.Create(null) is not null ||
                !ReferenceEquals(
                    returnDestination.Update(null, previous),
                    previous))
            {
                throw new InvalidOperationException(
                    "ReturnDestination did not run before dispatch.");
            }

            var throwMapper = (ITypeMapper<IThrowSource, BaseDto>)mapper;
            AssertNullSourceFailure(
                () => throwMapper.Create(null),
                MappingOperation.Create);
            AssertNullSourceFailure(
                () => throwMapper.Update(null, previous),
                MappingOperation.Update);
        }

        private static void AssertNullSourceFailure(
            Action action,
            MappingOperation operation)
        {
            try
            {
                action();
                throw new InvalidOperationException(
                    "A null source bypassed NullSourceHandling.Throw.");
            }
            catch (NullSourceException exception)
            {
                if (exception.Operation != operation ||
                    exception.SourceType != typeof(IThrowSource) ||
                    exception.DestinationType != typeof(BaseDto))
                {
                    throw new InvalidOperationException(
                        "The null-source failure belongs to the wrong pair.");
                }
            }
        }
    }
}
