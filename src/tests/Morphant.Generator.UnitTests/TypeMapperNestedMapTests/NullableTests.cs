using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TypeMapperNestedMapTests;

[TestFixture]
internal sealed class NullableTests
{
    [Test]
    public void Preserves_nullable_pair_types_and_explicit_null_update()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using Morphant;
using Morphant.Context;

namespace TestCase
{
    public sealed record OuterSource(string? Text, int? Number);

    public sealed class OuterDestination
    {
        public string? Text { get; set; }

        public int? CreatedNumber { get; set; }

        public int? UpdatedNumber { get; set; }
    }

    [MorphantMapper]
    public partial class OuterMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<OuterSource, OuterDestination>()
                .Members((source, _) => new()
                {
                    Text = Map<string?>(source.Text),
                    CreatedNumber = Map(source.Number),
                    UpdatedNumber = Map<int?>(
                        source.Number,
                        (int?)null)
                });
    }

    public sealed class TextMapper : ITypeMapper<string?, string?>
    {
        public int Calls { get; private set; }

        public string? Map(
            string? source,
            MappingContext context)
        {
            if (context.Operation != MappingOperation.Create)
            {
                throw new InvalidOperationException(
                    "Nullable text used the wrong operation.");
            }

            Calls++;
            return source ?? "text-null";
        }

        public string? Map(
            string? source,
            string? destination,
            MappingContext context) =>
            throw new InvalidOperationException(
                "Nullable text unexpectedly used Update.");
    }

    public sealed class NumberMapper : ITypeMapper<int?, int?>
    {
        public List<MappingOperation> Operations { get; } = new();

        public int? Map(
            int? source,
            MappingContext context)
        {
            Operations.Add(context.Operation);
            return source ?? 7;
        }

        public int? Map(
            int? source,
            int? destination,
            MappingContext context)
        {
            if (destination.HasValue)
            {
                throw new InvalidOperationException(
                    "The explicit null destination was replaced.");
            }

            Operations.Add(context.Operation);
            return source ?? 9;
        }
    }

    public sealed class ManualServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, object> _services = new();

        public object? GetService(Type serviceType) =>
            _services.TryGetValue(serviceType, out var service)
                ? service
                : null;

        public void Add<TService>(TService service)
            where TService : class =>
            _services[typeof(IEnumerable<TService>)] =
                new TService[] { service };
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var outer = new OuterMapper();
            var text = new TextMapper();
            var number = new NumberMapper();
            var provider = new ManualServiceProvider();
            provider.Add<ITypeMapper<OuterSource, OuterDestination>>(
                outer);
            provider.Add<ITypeMapper<string?, string?>>(text);
            provider.Add<ITypeMapper<int?, int?>>(number);
            var mapper = new Mapper(provider);

            var result = mapper.Map<OuterSource, OuterDestination>(
                new OuterSource(null, null));

            if (result.Text != "text-null" ||
                result.CreatedNumber != 7 ||
                result.UpdatedNumber != 9 ||
                text.Calls != 1 ||
                number.Operations.Count != 2 ||
                number.Operations[0] != MappingOperation.Create ||
                number.Operations[1] != MappingOperation.Update)
            {
                throw new InvalidOperationException(
                    "Nullable nested pair semantics are incorrect.");
            }
        }
    }
}
""";

        BasicMembersTypeMapperGeneratorTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }
}
