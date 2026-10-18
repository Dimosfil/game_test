# Pending Tasks

## GI Bootstrap — 2026-10-17

Goal: install a project-owned instruction kit from the requested source.

- [x] Verify target identity and preserve the existing README and Git remote.
- [x] Install GI instructions, routing, preferences, and project memory.
- [x] Adapt the project entrypoint and runbook to the empty repository.
- [x] Verify installed paths, metadata, PowerShell syntax, and whitespace.
- [ ] Ask whether to enable WorkNest plan sync or use none.

No product implementation or technology selection was requested.

## Unity Pipeline Connection — 2026-10-17

Goal: connect Pipeline to the current Unity project and verify MCP access.

- [x] Verify project identity, installed CLI help, latest CLI version, and missing Pipeline.
- [x] Install the latest Pipeline package using official project-scoped CLI (0.8.0-exp.1).
- [x] Verify live Editor identity and scenes through MCP in this chat.
- [x] Record package version, commands, and verification evidence.

Mechanism: official CLI updates Packages/manifest.json; Unity Package Manager
resolves/imports the package; Pipeline serves Editor commands to Unity MCP.
Preserve scenes, Play Mode, user focus, and unrelated changes.
