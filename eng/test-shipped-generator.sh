#!/usr/bin/env bash
set -euo pipefail

roslyn_version="${1:?Usage: $0 <roslyn-version> [dotnet-test-options...]}"
shift
minimum_roslyn_version=4.4.0
unit_project=src/tests/Morphant.Generator.UnitTests/Morphant.Generator.UnitTests.csproj
generator_assembly=src/Morphant.Generator/bin/Release/netstandard2.0/Morphant.Generator.dll
test_generator_assembly=src/tests/Morphant.Generator.UnitTests/bin/Release/net10.0/Morphant.Generator.dll

# The baseline job also runs the integration suite from the same build.
build_target="$unit_project"
if [[ "$roslyn_version" == "$minimum_roslyn_version" ]]; then
  build_target=src/Morphant.slnx
fi

# Build the generator and its dependencies against the minimum supported Roslyn.
dotnet restore "$build_target" -p:MorphantRoslynVersion="$minimum_roslyn_version"
dotnet build "$build_target" \
  --configuration Release --no-restore \
  -p:ContinuousIntegrationBuild=true -p:MorphantRoslynVersion="$minimum_roslyn_version"
generator_checksum="$(sha256sum "$generator_assembly")"

# Change only the test host when validating a newer Roslyn version.
if [[ "$roslyn_version" != "$minimum_roslyn_version" ]]; then
  dotnet restore "$unit_project" -p:MorphantRoslynVersion="$roslyn_version"
  dotnet build "$unit_project" \
    --configuration Release --no-restore \
    -p:ContinuousIntegrationBuild=true -p:MorphantRoslynVersion="$roslyn_version" \
    -p:BuildProjectReferences=false
fi

printf '%s\n' "$generator_checksum" | sha256sum --check
cmp "$generator_assembly" "$test_generator_assembly"

dotnet test "$unit_project" \
  --configuration Release --no-build --no-restore \
  -p:MorphantRoslynVersion="$roslyn_version" \
  --results-directory "artifacts/test-results/roslyn-$roslyn_version" \
  --logger "trx;LogFileName=roslyn-$roslyn_version.trx" \
  "$@"
