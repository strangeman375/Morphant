# Полная проверка Morphant: этап 1

Дата: 2026-09-05. План: [RELEASE_REVIEW_PLAN.md](RELEASE_REVIEW_PLAN.md).

Статус: инвентаризация выполнена; отчёт ожидает оценки пользователя. Этапы 2–13 не начаты. Этот этап фиксирует область и доказательства для дальнейшего ревью, а не заключение о готовности продукта к релизу.

## 1. Исходная версия и границы результата

- Исходный продукт: `7759f41114fcbf117bffb9f776b80ba88d16f59d`, `fix: preserve tuple presentation during substitution`.
- План опубликован перед началом этапа: `536ed9baa451a061b8e684cd1bae4349dc0e98da`. Его единственное отличие от исходной версии — новый внутренний документ.
- На старте канонический checkout `/workspace/Morphant`, ветка `main`, соответствовал remote; незакоммиченных изменений не было. Во время этапа исходники продукта, тесты и сборочная конфигурация не менялись.
- В [Morphant.Product.props](../../src/Morphant.Product.props) ещё указаны `Version=0.4.0` и `AssemblyVersion=0.4.0.0`; новые изменения описаны в `Unreleased` [CHANGELOG.md](../../CHANGELOG.md). Номер и release notes следующего пакета пока не являются зафиксированным результатом этого аудита.

Проверено статически: структура продукта, проекты и их зависимости, CI, перечень возможностей и диагностик, расположение тестов, категории генерации и заявленные ограничения. Дополнительно проверены локальная инструментальная готовность и состояние CI исходного commit. Содержательная полнота каждой возможности и каждого теста остаётся предметом соответствующего этапа.

## 2. Версии и матрица окружений

Три разные оси проверки: язык пользовательского кода; Roslyn, против которого собирается генератор или работает unit-driver; compiler host, реально загружающий analyzer. `netstandard2.0` у библиотеки не означает, что проверены все совместимые с ним runtime и IDE.

| Область | Зафиксированная конфигурация | Источник и граница вывода |
| --- | --- | --- |
| Пользовательский контракт | C# 9+, host совместимый с Roslyn 4.4.0+, runtime совместимый с .NET Standard 2.0 | [README](../../README.md), [ограничения](../limitations.md). Это заявленная поддержка, а не исчерпывающий список пройденных окружений |
| Сборка репозитория | SDK 10.0.100, `rollForward=latestPatch`, prerelease отключены | [global.json](../../global.json). Production source использует `LangVersion=latest`; это не минимум языка потребителя |
| Runtime, generator, build tasks | Все три проекта имеют TFM `netstandard2.0` | Их `.csproj`; продукт подписывается strong name |
| Базовые Roslyn-зависимости | `MorphantRoslynVersion=4.4.0`; свойство можно переопределить | [Directory.Build.props](../../src/Directory.Build.props), generator/unit-test `.csproj` |
| Дополнительный Roslyn в CI | 4.9.2 | Пересобираются и generator, и unit-проект. Это проверка исходников под этой зависимостью, не отдельное доказательство загрузки неизменённого бинарника, собранного с 4.4.0 |
| Unit и integration runner | `net10.0`, NUnit 4.3.2, adapter 5.0.0, Microsoft.NET.Test.Sdk 17.14.0 | [unit project](../../src/tests/Morphant.Generator.UnitTests/Morphant.Generator.UnitTests.csproj), [integration project](../../src/tests/Morphant.Generator.IntegrationTests/Morphant.Generator.IntegrationTests.csproj) |
| Обычные consumer-проекты | C# 9, C# 11, `latest`; отдельный C# 9 consumer настроек сборки; TFM `net10.0` | Четыре проекта `Morphant.Generator.IntegrationTests.*`; generator подключён как analyzer через `ProjectReference` |
| Потребление NuGet | C# 9 / `net10.0`; сценарии multi-targeting `netstandard2.0;net10.0` | `PackageTests.Consumer`, `I/PackageConsumptionTests.cs` |
| Минимальный SDK consumer | SDK 7.0.100, roll-forward отключён, C# 9 / `net7.0`; CI обозначает MSBuild 17.4 | [consumer global.json](../../src/tests/Morphant.Generator.PackageTests.MinimumSdkConsumer/global.json), [CI](../../.github/workflows/ci.yml) |
| Неподдерживаемые/ошибочные подключения | C# 8, отсутствующий/неполный/несовместимый/неоднозначный runtime, suppression и severity overrides | Соответствующие отрицательные сценарии среди 30 проектов `CompatibilityFixtures`; рядом есть корректные контрольные случаи. Негативные fixtures не расширяют поддержку |
| Локальная среда этапа | Ubuntu 24.04, `linux-x64`, SDK 10.0.100, MSBuild 18.0.2, .NET runtime 10.0.0 | Успешный `dotnet --info`; установлен один SDK в использованном dotnet root |

### Подтверждённый CI исходного commit

На 2026-09-05 через GitHub API проверен [CI run 33903733018](https://github.com/strangeman375/Morphant/actions/runs/33903733018), привязанный именно к `7759f41114fcbf117bffb9f776b80ba88d16f59d`. Он завершился 2026-09-04. Проверены `conclusion` job и соответствующих build/test steps; скачивания и нового анализа всех TRX/coverage-файлов CI на этом этапе не было.

| Проверка | Наблюдаемое доказательство |
| --- | --- |
| Ubuntu, базовый Roslyn 4.4.0 | Build, unit, integration и проверка порогов покрытия — `success` |
| Windows | Build, unit, integration — `success` |
| macOS | Build, unit, integration — `success` |
| Roslyn 4.9.2 | Build и unit — `success` |
| Пакет в SDK 7.0.100 / `net7.0` | Сборка пакета современным SDK, затем build/run потребителя минимальным SDK с Git snapshot — `success` |

Это исторические результаты на точной исходной версии, а не новые полные прогоны текущего этапа. Labels `windows-latest`/`macos-latest`/`ubuntu-latest` записаны как labels CI; точные версии образов этим отчётом не устанавливаются.

### Матрица дальнейшей проверки

| Сценарий | Что уже есть | Что необходимо проверить в аудите |
| --- | --- | --- |
| Текущий CLI, C# 9/11/latest | Реальные analyzer-backed consumers и успешный исходный CI | Поведение новых направленных сценариев; этапы 3–9, 11 |
| Минимальный SDK 7.0.100 | Успешный отдельный package consumer в CI | Повтор на окончательном пакете после исправлений; этапы 11, 13 |
| Roslyn 4.4.0/4.9.2 | Два unit-режима | Разделить пересборку generator и host-совместимость одного поставляемого binary; этапы 10–11 |
| Две сборки с одинаковыми парами и IVT | Есть проверка доступности внутренних model types; нет найденной регрессии для двух генерирующих DSL сборок | Implementation DLL, reference assembly и source project reference отдельно; этапы 3, 10–11 |
| Workspace / IDE | Есть `CompilationReference`-актуализация в unit-тестах | Workspace с двумя generator projects; затем доступные реальные IDE. Доказательства реального Rider/Visual Studio пока не получены; этап 10 |
| Debug/Release, clean/rebuild, multi-TFM, транзитивное подключение | Есть package/snapshot сценарии | Проверить точные границы существующих тестов и реальные consumers, а не только наличие `buildTransitive` assets; этап 11 |
| Остальные совместимые runtime, платформы и host-версии | Широкая формулировка поддержки | Не считать автоматически проверенными по TFM библиотеки. Уточнить достаточную release-матрицу на этапах 2 и 11 |

## 3. Компоненты и структура тестов

Короткие обозначения в реестре ниже — относительные пути от корня репозитория:

- `R/` — [src/Morphant](../../src/Morphant/): публичный runtime, DSL-контракты, delegates, markers, настройки и exceptions.
- `G/` — [src/Morphant.Generator](../../src/Morphant.Generator/): регистрация, конфигурация, построение кода, диагностики и incremental pipeline.
- `B/` — [src/Morphant.Build.Tasks](../../src/Morphant.Build.Tasks/): жизненный цикл Git snapshot и ошибки MSBuild.
- `U/` — [src/tests/Morphant.Generator.UnitTests](../../src/tests/Morphant.Generator.UnitTests/).
- `I/` — [src/tests/Morphant.Generator.IntegrationTests](../../src/tests/Morphant.Generator.IntegrationTests/).

[Morphant.slnx](../../src/Morphant.slnx) содержит 10 проектов: 3 production, 2 test runner, 4 consumer-среза и `UnitTests.TestAssets`. Дополнительно за пределами solution находятся 30 compatibility fixture проектов и 2 package consumer проекта. Поэтому одна успешная сборка solution сама по себе не означает выполнение всех consumer-сценариев.

В unit-проекте найдено 227 `.cs` файлов, включая 19 в `TestUtils`; в integration runner — 121, включая 4 в `TestUtils`. Это размеры исходной структуры, не число тестов и не метрика полноты. Отдельно существуют исходники четырёх компилируемых consumer-проектов. В CI настроены пороги line/branch coverage: generator 85/75, runtime 75/75, build tasks 50/40; они не заменяют проверку ожидаемого поведения.

## 4. Реестр возможностей и контроль полноты

Статус всех строк: **включено в аудит, инвентаризация выполнена, содержательная проверка впереди**. Колонка тестов указывает найденные места проверки, не гарантирует полноту покрытия. На этапе 13 необходимо закрыть каждую строку результатами указанных этапов.

| ID / область | Основная реализация | Документация | Найденные тесты | Этапы |
| --- | --- | --- | --- | --- |
| F01. Совместимость языка, runtime и generator | `G/Compatibility/`, `R/Morphant.csproj` | [README](../../README.md), [diagnostics](../diagnostics.md) | `U/CompatibilityDiagnosticsTests/`, `I/CompatibilityDiagnosticsTests.cs`, compatibility fixtures | 3, 9, 11 |
| F02. Объявления маппера, self-type, generic/CRTP, вложенность и доступность | `R/TypeMapper.cs`, `G/MapperDeclaration/` | [Map](../api/map.md), [generated code](../generated-code.md), [limitations](../limitations.md) | `U/MapperDeclarationTests/`, `I/DeclarationDiagnosticsTests.cs` | 2–3, 9 |
| F03. Регистрация пар, идентичность и DSL scope, конкуренция расширений | `G/MappingPair/`, `G/MapperTypeSubstitution.cs` | [Map](../api/map.md), [tuple mapping](../tuple-mapping.md), [generated code](../generated-code.md) | `U/MappingRegistrationTests/`, `U/GeneratedExtensionCollisionTests.cs`, `I/RegistrationDiagnosticsTests.cs`, `I/CompiledConsumerTests.cs` | 2–3, 7, 10–11 |
| F04. Категории, объём и имена генерируемых сущностей | `G/ConstructionSurface/`, `G/MemberSurface/`, `G/GeneratedPlanNaming.cs`, `G/GeneratedSourceHintName.cs`, `G/Incrementality/DestinationPlanCoordination.cs` | [API](../api/README.md), [generated code](../generated-code.md), `AGENTS.md` | `U/ConstructionSurfaceTests/`, `U/MemberSurfaceTests/`, `U/GeneratedNameLengthTests.cs`, `U/GeneratedExtensionCollisionTests.cs` | 2–4, 7, 13 |
| F05. `Create`/`Update`, переиспользование и замена результата, хелперы | `R/TypeMapper.cs`, `R/TypeMapperExtensions.cs`, `G/TypeMapperGeneration/TypeMapperEmitter.cs` | [Create and Update](../create-and-update.md) | `U/TypeMapperMappingModeTests.cs`, `U/TypeMapperCreationResultTests/`, `I/TypeMapperMappingModeTests.cs`, `I/TypeMapperRuntimeConstructionTests/` | 2, 4, 8 |
| F06. Convention-конструкторы и отбор обязательных входов | `G/TypeMapperGeneration/ConventionConstructorMappingPlanner.cs`, `G/ConstructionSurface/ConstructionPlan/` | [conventions](../conventions.md), [constructor selection](../settings/constructor-selection.md) | `U/TypeMapperConstructorSelectionTests/`, `U/TypeMapperConventionTests/`, соответствующие группы `I/` | 4, 6 |
| F07. Декларативные `Construct`/`Resolve`, параметры, markers | `R/Delegates/`, `R/Markers/`, `R/Members/`, `G/TypeMapperGeneration/StructuredConstructMappingPlanner.cs`, `G/TypeMapperGeneration/UserResultMappingPlanner.cs` | [Construct](../api/construct.md), [Resolve](../api/resolve.md), [declarative mapping](../declarative-mapping.md) | `U/TypeMapperStructuredConstructTests/`, `U/ConstructionDiagnosticsTests/`, `I/TypeMapperStructuredConstructTests/` | 2, 4–5, 7 |
| F08. Runtime callbacks: `ConstructUsing`/`ResolveUsing`/`Convert` | `R/Delegates/`, `G/TypeMapperGeneration/RuntimeCallbackMethodPlanner.cs`, `G/TypeMapperGeneration/ManualConvertMappingPlanner.cs` | [ConstructUsing](../api/construct-using.md), [ResolveUsing](../api/resolve-using.md), [Convert](../api/convert.md) | `U/TypeMapperConvertTests/`, `U/CallbackDiagnosticsTests/`, `I/TypeMapperRuntimeConstructionTests/`, `I/TypeMapperConvertTests/` | 2, 4–5, 8 |
| F09. Члены, conventions/явные правила, mutability, init/required | `G/MappingPair/DestinationMemberPolicy.cs`, `G/MemberSurface/`, `G/TypeMapperGeneration/ConventionMemberMappingPlanner.cs` | [Members](../api/members.md), [member selection](../settings/member-selection.md), [conventions](../conventions.md) | `U/TypeMapperMemberTests/`, `U/MemberDiagnosticsTests/`, `I/TypeMapperMemberTests/`, C# 11 consumer | 4–5, 7 |
| F10. Nullable-контракты, атрибуты, `Option<T>`, null/default | `R/Option.cs`, `R/Members/`, `G/MappingPair/MappingTypeNormalization.cs`, `G/TypeMapperGeneration/` | [null handling](../settings/null-handling.md), [Create and Update](../create-and-update.md) | `U/OptionTests.cs`, `U/MappingInterfaceNullabilityTests.cs`, `U/TypeMapperNullHandlingTests.cs`, `I/TypeMapperNullHandlingTests.cs` | 4, 7–8 |
| F11. Configuration flow, распознавание DSL, перенос и вычисление выражений | `G/TypeMapperConfigure/`, `G/PairConfiguration/BuilderFlowAnalyzer.cs`, `G/TypeMapperGeneration/TransferableLambdaSyntax.cs`, planners/rewriters в `G/TypeMapperGeneration/` | [declarative expressions](../api/declarative-expressions.md), [declarative mapping](../declarative-mapping.md), [manual mapping](../manual-mapping.md) | `U/MapperConfigurationTests/`, `U/TypeMapperExpressionTransferTests.cs`, `U/TypeMapperDeclarativeControlFlowTests.cs`, `U/TypeMapperDependencyGraphTests.cs`, `I/TypeMapperEvaluationTests/` | 3, 5, 9 |
| F12. Восемь настроек, defaults, приоритет и suppression | `R/MapperBuilderBase.cs`, setting enums в `R/`, `G/Settings/`, `R/build/Morphant.props` | [settings](../settings/README.md) и все страницы `docs/settings/` | `U/MappingSettingsDiagnosticsTests/`, `U/MapperConfigurationTests/`, `I/SettingsDiagnosticsTests.cs`, AssemblySettings consumer, Settings fixtures | 2, 6, 9, 11 |
| F13. `base.Configure`, `IncludeBase`, наследование правил и overrides | `G/PairConfiguration/`, `G/MapperTypeSubstitution.cs`, `R/MappingBuilder.cs` | [configuration inheritance](../configuration-inheritance.md), [IncludeBase](../api/include-base.md) | `U/TypeMapperInheritanceTests/`, `U/InheritanceDiagnosticsTests/`, `U/MappingCompositionTests/`, `I/TypeMapperInheritanceTests/` | 3, 6–7 |
| F14. Flattening | `G/TypeMapperGeneration/ConventionSourceMemberResolver.cs`, `G/TypeMapperGeneration/FlatteningDiagnosticAnalyzer.cs`, `G/Incrementality/FlatteningSemanticDependencyBuilder.cs` | [flattening](../flattening.md), [setting](../settings/flattening.md) | `U/FlatteningTests/`, `I/FlatteningTests.cs` | 4, 6, 10 |
| F15. `IncludeMembers` и взаимодействие с conventions | `R/MappingBuilder.cs`, `G/TypeMapperGeneration/IncludedSourceMemberSet.cs`, `G/TypeMapperGeneration/IncludeMembersDiagnosticAnalyzer.cs` | [guide](../include-members.md), [API](../api/include-members.md) | `U/IncludeMembersTests/`, `I/IncludeMembersTests.cs` | 5–7 |
| F16. Вложенные маппинги, внутренний/внешний lookup | `G/TypeMapperGeneration/DeclarativeNestedMapExpression.cs`, `G/TypeMapperGeneration/NestedMappingDiagnosticAnalyzer.cs`, `R/Context/` | [nested mapping](../nested-mapping.md) | `U/TypeMapperNestedMapTests/`, `U/NestedMappingDiagnosticsTests/`, `I/TypeMapperNestedMapTests/` | 4–5, 8–9 |
| F17. Все поддерживаемые формы кортежей, имена и state | `G/MappingPair/BclTupleShape.cs`, `G/ConstructionSurface/BclTuplePlanModelBuilder.cs`, `G/TypeMapperGeneration/BclTupleMappingPlanner.cs`, `G/MapperTypeSubstitution.cs` | [tuple mapping](../tuple-mapping.md) | tuple-сценарии в `U/MappingRegistrationTests/`, `U/ConstructionSurfaceTests/`, `U/MemberSurfaceTests/`, `U/ActualizationTests/`, `I/TupleMappingTests/` | 3–7, 10, 13 |
| F18. `ForDerived`, выбор ветви, неизвестный runtime type | `R/MappingBuilder.cs`, `R/Mapper.cs`, `G/PairConfiguration/PolymorphismDiagnosticPipeline.cs`, `G/TypeMapperGeneration/` | [runtime polymorphism](../runtime-polymorphism.md), [ForDerived](../api/for-derived.md), [setting](../settings/unknown-derived-type-handling.md) | `U/RuntimePolymorphismTests/`, `I/RuntimePolymorphismTests/` | 6, 8–9 |
| F19. `IMapper`, `ITypeMapper`, DI и неоднозначность регистрации | `R/Mapper.cs`, `R/TypeMapper.cs`, `R/TypeMapperExtensions.cs` | [runtime dispatch](../runtime-dispatch.md), [quick start](../quick-start.md) | `U/MapperRuntimeTests.cs`, `I/MapperDispatchTests/`, `I/TypeMapperStandaloneDispatchTests.cs`, `I/CompiledConsumerTests.cs` | 2, 8 |
| F20. Context/scope, delegates, exceptions и lifetime | `R/Context/`, `R/Delegates/`, `R/Exceptions/` | [runtime dispatch](../runtime-dispatch.md), [exceptions](../exceptions.md), callback API | `U/MappingDelegateTests.cs`, `U/MapperRuntimeTests.cs`, `I/TypeMapperCallbackTests/`, `I/TypeMapperNestedMapTests/` | 5, 8–9 |
| F21. Диагностики, typed stubs, независимость пар, защита от сбоев | `G/*DiagnosticDescriptors.cs` и вложенные каталоги, `G/GeneratorStageGuard.cs`, `G/DiagnosticPipeline.cs`, `G/TypeMapperGeneration/` | [diagnostics](../diagnostics.md), 60 help pages, [exceptions](../exceptions.md) | все diagnostic-группы `U/` и `I/`, `U/DiagnosticCatalogAuditTests.cs`, `U/GeneratorFailureDiagnosticsTests/`, `U/TypeMapperObservableFailureTests.cs` | 3–9, 13 |
| F22. Incrementality, actualization, удаление результатов и location | `G/MorphantGenerator.cs`, `G/Incrementality/`, `G/DiagnosticLocationActualizer.cs`, production pipelines | [generated code](../generated-code.md), [testing guidelines](TESTING_GUIDELINES.md) | `U/IncrementalityTests/`, `U/ActualizationTests/`, `U/TestUtils/GeneratorIncrementalityTest.cs`, `U/TestUtils/GeneratorActualizationTest.cs` | 10, 13 |
| F23. Git snapshots и MSBuild-ошибки | `R/build/`, `B/` | [generated code](../generated-code.md) | `U/GitSnapshotTests/`, `I/PackageConsumptionTests.cs`, MinimumSdkConsumer | 9, 11 |
| F24. NuGet assets, strong name, host loading и release pipeline | `R/Morphant.csproj`, `src/Morphant.Product.props`, `.github/workflows/` | [README](../../README.md), [quick start](../quick-start.md), [CHANGELOG](../../CHANGELOG.md), package metadata | `U/StrongNameIdentityTests.cs`, `U/CompatibilityDiagnosticsTests/`, `I/PackageConsumptionTests.cs`, package consumers и CI | 2, 11–13 |
| F25. Публичная документация, XML и отложенные исследования | `docs/`, `README.md`, XML в `R/` и emitters | [documentation index](../README.md), три внутренних research-документа | `U/DiagnosticCatalogAuditTests.cs`, `U/PublicApiBaselineTests.cs`, consumer-примеры; полный прогон примеров ещё предстоит | 2, 12–13 |
| F26. Достоверность тестов, snapshots и межфункциональные сочетания | `U/TestUtils/`, `I/TestUtils/`, consumer slices, `eng/` | [testing guidelines](TESTING_GUIDELINES.md), [testing mappings](../testing.md) | `U/ProductionCompositionTests.cs`, surface/actualization suites, integration consumers | Все этапы, итог на 13 |

## 5. Инвентаризация генерации

Это исходная модель объёма по emitters/pipelines. Не объявляем все перечисленные сущности необходимыми только потому, что они уже генерируются. Их необходимость, переиспользование, доступность и отсутствие вредного дублирования предстоит подтвердить.

| Категория hint name | Содержимое | От чего зависит появление и объём |
| --- | --- | --- |
| `TypeMapper` | Partial маппер, реализации `ITypeMapper<,>`, методы операций и необходимые runtime-хелперы | От маппера, его эффективных пар, конфигурации и recovery-состояния |
| `Construction` | Внутренний sealed class construction-плана; при наличии parameter fields — отдельный sealed class `ConstructorParameters` с полями | От доступного construction-контракта destination и координации планов; число конструкторов/параметров меняет состав |
| `Member` | Внутренний sealed record member-плана с properties и явно заданными служебными методами record | От доступной member surface; число и mutability членов меняют состав. Синтезируемые компилятором record members тоже необходимо учитывать в оценке объёма сборки |
| `MappingExtension` | `Construct`, `Resolve`, `ConstructUsing`, `ResolveUsing`, `Convert` | 7 методов в каждом выводе этого emitter; ещё 4 при `HasStructuredConstruction` |
| `MemberExtension` | Четыре формы `Members` | 4 метода, когда выводится соответствующая member surface |
| `GeneratorFailure` | Текстовый отчёт об исключении генератора | Только при соответствующем перехваченном сбое; это не штатный набор DSL-типов |

Источники: [construction emitter](../../src/Morphant.Generator/ConstructionSurface/ConstructionPlan/ConstructionPlanEmitter.cs), [mapping extensions emitter](../../src/Morphant.Generator/ConstructionSurface/PairConfiguration/PairConfigurationEmitter.cs), [member emitter](../../src/Morphant.Generator/MemberSurface/MemberPlan/MemberPlanEmitter.cs), [member extensions emitter](../../src/Morphant.Generator/MemberSurface/PairConfiguration/MemberConfigurationEmitter.cs), [failure guard](../../src/Morphant.Generator/GeneratorStageGuard.cs).

Таким образом, для одной полной конфигурационной surface получается **15 extension-методов**: 2 `Construct`, 2 `Resolve`, 2 `ConstructUsing`, 2 `ResolveUsing`, 3 `Convert`, 4 `Members`. Это число методов одной surface, а не формула «15 × количество `Map`» для всего проекта: необходимо учитывать shared/scoped/family правила, переиспользование планов и недоступные поверхности.

`Map`, common settings, `IncludeBase`, `IncludeMembers` и `ForDerived` находятся в runtime-DSL контрактах. Измерение количества файлов также не равно количеству типов: partial extension contributions могут объединяться в один контейнер.

Правила scope сверены с [MappingSurfaceModel.cs](../../src/Morphant.Generator/MappingPair/MappingSurfaceModel.cs):

- Generic-параметр в source/destination приводит к family-scoped surface.
- Иначе `ValueTuple`, nullable reference или `dynamic` рекурсивно приводят к mapper scope; при self-параметре семейства — к family scope.
- Остальные пары используют shared surface. `System.Tuple` сам по себе не требует mapper scope.
- Выбор основан на объявленной паре до generic-подстановки. Shared receiver использует `MapperBuilderBase<MappingBuilder<TMapper, S, D>>`; scoped/family receiver использует соответствующий `MappingBuilder`.

Обязательные дальнейшие вопросы по объёму: когда один destination получает несколько планов; когда повторное объявление/наследование даёт лишнюю surface; как число scoped/family surfaces связано с нужным пользовательским различием; какие методы действительно должны быть доступны при ограничениях destination и recovery; не создаёт ли сокращение поверхности потерю вывода типов, обязательности аргументов или новых конфликтов. Сокращение ради числа не является выбранным решением.

## 6. Реестр диагностик

Статическое сопоставление обнаружило **60 уникальных generator-кодов и ровно 60 соответствующих help pages**, `MORPH0001`–`MORPH0060`. Все коды имеют буквальное упоминание в test sources; это включает catalog audit и поэтому не доказывает отдельного поведенческого теста каждого условия.

| Область descriptor | Коды | Основные test-группы |
| --- | --- | --- |
| Compatibility | 0001–0004 | `CompatibilityDiagnosticsTests` |
| Declaration | 0005–0010, 0034, 0058–0059 | `MapperDeclarationTests`, `DeclarationDiagnosticsTests` |
| Registration | 0011–0014, 0056, 0060 | `MappingRegistrationTests`, `RegistrationDiagnosticsTests` |
| Configuration flow | 0015–0018 | `MapperConfigurationTests`, `ConfigurationDiagnosticsTests` |
| Composition | 0019–0020 | `MappingCompositionTests`, `CompositionDiagnosticsTests` |
| Settings | 0021–0023 | `MappingSettingsDiagnosticsTests`, `SettingsDiagnosticsTests` |
| Inheritance | 0024–0028 | `InheritanceDiagnosticsTests` |
| Callbacks | 0029–0033 | `CallbackDiagnosticsTests` |
| Construction | 0035–0039 | `ConstructionDiagnosticsTests` |
| Members | 0040–0043 | `MemberDiagnosticsTests` |
| Nested mapping | 0044–0046 | `NestedMappingDiagnosticsTests` |
| Completeness | 0047–0048 | `MappingCompletenessDiagnosticsTests` |
| IncludeMembers | 0049–0050 | `IncludeMembersTests` |
| Flattening | 0051 | `FlatteningTests` |
| Polymorphism | 0052–0055 | `RuntimePolymorphismTests` |
| Generator failure | 0057 | `GeneratorFailureDiagnosticsTests` |

Полный префикс каждой числовой записи — `MORPH`. На этапе 9 для каждого кода нужна проверка конкретных условий, отсутствия ложных срабатываний, severity/suppression, source spans, дополнительных locations, восстановления и сохранения независимых пар. Тест каталога этого не заменяет.

В build tasks и `.targets` найдено **15 MSBuild-кодов**: `MORPHANTMSB001`–`008`, `015`, `016`, `017`, `019`, `020`, `021`, `999`. Нумерация с пропусками сама по себе не является дефектом.

У `MORPHANTMSB007`, `008`, `015`, `019`, `999` не найдено буквального упоминания в C# test sources. Это **кандидаты на проверку покрытия**, а не доказанное отсутствие косвенного выполнения. Основные места проверки — `U/GitSnapshotTests/GitSnapshotLifecycleTests.cs` и `I/PackageConsumptionTests.cs`; на этапах 9/11 нужно установить, проверяются ли сам код ошибки, сообщение, отказ до мутации файлов и сохранность данных.

## 7. Исходный реестр проблем, гипотез и границ

| ID | Классификация и установленный факт | Что ещё требуется | Этап |
| --- | --- | --- | --- |
| Q01 | Ранее зафиксированная проблема IVT. [Исследование удаления DSL](DSL_ARTIFACT_STRIPPING_RESEARCH.md) описывает успешную metadata-only пробу конкурирующих shared extensions. В текущем этапе новый воспроизводящий consumer не запускался | Заново воспроизвести на исходном commit для implementation/ref/source references, сохранить минимальные проекты и native/Morphant diagnostics. До этого нельзя считать проблему ни исправленной, ни заново подтверждённой этим аудитом | 3 |
| Q02 | Подтверждённый пробел найденной тестовой матрицы: единственный явный IVT-case в test sources проверяет доступность внутренних **model types**. Он не генерирует DSL в обеих сборках | Добавить к направленной проверке два независимых маппера одной пары в friend assemblies; сравнить с отсутствием IVT. Отдельно shared/scoped/family | 3, 10–11 |
| Q03 | Незакрытый вопрос дизайна отложенного cleaner: в research его устранение cross-assembly конкуренции обосновано очисткой implementation/ref/refint. Проверка source project references/IDE для этого утверждения отсутствует | Установить действительную модель ссылок в Workspace/IDE. Не считать post-compile очистку уже работающим или достаточным решением IDE-конкуренции | 3, 10, 12 |
| Q04 | Гипотезы: совпадающие FQN generated plan types через IVT; пересекающиеся generic/CRTP-family подстановки; влияние constraints и общего receiver | Воспроизводимые примеры overload resolution и type lookup. Внутрисборочные collision-тесты не закрывают межсборочный случай | 3 |
| Q05 | Подтверждённая граница доказательств: CI Roslyn 4.9.2 пересобирает generator с 4.9.2. Реальные package consumers покрывают SDK 7/10 отдельно | Явно разложить release-матрицу на «что собираем» и «чем загружаем», затем проверить необходимые сочетания одного поставляемого binary | 11 |
| Q06 | Кандидаты пробелов тестов MSBuild: пять кодов без буквального упоминания в C# тестах | Проверить фактический путь и наблюдаемые assertions; добавить регрессии после согласования необходимых изменений | 9, 11 |
| Q07 | Вопрос release-подготовки: product version и package release notes относятся к 0.4.0, API меняется в `Unreleased` | Выбрать версию, проверить binary/source compatibility и migration notes. Не менять версию в ходе инвентаризации | 2, 11–12 |
| Q08 | Непроверенная необходимость объёма генерации: найдены 6 категорий outputs и до 15 extension-методов на полную surface | Доказать необходимость/достаточность отдельных surface и координации планов, оценить условия роста без потери API | 2–4, 7, 13 |

Намеренные ограничения из [limitations](../limitations.md) занесены в границу проверки: автоматическое отображение элементов коллекций/словарей/буферов, `IQueryable`, unflattening, patch missing/null/default, автоматическая immutable-реконструкция, keyed/discriminator mappings, недоступные nested mappers, cycles/shared references, cross-assembly configuration inheritance, extern-alias-only/неоднозначные global-контракты, generated DI registration, configurable enum mapping, reverse/before/after/async mapping. Отсутствие этих возможностей не считается дефектом автоматически; проверяем честность границы, диагностики там, где они обещаны, и взаимодействие с поддерживаемыми путями.

[Удаление DSL](DSL_ARTIFACT_STRIPPING_RESEARCH.md), [null-assignment/patch](NULL_ASSIGNMENT_HANDLING_RESEARCH.md) и [reference handling](REFERENCE_HANDLING_RESEARCH.md) имеют статус отложенных исследований. Ни cleaner, ни его возможные атрибуты/manifest, ни предложенное изменение receiver на основе covariance не считаются реализованными или выбранными этим аудитом.

## 8. Новые проверки инструментов на этом этапе

### Unit smoke

Локально выполнен ограниченный набор существующих тестов: `GeneratedExtensionCollisionTests`, `MapperFamilyParameterTests`, `TuplePresentationTests`, `DiagnosticCatalogAuditTests`, `PublicApiBaselineTests`, `OptionTests`, `TupleBrokenEditRecoveryActualizationTests`.

Команда запускалась с SDK 10.0.100, `--configuration Release --no-restore -p:MorphantRoslynVersion=4.4.0 -p:UseSharedCompilation=false -m:1 -nodeReuse:false`, переменными `DOTNET_CLI_TELEMETRY_OPTOUT=1`, `MSBUILDDISABLENODEREUSE=1`; фильтр — OR по `FullyQualifiedName~<имя группы>` выше. Сборка не отключалась. Фактические generator/unit assets содержат Roslyn 4.4.0.

Получен завершённый `artifacts/release-review/stage-01/unit/baseline-smoke.trx`: **48/48 passed**, failed/error/aborted/notExecuted — 0. TRX фиксирует интервал 2026-09-05 09:49:51–09:50:01 UTC; timestamps сборок generator и unit DLL соответствуют этому запуску.

Инструмент возврата консольного результата сообщил `network approval was cancelled before a decision was returned`. Поэтому финальный exit code команды и полный build stdout не получены. После этой ошибки проверены существование, время, завершённость, счётчики и состав TRX, а также отсутствие оставшегося dotnet-процесса. Успех 48 тестов установлен по TRX; ошибка транспорта не объявляется ошибкой продукта, а успешный TRX не используется для утверждения о неизвестном exit code или отсутствии всех build warnings.

### Integration smoke

Выполнен `dotnet test src/tests/Morphant.Generator.IntegrationTests/Morphant.Generator.IntegrationTests.csproj` с теми же SDK, Release/no-restore и MSBuild-параметрами, фильтром `FullyQualifiedName~CompiledConsumerTests`. Подтверждены сборка consumer-проектов с analyzer, итоговый exit code **0** и **4/4 passed**, без skipped/failed.

Проверены `CSharp9_quick_start_compiles_and_executes`, `CSharp9_unrelated_CRTP_families_compile_and_execute`, `CSharp11_required_member_consumer_compiles_and_executes`, `Multiple_assemblies_use_standard_DI_and_scoped_dependencies`. Последний тест использует также consumer с `LangVersion=latest`; это runtime/DI-проверка между сборками, не проверка IVT-конкуренции DSL.

TRX: `artifacts/release-review/stage-01/integration/baseline-consumers.trx`, 2026-09-05 09:55:12–09:55:13 UTC. Контрольные SHA-256 исходных локальных отчётов: unit — `fc3332ad99d84f8295c29f3077d60c4845584008e9d26f019078b971cfc73a4b`; integration — `caf77cb47fa22f7eab55b1e5719da8aa1761605e5d64a3a97ccc68dc39e34c09`. Сами build/TRX artifacts не входят в git; результаты и границы доказательств сохранены здесь.

### Ограничения

Новые полные unit/integration прогоны, измерение покрытия, новые дефектные consumer-проекты и реальная IDE-проверка в этот этап не включались. Исходный CI проверен отдельно. Успешный существующий тест на коллизии не доказывает отсутствие конкуренции в неохваченном IVT-сценарии.

## 9. Результат этапа и условие продолжения

Область аудита закреплена в F01–F26; диагностический каталог, категории генерации, матрица окружений и исходные вопросы Q01–Q08 зафиксированы. Есть проверяемый успешный CI точной исходной версии и ограниченная новая проверка локального test tooling. Новых подтверждённых поведенческих дефектов продукта этот инвентаризационный этап не устанавливает; он выявляет конкретные границы имеющихся доказательств и направляет дальнейшее ревью.

Следующий этап по плану — **2: дизайн, публичный API и пользовательский опыт**, включая обоснованность объёма генерации и миграцию с выпущенного API. Начинать его можно только после оценки этого отчёта и команды пользователя. Исправлений продукта или выбора нового решения изоляции DSL в этом этапе нет.
