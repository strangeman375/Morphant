# Compiler-прототип специализированных расширений

Обоснование: [решения S03-01–S03-03](../../RELEASE_REVIEW_STAGE_03_SOLUTIONS.md).

Прототип обновлён после замечания пользователя о сложности shared-поверхности.
Проверяемая схема: прямые `MappingBuilder<ConcreteMapper, S, D>` receivers и
`IMappingBuilder<Family<TMapper, ...>, S, D>` для bare CRTP self. Сборочного
маркера нет. Неограниченные shared-методы остаются только в контрольном старом
producer; два direct-family контроля воспроизводят конфликт без covariance.

Это compiler-эксперимент: настоящие delegates/context-типы Morphant,
тестовые builders в namespace `Audit`, минимальные construction/member-классы.
Production generator и runtime mapper не запускаются. `Setting()` проверяет
полный fluent-return до и после callback. Обязательный `int`-аргумент
construction-класса проверяется отдельно; реальные constructor overloads,
marker conversions, input annotations и XML здесь не моделируются.

## Запуск

Из корня репозитория, с SDK 10.0.100 и доступным NuGet-кешем:

```sh
dotnet restore docs/internal/release-review-stage-03/solutions/SurfaceProbe.csproj --disable-parallel -m:1 -nodeReuse:false
dotnet build docs/internal/release-review-stage-03/solutions/SurfaceProbe.csproj -c Release --no-restore -m:1 -nodeReuse:false
dotnet docs/internal/release-review-stage-03/solutions/bin/Release/net10.0/SurfaceProbe.dll artifacts/release-review/stage-03/specialized-matrix
```

В текущем рабочем окружении вместо `dotnet` использовать
`/workspace/morphant-tools/dotnet`; single-process MSBuild требуется и для
restore. Сохранённый [results.json](results.json) — компактный `summary.json`
запуска. Исходные consumer fixtures, diagnostics и полные сведения о каждом
binding воспроизводятся под `artifacts` и не добавляются в git.

## Матрица и результаты

39 сравнительных компиляций, C# 9, nullable enabled, Roslyn 4.4.0:

- 24 consumer compilation: ordinary/nullable/tuple/unrelated family,
  IVT on/off, source/DLL/reference assembly.
- 3 consumer compilation со старой shared-поверхностью producer.
- 6 related family compilation: generic, вложенная generic-подстановка,
  одинаковые constraints, tuple names, `dynamic`/`object`, recursive nullability.
- 1 non-partial nullable-база с собственными local/direct receivers.
- 5 отрицательных контролей: два прямых family receivers, ошибочная лямбда с
  откатом к базе, отсутствие local surface и пропущенный required argument.

Все 34 положительных сценария чистые; проверено 652 callback-binding, включая
все 15 перегрузок и явные имена construction/member-типов. Shared model types
в межсборочных проверках объявлены только в producer. Reference assembly
emitted отдельно с `metadataOnly: true, includePrivateMembers: false`.
Source-backed reference — `CSharpCompilationReference`, не IDE-проверка.

`BindingMismatches` сравнивает receiver с ожидаемым владельцем. Прямой
`MappingBuilder` сообщает конкретный self, ковариантный `IMappingBuilder` —
номинальное семейство. В direct-family контролях одинаковый bare `TMapper`
не различает владельцев; C# сообщает `CS0121` в производном теле. Эти результаты
не считаются дефектами положительной матрицы.

При ошибочной tuple-лямбде 11 вызовов выбирают базу без diagnostics C#; при
отсутствии local surface 17 вызовов выбирают базу и появляются `CS8620`.
Пропуск обязательного аргумента даёт `CS7036`. Предлагаемая семантическая
защита от ошибочного binding в генераторе здесь не реализована.

Программа требует чистой компиляции producer и завершится ошибкой при
warnings/errors или неверном владельце в положительной матрице. Отрицательные
контроли сохраняются для отдельного просмотра. Это не production integration
harness, полная проверка input contract или проверка совместимости пакетов.
