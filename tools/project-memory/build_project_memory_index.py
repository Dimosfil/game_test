"""Project-owned local Markdown/JSON retrieval. Python stdlib + SQLite FTS5 only."""
import argparse
from contextlib import closing
import datetime as dt
import fnmatch
import hashlib
import json
import os
from pathlib import Path
import re
import sqlite3
import subprocess

ROOT = Path(__file__).resolve().parents[2]
CONFIG = ROOT / "tools/project-memory/rag-system.json"


def config():
    return json.loads(CONFIG.read_text(encoding="utf-8-sig"))


def local_path(value):
    path = (ROOT / value).resolve()
    if not path.is_relative_to(ROOT):
        raise ValueError("Generated/retrieval path leaves the project")
    return path


def sha(text):
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def config_hash(settings):
    stable = json.loads(json.dumps(settings))
    stable.get("rebuild", {}).pop("state", None)
    return sha(json.dumps(stable, sort_keys=True))


def sources(settings):
    found = {}
    for group in settings["source_groups"]:
        if not group.get("include", False):
            continue
        for pattern in group["paths"]:
            for path in ROOT.glob(pattern):
                if not path.is_file() or path.is_symlink() or not path.resolve().is_relative_to(ROOT):
                    continue
                relative = path.relative_to(ROOT).as_posix()
                if path.suffix not in {".md", ".json"} or any(fnmatch.fnmatch(relative, p) for p in settings["exclude_globs"]):
                    continue
                if relative in found:
                    continue  # First group is the authoritative classification.
                text = path.read_text(encoding="utf-8-sig")
                found[relative] = {"group": group["id"], "trust": group["trust"], "text": text,
                                   "hash": sha(text), "bytes": path.stat().st_size}
    return found


def chunks(text, maximum):
    headings = []
    pending = []
    size = 0
    start = 1
    end = 1
    for number, line in enumerate(text.splitlines(), 1):
        heading = re.match(r"^(#{1,6})\s+(.+)", line)
        if pending and (heading or size + len(line) + 1 > maximum):
            yield " / ".join(headings), start, end, "\n".join(pending)
            pending, size = [], 0
        if heading:
            depth = len(heading[1])
            headings = headings[:depth - 1] + [heading[2]]
        for offset in range(0, max(1, len(line)), maximum):
            piece = line[offset:offset + maximum]
            if size + len(piece) + 1 > maximum and pending:
                yield " / ".join(headings), start, end, "\n".join(pending)
                pending, size = [], 0
            if not pending:
                start = number
            pending.append(piece)
            size += len(piece) + 1
            end = number
    if pending:
        yield " / ".join(headings), start, end, "\n".join(pending)


def generated_path(value, suffix):
    path = local_path(value)
    if path.suffix != suffix or not path.is_relative_to(ROOT / "tools/project-memory"):
        raise ValueError("Generated retrieval output must use the documented memory directory and file type")
    ignored = subprocess.run(["git", "check-ignore", "-q", "--", path.relative_to(ROOT).as_posix()], cwd=ROOT, check=False)
    if ignored.returncode != 0:
        raise ValueError("Generated retrieval output must be ignored before writing")
    return path


def database(settings):
    return generated_path(settings["structured_memory"]["path"], ".sqlite")


def rebuild():
    settings = config()
    if not settings["keyword_retrieval"]["enabled"]:
        raise ValueError("Keyword retrieval is explicitly disabled")
    manifest = sources(settings)
    path = database(settings)
    temporary = path.with_name(path.name + "-build")
    path.parent.mkdir(parents=True, exist_ok=True)
    # Only this configured generated index is replaced; source files are untouched.
    if temporary.exists():
        temporary.unlink()
    with closing(sqlite3.connect(temporary)) as connection:
        connection.executescript("""
        CREATE TABLE documents(path TEXT PRIMARY KEY, source_group TEXT, trust TEXT, hash TEXT, bytes INTEGER);
        CREATE TABLE chunks(id INTEGER PRIMARY KEY, path TEXT, heading TEXT, start_line INTEGER, end_line INTEGER, body TEXT, hash TEXT);
        CREATE VIRTUAL TABLE chunk_search USING fts5(path, heading, body, tokenize='unicode61');
        CREATE TABLE metadata(key TEXT PRIMARY KEY, value TEXT);
        """)
        for relative, source in sorted(manifest.items()):
            connection.execute("INSERT INTO documents VALUES(?,?,?,?,?)", (relative, source["group"], source["trust"], source["hash"], source["bytes"]))
            for heading, first, last, body in chunks(source["text"], settings["chunking"]["max_chars"]):
                cursor = connection.execute("INSERT INTO chunks(path,heading,start_line,end_line,body,hash) VALUES(?,?,?,?,?,?)",
                                            (relative, heading, first, last, body, sha(body)))
                connection.execute("INSERT INTO chunk_search(rowid,path,heading,body) VALUES(?,?,?,?)", (cursor.lastrowid, relative, heading, body))
        commit = subprocess.run(["git", "rev-parse", "HEAD"], cwd=ROOT, capture_output=True, text=True, check=True).stdout.strip()
        metadata = {"project_id": settings["project_id"], "built_utc": dt.datetime.now(dt.timezone.utc).isoformat(),
                    "commit": commit, "config_hash": config_hash(settings), "schema": "1"}
        connection.executemany("INSERT INTO metadata VALUES(?,?)", metadata.items())
        connection.commit()
    os.replace(temporary, path)
    return stats()


def stats():
    settings = config()
    manifest = sources(settings)
    result = {"project_id": settings["project_id"], "sources": len(manifest), "database": settings["structured_memory"]["path"]}
    path = database(settings)
    if not path.exists():
        return dict(result, status="unavailable", reason="Run the documented SQL node rebuild")
    with closing(sqlite3.connect(f"file:{path.as_posix()}?mode=ro", uri=True)) as connection:
        indexed = dict(connection.execute("SELECT path,hash FROM documents"))
        metadata = dict(connection.execute("SELECT key,value FROM metadata"))
        stale = sorted(key for key in set(indexed) | set(manifest) if key not in manifest or indexed.get(key) != manifest[key]["hash"])
        config_changed = metadata["config_hash"] != config_hash(settings)
        result.update(status="stale" if stale or config_changed else "current", stale_sources=stale,
                      config_changed=config_changed, indexed_sources=len(indexed), metadata=metadata,
                      chunks=connection.execute("SELECT count(*) FROM chunks").fetchone()[0],
                      fts_chunks=connection.execute("SELECT count(*) FROM chunk_search").fetchone()[0])
    return result


def search(query, limit=10, source_group=None):
    if not 1 <= limit <= 40:
        raise ValueError("Limit must be 1..40")
    settings = config()
    if not settings["keyword_retrieval"]["enabled"] or not database(settings).exists():
        raise ValueError("Keyword retrieval is unavailable; inspect the SQL node")
    words = re.findall(r"\w+", query, re.UNICODE)[:16]
    if not words:
        return []
    match = " OR ".join('"' + word + '"' for word in words)
    with closing(sqlite3.connect(f"file:{database(settings).as_posix()}?mode=ro", uri=True)) as connection:
        connection.row_factory = sqlite3.Row
        rows = connection.execute("""
            SELECT c.path,c.heading,c.start_line,c.end_line,c.hash,d.source_group,d.trust,
                   substr(c.body,1,1200) AS excerpt
            FROM chunk_search f JOIN chunks c ON c.id=f.rowid JOIN documents d ON d.path=c.path
            WHERE chunk_search MATCH ? AND (? IS NULL OR d.source_group=?)
            ORDER BY bm25(chunk_search,5,3,1),c.id LIMIT ?
        """, (match, source_group, source_group, limit)).fetchall()
        return [dict(row) for row in rows]


def export_chunks():
    settings = config()
    health = stats()
    if health["status"] != "current":
        raise ValueError("SQL sources are stale; rebuild that node before exporting")
    output = generated_path(settings["chunking"]["export_path"], ".jsonl")
    with closing(sqlite3.connect(f"file:{database(settings).as_posix()}?mode=ro", uri=True)) as connection, output.open("w", encoding="utf-8") as stream:
        connection.row_factory = sqlite3.Row
        for row in connection.execute("SELECT c.*,d.source_group,d.trust FROM chunks c JOIN documents d ON d.path=c.path ORDER BY c.id"):
            stream.write(json.dumps(dict(row), ensure_ascii=False) + "\n")
    return {"status": "exported", "path": output.relative_to(ROOT).as_posix(), "chunks": health["chunks"]}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=["rebuild", "stats", "search", "export-chunks"])
    parser.add_argument("query", nargs="?")
    parser.add_argument("--limit", type=int, default=10)
    parser.add_argument("--group")
    args = parser.parse_args()
    if args.command == "search" and not args.query:
        parser.error("search requires a query")
    result = {"rebuild": rebuild, "stats": stats, "export-chunks": export_chunks}.get(args.command)
    print(json.dumps(result() if result else {"results": search(args.query, args.limit, args.group)}, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
