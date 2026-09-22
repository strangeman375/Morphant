# Обычные enum: неизвестные числа, identity и диапазон

Дата: 2026-09-22. Статус: исследование для обсуждения; рекомендации не утверждены.
По решению пользователя flags отложены как отдельная тема. Здесь рассматриваются
только обычные enum, без проекций и пользовательских конвертеров, заменяющих
стандартный алгоритм. Основной контракт: [enum design](ENUM_MAPPING_DESIGN.md).

## Метод и границы проверки

Проверены официальная документация, исходники и тесты. Библиотеки целиком
не запускались. Результаты для чисел вроде `300` ниже — вывод из конкретного
кода преобразования и правил C#, а не отчёт о сравнительном runtime-прогоне.
Ветки разработки не приравниваются к последнему опубликованному NuGet-пакету.

| Исходники | Проверенный commit |
|---|---|
| AutoMapper, main | `6e8697bc44f02fb54ef6a556a7d134e63485299e` |
| Mapster, master | `cbc1e5f04e6e003744b04f83d4705670a85e459b` |
| Mapperly, main | `f87d48b12a6010a224ca26ad112fb48c07cd6d5d` |
| AutoMapper.Extensions.EnumMapping, master | `6c273e228373afa03fd70c0c099686006d6f47d8` |

## Неизвестные значения

Для сравнения число `42` помещается в оба underlying type, но не объявлено
ни в source, ни в destination. Explicit overrides отсутствуют.

| Маппер и режим | Что происходит с `42` | Подтверждение |
|---|---|---|
| AutoMapper, встроенный enum mapper | Сохраняется; `ToString`/`TryParse` допускают числовое представление, при неуспехе есть cast | [EnumToEnumMapper](https://github.com/LuckyPennySoftware/AutoMapper/blob/6e8697bc44f02fb54ef6a556a7d134e63485299e/src/AutoMapper/Mappers/EnumToEnumMapper.cs) |
| Mapster, `ByValue` | Сохраняется обычным cast, без проверки объявленности | [EnumAdapter](https://github.com/MapsterMapper/Mapster/blob/cbc1e5f04e6e003744b04f83d4705670a85e459b/src/Mapster/Adapters/EnumAdapter.cs), [PrimitiveAdapter](https://github.com/MapsterMapper/Mapster/blob/cbc1e5f04e6e003744b04f83d4705670a85e459b/src/Mapster/Adapters/PrimitiveAdapter.cs) |
| Mapperly, `ByValue` | Сохраняется обычным cast; diagnostics покрытия не меняют этот runtime-путь | [EnumTest](https://github.com/riok/mapperly/blob/f87d48b12a6010a224ca26ad112fb48c07cd6d5d/test/Riok.Mapperly.Tests/Mapping/EnumTest.cs) |
| Mapperly, `ByValueCheckDefined` | Проверяется destination; неизвестное значение приводит к fallback либо исключению | [EnumCastMapping](https://github.com/riok/mapperly/blob/f87d48b12a6010a224ca26ad112fb48c07cd6d5d/src/Riok.Mapperly/Descriptors/Mappings/Enums/EnumCastMapping.cs), [fallback tests](https://github.com/riok/mapperly/blob/f87d48b12a6010a224ca26ad112fb48c07cd6d5d/test/Riok.Mapperly.Tests/Mapping/EnumFallbackValueTest.cs) |
| AutoMapper.Extensions.EnumMapping, `MapByValue` | Без отдельного override нет записи в таблице; runtime mapping бросает исключение | [EnumMappingFeature](https://github.com/AutoMapper/AutoMapper.Extensions.EnumMapping/blob/6c273e228373afa03fd70c0c099686006d6f47d8/src/AutoMapper.Extensions.EnumMapping/Internal/EnumMappingFeature.cs), [CustomMapExpressionFactory](https://github.com/AutoMapper/AutoMapper.Extensions.EnumMapping/blob/6c273e228373afa03fd70c0c099686006d6f47d8/src/AutoMapper.Extensions.EnumMapping/Internal/CustomMapExpressionFactory.cs) |

У AutoMapper есть прямой тест `ShouldMapEnumWithInvalidValue`: ноль,
не объявленный ни на одной стороне, сохраняется в другом enum.
[Тест](https://github.com/LuckyPennySoftware/AutoMapper/blob/6e8697bc44f02fb54ef6a556a7d134e63485299e/src/UnitTests/Enumerations.cs).

У Mapperly тест `EnumToOtherEnumByValueShouldCast` ожидает cast даже при
непересекающихся наборах объявленных чисел и warnings о непокрытых значениях.
Вариант с проверкой отделён в публичном API; fallback документирован для
`ByName` и `ByValueCheckDefined`. Передача fallback вместе с `ByValue`
диагностируется, а проверенный builder включает проверку объявленности.
[Документация](https://mapperly.riok.app/docs/configuration/enum/),
[builder](https://github.com/riok/mapperly/blob/f87d48b12a6010a224ca26ad112fb48c07cd6d5d/src/Riok.Mapperly/Descriptors/MappingBuilders/EnumToEnumMappingBuilder.cs).

Это также различает неизвестный source и неизвестный destination. Если число
не объявлено в source, но объявлено в destination, Mapperly с проверкой
при одинаковой числовой представимости его принимает. Расширение AutoMapper
строит default-таблицу из объявленных значений обеих сторон; такого source
в таблице нет. Включение `EnableEnumMappingValidation` относится к проверке
конфигурации, а runtime lookup бросает исключение при отсутствии записи
независимо от её включения.

Ограничение реализации расширения: default numeric lookup сравнивает boxed
значения их собственных underlying types через `Equals`; при разных
underlying types это не полноценное сравнение математических чисел.
Имеющийся тест `EnumValueWithOtherUnderlyingTypeMapping` использует `byte`
на обеих сторонах. Поэтому расширение нельзя считать образцом корректного
общего алгоритма для всех сочетаний ширины и знака.
[Тест](https://github.com/AutoMapper/AutoMapper.Extensions.EnumMapping/blob/6c273e228373afa03fd70c0c099686006d6f47d8/src/AutoMapper.Extensions.EnumMapping.Tests/EnumValueWithOtherUnderlyingTypeMapping.cs).

### Название ByName не гарантирует запрет числового переноса

Mapster при `ByName` форматирует source, затем разбирает строку. Для
неназванного source его helper возвращает числовую строку и затем использует
numeric Parse destination underlying type. Поэтому `42` тоже сохраняется,
если помещается. Именованное source без совпадающего имени может при этом
дать ошибку parsing. [Enum helper](https://github.com/MapsterMapper/Mapster/blob/cbc1e5f04e6e003744b04f83d4705670a85e459b/src/Mapster.Core/Utils/Enum.cs).

Встроенный AutoMapper дополнительно имеет numeric cast после неуспешного
поиска по строке. Mapperly `ByName` строит switch по известным соответствиям
с fallback/throw. Для Morphant уже выбран последний принцип: отсутствие
имени не должно незаметно превращаться в числовое соответствие. Default
`ByName` не пересматривается; основания — в [исследовании default](ENUM_MAPPING_DEFAULT_RESEARCH.md).

## E → E

Без пользовательского преобразования три основных C# маппера используют
сохранение исходного значения, включая неназванное число:

- AutoMapper: `AssignableMapper` стоит раньше enum mapper и возвращает source.
  [Registry](https://github.com/LuckyPennySoftware/AutoMapper/blob/6e8697bc44f02fb54ef6a556a7d134e63485299e/src/AutoMapper/Mappers/MapperRegistry.cs),
  [AssignableMapper](https://github.com/LuckyPennySoftware/AutoMapper/blob/6e8697bc44f02fb54ef6a556a7d134e63485299e/src/AutoMapper/Mappers/AssignableMapper.cs).
- Mapster: `PrimitiveAdapter` пропускает преобразование при одинаковых типах.
  Это относится и к настройке `ByName`, поскольку `ConvertType` не вызывается.
- Mapperly: тест `EnumToSameEnumShouldAssign` ожидает `return source;`.
  `DirectAssignmentMappingBuilder` находится раньше enum builder.
  [Порядок builders](https://github.com/riok/mapperly/blob/f87d48b12a6010a224ca26ad112fb48c07cd6d5d/src/Riok.Mapperly/Descriptors/MappingBuilders/MappingBuilder.cs),
  [direct assignment](https://github.com/riok/mapperly/blob/f87d48b12a6010a224ca26ad112fb48c07cd6d5d/src/Riok.Mapperly/Descriptors/MappingBuilders/DirectAssignmentMappingBuilder.cs).

При явном подключении AutoMapper.Extensions.EnumMapping для `E → E`
используется его custom converter с той же таблицей: неназванное число без
override не получает identity. Это следует из реализации converter, а не
из отдельного найденного теста same-type с неназванным значением.

Эти реализации подтверждают полезность identity, но их раннее присваивание
не является готовым контрактом Morphant: у нас должны сохраняться приоритет
`Members`, действие `Explicit`/`Auto()` и lifecycle Using.

## Выход из числового диапазона

Пример: source underlying type — `ushort`, destination — `byte`, вход —
`300`; для AutoMapper предполагается отсутствие совпадающего имени.
Число `300` не представимо в destination. Сам C# при unchecked cast может
дать `44`, при checked — исключение. Enum-cast использует правила
преобразования underlying types. [Спецификация C#](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/conversions#1033-explicit-enumeration-conversions).

| Путь | Вывод из реализации |
|---|---|
| Mapster `ByValue` | `Expression.Convert`, без range guard: числовое сужение может усечь `300` до `44` |
| AutoMapper enum-to-enum после неуспешного TryParse | Тот же unchecked `Expression.Convert`; нет перехода к настраиваемому enum fallback |
| Mapperly `ByValue` | Обычный cast в generated C#, без собственного range guard; при unchecked compilation — `44`, при checked compilation — исключение |
| Mapperly `ByValueCheckDefined` | Сначала cast, затем проверка destination-констант; проверка объявленности не защищает исходное число от усечения |
| AutoMapper.Extensions.EnumMapping `MapByValue` | Поиск в таблице соответствий; для `300` нет default-соответствия в byte destination, поэтому ошибка lookup, а не усечение |

`ExpressionType.Convert` и `ConvertChecked` — разные операции с разной
проверкой переполнения. [Документация .NET](https://learn.microsoft.com/en-us/dotnet/api/system.linq.expressions.expressiontype?view=net-10.0).
У Mapperly [CastMapping](https://github.com/riok/mapperly/blob/f87d48b12a6010a224ca26ad112fb48c07cd6d5d/src/Riok.Mapperly/Descriptors/Mappings/CastMapping.cs)
создаёт обычный cast, а `EnumCastMapping` проверяет уже приведённое значение.
Если destination содержит `Success = 44`, исходное `300` при unchecked
compilation может пройти `ByValueCheckDefined` как `Success`, минуя fallback.
Это конкретный вывод из проверенного emitter, не обещание библиотеки и не
утверждение о результатах запуска опубликованного пакета.

В рассмотренных стандартных enum-путях не найден единый контракт
«непредставимое исходное число всегда означает отсутствие соответствия и
вызывает fallback». Ошибка lookup расширения и проверка уже приведённого
числа Mapperly не эквивалентны такой политике.

## Что дают другие языки

[MapStruct](https://mapstruct.org/documentation/stable/reference/html/#mapping-enum-types)
подтверждает модель «специальные правила, затем совпадение имён, затем
fallback» через `ANY_REMAINING`. Но Java enum содержит только объявленные
экземпляры: аналога произвольного `(E)42` у него нет.
[Java language specification](https://docs.oracle.com/javase/specs/jls/se25/html/jls-8.html#jls-8.9).
Поэтому его строгий enum mapping не отвечает за нас на вопрос о сохранении
неизвестного C# числа или сужении underlying type.

Исторический [Mapperly #482](https://github.com/riok/mapperly/issues/482)
показывает сочетание `ByValue` с отдельными overrides: предложенный switch
сохраняет cast для остальных входов. Текущий builder реализует это направление.
Это свидетельство конкретного сценария, а не опрос пользователей или
доказательство причин первоначального выбора default.

## Рекомендация для Morphant, требующая согласования

1. Для обычных enum `ByValue` переносит любое представимое математическое
   число независимо от объявленности на обеих сторонах. Это соответствует
   обычному числовому пути Mapster и Mapperly и не добавляет скрытой проверки
   допустимости. В отличие от unchecked cast сохраняется уже принятое
   требование Morphant не терять число.
2. Для одинакового enum конвенция сохраняет значение, включая неназванное,
   при обеих стратегиях. Identity действует после специальных правил;
   `Explicit` отключает его неявное применение, явный `Auto()` запрашивает.
3. Непредставимое число означает отсутствие конвенционного соответствия:
   неявный путь переходит к завершающему fallback, без него бросает mapping
   exception. Явный `Auto()` при неудаче бросает исключение без перехода
   к следующей ветке. Пользовательские исключения не перехватываются.

Первые два предложения имеют прямые аналоги у популярных мапперов. Третье —
наш выбор для согласованности с `Members`, а не установленный отраслевой
default. Новая настройка не предлагается. `UnmappedMemberValidation` остаётся
compile-time-проверкой покрытия, отдельно от runtime-допустимости числа.

Следствие предложений: при `ByValue` число в диапазоне, включая неизвестное,
не дойдёт до завершающего `_ => Unknown`. Аналогично при `E → E` неизвестное
значение сохраняется конвенцией. Для закрытого набора пользователь задаёт
явные ограничения; `Explicit` позволяет полностью определить его через
switch. Это реальный компромисс, который нужно согласовать вместе с выбором
смысла `ByValue`, а не скрывать в реализации.

Обсуждение flags, составных масок и побитовых fallback сюда не переносится.
Production API, генератор и тесты Morphant этим исследованием не меняются.
