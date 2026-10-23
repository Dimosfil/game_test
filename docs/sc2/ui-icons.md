# Игровые UI-иконки

Статус: raster atlas получен и подключён в коде итерации 04; Unity import,
compile и graphical smoke выполняет интегратор.
GameHost → BattleHud.Initialize → однократный HudIconAtlas.Initialize загружает
`Resources/UI/command-icons`. HUD и TechnologyPanel используют тот же atlas и
`GUI.DrawTextureWithTexCoords`; PNG не режется и не переписывается кодом.
Сигнатура UV draw проверена по [Unity 6000.3 API](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/GUI.DrawTextureWithTexCoords.html).

Сценарий → механизм → владелец → проверка:

- Выбор сущности/приказ/строительство → прежние public Interaction entry points
  и authoritative gates → BattleHud меняет только presentation → compile и
  graphical smoke тех же кнопок с иконками и читаемыми названиями.
- Tech graph/research → ResearchAvailability и наблюдаемые queues/upgrades →
  TechnologyPanel отображает, Simulation решает доступность → locked/available/
  in-progress/completed и replay/pause checks.
- Icon identity/UV → фиксированная 6×4 row-major схема → HudIconAtlas → 24 ячейки,
  top-left origin корректно переведён в bottom-left UV, без соседнего bleed.

## Atlas contract

Ожидается 1536×1024 PNG: 6 столбцов × 4 строки, 256×256 pixels на ячейку.
Допускается кратный размер с теми же квадратными ячейками. Порядок enum:

| Строка сверху | 0 | 1 | 2 | 3 | 4 | 5 |
| --- | --- | --- | --- | --- | --- | --- |
| 0 | Worker | Marine | Depot | Barracks | Refinery | EngineeringBay |
| 1 | Armory | Command | Move | Attack | Stop | Hold |
| 2 | Patrol | Repair | Build | Rally | Stim | Cancel |
| 3 | Weapons | Armor | Technology | Minerals | Gas | Supply |

Стиль — тёмный промышленный sci-fi: steel/cyan, сдержанные amber highlights,
выразительный предмет в центральных 70%, без надписей внутри raster. Полные
названия, цены, состояние и hotkeys сохраняются текстом рядом с изображением.
Для turret применяется generic Build tile с явной подписью «Автотурель».

Resources.Load выполняется только при bootstrap. GUIContent tooltip и GUIStyle
кэшируются; draw не загружает Resources и не создаёт Texture2D/Sprite для ячеек.
Отсутствующий/некорректный atlas оставляет подписи и действия работоспособными,
но такой запуск не закрывает visual icon gate.

Геометрия интерактивных областей, включая TechnologyButtonRect, не меняется;
иконки не изменяют ввод RtsInteraction. Карта, fog, simulation rules и save schema не
зависят от графических файлов. Generated PNG остаётся project-local; Git commit
медиа не выполняется.

Источник — встроенный ImageGen, новый atlas, созданный для этой игры; интегратор
проверил расположение всех 24 изображений. Файл:
`Assets/Game/Resources/UI/command-icons.png`, 1536×1024.
SHA256: `e66fe066e2384fecc4d711c3c8db2430c6482aeb0080f4e7d9f3bb78acd3ee9a`.
Пиксели после генерации не редактировались. Требуемый Texture import: Default/sRGB,
no mipmaps, no compression, max size 2048, NPOT None, Clamp/Bilinear, ReadWrite off;
это настройки импорта, а не изменение исходного PNG.

Иконки используются в resource header, selected portrait/group, build/train/
research/action buttons и technology graph. Подписи и hotkeys не убираются;
disabled state сохраняет gate симуляции, иконка затемняется. Tooltip content и
стили кэшируются; technology styles пересоздаются только при смене UI scale.
Набор не добавляет самостоятельную команду Move/Rally: эти иконки доступны
для соответствующих presentation states, прежний контекстный ввод не меняется.
Проверено в итоговой оконной сборке 21.10.2026: `StandaloneFinal/player-smoke.json`
прошёл 12 checks, включая реальный atlas, worker/action panel, technology graph,
research cancel/restart/progress, save/load/replay, без runtime errors. Снимки
`Builds/Evidence/Iteration04/StandaloneFinal/worker-hud.png` и
`technology-progress.png` просмотрены: иконки читаемы, подписи и состояния
сохраняются, ресурсы/header не перекрываются. В `SessionFinal/session-selected800.png`
проверены group portrait, resource/command icons и HUD на нагрузке 800 патрулей.
Реальный atlas/import integration входит в итоговые 140/140 Unity tests.

## Prompt генерации

Встроенный `image_gen`, новый единый atlas, без входных изображений. Передан
следующий prompt (PNG после генерации только скопирован в Resources):

```text
Use case: stylized-concept. Asset type: ONE production-ready UI icon atlas for an original industrial science-fiction real-time strategy game, a SINGLE image asset containing exactly 24 square illustrated action icons in a precise 6-column by 4-row grid. Landscape canvas 1536 by 1024, each tile exactly 256 square, edge-to-edge uniform alignment. Every tile has the same near-black navy background and a restrained beveled gunmetal square frame; central subject stays inside central 70% of tile with generous dark padding, crisp high contrast recognizable at 40 pixels. Premium hand-painted/3D-rendered RTS command palette, steel blue/cyan highlights with amber accents. NO text, NO letters, NO numerals, NO watermark, NO gaps between tiles. Exact ordered subjects from left to right, top to bottom: ROW1 a yellow industrial worker exosuit; a blue heavy armored rifle infantry helmet; a compact supply storage bunker; a military barracks building; a gas refinery with green vapor; an engineering workshop with a crane. ROW2 a military armory building with armor racks; a fortified command headquarters; a cyan movement arrow; a red targeting crosshair; a stop hand; a shield marking hold position. ROW3 a circular patrol arrow around a waypoint; a metallic repair wrench; a construction hammer; a rally flag; a glowing stimulant syringe; a red cancel cross. ROW4 a futuristic infantry rifle for weapon research; a reinforced breastplate for armor research; a branching technology circuit; brilliant blue mineral crystals; a green gas canister; three supply personnel silhouettes. Render every icon as a fully distinct tile, exact flat contact-sheet layout with consistent framing, no perspective applied to the overall atlas. These are game interface assets, not a mockup of a screen. Do not add captions or change cell order.
```
