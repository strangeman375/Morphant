# Полная проверка Morphant: этап 3

Дата: 2026-09-05. План: [RELEASE_REVIEW_PLAN.md](RELEASE_REVIEW_PLAN.md).

Статус: проверка этапа завершена. Найдены три дефекта изоляции DSL; изменения реализации требуют отдельного согласования. Этап 4 не начат. Пользователь отдельно разрешил публикацию отчёта и воспроизведений в публичный `main`.

После отчёта пользователь отдельно запросил анализ и выбор исправлений. Результат сохранён в [решениях S03-01–S03-03](RELEASE_REVIEW_STAGE_03_SOLUTIONS.md); production-исправления ещё не внедрены.

## 1. Результат

1. **S03-01:** `InternalsVisibleTo` делает shared-расширения одной сборки применимыми к мапперу другой. Для одинаковой обычной пары все 15 callback-перегрузок получают `CS0121`.
2. **S03-02:** generated construction/member types имеют одинаковые полные имена в friend assemblies. `CS0436` появляется даже при bare-регистрации, у mapper-scoped и family-scoped API и при разных source с общим destination. Warnings-as-errors останавливает сборку.
3. **S03-03:** связанные CRTP-семейства в одной сборке конфликтуют, когда производная конфигурация намеренно не вызывает `base.Configure(builder)`, а базовая поверхность нужна отдельному мапперу. Все 15 перегрузок получают `CS0121`. Это разрешённый способ отказаться от наследования конфигурации.

Изменения receiver сами по себе не устранят S03-02, а переименование типов само по себе не устранит конкуренцию `Using`/`Convert`. Post-compile удаление DSL не решает воспроизведённую конкуренцию в source-backed compilation. Решение этих проблем в ходе аудита не выбиралось и не реализовывалось.

## 2. Версия, окружение и доказательства

- HEAD на старте: `f2cfec398bb9e8379f54238df52617ae77c9e0a9`, дерево чистое, remote `main` совпадал.
- Проверенная правка документации опубликована в [f55d4cf](https://github.com/strangeman375/Morphant/commit/f55d4cf67785c09f57d0a975006adf097d65a5bb), tree `78a72b53e24a7455905af6f53591fb91fd4346e9`. Соответствующий локальный commit — `03406f1c1750941eb479a0913995cf42fa8ff013`; деревья совпадают. Исправлены справочник и генерируемые XML-подсказки, а в 29 snapshot-файлах механически заменены только эти XML-строки. Алгоритмы генерации и маппинга не менялись.
- Локально: Linux, SDK **10.0.100**, runtime **net10.0**, generator и compiler probe используют Roslyn **4.4.0**. Входные пользовательские программы — C# 9, nullable enabled. MSBuild consumers подключают generator через analyzer-style `ProjectReference` и включают warnings-as-errors.
- Compiler probe загружает production analyzer через `AnalyzerFileReference`; не вызывает planners/emitters и не конструирует ожидаемую поверхность средствами продукта. Он сохраняет исходники, все generated files и все warning/error diagnostics. Это исследовательская compiler-проверка, не новый runtime integration harness и не snapshot-тест.
- Source reference — настоящий `CSharpCompilationReference` на результат генератора producer. DLL и reference assembly отдельно emitted из той же producer compilation; reference assembly создаётся с `metadataOnly: true, includePrivateMembers: false`. Это не проверка Rider или Workspace project graph.
- Независимая MSBuild-проверка использует настоящие проекты, implementation DLL и `obj/Release/net10.0/ref` producer. Последующие consumer builds используют уже собранные dependencies. Compiler host здесь — SDK 10, а не unit-driver Roslyn 4.4.0.

| Новый запуск | Результат |
| --- | --- |
| Полная Release-сборка `src/Morphant.slnx` | Exit 0, 0 warnings/errors |
| Полный unit-проект, Roslyn 4.4.0 | Exit 0; 733 passed, 1 штатный skipped, 0 failed |
| Полный integration-проект | Exit 0; 266 passed, 0 skipped/failed |
| Матрица compiler probes | 540 случаев; дополнительно 5 направленных CRTP-вариантов; без исключений генератора |
| Реальные MSBuild consumers | 21 случай: 3 успешных контроля и 18 отказов с исследуемыми diagnostics |
| Явное имя `DestinationConstruction` | Отдельный C# 9 consumer собран без warnings/errors и выполнен: выбран ожидаемый `string`-конструктор |

Пропущенный unit-тест использует collection expressions, требующие более нового Roslyn. Успех существующего suite не закрывает новые дефекты: нужных отрицательных условий в прежних collision-tests не было.

Первый локальный `--no-restore` запуск остановился до тестов из-за assets со старым `/root/.nuget/packages`. После restore ссылки указывают на действующий `/workspace/.nuget/packages`; приведённые успешные результаты относятся к последующим завершённым запускам. При возврате результата restore произошла ошибка транспортного approval; обновление assets и итоговые exit codes сборки/тестов проверены отдельно.

Локальные журналы: `artifacts/release-review/stage-03/`. SHA-256 unit TRX: `d58d8fd6b1b8e6fdb508a7639765849ca778f1ff77d354e6a531fa4f837bec15`; integration TRX: `d40a24599fe84915d3ae568b76358d3213375d77a7a70ccc5f53a4e2dc0d4289`. Проверенная generator DLL: `dfbae96f0d4357c29bd0bd85049039bf64874a1f4965319dd8b1b16485bed42e`.

## 3. Проверенная область

| Область | Что сопоставлено и проверено | Вывод и граница |
| --- | --- | --- |
| Self-type / CRTP | `MapperDeclarationPipeline`, `SelfTypeTests`; собственный тип, recursive constraint каждого reusable level, nullable constraint, concrete intermediate base, восстановление после ошибки | Нового дефекта декларационного gate не найдено. Конкуренция двух допустимых семейств выделена в S03-03 |
| Вложенность, `partial`, доступность | `StructuralDeclarationTests`, `GateAndActualizationTests`, реальные Declaration consumer fixtures; public/internal/protected-internal, недоступные и file-local контейнеры | Отказы и сохранение независимых допустимых пар подтверждены существующими проверками; private/protected-only mapper остаётся намеренным ограничением |
| Регистрация / интерфейсы / унификация | `MappingPairPipeline`, `MapperContractPipeline`, `InterfaceContractTests`, `DuplicateRegistrationTests`, `MapperFamilyParameterTests`; нормализация пары, direct/inherited contracts, возможная унификация и независимые пары | В этих проверках новых расхождений не найдено; constraints не используются как повод разрешить запрещённую C# унификацию интерфейсов |
| Generic-параметры family | Участие каждого non-self параметра в каждой паре, containing types/arrays, ограничения только через constraints, suppression и восстановление | Граница `MORPH0060` подтверждена. Она не предотвращает S03-03, где все параметры участвуют в паре |
| Aliases и полные имена | `MappingTypeEligibilityPolicy`, `AvailabilityTests`: extern-only, global+extern, неоднозначные global FQN, namespace/type collision, IVT-доступ к model types | Заявленные неподдерживаемые контракты отклоняются. Эти проверки доступности models не защищают generated types от S03-02 |
| Пользовательские расширения | `GeneratedExtensionCollisionTests`: конкурирующие и более специфичные методы, layered callbacks, актуализация; проверены правила аутентификации generated method | Прежние сценарии проходят. Различие recovery для DLL/source references зафиксировано ниже; полный parsing audit остаётся этапу 5 |
| Namespace-пути и длинные имена | Construction/member naming snapshots, `GeneratedExtensionCollisionTests`, `GeneratedNameLengthTests`: nested/generic scopes, keywords, реальные case-insensitive collisions, UTF-8/Unicode, запись файла | Внутрисборочные проверки проходят. Из этого не следует уникальность имён между сборками |
| Shared / mapper / family scope | Новые проверки всех 15 перегрузок; IVT on/off; source/DLL/ref; nullable pair, ValueTuple, generic family, разные source одного destination | S03-01 и S03-02 воспроизведены и разделены |
| Семейства и пересекающиеся подстановки | Два unrelated family с `class` / `class, new()`; закрывающиеся на один контракт параметры; дополнительный `List<T>` в одной семье; связанные семьи с/без `base.Configure` | Unrelated/nested варианты чистые внутри одной сборки. Связанные семьи без base-вызова дают S03-03 |

Полный unit-прогон включает 62 теста в `MapperDeclarationTests`, 71 в `MappingRegistrationTests`, 38 в `ConstructionSurfaceTests`, 24 в `MemberSurfaceTests`, а также collision/naming/actualization-группы вне этих namespaces. Это состав реально запущенного suite, не утверждение о переборе всех допустимых программ.

## 4. S03-01 — shared-расширения конкурируют через IVT

**Подтверждённый дефект, высокая важность.** Два независимо настроенных маппера обычной пары не должны влиять на компиляцию конфигурации друг друга.

Минимальные проекты сохранены: [Producer.cs](release-review-stage-03/msbuild/Producer/Producer.cs), [Consumer.cs](release-review-stage-03/msbuild/Consumer/Consumer.cs). Producer содержит публичные `Shared.Source` и `Shared.Destination`, свой bare mapper и:

```csharp
[assembly: InternalsVisibleTo("AuditConsumer")]
```

В consumer:

```csharp
[MorphantMapper]
public partial class ConsumerMapper : TypeMapper<ConsumerMapper>
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Shared.Source, Shared.Destination>()
            .Construct(s => new(s.Id));
}
```

Shared receiver имеет вид `MapperBuilderBase<MappingBuilder<TMapper, Source, Destination>>`. Его свободный `TMapper` подходит и к чужому mapper. IVT раскрывает `internal` extension container producer; оба набора кандидатов участвуют в C# overload resolution.

| Ссылка consumer → producer | Без IVT | С IVT: 15 перегрузок | С IVT: bare / explicit generated name |
| --- | --- | --- | --- |
| Source compilation | Все 18 случаев чистые | `CS0121` + `CS0436` | Только `CS0436` |
| Implementation DLL | Все 18 случаев чистые | `MORPH0018` + `CS0121` + `CS0436` | Только `CS0436` |
| Reference assembly | Все 18 случаев чистые | `MORPH0018` + `CS0121` + `CS0436` | Только `CS0436` |

18 форм — bare, 15 callback-перегрузок и явные `DestinationConstruction` / `DestinationMembers`. Перегрузки: Construct ×2, Resolve ×2, ConstructUsing ×2, ResolveUsing ×2, Convert ×3, Members ×4. Обычный MSBuild подтвердил shared-отказы для `Construct` и `Convert` при всех трёх видах файловой ссылки, а также два контроля без IVT.

Явное имя собственного generated-типа помогает выбрать нужный `Construct`/`Members`, но не устраняет S03-02. У `Using`/`Convert` callback-типы принадлежат runtime API, поэтому переименование construction/member types не устраняет их неоднозначность.

## 5. S03-02 — совпадающие generated types в friend assemblies

**Подтверждённый дефект.** Без warnings-as-errors это warnings; с включённой политикой — отказ сборки поддерживаемого consumer.

Для общего destination обе сборки объявляют, например:

```text
Morphant.Generated.Types.N_Shared.Plans.DestinationConstruction
Morphant.Generated.Types.N_Shared.Plans.DestinationConstructorParameters
Morphant.Generated.Types.N_Shared.Plans.DestinationMembers
```

Компилятор выбирает локальное определение и сообщает `CS0436` в generated declarations/signatures. Это не конфликт публичных DTO: в примере model types объявлены только в producer и имеют одну CLR-идентичность.

| Направленная матрица | Без IVT | С IVT |
| --- | --- | --- |
| Nullable source/destination → mapper-scoped API | 48 чистых случаев | 48 случаев только с `CS0436` |
| Named ValueTuple destination → mapper-scoped API | 48 чистых случаев | 48 случаев только с `CS0436` |
| Generic CRTP family; разные self-constraints | 48 чистых случаев | 48 случаев только с `CS0436` |
| Другой source при общем обычном destination | 48 чистых случаев | 48 случаев только с `CS0436` |

Каждая строка охватывает bare + 15 перегрузок и три формы reference. Для tuple-пробы source имеет оба нужных имени `Id`/`Other`; предварительный вход без `Other` получал ожидаемый `MORPH0035` и был исправлен до итогового сравнения. Таблица относится к исправленному входу.

Во всех этих строках receiver изолирует методы либо source различает пару, но plan namespace не различает сборки. MSBuild подтвердил `CS0436` для `Construct` в каждой строке. S03-02 требует самостоятельного решения идентичности/доступности типов с сохранением поддерживаемых явных имён.

## 6. S03-03 — связанные семьи без наследования конфигурации

**Подтверждённый дефект, высокая важность.** Для возникновения IVT и внешние сборки не нужны.

[Полный минимальный пример](release-review-stage-03/family-variants/annotated.cs):

```csharp
[MorphantMapper]
public partial class Root<TMapper, T> : TypeMapper<TMapper>
    where TMapper : Root<TMapper, T>
    where T : class
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source<T>, Destination<T>>();
}

[MorphantMapper]
public partial class Derived<TMapper, T> : Root<TMapper, T>
    where TMapper : Derived<TMapper, T>
    where T : class, new()
{
    protected override void Configure(MapperBuilder builder) =>
        builder.Map<Source<T>, Destination<T>>()
            .Construct(s => new(s.Id));
}
```

Оба generated family containers объявляют одинаковые generic signatures с receiver `MappingBuilder<TMapper, Source<T>, Destination<T>>`. В производном контексте выполняются ограничения обеих семей; более строгое `where` не делает метод лучшим C# кандидатом.

| Вариант | Фактический результат |
| --- | --- |
| Related families, без `base.Configure` | Bare чистый; все 15 перегрузок — `CS0121` |
| Одинаковые `where T : class` на обоих уровнях | `Construct` — `CS0121` |
| Обе generic-базы abstract | `Construct` — `CS0121` |
| Базы без атрибутов; два конкретных attributed leaf, отдельно использующих каждую базу | `Construct` — `CS0121` |
| Добавлен `base.Configure(builder)` в производной конфигурации | `Construct` компилируется чисто |
| Unrelated семьи с `class` / `class, new()` | Bare и все 15 перегрузок чистые |
| Unrelated семьи с `T` / `List<T>` в source/destination | Bare и все 15 перегрузок чистые |

MSBuild SDK 10 независимо подтвердил attributed case, case с двумя конкретными leaves и положительный контроль с `base.Configure`.

Причина пропуска существующим suite установлена: `Related_CRTP_families_do_not_create_competing_extensions` вызывает `base.Configure`. В новом отрицательном случае это ребро переиспользования отсутствует, и появляются два применимых набора расширений. Guide явно разрешает не включать базовую конфигурацию. Делать base-вызов обязательным ради overload resolution нельзя без изменения согласованной пользовательской модели.

## 7. Объём генерации и recovery

В каждой отдельной consumer compilation матрицы с одной полной парой получено 5 файлов: TypeMapper, Construction, Member, MappingExtension, MemberExtension; всего 15 расширений. IVT не увеличивает локальное число файлов — добавляет внешние конкурирующие кандидаты.

Для двух generic families внутри одной сборки получено 8 файлов: два TypeMapper, по два MappingExtension/MemberExtension, общие Construction/Member. Всего 30 расширений. Для unrelated families дополнительные методы сохраняют family constraints. Для related families без base-вызова эти 30 методов создают S03-03; с base-вызовом в контрольной пробе остаётся 6 файлов и 15 расширений. Это конкретный случай некорректного дублирования, а не основание сокращать все категории DSL.

Структурные причины сопоставлены с [MappingSurfacePolicy](../../src/Morphant.Generator/MappingPair/MappingSurfaceModel.cs), [canonical coordination](../../src/Morphant.Generator/MappingPair/CanonicalMappingPairPipeline.cs), [receiver construction](../../src/Morphant.Generator/ConstructionSurface/PairConfiguration/PairConfigurationModelBuilder.cs), [family containers](../../src/Morphant.Generator/MappingPair/GeneratedMappingExtensionNaming.cs) и naming policies. Подсчёт файлов/методов — измерение сохранённого output, не замена полным snapshot assertions.

В shared-IVT случаях с DLL/ref generator сообщает `MORPH0018` и выдаёт typed `MappingConfigurationException` stubs. При source reference `MORPH0018` отсутствует, а mapper содержит обычные методы; `CS0121` всё равно блокирует компиляцию. Исключений генератора и исчезновения всей генерации не было. Различие нельзя объявлять молчаливой успешной компиляцией; authentication/recovery следует отдельно проверить на этапах 5/9/10. `IsGeneratedMethod` учитывает syntax references, имеющиеся у source symbols и отсутствующие у metadata symbols.

## 8. Варианты и необходимые регрессии

| Направление для обсуждения | Что решает / что требуется доказать |
| --- | --- |
| Изолировать применимость shared-расширений по владельцу | Может устранить S03-01. Нужно оценить receiver, вывод типов, доступность и рост генерации; S03-02 остаётся отдельно |
| Различать generated types разных сборок | Может устранить S03-02. Сохранить короткие читаемые leaf names, явные имена/aliases, стабильность и полный input contract; S03-01 для `Using`/`Convert` остаётся |
| Переиспользовать эквивалентные surfaces связанных семейств без включения base-конфигурации либо иначе разделить применимость | Кандидат для S03-03. Нельзя попутно наследовать настройки/правила; нужно проверить различающиеся tuple presentations и constraints |
| Post-compile удаление DSL | Может убрать внешние metadata-кандидаты после обработки всех артефактов, но не устраняет source-backed и внутрисборочный случаи. Отложенное исследование не является готовым решением |

Регрессии после согласования исправлений:

- Для S03-01: все 15 overloads, friend on/off, implementation/ref/source references, отсутствие Morphant/native diagnostics в допустимых consumers.
- Для S03-02: bare и явные construction/member names, shared/scoped/family, общий destination при разных source, tuple presentations; не скрывать `CS0436` общей suppression.
- Для S03-03: base-вызов есть/нет, attributed bases и отдельные concrete leaves, одинаковые/разные constraints и пересекающиеся подстановки; проверить самостоятельность настроек и правил каждой конфигурации.
- Проверить результат исправления полноценными literal surface snapshots, compiler usage и обычными MSBuild consumers. Новые несовместимости не закреплять как ожидаемое поведение продукта.

## 9. Воспроизведение и оставшиеся границы

[Инструкция и команды](release-review-stage-03/README.md), [компактные результаты](release-review-stage-03/results.json), [compiler probe](release-review-stage-03/compiler/Program.cs). Существенные входы сохранены в репозитории локально; большие generated outputs и build logs находятся в игнорируемом `artifacts`.

На этом этапе не проверялись живой Rider/Visual Studio, Workspace graph с generated documents, дополнительная Roslyn-матрица 4.9.2, Windows/macOS и SDK 7. Новые эксперименты не заменяют этапы 7/10/11: полную матрицу tuple-форм, IDE actualization и фактическое NuGet-потребление. Подавление предупреждений или смена nullability ради обхода IVT не принимается за решение.

Q01–Q04 первого этапа уточнены: межсборочная конкуренция воспроизведена; прежний IVT model-access тест действительно недостаточен; source reference опровергает достаточность одного post-compile cleaner; совпадение FQN подтверждено. Q08 дополнен конкретным некорректным дублированием related-family extensions. Этапы 5/9/10 должны вернуться к разнице source/metadata recovery.

Внедрение выбранных после отчёта решений S03-01–03 остаётся отдельной работой. К этапу 4 автоматически не переходим. Публичная поправка про обе формы `new` уже в `main`; публикация внутренних материалов отдельно подтверждена пользователем после первоначального отказа автоматической проверки разрешений.
