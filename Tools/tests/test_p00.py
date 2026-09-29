# /Tools/tests/test_p00.py
# 공용코드 수정: P00 도구 직접 시험. Unity/Windows 검증을 대체하지 않는다.
import importlib.util
import json
from pathlib import Path
import shutil
import tempfile
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location("p00", Path(__file__).resolve().parents[1] / "p00.py")
p00 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(p00)
SOURCE = p00.ROOT
XML = ("<test-run result='Passed' total='3' passed='3' failed='0' skipped='0' inconclusive='0'>"
       "<test-case fullname='RP.Tests.EditMode.ProjectSetupValidatorTests.One' result='Passed'/>"
       "<test-case fullname='RP.Tests.EditMode.BuildCommandTests.One' result='Passed'/><test-case fullname='RP.Tests.EditMode.P00ProjectSetupTests.One' result='Passed'/></test-run>")


class P00ToolsTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="rp-p00-")
        self.root = Path(self.temp.name).resolve() / "project"
        self.root.mkdir()
        for directory in ("Assets/_Game", "Docs"):
            shutil.copytree(SOURCE / directory, self.root / directory, ignore=shutil.ignore_patterns("Artifacts", "__pycache__"))

    def tearDown(self):
        self.temp.cleanup()

    def change_json(self, relative, fn):
        path = self.root / relative
        data = json.loads(path.read_text(encoding="utf-8")); fn(data)
        path.write_text(json.dumps(data), encoding="utf-8")

    def test_static_audit_is_not_unity_pass(self):
        result = p00.audit(self.root)
        self.assertEqual("PASS", result["status"])
        self.assertFalse(result["unityReady"])
        self.assertTrue(result["blockers"])

    def test_strict_preflight_blocks_missing_project(self):
        self.assertEqual("BLOCKED", p00.audit(self.root, strict=True)["status"])

    def test_core_engine_reference_fails(self):
        self.change_json("Assets/_Game/Scripts/Core/RP.Core.asmdef", lambda x: x.update(noEngineReferences=False))
        self.assertEqual("FAIL", p00.audit(self.root)["status"])

    def test_assembly_cycle_fails(self):
        self.change_json("Assets/_Game/Scripts/Data/RP.Data.asmdef", lambda x: x.update(references=["RP.UnityRuntime"]))
        self.assertTrue(any("cycle" in x for x in p00.audit(self.root)["errors"]))

    def test_player_editor_leak_fails(self):
        self.change_json("Assets/_Game/Scripts/UnityRuntime/RP.UnityRuntime.asmdef", lambda x: x.update(references=["RP.Editor"]))
        self.assertEqual("FAIL", p00.audit(self.root)["status"])

    def test_duplicate_baseline_id_fails(self):
        self.change_json("Docs/Progress/BASELINE_INDEX.json", lambda x: x["fileContracts"][1].update(id=x["fileContracts"][0]["id"]))
        self.assertEqual("FAIL", p00.audit(self.root)["status"])

    def test_constitution_missing_id_fails(self):
        path = self.root / "Docs/Development/WORKING_RULES.md"
        path.write_text(path.read_text().replace("| C26 |", "| C99 |"), encoding="utf-8")
        self.assertEqual("FAIL", p00.audit(self.root)["status"])

    def test_done_without_evidence_fails(self):
        self.change_json("Docs/Progress/P00_STATUS.json", lambda x: x["tasks"][0].update(status="DONE"))
        self.assertEqual("FAIL", p00.audit(self.root)["status"])

    def test_done_does_not_bypass_dependencies(self):
        def change(x):
            t = next(t for t in x["tasks"] if t["id"] == "F139")
            t.update(status="DONE", testStatus="PASS", blockedReason=None, evidence=["Docs/QA/GATE_MATRIX.md"])
        self.change_json("Docs/Progress/P00_STATUS.json", change)
        self.assertTrue(any("incomplete prerequisite" in x for x in p00.audit(self.root)["errors"]))

    def test_gate_cannot_bypass_tasks(self):
        self.change_json("Docs/Progress/P00_STATUS.json", lambda x: x.update(exitStatus="DONE"))
        self.assertEqual("FAIL", p00.audit(self.root)["status"])

    def test_traversal_and_windows_paths_rejected(self):
        for relative in ("../x", "a/../x", "/absolute", "a//b", "C:/x", "a\\b", "./x"):
            with self.subTest(relative=relative), self.assertRaises(ValueError):
                p00.contained(self.root, relative)

    def test_link_cannot_escape(self):
        link = self.root / "escape"
        try:
            link.symlink_to(Path(self.temp.name).resolve(), target_is_directory=True)
        except OSError:
            self.skipTest("Host cannot create symbolic links")
        with self.assertRaises(ValueError):
            p00.contained(self.root, "escape/file")

    def test_project_hash_rejects_symbolic_assets(self):
        link = self.root / "Assets/escape"
        try:
            link.symlink_to(Path(self.temp.name).resolve(), target_is_directory=True)
        except OSError:
            self.skipTest("Host cannot create symbolic links")
        with self.assertRaises(ValueError):
            p00.source_hash(self.root)

    def test_hash_tracks_source_not_artifacts(self):
        before = p00.source_hash(self.root)
        (self.root / "Artifacts").mkdir(); (self.root / "Artifacts/log").write_text("not source")
        self.assertEqual(before, p00.source_hash(self.root))
        (self.root / "Assets/new.txt").write_text("changed")
        self.assertNotEqual(before, p00.source_hash(self.root))

    def test_invalid_run_ids(self):
        for value in ("", "../evil", "a/b", "a\\b", "NUL", "COM1.log", "run.", "x" * 65, "x\n"):
            self.assertFalse(p00.valid_run_id(value), value)
        self.assertTrue(p00.valid_run_id("p00-01_a.2"))

    def test_evidence_never_overwrites(self):
        path = self.root / "Artifacts/Evidence/test/e.json"
        p00.new_json(path, {"state": "first"})
        with self.assertRaises(FileExistsError):
            p00.new_json(path, {"state": "second"})
        self.assertEqual("first", p00.read_json(path)["state"])

    def test_duplicate_and_nonfinite_json_rejected(self):
        path = self.root / "test.json"
        for text in ('{"x":1,"x":2}', '{"x":NaN}', '{"x":Infinity}', '[]'):
            path.write_text(text)
            with self.assertRaises(ValueError):
                p00.read_json(path)

    def test_only_real_passing_cases_count(self):
        path = self.root / "tests.xml"; path.write_text(XML)
        self.assertTrue(p00.passing_tests(path))
        for text in (XML.replace("total='3'", "total='4'"), XML.replace("skipped='0'", "skipped='1'"),
                     XML.replace("BuildCommandTests.One", "Other.One"), XML.replace("One' result='Passed'", "One' result='Failed'"),
                     "<test-run result='Passed' total='0' passed='0' failed='0' skipped='0' inconclusive='0'/>",
                     "<!DOCTYPE test-run [<!ENTITY x SYSTEM 'file:///etc/passwd'>]>" + XML, "bad xml"):
            path.write_text(text)
            self.assertFalse(p00.passing_tests(path), text)

    def make_unity_source(self):
        source = Path(self.temp.name).resolve() / "unity-source"
        (source / "ProjectSettings").mkdir(parents=True)
        (source / "Packages").mkdir(); (source / "Assets").mkdir()
        (source / "ProjectSettings/ProjectVersion.txt").write_text("m_EditorVersion: " + p00.EXPECTED_EDITOR + "\n")
        for name in ("manifest.json", "packages-lock.json"):
            (source / "Packages" / name).write_text('{"dependencies":{}}')
        (source / "Assets/SourceOnly.txt").write_text("generated fixture")
        return source

    def test_import_dry_run_then_apply(self):
        source = self.make_unity_source()
        self.assertFalse(p00.adopt(self.root, source)["applied"])
        self.assertFalse((self.root / "ProjectSettings").exists())
        self.assertTrue(p00.adopt(self.root, source, True)["applied"])
        self.assertEqual("generated fixture", (self.root / "Assets/SourceOnly.txt").read_text())
        with self.assertRaises(ValueError):
            p00.adopt(self.root, source, True)

    def test_import_conflict_does_not_partially_write(self):
        source = self.make_unity_source()
        (self.root / "Assets/SourceOnly.txt").write_text("keep")
        with self.assertRaises(FileExistsError):
            p00.adopt(self.root, source, True)
        self.assertEqual("keep", (self.root / "Assets/SourceOnly.txt").read_text())
        self.assertFalse((self.root / "ProjectSettings").exists())

    def test_import_copy_failure_rolls_back_only_new_files(self):
        source = self.make_unity_source()
        with patch.object(p00.shutil, "copyfileobj", side_effect=OSError("injected")):
            with self.assertRaises(OSError):
                p00.adopt(self.root, source, True)
        self.assertFalse((self.root / "Assets/SourceOnly.txt").exists())
        self.assertTrue((self.root / "Assets/_Game/Editor/BuildCommand.cs").exists())

    def test_missing_editor_cannot_claim_build_pass(self):
        with self.assertRaises(FileNotFoundError):
            p00.run_unity(self.root, self.root / "missing-unity", "test-1", 30)

    def test_runner_uses_no_shell_and_tests_without_quit(self):
        # Contract check only; actual editor process must be tested separately.
        text = (p00.ROOT / "Tools/p00.py").read_text(encoding="utf-8")
        self.assertIn("shell=False", text)
        test_line = next(line for line in text.splitlines() if 'execute("editmode"' in line)
        self.assertNotIn('"-quit"', test_line)


if __name__ == "__main__":
    unittest.main()
