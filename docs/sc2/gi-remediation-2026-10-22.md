# Исправления по аудиту ГИ

Дата: 22 октября 2026 года. Объём — GI-A01–GI-A08 из
[аудита](gi-audit-2026-10-22.md). **Все восемь пунктов исправлены и проверены.**
Игровой ruleset/save/replay остаётся prototype-terran-0.3.
Независимая работа C14–C17 и её performance evidence сохранены.

| Пункт | Исправление и проверка |
| --- | --- |
| GI-A01 Restore | Train и Restore используют общий predicate producer/unit. Проверяются владелец, готовность, отсутствие research-конфликта, configured costs/duration и прогресс очереди. Нулевой готовый head допустим для занятого выхода. 16 новых контрактных случаев прошли. |
| GI-A02 provenance | Новый rts-manifest проверяет полный состав source/player trees и все Runtime asmdef DLL, включая Simulation. Изменённая DLL, отсутствующая DLL и новый незаписанный файл отклонены в трёх fixture-проверках. Старые манифесты не изменены. |
| GI-A03 инструкции | Из pinned commit a1c737f9ae204e44f3d10361219c10fd66b10ef0 принятой версии 2026.10.17.1 восстановлены 15 недостающих файлов с учётом транзитивных ссылок. Проверка 62 instruction files / 43 routes проходит. Pending migrations 0. |
| GI-A04 UI bindings | Production берёт ProducerId из каталога; меню, порядок и строительные клавиши вынесены в rts-ui.json. Кнопка выбранного типа правильно разрешает производителя при смешанном выделении. Новый catalog producer/unit проверен через настоящий Match.Train. |
| GI-A05 локализация | Тексты, статусы, форматы стоимости/времени, причины отказа и АТК/БР находятся в ui-ru.json. Названия клавиш в captions формируются из input layout. Русских строк в Presentation C# больше нет. |
| GI-A06 RAG | Вместо фиктивного backend работает локальный stdlib SQLite/FTS5: source hashes, heading chunks, bounded search, freshness, export и три retrieval evals. Python задаётся через PATH/GI_PYTHON/игнорируемую local-конфигурацию. Vector/services отключены. |
| GI-A07 архитектура | В architecture-migrations.md записан фактический переход iteration04 на worker/публикацию снимков, с прежней границей, инвариантами, совместимостью и ссылками на сохранённые проверки. |
| GI-A08 runner | До тестов проверяются identity/Play/scenes/занятость. Setup сохраняется до recompile, finally отменяет незавершённый запрос и восстанавливает сцены только при изменении. Пустая выборка и watchdog timeout возвращают ошибку с восстановленной Frontier; dirty/untitled work отклоняется без потери изменений. |

## Проверки

Свежий финальный прогон: **231/231 за 37,16 с**, failed/skipped/inconclusive 0.
Source manifest зафиксирован после compile и проверен после suite:
**200 файлов, ноль изменений и точное совпадение состава**. Frontier восстановлена
и проверена: active, clean, один root, Play stopped. Прежний 195-test audit
snapshot этим результатом не подменяется.

Отдельно проверены корректное производство после JSON/replay, неправильные
producer/payment/timer, нулевой таймер/возврат, новый контент каталога, выбор
производителя, неверные UI bindings и переводы причин. Защитные отрицательные
запуски runner не включаются в число успешно пройденных игровых tests.

На время новых performance players другой работы сборки/общая suite не запускались.
Собственный dirty-scene fixture был опознан и закрыт после подтверждённого отказа;
пользовательские сцены не сохранялись и не отбрасывались.

Изолированные Release и Development сборки успешно завершились в
`Builds/Players/GiVerification/Windows` и `WindowsProfile`. Полный binary manifest
проверяет **394 файла**, включая обе Simulation DLL, Runtime DLL, engine/plugin
зависимости и content. Исходные 200 файлов не изменились и после сборки.

Обычный оконный smoke прошёл: **291 кадр, 0 runtime errors, replay завершился
ровно на Tick 626**, snapshot/save/load/replay hashes одинаковы. Настоящий research
fixture загрузился; cancel/refund/restart и progress HUD прошли. Просмотрены
worker-hud.png и technology-progress.png: подписи, строительные кнопки, прогресс
и переведённые причины читаются без обнаруженного перекрытия.

Build jobs с проверкой BuildReport, полный binary manifest, source manifest и
standalone smoke сохранены в `Builds/Evidence/GiRemediation/2026-10-22/`;
итоговые ссылки/хеши — в `verification-summary.json`. Производительность и hardware input этим scope
не принимаются. Исторические отсутствовавшие хеши Simulation DLL восстановить
сегодняшними хешами невозможно; прежние C13–C16 записи остаются частичными.

CLI обновлён до beta.14, current/latest совпадают. Editor/Pipeline оставлены
6000.3.24f1 / 0.8.0-exp.1. Свежий официальный MCP probe прошёл initialize,
tools/list (160) и status/scenes; инструменты Editor callable в текущем чате.
Новая системная трасса, второй bridge, внешняя служба, commit/push не выполнялись.

## Поддерживаемые контракты

- [UI команды и ресурсы](ui-commands.md).
- [Production/save contract](../../tools/project-memory/specs/rts-first-slice.md).
- [UI binding contract](../../tools/project-memory/specs/rts-ui-commands.md).
- [Lifecycle, manifests, kit и local retrieval](../../tools/project-memory/specs/integration-contracts/development-tooling.md).
- [Архитектурная история](../../tools/project-memory/architecture-migrations.md).
- [Команды проверки](../../tools/AGENT_RUNBOOK.md).

## Проверка после C21–C22

Повторная ограниченная проверка 22.10.2026 охватывает WindowsCpuProbe,
BattleHud, SessionPerformanceProfile.Combat и GuiThreadCycleEvidenceTests.
Новых нарушений в просмотренном коде не найдено: native cycle reads включаются
только диагностическим флагом, недоступные/частичные интервалы не выдаются за
измеренный ноль; Screen.height сохраняется только внутри одного Repaint.
Межсобытийного кеша, изменения правил, графического качества или GUI cadence нет.
Смена высоты посреди одного callback не доказана пиксельно идентичной прежним
повторным getter reads; следующие Repaint получают новую высоту.

Свежая сверка C22: **204 source files и 394 binary files** совпали с полным
набором путей, длинами и SHA256. Сохранённые тесты C21 — 246/246 за 38.1s,
C22 Retry1 — **246/246 за 35.91s**, compile 0/0, Frontier восстановлена чистой.
Это проверка существующих evidence, а не новый запуск Unity. Исходный итог
231/231 выше остаётся историческим результатом исправлений GI-A01..GI-A08.

Пробелы найдены в актуальности: README/testing/stack вели к прежним результатам,
локальный FTS отставал от четырёх изменённых документов. Указатели исправлены;
SQL/chunk export обновлены через существующие локальные helpers; индекс current,
четыре retrieval evals прошли, включая поиск C21 контракта WindowsCpuProbe.
Новые внешние сервисы и системные трассы не запускались.

Канонические [C21–C22 контракты](../../tools/project-memory/specs/rts-moving-ai-performance.md)
уже описывают владельцев, opt-in/validity, Repaint/resize и ограничения evidence;
переписывать их не потребовалось. [Текущая итерация](current-iteration.md)
сохраняет неблагоприятные результаты: строгая FPS цель не достигнута,
повторяемый выигрыш C22 не установлен, один trial focus-qualified.
Подготовленный C23 trace не выполнялся и не считается закрытием этой цели.
