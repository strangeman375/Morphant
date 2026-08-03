using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.MappingPairTests;

[TestFixture]
internal sealed class MappingPairSourceEligibilityTests
{
    [Test]
    public async Task Accepts_every_supported_source_root_family()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Morphant;

namespace TestCase
{
    public enum Status { None }
    public class SourceClass { }
    public struct SourceStruct { }
    public record SourceRecord;
    public interface ISource { }
    public abstract class AbstractSource { }
    public sealed class Envelope<T> { }
    public sealed class SafeDestination { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<bool, SafeDestination>();
            builder.Map<string, SafeDestination>();
            builder.Map<Guid, SafeDestination>();
            builder.Map<Status, SafeDestination>();
            builder.Map<SourceClass, SafeDestination>();
            builder.Map<SourceStruct, SafeDestination>();
            builder.Map<SourceRecord, SafeDestination>();
            builder.Map<ISource, SafeDestination>();
            builder.Map<AbstractSource, SafeDestination>();
            builder.Map<int?, SafeDestination>();
            builder.Map<SourceStruct?, SafeDestination>();
            builder.Map<Envelope<Task<int>>, SafeDestination>();
            builder.Map<Envelope<(int Id, string Name)>, SafeDestination>();
            builder.Map<Envelope<int[]>, SafeDestination>();
            builder.Map<Envelope<Func<int>>, SafeDestination>();
            builder.Map<Envelope<Memory<int>>, SafeDestination>();
        }
    }
}
""";

        const string destination =
            "global::TestCase.SafeDestination";

        await MappingPairGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            "TestCase.TestMapper",
            hasUnifiablePairs: false,
            Pair("global::System.Boolean", destination),
            Pair("global::System.String", destination),
            Pair("global::System.Guid", destination),
            Pair("global::TestCase.Status", destination),
            Pair("global::TestCase.SourceClass", destination),
            Pair("global::TestCase.SourceStruct", destination),
            Pair("global::TestCase.SourceRecord", destination),
            Pair("global::TestCase.ISource", destination),
            Pair("global::TestCase.AbstractSource", destination),
            Pair(
                "global::System.Nullable<global::System.Int32>",
                destination),
            Pair(
                "global::System.Nullable<global::TestCase.SourceStruct>",
                destination),
            Pair(
                "global::TestCase.Envelope<global::System.Threading.Tasks.Task<global::System.Int32>>",
                destination),
            Pair(
                "global::TestCase.Envelope<global::System.ValueTuple<global::System.Int32, global::System.String>>",
                destination),
            Pair(
                "global::TestCase.Envelope<global::System.Int32[]>",
                destination),
            Pair(
                "global::TestCase.Envelope<global::System.Func<global::System.Int32>>",
                destination),
            Pair(
                "global::TestCase.Envelope<global::System.Memory<global::System.Int32>>",
                destination));
    }

    [Test]
    public async Task Rejects_every_deferred_source_root_family()
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
            builder.Map<(int Id, string Name), SafeDestination>();
            builder.Map<(int Id, string Name)?, SafeDestination>();
            builder.Map<Tuple<int, string>, SafeDestination>();
            builder.Map<ValueTuple, SafeDestination>();
            builder.Map<ITuple, SafeDestination>();
            builder.Map<CustomTuple, SafeDestination>();

            builder.Map<int[], SafeDestination>();
            builder.Map<int[,], SafeDestination>();
            builder.Map<IEnumerable, SafeDestination>();
            builder.Map<IEnumerable<int>, SafeDestination>();
            builder.Map<IEnumerator, SafeDestination>();
            builder.Map<IEnumerator<int>, SafeDestination>();
            builder.Map<List<int>, SafeDestination>();
            builder.Map<Dictionary<int, string>, SafeDestination>();
            builder.Map<CustomCollection, SafeDestination>();
            builder.Map<CustomEnumerator, SafeDestination>();
            builder.Map<IAsyncEnumerable<int>, SafeDestination>();
            builder.Map<IAsyncEnumerator<int>, SafeDestination>();
            builder.Map<CustomAsyncEnumerable, SafeDestination>();
            builder.Map<Memory<int>, SafeDestination>();
            builder.Map<ReadOnlyMemory<int>, SafeDestination>();
            builder.Map<ReadOnlySequence<int>, SafeDestination>();
            builder.Map<ReadOnlySequence<int>?, SafeDestination>();

            builder.Map<CustomDelegate, SafeDestination>();
            builder.Map<Func<int>, SafeDestination>();
            builder.Map<Delegate, SafeDestination>();
            builder.Map<MulticastDelegate, SafeDestination>();
            builder.Map<Expression, SafeDestination>();
            builder.Map<LambdaExpression, SafeDestination>();
            builder.Map<Expression<Func<int>>, SafeDestination>();

            builder.Map<Task, SafeDestination>();
            builder.Map<Task<int>, SafeDestination>();
            builder.Map<CustomTask, SafeDestination>();
            builder.Map<ValueTask, SafeDestination>();
            builder.Map<ValueTask<int>, SafeDestination>();
            builder.Map<ValueTask<int>?, SafeDestination>();
            builder.Map<Lazy<int>, SafeDestination>();
            builder.Map<CustomLazy, SafeDestination>();
            builder.Map<IObservable<int>, SafeDestination>();
            builder.Map<CustomObservable, SafeDestination>();

            builder.Map<string, SafeDestination>();
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
                "global::System.String",
                "global::TestCase.SafeDestination"));
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
    public class BaseSource { }
    public interface ISource { }
    public sealed class Envelope<T> { }
    public sealed class SafeDestination { }

    [MorphantMapper]
    public partial class TestMapper<TClass, TStruct, TNew, TBase, TInterface> : TypeMapper
        where TClass : class
        where TStruct : struct
        where TNew : new()
        where TBase : BaseSource
        where TInterface : ISource
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<TClass, SafeDestination>();
            builder.Map<TStruct, SafeDestination>();
            builder.Map<TStruct?, SafeDestination>();
            builder.Map<TNew, SafeDestination>();
            builder.Map<TBase, SafeDestination>();
            builder.Map<TInterface, SafeDestination>();
            builder.Map<Envelope<TClass>, SafeDestination>();
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
                "global::TestCase.Envelope<TClass>",
                "global::TestCase.SafeDestination"));
    }

    [Test]
    public async Task Rejects_file_local_source_types_at_any_depth()
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
            builder.Map<FileLocal, SafeDestination>();
            builder.Map<Envelope<FileLocal>, SafeDestination>();
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
                "global::TestCase.SafeDestination"));
    }

    [Test]
    public async Task Rejects_private_source_visible_only_to_its_nested_mapper()
    {
        // lang=c#
        const string source =
"""
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public sealed class SafeDestination { }

    public sealed class Container
    {
        private sealed class PrivateSource
        {
            [MorphantMapper]
            public partial class TestMapper : TypeMapper
            {
                protected override void Configure(MapperBuilder builder) =>
                    builder.Map<PrivateSource, SafeDestination>();
            }
        }
    }
}
""";

        await MappingPairGeneratorTest.RunAndAssert(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Container+PrivateSource+TestMapper",
            hasUnifiablePairs: false);
    }

    private static MappingPairExpectation Pair(
        string source,
        string destination)
    {
        return new MappingPairExpectation(
            source,
            destination,
            Structured: true,
            Members: false);
    }
}
