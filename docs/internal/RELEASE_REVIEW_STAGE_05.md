# Полная проверка Morphant: этап 5

Дата начала: 2026-09-07. План: [RELEASE_REVIEW_PLAN.md](RELEASE_REVIEW_PLAN.md).
Переход к этапу 5 разрешён пользователем после завершения этапа 4.
Статус: аудит выполняется; исправления требуют отдельного согласования.

## Версия и область

Исходная версия: [3352e94](https://github.com/strangeman375/Morphant/commit/3352e940d37820adeaaa59255ba2a2e601432248),
tree `50c860c27c849cad86626ed349d42af252348839`.
Рабочее дерево чистое; remote main проверен. Код продукта в этом этапе
пока не изменяется. Окружение: Linux, SDK 10.0.100, net10.0,
unit-host Roslyn 4.4.0; дополнительно проверяется Roslyn 4.9.2.
MSBuild consumers используют compiler 5.0.0 из SDK 10.0.100;
генератор собран с reference Roslyn 4.4.0. Эти host-проверки различаются.
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
на Roslyn 4.4.0. Разбор Configure и builder flow: ещё 35 passed.
Соответствующие integration-категории, включая lifecycle,
evaluation, runtime construction и typed recovery: 63 passed.
Это новые прогоны на указанной версии, а не результаты этапа 4.

[Направленные MSBuild-входы](release-review-stage-05/README.md):
15 конфигураций, девять чистых сборок и шесть ожидаемых диагностических
отказов. Выполнены 83 runtime-проверки: 71 успешная, 12 расхождений
относятся к двум дефектам ниже. Context и Runtime первоначально имели ошибки
входов (nullable source у Convert и тип delegate); исправленные варианты
прошли 11 и 22 проверки. Evaluation прошёл 23 проверки. Полные значения,
команды, SHA-256 проверенных входов и отдельный исторический прогон:
[results.json](release-review-stage-05/results.json).

## S05-01 — Create пропускает явное правило одноимённого члена

В Binding destination имеет `Destination(int value)` и settable `Value`.
Source.Value = 7; явное правило Members вычисляет 17. Create возвращает 7
без диагностик. Generated Create содержит только конструктор с source.Value;
generated Update содержит полное явное выражение. Чужие имена Auto/Ignore/Map/
Value в этом выражении сохранены корректно: проблема не в их распознавании.

Простое `source.Value + 10` воспроизводит тот же дефект. Bare mapping
и `Construct(source => new(ByConvention()))` возвращают 7 при Create
и Update(null), 17 при Update(existing). Конструктор без параметров
и явное `Construct(source => new(source.Value))` возвращают 17 во всех
трёх операциях.

Причина: `ConventionConstructorMappingPlanner.BuildPlan` отбрасывает
совпавшее initializer-назначение, если оно не требуется для required-member.
Этот фильтр не различает convention и `ExplicitValueExpression`.
Соседний `BuildExplicitPlan` уже сохраняет явное выражение.
Предложение: ограничить исключение назначения автоматическими правилами
и добавить Create/Update(null)/Update(existing)-регрессии для обоих путей.

## S05-02 — перенос меняет невиртуальный вызов базового mapper

InheritedBinding имеет `BaseMapper.Read() => "base"`, а конечный mapper
скрывает его через `new Read() => "derived"`. Обычный метод, объявленный
в BaseMapper, возвращает `"base"`. Унаследованные через IncludeBase callbacks
всех шести семейств возвращают `"derived"`: шесть расхождений при чистой
компиляции. Generated-код содержит `Read()` уже в производном классе.

Контроли сохраняют исходную привязку static-метода и ожидаемый virtual
dispatch. Явный `base.ReadVirtual()` в импортируемом callback отдельно
отклоняется с MORPH0028: этот запрет прямо реализован в проверке переноса.
Он не засчитывается как молчаливое изменение вызова.

Причина: при переносе проверяется доступность исходного члена, но не
эквивалентность нового связывания. `ConstructExpressionRewriter` оставляет
простой instance-вызов в новом lexical scope; `TypeMapperTransferValidator`
проверяет ошибки компиляции, которых при выборе другого допустимого метода нет.
Предложение: сохранять исходный symbol невиртуального вызова; когда корректный
перенос недоступен, сообщать диагностику. Добавить регрессии для скрывающих
методов, перегрузок и instance-членов, сохранив virtual dispatch.
Полная матрица наследования и настроек остаётся этапу 6.

## Продолжение

Выполнить направленные compiler/runtime-проверки и изучить generated output.
Для находок сохранить минимальные воспроизведения, ожидаемое и фактическое
поведение, причину и предложение; отделить дефекты от ограничений контракта.
Переход к этапу 6 ожидает отдельной команды пользователя.
