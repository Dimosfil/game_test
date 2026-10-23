# Итерация 03 — исследования и измерение графики

Дата: 21.10.2026. Ruleset: `prototype-terran-0.3`.
Статус: итерация завершена в объёме экспериментального Terran tech/research
среза и rendering profile. Финальные compile, contracts, actual-rules smoke,
Release/Development builds, paired profiles и graphical standalone пройдены.
Полные gates E0–E3 и дальнейшая цель остаются открытыми.

## Объём и владельцы

Третий пакет продолжает [итерацию 02](iteration-02.md): отдельный standalone
render/frame стенд, исправление измеренного geometry bottleneck и небольшой
Terran tech/research loop. Это экспериментальный срез, а не полный Terran
roster, числовая копия SC2 или завершение E0–E3. Общая цель остаётся в
[плане](implementation-plan.md), продолжение — в [очереди](next-iterations.md).

- Simulation/Research владеет доступностью, prerequisites, оплатой, резервом,
  завершением, отменой и эффективными значениями damage/armor.
- Content задаёт Engineering Bay, Armory, шесть отдельных уровней улучшений
  пехотного оружия/брони и связи требований в едином runtime catalog.
- Presentation показывает authoritative availability, собственную очередь,
  прогресс и технологический граф; она не рассчитывает цену или урон отдельно.
- ProceduralArt/ArtBuilder владеет shared geometry и временем её жизни.
  RenderingProfile владеет воспроизводимым измерением; он не меняет rules.
- Интегратор объединяет источники, HUD/input и все Editor/build операции;
  независимые агенты проверяют research и mesh lifecycle contracts.

Контракты: [research](../../tools/project-memory/specs/rts-research-tech.md),
[render profile](../../tools/project-memory/specs/rts-render-profile.md).
Точная процедура измерения — [render-profiling](render-profiling.md).

## Реализованное поведение

Исследование выбирает одного завершённого собственного производителя.
Prerequisites и средства проверяются до списания; у здания не более одного
активного исследования, одновременные research и production запрещены.
Одно улучшение нельзя одновременно зарезервировать у двух производителей
владельца. Противники имеют независимые upgrades.

Отмена возвращает фактически оплаченную стоимость и освобождает резерв;
разрушение производителя теряет стоимость и прогресс. Завершённые улучшения
действуют на существующих и будущих подходящих юнитов владельца. Damage bonus
применяется перед armor; минимальный урон попадания — 1. Это правила
экспериментального каталога, точное соответствие SC2 не заявлено.

Состояние research и завершённые upgrades входят в hash, JSON snapshot и replay.
Restore проверяет цепочки уровней, производителя, резерв, таймеры и оплаченную
стоимость. Observation скрывает чужие очереди и завершённые исследования.
F2 открывает технологический граф; HUD показывает доступность и ход исследования.

Typed observation projection заменяет JSON-клонирование на горячем пути,
сохраняя detached read model и redaction. Сравнение с прежним fallback для
обоих игроков на 601 тике не выявило различий. Это проверка семантики projection,
а не основание передавать diagnostic State/Find сетевому клиенту. Проверка
сохранена в `observation-equivalence.json`: old reference и reflection fallback,
все DTO fields и final JSON; исходники и probe привязаны SHA256. Это retained
успешный bounded probe, а не дополнительный Unity test или performance result.

Процедурная графика переиспользует геометрически одинаковые combined meshes.
Режимы Unique/Shared выбираются на экземпляре; материалы команд, все детали,
поворотные узлы и независимая анимация сохраняются. Последняя lease удаляет
cache entry и освобождает mesh; живые соседние объекты не теряют ресурс.

## Проверки правил и интеграции

| Проверка | Подтверждённый результат | Evidence под Builds/Evidence/Iteration03 |
| --- | --- | --- |
| Финальная Unity compile | 0 errors, 0 warnings | `final-compile.json` |
| Финальные EditMode contracts | 104/104 passed, 0 skipped/failed, 16.2 s | `final-editmode-results.json` |
| Research contracts | 33 случая: prerequisites, cost, reservation, cancel/death, combat, visibility и persistence | ResearchTests в финальном результате |
| Mesh/cache contracts | 14 случаев: geometry identity, palette, animation, lease cleanup и PreviewScene lifecycle | RenderCacheTests в финальном результате |
| Actual-rules/live-AI smoke | passed; 1200 ticks, snapshot Tick 600, original/restore/replay hash совпали | `smoke.json`, `final-smoke-job.json` |
| Actual-catalog technology smoke | passed; 31 149 ticks, 20 принятых команд, все шесть upgrades и новый marine | `technology-smoke.json` |
| Mid-research JSON restore | Снимок Tick 11 821; продолжение совпало | `technology-research-snapshot.json` и smoke |
| Accepted-command replay | Конечный hash равен исходному | `technology-smoke.json` |
| Финальные Release/Development builds | Обе сборки пройдены; runtime gates проверены отдельно | `build-results.json`, `source-manifest.json` |
| Финальный graphical standalone | passed/exit0, 283 frames, 0 runtime errors, research fixture workflow пройден | `StandaloneVerified/player-smoke.json` |

Technology smoke использует неизменённый runtime catalog и настоящие команды
рабочих: добыча минералов/газа, refinery, depot, barracks, Engineering Bay и
Armory. AI в этом длительном fixture выключен; это не проверка самостоятельной
технологической стратегии бота. Живой AI после mid-research restore отдельно
проверяется контрактным тестом `MidResearchJsonSavePreservesWorkingAiAndAcceptedReplay`.
Обычный actual-rules smoke с работающим AI отдельно прошёл 1200 ticks:
`60c597ff1226122835f851fbbeadb63dc7dbb67268e9c08b9d4e7b4f5991de7d`
совпал для оригинала, JSON restore и replay.

Итоговый gameplay/replay hash:
`5c424cbbd0a5c73ceacc96615a5d6e57272d71d6eda125b9c7b88cb19160fcdc`.
Снимок и компактный manifest получены из настоящего command timeline; финальный
player smoke загрузил его через opt-in `-rtsTechnologyFixture`, отменил
исследование с refund, снова запустил его и снял producer HUD/graph progress.

Graphical standalone прошёл реальные GameHost entry points и Update/IMGUI:
обычный матч, selection, Patrol, pause/save/load и frame-driven replay от Tick 0
до точного Tick 618. Saved/loaded/replay final hashes совпали:
`27d42c09213d26a34cfd63a2d872814237754a3e87cbbb8636564c280aba742a`.
Затем genuine research fixture Tick 11821 прошёл product load, cancel/refund,
повторный research и progress capture. Все 11 проверок прошли; 283 frames,
0 runtime errors, PNG 1600×900: `gameplay.png`, `worker-hud.png`,
`technology-tree.png`, `research-hud.png`, `technology-progress.png`.
Это автоматизация публичных игровых входов, не физический keyboard/mouse playtest.

## Standalone profile: финальные парные измерения

Оконный Release player: Ryzen 5 2600 / Radeon HD 7800, D3D11, Built-in,
1600×900, Ultra, 4×MSAA, soft shadows, VSync off. На сценарий 90 warmup и
180 timed реальных camera/IMGUI кадров. Render-only и combined сравниваются
раздельно; combined сохраняет одинаковую экранную геометрию и добавляет
независимую движущуюся 20 Hz simulation army с detached observation.

Все четыре Release profiles завершились `passed`: `RenderBaselineVerified`,
`RenderOptimizedVerified`, `CombinedBaselineVerified`,
`CombinedOptimizedVerified`. Baseline/optimized исполнялись одной сборкой
с Unique/Shared режимом соответственно, без изменения видимой геометрии.
`verified-comparison.json` подтверждает равенство hardware/geometry и SHA256
runtime assembly:
`f179978f94f92b3fa0ea35d4e25aa9bfb147209cde33e64fce01f6aa82144177`.
Финальный `source-manifest.json` содержит 111 inputs и 6 binary entries;
большие generated artifacts остаются в игнорируемом Builds.

Render-only, без simulation/AI/fog/gameplay HUD/input:

| Marines | Frame p95 baseline → optimized, ms | Unique meshes baseline → optimized | Native draw calls p50 baseline → optimized |
| --- | --- | --- | --- |
| 200 | 19.7907 → 5.5008 | 3200 → 13 | 9009 → 207 |
| 400 | 42.0043 → 12.7139 | 6400 → 13 | 18009 → 229 |
| 800 | 72.2643 → 24.7756 | 12800 → 13 | 36102 → 265 |

На 800 render-only native GPU p50 уменьшился с 55.5090 до 3.5950 ms;
active CPU Main Thread p50 — с 27.1042 до 20.4109 ms. После снятия geometry/
draw overhead главная оставшаяся render-only стоимость находится на CPU.
Материалы, модели, анимация, число деталей и видимость не упрощались.

Combined с независимым движущимся simulation workload:

| Marines | Frame p95 baseline → optimized, ms | Step + Observe p95 baseline → optimized, ms | Первый group-command step baseline → optimized, ms |
| --- | --- | --- | --- |
| 200 | 23.0625 → 12.5970 | 8.6706 → 8.4086 | 38.3282 → 37.5204 |
| 400 | 52.4151 → 26.9207 | 21.2825 → 18.2165 | 29.5951 → 33.9552 |
| 800 | 106.6294 → 51.2135 | 42.4262 → 32.4235 | 68.4143 → 59.2852 |

Во всех сценариях четырёх profiles: 180 actual camera frames, 180 GUI repaints,
180 focused frames, 0 catchup clamps; cache после cleanup возвращается к
исходному числу leases. Длина timed interval задана кадрами, поэтому число
20 Hz ticks различается: baseline 53/118/289, optimized 23/53/154. Это короткий
контролируемый profile, а не доказательство длительной игровой устойчивости.
Отдельный первый массовый приказ не скрыт в steady-state percentiles.

Native GPU timing доступен. Release native GC allocations/frame не зарегистрирован,
а calibrated System.GC probe на этом runtime не производит валидные байты.
Обе метрики обозначены unavailable. Предыдущие постоянные нули не доказывают
отсутствие allocations и исключены из итогов; папки `*Final` не используются.

Отдельный `DevelopmentAllocationsVerified/profile.json` прошёл с producing
native `GC Allocated In Frame` (180 samples на сценарий):

| Marines | Native allocation p50, bytes/frame | Native allocation p95, bytes/frame |
| --- | --- | --- |
| 200 | 696 | 290952 |
| 400 | 696 | 496288 |
| 800 | 908954 | 915776 |

Это allocations всего измеряемого Development frame, не отдельно simulation
step и не Release budget. При 800 остаётся около 916 KB/frame p95: следующий
пакет должен найти allocation owners в model/simulation и presentation без
ослабления detached observation. Development timing не смешивается с Release
paired results; System.GC fallback в Development также unavailable.

`passed` в profile означает завершение измерения с валидными frames, а не
достижение target FPS. На 400/800 budget 60 FPS не выполнен даже в этом fixture;
полный игровой session/fog/HUD/input этим стендом не моделируется.

## Найденные дефекты и исправления цикла

- Click-through кнопки технологии в игровой мир: исправлен общий input gate,
  добавлена регрессия; сам программный input test не доказывает физический ввод.
- Technology smoke сначала не дожидался доступных средств/корректной смены
  mineral target. Исправлена автоматизация на настоящих командах; runtime
  цены и ожидания тестов ради успеха не ослаблялись.
- Render tests столкнулись с ограничением активной сцены Unity Test Framework;
  проверка изолирована в PreviewScene, без замены пользовательской сцены.
- Actual Unity lifecycle выявил неосвобождённые mesh leases в EditMode:
  resource owner получил ExecuteAlways и явный Awake; повторные 14 случаев прошли.
- Непроизводящий System.GC API возвращал 0 без exception. Добавлена известная
  allocation calibration; такая метрика теперь unavailable с причиной.

Неуспешные промежуточные прогоны сохранены как история ошибок. Они не заменяют
финальный статус и не используются для favourable performance comparison.

## Открытые проверки и следующий пакет

- Windows physical mouse/keyboard playtest по-прежнему отдельный gate;
  ScreenCapture и вызовы публичных product entry points его не заменяют.
- Следующий performance owner — CPU animation/renderers, Step/Observe и
  allocations на 400/800; shared geometry не закрывает стоимость множества
  Transform/Renderer и создание detached model objects.
- Измерен P2 в drop-off routing: глобальный endpoint/stoppingDistance и tie
  направляют рабочих в один угол базы. Navigation в этой итерации не исправлялась.
- Технологическая стратегия AI, полный Terran roster и точные SC2 build/data/map
  manifest остаются открыты; network spike не выполнен. Другие расы, кампании,
  Co-op и Arcade не реализованы этим пакетом.

Продолжать по [next-iterations](next-iterations.md), сохраняя этот проверенный
baseline и отдельные reproduction evidence для каждого нового исправления.
