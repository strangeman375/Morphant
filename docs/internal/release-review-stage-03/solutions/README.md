# Compiler-прототип решений S03-01–S03-03

Обоснование: [выбранные решения](../../RELEASE_REVIEW_STAGE_03_SOLUTIONS.md).

Это исследовательская проверка C# receiver, overload resolution и имён типов.
Она использует настоящие delegates/context-типы Morphant, но тестовые builders
из namespace `Audit` и минимальные construction/member-классы. Не запускает
production generator, не вызывает runtime mapper и не является integration
harness либо снимком production-generated API.

`TypeMapper`/`MappingBuilder` в строковом fixture моделируют необходимую для
binding часть контракта. `Setting()` проверяет сохранение полного builder до и
после callback-вызова. У construction-класса есть обязательный `int`-аргумент;
реальные constructor overloads, marker conversions, типы полей и XML здесь не
моделируются. Их сохранение проверяется production-тестами при внедрении.

## Запуск

Из корня репозитория, с SDK 10.0.100 и доступным NuGet-кешем:

```sh
dotnet restore docs/internal/release-review-stage-03/solutions/SurfaceProbe.csproj --disable-parallel -m:1 -nodeReuse:false
dotnet build docs/internal/release-review-stage-03/solutions/SurfaceProbe.csproj -c Release --no-restore -m:1 -nodeReuse:false
dotnet docs/internal/release-review-stage-03/solutions/bin/Release/net10.0/SurfaceProbe.dll artifacts/release-review/stage-03/solution-matrix
```

В рабочем окружении этой проверки вместо `dotnet` использовать
`/workspace/morphant-tools/dotnet`, который задаёт SDK и расположение кешей.
Single-process MSBuild нужен также при restore: обычный запуск здесь мог
завершаться с code 1 без опубликованных build errors.

Сохранённый [results.json](results.json) — компактный `summary.json` запуска.
Каталог под `artifacts` содержит все исходные consumer fixtures, diagnostics и
полные сведения о binding каждого вызова. Эти воспроизводимые промежуточные
выходы не добавляются в git.

## Что проверяется

- Все 15 перегрузок и явные имена construction/member-типов.
- IVT и отсутствие IVT, source-backed reference, DLL и reference assembly.
- Раздельные причины: старый receiver, только namespace, только маркер,
  выбранное сочетание. Shared, nullable, tuple и unrelated generic families.
- Связанные CRTP-семейства без `base.Configure`: одинаковые и разные
  constraints, вложенная generic-подстановка, разные tuple names,
  `dynamic`/`object` и recursive nullability.
- Non-partial база, её локальное переопределение и независимый shared-маппер.
- Откат ошибочной лямбды к базовому методу, потеря presentation при объединении
  семейств и отсутствие собственного receiver у производного маппера.
- Обязательный аргумент construction-класса.
- Старый producer с неограниченными расширениями и новый consumer.

Входной язык всегда C# 9, nullable enabled; Roslyn 4.4.0. Reference assembly
emitted отдельно с `metadataOnly: true, includePrivateMembers: false`.
Producer и consumer ссылаются на одни и те же типы моделей; модели определены
только в producer. Проверка source-backed reference не означает проверку IDE.

Всего 53 сравнительных компиляции. 31 положительный сценарий выбранной формы
прошёл без предупреждений и ошибок; проверен 601 callback-binding. Остальные
сценарии — сравнительные и отрицательные контроли, а не незакрытые ошибки
положительной матрицы. В частности, `related-generic-nested-baseline` является
чистым контролем старой формы.

`BindingMismatches` сравнивает выбранный receiver с ожидаемым владельцем новой
формы. У старой формы owner записывается как `UNSCOPED`; счётчик сам по себе
не доказывает неправильное поведение baseline. Нативные ошибки сохраняются
отдельно в `Diagnostics`. У номинальных вариантов несовпадения показывают
именно другого владельца: 15 при tuple-deduplication, 11 при ошибочном откате к
базе, 17 при отсутствии локальной поверхности у производного маппера.

Программа требует чистой компиляции producer и завершится ошибкой при
warnings/errors или неверном владельце в положительной матрице. Отрицательные
контроли сохраняются для отдельного просмотра; отсутствие ошибки C# в них
может быть исследуемым дефектом, как в случае отката к базе. Предлагаемая
семантическая проверка генератора в этом прототипе не реализована.
