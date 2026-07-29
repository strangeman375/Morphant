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

**Фаза 3 → Statement-level управляющие конструкции `Template`.** Следующий
самостоятельный срез определяет поддержку `if`, `switch`, циклов,
присваиваний, нескольких `return`, объявлений local functions внутри
`Template` lambda и общего control flow. Точную границу нужно отдельно
согласовать до тестов и production-кода.

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
  Из Configure-контекста переносятся compile-time constants. Обычные locals,
  `builder` и любые local functions из `Configure()` не переносятся;
  переиспользуемая логика выносится в обычный instance/static метод mapper-а.
  Общая граница block-lambda и управляющих форм описана отдельным завершённым
  пунктом ниже.

- [x] Явный конструктор в `Template()`.

  Поддерживаются positional, named и mixed constructor arguments, включая
  перестановку named arguments. Аргументы вычисляются слева направо в порядке
  записи, затем вызывается выбранный destination-конструктор, после чего идут
  explicit initializer members и оставшиеся convention members.

  Optional-параметры можно опускать; применяется default соответствующего
  destination-конструктора. `params` поддерживает omission либо передачу
  массива целиком, но не expanded-форму. Синтаксически присутствующий `null`
  остаётся явно переданным аргументом. Если все optional/`params` параметры
  опущены, `new()` выбирает parameterless-конструктор при его наличии, иначе
  применимый optional-конструктор по обычным правилам overload resolution C#.

  Перегрузка выбирается компиляторным probe с теми же
  `ConstructorMember<T>`-сигнатурами, что у template type. Поэтому
  positional/named binding, optional tie-breaking и неоднозначности не
  воспроизводятся вручную. Явный `ConstructorMember<T>` cast выбирает нужную
  template-перегрузку и переносится в generated mapper как cast к фактическому
  типу destination-параметра.

  Каждый параметр выбранного destination-конструктора исключает
  соответствующий automatic member только из `MapNew`, в том числе когда
  optional/`params` argument был опущен. Explicit initializer сохраняется
  всегда; required initializer без `[SetsRequiredMembers]` также сохраняется.
  В `MapExisting` constructor arguments вообще не вычисляются: применяются
  только initializer и convention mappings.

- [x] `ByConvention()` в `Template()`.

  Поддерживаются bare-маркер и объект с явными constructor-member mappings:
  `new(ByConvention())` и
  `new(ByConvention(), new() { id = source.ExternalId })`. Параметры
  специального template-конструктора сохраняют обычное C#-связывание, включая
  named arguments и их перестановку.

  Сначала текущая стратегия `Unambiguous` выбирает destination-конструктор.
  Явные mappings не участвуют в выборе перегрузки, а перекрывают automatic
  mapping параметров уже выбранного конструктора. Остальные параметры
  маппятся convention либо опускаются, если они optional/`params`.
  `Map()` внутри constructor mappings остаётся для отдельного среза вложенного
  маппинга.

  Явные constructor mappings вычисляются в порядке записи, затем automatic
  arguments — в порядке параметров, после чего вызывается конструктор. Если
  automatic-значение одновременно требуется для `required` initializer-а,
  предшествующие аргументы вычисляются в локальные переменные в том же порядке,
  поэтому повторное использование не меняет C#-семантику вычислений. Далее
  выполняются внешний explicit initializer и оставшиеся convention members.

  Фактически переданный constructor parameter исключает соответствующий
  automatic member только из `MapNew`. Внешний explicit initializer
  сохраняется; required initializer без `[SetsRequiredMembers]` также
  сохраняется. В `MapExisting` маркер, объект constructor mappings и все их
  выражения полностью игнорируются: работают только внешний initializer и
  convention mapping. Для abstract class и interface `MapNew` остаётся
  заглушкой, а `MapExisting` поддерживается.

- [x] `ByFactory()` в `Template()`.

  Поддерживается inline expression-lambda `() => expression`. Factory-
  выражение может использовать source, instance/static members mapper-а и
  static API; ссылки переносятся в generated-код по тем же правилам, что и
  остальные explicit expressions. `IByFactoryMarker<out TDestination>`
  ковариантен, поэтому interface/base destination не требует явного generic-
  аргумента:
  `new(ByFactory(() => new ConcreteDestination()))`.

  В `MapNew` factory вычисляется ровно один раз и сохраняется в переменную
  заявленного destination-типа. После неё в порядке DSL выполняются explicit
  assignable members, затем оставшиеся convention members. Factory отвечает
  за создание объекта, required- и init-only-члены. Explicit init-only member
  во внешнем initializer-е пропускается для factory-created объекта, но не
  делает весь `MapNew` неподдерживаемым. Автоматической null-проверки,
  подстановки нового destination или иной обработки результата factory пока
  нет.

  В `MapExisting` factory и всё её выражение полностью игнорируются; работают
  только assignable outer initializer и convention mappings.

- [x] Маркеры членов.

  Поддерживаются прямые вызовы generic и non-generic вариантов `Auto()` и
  `Ignore()`; обе формы каждого маркера имеют одинаковую семантику. Marker
  распознаётся по фактическому API `TypeMapper`, а не по имени метода.
  Conditional expressions и локальные marker-значения поддерживаются в
  завершённом срезе управляющих конструкций ниже.

  Во внешнем initializer-е `Auto()` явно запрашивает обычный automatic mapping
  с теми же правилами имени, доступности и совместимости. Explicit expressions
  и `Auto()` выполняются в общем порядке записи; полностью неуказанные
  convention members добавляются после них в обычном порядке destination-
  членов. `Ignore()` исключает member из `MapNew` и `MapExisting`. Init-only
  member по-прежнему применим только при создании destination; после
  `ByFactory()` он не присваивается. Required-члены сохраняют обычные правила
  выбранного способа создания, включая ответственность factory и контракт
  `[SetsRequiredMembers]`.

  В явном destination-конструкторе `Auto()` занимает место аргумента в
  записанном пользователем порядке. Совместимое source-значение выбирается по
  обычным constructor-convention правилам. Если оно одновременно требуется
  для обязательного required initializer-а, значение вычисляется один раз и
  переиспользуется без изменения порядка предшествующих аргументов.

  Constructor `Ignore()` затрагивает только параметр: optional/`params`
  argument опускается, а одноимённый writable destination member остаётся
  доступен отдельному outer/convention mapping. Required-параметр либо omission,
  меняющий выбранную перегрузку, делает только `MapNew` неподдерживаемым;
  искусственный `default` не подставляется.

  В `ByConvention(..., members)` explicit constructor expressions по-прежнему
  вычисляются первыми в порядке записи, а `Auto()` возвращает параметр в
  automatic-фазу и порядок параметров. `Ignore()` опускает только разрешённый
  параметр. В `MapExisting` вся constructor-часть и её выражения игнорируются.

- [x] Вложенный `Map()`.

  Поддерживаются прямые вызовы четырёх форм:
  `Map(source)`, `Map(source, destination)`, `Map<T>(source)` и
  `Map<T>(source, destination)`. Форма без существующего destination всегда
  вызывает вложенный `MapNew`, а форма с ним — вложенный `MapExisting` и
  присваивает возвращённый результат обратно. Это не зависит от режима
  внешнего mapping-а.

  Source-тип mapping-пары определяется по статическому типу первого аргумента.
  Destination берётся из целевого member/constructor parameter для non-generic
  формы либо из явно указанного `T`; runtime-типы аргументов на выбор пары не
  влияют. Nullable-аннотации сохраняются, включая method-return expressions,
  nullable generic arguments и типизированный `null`. `Map(null)` без
  типизирующего cast остаётся неподдерживаемым.

  Все вложенные вызовы делегируются в
  `context.Mapper.Map<TSource, TDestination>(...)` без внешних null-проверок,
  подстановок или обработки результата. Для existing-формы порядок
  вычисления named arguments сохраняется даже при перестановке
  `destination:` перед `source:`.

  Внешние initializer mappings работают в уже согласованном порядке Template
  в обоих режимах и после factory creation. Explicit destination-constructor
  и `ByConvention(..., members)` mappings работают только в `MapNew`, как и
  остальная constructor-часть. Настоящий marker распознаётся по API
  `TypeMapper`; одноимённые пользовательские методы остаются обычными
  expressions.

  Generic existing-overload принимает второй аргумент как `object?`, поэтому
  C# не может случайно вывести `T` из существующего destination:
  `Map(source, destination)` остаётся target-inferred формой, а `T` задаётся
  только явно написанным `Map<T>(...)`.

  Conditional expressions и локальные marker-значения для `Map()` поддержаны
  завершённым срезом управляющих конструкций ниже. Произвольное вложение
  marker-а в обычное вычисляемое выражение по-прежнему не поддерживается.

- [x] Template с предыдущим destination.

  Поддерживается вторая expression-lambda перегрузка
  `Template((source, destination) => ...)`. Параметр `destination` означает
  предыдущее состояние: в `MapNew` каждое его использование заменяется на
  типизированный `default` (`null` для reference destination, нулевое значение
  для struct), а в `MapExisting` — на переданный destination. Direct-template
  expression выполняется в обоих режимах с соответствующим значением.

  В `MapNew` parameterless/explicit constructor, `ByConvention()` и
  `ByFactory()` сохраняют обычную семантику создания, но все их ссылки на
  destination получают `default`. В `MapExisting` construction-часть и её
  выражения не вычисляются. Init-only outer member также применяется только в
  `MapNew`.

  В `MapExisting` все explicit assignable member expressions сначала
  вычисляются в порядке template initializer-а и сохраняются в типизированные
  collision-safe локальные переменные. Только после этого выполняются внешние
  присваивания в полном пользовательском порядке, включая `Auto()`, а затем
  оставшиеся convention mappings. Поэтому более позднее explicit-выражение
  видит предыдущее состояние destination, а не результат более раннего
  mapping-а. `Ignore()` по-прежнему исключает member из обоих режимов.

  Правило действует для reference, value и nullable value destinations.
  Nullable struct сохраняет текущую раннюю null-проверку и изменяемую локальную
  копию. Вложенный `Map()` отдельно переносится для каждого внешнего режима:
  его source и existing-destination arguments также получают `default` либо
  переданный destination согласно описанному правилу.

- [x] Управляющие конструкции DSL.

  Поддерживается block-lambda из последовательных объявлений локальных
  переменных и одного финального `return`. Direct templates и object templates
  используют одинаковую модель locals. Обычная локальная переменная
  вычисляется ровно один раз, в порядке объявления и только в тех generated-
  режимах, где от неё зависит итоговый mapping. Транзитивные зависимости
  сохраняются. Пользовательское имя сохраняется отдельно в `MapNew` и
  `MapExisting`; числовой суффикс добавляется только при реальном конфликте в
  соответствующем generated-методе.

  Conditional expression `?:` может выбирать целый template и способ создания,
  explicit member/constructor value, `Auto()`, `Ignore()`, `Map()` либо
  локальное DSL-значение. Ветви планируются независимо для `MapNew` и
  `MapExisting`: construction-only условия и locals исчезают из
  `MapExisting`, а одинаковые ветви сворачиваются без вычисления условия.
  Условия, влияющие на member mappings, выполняются до первых присваиваний.
  Значения, читающие предыдущий destination, по-прежнему фиксируются в locals
  до его изменения. Conditional `Ignore()` для required/init-only member
  оставляет недопустимую ветвь `MapNew` неподдерживаемой по обычным правилам
  required construction.

  Если ветви отличаются только значениями аргументов одного и того же
  destination-конструктора, условные значения выносятся в типизированные
  locals, а constructor call и `return` генерируются один раз. Все аргументы
  до последнего условного также вычисляются через locals, чтобы сохранить
  исходный порядок и однократность вычислений.

  `with` сохраняет способ создания базового template и накладывает outer member
  mappings. Более поздний overlay заменяет прежнее правило того же member;
  заменённое выражение не попадает в generated-код. Overlay применяется к
  каждой ветви условного базового template.

  Условные значения поддерживаются как во внешнем initializer-е, так и в
  аргументах явного конструктора и в
  `ByConvention(..., constructorMembers)`. Constructor/factory expressions
  вычисляются только в `MapNew`; compiler probes получают необходимые обычные
  locals, не разворачивая их в итоговом коде.

  Statement-level control flow вынесен в отдельный будущий этап ниже. До его
  реализации и фазы диагностик генератор успешно создаёт mapper для
  неподдерживаемого block, но обе generated-перегрузки `Map` бросают
  `NotSupportedException` при вызове. Такой ввод не маскируется fallback-ом на
  convention mapping и не приводит к исключению внутри самого генератора.

- [x] Расширенные формы factory.

  Inline lambda из `ByFactory` не раскладывается на собственную модель
  statements и expressions. Генератор переносит её expression-body либо весь
  block в generated local function внутри `MapNew`; обычный C#-компилятор
  обрабатывает ветвления, циклы, несколько `return`, `throw`, вложенные local
  functions и остальные допустимые синхронные конструкции. Генератор только
  перепривязывает имена и внешние зависимости, не воспроизводя control flow.

  Source и предыдущий destination передаются local function как параметры, а
  нужные Template-locals захватываются из `MapNew` после однократного
  вычисления. Factory helper вызывается ровно один раз, после чего применяются
  assignable explicit/convention members. Ни helper, ни construction-only
  Template-locals не попадают в `MapExisting`.

  `ByFactory` дополнительно принимает method group и заранее созданный
  `Func<TDestination>` из поля или свойства mapper-а. Они вызываются
  непосредственно в выражении создания destination без generated helper-а.
  Явное преобразование к исходному `Func<TDestination>` сохраняет target
  typing, overload resolution и семантику value-type receiver-а. Между
  mapping-вызовами receiver или delegate не кешируется.

  Переносимые factory-выражения могут использовать source, предыдущее
  destination, Template-locals, instance/static members mapper-а, static API и
  Configure-local compile-time constants. Обычные Configure-locals, параметр
  `builder` и все local functions, объявленные в `Configure()`, не
  переносятся; переиспользуемую логику пользователь выносит в обычный
  instance/static метод mapper-а. Local functions, объявленные внутри самого
  factory block, переносятся автоматически вместе с телом. Если
  неподдерживаемая зависимость находится только внутри factory, generated
  `MapNew` бросает `NotSupportedException`, а независимый `MapExisting`
  сохраняется.

  Константы подставляются и в обычные Template-выражения. Набор statement-level
  конструкций у `Template` и `ByFactory` намеренно больше не связан:
  `Template` остаётся анализируемым DSL, а factory body передаётся
  C#-компилятору как обычный runtime-код.

  Discovery допускает local declarations и local function declarations рядом
  с прямыми builder-chain, но по-прежнему не следует за aliases, delegates,
  helper/local-function calls или иным непрямым кодом регистрации.

- [ ] Statement-level управляющие конструкции `Template`.

  Отдельно определить и реализовать statement-level `if` и `switch`, циклы,
  присваивания, объявления local functions внутри `Template` lambda,
  несколько `return` и прочий общий control flow. В этот же этап входит
  DSL-shaping `switch` expression. Обычный
  switch-expression внутри уже поддерживаемого explicit C#-значения не меняет
  семантику Template и остаётся обычным переносимым выражением.

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
  mismatch, конфликтующие registrations. Отдельно сообщать о явно заданном
  init-only member, который не может быть применён после `ByFactory()` в
  `MapNew` и не может присваиваться в `MapExisting`. Оставить поздним этапом,
  как и было согласовано.

- [ ] Актуализация generated mapper-а.

  Добавление, изменение и удаление mapper-ов, mappings, templates, типов и
  references.

- [ ] Инкрементальность.

  Кэширование, точечная инвалидизация отдельных mappings и отсутствие
  глобальной перестройки при нерелевантных изменениях.

- [ ] Интеграционный срез.

  Обновить sample, проверить реальное выполнение generated mapper-а и
  определить границу со следующим большим этапом — runtime-фасадом `IMapper`.
