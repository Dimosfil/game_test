# Connected Projects and Reference Sources

Reviewed: 2026-10-20. Public documentation research only. No external repository
is cloned, vendored, installed, or used as a product runtime dependency.

| Source and owner | Role | Canonical location | Boundary |
| --- | --- | --- | --- |
| SC2 Editor Guides, Blizzard and contributors | Mechanics, authoring, presentation | [Guides](https://s2editor-guides.readthedocs.io/) | Public read-only; historical examples do not establish live balance |
| Blizzard s2client-proto | Commands, observations, version/replay behavior | [Repository](https://github.com/Blizzard/s2client-proto) | Public schemas/docs; not a Unity engine or installed game |
| Blizzard s2client-api | Candidate future reference harness | [Repository](https://github.com/Blizzard/s2client-api) | No local folder, build, service or actual integration |
| Blizzard s2protocol | Candidate replay-event analysis | [Repository](https://github.com/Blizzard/s2protocol) | Decoder; not complete balance/behavior |
| Blizzard SC2 patch notes | Proposed melee reference release | [5.0.17](https://news.blizzard.com/en-gb/article/24309308/starcraft-ii-5-0-17-patch-notes) | Delta only; binary/data/map manifest still required |
| Unity Technologies documentation | Architecture/profiling options | [Unity 6000.3](https://docs.unity3d.com/6000.3/Documentation/Manual/) | Package research does not authorize dependency replacement |

No external private paths, credentials or user data were inspected. Installing
or launching SC2, reading a specific external local folder, and starting online
services are outside this planning change. Retrieve current sources through
canonical URLs. Add local paths, IDs, manifests and update commands when an
actual integration is introduced.

Proposed behavior: [SC2 contract](../sc2-recreation-contract.md).
Research and limitations: [research](../../../../docs/sc2/reference-research.md).
