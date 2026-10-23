# Проверки первого RTS-среза

Проверки исправлений GI-A01–GI-A08 и новые source/binary manifests описаны в
[отчёте исправлений ГИ](gi-remediation-2026-10-22.md). Его correctness-сборка
отдельна от performance C13–C17; прежние цифры ниже сохраняют исторический scope.
Новый test runner сохраняет/восстанавливает набор сцен и пишет отдельный run в
`Builds/Evidence/Tests/`; исходники фиксируются после compile и сверяются после
тестов. Подробности и команды — в [runbook](../../tools/AGENT_RUNBOOK.md).

## Актуальная проверка C21–C22

C21: 246/246 тестов за 38.1s; C22 Retry1: 246/246 за 35.91s, без
failed/skipped/inconclusive. Оба compile 0/0; сцена Frontier восстановлена
чистой. При повторной проверке ГИ source manifest C22 совпал с текущими
204 файлами; полный binary manifest — с 394 файлами обеих frozen сборок.
Тесты повторно не запускались: эта сверка документации не меняет продукт.

C21 ordinary: 10 боёв, max 23.0099ms; C22 ABAB: 8 боёв,
control/candidate max 22.1452/23.5397ms. Строгая FPS цель не достигнута;
candidateD содержит один unfocused кадр, повторяемый WorldLabels выигрыш
не установлен. Диагностические и ordinary серии учитываются отдельно.
[Результаты и evidence](current-iteration.md),
[повторная проверка ГИ](gi-remediation-2026-10-22.md#проверка-после-c21c22).

## Историческая проверка C13 и предыдущих циклов

[Контракт и источник baseline](iteration-06.md). Сценарий теперь включает
начальные 400+400 в двух формациях, player0 AttackMove и обычного bot1,
240 ticks, реальные movement/damage/death и первоначальные costs/supply.

Историческая сборка C13 прошла ограниченные acceptance gates: **195/195 tests
за36.01s**, compile0/0, обе builds success за12.375s; manifests179inputs/8bins
заморожены. Три обычных Release players: **30 clean battles/50545frames**,
все p95/p99/20Hz/movement/AI/full800/near720 gates passed, полный focus,
0clamps и прежний hash. P99 selection1 **9.3438–12.4454ms**,
all **10.8571–14.0427ms**; full800 p99 максимум **16.2196/14.6846ms**,
near720 **15.6884/15.2885ms**. Остались **11 кадров>16.67ms**,0>25/33/50ms,
max **20.9734ms**, longest burst3. Это не обещание каждого кадра<=16.67ms
и не доказательство причинного FPS-улучшения относительно C10. C13 сохраняет
actual high-water buffers и подготовленный index prefix: **6 index uploads
на50545 geometry uploads**,50539 повторных index uploads не потребовались.

Отдельные native Release/Development C13: **3423/3239samples**, health/mainloop/
native-wait proofs passed без ошибок alignment; оба p99/20Hz/full800/near720
passed, полный focus,0clamps, прежний hash. Max **20.9221/17.6425ms**,
соответственно1/4кадра>16.67ms,0>25ms. Development GC:
**325925.01/320847.21bytes/completed tick**, totals78222002/77003330bytes,
GCmax340629/378841bytes/frame, collections52/41 по каждому generation.
Release allocation recorder unavailable: записанные numeric0 не означают zero-GC.
Все пять wait handles доступны в Development; в Release DXGI/TimeUpdate
недоступны. Худший Release frame890/tick97:20.9221ms, из них18.1902ms
доEOF, Gfx wait marker12.1227ms; steady health geometry, upload0.0167ms,
draw0.0220ms. Это отдельные callback/marker boundaries, не exclusive attribution.
Patrol p95 **9.1982/12.6617ms** прошёл бюджет; standalone regressions завершены.

История невыгодных кандидатов сохранена: C11 failed (p99 лишь5/10,
max201.776ms); C12 clean **20боёв/32781frames**, max32.8942ms,2кадра>25ms.
Отдельная native-diagnostic C12-серия:2trials/3076valid samples,
max44.8992ms,4кадра>25ms при стабильной capacity. Эти данные не заменены
более удачными C13 сериями.

Исторический C10: **191/191 tests**, compile0/0, обе builds success;
три обычных Release players дали **30 clean battles/52661frames**, все p99/
20Hz/full800/near720 bands passed; 9 кадров>16.67ms, 0>25ms,
max **21.6823ms**. P99 selection1 **8.6807–12.0176ms**,
all **10.3343–10.8931ms**; полный focus,0clamps,прежние hash/AI/death/quality.
C9 reuse снизил first-trial true creations278→14,264reuse,
но recurring Gfx wait сохранился. Разбор единственной разрешённой CPU/GPU
трассы завершён с ограниченным интервалом сопоставления часов: в целевом
интервале main blocked15.6484ms, on-CPU7.2995ms, ready0.0064ms;
graphics worker blocked14.9310ms. Изменение native health buffers реально,
но причинность не доказана; данных очереди DxgKrnl нет. Запись остановлена,
второй системной записи не было. C10 Development GC остаётся
322949.50/321540.70bytes/tick; отдельный native Release сохранил35.5023ms
GC-associated outlier. Все прежние failures и подробности — в отчёте.

Свежие C5/C6: **182/184 Unity tests** соответственно, обе конфигурации builds
success. C6 paired pure proof сохраняет240 states/observations/AI/Restore/replay;
allocations двух Observe/tick **488880.4→466385.1 bytes** (−4.60%), без CPU
speedup claim. Два fresh C6 Release players: **20 battles/32901 frames**, все
p95/p99/20Hz passed, p99 **9.2709–12.7866ms**,11 frames>16.67ms,1>25ms,
max **29.4451ms**. Этот кадр при800 visible даёт full800-band p99 **29.4451ms**;
не устранённый rare tail сохраняется в raw evidence. C5A187.8012ms outlier также
сохранён отдельно. C6 Dev native GC **326290.38/322531.90 bytes/tick**,
не zero-GC и не Release timing. C7 добавляет opt-in main-loop boundaries;
полный fresh suite **184/184** прошёл36.59s, graphical evidence приведено ниже.
Подробности, manifests и ограничения — в [iteration06](iteration-06.md).

C7 diagnostic10 trials прошли, но следующий normal flag-off player дал
**9/10 p99**, all trial5 p99 **21.037ms**, max всей серии **297.4777ms**.
Это failed gate, сохранённый отдельно от C6. C8 compile0/0,184/184 tests38.33s
и обе builds14.426s success. Release native-wait10trials: оба validators17012
samples passed, p99/20Hz10/10, но max **79.1918ms**. Из него71.5987ms
междуEOF/раннимUpdate с малой main-thread cycle delta; точный engine/driver/OS
owner не установлен. Dev4trials/6309samples подтвердил все пять recorder handles,
max17.7229ms, validators passed. Дополнительные Dev10trials/16165samples
прошли validators/p99/20Hz,max18.4544ms. Обычные C8 Release A/B дали20/20
p99/20Hz,34678frames,9>16.67ms,0>25ms,max21.9345ms. Gameplay/technology,
standalone research/save/load/replay и Patrol regressions C8 passed.
C9 повторно использует наблюдавшиеся compact marine visuals: fresh191/191
tests и builds прошли, снижение true creations подтверждено; оставшийся stall
исследован в C10. Исторический C13 прошёл bounded gates выше; полного отсутствия
редких долгих кадров не заявляется.

- C0 Release real-time 20 Hz, initial selection1/all400 frame p95
  **10.2267/11.9847 ms**, p99 **12.6269/15.4546 ms**, но p99.9
  **19.2865/33.4393 ms**, max **21.2013/38.5555 ms**. Over16.67 frames7/10,
  over33.33 frames0/2, longest bursts1/3; редкие dips ещё не устранены.
- Настоящие 253 deaths/547 survivors;320/323 moved unique units, cumulative
  path3009.033/2683.473 m; bot12 thinks,5 accepted AttackMove,395 unique
  commanded IDs и 1 Build. Hash selections/builds одинаков:
  `57aa79fa7f18f8e3f46f42c40386b51ca3891b0b2d9d7cc83d6ab56fdb544a46`.
  Это initial800 и существенная движущаяся часть, не все 800 moving одновременно.
- Отдельный Dev C0 native GC **3373522.48/3426020.52 bytes/completed tick**.
  C0 final pause gate **false** при retained valid timings: ошибочный untimed
  volley tail исправлен в source; mislabeled PNG не доказывает first volley.
- C0 baseline tests163/163 за 36.61 s, build f9ff598788b44e7c9b4ad24812803ae1
  success15.566 s. C1 source fixes coverage-aware bands, final pauseRequire,
  unique AI IDs/moving-shot checks и diagnostic preallocation; подтверждённые
  C1 rerun/proof/results приведены ниже.
- C1: compile0/0, **167/167 tests за35.83 s**, обе builds passed.
  Pure240 full states/observations/counters и liveAI120 Restore/replay равны;
  Step-only bytes/tick **3136951.73→26555.83**, не whole Unity GC.
- `MovingAiReleaseCycle1`: **3276 frames** суммарно за два независимых12-second
  trials; все<=16.67 ms,20 Hz,0 clamps/unfocused. Selection1/all400 p99
  **10.1504/12.2958 ms**, p99.9 **12.1406/14.6054**, max **16.6418/16.4806 ms**.
  Final pause=true, state/253 deaths/547 survivors/movement/AI сохранились;
  185 moving-attack publications,395 unique bot IDs,0 matrix growth/tracer creation.
  Full800 band61/52 frames: p99 **11.4012/14.6054 ms**; короткий band не
  превращается в steady800. Worker batch p95 **43.7248/50.9333**, max
  **154.1194/116.2982 ms**, не per-tick. Один удачный run не гарантирует
  отсутствие будущих spikes; более длинная C2 серия приведена ниже.
- C1 отдельный Development: native GC totals **77229858/79296448 bytes**,
  **321791.08/330401.87 bytes/completed tick**, collection counts60/56;
  frame p99 **15.319/14.236 ms**, max **27.756/20.259 ms**. Whole-run снижение
  около90.4% относительно C0 включает известную after-warmup FrameTrace
  allocation около1.44 MB. C2 исправляет границу preallocation и захват
  GC.CollectionCount до postprocessing; этот measurement fix отделяется от
  pure product proof, raw C0/C1 samples не меняются.
- C2 source frozen: pure paired240 states/observations/counters и Restore/
  replay/две AI variants совпали;4810 independent goal-axis oracle cases passed.
  Step CPU **4768.30→2509.30 ms**, bytes/tick **28188.47→26865.27**, с initial
  lazy caches, без Observe/Unity. CombatTrials backing перенесён до warmup,
  Coverage стал value type; это отдельные instrumentation corrections.
  Final C2 compile **0 errors/warnings**, **173/173 Unity tests за36.43 s**,
  обе builds success13.172 s, job `971ff4e01126412dae9620a9710cc14e`.
  Frozen attempted-final C2 snapshot171 inputs/8 binaries.
- `MovingAiReleaseFinal` — attempted-final C2 evidence, не успешный финал:
  все10 trials p95/20 Hz passed, но all-selection trial4 p99 **17.6281 ms**,
  общий p99 gate **failed,9/10 pass**. Series max **24.5411 ms**,0 frames>25 ms.
  Некоторые full800 bands также выше16.67 ms; невыгодные raw samples сохранены.
  Legitimate fresh-Tick1 volley800 и final pause passed.
- C2 отдельный Development: оба20 Hz/p99 passed, native GC totals
  **79236242/79906957 bytes**, **330151.01/332945.65 bytes/completed tick**,
  max **352309/472681 bytes/frame** с clean warmup boundary. Dev frame max
  **28.4955/17.5708 ms** не относится к Release. Исторические C2 manifests
  сохранены под suffix-cycle2; attempted-final raw folder не перезаписывается.
- C3 exact per-prototype pose cache уже в source; read-only alias/time/lifecycle
  review нового дефекта не нашёл. Per-instance value copies/cessation guards
  сохраняются; fresh Unity **177/177 passed за34.16 s**, compile0/0.
  PrepareMs/DrawMs разделяют SubmitMs; cache вычисляется в View Animate.
  Первый CLI HTTP400 совпал с успешным domain reload, затем up-to-date0/0;
  full fresh suite подтвердил новые assemblies, без restart/package change.
  C3 dual build job `3f4b34393d474235a09effb759a4d39d` success12.142 s,
  manifests171 inputs/8 binaries; C2 provenance сохранён отдельно.
- `MovingAiReleaseCycle3Final` также failed attempted-final: p99 **9/10**,
  all-selection trial2 **17.0287 ms** выше бюджета. Все10 p95/20 Hz/hash/AI
  сохранились. All trial4 p99 **16.0384 ms**, max **29.4379 ms**; в серии
  **5 frames>25 ms**. Raw evidence и C3 snapshot сохраняются, outliers не
  удаляются. View mean около0.7 vs0.9 ms — умеренное наблюдение; отсутствие
  CollectionCount delta не исключает incremental GC. C4 сначала сопоставляет
  native GC и CPU/worker clusters, causal fix ещё не выбран. Финальные
  Development/regressions/success iteration06 pending.
- C4 `GcCpuDevelopmentDiagnosis`:4 Dev trials, producing CPU/GC counters;
  incremental GC enabled/default3 ms, GCCollect max **2.38–3.10 ms**,
  Incremental max **3.53 ms**. Worst frame **26.37 ms** имел GCCollect0 и
  Incremental **0.0012 ms**; coarse/all-thread ProcessCPU109.375 ms и
  ThreadCycles53.9M не доказывают OS cause. Native calls calibrated positive,
  ProcessCPU granularity около15.625 ms; GC discovery сохранён отдельно.
- Diagnostic-only C4 Release build success **7.804 s**, compile0/0, job
  `a2c189db43b449e8bfd2513d40c7cfce`; новый full suite не запускался, последняя
  gameplay suite C3 **177/177**. Same-binary ABAB default11 actual Unity workers
  и opt-in4 завершён:4 fresh players×2 battles, mixed/inconclusive.
  DefaultA maxima **28.22/13.36 ms**, defaultC **17.86/27.19**;
  4B **19.95/19.34**,4D **19.31/25.07 ms**. 4D all p99 **16.1431 ms** хуже
  defaultC **15.1513 ms**; hash/quality/20 Hz сохранены. `job-worker-paired.json`
  и четыре raw folders retained. **4 workers not adopted**, default11 и GC3 ms
  unchanged. C4 snapshot frozen; C5 affine preparation/bounds math требует
  parity, fresh suite/build/long series. Final iteration06 success pending.

Evidence: ignored `Builds/Evidence/Iteration06/MovingAiReleaseBaseline` и
`MovingAiDevelopmentBaseline`; C0 Runtime70files frozen. Результаты 05 ниже
относятся только к её собственному frozen snapshot, не к новым исходникам 06.

## Итерация 05 — combat p95/p99 и real-time достигнуты в bounded scope

[Итерация 05](iteration-05.md) завершена: начальные 400+400 marines, настоящий
GameHost/view/fog/HUD/minimap, damage/death, автономный idle Engage, fixture
AI-off. Финальный Release прошёл 10 trials с 20 Hz и p95/p99<=16.67 ms.
Moving combat и работающий bot planner переходят в 06; этот результат их не
доказывает. Подробная история stalled baseline и Cycles1–3 сохранена в отчёте.

- Compile: **0 errors/warnings**. Final EditMode **160/160 passed**, **34.42 s**,
  0 failed/skipped. Обе final Windows builds success, job
  `326cb4922dee4c6eb11b59932986bf2f`,12.172 s.
- Release:5 independent60-tick battles на selection1 и 5 на all400. По отдельным
  trials p95 ranges **10.3275–11.5370 /11.8812–13.1831 ms**, p99
  **11.4723–15.5388 /13.4584–15.2076 ms**. Все 10 real-time 20 Hz,0 clamps/
  unfocused,1779/1531 actual camera/GUI/focused frames за 15.1679/15.1663 s.
  Published-full800 band683/583 frames, p99 максимум **13.5816/16.1001 ms**.
- Каждый short battle:800→760 survivors, shots **1981/1979**, damage
  **11823/11817**, deaths **19/21**, coverage validated. Hash во всех selections/
  trials/builds одинаков:
  `45b7fcab4e37107919287292d14e09874a6ef919253f440928b9f071056cf0db`.
  По 3921 displayed tracers,0 новых native tracer objects в samples; качество и
  реальные выстрелы сохраняются. 800 — initial состав, не immortal steady army.
- Final отдельная Development build: GC available=true, raw samples/total/
  bytes-per-completed-tick сохранены. Selection1/all400: GC p95
  **306203/306514 bytes/frame**, max **756272/832865**, totals
  **20759533/20419586 bytes**, **345992.22/340326.43 bytes/completed tick**.
  Frame p95 **12.1784/14.0328**, p99 **14.6378/16.7197 ms**; Dev all-selection
  p99 выше 16.67 ms, его нельзя подменять Release. Zero-GC не достигнут.
- Allocation comparison: Final whole-run bytes/tick примерно 10.81%/9.16% ниже
  real-time Cycle1 с другой frame/tick смесью. Stalled initial baseline не
  используется для причинного сравнения GC percentiles. Pure isolated C2→C3
  Step bytes/tick **22260.80→13843.33** на одинаковых 240 ticks исключают Observe/
  Unity; CPU total863.05→860.49 ms не объявляется существенным speedup.
- Pure proof: все 240 последовательных full-state JSON/observation/diagnostic/
  hash checkpoints C2/C3 равны, live AI/Restore/replay и 6 custom cases прошли.
  Ранее Cycles1/2 сохранили все 60 combat states и независимые AI scenarios.
- Final attrition:240 ticks за **12.0074/12.0113 s**,20 Hz,0 clamps/unfocused,
  **511 deaths**, **289 survivors** (280/9), shots3189/2796, damage17961/16416.
  Frame p95 **9.5414/11.0797**, p99 **11.2508/13.5614 ms**;5838 displayed
  tracers,0 creations. Hash:
  `ce83969e81d66869aeb3db0ffbde2d8d26e56bf825e8e927f7454daf32f5ef2b`.
  Surviving tail не улучшает 800-band acceptance.
- Final Patrol regression:800 Patrol centers,20 Hz,0 clamps/unfocused;
  p95 **10.7286/13.4891**, p99 **12.7145/17.4149 ms**. Health bars1/800 по 1 draw,
  shrink/empty/pause-drain проверены. All-selection p99 остаётся выше бюджета.
- StandaloneFinal: **passed/exit0**, **288 frames**, **0 runtime errors**,
  все 12 checks, replay до Tick **622**, genuine research fixture workflow passed.
  Save/load/replay hash одинаков:
  `894c34d6e4081b2682598e9374191311c47b03e6c092846883f041078ed0db22`.
- Final actual-rules live-AI smoke:1200 ticks, restore/replay hash
  `60c597ff1226122835f851fbbeadb63dc7dbb67268e9c08b9d4e7b4f5991de7d`.
  Technology:31149 ticks,6 upgrades; final/replay
  `5c424cbbd0a5c73ceacc96615a5d6e57272d71d6eda125b9c7b88cb19160fcdc`.
- Scope limits: worst short-combat max **26.2861 ms**, attrition max
  **37.7824 ms**. Каждый кадр в бюджете и отсутствие редких dips не заявлены.
  Moving+AI, rare-tail diagnostics, physical input и subjective playtest —
  отдельные gates следующего пакета; полная SC2 parity не заявляется.

Frozen evidence: `Builds/Evidence/Iteration05/`, `source-manifest-final.json`
(**169 inputs**) и `binary-manifest-final.json` (**8 binaries**) созданы **до
новых source изменений 06**. Они доказывают snapshot05, не будущий source06.
Ruleset остаётся `prototype-terran-0.3`; Git commit не создавался. Канонические
серии: `CombatReleaseFinal`, `CombatDevelopmentFinal`, `CombatAttritionFinal`,
`PatrolFinal`, `StandaloneFinal`; final compile/tests/build/rules JSON и pure
proofs лежат рядом. Все runtime runs выполнял один интегратор последовательно.

## Итерация 04 — bounded Patrol p95 цель достигнута, ruleset prototype-terran-0.3

Итерация завершена в своём ограниченном объёме: exact visibility/copy,
compact marine rendering, worker-owned simulation, batch world health bars
и generated UI icons. Настоящая SessionFinal достигла p95 бюджета 16.67 ms
при 800 видимых Patrol marines и 20 Hz simulation. Полный отчёт с историей
failed runs, механизмами и ограничениями: [iteration04](iteration-04.md).
Ниже сохранены результаты итерации 03 и более ранняя история.

- Финальная Unity compile: **0 errors/warnings**. EditMode **140/140 passed**,
  **33.97 s**, 0 failed/skipped. Включены compact geometry/pose/lifecycle,
  fog/cache, настоящий imported atlas, combat targeting и девять runner cases.
  PNG импортирован точно 1536×1024, readonly/no mipmaps; NPOT failure исправлен.
- Обе финальные Windows builds passed: job `35803612f120411581cd3847f8fd120a`,
  11.214 s. `source-manifest.json`: 159 inputs/8 binary hashes. Ruleset и rule
  hash `5882f81b13abef703b9350608813e5f160707c3082a3fe96d03ee093adcf5f04`
  сохранены. Git commit не создавался; outputs находятся в ignored Builds.
- **Release SessionFinal:** selection1 — 1719 frames/300 ticks за 15.0082 s,
  frame p95 **11.4067 ms**; selection802 — 1295 frames/301 ticks за 15.0085 s,
  p95 **14.2755 ms**. Обе фазы Meets60FpsP95=true, все 800 observed/visible/
  patrol, 0 attacking/clamps/unfocused; camera/GUI/focus для каждого frame.
  Worker compute p95 **27.0552/26.5133 ms** и main-thread Advance p95
  **0.0410/0.0281 ms** измерены отдельно; simulation выдержала 20 Hz.
- Health bars **1/800**, каждый pass **1 draw**. All→1→0 selection и pause/
  worker-drain validation passed. Интегратор просмотрел selected800 PNG:
  прежние geometry и genuine icons присутствуют. Rings остаются прежними
  48-point LineRenderer. Release native GC unavailable, нули невалидны.
- Это **bounded Patrol p95 acceptance**, с добавленными 800 marines, очищенным
  terrain и AI-off только в opt-in fixture. All-selection p99 **17.3361 ms**,
  max22.9922 ms; каждый кадр короче 16.67 ms не обещается. Live combat/AI,
  исходная карта, build preview, длинная сессия и physical input требуют
  собственных проверок. Автоматизация не доказывает субъективное совершенство.
- **Отдельная Development session:** native GC available=true. Selection1:
  1546 frames, frame p95 **14.2961 ms**, GC p50/p95 **29397/380861 bytes/frame**.
  Selection802: 1182 frames, p95 **16.2222 ms**, GC **29955/380260 bytes/frame**.
  По 15 s, 20 Hz, 0 clamps/unfocused. Allocations ненулевые; timings отдельной
  Development build не заменяют Release acceptance и не сравниваются напрямую
  с synthetic Development scope итерации 03.
- **Final backend pair:** одна Release build, Shared legacy/compact, одинаковые
  hardware/geometry/materials/animation/camera/quality, минимум 15 s/case.
  Frame p95 legacy→compact для 200/400/800: **6.3835→3.2175 / 14.6296→6.2562 /
  29.1968→14.2488 ms**; native draws p50 **207→57 / 229→85 / 265→129**.
  По 300 ticks, 0 clamps/unfocused, actual camera/GUI/focus. Это synthetic
  combined render + независимый 20 Hz Match/Observe; отдельно от product session.
- **StandaloneFinal:** passed/exit0, **291 frames**, **0 runtime errors**,
  все 12 checks. Worker HUD и technology-progress captures просмотрены:
  generated icons видны, clipping/header overlap не обнаружены. Product Patrol,
  pause/save/load, replay Tick 0→**623**, genuine research load/cancel/refund/
  restart/progress пройдены; snapshot/saved/loaded/replay hash совпали:
  `17ce1192c123b386586348bff9ac581d974548ef8874fb7bbf5bb8284169e5be`.
- **Final actual-rules smoke:** 1200 ticks с live AI, restore/replay hash
  `60c597ff1226122835f851fbbeadb63dc7dbb67268e9c08b9d4e7b4f5991de7d`.
  Technology smoke: 31149 ticks, все 6 upgrades и новый marine; final/replay
  `5c424cbbd0a5c73ceacc96615a5d6e57272d71d6eda125b9c7b88cb19160fcdc`.
  Его AI-off fixture не доказывает самостоятельную tech-стратегию бота.
- **Pure .NET diagnostics:** 601 ticks×оба игрока old raster/deep-copy/fallback
  equivalence;100 steady Patrol hashes равны в 6 probes; circle oracle 100000
  primitive + 30000 full-static cases. No-substage Step mean 19.3366→9.4721 ms,
  p95 21.1946→10.9084 ms. Это pure доказательства семантики/причины, не Unity
  FPS/native GC; подробные baselines и failed cycles сохранены в отчёте.

Финальные evidence в `Builds/Evidence/Iteration04/`: `compile-final.json`,
`editmode-final.json`, `build-final.json`, `source-manifest.json`,
`verified-comparison.json`, `CombinedLegacyFinal/profile.json`,
`CombinedInstancedFinal/profile.json`, `SessionFinal/session-profile.json`,
`SessionDevelopmentFinal/session-profile.json`, `StandaloneFinal/player-smoke.json`,
`smoke-final.json`, `technology-smoke-final.json`. Исторические Cycle1/2 и первый
StandaloneCycle3 failure не подменяют эти результаты. Полная SC2 parity, другие
расы, сеть и campaigns этим пакетом не заявляются.

## Итерация 03 — проверка 21.10.2026 (история)

Итерация 03 завершена в своём экспериментальном объёме; compile, contracts,
actual-rules smoke, builds, paired profiles и graphical standalone пройдены. Полный
отчёт и оставшиеся gates: [iteration03](iteration-03.md). Ниже этой секции
сохранена история прежних итераций; их numbers не являются новым результатом.

- Unity compile: **0 errors/warnings**. EditMode: **104/104 passed**, 0 failed,
  0 skipped, **16.2 s**; включая 33 research и 14 mesh/cache lifecycle случаев.
- Actual-catalog technology smoke: **passed**, **31 149 ticks**, все шесть
  infantry upgrades, настоящий доход/стройка и новый marine. AI выключен;
  самостоятельная tech-стратегия бота этим smoke не проверяется.
- JSON snapshot Tick **11 821**, продолжение и accepted-command replay совпали:
  `5c424cbbd0a5c73ceacc96615a5d6e57272d71d6eda125b9c7b88cb19160fcdc`.
  Живой AI после restore отдельно покрыт контрактным тестом.
- Обычный actual-rules smoke с работающим AI: **1200 ticks**, snapshot Tick 600;
  original/restore/replay hash одинаков:
  `60c597ff1226122835f851fbbeadb63dc7dbb67268e9c08b9d4e7b4f5991de7d`.
- Typed observation сравнен с old reference и fallback на **601 последовательном
  тике для обоих игроков**, включая все поля DTO и final JSON. Это отдельный
  retained pure probe с SHA привязкой; он не увеличивает счётчик Unity tests.
- Release/Development builds пройдены; их runtime acceptance проверяется
  отдельно. `CombinedOptimizedVerified`: frame p95 **12.5970 / 26.9207 /
  51.2135 ms** и Step+Observe p95 **8.4086 / 18.2165 / 32.4235 ms** для
  200/400/800. Везде по 180 actual camera/GUI/focused frames, 0 catchup clamps.
- Все четыре Release paired profiles passed. Render-only frame p95 baseline
  **19.7907 / 42.0043 / 72.2643 ms** → optimized **5.5008 / 12.7139 /
  24.7756 ms**. При 800 shared geometry: 12800 → 13 unique meshes,
  draw calls p50 36102 → 265, без уменьшения видимой модели/качества.
- **60 FPS на 400/800 combined не выполнены.** Combined — контролируемый
  fixture, не полный игровой match. Полные таблицы, native GPU и первый
  group-command spike приведены в отчёте.
- Старые постоянные нули System.GC allocations невалидны: calibration выявляет
  непроизводящий runtime API. Release counters без samples — unavailable;
  отдельный native Development GC measurement passed: p95 **290952 / 496288 /
  915776 bytes/frame** для 200/400/800. Это не нулевые allocations и не Release
  измерение; поиск allocation owner остаётся задачей следующей итерации.
- Финальный graphical player smoke: **passed/exit0**, **283 frames**, 0 runtime
  errors; Patrol, HUD, F2 graph, pause/save/load, replay Tick 0 → **618**.
  Дополнительно genuine research fixture прошёл product load/cancel/refund/
  restart и producer/graph progress captures. Все 11 checks пройдены.
  Physical keyboard/mouse и субъективное качество остаются отдельным playtest gate.

Evidence: `Builds/Evidence/Iteration03/final-compile.json`,
`final-editmode-results.json`, `smoke.json`, `technology-smoke.json`,
`observation-equivalence.json`, `build-results.json`, `source-manifest.json`,
`verified-comparison.json` и финальные
`RenderBaselineVerified`, `RenderOptimizedVerified`, `CombinedBaselineVerified`,
`CombinedOptimizedVerified`, `DevelopmentAllocationsVerified`,
`StandaloneVerified`. Прежние `*Final` не используются для
новых итоговых цифр. Полные расы, network и точная SC2 parity не заявляются.

## Актуальный прогон 21.10.2026 — prototype-terran-0.2

- Unity compile: 0 errors/warnings; EditMode **56/56 passed**, 14.2s.
- Actual-rules smoke: 1200 ticks, живой AI после restore, replay и исходный
  gameplay hash совпадают. Final hash:
  `57681d3d14b84d4ced55f1e23dd642918fd52ca6c76a65b85a8f8c366680bee4`.
- Synthetic Editor navigation: 200/400/800 movers, 600 и 1800 ticks. К 1800
  прибыли все 200/200, 400/400 и 800/800; not-arrived/stuck0; sampled geometry violations0.
  Точные p95/max и ограничения — [iteration02](iteration-02.md).
- Windows BuildReport: success. Graphical standalone: **passed/exit0**, 245 frames,
  0 runtime errors, настоящие HUD PNG1600×900 для армии и КСМ. Patrol активен до
  save; JSON load и viewer replay до тика 616 дают одинаковый hash.
- Evidence: `Builds/Evidence/editmode-results.json`, `smoke.json`,
  `navigation-benchmark*.json`, `Iteration02/Standalone/player-smoke.json`,
  `gameplay.png`, `worker-hud.png`, `Iteration02/source-manifest.json`.

Новые tests: Patrol roundtrip/queue/Stop/combat; Repair costs/caps/funds,
ownership/self-target/death и save/replay; resource rally; group16 geometry;
route redaction; active avoidance JSON continuation; rounded corners,
occupied/coarse waypoints и точный endpoint возле круглого здания с Shift-Move.
Их ожидания не ослаблялись после failed runs. Независимый review не подтвердил
P0/P1; planner terminal connector остаётся P2, покрытым проверкой recovery.

Обычный UI smoke повторён через Computer Use: запуск окна и UIA доступны,
но screenshot возвращает FrameArrived/window-capture timeout, а координатное
действие — coordinate input geometry is unavailable. Игровые controls в UIA
не представлены; слепые нажатия не использовались. Тестовое окно закрыто Alt+F4.
Это НЕ доказательство gameplay keyboard/mouse input; проверка остаётся открытой.
Встроенный ScreenCapture standalone работает и отдельно проверен.

800-unit tick p95 и spikes массового приказа ещё выше желаемого бюджета;
Editor timing не подтверждает 60 FPS player. Следующий пакет —
[standalone frame/render profile](next-iterations.md), затем tech/research.
Ни полные расы, ни сеть/кампании этим прогоном не закрываются.

## Исходный контракт и первая сборка

`Assets/Game/Tests/EditMode/ColonyRTS.Tests.asmdef` содержит независимые NUnit
контрактные тесты публичного `Match` API. Владельцем правил, валидации команд,
транзакций ресурсов, обзора и persistence остаётся Simulation; тесты не реализуют
параллельную игровую логику и не используют reflection/private helpers.

Синтетический каталог `SimulationFixture` намеренно не воспроизводит баланс SC2.
В нём ресурс содержит 17 единиц при грузе 5: сценарий обязан доставить остаток 2
без создания ресурсов. Скорость задана в миллиметрах в секунду, проверки идут
фиксированными тиками, с фиксированным seed и ограничением длительности.

Проверяемые контракты:

- Одинаковые seed и ввод дают одинаковый hash на каждом тике.
- Смешанный чужой selection отклоняется атомарно; duplicate не оплачивается дважды.
- Нехватка средств/supply не создаёт резерв или очередь; enqueue резервирует supply,
  cancel последнего production item полностью возвращает оплату и резерв.
- Добыча зачисляется только после drop-off, исчерпанный узел больше не платит.
  Газ требует завершённого собственного refinery и поступает в отдельный кошелёк.
- Placement учитывает границы, terrain и footprints; незавершённое здание занимает
  место, отмена удаляет его и возвращает полную стоимость. Supply появляется после
  завершения, ровно один раз.
  Весь footprint должен быть виден: preview и причина отказа Build одинаковы для
  неизвестной свободной и неизвестной занятой площадки, чтобы не раскрывать fog.
- Движение достигает доступной точки в обход стены и не пересекает её клетки.
  При клике внутрь terrain движение завершается у доступной границы и не повторяет
  бесконечный поиск недостижимой исходной точки.
  Новый foundation пересекает уже построенный маршрут: кэш navigation обновляется,
  движущийся юнит обходит footprint и доходит до исходной цели.
- Unknown enemy отсутствует в observation; подбор его ID не позволяет атаковать.
  Explored-клетки сохраняются, а скрытый урон не обновляет last-known здоровье здания.
  Возвращаемые observation, State, Find, Rules и snapshot отделены от live state.
  Observation сохраняет собственный приказ, но не выдаёт внутренние Path/PathIndex:
  маршрут авторитетного мира может кодировать неизвестные игроку препятствия.
- JSON save/load сохраняет pending input, bot scheduler, sequence и новые команды
  работающего AI; продолжение сравнивается с непрерывным прогоном.
- Replay хранит только принятые команды обеих сторон, не запускает AI повторно,
  воспроизводит конечный gameplay hash. Несовпавшие version/hash rules отвергаются.
  `BeginReplay` начинает на Tick 0 для интерактивного viewer; `FromReplay` служит
  проверкой полного повтора до конечного тика.
- Некорректный каталог (дублирующийся ID, отсутствующая ссылка, пустая карта)
  отвергается до запуска симуляции.
- Stim оплачивает HP, запрещает повтор до истечения и не может убить применяющего.
- Уничтожение последней базы даёт исход матча и останавливает симуляцию.

Тестовые ожидания выведены из `current-iteration.md` и согласованных контрактов:
Tick начинается с 0; Step исполняет готовые приказы, затем продвигает время;
production cancel и construction cancel в этом экспериментальном ruleset дают
полный возврат. Эти политики не заявлены как точное соответствие SC2.

## Подтверждённый результат 20.10.2026

| Проверка | Результат | Evidence под Builds/Evidence |
| --- | --- | --- |
| Реальная Unity-компиляция | completed, compilationFailed=false | compile.json; CLI recompile_status |
| EditMode contracts | 30 passed, 0 failed/skipped, 10.04 s | editmode-results.json |
| Реальные runtime rules, 1200 ticks | passed; исходный/save-load/replay final hash одинаков | smoke.json |
| Windows x64 BuildReport | succeeded; EXE создан | editor-session.log, build-result.json |
| Graphical standalone после исправлений UI | passed, exit code 0, 0 runtime errors, 230 frames | StandaloneFinal/player-smoke.json, player.log |
| HUD/камера/процедурная геометрия | кадр 1600×900, IMGUI repaint; просмотрен интегратором | StandaloneFinal/gameplay.png |
| Product pause/save/load | tick 609, одинаковые saved/loaded hashes, отдельный профиль | StandaloneFinal/Saves/frontier-save.json |
| Реальный replay viewer | старт Tick 0, продвижение по кадрам, конец ровно Tick 609, hash равен save | StandaloneFinal/player-smoke.json |

Симуляционный smoke проверяет живой AI после JSON restore, не только frozen
snapshot. Финальный hash этого сценария —
`bc027a56efe72ed4310152a9a53189a8be8f233a2ad94705d23a4a02ca665be9`.
Player smoke проходит реальные GameHost entry points, Update и рендер; он не
выдаёт программный вызов за аппаратный ввод.

Независимый обзор frontend выявил F5/F9 за ранним return в Paused и ложный текст
поражения в конце незавершённого replay. Обработчик перенесён перед gate паузы,
а для записи без исхода добавлен нейтральный текст. После исправлений выполнены
recompile, новая Windows-сборка и полный standalone smoke до конца replay.

## Повторение и ограничения

Контракты: `./tools/test-rts.ps1`. Создание сцены/build и headless smoke:
[runbook](../../tools/AGENT_RUNBOOK.md). Обычный оконный player smoke:

```powershell
& .\Builds\Players\Windows\KopruluFront.exe -rtsSmoke -rtsEvidencePath C:\work\game_test\Builds\Evidence\StandaloneFinal -screen-width 1600 -screen-height 900 -screen-fullscreen 0 -logFile C:\work\game_test\Builds\Evidence\StandaloneFinal\player.log | Out-Host
$LASTEXITCODE # ожидается 0; дополнительно проверить Status в player-smoke.json
```

Предыдущие неуспешные проверки сохранены: batchmode Editor не имеет Game View
render target; скрытый standalone пропускает рендер/IMGUI и завершил smoke failed.
Повтор в обычном оконном режиме прошёл. Camera-only снимок не считается HUD.

Мышь/клавиатура через Windows automation не проверены: GetCursorPos вернул
access denied. UI screenshot просмотрен, но полнота интерактивного playtest,
длительный матч, массовое движение, performance percentiles и SC2 parity не
подтверждены этим прогоном. E1 network spike, E0 reference manifest и полный art
gate остаются открытыми. Чистая симуляция не закрывает эти проверки.
