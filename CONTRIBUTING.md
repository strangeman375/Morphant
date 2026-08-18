# Contributing to Morphant

Bug reports and focused pull requests are welcome. Please discuss substantial
API or behavior changes in an issue before implementing them. Report security
issues through the process in [SECURITY.md](SECURITY.md), not in a public issue.

## Development setup

Install the .NET SDK selected by `global.json`, then run the release validation
from the repository root:

```shell
dotnet restore src/Morphant.slnx -p:MorphantRoslynVersion=4.4.0
dotnet build src/Morphant.slnx \
  --configuration Release --no-restore \
  -p:ContinuousIntegrationBuild=true -p:MorphantRoslynVersion=4.4.0
dotnet test src/tests/Morphant.Generator.UnitTests/Morphant.Generator.UnitTests.csproj \
  --configuration Release --no-build --no-restore \
  -p:MorphantRoslynVersion=4.4.0
dotnet test src/tests/Morphant.Generator.IntegrationTests/Morphant.Generator.IntegrationTests.csproj \
  --configuration Release --no-build --no-restore \
  -p:MorphantRoslynVersion=4.4.0
```

Keep each pull request focused. Add or update tests for behavior changes, update
user documentation when the public contract changes, and add a concise entry to
`CHANGELOG.md` for user-visible changes. Never include credentials or private
consumer code in issues, logs, fixtures, or commits.
