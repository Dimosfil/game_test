# Local instruction index

This is the project-owned consumer kit, not a full shared-library checkout.
Start with [AGENTS.md](AGENTS.md); runtime routing is listed there and in
[config/gi-command-routes.json](config/gi-command-routes.json).

- [Runtime modules](patterns/AGENTS_RUNTIME/)
- [Architecture and code quality](patterns/ARCHITECTURE_AND_CODE_QUALITY.md)
- [Configuration boundaries](patterns/CONFIGURATION_BOUNDARIES.md)
- [Modular engineering](patterns/MODULAR_SERVICE_ENGINEERING.md)
- [Documentation layers](patterns/PROJECT_DOCUMENTATION_LAYERS.md)
- [Memory specifications](patterns/PROJECT_MEMORY_SPECIFICATIONS.md)
- [RAG structure](patterns/RAG_SYSTEM_STRUCTURE.md)
- [Semantic retrieval](patterns/SEMANTIC_RAG_RETRIEVAL.md)
- [First-message handling](patterns/FIRST_MESSAGE_HANDLING.md)
- [FTP deployment](patterns/PROJECT_FTP_DEPLOY.md)
- [Bootstrap contract](patterns/SHARED_INSTRUCTIONS_BOOTSTRAP.md)
- [Agent harness runtime](patterns/AGENT_HARNESS_RUNTIME.md)
- [Feature workflow contracts](patterns/FEATURE_WORKFLOW_CONTRACTS.md)
- [Model routing and costs](patterns/MODEL_ROUTING_AND_COST_CONTROL.md)
- [Project testing strategy](patterns/PROJECT_TESTING_STRATEGY.md)
- [Architecture history template](templates/ARCHITECTURE_MIGRATIONS.template.md)
- [RAG configuration template](templates/rag-system.template.json)
- [Semantic eval template](templates/semantic-retrieval-evals.template.md)
- [FTP configuration template](templates/ftp.local.template.json)
- [Summary template](templates/SUMMARY.template.md)
- [Installed instruction metadata](tools/project-memory/instruction-kit.json)

Validate transitive static library references with `tools/test-instruction-kit.ps1`.
Recover missing consumer files with `tools/repair-instruction-kit.ps1 -SourceCommit`
using a reviewed commit whose VERSION matches the installed accepted version.
Recovery adds missing files only; it never overwrites project-local instructions.
