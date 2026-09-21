# First-class enum mapping: исследование и набросок дизайна

Дата: 2026-09-21. Статус: предложение для обсуждения, не принятый контракт и
не обещание реализации. Названия нового API предварительные. Исходная точка:
Morphant 0.5.0, remote `main` `b77d654d8d255bb89c04f9b9c69cbb1a56c7d7c5`.

## 1. Что дают другие инструменты

Проверены первичные источники; ниже описаны документированные подходы,
а не результаты сравнительного запуска библиотек. Последний столбец —
вывод для Morphant, а не утверждение об устройстве чужого инструмента.

| Инструмент | Подход | Полезная идея |
|---|---|---|
| [AutoMapper.Extensions.EnumMapping](https://docs.automapper.io/en/stable/Enum-Mapping.html) | Отдельное расширение: по числу по умолчанию, по имени опционально; `MapValue`, валидация и специальные правила обратного отображения. | Краткие типизированные overrides. Many-to-one mapping делает автоматический reverse неоднозначным. |
| [Mapster](https://github.com/MapsterMapper/Mapster/wiki/Data-types) | Enum-to-enum по числу по умолчанию; можно выбрать имя. Есть enum/string/numeric conversions и flags. | Полезна единая поддержка скалярных преобразований, но строковый parsing и перенос числа требуют собственных контрактов. |
| [Mapperly](https://mapperly.riok.app/docs/configuration/enum/) | Source generator: `ByValue` по умолчанию, `ByName`, `ByValueCheckDefined`; overrides, fallback, проверка покрытия source/target, строковые naming policies. | Семантика сопоставления, runtime-проверка и compile-time-полнота — разные решения. |
| [MapStruct, Java](https://mapstruct.org/documentation/stable/reference/html/#mapping-enum-types) | По имени; отсутствующее соответствие source — ошибка компиляции. `ANY_REMAINING` сохраняет конвенцию, `ANY_UNMAPPED` отключает её для неописанных случаев; есть преобразования имён. | Проверять эволюцию enum при компиляции. Различать fallback после конвенции и полностью explicit mapping. Java enum не моделирует произвольное C# enum-число. |
| [Chimney, Scala](https://chimney.readthedocs.io/en/stable/supported-transformations/#between-sealedenums) | Сопоставляет варианты sealed/enum по имени, поддерживает явное переименование и вычисляемую обработку варианта; различает total и partial transformations. | Сначала определить полный набор поддержанных входов. Result-based ошибки — отдельный API, не обязательная часть enum feature. Scala enum также может содержать данные. |
| [Serde, Rust](https://serde.rs/variant-attrs.html) | Раздельные имена для сериализации/десериализации; несколько входных `alias` для одного варианта. | Входных текстовых имён может быть несколько, выходное представление должно быть однозначным. |
| [System.Text.Json](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/customize-properties#enums-as-strings) | Числа по умолчанию; строковый converter, naming policy, `JsonStringEnumMemberName`. [Разрешение чисел](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.serialization.jsonstringenumconverter-1.-ctor?view=net-10.0) задаётся отдельно. | Имя CLR, имя в протоколе и допустимость числового представления не должны смешиваться. |
| [Protocol Buffers](https://protobuf.dev/programming-guides/enum/) | Open enum сохраняет неизвестное число; closed enum обрабатывает его через unknown fields. | Сохранение неизвестных значений бывает намеренным требованием совместимости, а не ошибкой. Для него нужен явный режим. |
| [Enums.NET](https://github.com/TylerBrinkley/Enums.NET) | Утилиты для enum/flags; `PrimaryEnumMember` позволяет выбрать основное имя среди дубликатов значения. | Нужна явная канонизация aliases при enum-to-string; порядок объявления не выражает пользовательское намерение. |

Не следует копировать все возможности сразу. Самые полезные общие идеи:
типизированные соответствия, проверка полноты, явная политика неизвестного
значения, отдельная семантика flags и независимые направления преобразования.

## 2. Исходные ограничения Morphant

- Enum сейчас является opaque destination без generated construction/member
  surface: [DestinationCapabilityPolicy](../../src/Morphant.Generator/MappingPair/DestinationCapabilityPolicy.cs).
  Готового enum algorithm нет; [Convert](../api/convert.md) уже позволяет
  описать преобразование целиком. Сам факт допустимости enum как типа пары
  не означает наличия first-class mapping.
- [TypeMapperModelBuilder](../../src/Morphant.Generator/TypeMapperGeneration/TypeMapperModelBuilder.cs)
  различает manual conversion, result policies и object construction.
  Enum algorithm должен формировать скалярный результат, а не конструктор
  enum или фиктивные members.
- [Nested mapping](../nested-mapping.md) всегда явный. `Auto()` копирует
  совместимое значение и не запускает зарегистрированную пару автоматически.
  [MemberTypeCompatibility](../../src/Morphant.Generator/TypeMapperGeneration/MemberTypeCompatibility.cs)
  проверяет implicit C# conversion; между разными enum её нет.
- `IMapper`/`ITypeMapper` и `MappingContext` уже подходят для enum. `Update`
  возвращает итоговое значение; изменять переданный value type по месту нельзя.
- [Null handling](../settings/null-handling.md) уже определён. Null, нулевое
  значение enum и неизвестное ненулевое значение — разные входы.
- Сохраняются [setting precedence](../settings/README.md#precedence),
  полное владение алгоритмом у `Convert` и
  [generator contracts](GENERATOR_CONTRACTS.md): без runtime reflection,
  с простым generated code, изоляцией ошибок и корректной incrementality.

## 3. Направление предложения

Для разных enum предлагается matching по точным именам с compile-time
проверкой source-покрытия. Перенос числа должен включаться явно. Неизвестное
runtime-значение по умолчанию приводит к исключению; fallback задаётся явно.

Вложенные enum-пары по-прежнему вызываются через `Map()`/`Create()`/`Update()`.
Aliases нужно проверять по реальному числовому значению, а flags — по
отображению отдельных битов. Полный набросок API и границ поддержки будет
добавлен после разбора этих случаев.
