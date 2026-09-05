# Исправления дефектов этапа 3

Основание: [утверждённые решения](RELEASE_REVIEW_STAGE_03_SOLUTIONS.md).
Этап 4 аудита не начат. Этот файл фиксирует новые проверки исправлений;
исторические результаты этапа 3 остаются в исходном отчёте.

## 1. Изоляция имён по генерирующей сборке

Production-изменение опубликовано в [0f7a959](https://github.com/strangeman375/Morphant/commit/0f7a9597d53e23875643a992e53354c1b21295cf),
tree `049f1e9a1bc10843c6b94efd9540746385d6cc37`.

- Обычные и tuple construction/member-типы получают assembly-область в
  namespace. Читаемые leaf-имена и hint names сохраняются.
- Scope использует простое имя и public key token; version не участвует.
  Пунктуация, Unicode и пользовательские escape-последовательности различимы.
- Одинаковые имена используются в предварительном связывании конфигурации
  и конечной генерации. Обновлены literal snapshots и explicit-name consumers.

Новый запуск: SDK 10.0.100, Roslyn 4.4.0, C# 9, net10.0/Linux.
`GeneratedPlanNamingUsageTests`, `ConstructionSurfaceTests` и
`MemberSurfaceTests`: **74 passed, 0 failed**. Проверены, в частности,
обычные и tuple snapshots, явные короткие construction/member-имена,
подписанная сборка, разные знаки и Unicode в имени сборки, стабильность при
смене assembly version на сохранённом driver, friend producer/consumer с
общими ordinary/ValueTuple destinations через source/DLL/reference assembly.
Компиляторные warnings/errors включены в проверки; runtime-исполнение этими
тестами не проверяется.

Состояние: направленная регрессия S03-02 устранена. Полная проверка,
новейший валидированный Roslyn и реальные MSBuild consumers ещё впереди.
S03-01/S03-03 пока не исправлены.

## 2. Специализированные receivers и проверка binding

Реализованы прямые receivers конкретных мапперов и простой ковариантный
`IMappingBuilder<Family<...>, S, D>` для bare CRTP self. Shared-ветка и
хешированные контейнеры семейств удалены. Runtime compatibility manifest и
reflection-based API inventory учитывают новый интерфейс.

Generated-метод должен принадлежать текущей compilation и назначенной
поверхности. Проверяется владелец и полное представление пары; неверная или
неразрешённая callback-привязка даёт `MORPH0018`. Координация больше не теряет
разные представления эффективной пары только из-за совпадения CLR-типов.

В том же окружении прошли **76 новых usage-regressions**: все 15 перегрузок
через friend source/DLL/ref и в связанных non-partial CRTP-базах без
`base.Configure`, с одинаковыми и различными constraints. Для семейств
проверен фактически выбранный owner. Ошибочное tuple-обращение, которое C#
принимает через базовую перегрузку без собственных warnings/errors, получает
`MORPH0018` от Morphant. Дополнительно прошли 15 существующих collision/API
проверок и 13 предварительных naming/CRTP проверок.

Контрольная точка намеренно публикуется до массовой актуализации literal
extension snapshots. Эти ожидания пока описывают прежние receivers и hint
names; общий suite на этом commit ещё не должен считаться пройденным.
Полная проверка, runtime recovery, конфигурационная независимость и расширенная
матрица воспроизведений остаются обязательными перед закрытием дефектов.
