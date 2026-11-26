using System.Diagnostics.CodeAnalysis;
using Morphant.Exceptions;

namespace Morphant;

/// <summary>
/// Configures mapper defaults and registers source/destination pairs.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class MapperBuilder : MapperBuilderBase<MapperBuilder>
{
    private MapperBuilder()
    {
    }

    /// <summary>
    /// Configures the default operations for this mapper.
    /// </summary>
    /// <param name="mappingMode">
    /// The supported mapping operations, as a compile-time constant.
    /// <see cref="Morphant.MappingMode.Default"/> inherits the setting;
    /// the fallback is
    /// <see cref="Morphant.MappingMode.CreateAndUpdate"/>.
    /// </param>
    /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/settings/mapping-mode.md"/>
    public MapperBuilder MappingMode(MappingMode mappingMode) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Registers a mapping from <typeparamref name="TSource"/> to
    /// <typeparamref name="TDestination"/> in this mapper.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TDestination">The destination type.</typeparam>
    /// <param name="mappingMode">
    /// The supported mapping operations, as a compile-time constant.
    /// <see cref="Morphant.MappingMode.Default"/> continues through
    /// normal setting precedence; the fallback is
    /// <see cref="Morphant.MappingMode.CreateAndUpdate"/>.
    /// </param>
    /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/api/map.md"/>
    public MappingBuilder<TSource, TDestination> Map<TSource, TDestination>(
        MappingMode mappingMode = Morphant.MappingMode.Default) =>
        throw new RuntimeInvocationNotSupportedException();
}
