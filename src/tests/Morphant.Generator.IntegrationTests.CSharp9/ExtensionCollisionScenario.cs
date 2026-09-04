using System;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9
{
    public sealed class FamilySource<T>
    {
        public T Value { get; set; } = default(T)!;
    }

    public sealed class FamilyDestination<T>
    {
        public FamilyDestination(T value)
        {
            Value = value;
        }

        public T Value { get; set; }
    }

    public abstract class FirstMapperFamily<TMapper, T> :
        TypeMapper<TMapper>
        where TMapper : FirstMapperFamily<TMapper, T>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<FamilySource<T>, FamilyDestination<T>>()
                .Convert(_ => new FamilyDestination<T>(default(T)!));
    }

    public abstract class SecondMapperFamily<TMapper, T> :
        TypeMapper<TMapper>
        where TMapper : SecondMapperFamily<TMapper, T>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<FamilySource<T>, FamilyDestination<T>>()
                .Members(source => new()
                {
                    Value = source.Value
                });
    }

    [MorphantMapper]
    public sealed partial class FirstFamilyMapper :
        FirstMapperFamily<FirstFamilyMapper, string>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<FamilySource<string>, FamilyDestination<string>>()
                .IncludeBase<
                    FamilySource<string>,
                    FamilyDestination<string>>();
        }
    }

    [MorphantMapper]
    public sealed partial class SecondFamilyMapper :
        SecondMapperFamily<SecondFamilyMapper, string>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<FamilySource<string>, FamilyDestination<string>>()
                .IncludeBase<
                    FamilySource<string>,
                    FamilyDestination<string>>();
        }
    }

    public static class ExtensionCollisionScenario
    {
        public static void Verify()
        {
            var first =
                (ITypeMapper<
                    FamilySource<string>,
                    FamilyDestination<string>>)
                new FirstFamilyMapper();
            var second =
                (ITypeMapper<
                    FamilySource<string>,
                    FamilyDestination<string>>)
                new SecondFamilyMapper();
            var source = new FamilySource<string> { Value = "source" };

            if (first.Create(source, default(MappingContext)).Value != null ||
                second.Create(source, default(MappingContext)).Value !=
                    "source")
            {
                throw new InvalidOperationException(
                    "Mapper-family callbacks were not applied.");
            }
        }
    }
}
