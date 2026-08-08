using Morphant.Context;

namespace Morphant.Generator.UnitTests;

[TestFixture]
internal sealed class MappingDelegateTests
{
    [Test]
    public void Exposes_semantic_callback_parameter_names()
    {
        AssertParameterNames<
            global::Morphant.Delegates.Construct<object, object>>(
            "source");
        AssertParameterNames<
            global::Morphant.Delegates.Construct<object, object, object>>(
            "source",
            "context");
        AssertParameterNames<
            global::Morphant.Delegates.Resolve<object, object, object>>(
            "source",
            "previous");
        AssertParameterNames<
            global::Morphant.Delegates.Resolve<object, object, object, object>>(
            "source",
            "previous",
            "context");
        AssertParameterNames<
            global::Morphant.Delegates.ConstructUsing<object, object>>(
            "source");
        AssertParameterNames<
            global::Morphant.Delegates.ConstructUsing<object, object, object>>(
            "source",
            "context");
        AssertParameterNames<
            global::Morphant.Delegates.ResolveUsing<object, object, object>>(
            "source",
            "previous");
        AssertParameterNames<
            global::Morphant.Delegates.ResolveUsing<
                object,
                object,
                object,
                object>>(
            "source",
            "previous",
            "context");
        AssertParameterNames<
            global::Morphant.Delegates.Members<object, object>>(
            "source");
        AssertParameterNames<
            global::Morphant.Delegates.Members<object, object, object>>(
            "source",
            "previous");
        AssertParameterNames<
            global::Morphant.Delegates.Members<
                object,
                object,
                object,
                object>>(
            "source",
            "previous",
            "result");
        AssertParameterNames<
            global::Morphant.Delegates.Members<
                object,
                object,
                object,
                object,
                object>>(
            "source",
            "previous",
            "result",
            "context");
        AssertParameterNames<
            global::Morphant.Delegates.Convert<object, object>>(
            "source");
        AssertParameterNames<
            global::Morphant.Delegates.Convert<object, object, object>>(
            "source",
            "previous");
        AssertParameterNames<
            global::Morphant.Delegates.Convert<
                object,
                object,
                object,
                object>>(
            "source",
            "previous",
            "context");
    }

    [Test]
    public void Preserves_callback_parameter_and_return_types()
    {
        AssertSignature<
            global::Morphant.Delegates.Construct<Source, Result>>(
            typeof(Result),
            typeof(Source));
        AssertSignature<
            global::Morphant.Delegates.Construct<
                Source,
                MappingContextMarker,
                Result>>(
            typeof(Result),
            typeof(Source),
            typeof(MappingContextMarker));
        AssertSignature<
            global::Morphant.Delegates.Resolve<
                Source,
                Previous,
                Result>>(
            typeof(Result),
            typeof(Source),
            typeof(Option<Previous>));
        AssertSignature<
            global::Morphant.Delegates.Resolve<
                Source,
                Previous,
                MappingContextMarker,
                Result>>(
            typeof(Result),
            typeof(Source),
            typeof(Option<Previous>),
            typeof(MappingContextMarker));
        AssertSignature<
            global::Morphant.Delegates.ConstructUsing<Source, Result>>(
            typeof(Result),
            typeof(Source));
        AssertSignature<
            global::Morphant.Delegates.ConstructUsing<
                Source,
                MappingContext,
                Result>>(
            typeof(Result),
            typeof(Source),
            typeof(MappingContext));
        AssertSignature<
            global::Morphant.Delegates.ResolveUsing<
                Source,
                Previous,
                Result>>(
            typeof(Result),
            typeof(Source),
            typeof(Option<Previous>));
        AssertSignature<
            global::Morphant.Delegates.ResolveUsing<
                Source,
                Previous,
                MappingContext,
                Result>>(
            typeof(Result),
            typeof(Source),
            typeof(Option<Previous>),
            typeof(MappingContext));
        AssertSignature<
            global::Morphant.Delegates.Members<Source, MemberPlan>>(
            typeof(MemberPlan),
            typeof(Source));
        AssertSignature<
            global::Morphant.Delegates.Members<
                Source,
                Previous,
                MemberPlan>>(
            typeof(MemberPlan),
            typeof(Source),
            typeof(Option<Previous>));
        AssertSignature<
            global::Morphant.Delegates.Members<
                Source,
                Previous,
                Result,
                MemberPlan>>(
            typeof(MemberPlan),
            typeof(Source),
            typeof(Option<Previous>),
            typeof(Result));
        AssertSignature<
            global::Morphant.Delegates.Members<
                Source,
                Previous,
                Result,
                MappingContextMarker,
                MemberPlan>>(
            typeof(MemberPlan),
            typeof(Source),
            typeof(Option<Previous>),
            typeof(Result),
            typeof(MappingContextMarker));
        AssertSignature<
            global::Morphant.Delegates.Convert<Source, Result>>(
            typeof(Result),
            typeof(Source));
        AssertSignature<
            global::Morphant.Delegates.Convert<
                Source,
                Previous,
                Result>>(
            typeof(Result),
            typeof(Source),
            typeof(Option<Previous>));
        AssertSignature<
            global::Morphant.Delegates.Convert<
                Source,
                Previous,
                MappingContext,
                Result>>(
            typeof(Result),
            typeof(Source),
            typeof(Option<Previous>),
            typeof(MappingContext));
    }

    private static void AssertParameterNames<TDelegate>(
        params string[] expectedNames)
        where TDelegate : Delegate
    {
        var invoke = typeof(TDelegate).GetMethod("Invoke")!;
        var actualNames = invoke
            .GetParameters()
            .Select(parameter => parameter.Name)
            .ToArray();

        Assert.That(actualNames, Is.EqualTo(expectedNames));
    }

    private static void AssertSignature<TDelegate>(
        Type returnType,
        params Type[] parameterTypes)
        where TDelegate : Delegate
    {
        var invoke = typeof(TDelegate).GetMethod("Invoke")!;

        Assert.Multiple(() =>
        {
            Assert.That(invoke.ReturnType, Is.EqualTo(returnType));
            Assert.That(
                invoke.GetParameters()
                    .Select(parameter => parameter.ParameterType),
                Is.EqualTo(parameterTypes));
        });
    }

    private sealed class Source;

    private sealed class Previous;

    private sealed class Result;

    private sealed class MemberPlan;
}
