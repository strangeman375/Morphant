# Исследование удаления compile-time DSL-артефактов из сборок

Статус: исследование зафиксировано 2026-09-04; реализация отложена. Документ
не задаёт выпущенный public API. Он сохраняет мотивацию, измерения,
рассмотренные варианты, выбранное рабочее направление, необходимые safety
checks и критерии готовности, чтобы при возвращении к feature не повторять
исследование с нуля.

Предпочтительное направление на момент отсрочки — узкий post-compile cleaner,
который работает по сгенерированному versioned manifest, удаляет только явно
помеченную compile-time поверхность и завершается ошибкой при любой
неизвестной связи. До изменения PE/metadata первым этапом должен быть
audit-only prototype.

## 1. Проблема и целевой инвариант

Morphant генерирует две принципиально разные категории кода:

1. Runtime-реализацию partial mapper-а: именно она выполняет `Create` и
   `Update` в готовом приложении.
2. Compile-time DSL: типизированные construction/member plans и extension-
   методы, которые нужны C# compiler-у, IntelliSense и generator-у только при
   разборе `Configure`.

Source generator является additive: каждый добавленный им source входит в ту
же `CSharpCompilation`. Поэтому `internal`, `file`, `EditorBrowsable`,
`CompilerGenerated` или отсутствие публикации `.g.cs` на диск не мешают
compile-time DSL попасть в metadata итоговой assembly.

По мере роста проекта эта поверхность масштабируется вместе с числом типов,
mapping pairs и их presentation. Кортежи и mapper-scoped API увеличили число
специализированных surface-файлов и сделали стоимость заметнее, но не создали
саму проблему. Она существовала и для обычных типов до обеих features.

Целевой инвариант для пользовательской runtime assembly:

- mapper остаётся одним пользовательским типом, дополненным сгенерированными
  runtime members;
- сгенерированные mapping methods и вся необходимая им runtime metadata
  остаются без изменений;
- generated construction plans, constructor-parameter plans, member plans и
  все `MorphantGeneratedMappingExtensions*` containers отсутствуют;
- `Configure` остаётся как минимальный override, потому что базовый contract
  абстрактный, но его declarative тело и исключительно принадлежащие ему
  compiler-synthesized artifacts отсутствуют;
- marker, cleanup manifest и другая служебная metadata cleaner-а также
  отсутствуют;
- ни один сохранённый type, signature, custom attribute, method body или
  structured symbol record не ссылается на удалённую сущность; служебный
  manifest carrier также отсутствует.

Этот инвариант относится не только к implementation DLL. Он должен выполняться
для всех выдаваемых compiler artifacts: implementation assembly, `ref`,
`refint`, XML documentation и соответствующих symbols, если они существуют.

Feature не удаляет обычные DSL-типы из самой `Morphant.dll`, например
`MapperBuilder` и `MappingBuilder<...>`. Они являются частью библиотечного
compile/runtime contract. Удаляется раздувающая каждую consumer assembly
специализированная generated поверхность и мёртвое тело её `Configure`.

## 2. Текущий инвентарь generated output

| Источник | Основные сущности | Назначение | Решение |
|---|---|---|---|
| `ConstructionPlanEmitter` | `*Construction`, `*ConstructorParameters`, tuple equivalents | Target-typed `new(...)`, выбор constructor-а и его аргументов внутри DSL | Удалить |
| `MemberPlanEmitter` | `*Members`, `TupleMembers` | Target-typed object initializer для `Members(...)` | Удалить |
| `PairConfigurationEmitter` | partial extension containers с `Construct`, `Resolve`, `Convert` и callback-вариантами | Типизированная pair-specific fluent surface | Удалить |
| `MemberConfigurationEmitter` | те же partial extension containers с `Members` overloads | Типизированная member surface | Удалить |
| `TypeMapperEmitter` | partial mapper, реализации `ITypeMapper<,>`, runtime mapping methods | Исполнение mapping-а | Сохранить |
| C# compiler | lambda/local-function methods, closure types, delegate caches из `Configure` | Физическая реализация declarative C#-тела | Удалять только при доказанной exclusive ownership |
| User source | override `Configure(MapperBuilder)` | Корень декларации, одновременно обязательный override abstract member-а | Заменить тело безопасным stub-ом, сам method сохранить |

Plans для обычных destination располагаются под
`Morphant.Generated.Types...Plans`; BCL tuple plans — под
`Morphant.Generated.Tuples.V...` или
`Morphant.Generated.Tuples.S...`. Удалять namespace по имени нельзя: namespace
не является metadata ownership boundary, а будущие emitters и пользовательский
код могут легально получить похожие имена.

Обычные surfaces собираются в общий `MorphantGeneratedMappingExtensions`.
CRTP family-scoped surfaces с bare self-type собираются в отдельный стабильный
container на mapper family: иначе методы разных family различались бы только
generic constraints, которые не входят в C# signature, и получали бы
`CS0111`. Каждый из этих metadata types всё равно состоит из многих partial
declarations. Это важно для marker-а: атрибут нельзя бездумно ставить на каждую
часть, а mapper partial вообще нельзя помечать compile-time-only — атрибут
относится к объединённому mapper type и тем самым пометил бы runtime реализацию
на удаление.

## 3. Измеренная стоимость на крупном consumer-е

Измерение выполнялось на крупном C# 9 integration consumer-е после появления
tuple и mapper-scoped surfaces. Это sample, а не обещание фиксированного
процента экономии для каждого проекта.

### 3.1. Generated sources

| Категория | Файлы | UTF-8 bytes | Доля |
|---|---:|---:|---:|
| Construction plans | 537 | 860 797 | Compile-time-only |
| Pair mapping extensions | 647 | 10 303 400 | Compile-time-only |
| Member plans | 374 | 441 584 | Compile-time-only |
| Member extensions | 385 | 2 493 521 | Compile-time-only |
| Runtime partial mappers | 246 | 2 294 334 | Сохраняются |
| **Всего** | **2 189** | **16 393 636** | |

Compile-time-only категории составили:

- 1 943 из 2 189 generated files — 88,8%;
- 14 099 302 из 16 393 636 source bytes — примерно 86%.

Source bytes не равны размеру DLL: XML comments, whitespace и повторяющийся C#
сильно сжимаются при превращении в metadata и IL. Эти цифры показывают прежде
всего масштаб compiler surface и число сущностей, а не ожидаемое уменьшение
файла один к одному.

### 3.2. Implementation assembly

Исследованная DLL имела размер 2 896 896 bytes и содержала:

| Metadata/IL | Всего | Классифицировано как compile-time-only | Доля |
|---|---:|---:|---:|
| TypeDef | 3 014 | 1 065 | 35,3% |
| MethodDef | 24 391 | 14 641 | 60,0% |
| PropertyDef | 2 311 | 866 | 37,5% |
| IL bytes только в helper bodies | — | 91 731 | — |

Основная стоимость находится не в IL телах, а в большом числе metadata rows,
signatures, names, custom attributes и parameter/property/method records.
Поэтому оценивать feature только по сумме IL нельзя.

### 3.3. Reference assemblies

И `ref`, и `refint` содержали те же 1 065 compile-time-only TypeDefs и около
13 582 helper MethodDefs. Очистка только implementation DLL не выполняет
целевой контракт: downstream compilation продолжит видеть лишнюю поверхность,
а packaged/intermediate artifacts будут расходиться.

## 4. Почему generator не может просто перестать emit-ить DSL

Текущий API опирается на C# type checking. Например, target-typed `new()` в
`Construct` и `Members` должен получить конкретный номинальный generated type,
а fluent call должен привязаться к конкретному generated extension method.
Generator анализирует эту уже связанную semantic model и переносит выражения в
runtime mapper.

После source generation обычный compiler всё равно компилирует исходный
`Configure`. В его IL остаются:

- constructor calls generated plan types;
- вызовы generated extension methods;
- delegate construction;
- ссылки на compiler-generated lambda methods;
- closure types и captured fields;
- static delegate caches;
- вызовы source-defined `base.Configure(builder)` в configuration chain.

Поэтому удаление одних TypeDefs создаст dangling metadata/IL references.
Сначала нужно нейтрализовать все declarative roots и доказанно принадлежащий им
compiler-generated graph.

Проверка на отдельном SDK 10 probe подтвердила, что
`RegisterImplementationSourceOutput` не меняет это свойство: output всё равно
добавляется к compilation и generated internal type остаётся в implementation,
`ref` и `refint`. Название API означает, когда output нужен IDE, а не
«исключить source из assembly».

`EmitCompilerGeneratedFiles=false` тоже ничего не удаляет. Эта настройка лишь
решает, сохранять ли копии generated source на диск.

## 5. Критерии выбора решения

Решение оценивалось по следующим обязательным свойствам:

1. Compile-time UX не ухудшается: IntelliSense, target-typed `new`, nullable
   analysis, diagnostics и просмотр generated source продолжают работать.
2. В runtime artifacts действительно нет specialized DSL, а не только скрыта
   её видимость.
3. Generator остаётся одним и тем же для IDE и command-line build. Нельзя
   возвращать расхождение project models и stale-output проблемы в Rider.
4. Ошибка классификации не может тихо повредить assembly. Неизвестная связь —
   build error, а не best-effort пропуск или heuristic delete.
5. Инкрементальность и actualization сохраняются. Cleaner не заставляет
   generator менять output из-за MSBuild-флага и не трогает неизменившиеся
   файлы.
6. Поддерживаются implementation/reference assemblies, portable/embedded PDB,
   deterministic builds, multi-TFM, signing, project references, pack и
   publish.
7. Пользовательский runtime code и output других generators не удаляются по
   namespace/name convention или общей «неиспользуемости».
8. Feature имеет безопасный opt-out и может быть введена поэтапно.

## 6. Рассмотренные варианты

| Вариант | Что даёт | Почему не выбран как основное решение |
|---|---|---|
| `internal`, `file`, `EditorBrowsable`, `CompilerGenerated`, `GeneratedCode` | Уменьшает видимость или помечает generated code | Metadata и IL остаются; `file` также ломает cross-file binding текущей surface |
| Только не публиковать `.g.cs` | Чистит filesystem view | Не влияет на compilation и assembly |
| `RegisterImplementationSourceOutput` | Не добавляет output в некоторые IDE-only сценарии | Source остаётся additive и компилируется |
| `[Conditional]` | Compiler может удалить call site | Работает только для `void`; fluent methods возвращают builder. Signatures, callbacks и plan types всё равно нужны |
| `#if` / разные build symbols | Может исключить declarative body во второй compilation | Создаёт два semantic мира для IDE и build, ломает стабильность diagnostics/actualization и требует отдельной surface |
| Два разных generator modes для design-time и build | Теоретически оставляет DSL только IDE | IDE/build compilations расходятся; высок риск stale Rider state и разного overload binding |
| Runtime module initializer | Может попытаться чистить/скрывать surface при загрузке | Загруженная assembly уже неизменяема как PE; metadata остаётся, а startup получает ненужный side effect |
| ILLink / `PublishTrimmed` | Удаляет часть недостижимого IL на publish | Publish-only, глобальная и конфигурируемая оптимизация; не очищает обычный build/ref artifacts и не даёт узкой гарантии |
| Уплотнение generated API | Сокращает число overloads/types | Полезная независимая оптимизация, но не достигает нулевой runtime surface |
| Полный redesign DSL на stable generic API | Может сделать configuration code runtime-free по построению | Теряются важные свойства текущего API: destination-shaped completion и естественный target-typed `new`; миграция велика |
| Two-pass compile/source rewrite/recompile | Может физически исключить исходный DSL | Хрупко взаимодействует с другими generators, resources, PDB, SourceLink, signing, incremental build и compiler options |
| Общий assembly trimmer/weaver | Может удалять мёртвый код | Слишком широкая ownership boundary; Morphant не должен решать судьбу произвольного пользовательского IL |
| Targeted manifest-driven post-compile cleaner | Сохраняет один compile model и удаляет только Morphant-owned graph | Требует аккуратного PE/PDB rewrite, signing и строгой verification; это выбранная цена |

Если targeted prototype не сможет надёжно выполнить signing, PDB remapping и
deterministic output, резервным направлением должен быть redesign DSL. Two-pass
compilation и general-purpose trimming не являются приемлемыми fallback-ами.

## 7. Выбранная архитектура

Рабочий pipeline:

```text
Csc + Morphant generator
-> неизменённые raw DLL/ref/refint/PDB под obj
-> audit manifest и ownership graph
-> targeted rewrite во временные файлы
-> полная post-verification
-> re-sign при необходимости
-> атомарная публикация cleaned artifacts
```

Ключевой принцип: generator всегда выдаёт одну и ту же source surface вне
зависимости от включения cleaner-а. Rider, compiler и другие source generators
видят обычную полную compilation. Очистка начинается только после успешного
`Csc` и никогда не выполняется в design-time build.

Raw compiler artifacts нужно сохранять в приватном deterministic каталоге
`obj`, чтобы:

- failure cleaner-а не испортил последний корректный output;
- opt-out мог восстановить исходную assembly;
- было что сравнивать в integration tests и audit mode;
- troubleshooting не требовал повторять compilation;
- можно было отдельно решить Hot Reload/debug policy.

Cleaner не должен переписывать official output in place. Он строит полный
набор временных artifacts, проверяет его и только затем заменяет официальный
набор атомарно настолько, насколько позволяет filesystem.

## 8. Необходимая instrumentation до PE rewrite

### 8.1. Internal marker

Рабочее имя — `MorphantCompileTimeOnlyAttribute`.

Это internal generated attribute, а не public пользовательская annotation. Им
помечаются TypeDefs, ownership которых generator знает точно:

- `*Construction`;
- `*ConstructorParameters`;
- `*Members`;
- tuple plan equivalents;
- общий и family-specific `MorphantGeneratedMappingExtensions*` containers;
- сама служебная marker/manifest surface, если она представлена TypeDef-ами.

Для partial extensions нужен один dedicated anchor source, который ровно один
раз объявляет с marker-ом каждый фактически emitted extension container.
Ставить marker на каждую pair/member partial-часть нельзя: это либо создаст
повторные attributes, либо потребует `AllowMultiple`, скрывая ошибку ownership.

Mapper partial не помечается никогда. Все partial declarations образуют один
metadata type, поэтому attribute на «generated части mapper-а» фактически
пометит на удаление весь пользовательский mapper.

Стандартных `GeneratedCodeAttribute` и `CompilerGeneratedAttribute`
недостаточно: ими могут пользоваться user code и другие generators. Они могут
оставаться дополнительной information, но не являются authority на удаление.

### 8.2. Versioned cleanup manifest

Marker отвечает на вопрос «кому принадлежит TypeDef», но недостаточен для
доказательства полноты. Generator должен также выдать machine-readable
manifest со своим отдельным cleanup contract version.

Manifest должен содержать как минимум:

- точные metadata identities всех disposable types, включая namespace,
  nesting и generic arity;
- точные identities всех source-defined `Configure` roots текущей assembly;
- source-defined base `Configure` methods, входящие в connected CRTP
  configuration chains;
- ожидаемое количество marked types и roots;
- версию cleanup schema/semantics;
- при необходимости identity mapper-а, которому принадлежит root, для
  понятной ошибки.

Metadata token не должен быть единственной identity: row numbers меняются при
обычных edits. Нужны стабильные declaring-type identity и method signature;
после чтения конкретной assembly cleaner может сопоставить их с tokens.

Физический формат manifest пока не выбран. Generated internal metadata type,
assembly attribute или embedded resource допустимы только если cleaner затем
удаляет и сам carrier. Формат нужно выбирать по простоте строгого чтения,
детерминизму и отсутствию ограничений на крупные compilations.

Нельзя переиспользовать существующий
`Morphant.GeneratorContractVersion`. Он описывает совместимость runtime
`Morphant.dll` и generator-а. Cleanup task имеет другой lifecycle и должен
получить независимую версию контракта.

Cleaner обязан cross-validate manifest и markers в обе стороны:

- каждый manifest type существует и имеет marker;
- каждый marked type перечислен в manifest;
- количество совпадает;
- каждый Configure root существует и имеет ожидаемую shape;
- версия поддерживается task-ом;
- одинаковый contract обнаружен во всех очищаемых artifacts.

Любое несовпадение является build error. Поиск по префиксу namespace,
суффиксу имени или hint name не может служить fallback-ом.

### 8.3. Диагностика DSL escape

После удаления compile-time surface пользователь не должен иметь возможность
сохранить на неё runtime dependency. Нужна error diagnostic для любого
статически видимого использования Morphant declarative/generated API вне
анализируемого `Configure`.

Она должна покрывать по меньшей мере:

- instance и explicit static invocation generated extension method-а;
- method group и созданный из него delegate;
- plan type в field, property, event, parameter, return type или constraint;
- local/array/tuple/generic construction с plan type;
- `typeof`, cast, pattern, `default` и `nameof`, если operand раскрывает
  generated surface;
- передачу DSL builder/plan в другой method или возврат из `Configure`;
- использование из generated source, если этот source уже является входом
  анализируемой compilation. На output peer generator-а полагаться нельзя.

Существующие `MORPH0017` и `MORPH0018` уже запрещают важную часть escape:
aliasing, передачу, возврат и условный flow mapper/mapping builder-а. Новая
диагностика должна закрыть оставшиеся способы прямой ссылки на generated
surface, а не дублировать их wording.

Source diagnostic является первой линией защиты и должна указывать точный
syntax span. Binary post-verifier остаётся последней линией: output другого
generator-а, необычный compiler lowering или пропущенная syntax form всё равно
не могут привести к assembly с dangling reference.

Reflection-by-name через произвольную строку статически доказать невозможно.
Generated internal surface не является поддерживаемым reflection contract;
после включения feature такой lookup закономерно перестанет находить тип.
Cleaner не должен пытаться распознавать строковые литералы или непрозрачные
resource payloads эвристикой: они не являются CLR metadata references.

### 8.4. MSBuild switch

Рабочее имя — `MorphantStripCompileTimeSurface`.

Требования к setting:

- на этапе prototype default `false`, включение явное;
- после полной build/test matrix default может стать `true`;
- `false` остаётся аварийным opt-out для несовместимого toolchain;
- принимаются только `true` и `false`, остальные значения дают MSBuild error;
- setting не является `CompilerVisibleProperty` и не меняет generator output;
- изменение setting учитывается incremental fingerprint и не оставляет
  случайно cleaned/raw artifact от предыдущего режима.

Public имя и момент смены default ещё нужно подтвердить перед реализацией.
Пользовательские attributes, compile constants и отдельный source-generator
mode для этой feature не нужны.

## 9. Алгоритм cleaner-а

### 9.1. Preflight

До любой записи task должен:

1. Найти implementation, `ref`, `refint`, XML docs и PDB для конкретного TFM.
2. Проверить cleanup contract version.
3. Прочитать manifest и markers.
4. Сопоставить каждый Configure root и marked TypeDef.
5. Построить полный reference graph metadata и IL.
6. Определить symbol format и signing mode.
7. Отказаться от rewrite, если какой-либо обязательный artifact или relation
   не поддержан.

### 9.2. Configure roots

Сам override удалять нельзя: `TypeMapper<TMapper>` требует abstract
`Configure(MapperBuilder)`. Рабочая замена тела — минимальный deterministic
`throw new RuntimeInvocationNotSupportedException()`.

Это лучше пустого `ret`:

- случайный runtime вызов не выглядит успешно выполненной configuration;
- поведение совпадает с существующей compile-time-only surface;
- failure имеет уже документированный Morphant exception type;
- stub не требует generated plan или extension references.

Нужно обработать все source-defined roots текущей assembly, а не только leaf
mapper. Если derived mapper вызывал `base.Configure(builder)`, после замены его
тела внешний call исчезает. Base root этой же assembly очищается независимо по
manifest. Configure из уже скомпилированной referenced assembly должен был
быть обработан при build той assembly; cleaner текущего проекта не модифицирует
dependencies.

### 9.3. Compiler-synthesized graph

Lambda/local-function methods, closure display classes, captured fields и
delegate caches нельзя удалять только по `CompilerGeneratedAttribute`.
Они могут принадлежать runtime code пользователя или другого generator-а.

Рабочее правило:

1. Взять original Configure bodies как roots declarative graph.
2. Проследить ссылки на compiler-synthesized methods/types/fields.
3. Построить обратные ссылки из всего retained graph.
4. После подстановки Configure stubs удалить только узлы, которые достижимы из
   declarative roots и не имеют владельца/ссылки из retained graph.
5. При shared или неизвестной ownership оставить узел только если в нём нет
   ссылки на disposable type; иначе завершить build ошибкой.

Нельзя применять общий dead-code elimination: даже private method без
статической ссылки может вызываться reflection, native interop или внешним
weaver-ом. Authority распространяется только на доказанно синтезированный
configuration graph.

### 9.4. Удаление generated surface

После нейтрализации roots task удаляет marked types целиком со всеми их:

- methods и IL bodies;
- properties, fields и events;
- parameters и generic parameters;
- nested types;
- custom attributes;
- interface implementations и method semantics;
- manifest/marker carrier metadata.

Удаление отдельных methods из `MorphantGeneratedMappingExtensions*` при
сохранении пустых type-ов не выполняет целевой контракт. В assembly не должен
оставаться ни один такой container.

### 9.5. Symbols, XML docs и SourceLink

Portable PDB нужно переписать вместе с PE: MethodDef tokens и sequence points
удалённых methods не могут остаться dangling. Embedded portable PDB требует
обновления embedded payload и PE checksum/debug directory.

XML documentation нужно очистить от members удалённой surface. Обычная
пользовательская и runtime generated documentation сохраняется. SourceLink и
document records не следует удалять только потому, что один declarative method
исчез: один source document может содержать retained user code.

Поддержку Windows PDB нельзя молча имитировать. Если выбранный rewrite engine
её не обеспечивает, stripping при таком format должен либо иметь явно
документированный support path, либо завершаться понятной ошибкой до записи.

### 9.6. Strong naming

Любой PE rewrite инвалидирует strong-name signature, потому что signature
покрывает hash содержимого assembly. Cleaner должен воспроизвести исходный
signing mode:

- fully signed;
- delay signed;
- public signed;
- unsigned.

Для signed assembly task должен получить те же signing inputs через обычный
MSBuild contract и повторно подписать уже проверенный cleaned PE. Нельзя тихо
снимать strong name или менять public key. Финальная verification выполняется
после signing и сверяет identity/signature.

### 9.7. Post-verification

Перед публикацией task повторно читает каждый output и доказывает:

- marked/manifest types отсутствуют;
- ни один TypeRef/TypeSpec/MemberRef/MethodSpec/signature/custom attribute не
  разрешается в disposable identity;
- ни один IL operand не ссылается на удалённый token;
- Configure roots имеют только ожидаемый stub;
- mapper runtime types и ожидаемые mapping contracts присутствуют;
- metadata tables, PE headers, PDB и XML docs согласованы;
- пользовательские resources сохранены byte-for-byte, кроме отдельного
  cleanup-owned manifest resource, если будет выбран такой carrier;
- assembly identity и signing state сохранены;
- `ref`/`refint` описывают тот же retained contract, что implementation.

Неизвестная table/record relation, которую verifier не умеет классифицировать,
является error. «Удалили почти всё» не считается успешным результатом.

## 10. MSBuild, IDE и инкрементальность

### 10.1. Место в build graph

Cleaner должен запускаться после успешного `Csc`, но до того, как artifacts
становятся входом project references, copy-local, pack, publish, ReadyToRun или
AOT. Существующий `Morphant.targets` уже аккуратно композирует
`TargetsTriggeredByCompilation` для Git snapshot; новая feature не должна
перезаписывать чужое значение или менять порядок snapshot publication.

Точный target hook нужно подтвердить integration prototype-ом. В частности,
нужно проверить путь, где compiler up-to-date и `Csc` фактически не запускался,
но изменился stripping switch или версия cleanup task-а.

### 10.2. Fingerprint и timestamps

Incremental key должен включать:

- bytes/hash raw assembly и каждого companion artifact;
- cleanup contract/task version;
- manifest contents;
- stripping mode;
- relevant PDB/signing options.

Повторный build с теми же inputs не должен переписывать cleaned file или
менять timestamp. Изменение mapper-а, model-а, tuple presentation, base
configuration, target framework или compiler options должно актуализировать
raw output и затем cleaned output в той же сборке.

Переключение `true -> false` должно восстановить raw artifact; переключение
`false -> true` — построить cleaned artifact даже без случайного source edit.
Для этого недостаточно надеяться только на факт запуска `Csc`: нужен собственный
stamp/target dependency contract.

### 10.3. Rider и generated snapshots

Design-time compilation остаётся полной и никогда не очищается. Поэтому Rider
продолжает показывать type-specific completion и файлы в
**Dependencies | Source Generators**.

`MorphantGitSnapshot` публикует generated C# после успешной compilation и
остаётся review mechanism. Cleaner не меняет generator source и не должен
делать snapshots зависимыми от stripping switch. При `Full` пользователь
по-прежнему может увидеть compile-time plans/extensions, хотя в runtime DLL их
нет; это ожидаемое различие source-review и binary contracts.

### 10.4. Hot Reload

Hot Reload может строить delta относительно unstripped metadata tokens, тогда
как запущено cleaned PE. Совместимость нельзя предполагать. До отдельного
доказательства нужно выбрать одно из двух явных поведений:

- dev/Hot Reload запускает сохранённый raw artifact;
- stripping автоматически отключается только в точно распознанном Hot Reload
  pipeline, без изменения generator output.

Обычный `Debug` сам по себе не должен навсегда означать «мусор разрешён»:
пользователи распространяют и тестируют Debug assemblies. Политику нужно
привязать к реальному tooling scenario и покрыть тестом.

## 11. Failure policy и diagnostics

При включённом stripping feature работает fail-closed.

| Условие | Результат |
|---|---|
| Manifest/marker mismatch | MSBuild error до записи |
| Неподдержанная cleanup contract version | MSBuild error с версиями generator/task |
| DSL escape найден в source | Morphant compiler error на точном span |
| Retained metadata ссылается на disposable type | MSBuild error с owner и target identity |
| Неизвестный compiler-synthesized sharing | MSBuild error; ничего не удаляется |
| Неподдержанный PDB/signing mode | MSBuild error до записи |
| Rewrite или verification exception | MSBuild error, raw/предыдущий output сохранён |
| Post-verification не прошла | Cleaned temporary files не публикуются |

Audit mode может сообщать counts и причины, но не должен превращать опасное
состояние в warning после того, как stripping включён. Иначе пользователь
получит непредсказуемую смесь cleaned и uncleaned assemblies.

MSBuild diagnostics должны иметь собственные стабильные коды семейства,
согласованного с существующими `MORPHANTMSB...`. Конкретные номера и тексты
следует назначить при реализации, когда известны все failure classes.

## 12. Взаимодействие с остальными features

### Tuples

Tuple plans и mapper-scoped tuple presentations удаляются по тому же manifest,
что обычные plans. Не должно быть отдельного tuple cleaner mode. Physical
tuple identity нужна generator-у при compilation, но не runtime assembly после
того, как mapping code уже сгенерирован.

### Mapper-scoped и shared surfaces

Ownership не выводится из того, была surface mapper-scoped, family-scoped или
shared. Все disposable metadata identities перечисляются явно. Общий и каждый
family-specific extension container удаляются целиком, когда manifest
подтверждает все их partial contributions.

### Configuration inheritance

Manifest включает source-defined roots всей connected chain. Cleaner не
пытается заново повторить semantic analysis inheritance: authority приходит от
generator-а, который уже разрешил CRTP chain и выдал diagnostics. Binary graph
проверяет реальный lowering `base.Configure`.

### Diagnostics и recovery stubs

Текущая защита Morphant требует сохранять complete mapper и typed exception
stubs при поддерживаемой shape, даже если отдельный mapping invalid. Cleaner
запускается только после успешного `Csc`; warnings и suppressed Morphant
diagnostics не дают ему права удалить неизвестный graph. Runtime generated
failure stubs относятся к mapper implementation и сохраняются.

### Other source generators

Другие generators видят полную input compilation, что совместимо с обычной
моделью Roslyn. Morphant не должен рассчитывать, что один source generator
увидит output другого в том же run. Если peer output сохраняет ссылку на
Morphant compile-time type, source diagnostic её не увидит, но post-verifier
обязан найти ссылку в готовой assembly и остановить build.

### Other post-processors/weavers

Порядок относительно coverage, obfuscation, aspects и других weavers должен
быть явным. Наиболее безопасно очищать сразу после compiler output, пока
manifest и original Configure graph узнаваемы, и предоставить документированную
точку зависимости для последующих processors. Coexistence нельзя объявлять по
умолчанию без integration tests.

### Trimming, ReadyToRun и AOT

Cleaner работает раньше этих этапов и уменьшает их вход. Он не заменяет ILLink
и не меняет annotations пользовательского runtime code. Publish pipelines
должны получать уже cleaned IL; попытка переписывать ReadyToRun/native output
слишком поздно и не поддерживается.

### `InternalsVisibleTo` и accidental dependencies

Generated helpers имеют `internal` accessibility, но
`InternalsVisibleTo` делает их частью lookup friend assembly. Компиляционная
проба подтвердила, что metadata-only reference сохраняет эти internals: если
friend assembly генерирует ту же shared pair surface, её `Convert`/`Members`
и остальные методы становятся неоднозначны с импортированными overloads.
Cleaner устраняет не только metadata-мусор, но и эту реальную cross-assembly
коллизию, если очищает implementation, `ref` и `refint` согласованно.

Прямые ссылки пользовательского кода на generated internals не являются
поддерживаемым API: после очистки reference assembly такая compilation должна
перестать собираться. Изменение нужно явно отметить в release notes при
включении default, но поддерживать generated internals как compatibility
contract нельзя.

## 13. Test matrix

Перед default-on нужны отдельные слои тестов.

### 13.1. Generator contract

- complete generated-source snapshots marker-а, anchor-а и manifest-а;
- точные metadata identities для ordinary, nested, generic и BCL tuple plans;
- shared, mapper-scoped и mapper-family-scoped surfaces;
- несколько mappers и partial extension contributions;
- общий и несколько family-specific extension containers;
- source-defined base/derived Configure chains;
- отсутствие marker-а на mapper partial;
- incremental caching при нерелевантном edit;
- invalidation при добавлении, удалении и изменении pair/mapper/base chain;
- actualization после edit в том же generator driver/build session.

### 13.2. DSL escape diagnostics

- direct и explicit static calls вне Configure;
- method groups/delegates;
- fields, properties, parameters, returns, generics, arrays и `typeof` plan
  types;
- alias/pass/return builder cases вместе с `MORPH0017`/`MORPH0018`;
- валидные usages внутри direct fluent chain и connected base Configure;
- несколько независимых mappers, чтобы invalid escape не ломал чужую
  generation;
- generated-source consumer, если Roslyn scheduling позволяет его увидеть;
- точный span, message arguments, severity и help page.

### 13.3. Audit and rewrite

- reflection и raw metadata доказывают присутствие surface до cleaner-а;
- audit находит точные ожидаемые counts без mutation;
- implementation, `ref` и `refint` после cleaner-а не содержат disposable
  TypeDefs/MethodDefs/Properties;
- friend assembly с пересекающейся shared pair не получает competing
  extensions из очищенной reference assembly;
- Configure body содержит только выбранный exception stub;
- closure, lambda method и delegate cache удаляются, если exclusive;
- shared compiler-generated artifact вызывает предсказуемый отказ;
- все виды metadata references проверяются на dangling identity;
- XML docs и PDB не содержат orphan records;
- repeated clean даёт byte-identical output и не меняет timestamps;
- corrupted/missing/foreign manifest никогда не приводит к heuristic delete.

### 13.4. Runtime regression

- полный Create/Update integration suite на cleaned assemblies;
- tuples, nested mapping, runtime polymorphism, configuration inheritance,
  nullable contracts, generics, callbacks, unsafe code и exception stubs;
- direct `ITypeMapper<,>` use и DI dispatch;
- отсутствие compile-time helpers через reflection;
- stack traces и debugging retained runtime mapper methods с PDB.

### 13.5. Build/package matrix

- minimum supported SDK/Roslyn host и newest validated version;
- C# 9 и latest language versions;
- Debug/Release;
- single- и multi-target projects;
- portable PDB, embedded PDB и выбранная policy для остальных formats;
- unsigned, fully signed, delay-signed и public-signed assemblies;
- deterministic rebuild byte equality;
- project references, copy-local, `dotnet pack`, `dotnet publish`;
- trimming, ReadyToRun и AOT smoke tests;
- Hot Reload policy;
- coexistence/order с representative post-processor;
- opt-in, opt-out и оба направления переключения без source edit;
- failed `Csc`, failed cleaner и recovery после failure;
- parallel multi-TFM/project builds без shared temporary paths.

## 14. Этапы реализации

### Этап 1. Контракт без удаления

1. Добавить internal marker и единый anchor source для всех extension
   containers.
2. Добавить отдельно versioned manifest.
3. Добавить complete generated-source и incremental tests.
4. Добавить DSL escape diagnostic и её help/test coverage.

Checkpoint завершён, когда generator output остаётся стабильным для IDE и
runtime behavior не меняется.

### Этап 2. Audit-only build task

1. Прочитать DLL, `ref`, `refint` и symbols без записи.
2. Cross-validate manifest и markers.
3. Найти Configure roots, lambdas, closures и cached delegates.
4. Построить ownership/reference graph.
5. Выполнить будущие dangling-reference checks в simulation.
6. Вывести deterministic summary: найдено, планируется удалить, сохранено,
   почему.

Это обязательный первый implementation checkpoint. На реальных consumer
assemblies нужно доказать полноту classifier-а до появления кода, меняющего
PE.

### Этап 3. Opt-in rewrite

1. Сохранять raw artifacts.
2. Переписывать implementation PE/PDB/XML.
3. Переписывать `ref` и `refint`.
4. Re-sign.
5. Выполнять полную post-verification.
6. Публиковать атомарно только весь согласованный набор.

Default остаётся `false`, пока не пройдена build/package matrix.

### Этап 4. Stabilization и default-on

1. Проверить крупный реальный consumer и сравнить size/metadata/build time.
2. Закрыть Hot Reload и other-weaver policy.
3. Проверить minimum/newest SDK и все signing/symbol modes.
4. Добавить public documentation, troubleshooting, diagnostics и changelog.
5. Только после этого сделать stripping default-on, сохранив opt-out.

## 15. Критерии готовности

Feature нельзя считать готовой только по уменьшению DLL. Нужны одновременно:

- нулевая disposable generated surface в implementation/ref/refint;
- отсутствие dangling metadata, IL, PDB и XML records;
- неизменная runtime семантика полного integration suite;
- неизменная IDE/source-generator модель;
- source diagnostics для поддерживаемых escape forms;
- fail-closed binary verifier для непредвиденных forms;
- byte-deterministic outputs и корректная incrementality;
- сохранённые assembly identity и signing;
- безопасные failure/recovery и opt-out paths;
- подтверждённые pack/publish/project-reference workflows.

## 16. Что уже решено и что ещё открыто

Зафиксированные решения:

- feature общая, не tuple-specific;
- текущий typed DSL сохраняется;
- основной путь — targeted post-compile cleaner;
- ownership задают marker + versioned manifest, не имена;
- mapper partial никогда не помечается disposable;
- Configure сохраняется как exception stub;
- unknown relation означает error;
- implementation, `ref`, `refint`, symbols и XML рассматриваются одним
  artifact set;
- generator output не зависит от MSBuild stripping switch;
- user attributes, `#if` и dual IDE/build generators не вводятся;
- первый executable prototype работает только в audit mode.

Открытые implementation decisions:

- физический формат manifest;
- rewrite engine: низкоуровневый `System.Reflection.Metadata` или проверенная
  библиотека с полноценным portable/embedded PDB и signing support;
- точные target hooks и порядок с существующим Git snapshot target;
- каталог/format raw artifacts и incremental stamps;
- поддерживаемые symbol formats и Hot Reload behavior;
- окончательное имя property и версия, в которой default станет `true`;
- конкретные `MORPH...`/`MORPHANTMSB...` номера и тексты diagnostics.

Эти вопросы нужно закрыть audit prototype-ом и tests, а не предположениями до
чтения реальных artifacts.

## 17. Использованные технические основания

- [Roslyn source generators design](https://github.com/dotnet/roslyn/blob/main/docs/features/source-generators.md) — generated sources добавляются к compilation.
- [`RegisterImplementationSourceOutput`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.codeanalysis.incrementalgeneratorinitializationcontext.registerimplementationsourceoutput) — API регистрации incremental output, не механизм исключения metadata.
- [`ConditionalAttribute`](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.conditionalattribute) — условные methods обязаны возвращать `void`.
- [.NET trimming](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trim-self-contained) — trimming относится к publish pipeline и не заменяет очистку build/reference assemblies.
- [Strong-named assemblies](https://learn.microsoft.com/en-us/dotnet/standard/assembly/create-use-strong-named) — rewrite требует сохранения identity и повторной подписи.
