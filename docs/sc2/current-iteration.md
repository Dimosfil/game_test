# Итерации RTS

Актуальный C23 завершён на прежних source204/binary394 и качестве
1600×900/4×MSAA/Ultra/shadow100/D3D11. **Strict FPS цель FAILED.**
Обычные A/B/C: **30 боёв/52025 кадров**, 10 >16.667ms, 7 >20ms,
6 >21ms, 4 >25ms, max **196.3688ms**. Whole p95/p99/real-time20Hz,
full800/near720 gates 30/30; это не every-frame гарантия. A:17353/1slow/
max20.0207ms; B:17175/8/max196.3688ms; C:17497/1/max19.5261ms.
Пять all-selection trials B имеют 6958 unfocused frames и сохранены отдельно.
Focus/source-eligible **25 боёв/44232 кадра** всё ещё дают 8 >16.667ms,
6 >20ms, 5 >21ms, 3 >25ms, max196.3688ms; все percentile/band/20Hz gates25/25.
Clamps/source events0; gameplay hash прежний. GUI proofs passed: ordinary
native query attempts0. Exact health:7096 native uploads/44929 reuse.

Худший B selection1 trial5 frame10091/tick165 полностью focused1757/1757:
GCCollect available0ms/0calls; previous source10090 View.8209ms, GUI1.0263ms,
Prepare2.2541ms, Draw.9111ms; health exact reuse без роста storage/index.
Ordinary MainLoop/native waits выключены: остаток196ms не локализован.
[C23 ordinary summary](../../Builds/Evidence/Iteration06/Cycle23SeriesAnalysis/cycle23-ordinary-summary.json),
[worst neighbours](../../Builds/Evidence/Iteration06/Cycle23SeriesAnalysis/cycle23-worst-neighborhood.json).

Отдельная единственная согласованная C23 CPU/GPU trace завершена и остановлена:
4 diagnostic боя/6425 кадров, 2 >16.667ms, **0 >20ms**, max17.5083ms.
Target tail не воспроизведён; фактическое native coverage4928, остальные1497
после остановки trace. Анализ ограничен5 covered targets+5 matched controls;
свежие anchors/identity и6 proofs passed. Source watcher отсутствовал (events
unavailable), pre/post204/394 совпали; focus/clamps0. Эти native данные другого
процесса не объясняют ordinary196ms. [Trace proofs](../../Builds/Evidence/Iteration06/NativeTraceCycle23/c23-proof-fps-summary.json),
[scheduler](../../Builds/Evidence/Iteration06/NativeTraceCycle23/native-analysis/scheduler-verdict.md),
[GPU limits](../../Builds/Evidence/Iteration06/NativeTraceCycle23/native-analysis/gpu-review.md).

C24 в работе: opt-in watchdog и minidump только собственного GAME-процесса,
без external app data, новой system trace или снижения качества. Это диагностика,
не FPS fix и не заявленный passed test. Необязательный minimized-UI контроль
остаётся без ответа и не выполнялся.

Предыдущий C22 завершён: retry suite 246/246 за 35.91s, compile 0/0,
обе builds прошли; source204 и оба binary manifests394 проверены.
Единственный production diff от frozen C21 — callback-local Screen.height:
2+N native getter calls →1 за Repaint, то есть N+1 вызовов убраны.
Root сохраняет этот bounded cleanup; качество и draw/input cadence сохранены.

ABAB **8 боёв**: control 4/5920 кадров, 11 >16.667ms, 4 >20ms, 0 >25ms,
max 22.1452ms; candidate 4/5895, 6/3/0, max 23.5397ms. **Оба strict FAILED.**
Whole p95/p99/20Hz 4/4 у обеих групп; bands control2/4, candidate4/4.
WorldLabels scope gain смешанный: selection1 medians −2.1%/−3.0%,
all −0.7% в первой паре и +9.7% во второй. Повторяемого FPS lead для30
боёв нет. CandidateD selection1 focus-qualified: 1622/1623 focused,
1 unfocused; остальные7 боёв полностью focused, clamps/source events0.
Gameplay hash/качество прежние, GUI native read attempts0.
[C22 evidence](../../Builds/Evidence/Iteration06/Cycle22SeriesAnalysis/cycle22-abab-summary.json),
[source review](../../Builds/Evidence/Iteration06/Cycle22Verification/implementation-review.md),
[quality review](../../Builds/Evidence/Iteration06/Cycle22Verification/runtime-quality-review.md).
Устранение всех FPS tails не достигнуто. Последующие C23 результаты приведены выше.

Предыдущий C21 diagnostic завершён: 246 tests, compile 0/0; frozen manifests
204 source/394 binary entries. Обычная серия: **10 боёв/14691 кадр**,
21 >16.667ms, 7 >20ms, 0 >25ms, max 23.0099ms; whole p95/p99/20Hz 10/10,
full800/near720 gates 7/10. Unfocused/clamps/source events 0, прежние gameplay
hash и качество 1600×900/4×MSAA/Ultra/shadow100. **Strict FAILED.**
Сохранены band failures: selection1 trial2 full800 p99 22.6827ms;
all trial3 near720 p99 16.9023ms; all trial5 full800 p95/p99
16.7148/19.7779ms и near720 p99 19.1320ms. Ordinary GUI-cycle scopes выключены:
новые read attempts/successes 0, availability false, что подтверждено proof.

Отдельный native diagnostic: 2 боя/2472 кадра, max 33.0446ms; GUI cycle,
boundary/native-wait/exact-health proofs passed. В matched controls GUI
17.1469ms/9251060 cycles против median 3.1864ms/10673314 cycles;
WorldLabels 9.7001ms/3286236 против 1.1269ms/3079652. Совпадают capture tick,
population, selection и GUI/Repaint counts; это не перевод cycles в ms
и не доказательство scheduler cause. [C21 ordinary](../../Builds/Evidence/Iteration06/Cycle21SeriesAnalysis/cycle21-ordinary-summary.json),
[matched cycle evidence](../../Builds/Evidence/Iteration06/Cycle21SeriesAnalysis/cycle21-cycle-owner-summary.json),
[GUI cycle proof](../../Builds/Evidence/Iteration06/NativeWaitReleaseCycle21/gui-cycles-proof.json).
Последующим шагом стал локальный Screen.height cleanup C22; результаты выше.
FPS fix этими проверками не подтверждён.

Предыдущий C18: 238 tests, 7 functional tests и dual builds за 12.423s прошли.
Frozen candidate manifests: 202 source/394 binary entries; control: 200/146.
Сравнение source inputs подтвердило единственный production diff в BattleHud
и добавление test/meta; остальные UI/resources сохранены. В paired ABAB
**8 боёв** отдельно от длинной серии: control 6838 кадров/9 >16.667ms/6 >25ms,
max 34.9216ms; candidate 6954/4/0, max 19.7982ms. Source-frame SelectionCapture
median selection1/all снизился 0.1233/0.1757 → 0.0027/0.0105ms,
p95 0.1980/0.2839 → 0.1109/0.1559ms. Это подтверждённое снижение scope,
а не статистическое доказательство FPS non-regression или полного успеха.

Обычные C18 A/B/C: **30 боёв/50384 кадра**, 68 >16.667ms, 22 >20ms,
10 >25ms, max 37.8034ms; unfocused/clamps/source events 0, gameplay hash прежний.
P95/real-time20Hz 30/30, p99 29/30, full800/near720 gates 27/30.
Сохранены adverse gates: A all trial3 full800 p95/p99 16.6755/17.4221ms
и near720 p99 16.9008ms; B all trial3 whole p99 17.5508ms;
B all trial5 near720 p95/p99 20.0567/25.5862ms;
C all trial5 обе полосы p95/p99 19.5737/24.5928ms.
Exact health proofs passed: 7070 native uploads/43314 reuse. **Strict FAILED:
устранение всех кадров 20–21ms не достигнуто.** Native diagnostic C18
рассматривается отдельно. [C18 evidence](../../Builds/Evidence/Iteration06/Cycle18SeriesAnalysis/cycle18-summary.json).

C19/C20 startup/build experiments отклонены для расширения до 30 боёв.
C19 ABAB: default 4/6492 кадра, 9 >16.667ms, max 23.0686ms;
direct 4/4945, 29 >16.667ms, 2 >25ms, max 35.4769ms, p99 3/4.
Actual modes подтверждены: ClientWorkerJobs → Direct; medians whole frame
хуже в обеих direct сериях. C20 default 4/5788, 4 >16.667ms, max 22.4065ms;
Graphics Jobs off 4/5332, 30 >16.667ms, 6 >25ms, max 31.5107ms,
p99 3/4, bands 2/4; actual mode Threaded. C20D selection1 имеет
1 unfocused из 1448 кадров и исключён из clean acceptance; связь конкретного
outlier с потерей focus неизвестна. Первый C20 A/B contrast полностью focused
и также показывает худшие jobs-off medians. Качество и product defaults
сохранены; bounded ABAB не доказывает единственную причину FPS tails.
[C19 evidence](../../Builds/Evidence/Iteration06/Cycle19SeriesAnalysis/cycle19-abab-summary.json),
[C20 evidence](../../Builds/Evidence/Iteration06/Cycle20SeriesAnalysis/cycle20-abab-summary.json).
После этих отклонённых вариантов выполнена opt-in C21 thread-cycle диагностика
(результаты выше). C19/C20 не закрыли strict FPS цель.

Предыдущий C17: 230 tests за 39.58s и dual builds за 15.293s прошли;
заморожены 189 inputs/8 binaries. C17A сохранён отдельно: 6 событий записи
Runtime, возможная фоновая компиляция Editor; он не включён в B/C/D.
Три серии B/C/D: **30 боёв/50126 кадров**, unfocused/clamped frames 0; 42 >16.667ms,
9 >20ms, 1 >25ms, max 32.3311ms. Whole p95/p99/20Hz — 30/30;
full800/near720 p95/p99 — 28/30: B all trial3 near720 p99 17.5335ms,
B all trial5 full800/near720 p99 18.8577/18.3710ms. **Strict gate FAILED:
устранение всех кадров 20–21ms не достигнуто.** 7069 native vertex uploads,
43057 exact reuse; pooled health-preparation medians selection1/all —
0.0127/0.0525ms. B/C/D не зарегистрировали Runtime/Resources writes, но
Editor использовал в среднем около 1.2 CPU cores за окно launcher;
это фоновый контекст, не доказанная причина просадок. Следующее расследование —
анализ имеющейся ETL активности Editor, не новый product fix.
[Компактные результаты C17](../../Builds/Evidence/Iteration06/Cycle17SeriesAnalysis/cycle17-bcd-summary.json).

Отдельный контроль C17 E/F/G с закрытым Editor: **30 боёв/41391 кадр**,
474 >16.667ms, 144 >20ms, 48 >25ms, max 96.3815ms. P95 — 29/30,
p99 — 16/30, full800/near720 bands — 20/30, real-time 20Hz — 30/30.
Unfocused/clamped frames и source events — 0; Editor отсутствовал,
его CPU delta недоступен/null, не измеренный ноль. Exact proofs прошли:
6472 native uploads/34919 reuse. Отдельная native diagnostic серия:
2808 кадров, max 39.9139ms, boundary/native-wait/exact-health validators passed.
Закрытие Editor **не устранило просадки**; B/C/D и E/F/G остаются отдельными,
нерандомизированными сериями и не доказывают причинность разницы.
[EFG evidence](../../Builds/Evidence/Iteration06/Cycle17SeriesAnalysis/cycle17-efg-editor-closed-summary.json),
[native diagnostic evidence](../../Builds/Evidence/Iteration06/Cycle17SeriesAnalysis/cycle17-editor-closed-native-summary.json).
Editor восстановлен official Unity open, PID 23464, Frontier проверена clean;
start-unity был заблокирован устаревшим isRunning. Необязательный контроль
со свёрнутым UI ожидает решения пользователя. Strict FPS цель не достигнута.

Ниже сохранён предыдущий отчёт C16.

Текущая [итерация 06](iteration-06.md): начальные 800, реальное движение/бой
и обычный AI, с сохранением HUD/fog/minimap и качества.
C16 пока **не принят**: 197 tests/compile 0/0/dual builds прошли, inputs/binaries
179/8 заморожены. Обычные 30 боёв/49315 кадров: полный focus, 0 clamps,
52 >16.667ms, 8 >25ms, max 34.8935ms; strict gate failed. Также сохранены
full800 p99 failure в C16A selection1 trial1 и whole p99 failure в C16C all trial5.
7098 vertex uploads и 42217 exact reuse сокращают повторные vertex API calls
примерно на 85.6%, но FPS улучшение не доказано. Native C16 ещё выполняется,
сравнение с C15 запланировано; качество и Repaint draw cadence сохранены.

Продолжение C14/C15 требует≤16.667ms для каждого измеренного кадра в длинных сериях.
C14 fog-кандидат отклонён; код возвращён к C13:0пропущенных nativeuploads.
C13 control clean max37.5949ms также сохраняет хвосты. C15 — только диагностика:
10боёв/16334кадра,24>16.667ms,3>25ms,max27.5312ms. Strict цель ещё не закрыта;
C15 Unity Profiler join с offset −1 проверен на обычных/holdout samples:
2000 loaded frames, 50/55 slow hierarchies, measured truncation 0. Нового
системного захвата не было; для него потребуется новое согласие.

C5 ускоряет подготовку
рендера; C6 уменьшает временные массивы observations; C9 повторно использует
ранее наблюдавшиеся compact visuals после fog/death. C13 сохраняет native buffers
полос здоровья по actual high-water и повторно использует подготовленные индексы:
6 index uploads вместо повторения на всех 50545 загрузках геометрии. Для C13
195 tests за36.01s, compile0/0 и обе builds за12.375s прошли; manifests179/8
заморожены. Три обычные Release-серии: 30 боёв/50545frames, все p95/p99/20Hz,
movement/AI/full800/near720 gates прошли, focus полный,0clamps. Остались
11 кадров>16.67ms,0>25/33/50ms,max20.9734ms,максимальная серия3кадра.
Это принятие ограниченных gates новой сборки, не устранение каждого long frame
и не доказательство причинного FPS-улучшения относительно C10.
Отдельные native Release3423/Development3239samples прошли validators/p99/20Hz;
max20.9221/17.6425ms. Невыгодные C11/C12 сохранены: C11 failed, C12 clean
max32.8942ms и diagnostic44.8992ms. Исторический C10:30clean боёв/52661frames,
max21.6823ms. Разбор единственной разрешённой CPU/GPU трассы завершён:
main/Gfx waits подтверждены, причинность health-buffer resize не доказана,
данных очереди DxgKrnl нет. Запись остановлена; второй системной записи не было.
[Итерация 05](iteration-05.md) сохранила настоящий400+400 бой, targeting,
pooling эффектов и экономию выделений в HUD/simulation как отдельный AI-off baseline.
[Итерация 04](iteration-04.md) добавила compact instanced rendering,
worker simulation и UI-иконки, проверив 800 патрулей с HUD/fog/minimap.
Rules/save/replay остаются prototype-terran-0.3.
[Итерация 03](iteration-03.md) добавила исследования оружия/брони и tech tree.
Актуальные результаты — [testing](testing.md); дальнейшая очередь —
[next-iterations](next-iterations.md).

Ниже сохранён контракт первой итерации; он не заменяет текущие результаты.

Продолжение от 21.10.2026: [итерация 02](iteration-02.md), групповое движение,
патруль и ремонт. Результаты ниже относятся к первой сборке 20.10.

Дата: 2026-10-20. Статус: первый экспериментальный срез E1/минимального E2
реализован и собран для Windows. Полные gates E0–E2 ещё не закрыты.

Сценарий: запуск → выбор матча → рабочие добывают → игрок строит и обучает армию →
разведывает и сражается с AI → исход → сохранение/загрузка и повтор.
Механизм: чистая детерминированная C# simulation, валидируемые команды, фиксированный
шаг, runtime catalog, grid navigation и player observations; Unity только отображает.
Владелец правил: Simulation/Match; владельцы presentation и Editor — интегратор.

Это собственный экспериментальный Terran-срез, не подтверждённый полный баланс SC2.
Параметры и UI title берутся из версионированного JSON, его происхождение отмечается.
Модели процедурные и приближённые; числовая и графическая идентичность не заявляется.

## Границы параллельной работы

- Simulation agent: `Assets/Game/Runtime/Simulation/`, кроме согласованного Contracts.cs.
- Art agent: `Assets/Game/Runtime/Presentation/ProceduralArt.cs` и `WorldArt.cs`.
- QA agent: `Assets/Game/Tests/`, тестирует публичный Match API.
- Интегратор: runtime host/input/HUD, resources JSON, Editor automation, сцены,
  CLI/MCP, build, интеграционные проверки и документация.
- Общие DTO: `Contracts.cs`. Изменения только после сообщения интегратору.

## Публичный Match API

Namespace `ColonyRTS.Simulation`. Координаты — миллиметры X/Z; cell — один метр.
`Match(RuleSet rules, bool enableEnemyAI = true, int seed = 42)` создаёт матч из spawns.
`State`, `Rules`, `LastResults` доступны только для чтения потребителями.
`Definition(string id)`, `Find(int id)`, `Observe(int player)` читают состояние.
`MakeCommand(int player, CommandKind kind, int[] ids = null, SimPoint target = default,
int targetId = 0, string definitionId = null, bool append = false)` выдаёт новую sequence.
`QueueCommand(GameCommand command)` ставит приказ; `Step()` валидирует и исполняет.
`CanPlace(string definitionId, SimPoint position, int player)` проверяет placement.
`CaptureState()` возвращает глубокую копию состояния.
`static Match Restore(RuleSet rules, MatchState state)` проверяет hash/version.
`ExportReplay()` работает с принятыми командами. `BeginReplay` начинает viewer
на Tick 0; `FromReplay` воспроизводит всю запись для сверки. `StateHash()`
детерминированно учитывает gameplay.

Симуляция не зависит от Unity. JSON загружает runtime boundary через JsonUtility.
Replay playback не запускает заново AI: источник — сохранённые команды обеих сторон.
Save/load сохраняет работающий AI и его sequence/think state. Observe не выдаёт
скрытые enemy positions; EntityState в observation — detached read model.

## Проверки первой итерации

Пройдены Unity compile и 30/30 EditMode tests. HeadlessSmoke с реальными rules
прошёл 1200 ticks: добыча/стройка/производство, восстановленный работающий AI и
повтор дают одинаковый конечный gameplay hash. Windows BuildReport — success.
Обычный graphical standalone подтвердил HUD/геометрию/камеру, 600+ ticks,
PNG 1600×900, pause/save/load и продвижение replay по кадрам, без runtime errors.
Детали и актуальный регрессионный результат: [testing](testing.md).

Закрыты найденные дефекты: начало viewer на Tick 0, возобновление стройки КСМ,
placement oracle в fog, утечка authoritative path, устаревший navigation cache,
пропавший lethal-shot feedback, F5/F9 на паузе, ложное поражение в конце записи.

GUI Editor упёрся в модальное предупреждение; Windows input automation вернула
GetCursorPos access denied. Подключён persistent batchmode Editor через официальный
CLI/MCP. В нём нет Game View render target; camera-only снимок не выдаётся за HUD.
Скрытый standalone не получил IMGUI repaint и отмечен failed; обычный оконный
запуск прошёл. Ручной input playtest и performance/network experiments остаются
открытыми. Evidence и кадры — игнорируемый `Builds/Evidence/`.
