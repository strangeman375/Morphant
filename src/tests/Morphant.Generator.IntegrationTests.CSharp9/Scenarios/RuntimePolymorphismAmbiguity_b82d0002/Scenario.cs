// Compiled integration scenario: runtime polymorphic ambiguity
#nullable enable
#pragma warning disable CS1591

using System;
using System.Linq;
using Morphant;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.RuntimePolymorphismAmbiguity_b82d0002
{
    public interface IRoot { }
    public interface IKnown : IRoot { }
    public interface IWorking : IKnown { }
    public interface IPet : IKnown { }
    public interface IWorkingPet : IWorking, IPet { }
    public interface ICompanion : IRoot { }
    public class WorkingBase : IRoot { }
    public sealed class WorkingPet : IWorking, IPet { }
    public sealed class SpecificWorkingPet : IWorkingPet { }
    public sealed class Hybrid : WorkingBase, ICompanion { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<IRoot, object>()
                .ForDerived<IKnown, object>()
                .ForDerived<IWorking, string>()
                .ForDerived<IPet, string>()
                .ForDerived<IWorkingPet, string>()
                .ForDerived<WorkingBase, string>()
                .ForDerived<ICompanion, string>()
                .Convert(_ => "base");
            builder.Map<IKnown, object>()
                .Convert(_ => "known");
            builder.Map<IWorking, string>()
                .Convert(_ => "working");
            builder.Map<IPet, string>()
                .Convert(_ => "pet");
            builder.Map<IWorkingPet, string>()
                .Convert(_ => "specific");
            builder.Map<WorkingBase, string>()
                .Convert(_ => "working-base");
            builder.Map<ICompanion, string>()
                .Convert(_ => "companion");

            builder.Map<object, object>()
                .ForDerived<IWorking[], string>()
                .ForDerived<IPet[], string>()
                .Convert(_ => "array-base");
            builder.Map<IWorking[], string>()
                .Convert(_ => "working-array");
            builder.Map<IPet[], string>()
                .Convert(_ => "pet-array");
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<IRoot, object>)new TestMapper();

            if (!Equals(
                    mapper.Create(new SpecificWorkingPet()),
                    "specific"))
            {
                throw new InvalidOperationException(
                    "A unique more-specific interface was not selected.");
            }

            try
            {
                mapper.Create(new WorkingPet());
                throw new InvalidOperationException(
                    "An ambiguous runtime source was accepted.");
            }
            catch (AmbiguousPolymorphicMappingException exception)
            {
                if (exception.SourceType != typeof(IRoot) ||
                    exception.DestinationType != typeof(object) ||
                    exception.ActualSourceType != typeof(WorkingPet) ||
                    !exception.MatchingSourceTypes.SequenceEqual(
                        new[] { typeof(IWorking), typeof(IPet) }) ||
                    !exception.MatchingDestinationTypes.SequenceEqual(
                        new[] { typeof(string), typeof(string) }))
                {
                    throw new InvalidOperationException(
                        "The ambiguity exception lost maximal branches.");
                }
            }

            try
            {
                mapper.Create(new Hybrid());
                throw new InvalidOperationException(
                    "A class/interface ambiguity was accepted.");
            }
            catch (AmbiguousPolymorphicMappingException exception)
            {
                if (!exception.MatchingSourceTypes.SequenceEqual(
                        new[] { typeof(WorkingBase), typeof(ICompanion) }))
                {
                    throw new InvalidOperationException(
                        "The class/interface ambiguity lost its branches.");
                }
            }

            var arrayMapper =
                (ITypeMapper<object, object>)new TestMapper();

            try
            {
                arrayMapper.Create(new[] { new WorkingPet() });
                throw new InvalidOperationException(
                    "A covariant array ambiguity was accepted.");
            }
            catch (AmbiguousPolymorphicMappingException exception)
            {
                if (exception.ActualSourceType != typeof(WorkingPet[]) ||
                    !exception.MatchingSourceTypes.SequenceEqual(
                        new[] { typeof(IWorking[]), typeof(IPet[]) }))
                {
                    throw new InvalidOperationException(
                        "The array ambiguity lost its branches.");
                }
            }
        }
    }
}
