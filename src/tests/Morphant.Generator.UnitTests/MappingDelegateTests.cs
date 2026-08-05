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
            "previous");
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
            global::Morphant.Delegates.Convert<object, object, object>>(
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
                Previous,
                Result>>(
            typeof(Result),
            typeof(Source),
            typeof(Option<Previous>));
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
            global::Morphant.Delegates.Convert<
                Source,
                Previous,
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
