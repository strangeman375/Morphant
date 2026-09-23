# Enum mapping: выбор конвенции и полезные подходы

Исследование: 2026-09-22. Пользователь принял `ByName` по умолчанию для
enum-to-enum, включая E → E, и enum-to-string. Числовой default integer-to-enum
выбран отдельно. Контракты и применимость — в [дизайне](ENUM_MAPPING_DESIGN.md#настройки).

Проверены первичные источники и исторические issues; сравнительных запусков
и benchmarks не было. Выводы о Morphant — наш анализ, не статистика предпочтений
пользователей и не приписанная авторам других библиотек мотивация.

## Что делают мапперы

Здесь сравниваются **разные enum-типы**; [E → E и числа](ENUM_MAPPING_NUMERIC_RESEARCH.md)
и [flags](ENUM_MAPPING_FLAGS_RESEARCH.md) имеют отдельные исследования.

| Инструмент | Default и существенные возможности | Первичный источник |
|---|---|---|
| AutoMapper, встроенный mapper | ToString → ignore-case TryParse → numeric cast; parsing допускает числовой текст. Это не чистый ByValue | [EnumToEnumMapper](https://github.com/LuckyPennySoftware/AutoMapper/blob/main/src/AutoMapper/Mappers/EnumToEnumMapper.cs) |
| AutoMapper.Extensions.EnumMapping | ByValue; явный ByName, MapValue overrides, validation, специальные правила reverse | [Документация](https://docs.automapper.io/en/stable/Enum-Mapping.html) |
| Mapster | ByValue; ByName через строку/parsing; enum/string/numeric conversions и flags. Enum-to-string — отдельный путь, не выбор этой стратегии | [Типы](https://github.com/MapsterMapper/Mapster/wiki/Data-types#enums), [adapter](https://github.com/MapsterMapper/Mapster/blob/master/src/Mapster/Adapters/EnumAdapter.cs) |
| Mapperly | ByValue; ByName, ByValueCheckDefined, overrides/fallback, source/target coverage, отдельная EnumNamingStrategy | [Enum mapping](https://mapperly.riok.app/docs/configuration/enum/) |
| MapStruct, Java | По имени; непокрытый source требует explicit правила или даёт compile-time ошибку. ANY_REMAINING сохраняет конвенцию, ANY_UNMAPPED её обходит; есть naming transformations | [Reference guide](https://mapstruct.org/documentation/stable/reference/html/#mapping-enum-types) |
| Chimney, Scala | Sealed/enum варианты по именам; explicit rename/computed handler; source должен быть покрыт, destination может иметь дополнительные варианты; total/partial transformations | [Supported transformations](https://chimney.readthedocs.io/en/stable/supported-transformations/#between-sealedenums) |

Единого default даже внутри C# нет. Встроенный AutoMapper и его расширение —
разные алгоритмы. Java/Scala полезны для сопоставления смысловых вариантов,
но не моделируют произвольное C# enum-число; Scala-варианты могут содержать данные.
Many-to-one overrides также делают автоматический reverse неоднозначным.

## Что показали issues

| Источник | Сценарий и вывод |
|---|---|
| [Mapperly #1581, 2024](https://github.com/riok/mapperly/issues/1581) | Запрос default ByName: gRPC enum начинается с Unknown, domain — с User, и числовой mapping меняет смысл в цепочке gRPC → DTO → domain → projection → REST. [Автор](https://github.com/riok/mapperly/issues/1581#issuecomment-2458889985) оценивает долю ByValue в своих проектах примерно в 60%; собеседник описывает противоположный опыт и общий default для множества проектов. Это два сценария, не опрос сообщества |
| [AutoMapper #3562, 2021](https://github.com/LuckyPennySoftware/AutoMapper/issues/3562) | Пользователь предлагает убрать запутывающий numeric fallback после несовпадения имён. Issue закрыт, проверенная реализация fallback сохраняет; запрос не стал контрактом библиотеки |
| [Mapster #402](https://github.com/MapsterMapper/Mapster/issues/402), [обсуждение](https://github.com/MapsterMapper/Mapster/issues/402#issuecomment-1509985039) | Стратегию задали на содержащем объекте, ожидая влияния на enum. Ответы объясняют enum pair / global default. Это проблема scope, не свидетельство популярности стратегии |
| [Mapperly #325](https://github.com/riok/mapperly/issues/325) | Запрос compile-time полноты и diagnostics с настраиваемой severity. Coverage полезен отдельно от выбора имени или числа и не обнаруживает смысловую ошибку уже полного mapping |

Исторического объяснения выбора default у Mapster и расширения AutoMapper
в просмотренных источниках не найдено. Простота cast и числовые протоколы —
аргументы анализа, не подтверждённая мотивация авторов. Количество issues
не измеряет долю пользователей стратегии.

## Почему Morphant выбирает ByName

Собственный пример перестановки кодов, а не результат запуска будущего feature:

| Source | Destination с тем же именем | ByName | ByValue |
|---|---|---|---|
| Draft = 0 | Draft = 2 | Draft | Active |
| Active = 1 | Active = 0 | Active | Cancelled |
| Cancelled = 2 | Cancelled = 1 | Cancelled | Draft |

Обе стратегии дают объявленные значения и полное coverage, но при одинаковом
смысле имён только ByName верен. Неизвестное имя заметно через fallback/throw;
случайно совпавший код может дать правдоподобный неверный статус. Blanket fallback
способен скрыть изменения и при ByName.

Обратный сценарий: Active = 10 и STATUS_ACTIVE = 10 обозначают один протокольный
код. ByValue полезнее списка overrides; имена могут меняться при стабильном коде.
ByName не является универсальным доказательством смысловой совместимости.

Решение для Morphant опирается на четыре свойства:

- Конвенции уже используют имена, включая tuples без позиционного подбора.
- Members описывает исключения; независимая нумерация domain/API не должна
  заставлять перечислять одинаковые имена.
- Числовой контракт выбирается явно для пары, mapper или MSBuild; scope
  настройки не меняет явный nested mapping. У нового API нет legacy default.
- Стратегия влияет также на enum-to-string: Active = 1 превращается в `"Active"`
  либо `"1"`. Числовой default других мапперов нельзя переносить без этого эффекта.

Атрибут Flags не переключает критерий: имя бита может сохранять смысл при иной
позиции; режим обработки выбирается отдельно. Скорость без benchmarks не решает
спор: Morphant может генерировать ByName switch без строк, а строгий числовой путь
не всегда сводится к cast. Numeric fallback под ByName не вводится: он поглотил бы
случай, для которого пользователь ожидает свой fallback или исключение.

## Идеи за пределами мапперов

Эти источники расширяют пространство решений, но не добавляют обещаний в scope.

| Инструмент | Полезное различие |
|---|---|
| [Serde](https://serde.rs/variant-attrs.html) | Разные имена при сериализации/десериализации и несколько входных aliases: входов может быть много, canonical output должен быть однозначным |
| [System.Text.Json](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/customize-properties#enums-as-strings) | Числа по умолчанию; string converter, naming policy, JsonStringEnumMemberName; [разрешение чисел](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.serialization.jsonstringenumconverter-1.-ctor?view=net-10.0) отдельно. CLR-имя, wire name и числовая допустимость — разные вопросы |
| [Protocol Buffers](https://protobuf.dev/programming-guides/enum/) | Open enum сохраняет неизвестный код; closed обрабатывает его через unknown fields. Сохранение числа может быть требованием совместимости |
| [Enums.NET](https://github.com/TylerBrinkley/Enums.NET) | PrimaryEnumMember явно выбирает canonical alias; порядок объявлений не выражает намерение |

Для первой версии достаточно типизированных overrides, независимых направлений,
явной числовой строгости и существующего coverage. Result-based ошибки, wire
names и автоматический reverse остаются отдельными возможностями.
