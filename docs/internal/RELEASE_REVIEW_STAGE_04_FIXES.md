# Исправления этапа 4

Дата начала: 2026-09-06. Основание: [аудит](RELEASE_REVIEW_STAGE_04.md)
и разрешение пользователя реализовать общее правило именования и исправить
все находки этапа. Исправления завершены 2026-09-06; итоговые проверки ниже.
Этап 5 не начат.

## Контрольная точка: компактные namespaces

Обычные и tuple destination-типы переведены на согласованный
`Morphant.Generated.N_<id>`. Короткие имена типов и hint-файлы сохранены.
Обновлены буквальные snapshots и явные имена в compiler, integration,
package consumers и исследовательских входах.

Для обычного destination ключ использует полное metadata-имя definition:
namespace, содержащие типы, generic arity и имя самого destination.
Eligibility по-прежнему требует однозначного глобального имени типа.
Для кортежей переиспользуется существующая полная идентичность контракта
и представления из hint identity. Общий хеш дополнительно включает простое
имя и public key token генерирующей сборки. Старый assembly-encoder удалён.

Распознавание read-only вложенного Update у System.Tuple теперь проверяет
destination связанного конфигурационного вызова, а не префикс namespace.

Проверки: Roslyn 4.4.0, C# 9, SDK 10.0.100, Linux.
`GeneratedPlanNamingUsageTests`: 15 passed. Проверены явные имена,
пунктуация/Unicode/подпись сборки, IVT с source/DLL/reference assembly,
смена версий сборки и зависимости, перестановка регистраций.
Затронутые полные surface snapshots и compiler-проверки вложенного маппинга
также прошли; общий прогон описан в итоговой проверке ниже.

## Контрольная точка: S04-01–S04-04

- S04-01: имена членов больше не отбрасываются из conventions и проверки
  полноты. В generated Members реализованы согласованные алиасы; исходные
  имена сохраняются в runtime-назначениях и наблюдениях диагностики.
- S04-02: три примера Resolve используют block body. В справочнике объяснено
  явное имя construction-типа для conditional expression с ветвью previous.
- S04-03: static/ref-like контейнер имени проверяется отдельно от типа,
  который передаётся generic-аргументом. Сам static/ref-like root не становится
  допустимым аргументом. Проверены вложенные обычные типы и generic-контейнеры.
- S04-04: TryGetValue распознаётся по связанному методу Option и конкретному
  параметру previous. Сохраняются присваивание out, его область видимости,
  nullable flow, short-circuit и число вычислений.

Направленные unit-проверки: восемь новых usage/diagnostic случаев прошли
на Roslyn 4.4.0, включая required/AllowNull в C# 11 и отрицательный контроль
чужого TryGetValue. Три полных MemberSurfaceNaming snapshots прошли;
сценарий только с reserved names теперь требует непустую корректную поверхность.
Четыре выбранных integration-теста прошли: алиасы/with/Auto/Ignore/read-only
Update/tuple, static-контейнеры, прежние arbitrary types и четыре формы
TryGetValue. Отдельный MSBuild consumer с TryGetValue и && прошёл все четыре
runtime-проверки Create/reuse/Update(null)/replacement.

## Контрольная точка: XML-описания tuple-типов

S04-05: описания Construction, ConstructorParameters и Members показывают
конкретное tuple-представление, включая имена элементов, вложенность,
nullable и generic-аргументы. Например, `(int Id, string Name)` теперь
отличимо от `(int Code, string Label)` непосредственно в summary.
Обычные destination-типы сохраняют ссылки на пользовательский тип.

TupleSurfaceTests: 12 passed на Roslyn 4.4.0; буквальные снимки охватывают
18 tuple-представлений, включая длинные и Unicode-имена. Сигнатуры и
hint-файлы этим изменением не затронуты. Реальная IDE остаётся этапу 10.

## Итоговая проверка

Проверенная версия production-кода и постоянных тестов:
[6dd4582](https://github.com/strangeman375/Morphant/commit/6dd458295b2d4b193d01141ddedd5eea887b9c94),
tree `1b8edb76813a9752ffe71e6eeed3d99770000243`. Итоговый документационный commit не меняет этот код.
Окружение: Linux, SDK 10.0.100, net10.0; минимальный Roslyn 4.4.0,
новый проверенный host 4.9.2. Обычные входы используют C# 9;
required-сценарии — C# 11.

| Проверка | Результат |
| --- | --- |
| Полная Release-сборка solution | 0 warnings, 0 errors |
| Все unit-тесты, Roslyn 4.4.0 | 846 passed, 1 штатный skip, 0 failed |
| Все integration-тесты | 271 passed, 0 skipped/failed; включая NuGet consumer и MSBuild fixtures |
| Затронутые unit-категории, Roslyn 4.9.2 | 143 passed, 0 skipped, 0 failed |
| Исходные MSBuild-воспроизведения | 15 положительных конфигураций, 136 успешных runtime assertions |
| Контроли C# target-typed conditional | 2 ожидаемых CS1729; правило вывода типа C# не менялось |
| Три буквальных Resolve-примера из Markdown | Чистый C# 9 build и 12 runtime assertions |

Штатный пропуск на Roslyn 4.4.0 относится к collection expressions C# 12.
После проверки нового host восстановлены зависимости и Release build
минимального Roslyn. Данные, список категорий и SHA-256 извлечённых примеров:
[fix-results.json](release-review-stage-04/fix-results.json).
Исторические отказы сохранены отдельно в `results.json`.

Naming consumer по-прежнему выдаёт 22 файла и 16 вспомогательных типов:
изменение namespaces не добавило дубликатов. Namespace каждого такого типа
имеет три сегмента и длину 53 символа. Полное имя OrderConstruction и
TupleConstruction занимает 71 символ без generic-аргументов.

При миграции нужно обновить явные `using`, aliases и полные имена generated-типов.
Короткие имена и обе формы `new` сохраняются. Для конфликтующих членов
явный Members использует согласованные алиасы; runtime-имена destination
не меняются.

S04-01–S04-05 закрыты в проверенной области. Реальные IDE и новый multi-OS CI
не проверены этим прогоном; emitted XML и compiler-проверки не заменяют IDE.
Матрицы последующих этапов остаются в плане. Этап 5 не начат.

## Согласованное представление конфликтующих членов

2026-09-06 пользователь выбрал алиасы. Сохраняются record и `with`.
Конфликтующие имена получают суффикс из подчёркиваний: `Clone_`,
`EqualityContract_`, `DestinationMembers_`. Если destination уже содержит
`Clone_`, имя для `Clone` будет `Clone__`; исходное `Clone_` не меняется.
То же правило применяется при конфликте с generic-параметром member-типа
и к элементам tuple. Имена остальных членов сохраняются.

Алиас используется только в generated Members. Conventions, проверка полноты
и runtime-назначение работают с исходным членом destination; XML связывает
алиас с ним. Проверены явные правила, Auto/Ignore, `with`, nullable/required,
наследование и read-only nested Update, включая уже занятые имена алиасов.
