# Unity Pipeline Integration

Status: current. Last checked: 2026-10-20.

2026-10-22 tooling verification: CLI updated to 1.0.0-beta.14 and its current/
latest check matched. Editor/Pipeline versions were preserved. Official project
stdio probe completed initialize (2025-03-26), listed 160 tools and read ready
Editor/Frontier clean state. Native editor_status/list_open_scenes/eval/test tools
also worked in this chat. This is a fresh connection check, not a new package or
bridge installation. Test scene preservation is owned by tools/test-rts.ps1;
see [development tooling](development-tooling.md).

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

2026-10-20 recovery: the newly launched GUI Editor was blocked by Administrator
Privileges Detected. Windows UI input returned GetCursorPos access denied. Only
that newly launched modal process was closed; the same installed Editor/project
was started in persistent batchmode without -quit/-nographics. CLI status and
MCP initialize/tools-list/tools-call through tools/unity-mcp.mjs now work;
160 tools advertised, editor_status ready, Frontier.unity active and clean.
tools/start-unity.ps1 reuses the live Editor. Native Unity tools are absent from
this chat catalog; the client does not restore them there.

Batchmode has no Game View render target. The actual standalone player passes
graphical HUD/save/load/replay checks in a normal window. Windows hardware input
remains unverified. See [current results](../../../../docs/sc2/testing.md).

Historical initial setup evidence, 2026-10-17:

CLI 1.0.0-beta.13 matched the latest version reported by self-update --check.
MCP initialize negotiated 2025-03-26; tools/list returned 160 tools;
tools/call editor_status identified this project's Editor as ready.
Direct MCP calls in the current chat also succeeded for editor_status,
list_open_scenes, get_scene_hierarchy, and console_status. The Editor was not
compiling, had no domain reload, and Play Mode was stopped. Console reported
zero errors/warnings and no compilation failure. One active scene without a
saved path contained Main Camera and Directional Light; isDirty was false.

Revalidate live state in later sessions; these observations are dated evidence.
