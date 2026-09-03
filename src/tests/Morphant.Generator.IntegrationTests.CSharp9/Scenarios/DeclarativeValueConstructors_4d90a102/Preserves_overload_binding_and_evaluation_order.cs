// Compiled integration scenario: TypeMapperDeclarativeValueTests::Preserves_value_constructor_binding_and_evaluation_order
#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using Morphant;
using Morphant.Context;
using Morphant.Members;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.DeclarativeValueConstructors_4d90a102
{
    public sealed class IntSelection
    {
    }

    public sealed class LongSelection
    {
    }

    public sealed class ObjectSelection
    {
    }

    public sealed class Selected<TSelection>
    {
        public Selected(int value)
        {
            Kind = "int";
            Value = value;
        }

        public Selected(long value)
        {
            Kind = "long";
            Value = value;
        }

        public Selected(object value)
        {
            Kind = "object";
            Value = value;
        }

        public string Kind { get; }

        public object Value { get; }
    }

    public sealed class ChildSource
    {
        public int Value { get; init; }
    }

    public sealed class ChildDestination
    {
        public ChildDestination(int value)
        {
            Value = value;
        }

        public int Value { get; }
    }

    public sealed class Source
    {
        public int First { get; init; }

        public int Second { get; init; }

        public long Local { get; init; }

        public ChildSource Child { get; init; } = new();

        public int Automatic { get; init; }
    }

    public sealed class CompositeDestination
    {
        public CompositeDestination(
            int first,
            int second,
            long local,
            object nested,
            Action callback)
        {
            First = first;
            Second = second;
            Local = local;
            Nested = nested;
            Callback = callback;
        }

        public int First { get; }

        public int Second { get; }

        public long Local { get; }

        public object Nested { get; }

        public Action Callback { get; }
    }

    public sealed class NestedSelectionDestination
    {
        public NestedSelectionDestination(ChildDestination value)
        {
            Kind = "child";
            Value = value;
        }

        public NestedSelectionDestination(object value)
        {
            Kind = "object";
            Value = value;
        }

        public string Kind { get; }

        public object Value { get; }
    }

    public sealed class ImplicitNestedDestination
    {
        public ImplicitNestedDestination(object value)
        {
            Value = value;
        }

        public object Value { get; }
    }

    public sealed class MarkerDestination
    {
        public MarkerDestination(
            int automatic,
            string ignored = "default")
        {
            Automatic = automatic;
            Ignored = ignored;
        }

        public int Automatic { get; }

        public string Ignored { get; }
    }

    public sealed class ByConventionValueDestination
    {
        public ByConventionValueDestination(object boxed)
        {
            Boxed = boxed;
        }

        public object Boxed { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        public static List<string> Events { get; } = new();

        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<ChildSource, ChildDestination>()
                .Construct(source => new(source.Value));

            builder.Map<Source, Selected<IntSelection>>()
                .Construct(_ => new(Value(1)));

            builder.Map<Source, Selected<LongSelection>>()
                .Construct(_ => new(Value<long>(1)));

            builder.Map<Source, Selected<ObjectSelection>>()
                .Construct(_ => new(Value<object>(1)));

            builder.Map<Source, CompositeDestination>()
                .Construct(source =>
                {
                    var local = Value<long>(
                        Track("local", source.Local));

                    return new(
                        second: Value(Track(
                            "second",
                            source.Second)),
                        first: Value(Track(
                            "first",
                            source.First)),
                        local: local,
                        nested: (ConstructorParameter<object>)
                            Map<ChildDestination>(source.Child),
                        callback: Value<Action>(
                            () => Events.Add("callback")));
                });

            builder.Map<Source, NestedSelectionDestination>()
                .Construct(source => new(
                    (ConstructorParameter<object>)
                        Map<ChildDestination>(source.Child)));

            builder.Map<Source, ImplicitNestedDestination>()
                .Construct(source => new(
                    Map<ChildDestination>(source.Child)));

            builder.Map<Source, MarkerDestination>()
                .Construct(_ => new(
                    Auto<int>(),
                    Ignore<string>()));

            builder.Map<Source, ByConventionValueDestination>()
                .Construct(source => new(
                    ByConvention(),
                    new()
                    {
                        boxed = Value<object>(source.First)
                    }));
        }

        private static T Track<T>(string name, T value)
        {
            Events.Add(name);
            return value;
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            TestMapper.Events.Clear();
            var mapper = new TestMapper();
            var source = new Source
            {
                First = 11,
                Second = 12,
                Local = 13,
                Child = new ChildSource { Value = 14 },
                Automatic = 15
            };
            var context = default(MappingContext);
            var selectedInt =
                ((ITypeMapper<Source, Selected<IntSelection>>)mapper)
                .Create(source, context);
            var selectedLong =
                ((ITypeMapper<Source, Selected<LongSelection>>)mapper)
                .Create(source, context);
            var selectedObject =
                ((ITypeMapper<Source, Selected<ObjectSelection>>)mapper)
                .Create(source, context);
            var composite =
                ((ITypeMapper<Source, CompositeDestination>)mapper)
                .Create(source);
            var marker =
                ((ITypeMapper<Source, MarkerDestination>)mapper)
                .Create(source, context);
            var nestedSelection =
                ((ITypeMapper<Source, NestedSelectionDestination>)mapper)
                .Create(source);
            var byConvention =
                ((ITypeMapper<Source, ByConventionValueDestination>)mapper)
                .Create(source, context);
            var implicitNested =
                ((ITypeMapper<Source, ImplicitNestedDestination>)mapper)
                .Create(source);

            if (selectedInt.Kind != "int" ||
                selectedInt.Value is not 1 ||
                selectedLong.Kind != "long" ||
                selectedLong.Value is not 1L ||
                selectedObject.Kind != "object" ||
                selectedObject.Value is not 1 ||
                composite.First != 11 ||
                composite.Second != 12 ||
                composite.Local != 13 ||
                composite.Nested is not ChildDestination
                {
                    Value: 14
                } ||
                nestedSelection.Kind != "object" ||
                nestedSelection.Value is not ChildDestination
                {
                    Value: 14
                } ||
                byConvention.Boxed is not 11 ||
                implicitNested.Value is not ChildDestination
                {
                    Value: 14
                } ||
                marker.Automatic != 15 ||
                marker.Ignored != "default")
            {
                throw new InvalidOperationException(
                    "Declarative constructor binding changed.");
            }

            var expectedEvents = new[]
            {
                "local",
                "second",
                "first"
            };

            if (TestMapper.Events.Count != expectedEvents.Length)
            {
                throw new InvalidOperationException(
                    "A constructor value was evaluated more than once.");
            }

            for (var index = 0; index < expectedEvents.Length; index++)
            {
                if (TestMapper.Events[index] != expectedEvents[index])
                {
                    throw new InvalidOperationException(
                        "Constructor argument evaluation order changed.");
                }
            }

            composite.Callback();

            if (TestMapper.Events.Count != expectedEvents.Length + 1 ||
                TestMapper.Events[^1] != "callback")
            {
                throw new InvalidOperationException(
                    "The constructor delegate value was invoked eagerly.");
            }
        }
    }
}
