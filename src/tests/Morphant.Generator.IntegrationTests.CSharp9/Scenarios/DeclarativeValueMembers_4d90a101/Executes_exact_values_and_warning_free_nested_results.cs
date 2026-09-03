// Compiled integration scenario: TypeMapperDeclarativeValueTests::Executes_exact_member_values_and_warning_free_nested_results
#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Morphant;
using Morphant.Context;
using Morphant.Members;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.DeclarativeValueMembers_4d90a101
{
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

    public readonly struct InputValue
    {
        public InputValue(int value)
        {
            Value = value;
        }

        public int Value { get; }
    }

    public readonly struct OutputValue
    {
        public OutputValue(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public static implicit operator OutputValue(InputValue value) =>
            new(value.Value);
    }

    public sealed class Source
    {
        public int Number { get; init; }

        public string? Text { get; init; }

        public string NonNullText { get; init; } = string.Empty;

        public bool SelectFirst { get; init; }

        public ChildSource Child { get; init; } = new();

        public InputValue Converted { get; init; }

        public int Automatic { get; init; }
    }

    public sealed class Destination
    {
        public Destination()
        {
            Ignored = "created";
        }

        public int Plain { get; set; }

        public int Inferred { get; set; }

        public int Local { get; set; }

        public int Conditional { get; set; }

        public int MixedConditional { get; set; }

        public int Wrapped { get; set; }

        public string? Nullable { get; set; }

        [AllowNull]
        public string AllowNullText { get; set; } = "initial";

        [DisallowNull]
        public string? DisallowNullText { get; set; }

        public object Boxed { get; set; } = new();

        public object NestedObject { get; set; } = new();

        public Action Callback { get; set; } = Empty;

        public Func<string> Formatter { get; set; } = EmptyText;

        public OutputValue Converted { get; set; }

        public int Automatic { get; set; }

        public string Ignored { get; set; }

        private static void Empty()
        {
        }

        private static string EmptyText() => string.Empty;
    }

    public class GenericSource<T>
    {
        public T Value { get; init; } = default!;
    }

    public class GenericDestination<T>
    {
        public T Value { get; set; } = default!;
    }

    public sealed class ClosedSource : GenericSource<int>
    {
    }

    public sealed class ClosedDestination : GenericDestination<int>
    {
    }

    public abstract class GenericValueMapper<T> : TypeMapper<GenericValueMapper<T>>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<GenericSource<T>, GenericDestination<T>>()
                .Members((source, _) => new()
                {
                    Value = Value<T>(source.Value)
                });
    }

    [MorphantMapper]
    public partial class ClosedValueMapper : GenericValueMapper<int>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<ClosedSource, ClosedDestination>()
                .IncludeBase<
                    GenericSource<int>,
                    GenericDestination<int>>();
        }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        public static List<string> Events { get; } = new();

        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<ChildSource, ChildDestination>()
                .Construct(source => new(source.Value));

            builder.Map<Source, Destination>()
                .Members((source, _) =>
                {
                    var local = Value<int>(
                        Track("local", source.Number + 1));
                    var conditional = source.SelectFirst
                        ? Value<int>(Track(
                            "conditional-first",
                            source.Number + 2))
                        : Value<int>(Track(
                            "conditional-second",
                            source.Number + 3));

                    return new()
                    {
                        Plain = Track("plain", source.Number),
                        Inferred = Value(Track(
                            "inferred",
                            source.Number + 4)),
                        Local = local,
                        Conditional = conditional,
                        MixedConditional = source.SelectFirst
                            ? Value<int>(Track(
                                "mixed-conditional",
                                source.Number + 6))
                            : source.Number + 7,
                        Wrapped = (Member<int>)Value<int>(Track(
                            "wrapped",
                            source.Number + 5)),
                        Nullable = Value<string?>(source.Text),
                        AllowNullText = Value<string?>(source.Text),
                        DisallowNullText = Value<string>(
                            source.NonNullText),
                        Boxed = Value<object>(source.Number),
                        NestedObject = Map<ChildDestination>(source.Child),
                        Callback = Value<Action>(
                            () => Events.Add(
                                "callback-" + source.Number)),
                        Formatter = Value<Func<string>>(Format),
                        Converted = Value<OutputValue>(source.Converted),
                        Automatic = Auto<int>(),
                        Ignored = Ignore<string>()
                    };
                });
        }

        private static T Track<T>(string name, T value)
        {
            Events.Add(name);
            return value;
        }

        private static string Format() => "formatted";
    }

    public static class Scenario
    {
        public static void Verify()
        {
            TestMapper.Events.Clear();
            var mapper = (ITypeMapper<Source, Destination>)
                new TestMapper();
            var source = new Source
            {
                Number = 10,
                Text = null,
                NonNullText = "non-null",
                SelectFirst = true,
                Child = new ChildSource { Value = 17 },
                Converted = new InputValue(18),
                Automatic = 19
            };
            var result = mapper.Create(source);

            if (result.Plain != 10 ||
                result.Inferred != 14 ||
                result.Local != 11 ||
                result.Conditional != 12 ||
                result.MixedConditional != 16 ||
                result.Wrapped != 15 ||
                result.Nullable is not null ||
                result.AllowNullText is not null ||
                result.DisallowNullText != "non-null" ||
                result.Boxed is not 10 ||
                result.NestedObject is not ChildDestination
                {
                    Value: 17
                } ||
                result.Converted.Value != 18 ||
                result.Automatic != 19 ||
                result.Ignored != "created" ||
                result.Formatter() != "formatted")
            {
                throw new InvalidOperationException(
                    "Declarative member values were not lowered exactly.");
            }

            var expectedEvents = new[]
            {
                "local",
                "conditional-first",
                "plain",
                "inferred",
                "mixed-conditional",
                "wrapped"
            };

            AssertEvents(expectedEvents);

            result.Callback();

            if (TestMapper.Events.Count != expectedEvents.Length + 1 ||
                TestMapper.Events[^1] != "callback-10")
            {
                throw new InvalidOperationException(
                    "The declarative delegate value was invoked eagerly.");
            }

            var closed =
                ((ITypeMapper<ClosedSource, ClosedDestination>)
                    new ClosedValueMapper())
                .Create(
                    new ClosedSource { Value = 23 },
                    default(MappingContext));

            if (closed.Value != 23)
            {
                throw new InvalidOperationException(
                    "An inherited generic Value type was not specialized.");
            }

            TestMapper.Events.Clear();
            var previous = new Destination
            {
                Ignored = "previous",
                NestedObject = new ChildDestination(17)
            };
            var updated = mapper.Update(
                new Source
                {
                    Number = 20,
                    Text = "updated",
                    NonNullText = "updated-non-null",
                    SelectFirst = false,
                    Child = new ChildSource { Value = 27 },
                    Converted = new InputValue(28),
                    Automatic = 29
                },
                previous);

            if (!ReferenceEquals(updated, previous) ||
                updated.Plain != 20 ||
                updated.Inferred != 24 ||
                updated.Local != 21 ||
                updated.Conditional != 23 ||
                updated.MixedConditional != 27 ||
                updated.Wrapped != 25 ||
                updated.Nullable != "updated" ||
                updated.AllowNullText != "updated" ||
                updated.DisallowNullText != "updated-non-null" ||
                updated.Boxed is not 20 ||
                updated.NestedObject is not ChildDestination
                {
                    Value: 17
                } ||
                updated.Converted.Value != 28 ||
                updated.Automatic != 29 ||
                updated.Ignored != "previous" ||
                updated.Formatter() != "formatted")
            {
                throw new InvalidOperationException(
                    "Update did not preserve declarative value semantics.");
            }

            AssertEvents(
                "local",
                "conditional-second",
                "plain",
                "inferred",
                "wrapped");

            updated.Callback();

            if (TestMapper.Events.Count != 6 ||
                TestMapper.Events[^1] != "callback-20")
            {
                throw new InvalidOperationException(
                    "The updated delegate captured the wrong source.");
            }
        }

        private static void AssertEvents(params string[] expected)
        {
            if (TestMapper.Events.Count != expected.Length)
            {
                throw new InvalidOperationException(
                    "A declarative value was evaluated more than once.");
            }

            foreach (var expectedEvent in expected)
            {
                var count = 0;

                foreach (var actualEvent in TestMapper.Events)
                {
                    if (actualEvent == expectedEvent)
                    {
                        count++;
                    }
                }

                if (count != 1)
                {
                    throw new InvalidOperationException(
                        "A selected declarative value was not evaluated " +
                        "exactly once.");
                }
            }
        }
    }
}
