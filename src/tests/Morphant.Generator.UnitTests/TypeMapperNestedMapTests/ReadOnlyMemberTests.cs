using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TypeMapperNestedMapTests;

[TestFixture]
internal sealed class ReadOnlyMemberTests
{
    [Test]
    public void Updates_non_null_get_only_member_and_skips_null_member()
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
using TestCase.Morphant.Generated;

namespace TestCase
{
    public sealed record ChildSource(int Value);

    public sealed class ChildDestination
    {
        public ChildDestination(int value)
        {
            Value = value;
        }

        public int Value { get; set; }
    }

    public sealed record OuterSource(
        string Name,
        ChildSource Child);

    public sealed class OuterDestination
    {
        private readonly ChildDestination _existing = new(10);

        public string Name { get; set; } = string.Empty;

        public int GetterCalls { get; private set; }

        public ChildDestination Existing
        {
            get
            {
                GetterCalls++;
                return _existing;
            }
        }

        public int ExistingValue => _existing.Value;

        public ChildDestination? Empty => null;
    }

    [MorphantMapper]
    public partial class OuterMapper : TypeMapper
    {
        public static int SourceCalls { get; private set; }

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<OuterSource, OuterDestination>()
                .Construct((_, _) => new(
                    ByFactory<OuterDestination>(
                        () => new OuterDestination())))
                .Members((source, _) =>
                {
                    var members = new OuterDestinationMembers
                    {
                        Name = source.Name
                    };

                    if (source.Name.Length > 0)
                    {
                        Update(GetSource(source), members.Existing);
                    }

                    Update<ChildDestination>(
                        ThrowingSource(source),
                        members.Empty);

                    return members;
                });

        private static ChildSource GetSource(OuterSource source)
        {
            SourceCalls++;
            return source.Child;
        }

        private static ChildSource ThrowingSource(OuterSource source) =>
            throw new InvalidOperationException(
                "A null get-only member evaluated its source.");
    }

    public sealed class ChildMapper :
        ITypeMapper<ChildSource, ChildDestination>
    {
        public int Calls { get; private set; }

        public ChildDestination Map(
            ChildSource? source,
            MappingContext context) =>
            throw new InvalidOperationException(
                "A get-only member selected nested Create.");

        public ChildDestination Map(
            ChildSource? source,
            ChildDestination? destination,
            MappingContext context)
        {
            Calls++;

            if (context.Operation != MappingOperation.Update ||
                destination is null)
            {
                throw new InvalidOperationException(
                    "A get-only member supplied the wrong destination.");
            }

            destination.Value += source!.Value;
            return new ChildDestination(999);
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
            var child = new ChildMapper();
            var provider = new ManualServiceProvider();
            provider.Add<ITypeMapper<OuterSource, OuterDestination>>(outer);
            provider.Add<ITypeMapper<ChildSource, ChildDestination>>(child);
            var mapper = new Mapper(provider);
            var source = new OuterSource("created", new ChildSource(3));
            var created = mapper.Map<OuterSource, OuterDestination>(source);

            AssertState(created, "created", 13, 1, child, 1);

            var updated = mapper.Map(
                new OuterSource("updated", new ChildSource(4)),
                created);

            if (ReferenceEquals(created, updated))
            {
                throw new InvalidOperationException(
                    "The outer Update did not select its replacement.");
            }

            if (created.Name != "created" ||
                created.ExistingValue != 13 ||
                created.GetterCalls != 1)
            {
                throw new InvalidOperationException(
                    "The get-only mapping mutated outer previous.");
            }

            AssertState(updated, "updated", 14, 1, child, 2);

            if (OuterMapper.SourceCalls != 2)
            {
                throw new InvalidOperationException(
                    "A get-only source was not evaluated exactly once.");
            }
        }

        private static void AssertState(
            OuterDestination value,
            string name,
            int childValue,
            int getterCalls,
            ChildMapper mapper,
            int mapperCalls)
        {
            if (value.Name != name ||
                value.ExistingValue != childValue ||
                value.GetterCalls != getterCalls ||
                mapper.Calls != mapperCalls)
            {
                throw new InvalidOperationException(
                    "The get-only member mapping state is incorrect.");
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
