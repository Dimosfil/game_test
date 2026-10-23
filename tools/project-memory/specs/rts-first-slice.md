# Контракт первого игрового RTS среза

Продолжение: [orders/navigation](rts-orders-navigation.md) описывает расширения
prototype-terran-0.2 от 21.10.2026. Исходные результаты ниже датированы 20.10.
Технологии 0.3: [research/combat](rts-research-tech.md). Графические измерения:
[standalone contract](rts-render-profile.md).

Статус: реализован экспериментальный Terran-срез; финальные результаты прогонов
публикуются в [testing](../../../docs/sc2/testing.md). Это не полный баланс SC2.
Владелец интеграции — RTS integrator; продуктовый план — [plan](../../../docs/sc2/implementation-plan.md).

## Карта реализации

| Ответственность | Источник истины |
| --- | --- |
| Общие DTO, команды, integer X/Z | Assets/Game/Runtime/Simulation/Contracts.cs |
| Match lifecycle и Step | Simulation/Match.cs |
| Validation/ownership/sequence/order | Simulation/Match.Commands.cs |
| A*, swept collision, derived walkability cache | Simulation/Match.Navigation.cs |
| Ресурсы, supply, стройка/производство/отмена | Simulation/Match.Economy.cs |
| Damage, targeting, Stim, смерть | Simulation/Match.Combat.cs |
| Opt-in derived combat counters, excluded from save/hash | Simulation/Match.Diagnostics.cs |
| Research eligibility/costs/bonuses и validation | Simulation/Match.Research.cs, Match.ResearchValidation.cs |
| Observation и remembered buildings | Simulation/Match.Observation.cs |
| Bot scheduler, экономика/волны через команды | Simulation/Match.Bot.cs |
| Rules validation/hash, snapshots и replay | Simulation/Match.Persistence.cs |
| Авторские значения среза | Assets/Game/Resources/rts-rules.json |
| UI strings | Assets/Game/Resources/ui-ru.json |
| Catalog-derived UI production/build options and shortcuts | Runtime/Presentation/UiCommandCatalog.cs, Resources/rts-ui.json |
| Session frontend, save boundary, frame clock | Assets/Game/Runtime/GameHost.cs |
| Exclusive pure worker / detached frame publication | Runtime/SimulationFrameRunner.cs |
| Camera/input/selection/contextual commands | Runtime/Presentation/RtsInteraction.cs |
| Только observed world, fog, animation/shot feedback | Runtime/Presentation/BattleView.cs |
| Reusable observed-shot resources and current-observation lookup | Presentation/BattleView.cs: ObservedShotPool |
| HUD/menu/pause/results/minimap | Runtime/Presentation/BattleHud.cs |
| Observed world health rectangles / reused mesh and shader | Presentation/HudHealthBarBatch.cs, Shaders/HudHealthBars.shader |
| Content-driven technology graph / research controls | Runtime/Presentation/TechnologyPanel.cs |
| Процедурные models/world, без gameplay | Runtime/Presentation/ProceduralArt.cs и WorldArt.cs |
| Scene/build/real-rules smoke | Assets/Game/Editor/GameProjectTools.cs |
| Графический standalone smoke, opt-in flag | Runtime/Diagnostics/PlayerSmoke.cs |
| Render fixture / combined fixed-step CPU load | Runtime/RenderingProfile.cs, Diagnostics/GraphicsSimulationFixture.cs |
| Compact marine submission / independent matrix animation | Presentation/InstancedMarineRenderer.cs |
| Generated command/resource/tech icon atlas | Presentation/HudIconAtlas.cs, Resources/UI/command-icons.png |
| Real presentation-path 800-patrol profile | Diagnostics/SessionPerformanceProfile.cs |
| Real400+400 combat, moving AI, population/GC/clock/tail evidence | Diagnostics/SessionPerformanceProfile.Combat.cs |
| Opt-in early Update/resume/EOF timestamp and cycle boundaries | Diagnostics/MainLoopBoundaryProbe.cs |
| Полный actual-rules economic tech loop | Assets/Game/Editor/TechnologySmoke.cs |

## Поведение и инварианты

- Step исполняет приказы текущего Tick, затем продвигает Tick. Координаты в
  миллиметрах, timers в ticks. MakeCommand назначает sequence и execution tick;
  дубликат и смешанное владение отклоняются без повторной оплаты.
- Ресурсы поступают только после доставки. Сырой geyser не даёт газ; нужен
  завершённый собственный refinery. Начальные рабочие получают deterministic
  initial gather orders; bot использует player observation и общий pipeline.
- Build оплачивается один раз; рабочий должен дойти и продолжать стройку.
  Stop/смерть builder приостанавливает работу. Build с TargetId unfinished own
  building назначает нового строителя без повторной оплаты. Правый клик КСМ
  использует эту ветку. Полный refund cancel — политика экспериментального среза.
- Train резервирует supply, completed supply buildings предоставляют capacity.
  Производство использует доступный выход, rally задаётся Move на producer.
- Placement требует видимость всего footprint до проверки живой занятости.
  CanPlace — общий owner preview/execution; нет отдельной независимой UI-логики.
- Новые/отменённые/уничтоженные static blockers инвалидируют derived nav cache.
  Каждая следующая movement segment проверяется по живой геометрии. Занятая
  конечная точка завершается у достижимой границы, не вызывает вечный A*.
- Observe/Find/State отделены от live state. Observation не раскрывает enemy
  orders/production/cargo и ничей authoritative Path/PathIndex. Свой приказ
  остаётся виден. Remembered building health не обновляется из скрытого мира.
- Presentation получает только observation. Последний виденный target snapshot
  допустим для killing-shot feedback; полный authoritative Find для эффекта не
  используется. Preview не спрашивает скрытые footprints обходным способом.
- Потеря последней базы завершает матч один раз; последующие Step не меняют мир.

## Сохранения и повтор

CaptureState — глубокая копия включая bot scheduler/sequence, pending commands,
queues, terrain knowledge и allocator. Disk save GameHost использует UTF-8,
temporary file и atomic replace. Load сначала валидирует version/hash/rules,
затем заменяет match. Для automation SaveDirectoryOverride изолирует собственные
данные в evidence, не затрагивая обычный профиль.

Production snapshot validation (GI-A01, 2026-10-22): every nonempty queue belongs
to an owned, completed, non-resource building with no simultaneous research.
Each item is a trainable non-building/non-resource definition whose ProducerId
matches that building. TotalTicks and paid costs equal the hashed catalog;
remaining ticks are within 0..TotalTicks. Only the head can have progress; later
items retain their full duration. A zero remaining head is legal while its exit
is blocked. Restore validates these invariants without payment, refunds, live
affordability checks or requiring start-time prerequisites to survive. Train and
Restore share the producer/definition predicate. Malformed JSON queues fail
before replacing the active match; legitimate snapshots/replay retain schema 0.3.
ProductionRestoreTests covers corrupt producer/payment/timer, zero-head refund
and ordinary paid queue continuation/replay.

BeginReplay создаёт timeline на Tick 0 без повторного запуска AI. FromReplay
полностью проигрывает timeline для проверки конечного состояния. Host viewer
использует BeginReplay и реальный frame clock. Gameplay StateHash исключает
transport/command history и AI scheduling bookkeeping, поэтому сравним с replay;
save/load AI дополнительно проверяется по новым командам и полному JSON state.

## Границы и следующие задачи

Зрение круговое, без height/terrain LOS и cloak; одна стратегия бота. Все costs,
timings и формы этого каталога экспериментальные. Полные расы, campaign/Co-op,
сетевая сессия и UGC не реализованы. E1 network/determinism/performance objectives
и E2 hardware input/visual QA отмечаются отдельно от контрактных тестов.

Ограничения среды: интерактивный Windows input вернул GetCursorPos access denied;
постоянный Editor запускается в документированном batchmode. Поэтому camera-only
capture не считается проверкой HUD. PlayerSmoke запускает обычный graphical player,
проверяет кадр/IMGUI и public entry points; физическая мышь/клавиатура остаётся
отдельным пробелом до успешной проверки.

Evidence — игнорируемый Builds/Evidence; code/tests/scenes/catalog/material inputs
под Assets/Game. Шейдеры Standard/Sprites/Unlit сохраняются компактными Resources
materials при CreatePlayableScene. Сырые медиа и player output не коммитятся.

Проверено 20.10.2026: 30/30 EditMode, actual-rules 1200 ticks с одинаковым
исходным/restored/replay hash, успешная Windows-сборка. Финальный обычный player
smoke: exit0, HUD PNG1600×900, 230 кадров, 0 runtime errors, pause/save/load,
viewer до конца Tick609 с hash исходного save. F5/F9 обрабатываются до pause gate;
конец незавершённого replay получает нейтральный текст. Подробное evidence и
оставшиеся пробелы — [testing](../../../docs/sc2/testing.md).
