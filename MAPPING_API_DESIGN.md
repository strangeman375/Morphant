# Новый дизайн mapping API Morphant

Статус документа: согласованный нормативный целевой дизайн mapping API.
Callback result-policy и read-only proxy revisions ещё не перенесены в
production-код; актуальный прогресс и оставшиеся границы фиксирует
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
| `Construct` | Как declarative constructor plan создаёт result при отсутствии previous? |
| `Resolve` | Как declarative constructor plan выбирает result для любой operation? |
| `ConstructUsing` | Как runtime callback создаёт result при отсутствии previous? |
| `ResolveUsing` | Как runtime callback выбирает result для любой operation? |
| `Members` | Как маппить body-members выбранного result? |
| `Convert` | Как целиком выполнить mapping без declarative pipeline? |

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

Один из `Construct` / `Resolve` / `ConstructUsing` / `ResolveUsing` задаёт
result policy declarative pipeline, а `Members` описывает body-members уже
выбранного result. Четыре result-policy methods занимают один
взаимоисключающий slot и не являются последовательными стадиями. `Convert`
является полностью отдельной альтернативой всему declarative pipeline.

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
  destination-конструктора либо выбора normalized previous, а не готовый
  `TDestination`; runtime `ConstructUsing` / `ResolveUsing` возвращают готовый
  destination без такого промежуточного plan;
- `member plan` — сгенерированное описание body-member mappings, а не готовый
  `TDestination`.

`previous` и `result` намеренно различаются. `Resolve` / `ResolveUsing` могут
выбрать previous либо replacement, а no-previous policies создают result.
Поэтому identity `result` не обязана совпадать с identity переданного
destination.

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

Для одной canonical mapping-пары разрешён один result-policy fragment
`Construct` / `Resolve` / `ConstructUsing` / `ResolveUsing` и один `Members`,
либо один `Convert`. Четыре result-policy methods взаимоисключаемы; смешивать
declarative pipeline с `Convert` нельзя.

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
`TBaseDestination`. Допускаются identity, implicit reference и boxing
conversions; numeric и user-defined conversions не образуют base-type
relation. Поэтому проверка охватывает class- и interface-иерархии, а также
обычную boxing assignability value type-а. Эти отношения проверяет generator:
C# не позволяет method-level `where`
ограничить `TSource` и `TDestination`, объявленные у содержащего
`MapperBuilder<TSource, TDestination>`, а переход к четырём method type
arguments ухудшил бы текущую форму вызова.

Узел composition идентифицируется не только canonical pair, а tuple
`(constructed mapper-level, canonical pair)`. Requested pair сначала ищется
среди остальных authoritative registrations текущего mapper-level независимо
от порядка объявлений, затем среди mapper-level-ов, подключённых через
`base.Configure(builder)`, от ближайшего к дальнему. Текущий узел исключается
из собственного lookup, но одноимённая pair подключённого base level остаётся
отдельным кандидатом. Если совпадение существует и на текущем, и на
подключённом уровне, используется текущий кандидат; среди подключённых уровней
используется ближайшее точное совпадение.

Поэтому повторно объявленная pair derived mapper-а может явно импортировать
plan одноимённой pair base mapper-а. Same-pair `IncludeBase` без подходящего
connected ancestor получает обычную ошибку отсутствующей pair, а не
self-reference. Отсутствие requested pair, несовместимость типов и повторный
вызов `IncludeBase` для одной текущей pair являются ошибками конфигурации.

Отдельного cycle state в v0 нет. Совместимое same-level ребро направлено к
равным либо базовым source/destination types и при исключённом текущем узле
хотя бы по одной координате является строгим; межуровневое ребро всегда идёт
вверх по ациклической C# base-chain. Exact same-pair между mapper-level-ами
также уменьшает уровень. Поэтому legal composition graph ацикличен по
построению. Обратное несовместимое ребро диагностируется как type
incompatibility, а циклическую C#-иерархию полностью диагностирует compiler.

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

Импорт mapping plan зависит от отношения узлов:

- cross-pair `IncludeBase`, например `Dog -> DogDto` из
  `Animal -> AnimalDto`, импортирует только правила `Members`; result policy и
  `Convert` не переносятся, а conventions и constructor selection вычисляются
  заново для текущей pair;
- exact same-pair из connected base mapper-а импортирует весь applicable
  effective plan, включая одну из четырёх result policies либо `Convert`, без
  runtime casts, адаптеров delegate signatures или попыток перенести result
  callback/converter между разными destination types.

Правила `Members` в обоих случаях объединяются по destination member
независимо от формы перегрузки. Локальный expression, `Auto()` или `Ignore()`
перекрывает унаследованное правило, после чего зависимости каждого effective
rule анализируются отдельно.

Локальный plan разрешается предсказуемо:

- при отсутствии локальных fragments exact same-pair полностью сохраняет
  inherited plan;
- локальный `Convert` заменяет весь inherited plan и владеет текущей pair;
- локальный declarative plan с любой result policy либо `Members` отбрасывает
  inherited `Convert`;
- для declarative plan inherited result policy служит fallback, локальная
  result policy любого из четырёх имён её перекрывает, а `Members` объединяются
  по обычному правилу локального приоритета.

Переносимые effective result-policy, `Members` и `Convert` callbacks испускаются
внутри derived mapper-а, поэтому все mapper-members в них должны быть доступны
из derived type. Обычные public, internal и protected helpers поддерживаются
согласно C# accessibility; private members и явный `base.` в оставшемся
inherited expression делают effective plan ошибочным. Полное локальное
перекрытие либо отбрасывание expression удаляет его до проверки accessibility.

Source generator не выполняет configuration code и не следует за
произвольными helper calls, которые изменяют builder. Переиспользуемые
вычисления остаются обычными instance/static методами mapper-а, вызываемыми
внутри result policy, `Members` или `Convert`.

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

## 6. Выбор result

### 6.1. Четыре result-policy метода

Declarative pipeline разделяет выбор result и настройку его members. Для
выбора result существуют четыре взаимоисключающих метода:

| Метод | Applicability | Callback class | Результат callback-а |
|---|---|---|---|
| `Construct` | Только no-previous branch | Declarative structured DSL | `DestinationConstruction` |
| `Resolve` | Все достижимые операции после null handling | Declarative structured DSL | `DestinationConstruction` |
| `ConstructUsing` | Только no-previous branch | Обычный runtime C# | Настоящий `TDestination` |
| `ResolveUsing` | Все достижимые операции после null handling | Обычный runtime C# | Настоящий `TDestination` |

`Construct` и `ConstructUsing` отвечают только за получение result, когда
нормализованный previous отсутствует. При существующем previous callback не
вызывается, а переданный instance становится result. `Resolve` и
`ResolveUsing` являются полными selector-ами: вызываются и с `Option.None`, и
с `Option.Some` и сами выбирают construction/reuse/replacement.

Для одной canonical pair может быть настроен не более чем один из этих
четырёх методов. Это один result-policy slot, а не последовательные stages и
не четыре независимых правила. `Members` после него остаётся единственным
declarative surface для свойств и полей. `Convert` по-прежнему заменяет весь
declarative pipeline и не комбинируется ни с одной result policy либо
`Members`.

### 6.2. Pair-specific generated builder surface

`ConstructUsing` и `ResolveUsing` являются pair-specific generated extension
methods и существуют для каждой eligible pair. Они входят в существующий
artifact `MappingExtension` и расширяют точный
`MapperBuilder<TSource, TDestination>` зарегистрированной pair:

```csharp
.ConstructUsing(source => CreateDestination(source))
.ConstructUsing((source, context) => CreateDestination(source, context))

.ResolveUsing((source, previous) =>
    ResolveDestination(source, previous))
.ResolveUsing((source, previous, context) =>
    ResolveDestination(source, previous, context))
```

Каждый метод имеет короткую и context-aware overload. `MappingContext` в
полной форме всегда является последним параметром; короткая форма только не
предоставляет ненужный context и не меняет lifecycle. Runtime callback может
быть expression- или block-lambda, natural method group либо materialized
delegate. Он исполняется ровно один раз на выбранном path; context-aware форма
может выполнять nested mapping через `context.Mapper`.

Zero-argument callback не вводится. Минимальная `ConstructUsing` overload
всегда получает `source`; если он не нужен, пользователь пишет `_`.

Pair-specific generation нужна из-за различия типов receiver-а и callback-а.
Receiver сохраняет source/destination-типы зарегистрированной pair. Callback
получает root-normalized non-null source после `NullSourceHandling`, а
`ResolveUsing` — ещё и `Option` от root-normalized destination после
`NullDestinationHandling`. Возвращаемый тип при этом не нормализуется: это
ровно destination-тип pair-builder-а, включая его корневую nullability.
Например, callback pair `Source? -> Destination?` получает `Source`, но
возвращает `Destination?`. Обычный generic-метод самого
`MapperBuilder<TSource, TDestination>` не может выразить этот раздельный
контракт для всех nullable reference/value forms.

`Construct` и `Resolve` являются generated extension methods и появляются
только при наличии structured creation capability: destination имеет хотя бы
один поддерживаемый доступный constructor. Оба возвращают один и тот же
generated `DestinationConstruction`:

```csharp
.Construct(source => new(source.Id))
.Construct((source, context) =>
    context.Operation == MappingOperation.Create
        ? new(source.Id)
        : new(source.Id, source.Revision))

.Resolve((source, previous) =>
{
    if (previous.HasValue && previous.Value.Id == source.Id)
        return previous;

    return new(source.Id);
})

.Resolve((source, previous, context) =>
    ResolvePlan(source, previous, context.Operation))
```

Structured callbacks требуют inline lambda и никогда не получают настоящий
`MappingContext`: generator анализирует их как конечный declarative DSL.
Короткий `Construct` получает normalized source, короткий `Resolve` — source и
`Option<TDestination> previous`. Максимальные overload-ы дополнительно получают
`MappingContextMarker` последним параметром.

Единственный поддерживаемый parameterless constructor не является
исключением: generator всё равно создаёт и `Construct`, и `Resolve`. Единый
критерий «есть callable constructor surface» остаётся предсказуемым, а такой
surface нужен для явного construction при `ConstructorSelection.Explicit`,
для structured replacement в `Resolve` и для включения `init`/`required`
member rules в общий object initializer. Добавление parameterized overload-а
не должно внезапно менять сам набор result-policy методов.

Если constructor surface отсутствует либо destination opaque, generated
`Construct` и `Resolve` отсутствуют. Создание на reachable no-previous path
тогда выражается `ConstructUsing`, `ResolveUsing` либо полностью ручным
`Convert`; Morphant не создаёт искусственный direct plan type и не выбирает
`default`.

### 6.3. Семантика callbacks и context

Концептуальный call-site surface:

```csharp
// Generated, только structured destination.
Construct(source => DestinationConstruction);
Construct((source, context) => DestinationConstruction);

Resolve((source, previous) => DestinationConstruction);
Resolve((source, previous, context) => DestinationConstruction);

// Pair-specific generated extensions, для любой eligible pair.
ConstructUsing(source => TDestination);
ConstructUsing((source, context) => TDestination);
ResolveUsing((source, previous) => TDestination);
ResolveUsing((source, previous, context) => TDestination);
```

Все callback-параметры используют именованные delegate-типы из
`Morphant.Delegates`, а не безымянные `Func<...>`, чтобы IntelliSense сохранял
semantic parameter names. Имена delegate families совпадают с fluent methods:
`Construct`, `Resolve`, `Members`, `ConstructUsing`, `ResolveUsing` и
`Convert`. Context-aware delegate получает отдельный generic parameter
`TContext`, закрываемый `MappingContextMarker` либо `MappingContext`; это даёт
каждой family уникальную generic arity без перехода на `Func` и без фиктивных
parameters.

`MappingContextMarker` является публичным типом только ради target typing
declarative lambda:

```csharp
public abstract class MappingContextMarker
{
    private protected MappingContextMarker()
    {
    }

    public abstract MappingOperation Operation { get; }
}
```

Runtime instance marker-а не создаётся. Generator lower-ит чтение
`context.Operation` к operation настоящего call frame. Сам marker нельзя
превращать в runtime value: запрещены alias, передача в helper, capture runtime
callback-а, comparison/pattern/null check, cast, `ToString` / `GetType` и
return. Извлечённый `MappingOperation` является обычным declarative значением и
может сохраняться в local либо передаваться helper-методу.

Structured `Resolve` может вернуть previous благодаря implicit conversion
`Option<TDestination> -> DestinationConstruction`. В block-lambda это
сохраняет target-typed `new(...)` в C# 9:

```csharp
.Resolve((source, previous) =>
{
    if (CanReuse(source, previous))
        return previous;

    return new(source.Id);
})
```

`ResolveUsing` возвращает настоящий destination, поэтому извлекает
`previous.Value` явно. Отдельный `DirectConstruction<T>`, implicit conversion
`Option<T> -> T`, `AsResult()` и `UsePrevious()` не вводятся.

`ConstructUsing` и `ResolveUsing` являются runtime-частями declarative
pipeline, а не manual mapping. Их короткие и context-aware overload-ы получают
normalized non-null source и, для `ResolveUsing`, root-normalized previous
после declarative null handling, но возвращают точный destination-тип
pair-builder-а без root-normalization. В
полной форме `context.Operation` сохраняет исходную public operation, а
`context.Mapper` использует текущий `MappingScope`. После non-null result
выполняется effective `Members` plan. В отличие от них `Convert` получает
исходные inputs до null handling и не запускает никакую declarative stage.

Declarative markers внутри `ConstructUsing` и `ResolveUsing` недоступны.
Constructor, object initializer, cache, factory, mutation, conditions, loops,
exceptions и local functions являются обычным C#. `ByFactory` полностью
удалён: ни marker-а внутри `DestinationConstruction`, ни top-level alias, ни
compatibility overload в целевом API нет.

### 6.4. Generated structured creation-plan

Creation-plan зеркалит поддерживаемые destination constructors и использует
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

    public static implicit operator DestinationConstruction(
        Option<Destination> previous);
}
```

Поддерживаемые формы creation-plan:

- явный destination constructor, включая parameterless `new()`;
- `ByConvention()`;
- `ByConvention()` с явными constructor-parameter rules;
- existing previous как result в `Resolve`.

Произвольный готовый `TDestination` не преобразуется в structured plan. Для
этого используется отдельный top-level `ConstructUsing` либо `ResolveUsing`,
что сохраняет единственную runtime boundary без вложенного factory callback-а.

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
nested markers. Они нужны там, где обычного target typing недостаточно,
например внутри declarative local, conditional- либо switch-expression.

Generated overload-ы creation-plan являются compiler probe для настоящих
destination constructors. Positional, named и mixed arguments, optional
parameters, omission и overload ambiguity разрешает C# compiler, а не ручной
алгоритм generator-а. `params` допускает omission либо передачу массива
целиком, но не expanded-форму. Явный cast к `ConstructorParameter<T>` остаётся
способом выбрать нужную generated overload; при lowering он превращается в
cast к фактическому типу destination parameter-а.

### 6.5. Поведение по умолчанию

Если previous отсутствует и result policy не настроена, structured destination
создаётся по convention с эффективным `ConstructorSelection`. Текущим default
остаётся `Unambiguous`. При существующем previous и отсутствии `Resolve` либо
`ResolveUsing` instance переиспользуется; `Construct` и `ConstructUsing` не
вызываются.

`Unambiguous` выбирает единственный поддерживаемый доступный parameterized
constructor, даже если одновременно существует parameterless constructor.
Если parameterized constructors отсутствуют, выбирается поддерживаемый
доступный parameterless constructor. Если parameterized constructors
несколько, требуется явный выбор даже при наличии parameterless constructor.
После выбора Morphant не делает fallback к другому constructor-у из-за
отсутствующего или несовместимого обязательного argument-а.

Остальные стратегии следуют той же stable supported-constructor surface:

- `Explicit` запрещает automatic selection, включая `ByConvention()`;
- `Parameterless` выбирает только supported parameterless constructor;
- `Single` требует ровно один supported constructor независимо от arity;
- `Greediest` строит применимые warning-free convention plans и выбирает
  уникальный plan с наибольшим числом фактически переданных arguments;
- `Largest` сначала выбирает уникальный supported constructor с наибольшим
  числом объявленных parameters и только затем проверяет применимость.

Опущенные optional/`params` parameters не увеличивают score `Greediest`, а
переданный `params` array считается одним argument. Равенство лучших scores у
`Greediest` либо максимального declared size у `Largest` не разрешается
порядком объявления и требует explicit structured result policy. `Largest`,
`Single`, `Unambiguous` и `Parameterless` не откатываются к другому constructor,
если выбранный кандидат неприменим. Required initializer plan и
`SetsRequiredMembers` участвуют в применимости.

Written rules `ByConvention()` участвуют в применимости и score: explicit
expression и успешный `Auto()` считаются переданными arguments, `Ignore()` —
нет. Explicit constructor внутри `Construct`/`Resolve` не зависит от
`ConstructorSelection`. `ConstructUsing`/`ResolveUsing` также не используют
constructor selection, поскольку их body является обычным C#.

Destination без structured capability требует configured `ConstructUsing` или
`ResolveUsing` на каждой reachable no-previous ветке. То же относится к opaque
destination: даже если C# технически позволяет `new()` или `default`, Morphant
не выбирает атомарное значение. Отсутствие настройки является ошибочной
configuration, а не fallback на `Convert`, runtime conversion либо `default`.

### 6.6. Порядок вычислений

В runtime выполняется только выбранная result-policy branch. `Construct` и
`ConstructUsing` вообще не вычисляются при существующем previous. Невыбранные
ветки `Resolve`, неприменимые operations и их dependencies также не
вычисляются.

Structured plan специализируется по заведомому `Option.None` / `Option.Some`.
Удаляются только доказанно недостижимые ветки с сохранением short-circuit и
side effects. Незащищённый `return previous`, достижимый при `Option.None`,
остаётся ошибочной branch и не получает hidden construction fallback. Если
после специализации обе стороны условия ведут в один plan, условие всё равно
вычисляется ради observable effects, а общий plan испускается один раз.

Явные constructor arguments вычисляются ровно один раз слева направо в порядке
записи, включая переставленные named arguments. Для `ByConvention()` сначала
в пользовательском порядке вычисляются written constructor-parameter rules,
после них — остальные automatic arguments в порядке parameters выбранного
constructor-а. `Ignore()` не вычисляет значение.

Фактически переданный constructor argument занимает одноимённый body-member
только относительно implicit member convention. Опущенный optional/`params`
parameter и `Ignore()` member не занимают. Explicit `Members` rule остаётся
авторитетным; `required` member остаётся в initializer, если выбранный
constructor не помечен `[SetsRequiredMembers]`. Общее automatic значение
вычисляется один раз и переиспользуется.

Structured `Construct`/`Resolve` и `Members` имеют общий path-sensitive
dependency graph. `ConstructUsing` и `ResolveUsing` являются атомарными runtime
callables и не участвуют в cross-plan sharing. Expression-body переносится как
выражение, block-body — целиком; обычный C# определяет внутренний порядок,
mutation и control flow. Получение настоящего result выполняется ровно один
раз. Если result non-null, после него действует общая member-фаза.

Переносимый runtime block либо materialized method group/delegate испускается
одним collision-safe private helper-ом mapper-а. Если callable достижим из
нескольких operations, они используют общий helper; его body и типизированный
delegate local не дублируются в leaf branches.

## 7. `Members`

### 7.1. Четыре префиксные перегрузки

Для каждой pair с member capability generator создаёт четыре conceptual
overload-ы одного declarative DSL:

```csharp
.Members(source => ...)

.Members((source, previous) => ...)

.Members((source, previous, result) => ...)

.Members((source, previous, result, context) => ...)
```

Параметры образуют стабильный префикс
`source -> previous -> result -> context`. Короткая overload только не
предоставляет ненужные данные: она не меняет operation,
creation/post-creation phase, applicability rules либо effective
`MemberSelection`.

Отдельные формы `(source, context)`, `(source, previous, context)` и
`(source, result)` не добавляются: они столкнулись бы по arity с prefix-формами.
Для доступа к `MappingContextMarker.Operation` используется полная overload с
проигнорированными `previous` и `result`. Само наличие неиспользуемых
parameters не создаёт dependencies и не переводит rules в post-construction
phase.

Generated callbacks используют именованные delegate-типы `Members` из
`Morphant.Delegates`; максимальная arity закрывает `TContext` типом
`MappingContextMarker`. Все четыре формы требуют inline lambda, поскольку
generator анализирует их как declarative member plan.

В local configuration pair можно вызвать ровно один `Members`; любой второй
вызов является ошибкой. `IncludeBase<TBaseSource, TBaseDestination>()`
объединяет inherited и local rules независимо от callback arity: четыре формы
являются одним fragment family и одним effective member plan.

### 7.2. Ответственность

`Members` является единственным declarative surface настройки body-members,
которые применимы к construction capability destination:

- properties с обычным `set`;
- `init`-only properties для structured destination;
- `required` properties и fields;
- writable fields;
- поддерживаемые унаследованные body-members.

Для destination без constructor surface из этого списка остаются только
обычные setters и mutable fields. Модификатор `required` их не исключает;
`init`-only property исключается.

Constructor parameters не входят в `Members`, потому что они не являются
body-members. Обычный C# внутри `ConstructUsing`, `ResolveUsing` или `Convert`
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
на `DestinationConstruction`: выбор result выражается отдельной result policy.

Собственные `set`-сеттеры служебного record нужны только для object initializer
и `with` и не связаны с `set`/`init`-семантикой destination. Последующая
mutation уже созданного local plan-а по-прежнему не входит в declarative
grammar; она остаётся лишь совместимой точкой возможного расширения после v0.

### 7.3. Применение плана

`Members` всегда применяется к выбранному `result`, а не к `previous`.
Параметр `previous` внутри lambda всегда означает исходный destination-вход
после null-предобработки, даже если `Resolve` либо `ResolveUsing` выбрал
replacement.

В трёх- и четырёхпараметрической перегрузках `result` означает именно
фактически выбранный instance: previous, constructor/convention result либо
runtime result, включая cached instance и его derived runtime-тип. Это позволяет
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
покрывает result из `ConstructUsing` / `ResolveUsing` без специальной привязки
member API к factory.

Параметр `result` не использует presence-wrapper и генерируется без
корневой nullability destination: `Customer?` даёт `Customer result`,
`Point?` — `Point result`, а вложенные nullable annotations сохраняются.
Любое выражение, которое фактически использует `result`, выполняется только
после появления non-null instance. Если `ConstructUsing` или `ResolveUsing`
вернул `null`, mapping завершается до применения member rules; недостижимое
состояние «result отсутствует» не несёт полезной информации и ложно намекало
бы на возможность заменить терминальный `null`.

Generator анализирует каждый structured member rule отдельно. Прямая либо
транзитивная ссылка на `result` в value, declarative local или условии делает
зависимые от неё rules post-creation. Само наличие третьего lambda-параметра
ничего не меняет. Поэтому result-independent `init` и creation-time `required`
rules допустимы и в result-aware перегрузках; diagnostic нужен только
тогда, когда конкретный creation-time rule либо условие его применимости
зависит от ещё не созданного result.

Пример:

```csharp
builder.Map<CustomerDto, Customer>()
    .Resolve((source, previous) =>
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

Если `Resolve` вернул replacement, `Name` и `Revision` применяются к
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

Если `ConstructUsing` / `ResolveUsing` возвращает уже созданный объект,
применить к нему `init`-only rule невозможно. Явная попытка совместить runtime
result policy с соответствующим `Members` rule должна давать diagnostic.
Destination без constructor surface вообще не получает `init`-only member,
поэтому для него такую конфигурацию нельзя записать.

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
| Standalone `Update(source, members.GetOnly)` | Обновить eligible non-null read-only reference member in-place и отбросить nested result |
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
runtime callback-ом или default initialization. Для previous он сохраняет
текущее значение выбранного result.

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
Readable non-writable properties и доступные `readonly` fields появляются в
generated `DestinationMembers` как get-only markers только тогда, когда их тип
после снятия корневой nullability является допустимым non-opaque reference-type
nested destination в v0. Такой proxy способен обозначить in-place nested
Update существующего объекта. Read-only value types, opaque и остальные
неподдерживаемые nested-root формы proxy не получают. `init`-only property
остаётся creation-only и такого proxy не получает. Для get-only marker
разрешена только standalone форма:

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
некуда. Такие markers не участвуют в conventions, `Auto()` и unmapped-member
validation.

Параметры `previous` и `result` в declarative `Resolve`/`Members` являются
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
Previous-result и result из `ConstructUsing` / `ResolveUsing` уже созданы
независимо от перегрузки, поэтому к ним применимы только доступные
post-construction assignments.

Каждое выражение вычисляется не более одного раза. Если выбранный execution
path требует его значение, оно вычисляется ровно один раз; невыбранные ветки,
неприменимые rules и значения другого mapping path не вычисляются. Declarative
local создаёт явную dependency: его initializer выполняется до использующих
его выражений. Внутри отдельного выражения сохраняется обычная C#-семантика, а
explicit constructor arguments вычисляются слева направо в порядке записи.

Dependency graph является общим для structured `Construct` / `Resolve` и
`Members`. Если на
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
structured `Construct` / `Resolve` и `Members`; `ConstructUsing`,
`ResolveUsing` и `Convert` остаются обычными C# blocks, из которых generator
не извлекает cross-plan subexpressions.

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

Structured `Construct`, `Resolve` и `Members` являются конечным анализируемым
DSL. В них поддерживаются:

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

Во внешнем structured `Construct`, `Resolve` или `Members` block не
поддерживаются:

- locals без initializer-а, последующие/deconstruction/compound assignments и
  `++` / `--`;
- loops, `break` / `continue` и standalone statements только ради side effect;
- local functions, объявленные во внешнем declarative block;
- `try` / `catch` / `finally`, `using`, `lock`, labels / `goto`;
- `ref` / `using` locals, `unsafe` / `fixed`, `async` / `await` и `yield`.

Сложное вычисление выносится в обычный instance/static member mapper-а, сложное
получение result — в `ConstructUsing` либо `ResolveUsing`, а полностью
специальный алгоритм — в `Convert`. Runtime result callbacks и `Convert`
переносятся как обычный синхронный C# block; внутри них доступны mutation,
loops, `try` / `finally`, nested local functions и остальные допустимые для их
сигнатуры синхронные конструкции.

Переносимый пользовательский код может обращаться к instance/static members
mapper-а, static API, типам, method groups и compile-time constants.
Configure-local compile-time constant подставляется как constant value.
Обычные Configure-locals, параметр `builder` и local functions, объявленные во
внешнем `Configure`, не захватываются: их runtime lifetime не совпадает с
lifetime generated mapper-а. Переиспользуемая логика должна быть обычным
member-ом mapper-а. Local functions внутри runtime/manual block
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
внутри structured result policy и `Members`.

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
previous и отсутствии `Resolve` / `ResolveUsing` он останется result без
изменений.

Для динамического алгоритма, который в runtime иногда должен выполнить полный
no-op, используется `Convert`. Отдельный `Skip()` в v0 не добавляется;
first-class whole-plan no-op и общая patch/merge policy полностью отложены до
после v0. Исследование возможной null-assignment policy сохранено в
[`NULL_ASSIGNMENT_HANDLING_RESEARCH.md`](NULL_ASSIGNMENT_HANDLING_RESEARCH.md).

## 8. Полностью ручной mapping

### 8.1. `MappingContext`, call frame и три overloads

Тип текущей mapping operation является частью `MappingContext` текущего
вызова, а не destination-specific previous-object:

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

Оба типа находятся в namespace `Morphant.Context`. `MappingOperation`
описывает ровно одну выполняемую operation и поэтому не переиспользует
flags-enum `MappingMode`. `Operation` доступен только для чтения; значение `0`
не является operation.

`MappingContext` является immutable call frame текущего outer либо nested
вызова. Morphant создаёт новый frame для каждого `Map`, передаёт его по
значению и не мутирует. Общее состояние всей chain хранится отдельно во
внутреннем reference-type `MappingScope`:

| Call frame (`MappingContext`) | Общий `MappingScope` |
|---|---|
| Текущая `Operation` | Scoped mapper |
| Immutable value | Будущий reference cache и внутренний chain state |
| Новый для каждого nested `Map` | Одна reference identity на всю chain |
| Описывает текущий вызов | Завершается вместе с root `Map` |

Root mapper и `context.Mapper` реализуют один `IMapper`, но имеют разный
lifetime. Root-вызов создаёт новый scope. `context.Mapper` привязан к уже
существующему scope и создаёт новый frame для каждой nested operation.
Отдельный `IContextualMapper` не вводится.

`Convert` является pair-specific generated extension на fluent pair-builder и
получает три префиксные runtime overloads:

```csharp
.Convert(source => ...)

.Convert((source, previous) => ...)

.Convert((source, previous, context) => ...)
```

Каждая overload полностью реализует все разрешённые `MappingMode` operations
pair. Source-only форма намеренно не различает Create, Update и наличие
destination. Previous-aware форма различает `Option.None` / `Option.Some`, но
не отличает `Create` от `Update(null)`. Полная форма дополнительно видит
`context.Operation` и scoped mapper. Отдельная форма `(source, context)` не
добавляется: используется полная overload с проигнорированным previous.

`TSource?` во всех формах означает исходное runtime-значение до
`NullSourceHandling`. `Option<TDestination>` формируется из исходного
destination без `NullDestinationHandling`: explicit null даёт `Option.None`,
а исходную public operation при необходимости сообщает context.

Pair-specific generation нужна для точной root-normalization. Обычный generic
`MapperBuilder<TSource, TDestination>` не может выразить одновременно исходный
nullable source, `Option` без корневой nullability destination и точный result
для всех nullable value/reference forms.

Выбор arity не меняет applicability либо manual lifecycle, а только доступные
inputs. Все формы являются обычными synchronous C# callbacks и допускают
natural method groups/materialized delegates. `MappingContext`, когда
присутствует, всегда последний parameter. Named delegates используют family
`Convert`; context-aware форма получает отдельный `TContext` и тем самым не
сталкивается по generic arity с previous-aware формой.

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
- result policy и `Members` не выполняются;
- `Auto()`, `Ignore()`, `Map(...)`, `Create(...)`, `Update(...)`,
  `ByConvention()` не являются DSL-маркерами и недоступны;
- ручные nested mappings доступны через `context.Mapper.Map(...)`;
- scoped mapper автоматически создаёт для вложенного вызова новый
  `MappingContext` и сохраняет общий scope;
- lambda возвращает настоящий `TDestination`;
- `MappingMode` по-прежнему определяет, какую публичную операцию можно вызвать.

Для одной пары разрешён ровно один `Convert`. Его смешивание с любой result
policy, `Members` или declarative constructor/member-specific configuration
является ошибкой конфигурации и должно диагностироваться. Унаследованные общие
settings, не имеющие эффекта в manual mapping, не запускают скрытый
declarative pipeline.

### 8.4. Runtime и declarative context за пределами `Convert`

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

Настоящий `MappingContext` передаётся context-aware overload-ам
`ConstructUsing` и `ResolveUsing`. Это один расширяемый runtime call frame: при
появлении новой runtime capability она будет доступна максимальным runtime
callbacks, а не только `Convert`.

Максимальные overload-ы structured `Construct`, `Resolve` и `Members` получают
вместо него `MappingContextMarker`. Marker раскрывает `Operation`, но намеренно не
`Mapper`; поэтому operation-aware declarative rules не требуют перехода на
runtime model, а nested mapping остаётся выражен только DSL markers. Два типа
не связаны наследованием и не взаимозаменяемы.

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

Для result policy и `Members` null handling выполняется до declarative
pipeline.

Порядок остаётся таким:

1. Проверить source и применить эффективный `NullSourceHandling`.
2. Для `Update` проверить destination и применить эффективный
   `NullDestinationHandling`.
3. Сформировать нормализованный `Option<TDestination>`.
4. Выбрать `result` через configured/default result policy.
5. Если пользовательский runtime result callback вернул `null`, немедленно вернуть
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
| `Throw` | Бросить `NullDestinationException` до result policy и `Members` |
| `Create` | Считать explicit `null` отсутствующим previous и перейти в no-previous construction branch |

`NullDestinationHandling.Create` не обещает новую identity: configured
no-previous policy может использовать constructor, factory или cache. Публичная
операция при этом остаётся `Update`, поэтому дополнительно включать
`MappingMode.Create` не требуется; достаточно доступного `MappingMode.Update`.

После `NullDestinationHandling.Create` следующие вызовы имеют одинаковый
`previous`, но намеренно различимы через `MappingContextMarker.Operation`:

```csharp
Map(source)
Map(source, null)
```

В обоих случаях `Resolve` / `Members` получают `Option.None`. При этом marker
сообщает `Create` для первого вызова и `Update` для второго; это независимая
информация, которую presence-wrapper восстановить не может.

`NullSourceHandling` сохраняет текущие варианты и precedence. В частности,
если effective policy возвращает результат или бросает исключение, ни result
policy, ни `Members` не выполняются. Вариант `Throw` бросает
`NullSourceException`.

### 9.2. `null` из пользовательского creation-кода

Фактический destination могут вернуть runtime result policies
`ConstructUsing` и `ResolveUsing`.

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
`ResolveUsing`, вернувший `null`, тем самым намеренно заменяет существующий
destination на `null`.

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

var result = RunResultPolicy(source, previous, context);

if (result is null)
    return null!;

ApplyMembers(source, previous, result);

return result;
```

`RunResultPolicy` вызывает effective `Resolve` / `ResolveUsing`, если настроена
полная policy; иначе — `Construct` / `ConstructUsing`, поскольку previous
отсутствует. Если result policy не настроена, destination с constructor surface
выполняет convention construction. Pair без constructor surface, включая
opaque destination, является ошибочной конфигурацией для reachable
no-previous ветки без `ConstructUsing` / `ResolveUsing`.

`Map(source, destination)` после null-предобработки работает так:

```csharp
ApplyNullSourceHandling(source);
var previous = ApplyNullDestinationHandling(destination);

Destination result;

if (fullResultPolicyConfigured)
{
    result = RunFullResultPolicy(source, previous, context);
}
else if (!previous.HasValue)
{
    result = RunNoPreviousPolicyOrConvention(source, context);
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
эмитить её только для runtime result callbacks, где `null` действительно возможен;
constructor, convention и previous дополнительных проверок не требуют.

Configured result policy никогда не подменяется другой lambda. Structured plan
lowering и runtime callback в итоге дают один настоящий `Destination result`.
Для пары существует не более одной из четырёх result policies.

Если `Members` не настроен, `ApplyMembers` применяет только effective
`MemberSelection` conventions. Если generated member surface отсутствует, эта
стадия не содержит применимых members.
В формах `Members` с `result` generator связывает фактически
выбранный non-null `result` непосредственно, без presence-wrapper, только с
выражениями, которые его используют. Lambda не является единым runtime-
callback и не образует отдельную member-фазу.

`ApplyMembers` обозначает единый effective plan, а generator может распределить
его части по разным допустимым фазам:

1. Generator объединяет inherited и local member rules независимо от формы
   перегрузки, выбирает declarative ветви и разрешает member-plan `with`-
   overlays. Заменённые rules удаляются вместе с ненужными dependencies.
2. Для structured `Construct` / `Resolve` и effective `Members` строится общий
   path-sensitive dependency graph. Одинаковые bound subexpressions становятся
   одной computation node; runtime/manual C# blocks остаются непрозрачными.
3. Для structured constructor/convention branch result-independent значения,
   необходимые `init` и creation-time `required` rules, могут быть вычислены
   при создании объекта. Explicit constructor arguments сохраняют обычный
   порядок вызова.
4. Выражение, зависящее от `result`, вычисляется только после появления
   non-null instance. Setter/field rule тогда применяется post-construction;
   result-dependent creation-time rule является ошибочной конфигурацией.
5. Previous и runtime result branches уже имеют result; доступные им
   post-construction rules применяются независимо от формы `Members`.
   Неприменимые `init` rules не вычисляются.
6. `null` runtime result завершает mapping до применения любых member
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
destination и действуют также для runtime result policies и `Convert`. Если типы
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
| Runtime result policy | Любая eligible pair | Pair-specific generated `ConstructUsing` и `ResolveUsing` в `MappingExtension` |
| Manual | Любая eligible pair | Три pair-specific generated `Convert` overload-а |
| Structured construction | Есть хотя бы один поддерживаемый доступный destination constructor, включая parameterless | Generated `Construct` и `Resolve`, возвращающие `DestinationConstruction` |
| Members | Есть поддерживаемый writable/creation-time body-member либо eligible read-only nested-update proxy; destination не opaque | Generated `DestinationMembers` и четыре `Members` overload-а |
| Collection / projection | Не входят в v0 capability model | Никакого generated surface; рассматриваются после v0 на отдельных этапах |

Structured и runtime result methods имеют разные имена и могут одновременно
существовать в IntelliSense, но в конфигурации занимают один
взаимоисключающий result-policy slot. `Convert` доступен для той же пары, но
является альтернативой всему declarative pipeline, а не fallback отдельной
неподдерживаемой ветки. Source shape сама по себе не меняет destination
surface.

Отсутствие members не убирает declarative surface. Pair с поддерживаемым
constructor получает structured `Construct` / `Resolve`; для любой eligible
pair независимо генерируются `ConstructUsing` / `ResolveUsing`. `Update` всё
равно может вернуть previous без изменений. No-previous ветка pair без
constructor surface требует configured runtime policy. Единственным общим gate
для публичной операции остаётся эффективный `MappingMode`.

Под «есть member» понимается member, реально включаемый в generated
`DestinationMembers`, а не любой symbol типа. Помимо обычных writable members,
учитывается read-only proxy, если readable non-writable property либо доступный
`readonly` field имеет ссылочный тип, который является допустимым non-opaque
nested destination root в v0. Такой proxy существует исключительно для
standalone nested `Update`. Read-only value types, opaque и остальные
неподдерживаемые nested-root формы, static members, indexers и прочие
непригодные symbols не считаются. `init`-only property без constructor surface
остаётся creation-only и не превращается в proxy.

Под «есть constructor» понимается instance-constructor любой arity, который
generator может использовать для создания данного destination. Недоступные и
неподдерживаемые constructors не считаются; constructor abstract-типа сам по
себе не делает тип создаваемым. Built-in, enum и отдельно определённые общей
type policy scalar-категории не получают structured surface, даже если metadata типа
технически содержит public constructors: Morphant намеренно не моделирует их
как structural constructor DSL.

Constructor и destination member accessibility вычисляются из общего
generated assembly-context, а не из lexical context конкретного mapper-а.
Поэтому public и доступные `internal` symbols образуют единый стабильный
surface, тогда как private/protected symbols не появляются даже у mapper-а,
который благодаря вложенности мог бы обратиться к ним вручную. Одна и та же
destination definition тем самым всегда получает одну форму construction и
один member surface независимо от набора зарегистрировавших её mapper-ов.

В v0 opaque scalar policy сохраняет полную проверенную границу прежнего
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

`ConstructUsing` / `ResolveUsing` получают уже созданный instance, к которому
Morphant может применить обычные setter-rules и mutable-field rules.
`required` не исключает такие members, если они остаются post-construction
assignable. `init`-only properties без structured constructor surface в member
plan не входят. Runtime result не является окончательным результатом в смысле
`Convert`. Opaque
destination member surface не получает.

Например, interface не имеет constructor surface, но может независимо иметь
writable body-members:

```csharp
builder.Map<Source, IDestination>()
    .ConstructUsing((source, _) => factory.Create(source.Id))
    .Members(source => new()
    {
        Name = source.Name
    });
```

Здесь runtime lambda получает экземпляр, а declarative member plan продолжает
иметь самостоятельную ценность. Обычный `set` либо mutable field доступен в
`Members`, в том числе при `required`; `init`-only property в этом surface не
генерируется.

Отдельного служебного creation type для scalar, opaque value object,
factory-only class, interface или abstract destination не создаётся. Их runtime
result policy сохраняет standard null handling. Declarative member plan дополнительно
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
| `NullSourceHandling` | Выполняется до result policy / `Members` | Не применяется |
| `NullDestinationHandling` | Выполняется перед previous normalization только в `Update` | Не применяется |
| `MemberSelection` | Управляет неуказанными supported body-members; работает и после runtime result policy | Не применяется |
| `ConstructorSelection` | Применяется только к structured convention / `ByConvention` creation | Не применяется |
| Boxing policy | Ограничивает только automatic constructor/member conversions; explicit expressions остаются обычным C# | Не применяется |
| `UnmappedMemberValidation` | Проверяет только mapping plan, который строит Morphant; runtime result callback не анализируется как набор member mappings | Не применяется |

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
policy должна диагностироваться. Для pair без structured constructor surface явно заданный
`ConstructorSelection` также ошибочен; остальные declarative settings работают
на своих стадиях, даже если на конкретной pair не найдено ни одного кандидата
для warning или conversion.

Частичная capability никогда не включает скрытый fallback. Недоступная
operation, отсутствующая обязательная runtime result policy, невозможный explicit
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
- overload-ы `Construct` / `Resolve` / `ConstructUsing` / `ResolveUsing` /
  `Members` / `Convert` и их XML documentation
  сохраняют один детерминированный порядок между regeneration-ами.

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
kind `Construction`, member plan — `Member`, generated `Construct` / `Resolve`
/ `ConstructUsing` / `ResolveUsing` / `Convert` methods — `MappingExtension`, а
`Members` methods — `MemberExtension`. Оба extension-artifact-а дополняют одну
internal partial class
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
type. Выбранный result регистрируется после effective structured либо runtime
result policy, но до `Members`: setter/field cycle тогда может замкнуться, а
constructor, `init` и required initializer cycle до появления result остаётся
неразрешимым.

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
    .Resolve((source, previous) =>
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

`Resolve` является полным выбором result для обоих публичных
вызовов. `Members` применяется уже к выбранному result.

### 13.4. Всегда создавать replacement

```csharp
builder.Map<Source, Destination>()
    .Resolve((source, _) => new(source.Id))
    .Members((source, _) => new()
    {
        Name = source.Name
    });
```

`Resolve` намеренно игнорирует previous и получает replacement в обеих
операциях.

### 13.5. Factory плюс members

```csharp
builder.Map<OrderDto, Order>()
    .ConstructUsing((source, _) => orderFactory.Create(source.Id))
    .Members((source, _) => new()
    {
        Number = source.Number
    });
```

Factory выполняется только в no-previous ветке `ConstructUsing`. При
обычном `Update` используется previous и применяется `Number`.

### 13.6. Runtime factory-only destination плюс members

```csharp
builder.Map<OrderDto, IOrder>()
    .ResolveUsing((source, previous, _) =>
        previous.HasValue && CanReuse(previous.Value, source)
            ? previous.Value
            : orderFactory.Create(source.Id))
    .Members((source, _) => new()
    {
        Number = source.Number
    });
```

У interface нет constructor surface, поэтому `ResolveUsing` возвращает
настоящий `IOrder`. Возврат `previous.Value` сохраняет existing instance;
factory даёт
replacement. В обеих ветках применимый member plan выполняется после выбора
result.

### 13.7. Scalar и opaque value object

```csharp
builder.Map<Order, decimal>()
    .ConstructUsing((source, _) =>
        source.Items.Sum(x => x.Price * x.Count));

builder.Map<string, OrderNumber>()
    .ConstructUsing((source, _) => OrderNumber.Parse(source));

builder.Map<string, Guid?>()
    .ConstructUsing((source, _) =>
        Guid.TryParse(source, out var value)
            ? value
            : null);
```

Для destination без structural constructor surface `ConstructUsing` сохраняет
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
replacement через `Resolve`:

```csharp
builder.Map<SnapshotDto, Snapshot>()
    .Resolve((source, previous) =>
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
вычисляются. `Resolve` нужен только для реального выбора
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

- несовместимое compilation environment или отсутствие однозначного
  совместимого обязательного contract Morphant;
- использование Morphant builder-а вне поддерживаемого прямого линейного
  `Configure` flow;
- повторную регистрацию одной canonical pair внутри одного mapper-а;
- любую вторую result policy `Construct` / `Resolve` / `ConstructUsing` /
  `ResolveUsing`, включая повтор одной family и смешение разных имён;
- любой второй локальный `Members`; форма перегрузки значения не имеет;
- повторный `Convert`;
- смешивание `Convert` с любой result policy или `Members`;
- pair-specific constructor/member settings, несовместимые с manual mapping;
- достижимый explicit `init`-rule либо creation-time `required`-rule structured
  surface, который невозможно применить в конкретной creation branch: result
  уже создан runtime callback-ом либо value/условие rule транзитивно зависит от ещё
  не созданного result; previous-result сохраняет такой member без вычисления
  неприменимого expression;
- reachable no-previous branch destination без convention construction и без
  configured `ConstructUsing` / `ResolveUsing`;
- `null` вместо generated `DestinationConstruction` или `DestinationMembers`
  plan;
- невозможный explicit constructor/member marker;
- две registrations одного generic mapper-а, чьи pair shapes могут
  унифицироваться при подстановке type parameters и породить одинаковый
  generated `ITypeMapper` contract.

Одинаковая canonical pair в разных mapper types и assemblies разрешена.
Отсутствие кандидата, несколько registrations, registration, разрешившаяся в
`null`, и завершённый mapping scope наблюдаются только при runtime dispatch и
не являются compile-time diagnostics: generator не видит application-wide
`IServiceProvider`.

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

Исключения из пользовательских result policies, `Members`, `Convert`, source
expressions, mapper dependencies и application service provider не
оборачиваются и сохраняют исходный тип, сообщение и stack.

## 15. Зафиксированные законы дизайна

1. `Map(source)` и `Map(source, destination)` остаются двумя публичными
   mapping-операциями; effective `MappingMode` управляет их доступностью.
2. Result policy и `Members` выполняются только после declarative null handling.
3. `Construct` и `ConstructUsing` выполняются только при отсутствии previous.
   Две structured arities `Construct` различаются только наличием marker
   context; короткая и context-aware `ConstructUsing` различаются только
   наличием настоящего `MappingContext`.
4. `Resolve` и `ResolveUsing` выполняются с `Option.None` и `Option.Some`.
   Две structured arities `Resolve` различаются только наличием marker context;
   короткая и context-aware `ResolveUsing` различаются только наличием
   настоящего `MappingContext`.
5. Если result policy отсутствует, destination с constructor surface создаёт
   no-previous result по convention. Pair без constructor surface ошибочна для
   reachable no-previous ветки без `ConstructUsing` / `ResolveUsing`.
   Существующий previous при отсутствии full resolver сам становится result.
6. `Construct`, `Resolve`, `ConstructUsing` и `ResolveUsing` занимают один
   взаимоисключающий result-policy slot; для pair допустим не более чем один
   такой fragment любой overload.
7. Structured result policy настраивает constructor plan, но не declarative
   body-member rules. Runtime result policy может вернуть object initializer
   либо иначе инициализированный instance, не создавая member rules.
8. `Members` является единственным declarative API для body-members. Structured
   surface включает `init` и `required`; surface без constructor capability
   включает обычные setters и mutable fields, в том числе `required`, но не
   `init`-only properties. Readable non-writable properties и доступные
   `readonly` fields дополнительно входят в обе поверхности только как get-only
   proxy для standalone nested Update, если имеют допустимый non-opaque
   reference-type nested destination; read-only value-type и прочие
   неприменимые nested targets proxy не получают. Эти proxy не участвуют в
   обычных member rules.
9. Для member-capable pair всегда генерируются четыре prefix-
   `Members`-перегрузки: `source`; `source`/`previous`;
   `source`/`previous`/`result`; `source`/`previous`/`result`/`context`.
   В локальной pair можно вызвать
   ровно один `Members`; любой второй вызов ошибочен.
   `IncludeBase<TBaseSource, TBaseDestination>()` объединяет унаследованный и
   локальный plans независимо от формы перегрузки.
10. Все формы `Members` являются одним declarative DSL и применяются к
    выбранному result; выбор overload сам по себе не задаёт evaluation phase.
    Набор доступных members определяется construction capability.
11. `previous` в `Members` всегда означает исходный нормализованный input, а не
    выбранный result. В формах с `result` это
    фактически выбранный non-null destination без presence-wrapper и без
    корневой nullability. Оба параметра являются read-only источниками:
    assignment, increment/decrement и `ref`/`out` mutation запрещены.
12. Неприменимое `init`-выражение в already-created-result ветке не
    вычисляется. В structured creation result-independent `init` и
    creation-time `required` rules допустимы во всех формах `Members`;
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
16. `Convert` является pair-specific generated extension на обычном fluent
    pair-builder, а не отдельным builder-типом.
17. У `Convert` есть три prefix-overload-а: с `source`; с `source` /
    `Option<TDestination>`; с `source` / `Option<TDestination>` /
    `MappingContext`. Arity меняет только доступные данные.
18. `Convert` полностью заменяет declarative pipeline и не запускает
    null-handling settings.
19. `MappingContext.Operation` сообщает текущую публичную операцию, а
    `Option<TDestination>` независимо сообщает наличие фактического
    destination instance.
20. Короткие и context-aware `ConstructUsing` / `ResolveUsing` генерируются в
    pair-specific `MappingExtension` для каждой eligible mapping-пары вместе с
    тремя `Convert` overload-ами. Четыре `Members` overload-а генерируются
    независимо при наличии применимых body-members у non-opaque destination.
21. Наличие хотя бы одного поддерживаемого constructor, включая parameterless,
    генерирует structured `Construct` и `Resolve`. Единственный parameterless
    constructor является полноценным surface. При отсутствии constructor
    surface либо для opaque destination эти generated methods отсутствуют.
22. `ConstructUsing` / `ResolveUsing` допускают обычный C# object initializer и
    возвращают уже созданный runtime result: к нему применимы обычные
    setter/mutable-field rules и conventions, но не creation-time `init` rules.
    Opaque destination атомарен и member surface не получает.
23. Возвращённый `Map` result всегда авторитетен.
24. Никаких скрытых fallback между manual и declarative mapping либо между
    разными configured lambdas нет.
25. В structured surface `Option<TDestination>` неявно преобразуется в
    `DestinationConstruction`, поэтому возврат самого `previous` выбирает existing
    result. `ResolveUsing` возвращает настоящий `TDestination` и использует
    `previous.Value`; отдельный runtime plan или implicit unwrap не вводится.
    Произвольный готовый `TDestination` не является structured plan и требует
    полной runtime result policy.
26. Для `ByConventionMarker` генерируется один creation-plan constructor с
    необязательным `DestinationConstructorParameters`.
27. Generated `DestinationMembers` является record с обычными `set`-
    properties. Object initializer и `with` поддерживают declarative overlay
    body-member rules; более поздний rule заменяет ранний без его вычисления.
    Creation-plan не получает `with`, а mutation уже созданного member-plan
    по-прежнему не поддерживается.
28. Максимальные overload-ы `ConstructUsing`, `ResolveUsing` и `Convert`
    получают текущий `MappingContext` последним параметром и используют его
    для runtime nested mappings. Короткие Using-overload-ы опускают только
    context и сохраняют ту же lifecycle applicability.
29. `MappingContext` является immutable value-type frame текущего outer или
    nested вызова; scoped `IMapper` создаёт новый frame с собственной
    `Operation`, разделяя общий mapping scope без mutation и восстановления.
30. Максимальные `Construct`, `Resolve` и `Members` получают
    `MappingContextMarker`, который предоставляет только `Operation` и не имеет
    runtime instance. Сам marker нельзя использовать как значение; declarative
    nested mapping выражается только `Map` / `Create` / `Update` markers.
31. Public `Map` принимает nullable source/destination inputs, но возвращает
    ровно выбранный пользователем `TDestination`, а не безусловный
    `TDestination?`.
32. Mapping-produced `Option<TDestination>` использует destination без
    корневой nullability и в роли previous никогда не содержит `Some(null)`;
    общий `Option<T>` при nullable `T` такого ограничения не имеет.
33. Result policies и `Members` получают source после declarative null handling
    как non-null underlying type; previous также root-normalized. Возвращаемый
    тип `ConstructUsing` / `ResolveUsing` совпадает с destination-типом
    pair-builder-а и не нормализуется. `Convert` получает исходное runtime-
    значение source.
34. `null` из `ConstructUsing` или `ResolveUsing` является авторитетным
    терминальным result: `Members` не выполняется, exception и fallback не
    генерируются, null-handling policies повторно не применяются.
35. `Convert` возвращает пользовательский result без generated null guard;
    `null` вместо generated creation/member plan остаётся ошибкой DSL, а не
    destination-result.
36. Root-вызовы используют независимые scopes и могут выполняться параллельно;
    scoped mapper действует только до завершения root `Map`, а параллельные
    nested-вызовы внутри одного scope не поддерживаются.
37. Каждое declarative expression вычисляется не более одного раза. Structured
    `Construct` / `Resolve` и `Members` имеют общий path-sensitive dependency graph: одинаковое
    bound subexpression, нужное обеим частям на выбранном пути, вычисляется
    один раз и разделяется между ними. Невыбранные ветки, неприменимые rules и
    operation-specific значения другого пути не вычисляются; runtime и manual
    blocks остаются вне cross-plan sharing. Structured `Resolve`
    специализируется по известному наличию previous для
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
41. Structured `Construct`, `Resolve` и `Members` поддерживают только конечный
    анализируемый control flow без изменяемого состояния. `ConstructUsing`,
    `ResolveUsing` и `Convert` являются обычными синхронными C# blocks. Ни одна форма не
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
    mapping-позициях даже для runtime/manual mapping.
44. Для любой другой eligible pair доступны runtime contract и pair-specific
    generated `ConstructUsing`, `ResolveUsing` и `Convert`; destination с
    constructor capability дополнительно получает generated `Construct` /
    `Resolve`, а non-opaque destination — `Members` при наличии применимых
    body-members.
    Значение отложенной категории внутри разрешённого root
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
    `Resolve`, а record-copy и иная специальная reconstruction —
    `Convert`.
53. `Construct` / `ConstructUsing` при существующем previous не является immutable
    replacement-path. `ByCopy`, generated destination-copy `with` и implicit
    record cloning в v0 отсутствуют; member-plan overlay из закона 27 не
    создаёт replacement.
54. Declarative existing-ветка без `Resolve` / `ResolveUsing`, которая
    статически не может ни заменить result,
    ни выполнить post-construction assignment, возвращает исходный destination
    без изменений. Для такого no-op не требуется resolver;
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
    зарегистрирован только после получения result policy; поэтому built-in
    preservation не
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
    двухаргументной формы API. Узел composition — `(constructed mapper-level,
    canonical pair)`: поиск сначала идёт среди остальных pairs текущего level
    независимо от порядка объявлений, затем по подключённым base levels и
    выбирает ближайшее точное совпадение. Текущий узел исключён, но exact
    same-pair connected base level является валидным кандидатом. Совместимый
    graph v0 ацикличен по построению; отдельного cycle state нет.
64. Через typed `IncludeBase` наследуются все map-level settings, включая
    `MappingMode` и `ConstructorSelection`. Settings precedence — current pair,
    included base pair, current mapper root, connected base roots, assembly,
    library default; `Default` продолжает поиск на следующем уровне.
65. Cross-pair `IncludeBase` импортирует только `Members`. Правила независимо
    от формы перегрузки объединяются по destination member с локальным
    приоритетом, включая expression, `Auto()` и `Ignore()`. Conventions и
    constructor selection вычисляются заново для текущей pair, а dependencies
    effective rules анализируются отдельно. Result policies и `Convert` между
    разными pairs не импортируются.
66. Exact same-pair из connected base mapper-а импортирует весь applicable
    effective plan, включая result policy либо `Convert`. Локальный `Convert`
    заменяет inherited plan; локальный declarative plan отбрасывает inherited
    `Convert`; локальная result policy перекрывает inherited result policy, а
    `Members` объединяются по закону 65. Runtime casts и адаптация callbacks
    между разными destination types не выполняются.
67. Base mapper не требует `MorphantMapperAttribute`, если его `Configure`
    доступен как source в текущей compilation. Прямой
    `base.Configure(builder)` поддерживается statement- и expression-bodied
    формой; повторный вызов ошибочен. Generic base DSL сохраняет открытый
    generated surface, а effective derived plan использует constructed type
    arguments, включая nested mapper declarations. Проверка accessibility
    выполняется только для оставшихся effective inherited result-policy,
    `Members` и `Convert` expressions, поэтому полное локальное перекрытие либо
    отбрасывание удаляет недоступный callback до emission.
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
    `BigInteger`, `Complex`, `Rune`, `Index` и `Range` являются opaque
    destinations без `Members` и automatic convention construction; custom
    value type следует обычной capability model.
83. Потенциально унифицирующиеся pair shapes одного generic mapper-а являются
    compile-time configuration conflict, а не runtime duplicate registration.
84. `IncludeMembers` как first-class convention flattening обязателен после
    v0; explicit member expressions, tuple-source и nested `Map` его не
    заменяют.
85. Непубличные generated execution helpers, которые становятся членами
    пользовательского mapper-а, используют зарезервированный префикс `__`:
    `__Create`, `__Update`, `__ConstructDestination`, `__ResolveDestination`,
    `__ConstructUsing`, `__ResolveUsing` и `__ConvertDestination`. При конфликте
    добавляется числовой суффикс;
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
общий dependency graph structured result policy / `Members` автоматически делит
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
| Единый DSL `Template` | Structured `Construct` / `Resolve` + `Members` |
| `TemplateMode.Raw` | Явный `Convert`; для получения instance с последующими conventions — `ConstructUsing` / `ResolveUsing` |
| Direct template для scalar | `ConstructUsing` / `ResolveUsing` |
| Source/destination-aware template | `Option<T>`, `Resolve` / `ResolveUsing` и result-aware `Members` |
| Factory/cached destination | `ConstructUsing` / `ResolveUsing`, затем общий member plan |
| Constructor/member markers | Сохранены на соответствующей plan-части |
| `IContextualMapper`-подобный nested dispatch | Scoped `context.Mapper : IMapper` |
| Record `with` настоящего destination | Обычный C# внутри `Convert` |
| `base.Configure` и typed `IncludeBase` | Явно разделённое наследование root settings и effective plan конкретной base pair |

Крупной потерянной feature в core v0 после этих поправок нет.

### 16.3. Сравнительная оценка

| Критерий | Прежний `Template` | Новый дизайн |
|---|---|---|
| Constructor + members call site | Компактнее в одной lambda | Иногда требует два fluent-вызова и повтор записи expression |
| Разделение ответственности | Одна форма одновременно описывает creation, members, existing и raw mode | Result policy, `Members` и `Convert` отвечают каждый на один вопрос |
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

Основная semantics core v0 реализована, но согласованная ревизия callback
surface из разделов 6–8 и уточнённая граница read-only proxy из разделов 7 и
11 ещё не перенесены в production-код, generated API и tests. Пользовательская
документация уже описывает целевой контракт и явно помечена как target.
Текущее состояние и следующий cross-cutting implementation slice фиксируются в
[`MAPPING_API_IMPLEMENTATION_PLAN.md`](MAPPING_API_IMPLEMENTATION_PLAN.md), а
независимая оценка полноты сценариев — в
[`MAPPING_API_FINAL_AUDIT.md`](MAPPING_API_FINAL_AUDIT.md).

Observable runtime failures и generated exception-stub boundary реализованы и
зафиксированы разделом 14.2. Compile-time diagnostics остаются отдельным
поздним планом и приостановлены до завершения callback API. Collections,
projection, polymorphism, reference handling и
остальные перечисленные выше возможности остаются post-v0 направлениями и не
расширяют текущий mapping semantics неявно. До отдельного продуктового решения
их API не должен определяться удобством существующей реализации.
