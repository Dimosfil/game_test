# Agent Runbook

Last checked: 2026-10-17.

## Project State

`game_test` is a Unity 6000.3.24f1 project with the local GI instruction kit.
`Assets/` is empty and `EditorBuildSettings.asset` contains no build scenes.
Graphics settings have no custom render pipeline assigned. The package manifest
contains built-in Unity modules and Multiplayer Center 1.0.1. Product
requirements, gameplay, and application test/build commands are not defined.

## Unity MCP Access

Checked 2026-10-17. Unity CLI 1.0.0-beta.13 is current according to
`unity self-update --check --json`. Installed `com.unity.pipeline` 0.8.0-exp.1
through the official CLI; Unity resolved it in `Packages/packages-lock.json`.

```powershell
unity pipeline install --project-path "C:\work\game_test" --non-interactive --json
unity status --project-path "C:\work\game_test" --json
```

After installation, Unity initially returned `STATUS_PIPELINE_LOAD_PENDING`.
The user activated the existing Editor window, allowing Package Manager to
import the package. No restart, scene changes, or client reconfiguration was
needed.

Verified access:

- Project-scoped MCP stdio: initialize succeeded (protocol 2025-03-26),
  tools/list advertised 160 tools, and tools/call editor_status returned ready
  for `C:\work\game_test`, Unity 6000.3.24f1.
- Direct MCP tools in this chat: editor_status, list_open_scenes,
  get_scene_hierarchy, and console_status all succeeded.
- Editor: not compiling, no domain reload, Play Mode stopped.
- One active scene without a saved path; isDirty=false; roots Main Camera and
  Directional Light. No scene was saved or modified during verification.
- Console: no compilation failure, zero errors and warnings at verification.

Connection behavior and boundaries are documented in
`tools/project-memory/specs/integration-contracts/unity-pipeline.md`.

Git ignore rules were configured on 2026-10-18 for Unity caches, local settings,
build output, generated IDE files, and local .env values. Assets and their
.meta files, Packages manifests/lockfile, and ProjectSettings remain eligible.

## GI Context

Run from the project root in PowerShell:

```powershell
.\tools\get-gi-context.ps1 -CommandText "gi start"
```

See `COMMANDS.md` for the GI command index. Add product commands here when
implemented.

## Documentation Check

```powershell
git diff --check
```

Expected result: exit code 0 and no whitespace errors.

## Preferences

- Agent replies match the user's language by default.
- Commit messages default to English; no commit was requested during setup.
- Task-manager plan sync awaits the user's choice: WorkNest or none.
- Config-service integration is disabled by default.
