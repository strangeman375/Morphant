// Compiled integration scenario: tuple interaction with composition and dispatch
#nullable enable
#pragma warning disable CS1591

using System;
using Microsoft.Extensions.DependencyInjection;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.TupleInteractions_a7b10006
{
    public class Animal
    {
        public string Name { get; init; } = string.Empty;
    }

    public sealed class Dog : Animal
    {
        public string Breed { get; init; } = string.Empty;
    }

    public class EntityDto
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    public sealed class TaggedEntityDto : EntityDto
    {
        public string Tag { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Animal, (string Name, string Kind)>()
                .ForDerived<Dog, (string Name, string Kind)>()
                .Members(source => new()
                {
                    Name = source.Name,
                    Kind = "animal"
                })
                .MemberSelection(MemberSelection.Explicit);

            builder.Map<Dog, (string Name, string Kind)>()
                .Members(source => new()
                {
                    Name = source.Name,
                    Kind = source.Breed
                })
                .MemberSelection(MemberSelection.Explicit);

            builder.Map<(int Id, string Name), EntityDto>();
            builder.Map<(int Id, string Name), TaggedEntityDto>()
                .IncludeBase<(int Id, string Name), EntityDto>()
                .Members(source => new()
                {
                    Tag = source.Name + ":tag"
                });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var generated = new TestMapper();

            using var provider = new ServiceCollection()
                .AddSingleton<ITypeMapper<
                    Animal,
                    (string Name, string Kind)>>(generated)
                .AddSingleton<ITypeMapper<
                    Dog,
                    (string Name, string Kind)>>(generated)
                .AddSingleton<ITypeMapper<
                    (int Id, string Name),
                    EntityDto>>(generated)
                .AddSingleton<ITypeMapper<
                    (int Id, string Name),
                    TaggedEntityDto>>(generated)
                .AddSingleton<IMapper, Mapper>()
                .BuildServiceProvider();
            var mapper = provider.GetRequiredService<IMapper>();

            Animal dog = new Dog
            {
                Name = "Ada",
                Breed = "collie"
            };
            var summary = mapper.Map<
                Animal,
                (string Name, string Kind)>(dog);
            var created = mapper.Map<
                (int Id, string Name),
                TaggedEntityDto>((Id: 17, Name: "Grace"));
            var previous = new TaggedEntityDto
            {
                Id = -1,
                Name = "old",
                Tag = "old:tag"
            };
            var updated = mapper.Map(
                (Id: 23, Name: "Linus"),
                previous);

            if (summary != (Name: "Ada", Kind: "collie") ||
                created.Id != 17 ||
                created.Name != "Grace" ||
                created.Tag != "Grace:tag" ||
                !ReferenceEquals(previous, updated) ||
                updated.Id != 23 ||
                updated.Name != "Linus" ||
                updated.Tag != "Linus:tag")
            {
                throw new InvalidOperationException(
                    "Tuple composition, runtime dispatch, or DI routing " +
                    "produced an unexpected result.");
            }
        }
    }
}
