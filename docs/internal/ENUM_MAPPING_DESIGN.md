# Enum mapping: согласованный дизайн

Обновлено 2026-11-17. **Feature ещё не реализована.** Это канонический контракт;
[сценарии](ENUM_MAPPING_SCENARIOS.md) содержат конфигурации, входы и результаты.
Обоснования: [default](ENUM_MAPPING_DEFAULT_RESEARCH.md),
[числа](ENUM_MAPPING_NUMERIC_RESEARCH.md), [flags](ENUM_MAPPING_FLAGS_RESEARCH.md).

## Объём и API

Поддерживаются enum ↔ enum, enum ↔ string, enum ↔ integer, flags, aliases,
nullable, Create/Update и IncludeBase. Integer: sbyte, byte, short, ushort,
int, uint, long, ulong. Class/struct, ValueTuple и System.Tuple → enum получают
scalar Members с явным выбором входа; enum → object/tuple сохраняет структурный API.

```csharp
builder.Map<DomainStatus, ApiStatus>()
    .Members(status => status switch
    {
        DomainStatus.Cancelled => ApiStatus.Deleted,
        DomainStatus.Corrupt => throw new InvalidOperationException(),
        _ => ApiStatus.Unknown
    });
```

Специальные ветки выполняются первыми; остальные значения получают конвенцию.
Unknown — fallback после её неуспеха. Без fallback отсутствие результата
даёт EnumMappingException. При default MemberSelection.Auto конвенция работает
и у регистрации без Members.

| API scalar enum-пары | Назначение |
|---|---|
| Members | Декларативные правила значений, включая обычные выражения и управляющие конструкции |
| ConstructUsing / ResolveUsing | Фабрика начального result; сочетается с Members, без Members её результат окончателен |
| [Convert](../api/convert.md) | Обычный callback, владеющий всем алгоритмом и null handling |
| Construct / Resolve | Не генерируются: scalar destination не имеет структурного создания |
| Map, settings, IncludeBase | Существующий API с правилами ниже; новых Values/Flags/MapValue/Inherited нет |

Вне объёма: автоматический reverse, общий numeric string parsing, wire attributes,
naming policies, коллекции, проекции, настройка разделителей.

## Настройки

| Настройка | Значения и library default |
|---|---|
| EnumMappingStrategy | Default, ByName, ByValue, ByValueAllowUndefined; default зависит от фактического входа — таблица ниже |
| FlagsMappingMode | Default, **ByBit**, ByMask; единица Members и способ flags-конвенции |
| UnmappedEnumMemberValidation | Существующий enum UnmappedMemberValidation: Default, None, **Source**, Destination, Strict; compile-time warnings |

```csharp
builder.Map<WireCode, StoredCode>()
    .EnumMappingStrategy(EnumMappingStrategy.ByValue);
```

| Существующая настройка | Контракт |
|---|---|
| MemberSelection.Auto / Explicit | Default Auto включает неявную конвенцию; Explicit отключает только её. Явный Auto доступен всегда |
| UnmappedMemberValidation | Только object/tuple members, прежний default None; независима от enum coverage |
| MappingMode | Default CreateAndUpdate; отключённая операция немедленно бросает |
| NullSourceHandling / NullDestinationHandling | Defaults ReturnNull / Create; zero не является null |
| ConstructorSelection | Scalar: общий default игнорируется, явная pair-настройка, включая Default, даёт MORPH0023 |
| Flattening | Не меняет enum-имена; неверное effective value сохраняет обычную диагностику |
| UnknownDerivedTypeHandling | Не относится к неизвестному enum-числу; scalar-пары не получают derived dispatch |

Нет новых настроек fallback, overflow, runtime validation, ignore-case,
а также Auto(fallback: ...) и Auto(value). Допустимость числа задаёт стратегия,
реакцию на неуспех — Members.

### Наследование и применимость

Для каждой настройки действует общий [precedence](../settings/README.md#precedence):
текущая пара → included пары от ближайшей → текущий mapper → подключённые base
mappers от ближайшего → MSBuild → library default. Базы подключаются через
base.Configure и IncludeBase. Последняя запись уровня побеждает; положение
mapper-level вызова относительно Map не влияет.

Default продолжает поиск, а не сбрасывает наследование. Для строгого режима поверх
ByValueAllowUndefined нужен ByValue, для побитового поверх ByMask — ByBit.
Pair-настройка базы выше настройки текущего mapper; локальная pair-настройка выше обеих.

UnmappedEnumMemberValidation использует свою цепочку и MSBuild-свойство
MorphantUnmappedEnumMemberValidation: Default нигде не подхватывает обычную
UnmappedMemberValidation; отсутствие значения даёт Source. None отключает coverage,
включая унаследованный Strict.

| Направление | EnumMappingStrategy | FlagsMappingMode |
|---|---|---|
| Enum → enum | Все; default ByName, включая E → E | Когда оба enum имеют Flags, включая nullable underlying types |
| Enum → string | Все; default ByName, числовые стратегии дают одинаковый текст | Только flags source под ByName; числовой режим форматирует целую маску |
| Integer → enum | ByValue (default), ByValueAllowUndefined | Неприменим: целое число, для flags destination проверяется допустимость маски |
| Enum → integer | ByValue (default), ByValueAllowUndefined; одинаковый перенос числа с проверкой диапазона | Неприменим |
| String → enum | ByName (default); числовые стратегии неприменимы | Для flags destination: ByBit — токены, ByMask — целая строка |

Явная несовместимая pair-настройка, включая inherited, даёт MORPH0023.
Несовместимый общий default игнорируется **без поиска нижней настройки**:
mapper ByName поверх MSBuild ByValueAllowUndefined для integer → enum оставляет
строгий library default ByValue. Некорректные значения настроек диагностируются.

Using не запускает конвенцию поверх готового результата и не запрещает совместимую
настройку. Convert игнорирует inherited declarative settings; локальные несовместимые
настройки диагностируются. MappingMode и UnknownDerivedTypeHandling остаются
применимыми; локальный Convert несовместим с Members/Using.

### Стратегия для входа конвенции

Стратегия и её library default определяются по **статическому типу входа конвенции**
и destination текущей пары: перед mapping switch — тип его выражения, вне switch —
source. Nullable рассматривается по underlying type; runtime object и совпавший
pattern тип не меняют. Регистрация пары сохраняется, другой mapping не вызывается.

Сначала precedence выбирает настройку и её происхождение; затем таблица выше
применяется к каждому автоматическому пути. Совместимое конкретное значение
сохраняется, включая inherited; при отсутствии подходящего общего значения
выбирается library default для этого пути. Поэтому enum и int в разных путях
могут получить разные defaults, но явная стратегия пары остаётся одной.

Все реальные автоматические пути должны ей соответствовать, включая Auto<T>
и inherited Auto. Guard, fallback и текущий runtime-вход не скрывают несовместимость.
MORPH0023 указывает стратегию, типы, switch/Auto и источник inherited-настройки.

Если автоматических путей нет, достаточно совместимости с зарегистрированной
парой **либо** одним статическим входом mapping switch после композиции.
При Explicit совместимая настройка остаётся неактивной; без Members проверяется
исходная пара. Прямые результаты не получают конвенцию или validation стратегии.

Неподдерживаемая конвенция отличается от несовместимой настройки: для object/tuple,
string → string, integer → string/integer неявный шаг отсутствует, явный Auto
диагностируется. Новых общих преобразований здесь нет.
Примеры — [143–149](ENUM_MAPPING_SCENARIOS.md#стратегия-и-тип-входа-конвенции),
[190–191](ENUM_MAPPING_SCENARIOS.md#совместимые-настройки-и-aliases).

### FlagsMappingMode и выбранный вход

Один effective mode действует на всю пару, включая inherited rules:

| Что определяется | Основание |
|---|---|
| Исходный проход Members | Зарегистрированная пара по таблице применимости |
| Встроенная flags-конвенция | Статический тип выбранного входа и destination; ByBit — биты, ByMask — целая маска |

Object/tuple → enum получает целый source один раз. В request.Access switch
patterns видят полную маску: Read не совпадает с Read \| Write. ByBit-конвенция
переводит её биты сама, без повторения Members. Для пользовательских правил
на каждом бите нужен nested Map в flags-пару.

В request.Text switch patterns видят полную строку. Конвенция в **обоих** режимах
разбирает весь список CLR-имён; ByMask не требует единственного имени. Для правил
на каждом токене нужен nested Map в string → flags пару. Integer-вход всегда целый.

Явный mode допустим для исходного прохода либо flags-преобразования хотя бы одного
выбранного статического входа, включая Explicit без Auto. Иначе — MORPH0023;
неприменимый общий default игнорируется. Если режим нужен только выбранному входу,
Explicit оставляет его неактивным; режим исходного прохода действует и при Explicit.
Без выбранных входов проверяется исходная пара.

Вычисленный вход образует **одну попытку конвенции**: любой непереведённый бит/токен
отменяет её частичный результат. Обычный fallback задаёт результат целиком, Auto
бросает. Внешний Ignore сохраняет единицу исходного Members, поэтому у object/tuple
он сохраняет initial result даже при ByBit-конвенции.
Примеры — [179–184](ENUM_MAPPING_SCENARIOS.md#flags-конвенция-для-выбранного-входа).

## Members: правила и конвенция

Members — декларативный DSL, не runtime delegate из Configure. Конвенция дополняет
только switch, непосредственно задающий правила результата. Вложенный switch
справа от выбранной ветки, пользовательские методы, Using и Convert остаются C#.

### Object/tuple → enum

Scalar Members доступен для class/struct, ValueTuple и System.Tuple, включая
nullable и все четыре callback-формы. Пользователь выбирает вход или готовый результат:

```csharp
builder.Map<Order, ApiStatus>()
    .Members(order => order.Status switch
    {
        DomainStatus.Cancelled => ApiStatus.Deleted
    });
```

Patterns и конвенция получают order.Status; отдельная enum-пара для конвенции
не нужна. Whole-object/tuple patterns допустимы, но не выбирают скрытое enum-поле:
Auto для целого неподдерживаемого входа диагностируется. Для зарегистрированной
пары служит явный Map<ApiStatus>(order.Status). Прямой enum-результат, включая
собранную flags-маску, окончателен. Using, IncludeBase и lifecycle общие;
enum → object/tuple сохраняет структурные Construct/Resolve/Members.
Примеры — [170–175](ENUM_MAPPING_SCENARIOS.md#members-для-objecttuple--enum).

### Где применяется конвенция

| Форма | Поведение |
|---|---|
| Без Members и Using | При Auto — конвенция source после null handling |
| Using без Members | Выбранный по lifecycle результат окончателен |
| Прямое выражение Members | Готовый результат текущей единицы, без конвенции до/после |
| Mapping switch | Специальные правила → при Auto конвенция → ближайший fallback |
| Явный Auto()/Auto<T>() | Запрос конвенции и при Explicit; неуспех немедленно бросает |

Конвенция не ищет зарегистрированную пользовательскую пару. Явный результат
правила не преобразуется и не проверяется повторно.

### Входы и приоритет

| Callback | Дополнительные доступные значения |
|---|---|
| source => rules | Non-null source после null policy |
| (source, previous) => rules | Option исходного non-null destination; None на Create и Update с null destination |
| (source, previous, result) => rules | Реально выбранный начальный non-null destination |
| (source, previous, result, context) => rules | DSL context с Operation, без полного runtime MappingContext |

Для каждой исходной единицы: локальные специальные ветки → специальные ветки
IncludeBase от ближайшей → одна неявная конвенция при Auto и поддерживаемом входе
→ ближайший fallback → EnumMappingException, если результата нет.

**Вход switch вычисляется один раз на своём месте.** Patterns и Auto этого switch
используют то же значение: source, Normalize(source), local или result.
У result switch конвенция имеет направление destination → destination.
Tuple/object не выбирает первый, именованный или enum-компонент; дополнительные
условия можно вынести в guard. Auto вне switch получает текущее source в точке
вызова; типизированная форма меняет только target typing.

### Null выбранного входа

Null сначала участвует в patterns/guards. У иначе поддерживаемого nullable
enum/integer или string-преобразования он означает неуспех конвенции, без
автоматического null-result, нуля или пустой строки:

| Путь | Поведение |
|---|---|
| Неявная конвенция | Обычный fallback; без результата — EnumMappingException |
| Выбранный Auto, включая inherited и вне switch | EnumMappingException немедленно |
| Явное null => null | Terminal null при nullable destination |
| Явное значение/throw | Написанный результат/исключение |

NullSourceHandling относится только к корневому source до Members. Null свойства,
элемента, local или результата метода не повторяет guards, фабрику или destination
policy. Пользовательское присваивание source = null после guards также не
запускает их заново. Nested Map имеет lifecycle своей пары.

Null входа ByBit передаёт управление fallback текущего прохода; null **результата**
завершает всю nullable-операцию. Null не добавляется к enum Source coverage и не
делает неподдерживаемый Auto допустимым. Явные .Value, .Trim() и другие выражения
сохраняют собственные C# исключения. Примеры —
[185–189](ENUM_MAPPING_SCENARIOS.md#null-выбранного-входа-конвенции).

### Ветки, Auto и вычисления

| Запись | Роль |
|---|---|
| Cancelled => Deleted; Corrupt => throw ... | Специальное соответствие/запрет до конвенции |
| Active => Auto() | Явная конвенция; её неуспех не продолжает поиск правил |
| _ when condition => ... | Специальная ветка; false позволяет базе/конвенции обработать тот же вход |
| Завершающий _ => expression или var remaining => expression без guard | Fallback после конвенции, при Explicit — после специальных правил |
| Завершающий _ => Auto() | Один автоматический путь этого switch, без повторной попытки после неуспеха |

Or-patterns, guards, блоки, if/else, switch statements, conditional expressions,
логические условия, locals и неизменяемые aliases сохраняют scopes и short-circuit.
Возвращаемый mapping switch остаётся таковым после переноса в local; вложенный
switch внутри выбранного результата остаётся C#. Требования C# к return-путям
сохраняются. Method group и полный imperative algorithm задаются Using/Convert.

Прямой Members(source => Compute(source)) завершает текущий путь до inherited
rules/конвенции. Вычисления, комментарии и их написанный порядок сохраняются;
независимые вызовы не объединяются. Изменение source до switch меняет вход;
присваивание в guard не меняет уже захваченное значение switch, но видно обычным
выражениям. User exceptions не перехватываются ради fallback.
Дополнение перед fallback — осознанное отличие от
[обычного C# switch](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/switch-expression).

### IncludeBase

Включаются правила **той же пары** по общему
[контракту наследования](../configuration-inheritance.md). Локальный Members
сохраняет inherited overrides; локальный fallback заменяет только ближайший
inherited fallback, включая throw/Auto. Уровень важнее специфичности: локальный
_ when true предшествует именованному case базы; false допускает базовую ветку.
Специальный Auto завершает поиск, завершающий Auto остаётся после всех специальных веток.

| Что наследуется/выбирается | Правило |
|---|---|
| Вход единственной неявной конвенции | Локальный mapping switch выбранного пути; без локального Members — ближайший inherited Members |
| Неподдерживаемый локальный вход | Неявный шаг отсутствует; подходящий вход базы вместо него не ищется |
| Входы базовых правил | Собственные выражения и контексты; локальный Normalize не подменяет source базы |
| Inherited fallback и Auto | Используют вход, locals и pattern bindings своего switch; Auto получает effective settings текущей пары |
| Вычисления уровня | Лениво при достижении, один раз; fallback использует уже полученные значения |
| Result | Один начальный result для всех уровней; выбранная ветка не становится входом следующего уровня |

Поэтому локальная неявная попытка может продолжиться inherited **явным** Auto
с другим входом. Например, локальный Legacy не переводится, но базовый Auto
для исходного Pending возвращает Pending. Без этого fallback было бы исключение;
локальный _ => Auto() заменяет его и бросает для Legacy. Цепочки неявных конвенций нет.

Прямой результат/Auto сохраняет завершение пути; дополнительный switch ради
наследования не создаётся. Исключение базового вычисления сохраняется, даже если
позднейшая локальная конвенция могла бы сработать. Using наследуется для точной пары;
проверки доступности result, конфликтов destination methods/Convert, accessibility
и cross-assembly inheritance остаются общими. Примеры —
[136–142](ENUM_MAPPING_SCENARIOS.md#includebase-и-разные-входы-switch).

## Lifecycle и Using

Порядок: общие guards → начальный destination → terminal-null check → Members
→ результат. [Null source](../settings/null-handling.md) проверяется первым;
Update с null destination остаётся Update. Ноль — имеющийся destination.
Nullable value-type registration точная, underlying pair автоматически не ищется.
ReturnNull для non-nullable enum возвращает default даже без объявленного нуля.

| Метод | Create | Update с destination | Update с null destination, policy Create |
|---|---|---|---|
| ConstructUsing | Callback | Пропущен; result = previous | Callback, previous = None |
| ResolveUsing | Callback, previous = None | Callback с исходным previous | Callback, previous = None |

При NullDestinationHandling.Throw callback не вызывается. Using принимает inline
lambda, method group или delegate; контекстная форма получает MappingContext с Mapper.
Вызов фабрики сохраняется даже при неиспользуемом result: эффекты/исключения
наблюдаемы, её null завершает операцию до Members. Null результата не повторяет policies.

Result доступен только после фабрики либо из non-null Update destination.
Без него диагностируется **чтение**, включая scalar Ignore; неиспользуемый параметр
для доступа к Operation допустим. Нельзя подставлять ноль или запускать конвенцию
заранее. Previous остаётся исходным destination. Замена результата через Members
относится только к scalar mapping; порядок fluent-вызовов смысл не меняет.

При source = Active и фабричном result = Pending:

| Members | Результат |
|---|---|
| result switch { _ => result } | Pending: конвенция result → result; неназванный result сохраняется через fallback |
| result switch { _ => Auto() } | Pending; неназванный result бросает при строгой стратегии |
| source switch { _ => result } | Active при Auto; Pending при Explicit |
| Прямое Normalize(result) | Написанный результат без конвенции |

## Ошибки выполнения

Один `Morphant.Exceptions.EnumMappingException : MappingException` означает
неуспех конвенции или отсутствие scalar-результата: без Members, после исчерпания
правил (включая Explicit и вход без конвенции) либо при неуспехе выбранного Auto.
Успешный fallback исключения не создаёт; unsupported **явный** Auto и несовместимая
стратегия остаются ошибками конфигурации с обычными typed failure stubs.

| Данные ошибки | Контракт |
|---|---|
| Operation, SourceType, DestinationType | Из [MappingException](../../src/Morphant/Exceptions/MappingException.cs): операция и точная зарегистрированная пара, включая Nullable<T>; computed input их не заменяет |
| Проверявшийся вход | Значение со статическим типом выражения: захваченный вход switch или текущий source вне него; у inherited Auto — его собственный вход |
| Причина | Нет имени, недопустимое число, диапазон, неоднозначность, null либо отсутствие правила; пропущенная конвенция не описывается как неуспешная |
| Составная попытка | Весь вход и проблемный бит/токен; исходный бит до Normalize не подставляется вместо computed mask |
| Представление | Enum/integer — точное число и знак, необязательно имя; string — строковое значение; null — null со статическим типом; object/tuple — достаточно типа |

Сообщение не является машинным протоколом. Ради него нельзя повторять getters,
Normalize, guards, фабрики, вызывать пользовательский ToString или исследовать поля.
Runtime reflection не добавляется. Вложенный mapping сообщает свою пару/операцию,
внешний не оборачивает исключение.

User throw/checked cast сохраняют тип и stack trace. NullSourceException,
NullDestinationException, MappingConfigurationException и отключённая операция
сохраняют [общий контракт](../exceptions.md). UnmatchedMappingSwitchException
остаётся у структурного destination; обычные C# switches, Using и Convert —
со своими исключениями. Примеры —
[159–163](ENUM_MAPPING_SCENARIOS.md#ошибки-enum-маппинга).

## Имена и строки

ByName не зависит от culture: **Ordinal, затем только при отсутствии точных
соответствий — OrdinalIgnoreCase**, как при
[подборе параметров конструктора](../api/members.md#constructor-parameters).
Object/tuple matching и регистрозависимые C# patterns этим не меняются.

У enum aliases одного числа неразличимы. Оба этапа выполняются **для всей группы**:
на выбранном этапе одно distinct destination-число означает успех, несколько —
неоднозначность, ни одного — отсутствие соответствия. Несовпавший alias не отменяет
успех; ignore-case кандидат не конкурирует с найденным exact; порядок объявлений
не влияет. Явное правило числа обрабатывает всю группу.

| Source | Destination | Результат ByName |
|---|---|---|
| Ready / READY — разные числа | Ready = 10, READY = 20 | 10 / 20 по точному имени |
| ready | Те же | Неоднозначность ignore-case этапа |
| ready | Ready = READY = 10 | 10 |
| Ready = ready = 1 | Ready = 10, READY = 20 | 10: точное совпадение всей группы найдено |
| Ready = Active = 1 | Ready = 10, Active = 20 | Неоднозначность двух точных чисел |

Неоднозначность — неуспех конвенции, обрабатываемый fallback/EnumMappingException.
Статически известный конфликт на используемом автоматическом пути даёт **Warning**
с входом и кандидатами, независимо от enum coverage. Обычный fallback warning
не снимает; безусловное правило до конвенции устраняет его для обработанного входа.
Explicit без Auto и прямые результаты не предупреждаются лишь из-за aliases.
Severity повышается обычными средствами. Дублирующие/недостижимые C# cases сохраняются.

Для string правило "ready" не устраняет конфликт остальных вариантов регистра,
например "rEaDy". Exact Ready/READY остаются допустимыми.
Примеры группового приоритета —
[192](ENUM_MAPPING_SCENARIOS.md#192-точные-совпадения-выбираются-для-всей-группы-aliases).

| Направление | Конвенция |
|---|---|
| Enum → string, ByName | CLR-имя с исходным регистром; aliases, даже различающиеся только регистром, требуют явного canonical output, иначе неоднозначность |
| Enum → string, числовые стратегии | Десятичный underlying integer с InvariantCulture без группировки; неназванные числа допустимы, ширина и знак точные |
| String → enum | CLR-имена по двум этапам выше; для обычного enum нет автоматических trim, пустоты/нуля, numeric/hex/qualified-name parsing |

Направления независимы; формат/culture/trim задаются выражением или callback.
ByName не имеет скрытого numeric fallback через ToString, кроме нулевой flags-маски.
[Enum.GetName](https://learn.microsoft.com/en-us/dotnet/api/system.enum.getname?view=net-10.0)
не гарантирует canonical alias.

### Строковый ввод flags

Разделители: `,`, `|`, `;`, включая смеси. Токены trim-ятся по whitespace;
внутренние пробелы, tab, перенос строки и `+` не разделяют имена. Quote/escape
протокола нет; имя с разделителем можно обработать целой строкой в ByMask.

| Режим исходной string → flags пары | Members и конвенция |
|---|---|
| ByBit | Правила для каждого обрезанного токена слева направо; конвенция CLR-имени; OR вкладов |
| ByMask | Правила один раз для исходной строки до trim; конвенция разбирает весь список CLR-имён без пользовательских правил на токенах |

Повторы и пустые токены сохраняются **до Members**, включая Explicit. Composite
имя ReadWrite даёт целое объявленное число, не повторные правила Read/Write.
Конвенция токенов "" и "0" даёт 0 даже без None; другой numeric text не разбирается.
Пустые токены равнозначны в любой позиции: "Read,,Write;" даёт Read \| Write,
строка из разделителей — 0. Правила "" => throw/Unknown меняют это поведение.
Различить пустую строку и пустой элемент списка можно проверкой целой строки.

Explicit выключает конвенцию, включая пустоту/0, но сохраняет токенизацию.
Неизвестный/неоднозначный токен в ByBit получает свой fallback; в ByMask отменяет
всю попытку. Например, с "view" => Read и _ => Unknown:
"view|Missing" даёт Read \| Unknown в ByBit, Unknown в ByMask.
Для строки из object/tuple действует [целый внешний проход](#flagsmappingmode-и-выбранный-вход).

### Строковый вывод flags

Под ByName: ByBit соединяет непустые вклады физических битов от младшего к старшему
через `", "`; повторы сохраняются, composite declaration не меняет разбиение.
ByMask возвращает имя **целого объявленного значения**, без сборки неназванной маски.

| Вход | ByBit + ByName | ByMask + ByName |
|---|---|---|
| Read \| Write, объявлен ReadWrite | "Read, Write" | "ReadWrite" |
| Read \| Audit без целого имени | "Read, Audit" | Fallback / исключение |
| Ноль с объявленным None | "None" | "None" |
| Ноль без объявления | "0" | "0" |

Нулевые aliases требуют canonical output: warning и неуспех конвенции, пока
явное правило не разрешит конфликт. Нулевой source проходит правила один раз;
Explicit выключает и нулевую конвенцию. Неизвестный ненулевой вход не становится
числовым текстом. Пустой вклад пропускается; итог из одних пустых вкладов — "",
без имени None или "0". Nullable null-result завершает всю операцию.

Обе числовые стратегии форматируют **всю маску** одной строкой: 3 → "3",
включая неизвестные биты; Members вызывается для маски, FlagsMappingMode неприменим.
Вычисленная маска ByBit + ByName образует один [составной вклад](#составной-вычисленный-вход-в-bybit).

## Числовая конвенция

ByValue требует допустимого **destination**, объявленность source не нужна:
обычный enum — точное объявленное число, flags — правило ниже. ByValueAllowUndefined
допускает любое представимое число; enum → integer тоже сохраняет неназванный source.

Оба режима сохраняют математическое число независимо от consumer checked options:
без усечения, смены знака или промежуточного переполнения. 256 не становится byte 0,
ulong не сужается до long. Выход за диапазон — неуспех конвенции, а не обязательно
OverflowException. Явные checked/unchecked и готовые результаты не перевалидируются.

Для integer → byte enum { Unknown = 0, Active = 1 } с fallback Unknown:

| Вход | ByValue | ByValueAllowUndefined |
|---|---|---|
| 1 | Active | Active |
| 42 | Unknown через fallback | Неназванное 42 |
| 300, -1 | Unknown через fallback | Unknown через fallback |

Без fallback/с Auto неуспех бросает. Для отдельного OverflowException нужен явный
range guard до конвенции, например `_ when (ushort)code > byte.MaxValue => throw ...`.
Завершающий checked/unchecked cast обрабатывает остаток после конвенции.
E → E подчиняется стратегии и правилам: строгий обычный enum не сохраняет
неназванное число; identity-оптимизация допустима только без изменения поведения.

## Flags

System.FlagsAttribute распознаётся при генерации, включая metadata и Nullable<T>.
Числа 1/2/4 без атрибута не делают enum flags. Ниже — flags → flags;
при атрибуте только с одной стороны обрабатывается целое значение.

### ByBit и ByMask

| Свойство | ByBit (default) | ByMask |
|---|---|---|
| Source в Members | Текущий установленный физический бит | Полная маска |
| Выполнение Members/IncludeBase | Для каждого бита, уровни лениво | Один проход уровней |
| Результат | OR вкладов | Выбранное значение целиком |
| Fallback | Весь вклад текущего исходного прохода | Итог операции |
| Composite case над неизменённым source | Не совпадает; доказанная недостижимость предупреждается | Проверяет точную маску |

Биты идут от младшего к старшему в точной ширине source; signed high bit —
последним, без расширения за ширину. Неизвестные биты также проходят правила;
aliases не добавляют проходов. Один бит может дать 0, один/несколько destination-битов;
совпадающие вклады объединяются. Explicit отключает конвенцию, сохраняя обход и OR.
EnumMappingStrategy не меняет единицу flags → flags Members.

ByName в ByBit требует имён отдельных source-битов: одно Pair = 3 не даёт имён
для 1 и 2. ByMask сопоставляет целые объявленные имена; одинаковое ReadWrite может
связать разные числа/наборы битов. Неназванная маска не собирается по отдельным
именам. Whole-mask overrides с побитовым переводом остальных комбинаций требуют
явного алгоритма/Convert; скрытого второго прохода и отдельных настроек режима
Members/конвенции нет. Object/tuple уже имеет целый внешний проход.

Непокрытый бит без fallback прерывает всю операцию: частичный результат не
возвращается, выполненные эффекты сохраняются. Примеры —
[51–62](ENUM_MAPPING_SCENARIOS.md#flags-между-enum).

### Составной вычисленный вход в ByBit

Normalize(flag), local или result может дать полную маску. Patterns проверяют
её целиком, достигнутая конвенция ByBit переводит физические биты **этого** типа
в его ширине/знаковости. Composite name не заменяет обход; ноль обрабатывается
отдельно. Integer-вход остаётся целым числом. Это одинаково для Auto и неявного
шага; пользовательские Members/IncludeBase ради компонентов не повторяются.

Все биты попытки должны перевестись. При неуспехе её частичные OR/строковые вклады
отбрасываются, fallback вычисляется один раз и задаёт весь вклад исходного прохода:

| Fallback | Вклад |
|---|---|
| Unknown / 0 / result | Именно указанное значение, без успешной части конвенции |
| Ignore() | 0 для маски, пустой вклад для строки |
| var remaining => Compute(remaining) | Полный захваченный вход собственного switch, не остаток битов |
| Inherited Auto | Конвенция его собственного входа, без примеси неуспешной попытки |

Без результата либо при неуспехе выбранного Auto — исключение. Успешные вклады
других исходных проходов сохраняются. Поэтому исходный Read \| Missing может дать
Read OR fallback, а один Read, нормализованный в ту же маску, — только fallback.
Вынос нормализации до вызова mapping способен изменить результат; local внутри
прохода — нет. Каждый исходный бит имеет независимые вычисления; фабрика общая.
Примеры — [150–158](ENUM_MAPPING_SCENARIOS.md#составной-вход-bybit-и-вычисления).

### Ноль, null и начальное значение

Нулевой source в обоих режимах проходит правила **один раз**. Если вход конвенции
равен нулю, flags → flags даёт 0 независимо от имён, объявлений и стратегии,
включая ByMask + ByName. Явное нулевое правило приоритетно и не добавляется к
ненулевой маске. При Explicit нужны правило, Auto или fallback.

Null результата любого бита завершает nullable mapping целиком; следующие биты
не вычисляются. Для пропуска вклада служат 0/Ignore. Null source и фабрики следуют
lifecycle. Using получает полную исходную маску/строку один раз, когда требуется;
previous/result — полные исходный/начальный destination, **не накопитель**.
Initial result автоматически не добавляется, отсутствие не заменяется нулём.

### Строгая числовая маска

Для целого flags destination ByValue допускает **OR целых объявленных значений**,
включая пустую комбинацию 0, после проверки диапазона. Это правило ByMask,
integer → flags и обычного enum → flags; source не меняет критерий.

| Объявления destination | Допустимо | Недопустимо |
|---|---|---|
| Read = 1, Write = 2 | 0, 1, 2, 3 | 4 |
| Только Pair = 3 | 0, 3 | 1, 2 |
| Pair = 3, Audit = 4 | 0, 3, 4, 7 | 1, 2, 5, 6 |
| Только All = -1 | 0, -1 | 1 |

Нельзя извлечь необъявленную часть Pair; All = -1 не разрешает любое число.
Это не Enum.IsDefined и не проверка «бит где-то встречается». Без перебора
подмножеств: в ширине destination объединить все объявленные c, целиком
содержащиеся в x, и сравнить OR с x. AllowUndefined разрешает представимое число.

ByBit проверяет каждый бит отдельно: Pair = 3 не разрешает 1/2 даже у result
switch E → E. AllowUndefined допускает их; ByMask проверяет целое 3.
Итоговый OR/явные вклады повторно не проверяются. При narrowing int → sbyte
целая -1 помещается, но отдельный положительный бит 128 уже нет.
Signed high bit сохраняет математическое значение своего типа, не unsigned
промежуточного. Неуспех computed mask отменяет всю попытку, как описано выше.

### Недостижимые composite cases

ByBit даёт Warning только при **доказанной** недостижимости case, сохраняя
возможность наследовать его для ByMask. Учитываются вход, locals, mutation и guards:
Normalize(flag) и полный result могут содержать composite. Методы при генерации
не выполняются; общего запрета нет. Молчаливое игнорирование недостижимого case
недопустимо; severity повышается обычными средствами, C# диагностики сохраняются.

## Проверка покрытия

UnmappedEnumMemberValidation проверяет scalar enum-правила независимо от
object/tuple member coverage и runtime-допустимости. Default **Source**, Warning:
новый объявленный вход требует обработки, дополнительный destination может
предназначаться другому источнику. Severity настраивается обычными средствами.

Проверяется композиция правил, конвенции и fallback, а не изолированные Members
уровней или все возможные результаты исполнения:

| Вход Source coverage | Правило |
|---|---|
| Mapping switch над source/свойством/элементом/local/методом/result | Все объявления статического enum-типа; Nullable<T> — underlying enum, null не является member |
| Без Members или Auto вне switch | Текущее source |
| Whole-object/tuple switch | Enum-свойства и константы внутри patterns не становятся отдельными входами |
| Прямой результат Members | Обрабатывает текущий вход; enum-выражения результата не создают новых Source-проверок |
| Nested Map/Create/Update | Собственная зарегистрированная пара и настройка |

Тела методов/getters не анализируются: Normalize, фактически возвращающий лишь
часть enum, не сужает его объявления. Warning означает неполноту правил типа,
не доказанную достижимость. Разные входы IncludeBase сохраняют собственные значения;
scope диагностики привязан к выбранному входу, пара регистрации не меняется.

| Режим/правило | Проверка |
|---|---|
| Source | Объявленные физические enum-значения обработаны результатом, Ignore, явным throw или успешной конвенцией; aliases — одна группа |
| Destination | Объявленные значения участвуют в соответствиях объявлений/явных правилах; many-to-one допустим |
| Strict / None | Обе стороны / без coverage; configuration/range checks сохраняются |
| Общий fallback, включая throw | Закрывает Source, в том числе для будущих объявлений; отдельной проверки использования fallback нет |
| Auto | Только успешное соответствие доказывает обработку; автоматическое исключение её не доказывает |
| Guard | Неизвестный результат требует учитывать false-путь |
| Динамический результат | Source может быть обработан, destination неизвестен: warning о границе анализа, а не о доказанной недостижимости |

Using не анализируется и не доказывает полноту Members; _ => result обрабатывает
Source, но не перечисляет Destination. Неназванный enum-source не закрывает
Destination coverage через конвенцию, хотя может быть допустим в runtime;
явное правило для него закрывает соответствие.

У string/integer-входа нет enum-объявлений: Source не превращается в Destination.
Destination/Strict проверяет конечную enum-сторону; для enum → string/integer
Source проверяет enum. Все строки, числа и состояния объектов не перечисляются.
None не предотвращает runtime-исключение для входа без результата.

У исходного ByBit и неизменённого входа-бита Source composite покрыт через свои
биты. У полного выбранного входа Read-ветка не покрывает Read \| Write:
whole-mask overrides не участвуют во встроенной конвенции компонентов.
Destination ByBit проверяет участие битов: вклад Read \| Write покрывает Read,
Write и ReadWrite без доказательства каждой достижимой комбинации. Ноль проверяется
отдельно; ByMask — целые объявленные значения. Все маски не перечисляются.

`_ = TargetEnum.Member;` — намеренное исключение **только Destination**, включая
E → E и всю группу aliases. Это настоящий discard отдельным statement верхнего
уровня Members, не переменная `_`; runtime, конвенцию и биты он не меняет.
Source исключается явным правилом, например Source.Member => Ignore().
Примеры — [96–107](ENUM_MAPPING_SCENARIOS.md#coverage-и-намеренные-исключения),
[167–169](ENUM_MAPPING_SCENARIOS.md#независимая-настройка-enum-coverage),
[176–178](ENUM_MAPPING_SCENARIOS.md#coverage-выбранного-enum-входа),
[184](ENUM_MAPPING_SCENARIOS.md#184-coverage-не-переносит-whole-mask-overrides-на-биты-комбинации).

## Вложенное использование

[Nested mapping](../nested-mapping.md) явный: разные enum требуют Map/Create/Update
и доступной точной пары. Auto структурного свойства/элемента требует implicit C#
conversion и не вызывает enum mapping. Same-enum property копируется как прежде;
IMapper, DI и get-only value members не получают обходных путей.

Scalar Members поддерживает Value<T>(value), Map(source), Create(source),
Update(source, destination), включая generic формы. Adaptive Map выбирает Update
при доступном result, иначе Create; это относится и к result фабрики на внешнем
Create. Map без source не применим: имени принимающего member нет.
Update возвращает scalar, не изменяет переданный value type по месту.

## Ignore

Ignore()/Ignore<T>() пропускает **исходную единицу Members**; typed-форма меняет
только target typing:

| Исходный проход | Результат Ignore |
|---|---|
| ByBit: биты/токены → flags | Нулевой вклад в OR, без чтения/необходимости initial result |
| ByBit: биты → string | Пропуск вклада без разделителя |
| Целый scalar, включая ByMask и object/tuple → enum | Начальный result; недоступное чтение диагностируется |

В ByBit nullable-итог из одних Ignore — **non-null** 0/"", без восстановления
initial result, имени нуля или "0". Нулевой source тоже можно игнорировать.
Ignore не очищает биты других вкладов и не сохраняет прежний флаг на Update;
полный initial result добавляется только явным => result.

Выбранный Ignore завершает текущую единицу, следующие обрабатываются.
Специальный Ignore предшествует конвенции, завершающий остаётся fallback;
прямой Members(_ => Ignore()) не запускает конвенцию. IncludeBase использует
исходный проход текущей пары, а ByBit-конвенция из объекта не меняет scalar Ignore.
Lifecycle/effects сохраняются. Source обработан, Destination не перечислен.
Примеры — [164–166](ENUM_MAPPING_SCENARIOS.md#ignore-в-bybit),
[182](ENUM_MAPPING_SCENARIOS.md#182-ignore-сохраняет-начальный-result-внешнего-scalar-прохода).

## Реализация и проверенные предпосылки

Исходный анализ: Morphant 0.5.0,
[main b77d654](https://github.com/strangeman375/Morphant/commit/b77d654d8d255bb89c04f9b9c69cbb1a56c7d7c5).
Enum пока opaque destination; допустимость пары не означает first-class algorithm.
Существующий Convert позволяет ручное преобразование.

| Интеграция | Изменение/граница |
|---|---|
| [DestinationCapabilityPolicy](../../src/Morphant.Generator/MappingPair/DestinationCapabilityPolicy.cs), [TypeMapperModelBuilder](../../src/Morphant.Generator/TypeMapperGeneration/TypeMapperModelBuilder.cs) | Scalar algorithm вместо object construction/member plan |
| [MemberConfigurationEmitter](../../src/Morphant.Generator/MemberSurface/PairConfiguration/MemberConfigurationEmitter.cs) | Четыре delegates с scalar result/rule kind, без фиктивных writable enum fields |
| [PairConfigurationEmitter](../../src/Morphant.Generator/ConstructionSurface/PairConfiguration/PairConfigurationEmitter.cs) | Композиция Using с Members при прежнем lifecycle |
| [MappingSettings](../../src/Morphant.Generator/Settings/MappingSettings.cs), [diagnostic pipeline](../../src/Morphant.Generator/Settings/MappingSettingsDiagnosticPipeline.cs) | Общий resolver/применимость; исходные [selection](../../src/tests/Morphant.Generator.IntegrationTests/TypeMapperMemberTests/MemberSelectionTests.cs) и [inheritance](../../src/tests/Morphant.Generator.IntegrationTests/TypeMapperInheritanceTests/SettingsCompositionTests.cs) tests |
| [MemberTypeCompatibility](../../src/Morphant.Generator/TypeMapperGeneration/MemberTypeCompatibility.cs) | Прежний implicit-conversion контракт nested Auto |

Enum shape: underlying type, constants/aliases, single-bit mask, locations;
ulong не сужается до long. Runtime reflection, Enum.Parse и boxing не нужны.
Open T : Enum без известных полей требует ручного алгоритма или diagnostic.
IMapper/ITypeMapper/MappingContext не требуют нового runtime API.

### Компилятор и типизация

Неполный switch предупреждает уже в Configure: полный generated switch этого
не устраняет; обязательный _ => Auto() противоречит краткому Members.

Интеграционный прототип 2026-09-23 заменяет isolated probes: настоящие
TypeMapper/MappingBuilder, четыре Members delegates, Option/context/Auto из
Morphant.dll; тестовый SG добавляет scalar marker, extensions и suppressor.
Production planner отсутствует.

| Проверка прототипа | Результат |
|---|---|
| SDK 10.0.100, MSBuild, C# 9, nullable, warnings-as-errors | Четыре формы; enum/string/integer/nullable, or/guards/throw, if, switch statement, conditional и local alias без предупреждений |
| Roslyn 4.4.0, C# 9, .NET Standard 2.0 references | Те же формы без исключения CS1701; CS8509/CS8524/CS8846 подавляются только у mapping switches |
| Отрицательные контроли | Ordinary/nested switch, Using, чужой Members сохраняют warnings; wrong type, obsolete, unreachable arm, missing return остаются ошибками при warnings-as-errors |
| Generated Create/Update одной пары | Четыре ожидаемых результата explicit + ByName; два неизвестных входа дают typed mapping exception |
| Дополнительный MSBuild | Nested switch сохраняет CS8509; RunAnalyzersDuringBuild=false в проверенном SDK не отключил suppressor |

Это feasibility, не проверка production, IncludeBase, flags, result dataflow или
полной классификации switches. Rider недоступен, редакторные inspections не проверены.

Nullable reference marker поддерживает natural null/default; генератор проверяет
их по настоящему destination. Conversions AutoMarker/AutoMarker<T> должны принимать
null для default рядом с Auto, иначе CS8604; struct marker ломает natural null.
У var со смесью enum/bare Auto может не хватить target type — нужен Auto<T>().
Bare => 0 не конвертируется в generic scalar marker: подходят Target.None,
default, (Target)0. Эти ограничения приняты; numeric conversions/object/dynamic
ради обхода типизации не добавляются.

[Suppressor](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.diagnostics.diagnosticsuppressor?view=roslyn-dotnet-4.4.0)
распознаёт настоящий DSL symbol, enum rule kind и только дополняемый planner switch,
а не имя Members. Не подавлять глобально, в структурном Members/Construct,
Using/Convert или nested switch; wrong-type/unreachable/obsolete сохраняются.
Object/tuple source допустим у scalar enum Members. Generated switch должен быть
явно полным; enum coverage — отдельная проверка.

### Generated code и критерии готовности

Простой случай: explicit arms → синтезированные соответствия → fallback, без
недостижимых arms. Guards/IncludeBase/смена входа могут требовать вложенного
продолжения; сохраняются единичное вычисление входов и условность результатов.
Нет local на каждый case, конвенции заранее ради result или catch вокруг user code.
Соблюдаются [generator contracts](GENERATOR_CONTRACTS.md): читаемость, scopes,
форматирование, stubs/diagnostics, compatibility manifest, incrementality,
cancellation/recovery; структурный mapping сохраняет контракт.

Production проверяется по [testing guidelines](TESTING_GUIDELINES.md) и всему
[каталогу сценариев](ENUM_MAPPING_SCENARIOS.md); прототип его не заменяет:

| Область | Обязательные сочетания |
|---|---|
| API/lifecycle | Bare и четыре Members формы; scalar/object/tuple/nullable; Create/Update, Update-only; все null policies, terminal null; available/unavailable/unused result; Using method groups/delegates/context и effects |
| Правила/композиция | Auto/Explicit, guards/throws/fallback, direct results, locals/mutation; все IncludeBase уровни/настройки, собственные inputs/bindings, ленивые scopes, общий result, конфликты Convert/Using |
| Стратегии/значения | Фактические типы и разные defaults путей; совместимые неактивные настройки; signed/ulong/range/checked; E → E; ordinal/culture/aliases и групповой exact-first; конечные стороны coverage, guards/dynamic results/discards |
| Flags/строки | ByBit/ByMask, исходный/вычисленный/проектируемый вход; whole fallback и отсутствие повторных правил; OR, ноль/null/Ignore, aliases/composites/Pair/All/signed high bit; separators/trim/empty/repeated tokens, canonical output и порядок effects |
| Ошибки/toolchain | EnumMappingException с исходной парой и фактическим входом без повторных вычислений/ToString; C# 9/minimum Roslyn/MSBuild/IDE, suppressor families/disabled analyzers/warnings-as-errors; edits settings/enums/callbacks, recovery/cancellation |

## Отложенное и проверки реализации

- Настройка разделителей: сейчас вход `,`, `|`, `;`, выход `", "`.
- Rider и production-интеграция; compiler/MSBuild прототип не подтверждает их.
