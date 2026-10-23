# Итерация 05 — массовый бой, frame tails и allocations

Статус: итерация завершена в ограниченном combat scope; финальный Release прошёл 10 trials с real-time 20 Hz и p95/p99
в бюджете. Compile/tests/builds, Development GC, attrition, Patrol и smokes пройдены.
Цель — измерить и ускорить настоящий бой с начальным составом **800 marines,
400 на владельца**, сохранив damage/death/targeting/navigation/AI rules и
настоящие GameHost, BattleView, fog, HUD, minimap и input. Ruleset остаётся
`prototype-terran-0.3`, если отдельное изменение не будет явно зафиксировано.
Полная SC2 parity этим пакетом не заявляется.

Предыдущая [итерация 04](iteration-04.md) достигла Release p95 11.4067/14.2755 ms
в AI-off Patrol fixture. Её native Development GC p95 380861/380260 bytes/frame
ненулевой. Это история и причина нового исследования; Patrol не является
combat baseline и не образует с ним честную before/after пару.

## Сценарии и владельцы до изменений

| Сценарий | Механизм / источник истины | Владелец изменения | Проверка |
| --- | --- | --- | --- |
| 400 friendly + 400 enemy начинают бой | Match.Combat idle Engage, исходные definitions | Root: opt-in combat fixture; Simulation owner: измеренный bottleneck | Точный initial manifest, атаки/урон/смерти, tick hashes |
| Death/target acquisition → видимый кадр | Match.Observation → SimulationFrameRunner → GameHost → BattleView | Root host/read models, presentation owner по evidence | Detached frame, fog redaction, dead-ID pruning, actual camera/GUI frames |
| Worker → HUD без ожидания | rts-host-frames contract, опубликованный observation | Root | Никаких live Match borrows в timed render path; main/worker timings отдельно |
| Allocations во время боя | Producing native GC и отдельная worker диагностика | Измеренный owner; root интегрирует | Release и Development раздельно, единый fixture/trace, ненулевые bytes и availability |
| Независимая проверка и отчёт | Baseline/final evidence, source/binary manifests | QA/docs agent | Контрактные regressions, настоящие smokes, корректные scopes и статусы |

Один интегратор управляет Editor, builds и всеми performance runs. Этот
документ разделяет контракт, историю циклов и фактические финальные результаты.
Root владеет runtime/harness и compact project-memory writeback; этот агент
владеет только отчётом, latest testing section и очередью.

## Реализованный measurement contract перед baseline

Source: `Runtime/Diagnostics/SessionPerformanceProfile.Combat.cs`, общий
`SessionPerformanceProfile.cs`, opt-in GameHost diagnostics/tick fence и
`Simulation/Match.Diagnostics.cs`. Launcher source подтверждает
`-Kind combat`, `-CombatTrials 1..5` (default 5) и `-DevelopmentProfile`.
Результат пишется в `session-profile.json` своего evidence directory.

Fixture удаляет исходные marine spawns и создаёт ровно 400+400 checkerboard
на прежней grid area; original nonmarine spawns/bases остаются, terrain очищен.
Каждый independent trial начинается на Tick 0, paused graphics warmup минимум 2 s
и 90 actual camera/GUI frames; затем обычный host проходит exact Tick 0..60.
Никакого HP reset внутри боя нет. Между trials создаётся новый полный матч.
Selection 1 и all-own 400 проходят отдельные identical-start battles.

Уточнение scope: **AI выключен только в этом fixture**, команды AttackMove и
работа bot planner не создают эту нагрузку. Checkerboard запускает обычное
автономное idle Engage обеих сторон с настоящими HP/damage/death. Это
targeting/firing/death stress, не AI-strategy/chase/navigation benchmark.
Live-AI поведение проверяется отдельными rules smokes; его combat performance
остаётся самостоятельным будущим gate. Это явное отличие от исходного
широкого намерения проверить также AI commands, а не скрытая подмена.

План baseline — один trial на selection (по 60 ticks), final — пять на selection,
то есть 300 simulation ticks/15 simulated seconds, разбитых на пять новых боёв.
Это **не один непрерывный 15-second бой с 800 survivors**. Before/after сравнивать
на одинаковых 60-tick trials; дополнительные final repetitions показывают
разброс, их нельзя молча агрегировать как равную baseline длительность.
Actual wall duration/frames/throughput публикуются отдельно.

После восстановления real-time добавлен отдельный **attrition run**:
один новый бой на selection до Tick240 (12 simulated seconds) для death/VFX/
lifetime tails. Это отдельный scope от 5×60 near-full trials. Ниже 720 survivors
естественная fog потеря записывается, а не исправляется всевидением; near-full
band>=720 всё ещё требует actual coverage. Поздний low-population tail не
улучшает заявленный 800 workload. Launcher поддерживает `-CombatTicks 60|240`;
сценарий имеет bounded watchdog200 s. Source и фактический 240-tick результат
Cycle2 приведены ниже.
Если выбранный marine погиб, selection1 становится 0 естественным образом,
без автоматического retarget ради постоянной тяжести HUD.

Host diagnostic struct содержит накопленные по всем steps shots, health damage
без overkill, deaths (по owner жертвы), live marine counts/HP. Эти derived
totals не входят в gameplay state/hash, начинаются с создания/Restore Match;
их snapshot не объявляется persistent counter continuity. Sampling не читает
live Match; FinalHash снимается после pause/drain вне timing interval.
Review выявил недостаточность initial-only coverage: source теперь проверяет
на каждой worker publication, что observed и unobscured centers равны live
population; сохраняет Coverage trace. PostFirstShotFrames отделён от
NewAttackPublications, first-volley PNG выполняется в отдельном visual run
за пределами timings. Эти source исправления проверены baseline запуском.
Per-publication projections входят в diagnostic instrumentation; одинаковый
overhead должен сохраняться в baseline/final и учитываться при интерпретации.
Population bands у render frames относятся к последнему опубликованному
presentation state. Они не читают ещё изменяющееся in-flight состояние worker.

## Честный initial fixture

Задать один версионированный initial manifest: seed, исходный catalog SHA,
fixture SHA, позиции и IDs обеих групп, карта/terrain, bases/workers/resources,
AI flags, команды и tick boundaries, camera/selection/quality. Не прибавлять
800 к четырём исходным marines и затем называть это ровно 800: исходные marine
spawns должны быть учтены так, чтобы designated состав был 400+400; total entity
counts и вновь произведённые/внешние combat units публикуются отдельно.

Сохранить реальные базы обоих владельцев: `Match.CheckVictory` завершает игру,
если хотя бы одна сторона не имеет IsBase. Сохранить и описать настоящую
AI среду, когда AI входит в измерение: `Match.Bot.Think` сразу возвращает
управление при отсутствии своей base. Flag enabled сам по себе не доказывает
работу planner или принятые
bot commands. Отличать fixture script Attack/AttackMove от команд настоящего
бота и показывать отдельные accepted/rejected counts и причины.

Не менять MaxHealth, Damage, Armor, cooldowns, Sight, AttackRange и правила
столкновений ради удобной нагрузки. Не применять invulnerability, health reset,
воскрешение, незаметное replenishment или подмену стрельбы анимацией. Новая
продукция обычным cost/queue путём допустима только как явно описанная часть
сценария с отдельными counters. Cleared terrain или перестановка bases —
явные fixture отличия, не «обычный полный матч на исходной карте».

Прогреть graphical assets/camera/GUI до начала измеряемого боя, не дать
переменному warmup съесть HP и часть армии. Pause/reset/start boundary и
первое исполнение команд должны иметь воспроизводимый tick. Для сравнения
selection1/all использовать независимый запуск того же initial fixture и
script; последовательная вторая фаза уже с потерями не изолирует цену selection.
Изменения selection должны происходить на описанной границе, а её естественное
уменьшение после смерти юнитов сохраняется.

## Измерение меняющейся нагрузки

**800 — начальный состав, а не постоянно живущие 800 бойцов.** Смерть уменьшает
число моделей, целей, health bars и вычислений. В отчёте рядом с frame timings
обязательны actual duration/ticks и trace состава, чтобы ускорение от потерь
не выглядело ускорением того же workload.

Фактические diagnostics и правила интерпретации:

- Start/End и TickTrace сохраняют live counts/HP по владельцам, cumulative
  shots, damage без overkill и deaths. `LastAttackTick` выживших не используется
  как счётчик shots. PostFirstShotFrames означает кадры после первой атаки,
  NewAttackPublications — публикации с приростом shots, не непрерывный огонь
  всех units в каждом render frame.
- Для каждого опубликованного tick — live counts, observed counts, enemy fog
  coverage, on-screen/unobscured world centers и selected count; raw frames
  имеют published live population. Выход врага из observation не равен смерти.
  Health-bar batch дополнительно проверяется сохранённым Patrol сценарием.
- HUD показывает bars не только selected units: все damaged/incomplete observed
  nonresources также подходят под `BattleHud.DrawWorldLabels`. В бою health-bar
  count не равен selection count; собственная all-army начинается с 400, затем
  уменьшается. Проверять прежний eligibility/culling и один draw для непустой
  batch, а не переносить fixed1/800 assertions Patrol сценария.
- Wall frame p50/p95/p99/max и raw samples с published tick/live count; отдельно
  initial engagement, steady measured interval и естественный конец боя.
  Не выбирать только выгодный surviving tail после просмотра результата.
- Main-thread host scheduling/publication time, worker compute, completed
  batches/**steps**, tick advancement, elapsed time, speed, pending/backlog,
  clamped/discarded time и focus. Быстрый render при отстающей simulation не
  считается real-time успехом. Один batch может содержать несколько ticks.
- Native CPU/GPU/draws/GC со своей availability и units. Development allocations
  измеряются отдельной build; current-thread System.GC не является суммой
  worker+main allocations и не заменяет native GC total.

Authoritative diagnostic aggregates собираются владельцем Match/worker и
публикуются detached, не читаются через `GameHost.Match` на каждом кадре.
Они не подмешиваются в player observation/UI, чтобы не открыть hidden world.
Короткие shots/deaths между двумя worker publications должны учитываться
в накопительных counters всех выполненных steps.

Paired baseline/final должен использовать тот же manifest, command trace,
tick horizon, selection policy и camera/quality/hardware/build type. Сверять
state hashes/боевые counters на одинаковых ticks, а не лишь конечные totals.
Если horizon закончился до требуемого статистического sample, либо бой
завершился раньше, зафиксировать фактический interval и coverage; не продолжать
idle кадры под названием «постоянный бой 800». Правила planned duration, minimum
samples и stop reason фиксируются до первого baseline по actual harness.

## Полезные ограничения старого harness

`SessionPerformanceProfile` итерации 04 намеренно утверждает 800 Patrol, no combat,
AI-off и проверяет counts только на конце фазы. Его нельзя переименовать в
combat profile: он отвергает атаки и требует отсутствия потерь. Новый сценарий
сохраняет проверенные механизмы foreground/camera/GUI/focus, preallocation,
bounded screenshot wait/decode, validated Builds path и watchdog/finally Quit,
но меняет именно workload contract и его инварианты.

Не помещать StateHash/deep snapshot, LINQ traversal всего мира, synchronous
Match borrow, PNG/JSON writing, percentile sorting или GC.Collect в timed
frames. Для диагностического instrumentation указывать стоимость и отличия
от final build; не выдавать pure .NET probes за Unity FPS.

## Цикл разработки и Definition of Done

1. Сохранить actual baseline с complete initial manifest и workload trace.
   Проверить настоящий damage/death, автономный Engage, графическое coverage,
   clock throughput, source/binary identity и валидность counters.
2. Назвать измеренный owner; согласовать bounded change и invariant. Изменить
   механизм у владельца, сохраняя rules, exact targeting/distance/ID tie order,
   observation detachment/fog и single-owner worker lifecycle.
3. Независимый review + минимальные meaningful regressions для причины.
   Unity compile/tests и обе builds ведёт интегратор; повторять только после
   изменений, failure или незакрытого gate.
4. Повторить тот же combat manifest/trace в Release, allocation measurement в
   отдельной Development build. Сверить одинаковые tick hashes/counters и
   presentation coverage, показать before/after и residual p99/max/allocations.
5. Actual-rules/live-AI и technology restore/replay smokes, normal standalone
   UI/save/load/replay, death/selection/health bars cleanup; PNG проверить глазами.
   Зафиксировать source/binary/evidence manifest и актуальные memory contracts.

`passed` означает корректно завершённое измерение. `Meets60FpsP95` — отдельная
проверка p95 <=16.67 ms в указанном workload/interval при real-time 20 Hz;
она не означает каждый кадр, постоянные 800 survivors, весь match или отсутствие
allocations. Итерация завершена только после фактических итоговых evidence и
отчёта об оставшихся gates. Итоговые gates фактически пройдены ниже; moving+AI и rare dips переходят в 06.

## Текущие результаты

### Baseline — валидное измерение, simulation throughput failed

`CombatReleaseBaseline/session-profile.json` и
`CombatDevelopmentBaseline/session-profile.json` завершились с status passed:
это корректно выполненные два 60-tick trials, **не выполненная performance цель**.
В обоих selection modes и обеих builds **RealTime20Hz=false**, хотя render
p95 укладывается в 16.67 ms. Async worker позволяет быстро рисовать почти
неизменный кадр, пока simulation существенно отстаёт.

| Release selection | Frames | Wall seconds для 60 ticks | Frame p50/p95/p99, ms | Worker batch p95, ms | Batches / completed ticks | Native GPU p95, ms |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | 1488 | 12.9203 | 8.5419 / 10.8856 / 11.9888 | 3642.4952 | 6 / 60 | 4.8252 |
| all400 | 1491 | 14.9910 | 9.9268 / 12.0899 / 14.2564 | 4253.5570 | 6 / 60 | 4.8606 |

Worker timing здесь — **время batch из нескольких steps**, не per-tick p95.
Simulation продвигается примерно 4.64/4.00 ticks/s вместо 20. Clamps и unfocused
frames отсутствуют; это само по себе не доказывает real-time throughput.
Coverage trace подтвердил, что все живые marines observed и находятся в
unobscured viewport на каждой publication. Parent-интегратор просмотрел
отдельный first-volley PNG: начальные 800, настоящие shots/muzzles и icons видны.
Coverage validation имеет собственный sub-ms overhead (около 0.4 ms maximum
в baseline), сохраняемый в обеих сравниваемых версиях.

Одинаковый Tick60 outcome обеих builds/selections: shots **1981/1979**,
health damage **11823/11817**, deaths **19/21**, survivors **381/379** (всего 760).
Final hash:
`45b7fcab4e37107919287292d14e09874a6ef919253f440928b9f071056cf0db`.
Это настоящий damage/death, без сохранения 800 бессмертных survivors.

| Отдельный Development baseline selection | Frames | Seconds | Frame p95, ms | Native GC p50/p95, bytes/frame | RealTime20Hz |
| --- | --- | --- | --- | --- | --- |
| 1 | 1376 | 12.9019 | 11.5186 | 8728 / 23204 | false |
| all400 | 1296 | 14.3952 | 13.5842 | 8138 / 25204 | false |

Development native GC available=true, Release unavailable; не заменять его
нулевыми полями Release. Эти низкие frame quantiles получены при stalled
simulation и всего 6 worker publications за 13–15 s. После восстановления 20 Hz
publications становятся чаще; per-frame p95 может вырасти даже при снижении
allocation на tick. Итог требует raw/total allocations, bytes/completed tick
и publication counts либо явного указания этого ограничения. Allocation effect
оценивается по **Cycle1 с восстановленным real-time → Cycle2 allocation fix**;
stalled baseline frame quantiles сохраняются как история, без лишнего rerun.

Cycle1 исправил измеренный Engage/visibility mechanism с proof сохранения
семантики и presentation VFX allocations/pooling. Его фактический результат,
следующий HUD/read-model allocation batch и отдельный final gate приведены ниже.

### Cycle1 — semantic proof и первый Unity feedback

`combat-cycle1-equivalence.json`: pure .NET10.0.11, baseline и optimized
Simulation side-by-side с namespace-only isolation и тем же extracted fixture.
Все **60 последовательных combat tick hashes**, полное state JSON, оба player
observations и diagnostics counters совпали на каждом tick; final hash равен
baseline выше. Дополнительно два AI сценария seeds971/972 по 160 ticks с
JSON Restore/replay и пять custom proof случаев на вариант.

Pure суммарное Step время 60 ticks **5696.9064→989.6325 ms** — диагностика
механизма, не Unity/player frame acceptance и не доказательство 20 Hz.
`editmode-baseline.json` ранее подтвердил 140/140 за 34.53 s. Первый Unity Cycle1
прогон: **158/159 passed**, единственный failure — новый beam test ожидал
точный float color, а native LineRenderer roundtrip даёт Color32 quantization
(0.5→около 0.502,0.9→около 0.902). Исправлен independent native-reference
expectation; production palette не меняется. Новый успешный Unity rerun и
Cycle1 player измерение приведены ниже.

### Cycle1 — real-time восстановлен, allocations измерены отдельно

После исправления oracle реальные Unity tests: **159/159 passed**, **35.34 s**.
`CombatReleaseCycle1/session-profile.json` и отдельный
`CombatDevelopmentCycle1/session-profile.json` passed; обе selections в обеих
builds **RealTime20Hz=true**, 60 batches на 60 completed ticks. Это один
short trial на selection; окончательные пять repetitions ещё впереди.

| Release initial selection | Frames / seconds | Frame p95 / p99 / max, ms | Worker batch p95, ms | Published-full800 frames / p95 / p99, ms |
| --- | --- | --- | --- | --- |
| 1 | 358 / 3.0453 | 10.7241 / 12.7966 / 15.6703 | 46.6867 | 130 / 10.8800 / 12.4485 |
| all400 | 311 / 3.0626 | 12.1477 / 14.1322 / 16.3245 | 47.9706 | 119 / 11.6627 / 14.0231 |

Обе selections: 0 clamps/unfocused; исходные 800 и live coverage сохранены,
Tick60 hash/counters/deaths/survivors совпали с baseline. Трассеры показывались
**3921 раз**, новых native tracer objects во время samples **0**; pool сохраняет
visual workload вместо отключения выстрелов. Worker batch p95 теперь имеет
60 samples, по одному step в batch этого прогона; его нельзя сравнивать как
per-tick p95 с baseline6 multi-step batches без этой оговорки.

| Отдельный Development Cycle1 initial selection | Frame p95 / p99, ms | Native GC p50 / p95, bytes/frame | Total native GC bytes | Bytes / completed tick |
| --- | --- | --- | --- | --- |
| 1 | 11.2125 / 14.2908 | 8742 / 311920 | 23274872 | 387914.53 |
| all400 | 13.2112 / 15.5565 | 26245 / 311366 | 22479264 | 374654.40 |

Raw native GC samples сохранены, available=true. Повышение GC frame p95 против
stalled baseline не называется regression: публикаций теперь 60 за ~3 s вместо 6
за 13–15 s. Это **reference для следующего allocation cycle** при восстановленном
real-time. Cycle2 меняет HUD temporary closures/string formatting и read-model
construction; simulation owner добавляет exact nearest-candidate early reject
до catalog lookup для worker headroom. Их Unity/player эффект приведён ниже.

### Cycle2 — повторения, allocation reference и attrition

`CombatReleaseCycle2/session-profile.json`: **10 trials**, пять независимых
60-tick боёв на selection. Все прошли real-time 20 Hz, frame p95 и p99<=16.67 ms,
0 clamps/unfocused; у каждого исходные 800, survivors760 и прежний final hash.
Диапазоны ниже — min/max **отдельных trial percentiles**, не усреднённый p95.

| Initial selection | Total frames / wall seconds | Frame p95 range, ms | Frame p99 range, ms | Worst frame max, ms | Published-full800 frames | Full800 p99 range, ms |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | 1720 / 15.1402 | 10.7554–12.2228 | 11.7955–14.7142 | 21.8128 | 676 | 11.5916–14.0464 |
| all400 | 1519 / 15.1608 | 12.0742–12.9778 | 13.6944–14.8138 | 25.1168 | 585 | 14.1973–16.4406 |

Worker batch p95 range27.0563–32.5088 ms /27.0314–30.1703 ms; целостность
state/counters подтверждена каждым identical Tick60 hash. Pure
`combat-cycle2-equivalence.json` отдельно сравнил 60 hashes, state/observations/
diagnostics каждого тика, AI/Restore/replay и пять proof cases. Pure Step bytes
на tick **36270.40→35451.87**, вне Observe/Unity; это отдельная малая экономия,
не whole-worker/player allocation total. Unity Cycle2: **159/159**, **34.09 s**.

`CombatDevelopmentCycle2` — один trial на selection, оба real-time. Recorded
native GC bytes/completed tick **360108.03/348199.00**, totals **21606482/20891940**
bytes. Против real-time Cycle1 это примерно 7.17%/7.06% меньше в измеренном
полном run. Render frame counts также изменились 339→304 и 278→235; это whole-run
эффект при другой frame/tick смеси, не изолированное доказательство экономии
одного owner. Development frame p95 **13.3760/19.3209 ms**; вторая selection
не выполнила 16.67 ms, worker batch p95 **30.2048/52.0379 ms**. Эти timings
не подменяются Release значениями.3921 tracers показаны,0 новых objects.

Отдельный `CombatAttritionCycle2`: один 12-second/240-tick бой на selection,
реальный clock сохранён. Frame p95 **9.1138/10.8291 ms**, p99 **10.8638/12.5804**;
frames2441/1929, wall12.0061/12.0086 s,0 clamps/unfocused. К Tick240 погибло
**511** marines (120/391), осталось **289** (280/9), shots3189/2796,
health damage17961/16416. Published observed/visible в конце 289, selected1/280;
5838 tracers показаны,0 созданы в sample. Hash обеих selections:
`ce83969e81d66869aeb3db0ffbde2d8d26e56bf825e8e927f7454daf32f5ef2b`.
Этот surviving tail не используется для улучшения 800 band figures.

### Cycle3 — allocation proof и финальная Release серия

Owner Simulation заменил два phase snapshot `ToArray` на captured-count
index loops при append-only/end-removal invariant, а lambda-based Entity(id)
Find — на прямой loop. Момент включения новых spawns в каждую phase, порядок
исполнения и same-tick targeting/death сохранены. Новый regression проверяет
phase behavior при создании unit.

`combat-cycle3-equivalence.json`: side-by-side frozen Cycle2/current pure
Simulation, **все 240 последовательных tick hashes**, full state JSON, обе
observations и diagnostics counters совпали; AI/Restore/replay и шесть custom
proof cases прошли. Финальный hash240 совпал с attrition Cycle2. Isolated
Step-only bytes/tick **22260.80→13843.33** на одинаковом 240-tick workload;
это примерно 37.81% меньше именно в pure Match.Step, без Observe/Unity.
Step total **863.05→860.49 ms** не подтверждает существенного CPU ускорения.
Числа других 60-tick horizons с ними не сравниваются как одинаковая нагрузка.

Финальные Unity compile **0 errors/warnings**, **160/160 tests**, **34.42 s**,
0 failed/skipped (`compile-final.json`, `editmode-final.json`). Обе final builds
успешны, job `326cb4922dee4c6eb11b59932986bf2f`.

`CombatReleaseFinal/session-profile.json`: пять независимых 60-tick trials для
initial selection1 и пять для all400. Все 10 **RealTime20Hz=true** и p95/p99
<=16.67 ms,0 clamps/unfocused. Каждый trial имеет исходные 800, end760, прежний
Tick60 hash/shot/damage/death counters и validated live observed/viewport coverage.

| Initial selection | Total frames / wall seconds (5 trials) | Frame p95 range, ms | Frame p99 range, ms | Worst frame max, ms | Worker batch p95 range, ms |
| --- | --- | --- | --- | --- | --- |
| 1 | 1779 / 15.1679 | 10.3275–11.5370 | 11.4723–15.5388 | 23.3394 | 26.2014–30.9412 |
| all400 | 1531 / 15.1663 | 11.8812–13.1831 | 13.4584–15.2076 | 26.2861 | 27.9470–29.8991 |

Диапазоны относятся к отдельным trials, не percentile от средних. Published
full800 band содержит 683/583 frames; его p99 ranges **11.9849–13.5816 /
13.5159–16.1001 ms**. Near-full>=720 band содержит 1727/1488 frames, p99 max
**15.5388/15.2076 ms**. Каждые trial показали 3921 tracer, создали 0 новых
tracer objects во время sample. Смерти и VFX сохраняются.

Worst worker batch max **59.3139 ms** и frame max **26.2861 ms** показывают
оставшиеся редкие tails. Этот результат — bounded measured p99 acceptance
с реальной simulation, не обещание каждого кадра или всего игрового match.
Moving combat и работающий AI будут отдельной [следующей итерацией](next-iterations.md);
этот checkerboard fixture остаётся AI-off и не используется как их доказательство.
Final Development, attrition, Patrol, standalone и actual-rules/technology
gates пройдены ниже; source и предыдущие evidence сохранены.

### Финальная Development allocation проверка

`CombatDevelopmentFinal/session-profile.json`, отдельная Development build:
по одному 60-tick trial на selection1/all400, оба real-time 20 Hz,0 clamps/
unfocused, тот же hash/counters760 survivors,3921 displayed tracers и 0 creations.
Native GC available=true, raw samples сохранены.

| Initial selection | Frames / seconds | Frame p95 / p99, ms | GC p50 / p95 / max, bytes/frame | Total recorded GC bytes | Bytes / completed tick |
| --- | --- | --- | --- | --- | --- |
| 1 | 327 / 3.0286 | 12.1784 / 14.6378 | 4248 / 306203 / 756272 | 20759533 | 345992.22 |
| all400 | 273 / 3.0255 | 14.0328 / 16.7197 | 8043 / 306514 / 832865 | 20419586 | 340326.43 |

Dev all-selection p99 **16.7197 ms** не выполнил 16.67 ms; он не подменяется
Release. Recorded bytes/tick против real-time Cycle1 меньше примерно 10.81%/
9.16%, с другой frame/tick смесью (339→327 и 278→273 frames). Это whole-run
measured allocation effect; изолированная Step экономия доказана отдельно pure
probe. Total — сумма записанного allocation counter, не retained heap size.
**Zero-GC не достигнут**; Release unavailable, а большие allocation peaks
остаются задачей следующего исследования.

### Финальные attrition, Patrol и workflows

`CombatAttritionFinal`:240 ticks за **12.0074/12.0113 s**, real-time 20 Hz,
2407/1824 frames,0 clamps/unfocused. Frame p95 **9.5414/11.0797 ms**, p99
**11.2508/13.5614 ms**; max **29.3670/37.7824 ms**.511 real deaths,289 survivors
(280/9), прежние shots3189/2796, damage17961/16416 и hash240
`ce83969e81d66869aeb3db0ffbde2d8d26e56bf825e8e927f7454daf32f5ef2b`.
5838 tracers показаны,0 новых objects в samples. Лёгкий surviving tail
не используется как доказательство 800-unit workload.

`PatrolFinal`: прежние 800 marines, HUD/fog/minimap,20 Hz,0 clamps/unfocused,
1778/1374 frames за 15.0039/15.0002 s. Selection1/802 frame p95
**10.7286/13.4891 ms**, p99 **12.7145/17.4149 ms**; all-selection p99 выше
бюджета. Health bars1/800 по 1 draw, shrink/empty selection и pause/drain
проверены. Это regression Patrol scope, не combat+AI benchmark.

`StandaloneFinal/player-smoke.json`: passed/exit0, **288 frames**, **0 runtime
errors**, все 12 checks, replay до Tick **622**. Pause/save/load/replay hash
`894c34d6e4081b2682598e9374191311c47b03e6c092846883f041078ed0db22` одинаков;
genuine research fixture load/cancel/refund/restart/progress пройден.
Physical input и субъективное качество остаются human playtest gates.

`smoke-final.json`:1200 live-AI ticks, restore/replay hash
`60c597ff1226122835f851fbbeadb63dc7dbb67268e9c08b9d4e7b4f5991de7d`.
`technology-smoke-final.json`:31149 ticks, все 6 upgrades, final/replay hash
`5c424cbbd0a5c73ceacc96615a5d6e57272d71d6eda125b9c7b88cb19160fcdc`.
Его AI-off fixture не доказывает самостоятельную tech-стратегию бота.

### Замороженное происхождение 05 и следующая цель

`source-manifest-final.json`: **169 inputs**; `binary-manifest-final.json`:
**8 binary hashes**. Manifests записаны до новых исходников 06.
`build-final.json`: обе builds success, job `326cb4922dee4c6eb11b59932986bf2f`,
12.172 s. Release Runtime SHA256
`10e115fc2711aacc84f3dc3541271d985194e4e917ce755164764467dc7566ad`.
Эти evidence относятся к замороженному состоянию 05, **не к будущему source06**.
Ruleset prototype-terran-0.3 и gameplay goldens не изменены. Git commit не
создавался; player/evidence payloads остаются в ignored Builds.

Следующий запрос —800 moving combatants, работающий AI и редкие FPS dips.
Checkerboard/idle Engage05 этого не покрывает. Приёмка 06 отдельно докажет
accepted bot decisions, настоящее displacement, movement/firing overlap и
rare-tail metrics. Измеренные residual max 37.7824 ms, Patrol all-selection
p99 17.4149 ms и ненулевые allocations сохраняются как задачи, а не стираются
удачными combat percentiles. Очередь: [next-iterations](next-iterations.md).

| Gate | Статус |
| --- | --- |
| Combat manifest и coverage | Ровно 400+400, real damage/death, source-backed AI-off scope |
| Исходный baseline | Измерен; stalled20 Hz failure сохранён как история |
| Compile/tests и builds | Final160/160/34.42 s, compile0/0, обе builds success |
| Final Release combat |10 trials, real-time 20 Hz, p95/p99 passed, одинаковые hashes/counters |
| Final Development allocations | Producing raw/total/per-tick GC measured; zero-GC не достигнут |
| Attrition и Patrol regression | Final240 ticks и два 15-second Patrol intervals passed |
| Actual-rules/technology/standalone | Все final passed, restore/replay goldens сохранены |
| Provenance freeze |169 source inputs/8 binary hashes до исходников 06 |

Evidence сохранены в ignored `Builds/Evidence/Iteration05/`.
CLI contract сверён с actual launcher source; все runtime runs проводил
интегратор последовательно, без конкурирующих performance jobs.
