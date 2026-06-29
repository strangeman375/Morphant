using Microsoft.CodeAnalysis;

namespace Morphant.Generator;

internal readonly record struct MapperBuilderMapCallInfo(ITypeSymbol SourceType, ITypeSymbol DestinationType);
