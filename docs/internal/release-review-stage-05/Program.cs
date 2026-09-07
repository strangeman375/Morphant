using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Stage05Audit
{
    internal static class Program
    {
        private static int Main()
        {
            Cases.Scenario.Run();
            Console.WriteLine(JsonSerializer.Serialize(Check.Results,
                new JsonSerializerOptions { WriteIndented = true }));
            return Check.Failed ? 1 : 0;
        }
    }

    internal static class Check
    {
        internal static readonly List<object> Results = new();
        internal static bool Failed { get; private set; }

        internal static void Equal<T>(string name, T expected, T actual)
        {
            bool passed = EqualityComparer<T>.Default.Equals(expected, actual);
            Failed |= !passed;
            Results.Add(new { name, expected, actual, passed });
        }

        internal static void Sequence(string name, string expected, IEnumerable<string> actual)
            => Equal(name, expected, string.Join(",", actual));

        internal static void Throws<T>(string name, Action action) where T : Exception
        {
            string actual = "no exception";
            try { action(); }
            catch (Exception error) { actual = error.GetType().FullName!; }
            Equal(name, typeof(T).FullName!, actual);
        }
    }
}
