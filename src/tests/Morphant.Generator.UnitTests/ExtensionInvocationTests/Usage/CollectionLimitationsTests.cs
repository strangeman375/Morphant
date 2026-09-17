using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.ExtensionInvocationTests.Usage;

[TestFixture]
internal sealed class CollectionLimitationsTests
{
    [TestCaseSource(nameof(Cases))]
    public void Rejects_unsupported_inline_initializers_and_accepts_ordinary_methods(
        string body, string[] compilerDiagnostics, LanguageVersion version, bool ordinaryMethod)
    {
        var callback = ordinaryMethod ? "source => CreateValue(source)" : "source => " + body;
        var method = ordinaryMethod ? "private static string CreateValue(int source)\n        " + body : "";
        var source = SourceTemplate.Replace("CALLBACK", callback, StringComparison.Ordinal)
            .Replace("METHOD", method, StringComparison.Ordinal);
        var result = GeneratorTestDriver.Run("ExtensionInvocation", source, version);
        var expectedDiagnostics = ordinaryMethod ? Array.Empty<string>() : compilerDiagnostics;
        var extension = version == LanguageVersion.CSharp9 ? MappingExtension9 : MappingExtension10;
        var mapper = ordinaryMethod
            ? version == LanguageVersion.CSharp9 ? MethodMapper9 : MethodMapper10
            : version == LanguageVersion.CSharp9 ? RejectedMapper9 : RejectedMapper10;
        var expected = new[]
        {
            (ExtensionHint, GeneratedSourceText.Normalize(extension)),
            (MapperHint, GeneratedSourceText.Normalize(mapper))
        };

        Assert.Multiple(() =>
        {
            Assert.That(result.CompilerWarningsAndErrors, Is.Empty);
            Assert.That(result.EffectiveDiagnostics.Select(diagnostic =>
                    (diagnostic.Id, diagnostic.Severity, diagnostic.GetMessage(),
                        GeneratorTestDriver.GetSourceText(diagnostic.Location))),
                Is.EqualTo(expectedDiagnostics.Select(code =>
                    ("MORPH0030", DiagnosticSeverity.Error,
                        "Convert for mapping 'int -> string' cannot be used by mapper 'ExtensionCases.Mapper': " +
                        "the generated mapping reports compiler diagnostic '" + code + "'.", callback))));
            Assert.That(result.EffectiveDiagnostics.SelectMany(diagnostic => diagnostic.AdditionalLocations), Is.Empty);
            Assert.That(result.GeneratedSources.Select(item => (item.HintName, item.SourceText.ToString())),
                Is.EquivalentTo(expected));
        });
    }

    private static IEnumerable<TestCaseData> Cases()
    {
        var cases = new[]
        {
            (Name: "InitAfterCollection", Body: InitAfterBody, Codes: new[] { "CS8852" }),
            // Relocating the only await also leaves the enclosing async lambda
            // without an await. Both compiler diagnostics become MORPH0030;
            // the final generated mapper must contain only valid typed stubs.
            (Name: "AwaitedMemberCollection", Body: AwaitedMemberBody, Codes: new[] { "CS1998", "CS4032" })
        };
        foreach (var item in cases)
        foreach (var version in new[] { LanguageVersion.CSharp9, LanguageVersion.CSharp10 })
        foreach (var ordinaryMethod in new[] { false, true })
            yield return new TestCaseData(item.Body, item.Codes, version, ordinaryMethod)
                .SetName(item.Name + "_" + version + (ordinaryMethod ? "_OrdinaryMethod" : "_RejectedInline"));
    }

    private const string ExtensionHint = "Morphant.Generated.MappingExtension.Mapper.Int32ToString__2f078999b56fc6fe4d4c3aded5d88f40.g.cs";
    private const string MapperHint = "Morphant.Generated.TypeMapper.Mapper__f76b1361cb552263930ff9b2ca8cd52b.g.cs";

    // lang=c#
    private const string SourceTemplate =
"""
#nullable enable
#pragma warning disable CS1591
using System;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Morphant;

namespace ExtensionCases
{
    public sealed class Bag : IEnumerable
    {
        public string Value = "";
        public IEnumerator GetEnumerator() => Array.Empty<int>().GetEnumerator();
        public void Add<T>(T value, [CallerMemberName] object? member = null) =>
            Value = value + ":" + member;
        public void Add(int value, object? member) =>
            throw new InvalidOperationException("Wrong overload.");
    }

    public sealed class Holder
    {
        public Bag Items { get; } = new();
        public int After { get; init; }
    }

    [MorphantMapper]
    public partial class Mapper : TypeMapper<Mapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<int, string>().Convert(CALLBACK);

        METHOD
    }
}
""";

    // lang=c#
    private const string InitAfterBody =
"""
{
            var holder = new Holder { Items = { source++ }, After = source++ };
            return holder.Items.Value + ":" + holder.After;
        }
""";

    // lang=c#
    private const string AwaitedMemberBody =
"""
{
            Func<Task<string>> read = async () => source > 0
                ? new Holder { Items = { await Task.FromResult(source) } }.Items.Value
                : "skip";
            return read().GetAwaiter().GetResult();
        }
""";

    // lang=c#
    private const string MappingExtension9 =
"""
// <auto-generated />
#nullable enable

namespace Morphant
{
    internal static partial class MorphantGeneratedMappingExtensions
    {
        /// <summary>
        /// Creates a destination through a callback only when none exists.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="construct">Callback returning the destination; null ends the mapping.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/construct-using.md"/>
        public static global::Morphant.MappingBuilder<global::ExtensionCases.Mapper, int, string> ConstructUsing(
            this global::Morphant.MappingBuilder<global::ExtensionCases.Mapper, int, string> builder,
            global::Morphant.Delegates.ConstructUsing<int, string> construct)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Creates a destination through a callback only when none exists.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="construct">Callback returning the destination; null ends the mapping.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/construct-using.md"/>
        public static global::Morphant.MappingBuilder<global::ExtensionCases.Mapper, int, string> ConstructUsing(
            this global::Morphant.MappingBuilder<global::ExtensionCases.Mapper, int, string> builder,
            global::Morphant.Delegates.ConstructUsing<int, global::Morphant.Context.MappingContext, string> construct)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Chooses the destination through a callback on Create and Update.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="resolve">Callback returning the destination; null ends the mapping.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/resolve-using.md"/>
        public static global::Morphant.MappingBuilder<global::ExtensionCases.Mapper, int, string> ResolveUsing(
            this global::Morphant.MappingBuilder<global::ExtensionCases.Mapper, int, string> builder,
            global::Morphant.Delegates.ResolveUsing<int, string, string> resolve)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Chooses the destination through a callback on Create and Update.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="resolve">Callback returning the destination; null ends the mapping.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/resolve-using.md"/>
        public static global::Morphant.MappingBuilder<global::ExtensionCases.Mapper, int, string> ResolveUsing(
            this global::Morphant.MappingBuilder<global::ExtensionCases.Mapper, int, string> builder,
            global::Morphant.Delegates.ResolveUsing<int, string, global::Morphant.Context.MappingContext, string> resolve)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Maps with ordinary C#, bypassing null policies and member rules.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="mapping">Lambda, method group, or delegate returning the final result.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/convert.md"/>
        public static global::Morphant.MappingBuilder<global::ExtensionCases.Mapper, int, string> Convert(
            this global::Morphant.MappingBuilder<global::ExtensionCases.Mapper, int, string> builder,
            global::Morphant.Delegates.Convert<int, string> mapping)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Maps with ordinary C#, bypassing null policies and member rules.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="mapping">Lambda, method group, or delegate returning the final result.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/convert.md"/>
        public static global::Morphant.MappingBuilder<global::ExtensionCases.Mapper, int, string> Convert(
            this global::Morphant.MappingBuilder<global::ExtensionCases.Mapper, int, string> builder,
            global::Morphant.Delegates.Convert<int, string, string> mapping)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Maps with ordinary C#, bypassing null policies and member rules.
        /// </summary>
        /// <param name="builder">The mapping to configure.</param>
        /// <param name="mapping">Lambda, method group, or delegate returning the final result.</param>
        /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/convert.md"/>
        public static global::Morphant.MappingBuilder<global::ExtensionCases.Mapper, int, string> Convert(
            this global::Morphant.MappingBuilder<global::ExtensionCases.Mapper, int, string> builder,
            global::Morphant.Delegates.Convert<int, string, global::Morphant.Context.MappingContext, string> mapping)
            => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();
    }
}
""";

    // lang=c#
    private const string RejectedMapper9 =
"""
// <auto-generated />
#nullable enable

namespace ExtensionCases
{
    public partial class Mapper :
        global::Morphant.ITypeMapper<int, string>
    {
        /// <inheritdoc/>
        protected override bool Supports(
            global::System.Type sourceType,
            global::System.Type destinationType) =>
                (sourceType == typeof(int) &&
                    destinationType == typeof(string)) ||
                base.Supports(sourceType, destinationType);

        /// <inheritdoc/>
        string global::Morphant.ITypeMapper<int, string>.Create(
            int source,
            global::Morphant.Context.MappingContext context)
            => throw new global::Morphant.Exceptions.MappingConfigurationException(
                global::Morphant.Context.MappingOperation.Create,
                typeof(int),
                typeof(string),
                "This mapping contains code that Morphant cannot generate.");

        /// <inheritdoc/>
        string global::Morphant.ITypeMapper<int, string>.Update(
            int source,
            string? destination,
            global::Morphant.Context.MappingContext context)
            => throw new global::Morphant.Exceptions.MappingConfigurationException(
                global::Morphant.Context.MappingOperation.Update,
                typeof(int),
                typeof(string),
                "This mapping contains code that Morphant cannot generate.");
    }
}
""";

    // lang=c#
    private const string MethodMapper9 =
"""
// <auto-generated />
#nullable enable

namespace ExtensionCases
{
    public partial class Mapper :
        global::Morphant.ITypeMapper<int, string>
    {
        /// <inheritdoc/>
        protected override bool Supports(
            global::System.Type sourceType,
            global::System.Type destinationType) =>
                (sourceType == typeof(int) &&
                    destinationType == typeof(string)) ||
                base.Supports(sourceType, destinationType);

        /// <inheritdoc/>
        string global::Morphant.ITypeMapper<int, string>.Create(
            int source,
            global::Morphant.Context.MappingContext context)
            => __ConvertDestination(source);

        /// <inheritdoc/>
        string global::Morphant.ITypeMapper<int, string>.Update(
            int source,
            string? destination,
            global::Morphant.Context.MappingContext context)
            => __ConvertDestination(source);

        private string __ConvertDestination(int source) => global::ExtensionCases.Mapper.CreateValue(source);
    }
}
""";

    // lang=c#
    private const string MappingExtension10 =
"""
// <auto-generated />
#nullable enable

namespace Morphant;

internal static partial class MorphantGeneratedMappingExtensions
{
    /// <summary>
    /// Creates a destination through a callback only when none exists.
    /// </summary>
    /// <param name="builder">The mapping to configure.</param>
    /// <param name="construct">Callback returning the destination; null ends the mapping.</param>
    /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/construct-using.md"/>
    public static global::Morphant.MappingBuilder<global::ExtensionCases.Mapper, int, string> ConstructUsing(
        this global::Morphant.MappingBuilder<global::ExtensionCases.Mapper, int, string> builder,
        global::Morphant.Delegates.ConstructUsing<int, string> construct)
        => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Creates a destination through a callback only when none exists.
    /// </summary>
    /// <param name="builder">The mapping to configure.</param>
    /// <param name="construct">Callback returning the destination; null ends the mapping.</param>
    /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/construct-using.md"/>
    public static global::Morphant.MappingBuilder<global::ExtensionCases.Mapper, int, string> ConstructUsing(
        this global::Morphant.MappingBuilder<global::ExtensionCases.Mapper, int, string> builder,
        global::Morphant.Delegates.ConstructUsing<int, global::Morphant.Context.MappingContext, string> construct)
        => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Chooses the destination through a callback on Create and Update.
    /// </summary>
    /// <param name="builder">The mapping to configure.</param>
    /// <param name="resolve">Callback returning the destination; null ends the mapping.</param>
    /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/resolve-using.md"/>
    public static global::Morphant.MappingBuilder<global::ExtensionCases.Mapper, int, string> ResolveUsing(
        this global::Morphant.MappingBuilder<global::ExtensionCases.Mapper, int, string> builder,
        global::Morphant.Delegates.ResolveUsing<int, string, string> resolve)
        => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Chooses the destination through a callback on Create and Update.
    /// </summary>
    /// <param name="builder">The mapping to configure.</param>
    /// <param name="resolve">Callback returning the destination; null ends the mapping.</param>
    /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/resolve-using.md"/>
    public static global::Morphant.MappingBuilder<global::ExtensionCases.Mapper, int, string> ResolveUsing(
        this global::Morphant.MappingBuilder<global::ExtensionCases.Mapper, int, string> builder,
        global::Morphant.Delegates.ResolveUsing<int, string, global::Morphant.Context.MappingContext, string> resolve)
        => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Maps with ordinary C#, bypassing null policies and member rules.
    /// </summary>
    /// <param name="builder">The mapping to configure.</param>
    /// <param name="mapping">Lambda, method group, or delegate returning the final result.</param>
    /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/convert.md"/>
    public static global::Morphant.MappingBuilder<global::ExtensionCases.Mapper, int, string> Convert(
        this global::Morphant.MappingBuilder<global::ExtensionCases.Mapper, int, string> builder,
        global::Morphant.Delegates.Convert<int, string> mapping)
        => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Maps with ordinary C#, bypassing null policies and member rules.
    /// </summary>
    /// <param name="builder">The mapping to configure.</param>
    /// <param name="mapping">Lambda, method group, or delegate returning the final result.</param>
    /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/convert.md"/>
    public static global::Morphant.MappingBuilder<global::ExtensionCases.Mapper, int, string> Convert(
        this global::Morphant.MappingBuilder<global::ExtensionCases.Mapper, int, string> builder,
        global::Morphant.Delegates.Convert<int, string, string> mapping)
        => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Maps with ordinary C#, bypassing null policies and member rules.
    /// </summary>
    /// <param name="builder">The mapping to configure.</param>
    /// <param name="mapping">Lambda, method group, or delegate returning the final result.</param>
    /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/convert.md"/>
    public static global::Morphant.MappingBuilder<global::ExtensionCases.Mapper, int, string> Convert(
        this global::Morphant.MappingBuilder<global::ExtensionCases.Mapper, int, string> builder,
        global::Morphant.Delegates.Convert<int, string, global::Morphant.Context.MappingContext, string> mapping)
        => throw new global::Morphant.Exceptions.RuntimeInvocationNotSupportedException();
}
""";

    // lang=c#
    private const string RejectedMapper10 =
"""
// <auto-generated />
#nullable enable

namespace ExtensionCases;

public partial class Mapper :
    global::Morphant.ITypeMapper<int, string>
{
    /// <inheritdoc/>
    protected override bool Supports(
        global::System.Type sourceType,
        global::System.Type destinationType) =>
            (sourceType == typeof(int) &&
                destinationType == typeof(string)) ||
            base.Supports(sourceType, destinationType);

    /// <inheritdoc/>
    string global::Morphant.ITypeMapper<int, string>.Create(
        int source,
        global::Morphant.Context.MappingContext context)
        => throw new global::Morphant.Exceptions.MappingConfigurationException(
            global::Morphant.Context.MappingOperation.Create,
            typeof(int),
            typeof(string),
            "This mapping contains code that Morphant cannot generate.");

    /// <inheritdoc/>
    string global::Morphant.ITypeMapper<int, string>.Update(
        int source,
        string? destination,
        global::Morphant.Context.MappingContext context)
        => throw new global::Morphant.Exceptions.MappingConfigurationException(
            global::Morphant.Context.MappingOperation.Update,
            typeof(int),
            typeof(string),
            "This mapping contains code that Morphant cannot generate.");
}
""";

    // lang=c#
    private const string MethodMapper10 =
"""
// <auto-generated />
#nullable enable

namespace ExtensionCases;

public partial class Mapper :
    global::Morphant.ITypeMapper<int, string>
{
    /// <inheritdoc/>
    protected override bool Supports(
        global::System.Type sourceType,
        global::System.Type destinationType) =>
            (sourceType == typeof(int) &&
                destinationType == typeof(string)) ||
            base.Supports(sourceType, destinationType);

    /// <inheritdoc/>
    string global::Morphant.ITypeMapper<int, string>.Create(
        int source,
        global::Morphant.Context.MappingContext context)
        => __ConvertDestination(source);

    /// <inheritdoc/>
    string global::Morphant.ITypeMapper<int, string>.Update(
        int source,
        string? destination,
        global::Morphant.Context.MappingContext context)
        => __ConvertDestination(source);

    private string __ConvertDestination(int source) => global::ExtensionCases.Mapper.CreateValue(source);
}
""";
}
