// Compiled integration scenario: MapperDispatchTests/RegistrationTests::Enforces_exactly_one_registration_for_each_requested_pair
#nullable enable
#pragma warning disable CS1591

using System;
using Microsoft.Extensions.DependencyInjection;
using Morphant;
using Morphant.Context;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ApplicationRegistration_9d7a0102
{
    public sealed class Source
    {
        public int Value { get; init; }
    }

    public sealed class Destination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class ExactMapper : TypeMapper<ExactMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }

    [MorphantMapper]
    public partial class BroadMapper : TypeMapper<BroadMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<object, Destination>()
                .Convert(_ => new Destination { Value = -1 });
    }

    public static class Scenario
    {
        public static void Verify()
        {
            VerifyNullProvider();
            VerifyMissingRegistration();
            VerifyExactLookup();
            VerifyAmbiguousRegistration();
            VerifyNullRegistration();
        }

        private static void VerifyNullProvider()
        {
            try
            {
                _ = new Mapper(null!);
            }
            catch (ArgumentNullException exception)
                when (exception.ParamName == "serviceProvider")
            {
                return;
            }

            throw new InvalidOperationException(
                "Mapper accepted a null service provider.");
        }

        private static void VerifyMissingRegistration()
        {
            using var provider = new ServiceCollection()
                .AddSingleton<IMapper, Mapper>()
                .BuildServiceProvider();
            var mapper = provider.GetRequiredService<IMapper>();

            Expect<MappingNotFoundException>(
                () => mapper.Map<Source, Destination>(new Source()),
                MappingOperation.Create);
            Expect<MappingNotFoundException>(
                () => mapper.Map(
                    new Source(),
                    new Destination()),
                MappingOperation.Update);
        }

        private static void VerifyExactLookup()
        {
            var broad = new BroadMapper();
            using var provider = new ServiceCollection()
                .AddSingleton<ITypeMapper<object, Destination>>(broad)
                .AddSingleton<IMapper, Mapper>()
                .BuildServiceProvider();
            var mapper = provider.GetRequiredService<IMapper>();

            Expect<MappingNotFoundException>(
                () => mapper.Map<Source, Destination>(new Source()),
                MappingOperation.Create);
        }

        private static void VerifyAmbiguousRegistration()
        {
            var generated = new ExactMapper();
            using var provider = new ServiceCollection()
                .AddSingleton<ITypeMapper<Source, Destination>>(generated)
                .AddSingleton<ITypeMapper<Source, Destination>>(generated)
                .AddSingleton<IMapper, Mapper>()
                .BuildServiceProvider();
            var mapper = provider.GetRequiredService<IMapper>();

            Expect<AmbiguousMappingException>(
                () => mapper.Map<Source, Destination>(new Source()),
                MappingOperation.Create);
            Expect<AmbiguousMappingException>(
                () => mapper.Map(
                    new Source(),
                    new Destination()),
                MappingOperation.Update);
        }

        private static void VerifyNullRegistration()
        {
            using var provider = new ServiceCollection()
                .AddSingleton<ITypeMapper<Source, Destination>>(_ => null!)
                .AddSingleton<IMapper, Mapper>()
                .BuildServiceProvider();
            var mapper = provider.GetRequiredService<IMapper>();

            Expect<InvalidMappingRegistrationException>(
                () => mapper.Map<Source, Destination>(new Source()),
                MappingOperation.Create);
            Expect<InvalidMappingRegistrationException>(
                () => mapper.Map(new Source(), new Destination()),
                MappingOperation.Update);
        }

        private static void Expect<TException>(
            Action action,
            MappingOperation operation)
            where TException : MappingException
        {
            try
            {
                action();
            }
            catch (TException exception)
                when (exception.Operation == operation &&
                      exception.SourceType == typeof(Source) &&
                      exception.DestinationType == typeof(Destination))
            {
                return;
            }

            throw new InvalidOperationException(
                $"Expected {typeof(TException).Name} for {operation}.");
        }
    }
}
