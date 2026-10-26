#!/usr/bin/env bash
set -euo pipefail

package_version="${1:?Usage: $0 <package-version> <package-feed>}"
package_feed="$(cd "${2:?Package feed is required}" && pwd)"
repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

cd "$repository_root/src/tests/Morphant.Generator.PackageTests.MinimumSdkConsumer"
test "$(dotnet --version)" = "7.0.100"
dotnet run \
  --configuration Release \
  -m:1 \
  -nodeReuse:false \
  -p:MorphantTestPackageVersion="$package_version" \
  -p:MorphantGitSnapshot=true \
  '-p:TargetsTriggeredByCompilation="MinimumSdkAfterCompile;PublishMorphantGitSnapshot"' \
  -p:RestoreSources="$package_feed" \
  -p:NuGetAudit=false
test -n "$(find Generated/Morphant/net7.0 -maxdepth 1 -name 'Morphant.Generated.TypeMapper.*.g.cs' -print -quit)"
test "$(cat obj/Release/net7.0/minimum-sdk-hook.txt)" = "executed"
