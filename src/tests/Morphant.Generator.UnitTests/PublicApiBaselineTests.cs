using System.Reflection;
using System.Runtime.CompilerServices;
using Morphant.Exceptions;
using Morphant.Markers;
using Morphant.Members;

namespace Morphant.Generator.UnitTests;

[TestFixture]
internal sealed class PublicApiBaselineTests
{
    [Test]
    public void Generator_assembly_does_not_expose_public_API()
    {
        Assert.That(
            typeof(MorphantGenerator).Assembly.GetExportedTypes(),
            Is.Empty);
    }

    [Test]
    public void Runtime_public_API_preserves_modifiers_inheritance_and_metadata()
    {
        var sealedTypes = new[]
        {
            typeof(Mapper),
            typeof(MorphantMapperAttribute),
            typeof(ByConventionMarker),
            typeof(AutoMarker),
            typeof(AutoMarker<>),
            typeof(IgnoreMarker),
            typeof(IgnoreMarker<>),
            typeof(MapMarker<>),
            typeof(ValueMarker<>),
            typeof(Member<>),
            typeof(ConstructorParameter<>),
            typeof(MapperBuilder),
            typeof(MapperBuilder<,>),
            typeof(AmbiguousMappingException),
            typeof(AmbiguousPolymorphicMappingException),
            typeof(InvalidMappingContextException),
            typeof(InvalidMappingRegistrationException),
            typeof(MappingConfigurationException),
            typeof(MappingNotFoundException),
            typeof(MappingOperationNotSupportedException),
            typeof(MappingScopeCompletedException),
            typeof(NestedDestinationTypeMismatchException),
            typeof(NullDestinationException),
            typeof(NullSourceException),
            typeof(OptionValueMissingException),
            typeof(PolymorphicDestinationTypeMismatchException),
            typeof(RuntimeInvocationNotSupportedException),
            typeof(UnmatchedMappingSwitchException),
            typeof(UnmatchedPolymorphicMappingException)
        };
        var abstractInfrastructureTypes = new[]
        {
            typeof(ConstructorMarker),
            typeof(MemberMarker),
            typeof(MapMarker),
            typeof(global::Morphant.Context.MappingContextMarker),
            typeof(MapperBuilderBase<>),
            typeof(TypeMapper),
            typeof(MorphantException),
            typeof(MappingException)
        };
        var reservedConstructors = new[]
        {
            typeof(MorphantException),
            typeof(MappingException)
        };
        var typeMapperParameters = typeof(ITypeMapper<,>)
            .GetGenericArguments();
        var extensionMethods = typeof(TypeMapperExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static);
        var mapMethod = typeof(MapperBuilder)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Single(method => method.Name == nameof(MapperBuilder.Map));
        var supportsMethod = typeof(TypeMapper).GetMethod(
            "Supports",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        var forDerivedMethod = typeof(MapperBuilder<,>)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Single(method => method.Name == "ForDerived");
        var mapperAttributeUsage =
            typeof(MorphantMapperAttribute)
                .GetCustomAttribute<AttributeUsageAttribute>()!;

        Assert.Multiple(() =>
        {
            Assert.That(
                sealedTypes.All(static type => type.IsSealed),
                Is.True);
            Assert.That(
                abstractInfrastructureTypes.All(static type => type.IsAbstract),
                Is.True);
            Assert.That(typeof(TypeMapperExtensions).IsAbstract, Is.True);
            Assert.That(typeof(TypeMapperExtensions).IsSealed, Is.True);
            Assert.That(
                mapperAttributeUsage.ValidOn,
                Is.EqualTo(AttributeTargets.Class));
            Assert.That(mapperAttributeUsage.AllowMultiple, Is.False);
            Assert.That(mapperAttributeUsage.Inherited, Is.False);
            Assert.That(
                reservedConstructors.Select(type => type.GetConstructors(
                        BindingFlags.NonPublic |
                        BindingFlags.Instance |
                        BindingFlags.DeclaredOnly)
                    .Single())
                    .All(static constructor =>
                        constructor.IsFamilyAndAssembly),
                Is.True);
            Assert.That(
                typeMapperParameters[0].GenericParameterAttributes &
                GenericParameterAttributes.VarianceMask,
                Is.EqualTo(GenericParameterAttributes.Contravariant));
            Assert.That(
                typeMapperParameters[1].GenericParameterAttributes &
                GenericParameterAttributes.VarianceMask,
                Is.EqualTo(GenericParameterAttributes.None));
            Assert.That(
                typeof(TypeMapperExtensions).IsDefined(
                    typeof(ExtensionAttribute),
                    inherit: false),
                Is.True);
            Assert.That(
                extensionMethods,
                Has.Length.EqualTo(2));
            Assert.That(
                extensionMethods.All(method => method.IsDefined(
                    typeof(ExtensionAttribute),
                    inherit: false)),
                Is.True);
            Assert.That(
                extensionMethods.Single(method =>
                        method.Name == nameof(TypeMapperExtensions.Create))
                    .GetParameters()
                    .Select(static parameter => parameter.Name),
                Is.EqualTo(new[] { "mapper", "source" }));
            Assert.That(
                extensionMethods.Single(method =>
                        method.Name == nameof(TypeMapperExtensions.Update))
                    .GetParameters()
                    .Select(static parameter => parameter.Name),
                Is.EqualTo(new[] { "mapper", "source", "destination" }));
            Assert.That(
                mapMethod.GetParameters().Single().HasDefaultValue,
                Is.True);
            Assert.That(
                mapMethod.GetParameters().Single().DefaultValue,
                Is.EqualTo(MappingMode.Default));
            Assert.That(supportsMethod.IsPublic, Is.False);
            Assert.That(supportsMethod.IsFamilyOrAssembly, Is.True);
            Assert.That(supportsMethod.IsVirtual, Is.True);
            Assert.That(
                supportsMethod.GetParameters()
                    .Select(static parameter => parameter.Name),
                Is.EqualTo(new[] { "sourceType", "destinationType" }));
            Assert.That(
                forDerivedMethod.GetGenericArguments()
                    .Select(parameter => parameter
                        .GetGenericParameterConstraints()
                        .Single()),
                Is.EqualTo(typeof(MapperBuilder<,>)
                    .GetGenericArguments()));
            Assert.That(
                GetImplicitOperatorCount(typeof(AutoMarker<>)),
                Is.EqualTo(1));
            Assert.That(
                GetImplicitOperatorCount(typeof(IgnoreMarker<>)),
                Is.EqualTo(1));
            Assert.That(
                GetImplicitOperatorCount(typeof(MapMarker<>)),
                Is.EqualTo(1));
            Assert.That(
                GetImplicitOperatorCount(typeof(ValueMarker<>)),
                Is.Zero);
            Assert.That(
                GetImplicitOperatorCount(typeof(Member<>)),
                Is.EqualTo(7));
            Assert.That(
                GetImplicitOperatorCount(typeof(ConstructorParameter<>)),
                Is.EqualTo(7));
            Assert.That(
                GetImplicitOperatorSourceTypes(typeof(Member<>)),
                Is.EqualTo(new[]
                {
                    "Morphant.Markers.AutoMarker",
                    "Morphant.Markers.AutoMarker<T>",
                    "Morphant.Markers.IgnoreMarker",
                    "Morphant.Markers.IgnoreMarker<T>",
                    "Morphant.Markers.MapMarker",
                    "Morphant.Markers.ValueMarker<T>",
                    "T"
                }));
            Assert.That(
                GetImplicitOperatorSourceTypes(
                    typeof(ConstructorParameter<>)),
                Is.EqualTo(new[]
                {
                    "Morphant.Markers.AutoMarker",
                    "Morphant.Markers.AutoMarker<T>",
                    "Morphant.Markers.IgnoreMarker",
                    "Morphant.Markers.IgnoreMarker<T>",
                    "Morphant.Markers.MapMarker",
                    "Morphant.Markers.ValueMarker<T>",
                    "T"
                }));
            Assert.That(
                new[] { typeof(Member<>), typeof(ConstructorParameter<>) }
                    .SelectMany(type => type.GetConstructors(
                        BindingFlags.Public |
                        BindingFlags.NonPublic |
                        BindingFlags.Instance |
                        BindingFlags.DeclaredOnly))
                    .All(static constructor =>
                        constructor.IsPrivate &&
                        constructor.GetParameters().Length == 0),
                Is.True);
        });
    }

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
T Morphant.Context.MappingContextMarker
  P Morphant.Context.MappingOperation Operation { get; }
T Morphant.Context.MappingOperation
  V Create, Update
T Morphant.Delegates.ConstructUsing<TSource, TResult>
  C .ctor(System.Object, System.IntPtr)
  M System.IAsyncResult BeginInvoke(TSource, System.AsyncCallback, System.Object)
  M TResult EndInvoke(System.IAsyncResult)
  M TResult Invoke(TSource)
T Morphant.Delegates.ConstructUsing<TSource, TContext, TResult>
  C .ctor(System.Object, System.IntPtr)
  M System.IAsyncResult BeginInvoke(TSource, TContext, System.AsyncCallback, System.Object)
  M TResult EndInvoke(System.IAsyncResult)
  M TResult Invoke(TSource, TContext)
T Morphant.Delegates.Construct<TSource, TResult>
  C .ctor(System.Object, System.IntPtr)
  M System.IAsyncResult BeginInvoke(TSource, System.AsyncCallback, System.Object)
  M TResult EndInvoke(System.IAsyncResult)
  M TResult Invoke(TSource)
T Morphant.Delegates.Construct<TSource, TContext, TResult>
  C .ctor(System.Object, System.IntPtr)
  M System.IAsyncResult BeginInvoke(TSource, TContext, System.AsyncCallback, System.Object)
  M TResult EndInvoke(System.IAsyncResult)
  M TResult Invoke(TSource, TContext)
T Morphant.Delegates.Convert<TSource, TResult>
  C .ctor(System.Object, System.IntPtr)
  M System.IAsyncResult BeginInvoke(TSource, System.AsyncCallback, System.Object)
  M TResult EndInvoke(System.IAsyncResult)
  M TResult Invoke(TSource)
T Morphant.Delegates.Convert<TSource, TPrevious, TResult>
  C .ctor(System.Object, System.IntPtr)
  M System.IAsyncResult BeginInvoke(TSource, Morphant.Option<TPrevious>, System.AsyncCallback, System.Object)
  M TResult EndInvoke(System.IAsyncResult)
  M TResult Invoke(TSource, Morphant.Option<TPrevious>)
T Morphant.Delegates.Convert<TSource, TPrevious, TContext, TResult>
  C .ctor(System.Object, System.IntPtr)
  M System.IAsyncResult BeginInvoke(TSource, Morphant.Option<TPrevious>, TContext, System.AsyncCallback, System.Object)
  M TResult EndInvoke(System.IAsyncResult)
  M TResult Invoke(TSource, Morphant.Option<TPrevious>, TContext)
T Morphant.Delegates.Members<TSource, TMembers>
  C .ctor(System.Object, System.IntPtr)
  M System.IAsyncResult BeginInvoke(TSource, System.AsyncCallback, System.Object)
  M TMembers EndInvoke(System.IAsyncResult)
  M TMembers Invoke(TSource)
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
T Morphant.Delegates.Members<TSource, TPrevious, TResult, TContext, TMembers>
  C .ctor(System.Object, System.IntPtr)
  M System.IAsyncResult BeginInvoke(TSource, Morphant.Option<TPrevious>, TResult, TContext, System.AsyncCallback, System.Object)
  M TMembers EndInvoke(System.IAsyncResult)
  M TMembers Invoke(TSource, Morphant.Option<TPrevious>, TResult, TContext)
T Morphant.Delegates.ResolveUsing<TSource, TPrevious, TResult>
  C .ctor(System.Object, System.IntPtr)
  M System.IAsyncResult BeginInvoke(TSource, Morphant.Option<TPrevious>, System.AsyncCallback, System.Object)
  M TResult EndInvoke(System.IAsyncResult)
  M TResult Invoke(TSource, Morphant.Option<TPrevious>)
T Morphant.Delegates.ResolveUsing<TSource, TPrevious, TContext, TResult>
  C .ctor(System.Object, System.IntPtr)
  M System.IAsyncResult BeginInvoke(TSource, Morphant.Option<TPrevious>, TContext, System.AsyncCallback, System.Object)
  M TResult EndInvoke(System.IAsyncResult)
  M TResult Invoke(TSource, Morphant.Option<TPrevious>, TContext)
T Morphant.Delegates.Resolve<TSource, TPrevious, TResult>
  C .ctor(System.Object, System.IntPtr)
  M System.IAsyncResult BeginInvoke(TSource, Morphant.Option<TPrevious>, System.AsyncCallback, System.Object)
  M TResult EndInvoke(System.IAsyncResult)
  M TResult Invoke(TSource, Morphant.Option<TPrevious>)
T Morphant.Delegates.Resolve<TSource, TPrevious, TContext, TResult>
  C .ctor(System.Object, System.IntPtr)
  M System.IAsyncResult BeginInvoke(TSource, Morphant.Option<TPrevious>, TContext, System.AsyncCallback, System.Object)
  M TResult EndInvoke(System.IAsyncResult)
  M TResult Invoke(TSource, Morphant.Option<TPrevious>, TContext)
T Morphant.Exceptions.AmbiguousMappingException
  C .ctor(Morphant.Context.MappingOperation, System.Type, System.Type)
T Morphant.Exceptions.AmbiguousPolymorphicMappingException
  C .ctor(Morphant.Context.MappingOperation, System.Type, System.Type, System.Type, System.Type[], System.Type[])
  P System.Collections.Generic.IReadOnlyList<System.Type> MatchingDestinationTypes { get; }
  P System.Collections.Generic.IReadOnlyList<System.Type> MatchingSourceTypes { get; }
  P System.Type ActualSourceType { get; }
  M Morphant.Exceptions.AmbiguousPolymorphicMappingException Create<TSource, TDestination>(Morphant.Context.MappingOperation, System.Object, System.ValueTuple<System.Boolean, System.Type, System.Type>[])
T Morphant.Exceptions.InvalidMappingContextException
  C .ctor()
T Morphant.Exceptions.InvalidMappingRegistrationException
  C .ctor(Morphant.Context.MappingOperation, System.Type, System.Type)
T Morphant.Exceptions.MappingConfigurationException
  C .ctor(Morphant.Context.MappingOperation, System.Type, System.Type, System.String)
  P System.String Reason { get; }
T Morphant.Exceptions.MappingException
  P Morphant.Context.MappingOperation Operation { get; }
  P System.Type DestinationType { get; }
  P System.Type SourceType { get; }
T Morphant.Exceptions.MappingNotFoundException
  C .ctor(Morphant.Context.MappingOperation, System.Type, System.Type)
T Morphant.Exceptions.MappingOperationNotSupportedException
  C .ctor(Morphant.Context.MappingOperation, System.Type, System.Type, Morphant.MappingMode)
  P Morphant.MappingMode EffectiveMappingMode { get; }
T Morphant.Exceptions.MappingScopeCompletedException
  C .ctor(Morphant.Context.MappingOperation, System.Type, System.Type)
T Morphant.Exceptions.MorphantException
T Morphant.Exceptions.NestedDestinationTypeMismatchException
  C .ctor(Morphant.Context.MappingOperation, System.Type, System.Type, System.Type, System.Type)
  P System.Type ActualDestinationType { get; }
  P System.Type ExpectedDestinationType { get; }
  M Morphant.Exceptions.NestedDestinationTypeMismatchException Create<TSource, TDestination>(Morphant.Context.MappingOperation, System.Object)
T Morphant.Exceptions.NullDestinationException
  C .ctor(Morphant.Context.MappingOperation, System.Type, System.Type)
T Morphant.Exceptions.NullSourceException
  C .ctor(Morphant.Context.MappingOperation, System.Type, System.Type)
T Morphant.Exceptions.OptionValueMissingException
  C .ctor()
T Morphant.Exceptions.PolymorphicDestinationTypeMismatchException
  C .ctor(Morphant.Context.MappingOperation, System.Type, System.Type, System.Type, System.Type, System.Type, System.Type)
  P System.Type ActualDestinationType { get; }
  P System.Type ActualSourceType { get; }
  P System.Type BranchSourceType { get; }
  P System.Type ExpectedDestinationType { get; }
  M Morphant.Exceptions.PolymorphicDestinationTypeMismatchException CreateForUpdate<TSource, TDestination, TBranchSource, TBranchDestination>(TBranchSource, System.Object)
T Morphant.Exceptions.RuntimeInvocationNotSupportedException
  C .ctor()
T Morphant.Exceptions.UnmatchedMappingSwitchException
  C .ctor(Morphant.Context.MappingOperation, System.Type, System.Type)
T Morphant.Exceptions.UnmatchedPolymorphicMappingException
  C .ctor(Morphant.Context.MappingOperation, System.Type, System.Type, System.Type)
  P System.Type ActualSourceType { get; }
  M Morphant.Exceptions.UnmatchedPolymorphicMappingException Create<TSource, TDestination>(Morphant.Context.MappingOperation, System.Object)
T Morphant.Flattening
  V Default, Auto, None
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
  M T Flattening(Morphant.Flattening)
  M T MemberSelection(Morphant.MemberSelection)
  M T NullDestinationHandling(Morphant.NullDestinationHandling)
  M T NullSourceHandling(Morphant.NullSourceHandling)
  M T UnknownDerivedTypeHandling(Morphant.UnknownDerivedTypeHandling)
  M T UnmappedMemberValidation(Morphant.UnmappedMemberValidation)
T Morphant.MapperBuilder<TSource, TDestination>
  M Morphant.MapperBuilder<TSource, TDestination> ForDerived<TDerivedSource, TDerivedDestination>()
  M Morphant.MapperBuilder<TSource, TDestination> IncludeBase<TBaseSource, TBaseDestination>()
  M Morphant.MapperBuilder<TSource, TDestination> IncludeMembers(System.Func<TSource, System.Object>)
T Morphant.MappingMode
  V Default, Create, Update, CreateAndUpdate
T Morphant.Markers.AutoMarker
T Morphant.Markers.AutoMarker<T>
T Morphant.Markers.ByConventionMarker
T Morphant.Markers.ConstructorMarker
T Morphant.Markers.IgnoreMarker
T Morphant.Markers.IgnoreMarker<T>
T Morphant.Markers.MapMarker
T Morphant.Markers.MapMarker<T>
T Morphant.Markers.MemberMarker
T Morphant.Markers.ValueMarker<T>
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
  M Morphant.Markers.ValueMarker<T> Value<T>(T)
  M System.Boolean Supports(System.Type, System.Type)
  M System.Void Configure(Morphant.MapperBuilder)
T Morphant.TypeMapperExtensions
  M TDestination Create<TSource, TDestination>(Morphant.ITypeMapper<TSource, TDestination>, TSource)
  M TDestination Update<TSource, TDestination>(Morphant.ITypeMapper<TSource, TDestination>, TSource, TDestination)
T Morphant.UnknownDerivedTypeHandling
  V Default, UseBaseMapping, Throw
T Morphant.UnmappedMemberValidation
  V Default, None, Source, Destination, Strict
""";

        var actual = DescribePublicApi(typeof(Mapper).Assembly);

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void Runtime_enums_preserve_flags_and_numeric_values()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                typeof(MappingMode).IsDefined(
                    typeof(FlagsAttribute),
                    inherit: false),
                Is.True);
            AssertEnumValues<ConstructorSelection>(
                (nameof(ConstructorSelection.Default), 0),
                (nameof(ConstructorSelection.Explicit), 1),
                (nameof(ConstructorSelection.Parameterless), 2),
                (nameof(ConstructorSelection.Single), 3),
                (nameof(ConstructorSelection.Unambiguous), 4),
                (nameof(ConstructorSelection.Greediest), 5),
                (nameof(ConstructorSelection.Largest), 6));
            AssertEnumValues<MappingMode>(
                (nameof(MappingMode.Default), 0),
                (nameof(MappingMode.Create), 1),
                (nameof(MappingMode.Update), 2),
                (nameof(MappingMode.CreateAndUpdate), 3));
            AssertEnumValues<Flattening>(
                (nameof(Flattening.Default), 0),
                (nameof(Flattening.Auto), 1),
                (nameof(Flattening.None), 2));
            AssertEnumValues<MemberSelection>(
                (nameof(MemberSelection.Default), 0),
                (nameof(MemberSelection.Auto), 1),
                (nameof(MemberSelection.Explicit), 2));
            AssertEnumValues<NullDestinationHandling>(
                (nameof(NullDestinationHandling.Default), 0),
                (nameof(NullDestinationHandling.Create), 1),
                (nameof(NullDestinationHandling.Throw), 2));
            AssertEnumValues<NullSourceHandling>(
                (nameof(NullSourceHandling.Default), 0),
                (nameof(NullSourceHandling.ReturnNull), 1),
                (nameof(NullSourceHandling.ReturnDestination), 2),
                (nameof(NullSourceHandling.Throw), 3));
            AssertEnumValues<UnknownDerivedTypeHandling>(
                (nameof(UnknownDerivedTypeHandling.Default), 0),
                (nameof(UnknownDerivedTypeHandling.UseBaseMapping), 1),
                (nameof(UnknownDerivedTypeHandling.Throw), 2));
            AssertEnumValues<UnmappedMemberValidation>(
                (nameof(UnmappedMemberValidation.Default), 0),
                (nameof(UnmappedMemberValidation.None), 1),
                (nameof(UnmappedMemberValidation.Source), 2),
                (nameof(UnmappedMemberValidation.Destination), 3),
                (nameof(UnmappedMemberValidation.Strict), 4));
            AssertEnumValues<global::Morphant.Context.MappingOperation>(
                (nameof(global::Morphant.Context.MappingOperation.Create), 1),
                (nameof(global::Morphant.Context.MappingOperation.Update), 2));
        });
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

    private static int GetImplicitOperatorCount(Type type) =>
        type.GetMethods(
                BindingFlags.Public |
                BindingFlags.Static |
                BindingFlags.DeclaredOnly)
            .Count(static method => method.Name == "op_Implicit");

    private static void AssertEnumValues<TEnum>(
        params (string Name, int Value)[] expected)
        where TEnum : struct, Enum
    {
        var actual = Enum.GetValues<TEnum>()
            .Select(value =>
                (value.ToString(), Convert.ToInt32(value)))
            .ToArray();

        Assert.That(actual, Is.EqualTo(expected));
    }

    private static string[] GetImplicitOperatorSourceTypes(Type type) =>
        type.GetMethods(
                BindingFlags.Public |
                BindingFlags.Static |
                BindingFlags.DeclaredOnly)
            .Where(static method => method.Name == "op_Implicit")
            .Select(method => FormatType(
                method.GetParameters().Single().ParameterType))
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
}
