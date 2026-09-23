# Enum: неизвестные числа, identity и диапазон

Исследование: 2026-09-22. Числовые решения приняты; канонический контракт —
[числовая конвенция](ENUM_MAPPING_DESIGN.md#числовая-конвенция) и
[применимость настроек](ENUM_MAPPING_DESIGN.md#наследование-и-применимость).
Здесь только обычные enum без проекций и заменяющих алгоритм user converters;
[flags исследованы отдельно](ENUM_MAPPING_FLAGS_RESEARCH.md).

Прочитаны документация, исходники и тесты. Библиотеки не запускались: результаты
для 42/300 — вывод из кода, не сравнительный runtime-прогон опубликованных пакетов.
Проверенные ветки не приравниваются к последним NuGet-релизам.

| Исходники | Проверенный commit |
|---|---|
| AutoMapper, main | `6e8697bc44f02fb54ef6a556a7d134e63485299e` |
| Mapster, master | `cbc1e5f04e6e003744b04f83d4705670a85e459b` |
| Mapperly, main | `f87d48b12a6010a224ca26ad112fb48c07cd6d5d` |
| AutoMapper.Extensions.EnumMapping, master | `6c273e228373afa03fd70c0c099686006d6f47d8` |

## Неизвестное число

42 помещается в оба underlying type, но нигде не объявлено; overrides отсутствуют.

| Алгоритм | Результат и основание |
|---|---|
| AutoMapper, встроенный | Сохраняет 42: ToString/TryParse допускают числовой текст, затем возможен cast. [Mapper](https://github.com/LuckyPennySoftware/AutoMapper/blob/6e8697bc44f02fb54ef6a556a7d134e63485299e/src/AutoMapper/Mappers/EnumToEnumMapper.cs) |
| Mapster ByValue | Сохраняет cast-ом без проверки объявленности. [EnumAdapter](https://github.com/MapsterMapper/Mapster/blob/cbc1e5f04e6e003744b04f83d4705670a85e459b/src/Mapster/Adapters/EnumAdapter.cs), [PrimitiveAdapter](https://github.com/MapsterMapper/Mapster/blob/cbc1e5f04e6e003744b04f83d4705670a85e459b/src/Mapster/Adapters/PrimitiveAdapter.cs) |
| Mapperly ByValue | Сохраняет cast-ом; coverage diagnostics не меняют runtime-путь. [EnumTest](https://github.com/riok/mapperly/blob/f87d48b12a6010a224ca26ad112fb48c07cd6d5d/test/Riok.Mapperly.Tests/Mapping/EnumTest.cs) |
| Mapperly ByValueCheckDefined | Проверяет destination, затем fallback/throw. [EnumCastMapping](https://github.com/riok/mapperly/blob/f87d48b12a6010a224ca26ad112fb48c07cd6d5d/src/Riok.Mapperly/Descriptors/Mappings/Enums/EnumCastMapping.cs), [fallback tests](https://github.com/riok/mapperly/blob/f87d48b12a6010a224ca26ad112fb48c07cd6d5d/test/Riok.Mapperly.Tests/Mapping/EnumFallbackValueTest.cs) |
| AutoMapper.Extensions.EnumMapping MapByValue | Нет записи в таблице — исключение. [Table builder](https://github.com/AutoMapper/AutoMapper.Extensions.EnumMapping/blob/6c273e228373afa03fd70c0c099686006d6f47d8/src/AutoMapper.Extensions.EnumMapping/Internal/EnumMappingFeature.cs), [lookup](https://github.com/AutoMapper/AutoMapper.Extensions.EnumMapping/blob/6c273e228373afa03fd70c0c099686006d6f47d8/src/AutoMapper.Extensions.EnumMapping/Internal/CustomMapExpressionFactory.cs) |

Дополнительные границы, существенные для Morphant:

- AutoMapper [ShouldMapEnumWithInvalidValue](https://github.com/LuckyPennySoftware/AutoMapper/blob/6e8697bc44f02fb54ef6a556a7d134e63485299e/src/UnitTests/Enumerations.cs)
  прямо проверяет сохранение необъявленного нуля. Mapperly EnumToOtherEnumByValueShouldCast
  ожидает cast даже при непересекающихся объявлениях и warnings о покрытии.
- Fallback Mapperly документирован для ByName/ByValueCheckDefined. С ByValue
  он диагностируется, а проверенный [builder](https://github.com/riok/mapperly/blob/f87d48b12a6010a224ca26ad112fb48c07cd6d5d/src/Riok.Mapperly/Descriptors/MappingBuilders/EnumToEnumMappingBuilder.cs)
  включает defined-check. [Документация](https://mapperly.riok.app/docs/configuration/enum/).
- Необъявленный source, уже объявленный в destination, Mapperly принимает при
  одинаковой представимости. Расширение AutoMapper строит default-таблицу из
  объявлений обеих сторон; EnableEnumMappingValidation не меняет lookup failure.
- Numeric lookup расширения сравнивает boxed значения собственных underlying
  types через Equals. [EnumValueWithOtherUnderlyingTypeMapping](https://github.com/AutoMapper/AutoMapper.Extensions.EnumMapping/blob/6c273e228373afa03fd70c0c099686006d6f47d8/src/AutoMapper.Extensions.EnumMapping.Tests/EnumValueWithOtherUnderlyingTypeMapping.cs)
  использует byte с обеих сторон; это не доказательство общего сравнения чисел
  между разными ширинами и знаками.

Название ByName само по себе не гарантирует запрета чисел: Mapster для неназванного
source получает числовой текст, затем numeric Parse destination; 42 сохраняется
в диапазоне. Именованный source без совпадения может дать parsing error.
[Helper](https://github.com/MapsterMapper/Mapster/blob/cbc1e5f04e6e003744b04f83d4705670a85e459b/src/Mapster.Core/Utils/Enum.cs).
AutoMapper добавляет cast после неуспешного parsing; Mapperly ByName использует
switch известных соответствий и fallback/throw. Последний принцип выбран Morphant.

## E → E

| Маппер без пользовательского преобразования | Почему сохраняется любое исходное число |
|---|---|
| AutoMapper | [AssignableMapper](https://github.com/LuckyPennySoftware/AutoMapper/blob/6e8697bc44f02fb54ef6a556a7d134e63485299e/src/AutoMapper/Mappers/AssignableMapper.cs) стоит до enum mapper в [registry](https://github.com/LuckyPennySoftware/AutoMapper/blob/6e8697bc44f02fb54ef6a556a7d134e63485299e/src/AutoMapper/Mappers/MapperRegistry.cs) |
| Mapster | PrimitiveAdapter пропускает ConvertType при одинаковых типах, в том числе при ByName |
| Mapperly | Тест EnumToSameEnumShouldAssign ожидает `return source`; [DirectAssignmentMappingBuilder](https://github.com/riok/mapperly/blob/f87d48b12a6010a224ca26ad112fb48c07cd6d5d/src/Riok.Mapperly/Descriptors/MappingBuilders/DirectAssignmentMappingBuilder.cs) предшествует enum builder в [списке](https://github.com/riok/mapperly/blob/f87d48b12a6010a224ca26ad112fb48c07cd6d5d/src/Riok.Mapperly/Descriptors/MappingBuilders/MappingBuilder.cs) |

Явно подключённое расширение AutoMapper использует custom converter с таблицей
и для E → E: неназванный ключ не получает identity. Это вывод из реализации,
не отдельный найденный тест. Раннее identity других мапперов не подходит Morphant:
оно обошло бы стратегию, Members, Explicit/Auto и lifecycle Using.

## Переполнение

Пример ushort → byte, source = 300; у AutoMapper предполагается отсутствие
совпадающего имени. [C# enum cast](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/conversions#1033-explicit-enumeration-conversions)
использует underlying conversions: unchecked может дать 44, checked — исключение.

| Алгоритм | Вывод из реализации |
|---|---|
| Mapster ByValue | Expression.Convert без range guard может усечь 300 до 44 |
| AutoMapper после неуспешного TryParse | Unchecked Expression.Convert; отдельного enum fallback для overflow нет |
| Mapperly ByValue | Cast в generated C#: результат зависит от checked compilation |
| Mapperly ByValueCheckDefined | Сначала cast, затем проверка destination; объявленность не защищает от усечения |
| Расширение AutoMapper MapByValue | Для 300 нет default-соответствия в byte destination: ошибка lookup, не усечение |

[ExpressionType.Convert и ConvertChecked](https://learn.microsoft.com/en-us/dotnet/api/system.linq.expressions.expressiontype?view=net-10.0)
различаются. Mapperly [CastMapping](https://github.com/riok/mapperly/blob/f87d48b12a6010a224ca26ad112fb48c07cd6d5d/src/Riok.Mapperly/Descriptors/Mappings/CastMapping.cs)
даёт обычный cast: при destination Success = 44 исходное 300 под unchecked
может пройти ByValueCheckDefined как Success, минуя fallback.

Единого контракта «непредставимое число всегда означает неуспех конвенции и
передаётся fallback» у исследованных стандартных путей нет. Lookup failure
и проверка уже приведённого числа не заменяют проверку исходного диапазона.

## Что взято в Morphant

Согласованы строгий ByValue с проверкой destination и ByValueAllowUndefined
для сохранения неизвестного числа. Оба сохраняют математическое число; диапазон
проверяется до сужения, overflow обрабатывается Members. E → E соблюдает те же
правила. Defaults, integer-to-enum и enum-to-string описаны один раз в [дизайне](ENUM_MAPPING_DESIGN.md).

Полезные дополнительные свидетельства:

- [MapStruct](https://mapstruct.org/documentation/stable/reference/html/#mapping-enum-types)
  через ANY_REMAINING подтверждает порядок «явные правила → имя → fallback»,
  но [Java enum](https://docs.oracle.com/javase/specs/jls/se25/html/jls-8.html#jls-8.9)
  не имеет произвольного `(E)42` и не решает числовые вопросы C#.
- [Mapperly #482](https://github.com/riok/mapperly/issues/482) показывает потребность
  сочетать ByValue с overrides и cast остатка; текущий builder продолжает этот
  подход. Это сценарий, не опрос и не доказательство причины default.

Преобладание cast в других инструментах не обязывает выбирать ослабленный default
в строгом Morphant. Coverage остаётся независимым от числовой допустимости;
[default ByName](ENUM_MAPPING_DEFAULT_RESEARCH.md) не пересматривается.
