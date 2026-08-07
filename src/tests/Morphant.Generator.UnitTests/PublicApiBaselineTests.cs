using System.Reflection;

namespace Morphant.Generator.UnitTests;

[TestFixture]
internal sealed class PublicApiBaselineTests
{
    [Test]
    public void Runtime_public_API_matches_the_core_v0_baseline()
    {
        // lang=text
        const string expected =
"""
T Morphant.ConstructorSelection
  V Default, Explicit, Parameterless, Single, Unambiguous, Greediest, Largest
T Morphant.Context.MappingContext
  P Morphant.Context.MappingOperation Operation { get; }
  P Morphant.IMapper Mapper { get; }
T Morphant.Context.MappingOperation
  V Create, Update
T Morphant.Delegates.Construct<TSource, TResult>
  C .ctor(System.Object, System.IntPtr)
  M System.IAsyncResult BeginInvoke(TSource, System.AsyncCallback, System.Object)
  M TResult EndInvoke(System.IAsyncResult)
  M TResult Invoke(TSource)
T Morphant.Delegates.Construct<TSource, TPrevious, TResult>
  C .ctor(System.Object, System.IntPtr)
  M System.IAsyncResult BeginInvoke(TSource, Morphant.Option<TPrevious>, System.AsyncCallback, System.Object)
  M TResult EndInvoke(System.IAsyncResult)
  M TResult Invoke(TSource, Morphant.Option<TPrevious>)
T Morphant.Delegates.Convert<TSource, TPrevious, TResult>
  C .ctor(System.Object, System.IntPtr)
  M System.IAsyncResult BeginInvoke(TSource, Morphant.Option<TPrevious>, Morphant.Context.MappingContext, System.AsyncCallback, System.Object)
  M TResult EndInvoke(System.IAsyncResult)
  M TResult Invoke(TSource, Morphant.Option<TPrevious>, Morphant.Context.MappingContext)
T Morphant.Delegates.Members<TSource, TPrevious, TMembers>
  C .ctor(System.Object, System.IntPtr)
  M System.IAsyncResult BeginInvoke(TSource, Morphant.Option<TPrevious>, System.AsyncCallback, System.Object)
  M TMembers EndInvoke(System.IAsyncResult)
  M TMembers Invoke(TSource, Morphant.Option<TPrevious>)
T Morphant.Delegates.Members<TSource, TPrevious, TResult, TMembers>
  C .ctor(System.Object, System.IntPtr)
  M System.IAsyncResult BeginInvoke(TSource, Morphant.Option<TPrevious>, TResult, System.AsyncCallback, System.Object)
  M TMembers EndInvoke(System.IAsyncResult)
  M TMembers Invoke(TSource, Morphant.Option<TPrevious>, TResult)
T Morphant.Exceptions.RuntimeInvocationNotSupportedException
  C .ctor()
T Morphant.IMapper
  M TDestination Map<TSource, TDestination>(TSource)
  M TDestination Map<TSource, TDestination>(TSource, TDestination)
T Morphant.ITypeMapper<TSource, TDestination>
  M TDestination Create(TSource, Morphant.Context.MappingContext)
  M TDestination Update(TSource, TDestination, Morphant.Context.MappingContext)
T Morphant.Mapper
  C .ctor(System.IServiceProvider)
  M TDestination Map<TSource, TDestination>(TSource)
  M TDestination Map<TSource, TDestination>(TSource, TDestination)
T Morphant.MapperBuilder
  M Morphant.MapperBuilder MappingMode(Morphant.MappingMode)
  M Morphant.MapperBuilder<TSource, TDestination> Map<TSource, TDestination>(Morphant.MappingMode)
T Morphant.MapperBuilderBase<T>
  M T ConstructorSelection(Morphant.ConstructorSelection)
  M T MemberSelection(Morphant.MemberSelection)
  M T NullDestinationHandling(Morphant.NullDestinationHandling)
  M T NullSourceHandling(Morphant.NullSourceHandling)
  M T UnmappedMemberValidation(Morphant.UnmappedMemberValidation)
T Morphant.MapperBuilder<TSource, TDestination>
  M Morphant.MapperBuilder<TSource, TDestination> IncludeBase<TBaseSource, TBaseDestination>()
T Morphant.MappingMode
  V Default, Create, Update, CreateAndUpdate
T Morphant.Markers.AutoMarker
T Morphant.Markers.AutoMarker<T>
T Morphant.Markers.ByConventionMarker
T Morphant.Markers.ConstructorMarker
T Morphant.Markers.IByFactoryMarker<TDestination>
T Morphant.Markers.IgnoreMarker
T Morphant.Markers.IgnoreMarker<T>
T Morphant.Markers.MapMarker
T Morphant.Markers.MapMarker<T>
T Morphant.Markers.MemberMarker
T Morphant.MemberSelection
  V Default, Auto, Explicit
T Morphant.Members.ConstructorParameter<T>
T Morphant.Members.Member<T>
T Morphant.MorphantMapperAttribute
  C .ctor()
T Morphant.NullDestinationHandling
  V Default, Create, Throw
T Morphant.NullSourceHandling
  V Default, ReturnNull, ReturnDestination, Throw
T Morphant.Option<T>
  P Morphant.Option<T> None { get; }
  P System.Boolean HasValue { get; }
  P T Value { get; }
  M Morphant.Option<T> Some(T)
  M System.Boolean TryGetValue(T&)
T Morphant.TypeMapper
  C .ctor()
  M Morphant.Markers.AutoMarker Auto()
  M Morphant.Markers.AutoMarker<T> Auto<T>()
  M Morphant.Markers.ByConventionMarker ByConvention()
  M Morphant.Markers.IByFactoryMarker<TDestination> ByFactory<TDestination>(System.Func<TDestination>)
  M Morphant.Markers.IgnoreMarker Ignore()
  M Morphant.Markers.IgnoreMarker<T> Ignore<T>()
  M Morphant.Markers.MapMarker Create(System.Object)
  M Morphant.Markers.MapMarker Map()
  M Morphant.Markers.MapMarker Map(System.Object)
  M Morphant.Markers.MapMarker Update(System.Object, System.Object)
  M Morphant.Markers.MapMarker<T> Create<T>(System.Object)
  M Morphant.Markers.MapMarker<T> Map<T>()
  M Morphant.Markers.MapMarker<T> Map<T>(System.Object)
  M Morphant.Markers.MapMarker<T> Update<T>(System.Object, System.Object)
  M System.Void Configure(Morphant.MapperBuilder)
T Morphant.UnmappedMemberValidation
  V Default, None, Source, Destination, Strict
""";

        var actual = DescribePublicApi(typeof(Mapper).Assembly);

        Assert.That(actual, Is.EqualTo(expected));
    }

    private static string DescribePublicApi(Assembly assembly)
    {
        var lines = new List<string>();

        foreach (var type in assembly.GetExportedTypes()
                     .OrderBy(
                        static type => type.FullName,
                        StringComparer.Ordinal))
        {
            lines.Add("T " + FormatType(type));

            if (type.IsEnum)
            {
                lines.Add("  V " + string.Join(", ", Enum.GetNames(type)));
                continue;
            }

            lines.AddRange(type.GetConstructors(
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.Instance |
                    BindingFlags.DeclaredOnly)
                .Where(static constructor => IsApiVisible(constructor))
                .Select(static constructor =>
                    "  C " + FormatConstructor(constructor))
                .OrderBy(static line => line, StringComparer.Ordinal));

            lines.AddRange(type.GetFields(
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.Instance |
                    BindingFlags.Static |
                    BindingFlags.DeclaredOnly)
                .Where(static field =>
                    !field.IsSpecialName && IsApiVisible(field))
                .Select(static field => "  F " + FormatField(field))
                .OrderBy(static line => line, StringComparer.Ordinal));

            lines.AddRange(type.GetProperties(
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.Instance |
                    BindingFlags.Static |
                    BindingFlags.DeclaredOnly)
                .Where(static property =>
                    IsApiVisible(property.GetMethod) ||
                    IsApiVisible(property.SetMethod))
                .Select(static property =>
                    "  P " + FormatProperty(property))
                .OrderBy(static line => line, StringComparer.Ordinal));

            lines.AddRange(type.GetMethods(
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.Instance |
                    BindingFlags.Static |
                    BindingFlags.DeclaredOnly)
                .Where(static method =>
                    !method.IsSpecialName && IsApiVisible(method))
                .Select(static method => "  M " + FormatMethod(method))
                .OrderBy(static line => line, StringComparer.Ordinal));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatConstructor(ConstructorInfo constructor) =>
        ".ctor(" + string.Join(", ", constructor.GetParameters()
            .Select(static parameter =>
                FormatType(parameter.ParameterType))) + ")";

    private static string FormatField(FieldInfo field) =>
        FormatType(field.FieldType) + " " + field.Name;

    private static string FormatProperty(PropertyInfo property) =>
        FormatType(property.PropertyType) + " " + property.Name +
        " { " +
        (property.GetMethod?.IsPublic == true ? "get; " : string.Empty) +
        (property.SetMethod?.IsPublic == true ? "set; " : string.Empty) +
        "}";

    private static string FormatMethod(MethodInfo method) =>
        FormatType(method.ReturnType) + " " + method.Name +
        (method.IsGenericMethodDefinition
            ? "<" + string.Join(", ", method.GetGenericArguments()
                .Select(static argument => argument.Name)) + ">"
            : string.Empty) +
        "(" + string.Join(", ", method.GetParameters()
            .Select(static parameter =>
                FormatType(parameter.ParameterType))) + ")";

    private static string FormatType(Type type)
    {
        if (type.IsByRef)
        {
            return FormatType(type.GetElementType()!) + "&";
        }

        if (type.IsArray)
        {
            return FormatType(type.GetElementType()!) + "[]";
        }

        if (type.IsGenericParameter)
        {
            return type.Name;
        }

        if (!type.IsGenericType)
        {
            return type.FullName ?? type.Name;
        }

        var definitionName = type.GetGenericTypeDefinition().FullName!;
        definitionName = definitionName[
            ..definitionName.IndexOf((char)96)];

        return definitionName + "<" + string.Join(", ",
            type.GetGenericArguments().Select(FormatType)) + ">";
    }

    private static bool IsApiVisible(MethodBase? method) =>
        method is not null &&
        (method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly);

    private static bool IsApiVisible(FieldInfo field) =>
        field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly;
}
