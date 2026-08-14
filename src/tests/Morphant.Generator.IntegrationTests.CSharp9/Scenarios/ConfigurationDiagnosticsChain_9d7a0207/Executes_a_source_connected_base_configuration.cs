// Compiled integration scenario: ConfigurationDiagnosticsTests::Executes_a_source_connected_base_configuration
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ConfigurationDiagnosticsChain_9d7a0207
{
    public sealed class BaseSource
    {
        public int Value { get; init; }
    }

    public sealed class BaseDestination
    {
        public int Value { get; set; }
    }

    public sealed class LocalSource
    {
        public int Value { get; init; }
    }

    public sealed class LocalDestination
    {
        public int Value { get; set; }
    }

    public abstract class BaseMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.NullSourceHandling(NullSourceHandling.Throw);
            builder.Map<BaseSource, BaseDestination>();
        }
    }

    [MorphantMapper]
    public partial class ConnectedMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<LocalSource, LocalDestination>();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var baseContract =
                typeof(ITypeMapper<BaseSource, BaseDestination>);
            var localContract =
                typeof(ITypeMapper<LocalSource, LocalDestination>);

            if (baseContract.IsAssignableFrom(typeof(ConnectedMapper)) ||
                !localContract.IsAssignableFrom(typeof(ConnectedMapper)))
            {
                throw new InvalidOperationException(
                    "base.Configure changed the derived mapper registrations.");
            }

            var mapper =
                (ITypeMapper<LocalSource, LocalDestination>)
                new ConnectedMapper();
            var result = mapper.Create(
                new LocalSource { Value = 17 },
                default);

            if (result.Value != 17)
            {
                throw new InvalidOperationException(
                    "The local mapping was not generated.");
            }

            try
            {
                mapper.Create(null, default);
            }
            catch (global::Morphant.Exceptions.NullSourceException)
            {
                return;
            }

            throw new InvalidOperationException(
                "The connected base root setting was not inherited.");
        }
    }
}
