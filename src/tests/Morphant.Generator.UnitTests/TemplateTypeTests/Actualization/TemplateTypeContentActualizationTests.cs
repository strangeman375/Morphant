using Morphant.Generator.UnitTests.TestUtils;
using Expected = Morphant.Generator.UnitTests.TestUtils.TemplateTypeActualizationExpectedSource;
using static Morphant.Generator.UnitTests.TestUtils.TemplateTypeActualizationTest;

namespace Morphant.Generator.UnitTests.TemplateTypeTests.Actualization;

[TestFixture]
internal sealed class TemplateTypeContentActualizationTests
{
    private const string DestinationHintName =
        "Morphant.TemplateType.TestCase_Destination.g.cs";

    [Test]
    public void Updates_template_when_destination_constructors_change()
    {
        // lang=c#
        const string initialDestination =
"""
    public sealed class Destination
    {
        public Destination()
        {
        }
    }
""";

        // lang=c#
        const string updatedDestination =
"""
    public sealed class Destination
    {
        public Destination(int id, string? name = null)
        {
        }
    }
""";

        // lang=c#
        const string constructorMembers =
"""
    /// <summary>
    /// Contains mappings for constructor arguments of <see cref="global::TestCase.Destination"/>.
    /// </summary>
    internal sealed class DestinationMorphantTemplateConstructorMembers
    {
        /// <summary>
        /// Configures the <c>id</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<int> id = null!;

        /// <summary>
        /// Configures the <c>name</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<string?> name = null!;
    }
""";

        // lang=c#
        const string updatedConstructor =
"""
        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="id">Configures the <c>id</c> constructor argument.</param>
        /// <param name="name">Configures the <c>name</c> constructor argument. If omitted, the destination constructor default value <c>null</c> is used.</param>
        public DestinationMorphantTemplate(
            global::Morphant.Members.ConstructorMember<int> id,
            global::Morphant.Members.ConstructorMember<string?>? name = null)
        {
        }
""";

        RunAndAssert(
            Step(
                "parameterless constructor",
                BuildSource(initialDestination),
                (
                    DestinationHintName,
                    Expected.Build(
                        destinationConstructors:
                            ParameterlessTemplateConstructor)
                )),
            Step(
                "parameterized constructor",
                BuildSource(updatedDestination),
                (
                    DestinationHintName,
                    Expected.Build(
                        declarationsBeforeTemplate: constructorMembers,
                        byConventionConstructor:
                            Expected.BuildByConventionConstructorWithMembers(
                                "DestinationMorphantTemplate",
                                "DestinationMorphantTemplateConstructorMembers"),
                        destinationConstructors: updatedConstructor)
                )));
    }

    [Test]
    public void Updates_template_when_destination_members_change()
    {
        // lang=c#
        const string initialDestination =
"""
    public sealed class Destination
    {
        public int Id { get; set; }

        public string Name { get; set; } = null!;
    }
""";

        // lang=c#
        const string updatedDestination =
"""
    public sealed class Destination
    {
        public long Id { get; set; }

        public string? DisplayName { get; set; }
    }
""";

        // lang=c#
        const string initialMembers =
"""
        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.Id"/>.
        /// </summary>
        public global::Morphant.Members.Member<int> Id
        {
            get => null!;
            set { }
        }

        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.Name"/>.
        /// </summary>
        public global::Morphant.Members.Member<string> Name
        {
            get => null!;
            set { }
        }
""";

        // lang=c#
        const string updatedMembers =
"""
        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.Id"/>.
        /// </summary>
        public global::Morphant.Members.Member<long> Id
        {
            get => null!;
            set { }
        }

        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.DisplayName"/>.
        /// </summary>
        public global::Morphant.Members.Member<string?> DisplayName
        {
            get => null!;
            set { }
        }
""";

        RunAndAssert(
            Step(
                "initial members",
                BuildSource(initialDestination),
                (
                    DestinationHintName,
                    Expected.Build(
                        destinationConstructors:
                            ParameterlessTemplateConstructor,
                        members: initialMembers)
                )),
            Step(
                "changed members",
                BuildSource(updatedDestination),
                (
                    DestinationHintName,
                    Expected.Build(
                        destinationConstructors:
                            ParameterlessTemplateConstructor,
                        members: updatedMembers)
                )));
    }

    [Test]
    public void Updates_template_when_destination_base_type_changes()
    {
        // lang=c#
        const string additionalSource =
"""
    public class FirstBase
    {
        public int Id { get; set; }
    }

    public class SecondBase
    {
        public string? Name { get; set; }
    }
""";

        // lang=c#
        const string initialDestination =
"""
    public sealed class Destination : FirstBase
    {
    }
""";

        // lang=c#
        const string updatedDestination =
"""
    public sealed class Destination : SecondBase
    {
    }
""";

        // lang=c#
        const string initialMember =
"""
        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.FirstBase.Id"/>.
        /// </summary>
        public global::Morphant.Members.Member<int> Id
        {
            get => null!;
            set { }
        }
""";

        // lang=c#
        const string updatedMember =
"""
        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.SecondBase.Name"/>.
        /// </summary>
        public global::Morphant.Members.Member<string?> Name
        {
            get => null!;
            set { }
        }
""";

        RunAndAssert(
            Step(
                "first base type",
                BuildSource(initialDestination, additionalSource),
                (
                    DestinationHintName,
                    Expected.Build(
                        destinationConstructors:
                            ParameterlessTemplateConstructor,
                        members: initialMember)
                )),
            Step(
                "second base type",
                BuildSource(updatedDestination, additionalSource),
                (
                    DestinationHintName,
                    Expected.Build(
                        destinationConstructors:
                            ParameterlessTemplateConstructor,
                        members: updatedMember)
                )));
    }

    [Test]
    public void Updates_template_when_destination_base_interface_changes()
    {
        // lang=c#
        const string additionalSource =
"""
    public interface IFirstBase
    {
        int Id { get; set; }
    }

    public interface ISecondBase
    {
        string? Name { get; set; }
    }
""";

        // lang=c#
        const string initialDestination =
"""
    public interface Destination : IFirstBase
    {
    }
""";

        // lang=c#
        const string updatedDestination =
"""
    public interface Destination : ISecondBase
    {
    }
""";

        // lang=c#
        const string initialMember =
"""
        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.IFirstBase.Id"/>.
        /// </summary>
        public global::Morphant.Members.Member<int> Id
        {
            get => null!;
            set { }
        }
""";

        // lang=c#
        const string updatedMember =
"""
        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.ISecondBase.Name"/>.
        /// </summary>
        public global::Morphant.Members.Member<string?> Name
        {
            get => null!;
            set { }
        }
""";

        // lang=c#
        const string byConventionConstructor =
"""
        /// <summary>
        /// Configures convention-based mapping without selecting a destination constructor.
        /// </summary>
        /// <param name="marker">Selects convention-based mapping.</param>
        public DestinationMorphantTemplate(global::Morphant.Markers.ByConventionMarker marker)
        {
        }
""";

        RunAndAssert(
            Step(
                "first base interface",
                BuildSource(initialDestination, additionalSource),
                (
                    DestinationHintName,
                    Expected.Build(
                        byConventionConstructor:
                            byConventionConstructor,
                        members: initialMember)
                )),
            Step(
                "second base interface",
                BuildSource(updatedDestination, additionalSource),
                (
                    DestinationHintName,
                    Expected.Build(
                        byConventionConstructor:
                            byConventionConstructor,
                        members: updatedMember)
                )));
    }

    [Test]
    public void Updates_template_when_generic_constraints_change()
    {
        // lang=c#
        const string initialSource =
"""
#pragma warning disable CS1591
#nullable enable

using Morphant;

namespace TestCase
{
    public sealed class Source
    {
    }

    public class Base
    {
    }

    public interface IContract
    {
    }

    public sealed class Constructed : Base, IContract
    {
        public Constructed()
        {
        }
    }

    public sealed class Destination<T>
        where T : Base
    {
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination<Constructed>>();
        }
    }
}
""";

        var updatedSource =
            initialSource.Replace(
                "where T : Base\n",
                "where T : Base, IContract, new()\n");

        const string hintName =
            "Morphant.TemplateType.TestCase_Destination_1.g.cs";

        RunAndAssert(
            Step(
                "base constraint",
                initialSource,
                (
                    hintName,
                    Expected.Build(
                        templateTypeReference:
                            "DestinationMorphantTemplate<T>",
                        destinationTypeName:
                            "global::TestCase.Destination<T>",
                        destinationCref:
                            "global::TestCase.Destination&lt;T&gt;",
                        typeParameterConstraints:
                            "        where T : global::TestCase.Base",
                        destinationConstructors:
                            ParameterlessTemplateConstructor)
                )),
            Step(
                "expanded constraints",
                updatedSource,
                (
                    hintName,
                    Expected.Build(
                        templateTypeReference:
                            "DestinationMorphantTemplate<T>",
                        destinationTypeName:
                            "global::TestCase.Destination<T>",
                        destinationCref:
                            "global::TestCase.Destination&lt;T&gt;",
                        typeParameterConstraints:
                            "        where T : global::TestCase.Base, global::TestCase.IContract, new()",
                        destinationConstructors:
                            ParameterlessTemplateConstructor)
                )));
    }

    [Test]
    public void Updates_template_when_documentation_appears_but_not_when_its_text_changes()
    {
        // lang=c#
        const string undocumentedDestination =
"""
    public sealed class Destination
    {
        public int Id { get; set; }
    }
""";

        // lang=c#
        const string documentedDestination =
"""
    /// <summary>
    /// Represents the initial destination documentation.
    /// </summary>
    public sealed class Destination
    {
        /// <summary>
        /// Gets or sets the initial identifier documentation.
        /// </summary>
        public int Id { get; set; }
    }
""";

        var editedDocumentationDestination =
            documentedDestination
                .Replace("initial destination", "edited destination")
                .Replace("initial identifier", "edited identifier");

        // lang=c#
        const string fallbackMemberDocumentation =
"""
        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.Id"/>.
        /// </summary>
        public global::Morphant.Members.Member<int> Id
        {
            get => null!;
            set { }
        }
""";

        // lang=c#
        const string inheritedMemberDocumentation =
"""
        /// <inheritdoc cref="global::TestCase.Destination.Id"/>
        public global::Morphant.Members.Member<int> Id
        {
            get => null!;
            set { }
        }
""";

        var documentedExpected = Expected.Build(
            destinationHasDocumentation: true,
            destinationConstructors: ParameterlessTemplateConstructor,
            members: inheritedMemberDocumentation);

        RunAndAssert(
            Step(
                "without documentation",
                BuildSource(undocumentedDestination),
                (
                    DestinationHintName,
                    Expected.Build(
                        destinationConstructors:
                            ParameterlessTemplateConstructor,
                        members: fallbackMemberDocumentation)
                )),
            Step(
                "documentation added",
                BuildSource(documentedDestination),
                (DestinationHintName, documentedExpected)),
            Step(
                "documentation text edited",
                BuildSource(editedDocumentationDestination),
                (DestinationHintName, documentedExpected)),
            Step(
                "documentation removed",
                BuildSource(undocumentedDestination),
                (
                    DestinationHintName,
                    Expected.Build(
                        destinationConstructors:
                            ParameterlessTemplateConstructor,
                        members: fallbackMemberDocumentation)
                )));
    }

    [Test]
    public void Updates_template_when_surface_affecting_attributes_change()
    {
        // lang=c#
        const string initialDestination =
"""
    public sealed class Destination
    {
        [Obsolete("Use Current instead.")]
        [DisallowNull]
        public string? Legacy { get; set; }
    }
""";

        // lang=c#
        const string updatedDestination =
"""
    public sealed class Destination
    {
        [Obsolete("Legacy was removed.", true)]
        [AllowNull]
        public string? Legacy { get; set; }
    }
""";

        // lang=c#
        const string initialMember =
"""
        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.Legacy"/>.
        /// </summary>
        [global::System.ObsoleteAttribute("Use Current instead.")]
        public global::Morphant.Members.Member<string> Legacy
        {
            get => null!;
            set { }
        }
""";

        // lang=c#
        const string updatedMember =
"""
        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.Legacy"/>.
        /// </summary>
        [global::System.ObsoleteAttribute("Legacy was removed.", true)]
        public global::Morphant.Members.Member<string?> Legacy
        {
            get => null!;
            set { }
        }
""";

        RunAndAssert(
            Step(
                "initial attributes",
                BuildSource(initialDestination),
                (
                    DestinationHintName,
                    Expected.Build(
                        destinationConstructors:
                            ParameterlessTemplateConstructor,
                        members: initialMember)
                )),
            Step(
                "changed attributes",
                BuildSource(updatedDestination),
                (
                    DestinationHintName,
                    Expected.Build(
                        destinationConstructors:
                            ParameterlessTemplateConstructor,
                        members: updatedMember)
                )));
    }

    [Test]
    public void Keeps_template_unchanged_when_unrelated_attributes_change()
    {
        // lang=c#
        const string initialDestination =
"""
    [Marker("destination-v1")]
    public sealed class Destination
    {
        [Marker("member-v1")]
        public int Id { get; set; }
    }
""";

        var updatedDestination =
            initialDestination
                .Replace("destination-v1", "destination-v2")
                .Replace("member-v1", "member-v2");

        // lang=c#
        const string additionalSource =
"""
    [AttributeUsage(AttributeTargets.All)]
    public sealed class MarkerAttribute : Attribute
    {
        public MarkerAttribute(string value)
        {
        }
    }
""";

        // lang=c#
        const string expectedMember =
"""
        /// <summary>
        /// Configures mapping for <see cref="global::TestCase.Destination.Id"/>.
        /// </summary>
        public global::Morphant.Members.Member<int> Id
        {
            get => null!;
            set { }
        }
""";

        var expected = Expected.Build(
            destinationConstructors: ParameterlessTemplateConstructor,
            members: expectedMember);

        RunAndAssert(
            Step(
                "initial unrelated attributes",
                BuildSource(initialDestination, additionalSource),
                (DestinationHintName, expected)),
            Step(
                "changed unrelated attributes",
                BuildSource(updatedDestination, additionalSource),
                (DestinationHintName, expected)));
    }

    [Test]
    public void Updates_template_when_referenced_destination_changes()
    {
        // lang=c#
        const string initialReferenceSource =
"""
#nullable enable

namespace ReferencedModels
{
    public sealed class Destination
    {
        public Destination()
        {
        }

        public int Id { get; set; }
    }
}
""";

        // lang=c#
        const string updatedReferenceSource =
"""
#nullable enable

namespace ReferencedModels
{
    public sealed class Destination
    {
        public Destination(string? name)
        {
        }

        public string? Name { get; set; }
    }
}
""";

        var initialReference = CreateReference(
            "ReferencedModels",
            initialReferenceSource);

        var updatedReference = CreateReference(
            "ReferencedModels",
            updatedReferenceSource);

        // lang=c#
        const string initialMember =
"""
        /// <summary>
        /// Configures mapping for <see cref="global::ReferencedModels.Destination.Id"/>.
        /// </summary>
        public global::Morphant.Members.Member<int> Id
        {
            get => null!;
            set { }
        }
""";

        // lang=c#
        const string constructorMembers =
"""
    /// <summary>
    /// Contains mappings for constructor arguments of <see cref="global::ReferencedModels.Destination"/>.
    /// </summary>
    internal sealed class DestinationMorphantTemplateConstructorMembers
    {
        /// <summary>
        /// Configures the <c>name</c> constructor argument.
        /// </summary>
        public global::Morphant.Members.ConstructorMember<string?> name = null!;
    }
""";

        // lang=c#
        const string updatedConstructor =
"""
        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        /// <param name="name">Configures the <c>name</c> constructor argument.</param>
        public DestinationMorphantTemplate(global::Morphant.Members.ConstructorMember<string?> name)
        {
        }
""";

        // lang=c#
        const string updatedMember =
"""
        /// <summary>
        /// Configures mapping for <see cref="global::ReferencedModels.Destination.Name"/>.
        /// </summary>
        public global::Morphant.Members.Member<string?> Name
        {
            get => null!;
            set { }
        }
""";

        const string hintName =
            "Morphant.TemplateType.ReferencedModels_Destination.g.cs";

        RunAndAssert(
            Step(
                "initial referenced destination",
                ReferencedDestinationUsageSource,
                new[] { initialReference },
                (
                    hintName,
                    Expected.Build(
                        templateNamespace:
                            "ReferencedModels.Morphant.Generated",
                        destinationTypeName:
                            "global::ReferencedModels.Destination",
                        destinationConstructors:
                            ParameterlessTemplateConstructor,
                        members: initialMember)
                )),
            Step(
                "updated referenced destination",
                ReferencedDestinationUsageSource,
                new[] { updatedReference },
                (
                    hintName,
                    Expected.Build(
                        templateNamespace:
                            "ReferencedModels.Morphant.Generated",
                        destinationTypeName:
                            "global::ReferencedModels.Destination",
                        declarationsBeforeTemplate: constructorMembers,
                        byConventionConstructor:
                            Expected.BuildByConventionConstructorWithMembers(
                                "DestinationMorphantTemplate",
                                "DestinationMorphantTemplateConstructorMembers"),
                        destinationConstructors: updatedConstructor,
                        members: updatedMember)
                )));
    }

    private static string BuildSource(
        string destinationDeclaration,
        string additionalSource = "")
    {
        return SourceTemplate
            .Replace("__ADDITIONAL_SOURCE__", additionalSource)
            .Replace("__DESTINATION_DECLARATION__", destinationDeclaration);
    }

    // lang=c#
    private const string SourceTemplate =
"""
#pragma warning disable CS1591
#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using Morphant;

namespace TestCase
{
    public sealed class Source
    {
    }

__ADDITIONAL_SOURCE__

__DESTINATION_DECLARATION__

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>();
        }
    }
}
""";

    // lang=c#
    private const string ReferencedDestinationUsageSource =
"""
#pragma warning disable CS1591

using Morphant;
using ReferencedModels;

namespace TestCase
{
    public sealed class Source
    {
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>();
        }
    }
}
""";

    // lang=c#
    private const string ParameterlessTemplateConstructor =
"""
        /// <summary>
        /// Creates a destination instance using a corresponding constructor.
        /// </summary>
        public DestinationMorphantTemplate()
        {
        }
""";
}
