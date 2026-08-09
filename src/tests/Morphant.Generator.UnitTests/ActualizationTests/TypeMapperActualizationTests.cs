using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;
using static Morphant.Generator.UnitTests.TestUtils.GeneratorActualizationTest;

namespace Morphant.Generator.UnitTests.ActualizationTests;

[TestFixture]
internal sealed class TypeMapperActualizationTests
{
    [Test]
    public void Actualizes_and_executes_conventions_settings_and_contract_lifecycle()
    {
        // lang=c#
        const string absentSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;

namespace TestCase
{
    public readonly struct Source
    {
        public int Value { get; init; }
    }

    public sealed class Destination
    {
        public int Value { get; set; }
    }

    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }
}
""";

        // lang=c#
        const string unrelatedSource =
"""
#nullable enable
#pragma warning disable CS1591

namespace Unrelated
{
    public static class Helper
    {
        public static int Value => 1;
    }
}
""";

        // lang=c#
        const string basicSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace TestCase
{
    public readonly struct Source
    {
        public int Value { get; init; }
    }

    public sealed class Destination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();
            var source = new Source { Value = 17 };
            var created = mapper.Create(source, default(MappingContext));
            var previous = new Destination();
            var updated = mapper.Update(
                source,
                previous,
                default(MappingContext));

            if (created.Value != 17 ||
                !ReferenceEquals(previous, updated) ||
                updated.Value != 17)
            {
                throw new InvalidOperationException(
                    "The basic actualized mapper was incorrect.");
            }
        }
    }
}
""";

        // lang=c#
        const string enhancedSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace TestCase
{
    /// <summary>An edited source.</summary>
    [Serializable]
    public readonly struct Source
    {
        public int Value { get; init; }

        public string Name { get; init; }
    }

    /// <summary>An edited destination.</summary>
    public sealed class Destination
    {
        public int Value { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    /// <summary>An edited mapper.</summary>
    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();
            var source = new Source
            {
                Value = 19,
                Name = "enhanced"
            };
            var created = mapper.Create(source, default(MappingContext));
            var previous = new Destination();
            var updated = mapper.Update(
                source,
                previous,
                default(MappingContext));

            if (created.Value != 19 ||
                created.Name != "enhanced" ||
                !ReferenceEquals(previous, updated) ||
                updated.Value != 19 ||
                updated.Name != "enhanced")
            {
                throw new InvalidOperationException(
                    "The edited members were not actualized.");
            }
        }
    }
}
""";

        // lang=c#
        const string createOnlySource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace TestCase
{
    public readonly struct Source
    {
        public int Value { get; init; }

        public string Name { get; init; }
    }

    public sealed class Destination
    {
        public int Value { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>(MappingMode.Create);
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();
            var source = new Source
            {
                Value = 23,
                Name = "create"
            };
            var created = mapper.Create(source, default(MappingContext));

            if (created.Value != 23 || created.Name != "create")
            {
                throw new InvalidOperationException(
                    "The Create setting was not actualized.");
            }

            try
            {
                mapper.Update(
                    source,
                    new Destination(),
                    default(MappingContext));
            }
            catch (Morphant.Exceptions.MappingOperationNotSupportedException)
            {
                return;
            }

            throw new InvalidOperationException(
                "The stale Update implementation remained available.");
        }
    }
}
""";

        // lang=c#
        const string constructorSource =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace TestCase
{
    public readonly struct Source
    {
        public int Value { get; init; }

        public string Name { get; init; }
    }

    public sealed class Destination
    {
        public Destination(int value) => Value = value;

        public int Value { get; }

        public string Name { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();
            var source = new Source
            {
                Value = 29,
                Name = "constructor"
            };
            var created = mapper.Create(source, default(MappingContext));
            var previous = new Destination(31);
            var updated = mapper.Update(
                source,
                previous,
                default(MappingContext));

            if (created.Value != 29 ||
                created.Name != "constructor" ||
                !ReferenceEquals(previous, updated) ||
                updated.Value != 31 ||
                updated.Name != "constructor")
            {
                throw new InvalidOperationException(
                    "Constructor/member actualization was incorrect.");
            }
        }
    }
}
""";

        // lang=c#
        const string basicMapper =
"""
// <auto-generated />
#nullable enable

namespace TestCase
{
    public partial class TestMapper :
        global::Morphant.ITypeMapper<global::TestCase.Source, global::TestCase.Destination>
    {
        /// <inheritdoc/>
        protected override bool Supports(
            global::System.Type sourceType,
            global::System.Type destinationType) =>
                (sourceType == typeof(global::TestCase.Source) &&
                    destinationType == typeof(global::TestCase.Destination)) ||
                base.Supports(sourceType, destinationType);

        /// <inheritdoc/>
        global::TestCase.Destination global::Morphant.ITypeMapper<global::TestCase.Source, global::TestCase.Destination>.Create(
            global::TestCase.Source source,
            global::Morphant.Context.MappingContext context)
            => __Create(source, context);

        /// <inheritdoc/>
        global::TestCase.Destination global::Morphant.ITypeMapper<global::TestCase.Source, global::TestCase.Destination>.Update(
            global::TestCase.Source source,
            global::TestCase.Destination? destination,
            global::Morphant.Context.MappingContext context)
        {
            if (destination is null)
            {
                return __Create(source, context);
            }

            return __Update(source, destination, context);
        }

        private global::TestCase.Destination __Create(
            global::TestCase.Source source,
            global::Morphant.Context.MappingContext context)
        {
            return new global::TestCase.Destination()
            {
                Value = source.Value
            };
        }

        private global::TestCase.Destination __Update(
            global::TestCase.Source source,
            global::TestCase.Destination destination,
            global::Morphant.Context.MappingContext context)
        {
            destination.Value = source.Value;

            return destination;
        }
    }
}
""";

        // lang=c#
        const string enhancedMapper =
"""
// <auto-generated />
#nullable enable

namespace TestCase
{
    public partial class TestMapper :
        global::Morphant.ITypeMapper<global::TestCase.Source, global::TestCase.Destination>
    {
        /// <inheritdoc/>
        protected override bool Supports(
            global::System.Type sourceType,
            global::System.Type destinationType) =>
                (sourceType == typeof(global::TestCase.Source) &&
                    destinationType == typeof(global::TestCase.Destination)) ||
                base.Supports(sourceType, destinationType);

        /// <inheritdoc/>
        global::TestCase.Destination global::Morphant.ITypeMapper<global::TestCase.Source, global::TestCase.Destination>.Create(
            global::TestCase.Source source,
            global::Morphant.Context.MappingContext context)
            => __Create(source, context);

        /// <inheritdoc/>
        global::TestCase.Destination global::Morphant.ITypeMapper<global::TestCase.Source, global::TestCase.Destination>.Update(
            global::TestCase.Source source,
            global::TestCase.Destination? destination,
            global::Morphant.Context.MappingContext context)
        {
            if (destination is null)
            {
                return __Create(source, context);
            }

            return __Update(source, destination, context);
        }

        private global::TestCase.Destination __Create(
            global::TestCase.Source source,
            global::Morphant.Context.MappingContext context)
        {
            return new global::TestCase.Destination()
            {
                Value = source.Value,
                Name = source.Name
            };
        }

        private global::TestCase.Destination __Update(
            global::TestCase.Source source,
            global::TestCase.Destination destination,
            global::Morphant.Context.MappingContext context)
        {
            destination.Value = source.Value;
            destination.Name = source.Name;

            return destination;
        }
    }
}
""";

        // lang=c#
        const string createOnlyMapper =
"""
// <auto-generated />
#nullable enable

namespace TestCase
{
    public partial class TestMapper :
        global::Morphant.ITypeMapper<global::TestCase.Source, global::TestCase.Destination>
    {
        /// <inheritdoc/>
        protected override bool Supports(
            global::System.Type sourceType,
            global::System.Type destinationType) =>
                (sourceType == typeof(global::TestCase.Source) &&
                    destinationType == typeof(global::TestCase.Destination)) ||
                base.Supports(sourceType, destinationType);

        /// <inheritdoc/>
        global::TestCase.Destination global::Morphant.ITypeMapper<global::TestCase.Source, global::TestCase.Destination>.Create(
            global::TestCase.Source source,
            global::Morphant.Context.MappingContext context)
            => __Create(source, context);

        /// <inheritdoc/>
        global::TestCase.Destination global::Morphant.ITypeMapper<global::TestCase.Source, global::TestCase.Destination>.Update(
            global::TestCase.Source source,
            global::TestCase.Destination? destination,
            global::Morphant.Context.MappingContext context)
            => throw new global::Morphant.Exceptions.MappingOperationNotSupportedException(
                global::Morphant.Context.MappingOperation.Update,
                typeof(global::TestCase.Source),
                typeof(global::TestCase.Destination),
                global::Morphant.MappingMode.Create);

        private global::TestCase.Destination __Create(
            global::TestCase.Source source,
            global::Morphant.Context.MappingContext context)
        {
            return new global::TestCase.Destination()
            {
                Value = source.Value,
                Name = source.Name
            };
        }
    }
}
""";

        // lang=c#
        const string constructorMapper =
"""
// <auto-generated />
#nullable enable

namespace TestCase
{
    public partial class TestMapper :
        global::Morphant.ITypeMapper<global::TestCase.Source, global::TestCase.Destination>
    {
        /// <inheritdoc/>
        protected override bool Supports(
            global::System.Type sourceType,
            global::System.Type destinationType) =>
                (sourceType == typeof(global::TestCase.Source) &&
                    destinationType == typeof(global::TestCase.Destination)) ||
                base.Supports(sourceType, destinationType);

        /// <inheritdoc/>
        global::TestCase.Destination global::Morphant.ITypeMapper<global::TestCase.Source, global::TestCase.Destination>.Create(
            global::TestCase.Source source,
            global::Morphant.Context.MappingContext context)
            => __Create(source, context);

        /// <inheritdoc/>
        global::TestCase.Destination global::Morphant.ITypeMapper<global::TestCase.Source, global::TestCase.Destination>.Update(
            global::TestCase.Source source,
            global::TestCase.Destination? destination,
            global::Morphant.Context.MappingContext context)
        {
            if (destination is null)
            {
                return __Create(source, context);
            }

            return __Update(source, destination, context);
        }

        private global::TestCase.Destination __Create(
            global::TestCase.Source source,
            global::Morphant.Context.MappingContext context)
        {
            return new global::TestCase.Destination(
                value: source.Value)
            {
                Name = source.Name
            };
        }

        private global::TestCase.Destination __Update(
            global::TestCase.Source source,
            global::TestCase.Destination destination,
            global::Morphant.Context.MappingContext context)
        {
            destination.Name = source.Name;

            return destination;
        }
    }
}
""";

        const string hintName =
            "Morphant.Generated.TypeMapper.TestCase_TestMapper.g.cs";

        RunAndAssert(
            LanguageVersion.CSharp9,
            new TestConventionTypeMapperGenerator(),
            Step("mapper attribute absent", absentSource),
            ExecutableStep(
                "mapper contract added",
                basicSource,
                "TestCase.Scenario",
                (hintName, basicMapper)),
            Step(
                "irrelevant source added",
                [
                    SourceFile("TestCase.cs", basicSource),
                    SourceFile("Unrelated.cs", unrelatedSource)
                ],
                (hintName, basicMapper)),
            ExecutableStep(
                "members documentation and attributes changed",
                enhancedSource,
                "TestCase.Scenario",
                (hintName, enhancedMapper)),
            ExecutableStep(
                "mapping mode changed",
                createOnlySource,
                "TestCase.Scenario",
                (hintName, createOnlyMapper)),
            ExecutableStep(
                "constructor and writable members changed",
                constructorSource,
                "TestCase.Scenario",
                (hintName, constructorMapper)),
            Step("mapper attribute removed", absentSource),
            ExecutableStep(
                "original contract restored",
                basicSource,
                "TestCase.Scenario",
                (hintName, basicMapper)));
    }
}
