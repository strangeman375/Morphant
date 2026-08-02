# Новый дизайн mapping API Morphant

Статус документа: согласованный рабочий дизайн, зафиксированный перед началом
переработки реализации. Документ описывает целевой API. Текущий код и
`IMPLEMENTATION_PLAN.md` пока отражают прежний `Template()`-дизайн и должны
быть пересмотрены отдельно после проверки этого документа.

## 1. Цель переработки

Текущий `Template()` одновременно описывает создание destination, параметры
конструктора, body-members, работу с существующим destination и полностью
ручной mapping. Из-за этого одна lambda имеет различную семантику в `MapNew` и
`MapExisting`, а `TemplateMode` дополнительно переключает способ её
интерпретации.

Новый API разделяет эти обязанности:

| API | Единственный вопрос |
|---|---|
| `Create` | Как получить result и настроить его constructor parameters? |
| `Members` | Как маппить body-members destination? |
| `MapManually` | Как целиком выполнить mapping без декларативного pipeline? |

Из целевого дизайна удаляются:

- `Template()`;
- `TemplateMode` и разделение на `Dsl` / `Raw`;
- отдельный `SelectResult`;
- отдельный builder для ручного mapping;
- специальный `Skip()` для полного пропуска member mapping;
- служебный `with`-overlay для template-типа.

`Create`, `Members` и `MapManually` не являются тремя последовательными
стадиями одного обязательного pipeline. `Create` и `Members` образуют
декларативный mapping, а `MapManually` является полностью отдельной
альтернативой ему.

## 2. Термины

В документе используются следующие термины:

- `MapNew` — публичная операция `Map(source)`;
- `MapExisting` — публичная операция `Map(source, destination)`;
- `previous` — фактический экземпляр destination, переданный в `MapExisting`;
  в declarative pipeline он формируется после null-предобработки, а в manual
  mapping — непосредственно из исходного аргумента;
- `result` — объект или значение, которое выбрано для применения member rules
  и в итоге возвращается из `Map`;
- `structured creation plan` — сгенерированное описание вызова поддерживаемого
  destination-конструктора либо выбора factory/previous, а не готовый
  `TDestination`; direct `Create` возвращает готовый destination без такого
  промежуточного plan;
- `member plan` — сгенерированное описание body-member mappings, а не готовый
  `TDestination`.

`previous` и `result` намеренно различаются. `Create` может выбрать previous,
создать replacement, получить объект из factory или cache. Поэтому identity
`result` не обязана совпадать с identity переданного destination.

Названия `MapNew` и `MapExisting` описывают форму публичного вызова, а не
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
контракт и добавляет `MappingContext`:

```csharp
public interface ITypeMapper<in TSource, TDestination>
{
    TDestination Map(
        TSource? source,
        MappingContext context);

    TDestination Map(
        TSource? source,
        TDestination? destination,
        MappingContext context);
}
```

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
`MapManually` и авторитетный `null` из пользовательского creation-кода могут
фактически вернуть `null` даже при non-nullable `TDestination`. Это осознанный
прагматичный контракт: обычный mapping не заставляет пользователя подавлять
предупреждение после каждого вызова, а ответственность за согласование
runtime policy с выбранной nullability остаётся у конфигурации и вызывающего
кода.

Обе операции остаются в generated contract для любой зарегистрированной пары.
Эффективный `MappingMode` определяет, какая из них поддерживается; вызов
отключённой операции по-прежнему завершается `NotSupportedException`.

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
    .Create(source => new(
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
    .MapManually((source, previous, context) =>
        MapCore(source, previous, context));
```

Для одной canonical mapping-пары разрешён либо декларативный набор
`Create` / `Members`, либо один `MapManually`. Смешивать эти модели нельзя.

## 5. `Previous<TDestination>`

Для представления возможного previous используется отдельная value-type
обёртка, аналогичная `Nullable<T>`:

```csharp
public readonly struct Previous<T>
    where T : notnull
{
    public bool HasValue { get; }

    public T Value { get; }

    public bool TryGetValue(
        [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out T value);
}
```

`T` всегда является destination без корневой nullability:

| Destination mapping-пары | Generated previous |
|---|---|
| `Customer` | `Previous<Customer>` |
| `Customer?` | `Previous<Customer>` |
| `Point` | `Previous<Point>` |
| `Point?` | `Previous<Point>` |
| `Envelope<string?>?` | `Previous<Envelope<string?>>` |

Удаляется только корневая nullability destination; nullability вложенных
generic arguments сохраняется. Далее в концептуальных сигнатурах документа
запись `Previous<TDestination>` всегда означает именно такой
root-normalized тип.

Семантически:

- `HasValue == false` означает отсутствие пригодного previous;
- `Value` возвращает гарантированно non-null previous, если он существует;
- обращение к `Value` при `HasValue == false` ошибочно так же, как у
  `Nullable<T>.Value`;
- `TryGetValue` при `true` записывает non-null значение, а при `false` может
  записать `default`;
- сама обёртка не бывает `null`.

`Previous<T>` ни в declarative, ни в manual mapping не хранит `Some(null)`.
В declarative pipeline явный `null` destination сначала обрабатывается
`NullDestinationHandling`, и только затем формируется
`Previous<TDestination>`.

Та же обёртка передаётся в `MapManually`, но там она формируется из исходного
destination без null-предобработки. Поэтому explicit `null` представлен как
`Previous.None`, а отличие `Map(source, null)` от `Map(source)` сообщает
`MappingContext.Operation`.

## 6. `Create`

### 6.1. Ответственность

`Create` отвечает только за:

- выбор способа получить `result`;
- выбор destination-конструктора;
- mapping constructor parameters;
- convention construction;
- factory construction;
- прямое получение готового destination, когда constructor-plan отсутствует;
- выбор existing destination как `result`, когда previous существует.

Body-members в `Create` не настраиваются. В частности, свойства и поля,
включая `init` и `required`, принадлежат только `Members`.

### 6.2. Выбор generated surface

После применения общей destination-type policy форма `Create` определяется
только наличием constructor surface, который Morphant действительно умеет
вызвать:

| Constructor capability | Generated `Create` | Что возвращает lambda |
|---|---|---|
| Есть хотя бы один поддерживаемый constructor | Structured | `DestinationCreation` |
| Поддерживаемого constructor surface нет | Direct | Настоящий `TDestination` |

Structured `Create` нужен для выбора destination-конструктора и настройки его
параметров. Если такого конструктора нет, моделировать промежуточный
constructor-plan бессмысленно: direct `Create` сразу получает готовый instance.

Наличие body-members не влияет на выбор формы `Create`. Оно независимо
определяет наличие `Members`. Поэтому interface или factory-only class с
writable members получает direct `Create` вместе с `Members`, а scalar без
members — только direct `Create`.

Одна mapping-пара никогда не получает обе формы. Пользовательский mode для
переключения между structured и direct surface не вводится.

### 6.3. Две перегрузки и общая семантика arity

Для structured surface генерируются:

```csharp
Create(
    Func<TSource, DestinationCreation> create);

Create(
    Func<TSource, Previous<TDestination>, DestinationCreation> create);
```

`DestinationCreation` — сгенерированный creation-plan для конкретного
destination. Это не настоящий `TDestination`.

Для direct surface генерируются:

```csharp
Create(
    Func<TSource, TDestination> create);

Create(
    Func<TSource, Previous<TDestination>, TDestination> create);
```

Обе формы используют один закон выбора result:

| Настройка | Previous отсутствует | Previous существует |
|---|---|---|
| `Create(source)` | Lambda определяет result | Lambda не вызывается; previous становится result |
| `Create(source, previous)` | Lambda вызывается с `Previous.None` | Lambda вызывается с `Previous.Some` |

Source-only structured `Create` концептуально эквивалентен:

```csharp
Create((source, previous) =>
    previous.HasValue
        ? previous
        : CreateFromSource(source));
```

Source-only direct `Create` имеет ту же семантику, но возвращает настоящий
destination:

```csharp
Create((source, previous) =>
    previous.HasValue
        ? previous.Value
        : CreateFromSource(source));
```

Эта небольшая синтаксическая асимметрия намеренна. Structured lambda выбирает
ветку creation-plan, поэтому `Previous<TDestination>` неявно преобразуется в
`DestinationCreation`. Direct lambda уже обязана вернуть `TDestination`, поэтому
после проверки `HasValue` явно извлекается `previous.Value`. Отдельный
`DirectCreation<T>`, implicit conversion `Previous<T> -> T`, `AsResult()` и
`UsePrevious()` не вводятся.

Настоящий return type direct source-only перегрузки также сохраняет естественные
method groups:

```csharp
builder.Map<string, Guid>()
    .Create(Guid.Parse);
```

Для одной пары можно настроить только один `Create`, независимо от выбранной
перегрузки. Повторный вызов является diagnostic; две перегрузки не образуют
отдельные `MapNew`- и `MapExisting`-правила.

### 6.4. Почему две перегрузки нужны только здесь

У `Create` arity действительно меняет политику:

```csharp
.Create(source => new(source.Id))
```

не заменяет existing destination, а:

```csharp
.Create((source, _) => new(source.Id))
```

создаёт result и при `MapNew`, и при `MapExisting`.

Это намеренное различие, а не сокращённая запись одной и той же операции.

Тот же закон действует для direct surface:

```csharp
.Create(Parse)
```

сохраняет existing destination, а:

```csharp
.Create((source, _) => Parse(source))
```

получает replacement и для `MapNew`, и для `MapExisting`.

### 6.5. Generated structured creation-plan

Creation-plan зеркалит поддерживаемые destination-конструкторы и использует
`ConstructorMember<T>` для их параметров. Концептуально:

```csharp
internal sealed class DestinationCreation
{
    public DestinationCreation(
        ConstructorMember<Guid> id,
        ConstructorMember<Guid> tenantId);

    public DestinationCreation(
        ByConventionMarker marker,
        DestinationConstructorMembers? members = null);

    public DestinationCreation(
        IByFactoryMarker<Destination> marker);

    public static implicit operator DestinationCreation(
        Previous<Destination> previous);
}
```

Это сохраняет полноценный DSL для constructor parameters:

```csharp
.Create(source => new(
    source.Id,
    Auto(),
    Map(source.Address)))
```

Поддерживаемые формы creation-plan:

- явный destination-конструктор;
- `ByConvention()`;
- `ByConvention()` с явными constructor-member rules;
- factory через `new(ByFactory(...))`;
- existing previous как result в previous-aware перегрузке.

Произвольный готовый `TDestination` не преобразуется в structured
creation-plan. Готовый или cached instance выражается явно как factory-ветка:

```csharp
.Create(source => new(ByFactory(() => cache.Get(source.Id))))
```

Форма `new(ByFactory(...))` обязательна: marker передаётся generated
constructor-у creation-plan, а implicit conversion от marker-interface не
генерируется.

Constructor-member rules сохраняют текущую модель:

| Запись | Семантика |
|---|---|
| Явное выражение | Вычислить и передать значение параметра |
| `Auto()` | Обязательно получить параметр по convention |
| `Ignore()` | Опустить параметр, когда это допустимо для optional / `params` |
| `Map(source)` / `Map<TDestination>(source)` | Выполнить nested `MapNew` и передать его результат |
| `Map(source, destination)` / `Map<TDestination>(source, destination)` | Выполнить nested `MapExisting` и передать его результат |

`Create` не гарантирует новую identity. В частности, `ByFactory()` может
вернуть cached instance. Название означает получение базового `result`, а не
обязательное выделение нового объекта.

### 6.6. Поведение по умолчанию

Для structured surface, если previous отсутствует и `Create` не настроен,
Morphant выполняет обычное convention construction с эффективным
`ConstructorSelection`. Текущим default остаётся `Unambiguous`.

У direct surface нет default creation: отсутствие поддерживаемого constructor
как раз означает, что Morphant не может самостоятельно получить instance. Если
доступная операция может прийти в no-previous ветку, direct `Create` должен быть
настроен. Отсутствие настройки является ошибочной конфигурацией, а не поводом
для скрытого fallback на `MapManually` или runtime conversion.

Если previous существует и configured `Create` — source-only, lambda не
вычисляется вообще. Constructor arguments, factory и любые используемые только
в этой lambda выражения также не вычисляются.

Если previous-aware structured `Create` выбирает previous, он становится
`result`. Constructor, convention или factory дают replacement-result. В
direct surface lambda возвращает либо `previous.Value`, либо готовый
replacement непосредственно.

Никакого скрытого fallback между различными ветками `Create` нет.

### 6.7. Порядок вычислений creation-plan

В runtime выполняется только выбранный путь `Create`. Невыбранная ветка,
source-only lambda при существующем previous и выражения, нужные только
неприменимой operation, не вычисляются.

В structured plan явные constructor arguments вычисляются ровно один раз
слева направо в порядке записи, включая переставленные named arguments. Затем
вызывается выбранный destination-constructor. Для `ByConvention()` сначала в
пользовательском порядке вычисляются явно записанные constructor-member rules,
после них — оставшиеся automatic arguments в порядке параметров выбранного
конструктора. `Ignore()` не вычисляет значение, а `Auto()` и `Map(...)`
занимают позицию соответствующего rule.

Plan-shaping locals, условия и selector-ы выполняются в своей позиции и только
на выбранном execution path. Если значение уже вычислено в declarative local,
оно переиспользуется, а не вычисляется повторно ради constructor или member
rule.

Direct `Create` и тело `ByFactory` являются обычным синхронным C#-кодом, а не
разбираемым statement-by-statement creation DSL. Expression-body переносится
как выражение, block-body — целиком; обычный C# определяет внутренний порядок,
ветвление, mutation, циклы, exceptions и local functions. Получение настоящего
result выполняется ровно один раз. После него действует общая member-фаза,
если result не равен `null`.

## 7. `Members`

### 7.1. Единственная перегрузка

Концептуальная сигнатура:

```csharp
Members(
    Func<TSource, Previous<TDestination>, DestinationMembers> members);
```

Source-only перегрузки нет. Если previous не нужен, пользователь пишет `_`:

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

`Members` является единственным местом настройки всех поддерживаемых
body-members destination:

- properties с обычным `set`;
- `init`-only properties;
- `required` properties и fields;
- writable fields;
- поддерживаемые унаследованные body-members.

Constructor parameters не входят в `Members`, потому что они не являются
body-members.

Концептуальный generated plan:

```csharp
internal sealed class DestinationMembers
{
    public Member<string> Name { get; set; }

    public Member<string> Code { get; set; }

    public Member<int> Revision { get; set; }
}
```

Собственные `set`-сеттеры служебного типа нужны только для составления плана и
не связаны с `set`/`init`-семантикой destination. Текущий declarative DSL
по-прежнему требует вернуть полностью сформированный plan и не поддерживает
последующую mutation local plan-а. Более широкая форма setter-а сохраняется как
совместимая точка расширения: если после v0 понадобится императивная сборка или
поэтапное дополнение plan-а, generated surface не придётся ломать.

### 7.3. Применение плана

`Members` всегда применяется к выбранному `result`, а не к `previous`.
Параметр `previous` внутри lambda всегда означает исходный destination-вход
после null-предобработки, даже если `Create` выбрал replacement.

Пример:

```csharp
builder.Map<CustomerDto, Customer>()
    .Create((source, previous) =>
        previous.HasValue &&
        previous.Value.TenantId == source.TenantId &&
        !previous.Value.IsFrozen
            ? previous
            : new(source.Id, source.TenantId))
    .Members((source, previous) => new()
    {
        Name = source.Name,

        Revision = previous.HasValue
            ? previous.Value.Revision + 1
            : 1
    });
```

Если `Create` вернул replacement, `Name` и `Revision` применяются к
replacement, но `previous.Value.Revision` читается из исходного объекта.

Generator самостоятельно раскладывает единый member plan по допустимым фазам:

- для result, создаваемого structured constructor/convention plan, `init` и
  creation-time `required` попадают в object initializer;
- обычные setters и writable fields применяются к выбранному result;
- если result является previous, его `init`-only members сохраняются;
- выражение explicit `init`-rule не вычисляется в ветке, где применить его
  невозможно;
- `required`-member с обычным доступным `set` можно обновлять у previous;
- replacement, созданный constructor/convention plan, получает те же
  creation-time member rules, что и обычный `MapNew`.

Если `ByFactory()` или direct `Create` возвращает уже созданный объект,
применить к нему `init`-only rule невозможно. Явная попытка совместить такую
creation-ветку с соответствующим `Members` rule должна давать diagnostic.
Factory или direct lambda должна инициализировать такой member сама либо
mapping должен быть ручным.

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
| `Map(source)` / `Map<TDestination>(source)` | Выполнить nested `MapNew` и присвоить результат |
| `Map(source, destination)` / `Map<TDestination>(source, destination)` | Выполнить nested `MapExisting` и присвоить результат |
| Member не указан | Применить эффективный `MemberMatching` |

При `MemberMatching.Auto` явные rules дополняют или переопределяют convention
rules. При `MemberMatching.Explicit` неуказанные members не маппятся.

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

Nested mapping всегда задаётся одной из четырёх явных форм:

| Форма | Nested destination | Операция |
|---|---|---|
| `Map(source)` | Выводится из целевого member или constructor parameter | `MapNew` |
| `Map<TDestination>(source)` | Явно заданный `TDestination` | `MapNew` |
| `Map(source, destination)` | Выводится из целевого member или constructor parameter | `MapExisting` |
| `Map<TDestination>(source, destination)` | Явно заданный `TDestination` | `MapExisting` |

Форм `Map()` и `Map<TDestination>()` без аргументов нет: source и, когда нужен
existing-вызов, child destination выбирает пользователь. Это правило одинаково
для body-members и constructor parameters; автоматической связи имени параметра
конструктора с member-ом внешнего previous не существует.

Статический тип nested source определяется первым аргументом. Runtime-тип не
меняет выбранную пару, а `Map(null)` без типизирующего cast не определяет
source-тип и потому недопустим. В generic-форме возвращаемый `TDestination`
должен warning-free неявно преобразовываться в тип целевого member или
constructor parameter; это позволяет, например, явно получить concrete child
для interface-typed места.

Явный `Map(...)` требует nested mapping даже тогда, когда source можно напрямую
присвоить целевому месту. One-argument форма всегда означает `MapNew`, а
two-argument — `MapExisting`, независимо от outer operation. Это сохраняется и
для explicit `null` во втором аргументе: null handling выполняет сама вложенная
mapping-пара, без внешней подстановки, fallback или смены операции.

Child previous также передаётся только явно. Если нужная операция зависит от
наличия outer previous, пользователь выражает обе ветки непосредственно:

```csharp
.Members((source, previous) => new()
{
    Address = previous.HasValue
        ? Map(source.Address, previous.Value.Address)
        : Map(source.Address)
});
```

В existing-ветке здесь читается именно исходный outer `previous`, а не
replacement, выбранный `Create`. Если `previous.Value.Address` равен `null`,
вызывается nested `MapExisting` с explicit `null`. Возвращённый nested result
авторитетен и присваивается выбранному outer result; nested `MapExisting` может
как сохранить или изменить старый child, так и вернуть replacement.

Аргументы каждого `Map(...)` вычисляются ровно один раз слева направо в порядке
записи, включая переставленные named arguments. Scoped `IMapper` создаёт для
вложенного вызова новый immutable call frame с выбранной operation и сохраняет
общий mapping scope.

### 7.5. Evaluation phases и shallow snapshot

Порядок member-фазы зависит не от публичной operation, а от происхождения
выбранного result:

| Происхождение result | Порядок member-фазы |
|---|---|
| Новый structured constructor/convention result | Обычный C#-порядок object initializer: constructor, затем очередное explicit value и соответствующий assignment в пользовательском порядке |
| Previous, `ByFactory` result или direct `Create` result | Сначала shallow snapshot всех применимых explicit values, затем generated outer assignments |

После explicit-фазы в обоих случаях выполняются неуказанные conventions в
обычном порядке destination-members. `Ignore()` не вычисляет значение и не
создаёт assignment. Неприменимый rule, включая `init` для уже созданного
result, не вычисляется.

Snapshot для уже созданного result охватывает обычные explicit expressions,
явный `Auto()` и nested `Map(...)`. Значения вычисляются ровно один раз в
пользовательском порядке и сохраняются в типизированные collision-safe locals.
Только после вычисления последнего применимого explicit value Morphant начинает
outer assignments, также в пользовательском порядке.

Например:

```csharp
.Members((_, previous) => new()
{
    Left = previous.Value.Right,
    Right = previous.Value.Left
});
```

для previous-result концептуально становится:

```csharp
var left = previous.Value.Right;
var right = previous.Value.Left;

result.Left = left;
result.Right = right;
```

Factory и direct result намеренно используют то же правило. Пользовательский
код может вернуть новый instance, но также source, previous или cached object;
generator не пытается угадывать aliasing по телу lambda.

Неуказанные conventions не входят в snapshot, чтобы каждый mapping не
раздувался locals для неявных rules. Если конкретное convention-value тоже
должно быть прочитано заранее, пользователь делает его явным через `Auto()`.

Snapshot является поверхностным. Он откладывает только присваивания во внешний
result, генерируемые Morphant. Вызов nested `Map(...)` выполняется в позиции
rule, и его mutation object graph либо другие пользовательские side effects
видны последующим выражениям. Generator не переносит более позднее чтение выше
такого вызова, потому что это изменило бы порядок exceptions и side effects и
потребовало бы знания реализации nested pair из другой сборки.

Нужную точку глубокого snapshot пользователь задаёт обычным declarative local:

```csharp
.Members((source, previous) =>
{
    var oldSummary = previous.Value.Child.Summary;

    return new()
    {
        Child = Map(source.Child, previous.Value.Child),
        Summary = oldSummary
    };
});
```

Для нового constructor/convention result universal snapshot не применяется.
Иначе значения `init`-members пришлось бы вычислять до constructor-а, меняя
естественный C#-порядок. Поэтому после вызова constructor каждое explicit
member value вычисляется и сразу попадает в object initializer; setter или
иные пользовательские side effects могут быть видны следующему expression.
Явный local остаётся способом зафиксировать более раннее значение.

### 7.6. Declarative control flow и captures

Structured `Create` и `Members` являются конечным анализируемым DSL. В них
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
Все выполняемые выражения вычисляются ровно один раз, а их observable side
effects сохраняют пользовательский порядок. Declarative locals задают явную
точку вычисления; последующая mutation такого local не поддерживается.

Во внешнем structured `Create` или `Members` block не поддерживаются:

- locals без initializer-а, последующие/deconstruction/compound assignments и
  `++` / `--`;
- loops, `break` / `continue` и standalone statements только ради side effect;
- local functions, объявленные во внешнем declarative block;
- `try` / `catch` / `finally`, `using`, `lock`, labels / `goto`;
- `ref` / `using` locals, `unsafe` / `fixed`, `async` / `await` и `yield`.

Сложное вычисление выносится в обычный instance/static member mapper-а, сложное
получение result — в direct `Create` либо `ByFactory`, а полностью специальный
алгоритм — в `MapManually`. Direct `Create`, factory body и `MapManually`
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

Generated properties `DestinationMembers` имеют обычный `set`. Это намеренный
запас для будущего расширения DSL, но само по себе не добавляет в v0
императивную сборку либо последующую mutation plan-а: текущая declarative
grammar и её evaluation semantics остаются прежними.

### 7.7. Почему `Skip()` не нужен

Полный статический отказ от implicit member mapping уже выражается настройкой:

```csharp
builder.Map<Source, Destination>()
    .MemberMatching(MemberMatching.Explicit);
```

Если `Members` отсутствует, ни один body-member не маппится. При существующем
previous и отсутствии previous-aware `Create` он останется result без
изменений.

Для динамического алгоритма, который в runtime иногда должен выполнить полный
no-op, используется `MapManually`. Отдельный `Skip()` в v0 не добавляется;
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
    MapNew = 0,
    MapExisting
}

public readonly struct MappingContext
{
    public MappingOperation Operation { get; }

    public IMapper Mapper { get; }
}
```

`MappingOperation` описывает ровно одну выполняемую операцию и поэтому не
переиспользует flags-enum `MappingMode`. `Operation` доступен пользователю
только для чтения; его значение устанавливает mapper.

`MappingContext` является immutable call frame текущего outer или nested
вызова. Morphant создаёт новый frame для каждого `Map`, передаёт его по
значению и не меняет после создания. Собственной reference identity у frame
нет; `default(MappingContext)` не является допустимым рабочим context.

Общее состояние всей mapping chain хранится отдельно во внутреннем
reference-type `MappingScope`:

| Call frame (`MappingContext`) | Общий `MappingScope` |
|---|---|
| Текущая `Operation` | Scoped mapper |
| Immutable и передаётся по значению | Будущий reference cache |
| Новый для каждого nested `Map` | Будущие per-call данные |
| Описывает ровно текущий вызов | Общий до завершения root `Map` |

Публичный root mapper и `context.Mapper` реализуют один контракт `IMapper`, но
являются разными экземплярами с разным lifetime. Root mapper начинает новую
mapping chain и создаёт новый scope для каждого публичного вызова.
`context.Mapper` является scoped-экземпляром, привязанным к уже существующему
scope. Отдельный `IContextualMapper`, полностью повторяющий `IMapper`, не
вводится.

Оба экземпляра видят один application-wide registry mapping-ов и используют
`IServiceProvider` текущего DI-scope. `MappingScope` сохраняет состояние одной
mapping chain, но никогда не ограничивает набор доступных пар конкретным
`TypeMapper`, mapper-графом или assembly.

Source-only перегрузка scoped mapper создаёт nested frame с
`MappingOperation.MapNew`, а two-parameter перегрузка — с
`MappingOperation.MapExisting`, даже когда переданный destination равен
`null`. Оба frame разделяют тот же scope, но `Operation` outer frame при этом
никогда не мутируется.

`MapManually` находится на обычном pair-builder и имеет одну универсальную
перегрузку:

```csharp
MapManually(
    Func<
        TSource?,
        Previous<TDestination>,
        MappingContext,
        TDestination> mapping);
```

`TSource?` здесь означает исходное runtime-значение source, включая `null`,
когда конкретный source type его допускает. Для reference type параметр
nullable, для nullable value type сохраняется `Nullable<T>`, а non-nullable
value type не поднимается искусственно. В отличие от declarative lambda,
manual lambda всегда видит значение до `NullSourceHandling`.

`Previous<TDestination>` использует non-null underlying destination по правилу
раздела 5. Поэтому explicit `null` никогда не превращается в `Some(null)` даже
в raw manual mapping: он представлен `Previous.None`, а исходную операцию
дополнительно сообщает `MappingContext.Operation`.

Source-only перегрузки нет. Если сведения о вызове и mapping context не нужны,
пользователь намеренно игнорирует оба дополнительных параметра:

```csharp
.MapManually((source, _, _) =>
    new Destination(source!.Id, source.Name));
```

`Previous<TDestination>` и `MappingContext` передаются раздельно, поскольку
отвечают на разные вопросы. `Previous` описывает наличие фактического
destination instance, а `MappingContext` — текущий call frame, включая его
операцию и scoped mapper для ручных nested mappings.
`MappingContext` является последним параметром, как и в generated
`ITypeMapper.Map(...)` contract.

### 8.2. Почему одного `Previous<T>` недостаточно

В manual mapping не выполняются `NullSourceHandling` и
`NullDestinationHandling`. Поэтому пользователь должен различать:

- `Map(source)`;
- `Map(source, null)`;
- `Map(source, destination)`.

Два первых вызова не имеют экземпляра destination, но являются разными
операциями. Форма вызова хранится в `MappingContext.Operation`, а наличие
экземпляра — независимо от неё в `Previous<TDestination>`.

Точные состояния:

| Вызов | `context.Operation` | `previous` |
|---|---|---|
| `Map(source)` | `MapNew` | `None` |
| `Map(source, null)` | `MapExisting` | `None` |
| `Map(source, destination)` | `MapExisting` | `Some(destination)` |

`Operation` и `Previous` хранят два независимых факта: какая публичная
операция вызвана и существует ли фактический destination instance. Поэтому
для различения explicit `null` не требуется отдельная generic call-обёртка.

### 8.3. Семантика

```csharp
builder.Map<Source, Destination>()
    .MapManually((source, previous, context) =>
    {
        if (source is null)
            return HandleNullSource(previous, context);

        if (context.Operation == MappingOperation.MapNew)
            return Create(source);

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
        new MappingContext(MappingOperation.MapNew, scope.Mapper));
}
finally
{
    scope.Complete();
}

// context.Mapper
scope.ThrowIfCompleted();

return scope.Dispatch(
    source,
    new MappingContext(MappingOperation.MapExisting, this),
    destination);
```

Конкретные constructors и внутренние методы здесь показаны только как
псевдокод; они не являются дополнительным public API.

`Operation` всегда описывает текущий вызов в mapping chain, а не корневую
операцию. Внутри nested `MapManually` виден новый frame с собственной
операцией, а продолжившийся после него outer manual mapping по-прежнему имеет
свой неизменившийся frame. Ничего восстанавливать после вложенного вызова не
нужно.

Exception из nested mapping не меняет outer frame. Его можно поймать и
продолжить outer mapping; recursion и последовательная reentrancy используют
новые frame и остаются безопасными относительно `Operation`.

`MapManually` полностью определяет результат во всех включённых
`MappingMode`-операциях. Внутри разрешён обычный C#:

- expression- и block-lambdas;
- условия, switch, циклы и несколько `return`;
- mutation;
- constructors и factories;
- record `with`;
- вызовы других методов и mapper-ов.

При выполнении `MapManually`:

- `NullSourceHandling` не применяется;
- `NullDestinationHandling` не применяется;
- convention construction не применяется;
- convention member mapping не применяется;
- `Create` и `Members` не выполняются;
- `Auto()`, `Ignore()`, `Map(...)`, `ByConvention()` и `ByFactory()` не являются
  DSL-маркерами и недоступны;
- ручные nested mappings доступны через `context.Mapper.Map(...)`;
- scoped mapper автоматически создаёт для вложенного вызова новый
  `MappingContext` и сохраняет общий scope;
- lambda возвращает настоящий `TDestination`;
- `MappingMode` по-прежнему определяет, какую публичную операцию можно вызвать.

Для одной пары разрешён ровно один `MapManually`. Его смешивание с `Create`,
`Members` или declarative constructor/member-specific configuration является
ошибкой конфигурации и должно диагностироваться. Унаследованные общие settings,
не имеющие эффекта в manual mapping, не запускают скрытый declarative pipeline.

### 8.4. Использование context за пределами `MapManually`

`MappingContext` участвует не только в manual mapping. Declarative pipeline
использует его внутренне для каждого explicit nested `Map(...)`: текущий вызов
получает собственный frame, а все frame mapping chain разделяют один scope.

Scope завершается в `finally` вместе с root `Map`. Сохранять
`context.Mapper` и вызывать его после завершения root mapping нельзя;
scoped mapper обязан проверить lifetime и немедленно отклонить такой вызов.
Точный тип и сообщение этой ошибки относятся к общему аудиту observable
failures.

Обычный root `IMapper` можно использовать параллельно: каждый root-вызов
получает независимый scope. Последовательные nested-вызовы, recursion и
reentrancy внутри одного scope поддерживаются. Параллельное использование
одного scoped mapper внутри одной mapping chain не поддерживается и не
получает thread-safety guarantee; это оставляет корректную основу для будущего
mutable reference cache без неявной синхронизации.

Однако пользовательским параметром `MappingContext` пока остаётся только в
`MapManually`. Добавлять его в `Create` или `Members` не нужно:

- declarative lambdas намеренно получают уже нормализованные source и
  previous после null handling;
- доступ к `context.Operation` позволил бы снова различать `Map(source)` и
  нормализованный `Map(source, null)`, обходя эту модель;
- declarative nested mapping уже выражается явным `Map(...)` marker.

Если в будущем в context появятся конкретные пользовательские данные или
новые extension points, необходимость доступа к ним из declarative DSL должна
быть согласована отдельно. Гипотетическая польза не является основанием
усложнять текущие сигнатуры `Create` и `Members`.

## 9. Null handling

### 9.1. Declarative mapping

Для `Create` и `Members` null handling выполняется до mapping DSL.

Порядок остаётся таким:

1. Проверить source и применить эффективный `NullSourceHandling`.
2. Для `MapExisting` проверить destination и применить эффективный
   `NullDestinationHandling`.
3. Сформировать нормализованный `Previous<TDestination>`.
4. Выбрать `result` через configured/default `Create` policy.
5. Если пользовательский direct/factory-код вернул `null`, немедленно вернуть
   его как авторитетный result.
6. Иначе применить `Members` и effective member conventions.

Когда declarative lambda начинает выполняться, source уже прошёл
`NullSourceHandling`. Она получает non-null underlying source: reference type
имеет non-null annotation, а `Nullable<T>` разворачивается в `T`. Поэтому
обычному declarative коду не нужны повторные null-check или `!`.

Для reference destination целевая семантика `NullDestinationHandling`:

| Настройка | Поведение |
|---|---|
| `Throw` | Бросить исключение до `Create` и `Members` |
| `TreatAsMissing` | Считать explicit `null` отсутствующим previous и перейти в no-previous ветку |

`TreatAsMissing` здесь и далее — только рабочее имя, а не принятое название.
Семантика текущего `CreateNew` уже уточнена: configured `Create` может
использовать factory или cache и не обязан возвращать новый instance. Выбор
окончательного имени вынесен в отдельный naming-этап.

После `TreatAsMissing` следующие вызовы намеренно неразличимы внутри
declarative DSL:

```csharp
Map(source)
Map(source, null)
```

В обоих случаях `Create` / `Members` получают `Previous.None`. Именно поэтому
для `Members` достаточно `Previous<TDestination>` без доступа к
`MappingContext.Operation`.

`NullSourceHandling` сохраняет текущие варианты и precedence. В частности,
если effective policy возвращает результат или бросает исключение, ни
`Create`, ни `Members` не выполняются.

### 9.2. `null` из пользовательского creation-кода

Фактический destination могут вернуть две declarative ветки:

- direct `Create`;
- `ByFactory` внутри structured `Create`.

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
Previous-aware `Create`, вернувший `null`, тем самым намеренно заменяет
существующий destination на `null`.

Для non-nullable destination обычный C# nullability analysis по возможности
предупреждает в конфигурации. Пользователь может сознательно подавить это
предупреждение либо получить `null` из oblivious API; Morphant уважает такой
runtime-результат. Для nullable destination `null` является обычной
declarative конверсией, например `string -> Guid?`.

Constructor, convention и previous дают non-null result по своей природе и не
нуждаются в такой проверке. `null` вместо самого generated
`DestinationCreation` или `DestinationMembers` является не destination-
результатом, а недопустимым DSL-plan и должен диагностироваться как ошибка
конфигурации.

### 9.3. Manual mapping

Для `MapManually` обе null-handling настройки полностью обходятся. В lambda
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

var previous = Previous<Destination>.None;

var result = RunNoPreviousCreate(source, previous);

if (result is null)
    return null!;

ApplyMembers(source, previous, result);

return result;
```

`RunNoPreviousCreate` вызывает любую configured `Create`-перегрузку, поскольку
previous отсутствует. Если `Create` не настроен, structured surface выполняет
convention construction, а direct surface не имеет default creation и является
ошибочной конфигурацией для такой reachable ветки.

`Map(source, destination)` после null-предобработки работает так:

```csharp
ApplyNullSourceHandling(source);
var previous = ApplyNullDestinationHandling(destination);

Destination result;

if (!previous.HasValue)
{
    result = RunNoPreviousCreate(source, previous);
}
else if (previousAwareCreateConfigured)
{
    result = RunCreate(source, previous);
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

`RunCreate` никогда не подменяется другой configured lambda. Structured plan
lowering и direct lambda в итоге дают один настоящий `Destination result`. Для
пары существует не более одного `Create`.

Если `Members` не настроен, `ApplyMembers` применяет только effective
`MemberMatching` conventions. Если generated member surface отсутствует, эта
стадия не содержит применимых members.

`ApplyMembers` является одной логической стадией, но lowering зависит от
creation branch:

1. Для structured constructor/convention branch вычисляются применимые
   plan-shaping условия и locals выбранного пути, затем constructor arguments,
   после чего вызывается constructor. Explicit member values и assignments
   выполняются в порядке rules как обычный object initializer; оставшиеся
   conventions следуют после них.
2. Для previous, factory и direct branch сначала полностью вычисляется
   выбранный declarative member plan. Все применимые explicit values
   сохраняются в locals без outer mutation, затем выполняются explicit
   assignments и только после них — оставшиеся conventions.
3. `null` factory/direct result завершает mapping до построения member plan.
4. Rule или ветка, неприменимые к текущей operation/result capability, не
   вычисляются. Это включает construction-only expressions в existing branch
   и `init`-rules для уже созданного result.

Generator вправе сворачивать одинаковые ветки и убирать временные переменные
только там, где доказана полная observable equivalence: exactly-once,
пользовательский порядок, набор выполняемых side effects и момент generated
outer mutation не меняются.

## 11. Pair eligibility, capabilities и generated API

### 11.1. Допустимость mapping-пары

Pair eligibility отделяется от конкретной declarative capability. Root mapping
в v0 является синхронным преобразованием одного уже материализованного
значения со статически известной верхнеуровневой формой в другое такое
значение. Поэтому mapping-пара допустима, если оба её корневых типа:

- являются допустимыми generic type arguments при минимальном C# 9 contract;
- могут быть однозначно названы из lexical context generated mapper-а;
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
destination и действуют также для direct `Create` и `MapManually`: для такой
пары v0 вообще не генерирует mapping contract. Забытая registration не
превращается в runtime lookup или скрытый manual fallback.

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
error types, anonymous/unnameable types и типы, недоступные из generated
lexical context. Это не продуктовые запреты, а формы, для которых невозможно
сформировать согласованный `ITypeMapper<TSource, TDestination>` при C# 9.

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
| Manual | Любая eligible pair | Один `MapManually` на обычном pair-builder |
| Structured creation | Есть хотя бы один поддерживаемый доступный destination constructor | `Create`, возвращающий generated `DestinationCreation` |
| Direct creation | Structured constructor surface отсутствует либо destination намеренно opaque | `Create`, возвращающий настоящий `TDestination` |
| Members | Есть хотя бы один поддерживаемый body-member | Generated `DestinationMembers` и `Members`, независимо от формы `Create` |
| Collection / projection | Не входят в v0 capability model | Никакого generated surface; рассматриваются после v0 на отдельных этапах |

Structured и direct creation взаимоисключающие: eligible pair получает ровно
одну форму `Create`. `MapManually` доступен для той же пары, но является
альтернативой всему declarative pipeline, а не fallback отдельной
неподдерживаемой ветки. Source shape сама по себе не меняет destination
surface.

Отсутствие members и constructors не убирает declarative surface. Такая пара
получает direct `Create`; `MapExisting` всё равно может вернуть previous без
изменений, а `MapNew` требует configured direct lambda. Единственным общим gate
для публичной операции остаётся эффективный `MappingMode`.

Под «есть member» понимается member, реально включаемый в generated
`DestinationMembers`, а не любой symbol типа. Static members, indexers,
get-only properties, readonly fields и другие неподдерживаемые формы не
считаются.

Под «есть constructor» понимается instance-constructor, который generator
может использовать для создания данного destination. Недоступные и
неподдерживаемые constructors не считаются. Constructor abstract-типа сам по
себе не делает тип создаваемым. Built-in, enum и отдельно определённые общей
type policy scalar-категории получают direct surface, даже если metadata типа
технически содержит public constructors: Morphant намеренно не моделирует их
как structural constructor DSL.

Direct `Create` семантически соответствует structured-ветке
`new(ByFactory(...))`: он получает уже созданный instance, после чего Morphant
применяет обычные setter-rules и member conventions. Direct result не является
окончательным результатом в смысле `MapManually`.

Например, interface не имеет constructor surface, но может независимо иметь
writable body-members:

```csharp
builder.Map<Source, IDestination>()
    .Create(source => factory.Create(source.Id))
    .Members((source, _) => new()
    {
        Name = source.Name
    });
```

Здесь direct lambda получает экземпляр, а declarative member plan продолжает
иметь самостоятельную ценность. Для `init` и creation-time `required` действуют
те же ограничения, что для structured factory-ветки: их должен заполнить код,
который вернул уже созданный instance.

Отдельного служебного creation type для scalar, opaque value object,
factory-only class, interface или abstract destination не создаётся. Их direct
surface сохраняет standard null handling и declarative member stage, поэтому
`MapManually` нужен только для действительно ручного алгоритма.

### 11.3. Settings matrix

После удаления `TemplateMode` settings наследуются прежним общим порядком
`map -> mapper root -> assembly -> library default`; `Default` продолжает
поиск менее конкретного уровня. Applicability определяется выбранной model и
capabilities пары:

| Setting | Declarative mapping | `MapManually` |
|---|---|---|
| `MappingMode` | Включает `MapNew`, `MapExisting` либо обе операции | Применяется так же и остаётся единственным effective setting manual mapping-а |
| `NullSourceHandling` | Выполняется до `Create` / `Members` | Не применяется |
| `NullDestinationHandling` | Выполняется перед previous normalization только в `MapExisting` | Не применяется |
| `MemberMatching` | Управляет неуказанными supported body-members; работает и после direct `Create` | Не применяется |
| `ConstructorSelection` | Применяется только к structured convention / `ByConvention` creation | Не применяется |
| Boxing policy | Ограничивает только automatic constructor/member conversions; explicit expressions остаются обычным C# | Не применяется |
| `UnmappedMemberValidation` | Проверяет только mapping plan, который строит Morphant; direct creation body не анализируется как набор member mappings | Не применяется |
| `NullabilityMismatchValidation` | Проверяет generator-selected constructor/member conversions; explicit C# expressions проверяет compiler | Не применяется |

Library defaults сохраняются: `MapNewAndExisting`, `ReturnNull`,
`TreatAsMissing`, `MemberMatching.Auto`, `ConstructorSelection.Unambiguous`,
разрешённый automatic boxing, `UnmappedMemberValidation.None` и
`NullabilityMismatchValidation.Error`. Здесь `TreatAsMissing` обозначает
согласованную default-семантику, но остаётся рабочим именем до naming-аудита:
policy сообщает отсутствие previous, но не обещает создание новой identity.

Inherited setting, неприменимая к конкретной pair, просто не имеет эффекта: её
уровень может обслуживать другие mappings. Явная map-level setting, которую
выбранная model принципиально обходит, является ошибкой конфигурации. Поэтому
у manual pair разрешён только `MappingMode`, а explicit null/member/constructor
policy должна диагностироваться. Для direct declarative pair явно заданный
`ConstructorSelection` также ошибочен; остальные declarative settings работают
на своих стадиях, даже если на конкретной pair не найдено ни одного кандидата
для warning или conversion.

Частичная capability никогда не включает скрытый fallback. Недоступная
operation, отсутствующий required direct `Create`, невозможный explicit rule
или setting без требуемой capability дают diagnostic; до реализации
соответствующей диагностики generated operation может быть unsupported, но не
переключается на manual, другую creation-ветку или runtime discovery.

## 12. Application registry и deterministic lookup

### 12.1. Граница runtime `IMapper`

`IMapper` является единой точкой входа во все mappings, зарегистрированные в
приложении. Он не привязан к конкретному `TypeMapper`, compilation или
mapper/profile graph. Application registry содержит descriptors пар из самого
приложения и всех подключённых composition root-ом assemblies.

Concrete `TypeMapper` остаётся единицей объявления конфигурации, генерации и
DI-активации. Он может иметь собственные dependencies, но его тип и assembly не
входят в ключ обычного lookup и не образуют скрытый mapping scope. Перенос pair
между mapper-классами сам по себе не должен менять поведение вызова.

Registry может быть immutable singleton, а root `IMapper` использует
`IServiceProvider` текущего DI-scope. Поэтому выбранный generated mapper
разрешается вместе со своими scoped/transient dependencies из того provider,
который обслуживает текущий публичный вызов. Точная registration-механика и
generated manifest являются implementation details; runtime reflection для
поиска pair не требуется.

### 12.2. Lookup law v0

Lookup key обычного `Map<TSource, TDestination>` — canonical type pair из
раздела 11. Mapper-type, assembly и порядок DI registrations в этот ключ не
входят. Registry хранит все кандидаты pair, а dispatch использует только их
количество:

| Кандидаты canonical pair | Поведение |
|---:|---|
| `0` | Mapping не найден; вызов завершается явной runtime-ошибкой |
| `1` | Кандидат разрешается через текущий `IServiceProvider` и выполняется |
| `2+` | Mapping неоднозначен; вызов завершается явной runtime-ошибкой |

Несколько registrations одной canonical pair допустимы, в том числе в разных
`TypeMapper` и assemblies. Само их наличие не является generator diagnostic и
не запрещает построение application registry. Неоднозначность наблюдаема
только при фактическом безымянном lookup этой pair.

Morphant никогда не выбирает первый или последний descriptor и не зависит от
порядка `IServiceCollection` registrations, assembly discovery либо вызовов
`AddMorphant`. `MappingMode` является capability выбранного mapping-а, а не
дополнительной частью lookup identity и не используется для скрытого выбора
между повторными pair registrations.

Точные exception-типы и сообщения для missing/ambiguous lookup определяются на
этапе observable failures. До этого отсутствие готового типа ошибки не даёт
права вводить `last registration wins` или другой fallback.

### 12.3. Root и nested dispatch

Root `IMapper.Map(...)` начинает новую mapping chain, создаёт `MappingScope` и
выполняет lookup в application registry. Explicit nested `Map(...)` и ручной
`context.Mapper.Map(...)` используют тот же registry и тот же текущий
`IServiceProvider`, но создают новый immutable call frame внутри уже
существующего scope.

Nested lookup не предпочитает mapping из `TypeMapper`, которому принадлежит
outer pair, и не ограничивается его assembly. Для одной canonical pair root и
nested вызовы применяют одинаковое правило `0 / 1 / 2+`; неоднозначность не
разрешается через outer mapper, call stack или порядок регистрации.

### 12.4. Post-v0 путь к keyed mappings

После v0 registry можно совместимо расширить явным ключом варианта. Рабочая
модель descriptor-а тогда имеет lookup identity
`(canonical pair, service key)`, где отсутствие ключа означает default-вариант.
Core-shape `IMapper.Map(...)` и generated
`ITypeMapper<TSource, TDestination>` при этом не меняются.

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
- наследует ли explicit nested `Map(...)` текущий ключ;
- разрешён ли fallback keyed lookup к default-варианту;
- как выглядит terminal fluent API для обеих mapping-операций;
- что происходит при нескольких кандидатах с одной pair и одним ключом.

Этот эскиз резервирует extension path, но не добавляет keyed semantics в v0 и
не делает текущий unkeyed lookup зависимым от будущего имени API.

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
    .Create(source => new(
        id: source.Id,
        tenantId: Auto()))
    .Members((source, _) => new()
    {
        Name = source.Name,
        Email = source.Email,
        RequiredCode = source.Code
    });
```

В `MapNew` выполняются `Create` и `Members`. В обычном `MapExisting` source-only
`Create` не выполняется, previous становится result, а применимые member rules
обновляют его. `RequiredCode` настраивается только в `Members`, независимо от
того, является ли он `set`- или `init`-member destination.

### 13.3. Условное переиспользование или replacement

```csharp
builder.Map<CustomerDto, Customer>()
    .Create((source, previous) =>
        previous.HasValue &&
        previous.Value.TenantId == source.TenantId &&
        !previous.Value.IsFrozen
            ? previous
            : new(
                source.Id,
                source.TenantId))
    .Members((source, previous) => new()
    {
        Name = source.Name,
        Revision = previous.HasValue
            ? previous.Value.Revision + 1
            : 1
    });
```

Previous-aware `Create` является полным выбором result для обоих публичных
вызовов. `Members` применяется уже к выбранному result.

### 13.4. Всегда создавать replacement

```csharp
builder.Map<Source, Destination>()
    .Create((source, _) => new(source.Id))
    .Members((source, _) => new()
    {
        Name = source.Name
    });
```

Двухпараметрический `Create` намеренно игнорирует previous и получает result в
обеих операциях.

### 13.5. Factory плюс members

```csharp
builder.Map<OrderDto, Order>()
    .Create(source =>
        new(ByFactory(() => orderFactory.Create(source.Id))))
    .Members((source, _) => new()
    {
        Number = source.Number
    });
```

Factory выполняется только в no-previous ветке source-only `Create`. При
обычном `MapExisting` используется previous и применяется `Number`.

### 13.6. Direct factory-only destination плюс members

```csharp
builder.Map<OrderDto, IOrder>()
    .Create((source, previous) =>
        previous.HasValue && CanReuse(previous.Value, source)
            ? previous.Value
            : orderFactory.Create(source.Id))
    .Members((source, _) => new()
    {
        Number = source.Number
    });
```

У interface нет constructor surface, поэтому `Create` возвращает настоящий
`IOrder`. Возврат `previous.Value` сохраняет existing instance; factory даёт
replacement. В обеих ветках применимый member plan выполняется после выбора
result.

### 13.7. Scalar и opaque value object

```csharp
builder.Map<Order, decimal>()
    .Create(source =>
        source.Items.Sum(x => x.Price * x.Count));

builder.Map<string, OrderNumber>()
    .Create(OrderNumber.Parse);

builder.Map<string, Guid?>()
    .Create(source =>
        Guid.TryParse(source, out var value)
            ? value
            : null);
```

Для destination без structural constructor surface direct `Create` сохраняет
обычный declarative pipeline без искусственного creation-plan и без перехода к
`MapManually`. В последнем примере `null` является авторитетным терминальным
результатом; member stage после него не выполняется.

### 13.8. Immutable или сложный ручной mapping

```csharp
builder.Map<SnapshotDto, Snapshot>()
    .MapManually((source, previous, _) =>
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

Никакого generated `with`-DSL для этого не требуется.

### 13.9. Immutable `MapExisting` в v0

Declarative mapping уже может условно сохранить previous либо явно построить
replacement через previous-aware `Create`:

```csharp
builder.Map<SnapshotDto, Snapshot>()
    .Create((source, previous) =>
        previous.HasValue &&
        previous.Value.Id == source.Id &&
        previous.Value.Name == source.Name
            ? previous
            : new(source.Id))
    .Members((source, _) => new()
    {
        Name = source.Name
    });
```

Если `Name` является `init`-only, он сохраняется при выборе previous и попадает
в object initializer при выборе constructor-result. Для обычного immutable
class все сохраняемые значения, которых нет в source, пользователь явно
переносит через constructor/member rules. Для record-copy с сохранением всех
остальных значений используется `MapManually` и обычный C# `with`, как в
предыдущем примере.

В v0 Morphant не выводит такое equality-условие автоматически, не добавляет
`ByCopy` и не клонирует record только из-за наличия неприменимого после создания
member-а. Source-only `Create` при существующем previous не выполняется и
replacement-path не образует. Это сохраняет явный контроль identity,
copy-constructor semantics и derived runtime type.

Статически неизбежный полный no-op является ошибочной конфигурацией: если
declarative `MapExisting` включён, existing-ветка не имеет previous-aware
`Create` и не содержит ни одного применимого post-construction assignment,
generator должен выдать diagnostic вместо молчаливого возврата previous.
Отключённый `MapExisting`, `MapManually` или explicit previous-aware `Create`
явно задают другое намерение и устраняют эту diagnostic. Наличие отдельных
immutable members в смешанном mutable/immutable destination само по себе не
делает всю operation ошибочной; неприменимые explicit rules и полнота
conventions проверяются своими diagnostics/settings.

После v0 запланирована отдельная opt-in setting условной reconstruction. Она
сможет вычислить creation-only member candidates до первой generated mutation,
сравнить их со значениями previous и выбрать replacement только при реальном
отличии; при равенстве previous identity сохраняется. Это надстройка над тем же
creation/member plan, а не новая creation-ветка и не часть
`NullAssignmentHandling`. Её public name, equality contract, reconstruction
source, factory/derived behavior и точный evaluation order будут согласованы
отдельно; до этого скрытой политики сравнения или реконструкции нет.

## 14. Ошибочные и конфликтующие конфигурации

В целевом дизайне diagnostics должны покрыть как минимум:

- повторный `Create` для одной pair, включая вызовы разных перегрузок;
- повторный `Members`;
- повторный `MapManually`;
- смешивание `MapManually` с `Create` или `Members`;
- pair-specific constructor/member settings, несовместимые с manual mapping;
- previous, factory или direct creation вместе с explicit `init`-rule, который
  невозможно применить к уже созданному result;
- статически неизбежный declarative `MapExisting` no-op: existing-ветка не
  может ни выбрать replacement, ни выполнить post-construction assignment;
- reachable no-previous branch direct surface без configured `Create`;
- `null` вместо generated `DestinationCreation` или `DestinationMembers`
  plan;
- невозможный explicit constructor/member marker;
- отсутствие кандидата при runtime lookup canonical pair;
- неоднозначный безымянный runtime lookup при двух и более кандидатах pair.

Несколько registrations одной canonical pair сами по себе не являются
ошибочной конфигурацией. Ошибка возникает только тогда, когда конкретный вызов
не может выбрать ровно одного кандидата.

Diagnostics остаются отдельной реализационной фазой, но отсутствие готового
diagnostic не должно вводить скрытый fallback на другой mapping algorithm.

## 15. Зафиксированные законы дизайна

1. `Map(source)` и `Map(source, destination)` остаются двумя публичными
   mapping-операциями; effective `MappingMode` управляет их доступностью.
2. Declarative `Create` и `Members` выполняются только после null handling.
3. Source-only `Create` выполняется только при отсутствии previous.
4. Previous-aware `Create` выполняется и с `Previous.None`, и с
   `Previous.Some`.
5. Если `Create` отсутствует, structured surface создаёт no-previous result по
   convention, direct surface не имеет default creation, а существующий previous
   в обеих формах сам становится result.
6. Для одной pair разрешён не более чем один `Create` любой перегрузки.
7. `Create` настраивает result selection и constructor parameters, но никогда
   не body-members.
8. `Members` является единственным declarative API для всех body-members,
   включая `init` и `required`.
9. У `Members` есть только одна universal previous-aware перегрузка.
10. `Members` применяется к выбранному result.
11. `previous` в `Members` всегда означает исходный нормализованный input, а не
    выбранный result.
12. Неприменимое `init`-выражение в existing-result ветке не вычисляется.
13. Member, не указанный в `Members`, следует effective `MemberMatching`.
14. `MemberMatching.Explicit` является статическим способом полностью
    отключить implicit member mapping; отдельного `Skip()` нет.
15. Nested mapping выполняется только через явные `Map(source)`,
    `Map<TDestination>(source)`, `Map(source, destination)` и
    `Map<TDestination>(source, destination)`. Форм без аргументов нет;
    conventions и `Auto()` используют только warning-free implicit
    C#-преобразование и не предполагают наличие mapping-пары. One-argument
    формы всегда вызывают nested `MapNew`, two-argument формы — nested
    `MapExisting`, включая explicit `null`, независимо от outer operation;
    child previous при необходимости передаёт сам пользователь.
16. `MapManually` является методом обычного pair-builder, а не отдельным
    builder-типом.
17. У `MapManually` есть только одна перегрузка с
    `Previous<TDestination>` и `MappingContext`.
18. `MapManually` полностью заменяет declarative pipeline и не запускает
    null-handling settings.
19. `MappingContext.Operation` сообщает текущую публичную операцию, а
    `Previous<TDestination>` независимо сообщает наличие фактического
    destination instance.
20. `MapManually` и ровно одна форма `Create` доступны для каждой поддерживаемой
    mapping-пары; `Members` генерируется независимо при наличии поддерживаемых
    body-members.
21. Наличие поддерживаемого constructor surface выбирает structured `Create`,
    его отсутствие — direct `Create`; пользовательского mode и пары с обеими
    формами нет.
22. Direct `Create` семантически соответствует уже созданному factory-result:
    после него выполняются применимые `Members` и member conventions.
23. Возвращённый `Map` result всегда авторитетен.
24. Никаких скрытых fallback между manual и declarative mapping либо между
    разными configured lambdas нет.
25. В structured surface `Previous<TDestination>` неявно преобразуется в
    `DestinationCreation`, поэтому возврат самого `previous` выбирает existing
    result. Direct surface возвращает настоящий `TDestination` и использует
    `previous.Value`; отдельный direct plan или implicit unwrap не вводится.
    Произвольный готовый `TDestination` в structured surface выражается только
    явной factory-веткой.
26. Для `ByConventionMarker` генерируется один creation-plan constructor с
    необязательным `DestinationConstructorMembers`.
27. Generated properties `DestinationMembers` имеют обычный `set` как
    совместимую точку будущего расширения; текущий declarative DSL по-прежнему
    не поддерживает mutation уже созданного member-plan.
28. `MapManually` получает текущий `MappingContext` отдельным последним
    параметром и использует его для ручных nested mappings.
29. `MappingContext` является immutable value-type frame текущего outer или
    nested вызова; scoped `IMapper` создаёт новый frame с собственной
    `Operation`, разделяя общий mapping scope без mutation и восстановления.
30. Declarative pipeline использует `MappingContext` внутренне, но `Create` и
    `Members` не получают его пользовательским lambda-параметром.
31. Public `Map` принимает nullable source/destination inputs, но возвращает
    ровно выбранный пользователем `TDestination`, а не безусловный
    `TDestination?`.
32. `Previous<T>` использует destination без корневой nullability и никогда не
    содержит `Some(null)`; `TryGetValue == true` гарантирует non-null value.
33. Declarative `Create` и `Members` получают source после null handling как
    non-null underlying type; `MapManually` получает исходное runtime-значение.
34. `null` из direct `Create` или `ByFactory` является авторитетным
    терминальным result: `Members` не выполняется, exception и fallback не
    генерируются, null-handling policies повторно не применяются.
35. `MapManually` возвращает пользовательский result без generated null guard;
    `null` вместо generated creation/member plan остаётся ошибкой DSL, а не
    destination-result.
36. Root-вызовы используют независимые scopes и могут выполняться параллельно;
    scoped mapper действует только до завершения root `Map`, а параллельные
    nested-вызовы внутри одного scope не поддерживаются.
37. Каждое выполняемое declarative expression вычисляется ровно один раз в
    пользовательском порядке; невыбранные ветки, неприменимые rules и
    operation-specific значения другого пути не вычисляются.
38. Explicit constructor arguments вычисляются в порядке записи до вызова
    constructor. Новый structured constructor/convention result сохраняет
    естественный порядок object initializer: очередное explicit member value
    сразу сопровождается assignment, затем выполняются оставшиеся conventions.
39. Previous, factory-result и direct-result всегда считаются потенциально
    aliased. Все их применимые explicit member expressions, `Auto()` и
    `Map(...)` образуют shallow snapshot до первого generated outer assignment;
    неуказанные conventions выполняются после explicit assignments.
40. Shallow snapshot не откатывает и не скрывает side effects пользовательских
    expressions или nested mappings. Они видны последующим expressions;
    более раннее чтение вложенного состояния пользователь задаёт declarative
    local-ом.
41. Structured `Create` и `Members` поддерживают только конечный анализируемый
    control flow без изменяемого состояния. Direct `Create`, factory body и
    `MapManually` являются обычными синхронными C# blocks. Ни одна форма не
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
44. Для любой другой eligible pair доступны runtime contract и `MapManually`;
    destination независимо получает ровно одну форму `Create` и, при наличии
    поддерживаемых body-members, `Members`. Значение отложенной категории
    внутри разрешённого root остаётся обычным единым C#-значением. `dynamic`
    канонически совпадает с `object`; root nullable reference annotation не
    создаёт отдельную runtime pair.
45. Manual mapping применяет только `MappingMode`. Остальные settings не
    запускают скрытый declarative pipeline; неприменимая explicit map-level
    setting является ошибкой, а inherited setting может быть безвредным no-op.
46. Public `IMapper` является application-wide фасадом: concrete `TypeMapper`,
    compilation и assembly не ограничивают видимый runtime registry.
47. Root и scoped mapper используют один registry и `IServiceProvider`
    текущего DI-scope; `MappingScope` хранит только состояние mapping chain.
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
52. В v0 `MapExisting` не создаёт replacement автоматически из-за отличия
    `init`-only, get-only или readonly state. Декларативный replacement задаётся
    previous-aware `Create`, а record-copy и иная специальная reconstruction —
    `MapManually`.
53. Source-only `Create` при существующем previous не является immutable
    replacement-path. `ByCopy`, generated `with` и implicit record cloning в
    v0 отсутствуют.
54. Declarative existing-ветка, которая статически не может ни заменить result,
    ни выполнить post-construction assignment, является configuration
    diagnostic, если `MapExisting` доступен и намерение не выражено
    previous-aware `Create` либо `MapManually`.
55. Отдельная post-v0 opt-in setting может условно реконструировать result при
    отличии хотя бы одного creation-only member candidate от previous. Эта
    identity-policy не является частью `NullAssignmentHandling`; её equality,
    reconstruction и evaluation contracts требуют отдельного решения.

## 16. Детали, которые ещё нужно закрепить перед реализацией

Этапы 1–8 и 11 согласованы. Pair eligibility, capability/settings matrix,
application-wide deterministic lookup и явная граница immutable
`MapExisting` зафиксированы. В v0 immutable replacement выполняется только
через previous-aware `Create` либо `MapManually`; автоматическая условная
reconstruction оставлена отдельной post-v0 setting. Этапы 9 и 10 — collections
и patch/merge — также сознательно отложены за границу v0; исследование
null-assignment policy сохранено отдельно и не меняет текущий default обычного
member assignment. Root type parameters и специальные tuple,
sequence/collection/buffer, delegate, expression-tree, deferred/async и
push-sequence categories также остаются post-v0. Keyed mappings оставлены
совместимым extension path. До миграции production API отдельного решения либо
реализационного планирования требуют:

- per-call data и typed пользовательский context, включая propagation в nested
  mapping;
- naming-аудит публичного API, включая рабочее `TreatAsMissing`, generated
  creation/member-plan types и возможную result-wrapper;
- нужен ли узкий result-aware `Members` для factory/direct destination,
  member rules которого зависят от runtime-state созданного instance;
- порядок миграции текущего `Template()` implementation и тестов;
- обновление `IMPLEMENTATION_PLAN.md`, XML-документации и user-facing docs;
- diagnostic IDs, сообщения и точная фаза их добавления.

До отдельного согласования эти детали не должны молча определяться удобством
текущей реализации.
