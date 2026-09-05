# Полная проверка Morphant: этап 2

Дата: 2026-09-05. План: [RELEASE_REVIEW_PLAN.md](RELEASE_REVIEW_PLAN.md). Исходная инвентаризация: [этап 1](RELEASE_REVIEW_STAGE_01.md).

Статус: пользователь оценил этап и разрешил исправить пояснения API и диагностики. Nullable return contract сохраняется по его решению; подробности ниже. Продолжение проверки — [этап 3](RELEASE_REVIEW_STAGE_03.md).

## 1. Результат

Основное разделение API обосновано: регистрация точной пары, выбор destination, правила членов и полный пользовательский алгоритм отвечают разным задачам. Кортежи вписываются в ту же модель пары и операций; отдельный multi-source/state API для рассмотренных сценариев не нужен. Оснований объединять `Construct` с `ConstructUsing`, убирать `ConstructorParameters`, заменять member record на struct или переносить хелперы в `ITypeMapper.Update` не найдено.

Подтверждено, что при допустимом `null` source результат может быть `null`, хотя C# считает его non-nullable. Потребитель компилируется без nullable warnings и получает `NullReferenceException`. После обсуждения пользователь выбрал сохранить управление аннотацией через `TDestination`: обязательные предупреждения во всех обычных вызовах ухудшили бы UX. Это осознанный компромисс существующего API, а не регрессия кортежей.

Также установлены трудности с объяснением синтаксиса `Construct`, диагностики standalone nested `Update` и миграции с 0.4.0. Отдельно отмечено намеренное, но легко пропускаемое поведение: неполный convention-маппинг по умолчанию не вызывает completeness warning.

## 2. База, источники и границы

- Проверяемый код продукта: `7759f41114fcbf117bffb9f776b80ba88d16f59d`.
- HEAD на старте этапа: `45bed0858b48417e32516dd9504543b85dbcedd5`; отличие от исходного продукта состояло только из двух документов этапа 1. Рабочее дерево было чистым; remote проверен через fetch.
- Последний опубликованный релиз на момент проверки: [Morphant 0.4.0](https://github.com/strangeman375/Morphant/releases/tag/v0.4.0), опубликован 2026-08-24. Статус latest/draft/prerelease проверен через GitHub API. Для сравнения исходников использован локальный tag `v0.4.0`.
- Прочитаны README, quick start, справочник методов и декларативных выражений, guides по Create/Update, conventions, null handling, settings, inheritance, tuples, DI, nested mapping, flattening, IncludeMembers и polymorphism. Сопоставлены соответствующие runtime-контракты и emitters; выполнен diff публичного runtime с `v0.4.0`.
- Проверки потребителей выполнялись настоящим MSBuild с analyzer-style `ProjectReference`: SDK 10.0.100, C# 9, `net10.0`, nullable enabled, warnings as errors, generator собран с Roslyn 4.4.0. Это compiler host SDK 10, а не unit-driver Roslyn 4.4.0.

Этап проверяет пользовательский контракт и дизайн. Полная проверка overload resolution/IVT относится к этапу 3, семантики всех форм — к этапам 4–8, каждой диагностики — к этапу 9, IDE — к этапу 10, упаковки — к этапу 11, всех документов и примеров — к этапу 12. Наличие хорошего API само по себе не подтверждает корректность этих реализаций.

## 3. Путь пользователя и оценка решений

| Сценарий | Текущая модель | Оценка и дальнейшая проверка |
| --- | --- | --- |
| Установка и первый маппер | Один пакет, `[MorphantMapper]`, `partial`, `TypeMapper<ThisMapper>`, override `Configure`, bare `Map<S,D>()` | Путь короткий и объяснён. Self-type — дополнительная обязанность; для простого класса достаточно одного понятного примера |
| Вызов из приложения | `IMapper.Map(source)`/`Map(source, destination)`; точная регистрация каждой пары через DI | Последовательно. Необходимо знать обе generic-типа для Create и сохранять результат Update. Ручная регистрация каждой пары — заметная цена на больших проектах, но она явно оговорена; генерация DI отложена |
| Использование без DI | Cast к точному `ITypeMapper<S,D>`, затем `Create`/`Update` | Предсказуемая альтернатива. Явный контракт устраняет необходимость угадывать destination у маппера с несколькими парами |
| Конструктор с conventions | Bare `Map`, `ConstructorSelection`, `ByConvention()` и overrides | Обязательность входов сохранена. `Unambiguous`, `Greediest` и `Largest` имеют разные объяснимые правила; переименования сейчас не нужны |
| Явное создание | `Construct(s => new(...))`, при необходимости `Members` | Компактно, сохраняет управление creation-only правилами. Нужно объяснить тип результата и обе формы `new`; S02-03 |
| Выбор существующего/нового результата | `Resolve((s, previous) => previous или new(...))` | Различие с `Construct` оправдано: выбор выполняется при каждой операции. `Option` позволяет отличить отсутствие destination от существующего default value |
| Factory/cache/interface destination | `ConstructUsing`/`ResolveUsing`, обычный callback и настоящий `MappingContext` | Разделение с декларативными методами оправдано: готовый объект уже инициализирован, его init-члены нельзя дозаполнить. Возвращённый `null` завершает маппинг |
| Переименование/вычисление членов | `Members(s => new() { ... })`, `Auto`, `Ignore`, `Value<T>` | Основной сценарий удобен. Дополнительные формы `previous`, `result`, `context` нужны для разных зависимостей; не требуют знания generated namespace |
| Изменение вложенного get-only объекта | Standalone `Update(source.Child, members.Child)` через локальный member-план | Возможность есть и работает, но здесь пользователь должен явно назвать сгенерированный тип. Отказ близкой формы через `result.Child` объяснён недостаточно; S02-02 |
| Произвольный алгоритм | `Convert`, исходный nullable source, `Option` previous, настоящий context | Самостоятельный режим с ясной ответственностью за null/loops/mutation/order. Смешение с destination/member rules было бы неоднозначным, текущий запрет обоснован |
| Необязательные значения и null | Отдельные source/destination policies; `Option` previous | Runtime-правила описаны. Аннотацию результата выбирает вызывающий код; это принятое ограничение S02-01 |
| Настройки | 8 настроек, assembly → mapper → pair с явным приоритетом include/base; `Default` продолжает поиск | Сложность в основном соответствует возможностям. Важны локальность overrides и независимость от порядка `Map`/mapper settings. Реализация матрицы остаётся этапу 6 |
| Наследование конфигурации | CRTP layers, `base.Configure`, затем явный `IncludeBase` | Повторение self-constraints усложняет иерархии, но граница сформулирована. `base.Configure` не регистрирует все base pairs в derived mapper; это явно объяснено в guide |
| `IncludeBase` разных/одинаковых пар | Разные пары делят настройки/member rules, одинаковая пара может импортировать destination rule | Различие необходимо: создание базового destination нельзя автоматически использовать как создание производного. Правила и dispatch остаются отдельными |
| Flattening и `IncludeMembers` | Поиск путей/добавление source scopes; nested mapping начинается только явным marker | Различие полезно: lookup членов не запускает неизвестные mappings. Приоритет прямого несовместимого member над flattened candidates требует помнить правило, но оно документировано |
| Multi-source, multi-result и state | Один tuple source/destination, explicit nested mappings | Сохраняет единый типизированный API. State явно передаётся в каждую вложенную пару, которая в нём нуждается; скрытого ambient state нет |
| `ValueTuple` и `System.Tuple` | Имена/explicit ItemN, разные правила mutable и read-only Update | Различие соответствует типам. Отсутствие positional fallback делает поведение менее неожиданным при перестановке имён; native tuple syntax не означает keyed runtime mapping |
| Полиморфизм | `ForDerived` отдельно от регистрации и `IncludeBase`; наиболее специфичная source-ветвь | Разделение трёх действий оправдано. Строгий Update destination и отдельная настройка unknown subtype делают отказ объяснимым. Проверка всех комбинаций остаётся этапу 8 |
| Ошибочный/неполный маппинг | Native C# diagnostics, Morphant diagnostics, runtime stubs для поддерживаемых контрактов | Концепция обоснована. Новая проба S02-02 показывает необходимость более точного сообщения; при выключенной completeness validation часть пропусков намеренно молчит — S02-04 |

`Create` и `Update` — операции над результатом, а не обещание обязательно создать объект или обязательно сохранить его identity. Эта модель согласована с structs, factories и tuples. Сохраняем требование пользоваться возвращённым значением.

Декларативные lambdas описывают зависимости и правила, а не произвольную последовательность C# statements. Пропущенное правило или перекрытое выражение может не вычисляться; `result` нельзя читать до его создания. Документация описывает эту границу, но при улучшении IntelliSense следует дать короткую ссылку на неё из `Construct`/`Resolve`/`Members`, чтобы пользователь не узнавал о различии только из длинного guide. Для полного обычного алгоритма уже есть `Convert`; ещё один режим не предлагается.

## 4. S02-01 — nullable-аннотацией результата управляет вызывающий код

Классификация после обсуждения: **принятый компромисс дизайна**. Поведение реализации соответствует null policies и имеющимся тестам. Non-nullable `TDestination` выражает ожидание вызывающего кода, а не проверяемую Morphant гарантию результата.

Минимальный пример для console consumer с nullable enabled и warnings as errors:

```csharp
using System;
using Morphant;

namespace Example
{
    public sealed class Source
    {
        public string Name { get; set; } = string.Empty;
    }

    public sealed class Destination
    {
        public string Name { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public sealed partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }

    internal static class Program
    {
        private static void Main()
        {
            ITypeMapper<Source, Destination> mapper = new TestMapper();
            Console.WriteLine(mapper.Create(null).Name);
        }
    }
}
```

`null` является допустимым аргументом: API объявляет `TSource?`. При `NullSourceHandling.ReturnNull` возвращается `default(TDestination)`. Но метод возвращает `TDestination` без `MaybeNull`, и при `TDestination=Destination` C# разрешает разыменование без `CS8602`.

В выполненной пробе подтверждены три пути: direct `Create(null).Name`, direct `Update(null, new Destination()).Name`, `IMapper.Map<Source, Destination>(null).Name`. Сборка: 0 warnings, 0 errors; каждый путь получает `NullReferenceException`. Исключения перехватывались самой пробой для фиксации результата. Этот риск сохраняется при неверно выбранной пользователем nullable-аннотации; заданная null policy выполняется корректно.

Источники: [IMapper](../../src/Morphant/Mapper.cs), [ITypeMapper](../../src/Morphant/TypeMapper.cs), [direct extensions](../../src/Morphant/TypeMapperExtensions.cs), [null-source emission](../../src/Morphant.Generator/TypeMapperGeneration/TypeMapperEmitter.cs), [null handling](../settings/null-handling.md). Общие сигнатуры были такими же в `v0.4.0`; проблема не ограничена mapper-scoped API или кортежами.

### Варианты

| Вариант | Последствия |
| --- | --- |
| Консервативно отразить возможный `null` в общих возвращаемых контрактах (`[return: MaybeNull]` / эквивалентная nullable-аннотация) | Сохраняет runtime-поведение. Появятся предупреждения в вызывающем коде, включая случаи, где конкретная конфигурация фактически гарантирует результат. Нужно согласованно охватить interfaces, implementations, extensions, generated methods и nested usage |
| Изменить runtime-контракт: для определённых non-nullable mappings возвращать только non-null либо бросать исключение | Может дать более сильную гарантию, но меняет null policies и требует отдельной модели nullable registrations/CLR identity, factories и `Convert`. Это существенно больший redesign |
| Оставить только пояснение в документации | Сохраняет удобство текущих вызовов, но compiler flow analysis продолжает не видеть реальный риск |

Решение пользователя от 2026-09-05: сохранить возвращаемый `TDestination` без безусловного `MaybeNull`. Пользователь выбирает, например, `IMapper.Map<Source, Destination?>`, если допускает null-результат. Приоритет — отсутствие лишних предупреждений в обычных сценариях; аннотация не меняет runtime null policies. Первоначальное предложение консервативно аннотировать все результаты не принято и не является задачей следующих этапов. Согласовано краткое пояснение в null-handling guide и XML-документации.

Покрытие сохраняет обе стороны принятого контракта: [MappingInterfaceNullabilityTests](../../src/tests/Morphant.Generator.UnitTests/MappingInterfaceNullabilityTests.cs) проверяет return metadata без принудительной nullable-аннотации, а [TypeMapperNullHandlingTests](../../src/tests/Morphant.Generator.UnitTests/TypeMapperNullHandlingTests.cs) подтверждает возврат default. На этапах 4/8 остаётся проверить фактическую семантику policies, factories/manual/nested paths и выбор nullable destination вызывающим кодом; менять общий return contract для этого не требуется.

## 5. S02-02 — standalone nested Update требует неочевидной формы

Классификация: **подтверждённая трудность API и неточное объяснение диагностики**, средняя важность. Неподдерживаемая форма диагностируется; молчаливого исчезновения генерации в пробе не было.

У destination есть `public ChildDestination Child { get; } = new();`, а `ChildDestination` — обычный reference type. В том же маппере зарегистрирована пара `ChildSource -> ChildDestination`. Пользователь естественным образом может написать:

```csharp
builder.Map<Source, Destination>()
    .Members((source, previous, result) =>
    {
        Update(source.Child, result.Child);
        return new();
    });
```

Проба получает `MORPH0046` на destination-аргументе со следующим объяснением: `Update requires a readable reference-type destination member here`. При этом `result.Child` действительно является доступным readable reference-type member. Отказ относится к форме выражения: standalone DSL Update здесь требует member selector сгенерированного member-плана.

Поддерживаемый вариант из [nested mapping](../nested-mapping.md):

```csharp
.Members((source, _) =>
{
    var members = new DestinationMembers();
    Update(source.Child, members.Child);
    return members;
});
```

После импорта конкретного generated namespace либо полного имени этот вариант собрался без warnings/errors и выполнил nested Update: `Child.Id` стал 42. В выполненной пробе использовалось полное имя `Morphant.Generated.Types.N_MorphantReviewStage02.Plans.DestinationMembers`.

Следствия для UX:

- Для обычного expression-bodied `Members` достаточно `new()`. Для этого сложного сценария нужно узнать имя и namespace generated type; `var members = new()` не даёт C# target type.
- У tuple-планов одно и то же leaf-имя `TupleMembers` в разных namespaces. При нескольких таких callbacks explicit names/aliases требуют дополнительного внимания. Их конкуренция отдельно не проверялась на этом этапе.
- Текст `MORPH0046` не объясняет отличие `members.Child` от `result.Child`. [Help page](../diagnostics/MORPH0046.md) тоже начинает с общей доступности destination, хотя ссылка на правильный standalone пример есть.

Рекомендация для ближайшего исправления: уточнить причину `ReadOnlyProxyInvalid` в [NestedMappingDiagnosticAnalyzer](../../src/Morphant.Generator/TypeMapperGeneration/NestedMappingDiagnosticAnalyzer.cs) и пример в help page. Существующего кода `MORPH0046` достаточно; новая диагностика ради этого различия не нужна. Добавить регрессию на ошибочную и исправленную формы, включая точную позицию и отсутствие побочных compiler errors.

Возможность принимать standalone `Update` через `result.Child` заслуживает отдельного обсуждения, но пока не предлагается как готовое изменение: необходимо проверить readonly-input policy, lifecycle, алиасы, null-skipping и отбрасывание replacement. Нельзя просто снять валидацию формы. Текущую возможность с явным generated type считаем рабочей, но имеющей установленную цену для пользователя.

## 6. S02-03 — синтаксис Construct недостаточно явно отделён от ordinary creation

Классификация: **подтверждённый пробел объяснения API**, средняя важность. Native C# корректно отвергает несовместимый callback; отдельный дефект алгоритма construction этим не установлен.

Для destination с конструкторами `Destination(string value)` и `Destination(object value)`:

```csharp
// Эта форма успешно компилируется без warnings/errors.
.Construct(source => new(source.Text));

// Эта форма получает CS0029 и CS1662.
.Construct(source => new Destination(source.Text));
```

Во втором случае компилятор сообщает о невозможности преобразовать `Destination` в сгенерированный `DestinationConstruction`. Callback возвращает сгенерированный `DestinationConstruction`, поэтому поддерживаются и `new(...)`, и `new DestinationConstruction(...)`. Target-typed `new` — именно необязательное сокращение C#. Первоначальный вывод о необходимости опускать имя типа был ошибочным: проверялось явное имя destination, а не сгенерированного типа.

Рекомендация: в `Construct`/`Resolve` и соответствующей IntelliSense-документации кратко объяснить тип результата и обе поддерживаемые формы — `new(...)` и явное имя сгенерированного construction-типа; для обычного callback, возвращающего уже созданный объект, указать `ConstructUsing`/`ResolveUsing`. Не превращать публичную страницу в описание внутренних планов и не добавлять автоматически дубликат C# diagnostic.

Положительная проверка также опровергла предварительное опасение по этому конкретному overload-case: `string`/`object` constructors не сделали target-typed DSL-вызов неоднозначным. Полная матрица overload resolution остаётся этапам 3/4; один успешный пример её не заменяет.

Смежная точность документации: [Construct](../api/construct.md) описывает tuple construction как «one callback parameter for each tuple element». В действительности callback имеет source и, опционально, context; элементы — аргументы `new(...)` внутри callback. На [tuple mapping](../tuple-mapping.md) стоит уточнить ту же терминологию. Это правка текста, не дополнительный API.

## 7. S02-04 — completeness validation по умолчанию выключена

Классификация: **подтверждённое намеренное поведение и вопрос обнаруживаемости настройки**, не дефект реализации.

```csharp
public sealed class Source { public long Id { get; set; } }
public sealed class Destination { public int Id { get; set; } }

// Нет warning-free implicit conversion long -> int.
builder.Map<Source, Destination>();
```

Выполненная проба: исходный `Id=42L`, результат `Id=0`; 0 warnings/errors. Это соответствует одновременно conventions и `UnmappedMemberValidation.None`.

Контрольная проба добавляет `.UnmappedMemberValidation(UnmappedMemberValidation.Destination)` и получает `MORPH0048` на регистрации пары. При warnings as errors сборка останавливается, то есть существующая защита работает при включении.

Рекомендация: сохранить нынешний default до отдельного решения, а в quick start добавить короткий шаг про `UnmappedMemberValidation.Destination` для обнаружения пропущенных destination-членов и ссылку на полную настройку. `Strict` полезен там, где действительно нужно проверять обе стороны; включение Source validation везде может давать шум при проекции большого DTO в небольшой результат.

Default `None` не следует представлять как отсутствие возможных пропусков mapping. Полнота диагностики при включённой настройке остаётся этапу 9.

## 8. S02-05 — миграция с 0.4.0 требует отдельного короткого маршрута

Классификация: **подтверждённый пробел release-документации**, средняя важность. Changelog сообщает о breaking change, а guides описывают новый API, но единого before/after маршрута не найдено.

| В 0.4.0 | Сейчас | Что нужно пользователю |
| --- | --- | --- |
| `TypeMapper` | `TypeMapper<TMapper>` | Для простого маппера указать собственный тип |
| Namespace-level `Morphant.MapperBuilder` | Protected nested `TypeMapper<TMapper>.MapperBuilder` | Обычный unqualified параметр override продолжает выглядеть как `MapperBuilder`; явное старое полное имя нужно заменить |
| `MapperBuilder<TSource, TDestination>` | `MappingBuilder<TMapper, TSource, TDestination>` | Учесть и переименование, и новый generic-аргумент там, где допустимый пользовательский код явно ссылался на builder type |
| Наследование от non-generic configuration base | Self-typed CRTP на каждой reusable layer | Нельзя мигрировать всю иерархию простым добавлением `<BaseMapper>` в её корне; leaf должен закрывать семейство собой |
| Generic-параметры reusable base | Каждый non-self параметр участвует в каждой объявленной паре | Объяснить `MORPH0060` и перенос не зависящих от параметра пар в другой base layer |
| Private/protected вложенный mapper | Требуется доступность для namespace-level generated code | Показать `MORPH0059` и варианты изменения расположения/доступности |
| Старые явные имена/imports generated types внутри Configure | Новый layout generated types | Для поддерживаемых явных member-plan locals обновить imports; не обещать стабильность старого generated namespace |

Правильный новый каркас reusable hierarchy:

```csharp
public abstract class CommonMapper<TMapper> : TypeMapper<TMapper>
    where TMapper : CommonMapper<TMapper>
{
    protected override void Configure(MapperBuilder builder)
    {
        // Общие настройки и пары для явного IncludeBase.
    }
}

[MorphantMapper]
public partial class ApplicationMapper : CommonMapper<ApplicationMapper>
{
    protected override void Configure(MapperBuilder builder)
    {
        base.Configure(builder);
        // Локальные Map и нужные IncludeBase.
    }
}
```

`IMapper.Map`, `ITypeMapper.Create/Update`, значения settings и ordinary DI registration не были переименованы этим рефакторингом. Не следует заставлять пользователя переписывать их по устаревшим ранним дизайнам.

Рекомендация: поместить короткую миграцию рядом с release notes, со ссылками на evergreen guides и `MORPH0058`–`0060`. Breaking change нельзя выпускать как совместимый patch 0.4.x. Вопрос новой версии и окончательной упаковки остаётся этапу 11; текущие `Version=0.4.0`, `AssemblyVersion=0.4.0.0` и старые package notes не менялись в этом аудите.

## 9. Обоснованность объёма генерации

Уточнение после этапа 3: пользователь утвердил отказ от shared-расширений
ради упрощения и изоляции DSL. Ниже сохранено измерение прежней реализации;
актуальные counts специализированных расширений и переиспользуемых
construction/member-файлов приведены в [отчёте исправлений](RELEASE_REVIEW_STAGE_03_FIXES.md).

### Нужны ли нынешние категории API

| Элемент | Пользовательская причина сохранять | Граница вывода |
| --- | --- | --- |
| `Construct` и `Resolve` | Только создание / выбор reuse или replacement при каждой операции | Различие не сводится к удобному имени одного и того же callback |
| `ConstructUsing` и `ResolveUsing` | Обычный runtime-код возвращает готовый destination и получает настоящий context | Объединение с declarative methods снова смешало бы lifecycle и два вида context |
| 3 формы `Convert` | Source; source+previous; source+previous+context | Удаление коротких форм заставит большинство пользователей писать лишние параметры. Основание считать их лишними не установлено |
| 4 формы `Members` | Постепенно добавляют previous, выбранный result и operation context | Наличие selected result функционально отличается от previous после replacement |
| Construction-план | Target typing конструкторных аргументов и markers с сохранением input contract | Нельзя убрать без замены механизма декларативного construction |
| `ConstructorParameters` | Частичные overrides выбранного convention-конструктора | Отдельный контейнер не делает обязательные явные параметры необязательными. Поля здесь соответствуют ранее согласованной модели |
| Member record с properties | Initializer, `with`, выражение правил по destination-членам | Замена на struct/поля не предлагается. Дополнительные compiler-generated record members учитываются в объёме сборки |
| `__Create`/`__Update` и другие runtime-хелперы | Читаемое разделение валидации, выбора ветви и исполнения | Само наличие метода не делает его ненужным. Проверка неиспользуемых хелперов остаётся этапу 4 |

Генерация всех форм DSL для **возможностей объявленной пары**, а не только для уже написанных вызовов, нужна для IntelliSense и редактирования конфигурации. API, появляющийся только после ручного ввода его имени, ухудшил бы согласованный UX. Это не оправдывает дублирование одной surface или генерацию недоступных для destination возможностей.

Единообразный подход к generation сохранён в рекомендациях. Перенос части `Using`/`Convert` в runtime-библиотеку, изменение properties на поля, замена record на struct и слияние Update с хелпером повторно не предлагаются.

### Измеренный пример

В отдельном consumer объявлены четыре маппера:

```csharp
// Два независимых маппера объявляют одну обычную пару.
builder.Map<Source, Destination>();
builder.Map<Source, Destination>();

// Ещё два — по одной tuple-паре с различным порядком имён.
builder.Map<Source, (int Id, int Count)>();
builder.Map<Source, (int Count, int Id)>();
```

Первые две строки находятся в разных `Configure`, как и две последние. Source содержит `int Id` и `int Count`; обычный destination — те же writable properties и parameterless constructor. Все регистрации bare, без вызовов генерируемого DSL.

Получено 16 generated files: 4 `TypeMapper`, по 3 `Construction`, `Member`, `MappingExtension`, `MemberExtension`. В extension outputs найдено **45 методов**, объединённых в один partial extension container. Это один shared набор обычной пары и два mapper-scoped набора кортежей. Два обычных маппера не дали 30 одинаковых extension-методов.

В этих outputs 8 именованных plan types: construction/member обычного destination и по constructor-parameters/construction/member для двух tuple-представлений. Для обычного parameterless destination отдельный `ConstructorParameters` не сгенерирован.

Сборка прошла без warnings/errors. Runtime-проба подтвердила обычные результаты и обе tuple-перестановки: при `Id=11`, `Count=22` значения сопоставляются по именам в каждом маппере.

Счётчики получены из полного набора файлов конкретного consumer и объявлений emitters; это измерение структуры, не новый snapshot-тест и не измерение IL-размера. Сам факт 45 методов не доказывает оптимальность. На этапе 3 нужно проверить координацию одинаковых представлений, generic families и пересекающихся scopes; на этапе 4 — лишние runtime-хелперы и recovery outputs.

Вывод этого этапа: в проверенных категориях нет обоснованной функционально нейтральной крупной редукции API. Имеется реальное переиспользование shared surface, а часть дополнительной генерации сохраняет необходимую пользовательскую информацию. Из этого не следует ни отсутствие избыточности во всех случаях, ни приемлемость любого роста. Отложенное удаление DSL из итоговой сборки остаётся отдельной работой.

## 10. Проверки и их фактические результаты

Временные consumer-исходники и build outputs находятся в `artifacts/release-review/stage-02/probes/`; они не входят в git. Существенные входы, наблюдения и рекомендации сохранены в этом отчёте. Это направленные проверки дизайна, а не добавленные в suite регрессионные тесты.

Общие параметры: `dotnet build -c Release --no-restore -p:MorphantRoslynVersion=4.4.0 -p:UseSharedCompilation=false -m:1 -nodeReuse:false`, nullable enabled, warnings as errors. Первой сборке предшествовал успешный restore; последующие использовали восстановленные зависимости. Generator подключался с `OutputItemType=Analyzer`, `ReferenceOutputAssembly=false`. Для проверки `IMapper` использован настоящий `Microsoft.Extensions.DependencyInjection` 10.0.0.

| Проба | Фактический результат |
| --- | --- |
| RuntimeContracts: nullable return + пропуск long→int convention | Build exit 0, 0 warnings/errors; три ожидаемых NRE зафиксированы пробой; `Id=42L` дал `Id=0`; process exit 0 |
| RuntimeContracts + Destination completeness validation | Build exit 1, ровно `MORPH0048`, повышенный warnings-as-errors; прочих diagnostics нет |
| ConstructorChoice: target-typed `new(source.Text)`, string/object constructors | Build exit 0, 0 warnings/errors. Отдельное runtime-выполнение этой пробы не выполнялось |
| ConstructorChoice: явный `new Destination(source.Text)` | Build exit 1, `CS0029` и `CS1662`; отдельного Morphant diagnostic нет |
| ReadOnlyResult: standalone `Update(source.Child, result.Child)` | Build exit 1, ровно `MORPH0046`; прочих diagnostics нет |
| ReadOnlyResult: исправленная форма через generated `members.Child` | Build exit 0, 0 warnings/errors; runtime `Child.Id=42`, process exit 0 |
| SurfaceVolume: две обычные shared и две tuple scoped регистрации | Build exit 0, 0 warnings/errors; измеренные counts и правильные runtime-значения, process exit 0 |

Во время исходного аудита полные unit/integration suite, реальный Rider и новые межсборочные IVT-repro не запускались. Исходный CI и тесты первого этапа не пересчитываются как новые результаты. Native errors в намеренно неверных пробах — ожидаемое наблюдение, а не сломанная основная solution. Проверки согласованных после ревью уточнений приведены в разделе 12.

## 11. Рекомендации, регрессии и переход к следующим этапам

| ID | Предлагаемое действие | Необходимая проверка | Где продолжить |
| --- | --- | --- | --- |
| S02-01 | Решено: сохранить управление nullable-аннотацией через `TDestination`; кратко объяснить ответственность вызывающего кода | Consumer flow analysis вместе с runtime null cases, factories/manual/nested paths | Решение принято; семантическая проверка на этапах 4/8 |
| S02-02 | Текст `MORPH0046`/help уточнён, регрессии проверены; ограничение формы сохранено | Полная проверка readonly lifecycle и null-skip | Этапы 5/8/9/12 |
| S02-03 | Синтаксис `new(...)` и tuple constructor terminology уточнены в справочнике и IntelliSense | Полная матрица construction и XML consistency | Этапы 4/9/12 |
| S02-04 | Destination validation показана в quick start; default сохранён | Полная проверка включённой/выключенной настройки, deliberate Ignore и partial mapping | Этапы 9/12 |
| S02-05 | Короткая миграция с 0.4.0 добавлена в changelog | Компилируемые migrated consumers и окончательные package notes/version | Этапы 3/11/12 |

Строки F02–F20, F24–F26 исходного реестра рассмотрены здесь на уровне пользовательского дизайна; F04/Q08 дополнены измерением и обоснованием категорий. Q07 дополнен маршрутом миграции. Их implementation-проверка не считается закрытой этим этапом. Q01–Q04 про конкуренцию DSL остаются открытыми; результаты SurfaceVolume внутри одной сборки не отвечают на IVT-вопрос.

Основные ранее выбранные решения сохраняются. Вопрос nullable return contract закрыт решением пользователя. Следующий этап плана — **3: объявления мапперов, пары и изоляция DSL**, начиная с воспроизведения межсборочной конкуренции; для его начала по-прежнему нужна команда пользователя.

## 12. Согласованные уточнения после ревью

- S02-01: описан выбор nullable результата в null handling, добавлена ссылка из runtime guide и короткие XML-пояснения. Сигнатуры и runtime-семантика сохранены.
- S02-02: уточнён текст `MORPH0046`, help и пояснение к существующему примеру nested Update. Добавлена регрессия для `result.Child`, усилена проверка неправильного selector; положительный случай уже есть в той же группе.
- S02-03: уточнены `new(...)` и выбор `Using` в справочнике, исправлена терминология tuple construction. Пояснение синтаксиса также появляется в IntelliSense; полные ожидаемые generated sources обновлены только в соответствующих XML-строках.
- S02-04: первый пример quick start включает Destination validation и кратко объясняет её назначение и выключенный default.
- S02-05: краткая миграция с 0.4.0 находится в changelog рядом с release changes и ссылается на подробный inheritance guide.

Изменения опубликованы контрольной точкой `d2c5f4c8d4051ebde09632c5c1145027fe19d507`. [CI этого коммита](https://github.com/strangeman375/Morphant/actions/runs/33965611910) завершился успешно. Статусы сверены с jobs и их полными журналами:

| Проверка | Результат |
| --- | --- |
| Release, Ubuntu / Windows / macOS | Каждая сборка: 0 warnings, 0 errors |
| Unit, Roslyn 4.4.0, Ubuntu / macOS | На каждой ОС: 733 passed, 1 skipped, 0 failed |
| Unit, Roslyn 4.4.0, Windows | 731 passed, 3 skipped, 0 failed |
| Unit, Roslyn 4.9.2 | 734 passed, 0 skipped, 0 failed; сборка без warnings/errors |
| Integration, Ubuntu / Windows / macOS | На каждой ОС: 266 passed, 0 skipped, 0 failed |
| Пакет, SDK 7.0.100 / MSBuild 17.4 | Build и выполнение consumer — success |
| Пороги покрытия | CI gate — success |

Пропуски предусмотрены существующими условиями тестов: collection expressions требуют Roslyn 4.8+ и проверены на 4.9.2; два теста directory symlinks пропущены на Windows и пройдены на Ubuntu/macOS. Это не пропуски новых регрессий.

Локальная Release-попытка остановилась до тестов: старые restore assets ссылались на отсутствующий кеш `/root/.nuget/packages`, тогда как зависимости доступны в `/workspace/.nuget/packages`. Она не учитывается как успешная проверка; итоговые сборочные и тестовые доказательства получены из CI точного опубликованного коммита.

Проверены 63 относительные ссылки и anchors изменённых документов, отсутствие устаревших формулировок в публичных docs и `git diff --check`. Механическая проверка подтвердила: в 29 snapshot-файлах изменились только 244 XML-строки с двумя согласованными подсказками. Уточнения этапа завершены; следующий этап аудита не начат.


## 13. Поправка после замечания пользователя

Первоначальное требование `new(...)` без имени типа было сформулировано ошибочно: явное имя **сгенерированного construction-типа** поддерживается. Неподходящим является результат обычного `new Destination(...)`, для которого есть `ConstructUsing`/`ResolveUsing`. Раздел S02-03 выше исправлен; удобство короткой формы не ограничивает поддерживаемый синтаксис.

Справочник, IntelliSense и соответствующие XML-строки snapshots исправлены в [f55d4cf](https://github.com/strangeman375/Morphant/commit/f55d4cf67785c09f57d0a975006adf097d65a5bb). Новая проба с `new DestinationConstruction(...)` собрана и выполнена успешно. Полная Release-сборка: 0 warnings/errors; unit: 733 passed и 1 штатный skipped на Roslyn 4.4.0; integration: 266 passed. Подробности запусков — в [отчёте этапа 3](RELEASE_REVIEW_STAGE_03.md).

Публичные изменения опубликованы. Автоматическая проверка разрешений первоначально отклонила публикацию обновлённых внутренних материалов. Пользователь отдельно подтвердил публикацию отчётов, плана и воспроизведений в публичный `main`.
