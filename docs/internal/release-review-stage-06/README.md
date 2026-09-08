# Этап 6: результат проверки

Код: `277ded2`; Linux, SDK 10.0.100, unit-host Roslyn 4.4.0.
Настройки, наследование, IncludeMembers и flattening: 120 unit и 65 integration passed.
Дополнительно прошли 13 MSBuild-проверок приоритетов Flattening/UnknownDerivedTypeHandling,
Default, порядка mapper-настроек и локальных Members/Auto/Ignore. Аудит завершён.

**S06-01:** после закрытия generic-базы `Members` и `IncludeMembers` меняют привязку
`source.Payload.Profile`: скрывающий член производного типа возвращает 99,
хотя исходный C# через generic constraint возвращает 11. Диагностики нет.
Подтверждено для Create, Update(null) и Update(existing) в обоих случаях;
сборка чистая, шесть расхождений, четыре контрольные проверки проходят.
[Binding.cs](Binding.cs) содержит воспроизведение и контроль обычного cross-pair IncludeBase.

Повтор: `dotnet run --project docs/internal/release-review-stage-06/Stage06Probe.csproj -c Release`.
Пока дефект открыт, пример печатает расхождения и завершается с кодом 1.

Причина: Members переносит source-доступ без сохранения исходного receiver binding;
IncludeMembers заново разрешает имена пути после generic-подстановки.
Рекомендуемое исправление: сохранять выбранные source-члены и семантику их получателей.
Пользователь разрешил исправление S06-01 и потребовал включать дополнительные
проверки в постоянный тестовый набор. Добавлены 8 integration-тестов настроек
и 7 тестов source binding; до исправления шесть случаев S06-01 должны падать.
Этап 7 ожидает решения пользователя после проверки исправления.
