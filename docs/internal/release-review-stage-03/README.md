# Воспроизведения этапа 3

Это исследовательские входы и измерения, а не новые тесты поддерживаемого поведения. Отказы зафиксированы в [отчёте](../RELEASE_REVIEW_STAGE_03.md); исправления ещё не выбраны.

Команды выполняются из корня Morphant. В текущем workspace вместо `dotnet` используется `/workspace/morphant-tools/dotnet`. Требуется SDK 10.0.100; generator и compiler probe используют Roslyn 4.4.0.

## Межсборочный случай в MSBuild

```shell
dotnet build docs/internal/release-review-stage-03/msbuild/Consumer/Consumer.csproj -c Release -p:UseSharedCompilation=false -m:1
```

Ожидаемый текущий отказ: `CS0121`, `CS0436`, `MORPH0018`. В producer есть bare mapper обычной пары и IVT к consumer. Удаление IVT из `Producer.cs` даёт чистую сборку. Замена `Construct` на bare Map оставляет `CS0436`; явное `new Morphant.Generated.Types.N_Shared.Plans.DestinationConstruction(s.Id)` также оставляет только `CS0436`.

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

Ожидается `CS0121`. `Single.cs` содержит пример без `base.Configure`. Направленные варианты находятся в `family-variants`: `base-call.cs` компилируется чисто, остальные четыре воспроизводят неоднозначность. Их можно по одному использовать вместо `Single.cs`; одновременно включать варианты в проект не нужно.

## Матрица compiler references

```shell
dotnet build docs/internal/release-review-stage-03/compiler/CompilerProbe.csproj -c Release
dotnet docs/internal/release-review-stage-03/compiler/bin/Release/net10.0/CompilerProbe.dll artifacts/release-review/stage-03/reproduced/shared shared
```

Вместо последнего `shared` доступны `nullable`, `tuple`, `family`, `distinct-source`, `same-family`, `same-related-family`, `same-nested-family`. Для каждого режима укажите отдельный каталог результата. Probe сохраняет producer/consumer sources, полные generated files, warning/error diagnostics и `summary.json`.

Первые пять режимов сравнивают source compilation, implementation DLL и reference assembly с IVT и без него. Последние три исследуют два семейства в одной compilation. Общая матрица — 540 случаев; статус программы 0 означает завершение измерения, а не отсутствие diagnostics. Ожидаемые текущие группы результатов сохранены в [results.json](results.json).

Отдельный CRTP-вариант:

```shell
dotnet docs/internal/release-review-stage-03/compiler/bin/Release/net10.0/CompilerProbe.dll artifacts/release-review/stage-03/reproduced/base-call custom docs/internal/release-review-stage-03/family-variants/base-call.cs
```

Это compiler-проверка production analyzer через `AnalyzerFileReference`. Она не выполняет generated runtime mapping и не моделирует живую IDE.
