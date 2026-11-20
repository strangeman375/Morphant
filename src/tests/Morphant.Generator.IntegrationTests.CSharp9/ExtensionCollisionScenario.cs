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

    public abstract class FirstMapperFamily<T> :
        TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<FamilySource<T>, FamilyDestination<T>>()
                .Convert(_ => new FamilyDestination<T>(default(T)!));
    }

    public abstract class SecondMapperFamily<T> :
        TypeMapper
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
        FirstMapperFamily<string>
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
        SecondMapperFamily<string>
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

    public sealed class ErasedSource<T>
    {
        public T Value { get; set; } = default(T)!;
    }

    public sealed class ErasedDestination<T>
    {
        public ErasedDestination(T value)
        {
            Value = value;
        }

        public T Value { get; set; }
    }

    [MorphantMapper]
    public sealed partial class ObjectMapper :
        TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<ErasedSource<object>, ErasedDestination<object>>();
    }

    [MorphantMapper]
    public abstract partial class StateMapper<TState> : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<
                    ErasedSource<object>,
                    ErasedDestination<object>>();
    }

    public sealed class ClosedStateMapper :
        StateMapper<string>
    {
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

            var closed =
                (ITypeMapper<
                    ErasedSource<object>,
                    ErasedDestination<object>>)
                new ClosedStateMapper();

            var mapped = closed.Create(new ErasedSource<object> { Value = "value" }, default);
            if (!Equals(mapped.Value, "value"))
                throw new InvalidOperationException("An unused mapper parameter changed the mapping.");
        }
    }
}
