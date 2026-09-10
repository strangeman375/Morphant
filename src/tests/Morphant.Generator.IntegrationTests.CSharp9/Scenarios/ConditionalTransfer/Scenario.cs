#nullable enable
using System;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ConditionalTransfer
{
    public enum Surface { Construct, Members, Convert }
    public enum ExpressionCase { Arithmetic, Chain, NestedChain, Indexer, Statement, Deferred, CoalesceThrow, ExplicitStatic }

    public sealed class Source
    {
        public string? Text { get; init; }
        public ExpressionCase Case { get; init; }
        public string Trace { get; private set; } = "";
        public string? Read() { Trace += "R"; return Text; }
        public int Argument() { Trace += "A"; return 1; }
        public int Index() { Trace += "I"; return 0; }
        public string Fallback() { Trace += "F"; return "fallback"; }
        public void Record(string value) => Trace += value;
    }

    public sealed class ConstructTag { }
    public sealed class MembersTag { }
    public sealed class ConvertTag { }
    public sealed class ExpressionsTag { }
    public sealed class Destination<T>
    {
        public Destination(string value) => Value = value;
        public string Value { get; set; }
    }

    public static class TextExtensions
    {
        public static string? Maybe(this string text, int argument, Source source)
        {
            source.Record("M");
            return text.Length == 0 ? null : text + argument;
        }

        public static string Echo(this string text, Source source)
        {
            source.Record("E");
            return text;
        }

        public static int? Measure(this string text, Source source)
        {
            source.Record("M");
            return text.Length == 0 ? null : text.Length;
        }

        public static void Touch(this string text, int argument, Source source) =>
            source.Record("T" + text + argument);
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination<ConstructTag>>()
                .Construct(source => new(
                    source.Read()?.Maybe(source.Argument(), source) ?? source.Fallback()));
            builder.Map<Source, Destination<MembersTag>>()
                .Members(source => new()
                {
                    Value = source.Read()?.Maybe(source.Argument(), source) ?? source.Fallback()
                });
            builder.Map<Source, Destination<ConvertTag>>()
                .Convert(source => new(
                    source!.Read()?.Maybe(source.Argument(), source) ?? source.Fallback()));
            builder.Map<Source, Destination<ExpressionsTag>>()
                .Convert(source =>
                {
                    if (source is null)
                        throw new ArgumentNullException(nameof(source));

                    switch (source.Case)
                    {
                        case ExpressionCase.Arithmetic:
                            return new((source.Read()?.Measure(source) + source.Argument())?.ToString() ?? "null");
                        case ExpressionCase.Chain:
                            return new(source.Read()?.Echo(source).Length.ToString() ?? "fallback");
                        case ExpressionCase.NestedChain:
                            return new(source.Read()?.Maybe(source.Argument(), source)?.Echo(source).ToUpperInvariant() ?? "fallback");
                        case ExpressionCase.Indexer:
                            return new((source.Read()?.Echo(source)[source.Index()] ?? '?').ToString());
                        case ExpressionCase.Statement:
                            source.Read()?.Maybe(source.Argument(), source)?.Touch(source.Argument(), source);
                            return new("done");
                        case ExpressionCase.Deferred:
                            Action touch = () => source.Read()?.Touch(source.Argument(), source);
                            void LocalTouch() => source.Read()?.Touch(source.Argument(), source);
                            Func<string> read = () =>
                            {
                                var conditionalReceiver = "fallback";
                                return source.Read()?.Maybe(source.Argument(), source) ?? conditionalReceiver;
                            };
                            source.Record("D");
                            touch();
                            LocalTouch();
                            return new(read());
                        case ExpressionCase.CoalesceThrow:
                            return new(source.Read()?.Maybe(source.Argument(), source) ?? throw new InvalidOperationException("missing"));
                        case ExpressionCase.ExplicitStatic:
                            return new(TextExtensions.Echo(source.Read() ?? source.Fallback(), source));
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                });
        }
    }

    public static class Scenario
    {
        public static void VerifySurface(Surface surface, string? text, bool update)
        {
            var source = new Source { Text = text };
            var mapper = new TestMapper();
            string actual;
            switch (surface)
            {
                case Surface.Construct:
                    actual = Execute((ITypeMapper<Source, Destination<ConstructTag>>)mapper, source, update);
                    break;
                case Surface.Members:
                    actual = Execute((ITypeMapper<Source, Destination<MembersTag>>)mapper, source, update);
                    break;
                default:
                    actual = Execute((ITypeMapper<Source, Destination<ConvertTag>>)mapper, source, update);
                    break;
            }

            Assert(actual, string.IsNullOrEmpty(text) ? "fallback" : "abc1");
            Assert(source.Trace, text is null ? "RF" : text.Length == 0 ? "RAMF" : "RAM");
        }

        public static void VerifyExpression(ExpressionCase expression, string? text,
            bool update, string expected, string expectedTrace)
        {
            var source = new Source { Text = text, Case = expression };
            ITypeMapper<Source, Destination<ExpressionsTag>> mapper = new TestMapper();
            if (expression == ExpressionCase.CoalesceThrow && string.IsNullOrEmpty(text))
            {
                try
                {
                    Execute(mapper, source, update);
                    throw new InvalidOperationException("The null result must throw.");
                }
                catch (InvalidOperationException exception) when (exception.Message == "missing") { }
            }
            else
            {
                Assert(Execute(mapper, source, update), expected);
            }
            Assert(source.Trace, expectedTrace);
        }

        private static string Execute<T>(ITypeMapper<Source, Destination<T>> mapper,
            Source source, bool update) => update
                ? mapper.Update(source, null).Value
                : mapper.Create(source).Value;

        private static void Assert(string actual, string expected)
        {
            if (actual != expected)
                throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
        }
    }
}
