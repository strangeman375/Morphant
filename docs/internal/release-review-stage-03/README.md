# Воспроизведения этапа 3

Это исследовательские входы и измерения. Исходные отказы зафиксированы в [отчёте этапа 3](../RELEASE_REVIEW_STAGE_03.md), результаты после исправлений — в [отчёте исправлений](../RELEASE_REVIEW_STAGE_03_FIXES.md). Постоянные compiler- и runtime-регрессии находятся в test projects.

Команды выполняются из корня Morphant. В текущем workspace вместо `dotnet` используется `/workspace/morphant-tools/dotnet`. Требуется SDK 10.0.100; generator и compiler probe используют Roslyn 4.4.0.

## Межсборочный случай в MSBuild

```shell
dotnet build docs/internal/release-review-stage-03/msbuild/Consumer/Consumer.csproj -c Release -p:UseSharedCompilation=false -m:1
```

После исправлений ожидается чистая сборка с IVT и без него. В producer есть bare mapper обычной пары. Явное имя construction-типа consumer теперь имеет вид `Morphant.Generated.Types.A_AuditConsumer.N_Shared.Plans.DestinationConstruction`.

После сборки producer можно проверить отдельные артефакты:

```shell
dotnet build docs/internal/release-review-stage-03/msbuild/Producer/Producer.csproj -c Release
dotnet build docs/internal/release-review-stage-03/msbuild/Consumer/Consumer.csproj -c Release -p:ReviewReferenceKind=dll
dotnet build docs/internal/release-review-stage-03/msbuild/Consumer/Consumer.csproj -c Release -p:ReviewReferenceKind=ref
```

## Связанные семьи в одной сборке

```shell
dotnet build docs/internal/release-review-stage-03/msbuild/Single/Single.csproj -c Release -p:UseSharedCompilation=false -m:1
```

Ожидается чистая сборка. `Single.cs` содержит пример без `base.Configure`. Направленные варианты находятся в `family-variants`; после исправлений все пять должны компилироваться без предупреждений. Их можно по одному использовать вместо `Single.cs`; одновременно включать варианты в проект не нужно.

## Матрица compiler references

```shell
dotnet build docs/internal/release-review-stage-03/compiler/CompilerProbe.csproj -c Release
dotnet docs/internal/release-review-stage-03/compiler/bin/Release/net10.0/CompilerProbe.dll artifacts/release-review/stage-03/reproduced/shared shared
```

Вместо последнего `shared` доступны `nullable`, `tuple`, `family`, `distinct-source`, `same-family`, `same-related-family`, `same-nested-family`, `same-ordinary`. Для каждого режима укажите отдельный каталог результата. Probe сохраняет producer/consumer sources, полные generated files, warning/error diagnostics и `summary.json`.

Первые пять режимов сравнивают source compilation, implementation DLL и reference assembly с IVT и без него. Последние четыре исследуют два маппера или семейства в одной compilation. Общая матрица — 556 случаев; `shared` сохраняется только как историческое имя режима обычной пары. Статус программы 0 означает завершение измерения: нужно отдельно проверить отсутствие diagnostics и исключений в `summary.json`. Там также записаны числа generated files, construction/member-файлов и callback-методов. Исходные отказы сохранены в [results.json](results.json), результаты повторной проверки исправлений — в [fix-results.json](fix-results.json).

Отдельный CRTP-вариант:

```shell
dotnet docs/internal/release-review-stage-03/compiler/bin/Release/net10.0/CompilerProbe.dll artifacts/release-review/stage-03/reproduced/base-call custom docs/internal/release-review-stage-03/family-variants/base-call.cs
```

Это compiler-проверка production analyzer через `AnalyzerFileReference`. Она не выполняет generated runtime mapping и не моделирует живую IDE.
