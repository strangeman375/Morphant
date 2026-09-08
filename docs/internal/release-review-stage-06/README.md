# Этап 6

S06-01 исправлен: наследуемые callbacks и IncludeMembers сохраняют выбранные
через generic constraint source-члены, включая скрытие, virtual dispatch и nullable-пути.
Недостающие проверки включены в постоянные тестовые проекты: 37 новых integration
и 19 unit-тестов. Временная MSBuild-проба заменена этими тестами и удалена.

Проверено на коде `863d36c`: Release без warnings/errors; 940 unit passed + 1 штатный
skip (Roslyn 4.4.0), 381 integration passed; в CI на Roslyn 4.9.2 — 941 unit passed.

Этап 7 ожидает решения пользователя после ревью исправления и тестов.
