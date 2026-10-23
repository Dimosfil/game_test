# Итерация 04 — CPU, память и UI-иконки

Цель: приблизить и проверить бюджет 16,67 мс на кадр с 800 юнитами на текущем
ПК при прежних моделях, 1600×900, 4×MSAA и soft shadows; заменить текстовые
команды иллюстрированными иконками. Ограниченная цель настоящего 800-unit
Patrol session достигнута: Release frame p95 **11.4067 ms** при selection1 и
**14.2755 ms** при selection802, simulation выдержала 20 Hz. Live combat и
полная игра требуют отдельных измерений. Итерация завершена в этом объёме;
финальные paired/GC/rules gates ниже пройдены.

## Контракты до изменений

| Сценарий | Механизм и владелец | Изменение | Проверка |
| --- | --- | --- | --- |
| Step → knowledge → Observe | Simulation/Match.Observation | Точный scanline raster и derived visibility cache; наружу detached masks | Старый raster и hashes, mutation/restore/commands, Unity contracts |
| 800 animated marines → камера | Presentation/ProceduralArt | Instanced submission прежней geometry/materials, pose matrices вместо тысяч native Renderer/Transform | Legacy/compact geometry и pose parity, leases, paired actual frames |
| Header/commands/research → действие | BattleHud/TechnologyPanel | Один generated atlas, cached mapping/UV, иконки с подписями и прежними hitboxes | Atlas coverage, standalone HUD/tree PNG, smoke actions |
| Выделение 1/all → HUD и world health bars | BattleHud | Reused selection HashSet/list snapshot на OnGUI; world health pass только Repaint | Та же видимость/selection/кнопки, новая session phase evidence |
| 800 health bars → GPU | HudHealthBarBatch | Прежние black/fill rectangles в одном reused dynamic mesh и одном immediate draw | Actual screenshot/order/clip parity и draw-count evidence Cycle3 |
| Доказательство frame budget | RenderingProfile + launcher | Явный выбор legacy/instanced с тем же Shared mesh режимом, отдельный Development GC | 200/400/800, actual camera/GUI/focus, Step/Observe, clamps, availability |

Один интегратор управляет Editor и всеми timing runs. Агенты владеют
Simulation, rendering и UI соответственно; границы API согласуются до
перекрёстных изменений. Rules/save/replay version и авторские значения не меняются.
Производительность pure .NET probes не выдаётся за результат Unity player.
Сравнение полного session/HUD/fog требуется отдельно от synthetic combined.

Evidence текущего пакета: игнорируемый `Builds/Evidence/Iteration04/`.
Предыдущий baseline и ограничения: [iteration-03](iteration-03.md).

## Реализация и границы

Simulation считает видимые клетки точными включительными scanline spans вместо
повторного обхода квадрата каждого источника зрения. Derived masks принадлежат
стабильному state/tick; во время Step изменяемый мир не использует устаревший
кэш. Observation получает копии масок, restore не переиспользует spawn masks
конструктора. Дробные центры, границы карты, нулевой/большой sight и прежняя
радиальная семантика сохраняются; height/terrain LOS и cloak не добавлены.
Typed copy public DTO сокращает boxing/reflection на горячем пути, сохраняя
deep copy, redaction и fallback. Это оптимизация механизма, не смена правил.

Compact marine backend использует исходные mesh/material parts и командные
палитры, но хранит позы в matrices и отправляет instanced batches вместо
тысяч native child Renderer/Transform. Сохраняются детали модели, idle/walk/
attack poses и flash. Prototype владеет leases; последний compact instance
освобождает их. Освобождение одного владельца сохраняет warm buffers других
владельцев. Неподдерживаемый GPU/shader и другие definitions используют legacy
shared geometry. Normal gameplay направляет marine в compact backend;
profile имеет явный legacy/instanced выбор для парной проверки одной сборки.

24 generated UI icons подключены одним atlas в resource header, selection,
action/build/train/research buttons и технологическом графе. Подписи, hotkeys,
цены, authoritative gates и прежние hitboxes сохраняются. Resource загружается
однократно; GUIContent/стили кэшируются. PNG, mapping, SHA и точный prompt:
[ui-icons](ui-icons.md). Pixel edits и Git commit медиа не выполнялись.

## Pure .NET диагностика — не Unity FPS

`simulation-baseline.json`, `simulation-fog-batch1.json`,
`simulation-typed-clone-batch2.json` и `simulation-comparison.json` получены
bounded probe в .NET 10.0.11, без Unity native/runtime frame measurement.
Сравнивается одинаковый GraphicsSimulationFixture: 200/400/800, 240 steady
samples до Tick 270; первоначальный group-command spike выделен отдельно.

| Marines | Средний Step + Observe baseline → final, ms/tick | Managed allocation baseline → final, bytes/tick | Checkpoint hashes |
| --- | --- | --- | --- |
| 200 | 7.6504 → 1.4779 | 238552.8 → 123177.1 | Совпадают |
| 400 | 13.6996 → 1.8798 | 393322.3 → 204310.7 | Совпадают |
| 800 | 27.8017 → 3.9071 | 704218.2 → 368252.7 | Совпадают |

Это **средние pure CLR показатели**, не Unity p95, native GC/frame, GPU time
или доказательство 60 FPS. Их назначение — выделить механизм и сохранить
одинаковую семантику перед измерением настоящего player.

Отдельный pure equivalence probe: 601 последовательный tick × оба игрока,
старый naive raster и deep-copy reference, typed/fallback projection, final
JSON совпадают; full State/Rules copy сверяется каждые 20 ticks. Источники и
probe привязаны SHA в `simulation-comparison.json`. Диагностический sample
buffer позднее получил предварительную ёмкость и overflow failure без смены
gameplay. Этот probe не увеличивает счётчик Unity tests.

## Циклы интеграционной проверки

Первый реальный Unity EditMode прогон: **125/126 passed**, один failed,
35.1 s, без skipped (`editmode-first.json`). Девять VisibilityCacheTests и
12 InstancedArtTests прошли в этом прогоне. Единственный failure — новый
реальный asset integration test: atlas шириной 1536 импортировался как 2048
из-за NPOT scaling. Проверка не заменялась синтетической текстурой, ожидание
не ослаблено: интегратор исправил importer на NPOT None и повторил прогон.
После исправления реальные Unity compile и EditMode подтвердили **0 errors/
warnings и 126/126 passed**, 0 skipped, **33.96 s** (`compile-batch2.json`,
`editmode-batch2.json`). Atlas импортирован точно 1536×1024, readonly, без mipmaps;
в итог входят 12 compact parity, 9 fog и один real atlas integration случай.

Независимый review выявил и закрыл в source несколько причин:

- Session output candidate присваивается рабочему evidence path только после
  проверки границы Builds; failure handler не пишет в отвергнутый внешний путь.
- Finish гарантирует Quit даже при ошибке записи evidence; screenshots имеют
  deadline, ожидание стабильного файла и PNG readback 1600×900.
- Live AI мог построить цель в пределах зрения крайнего Patrol marine и
  превратить нагрузку в бой. Только opt-in session fixture отключает AI и
  проверяет отсутствие атак; обычные live-AI smoke остаются отдельной проверкой.
- Simulation sample list не расширяется внутри timed interval: capacity
  выделена заранее, переполнение явно завершает тест ошибкой.
- Освобождение последнего enemy prototype больше не сбрасывает batches
  живых friendly instances и не требует повторного выделения всех их buffers.

Compile/tests Cycle2 подтверждены: **131/131 passed**, 0 skipped/fail,
**35.25 s**, compile **0 errors/warnings** (`editmode-cycle2.json`,
`compile-cycle2.json`). Добавлены пять CombatTargeting regression случаев.
Обе Windows builds Cycle1 были успешны; Cycle2 Release использована в настоящем
session profile ниже. После последних boundary/selection исправлений финальная
Unity проверка: **140/140 passed**, **33.97 s**, 0 failed/skipped,
compile **0 errors/warnings** (`editmode-final.json`, `compile-final.json`),
включая девять runner cases. Ранее `editmode-cycle3-final.json` прошёл за 34.41 s;
актуальный результат — `editmode-final.json`. Первый failed
прогон сохранён как история причины
и исправления; он не подменяет повторную успешную Unity проверку.

## Финальная матрица

| Gate | Подтверждённый статус | Evidence и границы |
| --- | --- | --- |
| Unity compile + EditMode | Final passed: 0 errors/warnings, 140/140, 33.97 s | `compile-final.json`, `editmode-final.json` |
| Actual-rules и technology smokes | Final passed: 1200 ticks live AI и 31149 ticks technology | `smoke-final.json`, `technology-smoke-final.json`, restore/replay hashes совпали |
| Release/Development Windows builds | Обе final builds passed | `build-final.json`, source/binary SHA manifest |
| Render-only | Исторический Compact Cycle1 passed; новой финальной пары нет | Final backend comparison использует combined ниже |
| Combined legacy/compact | Final pair passed на одной Release build | `verified-comparison.json`, 200/400/800, 15 s/case, actual frames, 0 clamps |
| Native Development GC | SessionDevelopmentFinal passed, producing samples в обеих фазах | GC p95 380861/380260 bytes/frame, отдельная Development build |
| Реальная session 800 | SessionFinal passed, обе фазы Meets60FpsP95=true | `SessionFinal/session-profile.json`, 20 Hz, 0 clamps/unfocused, 800 Patrol centers |
| Graphical smoke с generated icons | StandaloneFinal passed: 291 frames, 0 runtime errors | Worker/technology captures просмотрены; полный session batching gate отдельно |

Session fixture добавляет 800 marines к исходным spawns и очищает terrain;
definitions/economy/research rules сохраняются. AI отключён только в этой
opt-in нагрузке, combat не измеряется. Используется настоящий GameHost,
BattleView, fog/minimap/HUD и input component; zoom 48, 1600×900. Две фазы —
один выбранный marine и вся собственная армия — проверяются отдельно после
графического warmup, минимум 15 seconds и 180 frames каждая. Публичные input
entry points и активный input component не доказывают physical mouse/keyboard.

## Подтверждённый runtime Cycle1

`SessionCycle1/session-profile.json` не достиг графического warmup 90 frames за 30 seconds;
sampling phases не начались. Это **failed gate**, не измеренные session p95
и не подтверждение полного frame budget. Cycle2 исправил локальный
combat candidate filter перед дорогим visibility scan, линейный selection
lookup в View/HUD и повторные native pose/scale writes. UI сохраняет тот же
Selection API, hitboxes, icons, labels и progress; health-bar drawing без
input controls перенесён исключительно в Repaint. Unity/player результаты
Cycle2 приведены отдельно; они не переписывают этот failed прогон.

Причина подтверждена отдельно pure .NET probe **точного real-catalog Patrol
fixture**, а не прежнего Move combined: `simulation-session-engage-baseline.json`,
10 ticks. Средний Step — 268.3597 ms, из них Orders — 264.1938 ms; Observe —
0.9499 ms. Начальный шаг с 800 отдельными Patrol commands — 483.9717 ms; это
не одна групповая команда Move из combined fixture. Orders выделяют
516466.4 bytes/tick. Локальный Patrol candidate выполнял глобальный visibility
scan до дешёвой проверки локальной дальности; nearest-search Cycle2 сохраняет
фильтры и distance/ID tie semantics. Эти значения — pure CLR диагностика причины,
не measured session frame percentiles.

Следующая pure проверка разделила steady Patrol на подсистемы: 100 samples
после warmup до Tick 130 (`simulation-session-substage-steady.json`). Step mean
20.8379 ms, Orders 19.7491 ms, Engage 5.0554 ms, MoveToward 14.3123 ms.
Owner candidate index дал Engage **0.3343 ms и 0 bytes/tick в этом stage**, Step mean
15.1226 ms; все 100 checkpoint hashes и final hash совпали с этим steady
baseline (`simulation-session-owner-index-steady.json`). Это сравнение
100-sample steady с 100-sample steady, не прямой speedup против ранних 10 ticks.

Оставшийся измеренный owner — static segment queries: около 6194 вызовов/tick,
каждый затрагивает порядка 30 удалённых объектов. Cycle2 добавил exact spatial
buckets и cached radius в существующий derived obstacle cache, без смены
collision/navigation правил и без уменьшения Patrol workload. Изолированный
AABB вариант не дал ясного общего ускорения; он не
объявляется performance успехом. Фактический выигрыш и семантика нового
spatial cache проверены отдельно перед Unity/player gates.

Финальная pure проверка navigation зафиксирована в
`session-navigation-comparison.json`: 30 warmup ticks и 100 одинаковых ticks
до Tick 130, все hashes равны во всех шести диагностических прогонах.
Честная no-substage пара до/после: Step mean **19.3366 → 9.4721 ms**, p95
**21.1946 → 10.9084 ms**. Отдельно instrumented static segments —
6.3198 → 1.1104 ms. Вложенные stage timings включительные: складывать их
нельзя; нулевые substage поля в no-substage runs означают выключенное измерение.
Independent integer/decimal circle oracle прошёл **100000 primitive и 30000
full-static/bucket cases**, включая касания, нулевые сегменты, bucket boundaries
и крайние integer radius (`circle-oracle.json`). Ни один из этих pure результатов
не объявляется Unity frame p95 или результатом полной session.

`RenderInstancedCycle1/profile.json` прошёл с 180 actual camera/GUI/focused
frames на сценарий. `CombinedInstancedCycle1/profile.json` измеряет минимум
15 seconds на сценарий, 300 ticks (20 Hz), 0 catchup clamps, все timed frames
подтверждены camera/GUI и focus:

| Marines | Render-only frame p95, ms | Combined frame p95, ms | Combined Step+Observe p95, ms | Combined frames | Первый group-command step, ms |
| --- | --- | --- | --- | --- | --- |
| 200 | 2.4659 | 3.1035 | 2.0145 | 6775 | 29.2589 |
| 400 | 6.5111 | 6.2632 | 3.9772 | 3674 | 16.0326 |
| 800 | 11.8774 | 15.0948 | 11.0396 | 1742 | 34.8743 |

При 800 сохранены 12800 logical render parts (12000 active без flash),
13 unique meshes и две палитры; compact backend имеет 801 Transform и 0 native
MeshRenderer под case root, но реально отправляет geometry через instanced
API. Native draw calls p50 — 57/85/129 для 200/400/800. Нулевое число обычных
MeshRenderer не означает отсутствие моделей.

Render-only interval здесь короткий: 0.385/0.848/1.568 s для 180 frames;
combined — 15.001/15.004/15.004 s. Это разные scopes, их timings не сравниваются
друг с другом как один paired эксперимент. Release native GC и calibrated
System.GC в этом Release unavailable, не measured zero; отдельный финальный
Development allocation run приведён ниже.

**15.0948 ms p95 при 800 укладывается в frame budget 16.67 ms только в этом
synthetic combined fixture.** Настоящая presentation session Cycle2 ниже этот
бюджет не выдержала, поэтому полная игровая цель 60 FPS ещё не объявляется.
Это исторический Cycle1 профиль; свежая финальная legacy Shared/compact пара
сравнивается отдельно в combined scope. Iteration03 измерения остаются историей,
а не новым paired baseline с другой simulation.

## Настоящая session Cycle2 и исправленный механизм

`SessionCycle2/session-profile.json` имеет status passed: измерение завершилось.
Обе фазы имеют **Meets60FpsP95=false**; успешный profiler не означает выполнение
производительной цели. В каждой фазе подтверждены 800 observed/patrolling/
visible marine centers, 0 attacking, 0 catchup clamps и все sample frames
одновременно camera/GUI/focused; unfocused frames нет.

| Selection | Seconds / ticks | Frames | Frame p50 / p95 / p99, ms | HostAdvance p95, ms | Native CPU / GPU p95, ms | Draw calls p50 |
| --- | --- | --- | --- | --- | --- | --- |
| 1 marine | 15.0084 / 300 | 1050 | 8.8866 / 34.9496 / 47.1324 | 23.2238 | 34.3876 / 5.1972 | 358 |
| 802 marines | 15.0264 / 301 | 683 | 17.1089 / 35.4926 / 42.3579 | 18.8457 | 35.3453 / 5.0988 | 2897 |

Вторая selection включает 800 добавленных и двух исходных своих marines.
Native GC в этом Release run unavailable; нулевые поля не являются measured
zero. Профиль выявил main-thread host advance и рост draw calls при массовом
выделении; его нельзя подменять pure probe или synthetic combined результатом.

Cycle3 меняет механизм: фиксированные 20 Hz simulation steps переходят на
worker, presentation получает immutable observed frame без ожидания worker в
горячем GUI пути. BattleHud/TechnologyPanel читают damage/armor, upgrade display
и research availability через cached frame APIs GameHost. Command/save/
diagnostic synchronization остаётся явным контрактом владельца host.

World health bars сохраняют те же условия видимости/selection, screen rects,
HP colors и порядок под header/bottom HUD, но отправляются одной mesh draw
через `HudHealthBarBatch`. Buffer capacity растёт заранее до максимума entities;
Mesh/Material освобождаются владельцем HUD. Repaint pass сохраняет GL matrices
через Push/Pop, shader `ColonyRTS/HudHealthBars` явно включается в build. Иконки,
подписи, progress, hotkeys и input hitboxes не меняются. Selection rings
сохраняют прежние 48-point LineRenderer; их batching не выполнялся.
Compile/tests, включая worker correctness
через девять runner cases, проверены в Unity; actual captures,
tick throughput, native counters и session timings проверены ниже. Source и
контрактные tests сами по себе не являются доказательством pixel parity или FPS.
Обычная игра теперь использует async worker; diagnostic Match borrow явно
дожидается завершения. Build preview CanPlace остаётся синхронным отдельным
путём и не считается закрытым этим Patrol profile.

Первый `StandaloneCycle3/player-smoke.json` остановился на проверке активного
observed Patrol после Issue, до GUI captures; runtime errors отсутствовали.
Smoke boundary исправлен: patrolTick фиксируется после command drain/publication,
затем ожидаются три действительно последующих тика. SetSpeed также дожидается
pending worker перед сменой границы. Дополнительно selection pruning использует
observed-ID HashSet одного immutable observation вместо вложенного Exists и
RemoveAll closure. Повторные 140/140 Unity tests пройдены; новые binary smoke и
session выполнены отдельно. Неуспешный smoke не выдаётся за проверку иконок
или health bars. Последующий `StandaloneFinal/player-smoke.json` прошёл:
**291 frames**, **0 runtime errors**, replay завершился на Tick **623**.
Интегратор просмотрел `worker-hud.png` и `technology-progress.png`: настоящие
generated icons видны в resources/buttons/portraits/tree, clipping и header
overlap не выявлены. Smoke включает 12 checks: обычный loop/Patrol, worker/graph
frames, pause/save/load, replay Tick 0→623 и genuine research fixture
load/cancel/refund/restart/progress. Snapshot/saved/loaded/replay final hash
совпали: `17ce1192c123b386586348bff9ac581d974548ef8874fb7bbf5bb8284169e5be`.

## Финальная Release session — ограниченная цель достигнута

`SessionFinal/session-profile.json`: passed на Unity 6000.3.24f1, Direct3D11,
AMD Ryzen 5 2600 Six-Core Processor / Radeon HD 7800 Series. Это тот же opt-in
800-unit Patrol scope с настоящими host/view/fog/minimap/HUD/input, 1600×900,
4×MSAA и soft shadows, без снижения geometry или количества юнитов. Warmup
5.0044 s дал 547 camera/GUI frames до Tick102.

| Selection | Seconds / ticks | Frames | Frame p50 / p95 / p99, ms | Main-thread Advance p95, ms | Worker compute p95, ms | Native CPU / GPU p95, ms | Draw calls p50 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 marine | 15.0082 / 300 | 1719 | 8.3827 / 11.4067 / 14.6488 | 0.0410 | 27.0552 | 11.3914 / 4.8387 | 357 |
| 802 marines | 15.0085 / 301 | 1295 | 11.3377 / 14.2755 / 17.3361 | 0.0281 | 26.5133 | 14.3404 / 4.9043 | 1298 |

В каждой фазе 800 observed/patrolling/visible centers, 0 attacking, 0 clamps,
0 unfocused; camera/GUI/focus совпадают с каждым sample frame. Worker batches —
300 в каждой фазе, прохождение ticks соответствует 20 Hz; main-thread время
не скрывает worker compute, он приведён отдельно. PendingSimulationFrames
849/693 означает кадры с ещё выполняющимся worker, а не пропавшие simulation
ticks. Worker p95 27.0552/26.5133 ms укладывается в 50 ms tick budget.

Health bars **1/800**, в обоих случаях **1 draw**. После timed phases успешно
проверены all→one→none selection и pause с drain worker. Интегратор просмотрел
`session-selected800.png`: прежняя geometry и genuine UI icons присутствуют.
Это подтверждает bounded Repaint/mesh интеграцию; physical input и субъективное
качество требуют human playtest.

Обе фазы достигли p95 frame budget 16.67 ms. Это не обещание, что каждый кадр
короче бюджета: all-selection p99 **17.3361 ms**, max **22.9922 ms**; single
max **26.9463 ms**. Live AI/combat, исходный terrain, build preview, длинная
сессия и полная SC2 игра в этом fixture не измерялись. Release GC unavailable;
его нулевые поля нельзя читать как allocation result.

## Отдельная Development GC проверка

`SessionDevelopmentFinal/session-profile.json` прошёл на отдельной Development
build, native GC available=true в обеих фазах. Это такой же настоящий Patrol
scope, но timings этого binary не заменяют Release acceptance.

| Selection | Seconds / ticks | Frames | Frame p95, ms | Native GC p50 / p95, bytes/frame |
| --- | --- | --- | --- | --- |
| 1 marine | 15.0003 / 300 | 1546 | 14.2961 | 29397 / 380861 |
| 802 marines | 15.0044 / 301 | 1182 | 16.2222 | 29955 / 380260 |

20 Hz сохранены, 0 clamps/unfocused. GC остаётся ненулевым; это измеренный
allocation budget для следующего owner investigation. Прежний Iteration03
synthetic Development profile имеет другой scope и не образует с этой session
честную before/after пару.

## Финальная парная backend проверка

`CombinedLegacyFinal/profile.json` и `CombinedInstancedFinal/profile.json`
сверены в `verified-comparison.json`: одна финальная Release build, Shared
mesh mode обоих backends, тот же hardware/camera/materials/animation/quality и
геометрия. Combined scope содержит static rendered coverage и независимый
движущийся 20 Hz Match/Observe fixture; это отдельный эксперимент от настоящего
GameHost session выше. Каждая пара имеет минимум 15 s/case, 300 ticks,
0 clamps/unfocused и совпадающие actual camera/GUI/focused sample frames.

| Marines | Legacy → compact frame p95, ms | Step+Observe p95, ms | Sample frames | Native draws p50 | Первый group-command step, ms |
| --- | --- | --- | --- | --- | --- |
| 200 | 6.3835 → 3.2175 | 2.0919 → 2.1780 | 2938 → 6539 | 207 → 57 | 27.6786 → 29.2826 |
| 400 | 14.6296 → 6.2562 | 5.0642 → 3.6523 | 1370 → 3642 | 229 → 85 | 17.0699 → 13.3586 |
| 800 | 29.1968 → 14.2488 | 10.7226 → 9.1734 | 665 → 1778 | 265 → 129 | 38.6707 → 32.0865 |

При 800 суммарная geometry обоих backends — **884800 vertices / 427200
triangles**; Transform count **17601 → 801**. Compact сохраняет 12800 logical
parts, 12000 active без flash и 13 unique meshes. Backend comparison не меняет
simulation rules; tick timing variation не выдаётся за отдельную алгоритмическую
оптимизацию backend. First-command step выделен отдельно от steady p95.

## Финальные rules, provenance и завершение

Actual-rules `smoke-final.json` прошёл **1200 ticks** с работающим AI,
snapshot/restore/accepted-command replay совпали:
`60c597ff1226122835f851fbbeadb63dc7dbb67268e9c08b9d4e7b4f5991de7d`.
`technology-smoke-final.json` прошёл **31149 ticks**, все шесть upgrades,
новый marine и настоящий cost/income/research loop; final/replay hash:
`5c424cbbd0a5c73ceacc96615a5d6e57272d71d6eda125b9c7b88cb19160fcdc`.
Technology fixture AI-off и не доказывает самостоятельную tech-стратегию бота.
Job `b2bf1cd3ab814d979792b3b13284b423` success, **29.948 s**.

Ruleset остаётся `prototype-terran-0.3`; rule hash
`5882f81b13abef703b9350608813e5f160707c3082a3fe96d03ee093adcf5f04`.
`build-final.json`: Release/Development success, job
`35803612f120411581cd3847f8fd120a`, **11.214 s**. `source-manifest.json`
фиксирует **159 inputs / 8 binary hashes**; финальный Runtime SHA256
`dd261e25a4eba92066ca10e8f6fb52b97525cdbd736d5804137ebe4b0cf15a8a`.
Финальный MCP probe: 160 tools, Editor ready, Frontier clean с одним root.
Source frozen; timing jobs выполнялись последовательно одним интегратором.
Git commit не создавался; build/evidence outputs остаются в ignored Builds.

Immediate mesh/matrix contract проверен по Unity 6000.3 primary API:
[DrawMeshNow](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Graphics.DrawMeshNow.html),
[LoadPixelMatrix](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/GL.LoadPixelMatrix.html),
[PushMatrix](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/GL.PushMatrix.html).

## Оставшаяся цель

Следующий пакет проверяет frame tails, allocations и настоящие combat/build
workflows после достижения bounded Patrol p95 цели. Новые owners выбираются
по соответствующим evidence; worker compute не исчезает из бюджета simulation.
Spatial navigation acceleration не исправляет drop-off endpoint/contact P2.
Drop-off routing P2, AI tech strategy, полный Terran roster и точный SC2
game/data/map manifest остаются открыты. Сеть, другие расы, campaigns/Co-op/
Arcade и числовая/визуальная SC2 parity этим пакетом не закрываются.
Продолжение: [next-iterations](next-iterations.md).
