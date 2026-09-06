# Исправления этапа 4

Дата начала: 2026-09-06. Основание: [аудит](RELEASE_REVIEW_STAGE_04.md)
и разрешение пользователя реализовать общее правило именования и исправить
все находки этапа. Этап 5 не начат.

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
также проверены; итоговый общий прогон ещё предстоит.

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

## Оставшаяся работа

- S04-05: сделать XML-описания tuple-типов различимыми по представлению.
- Выполнить итоговые Release и оба test projects, проверить затронутые
  Roslyn-facing категории на Roslyn 4.9.2 и обновить итог аудита.

## Согласованное представление конфликтующих членов

2026-09-06 пользователь выбрал алиасы. Сохраняются record и `with`.
Конфликтующие имена получают суффикс из подчёркиваний: `Clone_`,
`EqualityContract_`, `DestinationMembers_`. Если destination уже содержит
`Clone_`, имя для `Clone` будет `Clone__`; исходное `Clone_` не меняется.
То же правило применяется при конфликте с generic-параметром member-типа
и к элементам tuple. Имена остальных членов сохраняются.

Алиас используется только в generated Members. Conventions, проверка полноты
и runtime-назначение работают с исходным членом destination; XML связывает
алиас с ним. Проверить явные правила, Auto/Ignore, `with`, nullable/required,
наследование и read-only nested Update, включая уже занятые имена алиасов.
Алиасы внедрены и проверены в указанной выше контрольной точке.
