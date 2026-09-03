// Compiled integration scenario: base and derived MappingMode boundaries
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.RuntimePolymorphismModes_b82d0015
{
    public interface IFirst { }
    public interface IFirstDerived : IFirst { }
    public sealed class FirstDerived : IFirstDerived { }
    public interface ISecond { }
    public interface ISecondDerived : ISecond { }
    public sealed class SecondDerived : ISecondDerived { }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<IFirst, object>(MappingMode.Create)
                .ForDerived<IFirstDerived, string>()
                .Convert(_ => "first-base");
            builder.Map<IFirstDerived, string>()
                .Convert(_ => "first-derived");

            builder.Map<ISecond, object>()
                .ForDerived<ISecondDerived, string>()
                .Convert(_ => "second-base");
            builder.Map<ISecondDerived, string>(MappingMode.Update)
                .Convert(_ => "second-derived");
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var first = (ITypeMapper<IFirst, object>)mapper;
            var second = (ITypeMapper<ISecond, object>)mapper;

            try
            {
                first.Update(new FirstDerived(), "previous");
                throw new InvalidOperationException(
                    "Derived dispatch bypassed the base MappingMode.");
            }
            catch (MappingOperationNotSupportedException exception)
            {
                if (exception.SourceType != typeof(IFirst) ||
                    exception.DestinationType != typeof(object) ||
                    exception.EffectiveMappingMode != MappingMode.Create)
                {
                    throw new InvalidOperationException(
                        "The base mode failure is incorrect.");
                }
            }

            try
            {
                second.Create(new SecondDerived());
                throw new InvalidOperationException(
                    "A matched disabled derived Create was accepted.");
            }
            catch (MappingOperationNotSupportedException exception)
            {
                if (exception.SourceType != typeof(ISecondDerived) ||
                    exception.DestinationType != typeof(string) ||
                    exception.EffectiveMappingMode != MappingMode.Update)
                {
                    throw new InvalidOperationException(
                        "The derived mode failure is incorrect.");
                }
            }
        }
    }
}
