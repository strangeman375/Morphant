# Исследование cycles и shared references

Статус: исследование зафиксировано 2026-08-02; built-in reference handling
полностью отложен до периода после v0. Документ не задаёт принятый public API.
Он сохраняет уже выполненный анализ, чтобы при возвращении к feature не
повторять его с нуля.

Связанный пункт roadmap: этап 14 в
[`MAPPING_API_DESIGN_REFINEMENT_PLAN.md`](MAPPING_API_DESIGN_REFINEMENT_PLAN.md).

## 1. Какие задачи решает reference handling

У object graph есть две связанные, но разные проблемы:

1. Один source instance встречается несколько раз. Без cache каждый вызов
   создаёт новый destination instance и shared identity теряется.
2. Graph содержит цикл. Без ранней регистрации destination recursive mapping
   не завершается и обычно заканчивается stack overflow.

Reference preservation не является обычной member convention. Это policy
всей mapping chain: nested calls должны видеть тот же cache, независимо от
числа call frames и pair registrations на пути.

`MappingContext` для этой задачи не подходит. Он является immutable frame
одного вызова и создаётся заново для каждого nested `Map`. Chain-wide state уже
зарезервирован во внутреннем reference-type `MappingScope`, который начинает
жизнь вместе с root `Map` и завершается после него.

Поэтому feature можно добавить после v0 без изменения `IMapper`,
`ITypeMapper`, `MappingContext`, `Construct` или `Members`.

## 2. Рабочая настройка

Минимальная future policy может иметь следующую форму:

```csharp
public enum ReferenceHandling
{
    Default = 0,

    None,
    Preserve
}
```

`ReferenceHandling` — рабочее имя. `Default` сохраняет обычную иерархию
настроек, а effective default предлагается оставить `None`.

Tracking не должен быть включён без запроса пользователя:

- dictionary lookup добавляется для каждого подходящего mapping-вызова;
- cache удерживает source и destination до завершения всей chain;
- обычные acyclic mappings не получают от этого пользы;
- identity preservation меняет observable semantics повторного mapping-а.

При `None` не нужно создавать cache или выполнять скрытые allocation/checks,
кроме минимального operation dispatch, который уже существует.

## 3. Identity cache entry

Рабочий ключ:

```text
(source reference identity, resolved mapping descriptor identity)
```

Source сравнивается именно по reference identity, а не через пользовательский
`Equals` или `GetHashCode`. Value-type source не имеет стабильной object
identity и не участвует в built-in preservation.

Одного destination type в ключе недостаточно. Application-wide registry
допускает несколько registrations одной canonical pair, а после v0 может
получить keyed variants и runtime-derived links. Две разные resolved
registrations не должны случайно разделить result только потому, что возвращают
одинаковый CLR type.

Descriptor identity берётся после обычного deterministic lookup. Cache не
участвует в выборе registration и не может устранять missing/ambiguous lookup.
Будущий key или polymorphic dispatch сначала разрешает конкретный descriptor,
и только затем выполняется cache lookup.

Upper-level destination type отдельно в ключ не нужен, если descriptor
однозначно задаёт mapping pair и variant. Если implementation не может дать
descriptor-у стабильную identity, это нужно решить внутри registry, а не
ослаблять cache key до type pair.

## 4. Lifecycle entry

Для каждого cacheable вызова нужен следующий порядок:

```text
cache lookup
-> mark entry as building
-> Construct/select result
-> register result
-> Members
-> mark entry as complete
```

Состояние `building` создаётся до `Construct`, чтобы recursion можно было
обнаружить даже тогда, когда result ещё не существует. Если recursive lookup
находит такую entry без зарегистрированного result, Morphant должен завершить
вызов понятной runtime error, а не продолжать recursion до stack overflow.

После появления reference-type result он сразу регистрируется, и только затем
начинается post-creation member mapping. Recursive setter/field member может
получить уже созданный instance и замкнуть цикл.

Entry становится `complete` только после успешного `Members`. При exception
scope всё равно завершается вместе с root call; частично построенный result не
должен утекать в следующую независимую mapping chain.

`null` result и value-type result не регистрируются. Для них невозможно
сохранить полезную reference identity.

## 5. Поддерживаемые и невозможные graph forms

| Сценарий | Рабочее поведение `Preserve` |
|---|---|
| Один reference source встречен повторно | Возвращается тот же result; mapping rules повторно не выполняются |
| Цикл проходит через writable property или field | Поддерживается: result уже зарегистрирован до `Members` |
| Цикл нужен constructor argument-у | Не поддерживается: result ещё не существует |
| Цикл нужен `init`/required initializer member-у | Не поддерживается по той же причине |
| Factory/direct `Construct` | Result можно зарегистрировать только после возврата пользовательского кода |
| Factory сама рекурсивно вызывает mapping до возврата | Обнаруживается building-entry без result и завершается ошибкой |
| Source или destination является value type | Built-in entry не создаётся |
| Пользовательский creation-код вернул `null` | `null` немедленно возвращается и не кэшируется |
| `Convert` | Built-in lifecycle не применяется автоматически |

Главная граница здесь принципиальна: cache не может сделать разрешимым цикл,
в котором объект обязан получить ссылку на самого себя до завершения
constructor/initializer. Подстановка `default(T)`, временного proxy или
неинициализированного объекта не входит в безопасную модель Morphant.

Статически гарантированную constructor/initializer cycle можно в будущем
диагностировать при генерации. Динамический цикл между несколькими pair может
быть виден только в runtime и должен завершаться тем же определённым error для
building-entry без result.

## 6. Shared reference semantics

Cache hit означает завершённый выбор результата для сочетания source и
descriptor. Morphant возвращает уже зарегистрированный result и не выполняет
повторно:

- null handling pair-а;
- `Construct`;
- factory/direct creation code;
- `Members`;
- nested mappings из этих rules.

Иначе повторная встреча одного source могла бы второй раз мутировать общий
destination, выполнять side effects или выбирать другой replacement. Такое
поведение противоречило бы самому смыслу identity preservation.

Результат может быть возвращён, пока entry ещё `building`, только если он уже
зарегистрирован. Это намеренно позволяет увидеть частично инициализированный
instance внутри цикла. Такое же свойство имеет обычная ручная сборка mutable
cyclic graph: обратная ссылка появляется до завершения остальных assignments.

## 7. `Update` и conflicting previous

Первый вызов `Update` связывает source/descriptor entry с конкретным
выбранным result. Это может быть переданный previous либо replacement из
`Construct`.

При повторном вызове с тем же source и descriptor:

- `Option.None` не меняет уже выбранный result;
- тот же previous instance допустим;
- уже выбранный replacement-result также допустим как previous;
- другой non-null previous является reference conflict.

Последний случай нельзя разрешать правилом «первый wins»: пользователь явно
передал другой destination, а Morphant молча проигнорировал бы его. Нельзя и
переместить entry на второй instance: уже построенные ссылки graph-а указывают
на первый result.

Точный exception type и текст относятся к общему этапу observable failures,
но конфликт должен быть отличим от missing mapping, ambiguity и
constructor-cycle.

## 8. Factory, direct creation и manual mapping

Structured constructor, factory и direct `Construct` различаются способом
получения result, но имеют одну cache boundary: регистрация возможна только
после фактического возврата reference-type instance.

Если factory возвращает shared singleton или cached instance, Morphant может
связать его с текущим source entry после возврата. Если два разных source
возвращают один singleton, это не cache collision: entries различаются по
source identity, хотя их values совпадают.

`Convert` остаётся авторитетным полным алгоритмом и обходит declarative
lifecycle. Автоматически оборачивать его cache lookup/register нельзя:

- manual code может намеренно возвращать разные результаты;
- неизвестно, в какой момент созданный instance безопасно публиковать;
- manual code может само управлять cache или выполнять non-object mapping;
- скрытая short-circuit ветка пропустила бы пользовательские side effects.

Если после v0 понадобится preservation для manual mapping, ему нужен отдельный
явный handler/lifecycle contract. Это не часть минимальной built-in policy.

## 9. Что не следует объединять с feature

### `MaxDepth`

Ограничение глубины обрезает graph и выбирает fallback после заданного числа
вызовов. Оно не сохраняет shared identity и не решает цикл корректным
повторным использованием result. Это независимая policy со своей семантикой
fallback-а.

### Custom reference handler

Пользовательский handler должен знать lookup, registration, building state и
конфликты previous. Публичное раскрытие этих деталей создаёт отдельный
lifecycle API. Для первой версии `Preserve` достаточно внутреннего cache;
customization нужно проектировать только по подтверждённому сценарию.

### Projection

Query provider строит expression и не выполняет runtime mapping chain с
`MappingScope`. Он не может воспроизвести object cache semantics обычной
projection. Projection рассматривается отдельной capability и не получает
client-side fallback ради reference preservation.

### Runtime polymorphism и keyed variants

Они влияют на descriptor selection, но не меняют основной cache algorithm.
Сначала выбирается exact/keyed/derived descriptor, затем его identity входит в
cache key. Reference cache не должен сам искать более подходящий mapping.

## 10. Сравнение с существующими мапперами

AutoMapper отказался от прежнего автоматического отслеживания всех mappings и
оставил opt-in `PreserveReferences`; статически обнаружимые recursion cases
могут быть отмечены generator/configuration logic. Это подтверждает, что
безусловный tracking слишком дорог для default path.

Источник:
[AutoMapper — Circular references](https://docs.automapper.io/en/stable/5.0-Upgrade-Guide.html#circular-references).

Mapster также включает preservation явно через `PreserveReference(true)` и
разделяет один context между nested mappings. Это соответствует chain-wide, а
не call-frame scope.

Источник:
[Mapster — Object references](https://github.com/MapsterMapper/Mapster/blob/master/docs/articles/settings/Object-references.md).

Mapperly использует opt-in reference handling и регистрирует target сразу
после его создания, до mapping child properties. Этот lifecycle подтверждает
границу между поддерживаемым mutable member cycle и невозможным constructor
cycle.

Источник:
[Mapperly — Reference handling](https://mapperly.riok.app/docs/configuration/reference-handling/).

Сравнение не утверждает, что Morphant должен копировать их public API. Для
Morphant важны собственные уже принятые `Construct`/`Members`, descriptor registry
и explicit manual boundary.

## 11. Принятая v0-граница

Для v0 зафиксировано только отсутствие feature:

- reference tracking по умолчанию и opt-in API пока нет;
- shared source может породить несколько destination instances;
- cyclic graph не получает built-in completion guarantee;
- `MappingScope` остаётся внутренней совместимой точкой расширения;
- public mapper contracts ради будущего cache не меняются;
- `Convert` позволяет пользователю реализовать специальный graph algorithm
  самостоятельно.

После v0 отдельно согласуются:

- окончательное имя и уровни настройки;
- runtime representation descriptor identity и entries;
- exact exception types и diagnostics;
- поведение при replacement/null-result и будущих variants;
- нужен ли explicit manual/custom handler contract.

До этого документ является сохранённым исследованием, а не обязательством
production implementation.
