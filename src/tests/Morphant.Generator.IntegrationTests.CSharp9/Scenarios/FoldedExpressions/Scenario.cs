#nullable enable
using System;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.FoldedExpressions
{
    public enum Surface { Construct, Resolve, Members, ConvertConstant }
    public sealed class ConstructTag { }
    public sealed class ResolveTag { }
    public sealed class MembersTag { }
    public sealed class ConstantTag { }
    public class BaseValue { }
    public sealed class DerivedValue : BaseValue { }
    public sealed class ConvertedValue
    {
        public ConvertedValue(string label) => Label = label;
        public string Label { get; }
        public static implicit operator ConvertedValue(BaseValue value) => new("implicit");
        public static explicit operator ConvertedValue(DerivedValue value) => new("explicit");
    }
    public sealed class Source
    {
        public int Value { get; init; } = 3;
        public double Other { get; init; } = 1.5;
        public DerivedValue UserValue { get; } = new();
    }
    public sealed class Destination<T>
    {
        public Destination(double arithmetic, double division, string switched, string conditional)
        {
            Arithmetic = arithmetic;
            Division = division;
            Switched = switched;
            Conditional = conditional;
        }
        public double Arithmetic { get; set; }
        public double Division { get; set; }
        public string Switched { get; set; }
        public string Conditional { get; set; }
        public string Converted { get; set; } = "implicit";
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        private static string Pick(int value) => "int";
        private static string Pick(double value) => "double";

        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination<ConstructTag>>()
                .Construct((source, context) => new(
                    context.Operation switch { MappingOperation.Create => source.Value + 1, _ => source.Value + 3 } * 2,
                    (context.Operation switch { MappingOperation.Create => source.Value, _ => source.Other }) / 2,
                    Pick(context.Operation switch { MappingOperation.Create => source.Value, _ => source.Other }),
                    Pick(context.Operation == MappingOperation.Create ? source.Value : source.Other)));
            builder.Map<Source, Destination<ResolveTag>>()
                .Resolve((source, previous, context) => new(
                    context.Operation switch { MappingOperation.Create => source.Value + 1, _ => source.Value + 3 } * 2,
                    (context.Operation switch { MappingOperation.Create => source.Value, _ => source.Other }) / 2,
                    Pick(context.Operation switch { MappingOperation.Create => source.Value, _ => source.Other }),
                    Pick(context.Operation == MappingOperation.Create ? source.Value : source.Other)));
            builder.Map<Source, Destination<MembersTag>>()
                .Members((source, previous, result, context) => new()
                {
                    Arithmetic = context.Operation switch { MappingOperation.Create => source.Value + 1, _ => source.Value + 3 } * 2,
                    Division = (context.Operation switch { MappingOperation.Create => source.Value, _ => source.Other }) / 2,
                    Switched = Pick(context.Operation switch { MappingOperation.Create => source.Value, _ => source.Other }),
                    Conditional = Pick(context.Operation == MappingOperation.Create ? source.Value : source.Other)
                });
            builder.Map<Source, Destination<ConstantTag>>()
                .Convert(source => new(
                    1 switch { 1 => source!.Value + 1, _ => source!.Value + 3 } * 2,
                    (1 switch { 1 => source!.Value, _ => source!.Other }) / 2,
                    Pick(1 switch { 1 => source!.Value, _ => source!.Other }),
                    Pick(true ? source!.Value : source!.Other))
                {
                    Converted = (true ? source!.UserValue : new ConvertedValue("unused")).Label
                });
        }
    }

    public static class Scenario
    {
        public static void Verify(Surface surface, bool update)
        {
            var mapper = new TestMapper();
            switch (surface)
            {
                case Surface.Construct:
                    Verify((ITypeMapper<Source, Destination<ConstructTag>>)mapper, update);
                    break;
                case Surface.Resolve:
                    Verify((ITypeMapper<Source, Destination<ResolveTag>>)mapper, update);
                    break;
                case Surface.Members:
                    Verify((ITypeMapper<Source, Destination<MembersTag>>)mapper, update);
                    break;
                default:
                    Verify((ITypeMapper<Source, Destination<ConstantTag>>)mapper, update, constant: true);
                    break;
            }
        }

        private static void Verify<T>(ITypeMapper<Source, Destination<T>> mapper, bool update, bool constant = false)
        {
            var result = update ? mapper.Update(new Source(), null) : mapper.Create(new Source());
            var expectedArithmetic = update && !constant ? 12 : 8;
            var expectedDivision = update && !constant ? 0.75 : 1.5;
            if (result.Arithmetic != expectedArithmetic || result.Division != expectedDivision ||
                result.Switched != "double" || result.Conditional != "double" || result.Converted != "implicit")
                throw new InvalidOperationException(
                    $"Folded expression changed: {result.Arithmetic}, {result.Division}, {result.Switched}, {result.Conditional}, {result.Converted}.");
        }
    }
}
