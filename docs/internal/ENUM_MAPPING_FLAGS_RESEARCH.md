# Flags enum: сравнение подходов

2026-09-23. Решения приняты; реализация впереди. Канонический контракт и примеры —
[Flags в основном дизайне](ENUM_MAPPING_DESIGN.md#flags); здесь источники и причины выбора.

Проверены документация, исходники и тесты, без сравнительного запуска библиотек.
Дополнительные примеры — вывод из кода. Commits мапперов совпадают с
[числовым исследованием](ENUM_MAPPING_NUMERIC_RESEARCH.md); Enums.NET проверен на
`bdf10cebfa44727abe3dda63218e5410d8034263`. Ветки не приравниваются к NuGet-релизам.

## Как работают мапперы

Сравниваются разные enum-типы; identity E → E не доказывает корректность flags-конвенции.

| Инструмент | Flags-поведение | Ограничение |
|---|---|---|
| Mapperly | ByValue переносит число; ByValueCheckDefined проверяет разрешённые destination-биты; ByName строит switch объявленных значений | Value overrides и ByName не применяются независимо к каждому биту произвольной комбинации |
| Mapster | Default numeric cast; ByName форматирует source в строку и разбирает её, включая списки flags | Неназванная маска может пройти числом даже под ByName; composites влияют на представление |
| AutoMapper, встроенный | ToString → ignore-case Enum.TryParse → numeric cast; строковые списки объединяются | Это не независимый перевод каждого бита по пользовательским overrides; отсутствие имени не запрещает перенос числа |
| AutoMapper.Extensions.EnumMapping | Таблица объявлений и MapValue overrides; lookup всей маски | Read и Write сами не создают соответствие неназванному Read\|Write; без ключа — исключение |

Первичные источники: [Mapperly builder](https://github.com/riok/mapperly/blob/f87d48b12a6010a224ca26ad112fb48c07cd6d5d/src/Riok.Mapperly/Descriptors/MappingBuilders/EnumToEnumMappingBuilder.cs),
[ByName](https://github.com/riok/mapperly/blob/f87d48b12a6010a224ca26ad112fb48c07cd6d5d/src/Riok.Mapperly/Descriptors/Mappings/Enums/EnumNameMapping.cs),
[Mapster adapter](https://github.com/MapsterMapper/Mapster/blob/cbc1e5f04e6e003744b04f83d4705670a85e459b/src/Mapster/Adapters/EnumAdapter.cs),
[AutoMapper mapper](https://github.com/LuckyPennySoftware/AutoMapper/blob/6e8697bc44f02fb54ef6a556a7d134e63485299e/src/AutoMapper/Mappers/EnumToEnumMapper.cs),
[extension table builder](https://github.com/AutoMapper/AutoMapper.Extensions.EnumMapping/blob/6c273e228373afa03fd70c0c099686006d6f47d8/src/AutoMapper.Extensions.EnumMapping/Internal/EnumMappingFeature.cs),
[extension lookup](https://github.com/AutoMapper/AutoMapper.Extensions.EnumMapping/blob/6c273e228373afa03fd70c0c099686006d6f47d8/src/AutoMapper.Extensions.EnumMapping/Internal/CustomMapExpressionFactory.cs).

### Проверка числа и проверка битов различаются

Mapperly для flags destination проверяет `value == (value & allowedMask)`, где
allowedMask — OR **всех** объявленных destination-констант, включая composites.
Source не обязан иметь FlagsAttribute. Проверяется уже приведённое значение:
[EnumCastMapping](https://github.com/riok/mapperly/blob/f87d48b12a6010a224ca26ad112fb48c07cd6d5d/src/Riok.Mapperly/Descriptors/Mappings/Enums/EnumCastMapping.cs),
[FlagsEnumToOtherEnumByValueCheckDefinedShouldCast](https://github.com/riok/mapperly/blob/f87d48b12a6010a224ca26ad112fb48c07cd6d5d/test/Riok.Mapperly.Tests/Mapping/EnumTest.cs).

Отсюда Pair = 3 разрешает 0, 1, 2, 3, а All = -1 — все биты своей ширины.
Это контракт «нет неизвестных битов», более широкий, чем «OR объявленных значений»;
проверка после cast также не защищает от сужения. [PR #510](https://github.com/riok/mapperly/pull/510)
добавил оптимизацию и поддержку flags, но просмотренное обсуждение не доказывает
пользовательский консенсус о предпочтительной строгости.

Enums.NET строит allFlags только из **одиночных** объявленных битов. Без custom
validator его Default принимает их комбинацию либо точное объявление:
[Enums.cs](https://github.com/TylerBrinkley/Enums.NET/blob/bdf10cebfa44727abe3dda63218e5410d8034263/Src/Enums.NET/Enums.cs),
[EnumCache.cs](https://github.com/TylerBrinkley/Enums.NET/blob/bdf10cebfa44727abe3dda63218e5410d8034263/Src/Enums.NET/EnumCache.cs).

[Rust bitflags](https://docs.rs/bitflags/latest/bitflags/index.html) считает известными
биты любых объявленных флагов, предупреждая о composites без самостоятельных битов.
Для внешнего протокола `_ = !0` может намеренно объявить известными все биты.
Объявленность, известные биты и допустимая комбинация — разные критерии;
[Enum.IsDefined](https://learn.microsoft.com/en-us/dotnet/api/system.enum.isdefined?view=net-10.0)
не заменяет выбора между ними.

### Строковый путь зависит от составных имён

Mapster подбирает объявленные значения от больших к меньшим, включая composites;
при неизвестном остатке возвращает число **всей** маски. Parser принимает числа.
[Flag_Enum_Is_Supported](https://github.com/MapsterMapper/Mapster/blob/cbc1e5f04e6e003744b04f83d4705670a85e459b/src/Mapster.Tests/WhenMappingEnums.cs):
6 → Six, 10 → Two, Eight, 7 → число.
[Helper](https://github.com/MapsterMapper/Mapster/blob/cbc1e5f04e6e003744b04f83d4705670a85e459b/src/Mapster.Core/Utils/Enum.cs).

AutoMapper использует стандартные [Enum.ToString](https://learn.microsoft.com/en-us/dotnet/api/system.enum.tostring?view=net-10.0)
и [Enum.TryParse](https://learn.microsoft.com/en-us/dotnet/api/system.enum.tryparse?view=net-10.0).
Его [flags-тест](https://github.com/LuckyPennySoftware/AutoMapper/blob/6e8697bc44f02fb54ef6a556a7d134e63485299e/src/UnitTests/Enumerations.cs)
переводит One|Four|Eight между одинаковыми именами и позициями; переименование
каждого бита пользовательским правилом этим не проверяется.

Следствие: добавление source-имени ReadWrite может заменить строку Read, Write
на ReadWrite. Если destination такого имени не имеет, результат изменится без
изменения отдельных флагов. ByBit Morphant устраняет эту зависимость: имя composite
не меняет разбиение. ByMask намеренно работает с целыми именами без строкового
промежуточного слоя и скрытого numeric fallback.

## Почему выбраны два режима

Одно переименование флага должно действовать во всех комбинациях — поэтому
**ByBit по умолчанию**. Пользователь, которому нужны точные whole-mask cases,
выбирает ByMask. Это самостоятельный вопрос: EnumMappingStrategy определяет
соответствие, MemberSelection — включение конвенции, coverage — диагностику.

Режим един для Members, Auto, fallback и всех уровней IncludeBase. Он устраняет
неоднозначность «override маски или вклад бита» без смешанного двойного прохода
с повторными guards/locals. Числовая стратегия не меняет единицу обработки.
Using остаётся обычной фабрикой полной маски; отдельный callback Flags не нужен.

Для ByMask + ByName выбрано имя **целого** объявленного значения. Отвергнут вариант
дополнительно собирать неизвестные комбинации по именам битов: он потребовал бы
приоритета между whole names, composites и aliases. Цена выбранной простоты —
whole-mask overrides не сочетаются с автоматическим переводом остальных неназванных
комбинаций. Пустая маска имеет отдельный [контракт](ENUM_MAPPING_DESIGN.md#ноль-null-и-начальное-значение).

## Почему строгая целая маска — OR объявлений

Сравнение уже представимых чисел, без пользовательских overrides и range failures:

| Destination; проверяемое число | Все известные биты: Mapperly | Одиночные биты или точное объявление: Enums.NET Default | OR целых объявлений: Morphant |
|---|---|---|---|
| Read = 1, Write = 2; 3 | Да | Да | Да |
| Только Pair = 3; 1 | Да | Нет | Нет |
| Только Pair = 3; 3 | Да | Да | Да |
| Pair = 3, Audit = 4; 7 | Да | Нет | Да |
| Только All = -1; 1 | Да | Нет | Нет |
| Только All = -1; -1 | Да | Да | Да |

Ноль допустим как пустая комбинация во всех трёх моделях. Morphant разрешает
использовать Pair целиком и сочетать его с Audit без объявления каждого OR,
но не извлекать необъявленный одиночный бит. All = -1 не разрешает любое число.
Это собственный контракт, не копирование проверки Mapperly/Enums.NET.

Применение к ByMask и integer-to-flags и алгоритм без перебора подмножеств —
в [дизайне](ENUM_MAPPING_DESIGN.md#строгая-числовая-маска). ByBit проверяет отдельные
биты, поэтому Pair = 3 не разрешает 1 и 2; whole-mask -1 из int в sbyte и
побитовая проверка диапазона также дают разные результаты. Неизвестное число
сохраняет существующий ByValueAllowUndefined, без ещё одной настройки.

## Остальные решения: причины

| Решение | Зачем |
|---|---|
| Zero проходит правила один раз, затем конвенция даёт 0 | Пустая маска не требует объявления, но None можно переопределить; Explicit сохраняет свой смысл |
| Null из битового правила сразу завершает nullable mapping | Отличает отсутствие результата от удаления бита нулём; эффекты следующих битов не выполняются |
| FlagsMappingMode только для flags-to-flags | Не предполагает, что обычный enum, string или integer поддерживает OR результатов |
| Порядок битов от младшего к старшему, в ширине source | Определённый порядок effects, независимый от aliases и объявлений; signed high bit последний |
| Warning только о доказанно недостижимом composite case | Обнаруживает ошибку режима, сохраняя возможность наследовать правила для ByMask; patterns над result/вычисленным выражением не запрещаются |

Подробности не дублируются: [flags-контракт](ENUM_MAPPING_DESIGN.md#flags),
[coverage](ENUM_MAPPING_DESIGN.md#проверка-покрытия),
[проверки реализации](ENUM_MAPPING_DESIGN.md#generated-code-и-критерии-готовности).
Списки flags-имён и обратный parsing вне объёма: canonical composite names,
aliases, порядок, разделитель и неизвестные биты потребуют отдельного контракта.
