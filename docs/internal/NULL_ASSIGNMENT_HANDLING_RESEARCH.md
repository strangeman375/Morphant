# Исследование null-assignment policy и patch/merge

Статус: исследование зафиксировано 2026-08-02; возможность полностью отложена
до периода после v0. Документ не задаёт принятый public API. Он сохраняет
пользовательские сценарии, найденные ограничения, сравнение с другими
мапперами и наиболее сильное рабочее направление, чтобы после v0 не повторять
исследование с нуля.

## 1. Что именно исследовалось

Исходная потребность — не повторять условный `Ignore()` для каждого member-а
при partial update:

```csharp
.Members((source, _) => new()
{
    Name = source.Name is null ? Ignore() : source.Name,
    Email = source.Email is null ? Ignore() : source.Email,
    Phone = source.Phone is null ? Ignore() : source.Phone
});
```

Рассматривалась общая policy, которая решает, что делать, если вычисленное для
destination member-а значение равно `null`:

```csharp
public enum NullAssignmentHandling
{
    Default = 0,

    Assign,
    Ignore,
    Throw
}
```

`NullAssignmentHandling` — рабочее, но наиболее точное из рассмотренных имён.
Старое направление `IgnoreNullSourceValues` уже по смыслу:

- policy может проверять не сырой source member, а явное выражение;
- для `Map(...)` важен результат nested mapping-а;
- `Throw` не выражается boolean-настройкой `Ignore...`;
- итоговое решение относится к assignment, а не к root source-null handling.

Эту policy нельзя смешивать с существующими настройками:

- `NullSourceHandling` решает судьбу всего mapping-а при `source == null`;
- `NullDestinationHandling` нормализует отсутствующий previous в
  `Update`;
- nullable-совместимость automatic mapping-а является фиксированной
  compile-time границей: разрешены только warning-free implicit C#-
  преобразования, а explicit expressions проверяет compiler;
- null-assignment policy является runtime правилом для отдельного member
  candidate-а.

## 2. Пользовательские сценарии

Одинаковая проверка `candidate is null` скрывает несколько разных намерений.
Их нужно сохранить раздельно при возвращении к feature.

| Сценарий | Ожидание от `null` | Естественный baseline |
|---|---|---|
| Полное создание нового объекта | Member результата становится `null` | Созданный объект не считается значимым состоянием для merge |
| Создание поверх destination defaults | Не затирать constructor/property/factory defaults | Новый объект после result policy |
| Полное обновление существующего объекта | Очистить member | Выбранный existing result |
| Patch/merge существующего объекта | Сохранить старое значение | Выбранный existing result |
| Строгий `Create` / `Update` | Считать неожиданный `null` ошибкой | Неважно: assignment не выполняется |
| Presence-aware patch | Различать absent, value и explicit `null` | Нужен отдельный source contract |

Основной и бесспорный сценарий `Ignore` — patch существующего объекта:

```csharp
var customer = mapper.Map(update, existingCustomer);
```

Но у `Create` тоже есть реальная потребность: DTO конфигурации из JSON/YAML
часто содержит nullable поля, а destination задаёт осмысленные defaults.

```csharp
public sealed class AppSettings
{
    public int Timeout { get; set; } = 30;
    public string Region { get; set; } = "eu";
}
```

В таком сценарии пользователь может ожидать, что `null` в DTO оставит `30` и
`"eu"`. Это не обычное полное преобразование, а операция «создать baseline с
defaults и наложить присутствующие значения».

## 3. Рассмотренные границы policy

### 3.1. Только convention / `Auto()` или все generated rules

Первый вариант применял `Ignore` только к automatic mappings, а явное
выражение всегда считал намеренным assignment-ом. Это удобно для очистки:

```csharp
Description = source.Description
```

Но плохо покрывает обычные patch DTO:

```csharp
Email = source.EmailAddress
```

Переименование не меняет пользовательское намерение. Если global policy
перестаёт работать только из-за explicit expression, пользователю снова
приходится писать условный `Ignore()` для каждого renamed или вычисляемого
member-а.

Рабочий вывод: policy должна применяться ко всем member rules, значение которых
генерирует Morphant:

- convention;
- `Auto()`;
- explicit expression;
- результат `Map(...)`.

Для намеренного исключения из общей policy нужен per-member override.

### 3.2. Только `Update` или оба режима

Аргумент за `Update`-only: именно здесь `Ignore` имеет очевидную
patch/merge-семантику — сохранить состояние переданного destination. Для
обычного `Create` наиболее естественно получить полное состояние из source, а
не зависеть от скрытого property initializer-а destination.

Аргумент за оба режима: явно включённое правило с названием
`NullAssignmentHandling.Ignore` естественно читается одинаково — если candidate
равен `null`, assignment не выполняется. Различается только baseline, значение
которого сохраняется. Кроме того, сохранение defaults при создании — реальный
пользовательский сценарий, подтверждённый поведением Mapster и запросами к
Mapperly.

Наиболее сильное рабочее направление к моменту отсрочки:

1. Policy семантически доступна и для `Create`, и для `Update`.
2. Default для обоих путей остаётся `Assign`.
3. Пользователь может задать разные effective values для `Create` и `Update`.
4. Простая настройка без scope применяется к обоим путям.

Это направление ещё не принято как контракт. После v0 его нужно повторно
проверить на новом declarative API и на законе нормализации previous.

## 4. Рабочая форма настройки

Базовый эскиз:

```csharp
builder.Map<Dto, Entity>()
    .NullAssignmentHandling(NullAssignmentHandling.Ignore);
```

Mode-specific эскиз для наиболее распространённой комбинации:

```csharp
builder.Map<Dto, Entity>(MappingMode.CreateAndUpdate)
    .NullAssignmentHandling(
        MappingMode.Update,
        NullAssignmentHandling.Ignore);
```

Ожидаемый результат:

```text
Create      -> Assign
Update      -> Ignore
```

Точный syntax не принят. В частности, после v0 нужно решить:

- допустим ли `MappingMode.CreateAndUpdate` во втором аргументе;
- нужны ли две именованные настройки вместо overload-а с flags;
- можно ли задавать operation-specific value на mapper root и assembly level;
- как совместить обычный inheritance precedence с mode-specific override;
- должно ли отсутствие previous выбирать `Create`-policy независимо от
  `MappingContext.Operation`.

Обычный settings precedence Morphant должен сохраниться:

```text
map -> mapper root -> assembly -> library default
```

Внутри одного уровня mode-specific override логично считать точнее общего
значения. Per-member override должен быть самым конкретным правилом. Однако
полная precedence matrix остаётся post-v0 решением.

## 5. Рабочая runtime-семантика

После выбора или создания `result` member rule концептуально lowering-ится так:

```csharp
var candidate = EvaluateMemberRuleOnce();

switch (effectiveHandling)
{
    case NullAssignmentHandling.Assign:
        result.Member = ConvertCandidate(candidate);
        break;

    case NullAssignmentHandling.Ignore:
        if (candidate is not null)
        {
            result.Member = ConvertCandidate(candidate);
        }
        break;

    case NullAssignmentHandling.Throw:
        if (candidate is null)
        {
            throw CreateNullAssignmentException();
        }

        result.Member = ConvertCandidate(candidate);
        break;
}
```

Это только концептуальный код. Конкретная exception type, message и оптимальное
lowering должны быть согласованы вместе с diagnostics/observable failures.

### 5.1. Что сохраняет `Ignore`

`Ignore` сохраняет значение member-а на уже выбранном `result`:

| Путь | Сохраняемое значение |
|---|---|
| `Create` | Значение после structured/runtime result policy либо convention creation |
| `Update`, result = previous | Старое значение previous |
| `Update`, `Resolve` / `ResolveUsing` выбрал replacement | Значение replacement-result |
| No-previous ветка после нормализации `null` destination | Значение нового baseline |

Последний случай требует отдельного решения. Ранее рабочим законом было:

```csharp
Map(source, destination: null) == Map(source)
```

Текущий target design уже делает эти вызовы неразличимыми внутри declarative
DSL: оба передают `Option.None`. Поэтому наиболее согласованно использовать
`Create`-oriented effective policy в обеих no-previous ветках. Но публичная
операция второго вызова остаётся `Update`, и это нужно явно учесть при
проектировании mode-specific API, а не получить случайно из implementation.

### 5.2. Где policy действует

Рабочая граница:

- только generated member assignments declarative pipeline-а;
- convention, `Auto()`, explicit expression и `Map(...)` подчиняются одному
  effective rule;
- constructor parameters и сами structured `Construct` / `Resolve` не являются
  member assignments и не подчиняются policy;
- `Ignore()` остаётся безусловным отсутствием конкретного assignment;
- `Convert` полностью обходит setting, как и остальные declarative
  settings;
- runtime `ConstructUsing` / `ResolveUsing` сами по себе policy не обходят: она
  всё ещё может применяться к последующему `Members`/convention stage;
- в старом production API эквивалентный raw/manual template не должен получать
  скрытое generated поведение.

## 6. Per-member overrides и условия

Global `Ignore` без исключения не позволяет намеренно очистить отдельное поле.
Рабочие имена marker-ов:

```csharp
.Members((source, _) => new()
{
    // Следует effective policy.
    Name = source.Name,

    // Присваивает candidate даже при null.
    Description = Assign(source.Description),

    // Пропускает assignment при null независимо от effective policy.
    Phone = IgnoreIfNull(source.Phone)
});
```

`Assign(...)` и `IgnoreIfNull(...)` являются только эскизами. Их names,
generic shape и необходимость отдельного `IgnoreIfNull` нужно проверить после
v0. Оба wrapper-а обязаны вычислять внутреннее выражение ровно один раз.

Для произвольного runtime-условия уже достаточно declarative control flow:

```csharp
Status = source.ChangeStatus
    ? source.Status
    : Ignore()
```

Ветка также позволяет не запускать дорогое вычисление:

```csharp
Photo = source.PhotoId is not null
    ? LoadPhoto(source.PhotoId.Value)
    : Ignore()
```

Это соответствует полезному различию AutoMapper:

- `Condition` проверяется после получения member value;
- `PreCondition` выполняется до source resolution и может предотвратить
  дорогое вычисление.

Morphant не обязательно нужны два отдельных condition API: выбранная ветка с
`Ignore()` уже выражает precondition обычным C#, а null-assignment policy
проверяет готовый candidate.

Whole-plan runtime no-op — отдельный вопрос. До отсрочки не было принято,
должен ли появиться first-class marker для него или сложный случай должен
остаться в `Convert`.

## 7. `null` не равен отсутствию поля

Обычный `T?` кодирует только значение, но не три patch-состояния:

```text
поле отсутствовало
поле присутствовало со значением
поле присутствовало с null
```

Ни один mapper не может восстановить presence после того, как serializer
поместил оба первых nullable-состояния в одно значение `null`. Нужен
source-owned contract. Для него можно использовать общий `Option<T>` Morphant
либо domain-specific wrapper:

```csharp
.Members((source, _) => new()
{
    Name = !source.Name.HasValue
        ? Ignore()
        : Assign(source.Name.Value)
});
```

Семантика:

- `Option.None` — assignment отсутствует;
- `Option.Some(value)` — присвоить значение;
- `Option.Some(null)` — намеренно очистить nullable member даже при global
  `Ignore`.

Наличие public `Option<T>` не делает его обязательным transport type и не
восстанавливает field presence автоматически. Serializer, GraphQL layer либо
сам source type должны явно сформировать `None` / `Some`; DSL также должен
позволять естественно использовать сторонний presence wrapper.

## 8. Порядок вычисления и nullable-граница

### 8.1. Candidate вычисляется ровно один раз

Недопустимое lowering:

```csharp
if (CreateName(source) is not null)
{
    result.Name = CreateName(source);
}
```

Правильная форма:

```csharp
var name = CreateName(source);

if (name is not null)
{
    result.Name = name;
}
```

Это важно для getters, пользовательских methods, nested mappings, exceptions и
любых side effects. Mapster issue #737 показывает реальную проблему двойного
вызова configured mapping expression при `IgnoreNullValues`.

### 8.2. Проверка выполняется до потери null-информации

Для `int? -> int` нельзя сначала превратить `null` в `0`, а затем применять
policy. После conversion mapper уже не отличит отсутствие значения от
настоящего нуля.

Рабочий pipeline:

```text
member expression / nested result
-> null-assignment policy
-> destination conversion
-> assignment
```

AutoMapper issue #2999 демонстрирует противоположный порядок: `Condition`
получал уже преобразованный `int`, поэтому исходный `null` выглядел как `0`.

Если explicit пользовательское выражение само превращает `null` в непустое
значение, policy видит его итоговый результат:

```csharp
Name = source.Name ?? "Unknown"
```

Если нужно проверить source до запуска converter или nested mapping-а,
пользователь задаёт условную ветку с `Ignore()`.

### 8.3. Nullable и oblivious формы

При возвращении к feature нужно отдельно покрыть:

- nullable reference candidate;
- `Nullable<T>`;
- oblivious reference из disabled nullable context;
- nullable generic type parameter;
- boxing `Nullable<T>`;
- flow attributes `MaybeNull`/`NotNull`.

Policy проверяет runtime value. Nullable annotations определяют compile-time
warnings и применимость diagnostics, но oblivious reference всё равно может
оказаться `null` во время выполнения.

## 9. Nested mappings и collections

Для:

```csharp
Address = Map(source.Address)
```

сначала полностью выполняется nested pair со своим `NullSourceHandling`,
`NullDestinationHandling` и mapping algorithm. Внешняя null-assignment policy
проверяет один раз именно возвращённый nested result. Она не должна заранее
считать `source.Address == null` отсутствующим, потому что nested mapping может
вернуть previous, replacement, fallback либо бросить exception.

Policy относится к destination member-у целиком:

```csharp
Orders = null
```

`Ignore` здесь означает сохранить текущую/default collection reference. Он не
означает автоматически:

- игнорировать `null` elements;
- merge элементов;
- сохранить отдельные existing elements;
- считать empty collection отсутствующей;
- выполнить key-based reconciliation.

Mapster issue #439 показывает, что распространение ignore-null с element/object
mapping на mapping collection как целого вызывает отдельные ожидания и ошибки.
Полная collection policy остаётся отдельным post-v0 этапом 9.

## 10. Почему `Create + Ignore` технически сложен

Проблемы ниже не доказывают, что `Create` нужно исключить. Они показывают, где
Morphant не может честно обещать «пропустить assignment» и должен либо
ограничить support boundary, либо потребовать явный fallback.

### 10.1. Обычные setters и mutable fields

Здесь проблема отсутствует:

```csharp
var result = new Destination();
var name = source.Name;

if (name is not null)
{
    result.Name = name;
}
```

При `null` сохраняется настоящий constructor/property default. То же относится
к уже созданному runtime result из `ConstructUsing` / `ResolveUsing` с
доступным setter-ом.

### 10.2. Обязательный constructor parameter нельзя пропустить

```csharp
public Destination(string name)
```

Если `name` равен `null`, объект ещё не существует и baseline member-а
сохранить невозможно. Передавать `default!` молча нельзя: это не `Ignore`, а
выдуманный mapper-ом fallback, который может нарушить invariant конструктора.

Разумная будущая граница:

- optional parameter с настоящим объявленным default можно опустить;
- required parameter требует explicit fallback, другого constructor-а,
  `ConstructUsing` / `ResolveUsing` либо `Convert`;
- невозможный declarative plan должен давать diagnostic.

Mapster issue #707 показывает, насколько легко смешать три разных смысла для
ignored constructor parameter: использовать declared default, синтетический
`default(T)` или сохранить existing destination. Для Morphant они не должны
становиться одним неявным поведением.

Mapster issue #811 относится к соседней, но не идентичной проблеме: `null`
constructor argument абстрактного типа запускал попытку создать abstract
destination. Он дополнительно подтверждает, что null-policy constructor stage
нельзя выводить из member-assignment policy.

### 10.3. `init` нельзя условно назначить после создания

Для init-only property невозможно написать:

```csharp
var result = new Destination();

if (name is not null)
{
    result.Name = name;
}
```

Object initializer также не поддерживает runtime-условное присутствие целой
строки assignment-а. Общие обходы неудовлетворительны:

- отдельный initializer для каждой комбинации nullable members даёт до
  `2^N` веток;
- предварительное вычисление всех значений может изменить порядок constructor,
  setters, exceptions и side effects;
- reflection/unsafe нарушает обычную C#-семантику `init`;
- `with` подходит только части record-сценариев и создаёт новый result.

Mapperly issue #2178 возник именно на config/JSON/YAML DTO с nullable полями и
non-required init-properties с defaults. Issue остаётся открытым и помечен как
bug и breaking change: пользовательское ожидание реально, но исправление меняет
семантику и не выражается простым условием внутри object initializer.

### 10.4. `required` contract

Если выбранный constructor не помечен `[SetsRequiredMembers]`, compiler требует
закрыть required member при создании. Условно пропустить assignment нельзя даже
при наличии property initializer-а.

Будущая граница:

- constructor/factory, уже удовлетворяющий required contract, предоставляет
  baseline;
- иначе `Create + Ignore` несовместим с potentially-null creation-time rule;
- это ограничение не переносится на настоящий `Update`, где объект уже
  создан.

Mapperly issue #1569 показывает обратную ошибку: creation-time required
validation была применена к existing-target mapping, хотя generated mutation
уже была корректной.

### 10.5. Get-only members и positional records

```csharp
record Destination(string Name);
```

Для get-only/positional member-а нет post-construction assignment-а. Если
constructor требует значение, независимого baseline не существует. Нужны
explicit fallback, reconstruction/clone strategy или manual mapping.

Эта проблема пересекается с отдельным этапом 11 об immutable `Update`, но
null-assignment policy не должна сама вводить hidden clone/reconstruction.

### 10.6. Factory и derived runtime type

Factory может вернуть instance с defaults, cached object или derived runtime
type. `Ignore` для обычного setter-а должен сохранять состояние именно этого
instance. Но generator не получает право переписывать его init-only/get-only
state или заменять его новым объектом без отдельной creation/reconstruction
strategy.

### 10.7. Порядок object initializer-а

Текущий target design сохраняет естественный C#-порядок: constructor, затем
поочерёдное вычисление и assignment explicit initializer members. Попытка
заранее вычислить все nullable candidates ради выбора одной initializer-ветки
может наблюдаемо изменить этот порядок. Будущая реализация policy не должна
ломать уже принятый evaluation law.

### 10.8. Nullability diagnostics

`Ignore` может сделать nullable-to-non-nullable mapping безопасным в runtime,
но только если baseline уже удовлетворяет invariant destination. Для
`Update` это ответственность существующего instance; для `Create` она
зависит от constructor/factory/initializer-а.

Поэтому нельзя автоматически считать любой nullable mismatch устранённым:

- `Ignore` защищает assignment;
- он не доказывает, что новый destination уже содержит valid non-null value;
- compile-time diagnostic должен учитывать creation capability и required
  contract.

## 11. Что делают другие мапперы

### 11.1. Mapster

Mapster предоставляет `IgnoreNullValues(true)` как merge-policy и отдельно
`IgnoreIf(...)` для произвольного условного пропуска. Его документация явно
описывает копирование только непустых input values.

Тест `WhenMappingIgnoreNullValues` закрепляет одинаковое поведение для двух
путей:

- создание нового destination;
- mapping в existing target.

В обоих случаях `null` сохраняет constructor-initialized string, nested object
и collection. Это наиболее прямой precedent для общей policy в обоих режимах.

Issues выявляют границы:

- #737 — configured expression может вычисляться дважды при null-check и
  assignment;
- #439 — collection mapping как целое не следует ожидаемой element/object
  semantics;
- #561 — пользователю нужен `Optional<T>` для настоящего field presence;
- #707 — ignored constructor parameters требуют отдельного fallback law;
- #811 — `null` constructor argument абстрактного типа ошибочно приводит к
  созданию abstract type.

### 11.2. Mapperly

Mapperly использует две взаимодействующие bool-настройки:

- `AllowNullPropertyAssignment` — разрешать ли присваивать `null`;
- `ThrowOnPropertyMappingNullMismatch` — бросать или пропускать assignment,
  когда `null` присваивать нельзя.

Вместе они дают результаты `Assign`, `Ignore` и `Throw`, но enum выражал бы это
взаимоисключающее решение яснее. Default `AllowNullPropertyAssignment` —
`true`, default `ThrowOnPropertyMappingNullMismatch` — `false`.

Официальная existing-target документация прямо показывает merge через
`AllowNullPropertyAssignment = false` и условные assignments.

Relevant feedback:

- #1232 — настройка только mapper-level заставляет создавать отдельные mapper
  classes; запрошен mapping-method scope;
- #2178 — non-required init defaults не сохраняются при создании;
- #1569 — required-member validation ошибочно блокировала partial update
  existing target.

Это поддерживает два решения для Morphant: map-level scope обязателен, а
`Create` и `Update` capabilities нельзя валидировать одной матрицей.

### 11.3. AutoMapper

AutoMapper предлагает общие `Condition`/`PreCondition`, а не цельную
first-class ignore-null assignment policy. `PreCondition` выполняется до
source resolution; `Condition` — после него и до assignment.

`NullSubstitute` решает другой сценарий — заменяет `null` заданным source value.
Для Morphant обычное C#-выражение проще и сильнее типизировано:

```csharp
Name = source.Name ?? "Unknown"
```

Relevant feedback:

- #3109 — пользователь partial DTO вынужден повторять условие для каждого
  nullable member-а; maintainer характеризует global поведение как
  application-specific и требующее отдельного switch-а;
- #2999 — condition после nullable-to-non-nullable conversion уже не видит
  исходный `null`.

Это аргумент за явный opt-in с default `Assign`, map-level scope и проверку
candidate до destination conversion.

## 12. Рабочая рекомендация на момент отсрочки

Если после v0 сценарный аудит подтвердит feature, начать с такой модели:

1. `NullAssignmentHandling { Default, Assign, Ignore, Throw }`.
2. Library default — `Assign`.
3. Policy доступна для `Create` и `Update`, но поддерживает раздельные
   effective values.
4. Простая настройка применяется к обоим путям; mode/path-specific override
   позволяет `Create = Assign`, `Update = Ignore`.
5. `Ignore` сохраняет member выбранного `result`, а не обязательно исходного
   previous.
6. No-previous ветки `Map(source)` и `Map(source, null)` должны оставаться
   эквивалентными.
7. Policy применяется ко всем generated member rules; manual mapping её
   обходит.
8. Candidate вычисляется ровно один раз и проверяется до destination
   conversion.
9. Constructor parameters и creation strategy не подчиняются member policy.
10. Mutable post-construction members поддерживаются полностью.
11. Невозможный conditional skip для constructor/init/required/get-only member
    даёт diagnostic либо требует explicit fallback; Morphant не подставляет
    скрытый `default(T)`.
12. Per-member override позволяет намеренно assign `null` при global `Ignore`.
13. Presence-aware patch остаётся source-owned contract и не выводится из
    `T?`.
14. Collections, immutable reconstruction и whole-plan no-op согласуются с
    соответствующими отдельными stages, а не появляются как побочный эффект
    setting-а.

Это не финальное решение. Главная причина отсрочки: policy полезна, но является
надстройкой над уже согласованными creation/member/null/evaluation laws и не
нужна для надёжного каркаса v0.

## 13. Вопросы для повторного открытия после v0

- Подтверждают ли реальные пользователи потребность `Ignore` в `Create`, или
  достаточно `Update`-only v1 slice?
- Называть scope через public `MappingMode` или через `Create` / `Update`
  path?
- Как exact setting выбирается для `Update` с `Option.None`?
- Нужны ли оба per-member marker-а `Assign` и `IgnoreIfNull`?
- Какой exception contract у `Throw`?
- Какие init/record cases можно поддержать без нарушения evaluation order?
- Нужен ли first-class whole-plan no-op?
- Нужна ли специальная serializer-интеграция для `Option<T>`, и как policy
  взаимодействует со сторонними presence wrappers?
- Как policy взаимодействует с collection replacement/fill/reconciliation?
- Как `UnmappedMemberValidation` учитывает условно пропущенный assignment?
- Когда `Ignore` действительно устраняет nullability mismatch diagnostic?
- Поддерживается ли policy в projection, где runtime statements могут быть
  невыразимы в expression tree?

## 14. Первичные источники

### Mapster

- [Ignoring members: `IgnoreIf` и `IgnoreNullValues`](https://github.com/MapsterMapper/Mapster/blob/master/docs/articles/settings/custom/Ignoring-members.md)
- [`WhenMappingIgnoreNullValues` tests](https://github.com/MapsterMapper/Mapster/blob/master/src/Mapster.Tests/WhenMappingIgnoreNullValues.cs)
- [#737: configured mapper вызывается дважды](https://github.com/MapsterMapper/Mapster/issues/737)
- [#439: ignore-null и collection mapping](https://github.com/MapsterMapper/Mapster/issues/439)
- [#561: mapping `Optional<T>`](https://github.com/MapsterMapper/Mapster/issues/561)
- [#707: constructor и ignored/default values](https://github.com/MapsterMapper/Mapster/issues/707)
- [#811: null constructor argument абстрактного типа](https://github.com/MapsterMapper/Mapster/issues/811)

### Mapperly

- [Existing-target merge](https://mapperly.riok.app/docs/configuration/existing-target/)
- [Null value settings](https://mapperly.riok.app/docs/configuration/mapper/#null-values)
- [#1232: method-level `AllowNullPropertyAssignment`](https://github.com/riok/mapperly/issues/1232)
- [#2178: non-required init defaults при nullable source](https://github.com/riok/mapperly/issues/2178)
- [#1569: required validation при existing-target mapping](https://github.com/riok/mapperly/issues/1569)

### AutoMapper

- [`Condition` и `PreCondition`](https://docs.automapper.io/en/stable/Conditional-mapping.html)
- [`NullSubstitute`](https://docs.automapper.io/en/stable/Null-substitution.html)
- [#3109: global nullable DTO update](https://github.com/LuckyPennySoftware/AutoMapper/issues/3109)
- [#2999: nullable value потерян до `Condition`](https://github.com/LuckyPennySoftware/AutoMapper/issues/2999)
