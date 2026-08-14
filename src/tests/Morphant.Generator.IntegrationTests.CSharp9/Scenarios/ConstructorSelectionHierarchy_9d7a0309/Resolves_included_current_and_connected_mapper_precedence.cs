// Compiled integration scenario: TypeMapperConstructorSelectionTests/ConfigurationTests::Resolves_included_current_and_connected_mapper_precedence
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ConstructorSelectionHierarchy_9d7a0309
{
    public sealed class Source
    {
        public int Value { get; init; }
    }

    public abstract class ObservableDestination
    {
        protected ObservableDestination(string selected)
        {
            Selected = selected;
        }

        public string Selected { get; }
    }

    public sealed class IncludedDestination : ObservableDestination
    {
        public IncludedDestination() : base("parameterless") { }

        public IncludedDestination(int value, string label = "largest")
            : base(label + ":" + value) { }
    }

    public sealed class LocalDestination : ObservableDestination
    {
        public LocalDestination() : base("parameterless") { }

        public LocalDestination(int value, string label = "largest")
            : base(label + ":" + value) { }
    }

    public sealed class BaseRootDestination : ObservableDestination
    {
        public BaseRootDestination() : base("parameterless") { }

        public BaseRootDestination(int value, string label = "largest")
            : base(label + ":" + value) { }
    }

    public sealed class CurrentRootDestination : ObservableDestination
    {
        public CurrentRootDestination() : base("parameterless") { }

        public CurrentRootDestination(int value, string label = "largest")
            : base(label + ":" + value) { }
    }

    public abstract class IncludedBaseMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, IncludedDestination>()
                .ConstructorSelection(ConstructorSelection.Largest);
            builder.Map<Source, LocalDestination>()
                .ConstructorSelection(ConstructorSelection.Largest);
        }
    }

    [MorphantMapper]
    public partial class IncludedMapper : IncludedBaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.ConstructorSelection(ConstructorSelection.Explicit);

            builder.Map<Source, IncludedDestination>()
                .IncludeBase<Source, IncludedDestination>();
            builder.Map<Source, LocalDestination>()
                .IncludeBase<Source, LocalDestination>()
                .ConstructorSelection(
                    ConstructorSelection.Parameterless);
        }
    }

    public abstract class RootBaseMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.ConstructorSelection(ConstructorSelection.Largest);
            builder.ConstructorSelection(
                ConstructorSelection.Parameterless);
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
            builder.ConstructorSelection(ConstructorSelection.Largest);
            builder.Map<Source, CurrentRootDestination>();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var source = new Source { Value = 31 };
            var includedMapper = new IncludedMapper();
            var included = ((ITypeMapper<Source, IncludedDestination>)
                includedMapper).Create(source);
            var local = ((ITypeMapper<Source, LocalDestination>)
                includedMapper).Create(source);
            var fromBase = ((ITypeMapper<Source, BaseRootDestination>)
                new BaseRootMapper()).Create(source);
            var current = ((ITypeMapper<Source, CurrentRootDestination>)
                new CurrentRootMapper()).Create(source);

            if (included.Selected != "largest:31" ||
                local.Selected != "parameterless" ||
                fromBase.Selected != "parameterless" ||
                current.Selected != "largest:31")
            {
                throw new InvalidOperationException(
                    "ConstructorSelection did not follow mapping, included " +
                    "mapping, current mapper, and connected base precedence.");
            }
        }
    }
}
