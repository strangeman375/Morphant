using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Stage04Audit
{
    internal static class Program
    {
        private static int Main()
        {
            Cases.Scenario.Run();
            Console.WriteLine(JsonSerializer.Serialize(Check.Results, new JsonSerializerOptions { WriteIndented = true }));
            return Check.Failed ? 1 : 0;
        }
    }

    internal static class Check
    {
        internal static readonly List<object> Results = new List<object>();
        internal static bool Failed { get; private set; }

        internal static void Equal<T>(string name, T expected, T actual)
        {
            var passed = EqualityComparer<T>.Default.Equals(expected, actual);
            Failed |= !passed;
            Results.Add(new { name, expected, actual, passed });
        }

        internal static void Throws<T>(string name, Action action) where T : Exception
        {
            string actual = "no exception";
            try { action(); }
            catch (Exception error) { actual = error.GetType().FullName!; }
            Equal(name, typeof(T).FullName!, actual);
        }
    }
}
