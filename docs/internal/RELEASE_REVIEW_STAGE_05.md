# Полная проверка Morphant: этап 5

Дата начала: 2026-09-07. План: [RELEASE_REVIEW_PLAN.md](RELEASE_REVIEW_PLAN.md).
Переход к этапу 5 разрешён пользователем после завершения этапа 4.
Статус: аудит выполняется; исправления требуют отдельного согласования.

## Версия и область

Исходная версия: [3352e94](https://github.com/strangeman375/Morphant/commit/3352e940d37820adeaaa59255ba2a2e601432248),
tree `50c860c27c849cad86626ed349d42af252348839`.
Рабочее дерево чистое; remote main проверен. Код продукта в этом этапе
пока не изменяется. Окружение: Linux, SDK 10.0.100, net10.0,
минимальный Roslyn 4.4.0; для различий переноса проверяется Roslyn 4.9.2.
Основные consumer-входы используют C# 9, nullable и warnings-as-errors.

| Область | Что проверяем |
| --- | --- |
| Формы callbacks | Inline, block, method group, delegate; все шесть семейств |
| Связывание | Именно методы Morphant; чужие одноимённые вызовы, ошибочные цепочки и аргументы |
| Декларативное вычисление | Выбранная ветвь, зависимости и число вычислений; неактивные правила, исключения, markers и overlays |
| Перенос C# | Overloads, named/optional arguments, checked, имена, захваты, область видимости, lexical context |
| Граница поддержки | Отказ с диагностикой для неподдерживаемой грамматики; сохранение обычной семантики runtime callbacks |

## Контракт, с которым сравниваем результат

`Construct`, `Resolve` и `Members` требуют inline lambda. Поддерживаются
выражения, initialized locals, полные if/switch-ветви, return и throw.
Независимые member-правила не обещают порядок C# statements. Вычисляются
только выражения выбранной ветви и применимых правил, каждое необходимое
выражение — не более одного раза. Local задаёт явную зависимость.

`ConstructUsing`, `ResolveUsing` и `Convert` принимают обычные синхронные
callbacks, method groups и доступные delegate-члены. Они сохраняют обычный
алгоритм C#, включая порядок, мутацию и исключения. Захваты Configure-locals
недоступны в обоих видах callbacks. Сравниваем с
[declarative mapping](../declarative-mapping.md),
[API](../api/README.md) и диагностическими контрактами MORPH0018, MORPH0029–0033.

## Проверки и результаты

Существующие unit-категории callbacks, transfer, declarative control flow,
values и Convert: 26 passed и один штатный skip collection expressions
на Roslyn 4.4.0. Соответствующие integration-категории, включая lifecycle,
evaluation, runtime construction и typed recovery: 63 passed.
Это новые прогоны на указанной версии, а не результаты этапа 4.

Первый прогон [направленных MSBuild-входов](release-review-stage-05/README.md):
Evaluation — чистый build и 23 успешные проверки. Binding — чистый build,
одно расхождение ниже. Context и Runtime первоначально имели ошибки самих
входов: nullable source у Convert и неверный тип delegate. Они исправлены;
повторный прогон ещё не завершён. Исходные результаты с версией входов:
[results.json](release-review-stage-05/results.json).

## S05-01 — Create пропускает явное правило одноимённого члена

В Binding destination имеет `Destination(int value)` и settable `Value`.
Source.Value = 7; явное правило Members вычисляет 17. Create возвращает 7
без диагностик. Generated Create содержит только конструктор с source.Value;
generated Update содержит полное явное выражение. Чужие имена Auto/Ignore/Map/
Value в этом выражении сохранены корректно: проблема не в их распознавании.

Подготовлены контроли с простым `source.Value + 10`, parameterless destination
и Update(null)/Update(existing). Причина и точная граница уточняются.

Дополнительно подготовлен InheritedBinding: проверка исходной привязки
nonvirtual/static/base-вызовов при переносе callback из базового маппера.
Этот вход ещё не проверен. Полная матрица наследования остаётся этапу 6.

## Продолжение

Выполнить направленные compiler/runtime-проверки и изучить generated output.
Для находок сохранить минимальные воспроизведения, ожидаемое и фактическое
поведение, причину и предложение; отделить дефекты от ограничений контракта.
Переход к этапу 6 ожидает отдельной команды пользователя.
