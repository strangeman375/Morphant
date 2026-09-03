// Compiled integration scenario: TypeMapperNestedMapTests/ReadOnlyMemberTests::Updates_non_null_read_only_members_and_skips_null_members
#nullable enable
#pragma warning disable CS1591

using System;
using Microsoft.Extensions.DependencyInjection;
using Morphant;
using Morphant.Context;
using Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ReadOnlyMember_c82cdb4e.Morphant.Generated;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ReadOnlyMember_c82cdb4e
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

        public readonly ChildDestination ExistingField = new(20);

        public readonly ChildDestination? EmptyField;

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
    public partial class OuterMapper : TypeMapper<OuterMapper>
    {
        public static int SourceCalls { get; private set; }

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<OuterSource, OuterDestination>()
                .ResolveUsing((_, _) => new OuterDestination())
                .Members((source, _) =>
                {
                    var members = new OuterDestinationMembers
                    {
                        Name = source.Name
                    };

                    if (source.Name.Length > 0)
                    {
                        Update(GetSource(source), members.Existing);
                        Update(GetSource(source), members.ExistingField);
                    }

                    Update<ChildDestination>(
                        ThrowingSource(source),
                        members.Empty);
                    Update<ChildDestination>(
                        ThrowingSource(source),
                        members.EmptyField);

                    return members;
                });

        private static ChildSource GetSource(OuterSource source)
        {
            SourceCalls++;
            return source.Child;
        }

        private static ChildSource ThrowingSource(OuterSource source) =>
            throw new InvalidOperationException(
                "A null read-only member evaluated its source.");
    }

    public sealed class ChildMapper :
        ITypeMapper<ChildSource, ChildDestination>
    {
        public int Calls { get; private set; }

        public ChildDestination Create(
            ChildSource? source,
            MappingContext context) =>
            throw new InvalidOperationException(
                "A read-only member selected nested Create.");

        public ChildDestination Update(
            ChildSource? source,
            ChildDestination? destination,
            MappingContext context)
        {
            Calls++;

            if (context.Operation != MappingOperation.Update ||
                destination is null)
            {
                throw new InvalidOperationException(
                    "A read-only member supplied the wrong destination.");
            }

            destination.Value += source!.Value;
            return new ChildDestination(999);
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var outer = new OuterMapper();
            var child = new ChildMapper();
            using var provider = new ServiceCollection()
                .AddSingleton<ITypeMapper<
                    OuterSource,
                    OuterDestination>>(outer)
                .AddSingleton<ITypeMapper<
                    ChildSource,
                    ChildDestination>>(child)
                .AddSingleton<IMapper, Mapper>()
                .BuildServiceProvider();
            var mapper = provider.GetRequiredService<IMapper>();
            var source = new OuterSource("created", new ChildSource(3));
            var created = mapper.Map<OuterSource, OuterDestination>(source);

            AssertState(created, "created", 13, 23, 1, child, 2);

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
                created.ExistingField.Value != 23 ||
                created.GetterCalls != 1)
            {
                throw new InvalidOperationException(
                    "The read-only mapping mutated outer previous.");
            }

            AssertState(updated, "updated", 14, 24, 1, child, 4);

            if (OuterMapper.SourceCalls != 4)
            {
                throw new InvalidOperationException(
                    "A read-only source was not evaluated exactly once.");
            }
        }

        private static void AssertState(
            OuterDestination value,
            string name,
            int childValue,
            int fieldValue,
            int getterCalls,
            ChildMapper mapper,
            int mapperCalls)
        {
            if (value.Name != name ||
                value.ExistingValue != childValue ||
                value.ExistingField.Value != fieldValue ||
                value.GetterCalls != getterCalls ||
                mapper.Calls != mapperCalls)
            {
                throw new InvalidOperationException(
                    "The read-only member mapping state is incorrect.");
            }
        }
    }
}
