#nullable enable
using System;
using Morphant;
using Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.Conversions.Calls;
namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.Conversions.Calls
{
    public static class Operations
    {
        public static int Increase(this ref int value, int amount) => value += amount;
        public static int Read(this in int value) => value;
        public static int Choose<T>(this T value, Func<T, int> convert) => convert(value);
        public static int Choose(this object value, Func<object, int> convert) => 99;
        public static int Receive(this int value, Number number) => value + number.Value;
    }
    public readonly struct Number
    {
        public Number(int value) => Value = value;
        public int Value { get; }
        public static int Conversions;
        public static implicit operator Number(int value) { Conversions++; return new Number(value + 1); }
    }
}
