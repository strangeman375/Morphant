#nullable enable

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.StaticContainers
{
    public sealed class Source { public int Id { get; set; } }
    public sealed class Destination { public int Id { get; set; } }

    public static class Models<T>
    {
        public static class Inner
        {
            public sealed class Source { public T Id { get; set; } = default!; }
            public sealed class Destination { public T Id { get; set; } = default!; }
        }
    }

    [MorphantMapper]
    public sealed partial class Mapper : TypeMapper<Mapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>();
            builder.Map<Models<int>.Inner.Source, Destination>();
            builder.Map<Source, Models<int>.Inner.Destination>();
            builder.Map<Models<string>.Inner.Source, Models<string>.Inner.Destination>();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new Mapper();
            ITypeMapper<Source, Destination> control = mapper;
            ITypeMapper<Models<int>.Inner.Source, Destination> nestedSource = mapper;
            ITypeMapper<Source, Models<int>.Inner.Destination> nestedDestination = mapper;
            ITypeMapper<Models<string>.Inner.Source, Models<string>.Inner.Destination> both = mapper;

            if (control.Create(new Source { Id = 11 }).Id != 11 ||
                nestedSource.Create(new Models<int>.Inner.Source { Id = 13 }).Id != 13 ||
                nestedDestination.Create(new Source { Id = 17 }).Id != 17 ||
                both.Create(new Models<string>.Inner.Source { Id = "value" }).Id != "value")
            {
                throw new System.InvalidOperationException("Nested static-container mappings lost values.");
            }

            var existing = new Models<int>.Inner.Destination { Id = -1 };
            var updated = nestedDestination.Update(new Source { Id = 19 }, existing);
            if (!object.ReferenceEquals(existing, updated) || existing.Id != 19)
            {
                throw new System.InvalidOperationException("Nested destination Update lost identity or assignment.");
            }
        }
    }
}
