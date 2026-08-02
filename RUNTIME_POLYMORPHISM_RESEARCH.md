# Исследование runtime polymorphism и inheritance

Статус: исследование зафиксировано 2026-08-02; автоматический runtime
polymorphism полностью отложен до периода после v0. Документ не задаёт
принятый public API. Он сохраняет пользовательские сценарии, найденные
ограничения, сравнение с другими мапперами и наиболее сильное рабочее
направление, чтобы после v0 не повторять исследование с нуля.

Связанный пункт roadmap: этап 13 в
[`MAPPING_API_DESIGN_REFINEMENT_PLAN.md`](MAPPING_API_DESIGN_REFINEMENT_PLAN.md).

## 1. Что именно исследовалось

Исходный сценарий — вызов статически известной base pair с объектом
производного runtime-типа:

```csharp
Animal source = new Dog();
AnimalDto result = mapper.Map<Animal, AnimalDto>(source);
```

При зарегистрированных pair `Animal -> AnimalDto` и `Dog -> DogDto` возможны
два разных контракта:

1. выполнить ровно запрошенную `Animal -> AnimalDto`;
2. по runtime-типу `Dog` выбрать `Dog -> DogDto` и вернуть его как
   `AnimalDto`.

Вторая семантика полезна для heterogeneous graphs и коллекций, но она не
следует из обычного application-wide lookup. Статическая и производная pair —
две независимые registration identity; наличие одной не должно автоматически
менять поведение другой.

Отдельно исследовались:

- связь dispatch с наследованием mapping-конфигурации;
- выбор наиболее конкретного derived source type;
- неоднозначность между interface-ветками;
- `MapExisting`, когда runtime-типы source и previous не образуют одну derived
  pair;
- polymorphic collection elements;
- generated closed-world dispatcher без runtime reflection;
- взаимодействие с application-wide registry, keyed variants и projection.

## 2. Почему `IncludeBase()` не должен включать dispatch

В Morphant `IncludeBase()` уже имеет самостоятельный смысл: derived mapping
наследует map-level settings и member/creation rules base mapping-а. Это
compile-time композиция plan-а.

Runtime dispatch отвечает на другой вопрос: какую mapping pair выполнить для
конкретного объекта. Объединение этих обязанностей создаёт неочевидные
последствия:

- derived mapping начинает влиять на все вызовы base pair, хотя пользователь
  мог хотеть только переиспользовать правила;
- удаление `IncludeBase()` ради другой precedence внезапно отключает dispatch;
- base pair становится зависимой от registrations, которые сама явно не
  перечисляет;
- `MapExisting` получает неявный выбор другой destination pair и потенциальную
  смену identity;
- projection вынуждена повторять runtime-dispatch semantics в expression tree.

Поэтому принятая v0-граница однозначна:

- `IncludeBase()` наследует конфигурацию и не включает runtime dispatch;
- runtime-тип аргумента не меняет canonical pair, выбранную generic-вызовом;
- base и derived registrations остаются независимыми;
- нужный special-case выражается обычным `MapManually` и явным `switch`.

## 3. Что уже возможно без специального API

Полиморфный результат можно получить полностью явно:

```csharp
builder.Map<Animal, AnimalDto>()
    .MapManually((source, previous, context) => source switch
    {
        Dog dog => context.Mapper.Map<Dog, DogDto>(
            dog,
            previous.TryGetValue(out var destination) &&
            destination is DogDto dogDestination
                ? dogDestination
                : null),

        _ => MapAnimal(source, previous, context)
    });
```

Точная форма manual helper-а зависит от финального `MappingContext` API, но
capability уже существует: пользователь сам определяет type-switch, fallback,
совместимость previous и replacement semantics. Для единичного special-case
этого достаточно.

Главный массовый сценарий — polymorphic elements коллекции — всё равно
отложен вместе с общей collection support. Поэтому отдельный dispatcher не
нужен для завершения v0 и не требует заранее менять `IMapper`, `ITypeMapper`,
`MappingContext` либо application registry.

## 4. Сравнение с другими мапперами

### 4.1. AutoMapper

AutoMapper одним механизмом решает две задачи: `Include`/`IncludeBase`
наследует конфигурацию и участвует в runtime polymorphism. Также доступен
`IncludeAllDerived`, который ищет производные mappings среди всей
конфигурации; документация отдельно отмечает стоимость такого поиска.

Это удобный short path, но не лучший ориентир для Morphant: уже принятый
`IncludeBase()` имеет узкую inheritance-семантику, а registry допускает
несколько registrations одной canonical pair. Смешивание обязанностей снова
сделало бы lookup зависимым от неявного graph traversal.

Источник:
[AutoMapper — Mapping Inheritance](https://docs.automapper.io/en/stable/Mapping-inheritance.html).

### 4.2. Mapster

Mapster по умолчанию несимметрично наследует конфигурацию: source inheritance
включён, destination inheritance выключен. Отдельный `Inherits` копирует
настройки, а `Include<TDerivedSource, TDerivedDestination>` добавляет derived
runtime result к base mapping.

Этот дизайн подтверждает полезность feature, но также показывает, насколько
легко смешать implicit inheritance, explicit inheritance и runtime dispatch в
одной model. Для Morphant предпочтительнее сохранить каждую связь явной.

Источник:
[Mapster — Configuration Inheritance](https://github.com/MapsterMapper/Mapster/blob/master/docs/articles/configuration/Config-inheritance.md).

### 4.3. Mapperly

Mapperly ближе к source-generator природе Morphant: пользователь явно
перечисляет пары через `MapDerivedType`, а generator строит обычный C#
type-switch. Source и destination каждой ветки должны быть совместимы с base
method; каждый derived source type уникален.

Это подтверждает, что runtime reflection не нужна. Однако в показанном
Mapperly-примере неизвестный subtype приводит к exception, тогда как Morphant
имеет полноценную base pair и может естественно использовать её как fallback.

Источник:
[Mapperly — Derived types and interfaces](https://mapperly.riok.app/docs/configuration/derived-type-mapping/).

### 4.4. Почему projection остаётся отдельной capability

Runtime dispatch в памяти — это `is`/type-switch над фактическим CLR-объектом.
Projection должна выразить ту же развилку в expression tree и рассчитывать на
возможности конкретного query provider-а. Эти механизмы нельзя считать одной
capability только потому, что они используют одни derived registrations.

Практические проблемы уже встречались в обоих runtime-мапперах:

- в AutoMapper одна polymorphic registration могла сломать `ProjectTo` для
  другой derived query;
- в Mapster `Include` между abstract base types приводил к ошибке во время
  `CompileProjection()`.

Источники:
[AutoMapper #4395](https://github.com/LuckyPennySoftware/AutoMapper/issues/4395),
[Mapster #801](https://github.com/MapsterMapper/Mapster/issues/801).

Вывод для Morphant: этап 13 резервирует runtime capability, а её применимость
к projection отдельно определяется на этапе 15. Никакого client-side fallback
в `Project` из этого исследования не следует.

## 5. Рабочая post-v0 форма регистрации

Наиболее сильное направление — отдельная explicit-связь на base pair:

```csharp
builder.Map<Animal, AnimalDto>()
    .IncludeDerived<Dog, DogDto>()
    .IncludeDerived<Cat, CatDto>();

builder.Map<Dog, DogDto>();
builder.Map<Cat, CatDto>();
```

`IncludeDerived` — только рабочее имя. Эта связь:

- не наследует rules сама по себе;
- не создаёт derived registration;
- не ищет все assignable mappings в application registry;
- разрешает base descriptor-у рассмотреть ровно перечисленные derived pairs;
- проверяет compile-time assignability derived source/destination к base
  source/destination.

`IncludeAllDerived` и application-wide discovery не рекомендуются. Они делают
поведение base mapping зависимым от появления unrelated registration в другой
assembly и конфликтуют с детерминированным правилом `0 / 1 / 2+`.

Точный public name, terminal fluent shape и возможность задавать связь со
стороны derived pair после v0 нужно согласовать заново. Текущий дизайн только
гарантирует, что для такого расширения не требуется менять core interfaces.

## 6. Рабочий dispatch algorithm

Для вызова `Map<TBaseSource, TBaseDestination>` предлагается следующая модель:

1. Application registry сначала разрешает ровно запрошенную canonical base
   pair по обычному правилу: `0` — missing, `1` — выбран descriptor, `2+` —
   ambiguity.
2. Если source равен `null`, runtime subtype отсутствует и null handling
   выполняет base pair.
3. Выбранный base descriptor проверяет только свои explicit derived links.
4. Среди links, source type которых соответствует runtime source, выбирается
   единственный наиболее конкретный type по assignability.
5. Если подходящих links нет, выполняется base mapping.
6. Если наиболее конкретных несравнимых links несколько, dispatch завершается
   ambiguity; порядок регистрации ничего не решает.
7. Выбранная derived canonical pair снова разрешается через тот же
   application-wide registry по правилу `0 / 1 / 2+`.
8. Missing или ambiguous derived pair является observable lookup error. После
   явного совпадения link Morphant не откатывается молча к base mapping.

Такой порядок важен при нескольких base registrations: сначала выбирается
конкретный base descriptor и только затем его собственная dispatch table.
Иначе derived mapping мог бы случайно устранить или скрыть исходную ambiguity.

### 6.1. Most-specific и interface ambiguity

Для цепочки `Animal <- Dog <- ServiceDog` link `ServiceDog` конкретнее link
`Dog`. Generator может расположить сравнимые class-ветки от наиболее
конкретной к базовой.

Для несвязанных interfaces регистрационный порядок неприемлем:

```csharp
IncludeDerived<IWorkingAnimal, WorkingAnimalDto>();
IncludeDerived<IPet, PetDto>();
```

Один runtime object может реализовать оба interface. Если ни один source type
не assignable к другому, оба кандидата максимальны и mapping неоднозначен.
Generated dispatcher должен это обнаружить через обычные type checks и
сообщить ambiguity, а не зависеть от порядка arms в C# `switch`.

## 7. `MapExisting`

Здесь одного runtime source type недостаточно: выбранная derived operation
должна получить destination совместимого типа.

Рабочая матрица:

| Runtime source | Previous | Действие |
|---|---|---|
| Derived link не найден | Любой | Base `MapExisting` |
| Derived link найден | `null` | Derived `MapExisting` с `null`; её own `NullDestinationHandling` определяет дальнейшее поведение |
| Derived link найден | Совместим с derived destination | Derived `MapExisting` с тем же instance |
| Derived link найден | Несовместим с derived destination | Base `MapExisting` с исходным previous |

Последняя строка принципиальна. Morphant не должен молча вызывать derived
`MapNew`, выбрасывать переданный previous и менять identity только потому, что
runtime source оказался производным. Если пользователю нужен replacement, он
задаётся обычным authoritative result base mapping-а либо явным manual
dispatcher-ом.

Если compatible derived mapping сама возвращает replacement, этот result
остаётся авторитетным по общему закону `MapExisting`. Если её operation
отключена effective `MappingMode`, действует обычная ошибка disabled
operation; polymorphic dispatch не вводит скрытый fallback.

## 8. Collections, nested mappings и variants

После появления collection mapping каждый элемент должен использовать тот же
dispatcher выбранной element base pair. Коллекция не должна отдельно искать
«любой подходящий» mapping по runtime element type: это дублировало бы lookup
laws и создавало другое поведение root и nested вызовов.

Explicit nested `Map(...)` также начинает с requested canonical pair и её
descriptor. Derived dispatch не предпочитает outer `TypeMapper` и не
ограничивается assembly.

Будущий keyed lookup должен сначала выбрать base descriptor по `(pair, key)`,
а уже потом использовать его dispatch links. Наследуется ли key при переходе
к derived pair и может ли link переопределить key — отдельные post-v0 вопросы;
текущий unkeyed контракт их не предрешает.

## 9. Почему feature отложена

Runtime polymorphism не входит в v0 по следующим причинам:

- базовые interfaces уже совместимы с будущим dispatcher-ом;
- application-wide descriptor registry оставляет естественное место для
  generated dispatch table;
- единичный сценарий покрывается `MapManually`;
- основной массовый сценарий зависит от отложенной collection support;
- projection требует отдельной capability model;
- точные observable lookup errors всё равно согласуются на этапе 20.

Это надстройка над exact-pair registry, а не фундаментальная часть
creation/member pipeline. Откладывание не требует временного API и не создаёт
breaking change для будущего расширения.

## 10. Вопросы, оставленные до post-v0

Перед реализацией нужно повторно согласовать:

- окончательное имя и сторона registration API;
- разрешены ли только direct links либо транзитивный dispatch graph;
- unknown subtype fallback для abstract/non-creatable base destination;
- compile-time и runtime форма interface ambiguity diagnostics;
- validation missing/ambiguous derived registrations;
- взаимодействие с keyed variants;
- projection capability;
- polymorphic collection element lifecycle;
- точные exception types и сообщения;
- нужен ли пользовательский dispatcher hook сверх `MapManually`.

До этих решений v0 всегда выполняет exact requested pair и не выводит dispatch
из type hierarchy, `IncludeBase()` или набора registrations.
