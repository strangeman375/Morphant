# Reference-срез прежнего `Template()`-дизайна

Этот каталог сохраняет реализационные наработки и тестовые сценарии прежнего
дизайна, которые могут пригодиться при реализации текущего API
`Construct` / `Members` / `Convert`.

`snapshot` — точная копия **всех 133 путей**, затронутых cleanup-коммитом:

- всех удалённых файлов;
- pre-cleanup версий всех изменённых файлов;
- исходного файла единственного rename.

Источник snapshot: commit `96b6e6c5b68324ebcac366d6385abb3568c60721`,
tree `c08a12909a116eec4a40af9bb6bc93efafe42e5e`.

Каталог находится вне `src`, не включён ни в один project и не участвует в
сборке или тестах. Код внутри не обязан компилироваться и не является
compatibility target. Его не нужно исправлять, актуализировать или приводить к
текущему публичному API. Нормативными остаются `MAPPING_API_DESIGN.md` и
`MAPPING_API_IMPLEMENTATION_PLAN.md`.

## Что здесь может пригодиться

| Этап текущего roadmap | Основные reference-источники |
|---|---|
| 7. `MappingMode` и null normalization | `TypeMapperMappingModeTests`, `TypeMapperNullHandlingTests`, прежние `MappingSettings` и `AssemblyMappingSettingsPipeline` |
| 8. Structured `Construct` | `TemplateConstructorMappingPlanner`, `TemplateByConventionMappingPlanner`, прежний `ConventionConstructorMappingPlanner`, constructor tests |
| 9. Direct `Construct` и `ByFactory` | `TransferableLambdaSyntax`, `TemplateByFactoryMappingPlanner`, direct-block и factory tests |
| 10–11. `Members` и lifecycle | `TemplateMappingPlanner`, `TemplateMemberMarker`, convention/member/map-existing tests |
| 12. Declarative control flow | `TemplateControlFlowPlanner`, control-flow и switch tests |
| 13. Dependency graph | `TemplateMappingPlanner`, `LegacyTypeMapperPipeline`, тесты однократного вычисления и ветвления |
| 15. Manual `Convert` | `TransferableLambdaSyntax`, direct-block tests и helper-generation paths |
| 16. Nested `Map` | `TemplateNestedMapMappingPlanner`, `TypeMapperNestedMapTests` |
| 17–19. Settings и composition | `MapperBuilderMapPipeline`, effective-settings tests, прежние settings models |
| 20–21. Actualization и incrementality | `TemplateType` / `TemplateExtension` actualization и incrementality suites, model comparers |
| 22. Migration и integration audit | declaration, destination-variety, documentation, naming и hint-name tests |

Generated `TemplateSurface` и historical snapshots также сохранены целиком.
Они не задают новый контракт, но служат каталогом обработанных C#-форм,
nullability/accessibility edge cases и уже найденных generator pitfalls.

## Как переиспользовать

1. Начинать с текущей спецификации соответствующего этапа.
2. Искать здесь готовый алгоритм или пользовательский сценарий.
3. Переносить нужную идею в текущий production-код, адаптируя её к новой
   semantic model.
4. Переписывать тест против текущего API; не возвращать historical suite в
   обязательный test run.
5. Не изменять snapshot ради зелёной сборки: это read-only reference, а не
   параллельная реализация Morphant.

Исторический roadmap прежнего дизайна дополнительно восстановлен в корне как
`IMPLEMENTATION_PLAN.md`, где он остаётся обычным читаемым документом.
