"""Read-only health and bounded retrieval evals for the configured local index."""
import argparse
import json
import sys
import build_project_memory_index as index

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--skip-vector", action="store_true")
parser.add_argument("command", choices=["health", "run"], default="run", nargs="?")
args = parser.parse_args()
health = index.stats()
errors = []
if health["status"] != "current":
    errors.append("SQL index is not current")
if health.get("chunks", 0) == 0 or health.get("chunks") != health.get("fts_chunks"):
    errors.append("Missing/inconsistent chunk counts")
settings = index.config()
if settings["vector_retrieval"]["enabled"] and not args.skip_vector:
    errors.append("No vector adapter is configured; keep it explicitly disabled")
cases = []
if args.command == "run" and not errors:
    evaluation = index.local_path("tools/project-memory/retrieval-evals.json")
    for case in json.loads(evaluation.read_text(encoding="utf-8"))["cases"]:
        results = index.search(case["query"], limit=10)
        passed = any(row["path"] == case["expected_path"] for row in results)
        cases.append({"id": case["id"], "passed": passed, "paths": list(dict.fromkeys(row["path"] for row in results))})
        if not passed:
            errors.append("Retrieval miss: " + case["id"])
print(json.dumps({"status": "failed" if errors else "passed", "health": health, "cases": cases, "errors": errors}, ensure_ascii=False, indent=2))
sys.exit(1 if errors else 0)
