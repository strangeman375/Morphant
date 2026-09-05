# Полная проверка Morphant: этап 4

Дата: 2026-09-05. План: [RELEASE_REVIEW_PLAN.md](RELEASE_REVIEW_PLAN.md).
Статус: проверка завершена в доступном окружении; найдены три дефекта
реализации, ошибка примеров Resolve и вопросы удобства сгенерированных имён.
Исправления и изменение дизайна ожидают обсуждения. Этап 5 не начат.

## Версия, восстановление и доказательства

Проверена версия [2695e07](https://github.com/strangeman375/Morphant/commit/2695e07429f468243dcfaeb90d5c99ca8ca874ba),
tree `9551b0e19f015a8ce589a4e84b9e805944eddf94`. Production-код и постоянные
тесты в рамках этапа 4 не менялись.

После остановки работы текущее окружение содержало старую копию после этапа 2.
Неопубликованный локальный commit `3e3a80b21eb1cf0d9ab62cc60766ea5a49ef0cd5`
и прежние scratch-логи отсутствовали. Checkout восстановлен из GitHub;
его дерево в точности совпало с указанной опубликованной версией.

Этот отчёт восстановлен из сохранённой истории; исследовательские исходники
воссозданы и проверены повторно. Это новая локальная контрольная точка,
а не побайтовое восстановление старого commit. Числа первоначального аудита
и проверки восстановленных исходников учитываются раздельно.

| Проверка | Результат и происхождение |
| --- | --- |
| Первоначальный аудит: выбранные unit-категории, Roslyn 4.4.0 | 127 passed, 0 skipped/failed; зафиксировано в истории, старый TRX не восстановлен |
| Первоначальный аудит: выбранные integration-категории | 85 passed, 0 skipped/failed; сведения из истории, не новый запуск после восстановления |
| Восстановленные Core / Modern | Чистая MSBuild-сборка, 46 / 12 runtime-проверок прошли |
| Naming: обычная форма / явные tuple aliases | 7 / 7 прошли |
| StaticContainer: контроль с обычным контейнером | 6 прошли |
| ResolveConditional: HasValue + ternary с явным именем / HasValue + block с new(...) | 4 / 4 прошли: Create, reuse, Update(null), replacement |
| Негативные восстановленные конфигурации | Подтвердили S04-01–S04-04; диагностики и неверные значения сохранены |
| Первоначальный compile-only Roslyn 4.4.0 / C# 9 | Core чист; reserved/static без диагностик и с неполным выводом; Resolve — CS1729 или MORPH0038; HasValue block чист. Это исторический результат |

Повторный прогон: Linux, SDK 10.0.100, net10.0; C# 9, для Modern — C# 11.
Nullable enabled, warnings-as-errors, production analyzer подключён обычным
ProjectReference. Runtime вызывается непосредственно в MSBuild consumer.
Всего 19 конфигураций; семь положительных дали 86 успешных assertions,
включая контрольные варианты. Негативные consumers не входят в solution.
Восстановленный Core уточнён по контракту: ReturnDestination задаётся явно;
намеренно возвращающий null factory использует null-forgiving в callback.

Входы и команды: [release-review-stage-04](release-review-stage-04/README.md).
Результаты повторного прогона, значения и происхождение исторических сведений:
[results.json](release-review-stage-04/results.json).
Полную Release-сборку, все unit/integration и multi-OS CI повторно не запускали.

## Область F04–F10

Сопоставлены public guides, planners/emitters, eligibility/naming policies,
существующие compiler/runtime fixtures и обычные MSBuild consumers.

| Область | Проверенное поведение |
| --- | --- |
| F05: Create/Update | Reuse/replacement, null source/destination, Update-only, идентичность и число init/factory-вызовов |
| F06: conventions и конструкторы | Constructor-selection fixtures; сохранение соответствующего члена при Create и назначение при Update; optional decimal/enum, пустой params |
| F07–F08: создание и callbacks | Structured/runtime-construction/Convert fixtures; ветви reuse/new, обе формы new, terminal null от factory, последующие member-правила и MappingOperation |
| F09: члены и формы типов | Классы, records, readonly/nullable struct, generic/nested, интерфейс с boxed struct, abstract factory; inherited required/init, mutable required field, SetsRequiredMembers |
| F10: nullable и обёртки | Warning-free conventions, AllowNull constructor/setter, MaybeNull/NotNull source, Some(default) против None, null против default struct |
| F04: имена и объём | Using, полные имена и tuple aliases; emitted XML/runtime-код, категории артефактов и ссылки на хелперы |

Различие нового instance value-type и default, пропуск init при reuse,
отсутствие fallback после принятого constructor-selection решения и terminal
null от factory соответствуют текущему контракту. Patch-семантика отсутствующих
значений не объявляется реализованной. Полные матрицы parsing, наследования,
кортежей и DI/lifetime остаются своим этапам.

## S04-01 — служебные имена member-типа меняют обычный маппинг

**Дефект реализации, высокий приоритет: молчаливая потеря значений.**

В [ReservedNames.cs](release-review-stage-04/Cases/ReservedNames.cs) source и
destination имеют публичные settable int-свойства Clone, EqualityContract,
DestinationMembers и контрольный Value. Bare Map компилируется без диагностик,
но Create и Update дают 0 вместо 13, 17, 19. Контрольный Value = 11 маппится.

| Изменение входа | Фактический результат |
| --- | --- |
| Без специальных настроек | 6 неверных значений, без диагностик |
| UnmappedMemberValidation(Destination) | Те же 6 ошибок; completeness validation молчит |
| Переименовать только destination в RenamedDestination | DestinationMembers начинает маппиться; Clone/EqualityContract по-прежнему теряются |
| Явно перечислить свойства в Members | CS0117 для Clone/DestinationMembers, CS0122 для record EqualityContract |

Это противоречит [member conventions](../conventions.md): свойства доступны,
точно совпадают по имени и допускают warning-free implicit conversion.
[DestinationMemberPolicy](../../src/Morphant.Generator/MappingPair/DestinationMemberPolicy.cs)
и [ConventionMemberMappingPlanner](../../src/Morphant.Generator/TypeMapperGeneration/ConventionMemberMappingPlanner.cs)
отбрасывают служебные record-имена и имя generated member-типа.
Ограничение представимости в DSL применяется к runtime conventions и проверке
полноты. В коде список шире; непосредственно выполнены три имени выше.

Предложение: отделить обычное назначение/проверку полноты от представления
селектора в record. Для явного Members согласовать читаемые aliases или
диагностируемую границу поддержки. Замена record на struct и молчаливое
переименование пользовательских членов не согласованы.
Регрессии: Create/Update, strict validation и переименование destination.

## S04-02 — тернарный Resolve из документации не компилируется

**Дефект документации/примера; непосредственную ошибку выдаёт C#.**

```csharp
.Resolve((source, previous) =>
    previous.TryGetValue(out var destination) && destination.Id == source.Id
        ? previous
        : new(source.Id));
```

В [Resolve](../api/resolve.md) и [Create and Update](../create-and-update.md)
приведена эта форма. C# связывает target-typed new с `Option<Destination>`
и выдаёт CS1729. Повторно подтверждено с C# 9, 11, latest в SDK 10;
в первоначальном аудите — также с Roslyn 4.4.0 / C# 9.
Форма из [declarative mapping](../declarative-mapping.md) с HasValue
и тем же `? previous : new(...)` тоже проверена и получает CS1729.

Явное `new DestinationConstruction(...)` устраняет ошибку C#, но TryGetValue
затем проявляет S04-04. Проверенная замена для примеров трёх страниц:

```csharp
.Resolve((source, previous) =>
{
    if (previous.HasValue && previous.Value.Id == source.Id)
        return previous;
    return new(source.Id);
});
```

Она проходит Create, Update(null), reuse и replacement. Ternary с HasValue
и явным construction-типом тоже проходит. Это ограничение конкретного
контекста вывода типа, а не требование использовать только одну форму new.

## S04-03 — типы внутри static-контейнера теряют регистрацию

**Дефект реализации, высокий приоритет: допустимая пара исчезает без объяснения.**

В [StaticContainer.cs](release-review-stage-04/Cases/StaticContainer.cs)
обычные Source/Destination объявлены внутри public static class Models.
Маппер регистрирует обычную контрольную пару, пару с Models.Source и пару
с Models.Destination. Сборка чистая, но `mapper is ITypeMapper<...>` для
обеих вложенных пар возвращает false. Контрольная пара остаётся работоспособной.
Изменение только static class Models на class Models восстанавливает оба
интерфейса и значения 13/17; все шесть проверок проходят.

[MappingTypeEligibilityPolicy.GetNameability](../../src/Morphant.Generator/MappingPair/MappingTypeEligibilityPolicy.cs)
классифицирует static named type как CompilerOwned и рекурсивно применяет
это правило к ContainingType. Ограничение самого generic-аргумента переносится
на допустимый контейнер имени вложенного обычного типа.

Предложение: разделить проверку mapping-типа и его containing scopes;
сам static root не должен стать допустимым generic-аргументом.
Регрессии: source/destination и независимая пара; дополнительно несколько
уровней и generic-контейнеры. Последние два усложнения пока не проверены.

## S04-04 — защищённый TryGetValue ошибочно получает MORPH0038

**Дефект реализации: корректная ветвь reuse отклоняется.**

[ResolveConditional.cs](release-review-stage-04/Cases/ResolveConditional.cs)
с EXPLICIT_NAME, BLOCK_BODY и NESTED_TRYGET получает MORPH0038:
previous якобы недоступен для Create/Update без существующего destination.
C#-ошибок в этих вариантах нет.

```csharp
.Resolve((source, previous) =>
{
    if (previous.TryGetValue(out var destination) && destination.Id == source.Id)
        return previous;
    return new(source.Id);
});
```

Ошибка сохраняется при двух вложенных if вместо && и при ternary с явным
construction-типом. Сравнимые HasValue-варианты проходят.
Документация [MORPH0038](../diagnostics/MORPH0038.md) обещает защиту через
HasValue или TryGetValue. В
[StructuredConstructMappingPlanner](../../src/Morphant.Generator/TypeMapperGeneration/StructuredConstructMappingPlanner.cs)
распознавание известной доступности previous привязано к HasValue;
[ConstructExpressionRewriter](../../src/Morphant.Generator/TypeMapperGeneration/ConstructExpressionRewriter.cs)
также специально сворачивает HasValue. Успешная ветвь TryGetValue не даёт
планировщику эквивалентного факта доступности.

Предложение: учитывать TryGetValue, сохраняя short-circuit, область
out-переменной и число вычислений. Регрессии: Create, Update(null), reuse,
replacement и формы ветвления на минимальном и новом Roslyn.
Полный разбор переносимых выражений остаётся этапу 5.

## S04-05 — удобство имён: понятные короткие имена, тяжёлые пути

**Вопрос дизайна и удобства; не новый конфликт типов.**

OrderConstruction, OrderMembers, OrderConstructorParameters понятно связаны
с пользовательским Order. Имена параметров/свойств сохраняют контракт;
source, destination, result в runtime-коде понятны. В Naming повторно прошли
короткие имена через using, полное имя вложенного Order и aliases двух tuple
представлений. Использование explicit generated-имён поддерживается.

| Представление | Полное generated имя | Длина |
| --- | --- | --- |
| Order | `Morphant.Generated.Types.A_Audit_002EStage04.N_Stage04Audit.N_Cases.Plans.OrderConstruction` | 91 |
| Other.Order | `Morphant.Generated.Types.A_Audit_002EStage04.N_Stage04Audit.N_Cases.T_Other.Plans.OrderConstruction` | 99 |
| `Envelope<T>.Item` | `Morphant.Generated.Types.A_Audit_002EStage04.N_Stage04Audit.N_Cases.T_Envelope_A1.Plans.ItemConstructorParameters` | 113 без списка generic-аргументов |
| (int Id, string Name) | `Morphant.Generated.Tuples.A_Audit_002EStage04.V2_a51caaf0c27a1203d7dd02a67a0a5455.TupleConstruction` | 99 |

У обычных имён 7–8 namespace-сегментов. Using/alias сокращает место вызова,
но длинный путь сначала нужно найти и выбрать. Для (int Id, string Name)
и (int Code, string Label) короткое имя одинаково; различие находится в хеше.
XML summary обоих construction-типов ссылается только на
``System.ValueTuple`2``, без конкретных типов и имён элементов. Это повторно
проверено в emitted source. Документация параметров/членов содержит Id/Name
или Code/Label, но требует смотреть глубже.

В первоначальном unit-прогоне прошли
[GeneratedPlanNamingUsageTests](../../src/tests/Morphant.Generator.UnitTests/GeneratedPlanNamingUsageTests.cs):
source compilation, DLL и reference assembly с IVT, общие обычные/tuple
destinations и явные construction/member-имена consumer. Это историческая
межсборочная проверка; новый Naming consumer работает в одной сборке.
Выбор A_PlanConsumer в using требует учитывать сборку генерации, а не только
сборку исходного Destination. Восемь вариантов имён/подписи сборки и проверка
независимости от version также прошли в первоначальном аудите.
Заказ🚀 кодируется как A__0417_0430_043A_0430_0437_D83D_DE80: уникальность
обеспечена, визуальная связь с исходным именем ослабевает.

Рекомендация для обсуждения: сохранить короткие leaf-имена и изоляцию сборок;
сначала сделать tuple summary различимым по пользовательскому представлению.
Отдельно оценить более читаемый tuple namespace и сокращение технических
сегментов обычных путей с доказательством уникальности/стабильности.
Хеш нельзя просто удалить, не сохранив идентичность. Это варианты,
а не выбранный новый алгоритм нейминга.

Реальный поиск/completion/tooltip в Rider или Visual Studio не проверен:
оценка основана на emitted source/XML и компиляции явных имён. Живая IDE
остаётся отдельной проверкой этапа 10.

## Объём и читаемость generated-кода

В Naming consumer: шесть пар у двух мапперов, 22 файла (Construction 6,
Member 4, MappingExtension 6, MemberExtension 4, TypeMapper 2).
Получено 16 plan-типов: 6 construction, 6 constructor-parameters, 4 member.
82 callback-overload метода объясняются четырьмя поверхностями по 15 и двумя
construction-only по 11. Все 12 private runtime helpers имеют вызовы.
Эти величины повторно измерены; они не универсальные ожидаемые константы.

В первоначальном чтении runtime-кода отмечены три Update helpers
(`Other.Order`, `Envelope<int>.Item`, `System.Tuple`) с единственным возвратом
destination. Устранение такого пустого вызова возможно как локальное упрощение,
но не требует объединять validation/dispatch с mapping-логикой.
Это не дефект корректности и не причина менять согласованную архитектуру.

Присваивания, конструкторы и ветвления читаются непосредственно. Нумерация
__Create1/__Update1 хуже объясняет конкретную пару в stack trace, но менее
существенна, чем имена типов, которые пользователь пишет сам.
Hint names в примере занимают 55–146 UTF-8 байт; они связаны с категорией
и destination, а tuple-файлы наследуют проблему непрозрачного хеша.
Overflow/Unicode/collision naming-тесты прошли в первоначальном запуске.

Не обнаружено неиспользуемых runtime helpers в проверенном выводе; обобщать
это на любую конфигурацию нельзя. Возвращать shared-расширения ради редкого
повторения одинаковой пары разными мапперами оснований нет.

## Решения и продолжение

Предлагаю до этапа 5 обсудить и исправить S04-01/S04-03/S04-04, затем поправить
три примера S04-02 и повторить затронутые compiler/runtime-проверки.
Постоянные регрессии должны следовать
[TESTING_GUIDELINES.md](TESTING_GUIDELINES.md).
Явные reserved-members и namespace-дизайн S04-05 требуют отдельного
согласования; этот отчёт не утверждает новый API.

Реальные IDE, исчерпывающая tuple-матрица, parsing всех callbacks, все сочетания
наследования/настроек и DI/lifetime остаются следующим этапам. Новых падений
генератора в выполненных пробах не обнаружено. Полный исторический CI
этапа 3 не выдаётся за новый запуск этапа 4.

Первоначальная публикация отчёта и исследовательских исходников была
отклонена автоматической проверкой разрешения на публичный GitHub.
Восстановление по команде пользователя продолжить работу сохранило результаты
и воспроизводимые входы; переход к этапу 5 требует отдельной команды.
