# Полная проверка Morphant: этап 5

Дата проверки: 2026-09-07. План: [RELEASE_REVIEW_PLAN.md](RELEASE_REVIEW_PLAN.md).
Переход к этапу 5 разрешён пользователем после завершения этапа 4.
Статус: аудит завершён; подтверждены два дефекта. Пользователь согласовал
исправления; выполнение и новые результаты — в
[отчёте об исправлениях](RELEASE_REVIEW_STAGE_05_FIXES.md).
Приведённые ниже результаты относятся к исходной версии до исправлений.

## Версия и область

Исходная версия: [3352e94](https://github.com/strangeman375/Morphant/commit/3352e940d37820adeaaa59255ba2a2e601432248),
tree `50c860c27c849cad86626ed349d42af252348839`.
В начале проверки рабочее дерево чистое, remote main проверен.
Окружение: Linux, SDK 10.0.100, net10.0,
unit-host Roslyn 4.4.0 и 4.9.2.
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

| Проверка | Результат |
| --- | --- |
| Unit: callbacks, transfer, declarative control flow, values, Convert и разбор Configure; Roslyn 4.4.0 | 61 passed, один штатный skip C# 12 collection expressions, без failures |
| Те же unit-категории; Roslyn 4.9.2 | 62 passed, без skips и failures |
| Integration: callbacks, control flow, values, Convert, lifecycle, evaluation и typed recovery | 63 passed, без skips и failures |
| Сборка unit-проекта после возврата reference Roslyn 4.4.0 | Release, без предупреждений и ошибок |

Это новые прогоны на указанной версии, а не результаты этапа 4.

[Направленные MSBuild-входы](release-review-stage-05/README.md):
15 конфигураций, девять чистых сборок и шесть ожидаемых диагностических
отказов. Выполнены 83 runtime-проверки: 71 успешная, 12 расхождений
относятся к двум дефектам ниже. Context и Runtime первоначально имели ошибки
входов (nullable source у Convert и тип delegate); исправленные варианты
прошли 11 и 22 проверки. Evaluation прошёл 23 проверки. Полные значения,
команды, SHA-256 проверенных входов и отдельный исторический прогон:
[results.json](release-review-stage-05/results.json).

## Проверенная грамматика и границы поддержки

Таблица описывает подтверждённые формы и ограничения; это не обещание
поддержки произвольного синтаксиса C# внутри декларативного callback.

| Область | Поддерживаемые формы и ограничения |
| --- | --- |
| `Configure` | Безусловная последовательность настроек и fluent-цепочек `Map`. Передача или сохранение root builder, условные регистрации дают MORPH0017; нарушение цепочки пары — MORPH0018. Чужие одноимённые API вне цепочки не участвуют в анализе. |
| `Construct`, `Resolve`, `Members` | Inline lambdas, в том числе обёрнутые приведением или скобками; выражения, initialized locals, полные `if`/`switch`, `return`, `throw`, `with` overlays. Method group или готовый delegate вместо inline lambda даёт MORPH0029. |
| Декларативное вычисление | Выбранная ветвь и применимые правила; общая local-зависимость вычисляется один раз. Неиспользуемые выражения и неактивное `init`-правило не выполняются. Порядок независимых правил не задан контрактом. |
| Markers и вложенный mapping | `Auto`, `Ignore`, `Value`, `Map`, `Create`, `Update` и декларативный `context` допустимы в предусмотренных контрактом позициях. Runtime-использование и неподдерживаемое вложение диагностируются MORPH0033. Чужие одноимённые обычные вызовы внутри выражения сохраняются. |
| `ConstructUsing`, `ResolveUsing`, `Convert` | Inline expression/block lambda, method group, совместимый delegate, anonymous method. Обычные циклы, мутация, `try`/`finally`, исключения и порядок statements сохраняются. Delegate должен соответствовать конкретной сигнатуре Morphant; source у `Convert` может быть nullable. |
| Захваты | Runtime-значения и local functions из `Configure` недоступны: MORPH0030. Константы и `nameof` не требуют такого захвата. Deferred source-capture допустим; захваты декларативных `previous`, `result` и `context` ограничены, допустимые snapshots проверены отдельно. |
| Неподдерживаемые statements | В декларативных callbacks циклы, `try` и другие неподдерживаемые statements дают MORPH0031. Мутация `previous`, `result` или отслеживаемого alias даёт MORPH0032. |
| Контекст переноса | Проверены aliases, `nameof`, extension invocation, overloads, named/optional arguments, caller-info, checked/unchecked, unsafe и предупреждения исходного контекста. Обычные LINQ queries и deferred local functions проверены integration-сценариями. |
| Ограничения связывания | Проверенные reduced extension method group и custom query pattern отклоняются с MORPH0030. Явный `base.` в импортированном callback отклоняется с MORPH0028. Перенос доступного невиртуального вызова имеет дефект S05-02. |

Основания: новые [MSBuild-входы](release-review-stage-05/README.md),
[MapperConfigurationTests](../../src/tests/Morphant.Generator.UnitTests/MapperConfigurationTests),
[CallbackDiagnosticsTests](../../src/tests/Morphant.Generator.UnitTests/CallbackDiagnosticsTests),
[ExpressionTransferTests](../../src/tests/Morphant.Generator.UnitTests/TypeMapperExpressionTransferTests.cs)
и [integration control flow](../../src/tests/Morphant.Generator.IntegrationTests/TypeMapperDeclarativeControlFlowTests).
Невалидное исходное связывание, полностью объясняемое компилятором C#,
не обязано дублироваться диагностикой Morphant; suppression проверенных
диагностик сохраняет типизированное восстановление.

Описания MORPH0028 и MORPH0030 стоит уточнить при проверке документации:
доступность helper сама по себе не разрешает импортированный `base.`-вызов,
а custom query pattern и extension method group имеют отдельные ограничения.
Эти случаи уже отклоняются с диагностикой и не засчитываются как молчаливая
потеря конфигурации.

## S05-01 — Create пропускает явное правило одноимённого члена

Минимальная форма правила при `Destination(int value)` и settable `Value`:

```csharp
builder.Map<Source, Destination>()
    .Members(source => new() { Value = source.Value + 10 });
```

В Binding destination имеет `Destination(int value)` и settable `Value`.
Source.Value = 7; явное правило Members вычисляет 17. Create возвращает 7
без диагностик. Generated Create содержит только конструктор с source.Value;
generated Update содержит полное явное выражение. Чужие имена Auto/Ignore/Map/
Value в этом выражении сохранены корректно: проблема не в их распознавании.

Простое `source.Value + 10` воспроизводит тот же дефект. Автоматический выбор
конструктора и `Construct(source => new(ByConvention()))` возвращают 7 при Create
и Update(null), 17 при Update(existing). Конструктор без параметров
и явное `Construct(source => new(source.Value))` возвращают 17 во всех
трёх операциях.

Причина: [ConventionConstructorMappingPlanner.BuildPlan](../../src/Morphant.Generator/TypeMapperGeneration/ConventionConstructorMappingPlanner.cs) отбрасывает
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
эквивалентность нового связывания.
[ConstructExpressionRewriter](../../src/Morphant.Generator/TypeMapperGeneration/ConstructExpressionRewriter.cs)
оставляет простой instance-вызов в новом lexical scope;
[TypeMapperTransferValidator](../../src/Morphant.Generator/TypeMapperGeneration/TypeMapperTransferValidator.cs)
проверяет ошибки компиляции, которых при выборе другого допустимого метода нет.
Предложение: сохранять исходный symbol невиртуального вызова; когда корректный
перенос недоступен, сообщать диагностику. Добавить регрессии для скрывающих
методов, перегрузок и instance-членов, сохранив virtual dispatch.
Полная матрица наследования и настроек остаётся этапу 6.

## Ограничения проверки и продолжение

Проверены выбранные compiler/integration-категории и направленные consumers;
полный тестовый набор, реальная IDE, другие ОС и потребление нового NuGet
в этом этапе не запускались. Новые runtime-воспроизведения выполнены
компилятором 5.0.0 из SDK 10.0.100; результаты unit-host 4.4.0/4.9.2 не выдаются за
повтор этих MSBuild-входов на старых SDK.

Полная матрица настроек и наследования остаётся этапу 6, кортежей — этапу 7,
диагностик — этапу 9, актуализации и IDE — этапу 10. Проверенные здесь
пересечения не заменяют эти этапы.

Рекомендация: согласовать и исправить S05-01 и S05-02 перед этапом 6,
поскольку оба дефекта молча меняют результат маппинга. В обоих случаях
нужны постоянные compiler- и runtime-регрессии, затем повтор затронутых
категорий и этих воспроизведений. Исправления не выполнены;
этап 6 не начат и ожидает отдельной команды пользователя.
