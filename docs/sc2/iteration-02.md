# Итерация 02 — управление и массовое движение

Дата: 21.10.2026. Статус: итерация завершена в объёме prototype-terran-0.2;
56 контрактов, нагрузочные сценарии и graphical Windows smoke пройдены.
Полные gates E0–E3 и дальнейшая цель остаются открытыми.

## Контракт работ

Сценарий: игрок ведёт группу через препятствия, задаёт патруль, ремонтирует
повреждённую технику/здания КСМ, сохраняет матч и воспроизводит команды.
Первый цикл улучшает navigation и измеряет массовое движение; второй добавляет
новые приказы и их отображение. Готовность требует общей компиляции, контрактов,
проверок реальных rules и новой graphical Windows-сборки.

- Simulation/Navigation владеет path, swept collision, unit broadphase и
  endpoint slots. Групповой приказ распределяет точки стабильно по ID, без
  чтения скрытой живой занятости для публичного order target. Один юнит сохраняет
  прежнее назначение. Если места для всей группы нет, приказ отклоняется атомарно.
- Simulation/Commands/Combat/Repair владеет новыми приказами. Patrol циклически
  идёт от origin к target, прерывается для боя и возвращается к маршруту. Shift
  после Move использует его endpoint как origin. Stop заменяет приказ.
- Repair принимает только собственных завершённых workers против собственного
  завершённого повреждённого Repairable target; саморемонт запрещён. Движение
  до контакта, затем оплачиваемые циклы из RuleSet. Нет средств — нет оплаты/HP,
  приказ ждёт. Полный HP/смерть цели завершает приказ. Все проверяется до мутаций.
- PathTarget/PathStoppingDistance входят в snapshots и determinism; Observation
  скрывает их вместе с path. Смена назначения после chase перестраивает path.
- Input/HUD использует те же команды. P — patrol, Y — repair; context repair
  использует только observed target. Replay разрешает камеру/выбор, без приказов.
- Каталог prototype-terran-0.2: repair 6 HP за 10 ticks, 1 mineral за цикл,
  0 gas; это экспериментальные значения. Старые snapshots с другим hash/version
  отклоняются; миграция сохранений предыдущего прототипа не обещана.
- У Patrol с queued successor исходящий участок конечен: достигнуть target,
  снять текущий order, затем выполнить следующий. Только последний Patrol
  циклический. Это устраняет принятую, но недостижимую очередь после Patrol.
- Building Move+visible resource TargetId сохраняет resource rally; новый КСМ
  начинает Gather. Ground rally сбрасывает ID. Невидимые ID не меняют назначение.

## Владельцы параллельных областей

Navigation agent: Match.Navigation.cs/Match.GroupMovement.cs. UI agent:
RtsInteraction.cs/BattleHud.cs/ui-ru.json. Независимый QA: новые тесты и
NavigationBenchmark.cs. Интегратор: DTO/commands/combat/repair/observation,
каталог, PlayerSmoke, документация и все Editor/build операции.

## Проверки

Патруль туда/обратно, combat/resume, Shift, Stop; ownership и саморемонт,
HP/cost/funds; snapshot/replay новых приказов; группа минимум 16 без наложения
и прохода сквозь wall; измерения 200/400/800 объектов с arrived/stuck и tick
percentiles. Watchdog отмечает failure, а не придумывает исход матча.
Измерения Editor отдельно от standalone frame budget. Не объявлять прежний
ориентир 400 units/60 FPS выполненным только по скорости headless simulation.

Evidence: игнорируемые Builds/Evidence; окончательный результат будет записан
в testing.md после интеграции. Аппаратный Windows input ранее блокировался
GetCursorPos access denied и не считается проверенным через API вызов.

## Промежуточные прогоны

Первый Unity прогон: 40/41 passed, 16-unit arrival failed. Сохранён в
Builds/Evidence/Iteration02/initial-editmode.json. После исправления terminal
steering/endpoint spacing: 49/49 passed (13.03s), включая новые Patrol queue,
Repair, route-redaction и resource-rally regressions. Большой benchmark отдельно
выявил низкую пропускную способность; идёт следующая итерация сглаживания пути.
Первое измерение сохранено в Iteration02/initial-navigation-benchmark.json;
это baseline уже с spatial cache, а не исторический исходный код 20.10.

Следующий прогон: 50/50 passed (12.33s), включая занятую промежуточную точку.
Реальные rules 0.2: 1200 ticks и live-AI/save/replay passed. Затем длительный
pure-C# прогон обнаружил остаточные1/200,1/400,1/800: oscillation stateless
avoidance и sampling blind spot возле rounded terrain corner. Для них добавлены
ещё три независимых recovery test; финальный результат ниже не подменяется этими
промежуточными прогонами.

## Итоговая регрессия и измерения

Финальная Unity-компиляция без ошибок; 56/56 EditMode passed, 14.2s.
Реальные rules 0.2: HeadlessSmoke 1200 ticks passed; final/restored/replay hash
`57681d3d14b84d4ced55f1e23dd642918fd52ca6c76a65b85a8f8c366680bee4`.
Независимый review не подтвердил P0/P1; terminal-connector concern воспроизведён
через публичный Match и покрыт тестом Move→Shift-Move с exact swept geometry.
Planner может выдать неидеальный connector, который исправляет локальный обход;
это P2 к качеству маршрута, а не закрытый вопрос глобальной навигации.

Изолированный Unity Editor, Ryzen 5 2600, 128×96 synthetic map, seed 1841,
runner radius 350 mm / speed 4000 mm/s. Baseline — 600 ticks (30 sim seconds),
extended — 1800 ticks (90 sim seconds), критерии/fixture не менялись.

| Movers | Arrived600 | Tick p95 / max600 ms | Arrived1800 | Stuck1800 |
| --- | --- | --- | --- | --- |
| 200 | 200 | 2.97 / 52.10 | 200 | 0 |
| 400 | 393 | 6.53 / 75.95 | 400 | 0 |
| 800 | 558 | 29.16 / 404.13 | 800 | 0 |

Sampled overlaps/wall intersections — 0; watchdog не сработал. Проверка 16 юнитов
проверяет геометрию каждый tick; большие cases — по объявленным samples.
Желаемый полный frame/tick budget НЕ закрыт: 800 stress и maximum latency
массового приказа требуют профилирования/следующей оптимизации. Editor benchmark
не доказывает60 FPS Windows player. Точные JSON лежат в Builds/Evidence/
navigation-benchmark.json и navigation-benchmark-extended.json; предыдущие
неуспешные варианты сохранены в Iteration02/. Source-manifest с SHA256 входов
связывает этот выпуск с исходниками без выдуманного Git commit.

Следующие исполнимые пакеты: [next-iterations](next-iterations.md).

Windows BuildReport successful. Финальный player smoke: passed/exit0,
245 frames, zero runtime errors, tick 616 save/load/replay hash одинаков.
Проверены реальные кадры army и worker HUD с кнопками P/Y и 8 worker actions.
Патруль выдан через продуктовый command entry point и сохранён активным.
Это не подмена аппаратного ввода. Повторный Computer Use увидел окно/UIA, но
window capture дважды timed out и coordinate input geometry unavailable;
gameplay input остаётся непройденным. Тестовое окно закрыто, Editor оставлен ready.
