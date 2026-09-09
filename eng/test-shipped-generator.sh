#!/usr/bin/env bash
set -euo pipefail

unit_project=src/tests/Morphant.Generator.UnitTests/Morphant.Generator.UnitTests.csproj
generator_assembly=src/Morphant.Generator/bin/Release/netstandard2.0/Morphant.Generator.dll
test_generator_assembly=src/tests/Morphant.Generator.UnitTests/bin/Release/net10.0/Morphant.Generator.dll

# Build every project against the minimum supported Roslyn first.
dotnet restore "$unit_project" -p:MorphantRoslynVersion=4.4.0
dotnet build "$unit_project" \
  --configuration Release --no-restore \
  -p:ContinuousIntegrationBuild=true -p:MorphantRoslynVersion=4.4.0
generator_checksum="$(sha256sum "$generator_assembly")"

# Change the test host without recompiling the generator or its dependencies.
dotnet restore "$unit_project" -p:MorphantRoslynVersion=4.9.2
dotnet build "$unit_project" \
  --configuration Release --no-restore \
  -p:ContinuousIntegrationBuild=true -p:MorphantRoslynVersion=4.9.2 \
  -p:BuildProjectReferences=false

printf '%s\n' "$generator_checksum" | sha256sum --check
cmp "$generator_assembly" "$test_generator_assembly"

dotnet test "$unit_project" \
  --configuration Release --no-build --no-restore \
  -p:MorphantRoslynVersion=4.9.2 \
  --results-directory artifacts/test-results/roslyn-4.9.2 \
  --logger 'trx;LogFileName=roslyn-4.9.2.trx'
