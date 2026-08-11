// Compiled integration scenario: InheritanceDiagnosticsTests::Inaccessible_inherited_callbacks_reject_every_effective_family
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0028

using Morphant;
using Morphant.Exceptions;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.InheritanceAccessibilityRecovery_7c0f0028
{
    public sealed class Source1 { public string Value { get; init; } = ""; }
    public sealed class Source2 { public string Value { get; init; } = ""; }
    public sealed class Source3 { public string Value { get; init; } = ""; }
    public sealed class Source4 { public string Value { get; init; } = ""; }
    public sealed class Source5 { public string Value { get; init; } = ""; }
    public sealed class Source6 { public string Value { get; init; } = ""; }

    public sealed class Destination1
    {
        public Destination1(string value) => Value = value;
        public string Value { get; }
    }

    public sealed class Destination2
    {
        public Destination2(string value) => Value = value;
        public string Value { get; }
    }

    public sealed class Destination3
    {
        public Destination3(string value) => Value = value;
        public string Value { get; }
    }

    public sealed class Destination4
    {
        public Destination4(string value) => Value = value;
        public string Value { get; }
    }

    public sealed class Destination5
    {
        public string Value { get; set; } = "";
    }

    public sealed class Destination6
    {
        public Destination6(string value) => Value = value;
        public string Value { get; }
    }

    public sealed class TransitiveSource { }
    public sealed class TransitiveDestination
    {
        public string Value { get; set; } = "";
    }

    public sealed class ValidSource
    {
        public int Value { get; init; }
    }

    public sealed class ValidDestination
    {
        public int Value { get; set; }
    }

    public abstract class BaseMapper : TypeMapper
    {
        private static string Secret(string value) => "secret:" + value;

        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source1, Destination1>()
                .Construct(source => new(Secret(source.Value)));
            builder.Map<Source2, Destination2>()
                .Resolve((source, _) => new(Secret(source.Value)));
            builder.Map<Source3, Destination3>()
                .ConstructUsing(source =>
                    new Destination3(Secret(source.Value)));
            builder.Map<Source4, Destination4>()
                .ResolveUsing((source, _) =>
                    new Destination4(Secret(source.Value)));
            builder.Map<Source5, Destination5>()
                .Members(source => new()
                {
                    Value = Secret(source.Value)
                });
            builder.Map<Source6, Destination6>()
                .Convert(source =>
                    new Destination6(Secret(source!.Value)));
            builder.Map<TransitiveSource, TransitiveDestination>()
                .Members(_ => new()
                {
                    Value = Secret("transitive")
                });
        }
    }

    public abstract class MiddleMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<TransitiveSource, TransitiveDestination>()
                .IncludeBase<TransitiveSource, TransitiveDestination>();
        }
    }

    [MorphantMapper]
    public partial class TestMapper : MiddleMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Source1, Destination1>()
                .IncludeBase<Source1, Destination1>();
            builder.Map<Source2, Destination2>()
                .IncludeBase<Source2, Destination2>();
            builder.Map<Source3, Destination3>()
                .IncludeBase<Source3, Destination3>();
            builder.Map<Source4, Destination4>()
                .IncludeBase<Source4, Destination4>();
            builder.Map<Source5, Destination5>()
                .IncludeBase<Source5, Destination5>();
            builder.Map<Source6, Destination6>()
                .IncludeBase<Source6, Destination6>();
            builder.Map<TransitiveSource, TransitiveDestination>()
                .IncludeBase<TransitiveSource, TransitiveDestination>();
            builder.Map<ValidSource, ValidDestination>();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();

            VerifyPair<Source1, Destination1>(
                mapper,
                new Source1(),
                new Destination1("previous"));
            VerifyPair<Source2, Destination2>(
                mapper,
                new Source2(),
                new Destination2("previous"));
            VerifyPair<Source3, Destination3>(
                mapper,
                new Source3(),
                new Destination3("previous"));
            VerifyPair<Source4, Destination4>(
                mapper,
                new Source4(),
                new Destination4("previous"));
            VerifyPair<Source5, Destination5>(
                mapper,
                new Source5(),
                new Destination5());
            VerifyPair<Source6, Destination6>(
                mapper,
                new Source6(),
                new Destination6("previous"));
            VerifyPair<TransitiveSource, TransitiveDestination>(
                mapper,
                new TransitiveSource(),
                new TransitiveDestination());

            var valid =
                ((ITypeMapper<ValidSource, ValidDestination>)mapper)
                    .Create(new ValidSource { Value = 29 }, default);

            if (valid.Value != 29)
            {
                throw new InvalidOperationException(
                    "An independent mapping pair did not execute.");
            }
        }

        private static void VerifyPair<TSource, TDestination>(
            object mapper,
            TSource source,
            TDestination destination)
        {
            var typed = (ITypeMapper<TSource, TDestination>)mapper;

            ExpectConfigurationFailure(() => typed.Create(source, default));
            ExpectConfigurationFailure(() =>
                typed.Update(source, destination, default));
        }

        private static void ExpectConfigurationFailure(Action action)
        {
            try
            {
                action();
            }
            catch (MappingConfigurationException)
            {
                return;
            }

            throw new InvalidOperationException(
                "An inaccessible inherited callback was executed.");
        }
    }
}
