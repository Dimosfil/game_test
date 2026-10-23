# Итерация 06 — движение, бой, настоящий AI и редкие FPS dips

## C23 — trace не воспроизвела target; ordinary strict FAILED

На прежних frozen source204/binary394 выполнены отдельные diagnostic capture
и ordinary A/B/C; quality1600×900/4×MSAA/Ultra/shadow100/D3D11 и gameplay hash
сохранены. Новых source fixes или снижения качества в этой серии нет.

Ordinary **30 боёв/52025 кадров**: **10 >16.667ms,7 >20ms,6 >21ms,4 >25ms,
max196.3688ms**. Whole p95/p99/real-time20Hz и full800/near720 gates30/30.
A:17353frames/1slow/max20.0207; B:17175/8/max196.3688; C:17497/1/max19.5261.
B all-selection trials1–5 имеют6958 unfocused frames; все сохранены, но не
включены в clean acceptance. Focus/source-eligible25/44232:8 >16.667ms,
6 >20ms,5 >21ms,3 >25ms,max196.3688; percentile/band/20Hz gates25/25.
Clamps/source events0, hash прежний; GUI proofs passed с0 новыми native reads;
exact health proofs passed:7096 actual native vertex uploads/44929 exact reuse.
Никакие unfavorable outliers не исключены из полного отчёта.

Worst B selection1 trial5 frame10091/tick165:wall196.3688ms, focused1757/1757,
0clamps, population608/observed-visible591. GCCollect available0ms/0invocations,
collections19/19/19. Previous source10090:View.8209,GUI1.0263,Prepare2.2541,
Draw.9111ms; completed/aligned GUI Calls2/Repaint1. Current nativeCPU8.3382ms/
GPU4.145629ms не являются разбиением полного wall. Health exact reuse,496v/744i,
без reserve/index/vertex-upload delta. MainLoop/native-wait/GUI-cycle probes
выключены; причина остатка wall неизвестна. Это полностью focused outlier,
его нельзя объяснить unfocused all-selection trials. [Ordinary summary](../../Builds/Evidence/Iteration06/Cycle23SeriesAnalysis/cycle23-ordinary-summary.json),
[exact neighbours/provenance](../../Builds/Evidence/Iteration06/Cycle23SeriesAnalysis/cycle23-worst-neighborhood.json).

Единственная отдельно согласованная CPU/GPU trace C23 завершена:WPR inactive,
PID26580, game/start/stop exit0. Diagnostic4/6425:2 >16.667ms,0 >20ms,
max17.5083ms; focus/clamps0,whole/bands/20Hz4/4. **Target20–40ms не воспроизведён.**
Fresh eight-anchor clock proof и6 structural proofs passed;4928frames покрыты
фактическими CSwitch/Ready ranges,1497четвёртого trial находятся после trace.
Canonical анализ —5 covered targets+5 matched controls, не25 investigated frames.
Два самых медленных covered loops имеют explicit main Waiting10.3191/11.5448ms
и Ready.0205/.0133ms; это не owner/fence diagnosis. Native Present API не равен
Unity Gfx marker; queued packet lifetime не равно exclusive GPU execution.
Source watcher unavailable/events null, полные pre/post204/394 совпали.
Эта trace другого процесса не атрибутируется позднему ordinary196ms кадру.
[Trace proof summary](../../Builds/Evidence/Iteration06/NativeTraceCycle23/c23-proof-fps-summary.json),
[bounded scheduler](../../Builds/Evidence/Iteration06/NativeTraceCycle23/native-analysis/scheduler-verdict.md),
[bounded GPU](../../Builds/Evidence/Iteration06/NativeTraceCycle23/native-analysis/gpu-review.md).

**Every-frame16.667ms цель не достигнута.** C24 в работе как opt-in собственный
GAME-only watchdog/minidump: без external app data/system trace/quality changes.
Это ещё не FPS fix и не passed runtime/test verification. Dump-triggered pauses
должны быть отделены от natural tails. Новый capture этим шагом не разрешён.

## C22 — bounded cleanup сохранён; strict цель не достигнута

Retry suite 246/246 passed за 35.91s, compile 0 errors/0 warnings;
обе builds прошли. Source204 и оба player binary manifests394 проверены.
Единственный production diff от frozen C21 — callback-local Screen.height
в DrawWorldLabels. За Repaint 2+N native getter calls заменены одним:
убраны N+1 вызовов, где N — projected eligible entities, не все observed units.
Root сохраняет bounded code-efficiency cleanup. Нет persistent height cache:
следующий callback снова читает размер; качество, visibility, input и draw
cadence сохранены. Source review не доказывает каждый mid-callback resize interleaving.

ABAB **8 боёв**, отдельные cohorts:

- Control 4/5920 кадров: 11 >16.667ms, 4 >20ms, 0 >25ms, max22.1452ms.
- Candidate 4/5895: 6 >16.667ms, 3 >20ms, 0 >25ms, max23.5397ms.

**Оба strict acceptance FAILED.** Whole p95/p99/real-time20Hz 4/4 у обеих
групп; full800/near720 gates control2/4, candidate4/4. ControlA selection1
full800 p99 21.4151ms, ControlA all full800 p99 17.0245ms сохранены.
CandidateD selection1 **focus-qualified: 1622/1623 focused,1 unfocused**;
остальные7 боёв полностью focused. Нет per-frame focus flag для связи
конкретного outlier с потерей focus. Clamps/source events0; прежний gameplay
hash и 1600×900/4×MSAA/Ultra/shadow100/D3D11 сохранены. GUI-cycle proofs passed,
новые GUI native reads0; health exact proofs прошли во всех4 процессах.

WorldLabels source-frame medians selection1 снизились на2.1%/3.0%, однако
вторая p95 пара почти плоская/хуже. All medians .6586→.6537ms в первой паре,
но .6337→.6949ms во второй (+9.7%); p95 второй пары 1.0017→1.0249ms.
Стабильный scope/FPS выигрыш не доказан, lead для расширения до30 боёв нет.
Это bounded cleanup, не измеренный FPS-tail fix и не причинное ABAB доказательство.
[C22 summary](../../Builds/Evidence/Iteration06/Cycle22SeriesAnalysis/cycle22-abab-summary.json),
[implementation review](../../Builds/Evidence/Iteration06/Cycle22Verification/implementation-review.md),
[runtime quality review](../../Builds/Evidence/Iteration06/Cycle22Verification/runtime-quality-review.md).

Последующий C23 выполнен после отдельного согласия; результаты выше.
Необязательный контроль со свёрнутым UI остался без ответа и не проводился.
Общая цель устранения всех20–21ms кадров остаётся открытой.

## C21 — завершённая диагностика

246 tests passed, compile 0 errors/0 warnings; frozen manifests 204 source/
394 binary entries. Обычная серия **10 боёв/14691 кадр**: 21 >16.667ms,
7 >20ms, 0 >25ms, max 23.0099ms. Whole p95/p99/real-time20Hz passed 10/10,
full800/near720 gates 7/10; unfocused/clamped frames и source events 0.
Прежний gameplay hash и качество 1600×900, 4×MSAA, Ultra, shadow distance100
сохранены. **Strict acceptance FAILED: все 20–21ms кадры не устранены.**
Неблагоприятные полосы: selection1 trial2 full800 p99 22.6827ms;
all trial3 near720 p99 16.9023ms; all trial5 full800 p95/p99
16.7148/19.7779ms и near720 p99 19.1320ms.
[Ordinary evidence](../../Builds/Evidence/Iteration06/Cycle21SeriesAnalysis/cycle21-ordinary-summary.json).

GUI-cycle instrumentation opt-in: в ordinary все scope attempts/cycles равны0,
availability false и cumulative GUI read attempts/successes 0→0 во всех10
trials. Это подтверждает отсутствие новых GUI native queries в этом режиме.
[Ordinary proof](../../Builds/Evidence/Iteration06/MovingAIReleaseCycle21A/gui-cycles-proof.json).
Отдельный native diagnostic: 2 боя/2472 кадра, 124 >16.667ms,
49 >20ms, 16 >25ms, max 33.0446ms; GUI-cycle/boundary/native-wait/exact-health
proofs passed. Измерено 29664 успешных GUI read attempts; ordinary и diagnostic
не объединяются в FPS acceptance.

Matched examples из native серии (другие deduplicated source frames того же
trial, capture tick/population/observed/visible и Calls2/Repaint1):

- All GUI frame2660/tick199: elapsed17.1469ms, 9251060 cycles;
  3 controls median3.1864ms/10673314 cycles.
- All WorldLabels frame2275/tick85: elapsed9.7001ms, 3286236 cycles;
  9 controls median1.1269ms/3079652 cycles.

Часть elapsed выбросов не сопровождается пропорциональным ростом thread cycles;
есть и случаи роста обоих. Это не cycles→ms, не сумма вложенных scopes и не
доказательство scheduler/blocking cause. Capture labels не гарантируют точный
observation payload более ранних GUI callbacks; source IDs сохранены.
[Matched cycle evidence](../../Builds/Evidence/Iteration06/Cycle21SeriesAnalysis/cycle21-cycle-owner-summary.json),
[native GUI proof](../../Builds/Evidence/Iteration06/NativeWaitReleaseCycle21/gui-cycles-proof.json).

Следующим шагом стал callback-local Screen.height cleanup C22; результаты
source/tests/builds/ABAB приведены выше. FPS fix не заявлен.

## C18 — предшествующий результат

238 tests, 7 functional tests и dual builds за 12.423s прошли.
Frozen candidate: 202 source/394 binary-manifest entries; control: 200/146.
Source manifests подтверждают только BattleHud production diff и добавленные
test/meta; прочие UI/resources одинаковы. Существующие input callbacks,
detached observations и качество не ослаблялись.

Paired ABAB **8 боёв** сохранены отдельно: control 4/6838 кадров,
9 >16.667ms, 6 >25ms, max 34.9216ms; candidate 4/6954, 4 >16.667ms,
0 >20/25ms, max 19.7982ms. P95/p99/20Hz и полосы прошли 4/4 у обеих групп.
По deduplicated completed/aligned GUI source frames median SelectionCapture
selection1/all: 0.1233/0.1757 → 0.0027/0.0105ms; p95:
0.1980/0.2839 → 0.1109/0.1559ms. Calls/RepaintCalls и source flags сохранены
в evidence; это all-callback scope за source frame, не per-Repaint CPU.
Scope выигрыш повторён в обеих парах, но bounded ABAB не доказывает
статистическую FPS non-regression или устранение tails.

Обычная серия A/B/C: **30 боёв/50384 кадра**, 68 >16.667ms,
22 >20ms, 10 >25ms, max 37.8034ms. Unfocused/clamped frames и source events 0;
gameplay hash прежний. Whole p95/real-time20Hz 30/30, p99 29/30,
full800/near720 p95/p99 gates 27/30. Неблагоприятные результаты:

- A all trial3: full800 p95/p99 16.6755/17.4221ms; near720 p99 16.9008ms.
- B all trial3: whole-trial p99 17.5508ms.
- B all trial5: near720 p95/p99 20.0567/25.5862ms.
- C all trial5: full800 и near720 p95/p99 19.5737/24.5928ms.

Exact health proofs passed: 7070 actual native uploads/43314 exact reuse.
**Strict acceptance FAILED: все кадры 20–21ms не устранены.** Остаточные
elapsed tails относятся к разным scopes (WorldLabels, renderer Draw/Prepare
и неразмеченные интервалы), а не к единому SelectionCapture owner.
Elapsed scope не доказывает exclusive CPU/GPU cause; GC0 в top5 не исключает
другие механизмы. Native diagnostic C18 остаётся отдельной серией.
[C18 combined evidence с hashes, denominators, stages и всеми slow frames](../../Builds/Evidence/Iteration06/Cycle18SeriesAnalysis/cycle18-summary.json).

### C19/C20 — отклонённые rendering-mode эксперименты

C19 ABAB, 8 боёв: default **4/6492 кадров**, 9 >16.667ms,
3 >20ms, 0 >25ms, max 23.0686ms; direct **4/4945**, 29/7/2,
max 35.4769ms. Actual startup mode ClientWorkerJobs/Direct подтверждён логами.
Whole-frame medians в обеих direct сериях хуже: selection1 8.52–8.86ms
против default 6.46–6.59ms, all 10.16–10.34ms против 7.61–7.85ms.
P99 default 4/4, direct 3/4; bands 2/4 у обеих групп. Unfocused/clamps/source
events 0, 20Hz/hash сохранены. Direct не дал lead для длинной серии.
[C19 compact evidence](../../Builds/Evidence/Iteration06/Cycle19SeriesAnalysis/cycle19-abab-summary.json).

C20 ABAB, 8 боёв: default **4/5788 кадров**, 4 >16.667ms,
2 >20ms, 0 >25ms, max 22.4065ms; Graphics Jobs off **4/5332**,
30/8/6, max 31.5107ms. Actual mode default ClientWorkerJobs, jobs-off Threaded.
Default p99/bands 4/4; jobs-off p99 3/4, bands 2/4. JobsOffB all не прошёл
whole p99 17.3149ms и обе полосы p95/p99; JobsOffD selection1 не прошёл
full800/near720 p99 17.4414ms. **D selection1 focus-qualified: 1447/1448
focused, 1 unfocused**, поэтому этот trial не является clean acceptance.
FrameTrace не содержит per-frame focus: потеря focus не привязывается к
конкретному outlier. Первый A/B contrast полностью focused и также показывает
худшие jobs-off medians обеих selections. Clamps/source events 0, 20Hz/hash
сохранены; все неблагоприятные результаты оставлены в отчёте.
[C20 compact evidence](../../Builds/Evidence/Iteration06/Cycle20SeriesAnalysis/cycle20-abab-summary.json).

Оба варианта отклонены для расширения до 30 боёв. D3D11/GPU и качество
1600×900, 4×MSAA, Ultra, shadow distance 100 сохранены; продуктовые defaults
сохранены, финальная C20 source-restoration проверка подтверждает исходные inputs.
Последовательные ABAB дают bounded evidence, не исключительную причинную
атрибуцию. Следующим шагом стала opt-in C21 thread-cycle диагностика,
результаты которой приведены выше. **C19/C20 не закрыли strict FPS цель;
все 20–21ms кадры не устранены.**

## C17 — предшествующий результат

230 tests за 39.58s и dual builds за 15.293s прошли; frozen 189 inputs/8 binaries.
C17A сохранён отдельно: 6 Runtime source events, possible Editor compile
contamination. Он не включён в три серии B/C/D: **30 боёв/50126 кадров**,
unfocused/clamped frames 0, 42 >16.667ms, 9 >20ms, 1 >25ms, max 32.3311ms.
Whole p95/p99/20Hz — 30/30; exact full800/near720 p95/p99 gates — 28/30.
Неблагоприятные B сохранены: all trial3 near720 p99 17.5335ms;
all trial5 full800/near720 p99 18.8577/18.3710ms. **Strict gate FAILED:
устранение всех кадров 20–21ms не достигнуто.**

Exact counters/proofs: 7069 native vertex uploads, 43057 exact reuse;
pooled health-preparation medians selection1/all — 0.0127/0.0525ms.
В B/C/D Runtime/Resources source events 0, однако aggregate Editor CPU
соответствует примерно 1.2 cores за окно launcher. Это background context,
не причинная атрибуция slow frames. Анализ имеющейся ETL активности Editor —
следующее расследование; новый product fix по этим данным не заявлен.
Evidence с provenance, всеми gates и top20 tails:
[cycle17-bcd-summary.json](../../Builds/Evidence/Iteration06/Cycle17SeriesAnalysis/cycle17-bcd-summary.json).

### C17 E/F/G — отдельный контроль с закрытым Editor

**30 боёв/41391 кадр**: 474 >16.667ms, 144 >20ms, 48 >25ms,
max 96.3815ms. P95 passed 29/30, p99 16/30, full800/near720 bands 20/30,
real-time 20Hz 30/30. Unfocused/clamped frames и Runtime/Resources source
events — 0. Editor отсутствовал в обоих observation boundaries; CPU delta
недоступен/null, а не измеренный ноль. Exact health proofs passed:
6472 native vertex uploads, 34919 exact reuse. Все неблагоприятные кадры и gates:
[cycle17-efg-editor-closed-summary.json](../../Builds/Evidence/Iteration06/Cycle17SeriesAnalysis/cycle17-efg-editor-closed-summary.json).

Отдельная instrumented native diagnostic серия — 2808 кадров,
max 39.9139ms; boundary/native-wait/exact-health validators passed.
[Diagnostic summary](../../Builds/Evidence/Iteration06/Cycle17SeriesAnalysis/cycle17-editor-closed-native-summary.json).
Закрытие Editor не устранило tails. Последовательные B/C/D и E/F/G
не являются рандомизированным причинным сравнением; полного FPS успеха нет.
Editor восстановлен через official Unity open (PID 23464), Frontier проверена
clean; start-unity упёрся в устаревший isRunning. Необязательный пользовательский
контроль со свёрнутым UI пока ожидает решения. Новое исправление продукта
из этих результатов не следует; strict цель каждого кадра ≤16.667ms открыта.

Ниже сохранён предыдущий отчёт C16.

C16 — предварительный кандидат, **не принят**. Exact health-geometry reuse
прошёл 197 tests за 38.74s, compile 0/0 и dual builds за 14.826s;
заморожены 179 inputs/8 binaries. Три обычные Release-серии:
**30 боёв/49315 кадров**, полный focus, 0 clamps; 52 кадра >16.667ms,
8 >25ms, max 34.8935ms — strict gate failed. Кроме того, C16A selection1 trial1
не прошёл full800 p99, C16C all trial5 — whole-trial p99. Все отказы сохранены.
Vertex uploads: 2367+2367+2364=7098; exact reuse: 14088+14186+13943=42217.
Это около 85.6% пропущенных повторных vertex API calls, не доказательство FPS
улучшения. Качество и каждый непустой Repaint draw сохранены. Длинная native C16
серия ещё выполняется; сравнение с C15 запланировано. Evidence:
`Builds/Evidence/Iteration06/cycle16-release-series.json`.

Продолжение C14/C15: цель ужесточена до каждого измеренного кадра≤16.667ms
в длинных сериях при неизменном качестве. Прежний C13 принят только по указанным
ниже bounded gates и не выполняет новый strict критерий. Compact aggregator
отделяет validation от performance acceptance: passedp99 не скрывает long frames.
C14 exact byte-dirty fog не принят: пропущено0nativeuploads, strict FPS gate
не пройден. BattleView возвращён к C13, пять экспериментальных texture tests
сохранены отдельно. В C14 discovery200 подтверждён после исправления неверного
33symbolGUID нового.meta; промежуточный195-test запуск не засчитан.
Контрольный неизменённый C13 также дал clean max37.5949ms; отдельные максимумы
не доказывают причинность fog-кандидата. Неблагоприятные результаты сохранены.

C15 добавляет только опциональную диагностику GUI и raw clock anchors.
Длинная native Release-серия: **10боёв/16334кадра**,24>16.667ms,3>25ms,
max27.5312ms. Новая strict цель остаётся незакрытой. HUD/fog/minimap,
1600×900,4×MSAA,Ultra и дальность теней100 сохранены. Offline join собственного
Unity binary Profiler C15 подтвердил offset −1 на обычных и holdout samples:
raw frameIndex = profile capture Frame − 1; это отдельно от source Frame−1
семантики callbacks. Загружено 2000 кадров, доступны 50 из 55 slow hierarchies,
measured truncation 0. Это не окончательная CPU/GPU причинная атрибуция.
Новый системный захват не запускался; для него требуется новое согласие.
Evidence: `Builds/Evidence/Iteration06/UnityCpuCycle15/profiler-marker-join.json`,
`Builds/Evidence/Iteration06/NativeWaitReleaseCycle15/tail-summary.json`,
`Builds/Evidence/Iteration06/NativeWaitReleaseCycle15/native-clock-proof.json`;
C14 и control сохранены в `MovingAiReleaseCycle14A`
и `Control13After14A` той же папки Iteration06.

Исторический результат C13:195/195tests и обе builds; три обычные Release-серии дали
**30cleantrials/50545frames**,всеp95/p99/20Hz/full800/near720 gates,
11>16.67ms,0>25ms,max20.9734ms. Nativeindexuploads всего6 на50545geometryuploads.
Patrol/standalone regressions passed. C11 провалил FPS gate; C12
прошёл20/20p99,но сохранил max32.8942ms. Единственная разрешённая системная
трасса разобрана: graphics-path blocking локализован,конкретная GPU/driver
причина не доказана. Полное устранение rare tails не заявляется; все raw
failures сохранены. C4 worker override не принят.
Запрос — измерить и улучшить совместную нагрузку от начальных 800 бойцов,
реального движения, damage/death и работающего AI; разобрать редкие frame dips.
Фактический baseline приведён ниже. [Итерация 05](iteration-05.md)
заморожена отдельно: её AI-off checkerboard не доказывает этот сценарий.

Rules/save/replay остаются `prototype-terran-0.3`. Это экспериментальный
Terran slice, не числовая/визуальная SC2 parity или полный melee match.

## Контракт baseline C0

Source: `Assets/Game/Runtime/Diagnostics/SessionPerformanceProfile.Combat.cs`,
`GameHost.cs`, `Simulation/Match.Diagnostics.cs`, `Match.Bot.cs`,
`Match.Navigation.cs`, `Match.cs`, `tools/run-rts-player.ps1`.

| Сценарий | Владелец и механизм | Observable до оптимизации |
| --- | --- | --- |
| Разнесённые 400+400 начинают бой | Combat fixture → обычные Match rules | Initial IDs/counts/positions, реальные shots/damage/deaths |
| Player0 идёт в бой | Один AttackMove на Tick0 к (49,49) через GameHost.Issue | Принятый command, actual travel/moved IDs, TickTrace |
| Player1 принимает решения | Обычный enabled Bot1 через Think/команды/validation | Thinks, accepted command kinds/units и accepted AiCommands log |
| Навигация → presentation | Match/worker publication → View/HUD/fog/minimap | Live/moving/observed/visible/selection trace, реальные camera/GUI frames |
| Редкий slow frame | Main/worker/View/GUI/instanced-submit stages | Raw frame trace, callback frame indices, GC/CPU/GPU и growth counters |
| Исправление bottleneck | Измеренный owner, один интегратор | Тот же fixture/hash/quality, paired actual player evidence |

C0 заменяет исходные marine spawns двумя формациями **40×10**, по 400 на owner,
spacing0.8 m и gap3.2 m между ближними рядами. Original nonmarine spawns,
bases/workers/resources,350 starting minerals и supply/cost rules сохраняются;
terrain очищен явно. Это другой manifest от 05, его timings не образуют с 05
парный before/after. Camera zoom48, focus(49,0,39),1600×900,4×MSAA/full shadows.
Все 800 initial centers должны быть observed в unobscured viewport.

Каждый trial: новый Match/seed42, pause warmup минимум 2 s и 90 camera/GUI frames
на Tick0, затем exact240 ticks (12 simulated seconds). Initial selection1/all400
проходят независимо; смерть естественно уменьшает composition/selection.
Никаких invulnerability, health reset, replenishment или fog override.
Player0 получает только один scripted AttackMove; player1 не получает scripted
orders и управляется обычным ботом. UI/view читают detached publications.

Подтверждённый launcher contract: `-Kind combat -MovingAI -CombatTicks 240`,
`-CombatTrials 1..5`, отдельный `-DevelopmentProfile`. MovingAI разрешён только
для combat/240 ticks. Результат — `session-profile.json` своего evidence path
в ignored `Builds/Evidence/Iteration06/`; exact run команды ведёт интегратор.

## Предварительно объявленные acceptance thresholds

До baseline зафиксированы и исполняются source assertions:

- Exactly400+400 на Tick0 и 800 initial observed/unobscured centers.
- К концу trial **>=100 unique moved marines каждой стороны** и cumulative
  travel **>100 m каждой стороны** (`>100000` integer units).
- **>=10 bot thinks**, accepted AttackMove commands>0, суммарно
  accepted BotAttackMoveUnits>=100 и accepted BotBuildCommands>0.
- После review добавлены более строгие C1 checks: >=100 **unique** IDs в
  accepted bot AttackMove log и >=10 MovingAttackPublications с положительным
  shot delta и TickMovingMarines>=20 каждой стороны. Это publication/last-step
  overlap при multi-step batch, не автоматически same-tick shot attribution.
- Настоящие shots/damage обеих сторон, deaths>0, exact Tick240, одинаковый
  final hash при смене selection/trial; actual camera/GUI samples и foreground.
- Frame budget и real-time 20 Hz — разные gates. Время на 240 ticks, completed
  batches/steps и publication gaps должны подтверждать clock throughput;
  быстрый render с отстающим worker не считается успешной игрой.

Порог 100 movers — **не утверждение, что все 800 двигаются одновременно**.
Actual moved counts/travel публикуются рядом с initial800. Ranged marine может
останавливаться для стрельбы, поэтому наличие AttackMove не заменяет movement
proof. Совместный marching/engagement workload подтверждается trace, а не одной
картинкой. Если результат покажет меньшую движущуюся часть, не переименовывать её
молча в «800 одновременно движутся и стреляют».

Stress army превышает обычный supply cap; нормальные Train rejects возможны.
Accepted Build и реальные army commands доказывают принятие решений/расходы,
но не полный technology/economic planner и не успешную production при нехватке
supply. AI commands должны быть действительно приняты, flag Enabled недостаточен.

## Семантика diagnostics и limits

`MatchCombatDiagnostics` — derived opt-in struct, не gameplay state/schema/hash.
RecordMarineMovement вызывается только после допустимого перемещения:
MovedMarines — unique IDs с любым положительным segment displacement;
MarineTravel — суммарная длина пути, не net displacement и не 100 m на каждый unit.
TickMovingMarines отражает последний выполненный step, если batch содержит
несколько steps. Cumulative travel/moved/shot counters учитывают все steps.

BotThinkCount растёт на обычных Think calls; BotAcceptedCommands и kinds — после
успешного Execute. BotAttackMoveUnits суммирует ID lengths повторных commands,
это не unique commanded set; retained AiCommands позволяет проверить источники.
В этом fixture только bot генерирует player1 commands. Counters обнуляются при
новом Match/Restore и не объявляются persistent cumulative continuity.

Raw FrameTrace связывает wall time с published tick/population, host advance,
pending worker, View LateUpdate/GUI/instanced submit timings и их frame indices,
native CPU/GPU/GC, collection counts, created visuals/matrix growths. Эти
поля не являются автоматически синхронными: callback indices и previous-frame
семантика native counters должны учитываться до причинных выводов о spike.

Live, observed и rendered/unobscured counts различаются при настоящем fog и
движении за пределы камеры. Не выдавать authoritative full800 band за 800 реально
видимых моделей, если Coverage trace показывает меньше. Natural deaths/fog
не исправляются всевидением; scope и population bands публикуются честно.

Detailed movement HashSets и FrameTrace добавляют diagnostic overhead. Их
capacity/growth и стоимость должны быть одинаковыми baseline/final; first-step
instrument growth не смешивается с доказательством product allocation owner.
Не читать Match синхронно внутри frame sampling ради diagnostics.

## Редкие dips: что считается evidence

Сохранять raw frame series, p50/p95/p99/**p99.9**/max; числа кадров >16.67,
>25,>33.33,>50 ms и longest consecutive overbudget burst. C0 source эти поля
считает. Вместе с ними публиковать actual duration, frame counts, population,
movement/shots/AI progress и focus/clamp/throughput flags.

Выбрать measured owner по aligned slow-frame trace: scheduling/publication,
worker backlog, view/GUI/submission, GC, spawn/death/selection cleanup, pool/
buffer growth либо GPU. Не удалять невыгодные outliers и не уменьшать geometry,
quality, army или живой AI ради percentiles. Fixed repeated windows и одинаковый
manifest важнее одного удачного run. Absolute absence любой OS/driver задержки
не выводится из ограниченной серии; residual spikes публикуются числами.

Allocation scope тот же, что 05: producing native Development counter отдельно
от Release, raw/total/bytes per completed tick плюс per-frame quantiles.
Current-thread pure .NET bytes не считаются полным Unity/worker GC total.

## Read-only review C0 и открытые проверки

- В C0 выявлено: movingAI пропускает подготовку first-volley, но общий tail
  пишет `combat-volley800.png` из paused Tick240, затем TogglePause возобновляет
  host. Это не first-volley800 evidence; WorkerPauseValidated может статьfalse
  при status passed. C0 performance/counters сохранены, mislabeled PNG не
  считается first-volley доказательством. Root исправил весь tail branch и
  добавил RequireWorkerPauseValidated; C1 player подтвердил final pause=true.
- C0 bands опирались на authoritative population без moving coverage gate.
  Исправленный source считает full/near-full band только при равенстве
  population==Observed==Visible, FrameTrace сохраняет все три counts.
- C0 first measured movement выделял/grow diagnostic HashSets. C1 source
  готовит четыре HashSet capacity400 при включении diagnostics до warmup;
  это explicit instrumentation correction, а не product behavior change.
- Повторный read-only review: pause/coverage/unique-AI fixes не выявили нового
  P1. P2 first-sample unconditional shot count исправлен строгим положительным
  delta противzero baseline, включая первую publication.
- Если бой естественно завершится до horizon уничтожением base, не подавлять
  victory и не дополнять интервал idle Results frames: записать actual finish
  tick/reason и объяснить scope. C0 пока требует Playing доTick240.

Source review отделён от фактического performance baseline ниже.
Root владеет runtime fixes; агент отчёта не меняет Editor/runtime и не запускает
параллельные probes. Новая feature scope не отменяет frozen результаты 05.

## Текущие подтверждённые факты

### C0 — moving/AI доказаны, rare dips и allocations остались

`MovingAiReleaseBaseline/session-profile.json` и отдельный
`MovingAiDevelopmentBaseline/session-profile.json`:240 ticks, оба selection
modes real-time 20 Hz,0 clamps/unfocused. C0 status passed имеет известный
post-run дефект **WorkerPauseValidated=false**, исправленный в C1 source;
timed performance/counters действительны, final pause gate C0 не пройден.

| Release initial selection | Frames / seconds | Frame p95 / p99 / p99.9 / max, ms | Frames >16.67 /25 /33.33 /50 ms | Longest burst | Worker batch p95, ms |
| --- | --- | --- | --- | --- | --- |
| 1 | 1595 / 12.0617 | 10.2267 / 12.6269 / 19.2865 / 21.2013 | 7 /0 /0 /0 | 1 | 68.0307 |
| all400 | 1352 / 12.0317 | 11.9847 / 15.4546 / 33.4393 / 38.5555 | 10 /2 /2 /0 | 3 | 72.2148 |

Render p95/p99 сами по себе скрывали редкие dips: all-selection p99.9
**33.4393 ms**, max **38.5555 ms** и burst3. Worker значения относятся к
batch, не автоматически к одному step; clock throughput измерен отдельно.

Одинаковый Tick240 state обеих builds/selections: **253 deaths**, **547 live**
(272/275), shots1144/1136, damage6489/6432. Реально двигались **320/323 unique
marines**, cumulative pathlength **3009.033/2683.473 m**. Bot:12 thinks,6 accepted
commands,5 AttackMove с 401 суммарными ID и 395 unique commanded IDs,1 Build;
Train/Gather accepted0. Это настоящее участие bot/движения, а не claim всех 800
одновременно moving+firing. Сохранился hash:
`57aa79fa7f18f8e3f46f42c40386b51ca3891b0b2d9d7cc83d6ab56fdb544a46`.

Отдельный Development baseline native GC total **809645394/822244925 bytes**,
**3373522.48/3426020.52 bytes/completed tick**. Frame p95 **14.1800/12.6382 ms**,
p99.9 **29.2915/19.5392 ms**. Это producing allocation evidence, не zero-GC,
не Release timings. Detailed diagnostic preparation меняется отдельно; итоговая
экономия должна различать instrument correction и настоящий product owner.

### C1 — workspace proof и реальный Release результат

Mechanisms: reusable A* workspace у navigation owner, inactive-flash matrix
reserve и O(1) renderer unregister у presentation owner; diagnostics buffers
готовятся до warmup. Никакие unit/rules/AI/quality не урезаны ради benchmark.
`moving-ai-cycle1-equivalence.json`:240 full states/обе observations/counters
и live-AI120tick Restore/replay в двух variants совпали с C0; конечный hash тот же.
Pure Step bytes/tick **3136951.73→26555.83**, около99.15% меньше; CPU total
**5877.47→4640.13 ms**, около21% pure. Эти значения не являются Unity whole-run
GC/frame или exclusive runtime CPU.

Реальная compile0/0, **167/167 Unity tests**, **35.83 s**; обе builds success,
job `da846648cedf47bca1a16c34e2f03da2`, **11.865 s**.
`MovingAiReleaseCycle1/session-profile.json`: оба trials real-time20 Hz,
0 clamps/unfocused, actual camera/GUI frames; final pauseRequire passed.

| Initial selection | Frames / seconds | Frame p95 / p99 / p99.9 / max, ms | >16.67 /25 /33.33 ms | Worker batches / p95 / max, ms |
| --- | --- | --- | --- | --- |
| 1 | 1783 / 12.0240 | 8.7608 / 10.1504 / 12.1406 / 16.6418 | 0 /0 /0 | 237 /43.7248 /154.1194 |
| all400 | 1493 / 12.0323 | 10.4193 / 12.2958 / 14.6054 / 16.4806 | 0 /0 /0 | 235 /50.9333 /116.2982 |

В этой конкретной серии **все3276 measured frames<=16.67 ms**, без заявления
об абсолютном отсутствии будущих OS/driver spikes. GC collection count63/57
против C0 698/640; это activity count, не allocation bytes.

Отдельный `MovingAiDevelopmentCycle1`: native GC totals **77229858/79296448
bytes**, **321791.08/330401.87 bytes/completed tick**, collection counts60/56.
Frame p99 **15.319/14.236 ms**, max **27.756/20.259 ms**: Development не
подменяет Release. Whole-run bytes/tick примерно на90.4% ниже C0, но C1 ещё
включает известную instrumentation allocation на границе warmup: backing
FrameTrace около1.44 MB готовился после warmup и попадал в первый native GC
sample. Pure paired Step proof выше отдельно подтверждает product improvement;
изменение границы измерения в C2 не будет объявлено дополнительной игровой
экономией. Исходные C0/C1 samples сохраняются без удаления выбросов.

State/hash/deaths253/survivors547 и movement/AI totals сохранились. Новые
proof checks:185 moving-attack publications и395 unique bot commanded IDs.
Matrix buffer growths и tracer creations в samples0; displayed tracers2197/2199.
Coverage-aware full800 band61/52 frames, p99 **11.4012/14.6054 ms**; near-full
band140/115 frames. Это короткая high-population часть естественного боя.
C0 bands были population-only; перед парным band сравнением требуется join
его FrameTrace.Tick и Coverage, исходный JSON не переписывается.

Worker batches не всегда single-step:240 ticks выполнены за237/235 publications.
Поэтому p9550.9333 и max154.1194 ms не выдаются за per-tick timings. Clock
реально выдержан, но headroom остаётся задачей C2.

План C2: exact nearest-goal-cell shortcut только если лучший unconstrained cell
допустим, иначе прежний scan; сохранить tie/blocked/footprint semantics и
независимый full-state proof. Root также исследует health-mesh buffer retention.
Harness переносит evidence backing allocation до90-frame warmup и фиксирует
GC.CollectionCount непосредственно после timed loop, до quantiles/ToArray.
Отдельная untimed fresh-Tick1 capture теперь применяет тот же player0 command
для MovingAI, требует800 living units и shots обеих сторон; каждый trial и
финальная capture должны завершаться drained pause. Это исправления измерения,
не изменение нагрузки боя. Результат расширенной серии10 trials приведён ниже.

### C2 — pure proof и расширенная Release серия с одним p99 failure

`moving-ai-cycle2-equivalence.json` подтверждает все240 full-state/diagnostic/
две player observations, Restore на Tick120 с точным продолжением, replay и
две отдельные160-tick AI variants. Дополнительно6 custom cases и4810
independent one-dimensional old-goal-scan cases; final hash57aa… тот же.
Frozen Cycle1 Simulation и текущая версия скомпилированы side-by-side.

Pure240 Step CPU **4768.30→2509.30 ms** (около47.4% меньше), allocations
**28188.47→26865.27 bytes/tick**. Scope включает initial lazy caches и
исключает Observe/publication/JSON/Unity. Это локальная paired proof и timing,
не доказательство player FPS/worker20Hz или native GC C2.

Последняя review correction: `CombatTrials.Add` также перенесён до warmup;
`CombatCoverage` теперь serializable value type с прежними JSON fields,
убирающий instrumentation object на каждую публикацию. Fresh untimed volley
имеет отдельные `VolleyCaptured/VolleyTick/VolleyLiveMarines`, shots обоих owners
и800 live на Tick1, без включения PNG encode в performance samples.

Final C2 compile **0 errors/warnings**, **173/173 Unity tests passed за36.43 s**.
Обе Windows builds success, job `971ff4e01126412dae9620a9710cc14e`, **13.172 s**.
Frozen attempted-final C2 manifest:171 inputs/8 binaries. Он относится к этой
версии, а не будущему C3. Сборка и тесты не заменяют измерение player.

`MovingAiReleaseFinal/session-profile.json` — имя attempted-final evidence,
**не успешное завершение итерации**. Все10 trials выдержали p95 и real-time20 Hz,
но all-selection trial4 frame p99 **17.6281 ms**, поэтому общий p99 gate
**failed,9/10 pass**. Максимальный frame всей серии **24.5411 ms**, кадров
>25 ms нет. В отдельных коротких coverage-aware full800 bands p99 также
выше16.67 ms, с максимальными samples около19–20 ms. Это не скрывается
overall percentile или surviving lower-population tail. Raw samples сохранены,
выбросы не исключались. Legitimate Tick1 volley800 PNG и final pause passed.

Диагностика указывает на presentation submission как следующий measured owner:
Submit mean около3.2–3.85 ms, worst **16.0078 ms**, GUI **2.69 ms**, View
**1.05 ms**, GPU около **4.18 ms**. Callback frame indices требуют alignment;
эти maxima сами по себе не доказывают единственную причину каждого tail.
GC.CollectionCount чаще не менялся на slow frames; incremental GC этим не
исключён.

Отдельный C2 Development: оба trials real-time20 Hz и p99 passed; native GC
totals **79236242/79906957 bytes**, **330151.01/332945.65 bytes/completed tick**,
GC max **352309/472681 bytes/frame** после исправления warmup boundary.
Frame max **28.4955/17.5708 ms** относится только к Development. Это ненулевые
allocations; дополнительной экономии относительно C1 whole-run не заявляется.
C2 provenance сохранён в `source-manifest-cycle2.json`,
`binary-manifest-cycle2.json`, `build-cycle2.json`, `compile-cycle2.json` и
`editmode-cycle2.json`. Final-named manifests позднее могут принадлежать C3;
историческая attempted-final folder C2 с raw samples остаётся отдельно.

### C3 — pose cache корректен, расширенная серия снова не прошла p99 gate

Presentation owner добавил exact cached per-prototype animation poses для
movement/attack/time. Каждая instance должна получать свою копию matrix data,
с сохранением независимости позы, recoil/flash и прежних transforms. Цель —
убрать повторные TRS/Euler native calls при том же качестве и составе армии.
Read-only review не выявил нового correctness defect: четыре pose matrices
немедленно копируются как values из ref-readonly cache entry, state/time keys
и movement/recoil cessation guards сохранены; neighboring instance не может
изменить уже скопированную позу. Cache живёт в prototype до последнего owner
reference. Same-time mixed states и nonmonotonic time покрываются новыми tests.
Этот review source проверен отдельным фактическим Unity запуском ниже.

Pose computation вызывается из BattleView Animate; прямое улучшение ожидается
в ViewMs. Старый Submit tail сам по себе не доказывает pose bottleneck. Новые
FrameTrace `PrepareMs/DrawMs` разделяют preparation/submission, сохраняя общий
SubmitFrame и отдельную wall-stage семантику. Сначала semantic/pose parity и
интеграционные тесты, затем свежие builds и та же длинная серия; заранее
успешный итог не заявляется.

C3 compile **0 errors/warnings**, fresh Unity suite **177/177 passed за34.16 s**.
Первый CLI recompile вернул HTTP400 во время успешного domain reload;
`editor-session.log` подтвердил Tundra build/new assemblies, повторная проверка
дала up-to-date0/0. Это transient tooling response; C# defect, Editor restart
или package change не потребовались. Полный fresh177 suite относится к новым
assemblies. Обе C3 builds job `3f4b34393d474235a09effb759a4d39d` success за
**12.142 s**. C3 source/binary manifests фиксируют171 inputs/8 binaries;
Cycle2 snapshot сохранён отдельно, C3 provenance сохраняется с suffix-cycle3
перед новым source batch.

`MovingAiReleaseCycle3Final` — ещё одна attempted-final серия, **не final
success**. Все10 trials p95/20 Hz и прежний deterministic hash/настоящий AI
сохранили. P99 прошли **9/10**: all-selection trial2 **17.0287 ms** — failure.
All-selection trial4 p99 **16.0384 ms**, но max **29.4379 ms**; по всей серии
**5 frames>25 ms**. Перцентиль trial не отменяет его редкие slow frames, а
остальные девять trials не отменяют failed общий gate. Raw evidence сохраняется.

View mean примерно0.7 ms вместо0.9 ms — умеренное наблюдение, не доказательство
устранения tail. В slow-frame clusters одновременно растут CPU stages и worker
timings, GPU около4.2 ms стабилен. Даже совпадающий CPU cluster ещё не доказывает
конкретный causal owner; cache remains correctness-preserving candidate fix,
не объявляется решением общего p99 failure.

### C4 — диагностика GC и CPU clusters перед следующим fix

Сначала добавить producing native GC instrumentation с явными sample/frame
boundaries и сопоставить её с CPU/worker traces. Не выводить отсутствие
incremental GC из неизменившегося CollectionCount. Не менять quality/workload,
не исключать невыгодные frames и не выдавать повторный удачный короткий run за
доказательство длинного gate.

`GcCpuDevelopmentDiagnosis`:4 Development trials завершены с producing native
CPU/GC probes. Incremental GC включён, default budget3 ms. По trials GCCollect
max **2.38–3.10 ms**, Incremental max **3.53 ms**. Worst observed frame
**26.37 ms**: GCCollect0 ms, Incremental около **0.0012 ms**, ThreadCycles
около **53.9 million**, ProcessCPU **109.375 ms**. Последний counter coarse и
суммирует все threads; это не CPU duration одного frame/worker и не доказательство
OS scheduling как причины. Изменения CollectionCount не заменяют producing
GC stage evidence, а один slow frame без GC не исключает GC во всей серии.

`gc-handles-editor.txt` фиксирует реально обнаруженные counters.
`cpu-probe-calibration.json` подтверждает доступность двух native CPU calls:
100 ns counter положителен, ProcessCPU квантован примерно15.625 ms. Первый
короткий spin не изменил coarse process time; это ограничение granularity,
не отсутствие native call. Эти calibration и Development результаты не являются
Release FPS evidence.

Editor сообщает12 logical CPUs,11 active/max job workers. C4 metadata теперь
сохраняет actual workers; launcher поддерживает opt-in `JobWorkers` override.
Release diagnostic build job `a2c189db43b449e8bfd2513d40c7cfce` success за
**7.804 s**, compile0/0. Нового full suite после diagnostic-only changes пока
не было; C3 **177 tests** остаются последним полным gameplay suite.

Worker-contention hypothesis проверяется same-binary ABAB: default11 против
CLI4, четыре свежих Release players, по два независимых боя. Результат
`job-worker-paired.json` смешанный: **hypothesis inconclusive,4 workers not
adopted**. Источники `JobWorkersReleaseDefaultA`, `JobWorkersRelease4B`,
`JobWorkersReleaseDefaultC`, `JobWorkersRelease4D` сохраняются как raw evidence.
Default A frame maxima1/all **28.22/13.36 ms**, default C **17.86/27.19 ms**;
4B **19.95/19.34 ms**,4D **19.31/25.07 ms**. 4D all p99 **16.1431 ms** хуже
default C **15.1513 ms**. Hash/quality/20 Hz одинаковы; выбор только выгодной
пары скрыл бы mixed result. Product default остаётся **11 actual workers**,
incremental GC budget **3 ms** не менялся. Runtime и binaries C4 frozen
отдельно в Cycle4Sources/Runtime и historical binary provenance.

### C5 — affine preparation math, source/parity сначала

Presentation owner оптимизирует PrepareWorld через affine matrix math и
вычисление bounds radius с одним sqrt. Finite TRS input должен сохранять
прежние transforms/bounds; nonaffine/nonfinite input требует прежнего fallback.
Randomized public legacy-parity tests проверяют assembly behavior, а не
дублируют внутренние formulas. Native CPU/GC probes остаются opt-in, production
policy не меняется. Fresh suite/build/длинная финальная серия C5 pending;
candidate optimization пока не доказывает устранение frame tail.

Свежий C5: compile0/0, **182/182 Unity tests passed за36.66 s**, включая
новый actual PrepareFrame test:32 signed/sheared/zero/tiny-scale cases,
legacy vertex containment, bounds, eligible batches/matrices, layer rebinding
и реактивация. Обе builds job `ce8b726bc3dc4cdcb3f9c32c35e95fcb` success
**13.581 s**. Source/binary manifests cycle5 и Cycle5Sources/Runtime сохраняют
этот вариант отдельно от следующих изменений.

`MovingAiReleaseCycle5A`:10 independent240-tick battles, все p95/p99/20Hz
passed; p99 range **10.0862–14.9353 ms**,15,621 measured frames. Всего20
frames>16.67ms, один>25/33.33/50ms. Worst **187.8012 ms** в all trial5,
tick162 при611 live/592 observed/visible. Этот выброс сохранён и не считается
устранённым: previous callbacks View0.8652/GUI2.023/Submit2.4027ms
(Prepare1.7626/Draw0.6399), native CPU6.66ms/GPU0, main cycles24.1million,
processCPU0 на coarse counter, GCCollect0/Incremental unavailable.
GPU0 здесь не доказывает отсутствие GPU-работы: sample неположительный.
Измеренные callbacks не объясняют wall interval; конкретная причина пока
не установлена. Остальные maxima до24.6643ms. Final pause, legitimate volley,
hash/AI/естественные253 deaths/547 survivors сохранены. Успешный p99 не
превращает эту серию в доказательство отсутствия rare dips.

### C6 — меньше временных массивов observation без fog disclosure

Scenario: worker/bot Observe -> detached projection -> Match.Observation
владеет фильтрацией/копированием -> private staging видимых entities и exact
fresh public list capacity -> paired every-tick state/observation/Restore/replay
proof, privacy/detachment tests, fresh builds и repeated graphical series.
Прямое sizing по authoritative count запрещено: публичный Capacity раскрыл бы
скрытую численность. Staging содержит только приватные refs, очищается finally;
публикуются независимые DTO в прежнем порядке. Read-only review не выявил
нового defect. Реальные allocation/Unity/FPS outcomes ещё ожидаются.

Paired C5/C6 pure CLR proof passed:240 full states/diagnostics/both-player
observations, Restore120 continuation/replay every tick и две160-tick AI
variants/Restore30. Final hash прежний. Direct TimedObserve обеих сторон
после warmup: **488880.4→466385.1 bytes/tick**, **4.60%** меньше;
это две observations на tick, обычный host публикует player0. CPU total
**1099.74→1103.36 ms** практически не меняется; ускорение CPU не заявляется.
Hidden-capacity/detachment proof passed. Evidence:
`moving-ai-observe-capacity-cycle6-equivalence.json`. Fresh Unity compile0/0,
**184/184 tests passed за35.78 s**. Graphical FPS/nativeGC ещё не измерены.

Обе C6 builds job `ab77fad601b14bf2bb8b191b2ea193d8` success **12.360 s**;
173 source inputs/8 binary hashes и Runtime frozen в Cycle6Sources/Runtime.
`MovingAiReleaseCycle6A/B`: два fresh players, **20 trials/32901 frames**,
все p95/p99/20Hz passed. P99 **9.2709–12.7866 ms**, максимальный trial p99.9
**18.0730 ms**; всего **11 frames>16.67 ms,1>25 ms,0>33.33 ms**.
Worst **29.4451 ms**, C6A selection1 trial4 tick12,800 live/observed/visible.
Поэтому короткий full800 band p99 достигает29.4451ms; ещё один full800
band17.6691ms в C6B. Max near720 band p99 **15.7766 ms**. Нижняя population
не скрывает этих failures. C6B отдельно не имел>25ms, max21.8942ms; raw C6A
outlier сохраняется, не заменяется удачной второй серией. Hash, AI decisions,
movement/death, quality и pause/volley gates одинаковы; новых tracer/matrix
allocations в timed interval нет. Это улучшенный bounded p99 результат,
не полное устранение rare dips и не изолированный causal FPS proof C6.

Отдельный `MovingAiDevelopmentCycle6`: оба20Hz/p99 passed. Native GC totals
**78309691/77407657 bytes**, **326290.38/322531.90 bytes/completed tick**;
max336149/467145 bytes/frame, producing GC/incremental counters доступны.
Это ненулевой whole-run GC; он не равен pure обеих observations proof и не
доказывает Release zero-GC. Source/binary manifests cycle6 сохранены отдельно.

### C7 — локализация оставшегося wall interval

Opt-in `-MainLoopBoundaryProbe` добавляет raw timestamps/cycles между previous
resume, WaitForEndOfFrame, early Update и current resume. Frame IDs/validity
guards и telescoping sums должны пройти actual player proof. Native CPU sample
перенесён до coverage, поэтому его boundary отличается от C6. Успех tests,
builds и диагностики ещё не заявлен; gameplay/worker/quality policy не меняется.

C7 compile0/0, **184/184 tests passed за36.59 s**, обе builds success
**18.859 s**, job `5f3969b4bc1b435190c97f0b3fd8d949`;175 inputs/8 binaries
frozen. `MainLoopReleaseCycle7`10 diagnostic trials p99/20Hz passed,
**16788/16788** wall/cycle samples valid,0 errors, wall-sum error максимум
3.55e-15ms. Восемь frames>16.67ms,0>25ms,max **21.7729ms**. Во всех slow
frames85.4–98.5% интервала находится в previous sample→EOF; worst сегменты
**21.4524/0.2813/0.0382ms**. EOF не равен Present, поэтому это локализация,
а не установленная причина engine/OS/driver wait.

Следующий обычный flag-off `MovingAiReleaseCycle7A` **failed p99 gate,9/10**.
All trial5 p99 **21.037ms**, max76.5848ms; all trial4 max **297.4777ms**.
Worst tick230,555live/546visible, nativeCPU9.9899ms, maincycles34.51million,
ProcessCPU31.25ms, GCCollect0; callbacks0.7637/3.0616/3.8155ms не объясняют
wall. Флаг boundaries был выключен, нулевые default MainLoop fields не являются
измерениями. Эти невыгодные trials сохранены; диагностическая успешная серия
не заменяет failed ordinary gate. C7 не объявляется решением rare tail.

### C8 — native wait markers, отдельная диагностика

Editor discovery обнаружил пять wait/Present markers. Opt-in native-wait probe
должен отдельно найти их в player, записать raw nanoseconds/invocations,
availability и capture frame. All-thread/nested durations не суммируются и не
выдаются за exclusive main-thread wall time. Unavailable остаётся -1, не0.
Сначала compile/tests/build, затем Release/Development producing evidence.

C8 compile0/0, **184/184 tests passed за38.33 s**, обе builds success
**14.426 s**, job `6dc099845b244368be3ffffc64eb4489`;175 source inputs/8 binary
hashes и Cycle8Sources/Runtime frozen. Обычный flag-off gameplay не меняется.

`NativeWaitReleaseCycle8`: **17012 samples**, оба validators passed,10 trials
p99/20Hz passed;5 frames>16.67ms,2>25ms,1>50ms,max **79.1918ms**.
Selection1 trial2/tick36: raw segments **7.4455/71.5987/0.1477ms**, в длинном
post-EOF интервале **873698 cycles**, available GfxWaitForPresent marker0.0023ms,
GCCollect0/nativeCPU7.7905ms. Следующий кадр31.8418ms имеет24.9467ms
post-EOF. Состав/HP/death/AI/quality неизменны. В этом player DXGI Present и
TimeUpdate markers unavailable (-1), RenderThread/TargetFPS producing invocations0.
Не подменять unavailable нулём. Отдельный tick97 frame21.8057ms связан с
Gfx.WaitForPresentOnGfxThread11.2146ms; это не причина всех других выбросов.

`NativeWaitDevelopmentCycle8`:4 trials, **6309 samples**, native и boundary
validators passed, все p99/20Hz, max17.7229ms/0>25ms. Все пять recorder handles
доступны: DXGI/TimeUpdate producing samples подтверждены, RenderThread/TargetFPS
здесь имеют0 invocations. Их наличие в Dev нельзя переносить на Release.

Дополнительный `NativeWaitDevelopmentCycle8Long`:10 trials/16165 samples,
оба validators и p99/20Hz passed,max18.4544ms,0>25ms. Обычные flag-off
`MovingAiReleaseCycle8A/B`:20 trials/34678frames,все p95/p99/20Hz passed,
9 frames>16.67ms,0>25ms,max21.9345ms. P99 ranges selection1
8.7227–9.2498ms/all10.6146–13.2101ms. Hash/AI/quality прежние,0unfocused/clamps.
Это bounded наблюдение; оно не отменяет предыдущих failures.

C8 regression: headless1200ticks и technology31149ticks passed; standalone
291frames/0runtimeerrors проверил research cancel/refund/restart,save/load и
replay до624. Patrol selection1/all802 p95 9.0298/11.8338ms,0clamps.
Evidence: `gameplay-regressions-cycle8.json`, `StandaloneCycle8`, `PatrolCycle8`.

### C9 — наблюдаемый lifecycle compact marine visuals

`tick97-creation-diagnosis.json` сохраняет raw-сопоставление четырёх свежих
процессов C7/C8: first-trial slow frame следует через1–3 samples после
CreatedVisuals972→973 и publication675/651→666/633 live/observed. Новым visual
оказался повторно видимый marine829 owner1, а не новый тип модели. В том же
переходе девять морпехов гибнут,18 уходят из observation; View создаёт/удаляет
native roots/rings. Later trials имеют этот переход без аналогичного spike.
Это association lifecycle и first-use, не доказательство причины всех waits.

Следующий bounded candidate принадлежит BattleView: pool только ранее реально
наблюдавшихся compact marine visual по owner. Retirement снимает registration,
очищает snapshot/ID; reuse сбрасывает authored pose, transform, selection и shot
history. Не compact/fallback модели сохраняют прежний lifecycle. Уничтожение
View освобождает retained ресурсы. Created/Reused counters разделяются;
Fresh compile0/0, **191/191 tests passed за34.07s**, обе builds success
**17.025s**, job`eeb01481c9c64286bae6a5778db956f2`.177 source inputs/8binaries
и Runtime frozen в Cycle9Sources/Runtime. До финального suite сохранены два
failed attempts: native LineRenderer Color quantization test oracle (исправлен
сравнением fresh/reused native getters) и20s timeout прежнего600tick visibility
proof. Лимит последнего не меняли; fresh повтор всей suite прошёл.
Длинные graphical серии ещё ожидаются.

Первый `MovingAiReleaseCycle9A`:10 trials,p99/20Hz10/10 passed;12frames
>16.67ms,1>25ms,max25.3061ms. Short full800-band p99max23.278ms **failed**.
First-trial true creations278→14 (−94.96%),264reuse,poolend258; все counters
delta/alignment guards passed. Но tick97 после pooled activation вместо create
всё равно дал25.3061ms. Поэтому pooling подтверждает снижение native churn,
не установление/устранение причины recurring stall. Обычные повторения и
native-wait/Development проверки продолжаются; прежние failures сохраняются.

`MovingAiReleaseCycle9B`:10 trials p99/20Hz passed,max29.783ms. Во время run
другой агент разобрал большой C9A JSON (~10s CPU/memory work), поэтому вся серия
явно помечена known-background-analyzer, а не чистый acceptance. В C9A
selection1 trial4 ещё661unfocused frames; остальные trials сохраняются с
индивидуальной eligibility, raw outliers не удаляются.

`NativeWaitReleaseCycle9`:2 trials/3537samples,оба validators passed,полный
focus/0clamps,p99/20Hz/full800 bands passed. First-process frame903/tick98
**19.6479ms**, segments16.9322/2.6444/.0715ms, GfxWaitForPresent11.4308ms,count1,
View.5582/Submit2.8441ms,нет creation/reuse/matrix/collection deltas.
Tick97 раньше использовал pooled visual. Новый root как достаточное объяснение
recurring Gfx wait исключён этим экспериментом, сам wait не устранён.

### C10 — upload/draw health mesh scopes

Managed instancing retirement не уменьшает массивы и не освобождает последние
prototype leases. Следующий diagnostic owner — HudHealthBarBatch: отдельно
измеряются existing native mesh uploads и immediate draw,с actual
vertex/index/bar counts,shrink/empty и explicit GUI/source frame alignment.
Несколько Repaints агрегируются; exceptions сохраняют incomplete,stale samples
не объявляются current. Upload/draw являются частями GUI wall,не добавляются
к нему. Низкое wall время не исключит deferred GPU work. Mesh API order,geometry,
quality и gameplay остаются прежними. Fresh compile0/0,191/191 tests35.09s;
dual builds/graphical measurements ожидаются.

Обе C10 builds success12.266s,job`4c2b936726004602af28ba3b32770783`,177inputs/
8binaries и Cycle10Sources/Runtime frozen. `MovingAiReleaseCycle10A`10trials/
17589frames:полный focus,0clamps,все p95/p99/20Hz/full800/near720band gates
passed.6frames>16.67ms,0>25ms,max21.6823ms. Все17589health samples valid,
0stale/incomplete/errors. Firsttick97 geometry GROW408→416vertices,upload
.0166/draw.0180ms; следующий18.0752ms кадрSTEADY416,upload.0375/draw.0278ms.
Tick98 actualshrink416→392 upload.0318/draw.0254ms,wall6.8966ms.
Все шесть slowframes STEADY; худшийtick151 upload.0488/draw.0341ms.
Measured synchronous HUD-shrink bottleneck не подтверждён; deferred GPU work
этими scopes не исключается. Нового speculative product fix по ним не внесено.

`MovingAiReleaseCycle10A/B/C`: **30 clean trials/52661frames**,все30
p95/p99/20Hz/movementAI/full800/near720 gates passed. **9frames>16.67ms,
0>25/33.33/50ms**,longest burst1,max **21.6823ms**. P99 selection1
**8.6807–12.0176ms**,all **10.3343–10.8931ms**;p99.9max18.0752/13.5469ms.
Full800 p99max13.882/14.9097ms,near72012.9587/13.4597ms. Все52661health
samples passed,0stale/incomplete;focus полный,0unfocused/clamps,один прежний
hash/AI/movement/death. C actual creations14/reuse264 в каждом trial,все delta
checks passed. C tick97 growth408→416 upload.0389/draw.0402ms,wall6.348ms;
max18.3931ms tick91 steady392,upload.0218/draw.0225ms. Никакие предыдущие
failed/contaminated/outlier серии этим aggregate не заменяются. Это устойчивый
bounded p99 результат на текущем ПК,не гарантия отсутствия будущего rare spike.

Для дальнейшего scheduler/GPU анализа подготовлен и schema-validated узкий
Windows Performance Recorder profile (CPU scheduling,D3D11,DXGI,DxgKrnl;
без file/network/registry providers). После отдельного явного разрешения
пользователя выполнена одна короткая запись `NativeTraceCycle10`,PID34720,
ETL683671552bytes;WPR остановлен. Оба trace trials p99/20Hz passed,но
selection1 tick99/frame882 **25.4128ms**,preEOF22.9543ms,GfxWaitForPresent15.6804ms.
Этот run с системным tracing отделён от clean FPS acceptance. Capture script/
outputs ограничены ignored Builds,без внешней передачи. Разбор native events
ожидается. Mono timestamps около108million при10MHz имеют относительную эпоху;
равная частота ETL не доказывает одинаковый origin. До проверки alignment
прямое сложение/совмещение raw clocks запрещено.

C10 Development:2trials/3186samples,все3validators/p99/20Hz/full800 bands,
полныйfocus,0clamps,max16.7084ms. NativeGC **322949.50/321540.70bytes/tick**,
totals77507879/77169767,collections52/42. GCCollectmax2.1688/2.7183ms,
Incremental2.2877/1.4401ms. Все5markerhandles доступны; DXGI/TimeUpdate/Present
producing,RenderThread/TargetFPS0invocations. Не zeroGC и не Release timing.

Отдельный `NativeWaitReleaseCycle10`:3432validatedsamples/2trials,все
p99/20Hz/full800bands,полныйfocus. Worst **35.5023ms** tick213/frame1899,
preEOF34.9546ms,Prepare16.7947/Submit18.1748ms,GCCollect10.1788ms,
GfxWaitForPresent.0037ms;healthSTEADY520 upload.0300/draw.0226ms. GC-associated outlier,
но эти scopes могут перекрываться и не дают exclusive attribution всех35ms.
Он сохраняется отдельно от30 ordinary trials без>25ms; diagnostics не скрыты.

C10 standalone regression:290frames,0runtimeerrors;researchcancel/refund/
restart,save/load и replay до624 passed. `PatrolCycle10` selection1/all802:
1988/1492frames,p95 **9.0755/12.1519ms**,обаbudgetgates passed,0clamps.
Simulation source не менялся после C8;
1200tick headless и31149tick technology proof C8 сохраняются с собственным
frozen manifest,а новые C10 graphical regressions проверяют изменённый View.

### C11 — отложенное изменение GPU storage полос здоровья

Native decode:8.689millionevents,rawQPC10MHz,headerlost/bufferslost0. Pipeline
cadence использован только для candidate search;13076 фактических onCPU capture
timestamps,trial holdout и normal/slowholdout сузили Mono→ETL offset до набора
интервалов внешней шириной103.8µs. Conservative10µs tolerance даёт132.2µs range;
schedulermetrics проверены на всех piecewise extrema внутри range. Без point
epoch worstpreEOF22.9543ms имеет main ONCPU7.2995ms,blocked15.6484ms,
ready-not-running.0064ms. Явно названный `UnityGfxDeviceWorker` в этот момент
blocked14.9310ms,ready.0085ms. Это блокировка native graphics path,а не нехватка
квантаCPU у готового главного потока в этом кадре. Near userstack содержит
win32u→d3d11→atidxx64→d3d11→UnityPlayer;это предшествующий sampledstack,
не exact switchstack. Ultimate GPU/driver cause ещё не доказан.

Observed sourceFrame880 shrinks392→288vertices/588→432indices/49→36bars;
sample8817.9511ms,short upload.0377/draw.0414ms. Следующий source881 steady288
попадает в slow sample88225.4128ms. ETL names прямо связывают буферы
`Observed health rectangles`10976→8064 vertexbytes (28byte stride),2352→1728
indexbytes. New buffers reported15.70/15.77ms после началаGfxwait,oldstops15.87ms.
Deferred resize установлен; порядок не устанавливает,что именно resize вызвал
wait. Constant buffers создавались ранее и остаются отдельным неподтверждённым
кандидатом. DxgKrnl provider не дал events,несмотря на configuredprofile;
конкретная очередь/fence этим capture не атрибутирована. Компактные schemas,
calibration,scheduler,resource evidence иSHAmanifest: `NativeTraceCycle10/native-analysis`.

Следующий candidate у HudHealthBarBatch: grow-only native vertex/indexcapacity
по фактически observedpotentialbars;activeprefix uploads иsubmesh толькоactual
indices/bounds. Clip,colorFloat32,28byte vertexlayout,UInt32 indices иpixelspace
сохраняются. Spare storage не рисуется;empty no upload/draw. Logical counts,
storagecapacity иreserveevents учитываются отдельно. Fresh compile0/0,
**195/195tests36.04s** passed,включая4new hand-coded geometry/capacity/cleanup
tests. PublicDraw signature сохраняется;advanced Mesh API availability проверена
по Unity6000.3 docs. Mesh.vertexCount теперь отражает private storage capacity,
а actual submitted vertices/indices/bars остаются отдельными logical counts;
submesh0 толькоactualindices/bounds,subMeshCount никогда не уменьшается.
Empty не меняет старый native mesh и не рисует его. Layoutreserve events
послеwarmup будут измерены отдельно,не объявлены нулём заранее.

C11 bothbuilds success12.762s,179inputs/8bins frozen.
`MovingAiReleaseCycle11A` **failed performance**:p995/10,p956/10,
full800/near720bands3/10,796frames>16.67ms,225>25ms,max **201.776ms**.
12786healthsamples validated,полныйfocus/0clamps/hash/20Hz сохранены.
Reservecount1→1 во всех trials,nativecapacity8192/16384 stable. Это доказывает
capacity goal,но не FPS benefit; candidate **не принят**. Медленные участки
затрагивают View/Prepare/GUI,не только upload. Firstbadcyclemedian35–48million
против20–24million C10,часть frames имеет большие off-CPU-like wall gaps;
exclusive cause не установлен. Aggregate теперь явно отделяет ValidationStatus
от PerformanceAcceptanceStatus=failed. Raw failures сохранены полностью.

### C12 — резерв по фактически отрисованному пику

C11 physicalcapacity294912bytes против e.g13328actualbytes при49bars (~22x)
может дорого обходиться native driver map/discard,хотя directcause не доказан.
Следующий narrow candidate сохраняет тот же exactlayout/advancedAPI/submesh,
но резервирует GPUstorage по actualpayloadhighwater с geometricheadroom.
Managed preparation по maximumBars сохраняется. Большой potential count с одним
actualbar не должен увеличивать native capacity. Реальный новый пик первого
боя может вызвать reserve;счётчики его сохраняют,не превращают в artificialzero.
Updatedtests195/19536.6s,compile0/0 passed;обе builds success12.498s,
179inputs/8bins frozen. Short `NativeWaitReleaseCycle12`:3076samples,
health/boundary/native validators passed,focus полный,0clamps,hash прежний.
Оба trials проходят p95/p99/20Hz/full800/near720;all p99 **16.2435ms**,
max **44.8992ms**,4frames>25ms. Первый trial честно вырос128→1024 поactual
payload648vertices/972indices;all retained4096/8192. Все4>25ms имеют стабильные
capacities и короткие health stages;часть задержек postEOF,один кадр связан с
GfxWait14.4476ms. Capacity contract подтверждён,устранение stalls — нет.
Результат обычных Release C12 series безnative flags приведён ниже.

C12 ordinary A/B завершены: **20clean trials/32781frames**,всеp95/p99/20Hz,
movementAI/full800/near720 gates passed;11frames>16.67ms,2>25ms,0>33.33/50ms,
max **32.8942ms**,longestburst2. Focus полный,0clamps,hash прежний.
Оба>25ms — all trial5 C12B tick93,stable2920vertices/365bars,capacity4096/8192,
reservecount3;healthupload/draw.0277/.0299 и.0706/.0365ms. Broad Prepare scopes
9.5749/20.7456ms иshorthealth не устанавливают exclusivecause этих кадров.
Регрессия C11 в этой серии не повторилась;полный rare-tail fix не доказан.

### C13 — сохранение неизменного index prefix

HudHealthBarBatch при каждом repaint повторно отправлял одинаковые quad indices.
Каждый прошедший clipping прямоугольник добавляет полную4vertex/6indexquad;
topology зависит только от длины prefix. C13 загружает indices при новом actual
index highwater либо пересоздании index storage,перед activevertexupload.
Vertex positions/colors,actualsubmesh/bounds,quality иdrawcount неизменны.
Пустой кадр не отправляет и не рисует старые данные. IndexUploadCount — отдельный
счётчик успешных nativeindexuploads,не capacityreserve и не CPU/GPU timing.
Тесты проверяют новый prefix внутри свободной capacity,clipping/shrink/empty/
regrow и фактические Mesh triangles/colors/bounds. Fresh compile0/0,
**195/195tests36.01s**,обе builds success12.375s;179inputs/8bins frozen.
Обычные Release profiles завершены;меньше uploads само по себе не означает
устранение stalls.

`MovingAiReleaseCycle13A`: **10cleantrials/16704frames**,всеp95/p99/20Hz,
movementAI/full800/near720 gates passed,focus полный,0clamps,hash прежний.
7frames>16.67ms,0>25ms,max **20.9734ms**,longestburst3. P99 selection1
9.8954–11.4649ms,all11.0530–13.9363ms;full800p99max14.7693/13.6170ms.
Health/index proof16704/16704passed. Фактических indexuploads всего2:
первый selection1 наtick1 одновременно с128→1024capacity и первый all наtick1
при увеличении initializedprefix4800→5280indices внутри уже выделенной8192
capacity. Остальные8trials безindexuploads/reserves;сравнительно с прежней
policy не повторены16702indexAPIcalls,не claim физических bytes или FPS
causality. Source/binary manifest verification179/8,changed0.

Финальные ordinary `MovingAiReleaseCycle13A/B/C`: **30cleantrials/50545frames**,
всеp95/p99/20Hz/movementAI/full800/near720 gates passed.11frames>16.67ms
(3selection1/8all),0>25/33.33/50ms,max **20.9734ms**,longestburst3.
P99 selection1 **9.3438–12.4454ms**,all **10.8571–14.0427ms**;
full800p99max16.2196/14.6846ms,near72015.6884/15.2885ms. Focus полный,
0clamps,прежниеquality/hash/AI/deaths. Health/indexproof **50545/50545**
aligned/complete,0errors. Всего **6 successfulindexuploads/50545geometryuploads**,
по2legitactualhighwatercalls наfreshprocess;50539повторныхindexAPIcalls
не выполняются. Это доказанная экономия API-вызовов,не bytes или exclusive
причина улучшения FPS. `cycle13-release-series.json` хранит source/healthproof
SHA и все trials; C11/C12/ранние failures не исключены из их собственных серий.
Worst20.9734ms приpopulation718/observedvisible693,tick51; повторяемый transition
околоtick97 в C13C дал20.5998ms соsteadygeometry,безindexgrowth. Residual остаётся.

C13 fresh diagnostic Release/Development:3423/3239samples,p95/p99/20Hz passed,
max20.9221/17.6425ms,0>25ms. Их дополнительные scopes и Dev allocation counters
отделены отordinary Release-series; validation/GC evidence сохраняются в своих
`NativeWaitReleaseCycle13`/`NativeWaitDevelopmentCycle13` директориях.
Все6health/boundary/nativeproofs passed,full800/near720/focus/0clamps/hash
подтверждены. Dev allocations **325925.01/320847.21bytes/completedtick**,
52/41collections;Release allocation recorder unavailable,неzeroGC.
Releaseworstframe890/tick97:preEOF18.1902ms,GfxWait12.1227ms,healthsteady416v/
624i/upload.0167/draw.0220ms. Recurring native wait всё ещё измеряется,
ноexclusivecause не установлен. ReleaseDXGI/TimeUpdate recorders unavailable;
Devвсе5handles доступны. RenderThread/TargetFPS валидны с0invocations.

C13 `PatrolCycle13`:selection1/all802,1994/1449frames,p95 **9.1982/12.6617ms**,
p99 **10.6377/15.1406ms**,max14.9188/20.2358ms,0>25ms,focus полный,0clamps.
Actual800observed/patrolling/visible и1/800healthbars сохранены. У этого profile
нет явного RuntimeErrorCount;он не подменяется нулём. Standalone:292frames,
0runtimeerrors,researchcancel/refund/reentry,save/loadhash иreplayendtick623 passed.
Research HUD и800volley просмотрены вручную. Компактные hashes/assertions:
`regression-summary-cycle13.json`. Physicalmouse/keyboard playtest остаётся
отдельным scope,автоматическая проверка его не заявляет.

FinalEditor:ready,Play stopped,Frontier active/clean,compilefailedfalse;
freshcompile0errors/0warnings. Currentconsole0errors/1warning онеобязательном
RuntimePipelineConfig (Pipeline bridge disabled inPlayer);retainedconsolehistory
содержит прежние errors/warnings и не очищалась. WPR inactive;второй системной
трассы не было. Bounded gates этой сборки пройдены,ноevery-frame16.67ms и
ultimate engine/driver cause не доказаны;C10vsC13 timing не является isolatedAB.

Отдельная попытка application-only D3D11/DXGI capture с ProcessExeFilter
KopruluFront.exe безSystemCollector не стартовала: WPR0xc5580612/no providers
(target process ещё отсутствовал). Unfilteredfallback не выполнялся,WPR inactive;
в `GameGraphicsCycle11` сохранён лишь validatedprofile,не новый native capture.

| Gate | Статус |
| --- | --- |
| Source C0 и initial fixture | Подготовлены,70 Runtime files заморожены в BaselineSources/Runtime manifest |
| Unity baseline tests |163/163 passed,36.61 s |
| Baseline Windows build | Job f9ff598788b44e7c9b4ad24812803ae1 success,15.566 s |
| MovingAiRelease/DevelopmentBaseline | Измерены; movement/AI/20 Hz подтверждены, C0 final pause failed и rare dips сохранены |
| C1 fix / semantic proof / Release / Development |167 tests, pure exact proof и оба runs passed; исходные GC samples сохраняют известную warmup allocation |
| C2 source / pure equivalence | Frozen,240 states + Restore/replay/AI и4810 axis oracle cases passed; не Unity performance |
| C2 tests/builds |173/173 passed,36.43 s, compile0/0; обе builds success13.172 s |
| C2 expanded attempted-final series |10 trials измерены; p95/20 Hz10/10, p99 only9/10 — общий gate failed |
| C3 pose cache / parity / Unity |177/177 passed,34.16 s, compile0/0; read-only review без нового defect |
| C3 builds | Обе success12.142 s,171 inputs/8 binaries frozen |
| C3 expanded attempted-final Release |10 trials; p95/20 Hz10/10, p99 only9/10 — общий gate failed;5 frames>25 ms |
| C4 Development native GC/CPU diagnosis |4 trials измерены; counters calibrated, OS/GC causal claim не установлен |
| C4 Release worker ABAB | Mixed/inconclusive;4 не принят, default11 и GC3 ms сохранены |
| C5 affine math / parity / fresh suite/build/profiles |182 tests/dual builds passed;10 Release p99 passed, max187.8012ms сохранён |
| C2 Development native GC | Оба trials20 Hz/p99 passed;330151.01/332945.65 bytes/tick, не Release |
| C6 visible staging / proof / tests/builds | Exact240 states/Restore/replay/AI;184 tests/dual builds passed; pure Observe bytes−4.60% |
| C6 ordinary Release A/B |20/20 p99/20Hz,32901frames; max29.4451ms при800visible, full800 band failures сохранены |
| C7 boundaries / ordinary Release |16788 valid diagnostic samples; ordinary p99 failed9/10, max297.4777ms сохранён |
| C8 native wait Release/Development |17012 Release +22474 Dev samples validated; Release max79.1918ms, exclusive cause не установлен |
| C8 ordinary Release A/B |20/20 p99/20Hz,34678frames;9>16.67ms,0>25ms,max21.9345ms |
| C8 gameplay/technology/standalone/Patrol |1200/31149 ticks, standalone research/save/load/replay и обе Patrol selections passed |
| C9 observed compact visual lifecycle |191/191 tests34.07s,dual builds17.025s; creations278→14/reuse264; recurring stall сохранился |
| C9 ordinary A/B и native | A имеет один unfocused trial,B known background analyzer; native3537samples validated,max19.6479ms |
| C10 health owner / suite/builds |191/191 tests35.09s,compile0/0,dual builds12.266s; actual health upload/shrink bottleneck не подтверждён |
| C10 ordinary Release A/B/C |30 clean trials/52661frames,всеp99/20Hz/full800 bands passed;9>16.67ms,0>25ms,max21.6823ms |
| C10 Development/native/regressions |3186Dev/3432Release diagnostic samples validated;native35.5023msoutlier сохранён;standalone/Patrol passed |
| C11 retained buffers |195tests/dualbuilds;FPS failed5/10p99/max201.776ms,raw retained |
| C12 actual high-water |195tests/dualbuilds;20clean/32781frames всеgates,max32.8942ms;native44.8992ms retained |
| C13 immutable index prefix |195tests/dualbuilds;30clean/50545frames всеgates,max20.9734ms,11>16.67/0>25;6indexuploads |
| C13 native/Development/Patrol/standalone |Freshdiagnostic2+2passed;Patrolbothbudgets/research/save/load/replay623passed |
| Final manifests и выводы | C5–C13 frozen;C13verify179inputs/8bins changed0;systemtrace stopped/analysis finished,полный rare-tail success не заявляется |

Предыдущий frozen report: [iteration05](iteration-05.md). Очередь и следующий
ownership batch: [next-iterations](next-iterations.md).
