# Проверки этапа 5

Обычный MSBuild consumer для [аудита callbacks и переноса C#](../RELEASE_REVIEW_STAGE_05.md).
Подключает production analyzer и вызывает generated mapper напрямую.
Исследовательские входы находятся вне solution; отрицательные варианты
проверяют диагностики и не должны собираться. Это дополнение к постоянным
compiler/integration-тестам, а не их замена.

## Воспроизведение

Из корня репозитория, SDK 10.0.100, C# 9, nullable и warnings-as-errors:

```shell
dotnet build docs/internal/release-review-stage-05/Stage05Probe.csproj -c Release -t:Rebuild -p:ReviewCase=Context -p:UseSharedCompilation=false -m:1
dotnet docs/internal/release-review-stage-05/bin/Release/net10.0/Audit.Stage05.dll
```

`ReviewCase` выбирает один файл. При смене варианта нужен Rebuild.
Не запускайте DLL после неуспешной сборки. В workspace SDK доступен через
`/workspace/morphant-tools/dotnet`. Generated output находится в
`obj/generated/<ReviewCase>`; сохраните его отдельно перед следующей сборкой.
Consumer печатает JSON expected/actual/passed и возвращает 1 при расхождении.
Исторический аудит на `3352e94`: 15 конфигураций, девять чистых сборок,
шесть ожидаемых диагностических отказов; 71 успешная runtime-проверка и
12 расхождений S05-01/S05-02. Повтор на исправленной версии `ba151fb`
сохранил все исходные ожидания: 83 из 83 runtime-проверок успешны,
отрицательные варианты дают прежние диагностики. Подробности — в
[отчёте об исправлениях](../RELEASE_REVIEW_STAGE_05_FIXES.md) и
[результатах прежнего исправления](fix-results.json).

После перехода к единому constructor/member-значению исходная матрица снова
прошла: 15 конфигураций, 83 runtime-проверки. См.
[текущий отчёт](../RELEASE_REVIEW_STAGE_05_MEMBER_VALUES.md) и
[результаты](unified-member-results.json). `ResultDependency` отдельно
фиксирует существующее поведение для обсуждения; он не означает принятия
нового исключения из единого правила.

| ReviewCase | DefineConstants | Что проверяем |
| --- | --- | --- |
| Context | — | Caller-info всех callbacks, nameof/aliases, extensions, overloads, checked/unchecked |
| Evaluation | — | Branch/local evaluation, неактивные init-правила, deferred capture, conditional extension, throw |
| Runtime | — | Порядок, циклы, mutation, finally, method group/delegate/anonymous method, фабрика и resolver |
| Binding | — | Чужие Auto/Ignore/Map/Value и явное member-правило при constructor convention |
| Binding | ORDINARY_VALUE | Контроль того же правила с обычным арифметическим выражением |
| Binding | PARAMETERLESS | Контроль без constructor parameter с именем члена |
| Binding | EXPLICIT_CONSTRUCT | Явный конструктор сохраняет member-правило |
| Binding | BY_CONVENTION | Конструирование через ByConvention и явное member-правило |
| ResultDependency | — | Наблюдаемое значение аргумента 7, результат 17 и два назначения для result-зависимого правила; решение ожидает обсуждения |
| InheritedBinding | — | Привязка nonvirtual/static и virtual dispatch после переноса из базового mapper |
| InheritedBinding | BASE_ACCESS | Явный base-вызов в импортируемом callback: ожидается MORPH0028 |
| Binding | CAPTURE | Configure-local в Members: ожидается MORPH0030 |
| Binding | RUNTIME_CAPTURE | Configure-local в Convert: ожидается MORPH0030 |
| Binding | LOOP | Цикл в Members: ожидается MORPH0031 |
| Binding | MUTATION | Мутация result: ожидается MORPH0032 |
| Binding | FOREIGN_CALLBACK | Чужой Members на mapping chain: ожидается MORPH0018 |

Для отрицательного варианта добавьте, например, `-p:ReviewCase=Binding
-p:DefineConstants=CAPTURE`. Ожидания таблицы сверяются с реальным результатом
в [итоговом отчёте](../RELEASE_REVIEW_STAGE_05.md) и
[результатах](results.json). Исходный прогон отдельно сохраняет ошибки входов;
он не выдаётся за результат исправленных примеров.
