using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.MappingPairTests;

[TestFixture]
internal sealed class MappingPairDestinationEligibilityTests
{
    [Test]
    public async Task Accepts_every_supported_destination_root_family()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using System.Threading.Tasks;
using Morphant;

namespace TestCase
{
    public sealed class SafeSource { }
    public enum Status { None }
    public class DestinationClass { }
    public struct DestinationStruct { }
    public record DestinationRecord;
    public interface IDestination { }
    public abstract class AbstractDestination { }
    public sealed class Envelope<T> { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<SafeSource, bool>();
            builder.Map<SafeSource, string>();
            builder.Map<SafeSource, Guid>();
            builder.Map<SafeSource, DateTime>();
            builder.Map<SafeSource, DateTimeOffset>();
            builder.Map<SafeSource, TimeSpan>();
            builder.Map<SafeSource, Status>();
            builder.Map<SafeSource, DestinationClass>();
            builder.Map<SafeSource, DestinationStruct>();
            builder.Map<SafeSource, DestinationRecord>();
            builder.Map<SafeSource, IDestination>();
            builder.Map<SafeSource, AbstractDestination>();
            builder.Map<SafeSource, int?>();
            builder.Map<SafeSource, DestinationStruct?>();
            builder.Map<SafeSource, Envelope<Task<int>>>();
            builder.Map<SafeSource, Envelope<(int Id, string Name)>>();
            builder.Map<SafeSource, Envelope<int[]>>();
            builder.Map<SafeSource, Envelope<Func<int>>>();
            builder.Map<SafeSource, Envelope<Memory<int>>>();
        }
    }
}
""";

        const string safeSource = "global::TestCase.SafeSource";

        await MappingPairGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            "TestCase.TestMapper",
            hasUnifiablePairs: false,
            Pair(safeSource, "global::System.Boolean", false),
            Pair(safeSource, "global::System.String", false),
            Pair(safeSource, "global::System.Guid", false),
            Pair(safeSource, "global::System.DateTime", false),
            Pair(safeSource, "global::System.DateTimeOffset", false),
            Pair(safeSource, "global::System.TimeSpan", false),
            Pair(safeSource, "global::TestCase.Status", false),
            Pair(safeSource, "global::TestCase.DestinationClass", true),
            Pair(safeSource, "global::TestCase.DestinationStruct", true),
            Pair(safeSource, "global::TestCase.DestinationRecord", true),
            Pair(safeSource, "global::TestCase.IDestination", false),
            Pair(safeSource, "global::TestCase.AbstractDestination", false),
            Pair(
                safeSource,
                "global::System.Nullable<global::System.Int32>",
                false),
            Pair(
                safeSource,
                "global::System.Nullable<global::TestCase.DestinationStruct>",
                true),
            Pair(
                safeSource,
                "global::TestCase.Envelope<global::System.Threading.Tasks.Task<global::System.Int32>>",
                true),
            Pair(
                safeSource,
                "global::TestCase.Envelope<global::System.ValueTuple<global::System.Int32, global::System.String>>",
                true),
            Pair(
                safeSource,
                "global::TestCase.Envelope<global::System.Int32[]>",
                true),
            Pair(
                safeSource,
                "global::TestCase.Envelope<global::System.Func<global::System.Int32>>",
                true),
            Pair(
                safeSource,
                "global::TestCase.Envelope<global::System.Memory<global::System.Int32>>",
                true));
    }

    [Test]
    public async Task Rejects_every_deferred_destination_root_family()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Morphant;

namespace TestCase
{
    public sealed class SafeSource { }
    public sealed class SafeDestination { }

    public sealed class CustomTuple : ITuple
    {
        public int Length => 0;
        public object? this[int index] => throw new IndexOutOfRangeException();
    }

    public sealed class CustomCollection : IEnumerable<int>
    {
        public IEnumerator<int> GetEnumerator() =>
            throw new NotImplementedException();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public sealed class CustomEnumerator : IEnumerator<int>
    {
        public int Current => 0;
        object IEnumerator.Current => Current;
        public bool MoveNext() => false;
        public void Reset() { }
        public void Dispose() { }
    }

    public sealed class CustomAsyncEnumerable : IAsyncEnumerable<int>
    {
        public IAsyncEnumerator<int> GetAsyncEnumerator(
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }

    public sealed class CustomTask : Task
    {
        public CustomTask() : base(() => { }) { }
    }

    public sealed class CustomLazy : Lazy<int>
    {
        public CustomLazy() : base(() => 0) { }
    }

    public sealed class CustomObservable : IObservable<int>
    {
        public IDisposable Subscribe(IObserver<int> observer) =>
            throw new NotImplementedException();
    }

    public delegate void CustomDelegate();

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<SafeSource, (int Id, string Name)>();
            builder.Map<SafeSource, (int Id, string Name)?>();
            builder.Map<SafeSource, Tuple<int, string>>();
            builder.Map<SafeSource, ValueTuple>();
            builder.Map<SafeSource, ITuple>();
            builder.Map<SafeSource, CustomTuple>();

            builder.Map<SafeSource, int[]>();
            builder.Map<SafeSource, int[,]>();
            builder.Map<SafeSource, IEnumerable>();
            builder.Map<SafeSource, IEnumerable<int>>();
            builder.Map<SafeSource, IEnumerator>();
            builder.Map<SafeSource, IEnumerator<int>>();
            builder.Map<SafeSource, List<int>>();
            builder.Map<SafeSource, Dictionary<int, string>>();
            builder.Map<SafeSource, CustomCollection>();
            builder.Map<SafeSource, CustomEnumerator>();
            builder.Map<SafeSource, IAsyncEnumerable<int>>();
            builder.Map<SafeSource, IAsyncEnumerator<int>>();
            builder.Map<SafeSource, CustomAsyncEnumerable>();
            builder.Map<SafeSource, Memory<int>>();
            builder.Map<SafeSource, ReadOnlyMemory<int>>();
            builder.Map<SafeSource, ReadOnlySequence<int>>();
            builder.Map<SafeSource, ReadOnlySequence<int>?>();

            builder.Map<SafeSource, CustomDelegate>();
            builder.Map<SafeSource, Func<int>>();
            builder.Map<SafeSource, Delegate>();
            builder.Map<SafeSource, MulticastDelegate>();
            builder.Map<SafeSource, Expression>();
            builder.Map<SafeSource, LambdaExpression>();
            builder.Map<SafeSource, Expression<Func<int>>>();

            builder.Map<SafeSource, Task>();
            builder.Map<SafeSource, Task<int>>();
            builder.Map<SafeSource, CustomTask>();
            builder.Map<SafeSource, ValueTask>();
            builder.Map<SafeSource, ValueTask<int>>();
            builder.Map<SafeSource, ValueTask<int>?>();
            builder.Map<SafeSource, Lazy<int>>();
            builder.Map<SafeSource, CustomLazy>();
            builder.Map<SafeSource, IObservable<int>>();
            builder.Map<SafeSource, CustomObservable>();

            builder.Map<SafeSource, SafeDestination>();
        }
    }
}
""";

        await MappingPairGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            "TestCase.TestMapper",
            hasUnifiablePairs: false,
            Pair(
                "global::TestCase.SafeSource",
                "global::TestCase.SafeDestination",
                structured: true));
    }

    [Test]
    public async Task Rejects_bare_type_parameters_but_accepts_them_inside_nominal_roots()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public class BaseDestination { }
    public interface IDestination { }
    public sealed class Envelope<T> { }
    public sealed class SafeSource { }

    [MorphantMapper]
    public partial class TestMapper<TClass, TStruct, TNew, TBase, TInterface> : TypeMapper
        where TClass : class
        where TStruct : struct
        where TNew : new()
        where TBase : BaseDestination
        where TInterface : IDestination
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<SafeSource, TClass>();
            builder.Map<SafeSource, TStruct>();
            builder.Map<SafeSource, TStruct?>();
            builder.Map<SafeSource, TNew>();
            builder.Map<SafeSource, TBase>();
            builder.Map<SafeSource, TInterface>();
            builder.Map<SafeSource, Envelope<TClass>>();
        }
    }
}
""";

        await MappingPairGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            "TestCase.TestMapper`5",
            hasUnifiablePairs: false,
            Pair(
                "global::TestCase.SafeSource",
                "global::TestCase.Envelope<TClass>",
                structured: true));
    }

    [Test]
    public async Task Rejects_file_local_destination_types_at_any_depth()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    file sealed class FileLocal { }
    public sealed class Envelope<T> { }
    public sealed class SafeSource { }
    public sealed class SafeDestination { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<SafeSource, FileLocal>();
            builder.Map<SafeSource, Envelope<FileLocal>>();
            builder.Map<SafeSource, SafeDestination>();
        }
    }
}
""";

        await MappingPairGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp11,
            source,
            "TestCase.TestMapper",
            hasUnifiablePairs: false,
            Pair(
                "global::TestCase.SafeSource",
                "global::TestCase.SafeDestination",
                structured: true));
    }

    [Test]
    public async Task Rejects_private_destination_visible_only_to_its_nested_mapper()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class SafeSource { }

    public sealed class Container
    {
        private sealed class PrivateDestination
        {
            [MorphantMapper]
            public partial class TestMapper : TypeMapper
            {
                protected override void Configure(MapperBuilder builder) =>
                    builder.Map<SafeSource, PrivateDestination>();
            }
        }
    }
}
""";

        await MappingPairGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Container+PrivateDestination+TestMapper",
            hasUnifiablePairs: false);
    }

    private static MappingPairExpectation Pair(
        string source,
        string destination,
        bool structured)
    {
        return new MappingPairExpectation(
            source,
            destination,
            structured,
            Members: false);
    }
}
