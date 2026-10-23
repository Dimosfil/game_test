# Контракт измерения графики RTS

Владелец harness — `Assets/Game/Runtime/RenderingProfile.cs`, namespace
`ColonyRTS`. Точка входа: `RenderingProfile.StartProfile(GameObject host)`.
`GameHost` вызывает её при `-rtsProfile` **до** обычной инициализации матча,
камеры, карты, input и HUD, после чего выходит из обычного bootstrap.
В Editor этот режим не запускается.

## Повторяемый запуск

Текущий массовый бой/CPU/память: [iteration-05](iteration-05.md).
Команды настоящего боя (AI выключен только в этой фикстуре):

```powershell
.\tools\run-rts-player.ps1 -Kind combat -CombatTrials 5 -CombatTicks 60
.\tools\run-rts-player.ps1 -Kind combat -CombatTrials 1 -CombatTicks 240
.\tools\run-rts-player.ps1 -Kind combat -CombatTrials 1 -DevelopmentProfile
```

Два выбора, независимые бои400+400, пауза только до измерения; обычные
HP/damage и естественные потери. Отдельны p95/p99 бюджета кадра и20Hz throughput,
все кадры и полосы населения800/>=720, Release и Development GC. В финальных
10 Release-прогонах05 худший p99 —15,54мс, но отдельный max —26,29мс:
это ограниченный бюджет по p99, а не гарантия каждого кадра. AI/движение вместе
требуют отдельного сценария06. Детали и raw evidence — в отчёте05.

Предыдущий CPU/память пакет: [iteration-04](iteration-04.md). Для сравнения
прежней Shared hierarchy и compact backend использовать **одну** сборку:

```powershell
.\tools\run-rts-player.ps1 -Kind profile -ProfileMode optimized -RenderBackend legacy -Simulation -ProfileSeconds 15
.\tools\run-rts-player.ps1 -Kind profile -ProfileMode optimized -RenderBackend instanced -Simulation -ProfileSeconds 15
.\tools\run-rts-player.ps1 -Kind profile -ProfileMode optimized -RenderBackend instanced -Simulation -DevelopmentProfile -ProfileSeconds 15
.\tools\run-rts-player.ps1 -Kind session
.\tools\run-rts-player.ps1 -Kind session -DevelopmentProfile
```

`-rtsProfileBackend legacy|instanced` независим от Shared/Unique mesh mode;
instanced требует `optimized`, иначе запрос отклоняется. При неподдерживаемом
GPU explicit instanced profile завершается ошибкой, а не успешным fallback.
В обычной игре marine использует compact с аппаратным fallback.
`-ProfileSeconds 0..30` задаёт минимальное время измерения **каждого** сценария
в дополнение к минимуму 180 кадров; буферы резервируются до timed interval.
LogicalRendererCount и authored geometry сравниваются отдельно от physical
Renderer/Transform: compact намеренно уменьшает их количество.

Session profile проходит настоящие GameHost, BattleView, fog, minimap, HUD,
input и 20Hz loop. Фикстура добавляет 800 marine к стартовым spawns, очищает
terrain blockers и выключает enemy AI только для этого opt-in запуска, чтобы
сохранить 800 патрулей без боя. Два интервала по 15 секунд: выделен один marine
и вся армия. Требуются actual camera/GUI frames, все 800 центров в открытой
игровой области, 800 активных Patrol, отсутствие боя и real-time tick throughput.
Это проверка полного presentation path, не полного combat/AI match.
`Meets60FpsP95` означает p95 <=16,67 мс; `passed` означает корректный прогон,
а не автоматически достигнутый FPS. Время, spikes и allocations публикуются
отдельно; Development и Release не смешиваются.

Итог итерации 04 при прежней rules version `prototype-terran-0.3`:
парный synthetic combined legacy/compact p95 при800 — 29,1968/14,2488мс.
Настоящий session с800Patrol, HUD/fog/minimap и20Hz: p95
11,4067мс(selection1)/14,2755мс(selection802); all-selection p9917,3361мс.
Это достижение ограниченного p95 бюджета, а не каждого кадра или полного боя.
Текущие серии и source/binary manifest — `Builds/Evidence/Iteration04/`;
подробные метрики, GC и границы — [отчёт итерации04](iteration-04.md).
[Итерация03](iteration-03.md) и `Iteration03/*Verified/` сохраняют историческое
сравнение Unique/Shared; это другой этап оптимизации.

После `GameProjectTools.BuildWindows()` запускать пары одной Release-сборки:

```powershell
.\tools\run-rts-player.ps1 -Kind profile -ProfileMode baseline -EvidenceDirectory C:\work\game_test\Builds\Evidence\Rendering\Baseline
.\tools\run-rts-player.ps1 -Kind profile -ProfileMode optimized -EvidenceDirectory C:\work\game_test\Builds\Evidence\Rendering\Optimized
```

Для combined добавить `-Simulation` к обоим вызовам. Для native GC allocations
сначала собрать `GameProjectTools.BuildWindowsDevelopmentProfile()`, затем
добавить `-DevelopmentProfile -Simulation`; выбрать отдельный evidence directory.
Development timings не смешивать с Release. Launcher явно открывает обычное
окно, ждёт завершения с watchdog и проверяет свежесть/статус результата.

Запускать обычный оконный Windows player в foreground. Нельзя использовать
`-batchmode`, `-nographics` или скрытый/minimized window: они не доказывают
реальную работу render loop. Нужны свободные 180 секунд до watchdog.

Параметры:

- `-rtsProfile` включает отдельную runtime-сцену бенчмарка.
- `-rtsEvidencePath <absolute-directory>` выбирает каталог результата внутри
  `Builds/` текущего checkout. По умолчанию —
  `Builds/Evidence/Rendering/baseline`. Runtime находит `Builds` по предкам
  расположения player; machine-specific пути в исходниках отсутствуют.
- `-rtsProfileMode baseline` сохраняет исходные уникальные combined meshes.
  `optimized` использует shared combined geometry с lease/refcount. Выбор
  неизменяем для созданного экземпляра; глобальный режим продукта не меняется.
- `-rtsProfileSimulation` добавляет независимый synthetic CPU workload через
  `ColonyRTS.Diagnostics.GraphicsSimulationFixture`: фиксированный шаг 20 Hz,
  группа из 200/400/800 бойцов и detached observation после каждого шага.

При сравнении baseline/optimized сохранять разные evidence directories и
идентичность сборки. Использовать одинаковые hardware, graphics API, build type, разрешение,
quality/shadow settings, framing и число юнитов. Сравнивать render-only с
render-only; combined — только с combined. Повторять прогоны при потере focus,
clamping или нерепрезентативном системном фоне.

## Нагрузка и границы измерения

Сценарии идут последовательно: **200, 400, 800** настоящих procedural marine,
две командные расцветки. Legacy использует `CreateForProfile` hierarchy;
instanced использует `CreateInstanced` с теми же mesh/material parts и poses.
Все модели получают реальную walk animation, без стрельбы. Построение ассетов и завершение Destroy
происходят до измерения; их стоимость создания указывается отдельно.

Камера перспективная с постоянными FOV/положением и проверкой, что центры всех
юнитов входят в кадр. Постоянная сетка из 40 колонок, расстояние 1,5 м.
Режим задаёт 1600×900, VSync=0, targetFrameRate=-1, Built-in lighting fixture,
4× MSAA, soft shadows. Фактические значения и оборудование записываются в JSON.

На сценарий: минимум **90 warmup frames + 180 timed frames**.
Перед первым сценарием — минимум две секунды settle и подтверждённые camera
`OnPostRender`/IMGUI Repaint; ожидание ограничено 15 секундами. Warmup считает
90 реальных camera/GUI кадров, а не просто Update. Timed interval дополнительно
проверяет минимум 180 camera/GUI кадров; скрытое или minimized окно не может
выдать быстрый цикл Update за успешный графический benchmark.
Стабильная
диагностическая IMGUI-панель присутствует во время измерения, но её текст не
форматируется на каждом sample frame. Она показывает вид нагрузки, число
моделей/renderers и фазу прогона. Frame/counter arrays заранее резервируют
ёмкость. Генерация PNG, JSON, подсчёт geometry, сортировка percentiles и
межсценарный GC.Collect находятся вне timed interval.

Render-only исключает simulation/AI/fog/gameplay HUD/input. Combined намеренно
оставляет ту же статическую сетку rendered marines, пока независимая simulation
army движется по своей карте. Это сохраняет одинаковое pixel coverage при
сравнении. Combined **не является** полным product/session/network benchmark и
не доказывает визуальное следование юнитов за simulation.

## Метрики и смысл

- `WallFrameMs`: Update-to-Update wall time, p50/p95/p99/max/mean и raw samples.
  Включает animation/harness work, render-thread/GPU/OS waits; это не exclusive
  CPU execution time и не GPU time.
- Optional native counters через `ProfilerRecorder`: Main Thread, draw calls,
  batches, SetPass, triangles, total/managed used/reserved memory, GC allocated
  bytes/frame. Availability проверяется discovery + producing recorder.
  Отсутствие метрики обозначается `Available=false` с причиной, а не числом 0.
  Recorder создаётся по обнаруженному native handle; фактические category/unit
  сохраняются. Active CPU Main/Render Thread Frame Time отделены от общего
  Main Thread marker с ожиданиями. Для timing conversion проверяется native
  TimeNanoseconds, чтобы не применять неподтверждённый множитель.
- GPU Frame Time записывается отдельно, только при положительных валидных
  native samples. При отсутствии поддержки/Frame Timing Stats остаётся явный
  пробел; wall time не подменяет GPU measurement.
- Combined: отдельные p50/p95/p99/max simulation step timings, количество
  прошедших ticks, первая стоимость шага с group command, command count и
  discarded/clamped catchup frames. Последние не позволяют ошибочно считать
  замедлившийся clock устойчивой работой в реальном времени.
- Counts: renderers, active renderers, transforms, unique meshes/shared
  materials, summed geometry. Это инвентарь ассетов; draw calls берутся из
  native recorder, а не вычисляются из количества объектов.

Формула percentiles — nearest rank: отсортировать N samples, взять
`ceil(N * percentile) - 1`. Sample arrays сохраняют исходный порядок.
GPU counters в nanoseconds переводятся в milliseconds; memory остаётся bytes.
Build type может ограничивать native counters: особенно GC allocations/frame
нужно отдельно проверять для Development и Release player.
Дополнительная Release-совместимая метрика `Main-thread managed allocated
bytes/frame (System.GC current-thread delta)` измеряет разницу
`GC.GetAllocatedBytesForCurrentThread()` между timed frames. Сохраняются raw
samples/percentiles; список резервируется до измерения. Это только managed
allocations текущего main thread, без других threads и native allocations,
не замена native GC total. Native GC counter сохраняет собственную availability.
Exception или немонотонный счётчик делают fallback unavailable, а не нулевым.
Перед timed interval выполняется calibration: между двумя чтениями выделяется
известный `byte[4096]`, удерживаемый через GC.KeepAlive. Если delta меньше 4096
или немонотонна, runtime считается non-producing, samples не собираются,
метрика unavailable с причиной. Это проверяет Mono/API stub, который может
возвращать постоянный 0 без exception. Наличие метода само по себе не доказывает
валидность измерения. Calibration не входит в timed allocation samples.
При использовании timing `ProfilerRecorder` Unity включает нужные измерения
при подключении counter; глобальный Frame Timing Stats для этого не требуется.
Не менять этот flag между сравниваемыми прогонами. Это отличается от прямого
опроса FrameTimingManager, который в Release требует отдельного включения.

## Evidence и отказ

`profile.json` содержит статус running/passed/failed, конфигурацию, samples,
доступность counters и ошибки. Каждый сценарий сохраняет настоящий framebuffer
`marines-200.png`, `marines-400.png`, `marines-800.png`, проверяя декодирование и
1600×900 dimensions. PNG и JSON находятся только в игнорируемом `Builds`.
Exit code: 0 после всех сценариев; 1 при invariant/runtime error/timeout.
Coroutine exception guard и независимый 180-секундный watchdog не оставляют
ожидаемую ошибку в бесконечном ожидании кадра.

`passed` означает завершение измерения, а не достижение целевого FPS. До
оптимизации сначала сохранить baseline evidence, назвать измеренный bottleneck
и его owner, изменить механизм в owner, затем повторить тот же fixture. Не
снижать качество/число видимых моделей ради формального улучшения результата.

## API references

Проверены официальные Unity 6 API: [ProfilerRecorder](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Unity.Profiling.ProfilerRecorder.html),
[LastValue и завершённый кадр](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Unity.Profiling.ProfilerRecorder.LastValue.html),
[counter availability](https://docs.unity3d.com/6000.0/Documentation/Manual/profiler-counters-reference.html).
[Timing counters через Recorder](https://docs.unity3d.com/6000.0/Documentation/Manual/frame-timing-manager-record-timing-data.html)
позволяют собирать только запрошенные timing metrics.
[System.GC current-thread allocations](https://learn.microsoft.com/en-us/dotnet/api/system.gc.getallocatedbytesforcurrentthread?view=netstandard-2.1)
определяет границы дополнительной managed-only метрики.
Документация описывает API; фактическая доступность определяется этим player
и сохраняется в evidence. Результаты не заявляются до настоящего запуска.

## Измеренная причина и shared mesh lifecycle

Первый действительный foreground baseline (`Iteration03/RenderBaseline`,
игнорируемый `Builds/Evidence`) завершился успешно: 180 actual camera/IMGUI
frames на сценарий, все timed frames focused. На Ryzen 5 2600 / Radeon HD 7800,
D3D11, 1600×900, Ultra/4×MSAA/soft shadows:

| Marines | Unique combined meshes | Draw calls p50 | Wall frame p50 / p95, ms |
| --- | --- | --- | --- |
| 200 | 3200 | 9009 | 14.32 / 17.17 |
| 400 | 6400 | 18009 | 31.42 / 41.57 |
| 800 | 12800 | 36102 | 64.99 / 83.90 |

У 800 GPU p50 — 57.36 ms, active render-thread p50 — 58.27 ms. У каждого
renderer был собственный combined mesh, хотя geometry и instancing-enabled
materials совпадали. Изменение направлено на owner `ArtBuilder.Finish`:
повторное использование идентичной combined geometry; shaders, lighting,
shadow quality, количество деталей и независимые animated pivots сохраняются.
Эффект этой реализации определяется новым paired baseline/optimized прогоном
одной и той же финальной сборки, не предварительной оценкой.

Публичный контракт `ColonyRTS.Presentation`:

- `ProceduralArt.Create(id, owner, parent)` — shared geometry по умолчанию.
- `CreateForProfile(id, owner, parent, ProceduralMeshMode.Shared|Unique)` —
  диагностический выбор для конкретного создаваемого объекта и всех его
  дочерних builders/flash; изменяемого global mode нет.
- `LiveSharedMeshCount` / `LiveSharedMeshReferences` — read-only diagnostics.
  Учитывают все живые world/unit roots; тесты сравнивают scoped deltas.
- Cache key — точная упорядоченная последовательность source-mesh identity,
  submesh index и local TRS matrices, с полным equality после hash lookup.
  Material/owner не входят в geometry key; palette materials продолжают
  назначаться каждому renderer отдельно.
- Shared meshes неизменяемы после создания. Animation меняет только локальные
  transforms экземпляра. Изменение вершин через `sharedMesh` нарушает контракт.
- Каждый renderer владеет одной lease. `ArtMeshLifetime.OnDestroy` освобождает
  её; последний release сначала удаляет cache entry, затем уничтожает mesh.
  В Play Mode это обычный deferred Destroy, в EditMode — DestroyImmediate.
  Lifetime имеет ExecuteAlways и явный Awake: Editor/PreviewScene также должны
  вызывать resource cleanup. Он регистрируется на активном child до parenting,
  включая создание под неактивным parent. Живые соседние leases не инвалидируются.
- Zero-reference derived meshes не удерживаются: cache ограничен реально
  используемыми geometry variants. Небольшой конечный cache source shapes и
  palette materials сохраняется на время процесса. Небезопасного Clear/reset
  при живых экземплярах нет.

Profile проверяет возврат shared cache counts/references к значению перед
сценарием после уничтожения всех его roots. Задачи lifecycle/identity/palette и
независимой animation дополнительно проверяет scoped RenderCacheTests; статус
тестов публикуется только после настоящего Unity-прогона.
