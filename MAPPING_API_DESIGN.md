# Новый дизайн mapping API Morphant

Статус документа: согласованный нормативный дизайн текущего mapping API.
Реализация core v0 следует этому контракту; актуальный прогресс и оставшиеся
границы фиксирует
[`MAPPING_API_IMPLEMENTATION_PLAN.md`](MAPPING_API_IMPLEMENTATION_PLAN.md).
Прежний `Template()`-дизайн упоминается только в сравнительном аудите там, где
он объясняет решения текущего API, и не является compatibility target.

## 1. Цель переработки

Текущий `Template()` одновременно описывает создание destination, параметры
конструктора, body-members, работу с существующим destination и полностью
ручной mapping. Из-за этого одна lambda имеет различную семантику в `Create` и
`Update`, а `TemplateMode` дополнительно переключает способ её
интерпретации.

Новый API разделяет эти обязанности:

| API | Единственный вопрос |
|---|---|
| `Construct` | Как получить result и настроить его constructor parameters? |
| `Members` | Как маппить body-members destination? |
| `Convert` | Как целиком выполнить mapping без декларативного pipeline? |

Из целевого дизайна удаляются:

- `Template()`;
- `TemplateMode` и разделение на `Dsl` / `Raw`;
- отдельный `SelectResult`;
- отдельный builder для ручного mapping;
- специальный `Skip()` для полного пропуска member mapping;
- единый служебный template-record и его `with`-overlay, одновременно
  объединявшие creation- и member-plan.

Узкий `with`-overlay для generated member-plan сохраняется: он решает
композицию только body-member rules и не возвращает прежний единый
`Template()`-контракт.

`Construct`, `Members` и `Convert` не являются тремя последовательными
стадиями одного обязательного pipeline. `Construct` и `Members` образуют
декларативный mapping, а `Convert` является полностью отдельной
альтернативой ему.

## 2. Термины

В документе используются следующие термины:

- `Create` — публичная операция `Map(source)`;
- `Update` — публичная операция `Map(source, destination)`;
- `previous` — фактический экземпляр destination, переданный в `Update`;
  в declarative pipeline он формируется после null-предобработки, а в manual
  mapping — непосредственно из исходного аргумента;
- `result` — объект или значение, которое выбрано для применения member rules
  и в итоге возвращается из `Map`;
- `structured creation plan` — сгенерированное описание вызова поддерживаемого
  destination-конструктора либо выбора factory/previous, а не готовый
  `TDestination`; direct `Construct` возвращает готовый destination без такого
  промежуточного plan;
- `member plan` — сгенерированное описание body-member mappings, а не готовый
  `TDestination`.

`previous` и `result` намеренно различаются. `Construct` может выбрать previous,
создать replacement, получить объект из factory или cache. Поэтому identity
`result` не обязана совпадать с identity переданного destination.

Названия `Create` и `Update` описывают форму публичного вызова, а не
гарантию новой или сохранённой identity результата.

## 3. Публичный mapping contract

Целевой базовый runtime-контракт сохраняет две операции, но явно допускает
`null` во входах:

```csharp
public interface IMapper
{
    TDestination Map<TSource, TDestination>(TSource? source);

    TDestination Map<TSource, TDestination>(
        TSource? source,
        TDestination? destination);
}
```

Generated `ITypeMapper<TSource, TDestination>` повторяет тот же nullable-
контракт, явно называет обе операции и добавляет `MappingContext`:

```csharp
public interface ITypeMapper<in TSource, TDestination>
{
    TDestination Create(
        TSource? source,
        MappingContext context);

    TDestination Update(
        TSource? source,
        TDestination? destination,
        MappingContext context);
}
```

Универсальный facade выражает операции двумя перегрузками `IMapper.Map`, а
контракт конкретной pair — отдельными `ITypeMapper.Create` и
`ITypeMapper.Update`. Их доступность задаёт flags-setting:

```csharp
[Flags]
public enum MappingMode
{
    Default = 0,

    Create = 1 << 0,
    Update = 1 << 1,

    CreateAndUpdate = Create | Update
}
```

`CreateAndUpdate` намеренно перечисляет объединяемые операции. Варианты
`Both` и `Map` не используются, чтобы имя оставалось однозначным после
добавления будущих flags, например `Project`. Library default —
`CreateAndUpdate`; `Default` продолжает обычную settings-precedence chain.

`TSource?` и `TDestination?` на входах позволяют передать `null`, когда
конкретный тип его допускает; non-nullable value type дополнительно не
оборачивается в `Nullable<T>`. Это соответствует runtime-настройкам
`NullSourceHandling` и `NullDestinationHandling`.

Возвращаемый тип намеренно равен `TDestination`, а не безусловному
`TDestination?`. Nullability обычного результата выбирает сам пользователь
типом destination:

```csharp
Customer customer = mapper.Map<Source, Customer>(source);
Customer? optionalCustomer = mapper.Map<Source, Customer?>(source);
Guid? id = mapper.Map<string, Guid?>(text);
```

Runtime-настройку конкретной пары невозможно выразить условной nullable-
аннотацией generic return type. Поэтому `NullSourceHandling.ReturnNull`, raw
`Convert` и авторитетный `null` из пользовательского creation-кода могут
фактически вернуть `null` даже при non-nullable `TDestination`. Это осознанный
прагматичный контракт: обычный mapping не заставляет пользователя подавлять
предупреждение после каждого вызова, а ответственность за согласование
runtime policy с выбранной nullability остаётся у конфигурации и вызывающего
кода.

Обе операции остаются в generated contract для любой зарегистрированной пары.
Эффективный `MappingMode` определяет, какая из них поддерживается; вызов
отключённой операции завершается
`Morphant.Exceptions.MappingOperationNotSupportedException`.

Возвращаемое значение `Map` всегда авторитетно:

```csharp
destination = mapper.Map(source, destination);
```

Вызов с проигнорированным результатом потенциально ошибочен, поскольку
declarative и manual mapping могут вернуть replacement:

```csharp
mapper.Map(source, destination);
```

Для такой формы в будущем нужен analyzer warning.

## 4. Общая форма pair-builder

Декларативный mapping выглядит так:

```csharp
builder.Map<Source, Destination>()
    .Construct(source => new(
        id: source.Id,
        tenantId: Auto()))
    .Members((source, _) => new()
    {
        Name = source.Name,
        Code = source.Code
    });
```

Полностью ручной mapping регистрируется на том же pair-builder:

```csharp
builder.Map<Source, Destination>()
    .Convert((source, previous, context) =>
        MapCore(source, previous, context));
```

Для одной canonical mapping-пары разрешён либо декларативный набор
`Construct` / `Members`, либо один `Convert`. Смешивать эти модели нельзя.

### 4.1. Наследование и композиция конфигурации

В v0 declarative configuration переиспользуется внутри одного mapper-level либо
через явную C#-иерархию mapper-ов. Вызов `base.Configure(builder)` подключает
configuration chain базового mapper-а, наследует его root-level settings и
делает base pair configurations кандидатами для `IncludeBase`. Сами base
registrations в generated surface derived mapper-а не добавляются. Без этого
вызова base configuration не участвует в текущем mapper-е.

Распознаётся только прямой вызов отдельным statement либо expression-bodied
override; generator не выполняет helper methods и control flow вокруг
builder-а. Повторный `base.Configure(builder)` является ошибкой конфигурации.
Base mapper не обязан иметь `MorphantMapperAttribute`, но его `Configure` body
должен быть доступен как source в текущей compilation. Для generic base
mapper-а generator сохраняет открытый fluent surface исходного DSL и
подставляет constructed type arguments в effective derived plan; это работает
и для nested partial mapper declarations.

Повторное объявление canonical pair в derived mapper-е не наследует её plan
автоматически. Оно начинает с чистого map-level plan и использует только
унаследованные root settings, пока пользователь явно не вызовет
`IncludeBase<TBaseSource, TBaseDestination>()` на pair-builder-е. Generic-
аргументы указывают конкретную base pair: текущий source type должен быть
приводим к `TBaseSource`, а текущий destination type — к
`TBaseDestination`. Проверка охватывает class- и interface-иерархии.
Эти отношения проверяет generator: C# не позволяет method-level `where`
ограничить `TSource` и `TDestination`, объявленные у содержащего
`MapperBuilder<TSource, TDestination>`, а переход к четырём method type
arguments ухудшил бы текущую форму вызова.

Base pair сначала ищется на текущем mapper-level независимо от порядка
объявлений, затем среди mapper-level-ов, подключённых через
`base.Configure(builder)`, от ближайшего к дальнему. Если одна и та же pair
встречается на текущем и подключённом уровнях, используется текущая; среди
подключённых уровней используется ближайшее точное совпадение. Отсутствие
указанной pair или совместимости типов, self-reference, cycle, а также
повторный вызов `IncludeBase` для одной текущей pair являются ошибками
конфигурации.

Effective settings разрешаются от более конкретного уровня к менее
конкретному:

| Уровень | Приоритет |
|---|---:|
| Текущая pair | 1 |
| Pair из `IncludeBase<TBaseSource, TBaseDestination>()` | 2 |
| Root текущего mapper-а | 3 |
| Roots подключённых base mapper-ов, от ближайшего к дальнему | 4 |
| Assembly | 5 |
| Library default | 6 |

Через `IncludeBase<TBaseSource, TBaseDestination>()` наследуются все явно
заданные map-level settings, включая `MappingMode` и `ConstructorSelection`.
Наследуется именно policy, а не выбранный для base destination constructor;
локальное значение перекрывает унаследованное, а `Default` продолжает поиск по
таблице приоритетов.

Из mapping plan импортируются только правила `Members`:

- правила объединяются по destination member независимо от формы перегрузки;
- локальный expression, `Auto()` или `Ignore()` перекрывает унаследованное
  правило, после чего зависимости каждого effective rule анализируются
  отдельно;
- conventions и constructor selection вычисляются заново для текущей pair;
- `Construct` и `Convert` base pair не импортируются;
- локальный `Convert` владеет всей текущей pair и отбрасывает импортированные
  member rules.

Переносимые effective member rules испускаются внутри derived mapper-а, поэтому
все mapper-members в них должны быть доступны из derived type. Обычные public,
internal и protected helpers поддерживаются согласно C# accessibility;
private members и явный `base.` в оставшемся inherited expression делают
effective plan ошибочным. Полное локальное перекрытие destination member
удаляет заменённое inaccessible правило до проверки accessibility.

Source generator не выполняет configuration code и не следует за
произвольными helper calls, которые изменяют builder. Переиспользуемые
вычисления остаются обычными instance/static методами mapper-а, вызываемыми
внутри `Construct`, `Members` или `Convert`.

Отдельные fragments для unrelated pairs и cross-assembly
`IncludeBase<TBaseSource, TBaseDestination>()` не входят в v0. Generic и nested
mapper-ы поддерживаются внутри одной compilation. Mapping-и из внешних
assemblies подключаются независимыми manual runtime registrations;
application-wide dispatch не становится неявным источником configuration
composition и будущие keyed variants не меняют это правило.

## 5. `Option<T>`

Для представления возможного previous используется отдельная value-type
обёртка, аналогичная `Nullable<T>`:

```csharp
public readonly struct Option<T>
{
    public static Option<T> None { get; }

    public static Option<T> Some(T value);

    public bool HasValue { get; }

    public T Value { get; }

    public bool TryGetValue(
        [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out T value);
}
```

`Option<T>` является общей presence-обёрткой и не содержит API, связанного
только с previous destination. Для неё действуют следующие законы:

- `default(Option<T>)` означает `None`;
- `None` доступен как именованное значение, а `Some(T)` является единственным
  публичным способом явно создать присутствующее значение; публичного
  конструктора и implicit conversion из `T` нет;
- `None` отличается от `Some(default(T))`;
- когда `T` допускает `null`, `None` отличается и от `Some(null)`;
- `Value` возвращает сохранённый `T`, а обращение к нему при `None` бросает
  `Morphant.Exceptions.OptionValueMissingException`;
- `TryGetValue` при `false` может записать `default`, что отражено
  `[MaybeNullWhen(false)]`.

В mapping API тип `T` всегда является destination без корневой nullability:

| Destination mapping-пары | Generated previous |
|---|---|
| `Customer` | `Option<Customer>` |
| `Customer?` | `Option<Customer>` |
| `Point` | `Option<Point>` |
| `Point?` | `Option<Point>` |
| `Envelope<string?>?` | `Option<Envelope<string?>>` |

Удаляется только корневая nullability destination; nullability вложенных
generic arguments сохраняется. Далее в концептуальных сигнатурах документа
запись `Option<TDestination>` всегда означает именно такой
root-normalized тип.

Поэтому конкретно в роли previous успешное значение всегда non-null, а
`Option<TDestination>` ни в declarative, ни в manual mapping не хранит
`Some(null)`. В declarative pipeline явный `null` destination сначала
обрабатывается `NullDestinationHandling`, и только затем формируется
`Option<TDestination>`.

Та же обёртка передаётся в `Convert`, но там она формируется из исходного
destination без null-предобработки. Поэтому explicit `null` представлен как
`Option.None`, а отличие `Map(source, null)` от `Map(source)` сообщает
`MappingContext.Operation`.

## 6. `Construct`

### 6.1. Ответственность

`Construct` отвечает только за:

- выбор способа получить `result`;
- выбор destination-конструктора;
- mapping constructor parameters;
- convention construction;
- factory construction;
- прямое получение готового destination, когда constructor-plan отсутствует;
- выбор existing destination как `result`, когда previous существует.

Declarative rules body-members в `Construct` не настраиваются. В частности,
`Members` остаётся единственным declarative surface для свойств и полей.

Direct `Construct` при этом является обычным C#-кодом, возвращающим готовый
instance, поэтому object initializer и любые допустимые C# assignments внутри
него разрешены:

```csharp
.Construct(source => new Destination
{
    Id = source.Id
})
```

Это не создаёт declarative member rule. `Construct` и `Members` остаются двумя
частями одного описания создания и инициализации destination, а generator
lower-ит их совместно. Поэтому для structured destination `init` и `required`
могут задаваться в `Members` и попадать в итоговый object initializer.

Direct lambda возвращает уже созданный instance. Для такой pair generated
`Members` содержит только post-construction assignable members: обычные
setters и mutable fields, включая помеченные `required`. `init`-only properties
в direct member surface не входят. Сам direct-код при этом свободен
использовать object initializer.

### 6.2. Выбор generated surface

После применения общей destination-type policy форма `Construct` определяется
только наличием constructor surface, который Morphant действительно умеет
вызвать:

| Constructor capability | Generated `Construct` | Что возвращает lambda |
|---|---|---|
| Есть хотя бы один поддерживаемый constructor, включая parameterless | Structured | `DestinationConstruction` |
| Поддерживаемого constructor surface нет либо destination opaque | Direct | Настоящий `TDestination` |

Structured `Construct` описывает не отдельный constructor-вызов, а единый plan
создания и инициализации destination. Поэтому доступный parameterless
constructor тоже выбирает structured surface: Morphant может выполнить его по
convention и включить `init`, `required` и остальные creation-time member rules
в тот же итоговый initializer.

Наличие body-members не влияет на выбор формы `Construct`. Оно независимо
определяет наличие `Members`. Поэтому interface или factory-only class с
post-construction writable members получает direct `Construct` вместе с
`Members`, а scalar без members — только direct `Construct`.

Одна mapping-пара никогда не получает обе формы. Пользовательский mode для
переключения между structured и direct surface не вводится.

### 6.3. Две перегрузки и общая семантика arity

Generated callback-параметры используют именованные delegate-типы из
`Morphant.Delegates`, а не `Func<...>`. Это сохраняет в IntelliSense
смысловые имена lambda-параметров независимо от конкретной generated pair:

```csharp
public delegate TResult Construct<in TSource, out TResult>(
    TSource source);

public delegate TResult Construct<in TSource, TPrevious, out TResult>(
    TSource source,
    Option<TPrevious> previous);

public delegate TMembers Members<in TSource, TPrevious, out TMembers>(
    TSource source,
    Option<TPrevious> previous);

public delegate TMembers Members<
    in TSource,
    TPrevious,
    in TResult,
    out TMembers>(
    TSource source,
    Option<TPrevious> previous,
    TResult result);

public delegate TResult Convert<in TSource, TPrevious, out TResult>(
    TSource source,
    Option<TPrevious> previous,
    MappingContext context);
```

`Construct` и `Members` намеренно используют одно имя для обеих arity.
`TPrevious` является root-normalized destination из раздела 5, а `TResult`
сохраняет точный result contract; поэтому для nullable destination эти два
generic argument-а могут различаться. Lambda и method group получают обычный
target typing; заранее материализованный callback имеет соответствующий
`Morphant.Delegates`-тип, поскольку разные concrete delegate-типы не имеют
implicit conversion друг в друга.

Для structured surface генерируются:

```csharp
Construct(
    Delegates.Construct<TSource, DestinationConstruction> construct);

Construct(
    Delegates.Construct<
        TSource,
        TDestination,
        DestinationConstruction> construct);
```

`DestinationConstruction` — сгенерированный creation-plan для конкретного
destination. Это не настоящий `TDestination`.

Для direct surface генерируются:

```csharp
Construct(
    Delegates.Construct<TSource, TDestination> construct);

Construct(
    Delegates.Construct<
        TSource,
        TDestination,
        TDestination> construct);
```

Обе формы используют один закон выбора result:

| Настройка | `previous` отсутствует | `previous` существует |
|---|---|---|
| `Construct(source)` | Lambda определяет result | Lambda не вызывается; previous становится result |
| `Construct(source, previous)` | Lambda вызывается с `Option.None` | Lambda вызывается с `Option.Some` |

Source-only structured `Construct` концептуально эквивалентен:

```csharp
Construct((source, previous) =>
{
    if (previous.HasValue)
    {
        return previous;
    }

    return ConstructFromSource(source);
});
```

Source-only direct `Construct` имеет ту же семантику, но возвращает настоящий
destination:

```csharp
Construct((source, previous) =>
    previous.HasValue
        ? previous.Value
        : ConstructFromSource(source));
```

Эта небольшая синтаксическая асимметрия намеренна. Structured lambda выбирает
ветку creation-plan, поэтому отдельный `return previous` неявно преобразует
`Option<TDestination>` в `DestinationConstruction`. Block-форма также сохраняет
target typing `new(...)` в C# 9; conditional expression с `previous` и
target-typed `new(...)` вместо этого пытается типизировать `new(...)` как
`Option<TDestination>` и не компилируется. Direct lambda уже обязана вернуть
`TDestination`, поэтому после проверки `HasValue` явно извлекается
`previous.Value`. Отдельный
`DirectConstruction<T>`, implicit conversion `Option<T> -> T`, `AsResult()` и
`UsePrevious()` не вводятся.

Настоящий return type direct source-only перегрузки также сохраняет естественные
method groups:

```csharp
builder.Map<string, Guid>()
    .Construct(Guid.Parse);
```

Для одной пары можно настроить только один `Construct`, независимо от выбранной
перегрузки. Повторный вызов является diagnostic; две перегрузки не образуют
отдельные `Create`- и `Update`-правила.

### 6.4. Почему две перегрузки нужны только здесь

У `Construct` arity действительно меняет политику:

```csharp
.Construct(source => new(source.Id))
```

не заменяет existing destination, а:

```csharp
.Construct((source, _) => new(source.Id))
```

создаёт result и при `Create`, и при `Update`.

Это намеренное различие, а не сокращённая запись одной и той же операции.

Тот же закон действует для direct surface:

```csharp
.Construct(Parse)
```

сохраняет existing destination, а:

```csharp
.Construct((source, _) => Parse(source))
```

получает replacement и для `Create`, и для `Update`.

### 6.5. Generated structured creation-plan

Creation-plan зеркалит поддерживаемые destination-конструкторы и использует
`ConstructorParameter<T>` для их параметров. Концептуально:

```csharp
internal sealed class DestinationConstruction
{
    public DestinationConstruction(
        ConstructorParameter<Guid> id,
        ConstructorParameter<Guid> tenantId);

    public DestinationConstruction(
        ByConventionMarker marker,
        DestinationConstructorParameters? parameters = null);

    public DestinationConstruction(
        IByFactoryMarker<Destination> marker);

    public static implicit operator DestinationConstruction(
        Option<Destination> previous);
}
```

Это сохраняет полноценный DSL для constructor parameters:

```csharp
.Construct(source => new(
    source.Id,
    Auto(),
    Map(source.Address)))
```

Поддерживаемые формы creation-plan:

- явный destination-конструктор;
- `ByConvention()`;
- `ByConvention()` с явными constructor-parameter rules;
- factory через `new(ByFactory(...))`;
- existing previous как result в previous-aware перегрузке.

Произвольный готовый `TDestination` не преобразуется в structured
creation-plan. Готовый или cached instance выражается явно как factory-ветка:

```csharp
.Construct(source => new(ByFactory(() => cache.Get(source.Id))))
```

Форма `new(ByFactory(...))` обязательна: marker передаётся generated
constructor-у creation-plan, а implicit conversion от marker-interface не
генерируется.

Constructor-parameter rules сохраняют текущую модель:

| Запись | Семантика |
|---|---|
| Явное выражение | Вычислить и передать значение параметра |
| `Auto()` | Обязательно получить параметр по convention |
| `Ignore()` | Опустить параметр, когда это допустимо для optional / `params` |
| `Map()` / `Map<TDestination>()` | Вывести source по target-name и выполнить adaptive nested mapping |
| `Map(source)` / `Map<TDestination>(source)` | Выполнить adaptive nested mapping явного source |
| `Create(source)` / `Create<TDestination>(source)` | Принудительно выполнить nested `Create` |
| `Update(source, destination)` / `Update<TDestination>(source, destination)` | Принудительно выполнить nested `Update` |

Typed-формы `Auto<T>()` и `Ignore<T>()` сохраняются вместе с generic-формами
nested markers. Они нужны там, где обычного target typing
недостаточно, например внутри declarative local, conditional- либо
switch-expression. Generic argument типизирует marker; окончательную
совместимость с constructor parameter по-прежнему проверяют обычные правила
C# и Morphant DSL.

Generated overload-ы creation-plan являются compiler probe для настоящих
destination constructors. Positional, named и mixed arguments, optional-
параметры, omission и overload ambiguity разрешает C# compiler, а не ручной
алгоритм generator-а. `params` допускает omission либо передачу массива
целиком, но не expanded-форму. Явный cast к `ConstructorParameter<T>` остаётся
способом выбрать нужную generated overload; при lowering он превращается в
cast к фактическому типу соответствующего destination-параметра.

`Construct` не гарантирует новую identity. В частности, `ByFactory()` может
вернуть cached instance. Название означает получение базового `result`, а не
обязательное выделение нового объекта.

### 6.6. Поведение по умолчанию

Для structured surface, если previous отсутствует и `Construct` не настроен,
Morphant выполняет обычное convention construction с эффективным
`ConstructorSelection`. Текущим default остаётся `Unambiguous`.

`Unambiguous` выбирает единственный поддерживаемый доступный parameterized-
constructor, даже если одновременно существует parameterless-constructor.
Если parameterized-конструкторов нет, выбирается поддерживаемый доступный
parameterless-constructor. Если parameterized-конструкторов несколько,
требуется явный выбор даже при наличии parameterless-constructor. После выбора
Morphant не делает fallback к parameterless либо другому constructor-у из-за
отсутствующего или несовместимого обязательного argument-а.

Остальные стратегии следуют той же stable supported-constructor surface:

- `Explicit` запрещает автоматический выбор, включая `ByConvention()`;
- `Parameterless` выбирает только поддерживаемый parameterless-constructor;
- `Single` требует ровно один поддерживаемый constructor независимо от его
  параметров;
- `Greediest` строит все применимые warning-free convention plans и выбирает
  уникальный plan с наибольшим числом фактически переданных arguments;
- `Largest` сначала выбирает уникальный supported constructor с наибольшим
  числом объявленных parameters и только затем проверяет его применимость.

Опущенные optional/`params` parameters не увеличивают score `Greediest`, а
переданный `params` array считается одним argument. Равенство лучших scores у
`Greediest` либо максимального declared size у `Largest` не разрешается
порядком объявления и требует explicit `Construct`. `Largest`, `Single`,
`Unambiguous` и `Parameterless` не откатываются к другому constructor-у, если
уже выбранный кандидат неприменим. Required initializer plan и
`SetsRequiredMembers` участвуют в применимости constructor-а.

В `ByConvention()` written parameter rules участвуют в применимости и score:
явное expression и успешный `Auto()` считаются переданными arguments,
`Ignore()` — нет. Explicit constructor и `ByFactory()` внутри `Construct` не
зависят от `ConstructorSelection`.

Direct destination не имеет поддерживаемого constructor surface, поэтому
reachable no-previous ветка требует configured direct `Construct`. То же
правило действует для opaque destination: даже если C# технически позволяет
`new()` или `default`, Morphant не выбирает за пользователя атомарное значение.
Отсутствие обязательной настройки является ошибочной конфигурацией, а не
поводом для fallback на `Convert`, runtime conversion или `default`.

Если previous существует и configured `Construct` — source-only, lambda не
вычисляется вообще. Constructor arguments, factory и любые используемые только
в этой lambda выражения также не вычисляются.

Если previous-aware structured `Construct` выбирает previous, он становится
`result`. Constructor, convention или factory дают replacement-result. В
direct surface lambda возвращает либо `previous.Value`, либо готовый
replacement непосредственно.

Structured plan специализируется отдельно для заведомо отсутствующего
previous в `Create` и существующего previous в обычном `Update`. Проверки
`previous.HasValue` и защищённые ими обращения к `previous.Value` сворачиваются
по известной operation, но только когда выбранная ветка доказуемо
недостижима. Short-circuit-порядок и side effects остальных частей условия
сохраняются. Незащищённый `return previous`, достижимый в `Create`, остаётся
ошибочной веткой и не заменяется скрытым construction fallback.
Если после специализации обе стороны оставшегося условия ведут в один plan,
условие всё равно вычисляется ради observable effects, а общий plan испускается
один раз. В generated code такое вычисление выражается явным discard
`_ = condition;`; части short-circuit expression, до которых выполнение не
доходит, не вычисляются.

Никакого скрытого fallback между различными ветками `Construct` нет.

### 6.7. Порядок вычислений creation-plan

В runtime выполняется только выбранный путь `Construct`. Невыбранная ветка,
source-only lambda при существующем previous и выражения, нужные только
неприменимой operation, не вычисляются.

В structured plan явные constructor arguments вычисляются ровно один раз
слева направо в порядке записи, включая переставленные named arguments. Затем
вызывается выбранный destination-constructor. Для `ByConvention()` сначала в
пользовательском порядке вычисляются явно записанные constructor-parameter rules,
после них — оставшиеся automatic arguments в порядке параметров выбранного
конструктора. `Ignore()` не вычисляет значение, а `Auto()` и `Map(...)`
занимают позицию соответствующего rule.

Фактически сформированный constructor argument занимает одноимённый
body-member только относительно неявной member-convention. Для этого
используется exact name, затем unique `OrdinalIgnoreCase`, как и при обычном
constructor mapping. Опущенный optional/`params` parameter и `Ignore()` не
занимают member, поскольку argument в constructor не передаётся. Explicit
`Members` rule остаётся авторитетным и применяется даже при соответствующем
constructor argument. `required` member также остаётся в initializer, если
выбранный constructor не помечен `[SetsRequiredMembers]`; общее automatic
значение при этом вычисляется один раз и переиспользуется.

Plan-shaping locals, условия и selector-ы выполняются в своей позиции и только
на выбранном execution path. Если значение уже вычислено в declarative local,
оно переиспользуется, а не вычисляется повторно ради constructor или member
rule.

Direct `Construct` и тело `ByFactory` являются обычным синхронным C#-кодом, а не
разбираемым statement-by-statement creation DSL. Expression-body переносится
как выражение, block-body — целиком; обычный C# определяет внутренний порядок,
ветвление, mutation, циклы, exceptions и local functions. Получение настоящего
result выполняется ровно один раз. После него действует общая member-фаза,
если result не равен `null`.

Переносимый block либо materialized method-group/delegate испускается одним
collision-safe private helper-ом mapper-а. Если один callable достижим и в
`__Create`, и в `__Update`, обе operations вызывают этот общий helper;
helper body и типизированный delegate local не дублируются в leaf-ветвях.
Operation-specific source/previous передаются параметрами только при
фактическом capture, поэтому reuse не меняет reachability и evaluation laws.

## 7. `Members`

### 7.1. Две альтернативные перегрузки

Для каждой pair с member-capability generator всегда создаёт обе
концептуальные перегрузки:

```csharp
Members(
    Delegates.Members<
        TSource,
        TDestination,
        DestinationMembers> members);

Members(
    Delegates.Members<
        TSource,
        TDestination,
        TDestination,
        DestinationMembers> members);
```

Это две формы одного declarative DSL. Первая не предоставляет параметр
`result`, вторая делает фактически выбранный non-null result доступным для
выражений, которым он нужен. Выбор перегрузки сам по себе не задаёт runtime-
фазу и не меняет семантику rules, не использующих `result`. Обе формы
генерируются для любой pair с member-capability. Сам набор members учитывает
форму construction: structured surface включает creation-time members, а
direct surface — только post-construction assignable members.

В локальной конфигурации pair можно вызвать ровно один `Members`.
Любой второй локальный вызов является ошибкой.
`IncludeBase<TBaseSource, TBaseDestination>()` объединяет унаследованный и
локальный member plans независимо от формы перегрузки:
rules с двумя и тремя lambda-параметрами являются одинаковыми элементами
effective plan. Source-only перегрузки нет. Если previous или result не нужны,
пользователь пишет `_`:

```csharp
.Members((source, _) => new()
{
    Name = source.Name,
    Age = source.Age
});
```

Это явно показывает, что member plan применяется в обеих операциях, а
previous может существовать, хотя данному правилу он не нужен.

### 7.2. Ответственность

`Members` является единственным declarative surface настройки body-members,
которые применимы к construction capability destination:

- properties с обычным `set`;
- `init`-only properties для structured destination;
- `required` properties и fields;
- writable fields;
- поддерживаемые унаследованные body-members.

У direct destination из этого списка остаются только обычные setters и mutable
fields. Модификатор `required` их не исключает; `init`-only property исключается.

Constructor parameters не входят в `Members`, потому что они не являются
body-members. Обычный C# внутри direct `Construct`, factory или `Convert`
может самостоятельно инициализировать либо изменять members; такие действия
не превращаются в declarative rules и не анализируются как `Members` plan.

Концептуальный generated plan:

```csharp
internal sealed record DestinationMembers
{
    public Member<string> Name { get; set; }

    public Member<string> Code { get; set; }

    public Member<int> Revision { get; set; }
}
```

`DestinationMembers` является record именно ради declarative `with`-
композиции. Пользователь может выбрать member-plan условием, а затем наложить
общие либо более конкретные rules:

```csharp
.Members((source, _) =>
{
    var specific = source.Kind switch
    {
        Kind.Regular => new DestinationMembers
        {
            Discount = source.Discount
        },
        Kind.Corporate => new DestinationMembers
        {
            CreditLimit = source.CreditLimit
        },
        _ => new DestinationMembers()
    };

    return specific with
    {
        Name = source.Name,
        UpdatedAt = source.UpdatedAt
    };
});
```

Более поздний overlay заменяет прежний rule того же member-а. Заменённое
expression и его ставшие ненужными dependencies не вычисляются; dependency-
анализ выполняется над effective plan после выбора ветвей и разрешения
overlay-ев. `with` не задаёт runtime-порядок assignments и не распространяется
на `DestinationConstruction`: creation composition уже выражается отдельным
`Construct`.

Собственные `set`-сеттеры служебного record нужны только для object initializer
и `with` и не связаны с `set`/`init`-семантикой destination. Последующая
mutation уже созданного local plan-а по-прежнему не входит в declarative
grammar; она остаётся лишь совместимой точкой возможного расширения после v0.

### 7.3. Применение плана

`Members` всегда применяется к выбранному `result`, а не к `previous`.
Параметр `previous` внутри lambda всегда означает исходный destination-вход
после null-предобработки, даже если `Construct` выбрал replacement.

В трёхпараметрической перегрузке `result` означает именно фактически
выбранный instance: previous, constructor/convention result, factory/cache
или direct result, включая его derived runtime-тип. Это позволяет
использовать состояние, которого нет ни в source, ни в previous:

```csharp
builder.Map<OrderDto, Order>()
    .Construct(source => new(source.Id))
    .Members((source, _, result) => new()
    {
        Revision = result.Revision + 1,
        Details = Map(source.Details)
    });
```

Здесь `Details` мог быть создан самим constructor-ом. Та же перегрузка
покрывает cached/factory result без специальной привязки API к factory.

Параметр `result` не использует presence-wrapper и генерируется без
корневой nullability destination: `Customer?` даёт `Customer result`,
`Point?` — `Point result`, а вложенные nullable annotations сохраняются.
Любое выражение, которое фактически использует `result`, выполняется только
после появления non-null instance. Если direct `Construct` или `ByFactory()`
вернул `null`, mapping завершается до применения member rules; недостижимое
состояние «result отсутствует» не несёт полезной информации и ложно намекало
бы на возможность заменить терминальный `null`.

Generator анализирует каждый structured member rule отдельно. Прямая либо
транзитивная ссылка на `result` в value, declarative local или условии делает
зависимые от неё rules post-creation. Само наличие третьего lambda-параметра
ничего не меняет. Поэтому result-independent `init` и creation-time `required`
rules допустимы и в трёхпараметрической перегрузке; diagnostic нужен только
тогда, когда конкретный creation-time rule либо условие его применимости
зависит от ещё не созданного result.

Пример:

```csharp
builder.Map<CustomerDto, Customer>()
    .Construct((source, previous) =>
    {
        if (previous.HasValue &&
            previous.Value.TenantId == source.TenantId &&
            !previous.Value.IsFrozen)
        {
            return previous;
        }

        return new(source.Id, source.TenantId);
    })
    .Members((source, previous) => new()
    {
        Name = source.Name,

        Revision = previous.HasValue
            ? previous.Value.Revision + 1
            : 1
    });
```

Если `Construct` вернул replacement, `Name` и `Revision` применяются к
replacement, но `previous.Value.Revision` читается из исходного объекта.

Generator самостоятельно раскладывает единый member plan по допустимым фазам:

- для result, создаваемого structured constructor/convention plan, применимые
  result-independent `init` и creation-time `required` rules могут попасть в
  object initializer независимо от формы `Members`;
- обычные setters и writable fields применяются к выбранному result; их
  result-dependent expressions вычисляются только после его создания;
- если result является previous, его `init`-only members сохраняются;
- выражение explicit `init`-rule не вычисляется в ветке, где применить его
  невозможно;
- `required`-member с обычным доступным `set` можно обновлять у previous;
- replacement, созданный constructor/convention plan, получает те же
  creation-time member rules, что и обычный `Create`.

Если `ByFactory()` возвращает уже созданный объект, применить к нему
`init`-only rule невозможно. Явная попытка совместить такую creation-ветку с
соответствующим `Members` rule должна давать diagnostic. Direct surface вообще
не включает `init`-only member, поэтому такую конфигурацию нельзя записать.

### 7.4. Explicit rules и conventions

Внутри `Members` остаются только member-level операции:

```csharp
.Members((source, _) => new()
{
    Name = source.Name,
    Age = Auto(),
    LegacyValue = Ignore(),
    Address = Map(source.Address)
});
```

Их семантика:

| Запись | Результат |
|---|---|
| Явное выражение | Вычислить и присвоить member выбранного result |
| `Auto()` | Обязательно найти convention mapping |
| `Ignore()` | Не маппить member и сохранить значение выбранного result |
| `Map()` / `Map<TDestination>()` | Вывести source по target-name и выполнить adaptive nested mapping |
| `Map(source)` / `Map<TDestination>(source)` | Выполнить adaptive nested mapping явного source |
| `Create(source)` / `Create<TDestination>(source)` | Принудительно выполнить nested `Create` и присвоить результат |
| `Update(source, destination)` / `Update<TDestination>(source, destination)` | Принудительно выполнить nested `Update` и присвоить результат |
| Standalone `Update(source, members.GetOnly)` | Обновить non-null get-only member in-place и отбросить nested result |
| Member не указан | Применить эффективный `MemberSelection` |

У `Auto` и `Ignore` сохраняются обе формы: `Auto()` / `Auto<T>()` и
`Ignore()` / `Ignore<T>()`. Typed marker нужен в declarative local или другой
позиции без достаточного target typing; в target-typed initializer обычная
форма остаётся предпочтительной. `Map<TDestination>(...)` аналогично явно
задаёт nested destination, когда его нельзя либо не следует выводить из
целевого места.

Выбор неуказанных members задаёт отдельная setting:

```csharp
public enum MemberSelection
{
    Default = 0,

    Auto,
    Explicit
}
```

При `MemberSelection.Auto` явные rules дополняют или переопределяют convention
rules. При `MemberSelection.Explicit` неуказанные members не маппятся.

`Ignore()` для нового result оставляет значение, полученное конструктором,
factory или default initialization. Для previous он сохраняет текущее значение
выбранного result.

Обычные conventions и явный `Auto()` никогда не предполагают nested mapping.
Они находят source-member по своим правилам имени и доступности и используют
его только при warning-free implicit C#-преобразовании в целевой тип. Если
имена совпали, но прямого преобразования нет, convention не разрешена. Generator
не проверяет наличие mapping-пары и не генерирует runtime lookup по одному
совпадению имён: полный набор mappings может быть объявлен в другой либо в
потребляющей сборке и потому неизвестен в точке генерации.

Точная граница convention member surface переносится из прежнего дизайна:

- source property должен иметь доступный getter, source field может быть
  mutable либо readonly;
- destination property должен иметь доступный обычный setter либо `init`, а
  destination field — быть mutable; `required` не создаёт отдельную категорию,
  но обязан быть удовлетворён на каждой достижимой creation-ветке;
- static/const members, indexers, ref-return properties, explicit interface
  implementations, fixed buffers, нечитаемые source-members, get-only
  destination properties и readonly destination fields не участвуют;
- имена, зарезервированные самим generated record (`Clone`,
  `EqualityContract`, `Equals`, `GetHashCode`, `PrintMembers`, `ToString` и имя
  `DestinationMembers`-типа), не образуют member rule: C# не позволяет
  объявить одноимённый record-member с требуемой плоской DSL-формой;
- destination member включается в общий generated `Members` surface только
  при доступности из assembly-context без привилегий конкретного mapper-а:
  доступны public и допустимые `internal`, но не private/protected members;
- source-member при последующем convention lowering проверяется в реальном
  lexical context generated mapper-а и с фактическим receiver type;
- class/record/struct members перечисляются base-first, затем в порядке
  destination declarations; объявление в derived type скрывает одноимённый
  base member даже тогда, когда само непригодно, а override появляется один
  раз;
- для interface выбирается единственное most-derived объявление; unrelated
  неоднозначные объявления не образуют convention candidate.

Body-member matching требует точного регистрозависимого имени. Для parameter-а
выбранного convention constructor-а source-member ищется сначала по точному
имени, затем по единственному `OrdinalIgnoreCase`-совпадению. Это различие
намеренно сохраняется: constructor parameter names часто следуют camelCase,
а destination members — PascalCase.

Совместимость определяется не сравнением type symbols, а warning-free
неявным C#-преобразованием фактического expression в generated lexical
context. Поддерживаются numeric и lifted nullable conversions, reference-
conversion, inheritance/interfaces, variance, arrays, boxing, type parameters,
tuple conversions и user-defined `implicit operator`. Narrowing, downcast,
unboxing и другие explicit conversions не добавляются; runtime dynamic
conversion также не выполняется. Nullable-анализ учитывает вложенные
annotations, `MaybeNull`/`NotNull`, `AllowNull`/`DisallowNull`, generic
constraints и oblivious-контекст. Generator не вставляет cast либо
null-forgiving operator, чтобы сделать несовместимый convention candidate
допустимым.

Nested mapping задаётся восемью marker-формами:

| Форма | Nested source | Nested destination | Операция |
|---|---|---|---|
| `Map()` | Выводится по имени target | Выводится из target | Adaptive |
| `Map<TDestination>()` | Выводится по имени target | Явный `TDestination` | Adaptive |
| `Map(source)` | Явное выражение | Выводится из target | Adaptive |
| `Map<TDestination>(source)` | Явное выражение | Явный `TDestination` | Adaptive |
| `Create(source)` | Явное выражение | Выводится из target | `Create` |
| `Create<TDestination>(source)` | Явное выражение | Явный `TDestination` | `Create` |
| `Update(source, destination)` | Явное выражение | Выводится из target | `Update` |
| `Update<TDestination>(source, destination)` | Явное выражение | Явный `TDestination` | `Update` |

Короткий `Map` предназначен для обычного member-aware сценария. В
no-previous outer branch он вызывает nested Create. В existing outer Update
он вызывает nested Update: writable member передаёт текущий member фактически
выбранного `result`, а constructor parameter — соответствующий readable member
исходного outer `previous`, поскольку новый result ещё не создан.

При replacement-ветке writable member использует именно replacement-result.
Если public outer Update с `null` вследствие `NullDestinationHandling.Create`
нормализован в no-previous branch, adaptive `Map` выполняет nested Create.
Явный `Update(source, null)` остаётся nested Update; дальнейшее поведение
определяет null policy вложенной mapping-пары.

Parameterless `Map` ищет readable source property/field по точному имени
target-member-а. Constructor parameter сначала связывается с readable
destination-member: точное совпадение имеет приоритет, иначе допускается одно
уникальное `OrdinalIgnoreCase`-совпадение. Source затем ищется по фактическому
имени этого destination-member-а. Если связи нет, no-previous ветка ищет
source по имени самого parameter-а. Existing Update без однозначно связанного
readable destination-member unsupported; пользователь выбирает source и
operation явно.

Статический тип nested source определяется source-expression либо найденным
source-member. Runtime-тип не меняет выбранную пару. В generic-форме
возвращаемый `TDestination` должен warning-free неявно преобразовываться в тип
целевого member или constructor parameter. В adaptive Update текущее значение
должно быть `null` либо runtime-совместимо с `TDestination`; incompatible
non-null value приводит к
`Morphant.Exceptions.NestedDestinationTypeMismatchException`, а не
превращается в `null` или скрытый Create. `null` передаётся дальше только если
выбранный `TDestination` способен его представить; для non-nullable value
destination та же typed ошибка происходит до nested dispatch.

Все формы могут храниться в declarative local. Local остаётся alias marker-а и
получает target context от конечного member-а либо constructor parameter-а.
Один adaptive local нельзя использовать для разных current destinations в
Update: такой plan неоднозначен и unsupported.

Для writable target nested result авторитетен и присваивается фактическому
outer result; nested Update может сохранить аргумент либо вернуть replacement.
True get-only destination property и property с недоступным обычным setter-ом
появляются в generated `DestinationMembers` как get-only markers. Direct
`init`-only property остаётся creation-only и такого proxy не получает. Для
get-only marker разрешена только standalone форма:

```csharp
.Members((source, _) =>
{
    var members = new DestinationMembers
    {
        Name = source.Name
    };

    Update(source.Address, members.Address);
    return members;
});
```

Generator читает `result.Address` один раз. При `null` nested mapper не
вызывается и source-expression не вычисляется. При non-null выполняется обычный
nested Update, но returned replacement отбрасывается, потому что присвоить его
некуда. Get-only value-type target unsupported. Такие markers не участвуют в
conventions, `Auto()` и unmapped-member validation.

Параметры `previous` и `result` в declarative `Construct`/`Members` являются
read-only источниками информации. Assignment, increment/decrement и передача
через `ref`/`out` самого параметра либо rooted member-а делают plan unsupported.
Контролируемый in-place update get-only graph выражается через
`members.Member`, а не прямой мутацией `result`.

Аргументы каждого nested marker-а вычисляются ровно один раз слева направо в
порядке записи, включая переставленные named arguments. Get-only null guard
выполняется до source-expression. Scoped `IMapper` создаёт новый immutable call
frame с выбранной operation и сохраняет общий mapping scope.

### 7.5. Dependencies и порядок вычислений

`Members` является источником DSL-информации для generator-а, а не runtime-
callback, который целиком вызывается до либо после создания destination.
Generator строит data/control dependencies между declarative locals,
условиями и отдельными member rules. Форма перегрузки в этот граф не входит.

Rule считается result-dependent, если его value либо условие применимости
прямо или транзитивно использует параметр `result`. Например:

```csharp
.Members((source, _, result) => new()
{
    Name = source.Name,
    Details = Map(source.Details)
});
```

`Name` не зависит от result и может участвовать в creation-time initializer,
если этого требует destination member. Adaptive `Details` в Update использует
`result.Details` и потому может быть вычислен только после создания result.
Использование `result` одним rule не переводит весь
`Members` в post-creation и не меняет фазу независимых rules.

Для structured constructor/convention result применимые result-independent
`init` и creation-time `required` rules могут быть вычислены при создании
объекта. Result-dependent setter/field rules выполняются после появления
non-null result. Если `init`, creation-time `required` либо управляющее таким
rule условие зависит от ещё не созданного result, конфигурация ошибочна.
Previous-result, factory и direct result уже созданы независимо от
перегрузки, поэтому к ним применимы только доступные post-construction
assignments.

Каждое выражение вычисляется не более одного раза. Если выбранный execution
path требует его значение, оно вычисляется ровно один раз; невыбранные ветки,
неприменимые rules и значения другого mapping path не вычисляются. Declarative
local создаёт явную dependency: его initializer выполняется до использующих
его выражений. Внутри отдельного выражения сохраняется обычная C#-семантика, а
explicit constructor arguments вычисляются слева направо в порядке записи.

Dependency graph является общим для structured `Construct` и `Members`. Если на
одном выбранном execution path двум plan-частям требуется одно и то же bound
пользовательское subexpression, это один computation node, а не два
независимых вызова:

```csharp
.Construct(source => new(Calculate(source)))
.Members((source, _) => new()
{
    NormalizedValue = Calculate(source)
});
```

Концептуально `Calculate(source)` вычисляется в local один раз и используется
для constructor argument и member rule. Это observable закон DSL, а не
необязательная оптимизация: в новом split API пользователь может быть вынужден
повторить запись expression, но не получает повторного side effect или другого
runtime-значения по сравнению с общим local прежнего `Template()`.

Равенство определяется после semantic binding и разрешения DSL marker-ов:
совпадают operation shape, symbols, receiver, arguments и их порядок,
constants, а для `Map(...)` — также выбранные nested operation и destination.
Лишние parentheses и разные контекстные обёртки `ConstructorParameter<T>` /
`Member<T>` не разделяют исходное пользовательское значение; необходимые
target conversions применяются к уже разделяемому значению отдельно. Похожий
текст, связавшийся с другим overload/symbol или другой target-typed nested
mapping, общей нодой не является.

Sharing остаётся path-sensitive: expression не выносится из условия и не
вычисляется, если ни одно effective использование на выбранном пути не нужно.
Если creation-use требует значение до constructor-а, это и задаёт момент
единственного вычисления; отдельные неповторяющиеся reads не образуют
глобальный snapshot. Обязательный общий граф охватывает анализируемые
structured `Construct` и `Members`; direct `Construct`, factory body и
`Convert` остаются обычными C# blocks, из которых generator не извлекает
cross-plan subexpressions.

Конкретный lowering member plan-а не является контрактом. Generator вправе
использовать object initializer, временные locals, немедленные assignments,
группировать вычисления либо выбирать другую реализацию, которая соблюдает
фактические dependencies и не вычисляет требуемое значение повторно. В
частности, не гарантируются:

- относительный порядок независимых member expressions;
- момент generated assignment относительно других независимых rules;
- видимость setter side effects или mutation из nested mapping между rules;
- наличие либо отсутствие shallow snapshot при aliasing `source`, `previous`
  и `result`.

Поэтому declarative plan нельзя использовать как гарантию последовательной
mutation или атомарного swap aliased result. Если результат зависит от порядка
independent rules, setter/nested mapping side effects либо конкретной точки
чтения изменяемого object graph, алгоритм выражается через `Convert`.

### 7.6. Declarative control flow и captures

Structured `Construct` и `Members` являются конечным анализируемым DSL. В них
поддерживаются:

- expression-lambda;
- locals с initializer-ом, `const` и вложенные blocks;
- `if` / `else if` / `else`, несколько `return` и `throw`;
- statement `switch`, если каждый выбранный завершённый путь возвращает plan
  либо бросает exception;
- conditional- и switch-expressions;
- условный выбор whole plan, creation strategy, constructor/member value,
  `Auto()`, `Ignore()` и `Map(...)`.

Каждая ветка планируется отдельно для достижимой mapping operation. Условие,
selector, local и value не выполняются, если от них не зависит выбранный путь.
Требуемые выражения вычисляются ровно один раз, а остальные — ни разу.
Сохраняются их явные data/control dependencies и обычная C#-семантика внутри
каждого выражения; относительный порядок независимых member rules и их side
effects не задаётся. Declarative locals задают dependency для использующих их
выражений; последующая mutation такого local не поддерживается.

Во внешнем structured `Construct` или `Members` block не поддерживаются:

- locals без initializer-а, последующие/deconstruction/compound assignments и
  `++` / `--`;
- loops, `break` / `continue` и standalone statements только ради side effect;
- local functions, объявленные во внешнем declarative block;
- `try` / `catch` / `finally`, `using`, `lock`, labels / `goto`;
- `ref` / `using` locals, `unsafe` / `fixed`, `async` / `await` и `yield`.

Сложное вычисление выносится в обычный instance/static member mapper-а, сложное
получение result — в direct `Construct` либо `ByFactory`, а полностью специальный
алгоритм — в `Convert`. Direct `Construct`, factory body и `Convert`
переносятся как обычный синхронный C# block; внутри них доступны mutation,
loops, `try` / `finally`, nested local functions и остальные допустимые для их
сигнатуры синхронные конструкции.

Переносимый пользовательский код может обращаться к instance/static members
mapper-а, static API, типам, method groups и compile-time constants.
Configure-local compile-time constant подставляется как constant value.
Обычные Configure-locals, параметр `builder` и local functions, объявленные во
внешнем `Configure`, не захватываются: их runtime lifetime не совпадает с
lifetime generated mapper-а. Переиспользуемая логика должна быть обычным
member-ом mapper-а. Local functions внутри direct/factory/manual block
переносятся вместе с этим block.

Generated record `DestinationMembers` имеет properties с обычным `set`.
Object initializer и `with` входят в declarative plan composition, но это не
добавляет императивную сборку либо последующую mutation plan-а: assignments к
local plan variable и последовательное изменение его properties по-прежнему
не поддерживаются.

### 7.7. Граница result-dependent logic

Result-aware `Members` закрывает структурные member-rules, которым нужно
прочитать состояние фактически созданного result и затем выполнить
обычные generated assignments. Он не делает declarative pipeline общим
imperative lifecycle.

Граница проходит по фактической зависимости конкретного rule от `result`, а
не по выбранной перегрузке `Members`. Rule без такой зависимости сохраняет ту
же семантику и допустимые creation-time возможности в обеих формах.

Если алгоритму нужны последовательная зависимость от setter side
effects, mutation между assignments, замена result после member-фазы,
итоговая imperative validation или другой полностью ручной lifecycle, он
выражается через `Convert`. Обычные синхронные instance/static методы
mapper-а, включая методы с injected services, можно по-прежнему вызывать
внутри `Construct` и `Members`.

`BeforeMap`, `AfterMap`, middleware либо эквивалентные lifecycle hooks
обязательно будут поддержаны после v0; их точная форма ещё не выбрана.
Post-processing с replacement-result до того же будущего этапа не утверждается.
Async mapping, I/O orchestration, first-class business validation, private-state
bypass, runtime-only dynamic shapes и automatic reverse mapping также не
расширяют core v0; обратная pair объявляется явно.

### 7.8. Почему `Skip()` не нужен

Полный статический отказ от implicit member mapping уже выражается настройкой:

```csharp
builder.Map<Source, Destination>()
    .MemberSelection(MemberSelection.Explicit);
```

Если `Members` отсутствует, ни один body-member не маппится. При существующем
previous и отсутствии previous-aware `Construct` он останется result без
изменений.

Для динамического алгоритма, который в runtime иногда должен выполнить полный
no-op, используется `Convert`. Отдельный `Skip()` в v0 не добавляется;
first-class whole-plan no-op и общая patch/merge policy полностью отложены до
после v0. Исследование возможной null-assignment policy сохранено в
[`NULL_ASSIGNMENT_HANDLING_RESEARCH.md`](NULL_ASSIGNMENT_HANDLING_RESEARCH.md).

## 8. Полностью ручной mapping

### 8.1. `MappingContext`, call frame и единственная перегрузка

Тип текущей mapping-операции является частью `MappingContext` текущего вызова,
а не destination-specific previous-объекта:

```csharp
public enum MappingOperation
{
    Create = 1,
    Update = 2
}

public readonly struct MappingContext
{
    public MappingOperation Operation { get; }

    public IMapper Mapper { get; }
}
```

Оба типа находятся в namespace `Morphant.Context`, соответствующем папке
`Context` runtime-проекта. Поэтому consumer, использующий их по короткому
имени, подключает `using Morphant.Context;`.

`MappingOperation` описывает ровно одну выполняемую операцию и поэтому не
переиспользует flags-enum `MappingMode`. `Operation` доступен пользователю
только для чтения; его значение устанавливает mapper. Значение `0` намеренно
не является операцией, поэтому default-initialized enum отличается от
`Create` и `Update`.

`MappingContext` является immutable call frame текущего outer или nested
вызова. Morphant создаёт новый frame для каждого `Map`, передаёт его по
значению и не меняет после создания. Собственной reference identity у frame
нет; `default(MappingContext)` не является допустимым рабочим context.

Общее состояние всей mapping chain хранится отдельно во внутреннем
reference-type `MappingScope`:

| Call frame (`MappingContext`) | Общий `MappingScope` |
|---|---|
| Текущая `Operation` | Scoped mapper |
| Immutable и передаётся по значению | Будущий reference cache и внутренний chain state |
| Новый для каждого nested `Map` | Одна reference identity на всю chain |
| Описывает ровно текущий вызов | Завершается вместе с root `Map` |

Пользовательский per-call state не хранится ни во frame, ни в scope. После
post-v0 включения tuple roots он передаётся как обычная часть source и при
необходимости явно включается пользователем в source следующего nested
mapping-а.

Публичный root mapper и `context.Mapper` реализуют один контракт `IMapper`, но
являются разными экземплярами с разным lifetime. Root mapper начинает новую
mapping chain и создаёт новый scope для каждого публичного вызова.
`context.Mapper` является scoped-экземпляром, привязанным к уже существующему
scope. Отдельный `IContextualMapper`, полностью повторяющий `IMapper`, не
вводится.

Оба экземпляра видят один application-wide набор manual registrations и
используют `IServiceProvider` текущего DI-scope. `MappingScope` сохраняет
состояние одной mapping chain, но никогда не ограничивает набор доступных пар
конкретным `TypeMapper`, mapper-графом или assembly.

Source-only перегрузка scoped mapper создаёт nested frame с
`MappingOperation.Create`, а two-parameter перегрузка — с
`MappingOperation.Update`, даже когда переданный destination равен
`null`. Оба frame разделяют тот же scope, но `Operation` outer frame при этом
никогда не мутируется.

`Convert` находится на обычном pair-builder и имеет одну универсальную
перегрузку:

```csharp
Convert(
    Delegates.Convert<
        TSource?,
        TDestination,
        TDestination> mapping);
```

`TSource?` здесь означает исходное runtime-значение source, включая `null`,
когда конкретный source type его допускает. Для reference type параметр
nullable, для nullable value type сохраняется `Nullable<T>`, а non-nullable
value type не поднимается искусственно. В отличие от declarative lambda,
manual lambda всегда видит значение до `NullSourceHandling`.

`Option<TDestination>` использует non-null underlying destination по правилу
раздела 5. Поэтому explicit `null` никогда не превращается в `Some(null)` даже
в raw manual mapping: он представлен `Option.None`, а исходную операцию
дополнительно сообщает `MappingContext.Operation`.

Source-only перегрузки нет. Если сведения о вызове и mapping context не нужны,
пользователь намеренно игнорирует оба дополнительных параметра:

```csharp
.Convert((source, _, _) =>
    new Destination(source!.Id, source.Name));
```

`Option<TDestination>` и `MappingContext` передаются раздельно, поскольку
отвечают на разные вопросы. `Option` описывает наличие фактического
destination instance, а `MappingContext` — текущий call frame, включая его
операцию и scoped mapper для ручных nested mappings.
`MappingContext` является последним параметром, как и в generated
`ITypeMapper.Create(...)` / `ITypeMapper.Update(...)` contract.

### 8.2. Почему одного `Option<T>` недостаточно

В manual mapping не выполняются `NullSourceHandling` и
`NullDestinationHandling`. Поэтому пользователь должен различать:

- `Map(source)`;
- `Map(source, null)`;
- `Map(source, destination)`.

Два первых вызова не имеют экземпляра destination, но являются разными
операциями. Форма вызова хранится в `MappingContext.Operation`, а наличие
экземпляра — независимо от неё в `Option<TDestination>`.

Точные состояния:

| Вызов | `context.Operation` | `previous` |
|---|---|---|
| `Map(source)` | `Create` | `None` |
| `Map(source, null)` | `Update` | `None` |
| `Map(source, destination)` | `Update` | `Some(destination)` |

`Operation` и `Option` хранят два независимых факта: какая публичная
операция вызвана и существует ли фактический destination instance. Поэтому
для различения explicit `null` не требуется отдельная generic call-обёртка.

### 8.3. Семантика

```csharp
builder.Map<Source, Destination>()
    .Convert((source, previous, context) =>
    {
        if (source is null)
            return HandleNullSource(previous, context);

        if (context.Operation == MappingOperation.Create)
            return CreateDestination(source);

        if (!previous.TryGetValue(out var destination))
            return HandleExplicitNullDestination(source);

        Update(destination, source, context);
        return destination;
    });
```

В lambda передаётся immutable `MappingContext` текущего вызова. Nested mapping
вручную вызывается через scoped `IMapper`; передавать context явно не нужно:

```csharp
var address = previous.TryGetValue(out var destination)
    ? context.Mapper.Map<AddressDto, Address>(
        source.Address,
        destination.Address)
    : context.Mapper.Map<AddressDto, Address>(
        source.Address);
```

Scoped mapper сам создаёт новый frame для выбранной overload-ом nested
операции и сохраняет общий scope. Концептуальный dispatch выглядит так:

```csharp
// Root IMapper
var scope = new MappingScope(...);

try
{
    return scope.Dispatch(
        source,
        new MappingContext(MappingOperation.Create, scope.Mapper));
}
finally
{
    scope.Complete();
}

// context.Mapper
scope.ThrowIfCompleted();

return scope.Dispatch(
    source,
    new MappingContext(MappingOperation.Update, this),
    destination);
```

Конкретные constructors и внутренние методы здесь показаны только как
псевдокод; они не являются дополнительным public API.

`Operation` всегда описывает текущий вызов в mapping chain, а не корневую
операцию. Внутри nested `Convert` виден новый frame с собственной
операцией, а продолжившийся после него outer manual mapping по-прежнему имеет
свой неизменившийся frame. Ничего восстанавливать после вложенного вызова не
нужно.

Exception из nested mapping не меняет outer frame. Его можно поймать и
продолжить outer mapping; recursion и последовательная reentrancy используют
новые frame и остаются безопасными относительно `Operation`.

`Convert` полностью определяет результат во всех включённых
`MappingMode`-операциях. Внутри разрешён обычный C#:

- expression- и block-lambdas;
- условия, switch, циклы и несколько `return`;
- mutation;
- constructors и factories;
- record `with`;
- вызовы других методов и mapper-ов.

При выполнении `Convert`:

- `NullSourceHandling` не применяется;
- `NullDestinationHandling` не применяется;
- convention construction не применяется;
- convention member mapping не применяется;
- `Construct` и `Members` не выполняются;
- `Auto()`, `Ignore()`, `Map(...)`, `Create(...)`, `Update(...)`,
  `ByConvention()` и `ByFactory()` не являются DSL-маркерами и недоступны;
- ручные nested mappings доступны через `context.Mapper.Map(...)`;
- scoped mapper автоматически создаёт для вложенного вызова новый
  `MappingContext` и сохраняет общий scope;
- lambda возвращает настоящий `TDestination`;
- `MappingMode` по-прежнему определяет, какую публичную операцию можно вызвать.

Для одной пары разрешён ровно один `Convert`. Его смешивание с `Construct`,
`Members` или declarative constructor/member-specific configuration является
ошибкой конфигурации и должно диагностироваться. Унаследованные общие settings,
не имеющие эффекта в manual mapping, не запускают скрытый declarative pipeline.

### 8.4. Использование context за пределами `Convert`

`MappingContext` участвует не только в manual mapping. Declarative pipeline
использует его внутренне для каждого nested `Map` / `Create` / `Update`:
текущий вызов получает собственный frame, а все frame mapping chain разделяют
один scope.

Scope завершается в `finally` вместе с root `Map`. Сохранять
`context.Mapper` и вызывать его после завершения root mapping нельзя;
scoped mapper обязан проверить lifetime и немедленно бросить
`Morphant.Exceptions.MappingScopeCompletedException`.

Обычный root `IMapper` можно использовать параллельно: каждый root-вызов
получает независимый scope. Последовательные nested-вызовы, recursion и
reentrancy внутри одного scope поддерживаются. Параллельное использование
одного scoped mapper внутри одной mapping chain не поддерживается и не
получает thread-safety guarantee; это оставляет корректную основу для будущего
mutable reference cache без неявной синхронизации.

Однако пользовательским параметром `MappingContext` пока остаётся только в
`Convert`. Добавлять его в `Construct` или `Members` не нужно:

- declarative lambdas намеренно получают уже нормализованные source и
  previous после null handling;
- доступ к `context.Operation` позволил бы снова различать `Map(source)` и
  нормализованный `Map(source, null)`, обходя эту модель;
- declarative nested mapping уже выражается явным `Map(...)` marker.

В v0 отдельные per-call arguments и пользовательский context не добавляются.
После включения tuple roots strongly typed state передаётся обычным source:

```csharp
builder.Map<(Order Order, MappingState State), Invoice>()
    .Members((source, _) => new()
    {
        Total = Format(source.Order.Total, source.State.Culture),
        Address = Map((source.Order.Address, source.State))
    });
```

Tuple здесь не получает особой state-семантики: типы и порядок элементов
образуют source type, а nested propagation всегда записывается явно. Ни
`MappingContext`, ни `MappingScope`, ни overload-ы `IMapper` ради этого не
расширяются. Отдельный автоматически распространяемый per-call contract имеет
смысл повторно рассматривать только при подтверждённой потребности, которую
явный tuple-source не покрывает.

## 9. Null handling

### 9.1. Declarative mapping

Для `Construct` и `Members` null handling выполняется до mapping DSL.

Порядок остаётся таким:

1. Проверить source и применить эффективный `NullSourceHandling`.
2. Для `Update` проверить destination и применить эффективный
   `NullDestinationHandling`.
3. Сформировать нормализованный `Option<TDestination>`.
4. Выбрать `result` через configured/default `Construct` policy.
5. Если пользовательский direct/factory-код вернул `null`, немедленно вернуть
   его как авторитетный result.
6. Иначе применить `Members` и effective member conventions.

Когда declarative lambda начинает выполняться, source уже прошёл
`NullSourceHandling`. Она получает non-null underlying source: reference type
имеет non-null annotation, а `Nullable<T>` разворачивается в `T`. Поэтому
обычному declarative коду не нужны повторные null-check или `!`.

Для reference destination целевой enum и его семантика:

```csharp
public enum NullDestinationHandling
{
    Default = 0,

    Create,
    Throw
}
```

| Настройка | Поведение |
|---|---|
| `Throw` | Бросить `NullDestinationException` до `Construct` и `Members` |
| `Create` | Считать explicit `null` отсутствующим previous и перейти в no-previous construction branch |

`NullDestinationHandling.Create` не обещает новую identity: configured
`Construct` может использовать constructor, factory или cache. Публичная
операция при этом остаётся `Update`, поэтому дополнительно включать
`MappingMode.Create` не требуется; достаточно доступного `MappingMode.Update`.

После `NullDestinationHandling.Create` следующие вызовы намеренно
неразличимы внутри
declarative DSL:

```csharp
Map(source)
Map(source, null)
```

В обоих случаях `Construct` / `Members` получают `Option.None`. Именно поэтому
для `Members` достаточно `Option<TDestination>` без доступа к
`MappingContext.Operation`.

`NullSourceHandling` сохраняет текущие варианты и precedence. В частности,
если effective policy возвращает результат или бросает исключение, ни
`Construct`, ни `Members` не выполняются. Вариант `Throw` бросает
`NullSourceException`.

### 9.2. `null` из пользовательского creation-кода

Фактический destination могут вернуть две declarative ветки:

- direct `Construct`;
- `ByFactory` внутри structured `Construct`.

Если такая ветка возвращает `null`, он считается намеренным терминальным
результатом независимо от nullable-аннотации destination:

```csharp
var result = RunUserCreation(source, previous);

if (result is null)
    return null!;

ApplyMembers(source, previous, result);
return result;
```

Проверка нужна только для short-circuit member stage. Morphant не генерирует
специальное исключение, не заменяет `null` на previous, не выбирает другой
constructor/factory и не применяет повторно `NullDestinationHandling`.
`Construct` с параметром `previous`, вернувший `null`, тем самым намеренно заменяет
существующий destination на `null`.

Для non-nullable destination обычный C# nullability analysis по возможности
предупреждает в конфигурации. Пользователь может сознательно подавить это
предупреждение либо получить `null` из oblivious API; Morphant уважает такой
runtime-результат. Для nullable destination `null` является обычной
declarative конверсией, например `string -> Guid?`.

Constructor, convention и previous дают non-null result по своей природе и не
нуждаются в такой проверке. `null` вместо самого generated
`DestinationConstruction` или `DestinationMembers` является не destination-
результатом, а недопустимым DSL-plan и должен диагностироваться как ошибка
конфигурации.

### 9.3. Manual mapping

Для `Convert` обе null-handling настройки полностью обходятся. В lambda
передаются исходный source, фактический previous и `MappingContext`, чей
`Operation` сохраняет исходную форму вызова.

Это не fallback и не специальный mode настройки. Полная обработка `null`
является частью ручного алгоритма пользователя. Lambda возвращает свой
`TDestination` непосредственно: Morphant не добавляет guard, не применяет
`Members` и не переинтерпретирует `null`.

## 10. Точный declarative алгоритм

Концептуально `Map(source)` работает так:

```csharp
ApplyNullSourceHandling(source);

var previous = Option<Destination>.None;

var result = RunNoPreviousConstruction(source, previous);

if (result is null)
    return null!;

ApplyMembers(source, previous, result);

return result;
```

`RunNoPreviousConstruction` вызывает любую configured `Construct`-перегрузку,
поскольку previous отсутствует. Если `Construct` не настроен, structured
surface выполняет convention construction. Direct pair, включая opaque
destination, является ошибочной конфигурацией для reachable no-previous ветки
без configured `Construct`.

`Map(source, destination)` после null-предобработки работает так:

```csharp
ApplyNullSourceHandling(source);
var previous = ApplyNullDestinationHandling(destination);

Destination result;

if (!previous.HasValue)
{
    result = RunNoPreviousConstruction(source, previous);
}
else if (previousAwareConstructionConfigured)
{
    result = RunConstruction(source, previous);
}
else
{
    result = previous.Value;
}

if (result is null)
    return null!;

ApplyMembers(source, previous, result);

return result;
```

Проверка `result` концептуально показана единообразно. Generated code обязан
эмитить её только для direct/factory-веток, где `null` действительно возможен;
constructor, convention и previous дополнительных проверок не требуют.

`RunConstruction` никогда не подменяется другой configured lambda. Structured plan
lowering и direct lambda в итоге дают один настоящий `Destination result`. Для
пары существует не более одного `Construct`.

Если `Members` не настроен, `ApplyMembers` применяет только effective
`MemberSelection` conventions. Если generated member surface отсутствует, эта
стадия не содержит применимых members.
В форме `Members` с третьим параметром generator связывает фактически
выбранный non-null `result` непосредственно, без presence-wrapper, только с
выражениями, которые его используют. Lambda не является единым runtime-
callback и не образует отдельную member-фазу.

`ApplyMembers` обозначает единый effective plan, а generator может распределить
его части по разным допустимым фазам:

1. Generator объединяет inherited и local member rules независимо от формы
   перегрузки, выбирает declarative ветви и разрешает member-plan `with`-
   overlays. Заменённые rules удаляются вместе с ненужными dependencies.
2. Для structured `Construct` и effective `Members` строится общий path-sensitive
   dependency graph. Одинаковые bound subexpressions становятся одной
   computation node; direct/factory/manual C# blocks остаются непрозрачными.
3. Для structured constructor/convention branch result-independent значения,
   необходимые `init` и creation-time `required` rules, могут быть вычислены
   при создании объекта. Explicit constructor arguments сохраняют обычный
   порядок вызова.
4. Выражение, зависящее от `result`, вычисляется только после появления
   non-null instance. Setter/field rule тогда применяется post-construction;
   result-dependent creation-time rule является ошибочной конфигурацией.
5. Previous, factory и direct branches уже имеют result; доступные им
   post-construction rules применяются независимо от формы `Members`.
   Неприменимые `init` rules не вычисляются.
6. `null` factory/direct result завершает mapping до применения любых member
   rules. Rule, условие или ветка другого operation/result path также не
   вычисляются.

Generator вправе выбирать object initializer, временные переменные,
немедленные assignments и иной lowering. Каждое требуемое пользовательское
выражение вычисляется не более одного раза, соблюдаются фактические
dependencies и обычная C#-семантика внутри отдельного expression. Относительный
порядок независимых member expressions, момент generated assignments и
видимость их side effects друг для друга не гарантируются.

## 11. Pair eligibility, capabilities и generated API

### 11.1. Допустимость mapping-пары

Pair eligibility отделяется от конкретной declarative capability. Root mapping
в v0 является синхронным преобразованием одного уже материализованного
значения со статически известной верхнеуровневой формой в другое такое
значение. Поэтому mapping-пара допустима, если оба её корневых типа:

- являются допустимыми generic type arguments при минимальном C# 9 contract;
- могут быть однозначно названы из общего generated assembly-context без
  private/protected-привилегий конкретного mapper-а;
- после снятия верхнеуровневой `Nullable<T>`-обёртки не являются type
  parameter;
- не входят в сознательно отложенные root-категории ниже.

До специальной поддержки после v0 полностью исключаются в обеих позициях
mapping-пары:

- tuple roots: tuple syntax, `System.ValueTuple`, `System.Tuple` и типы,
  реализующие `System.Runtime.CompilerServices.ITuple`;
- sequence, collection и buffer roots: arrays, любой тип, реализующий
  `System.Collections.IEnumerable` кроме `string`, типы, реализующие
  `System.Collections.IEnumerator`, `IAsyncEnumerable<T>` или
  `IAsyncEnumerator<T>`, а также `Memory<T>`, `ReadOnlyMemory<T>` и
  `ReadOnlySequence<T>`;
- delegate roots: любой конкретный delegate type, а также базовые
  `System.Delegate` и `System.MulticastDelegate`;
- expression-tree roots: вся иерархия `System.Linq.Expressions.Expression`,
  включая `Expression<TDelegate>`;
- deferred/async roots: иерархия `Task`, `ValueTask`, `ValueTask<T>` и
  `Lazy<T>`;
- push-sequence roots: типы, реализующие `IObservable<T>`.

Collection-категория включает generic/non-generic sequence interfaces,
dictionaries, enumerators, async sequences, memory buffers и пользовательские
типы, реализующие соответствующие контракты. Для delegates, expression trees,
deferred/async и push values сначала нужна отдельная семантика либо явное
решение об их долгосрочной неподдерживаемости. Запреты симметричны для source и
destination и действуют также для direct `Construct` и `Convert`. Если типы
можно законно использовать в `ITypeMapper<TSource, TDestination>`, v0
генерирует только executable contract, обе операции которого бросают
`MappingConfigurationException`; construction, member и pair-extension
surfaces не генерируются. Забытая registration не превращается в runtime
lookup или скрытый manual fallback.

Категория определяется после снятия верхнеуровневой `Nullable<T>`-обёртки:
например, `ValueTask<int>?` также запрещён как root. Для разрешённого
underlying value type сама `Nullable<T>`-форма при этом сохраняет собственную
canonical identity mapping-пары.

Эти ограничения относятся только к корню пары. Значение любой отложенной
категории может оставаться типом обычного member-а, constructor parameter либо
generic argument внешнего разрешённого root-типа. Например,
`Envelope<Task<Result>>` остаётся допустимым root. В v0 вложенное значение
рассматривается целиком: оно доступно через warning-free implicit
C#-преобразование либо явное пользовательское expression, но Morphant не
выполняет element mapping, ожидание deferred result, expression rebinding или
другую специальную обработку.

Type parameter непосредственно в любой root-позиции запрещён независимо от
constraints:

```csharp
Map<T, Destination>();
Map<Source, T>();
Map<TSource, TDestination>();
Map<T?, Destination>();
```

Ограничения `class`, `struct`, `new()`, базовым классом или интерфейсом не
определяют точный верхнеуровневый тип и не создают исключения. Type parameter
внутри известного nominal root остаётся допустимым:

```csharp
Map<Page<T>, PageDto<T>>();
Map<Result<T>, Response<T>>();
```

Если сам известный root реализует или наследует одну из отложенных категорий,
соответствующий запрет всё равно применяется.

Технически исключаются `void`, pointers, function pointers, ref-like types,
error types, anonymous/unnameable types и типы, недоступные из общего generated
assembly-context. Это не продуктовые запреты, а формы, для которых невозможно
сформировать единый стабильный pair surface при C# 9. Вложенность mapper-а в
private/protected type не расширяет эту границу.

Остальные статически выразимые roots допустимы: built-in и BCL scalars, enums,
classes, structs, records, nullable value/reference forms, abstract classes,
interfaces и constructed generics с известной верхнеуровневой nominal-формой.
Их generic arguments могут содержать type parameters. `dynamic` имеет
каноническую identity `object` и не образует отдельную пару. Алиасы также не
меняют identity; root nullable reference annotation не создаёт вторую runtime
pair, тогда как `Nullable<T>` разрешённого underlying type остаётся
самостоятельным constructed value type.

### 11.2. Capability model

API отражает реальные возможности destination и не показывает бесполезные
declarative методы. Для каждой eligible pair capabilities выводятся независимо:

| Capability | Условие | Generated surface |
|---|---|---|
| Runtime contract | Любая eligible pair | Обе `Map`-операции; effective `MappingMode` остаётся единственным operation gate |
| Manual | Любая eligible pair | Один `Convert` на обычном pair-builder |
| Structured creation | Есть хотя бы один поддерживаемый доступный destination constructor, включая parameterless | `Construct`, возвращающий generated `DestinationConstruction` |
| Direct creation | Поддерживаемый constructor surface отсутствует либо destination намеренно opaque | `Construct`, возвращающий настоящий `TDestination` |
| Members | Для structured destination есть поддерживаемый body-member; для direct destination есть post-construction assignable member; destination не opaque | Generated `DestinationMembers` и обе альтернативные `Members`-перегрузки |
| Collection / projection | Не входят в v0 capability model | Никакого generated surface; рассматриваются после v0 на отдельных этапах |

Structured и direct creation взаимоисключающие: eligible pair получает ровно
одну форму `Construct`. `Convert` доступен для той же пары, но является
альтернативой всему declarative pipeline, а не fallback отдельной
неподдерживаемой ветки. Source shape сама по себе не меняет destination
surface.

Отсутствие members не убирает declarative surface. Pair с поддерживаемым
constructor получает structured `Construct`, а pair без него — direct
`Construct`; `Update` всё равно может вернуть previous без изменений.
No-previous ветка direct pair требует configured lambda. Единственным общим
gate для публичной операции остаётся эффективный `MappingMode`.

Под «есть member» понимается member, реально включаемый в generated
`DestinationMembers`, а не любой symbol типа. Static members, indexers,
get-only properties, readonly fields и другие неподдерживаемые формы не
считаются.

Под «есть constructor» понимается instance-constructor любой arity, который
generator может использовать для создания данного destination. Недоступные и
неподдерживаемые constructors не считаются; constructor abstract-типа сам по
себе не делает тип создаваемым. Built-in, enum и отдельно определённые общей
type policy scalar-категории получают direct surface, даже если metadata типа
технически содержит public constructors: Morphant намеренно не моделирует их
как structural constructor DSL.

Constructor и destination member accessibility вычисляются из общего
generated assembly-context, а не из lexical context конкретного mapper-а.
Поэтому public и доступные `internal` symbols образуют единый стабильный
surface, тогда как private/protected symbols не появляются даже у mapper-а,
который благодаря вложенности мог бы обратиться к ним вручную. Одна и та же
destination definition тем самым всегда получает одну форму construction и
один member surface независимо от набора зарегистрировавших её mapper-ов.

В v0 opaque/direct scalar policy сохраняет полную проверенную границу прежнего
surface:

- C# built-in scalar types, включая `object`, `string`, numeric types, `char`,
  `bool`, `nint` и `nuint`;
- enums;
- `Guid`, `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeSpan`,
  `Half`, `Int128` и `UInt128`;
- `Uri`, `Version`, `BigInteger`, `Complex`, `Rune`, `Index` и `Range`.

Верхнеуровневая nullable value wrapper не превращает такой destination в
structured type. Opaque означает атомарный destination: он не получает ни
`Members`, ни automatic convention construction. Custom struct/record не
становится opaque только из-за value semantics и получает capabilities по
обычным правилам своих constructors и members.

Direct `Construct` non-opaque destination семантически соответствует
structured-ветке `new(ByFactory(...))`: он получает уже созданный instance, к
которому Morphant может применить обычные setter-rules и mutable-field rules.
`required` не исключает такие members, если они остаются post-construction
assignable. `init`-only properties в direct member surface не входят. Direct
result не является окончательным результатом в смысле `Convert`. Opaque
destination member surface не получает.

Например, interface не имеет constructor surface, но может независимо иметь
writable body-members:

```csharp
builder.Map<Source, IDestination>()
    .Construct(source => factory.Create(source.Id))
    .Members((source, _) => new()
    {
        Name = source.Name
    });
```

Здесь direct lambda получает экземпляр, а declarative member plan продолжает
иметь самостоятельную ценность. Обычный `set` либо mutable field доступен в
`Members`, в том числе при `required`; `init`-only property в этом surface не
генерируется.

Отдельного служебного creation type для scalar, opaque value object,
factory-only class, interface или abstract destination не создаётся. Их direct
surface сохраняет standard null handling. Declarative member plan дополнительно
доступен только non-opaque destination с поддерживаемыми post-construction
members, поэтому `Convert` нужен лишь для действительно ручного алгоритма.

### 11.3. Settings matrix

После удаления `TemplateMode` settings наследуются общим порядком
`current map -> included base map -> current mapper root -> connected base
mapper roots -> assembly -> library default`; `Default` продолжает поиск менее
конкретного уровня. Base-уровни появляются только через явно распознанные
`base.Configure(builder)` и
`IncludeBase<TBaseSource, TBaseDestination>()`, описанные в разделе 4.1.
Applicability определяется выбранной model и capabilities пары:

На одном C#-уровне побеждает последний распознанный вызов конкретной setting,
включая последний вызов с `Default`: он очищает решение этого уровня и
возобновляет наследование. Итоговая root-setting применяется ко всем pair-ам
mapper-а независимо от позиции вызова относительно `Map(...)` в линейном
`Configure`. То же last-call-wins правило действует на map-level chain.

Assembly-level defaults задаются только compiler-visible MSBuild properties,
без дублирующих assembly attributes. Их итоговое значение определяется
обычным порядком MSBuild imports до запуска generator-а; отсутствующее,
пустое либо `Default` значение продолжает общую precedence chain.

| Setting | Declarative mapping | `Convert` |
|---|---|---|
| `MappingMode` | Включает `Create`, `Update` либо обе операции | Применяется так же и остаётся единственным effective setting manual mapping-а |
| `NullSourceHandling` | Выполняется до `Construct` / `Members` | Не применяется |
| `NullDestinationHandling` | Выполняется перед previous normalization только в `Update` | Не применяется |
| `MemberSelection` | Управляет неуказанными supported body-members; работает и после direct `Construct` | Не применяется |
| `ConstructorSelection` | Применяется только к structured convention / `ByConvention` creation | Не применяется |
| Boxing policy | Ограничивает только automatic constructor/member conversions; explicit expressions остаются обычным C# | Не применяется |
| `UnmappedMemberValidation` | Проверяет только mapping plan, который строит Morphant; direct creation body не анализируется как набор member mappings | Не применяется |

Library defaults сохраняются: `MappingMode.CreateAndUpdate`,
`NullSourceHandling.ReturnNull`, `NullDestinationHandling.Create`, `MemberSelection.Auto`,
`ConstructorSelection.Unambiguous`, разрешённый automatic boxing и
`UnmappedMemberValidation.None`.

`NullabilityMismatchValidation` в целевой API не входит. Automatic
constructor/member mappings допускаются только при warning-free implicit C#-
преобразовании, а explicit expressions проверяет compiler. Если после v0
понадобится массовая политика nullable-to-non-nullable mapping-а, она будет
проектироваться отдельно, а не предрешается v0-setting-ом.

Inherited setting, неприменимая к конкретной pair, просто не имеет эффекта: её
уровень может обслуживать другие mappings. Явная map-level setting, которую
выбранная model принципиально обходит, является ошибкой конфигурации. Поэтому
у manual pair разрешён только `MappingMode`, а explicit null/member/constructor
policy должна диагностироваться. Для direct declarative pair явно заданный
`ConstructorSelection` также ошибочен; остальные declarative settings работают
на своих стадиях, даже если на конкретной pair не найдено ни одного кандидата
для warning или conversion.

Частичная capability никогда не включает скрытый fallback. Недоступная
operation, отсутствующий обязательный direct `Construct`, невозможный explicit
rule или setting без требуемой capability дают diagnostic. До реализации
соответствующей диагностики C#-legal generated operation бросает
`Morphant.Exceptions.MappingConfigurationException`, но не переключается на
manual, другую creation-ветку или runtime discovery.

### 11.4. Carry-forward contract generated surface

Новый `DestinationConstruction` / `DestinationConstructorParameters` /
`DestinationMembers` surface сохраняет уже проверенные UX-контракты прежних
template-types. Разделение API не является причиной заново упрощать либо
переопределять их.

Nullability generated inputs зеркалит input-контракт соответствующего
destination constructor parameter или body-member:

- сохраняются nullable value/reference types, вложенные annotations,
  `AllowNull`, `DisallowNull` и oblivious context;
- generic argument `Member<T>` / `ConstructorParameter<T>` отражает тип
  принимаемого значения, а внешняя wrapper-аннотация допускает `null` только
  тогда, когда `null` является валидным explicit rule;
- nullable-аннотация wrapper-а не делает parameter optional: required
  destination input, допускающий `null`, остаётся required generated
  constructor parameter без default value; optionality задаётся только
  default argument-ом;
- optional non-nullable constructor parameter использует suppressed `null!`
  только как generated omission sentinel, не ослабляя публичную nullable-
  аннотацию parameter-а;
- nullability, зависящая от effective mapping settings, выводится только после
  разрешения этих settings и не подменяется общей консервативной аннотацией.

Generated documentation наследуется через `inheritdoc` от destination type,
constructors и members. Если исходной документации нет, Morphant генерирует
короткий содержательный fallback summary для plan type, его overload-ов и
properties. Применимый `ObsoleteAttribute`, включая message и `error`,
переносится на соответствующий generated surface, чтобы IntelliSense и
compiler не скрывали устаревание исходного API.

IntelliSense и source output имеют стабильный смысловой порядок:

- destination constructors следуют declaration order;
- объединённые constructor-parameter properties следуют порядку первого
  появления parameter-а в constructors;
- body-members следуют base-first declaration order с уже описанными hiding-
  rules;
- overload-ы `Construct` / `Members` и их XML documentation сохраняют один
  детерминированный порядок между regeneration-ами.

Для generic destination generator создаёт один generic plan на original
destination definition, а не отдельный plan для каждой closed pair. Он
воспроизводит type parameters содержащих и вложенного типов, их порядок,
nullable/oblivious contract и все допустимые C# constraints. Несколько
`Envelope<User>` / `Envelope<Order>` используют один условный
`EnvelopeMembers<T>` surface; runtime registrations самих closed mapping-пар
при этом остаются независимыми.

Alpha-equivalent open pair shapes разных mapper-ов также получают один общий
pair-specific extension. Его type parameters соответствуют свободным
параметрам source/destination shape, а `where`-ограничения выводятся только из
generic definitions, входящих в эти source/destination types и необходимых
для корректности generated сигнатуры. Дополнительные constraints конкретного
mapper-а не копируются: mapper-ы `where T : class` и `where T : struct` могут
использовать один unconstrained extension для `Source<T> -> Destination<T>`,
если сами `Source<>` и `Destination<>` не ограничивают `T`. Constraints
generic construction/member plan по-прежнему точно воспроизводят destination
definition.

Минимальный user language contract остаётся C# 9. Generated files
детерминированы, используют CRLF, начинаются с `// <auto-generated />` и
`#nullable enable`. Hint name следует схеме
`Morphant.Generated.<ArtifactKind>.<StableIdentity>.g.cs`; stable hash
добавляется только при реальном case-insensitive collision после
sanitization, а не ко всем artifacts по умолчанию.

Physical artifacts разделены по ответственности: construction plan использует
kind `Construction`, member plan — `Member`, `Construct` / `Convert` methods —
`MappingExtension`, а `Members` methods — `MemberExtension`. Оба extension-
artifact-а дополняют одну internal partial class
`MorphantGeneratedMappingExtensions`; разделение файлов не создаёт второй
пользовательский fluent surface.

Plan types находятся в destination-relative namespace `.Morphant.Generated`.
Дополнительное слово `Morphant` в `DestinationConstruction`,
`DestinationConstructorParameters` или `DestinationMembers` не добавляется:
namespace уже изолирует generated surface, а реальные collisions разрешаются
детерминированной naming policy. Для destination из global namespace пустой
destination-prefix даёт `Morphant.Generated`, а ссылки используют
`global::Morphant.Generated`. Искусственный segment `Global` не добавляется:
он не является C# alias `global::` и создавал бы коллизию с реальным
destination namespace `Global`.

## 12. Application dispatch и deterministic lookup

### 12.1. Граница runtime `IMapper`

`IMapper` является единой точкой входа во все mappings, зарегистрированные в
приложении. Он не привязан к конкретному `TypeMapper`, compilation или
mapper/profile graph. В core v0 application-wide множество кандидатов образуют
вручную зарегистрированные closed
`ITypeMapper<TSource, TDestination>` services текущего
`IServiceProvider`.

Concrete `TypeMapper` остаётся единицей объявления конфигурации, генерации и
DI-активации. Он может иметь собственные dependencies, но его тип и assembly
не входят в ключ обычного lookup и не образуют скрытый mapping scope. Перенос
pair между mapper-классами сам по себе не должен менять поведение вызова, если
соответствующая exact-pair registration обновлена.

Root `Mapper` получает `IServiceProvider` текущего DI-scope. Для каждой pair он
запрашивает только
`IEnumerable<ITypeMapper<TSource, TDestination>>`, поэтому generated mapper и
его scoped/transient dependencies создаются тем provider-ом, который
обслуживает текущий публичный вызов. Набор registrations считается
зафиксированным после построения application provider.

В core v0 нет `AddMorphant(...)`, generated manifests, registration assembly
attributes и automatic assembly scanning. Пользователь вручную регистрирует
root `IMapper` и каждую closed `ITypeMapper<TSource, TDestination>` pair.
Convenience API и manifest wiring проектируются отдельно после v0. Runtime
reflection для поиска pair не требуется.

### 12.2. Lookup law v0

Lookup key обычного `Map<TSource, TDestination>` — canonical type pair из
раздела 11. Mapper-type, assembly и порядок DI registrations в этот ключ не
входят. Provider возвращает все кандидаты pair, а dispatch использует только
их количество:

| Кандидаты canonical pair | Поведение |
|---:|---|
| `0` | `MappingNotFoundException` |
| `1` | Единственный mapper выполняется |
| `2+` | `AmbiguousMappingException` |

Несколько registrations одной canonical pair допустимы, в том числе в разных
`TypeMapper` и assemblies. Само их наличие не является generator diagnostic
или startup error. Неоднозначность наблюдаема только при фактическом
безымянном lookup этой pair.

Morphant никогда не выбирает первый или последний mapper и не зависит от
порядка registrations. `MappingMode` является capability выбранного
mapping-а, а не дополнительной частью lookup identity и не используется для
скрытого выбора между повторными pair registrations. При ambiguity ни один
candidate mapping method не вызывается; момент создания самих service
instances определяется текущим `IServiceProvider`.

Если единственная registration разрешилась в `null`, вызов бросает
`InvalidMappingRegistrationException`. Количество registrations проверяется
раньше значения кандидата: две и более registrations остаются ambiguity, даже
если одна из них разрешилась в `null`.

### 12.3. Root и nested dispatch

Root `IMapper.Map(...)` начинает новую mapping chain, создаёт `MappingScope` и
выполняет application-wide lookup. Declarative nested `Map` / `Create` /
`Update` и ручной `context.Mapper.Map(...)` используют тот же набор
registrations и тот же текущий `IServiceProvider`, но создают новый immutable
call frame внутри уже существующего scope.

Nested lookup не предпочитает mapping из `TypeMapper`, которому принадлежит
outer pair, и не ограничивается его assembly. Для одной canonical pair root и
nested вызовы применяют одинаковое правило `0 / 1 / 2+`; неоднозначность не
разрешается через outer mapper, call stack или порядок регистрации.

### 12.4. Post-v0 путь к keyed mappings

После v0 registry можно совместимо расширить явным ключом варианта. Рабочая
модель descriptor-а тогда имеет lookup identity
`(canonical pair, service key)`, где отсутствие ключа означает default-вариант.
Core-shape `IMapper.Map(...)` и generated
`ITypeMapper<TSource, TDestination>.Create(...)` / `Update(...)` при этом не
меняются.

Возможный terminal extension API:

```csharp
var destination = mapper
    .From(source)
    .To<Destination>()
    .WithServiceKey("public");
```

`WithServiceKey` здесь является рабочим именем, а не принятым API. Если ключ
будет принадлежать собственному registry Morphant, а не настоящей keyed DI
registration, точнее может оказаться `WithMappingKey`. Сам ключ не следует
ограничивать строкой; совместимая внутренняя форма — `object?`.

Перед добавлением keyed mappings отдельно согласуются:

- назначается ли ключ всему concrete `TypeMapper` или отдельной pair;
- наследует ли declarative nested mapping текущий ключ;
- разрешён ли fallback keyed lookup к default-варианту;
- как выглядит terminal fluent API для обеих mapping-операций;
- что происходит при нескольких кандидатах с одной pair и одним ключом.

Этот эскиз резервирует extension path, но не добавляет keyed semantics в v0 и
не делает текущий unkeyed lookup зависимым от будущего имени API.

### 12.5. Runtime polymorphism после v0

В v0 runtime-тип source не меняет requested canonical pair. Вызов
`Map<Animal, AnimalDto>` всегда разрешает `Animal -> AnimalDto`, даже если
фактический source является `Dog` и отдельно зарегистрирована
`Dog -> DogDto`. `IncludeBase<TBaseSource, TBaseDestination>()` наследует
только mapping-конфигурацию и не включает runtime dispatch.

Нестандартный polymorphic алгоритм выражается через `Convert` с явным
type-switch и exact nested mappings. Основной массовый сценарий polymorphic
collection elements отложен вместе с collection support. Поэтому базовые
interfaces, call frames и dispatch contract в v0 не расширяются.

После v0 registry можно совместимо дополнить explicit derived links на
конкретном base descriptor-е. Рабочее направление использует отдельный от
`IncludeBase<TBaseSource, TBaseDestination>()` API с условным именем
`IncludeDerived<TSource, TDestination>()`, closed-world generated dispatcher и
most-specific selection. Оно не предусматривает `IncludeAllDerived`, поиск
всех assignable application registrations или зависимость от порядка
регистрации.

Base descriptor сначала выбирается обычным правилом `0 / 1 / 2+`; только затем
рассматриваются его explicit links. Неизвестный subtype использует base
mapping, а несколько несравнимых наиболее конкретных interface-кандидатов
дают ambiguity. Derived pair снова разрешается через application-wide exact
lookup.

Для будущего polymorphic `Update` derived branch допустима только при
`null` previous либо runtime-совместимом derived destination. Несовместимый
previous обрабатывает base mapping: runtime source сам по себе не разрешает
молча выбросить destination и вызвать derived `Create`. Projection остаётся
отдельной capability и не получает client-side fallback.

Точный API, транзитивность links, keyed propagation, collection lifecycle и
observable errors согласуются после v0. Полное исследование сохранено в
[`RUNTIME_POLYMORPHISM_RESEARCH.md`](RUNTIME_POLYMORPHISM_RESEARCH.md).

### 12.6. Cycles и shared references после v0

В v0 Morphant не сохраняет reference identity автоматически и не гарантирует
завершение cyclic object graph. `MappingScope` уже отделён от immutable
`MappingContext` и остаётся совместимой chain-wide точкой для будущего cache,
поэтому публичные mapper interfaces ради отсрочки не расширяются.

Будущая built-in policy рассматривается как opt-in. Её рабочий default — не
выполнять tracking. Cache идентифицирует entry по reference identity source и
identity уже разрешённого mapping descriptor-а, а не только по destination
type. Выбранный result регистрируется после `Construct`, но до `Members`:
setter/field cycle тогда может замкнуться, а constructor, `init` и required
initializer cycle до появления result остаётся неразрешимым.

Повторный source должен вернуть тот же result без повторного выполнения rules.
Для `Update` другой non-null previous при уже существующей cache entry
является reference conflict, а не основанием молча выбрать первый instance.
`Convert`, custom handler, `MaxDepth` и projection не получают эту
семантику автоматически.

Реализация, точное имя setting и отдельные failures этой policy отложены до
после v0.
Полное исследование сохранено в
[`REFERENCE_HANDLING_RESEARCH.md`](REFERENCE_HANDLING_RESEARCH.md).

### 12.7. Projection после v0

`IQueryable` projection однозначно исключена из v0. Публичного `Project(...)`,
projectable capability и special expression-tree roots нет. Точный public
contract, expression-compatible subset и внутренняя representation будут
спроектированы отдельным post-v0 этапом; текущая спецификация не обещает
client-side fallback и не накладывает ради будущей projection дополнительные
ограничения на production implementation v0.

### 12.8. Generic, runtime-type и multi-source boundary

Constructed generic root со статически известной nominal-формой является
обычной canonical pair. Например, `Page<Order> -> PageDto<Order>` разрешается
тем же exact lookup, что и любая non-generic pair. Generic mapper также может
сгенерировать contract для `Page<T> -> PageDto<T>`, поскольку type parameter
находится внутри известного root, а не непосредственно в root-позиции.

Это не создаёт open-generic registration. Application dispatch v0 видит только
явно зарегистрированные closed mappings. Поэтому явно зарегистрированный
`PageMapper<Order>` предоставляет closed pair, но dispatch не выводит `T` из
запрошенных типов, не закрывает `PageMapper<T>` автоматически и не
сопоставляет generic definitions. Все closed pairs следуют обычному правилу
`0 / 1 / 2+`.

Generic arguments могут содержать type parameters, nullable-типы и даже
категории, запрещённые непосредственно как root. Например,
`Envelope<Task<T>>` допустим: Morphant рассматривает вложенный `Task<T>` как
единое значение и не применяет к нему async semantics. Каждый полный
constructed root должен оставаться выразимым из общего generated
assembly-context; reflection-обхода для недоступных типов нет.

Reference nullability не входит в runtime identity и внутри generic
arguments: `Page<string>` и `Page<string?>` являются одной canonical pair.
`Page<int>` и `Page<int?>` различаются, потому что `Nullable<int>` является
отдельным CLR-типом.

Type parameter непосредственно в root-позиции, open-generic registration и
mapping по runtime source/destination `Type` отсутствуют в v0. Их можно
добавить после v0 отдельными registry capabilities без изменения базового
generic `IMapper` contract, но текущий lookup не делает runtime inference или
fallback к generic definition.

Tuple/multi-source support также остаётся после v0. Будущая tuple является
обычным `TSource`, поэтому специальные overloads `IMapper` на два или три
source не нужны. Canonical identity учитывает типы и порядок tuple-elements,
но не их имена. Пользовательский state является обычным элементом source и
передаётся в nested tuple mappings явно, без ambient propagation.

Отдельно от runtime dispatch generator проверяет pair shapes внутри одного
generic mapper contract. Если две различающиеся registrations могут стать
одинаковым `ITypeMapper<TSource, TDestination>` при подстановке type parameters,
обе конфликтующие pair не генерируются и требуют configuration diagnostic.
Независимые legal pair того же mapper-а продолжают генерироваться. Это правило
действует и для вложенных constructed roots (`Box<T>` против `Box<int>`);
constraints не используются как неявное доказательство неравенства типов.
Application-wide правило нескольких registrations здесь не помогает: конфликт
возникает раньше, при формировании списка interfaces и explicit implementations
одного closed mapper type.

### 12.9. `IncludeMembers` после v0

First-class convention flattening обязательно входит в post-v0 roadmap.
Будущий `IncludeMembers` подключает выбранный вложенный либо дополнительный
source-object как источник convention candidates сразу для набора destination
members. Это самостоятельная capability:

- explicit rule `City = source.Address.City` покрывает один member, но не
  подключает `Address` к conventions;
- tuple/multi-source определяет root source shape, но сам по себе не говорит
  искать candidates внутри tuple-elements;
- обычный nested `Map(...)` создаёт значение одного destination-place и также
  не заменяет flattening.

В v0 `IncludeMembers` не генерируется. Точные API, precedence между root и
included candidates, null semantics, ambiguity diagnostics и композиция через
`IncludeBase<TBaseSource, TBaseDestination>()` согласуются отдельным post-v0
этапом. Текущая candidate model и application dispatch не должны блокировать
его добавление, но до этого явный flattening остаётся обычным member
expression.

## 13. Основные сценарии

### 13.1. Полностью convention mapping

```csharp
builder.Map<Source, Destination>();
```

Поведение:

- `Map(source)` создаёт destination по convention;
- `Map(source, destination)` использует destination как result;
- body-members маппятся по effective conventions.

### 13.2. Явный constructor и единый member plan

```csharp
builder.Map<UserDto, User>()
    .Construct(source => new(
        id: source.Id,
        tenantId: Auto()))
    .Members((source, _) => new()
    {
        Name = source.Name,
        Email = source.Email,
        RequiredCode = source.Code
    });
```

В `Create` выполняются `Construct` и `Members`. В обычном `Update` source-only
`Construct` не выполняется, previous становится result, а применимые member rules
обновляют его. `RequiredCode` настраивается только в `Members`, независимо от
того, является ли он `set`- или `init`-member destination.

### 13.3. Условное переиспользование или replacement

```csharp
builder.Map<CustomerDto, Customer>()
    .Construct((source, previous) =>
    {
        if (previous.HasValue &&
            previous.Value.TenantId == source.TenantId &&
            !previous.Value.IsFrozen)
        {
            return previous;
        }

        return new(
            source.Id,
            source.TenantId);
    })
    .Members((source, previous) => new()
    {
        Name = source.Name,
        Revision = previous.HasValue
            ? previous.Value.Revision + 1
            : 1
    });
```

`Construct` с параметром `previous` является полным выбором result для обоих публичных
вызовов. `Members` применяется уже к выбранному result.

### 13.4. Всегда создавать replacement

```csharp
builder.Map<Source, Destination>()
    .Construct((source, _) => new(source.Id))
    .Members((source, _) => new()
    {
        Name = source.Name
    });
```

Двухпараметрический `Construct` намеренно игнорирует previous и получает result в
обеих операциях.

### 13.5. Factory плюс members

```csharp
builder.Map<OrderDto, Order>()
    .Construct(source =>
        new(ByFactory(() => orderFactory.Create(source.Id))))
    .Members((source, _) => new()
    {
        Number = source.Number
    });
```

Factory выполняется только в no-previous ветке source-only `Construct`. При
обычном `Update` используется previous и применяется `Number`.

### 13.6. Direct factory-only destination плюс members

```csharp
builder.Map<OrderDto, IOrder>()
    .Construct((source, previous) =>
        previous.HasValue && CanReuse(previous.Value, source)
            ? previous.Value
            : orderFactory.Create(source.Id))
    .Members((source, _) => new()
    {
        Number = source.Number
    });
```

У interface нет constructor surface, поэтому `Construct` возвращает настоящий
`IOrder`. Возврат `previous.Value` сохраняет existing instance; factory даёт
replacement. В обеих ветках применимый member plan выполняется после выбора
result.

### 13.7. Scalar и opaque value object

```csharp
builder.Map<Order, decimal>()
    .Construct(source =>
        source.Items.Sum(x => x.Price * x.Count));

builder.Map<string, OrderNumber>()
    .Construct(OrderNumber.Parse);

builder.Map<string, Guid?>()
    .Construct(source =>
        Guid.TryParse(source, out var value)
            ? value
            : null);
```

Для destination без structural constructor surface direct `Construct` сохраняет
обычный declarative pipeline без искусственного creation-plan и без перехода к
`Convert`. В последнем примере `null` является авторитетным терминальным
результатом; member stage после него не выполняется.

### 13.8. Immutable или сложный ручной mapping

```csharp
builder.Map<SnapshotDto, Snapshot>()
    .Convert((source, previous, _) =>
    {
        if (source is null)
            return default!;

        if (previous.TryGetValue(out var destination) &&
            destination.Version == source.Version)
        {
            return destination with
            {
                Name = source.Name
            };
        }

        return new Snapshot(
            source.Id,
            source.Name,
            source.Version);
    });
```

Никакого generated `with`-DSL для клонирования самого destination здесь не
требуется; member-plan `with` из раздела 7.2 решает другую задачу.

### 13.9. Immutable `Update` в v0

Declarative mapping уже может условно сохранить previous либо явно построить
replacement через previous-aware `Construct`:

```csharp
builder.Map<SnapshotDto, Snapshot>()
    .Construct((source, previous) =>
    {
        if (previous.HasValue &&
            previous.Value.Id == source.Id &&
            previous.Value.Name == source.Name)
        {
            return previous;
        }

        return new(source.Id);
    })
    .Members((source, _) => new()
    {
        Name = source.Name
    });
```

Если `Name` является `init`-only, он сохраняется при выборе previous и попадает
в object initializer при выборе constructor-result. Для обычного immutable
class все сохраняемые значения, которых нет в source, пользователь явно
переносит через constructor/member rules. Для record-copy с сохранением всех
остальных значений используется `Convert` и обычный C# `with`, как в
предыдущем примере.

В v0 Morphant не выводит такое equality-условие автоматически, не добавляет
`ByCopy` и не клонирует record только из-за наличия неприменимого после создания
member-а. Source-only `Construct` при существующем previous не выполняется и
replacement-path не образует. Это сохраняет явный контроль identity,
copy-constructor semantics и derived runtime type.

Статически пустая existing-ветка является корректным no-op: declarative
`Update` возвращает тот же destination, если не выбирает replacement и не
содержит применимых post-construction assignments. Source-only `Construct`
при этом не выполняется, а неприменимые creation-time expressions не
вычисляются. Previous-aware `Construct` нужен только для реального выбора
reuse/replacement, а не как обязательное подтверждение сохранения identity.
Доступность `Update` означает наличие операции, но не гарантирует mutation;
полнота mapping-а контролируется `UnmappedMemberValidation`, а неприменимые
explicit rules — собственными diagnostics.

После v0 запланирована отдельная opt-in setting условной reconstruction. Она
сможет вычислить creation-only member candidates до первой generated mutation,
сравнить их со значениями previous и выбрать replacement только при реальном
отличии; при равенстве previous identity сохраняется. Это надстройка над тем же
creation/member plan, а не новая creation-ветка и не часть
`NullAssignmentHandling`. Её public name, equality contract, reconstruction
source, factory/derived behavior и точный evaluation order будут согласованы
отдельно; до этого скрытой политики сравнения или реконструкции нет.

## 14. Ошибочные и конфликтующие конфигурации

### 14.1. Compile-time diagnostics

В целевом дизайне diagnostics должны покрыть как минимум:

- повторный `Construct` для одной pair, включая вызовы разных перегрузок;
- любой второй локальный `Members`; форма перегрузки значения не имеет;
- повторный `Convert`;
- смешивание `Convert` с `Construct` или `Members`;
- pair-specific constructor/member settings, несовместимые с manual mapping;
- достижимый explicit `init`-rule либо creation-time `required`-rule structured
  surface, который невозможно применить в конкретной creation branch: result
  уже создан factory code либо value/условие rule транзитивно зависит от ещё
  не созданного result; previous-result сохраняет такой member без вычисления
  неприменимого expression;
- reachable no-previous branch direct surface без configured `Construct`;
- `null` вместо generated `DestinationConstruction` или `DestinationMembers`
  plan;
- невозможный explicit constructor/member marker;
- две registrations одного generic mapper-а, чьи pair shapes могут
  унифицироваться при подстановке type parameters и породить одинаковый
  generated `ITypeMapper` contract;
- отсутствие кандидата при runtime lookup canonical pair;
- неоднозначный безымянный runtime lookup при двух и более кандидатах pair.

Несколько registrations одной canonical pair сами по себе не являются
ошибочной конфигурацией. Ошибка возникает только тогда, когда конкретный вызов
не может выбрать ровно одного кандидата.

Diagnostics остаются отдельной реализационной фазой, но отсутствие готового
diagnostic не должно вводить скрытый fallback на другой mapping algorithm.

### 14.2. Observable runtime failures

Все продуктовые ошибки маппинга, создаваемые самим Morphant, и все исключения
из generated code наследуются от публичного
`Morphant.Exceptions.MorphantException`. Зафиксированы следующие типы:

| Состояние | Exception |
|---|---|
| Invalid либо непереносимая mapping-конфигурация | `MappingConfigurationException` |
| Операция отключена effective `MappingMode` | `MappingOperationNotSupportedException` |
| Null source/destination отвергнут policy | `NullSourceException` / `NullDestinationException` |
| Exact-pair lookup дал `0`, `2+` либо единственный `null` | `MappingNotFoundException` / `AmbiguousMappingException` / `InvalidMappingRegistrationException` |
| Scoped mapper использован после завершения root call | `MappingScopeCompletedException` |
| Adaptive nested destination runtime-несовместим | `NestedDestinationTypeMismatchException` |
| Declarative switch не выбрал ветку | `UnmatchedMappingSwitchException` |
| `Option<T>.Value` прочитан у `None` | `OptionValueMissingException` |
| Compile-time DSL API вызван как runtime API | `RuntimeInvocationNotSupportedException` |

Обычная проверка предусловий рукописного public API следует соглашениям .NET.
В частности, `new Mapper(null)` бросает `ArgumentNullException` с
`ParamName == "serviceProvider"`; это не отдельная продуктовая ошибка
маппинга и не часть иерархии `MorphantException`.

Если C# способен объявить `ITypeMapper<TSource, TDestination>`, invalid либо
unsupported состояние не оставляет partial mapper незавершённым. Generated
mapper сохраняет interface и обе операции; недоступная operation получает
typed exception stub, а доступная остаётся исполнимой. Unsupported root не
получает ложных construction/member/extension surfaces.

Только структурно невозможные contracts не получают executable stub:
неподходящая mapper declaration (например, non-partial/file-local либо
вложенная в non-partial containing type), unnameable generic argument и
конфликтующие generic interfaces, способные унифицироваться. Такая pair не
подавляет независимые legal pairs того же mapper-а.

Исключения из пользовательских `Construct`, `Members`, `Convert`, source
expressions, mapper dependencies и application service provider не
оборачиваются и сохраняют исходный тип, сообщение и stack.

## 15. Зафиксированные законы дизайна

1. `Map(source)` и `Map(source, destination)` остаются двумя публичными
   mapping-операциями; effective `MappingMode` управляет их доступностью.
2. Declarative `Construct` и `Members` выполняются только после null handling.
3. Source-only `Construct` выполняется только при отсутствии previous.
4. `Construct` с параметром `previous` выполняется и с `Option.None`, и с
   `Option.Some`.
5. Если `Construct` отсутствует, structured surface создаёт no-previous result
   по convention, а direct pair является ошибочной для reachable no-previous
   ветки. Существующий previous в обеих формах сам становится result.
6. Для одной pair разрешён не более чем один `Construct` любой перегрузки.
7. `Construct` настраивает result selection и constructor parameters, но не
   declarative body-member rules. Обычный C# direct lambda может вернуть object
   initializer либо иначе инициализированный instance.
8. `Members` является единственным declarative API для body-members. Structured
   surface включает `init` и `required`; direct surface включает обычные
   setters и mutable fields, в том числе `required`, но не `init`-only
   properties. True get-only properties и properties с недоступным обычным
   setter-ом дополнительно входят в обе поверхности только как get-only proxy
   для явного nested Update и не участвуют в обычных member rules.
9. Для member-capable pair всегда генерируются две альтернативные
   `Members`-перегрузки: с `source`/`previous` и с
   `source`/`previous`/`result`. В локальной pair можно вызвать
   ровно один `Members`; любой второй вызов ошибочен.
   `IncludeBase<TBaseSource, TBaseDestination>()` объединяет унаследованный и
   локальный plans независимо от формы перегрузки.
10. Обе формы `Members` являются одним declarative DSL и применяются к
    выбранному result; выбор overload сам по себе не задаёт evaluation phase.
    Набор доступных members определяется construction capability.
11. `previous` в `Members` всегда означает исходный нормализованный input, а не
    выбранный result. В трёхпараметрической форме `result` — это
    фактически выбранный non-null destination без presence-wrapper и без
    корневой nullability. Оба параметра являются read-only источниками:
    assignment, increment/decrement и `ref`/`out` mutation запрещены.
12. Неприменимое `init`-выражение в already-created-result ветке не
    вычисляется. В structured creation result-independent `init` и
    creation-time `required` rules допустимы в обеих формах `Members`;
    ошибочен только конкретный creation-time rule либо условие его
    применимости, которое зависит от ещё не созданного result.
13. Member, не указанный в `Members`, следует effective `MemberSelection`.
14. `MemberSelection.Explicit` является статическим способом полностью
    отключить implicit member mapping; отдельного `Skip()` нет.
15. Nested mapping имеет adaptive формы `Map()`, `Map<TDestination>()`,
    `Map(source)`, `Map<TDestination>(source)` и explicit формы
    `Create(source)`, `Create<TDestination>(source)`,
    `Update(source, destination)`,
    `Update<TDestination>(source, destination)`. Adaptive `Map` следует
    фактической outer lifecycle branch и использует current destination;
    explicit forms всегда сохраняют выбранную nested operation. Conventions и
    `Auto()` используют только warning-free implicit C#-преобразование и не
    предполагают наличие mapping-пары. Get-only member обновляется только
    standalone `Update(..., members.Member)` с generated null guard и discard
    returned result.
16. `Convert` является методом обычного pair-builder, а не отдельным
    builder-типом.
17. У `Convert` есть только одна перегрузка с
    `Option<TDestination>` и `MappingContext`.
18. `Convert` полностью заменяет declarative pipeline и не запускает
    null-handling settings.
19. `MappingContext.Operation` сообщает текущую публичную операцию, а
    `Option<TDestination>` независимо сообщает наличие фактического
    destination instance.
20. `Convert` и ровно одна форма `Construct` доступны для каждой поддерживаемой
    mapping-пары; обе `Members`-перегрузки генерируются независимо при наличии
    применимых к construction capability body-members у non-opaque destination.
21. Наличие хотя бы одного поддерживаемого constructor, включая parameterless,
    выбирает structured `Construct`, его отсутствие — direct `Construct`;
    opaque destination всегда direct. Пользовательского mode и пары с обеими
    формами нет.
22. Direct `Construct` допускает обычный C# object initializer и семантически
    соответствует уже созданному factory-result: к нему применимы обычные
    setter/mutable-field rules и conventions, но не `init`-only rules. Opaque
    destination атомарен и member surface не получает.
23. Возвращённый `Map` result всегда авторитетен.
24. Никаких скрытых fallback между manual и declarative mapping либо между
    разными configured lambdas нет.
25. В structured surface `Option<TDestination>` неявно преобразуется в
    `DestinationConstruction`, поэтому возврат самого `previous` выбирает existing
    result. Direct surface возвращает настоящий `TDestination` и использует
    `previous.Value`; отдельный direct plan или implicit unwrap не вводится.
    Произвольный готовый `TDestination` в structured surface выражается только
    явной factory-веткой.
26. Для `ByConventionMarker` генерируется один creation-plan constructor с
    необязательным `DestinationConstructorParameters`.
27. Generated `DestinationMembers` является record с обычными `set`-
    properties. Object initializer и `with` поддерживают declarative overlay
    body-member rules; более поздний rule заменяет ранний без его вычисления.
    Creation-plan не получает `with`, а mutation уже созданного member-plan
    по-прежнему не поддерживается.
28. `Convert` получает текущий `MappingContext` отдельным последним
    параметром и использует его для ручных nested mappings.
29. `MappingContext` является immutable value-type frame текущего outer или
    nested вызова; scoped `IMapper` создаёт новый frame с собственной
    `Operation`, разделяя общий mapping scope без mutation и восстановления.
30. Declarative pipeline использует `MappingContext` внутренне, но `Construct` и
    `Members` не получают его пользовательским lambda-параметром.
31. Public `Map` принимает nullable source/destination inputs, но возвращает
    ровно выбранный пользователем `TDestination`, а не безусловный
    `TDestination?`.
32. Mapping-produced `Option<TDestination>` использует destination без
    корневой nullability и в роли previous никогда не содержит `Some(null)`;
    общий `Option<T>` при nullable `T` такого ограничения не имеет.
33. Declarative `Construct` и `Members` получают source после null handling как
    non-null underlying type; `Convert` получает исходное runtime-значение.
34. `null` из direct `Construct` или `ByFactory` является авторитетным
    терминальным result: `Members` не выполняется, exception и fallback не
    генерируются, null-handling policies повторно не применяются.
35. `Convert` возвращает пользовательский result без generated null guard;
    `null` вместо generated creation/member plan остаётся ошибкой DSL, а не
    destination-result.
36. Root-вызовы используют независимые scopes и могут выполняться параллельно;
    scoped mapper действует только до завершения root `Map`, а параллельные
    nested-вызовы внутри одного scope не поддерживаются.
37. Каждое declarative expression вычисляется не более одного раза. Structured
    `Construct` и `Members` имеют общий path-sensitive dependency graph: одинаковое
    bound subexpression, нужное обеим частям на выбранном пути, вычисляется
    один раз и разделяется между ними. Невыбранные ветки, неприменимые rules и
    operation-specific значения другого пути не вычисляются; ordinary direct,
    factory и manual blocks остаются вне cross-plan sharing. Previous-aware
    structured plan специализируется по известному наличию previous для
    `Create` и `Update`; удаляются только доказанно недостижимые ветки с
    сохранением short-circuit и side effects.
38. Explicit constructor arguments вычисляются в порядке записи до вызова
    constructor. Result-independent значения для `init` и creation-time
    `required` могут участвовать в structured initializer независимо от формы
    `Members`; result-dependent expressions выполняются только после создания
    non-null result. Фактически переданный constructor argument подавляет
    только соответствующую неявную member-convention; explicit `Members`
    сильнее, а опущенный optional/`params` parameter member не занимает.
39. Generator анализирует зависимость каждого member rule, declarative local и
    условия от `result` прямо и транзитивно. Использование `result` одним rule
    не переводит весь `Members` в post-creation; форма перегрузки не входит в
    dependency graph.
40. Snapshot, относительный порядок независимых member expressions и момент
    generated assignments являются деталями lowering. Нельзя полагаться на
    видимость setter либо nested mapping side effects между независимыми
    rules; требующий такого контроля алгоритм использует `Convert`.
41. Structured `Construct` и `Members` поддерживают только конечный анализируемый
    control flow без изменяемого состояния. Direct `Construct`, factory body и
    `Convert` являются обычными синхронными C# blocks. Ни одна форма не
    захватывает обычные Configure-locals, `builder` или внешние Configure-local
    functions.
42. Eligible pair определяется отдельно от её capabilities. Оба root-типа
    должны иметь статически известную верхнеуровневую форму; type parameter в
    root-позиции запрещён независимо от constraints, но может оставаться
    generic argument известного nominal root. Для классификации верхнеуровневая
    `Nullable<T>`-обёртка снимается, не меняя canonical identity разрешённой
    nullable value pair.
43. До post-v0 tuple, sequence/collection/buffer, delegate, expression-tree,
    deferred/async и push-sequence roots полностью исключаются в обеих
    mapping-позициях даже для direct/manual mapping.
44. Для любой другой eligible pair доступны runtime contract и `Convert`;
    destination получает ровно одну форму `Construct` и, если он не opaque,
    `Members` при наличии применимых к этой construction capability
    body-members. Значение отложенной категории внутри разрешённого root
    остаётся обычным единым C#-значением. `dynamic`
    канонически совпадает с `object`; root nullable reference annotation не
    создаёт отдельную runtime pair.
45. Manual mapping применяет только `MappingMode`. Остальные settings не
    запускают скрытый declarative pipeline; неприменимая explicit map-level
    setting является ошибкой, а inherited setting может быть безвредным no-op.
46. Public `IMapper` является application-wide фасадом: concrete `TypeMapper`,
    compilation и assembly не ограничивают видимые manual registrations.
47. Root и scoped mapper используют один фиксированный набор manual
    registrations и `IServiceProvider` текущего DI-scope; `MappingScope`
    хранит только состояние mapping chain. `AddMorphant(...)`, generated
    manifests и assembly scanning остаются post-v0.
48. Обычный v0 lookup идентифицируется canonical type pair. Ноль кандидатов
    означает missing mapping, один — выполнение, два и более — ambiguity.
49. Повторные registrations pair допустимы и не являются generator/startup
    error; Morphant никогда не разрешает их порядком регистрации или правилом
    last-registration-wins.
50. Explicit nested и manual nested mappings выполняют тот же application-wide
    lookup, не предпочитая outer `TypeMapper` или assembly.
51. Post-v0 keyed lookup добавляется как явное расширение выбора descriptor-а,
    не меняющее базовый `IMapper`/`ITypeMapper` shape; точный API, назначение и
    наследование ключа согласуются отдельно.
52. В v0 `Update` не создаёт replacement автоматически из-за отличия
    `init`-only, get-only или readonly state. Декларативный replacement задаётся
    previous-aware `Construct`, а record-copy и иная специальная reconstruction —
    `Convert`.
53. Source-only `Construct` при существующем previous не является immutable
    replacement-path. `ByCopy`, generated destination-copy `with` и implicit
    record cloning в v0 отсутствуют; member-plan overlay из закона 27 не
    создаёт replacement.
54. Declarative existing-ветка, которая статически не может ни заменить result,
    ни выполнить post-construction assignment, возвращает исходный destination
    без изменений. Для такого no-op не требуется previous-aware `Construct`;
    доступность `Update` не гарантирует mutation.
55. Отдельная post-v0 opt-in setting может условно реконструировать result при
    отличии хотя бы одного creation-only member candidate от previous. Эта
    identity-policy не является частью `NullAssignmentHandling`; её equality,
    reconstruction и evaluation contracts требуют отдельного решения.
56. В v0 нет отдельного per-call arguments/context contract. После включения
    tuple roots пользовательский state является обычным элементом source и
    передаётся в nested mappings явно; он не хранится в `MappingContext` или
    `MappingScope` и не распространяется ambient-механизмом.
57. В v0 runtime-тип source не меняет requested canonical pair.
    `IncludeBase<TBaseSource, TBaseDestination>()` наследует только
    конфигурацию и не включает runtime dispatch; special-case остаётся областью
    explicit `Convert`.
58. В v0 reference tracking отсутствует. `MappingScope` резервирует
    chain-wide extension point, но shared source может породить разные result,
    а cyclic graph не получает built-in завершение.
59. Будущий reference cache является opt-in и использует source reference
    identity вместе с resolved mapping descriptor identity. Result может быть
    зарегистрирован только после `Construct`; поэтому built-in preservation не
    делает constructor/initializer cycles разрешимыми.
60. `IQueryable` projection, public `Project(...)`, projectable capability и
    expression-tree roots полностью отсутствуют в v0 и рассматриваются после
    него отдельным дизайном.
61. В v0 configuration reuse следует только registrations текущего mapper-level
    и явно подключённой C#-иерархии mapper-ов. Generator не выполняет arbitrary
    builder helpers и не ищет fragments или подходящие plans в application
    dispatch.
62. `base.Configure(builder)` подключает base configuration chain и её root
    settings, но не добавляет base registrations в generated surface derived
    mapper-а. Pair configurations chain становятся кандидатами для typed
    `IncludeBase`; без этого вызова они недоступны.
63. Повторно объявленная pair без
    `IncludeBase<TBaseSource, TBaseDestination>()` начинает с чистого map-level
    plan, сохраняя унаследованные root settings. Generic-аргументы задают
    точную base pair; текущие source и destination должны быть приводимы к
    соответствующим base types. Эти отношения проверяются generator-ом, потому
    что C# не позволяет выразить их method-level constraints без изменения
    двухаргументной формы API. Поиск сначала идёт по текущему level независимо
    от порядка объявлений, затем по подключённым base levels и выбирает
    ближайшее точное совпадение; self-reference и cycles ошибочны.
64. Через typed `IncludeBase` наследуются все map-level settings, включая
    `MappingMode` и `ConstructorSelection`. Settings precedence — current pair,
    included base pair, current mapper root, connected base roots, assembly,
    library default; `Default` продолжает поиск на следующем уровне.
65. Из base plan импортируются только `Members`. Правила независимо от формы
    перегрузки объединяются по destination member с локальным приоритетом,
    включая expression, `Auto()` и `Ignore()`. Conventions и constructor
    selection вычисляются заново для текущей pair, а dependencies effective
    rules анализируются отдельно.
66. `Construct` и `Convert` base pair не импортируются. Локальный `Convert`
    владеет всей текущей pair и отбрасывает импортированные member rules.
67. Base mapper не требует `MorphantMapperAttribute`, если его `Configure`
    доступен как source в текущей compilation. Прямой
    `base.Configure(builder)` поддерживается statement- и expression-bodied
    формой; повторный вызов ошибочен. Generic base DSL сохраняет открытый
    generated surface, а effective derived plan использует constructed type
    arguments, включая nested mapper declarations. Проверка accessibility
    выполняется только для оставшихся effective member rules, поэтому полное
    локальное перекрытие удаляет недоступное base rule до emission.
68. General-purpose fragments и cross-assembly typed `IncludeBase` отсутствуют
    в v0. Generic и nested mapper-ы поддерживаются внутри одной compilation;
    внешние mappings регистрируются независимо и не импортируют configuration
    друг друга.
69. Constructed generic root с известной nominal-формой является обычной exact
    pair; mapper type parameters допустимы внутри его generic arguments.
70. Generic mapper contract не является open-generic registration. Dispatch
    v0 видит только явно зарегистрированные closed mappings и не выводит
    arguments, не закрывает mapper type и не сопоставляет generic definitions.
71. Reference nullable annotations не входят в canonical identity, включая
    annotations внутри generic arguments. `Nullable<T>` value type остаётся
    настоящей частью constructed type и меняет identity.
72. Generic arguments могут содержать type parameters и отложенные root-
    категории как обычные единые значения, если полный root выразим из общего
    generated assembly-context; reflection-обхода недоступности нет.
73. Bare root type parameter, open-generic registration и mapping по runtime
    `Type` отсутствуют в v0 и не получают fallback через application dispatch.
74. После v0 tuple/multi-source mapping использует обычный tuple `TSource` без
    специальных overload-ов `IMapper`; identity учитывает типы и порядок
    элементов, но не имена, а пользовательский state передаётся детям явно.
75. Result-dependent rules `Members` остаются declarative и выполняются после
    появления result; остальные rules не меняют фазу из-за формы перегрузки.
    Зависимость от порядка независимых rules, setter/nested mapping side
    effects, mutation между assignments, replacement-result и полный
    imperative lifecycle требуют `Convert`.
76. `BeforeMap`, `AfterMap`, middleware либо эквивалентные lifecycle hooks не
    входят в v0, но обязательно будут поддержаны после v0; точная
    форма будущего API пока не выбрана.
77. Typed `Auto<T>()` и `Ignore<T>()`, generic
    `Map<TDestination>(...)` и явный `ConstructorParameter<T>` cast сохраняются
    как target-typing и overload-selection affordances declarative DSL.
78. Общий destination constructor/member surface использует стабильную
    assembly-accessibility без private/protected-привилегий mapper-а. Остальные
    accessibility/hiding/order rules, exact-name body matching,
    exact-then-unique-`OrdinalIgnoreCase` constructor matching и warning-free
    implicit C# conversion matrix переносятся без упрощения.
79. Generated wrappers точно отражают nullable input contract destination,
    включая attributes и oblivious context; optional non-nullable omission
    использует только suppressed `null!` sentinel. Documentation,
    `ObsoleteAttribute` и deterministic IntelliSense order также сохраняются.
80. Один generic generated plan переиспользуется для original destination
    definition и воспроизводит containing type parameters и constraints;
    alpha-equivalent pair extensions дедуплицируются и получают только
    definition-derived constraints, без mapper-specific `where`; closed
    runtime registrations при этом остаются отдельными.
81. На одном C# settings-level побеждает последний вызов, включая `Default`,
    а root result не зависит от позиции в линейном `Configure`. Assembly
    settings передаются только compiler-visible MSBuild properties.
82. Built-in scalars, enums, `Guid`, `DateTime`, `DateTimeOffset`, `DateOnly`,
    `TimeOnly`, `TimeSpan`, `Half`, `Int128`, `UInt128`, `Uri`, `Version`,
    `BigInteger`, `Complex`, `Rune`, `Index` и `Range` являются opaque/direct
    destinations без `Members` и automatic convention construction; custom
    value type следует обычной capability model.
83. Потенциально унифицирующиеся pair shapes одного generic mapper-а являются
    compile-time configuration conflict, а не runtime duplicate registration.
84. `IncludeMembers` как first-class convention flattening обязателен после
    v0; explicit member expressions, tuple-source и nested `Map` его не
    заменяют.
85. Непубличные generated execution helpers, которые становятся членами
    пользовательского mapper-а, используют зарезервированный префикс `__`:
    `__Create`, `__Update`, `__ConstructDestination`, `__CreateByFactory` и
    `__ConvertDestination`. При конфликте добавляется числовой суффикс;
    explicit implementations `ITypeMapper.Create` / `Update` сохраняют имена
    публичного контракта.

## 16. Аудит переноса прежнего `Template()`-дизайна

Перед naming-аудитом целевой API был сопоставлен с последним прежним
`Template()` surface, его implementation roadmap и executable tests. Целью
было найти не похожие method names, а реальные возможности, UX-контракты и
runtime laws, которые могли случайно исчезнуть при разделении `Construct`,
`Members` и `Convert`.

### 16.1. Найденные разрывы и решения

Единственный первоначально найденный семантический разрыв — общий local между
constructor- и member-частью прежнего template:

```csharp
var value = Calculate(source);

return new(value)
{
    NormalizedValue = value
};
```

В split API пользователю иногда приходится дважды записать expression, но
общий dependency graph structured `Construct` / `Members` автоматически делит
одинаковое bound subexpression. Поэтому runtime-значение и число side effects
не расходятся; остаётся только небольшая синтаксическая избыточность. Для
длинного неповторяющегося вычисления обычный mapper method остаётся естественным
средством переиспользования.

Старый `with` решал две разные задачи. Выбор creation strategy с последующим
наложением общих members теперь естественнее выражается независимыми `Construct`
и `Members`, поэтому unified template overlay не возвращается. Динамический
выбор одного member-plan с последующим добавлением общих либо overriding rules
имеет самостоятельную ценность; ради него `DestinationMembers` остаётся record
и поддерживает `with` по законам раздела 7.2.

`IncludeMembers` не входил в прежний v0, но был явно отложенной first-class
capability и действительно выпал из нового roadmap. Он возвращён как
обязательный post-v0 этап. Typed marker forms, compiler-based constructor
binding, nullability wrappers, documentation/attributes/order, generic plan
sharing, settings semantics, convention boundaries, opaque destinations и
generic pair unification не проектируются заново: они зафиксированы в
разделах 6, 7, 11 и 12 как carry-forward requirements.

### 16.2. Что заменено новым механизмом

| Прежний механизм | Целевой эквивалент |
|---|---|
| Единый DSL `Template` | Structured `Construct` + `Members` |
| `TemplateMode.Raw` | Явный `Convert`; для получения instance с последующими conventions — direct/factory `Construct` |
| Direct template для scalar | Direct `Construct` |
| Source/destination-aware template | `Option<T>`, previous-aware `Construct` и result-aware `Members` |
| Factory/cached destination | `ByFactory` либо direct `Construct`, затем общий member plan |
| Constructor/member markers | Сохранены на соответствующей plan-части |
| `IContextualMapper`-подобный nested dispatch | Scoped `context.Mapper : IMapper` |
| Record `with` настоящего destination | Обычный C# внутри `Convert` |
| `base.Configure` и typed `IncludeBase` | Явно разделённое наследование root settings и member rules конкретной base pair |

Крупной потерянной feature в core v0 после этих поправок нет.

### 16.3. Сравнительная оценка

| Критерий | Прежний `Template` | Новый дизайн |
|---|---|---|
| Constructor + members call site | Компактнее в одной lambda | Иногда требует два fluent-вызова и повтор записи expression |
| Разделение ответственности | Одна форма одновременно описывает creation, members, existing и raw mode | `Construct`, `Members`, `Convert` отвечают каждый на один вопрос |
| `Update` | Интерпретация template меняется по operation и mode | `previous` / reuse / replacement выражены явно |
| Factory/cached/derived result | Фактическое состояние result трудно использовать декларативно | Result-aware `Members` видит выбранный instance |
| Manual mapping | Семантика `Template` переключается setting-ом | Отдельный очевидный escape hatch |
| Общие вычисления | Один lexical local | Общий path-sensitive graph при возможном дублировании записи |
| Композиция rules | Unified template-record и широкий `with` | Независимые creation/member plans и узкий member-only `with` |
| Расширяемость | Новая capability дополнительно перегружает `Template` | Collections, hooks, projection и polymorphism добавляются ортогонально |
| Generator | Один surface со сцепленной семантикой | Более сложный lowering, но раздельные модели и явные laws |

Итог аудита: прежний дизайн остаётся ценным prototype-ом DSL и источником
проверенных contracts, но новый дизайн удачнее как публичная архитектура
general-purpose mapper-а. После expression sharing, member-only `with`,
обязательного post-v0 `IncludeMembers` и переноса старых invariants у единого
`Template` остаётся только преимущество компактности смешанного mapping-а.
Возвращаться к нему как к основе API не следует.

### 16.4. Сознательные сужения

Следующие отличия не считаются забытыми features:

- bare root type parameters, которые прежний generator частично поддерживал,
  в новом v0 запрещены вместе с root tuple/collection/buffer, delegate,
  expression-tree, deferred/async и push-sequence categories;
- collections, tuple/multi-source, patch/merge, projection, runtime
  polymorphism, reference tracking и cross-assembly plan composition явно
  отложены;
- hooks/middleware гарантированы после v0, но не маскируются manual mapping-ом
  до выбора точного lifecycle API;
- snapshot, порядок независимых member rules и видимость setter/nested side
  effects намеренно не являются контрактом;
- immutable `Update` не реконструируется автоматически: статически пустая
  existing-ветка сохраняет identity и возвращает previous без изменений.

## 17. Статус реализации и оставшиеся границы

Согласованный core v0 реализован. Текущее состояние migration audit,
документации и compile-time integration slice фиксируется в
[`MAPPING_API_IMPLEMENTATION_PLAN.md`](MAPPING_API_IMPLEMENTATION_PLAN.md), а
независимая оценка полноты сценариев — в
[`MAPPING_API_FINAL_AUDIT.md`](MAPPING_API_FINAL_AUDIT.md).

Observable runtime failures и generated exception-stub boundary реализованы и
зафиксированы разделом 14.2. Compile-time diagnostics остаются отдельным
поздним планом. Collections, projection, polymorphism, reference handling и
остальные перечисленные выше возможности остаются post-v0 направлениями и не
расширяют текущий mapping semantics неявно. До отдельного продуктового решения
их API не должен определяться удобством существующей реализации.
