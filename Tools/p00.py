# /Tools/p00.py
# 공용코드 수정: P00 검사·시험 영수증 계약. RP.Editor의 F131/F139와 함께 변경한다.
"""Dependency-free P00 tooling. Static PASS is not a Unity or Windows PASS."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
from datetime import datetime, timezone
import uuid
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
EXPECTED_EDITOR = "6000.6.3f1"
EDITOR_PACKAGES = ("com.unity.render-pipelines.universal", "com.unity.inputsystem", "com.unity.test-framework")
PACKAGES = (
    "com.unity.render-pipelines.universal", "com.unity.inputsystem",
    "com.unity.cinemachine", "com.unity.animation.rigging", "com.unity.test-framework",
)
# Unity 6000.6 core packages are bound to the editor, unlike registry pins.
# Source: docs.unity.com/en-us/engine/6000.6/manual/packages-list/packages-all/pack-core
EDITOR_BOUND_PACKAGES = {
    "com.unity.render-pipelines.universal", "com.unity.cinemachine",
    "com.unity.animation.rigging", "com.unity.test-framework",
}
ASSEMBLIES = {
    "RP.Core": "Scripts/Core/RP.Core.asmdef",
    "RP.Data": "Scripts/Data/RP.Data.asmdef",
    "RP.UnityRuntime": "Scripts/UnityRuntime/RP.UnityRuntime.asmdef",
    "RP.Editor": "Editor/RP.Editor.asmdef",
    "RP.Tests.EditMode": "Tests/EditMode/RP.Tests.EditMode.asmdef",
    "RP.Tests.PlayMode": "Tests/PlayMode/RP.Tests.PlayMode.asmdef",
}
SCENE = "Assets/_Game/Scenes/P00_EMPTY.unity"
PROFILE = "Assets/_Game/Config/BuildProfile.asset"


def utc() -> str:
    return datetime.now(timezone.utc).isoformat()


def linked(path: Path) -> bool:
    return path.is_symlink() or (hasattr(path, "is_junction") and path.is_junction())


def contained(root: Path, relative: str) -> Path:
    if not relative or "\\" in relative or ":" in relative or relative.startswith("/"):
        raise ValueError("Expected a relative slash path")
    parts = relative.split("/")
    if any(p in ("", ".", "..") for p in parts):
        raise ValueError("Path traversal rejected")
    root = root.absolute()
    if any(linked(p) for p in [root, *root.parents]):
        raise ValueError("Symbolic root rejected")
    current = root
    for part in parts:
        current /= part
        if linked(current):
            raise ValueError("Symbolic path rejected")
    return current


def read_json(path: Path, limit: int = 8 * 1024 * 1024) -> dict:
    if path.stat().st_size > limit:
        raise ValueError("JSON exceeds size limit")
    def pairs(values):
        result = {}
        for key, value in values:
            if key in result:
                raise ValueError("Duplicate JSON key: " + key)
            result[key] = value
        return result
    def invalid(value):
        raise ValueError("Non-finite JSON value: " + value)
    result = json.loads(path.read_text(encoding="utf-8-sig"), object_pairs_hook=pairs, parse_constant=invalid)
    if not isinstance(result, dict):
        raise ValueError("Expected JSON object")
    return result


def hash_file(path: Path) -> str:
    with path.open("rb") as stream:
        digest = hashlib.sha256()
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
        return digest.hexdigest()


def project_files(root: Path) -> list[str]:
    result = []
    for directory in ("Assets", "ProjectSettings"):
        base = contained(root, directory)
        if not base.exists():
            continue
        for folder, dirs, files in os.walk(base, followlinks=False):
            for name in dirs + files:
                p = Path(folder) / name
                if linked(p):
                    raise ValueError("Symbolic project entry rejected")
            result.extend((Path(folder) / n).relative_to(root).as_posix() for n in files)
    for name in ("Packages/manifest.json", "Packages/packages-lock.json"):
        if contained(root, name).is_file():
            result.append(name)
    # Match .NET StringComparer.Ordinal even for non-BMP filenames.
    return sorted(result, key=lambda s: s.encode("utf-16-be"))


def source_hash(root: Path) -> str:
    lines = "".join(p + "\0" + hash_file(contained(root, p)) + "\n" for p in project_files(root))
    return hashlib.sha256(lines.encode("utf-8")).hexdigest()


def valid_run_id(value: str) -> bool:
    return bool(re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9._-]{0,63}", value)) and not value.endswith(".") and not re.fullmatch(
        r"CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9]", value.split(".")[0], re.I)


def new_json(path: Path, value: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("x", encoding="utf-8", newline="\n") as stream:
        json.dump(value, stream, ensure_ascii=False, indent=2, allow_nan=False)
        stream.write("\n")


def passing_tests(path: Path) -> bool:
    if not path.is_file() or path.stat().st_size > 4 * 1024 * 1024:
        return False
    raw = path.read_bytes()
    if b"<!DOCTYPE" in raw.upper() or b"<!ENTITY" in raw.upper():
        return False
    try:
        node = ET.fromstring(raw)
        if node.tag != "test-run" or node.get("result") != "Passed":
            return False
        total, passed = int(node.get("total", "-1")), int(node.get("passed", "-1"))
        if total <= 0 or total != passed or any(int(node.get(k, "-1")) != 0 for k in ("failed", "skipped", "inconclusive")):
            return False
        cases = list(node.iter("test-case"))
        if len(cases) != total or any(c.get("result") != "Passed" for c in cases):
            return False
        return all(any(c.get("fullname", "").startswith("RP.Tests.EditMode." + fixture + ".") for c in cases)
                   for fixture in ("ProjectSetupValidatorTests", "BuildCommandTests", "P00ProjectSetupTests"))
    except (ET.ParseError, ValueError, OverflowError):
        return False


def audit(root: Path, strict: bool = False, for_build: bool = True) -> dict:
    errors, blockers, warnings = [], [], []
    graph = {}
    for name, relative in ASSEMBLIES.items():
        try:
            value = read_json(contained(root, "Assets/_Game/" + relative))
            if value.get("name") != name:
                errors.append(name + ": mismatched name")
            refs = value.get("references", [])
            if not isinstance(refs, list) or any(not isinstance(r, str) for r in refs):
                raise ValueError("references must be strings")
            graph[name] = refs
            if name == "RP.Core" and (not value.get("noEngineReferences") or refs):
                errors.append("RP.Core must have no engine or assembly references")
            if name in ("RP.Editor", "RP.Tests.EditMode") and value.get("includePlatforms") != ["Editor"]:
                errors.append(name + ": must be Editor only")
            if name.startswith("RP.Tests.") and (value.get("autoReferenced", True) or "TestAssemblies" not in value.get("optionalUnityReferences", [])):
                errors.append(name + ": missing test isolation")
            if name in ("RP.Core", "RP.Data", "RP.UnityRuntime") and any("Editor" in r or r.startswith("RP.Tests.") for r in refs):
                errors.append(name + ": Editor/test leaked into player")
        except (OSError, ValueError, TypeError) as exc:
            errors.append(name + ": " + str(exc))
    active, visited = set(), set()
    def visit(name):
        if name in active:
            raise ValueError("Assembly cycle: " + name)
        if name in visited:
            return
        active.add(name)
        for dep in graph[name]:
            if dep.startswith("RP.") and dep not in graph:
                raise ValueError("Missing assembly: " + dep)
            if dep in graph:
                visit(dep)
        active.remove(name)
        visited.add(name)
    try:
        for name in graph:
            visit(name)
        for path in contained(root, "Assets/_Game/Scripts/Core").rglob("*.cs"):
            if re.search(r"\b(UnityEngine|UnityEditor)\b", path.read_text(encoding="utf-8")):
                errors.append("Core engine reference: " + path.name)
        rules = contained(root, "Docs/Development/WORKING_RULES.md").read_text(encoding="utf-8")
        ids = re.findall(r"(?m)^\| (C\d{2}) \|", rules)
        if ids != [f"C{i:02}" for i in range(1, 27)]:
            errors.append("Constitution C01-C26 missing/duplicated/out of order")
        index = read_json(contained(root, "Docs/Progress/BASELINE_INDEX.json"))
        file_ids = [r["id"] for r in index["fileContracts"]]
        if len(file_ids) != 140 or len(set(file_ids)) != 140 or len(index["testIds"]) != 100 or len(set(index["testIds"])) != 100:
            errors.append("Baseline 140 files/100 tests not preserved")
        state = read_json(contained(root, "Docs/Progress/P00_STATUS.json"))
        tasks = state["tasks"]
        expected = {f"T00-{i:02}" for i in range(1, 10)} | {"F131", "F139", "EXIT-P00"}
        if {t["id"] for t in tasks} != expected or len(tasks) != len(expected):
            errors.append("P00 task IDs missing or duplicated")
        by_id = {t["id"]: t for t in tasks}
        for t in tasks:
            if t["status"] not in {"TODO", "READY", "IN_PROGRESS", "REVIEW", "BLOCKED", "DONE"}:
                errors.append(t["id"] + ": invalid state")
            if t["status"] == "DONE":
                if t.get("blockedReason") or t.get("testStatus") != "PASS" or not t.get("evidence"):
                    errors.append(t["id"] + ": DONE without passing evidence")
                for dep in t["dependencies"]:
                    if dep not in by_id or by_id[dep]["status"] != "DONE":
                        errors.append(t["id"] + ": incomplete prerequisite " + dep)
                for evidence in t.get("evidence", []):
                    if not contained(root, evidence).is_file():
                        errors.append(t["id"] + ": missing evidence file")
        if state.get("exitStatus") == "DONE" and any(t["status"] != "DONE" for t in tasks):
            errors.append("EXIT-P00 cannot bypass unfinished tasks")
    except (OSError, ValueError, KeyError, TypeError) as exc:
        errors.append("Policy/trace: " + str(exc))
    try:
        version = contained(root, "ProjectSettings/ProjectVersion.txt")
        if not version.exists():
            blockers.append("Actual Unity ProjectVersion is missing")
        elif not re.search(r"(?m)^m_EditorVersion: " + re.escape(EXPECTED_EDITOR) + r"\s*$", version.read_text(encoding="utf-8")):
            errors.append("Unity editor does not match the preserved project pin: " + EXPECTED_EDITOR + "")
        manifest_path = contained(root, "Packages/manifest.json")
        lock_path = contained(root, "Packages/packages-lock.json")
        if not manifest_path.exists() or not lock_path.exists():
            blockers.append("Actual package manifest and resolved lock are missing")
        else:
            manifest = read_json(manifest_path).get("dependencies", {})
            lock = read_json(lock_path).get("dependencies", {})
            for package in PACKAGES:
                pin = manifest.get(package)
                if pin is None:
                    if package in EDITOR_PACKAGES:
                        errors.append(package + ": required editor package missing")
                    elif for_build:
                        blockers.append(package + ": authoring package not installed; retain user lock until Package Manager resolves it")
                    continue
                if not isinstance(pin, str) or not re.fullmatch(r"\d+\.\d+\.\d+", pin):
                    errors.append(package + ": explicit stable package pin required")
                resolved = lock.get(package)
                if not isinstance(resolved, dict):
                    errors.append(package + ": lock missing/invalid")
                    continue
                resolved_version = resolved.get("version")
                if not isinstance(resolved_version, str) or not re.fullmatch(r"\d+\.\d+\.\d+", resolved_version):
                    errors.append(package + ": invalid resolved version")
                    continue
                editor_bound = resolved.get("source") == "builtin" and package in EDITOR_BOUND_PACKAGES
                if resolved.get("source") == "builtin" and not editor_bound:
                    errors.append(package + ": unexpected builtin source; review package policy")
                if resolved_version != pin:
                    if editor_bound:
                        warnings.append(package + ": editor-bound core resolution " + resolved_version +
                                        " differs from manifest " + str(pin) +
                                        "; preserved, not certified. Validate actual PackageInfo in Unity.")
                    else:
                        errors.append(package + ": lock mismatch/missing")
        for name in ((SCENE, PROFILE) if for_build else ()):
            if not contained(root, name).is_file():
                blockers.append("Unity-generated asset missing: " + name)
        paths = project_files(root)
        no_meta = [p for p in paths if p.startswith("Assets/") and not p.endswith(".meta") and not contained(root, p + ".meta").is_file()]
        if no_meta:
            blockers.append(f"Unity-generated metadata missing for {len(no_meta)} files")
        guids = set()
        for p in paths:
            if p.endswith(".meta"):
                match = re.search(r"(?m)^guid: ([a-f0-9]{32})$", contained(root, p).read_text(encoding="utf-8"))
                if not match or match[1] in guids:
                    errors.append("Invalid or duplicate Unity GUID: " + p)
                if match:
                    guids.add(match[1])
        fingerprint = source_hash(root)
    except (OSError, ValueError, TypeError) as exc:
        errors.append("Project safety: " + str(exc)); fingerprint = None
    return {"scope": "P00-STATIC", "status": "FAIL" if errors else "BLOCKED" if strict and blockers else "PASS",
            "strictUnityFiles": strict, "unityReady": False, "sourceHash": fingerprint,
            "errors": errors, "blockers": blockers, "warnings": warnings,
            "note": "Static inspection only. Unity import, EditMode execution, and Windows smoke require separate evidence."}


def adopt(root: Path, source: Path, apply: bool = False) -> dict:
    """Import an actual fresh URP project without overwriting any existing file."""
    source = source.absolute()
    version = contained(source, "ProjectSettings/ProjectVersion.txt")
    if not re.search(r"(?m)^m_EditorVersion: " + re.escape(EXPECTED_EDITOR) + r"\s*$", version.read_text(encoding="utf-8")):
        raise ValueError("Source editor does not match the preserved project version")
    for name in ("Packages/manifest.json", "Packages/packages-lock.json"):
        read_json(contained(source, name))
    if contained(root, "ProjectSettings/ProjectVersion.txt").exists():
        raise ValueError("Destination already initialized; no engine/settings replacement")
    candidates = project_files(source)
    for name in candidates:
        if contained(root, name).exists():
            raise FileExistsError("Import conflicts with repository file: " + name)
    if apply:
        created = []
        try:
            for name in candidates:
                target = contained(root, name)
                target.parent.mkdir(parents=True, exist_ok=True)
                with target.open("xb") as out:
                    created.append(target)
                    with contained(source, name).open("rb") as inp:
                        shutil.copyfileobj(inp, out)
        except BaseException:
            # Only files created by this invocation are removed, never previous files.
            for path in reversed(created):
                path.unlink(missing_ok=True)
            raise
    return {"scope": "P00-IMPORT", "applied": apply, "files": candidates,
            "note": "No package installation or compatibility certification. Open in Unity, import metadata, and validate."}


def run_unity(root: Path, editor: Path, run_id: str, timeout: int, tests_only: bool = False) -> dict:
    if not valid_run_id(run_id):
        raise ValueError("Unsafe run ID")
    if not editor.is_file():
        raise FileNotFoundError("Supply an installed licensed Unity executable")
    if contained(root, "Temp/UnityLockfile").exists():
        raise ValueError("Close the running Unity editor before starting a batch run; lock is preserved")
    check = audit(root, strict=True, for_build=not tests_only)
    if check["status"] != "PASS":
        raise ValueError("Project prerequisites not ready: " + json.dumps(check, ensure_ascii=False))
    evidence = contained(root, "Artifacts/Evidence/" + run_id)
    evidence.mkdir(parents=True, exist_ok=False)
    base = [str(editor.absolute()), "-batchmode", "-projectPath", str(root.absolute()), "-rpRunId", run_id]
    if not tests_only:
        base += ["-buildTarget", "win64", "-activeBuildProfile", PROFILE]
    def execute(label: str, extra: list[str]) -> None:
        args = base + ["-logFile", str(evidence / (label + ".log"))] + extra
        subprocess.run(args, check=True, timeout=timeout, cwd=root, shell=False)
    result = {"scope": "P00-EDITOR-TESTS" if tests_only else "P00-WINDOWS-PREBUILD", "status": "FAIL", "runId": run_id, "startedUtc": utc(), "windowsSmoke": "NOT_RUN"}
    try:
        before = source_hash(root)
        execute("validate", ["-executeMethod", "RP.Editor.ProjectSetupValidator.ValidateEditorBatch" if tests_only else "RP.Editor.ProjectSetupValidator.ValidateBatch", "-quit"])
        # Do NOT add -quit to runTests: Unity may exit before tests finish.
        execute("editmode", ["-runTests", "-testPlatform", "EditMode", "-assemblyNames", "RP.Tests.EditMode", "-testResults", str(evidence / "editmode.xml")])
        if not passing_tests(evidence / "editmode.xml"):
            raise ValueError("Missing, empty, skipped or failed P00 test results")
        if before != source_hash(root):
            raise ValueError("Project changed during import/tests. Review generated files and rerun with a new ID")
        new_json(evidence / "test-receipt.json", {"schemaVersion": 2, "scope": result["scope"], "runId": run_id, "sourceHash": before,
                    "resultsSha256": hash_file(evidence / "editmode.xml"), "status": "PASS", "createdUtc": utc()})
        if tests_only:
            result.update(status="PASS", sourceHash=before, windowsBuild="NOT_RUN")
            return result
        execute("build", ["-executeMethod", "RP.Editor.BuildCommand.BuildP00", "-quit"])
        build = read_json(evidence / "build.json")
        if build.get("status") != "PASS" or build.get("sourceHash") != before:
            raise ValueError("Missing/stale/failed build evidence")
        result.update(status="PASS", sourceHash=before)
    except (OSError, ValueError, subprocess.SubprocessError) as exc:
        result["error"] = type(exc).__name__
        raise
    finally:
        result["finishedUtc"] = utc()
        new_json(evidence / "runner.json", result)
    return result


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=ROOT)
    commands = parser.add_subparsers(dest="command", required=True)
    check = commands.add_parser("audit"); check.add_argument("--strict-unity-files", action="store_true")
    commands.add_parser("fingerprint")
    import_cmd = commands.add_parser("adopt"); import_cmd.add_argument("--source", type=Path, required=True); import_cmd.add_argument("--apply", action="store_true")
    unity = commands.add_parser("unity"); unity.add_argument("--editor", type=Path, required=True)
    unity.add_argument("--run-id", default="p00-" + uuid.uuid4().hex[:12]); unity.add_argument("--timeout", type=int, default=1800)
    unity.add_argument("--tests-only", action="store_true", help="Run editor validation/tests without switching platform or building Windows")
    args = parser.parse_args(argv)
    try:
        root = args.root.absolute()
        if args.command == "audit":
            result = audit(root, args.strict_unity_files)
        elif args.command == "fingerprint":
            result = {"sourceHash": source_hash(root)}
        elif args.command == "adopt":
            result = adopt(root, args.source, args.apply)
        else:
            if args.timeout <= 0:
                raise ValueError("Timeout must be positive")
            result = run_unity(root, args.editor, args.run_id, args.timeout, args.tests_only)
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 1 if result.get("status") == "FAIL" else 2 if result.get("status") == "BLOCKED" else 0
    except (OSError, ValueError, subprocess.SubprocessError) as exc:
        print(json.dumps({"scope": "P00-TOOL", "status": "BLOCKED" if isinstance(exc, FileNotFoundError) else "FAIL", "error": str(exc)}, ensure_ascii=False), file=sys.stderr)
        return 2 if isinstance(exc, FileNotFoundError) else 1


if __name__ == "__main__":
    raise SystemExit(main())
