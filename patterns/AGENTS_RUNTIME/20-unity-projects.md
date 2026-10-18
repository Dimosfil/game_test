# Unity Projects

Apply this module to projects developed with Unity Editor. Read project-local
instructions, the Unity runbook, `ProjectSettings/ProjectVersion.txt`, and
relevant package manifests before Unity operations. Keep exact commands,
versions, project selectors, and verification evidence in the local runbook or
integration contract; do not copy one project's machine paths into shared rules.

## Official Tooling First

- Try the installed official `unity` CLI first; check its current help before
  choosing commands or flags. Prefer the official MCP flow through `unity mcp`
  and the project's Unity Pipeline package for available Editor operations.
- Reuse project-owned automation when a required official command is missing
  or fails. Explain the concrete limitation before using another method; do not
  introduce a second Editor bridge merely to bypass an undiagnosed connection.
- Treat CLI installation, MCP client configuration, and per-project Pipeline
  installation as separate responsibilities. Do not reinstall or reconfigure
  them automatically just because a live check failed.

## Live Connection And Project Identity

- Before relying on Editor operations in a session, establish the selected
  project's identity through a read-only live status call and inspect its open
  scenes using the server's discovered tools or documented CLI equivalents.
  Recheck after reconnection or a change of selected project/Editor.
- For MCP verification, require successful `initialize`, a nonempty
  `tools/list`, and a read-only `tools/call` returning the intended project's
  live Editor/scene state. Discover real tool names and schemas; do not assume
  names or arguments from old notes.
- Separately verify tool availability in the current agent chat by calling its
  exposed read-only Editor tools. A standalone protocol probe or CLI call does
  not prove that the chat loaded the MCP tools.
- Report three distinct states: client configuration present, live Editor
  connection verified, and tools callable in the current chat. Historical
  evidence, configuration, or handshake alone is not a current connection.
- With multiple Editors, verify the target before any writes. Use an explicit
  per-operation project selector when needed. Stop operations against an
  unexpected or ambiguous target; do not change global configuration to pin
  every project to one checkout.

## Failure Handling And Boundaries

- Diagnose the failed layer: missing tooling/configuration, unavailable Editor,
  missing or unrestored Pipeline package, import/compilation in progress,
  ambiguous project selection, or chat tool discovery. Name confirmed failures
  and distinguish them from possible causes.
- For an empty tool catalog, verify Editor and Pipeline readiness first. After
  they become ready, refresh discovery where supported; if the chat still lacks
  tools, report that a new session or client restart may be required. Do not
  claim a separate CLI success restored tools to the current chat.
- Preserve scenes, unsaved changes, Play Mode, Editor processes, and user focus
  during connection checks. Do not launch/close an Editor, change scenes, install
  packages, or rewrite client settings solely to answer a connection question.
  Continue independently authorized work and report the specific blocked step.
- Stop repeated network attempts on registry, authentication, DNS, or timeout
  failures; report the concrete error and check access/VPN as applicable before
  retrying. Keep discovered tokens and connection descriptors out of source,
  shared instructions, logs shown to the user, and project memory.
