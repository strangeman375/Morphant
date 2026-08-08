# Финальный аудит функциональности и удобства Morphant

Дата аудита: 2 августа 2026 года.

Статус документа: независимая продуктовая оценка целевого mapping API,
выполненная перед naming-аудитом. После завершения этапа 19 и согласования
callback result-policy revision терминология актуализирована, но оценки и
рекомендации не менялись. Нормативным источником
семантики остаётся `MAPPING_API_DESIGN.md`; этот документ фиксирует полноту
сценариев, сравнение с конкурентами, найденные риски и рекомендации.

## 1. Объём и методика аудита

Аудит рассматривает Morphant как будущий пользовательский продукт, а не только
как внутренне непротиворечивый source generator. Проверялись:

- целевой дизайн result policies, `Members` и `Convert`;
- контракты `Create` и `Update`, включая identity результата;
- constructors, `init`, `required`, nullability, factories и immutable types;
- nested mapping, generics, variants, settings, inheritance и composition;
- уже запланированные post-v0 capabilities;
- массовые пользовательские сценарии и проблемы из issues других mapper-ов;
- удобство happy path и сложных mapping-сценариев;
- совместимость текущего фундамента с будущим расширением продукта.

Согласно принятому правилу, возможность считается поддерживаемой Morphant,
если она явно оставлена на будущее. При этом в оценке различаются три степени
готовности:

1. сценарий уже имеет определённый публичный контракт и точную семантику;
2. сценарий является обязательством roadmap, но его API и законы ещё предстоит
   спроектировать;
3. сценарий не включён ни в текущий дизайн, ни в явный roadmap и поэтому
   является настоящим пробелом.

Сравнение проводилось с AutoMapper, Mapster и Mapperly. Особое внимание
уделялось не количеству функций, а предсказуемости lifecycle, precedence,
existing-target semantics и поведению в сложных графах объектов.

## 2. Итоговая оценка

| Область | Оценка |
|---|---:|
| Архитектурная зрелость | 9/10 |
| Удобство core DSL | 8/10 |
| Полнота продукта с учётом roadmap | 8/10 |
| Готовность полной спецификации к реализации | 7/10 |
| Итог | 8/10 |

Фундамент Morphant уже выглядит взрослым. Перепроектировать core API не нужно.
Главная ценность дизайна — не максимальная автоматизация, а отсутствие скрытых
решений в тех местах, где они меняют observable behavior.

В спецификации явно определены аспекты, которые зрелые mapper-ы часто оставляют
следствием реализации:

- identity переданного destination;
- reuse и replacement результата;
- поведение `init`, `required` и immutable объектов;
- null-семантика root, previous, result и members;
- constructor binding без скрытого fallback;
- precedence settings и inherited plans;
- точное число вычислений и отношение к side effects;
- граница declarative и manual mapping;
- generic interface collisions;
- отсутствие скрытого runtime polymorphism;
- observable failures и будущие diagnostics.

Разделение result-policy slot-а, `Members` и `Convert` является удачной
основой. Оно немного многословнее прежнего `Template`, но каждый API отвечает
на один вопрос, а общая модель объясняется без специальных режимов и
исключений.

## 3. Сравнение с другими mapper-ами

| Mapper | Основная сильная сторона | Слабая сторона относительно Morphant |
|---|---|---|
| AutoMapper | Максимально короткий happy path, automatic nested mapping, collections, flattening, projection и reverse mapping | Значительная часть поведения определяется runtime-конфигурацией и порядком расширений; identity и existing-target semantics менее очевидны |
| Mapster | Компактный fluent API, conditional mapping, hooks, projection и высокая гибкость | Сложное взаимодействие global transforms, inheritance и explicit rules |
| Mapperly | Ближайший концептуальный конкурент: source generation, diagnostics, rich conversions, enum mapping, projections и reference handling | Attribute-oriented configuration, отдельные new/existing methods и местами дублирование правил |
| Morphant | Самая явная lifecycle-модель, сильная семантика existing destination, compile-time DSL и единый pair-plan для new/existing | Больше явной записи для nested graphs; несколько массовых affordances пока существуют только как roadmap commitments |

AutoMapper автоматически применяет зарегистрированную вложенную pair и
поддерживает основные коллекции без отдельной регистрации collection pair.
Existing collection по умолчанию очищается. Это удобно, но содержит скрытые
решения о dispatch и lifecycle. См. [Nested Mappings][automapper-nested] и
[Lists and Arrays][automapper-collections].

Mapperly шире текущего Morphant в автоматических преобразованиях: collections,
dictionaries, tuples, spans, parsing, explicit casts, `ToString` и factory
methods. Полностью переносить такой каскад не следует: порядок преобразований
быстро становится отдельным неявным языком. Однако его first-class enum support
— стратегии по значению и имени, fallback и exhaustiveness diagnostics —
покрывает реальную пользовательскую потребность. См. [Supported
Conversions][mapperly-conversions] и [Enum Mappings][mapperly-enums].

В сравнении с конкурентами особенно оправданы следующие решения Morphant:

- авторитетный возвращаемый результат `Update` вместо `void`-update;
- явный выбор reuse или replacement;
- отсутствие тайной мутации `init` после создания объекта;
- explicit nested и polymorphic links вместо глобального поиска assignable
  mappings;
- единый `Members` plan для `Create` и `Update`;
- детерминированное precedence вместо смешения global transforms, inherited
  settings и explicit rules;
- `Convert` как честная граница для imperative algorithm, а не скрытый
  fallback generator-а.

## 4. Что показывают пользовательские issues

| Пользовательская боль | Состояние Morphant |
|---|---|
| Приходится дублировать правила для Create и Update в [Mapperly #1294][mapperly-1294] | Решено архитектурно: один `Members` обслуживает обе операции |
| Global transform перебивает explicit mapping в [Mapster #952][mapster-952] | Предотвращено явным precedence и отсутствием скрытых transforms |
| Неявное inheritance переносит неподходящие settings в [Mapster #947][mapster-947] | Решено разделением `base.Configure(builder)` и typed `IncludeBase` |
| Nullable DTO должен сохранять initializer destination в [Mapperly #2178][mapperly-2178] | Входит в будущий patch/null-assignment design, но публичный контракт ещё не закрыт |
| Нужен не только ignore-null, но и ignore-default в [Mapster #982][mapster-982] | Patch-этап следует расширить до общей presence/default policy |
| Projection приходится объявлять и конфигурировать повторно в [Mapperly #2252][mapperly-2252] | Есть риск повторить проблему: projection обещана, но её связь с основным pair-plan пока не зафиксирована |
| Нужен collection-path flattening для EF join entities в [Mapperly #2253][mapperly-2253] | Не включён явно в текущий collection/`IncludeMembers` roadmap |
| Нужен nested update существующего member-объекта в [Mapperly #1700][mapperly-1700] | Get-only complex child сейчас требует `Convert`; отдельного declarative сценария нет |
| Нужны несколько sources для required/init destination в [Mapperly #1978][mapperly-1978] | Поддерживается будущими tuple roots и multi-source mapping |
| Нужны настраиваемые правила сопоставления имён в [Mapperly #2039][mapperly-2039] | Сейчас доступны exact matching и explicit rules, но нет масштабируемого opt-in affordance |

Основные системные боли конкурентов — неоднозначная композиция, precedence и
различие new/existing configuration — в Morphant уже предотвращены самим
дизайном. Найденные недостатки лежат преимущественно в массовых удобствах и в
степени проработки будущих capabilities, а не в фундаментальной модели.

## 5. Покрытие пользовательских сценариев

| Сценарий | Статус и оценка |
|---|---|
| Mutable POCO, records, constructors, optional/`params` | Контракт закрыт хорошо |
| `init`, `required`, defaults и factories | Контракт закрыт; conditional null-preservation входит в будущий patch |
| Scalar и opaque value object | Закрыт через runtime `ConstructUsing` / `ResolveUsing` |
| Existing destination: reuse, mutation и replacement | Закрыт лучше, чем у сравниваемых mapper-ов |
| Immutable existing destination | Закрыт явным replacement или ручным `with`; скрытой mutation нет |
| Custom expressions, injected services и специальный synchronous algorithm | Закрыт |
| Nested Create/Update | Закрыт, но требует явного `Map(...)` |
| Get-only mutable child object | Только `Convert`; отдельного declarative сценария нет |
| Collections, dictionaries и getter-only collections | Обязательная post-v0 capability; точная lifecycle-матрица ещё проектируется |
| Collection reconciliation по ключу | Поддерживается после v0, без скрытого default; API не выбран |
| Explicit flattening | Закрыт |
| Convention flattening | Обязательный post-v0 `IncludeMembers` |
| Flattening через collection element path | Не включён явно |
| Patch/merge: absent, null и value | Обязательная post-v0 capability; исследование есть, API не закрыт |
| Multiple mapping variants | Будущие keys; scope, selection и propagation пока не определены |
| Runtime polymorphism | Будущие explicit derived links; направление качественное |
| Shared references и cycles | Будущий opt-in reference cache; направление качественное |
| Projection | Обещана после v0, но пока практически не спроектирована |
| Multi-source и per-call data | Будущие tuple roots и multi-source mapping |
| Open generics и runtime destination | После v0 |
| Cross-assembly configuration composition | После v0 |
| Hooks/middleware | Гарантированы после v0; shape не выбран |
| Reverse mapping | Обратная pair объявляется явно; automatic reverse mapping не требуется |
| Enum ↔ enum/string | Выражается вручную, но нет удобной first-class policy |
| Naming conventions и member filters | Выражаются explicit rules, но массового affordance нет |

Фундаментальные object-mapping scenarios — mutable и immutable destinations,
constructors, `init`, `required`, null, factories, nested pairs, reuse,
replacement и identity — покрыты убедительно. Наименее зрелые области —
collections, patch, projection, keyed variants и conventions поверх имён.

## 6. Обнаруженные пробелы и необходимые дополнения

### 6.1. Existing get-only complex child

Это отдельный массовый сценарий, который не следует смешивать только с
getter-only collections:

```csharp
public Address Address { get; } = new();
```

Декларативный mapping должен уметь обновить `result.Address` без присваивания
нового `Address` и без перевода всего outer mapping в `Convert`.

Безопасная будущая семантика:

- member обязан быть readable и non-null;
- применяется nested `Update` к уже доступному child;
- nested pair должна статически гарантировать сохранение identity;
- если nested mapping может вернуть replacement, configuration diagnostic;
- отсутствующий child или необходимость replacement требуют mutator/factory
  либо `Convert`.

Обычный two-argument `Map(source, destination)` с последующим assignment не
решает get-only member: авторитетный nested replacement некуда присвоить.
Поэтому identity-preserving nested update должен стать отдельной declarative
capability.

### 6.2. Projection compatibility contract

Фразы «projection после v0» недостаточно, потому что решения v0 могут случайно
сделать повторное использование pair-plan невозможным или заставить
пользователя поддерживать вторую конфигурацию.

До реализации следует закрепить минимальные законы:

- projection переиспользует тот же declarative pair-plan, а не отдельную
  configuration model;
- поддерживается только статически translatable подмножество `Create`;
- runtime/manual logic, previous/result-dependent behavior, hooks и reference
  tracking не получают ложной projectability;
- вся цепочка nested mappings должна быть projectable;
- client-side fallback отсутствует;
- запрос projection для неподдерживаемого plan приводит к понятному
  diagnostic.

Это не требует проектировать весь provider-specific lowering сейчас, но
защищает главную пользовательскую гарантию: mapping rules не объявляются
повторно только ради projection.

### 6.3. First-class enum mapping

Ручной `Construct` функционально достаточен для единичной пары, но плохо
масштабируется и не даёт exhaustiveness diagnostics. В roadmap следует добавить:

- mapping by value, checked value и name;
- optional case-insensitive matching;
- explicit overrides;
- ignored values;
- fallback;
- source/target exhaustiveness diagnostics;
- enum ↔ string naming strategy.

Широкое автоматическое обнаружение `Parse`, `ToString` и произвольных factory
methods не требуется. Для них explicit `Construct` безопаснее и понятнее.

### 6.4. Масштабируемое name matching

Exact-by-default следует сохранить. В качестве opt-in нужны детерминированные
правила:

- case-insensitive matching;
- prefixes и suffixes;
- snake_case, camelCase и PascalCase;
- ограниченные replacements/normalization;
- ambiguity diagnostic после нормализации.

Произвольный runtime delegate здесь не нужен: он плохо сочетается с source
generation, детерминизмом и compile-time diagnostics.

### 6.5. Расширение collection design

В collection-этап следует явно включить:

- flattening через element path;
- mapping join entities;
- выбор между writable replacement и clear/fill;
- reconciliation по key;
- авторитетный element replacement;
- взаимодействие с projection, patch и runtime polymorphism.

Без этих пунктов слово «collections» покрывает типы контейнеров, но не
закрывает основной existing-target lifecycle.

### 6.6. Расширение patch design

Patch должен описывать не только ignore-null, но и общую модель presence:

- absent, value и explicit-null;
- `default(T)` как отдельную opt-in policy;
- сохранение constructor/property initializers;
- operation-specific create/update behavior;
- nested и collection patch.

Иначе отдельные null/default switches быстро образуют труднообъяснимую систему
пересекающихся исключений.

### 6.7. Keyed variants

Несколько unkeyed descriptors и будущие keyed mappings уже имеют направление,
но для продуктовой готовности нужно определить:

- область уникальности key;
- compile-time и runtime selection;
- propagation key во вложенные mappings;
- взаимодействие с inheritance, polymorphism и collections;
- default variant и поведение при его отсутствии;
- ambiguity diagnostics.

Это не требует изменения текущей pair/lifecycle модели, но без этих законов
variants пока являются обещанием, а не готовой capability.

## 7. Что рекомендуется объявить non-goals

Некоторые текущие формулировки «рассмотреть после v0» по принятому правилу
автоматически становятся обещанием поддержки. Чтобы roadmap не стал
неограниченным, рекомендуется явно объявить non-goals:

- async/I/O mapping;
- root `Task`, `ValueTask`, `Lazy`, `IObservable` и `IAsyncEnumerable`;
- arbitrary delegate и expression-tree roots; projection должна оставаться
  отдельной capability;
- private-state bypass;
- runtime-only dynamic shapes;
- automatic reverse mapping.

Это рекомендации аудита, а не уже принятые нормативные решения. Их явное
принятие не сузит обычный object mapper, но отделит mapping от orchestration,
runtime serialization и произвольного исполнения пользовательского кода.

## 8. Оценка удобства

### 8.1. Где Morphant особенно удобен

Для сложных сценариев Morphant удобнее сравниваемых mapper-ов. Пользователь
видит:

- когда объект создаётся и когда переиспользуется previous;
- какой result получит member-plan;
- может ли `Update` вернуть replacement;
- где применяются conventions;
- где заканчивается declarative model и начинается ручной algorithm;
- какие rules унаследованы и каков их precedence;
- какая nested pair и какая операция вызывается.

Это особенно ценно для immutable objects, factories/cache, nullable graphs,
records, existing aggregate roots и конфигураций с inheritance.

### 8.2. Где Morphant многословнее

Для простого DTO graph цена предсказуемости заметна:

- nested pair не применяется автоматически;
- `Option<T>` нужно изучить;
- structured result policy и `Members` иногда повторяют одно expression, хотя
  generator обязан вычислить его один раз;
- declarative `with` и expression sharing образуют graph DSL, а не обычное
  последовательное выполнение C#;
- до появления collections, enum policies и name normalization часть частых
  сценариев потребует ручной записи.

Эта цена приемлема, если generated surface обеспечивает сильный IntelliSense,
а diagnostics коротко объясняют ошибку и показывают рекомендуемый путь. В
документации потребуется раннее и явное правило: declarative DSL выглядит как
C#, но описывает dependency graph, а не imperative execution order.

## 9. Release readiness

v0 без collections нельзя позиционировать как полноценный general-purpose
mapper: массивы и списки встречаются почти в каждом реальном DTO graph. Такой
v0 корректно выпускать как architectural preview, foundation release или
ограниченный object-mapping core.

Для пользовательского 1.0 минимальная collection capability должна быть
реализована. Enum policy и name normalization существенно улучшат onboarding и
конкурентоспособность, но не блокируют проверку core architecture.

Полная спецификация пока оценивается ниже самой архитектуры, потому что часть
важных future-capabilities только названа. Это не требует нового redesign:
текущие result policies, `Members`, `Convert`, authoritative result и explicit
nested dispatch оставляют для них совместимые точки расширения.

## 10. Рекомендации, отложенные при переходе к naming-аудиту

Перед naming-аудитом были рекомендованы следующие дополнительные продуктовые
решения:

1. добавить existing get-only complex-child update как отдельное post-v0
   обязательство;
2. закрепить минимальные projection invariants;
3. добавить first-class enum mapping и opt-in name matching в roadmap;
4. расширить collection и patch stages найденными массовыми сценариями;
5. уточнить законы keyed variants;
6. разделить future commitments и явные non-goals.

Из-за дедлайна v0 принято продолжить с уже согласованным core design, не
расширяя его этими возможностями сейчас. Рекомендации остаются post-v0
направлениями и не являются незакрытой частью naming-этапа.

## 11. Финальный вердикт

Новый дизайн Morphant действительно взрослый. Его сильная сторона — не
количество автоматических conversions, а явная и проверяемая lifecycle-модель.
По архитектурной предсказуемости он превосходит AutoMapper и Mapster и
концептуально чище Mapperly в existing-target и configuration composition.

Core redesign завершён удачно. Найденные проблемы не требуют возвращения к
unified `Template` и не ломают разделение `Construct` / `Members` /
`Convert`. После v0 продуктовый roadmap следует дополнить contracts будущих
массовых capabilities, сохраняя нынешнюю явность identity, null, precedence и
operation semantics.

[automapper-nested]: https://docs.automapper.io/en/stable/Nested-mappings.html
[automapper-collections]: https://docs.automapper.io/en/stable/Lists-and-arrays.html
[mapperly-conversions]: https://mapperly.riok.app/docs/configuration/conversions/
[mapperly-enums]: https://mapperly.riok.app/docs/configuration/enum/
[mapperly-1294]: https://github.com/riok/mapperly/issues/1294
[mapster-952]: https://github.com/MapsterMapper/Mapster/issues/952
[mapster-947]: https://github.com/MapsterMapper/Mapster/issues/947
[mapperly-2178]: https://github.com/riok/mapperly/issues/2178
[mapster-982]: https://github.com/MapsterMapper/Mapster/issues/982
[mapperly-2252]: https://github.com/riok/mapperly/issues/2252
[mapperly-2253]: https://github.com/riok/mapperly/issues/2253
[mapperly-1700]: https://github.com/riok/mapperly/issues/1700
[mapperly-1978]: https://github.com/riok/mapperly/issues/1978
[mapperly-2039]: https://github.com/riok/mapperly/issues/2039
