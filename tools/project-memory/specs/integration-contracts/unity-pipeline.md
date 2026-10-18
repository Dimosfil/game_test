# Unity Pipeline Integration

Status: current. Last checked: 2026-10-17.

## Purpose And Ownership

Connect the project's existing Unity Editor to official Unity CLI and MCP.
Unity Package Manager owns package resolution; `com.unity.pipeline` owns the
Editor server; `unity mcp` exposes the MCP bridge. Do not add a second bridge.
This is development tooling, not gameplay logic.

## Inputs And Configuration

- Unity version: `ProjectSettings/ProjectVersion.txt` (6000.3.24f1).
- Dependency: `Packages/manifest.json` (`com.unity.pipeline` 0.8.0-exp.1).
- Resolved dependency graph: `Packages/packages-lock.json`.
- Installation and status commands: [runbook](../../../AGENT_RUNBOOK.md).
- Select this project explicitly for CLI/MCP operations, using the current
  checkout's root. Reverify identity after reconnecting or switching Editors.
- Keep runtime descriptors, connection tokens, and ports out of committed config.

## Lifecycle And Failure Handling

1. Official installer adds the package declaration.
2. Unity Package Manager imports the package in the existing Editor.
3. Pipeline starts its Editor server; MCP discovers the registered tools.
4. Verify Editor project identity and open scenes before mutations.

Manifest presence alone does not prove connectivity. During first setup,
`STATUS_PIPELINE_LOAD_PENDING` persisted until the user activated the Unity
window. Preserve scenes and Play Mode; do not restart an Editor to bypass this.
Compilation errors, Safe Mode, import state, or an ambiguous target must be
diagnosed separately. Never treat CLI success as proof of chat tool access.

## Verification Evidence

CLI 1.0.0-beta.13 matched the latest version reported by self-update --check.
MCP initialize negotiated 2025-03-26; tools/list returned 160 tools;
tools/call editor_status identified this project's Editor as ready.
Direct MCP calls in the current chat also succeeded for editor_status,
list_open_scenes, get_scene_hierarchy, and console_status. The Editor was not
compiling, had no domain reload, and Play Mode was stopped. Console reported
zero errors/warnings and no compilation failure. One active scene without a
saved path contained Main Camera and Directional Light; isDirty was false.

Revalidate live state in later sessions; these observations are dated evidence.
