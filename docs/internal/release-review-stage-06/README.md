# Этап 6: рабочая точка

Код: `277ded2`; Linux, SDK 10.0.100, unit-host Roslyn 4.4.0.
Настройки, наследование, IncludeMembers и flattening: 120 unit и 65 integration passed.

**S06-01:** после закрытия generic-базы `Members` и `IncludeMembers` меняют привязку
`source.Payload.Profile`: скрывающий член производного типа возвращает 99,
хотя исходный C# через generic constraint возвращает 11. Диагностики нет.
[Binding.cs](Binding.cs) содержит воспроизведение и контроль обычного cross-pair IncludeBase.

Повтор: `dotnet run --project docs/internal/release-review-stage-06/Stage06Probe.csproj -c Release`.
Пока дефект открыт, пример печатает расхождения и завершается с кодом 1.

Осталось проверить границы находки и сочетания дополнительных настроек с композицией.
