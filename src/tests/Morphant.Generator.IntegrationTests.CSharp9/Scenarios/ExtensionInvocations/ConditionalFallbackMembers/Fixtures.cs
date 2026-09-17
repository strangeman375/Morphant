#nullable enable
using System;
using Morphant;
namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.ConditionalFallbackMembers
{
    public static class Closer
    {
        public static int Describe(this string value) => 99;
        public static void Record(this string value) => throw new InvalidOperationException("Wrong extension.");
    }
    public sealed class Source
    {
        public static int Records;
        public int Reads;
        public string Text => GetText();
        public string this[int index] => GetText();
        public string GetText() { Reads++; return "abc"; }
    }
}
