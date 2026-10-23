# Исследование документации StarCraft II для проекта Unity

Дата: 20 октября 2026 года. Изучены публичные первичные руководства и схемы. Факты ниже отделены от предлагаемых решений нашего [плана](implementation-plan.md).

## Проверенные источники и применение

| Источник | Что подтверждает | Применение |
| --- | --- | --- |
| [Blizzard 5.0.17 Patch Notes](https://news.blizzard.com/en-gb/article/24309308/starcraft-ii-5-0-17-patch-notes) | Опубликован live 5.0.17; большинство изменений 5.0.16 возвращено к 5.0.15, старт рабочих снова 12 | Предлагаемый pinned melee baseline; patch notes — дельта, не вся база чисел |
| [Происхождение Editor Guides](https://s2editor-guides.readthedocs.io/) | Старые официальные tutorials и подробные guides сообщества при поддержке Blizzard | Механизмы/authoring, без гарантии текущего баланса |
| [Standard Dependencies](https://s2editor-guides.readthedocs.io/New_Tutorials/01_Introduction/013_Standard_Dependencies/) | Отличаются campaign и multiplayer dependencies | Раздельные rulesets и inventories режимов |
| [Data Editor Introduction](https://s2editor-guides.readthedocs.io/New_Tutorials/04_Data_Editor/058_Data_Editor_Introduction/) | Разделены каталоги gameplay, campaign, actors, terrain и UI | Каталог definitions и общие исполнители вместо одного большого Unit |
| [Actors](https://s2editor-guides.readthedocs.io/New_Tutorials/04_Data_Editor/060_Actors/) | Локальные image/audio actors не определяют синхронизированный gameplay; описан fog presentation | Разделение исхода матча и изображения |
| [Units](https://s2editor-guides.readthedocs.io/New_Tutorials/04_Data_Editor/059_Units/) | Связи юнита с компонентами; несколько оружий и турели | Отдельные проверки turning, acquisition и attack timing |
| [Effects](https://s2editor-guides.readthedocs.io/Classic_Tutorials/03_Data_module/2/), [Validators](https://s2editor-guides.readthedocs.io/New_Tutorials/04_Data_Editor/071_Validators/) | Связи способностей с эффектами и проверками, в том числе ongoing | Общий lifecycle и валидация cast/impact/ongoing |
| [Footprints](https://s2editor-guides.readthedocs.io/New_Tutorials/04_Data_Editor/073_Footprints/), [Custom Footprints](https://s2editor-guides.readthedocs.io/New_Tutorials/04_Data_Editor/074_Custom_Footprints/) | Различаются footprint, pathing и placement checks | Размер mesh не заменяет правила размещения и движения |
| [Basic Unit Controls Blizzard](https://news.blizzard.com/en-us/article/4552956/game-guide-basic-unit-controls) | Приказы имеют разные правила поведения | Контракт Move/Attack/Stop/Hold/Patrol и очередей |
| [Terran Add-ons Blizzard](https://news.blizzard.com/en-us/article/5740265/game-guide-terran-add-ons) | Tech Lab/Reactor, смена подключённого здания и производство | Явная модель add-on; исторические числа сверяются отдельно |
| [SC2API overview](https://github.com/Blizzard/s2client-proto) | API управляет существующей SC2, используется для ботов и анализа повторов | Исследовательский адаптер, не игровой движок Unity |
| [sc2api.proto](https://github.com/Blizzard/s2client-proto/blob/master/s2clientprotocol/sc2api.proto) | Version metadata, create/join/observation/action/replay flows | Manifest эталона и наблюдаемые сценарии |
| [data.proto](https://github.com/Blizzard/s2client-proto/blob/master/s2clientprotocol/data.proto) | Данные unit types, abilities, weapons, стоимости/supply/технологий | Основа инвентаризации; не вся семантика взаимодействий |
| [raw.proto](https://github.com/Blizzard/s2client-proto/blob/master/s2clientprotocol/raw.proto) | Unit tags, orders/targets/queue flag, visibility/creep, pathing/placement/height, snapshots | Собственные IDs/commands, карты и observation; не доказательство внутренних алгоритмов |
| [Protocol documentation](https://github.com/Blizzard/s2client-proto/blob/master/docs/protocol.md) | Seed и детерминизм; replay исполняет ввод; требуется правильная binary/data version; API actions могут отличаться от replay actions | Собственные versioned snapshots/replay и диагностика divergence |
| [s2client-api](https://github.com/Blizzard/s2client-api), [API reference](https://blizzard.github.io/s2client-api/) | C++-обёртка наблюдений/управления и примеры ботов | Возможный reference harness, без обязательной зависимости продукта |
| [Blizzard s2protocol](https://github.com/Blizzard/s2protocol) | Декодирует replay events, не предоставляет balance data и high-level analysis | Дополнительный анализ, не замена игре и каталогу |
| [Unity fixed updates](https://docs.unity3d.com/6000.3/Documentation/Manual/fixed-updates.html), [Profiler](https://docs.unity3d.com/6000.3/Documentation/Manual/profiler-introduction.html) | Расписание fixed updates и измерения Unity | Собственный clock и profiling; FixedUpdate не доказывает детерминизм |
| [Unity AI Navigation](https://docs.unity3d.com/Packages/com.unity.ai.navigation@2.0/manual/index.html), [Entities](https://docs.unity3d.com/Packages/com.unity.entities@1.4/manual/index.html) | Кандидатные пакетные технологии и их workflow | Оценка в spike; пакеты не установлены, решение не принято |

## Выводы для реализации

Публичные материалы позволяют восстановить наблюдаемые контракты, структуру данных и много отдельных правил. Наиболее важны приказы/отмена, добыча/маршруты рабочих, tech prerequisites, асимметрия производства, timing оружия, способности, групповое движение, обзор и обнаружение. Конкретные acceptance cases нашего проекта приведены в плане.

Расы требуют разных процессов поверх общих ресурсов, эффектов и validation. E0 фиксирует реестр всех сущностей/форм/способностей и неизвестные поля. Подтверждённые числа конкретного content batch обязательны до его переноса как эталонного; ядро может развиваться с явно тестовыми данными. Чтение tutorials не заменяет полную проверку каталога.

Разделение simulation/presentation — наше решение, поддержанное описанием Actors. Авторитетный сервер, C#-структуры, алгоритм пути, собственный replay и бюджет FPS — проектные предложения, а не утверждения о технологии Blizzard.

## Границы доказанности

Установленный base build/data version SC2 не измерен. Полный каталог актуального baseline не выгружен, дифференциальные матчи не проведены. Бинарники, исходники движка и оригинальные медиа не скачивались.

Официальная [страница SC2](https://starcraft2.blizzard.com/) перечисляет режимы в поисковом индексе; прямое открытие во время исследования было недоступно. Разделение campaign/multiplayer дополнительно проверено по Standard Dependencies. Карточка [GDC AI Navigation](https://gdcvault.com/play/1014514/AI-Navigation-It-s-Not) подтверждает участие James Anhalt из Blizzard, но видео здесь не просмотрено: конкретные алгоритмы доклада не считаются установленными фактами.

Каждому неподтверждённому полю нужны источник, единица, версия и сценарий. Без эталона сохраняется «не проверено», а не значение из памяти или другого дополнения. Это блокирует заявление о точном соответствии, но не независимую разработку ядра.
