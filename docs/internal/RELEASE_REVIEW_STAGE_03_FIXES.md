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
