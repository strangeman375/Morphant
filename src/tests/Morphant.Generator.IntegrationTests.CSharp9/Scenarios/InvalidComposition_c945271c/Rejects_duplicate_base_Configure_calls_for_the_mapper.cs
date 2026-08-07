// Compiled integration scenario: TypeMapperInheritanceTests/InvalidCompositionTests::Rejects_duplicate_base_Configure_calls_for_the_mapper
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.InvalidComposition_c945271c
{
    public sealed class Source
    {
    }

    public sealed class Destination
    {
    }

    public sealed class LocalSource
    {
    }

    public sealed class LocalDestination
    {
    }

    public abstract class BaseMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }

    [MorphantMapper]
    public partial class DerivedMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            base.Configure(builder);
            builder.Map<LocalSource, LocalDestination>();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            try
            {
                ((ITypeMapper<LocalSource, LocalDestination>)
                    new DerivedMapper())
                    .Create(new LocalSource(), default);
            }
            catch (NotSupportedException)
            {
                return;
            }

            throw new InvalidOperationException(
                "Duplicate base Configure calls were accepted.");
        }
    }
}
