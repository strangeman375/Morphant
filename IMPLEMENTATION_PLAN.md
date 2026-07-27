# План реализации generated mapper-а Morphant

Этот документ хранит текущий согласованный roadmap и состояние работ. Он нужен,
чтобы сохранять направление между рабочими сессиями, но не является
неизменяемой спецификацией. Указанный порядок используется по умолчанию, пока в
ходе реализации не появится причина выбрать более удачный путь.

План можно уточнять и дополнять, а его пункты — разделять, объединять,
переставлять или исключать. Если агент видит основание изменить план, он должен
объяснить предлагаемое изменение и согласовать его с пользователем до того, как
отклоняться от текущего roadmap. После согласования документ обновляется вместе
с изменением направления работ.

Описание каждого пункта задаёт ожидаемую границу следующего полноценного
TDD-среза, но детали могут уточняться перед реализацией. Если объём поддержки
требует отдельного продуктового решения, он согласуется с пользователем до
написания тестов и production-кода. По текущему плану актуализация,
инкрементальность и диагностики остаются в поздней фазе, но это решение также
может быть пересмотрено по общему правилу выше.

После завершения пункта нужно отметить его в этом документе и переставить
указатель следующего среза.

## Следующий срез

**Фаза 3 → Явный конструктор в `Template()`.** Следующий срез добавляет
constructor arguments в template object creation. Точную границу
поддерживаемых аргументов, optional/`params`, порядок их вычисления и
взаимодействие с `MapExisting` нужно согласовать до написания тестов и
production-кода.

## Фаза 1. Контракт generated mapper-а

Статус: завершена.

- [x] Завершить Shape-категорию.

  Проверить форму класса, explicit-интерфейсы и методы, namespaces,
  accessibility, несколько mappings, порядок вывода и отсутствие лишнего
  файла.

- [x] Определить поддерживаемые разновидности mapper-классов.

  Отдельно рассмотреть nested, generic, abstract, sealed и file-local mapper-ы.
  Перед тестами согласовать, что именно поддерживаем.

- [x] Полностью покрыть обнаружение registrations.

  Обычные и expression-bodied `Configure()`, цепочки вызовов, aliases,
  дубликаты, ложные `Map()` из других API.

- [x] Зафиксировать поддержку типов в mapping-парах.

  Nullable-типы, constructed generics, tuples, массивы, вложенные и
  недоступные типы, canonical identity и hint-name collisions.

  Текущая граница: корневые кортежи (`System.ValueTuple` и `System.Tuple`),
  массивы и типы, реализующие `System.Collections.IEnumerable` (кроме
  `string`), не поддерживаются ни как source, ни как destination. Дизайн
  маппинга кортежей и коллекций будет согласован отдельно позже. Делегаты во
  всех корневых формах не поддерживаются как не имеющие осмысленной семантики
  для маппера.

- [x] Зафиксировать публичный контракт `ITypeMapper`.

  Интерфейс и оба режима маппинга документированы. `source` и существующий
  `destination` допускают `null`, а результат помечен как potentially null,
  поскольку эффективные null-handling настройки могут вернуть `null` или
  `default`. Generated explicit implementations повторяют этот nullable-
  контракт и наследуют XML-документацию интерфейса.

- [x] Проверить nullable-контракт template surface.

  Template types сохраняют input-nullability destination members и
  constructor parameters, включая nullable value/reference types,
  `AllowNull`/`DisallowNull` и oblivious-контекст. Внешняя обёртка `Member<T>`
  или `ConstructorMember<T>` допускает `null` только там, где он является
  валидным явно заданным значением; для optional non-nullable constructor
  mappings `null!` остаётся только внутренним omission sentinel.

  Nullable-аннотации параметров и результата `Template()`-лямбд, чья точная
  семантика зависит от effective null-handling settings, намеренно оставлены
  до реализации настроек.

## Фаза 2. Минимальное исполняемое маппирование

- [x] Простейший MapNew.

  Destination без членов и доступный parameterless-конструктор: заменить
  заглушку на `return new Destination();`.

- [x] Простейший MapExisting.

  Для non-nullable non-record class без instance fields/properties вернуть
  переданный экземпляр без изменений. Доступный конструктор и concrete-класс
  для этого не требуются; остальные разновидности destination остаются
  отложены до отдельного пункта ниже.

- [x] Convention mapping обычных свойств.

  Сначала только одинаковое имя и заведомо совместимый тип, без настроек и
  специальных случаев.

  Первый executable-срез закрепил регистрозависимое сопоставление по имени и
  идентичному типу с учётом nullable-аннотации, object initializer для
  `MapNew` и последовательные присваивания для `MapExisting`. Полная текущая
  граница выбора членов описана следующим завершённым пунктом.

- [x] Полная матрица доступности членов.

  Convention mapping поддерживает все четыре комбинации property/field.
  Source-член должен быть доступен generated mapper-у и читаем через корневой
  source-тип: property требует доступный `get`, field может быть mutable или
  readonly. Для destination `MapNew` принимает доступный `set` или `init`,
  включая set-only property, либо mutable field; `MapExisting` исключает
  init-only property, но использует обычные setter-ы и mutable fields.

  Accessibility вычисляется в реальном lexical context generated mapper-а и
  с учётом типа receiver-а. Поэтому учитываются private/protected-доступ,
  internal-граница assembly и referenced types. Члены class/record/struct
  source и class destination наследуются base-first; любое объявление в
  производном типе скрывает одноимённые базовые члены независимо от своей
  пригодности, override выводится один раз. Для interface source выбирается
  единственное most-derived объявление, а unrelated неоднозначные объявления
  не маппятся. Source type parameters используют class/interface constraints,
  включая транзитивные constraints.

  `required` destination-члены разрешают `MapNew`, если все они закрыты
  convention initializer-ом либо parameterless-конструктор помечен
  `[SetsRequiredMembers]`; иначе только `MapNew` остаётся заглушкой.
  Static/const, indexers, ref-return properties, explicit interface
  implementations, fixed buffers, нечитаемые source-члены, get-only
  destination properties и readonly destination fields не участвуют.

  Порядок mappings — base-first и затем порядок destination-деклараций с
  most-derived hiding. Для metadata-типа исходное взаимное чередование fields
  и properties не представлено, поэтому сохраняется детерминированный порядок
  членов, предоставленный Roslyn. Сопоставление по-прежнему требует одинакового
  регистрозависимого имени; правила совместимости типов описаны следующим
  завершённым пунктом. Поддерживаемые разновидности destination этим срезом не
  расширены.

- [x] Совместимость типов выражений.

  Convention member маппится, если выражение присваивания имеет статически
  разрешимое неявное C#-преобразование и не создаёт nullable-warning. Никакие
  дополнительные cast или null-forgiving operator в выражение не вставляются.

  Поддерживаются implicit numeric и lifted nullable conversions, implicit
  reference conversions, inheritance, interfaces, variance, arrays, boxing,
  type parameters, tuple conversions и пользовательские `implicit operator`.
  Коллекция, массив или tuple здесь остаются единым значением члена; корневые
  collection/tuple mappings этим срезом не включаются.

  Boxing по умолчанию остаётся частью convention mapping как обычное
  однозначное implicit-преобразование C#. Возможность потребовать явного
  согласия на boxing будет добавлена отдельной настройкой в фазе поддержки
  настроек; текущий executable-срез ради неё не усложняется.

  Narrowing, downcast, unboxing и другие explicit conversions не выполняются.
  Runtime dynamic conversion также исключается; статические identity/reference
  conversions в `dynamic`/`object` остаются допустимыми. Nullable-совместимость
  проверяется на фактическом выражении в lexical context generated mapper-а,
  поэтому учитываются вложенные annotations, `MaybeNull`/`NotNull`,
  `AllowNull`/`DisallowNull`, nullable generic constraints и oblivious-код.

- [x] Конструкторное маппирование по умолчанию.

  Parameterless и текущая стратегия `Unambiguous`: параметры конструктора,
  optional-параметры и исключение из `MapNew` initializer-а destination-членов,
  уже удовлетворённых переданными constructor parameters.

  `Unambiguous` выбирает единственный доступный поддерживаемый parameterized-
  конструктор, даже при наличии parameterless. Если таких конструкторов
  несколько, `MapNew` пока остаётся заглушкой; если нет ни одного, используется
  доступный parameterless. Конструкторы с `ref`/`out`/`in` или ref-like
  параметрами не участвуют. Неприменимость выбранного конструктора не вызывает
  fallback на parameterless.

  Source-член для параметра выбирается сначала по точному имени, затем по
  единственному `OrdinalIgnoreCase`-совпадению. Аргумент допускается только при
  warning-free implicit C#-преобразовании; вызов с именованными аргументами
  дополнительно связывается Roslyn-ом именно с выбранным конструктором.
  Несовместимый или отсутствующий optional/`params`-параметр опускается,
  обязательный блокирует `MapNew`.

  Фактически переданный constructor parameter удовлетворяет соответствующий
  destination-член: его automatic member mapping исключается только из
  initializer-а `MapNew`. Source-член остаётся доступен другим mappings, а
  `MapExisting` не меняется. Для `required`-члена без
  `[SetsRequiredMembers]` обязательный initializer сохраняется; общее source-
  значение вычисляется один раз и используется и в конструкторе, и в
  initializer-е. `[SetsRequiredMembers]` снимает это требование обычным
  контрактом C#. Кэшированное значение получает читаемое имя от source-члена
  (`sourceId`); коллизии с другими локальными именами и видимыми type parameters
  разрешаются числовым суффиксом (`sourceId1`).

- [x] Разновидности destination.

  Classes, structs, records, nullable structs и остальные формы, для которых
  маппирование имеет осмысленную семантику.

  Concrete class/record class и struct/record struct поддерживают оба режима.
  `MapNew` использует тот же constructor/member planning независимо от record-
  формы; `MapExisting` изменяет переданный reference destination либо локальную
  копию value destination и возвращает результат. Readonly/init-only члены
  участвуют только в `MapNew`. Abstract classes и interfaces не создаются, но
  поддерживают `MapExisting` через доступные mutable-члены. Nullable reference
  destination следует семантике соответствующего reference type. Constructed
  generic destination следует разновидности своего constructed-типа.

  Для nullable struct `MapNew` создаёт underlying value. `MapExisting` явно
  проверяет `destination` на `null`: non-null значение копируется, изменяется и
  возвращается, а null-ветка пока бросает `NotImplementedException` до этапа
  `NullDestinationHandling`. Локальная копия получает читаемое collision-safe
  имя `destinationValue`, при необходимости с числовым суффиксом.

  Destination type parameter поддерживается по constraints. `MapNew` доступен
  при `struct`, `unmanaged` или `new()` constraint. `MapExisting` доступен при
  reference-type или named class/interface constraint; члены берутся из
  class/interface constraints, включая транзитивные. Value-constrained type
  parameter изменяется и возвращается как копия. Unconstrained type parameter
  остаётся заглушкой.

  C# predefined types, enums и согласованный набор direct BCL types остаются
  заглушками до явного `Template()`: создание через `default` и возврат
  существующего значения не считаются convention mapping. Этот список общий
  для generated mapper-а и template surface. Корневые tuples, arrays,
  collections и delegates по-прежнему исключаются более ранней type policy.

## Фаза 3. Template DSL

- [x] Базовый `Template()`.

  Однопараметрическая expression-lambda поддерживает две базовые формы.
  Generated destination принимает `new() { Member = expression }`, где
  target-typed `new()` явно выбирает доступный parameterless-конструктор,
  включая обычный контракт `[SetsRequiredMembers]`. Явные значения закрывают
  соответствующие required-члены.
  Direct destination принимает итоговое expression-значение и использует его
  в обоих режимах маппинга.

  Явные initializer-выражения перекрывают convention mappings и вычисляются в
  порядке initializer-а; оставшиеся convention mappings следуют после них в
  обычном порядке destination-членов. Init-only destination-член применяется
  только в `MapNew`. Lambda-параметр переносится как nullable-safe `source!`,
  а ссылки на типы, static-члены и extension methods переносятся в
  fully-qualified форме, чтобы generated-код не зависел от `using` исходного
  файла. Compile-time результат `nameof` сохраняется строковым литералом.
  Configure-local captures и управляющие формы lambda остаются отложенными.

- [ ] Способы создания destination.

  Явный конструктор, `ByConvention()` и `ByFactory()` — по одному независимому
  срезу.

- [ ] Маркеры членов.

  `Auto()` и `Ignore()`, включая generic-варианты и взаимодействие с convention
  mapping.

- [ ] Вложенный `Map()`.

  Generic и выводимые перегрузки, создание нового вложенного объекта и маппинг
  в существующий.

- [ ] Template с текущим destination.

  Вторая перегрузка `Template((source, destination) => ...)`. Отдельно
  согласовать её отношение к MapNew.

- [ ] Управляющие конструкции DSL.

  Conditional expressions, простые block-lambda, локальные переменные и
  with-overlay.

## Фаза 4. Настройки и композиция

- [ ] `MappingMode`.

  Определить поведение обеих интерфейсных перегрузок для `MapNew`,
  `MapExisting` и `MapNewAndExisting`.

- [ ] Модель эффективных настроек.

  Assembly/root/map-level значения и правила наследования `Default`, пока без
  сложного наследования mapper-ов.

- [ ] Настройка boxing-преобразований.

  По умолчанию сохранять автоматический convention mapping с boxing. Добавить
  строгий режим, в котором требующее boxing преобразование не выбирается по
  convention, но остаётся доступным через явное выражение в `Template()`.
  Точный публичный API, название настройки и границу потенциального boxing для
  generic type parameters согласовать перед реализацией.

- [ ] Настройки выбора членов и конструкторов.

  `MemberMatching` и все стратегии `ConstructorSelection`, каждая как отдельный
  TDD-срез.

- [ ] Null-handling.

  `NullSourceHandling` и `NullDestinationHandling` для reference types,
  nullable/value types и двух режимов маппинга. В этом же срезе уточнить
  nullable-аннотации входов и результата `Template()`-лямбд по их фактической
  семантике после применения effective settings.

- [ ] Наследование конфигурации.

  `base.Configure(builder)`, затем `IncludeBase()` и правила наследования
  root-level/map-level настроек и member mappings.

## Фаза 5. Надёжность и завершение

- [ ] Диагностики и валидация.

  Unsupported DSL, неоднозначные конструкторы, unmapped members, nullability
  mismatch, конфликтующие registrations. Оставить поздним этапом, как и было
  согласовано.

- [ ] Актуализация generated mapper-а.

  Добавление, изменение и удаление mapper-ов, mappings, templates, типов и
  references.

- [ ] Инкрементальность.

  Кэширование, точечная инвалидизация отдельных mappings и отсутствие
  глобальной перестройки при нерелевантных изменениях.

- [ ] Интеграционный срез.

  Обновить sample, проверить реальное выполнение generated mapper-а и
  определить границу со следующим большим этапом — runtime-фасадом `IMapper`.
