# Проверки этапа 4

Обычный MSBuild consumer для [аудита семантики и нейминга](../RELEASE_REVIEW_STAGE_04.md).
Он подключает production analyzer и напрямую вызывает generated mapper через
ITypeMapper extensions. Исследовательские входы находятся вне solution.
Они не заменяют постоянные snapshot/integration-тесты.

Материалы восстановлены из истории после отката окружения. Текущее содержимое
consumer проверено заново: 19 конфигураций, семь положительных, 86 успешных
assertions. Первоначальные 127 unit/85 integration и compile-only Roslyn 4.4
сохранены в [results.json](results.json) как исторические сведения; прежние
сырьевые логи и исходный локальный commit не восстановлены побайтово.

## Воспроизведение

SDK 10.0.100, net10.0, nullable enabled, warnings-as-errors. По умолчанию C# 9;
Modern использует C# 11. Команды из корня репозитория:

```shell
dotnet build docs/internal/release-review-stage-04/Stage04Probe.csproj -c Release -t:Rebuild -p:ReviewCase=Core -p:UseSharedCompilation=false -m:1
dotnet docs/internal/release-review-stage-04/bin/Release/net10.0/Audit.Stage04.dll
```

В текущем workspace используется `/workspace/morphant-tools/dotnet`.
ReviewCase выбирает один файл из Cases. Для варианта замените
`-p:ReviewCase=Core`, например, на
`-p:ReviewCase=ResolveConditional -p:DefineConstants=HAS_VALUE_BLOCK`.
DefineConstants из таблицы используются по одному. При смене входа нужен
Rebuild. Не запускайте DLL после неуспешного build: она может относиться
к предыдущей конфигурации.

Consumer выводит JSON с expected/actual/passed. Exit 1 означает выявленное
расхождение; намеренно негативные случаи не меняют сборку solution.
Generated sources находятся в `obj/generated/<ReviewCase>`; при сравнении
сохраните каждый вывод отдельно. Bin/obj и большие build logs не коммитятся.

| ReviewCase | DefineConstants | Результат повторного прогона |
| --- | --- | --- |
| Core | — | Чистый build, 46 проверок прошли |
| Modern | — | Чистый build, 12 проверок прошли |
| Naming | — | Короткие/полные имена, nested generic и tuples; 7 прошли |
| Naming | EXPLICIT_TUPLES | Явные aliases двух tuple-представлений; 7 прошли |
| ReservedNames | — | Чистый build, 6 из 8 значений неверны |
| ReservedNames | STRICT | Те же 6 ошибок, без completeness diagnostics |
| ReservedNames | RENAME_DESTINATION | DestinationMembers начинает маппиться; 4 из 8 значений неверны |
| ReservedNames | EXPLICIT_MEMBERS | CS0117 и CS0122 |
| StaticContainer | — | Чистый build, оба интерфейса nested-пар отсутствуют; 2 из 4 проверок не проходят |
| StaticContainer | NON_STATIC | Интерфейсы и значения восстановлены; 6 прошли |
| ResolveConditional | — | CS1729 на C# 9, 11 и latest |
| ResolveConditional | HAS_VALUE_TARGET_TYPED | CS1729 на C# 9 |
| ResolveConditional | EXPLICIT_NAME | MORPH0038 после устранения CS1729 |
| ResolveConditional | BLOCK_BODY | MORPH0038 с TryGetValue и && |
| ResolveConditional | NESTED_TRYGET | MORPH0038 с двумя вложенными if |
| ResolveConditional | HAS_VALUE | HasValue, ternary, явный construction-тип; 4 прошли |
| ResolveConditional | HAS_VALUE_BLOCK | HasValue, block, new(...); 4 прошли |

Для версий языка передайте `-p:LangVersion=9.0`, `-p:LangVersion=11.0`
или `-p:LangVersion=latest`. Успешные Resolve проверяют Create, reuse,
Update(null) и replacement.

В Core сохранение destination при null source явно настраивается через
ReturnDestination. Factory с отрицательным Id намеренно возвращает null
для проверки terminal-null пути; null-forgiving в callback относится только
к этой исследовательской границе.

## Исторические дополнительные проверки

Первоначальный compile-only Roslyn 4.4.0 / C# 9 использовал custom mode
[compiler probe этапа 3](../release-review-stage-03/compiler/CompilerProbe.csproj).
Копии входов компилировались production analyzer в сборке AuditCustom:
using scope заменялся с A_Audit_002EStage04 на A_AuditCustom; включался nullable,
подавлялся CS1591 и добавлялся пустой Check с сигнатурами Equal/Throws.
Этот Check не исполнялся и не использовался для runtime-доказательств.
При восстановлении повторялись обычные MSBuild consumers, а не этот driver.

Первоначальный unit filter объединял через `|` предикаты
`FullyQualifiedName~<имя>`: TypeMapperConventionTests,
TypeMapperConstructorSelectionTests, TypeMapperStructuredConstructTests,
TypeMapperCreationResultTests, TypeMapperMemberTests, TypeMapperNullHandlingTests,
TypeMapperMappingModeTests, TypeMapperArbitraryTypeTests, ConstructionSurfaceTests,
MemberSurfaceTests, GeneratedNameLengthTests, GeneratedPlanNamingUsageTests,
OptionTests и MappingInterfaceNullabilityTests.

Integration filter аналогично объединял TypeMapperConventionTests,
TypeMapperConstructorSelectionTests, TypeMapperStructuredConstructTests,
TypeMapperRuntimeConstructionTests, TypeMapperMemberTests,
TypeMapperNullHandlingTests, TypeMapperMappingModeTests,
TypeMapperArbitraryTypeTests и TypeMapperConvertTests.

Старые запуски использовали существующие Release DLL с --no-build --no-restore.
После восстановления окружения эти старые DLL не следует считать актуальной
сборкой тестов; для нового запуска нужно собрать соответствующий test project.
