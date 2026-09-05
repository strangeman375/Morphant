# Исправления дефектов этапа 3

Основание: [утверждённые решения](RELEASE_REVIEW_STAGE_03_SOLUTIONS.md).
**S03-01–S03-03 исправлены и проверены.** Итоговая версия:
[6bb3e09](https://github.com/strangeman375/Morphant/commit/6bb3e090281bb98e543c03c6eafe0006d2cc8c60),
tree `28a5e610985a68602c2a267d796e47180c9e8cfa`. Этап 4 аудита не начат.
Разделы 1–3 сохраняют состояние промежуточных контрольных точек;
окончательные результаты приведены в разделе 4. Исходные воспроизведения
дефектов остаются в [историческом отчёте](RELEASE_REVIEW_STAGE_03.md).

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

## 3. Актуализация основных snapshots

Полные literal snapshots обычных construction/member-расширений обновлены
механически и проверены. Случаи с несколькими независимыми мапперами теперь
содержат отдельные полные ожидания для каждого владельца. Сохранены точные
nullable/marker-контракты, XML-документация и позиции намеренных предупреждений.

Уточнена граница новой проверки: неразрешённый обычный callback, ошибку
которого полностью объясняет C#, не получает дополнительный `MORPH0018`.
Защита от отката CRTP к базе сохраняется. Обновлена тестовая модель текущего
runtime-контракта. Некорректная CRTP-пара с `MORPH0060` больше не может
воспользоваться методом независимого маппера; её вызов также отклоняет C#.

В focused запуске проверены 89 сценариев surface, compiler-owned diagnostics,
runtime compatibility и invalid-family binding: 86 прошли сразу, три
исправленных ожидания hint names прошли отдельным запуском. Общий запуск до
этой актуализации дал 714 passed, 107 failed, 1 skipped; это промежуточный
результат, а не итог исправления. Ожидания actualization/incrementality ещё
актуализируются; общий suite остаётся незавершённым.

## 4. Завершение исправлений и итоговая проверка

Актуализированы полные snapshots, actualization/incrementality и package
consumers. На сохранённом driver проверены удаление и переименование
мапперов: их расширения исчезают, а construction/member-файлы сохраняются,
пока нужны оставшимся мапперам. Результаты сравниваются с чистым запуском.

`DslIsolationUsageTests` теперь содержит 90 сценариев: 75 положительных и
15 проверок ошибочного отката к базовому семейству. Для каждой callback-
перегрузки проверен переход ошибка → исправление → повторная ошибка на
одном driver, включая полный набор generated sources и diagnostics.
`MORPH0018` появляется и исчезает вместе с ошибкой привязки.

Добавлены два MSBuild-backed runtime-сценария. `DslFamilyIsolation`
проверяет `Create`/`Update` и независимость настроек связанных семейств
с `base.Configure` и без него. `DslFamilyRecovery` проверяет typed stubs
при подавленном `MORPH0018` и работоспособность независимой корректной пары.

Общий прогон обнаружил и помог устранить регрессию file-local mapper:
специализированный receiver не должен ссылаться на тип, недоступный из
generated file. Такие объявления и file-local контейнеры теперь исключаются
из канонических DSL-поверхностей; сохраняется `MORPH0008` без каскада ошибок
generated C#. Усилены проверки с публичными и file-local моделями.

### Версии и полные прогоны

Последнее изменение production-кода:
[b973e89](https://github.com/strangeman375/Morphant/commit/b973e8988d264d57736eeb263ab805309967c04c).
Следующий коммит `6bb3e09` добавляет недостающие literal snapshots отдельного
`ResolveMapper` для collection expressions; production-код не меняет.
[CI итогового коммита](https://github.com/strangeman375/Morphant/actions/runs/33980981738)
завершился успешно, результаты сверены с jobs и журналами.

| Проверка | Результат |
| --- | --- |
| Локальная Release-сборка, SDK 10.0.100, Roslyn 4.4.0 | 0 warnings, 0 errors |
| Локальный полный unit-проект, Roslyn 4.4.0 | 836 passed, 1 штатный skipped, 0 failed |
| CI Release, Ubuntu / Windows / macOS | Все три сборки и test jobs успешны |
| CI unit, Ubuntu, Roslyn 4.4.0 | 836 passed, 1 skipped, 0 failed |
| CI unit, Roslyn 4.9.2 | 837 passed, 0 skipped/failed; сборка без warnings/errors |
| CI integration, Ubuntu | 268 passed, 0 skipped/failed; Windows/macOS jobs также успешны |
| CI package consumer, SDK 7.0.100 / MSBuild 17.4 | Сборка и выполнение успешны |
| CI пороги покрытия | Успешны |

Collection expressions штатно пропускаются на Roslyn 4.4.0 и проверены на
4.9.2. Локальные Release/unit выполнены на `b973e89`; полный CI — на
`6bb3e09`. Незавершённый локальный повтор integration остановлен и не
считается успешным: итоговое доказательство полного integration-прогона
получено из CI точной опубликованной версии.

### Повтор исходных воспроизведений и объём генерации

Production analyzer повторно загружен через `AnalyzerFileReference` в
Roslyn 4.4.0; входные программы используют C# 9, nullable enabled. Все
**556 случаев матрицы и 5 дополнительных CRTP-вариантов** завершились без
warning/error diagnostics и исключений генератора. Проверены IVT on/off,
source compilation/DLL/reference assembly, обычные/nullable/tuple пары,
общий destination при разных source, независимые и связанные семейства,
одинаковые constraints и вложенные generic-подстановки. Явные generated-
имена входят в матрицу обычной пары.

Реальные MSBuild-проекты также собраны заново с C# 9 и warnings-as-errors:
producer, consumer через `ProjectReference`, implementation DLL и reference
assembly, а также связанные annotated CRTP-семейства без `base.Configure`.
Все пять сборок: exit 0, 0 warnings, 0 errors.

| Измеренный consumer | Generated files | Construction/member-файлы | Callback-методы |
| --- | ---: | ---: | ---: |
| Один маппер полной пары | 5 | 2 | 15 |
| Два независимых обычных маппера одной пары | 8 | 2 | 30 |
| Два семейства в проверенных same-family режимах | 8 | 2 | 30 |

Это измерения конкретных входов, не оценка размера IL или времени генерации.
Раздельные расширения сохраняют изоляцию, а destination-планы по-прежнему
переиспользуются внутри сборки. Компактные результаты, SHA-256 generator
DLL и локального unit TRX сохранены в [fix-results.json](release-review-stage-03/fix-results.json).
[Команды и исходники](release-review-stage-03/README.md) позволяют повторить
compiler- и MSBuild-проверки.

### Закрытие дефектов и границы

| Дефект | Исправление и подтверждение |
| --- | --- |
| S03-01 | Shared-расширения удалены; специализированные receivers проходят все 15 перегрузок в friend assemblies |
| S03-02 | Assembly-область разделяет полные имена планов; IVT-проверки, явные имена и naming-регрессии чистые |
| S03-03 | Номинальный ковариантный family receiver устраняет неоднозначность; проверка binding запрещает ошибочный откат к базе, recovery проверен при компиляции и выполнении |

Условие миграции сохраняется: для явно названных generated-типов нужно
обновить `using` или полное имя; читаемые короткие имена и `new(...)`
поддерживаются. Сборочного маркера и новых требований к `base.Configure`
или `partial` reusable-баз не добавлено.

Эти три дефекта закрыты в проверенной области. Реальная IDE не проверялась;
полный аудит IDE, упаковки, смешанных версий и оставшейся семантики остаётся
соответствующим этапам плана. Исправления не являются решением о готовности
всего релиза. Этап 4 ожидает отдельной команды пользователя.
