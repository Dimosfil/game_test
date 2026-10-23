# Контракт воссоздания StarCraft II

Дата: 2026-10-20. Статус: полный целевой контракт; первый экспериментальный
срез реализуется отдельно в [first-slice contract](rts-first-slice.md).
Владелец: интегратор RTS. [План](../../../docs/sc2/implementation-plan.md),
[исследование](../../../docs/sc2/reference-research.md).

## Цель и границы

Пользователь запросил исследование и план, затем явно поручил начать реализацию,
восстановить Unity/MCP и итерационно разрабатывать с агентами.
Полная продуктовая цель не подменяется первым skirmish.

Предлагаемый baseline: Legacy of the Void Versus 1v1 live 5.0.17. Локальные
base build/data hash ещё не проверены. Campaign/Co-op имеют отдельные rulesets.
Полнота roster, карт, миссий и audiovisual inventory фиксируется в E0.

## Пользовательский workflow

Запуск player → выбор режима/карты/расы/слотов → проверка версий → загрузка →
управление/добыча/развитие → разведка/бой → исход → результат/replay/меню.

Несовместимая конфигурация отклоняется до старта с причиной. Недопустимый приказ
не списывает ресурсы и не выполняет запрещённый эффект. Терминальный исход
выдаётся один раз; старый матч не изменяет меню либо следующий матч.

## Владение состоянием

| Область | Единственный владелец | Разрешённые границы |
| --- | --- | --- |
| State, IDs, tick, RNG | Core simulation | Изменения только в заданной фазе Step; внешним слоям read models |
| Definitions и числа | Content catalog | Validated read-only runtime catalog для simulation/UI |
| World layers/blockers | World/navigation | Placement/path/visibility queries |
| Ресурсы/резервы/очереди | Economy/production | Операции с определённой атомарностью |
| Damage/effects/death | Combat/abilities | События для presentation без обратной мутации |
| Player knowledge | Visibility | UI, AI, network view читают PlayerObservation и разрешённые PlayerEvents |
| Save/replay schema | Persistence | Версии и полнота restore проверяются до загрузки |
| Авторитет и доставка | Multiplayer host | Validated commands, filtered observations |

## Предварительные публичные контракты

Это семантика будущих границ, не существующие C#-типы. До независимой
реализации закрепить поля и ошибки в схемах и contract tests.

- `MatchConfig`: simulation/schema version, ruleset/content hash, map hash,
  seed, time model, slots. Display version без hash недостаточна.
- `CommandEnvelope`: match/player IDs, sequence, requested tick, command type,
  entity IDs, target union, queue mode. Host проверяет ownership и назначает
  accepted execution tick. Дубликат не исполняется повторно; late policy явная.
- `CommandResult`: accepted/rejected, причина, tick, область действия. Для
  группы задать atomic/per-unit policy и причины частичного исполнения. Цена
  и возврат принадлежат production/ability owner. Публичная причина отказа не
  раскрывает неизвестный вражеский объект; полный diagnostic reason — host only.
- `Step`: единственный проход изменения состояния за tick. Стабильны команды,
  фазы систем, обходы и применение jobs. Wall clock, Unity input, Transform и
  global random не являются источниками игровых правил.
- `EntityId`: generation либо эквивалентная защита; старый target не попадает
  в новую сущность после reuse индекса.
- `NavigationRequest/Result`: layer, radius, start/goal, map revision;
  path/partial/unreachable. Stale result перепроверяется. Порядок завершения
  фоновых работ не меняет outcome.
- `PlacementQuery`: footprint, height, occupation, запрещённые зоны,
  power/creep. Preview и execution используют одну семантику; мир проверяется
  повторно в execution tick.
- `PlayerObservation`: свои сведения и разрешённые чужие; visible/explored/
  last-known/detection различаются. Снимок врага не обновляется скрытыми данными.
- `MatchEvent`: tick, sequence, смысловой payload. Presentation отображает
  событие, не изменяет симуляцию. Полный authoritative поток хранится отдельно;
  Visibility проецирует его в `PlayerEvent` с разрешённой аудиторией/сведениями
  до передачи presentation/network. Скрытые ID/координаты, damage/cast/death/audio
  не обходят fog. Replay/observer получают сведения по своей явной policy.
- `Snapshot/ReplayHeader`: версии/hashes, tick, RNG, ID allocator, очереди,
  timers/effects/projectiles, blockers, ресурсы, visibility/knowledge, AI planner
  state и mission/trigger/match-local progression state. Их восстановление из
  других полей допустимо только с доказанным эквивалентным продолжением. Restore
  на границе tick; replay содержит accepted commands и checkpoints. Межмиссионный
  профиль получает отдельную versioned save schema в E8.

## Инварианты и отказы

1. У числового правила один catalog owner. UI/prefab не дублирует цены.
2. Ресурсы/supply/резервы согласованы при отказе, повторе, отмене, смерти/morph.
3. Жизненные циклы приказа, производства, cast/channel/expire задают interruption
   и refund по эталону; расовые отличия не стираются общим исполнителем.
4. Footprint/lift/death обновляет pathing. Unreachable отличается от stuck;
   recovery не телепортирует сквозь препятствия без игровой способности.
5. Vision, targetability и detection отдельны; UI/AI не читают скрытый мир.
   Фильтрация охватывает snapshot, event/audio stream и command error payload.
6. Кандидатная authoritative сеть передаёт filtered state. Lockstep с полным
   миром не обещает защиту этого мира от модифицированного клиента.
7. Replay требует совместимых simulation/content/map и фактических команд.
   Совместимость с форматом SC2 не заявляется.
8. Save/load сохраняет незавершённую работу; несовместимость не маскируется
   частичной загрузкой.
9. AI использует те же команды/observation. Форы, если есть, задаются явно.
10. Смерть/исход обрабатываются однократно; stale references отклоняются.

## Верификация и текущая реализация

Для механизма обязательна цепочка scenario → mechanism → owner → source/test →
evidence. Статус всех будущих контрактов — planned. Механическое, числовое,
визуальное и контентное соответствие проверяются отдельно.

Начальные сценарии: добыча с исчезнувшей базой; cancel/refund; рампа с новым
blocker; cast/impact/cancel; cloak/detection/last-known и event/payload visibility;
build order каждой расы; replay/hash; snapshot с эффектами; save/load с работающим
AI и сравнением его новых команд; mission/trigger restore после E8; два клиента
при задержке/duplicate; standalone.

Первичная проверка 2026-10-20 обнаружила пустой Assets и недоступный Pipeline.
После авторизованного восстановления постоянный Editor отвечает на CLI и MCP;
исходники и тесты созданы под Assets/Game. Текущие результаты/пробелы и точные
команды — в runbook/testing, поведение среза — в first-slice contract. Evidence и
players хранятся в игнорируемом Builds. Внешние источники: [register](integration-contracts/connected-projects.md).
