# Итерация 06 и следующие пакеты

Основной план: [implementation-plan](implementation-plan.md). Текущий отчёт:
[итерация 06](iteration-06.md), актуальная проверка — [testing](testing.md).
Очередь не означает выполненные возможности или фоновую работу после сеанса.
Перед новым пакетом сохранять source/binary/evidence manifests предыдущего.
Ruleset остаётся `prototype-terran-0.3`; номер итерации не меняет gameplay version.

Финальный Release итерации 05 прошёл 10 independent combat trials: начальные 400+400,
обычный damage/death, real-time 20 Hz, p95/p99<=16.67 ms. Initial selection1/all400:
p99 ranges11.4723–15.5388/13.4584–15.2076 ms; full800 band p99 максимум 16.1001 ms.
Все Tick60 hashes/counters совпали; survivors760, без invulnerability/HP reset.
Final tests160/160 за 34.42 s, обе builds, Development/attrition/Patrol/
standalone/rules gates пройдены. Native GC остаётся ненулевым: final
345992.22/340326.43 bytes/completed tick. Worst attrition frame 37.7824 ms и
Patrol all-selection p99 17.4149 ms подтверждают оставшиеся rare tails.
169 input/8 binary manifests заморожены до исходников 06, к которым результаты 05
не приписываются. История baseline и Cycles1–3 —
в отчёте, прежний Patrol baseline — [iteration04](iteration-04.md).

**Текущая пользовательская цель — итерация 06:** начальные 800 бойцов реально
двигаются и сражаются при работающем AI; уменьшить редкие FPS просадки.
Итерация 05 использует AI-off checkerboard/idle Engage и этого не доказывает.
Она сохраняется как завершённый по своему bounded scope baseline после финальных
проверок. Новый C0 fixture уже имеет собственный frozen manifest/baseline:
320/323 moved units, реальные AI orders/build,253 deaths/547 survivors.
Release all-selection p99.9 33.4393 ms/max 38.5555 ms и producing Dev GC
3.37/3.43 million bytes/tick показывают открытые tails/allocations. C1 source
исправил proof gates/instrumentation. C1 Release3276 frames без >16.67 ms,
20 Hz; p99.9 12.1406/14.6054 ms, final pause passed,167/167 tests. C1 отдельный
Dev показывает321791.08/330401.87 bytes/tick, без zero-GC claim. Его известная
after-warmup evidence allocation исправляется отдельно от product optimization;
raw samples сохраняются. C2 pure proof сохраняет240 states/Restore/replay/AI;
final173/173 tests и обе builds прошли, source171 inputs/8 binaries frozen.
Расширенная C2 серия завершилась: все10 trials p95/20 Hz passed, p99 only9/10;
all-selection trial4 **17.6281 ms**, series max **24.5411 ms**,0 frames>25 ms.
Это failed attempted-final gate, raw outliers сохранены. C3 измеряет следующий
presentation owner: exact per-prototype animation pose cache с копией matrices
на instance и parity proof. C2 Dev измерен отдельно:330151.01/332945.65
bytes/tick, оба20 Hz/p99 passed, не zero-GC/Release. C3 fresh177/177 tests,
compile0/0 и обе builds success12.142 s; его длинная серия тоже p99 **9/10**:
all trial2 **17.0287 ms**,5 frames>25 ms в серии. Все p95/20 Hz/hash/AI
сохранились. C4 сначала диагностирует native GC/CPU/worker clusters; candidate
pose cache не объявляется causal решением tail. C2/C3 snapshots и raw evidence
сохраняются. C4 producing Dev probes не установили единственную причину;
same-binary Release ABAB default11 против opt-in4 дал mixed/inconclusive result,
4 не принят, default11 и GC3 ms сохранены. C5 проверяет finite-affine
PrepareWorld/bounds math с randomized legacy parity и fallback для остальных
inputs. C5 fresh182 tests/dual build прошли;10 trials p99 passed, но187.8012ms
wall outlier сохранён. C6 private visible staging уменьшил pure Observe bytes
на4.60% без fog-capacity disclosure;184 tests/dual builds и exact240 states/
Restore/replay/AI passed. Два Release players дали20/20 p99/20Hz и32901frames,
но11 кадров>16.67ms,один29.4451ms при800visible; full800 band p99 тожеfailed.
Native Dev остаётся326290.38/322531.90bytes/tick. C7 opt-in main-loop boundaries
проверяются отдельно. C7 ordinary failed9/10 p99,max297.4777ms; C8 diagnostics
дали79.1918ms,max post-EOF71.5987ms,exclusive owner не установлен. C8 ordinary
A/B прошли20/20 p99/20Hz,max21.9345ms,0>25ms. Повторяемый tick97 transition
связан с native creation/removal ранее видимых marines; C9 owner-specific
compact reuse уменьшил actual creations278→14,264reuse,но stall сохранился.
C10 health upload/draw scopes не подтвердили synchronous mesh shrink bottleneck.
Исторические191tests/dual builds и30clean ordinary Release trials/52661frames
прошли p99/20Hz/full800 bands: 9>16.67ms,0>25ms,max21.6823ms; отдельная native
серия сохранила35.5023ms GC-associated outlier. Разбор единственной разрешённой
CPU/GPU ETL-трассы завершён: bounded clock alignment подтверждает main
blocked15.6484ms/on-CPU7.2995ms/ready0.0064ms и graphics worker
blocked14.9310ms. Health-buffer resize реален, причинность не доказана;
данных очереди DxgKrnl нет. Запись остановлена, второй системной записи не было.
C11 potential-population GPU reserve не прошёл performance (p99 лишь5/10,
max201.776ms). C12 actual high-water сохранил20clean боёв/32781frames,
max32.8942ms и2>25ms; отдельные2native-diagnostic trials/3076samples дали
max44.8992ms и4>25ms при стабильной capacity. Эти failures не удаляются.
C13 оставляет actual high-water и повторно использует неизменный index prefix.
Свежие195/195 tests36.01s,compile0/0,dual builds12.375s прошли;
179inputs/8bins заморожены. Три обычных Release players дали30боёв/50545frames,
все p95/p99/20Hz/movement/AI/full800/near720 gates passed,focus полный,
0clamps,прежний hash. Всего6index uploads на50545geometry uploads. Остались
11кадров>16.67ms,0>25/33/50ms,max20.9734ms,longest burst3. Отдельные C13
native Release3423/Development3239samples прошли health/mainloop/native-wait
validators и p99/20Hz,max20.9221/17.6425ms. Patrol p95 9.1982/12.6617ms
и standalone regressions прошли. Bounded gates новой сборки приняты; каждый
кадр<=16.67ms и причинное FPS-улучшение против C10 не заявляются. При следующем
performance пакете продолжать по сохранённым tails и контрактам owner,
сохраняя quality, worker/GC policy и все невыгодные данные.

1. **E1/E2 — итерация 06: moving combat, настоящий AI и frame tails.** Сохранить
   normal host/view/fog/HUD/minimap, исходные damage/death/cost/supply и quality.
   Начать с разнесённых армий и tick-driven player0 script; player1 orders должны
   исходить от работающего bot planner. Показать accepted AI commands по типам,
   не только Enabled=true. Подтвердить actual displacement/count/distance обоих
   initial ID sets, concurrent movement и shots; наличие AttackMove не означает
   движение. Задать march/engagement/overlap windows и пороги до baseline.
   Сохранить реальные bases/workers/resources;400 free fixture soldiers могут
   блокировать Train по supply, поэтому учитывать законные rejects и реальную
   economy/build деятельность без бесплатной production.
   Публиковать live/observed/visible/selected/moving/firing/casualty traces,
   real-time ticks, worker steps/batches/backlog отдельно от render clock.
   Не поддерживать 800 survivors воскрешением. Не заполнять итог natural victory
   пустыми Results frames. Для редких dips сохранять p99.9/max, counts кадров
   >16.67/25/33.33/50 ms и longest consecutive overbudget burst, raw frame/tick/
   population/GC/nativeCPU/GPU correlation. Проверить repeated fixed windows,
   один manifest и exact hashes/AI Restore/replay; не удалять невыгодные outliers.
   Менять только измеренный owner. Среди ещё открытых путей — synchronous
   CanPlace build preview, detached-copy allocations и selection rings
   (прежние 48-point LineRenderer), если новое evidence подтвердит их стоимость.
   Development GC/timings отделять от Release, unavailable не объявлять нулём.

2. **E3 — drop-off contact routing.** Измеренный P2: global endpoint/
   stoppingDistance tie ведёт рабочих к одному углу базы. До правки закрепить
   сценарии нескольких рабочих вокруг footprint, занятых contact points,
   нового blocker, смены базы и исчезнувшего drop-off; отделить unreachable
   от stuck. Исправлять общий goal/contact owner, сохраняя mining/rally/repair,
   deterministic continuation, fog redaction и прежние group navigation checks.
   Exact static-obstacle acceleration в итерации 04 сохраняет прежние правила,
   не исправляет drop-off contact. Прежний terminal connector риск остаётся
   предметом наблюдения, а не автоматически закрытым дефектом.
3. **E3 — технологическая стратегия AI.** Бот пока не строит tech progression
   и не выбирает исследования самостоятельно. Добавить решения через тот же
   ResearchAvailability/команды/observation, без diagnostic hidden world или
   бесплатных upgrades. Выход: воспроизводимый live-AI build/research сценарий,
   разумное ожидание средств/producer, сохранение planner state и совпадение
   новых команд после JSON restore; replay использует принятый command log.
4. **E0 — reference completeness и data manifest.** Заполнить полный
   roster/forms/abilities/upgrades inventory и источники; точные game/data build,
   зависимости и карта/hash SC2 пока неизвестны. Явно отделять experimental
   значения от эталонных. Подтверждённые поля обязательны перед переносом
   конкретного content batch как SC2 baseline; ядро может развиваться отдельно.
5. **E3/E4 — Terran roster и оставшиеся generic combat primitives.** После
   per-batch contracts расширять roster и проверять add-ons, repair/lift/land,
   формы/энергию и специальные abilities по полнотному инвентарю. Сначала
   mechanism/ownership и независимые interaction scenarios, затем content,
   presentation и art states. Engineering Bay/Armory и шесть upgrades не
   означают готовый полный tech tree или числовую parity.
6. **E1 — network spike.** Два локальных процесса с authoritative Match,
   commands и filtered observations/events; versions/ownership validation,
   duplicate/out-of-order и заданная задержка. Проверить saved-state resync либо
   явный отказ. Не передавать скрытый полный мир и diagnostic error reasons.
   Это исследование транспорта, а не готовые lobby/matchmaking/Battle.net.

Перед каждым пакетом сохраняются ownership/scenario contracts и один интегратор
Editor. Не запускать конкурирующие performance jobs. Критический провал даёт
минимальный regression и исправление владельцем механизма. Physical input,
длительный human playtest, качество art, полные расы, сеть/кампании/Co-op/Arcade
остаются отдельными gates; автоматизация не доказывает абсолютное совершенство.
