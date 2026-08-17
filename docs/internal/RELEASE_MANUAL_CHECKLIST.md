# Финальная ручная проверка Morphant 0.1.0

Этот чек-лист проверяет релиз-кандидат как пользовательский продукт: от
конкретного commit и NuGet-пакета до работы generated API, диагностик,
документации и IDE. Он дополняет, но не заменяет автоматические тесты.

## Как пользоваться чек-листом

- Проверять один неизменяемый commit из `main` и пакет, собранный именно из
  него.
- Ставить отметку только при полном совпадении с ожидаемым результатом.
- Любое необъяснённое warning, отличие package payload, текста сообщения или
  runtime-поведения считать блокирующим.
- Для неприменимой дополнительной платформенной проверки писать `N/A` и
  причину; обязательные проверки пропускать нельзя.
- После исправления повторять затронутую категорию, а также проверки
  происхождения и содержимого пакета. После изменения generator/runtime API
  повторять весь чек-лист.
- Логи, распакованные пакеты и заполненную копию хранить вне tracked-файлов
  репозитория, например в игнорируемой папке `artifacts/manual-release`.

## Автоматизированный release flow

- Workflow `CI` на каждом pull request и push в `main` выполняет Release build,
  unit- и integration-тесты на Roslyn 4.4.0, проверку unit-тестов на Roslyn
  4.9.2 и coverage gates. На push в `main` он дополнительно прогоняет полный
  набор тестов на Windows и macOS. После успешного полного прогона line
  coverage badge обновляется в служебной ветке `badges`; подробный отчёт
  остаётся в summary и artifact workflow.
- Ветка `main` защищена от прямых изменений не-владельцами: для них требуется
  pull request, успешные проверки `Validate (Ubuntu, Roslyn 4.4)` и
  `Roslyn 4.9.2 compatibility`, а также разрешение всех review discussions.
  Force push и удаление ветки запрещены. Ограничения не применяются к
  repository administrator, поэтому владелец сохраняет право прямого commit.
- Workflow `Release` запускается вручную из `main` с точной версией из
  `src/Morphant.Product.props`. Он повторяет release matrix, один раз создаёт
  package artifact и после ручного разрешения публикует именно эти байты в
  NuGet, создаёт tag и GitHub release.
- Перед первым запуском нужно создать GitHub environment `nuget.org`, разрешить
  deployment только из `main`, назначить required reviewer и добавить
  environment variable `NUGET_TRUSTED_PUBLISHING_CONFIGURED=true`. Если
  reviewer только один и это владелец repository, `Prevent self-review`
  включать нельзя.
- В NuGet.org нужно создать trusted publishing policy для владельца
  `strangeman375`, repository `Morphant`, workflow `release.yml` и environment
  `nuget.org`. Постоянный API key в GitHub не нужен.
- Dependabot ежемесячно проверяет GitHub Actions и NuGet dependencies. Версии
  Roslyn исключены: 4.4.0 является минимальным публичным контрактом и меняется
  только осознанно вместе с compatibility review.

## Карточка релиз-кандидата

| Поле | Значение |
|---|---|
| Версия пакета | `0.1.0` |
| Commit SHA | |
| Ветка | `main` |
| Release workflow run | |
| Release artifact | `morphant-release-0.1.0` |
| Дата проверки, UTC | |
| Проверяющий | |
| ОС и архитектура | |
| Версия Rider | |
| `dotnet --info` | |
| Минимальный Roslyn | `4.4.0` |
| Дополнительно проверенный Roslyn | `4.9.2` |
| SHA-256 `.nupkg` | |
| SHA-256 `.snupkg` | |
| Итог | `GO` / `NO-GO` |

## Карта проверок

| Категория | Проверяет |
|---|---|
| 1. Происхождение | commit, версии, лицензия, manifests и assets |
| 2. Toolchain и build | SDK, Roslyn matrix, Release build и tests |
| 3. NuGet package | metadata, payload, symbols, strong name и Source Link |
| 4. Установка и IDE | clean consumer, Rider и generated surface |
| 5. Create и Update | основной runtime contract |
| 6. Callbacks и Convert | declarative lifecycle и manual boundary |
| 7. Settings | precedence, modes, null handling и validation |
| 8. Nested mapping и DI | operation selection, lookup и scopes |
| 9. Inheritance | `base.Configure` и `IncludeBase` |
| 10. Failures | diagnostics, recovery и exceptions |
| 11. Incrementality | caching, invalidation, actualization и determinism |
| 12. Документация | README, guides, XML docs и links |
| 13. Совместимость | language, Roslyn, target frameworks и platforms |
| 14. Решение | artifact identity и GO/NO-GO |

## Общие исходные состояния

В проверках ниже используются следующие обозначения.

- **RC** — отдельный чистый checkout выбранного commit из `main`. В checkout
  нет локальных изменений, а старые package artifacts не смешаны с новыми.
- **PACKAGE** — `Morphant.0.1.0.nupkg` и `Morphant.0.1.0.snupkg`, скачанные из
  неизменяемого release artifact для RC после финальной сборки generator на
  Roslyn 4.4.0. Для проверки до запуска workflow допускается локально
  воспроизведённая пара; перед публикацией все package-проверки выполняются на
  artifact из workflow.
- **LOCAL FEED** — новая временная папка только с файлами PACKAGE.
- **C9** — новый SDK-style consumer без ссылок на исходники Morphant:
  `net10.0`, `LangVersion=9.0`, nullable enabled, warnings as errors, direct
  `PackageReference` на `Morphant` 0.1.0 из LOCAL FEED.
- **LATEST** — такой же изолированный consumer с `LangVersion=latest`.
- **DI** — изолированный consumer с `Morphant`,
  `Microsoft.Extensions.DependencyInjection` и регистрациями из quick start.
- **INVALID** — отдельный изолированный consumer, в котором по одной вносятся
  заведомо ошибочные mapping-конфигурации.

Для consumer-проектов нужен отдельный пустой NuGet cache или уникальная
версия/папка restore. В restore sources должен оставаться LOCAL FEED (и только
явно необходимые источники для сторонних пакетов). Ни один consumer не должен
иметь `ProjectReference` на Morphant или generator.

## 1. Происхождение и состав релиз-кандидата

- [ ] **RC-01 — Зафиксирован точный commit**
  - Исходное состояние: выбран кандидат в локальной ветке `main`; remote refs
    обновлены обычным `git fetch`.
  - Действие: сравнить `git rev-parse HEAD`, `git rev-parse main` и
    `git rev-parse origin/main`; записать SHA в карточку.
  - Ожидаемый результат: все три SHA совпадают; история является обычным
    fast-forward без локального или удалённого расхождения.

- [ ] **RC-02 — Checkout чистый**
  - Исходное состояние: RC до restore, build и pack.
  - Действие: выполнить `git status --short --branch` и проверить untracked
    файлы, не полагаясь только на IDE.
  - Ожидаемый результат: нет tracked или untracked изменений; показана только
    ветка `main` без ahead/behind.

- [ ] **RC-03 — Diff кандидата осмыслен**
  - Исходное состояние: известен весь диапазон commit первого релиза.
  - Действие: просмотреть `git log`, итоговый `git diff --stat` и список
    tracked-файлов; отдельно проверить случайные binaries, temporary files и
    editor settings.
  - Ожидаемый результат: в релиз входят только product source, tests,
    документация и согласованные assets; случайных файлов нет.

- [ ] **RC-04 — Версии согласованы**
  - Исходное состояние: RC без локальных overrides MSBuild properties.
  - Действие: сверить `Version`, `AssemblyVersion`, changelog, release notes и
    все точные упоминания версии поиском по репозиторию.
  - Ожидаемый результат: package version — `0.1.0`, assembly version —
    `0.1.0.0`; нет `0.0.0`, prerelease suffix или другой релизной версии,
    кроме намеренных test-fixture versions.

- [ ] **RC-05 — Changelog готов к тегу**
  - Исходное состояние: в `CHANGELOG.md` есть разделы `Unreleased` и `0.1.0`.
  - Действие: прочитать раздел 0.1.0 и проверить reference links внизу файла.
  - Ожидаемый результат: перечислены фактически поставляемые возможности и
    границы; ссылки используют тег `v0.1.0`; `Unreleased` остаётся пустым.

- [ ] **RC-06 — Диагностики отмечены как shipped**
  - Исходное состояние: generator содержит release tracking manifests.
  - Действие: сопоставить `AnalyzerReleases.Shipped.md`,
    `AnalyzerReleases.Unshipped.md`, каталог в `docs/diagnostics.md` и файлы
    `docs/diagnostics/MORPH*.md`.
  - Ожидаемый результат: MORPH0001–MORPH0048 ровно по одному разу находятся в
    shipped release 0.1.0; unshipped manifest пуст; существуют 48 страниц и
    48 ссылок из каталога.

- [ ] **RC-07 — Лицензия и copyright согласованы**
  - Исходное состояние: RC собирается в текущем календарном году UTC.
  - Действие: проверить `LICENSE`, package properties, README и отсутствие
    другой license declaration.
  - Ожидаемый результат: везде MIT; автор — `strangeman375`; copyright в
    assembly/package получает текущий UTC-год автоматически и не требует
    ручной правки.

- [ ] **RC-08 — В репозитории нет непреднамеренных секретов**
  - Исходное состояние: полный tracked tree RC.
  - Действие: проверить API keys, tokens, passwords, connection strings,
    локальные absolute paths и private configuration.
  - Ожидаемый результат: реальные credentials отсутствуют. Файл
    `src/Morphant.snk` с private strong-name частью является отдельно
    согласованным публичным build asset, а не секретом.

- [ ] **RC-09 — Assets открываются**
  - Исходное состояние: tracked `logo.png`, source logo asset и README.
  - Действие: открыть изображения обычным viewer и README renderer.
  - Ожидаемый результат: PNG не повреждён, имеет ожидаемого детального
    мамонта, хорошо читается в малом размере; в README логотип расположен
    слева и не растянут.

## 2. Toolchain, сборка и автоматические gates

Проверку нового Roslyn нужно выполнить до финальной сборки. После неё product
обязательно пересобирается с минимальным Roslyn 4.4.0, чтобы в PACKAGE не
попала generator assembly, скомпилированная против 4.9.2.

- [ ] **BUILD-01 — Используется закреплённый SDK**
  - Исходное состояние: shell открыт в корне RC, установлен SDK из
    `global.json`.
  - Действие: выполнить `dotnet --version` и `dotnet --info`.
  - Ожидаемый результат: выбран .NET SDK `10.0.100` либо разрешённый
    `latestPatch`; prerelease SDK не используется; архитектура соответствует
    карточке кандидата.

- [ ] **BUILD-02 — Полный unit-прогон на Roslyn 4.9.2**
  - Исходное состояние: RC, restore/build разрешены с
    `-p:MorphantRoslynVersion=4.9.2`.
  - Действие: выполнить Release `dotnet test` проекта
    `Morphant.Generator.UnitTests` с Roslyn 4.9.2.
  - Ожидаемый результат: 545 тестов passed, failed нет, skipped нет; в том
    числе выполняется тест collection expressions.

- [ ] **BUILD-03 — Финальный restore возвращён на Roslyn 4.4.0**
  - Исходное состояние: после BUILD-02 outputs могли быть построены против
    Roslyn 4.9.2.
  - Действие: очистить только build outputs RC, затем выполнить restore
    solution с `-p:MorphantRoslynVersion=4.4.0`.
  - Ожидаемый результат: restore успешен; assets generator и unit-test host
    разрешают Microsoft.CodeAnalysis 4.4.0; старые 4.9.2 binaries не будут
    использованы для pack.

- [ ] **BUILD-04 — Полная Release-сборка чистая**
  - Исходное состояние: выполнен BUILD-03, локальных source-изменений нет.
  - Действие: выполнить `dotnet build src/Morphant.slnx -c Release
    --no-restore -p:MorphantRoslynVersion=4.4.0`.
  - Ожидаемый результат: exit code 0, warnings 0, errors 0; собраны runtime,
    generator и все test consumer assemblies.

- [ ] **BUILD-05 — Unit-тесты проходят на минимальном Roslyn**
  - Исходное состояние: финальные Release outputs из BUILD-04.
  - Действие: выполнить unit-test project с `--no-build --no-restore` и
    `MorphantRoslynVersion=4.4.0`.
  - Ожидаемый результат: 544 passed, 1 skipped, 0 failed. Единственный skip —
    collection-expression test с явной причиной «требуется Roslyn 4.8+».

- [ ] **BUILD-06 — Интеграционные тесты проходят полностью**
  - Исходное состояние: финальные Release outputs из BUILD-04.
  - Действие: выполнить `dotnet test` проекта
    `Morphant.Generator.IntegrationTests` с `--no-build --no-restore`.
  - Ожидаемый результат: 235 passed, 0 skipped, 0 failed; package-consumption
    test также проходит.

- [ ] **BUILD-07 — Package-consumption gate действительно выполнен**
  - Исходное состояние: завершён BUILD-06, его полный summary/log доступен.
  - Действие: убедиться, что `PackageConsumptionTests` не был отфильтрован или
    пропущен и завершился успешно; не запускать весь suite повторно.
  - Ожидаемый результат: test упаковал свежую уникальную version, проверил
    payload/metadata/strong name/buildTransitive и запустил consumer из
    временного local feed.

- [ ] **BUILD-08 — Проверка не изменила repository tree**
  - Исходное состояние: завершены restore, build и tests.
  - Действие: снова выполнить `git status --short`.
  - Ожидаемый результат: tracked и untracked tree остаётся чистым; generated
    test artifacts находятся только в ignored output directories.

## 3. Создание и содержимое NuGet-пакета

- [ ] **PKG-01 — Пакеты созданы из финальной 4.4-сборки**
  - Исходное состояние: BUILD-03–BUILD-06 пройдены, после них product source и
    outputs не менялись; LOCAL FEED пуст.
  - Действие: выполнить Release `dotnet pack` для
    `src/Morphant/Morphant.csproj` с `--no-build --no-restore`, version 0.1.0 и
    output в LOCAL FEED.
  - Ожидаемый результат: созданы ровно `Morphant.0.1.0.nupkg` и
    `Morphant.0.1.0.snupkg`; pack завершён без warnings.

- [ ] **PKG-02 — Main package имеет точный product payload**
  - Исходное состояние: свежий `.nupkg` из PKG-01 открыт как ZIP.
  - Действие: исключив стандартные NuGet `_rels`, `[Content_Types].xml` и
    `package/services/metadata`, сравнить список entries.
  - Ожидаемый результат: product payload состоит ровно из `Morphant.nuspec`,
    `LICENSE`, `README.md`, `logo.png`, `lib/netstandard2.0/Morphant.dll`,
    `lib/netstandard2.0/Morphant.xml`,
    `analyzers/dotnet/cs/Morphant.Generator.dll` и
    `buildTransitive/Morphant.props`, `buildTransitive/Morphant.targets`.

- [ ] **PKG-03 — В package нет лишних файлов**
  - Исходное состояние: распакованный `.nupkg`.
  - Действие: поискать `.snk`, source files, tests, `docs/internal`, PDB
    generator, `bin`, `obj`, local paths и duplicate assemblies.
  - Ожидаемый результат: ничего перечисленного нет; generator DLL встречается
    только в analyzer path, runtime DLL — только в `lib/netstandard2.0`.

- [ ] **PKG-04 — Nuspec identity корректна**
  - Исходное состояние: `Morphant.nuspec` из PACKAGE.
  - Действие: проверить `id`, `version`, `title`, `authors`, `description`,
    tags и copyright.
  - Ожидаемый результат: identity — Morphant 0.1.0, author
    `strangeman375`; описание кратко говорит о compile-time strongly typed
    mapping без runtime reflection; tags содержат mapper, source-generator,
    compile-time, Roslyn и C#; год copyright текущий по UTC.

- [ ] **PKG-05 — NuGet presentation metadata корректна**
  - Исходное состояние: nuspec и package entries доступны.
  - Действие: проверить license, icon, readme, project URL и release notes.
  - Ожидаемый результат: license expression `MIT`; icon — `logo.png`; readme —
    `README.md`; project URL ведёт в GitHub repository; release notes называют
    initial stable 0.1 release и содержат ссылки через tag `v0.1.0` на
    changelog и limitations.

- [ ] **PKG-06 — Repository metadata указывает на RC**
  - Исходное состояние: nuspec из PACKAGE и SHA из карточки кандидата.
  - Действие: сравнить repository type, URL, branch и commit.
  - Ожидаемый результат: type `git`, URL
    `https://github.com/strangeman375/Morphant`, branch `refs/heads/main`,
    commit полностью совпадает с RC SHA.

- [ ] **PKG-07 — Package не приносит runtime dependencies**
  - Исходное состояние: dependencies section nuspec.
  - Действие: проверить `.NETStandard2.0` dependency group и restore C9.
  - Ожидаемый результат: dependency group существует, но package dependencies
    отсутствуют; установка Morphant не добавляет Microsoft.CodeAnalysis или
    DI package как runtime dependency.

- [ ] **PKG-08 — Repository files упакованы без подмены**
  - Исходное состояние: LICENSE, README и logo одновременно доступны в RC и
    PACKAGE.
  - Действие: побайтово сравнить соответствующие files.
  - Ожидаемый результат: три пары файлов идентичны; package не содержит
    устаревшую копию README, лицензии или логотипа.

- [ ] **PKG-09 — buildTransitive содержит settings и generated cleanup**
  - Исходное состояние: `buildTransitive/Morphant.props` и
    `buildTransitive/Morphant.targets` из PACKAGE.
  - Действие: проверить список `CompilerVisibleProperty` и target очистки
    Morphant generated output.
  - Ожидаемый результат: ровно шесть properties:
    `MorphantMappingMode`, `MorphantNullSourceHandling`,
    `MorphantNullDestinationHandling`, `MorphantConstructorSelection`,
    `MorphantMemberSelection`, `MorphantUnmappedMemberValidation`; cleanup
    ограничен каталогом `Morphant.Generator`, пропускает design-time/no-op
    builds и допускает opt-out через `MorphantCleanCompilerGeneratedFiles`.

- [ ] **PKG-10 — Runtime XML documentation поставляется**
  - Исходное состояние: runtime DLL и XML из PACKAGE.
  - Действие: открыть XML, затем подключить package в Rider и вызвать
    completion по основным public types.
  - Ожидаемый результат: XML не пуст, соответствует `Morphant.dll`; Rider
    показывает краткие docs для mapper, builder, context, settings, markers и
    exceptions без broken `<see>` references.

- [ ] **PKG-11 — Обе assemblies strong-named**
  - Исходное состояние: runtime и generator DLL извлечены из PACKAGE.
  - Действие: просмотреть assembly identity через Rider/decompiler либо
    `AssemblyName.GetAssemblyName(...).GetPublicKeyToken()`.
  - Ожидаемый результат: у обеих assemblies непустой public key и одинаковый
    public key token `ba27fb6be8f80649`.

- [ ] **PKG-12 — Assembly versions стабильны**
  - Исходное состояние: обе DLL из PACKAGE.
  - Действие: проверить assembly, file и informational versions.
  - Ожидаемый результат: assembly version — `0.1.0.0`; product/package
    informational version соответствует 0.1.0 и RC commit; runtime и generator
    не расходятся по product identity.

- [ ] **PKG-13 — Symbol package имеет точный payload**
  - Исходное состояние: свежий `.snupkg` из PKG-01.
  - Действие: открыть его как ZIP и исключить стандартные NuGet metadata
    entries.
  - Ожидаемый результат: остаются только `Morphant.nuspec` и непустой
    `lib/netstandard2.0/Morphant.pdb`; отдельного generator PDB нет, потому что
    generator использует embedded debug information.

- [ ] **PKG-14 — Package восстанавливается только из LOCAL FEED**
  - Исходное состояние: C9 использует пустой отдельный package cache и source,
    содержащий PACKAGE.
  - Действие: выполнить restore, затем временно сделать feed недоступным и
    повторить build с `--no-restore`.
  - Ожидаемый результат: restore выбирает ровно Morphant 0.1.0 из LOCAL FEED;
    последующий build не обращается к исходному repository и успешно работает
    из restored package.

- [ ] **PKG-15 — Зафиксированы hashes финальных artifacts**
  - Исходное состояние: после PKG-01 package files больше не изменялись.
  - Действие: вычислить SHA-256 обоих файлов и записать в карточку и release
    evidence.
  - Ожидаемый результат: hashes непустые и относятся к файлам, прошедшим все
    последующие consumer-проверки; package больше не перепаковывается.

- [ ] **PKG-16 — Source Link указывает на исходники RC**
  - Исходное состояние: runtime PDB извлечён из `.snupkg`; RC commit доступен
    в GitHub.
  - Действие: просмотреть Source Link document map и открыть исходник из
    package через debugger либо Source Link inspection tool.
  - Ожидаемый результат: local source paths переписаны на
    `raw.githubusercontent.com/strangeman375/Morphant/<RC-SHA>/*`; debugger
    получает файл того же commit, абсолютный local path не раскрывается как
    конечный URL.

- [ ] **PKG-17 — NuGet README preview выглядит корректно**
  - Исходное состояние: PACKAGE открыт в NuGet-compatible package viewer с
    поддержкой embedded README.
  - Действие: отрендерить package page/README и проверить первый экран, image,
    code blocks и external links.
  - Ожидаемый результат: icon и детальный мамонт отображаются, README logo
    расположен слева, текст не ломается, install snippet виден и GitHub links
    ведут на существующие страницы.

## 4. Установка, IDE и generated surface

- [ ] **USE-01 — Одна PackageReference устанавливает runtime и generator**
  - Исходное состояние: новый C9 без Morphant и без cached copy package.
  - Действие: добавить только `PackageReference Include="Morphant"
    Version="0.1.0"`, restore и открыть project assets.
  - Ожидаемый результат: `Morphant.dll` доступна для compile/runtime,
    `Morphant.Generator.dll` подключена как analyzer; отдельная generator
    package/reference не нужна.

- [ ] **USE-02 — Quick-start mapper виден в Rider**
  - Исходное состояние: C9 содержит types и mapper, дословно скопированные из
    root README.
  - Действие: дождаться design-time analysis, открыть completion и generated
    code для partial mapper.
  - Ожидаемый результат: `[MorphantMapper]` распознан; generated partial class
    реализует `ITypeMapper<Customer, CustomerDto>`; нет ложных красных ошибок
    или необходимости перезапустить Rider.

- [ ] **USE-03 — Clean CLI build совпадает с IDE**
  - Исходное состояние: USE-02 без IDE-only changes.
  - Действие: выполнить clean Release build из терминала.
  - Ожидаемый результат: build проходит с warnings as errors; поведение и
    диагностики совпадают с Rider.

- [ ] **USE-04 — EmitCompilerGeneratedFiles работает по инструкции**
  - Исходное состояние: в C9 добавлены properties и `Compile Remove` из
    `docs/generated-code.md`.
  - Действие: build, добавить вторую mapping pair, build, удалить эту pair и
    снова build без `clean`; просмотреть указанную generated directory.
  - Ожидаемый результат: появились только Morphant generated `.g.cs` files;
    они не компилируются второй раз, не вызывают duplicate symbol errors и
    обновляются после mapping changes; файлы удалённой pair исчезают, output
    других generators не удаляется.

- [ ] **USE-05 — Удаление package не оставляет скрытой зависимости**
  - Исходное состояние: успешно собранный C9 с незафиксированными generated
    copies.
  - Действие: удалить PackageReference, очистить `bin`, `obj` и generated
    output, затем build; после этого вернуть package и build ещё раз.
  - Ожидаемый результат: без package compile ожидаемо не находит Morphant API;
    stale generated implementation не остаётся. После возврата package project
    снова собирается без ручной правки generated files.

- [ ] **USE-06 — Multi-project consumer не зависит от исходников Morphant**
  - Исходное состояние: solution с class library, которая напрямую ссылается
    на PACKAGE и объявляет mapper, и console/test host, который ссылается на
    library.
  - Действие: собрать solution и вызвать mapping из host.
  - Ожидаемый результат: mapper генерируется в owning library, runtime types
    доступны host транзитивно, mapping выполняется; analyzer не требует
    ProjectReference на Morphant source.

- [ ] **USE-07 — Public API discoverable и не содержит generator API**
  - Исходное состояние: PACKAGE подключён в Rider.
  - Действие: просмотреть namespaces и exported types обеих assemblies.
  - Ожидаемый результат: runtime surface соответствует public API baseline;
    generator assembly не экспортирует public types; internal generated helper
    types не выглядят как обещанный application API.

- [ ] **USE-08 — Поддерживаемые формы mapper declaration компилируются**
  - Исходное состояние: C9 содержит отдельные abstract/non-sealed, closed
    generic mapping pairs, generic mapper, mapper в global namespace и nested
    protected/private partial mappers; содержащие types также `partial`.
  - Действие: build и просмотр generated declarations.
  - Ожидаемый результат: legal forms генерируют правильную accessibility,
    generic parameters/constraints и exact mapper contracts; global/nested
    namespaces и hint names не конфликтуют.

## 5. Основные Create и Update сценарии

- [ ] **MAP-01 — Convention Create**
  - Исходное состояние: C9 содержит `Source { int Id; string Name; }`,
    аналогичный mutable `Destination` и bare `Map<Source, Destination>()`.
  - Действие: вызвать Create через `ITypeMapper` и через `IMapper`.
  - Ожидаемый результат: создаётся новый destination, `Id` и `Name` равны
    source; оба entry points дают одинаковый result.

- [ ] **MAP-02 — Convention Update переиспользует instance**
  - Исходное состояние: MAP-01 и existing destination с другими значениями.
  - Действие: вызвать Update и сохранить return value.
  - Ожидаемый результат: result — тот же instance; settable members обновлены;
    исходная переменная корректна именно после присваивания return value.

- [ ] **MAP-03 — Resolve может заменить destination**
  - Исходное состояние: mapping с `Resolve`, переиспользующим previous только
    при совпадении identity key.
  - Действие: выполнить Update один раз с совпадающим и один раз с другим key.
  - Ожидаемый результат: в первом случае возвращён тот же instance, во втором
    новый; applicable `Members` применены к выбранному result.

- [ ] **MAP-04 — Null Update destination создаёт result, оставаясь Update**
  - Исходное состояние: default `NullDestinationHandling.Create`, context-aware
    construction записывает `context.Operation`.
  - Действие: вызвать `Map(source, (Destination?)null)`.
  - Ожидаемый результат: создаётся non-null result по creation rules, но
    operation в callback равна `Update`, а не `Create`.

- [ ] **MAP-05 — Creation-only members различаются при reuse и replacement**
  - Исходное состояние: destination имеет constructor argument, `init`
    property, settable property и mutable field; `Resolve` умеет reuse/replace.
  - Действие: Create, Update с reuse и Update с replacement.
  - Ожидаемый результат: constructor/`init` rules выполняются на Create и
    replacement; при reuse их значения сохраняются; settable property и field
    обновляются во всех применимых случаях.

- [ ] **MAP-06 — Constructor convention сохраняет C# contract**
  - Исходное состояние: source members отличаются регистром для constructor
    parameters; один parameter optional с default.
  - Действие: выполнить Create без explicit `Construct`.
  - Ожидаемый результат: exact match имеет приоритет, unique case-insensitive
    match работает, optional parameter может оставить declared default;
    неоднозначный match не выбирается молча.

- [ ] **MAP-07 — Explicit Construct и ByConvention**
  - Исходное состояние: destination constructor имеет автоматически
    сопоставимые arguments и один argument из nested source path.
  - Действие: проверить explicit `Construct`, затем `Construct(ByConvention(),
    new { ... })`.
  - Ожидаемый результат: выбран именно указанный/настроенный constructor;
    explicit argument перекрывает convention, остальные arguments получают
    convention values.

- [ ] **MAP-08 — Member markers имеют различимое поведение**
  - Исходное состояние: `MemberSelection.Explicit`; в `Members` используются
    ordinary expression, `Auto()`, `Ignore()` и `Value<T>()`.
  - Действие: Create и Update destination с заранее заданными values.
  - Ожидаемый результат: expression присвоен, `Auto` берёт exact-name source,
    `Ignore` сохраняет текущий/default value, `Value<T>` использует ровно
    указанный receiving type; неперечисленный member не меняется.

- [ ] **MAP-09 — Conventions не угадывают лишнее**
  - Исходное состояние: source содержит exact-name, different-case,
    flattenable path и type, требующий warning-producing conversion.
  - Действие: bare mapping и просмотр diagnostic/generated behavior.
  - Ожидаемый результат: member convention использует только exact
    case-sensitive name и warning-free implicit conversion; flattening,
    normalization и nested mapping автоматически не запускаются.

- [ ] **MAP-10 — Поддерживаемые destination kinds работают**
  - Исходное состояние: отдельные mappings в C9 для class, struct, record,
    nullable value type и closed generic destination; interface/abstract
    destination получает explicit factory/result.
  - Действие: выполнить Create и применимый Update каждого mapping.
  - Ожидаемый результат: каждый legal mapping компилируется и возвращает
    ожидаемое значение; interface/abstract тип не конструируется convention-ом,
    но работает с explicit result.

- [ ] **MAP-11 — Required и nullable contract соблюдаются**
  - Исходное состояние: LATEST destination содержит required, nullable,
    non-nullable, `AllowNull`/`DisallowNull` members и nullable value type.
  - Действие: собрать warning-free legal mapping и отдельно нарушить каждый
    обязательный contract.
  - Ожидаемый результат: legal mapping не создаёт warnings; required members
    и annotations сохраняются generated API; invalid assignment получает
    compiler/Morphant diagnostic, а не скрытый cast или default.

- [ ] **MAP-12 — Standalone exact mapper работает без DI**
  - Исходное состояние: generated mapper можно создать напрямую и привести к
    `ITypeMapper<Source, Destination>`.
  - Действие: вызвать extension overloads `Create(source)` и
    `Update(source, destination)`.
  - Ожидаемый результат: оба вызова работают без service provider, когда
    mapping не делает runtime nested lookup; результат совпадает с основным
    contract.

## 6. Declarative callbacks и Convert

- [ ] **CALL-01 — Previous и result имеют правильный смысл**
  - Исходное состояние: `Members` получает source, previous и result; Resolve
    иногда заменяет destination.
  - Действие: выполнить Create, reused Update и replacement Update.
  - Ожидаемый результат: previous — `None` на Create и `Some` при реальном
    existing destination; result — фактически выбранный объект; replacement не
    подменяет значение previous.

- [ ] **CALL-02 — Context сообщает текущую операцию**
  - Исходное состояние: context-aware `Construct`, `Resolve` и `Members`
    сохраняют увиденную operation.
  - Действие: вызвать Create, обычный Update и Update с null destination.
  - Ожидаемый результат: Create видит `Create`; оба Update-вызова видят
    `Update` независимо от создания replacement.

- [ ] **CALL-03 — Declarative control flow сохраняет семантику C#**
  - Исходное состояние: inline lambda использует initialized locals, complete
    `if`, `switch`, returns и throw.
  - Действие: выполнить все reachable branches.
  - Ожидаемый результат: выбирается ожидаемая ветка, returns и throw работают
    как написано; unmatched mapping switch даёт
    `UnmatchedMappingSwitchException`, когда generator обязан добавить stub.

- [ ] **CALL-04 — Ненужные выражения не вычисляются**
  - Исходное состояние: выбранная и невыбранная branches имеют отдельные
    counters/throwing getters; одно нужное expression переиспользуется.
  - Действие: выполнить каждую branch отдельно.
  - Ожидаемый результат: expressions из невыбранной или неприменимой branch не
    вызываются; каждое нужное expression вычисляется не более одного раза.

- [ ] **CALL-05 — ConstructUsing возвращает готовый result**
  - Исходное состояние: interface destination создаётся factory callback, а
    `Members` имеет наблюдаемый side effect.
  - Действие: factory сначала возвращает object, затем `null`.
  - Ожидаемый результат: object получает Members; `null` является финальным
    result, Members не выполняется и null handling повторно не применяется.

- [ ] **CALL-06 — ResolveUsing управляет reuse/replacement**
  - Исходное состояние: callback получает `Option<Destination>` и может вернуть
    previous, new object или `null`; Members наблюдаем.
  - Действие: выполнить все три branches.
  - Ожидаемый результат: возвращается выбранное значение; Members выполняется
    для non-null result и пропускается для `null`; null handling после callback
    не запускается.

- [ ] **CALL-07 — Convert полностью владеет mapping**
  - Исходное состояние: manual mapping с callbacks для source, previous и
    context; settings null/member/constructor настроены на заметные значения.
  - Действие: выполнить Create, Update, null source и null destination.
  - Ожидаемый результат: `Convert` получает оригинальные inputs и возвращает
    финальный result; null handling, construction, conventions и Members вокруг
    него не выполняются; `MappingMode` по-прежнему соблюдается.

- [ ] **CALL-08 — User exceptions не оборачиваются**
  - Исходное состояние: constructor, member expression, factory и Convert по
    очереди бросают разные custom exceptions.
  - Действие: вызвать соответствующие mappings.
  - Ожидаемый результат: наружу выходит исходный exception type и полезный
    stack trace; Morphant не заменяет его `MorphantException`.

- [ ] **CALL-09 — Option различает отсутствие destination**
  - Исходное состояние: callback читает `HasValue`, `TryGetValue` и `Value`.
  - Действие: вызвать Create, Update с null и Update с non-null destination.
  - Ожидаемый результат: первые два получают `None`, третий `Some`; чтение
    `Value` у `None` бросает `OptionValueMissingException`.

- [ ] **CALL-10 — Whole-value special cases явно выражаются через Convert**
  - Исходное состояние: manual mappings для collection/tuple/delegate-like
    value используют обычный synchronous C# и при необходимости nested
    `context.Mapper` calls.
  - Действие: выполнить collection mapping с сохранением порядка и ещё один
    arbitrary whole-value mapping.
  - Ожидаемый результат: `Convert` возвращает написанный пользователем result;
    Morphant не пытается автоматически map elements или изменить алгоритм;
    documented workaround для отсутствующей automatic collection support
    работоспособен.

## 7. Settings, modes и null handling

- [ ] **SET-01 — Defaults совпадают с документацией**
  - Исходное состояние: bare mapping без MSBuild, mapper и map overrides.
  - Действие: проверить generated/runtime behavior всех шести settings.
  - Ожидаемый результат: `CreateAndUpdate`, `ReturnNull`, null destination
    `Create`, member `Auto`, constructor `Unambiguous`, unmapped validation
    `None`.

- [ ] **SET-02 — Precedence применяется независимо для каждого setting**
  - Исходное состояние: разные values заданы на MSBuild, base mapper, mapper,
    included mapping и current mapping levels.
  - Действие: собрать матрицу с `Default`, повторными writes и local override.
  - Ожидаемый результат: current map побеждает, затем nearest include, mapper,
    nearest connected base и MSBuild; `Default` продолжает поиск; последний
    write на одном level побеждает; settings не влияют друг на друга.

- [ ] **SET-03 — MappingMode проверяет обе операции**
  - Исходное состояние: три mappings с `Create`, `Update` и
    `CreateAndUpdate`.
  - Действие: вызвать Create и Update каждого mapping, включая Convert.
  - Ожидаемый результат: разрешённые calls работают; запрещённый call сразу
    бросает `MappingOperationNotSupportedException` с operation, source type,
    destination type и effective mode.

- [ ] **SET-04 — NullSourceHandling.ReturnNull**
  - Исходное состояние: nullable source, default `ReturnNull`, callbacks имеют
    counters.
  - Действие: Create и Update с null source.
  - Ожидаемый результат: возвращается `default(TDestination)`; callbacks и
    destination handling не выполняются; для non-nullable value destination
    возвращается zero-initialized value.

- [ ] **SET-05 — NullSourceHandling.ReturnDestination**
  - Исходное состояние: mapping настроен на `ReturnDestination`, есть existing
    destination и callback counter.
  - Действие: Create с null source и Update с null source.
  - Ожидаемый результат: Create возвращает default destination, Update — ровно
    supplied destination; callback counter остаётся нулевым.

- [ ] **SET-06 — NullSourceHandling.Throw**
  - Исходное состояние: mapping настроен на `Throw`.
  - Действие: Create и Update с null source.
  - Ожидаемый результат: оба бросают `NullSourceException` с правильными
    operation и pair; mapping expressions не выполняются.

- [ ] **SET-07 — NullDestinationHandling matrix**
  - Исходное состояние: два Update mappings с `Create` и `Throw`, destination
    равен null.
  - Действие: вызвать оба.
  - Ожидаемый результат: `Create` использует creation rules при operation
    Update; `Throw` бросает `NullDestinationException` до mapping expressions.

- [ ] **SET-08 — Null source имеет приоритет над null destination**
  - Исходное состояние: source и Update destination одновременно null;
    source/destination policies комбинируются в нескольких mappings.
  - Действие: выполнить combinations `ReturnNull`, `ReturnDestination`, `Throw`
    со destination `Create`/`Throw`.
  - Ожидаемый результат: source policy полностью завершает вызов; destination
    exception/creation и mapping expressions ни в одной комбинации не
    выполняются.

- [ ] **SET-09 — ConstructorSelection strategies различимы**
  - Исходное состояние: destinations подготовлены для `Explicit`,
    `Parameterless`, `Single`, `Unambiguous`, `Greediest` и `Largest`, включая
    ties и inapplicable largest constructor.
  - Действие: собрать и выполнить legal cases, затем ambiguous cases.
  - Ожидаемый результат: каждая strategy выбирает documented constructor;
    ties/невозможный выбранный constructor дают diagnostic, без silent fallback.

- [ ] **SET-10 — MemberSelection Auto и Explicit**
  - Исходное состояние: два одинаковых mappings отличаются только setting,
    один member явно использует `Auto()`.
  - Действие: Create и Update.
  - Ожидаемый результат: Auto mapping заполняет unmentioned compatible members;
    Explicit оставляет их как есть, но explicit `Auto()` работает.

- [ ] **SET-11 — UnmappedMemberValidation и severity**
  - Исходное состояние: mapping намеренно не использует source member и не
    заполняет destination member.
  - Действие: собрать с `Source`, `Destination`, `Strict`; затем изменить
    severities MORPH0047/0048 через `.editorconfig`.
  - Ожидаемый результат: появляются только применимые MORPH0047/0048 с точным
    location; default — warning; severity override меняет compiler presentation;
    `Ignore()` и standalone source discard учитываются документированно.

- [ ] **SET-12 — MSBuild settings доходят из package**
  - Исходное состояние: C9 задаёт каждую `Morphant*` property по очереди, не
    меняя mapping source.
  - Действие: clean build и runtime check каждого value; отдельно проверить
    mixed casing, empty, `Default` и invalid text.
  - Ожидаемый результат: valid values видны generator; names case-insensitive;
    empty/Default наследуют; invalid value даёт MORPH0022 и не превращается в
    случайный default.

## 8. Nested mappings, DI и scopes

- [ ] **NEST-01 — Explicit Map выбирает Create или Update по state**
  - Исходное состояние: parent mapping имеет writable child member и explicit
    `Map` rule; child mapping зарегистрирован.
  - Действие: parent Create, Update с child, Update без current child.
  - Ожидаемый результат: nested Create используется без current value, nested
    Update — с current value; выбранная operation наблюдаема в child context.

- [ ] **NEST-02 — Explicit Create и Update не адаптируются**
  - Исходное состояние: parent uses `Create(source.Child)` и отдельно
    `Update(source.Child, destination.Child)`.
  - Действие: выполнить parent Create/Update.
  - Ожидаемый результат: requested nested operation выполняется явно и не
    меняется из-за parent operation; отсутствие legal Update destination даёт
    documented diagnostic.

- [ ] **NEST-03 — Writable member сохраняет nested replacement**
  - Исходное состояние: nested Update возвращает новый child object.
  - Действие: обновить parent с writable child member.
  - Ожидаемый результат: returned child replacement присвоен обратно parent,
    старый child больше не является member value.

- [ ] **NEST-04 — Read-only member обновляется только in place**
  - Исходное состояние: readable reference child без setter и standalone
    `Update(source.Child, members.Child)` rule.
  - Действие: выполнить parent Update с non-null child, затем с null child;
    child mapper возвращает replacement.
  - Ожидаемый результат: non-null child получает in-place mutations, replacement
    отбрасывается; при null child nested call пропускается, потому что replacement
    невозможно присвоить.

- [ ] **NEST-05 — Nested lookup требует exact pair**
  - Исходное состояние: DI зарегистрирована только base либо другая generic
    pair, а rule запрашивает exact derived pair.
  - Действие: выполнить nested call.
  - Ожидаемый результат: Morphant не ищет assignable/open-generic substitute;
    бросается `MappingNotFoundException` для exact source/destination types.

- [ ] **NEST-06 — Incompatible current destination обнаруживается**
  - Исходное состояние: nested destination статически допускает base type, но
    runtime current object несовместим с required concrete child mapping.
  - Действие: выполнить nested Update.
  - Ожидаемый результат: `NestedDestinationTypeMismatchException` содержит
    expected и actual destination types; неправильный object не мутируется.

- [ ] **DI-01 — Одна registration выполняет mapping**
  - Исходное состояние: DI зарегистрирует concrete mapper, exact
    `ITypeMapper<,>` и scoped `IMapper` по quick start.
  - Действие: resolve `IMapper` из scope и вызвать Create/Update.
  - Ожидаемый результат: выбирается единственная exact registration; оба calls
    дают ожидаемые результаты.

- [ ] **DI-02 — Нулевая, множественная и null registration различаются**
  - Исходное состояние: три service providers: без exact mapper, с двумя exact
    registrations и с единственной registration, возвращающей null.
  - Действие: вызвать одинаковую pair в каждом provider.
  - Ожидаемый результат: соответственно `MappingNotFoundException`,
    `AmbiguousMappingException`, `InvalidMappingRegistrationException`;
    registration order не выбирает победителя.

- [ ] **DI-03 — Один generated mapper сохраняет scoped identity**
  - Исходное состояние: mapper implements несколько pairs, имеет constructor
    dependency и все interfaces зарегистрированы через один concrete scoped
    service.
  - Действие: resolve pairs внутри одного и разных scopes.
  - Ожидаемый результат: внутри scope используется один concrete mapper и одна
    scoped dependency; в другом scope instances новые; mappings выполняются.

- [ ] **DI-04 — MappingContext.Mapper ограничен top-level call**
  - Исходное состояние: callback сохраняет ссылку на `context.Mapper` только
    для проверки; есть два параллельных независимых top-level calls.
  - Действие: использовать mapper во вложенном call, после возврата top-level
    call и параллельно из двух отдельных top-level calls.
  - Ожидаемый результат: nested call внутри scope работает; использование
    сохранённого mapper после завершения бросает
    `MappingScopeCompletedException`; независимые top-level calls не делят
    scope и не мешают друг другу.

## 9. Configuration inheritance

- [ ] **INH-01 — base.Configure подключает defaults явно**
  - Исходное состояние: base mapper задаёт setting и mapping; derived mapper
    имеет variants с вызовом и без вызова `base.Configure(builder)`.
  - Действие: собрать и выполнить derived mapping.
  - Ожидаемый результат: setting наследуется только при вызове base Configure;
    base mapping становится доступен для composition, но не реализуется
    derived mapper автоматически.

- [ ] **INH-02 — IncludeBase объединяет member rules**
  - Исходное состояние: legal base/derived type pairs, base mapping rules и
    local derived rules с одним совпадающим destination member.
  - Действие: выполнить derived Create/Update.
  - Ожидаемый результат: неперекрытые base rules применены, local rule заменяет
    base rule для того же member, declaration order результата не меняет.

- [ ] **INH-03 — Result policy наследуется только для exact pair**
  - Исходное состояние: IncludeBase используется сначала для разных
    source/destination types, затем для exact same pair из base mapper.
  - Действие: проверить `Construct`, `Resolve`, runtime variants и `Convert`.
  - Ожидаемый результат: для разных pairs result policies не импортируются;
    member rules/settings импортируются. Для exact pair разрешённое наследование
    result policy работает по документации.

- [ ] **INH-04 — Precedence ближайшего include/base стабильна**
  - Исходное состояние: цепочка из нескольких base mappers/includes задаёт
    conflicting settings и member rules.
  - Действие: выполнить mapping и переставить независимые declarations.
  - Ожидаемый результат: current/nearest levels побеждают согласно documented
    precedence; порядок unrelated registrations не становится tie-breaker.

- [ ] **INH-05 — Unsupported inheritance завершается диагностикой**
  - Исходное состояние: отдельные INVALID cases с missing pair, incompatible
    types, duplicate relation, inaccessible inherited expression и
    cross-assembly configuration.
  - Действие: собрать каждый case.
  - Ожидаемый результат: cross-assembly base configuration даёт MORPH0016,
    остальные случаи — соответствующие MORPH0024–MORPH0028; нет generator
    crash, partial случайного наследования или silent fallback.

## 10. Compile-time diagnostics и runtime failures

Для каждого diagnostic smoke-test дополнительно проверяются: точный ID,
severity, location на пользовательском коде, понятный текст, рабочий help link
и отсутствие `AD0001`/stack trace generator. Suppression меняет показ
diagnostic, но не делает invalid configuration работоспособной.

- [ ] **DIAG-01 — Valid consumer чист от диагностик**
  - Исходное состояние: C9 с quick-start mapping и warnings as errors.
  - Действие: clean CLI/Rider build.
  - Ожидаемый результат: нет Morphant, compiler и analyzer warnings/errors;
    generated code также warning-free.

- [ ] **DIAG-02 — Compatibility diagnostic**
  - Исходное состояние: INVALID копирует valid mapper, но использует C# 8.
  - Действие: build.
  - Ожидаемый результат: MORPH0001 error указывает на unsupported language
    version; generator не падает. После возврата C# 9 diagnostic исчезает.

- [ ] **DIAG-03 — Declaration и registration diagnostics**
  - Исходное состояние: отдельные cases: attributed class не наследует
    `TypeMapper`, mapper не `partial`, duplicate pair registration.
  - Действие: build каждого case.
  - Ожидаемый результат: соответственно MORPH0005, MORPH0006 и MORPH0013;
    независимая legal pair в том же mapper продолжает генерироваться, когда её
    contract можно сохранить.

- [ ] **DIAG-04 — Configuration и composition diagnostics**
  - Исходное состояние: отдельные cases с unanalyzable Configure, duplicate
    Members/result rule и `Convert` вместе с declarative rules.
  - Действие: build.
  - Ожидаемый результат: применимые MORPH0017/0018, MORPH0019 и MORPH0020;
    invalid mapping получает typed failure stub, а не произвольный compiler
    cascade.

- [ ] **DIAG-05 — Settings и inheritance diagnostics**
  - Исходное состояние: invalid setting constant/MSBuild text, setting на
    неприменимом mapping и bad IncludeBase cases.
  - Действие: build.
  - Ожидаемый результат: MORPH0021–MORPH0028 по фактической причине; location
    указывает на argument/call, который пользователь должен исправить.

- [ ] **DIAG-06 — Callback diagnostics**
  - Исходное состояние: named delegate вместо inline lambda, inaccessible
    capture, unsupported statement, mutation previous/result и marker в
    unsupported position.
  - Действие: build каждого case.
  - Ожидаемый результат: MORPH0029–MORPH0033 без crash; message сообщает
    ограничение и практический путь исправления (`Convert` там, где уместно).

- [ ] **DIAG-07 — Construction и member diagnostics**
  - Исходное состояние: impossible construction, ambiguous constructor,
    invalid constructor rule, required member gap и invalid member rule.
  - Действие: build.
  - Ожидаемый результат: применимые MORPH0035–MORPH0043; generated mapper
    сохраняет legal `ITypeMapper` contract и бросает typed configuration
    exception при недоступной operation.

- [ ] **DIAG-08 — Nested и completeness diagnostics**
  - Исходное состояние: nested types cannot be inferred, incompatible result,
    invalid Update destination и Strict completeness mapping.
  - Действие: build.
  - Ожидаемый результат: MORPH0044–MORPH0046 errors и MORPH0047–MORPH0048
    warnings; каждый help link открывает страницу с причиной и исправлением.

- [ ] **DIAG-09 — Severity suppression не легализует configuration**
  - Исходное состояние: INVALID error diagnostic suppressed через
    `.editorconfig`.
  - Действие: build и, если compiler позволяет, вызвать generated operation.
  - Ожидаемый результат: diagnostic presentation подавлен, но invalid mapping
    не начинает выполнять guessed behavior; operation остаётся typed failure
    stub либо contract обоснованно не генерируется.

- [ ] **ERR-01 — Runtime exception metadata не требует parsing message**
  - Исходное состояние: scenarios для disabled operation, null source/null
    destination и lookup failures.
  - Действие: перехватить specific exceptions и прочитать properties.
  - Ожидаемый результат: operation, source type, destination type и
    type-specific metadata точны; все Morphant exceptions наследуют ожидаемый
    base type; message понятен без внутренних имён generator.

- [ ] **ERR-02 — Default MappingContext не маскирует ошибку**
  - Исходное состояние: default `MappingContext` и код, читающий его
    properties.
  - Действие: прочитать `Operation` и `Mapper` вне generated call.
  - Ожидаемый результат: бросается `InvalidMappingContextException`, а не
    `NullReferenceException` и не случайное default value.

- [ ] **ERR-03 — Declarative marker нельзя выполнить как runtime API**
  - Исходное состояние: ordinary runtime method напрямую вызывает `Auto`,
    `Ignore`, `Map` либо другой compile-time marker вне analyzed Configure.
  - Действие: выполнить method.
  - Ожидаемый результат: `RuntimeInvocationNotSupportedException` ясно
    сообщает misuse; приложение не получает fake marker value.

## 11. Incrementality, actualization и determinism

Эти проверки оценивают observable build/IDE behavior, а не внутреннее число
ветвей generator pipeline.

- [ ] **INC-01 — Cold generation полна и детерминирована**
  - Исходное состояние: две копии одного C9 consumer в разных чистых временных
    directories, generated output включён.
  - Действие: clean build обеих копий и сравнить relative hint names и bytes
    generated `.cs` files.
  - Ожидаемый результат: набор имён и содержимое полностью совпадают, не
    содержат absolute paths или случайных IDs; files начинаются с
    `// <auto-generated />`, включают `#nullable enable` и используют CRLF.

- [ ] **INC-02 — No-op и unrelated edit не меняют generated result**
  - Исходное состояние: C9 открыт в Rider, hashes generated files записаны.
  - Действие: повторная сборка без changes; затем изменить body unrelated
    method/type, не участвующего в mapping, и собрать снова.
  - Ожидаемый результат: generated file set и content hashes не меняются;
    IDE не показывает stale diagnostics или flicker исчезающего API.

- [ ] **INC-03 — Изменение mapper configuration актуализирует только нужное**
  - Исходное состояние: consumer имеет два независимых mappers/pairs.
  - Действие: по очереди добавить и удалить `Members`, `ConstructUsing` и
    `Convert` у первой pair; затем добавить и удалить вторую pair. После
    каждого шага сохранить файл и проверить live output в `Dependencies |
    Source Generators` без перезапуска Rider; отдельно выполнить build для
    проверки физического generated snapshot.
  - Ожидаемый результат: runtime behavior и generated content первой pair
    сразу обновляются и возвращаются к default после удаления настройки;
    независимая pair остаётся байтово прежней; добавленные artifacts появляются,
    удалённые исчезают; старое поведение не сохраняется из кэша.

- [ ] **INC-04 — Изменение mapped type актуализирует surface и diagnostics**
  - Исходное состояние: valid convention mapping с сохранённым generated
    output.
  - Действие: добавить/переименовать member, изменить его type/nullability,
    затем исправить source/configuration.
  - Ожидаемый результат: member/constructor plans и diagnostics сразу отражают
    каждое состояние; после исправления stale diagnostics исчезают, runtime
    mapping использует новую model.

- [ ] **INC-05 — Изменение MSBuild setting актуализирует behavior**
  - Исходное состояние: mapping зависит от assembly-level member/null/mode
    property.
  - Действие: менять property без правки C# и rebuild.
  - Ожидаемый результат: generated/runtime behavior и diagnostics меняются при
    каждом valid/invalid value; возврат исходного value восстанавливает
    исходный output.

- [ ] **INC-06 — Compilation context актуализируется**
  - Исходное состояние: fixture зависит от language version, preprocessor
    symbol, nullable context и conditional reference.
  - Действие: по одному менять эти inputs и rebuild через тот же Rider session.
  - Ожидаемый результат: compatibility/type analysis и generated output
    соответствуют текущей compilation; ни один результат предыдущего state не
    протекает в следующий.

- [ ] **INC-07 — Добавление и удаление mapper очищает generated state**
  - Исходное состояние: valid mapper успешно generated.
  - Действие: удалить attribute/mapper, rebuild; затем вернуть declaration с
    временной ошибкой и исправить её.
  - Ожидаемый результат: удалённые generated declarations исчезают; ошибка
    показывает правильный diagnostic; после исправления API восстанавливается
    без clean/restart.

- [ ] **INC-08 — Переименование и case-insensitive hint collision стабильны**
  - Исходное состояние: consumer содержит mapper/type names, дающие похожие
    sanitized hint names, включая различие только регистром.
  - Действие: build, затем unrelated edit и clean rebuild.
  - Ожидаемый результат: files не перезаписывают друг друга; stable hash suffix
    появляется только при реальной collision; hint names не меняются от
    unrelated edit.

## 12. Документация и внешний вид

- [ ] **DOC-01 — Root README корректно рендерится**
  - Исходное состояние: README открыт локально и на GitHub для RC commit.
  - Действие: проверить logo, headings, code fences и все links.
  - Ожидаемый результат: logo слева; install и first mapping видны без лишней
    прокрутки; links не сломаны; нет placeholder text или обещания
    неподдерживаемых features.

- [ ] **DOC-02 — Quick start копируется без скрытых шагов**
  - Исходное состояние: пустой consumer с PACKAGE, код берётся только из
    `docs/quick-start.md`.
  - Действие: выполнить install, declaration, DI registration, Create, Update,
    explicit rules и standalone calls.
  - Ожидаемый результат: snippets компилируются после добавления явно названных
    packages/usings; outputs соответствуют тексту; не требуется знание
    внутренних generated types.

- [ ] **DOC-03 — Guides согласованы с фактическим API**
  - Исходное состояние: все pages из `docs/README.md` и PACKAGE consumer.
  - Действие: по одному проверить signatures и ключевые examples Create/Update,
    conventions, declarative/manual/nested mapping, DI, inheritance и settings.
  - Ожидаемый результат: names, overloads, defaults и lifecycle точны;
    snippets короткие и не используют удалённый API. Generated plan type
    упоминается только в документированной форме Update read-only member.

- [ ] **DOC-04 — Limitations не противоречат продукту**
  - Исходное состояние: `docs/limitations.md`, public API и PACKAGE.
  - Действие: сопоставить Included/Not included со smoke scenarios и попытаться
    найти заявление о collections, projection, automatic flattening,
    polymorphism, keyed/reverse/async mapping.
  - Ожидаемый результат: поддерживаемое перечислено точно; отсутствующие
    features не обещаны; `Convert` описан как явный workaround там, где это
    действительно возможно.

- [ ] **DOC-05 — Diagnostics documentation полна**
  - Исходное состояние: catalog и 48 diagnostic pages.
  - Действие: проверить навигацию catalog → page → relevant guide, IDs,
    default severity, cause и fix; открыть несколько help links прямо из Rider.
  - Ожидаемый результат: все links рабочие, каждый ID уникален, причина и
    исправление соответствуют фактическому message; published IDs не
    перенумерованы и не переиспользованы.

- [ ] **DOC-06 — XML docs кратки и корректны**
  - Исходное состояние: public types/members просматриваются через Rider
    Quick Documentation из PACKAGE.
  - Действие: пройти весь public API inventory, особенно `Convert`, destination
    selection, `MappingContext`, null/mode settings и exception properties.
  - Ожидаемый результат: docs объясняют контракт без двусмысленности и лишней
    реализации; у `Convert` явно сказано, что null handling не применяется;
    context wording не предполагает, что в нём навсегда останется только
    operation.

- [ ] **DOC-07 — Repository links готовы к опубликованному tag**
  - Исходное состояние: RC ещё может не иметь tag, но все будущие URLs известны.
  - Действие: проверить relative links локально, GitHub `main` links сейчас и
    syntactic target links на `v0.1.0`.
  - Ожидаемый результат: текущие links открываются; tag links указывают на
    реально существующие paths и начнут работать без правки файлов сразу после
    создания `v0.1.0`.

- [ ] **DOC-08 — Терминология единообразна**
  - Исходное состояние: README, docs, XML docs, diagnostics и exceptions.
  - Действие: поискать варианты названий операций/API и устаревшие terms.
  - Ожидаемый результат: Create/Update, source/destination, mapping/mapper,
    `Construct`, `Resolve`, `Members`, `Convert` используются последовательно;
    нет ссылок на удалённые implementation plans или незавершённый v0.

## 13. Совместимость и дополнительные окружения

- [ ] **COMP-01 — C# 9 остаётся минимальным language contract**
  - Исходное состояние: C9 использует только C# 9 syntax и PACKAGE.
  - Действие: clean restore/build/run с warnings as errors.
  - Ожидаемый результат: generator загружается и весь documented core scenario
    работает; C# 8 case получает MORPH0001, а не analyzer load failure.

- [ ] **COMP-02 — Новая syntax consumer не ограничена Roslyn 4.4 baseline**
  - Исходное состояние: LATEST на compiler с collection expressions; mappings
    используют collection expressions в `Construct`, `Resolve` и `Members`
    через подходящий `Value<T>` target type.
  - Действие: build/run всех трёх mappings из PACKAGE.
  - Ожидаемый результат: expressions компилируются и выполняются с ожидаемыми
    values; generator, собранный против 4.4.0, не ограничивает language version
    consumer.

- [ ] **COMP-03 — netstandard2.0 compile boundary работает**
  - Исходное состояние: новый C# 9 class library target `netstandard2.0` с
    direct PACKAGE reference и simple mapper.
  - Действие: clean build.
  - Ожидаемый результат: runtime reference и analyzer совместимы, warnings и
    dependency conflicts отсутствуют; generated code не требует более нового
    target framework API.

- [ ] **COMP-04 — Современные runtime hosts выполняют один contract**
  - Исходное состояние: один consumer scenario собран/запущен на доступных
    stable `net8.0` и `net10.0` hosts.
  - Действие: Create, Update, DI nested mapping и exception smoke на каждом.
  - Ожидаемый результат: observable results и exception types одинаковы; нет
    binding/version conflicts.

- [ ] **COMP-05 — Nullable и oblivious consumers не ломают generator**
  - Исходное состояние: отдельные projects/regions с nullable enabled и
    disabled annotations.
  - Действие: собрать mappings с reference/value nullable contracts.
  - Ожидаемый результат: generated annotations повторяют input contract;
    warning-free conversions работают; Morphant не создаёт лишние nullable
    warnings и не скрывает реальные.

Следующие platform checks обязательны, если окружение доступно; иначе для
первого релиза они фиксируются как `N/A` и переносятся в CI matrix.

- [ ] **COMP-06 — Windows x64**
  - Исходное состояние: чистый Windows host с поддерживаемым SDK, Rider или
    CLI и PACKAGE из карточки.
  - Действие: restore/build/run quick start и diagnostic smoke.
  - Ожидаемый результат: те же результаты, пути и line endings не вызывают
    ошибок; package не зависит от Unix tooling.

- [ ] **COMP-07 — Linux x64**
  - Исходное состояние: чистый Linux host с тем же PACKAGE.
  - Действие: restore/build/run quick start и diagnostic smoke.
  - Ожидаемый результат: те же результаты; filesystem case sensitivity не
    вызывает hint-name collisions или missing files.

- [ ] **COMP-08 — macOS arm64/x64**
  - Исходное состояние: доступный macOS host с поддерживаемым SDK и тем же
    PACKAGE.
  - Действие: restore/build/run quick start и diagnostic smoke.
  - Ожидаемый результат: те же results и diagnostics; analyzer загружается на
    host architecture без дополнительной настройки.

## 14. Финальное решение

- [ ] **FINAL-01 — Проверен именно неизменённый RC**
  - Исходное состояние: все обязательные checks отмечены; source не должен был
    меняться после PKG-01.
  - Действие: повторно сравнить current HEAD с SHA карточки и проверить Git
    status.
  - Ожидаемый результат: HEAD всё ещё равен RC SHA, tree чист; иначе package и
    результаты признаны устаревшими.

- [ ] **FINAL-02 — Проверены именно зафиксированные artifacts**
  - Исходное состояние: consumer runs завершены.
  - Действие: повторно вычислить hashes `.nupkg` и `.snupkg`.
  - Ожидаемый результат: hashes совпадают с карточкой; файлы не были
    перепакованы или заменены после проверки.

- [ ] **FINAL-03 — Нет незакрытых отклонений**
  - Исходное состояние: заполненный checklist и release evidence.
  - Действие: просмотреть все unchecked, `N/A`, warnings и заметки.
  - Ожидаемый результат: обязательных пропусков и необъяснённых отклонений нет;
    каждое `N/A` относится только к дополнительной platform check и имеет
    причину.

- [ ] **FINAL-04 — Tag сможет однозначно назвать RC**
  - Исходное состояние: release tag ещё не создан либо создан локально без
    публикации.
  - Действие: проверить planned annotated tag `v0.1.0`, его target SHA и
    changelog/package links без push/publish.
  - Ожидаемый результат: tag указывает ровно на RC SHA; повторная сборка или
    commit после tag не требуется.

- [ ] **FINAL-05 — Принято решение GO/NO-GO**
  - Исходное состояние: FINAL-01–FINAL-04 завершены.
  - Действие: записать решение, дату и проверяющего в карточку.
  - Ожидаемый результат: `GO` ставится только при полном прохождении
    обязательных gates. При `NO-GO` записаны blocking checks и новый candidate
    получает новый полный цикл проверки.

- [ ] **FINAL-06 — Публикация разрешена для проверенного artifact**
  - Исходное состояние: принято решение `GO`; release workflow ожидает approval
    environment `nuget.org`; его commit, version, artifact name и hashes
    совпадают с карточкой.
  - Действие: проверить summary всех jobs и одобрить deployment.
  - Ожидаемый результат: workflow публикует тот же `.nupkg`, создаёт annotated
    tag `v0.1.0` на RC commit и GitHub release с обоими packages и
    `SHA256SUMS`; повторной сборки после approval нет.

## Рекомендуемая последовательность CLI-команд

Пути к временной папке и LOCAL FEED нужно подставить явно. Команды не удаляют
исходники или repository history; `clean` касается только build outputs.

Сначала выполняется дополнительная проверка нового Roslyn:

```shell
dotnet test src/tests/Morphant.Generator.UnitTests/Morphant.Generator.UnitTests.csproj \
  -c Release -p:MorphantRoslynVersion=4.9.2
```

Затем создаётся финальная baseline-сборка на Roslyn 4.4.0:

```shell
dotnet clean src/Morphant.slnx -c Release
dotnet restore src/Morphant.slnx -p:MorphantRoslynVersion=4.4.0
dotnet build src/Morphant.slnx -c Release --no-restore \
  -p:MorphantRoslynVersion=4.4.0

dotnet test src/tests/Morphant.Generator.UnitTests/Morphant.Generator.UnitTests.csproj \
  -c Release --no-build --no-restore -p:MorphantRoslynVersion=4.4.0

dotnet test src/tests/Morphant.Generator.IntegrationTests/Morphant.Generator.IntegrationTests.csproj \
  -c Release --no-build --no-restore -p:MorphantRoslynVersion=4.4.0
```

PACKAGE создаётся без повторной компиляции:

```shell
dotnet pack src/Morphant/Morphant.csproj -c Release \
  --no-build --no-restore --output <LOCAL_FEED> \
  -p:PackageVersion=0.1.0 -p:MorphantRoslynVersion=4.4.0
```

Для изолированного consumer restore можно передать отдельные paths, не очищая
общий machine-wide NuGet cache:

```shell
dotnet restore <CONSUMER.csproj> \
  -p:RestoreSources=<LOCAL_FEED> \
  -p:RestorePackagesPath=<TEMP_PACKAGE_CACHE>

dotnet build <CONSUMER.csproj> -c Release --no-restore \
  -p:RestorePackagesPath=<TEMP_PACKAGE_CACHE>
```

Для DI consumer к `RestoreSources` добавляется обычный доверенный source,
необходимый для `Microsoft.Extensions.DependencyInjection`; Morphant всё равно
должен разрешиться из LOCAL FEED. В логах restore нужно проверить фактический
source и version, а не только успешный exit code.

## Минимальный набор сохраняемых свидетельств

- commit SHA и вывод `git status`;
- `dotnet --info`;
- summaries build, unit 4.4.0, unit 4.9.2 и integration tests;
- списки entries `.nupkg` и `.snupkg`;
- извлечённый nuspec;
- public key tokens обеих assemblies;
- SHA-256 обоих package artifacts;
- логи clean consumer restore/build/run;
- список `N/A` с причинами и итоговое GO/NO-GO.

CI/CD автоматизирует воспроизводимые пункты этого документа. Ручными release
gates остаются package presentation, Rider experience, смысл сообщений и
документации, а также окончательная проверка соответствия artifact конкретному
commit.
