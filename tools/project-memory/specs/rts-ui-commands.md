# UI command bindings and localization

Owner: Presentation/UiCommandCatalog resolves catalog relationships and resource
layout. Match remains the sole authority for production/research/costs/supply.
GameHost loads the required UI layout once before initializing input/HUD.

RuleSet.Definitions is authoritative for trainable units and ProducerId. The UI
derives per-producer options for non-building/non-resource definitions. A named
training action resolves a completed producer from the current player's detached
observation and current selection; enemy/hidden/unselected IDs cannot be used.
Q's default selects the first configured option of the first observed eligible
producer. Match validates the resulting ordinary Train command at execution.

Resources/rts-ui.json owns display order, hidden build-menu definitions, default
train key and per-building shortcuts. It does not override gameplay prices,
timers, producer relationships or eligibility. New valid catalog buildings and
units appear even when absent from ordering lists; catalog order is the stable
fallback. The experimental base is explicitly hidden from the build menu in the
resource, preserving the previous slice. Unknown/duplicate/incompatible IDs,
invalid key names and duplicate train/build shortcuts fail at resource loading.

HUD and RtsInteraction consume the same catalog instance. A named unit action
uses its definition ID, avoiding mismatched mixed-selection labels/commands.
Only a producer's first training option advertises the default train shortcut.
Shortcut annotations come from the same bindings as input, not copied localized
text. Workers retain build-menu priority; research and production controls can
coexist for a selected producer and still use authoritative research gates.

Resources/ui-ru.json owns captions, technology states, translated rejection
templates, costs/time units and combat-stat labels. RuntimeText loads/validates
the table, formats arguments and maps research reason keys to resources. Unknown
research reasons use the localized unavailable message. New content without an
optional localized caption falls back to its catalog DisplayName. Unit/upgrade
DisplayName remains content-owned. No Russian translation dictionary is embedded
in TechnologyPanel; it only chooses observed states and draws formatted text.

Implementation: GameHost.UiCommands; UiCommandCatalog/UiCommandLayout;
RtsInteraction.Train; BattleHud command actions; RuntimeText and TechnologyPanel.
UiCommandCatalogTests checks new catalog content through actual Match commands,
mixed selection/ownership, actual resource ordering, invalid bindings, key-label
parity and all supported research rejection formats. Standalone smoke remains
the assembled host/HUD/save/load/replay check; tests do not prove physical input.
