# /Tools/tests/test_p00_migration.py
# 공용코드 수정: 새 저장소 루트·실제 버전 pin·Mac 시험 분리 회귀. Unity 호출은 명시적인 mock이다.
import json
from pathlib import Path
import re
import unittest
from unittest.mock import patch
import test_p00 as original

p00 = original.p00


class MigrationTests(unittest.TestCase):
    setUp = original.P00ToolsTests.setUp
    tearDown = original.P00ToolsTests.tearDown
    change_json = original.P00ToolsTests.change_json

    def initial_project(self):
        (self.root / "ProjectSettings").mkdir()
        (self.root / "Packages").mkdir()
        (self.root / "ProjectSettings/ProjectVersion.txt").write_text(
            "m_EditorVersion: " + p00.EXPECTED_EDITOR + "\n", encoding="utf-8")
        versions = dict(zip(p00.EDITOR_PACKAGES, ("17.7.0", "1.20.0", "1.8.0")))
        (self.root / "Packages/manifest.json").write_text(json.dumps({"dependencies": versions}))
        (self.root / "Packages/packages-lock.json").write_text(json.dumps({"dependencies": {
            key: {"version": version} for key, version in versions.items()}}))

    def test_existing_user_pin_is_accepted(self):
        self.initial_project()
        self.assertEqual("PASS", p00.audit(self.root)["status"])

    def test_downgrade_or_other_patch_is_not_implicitly_accepted(self):
        self.initial_project()
        for version in ("6000.3.1f1", "6000.6.4f1", "6000.6.3b1"):
            (self.root / "ProjectSettings/ProjectVersion.txt").write_text("m_EditorVersion: " + version + "\n")
            self.assertEqual("FAIL", p00.audit(self.root)["status"])

    def test_audit_does_not_rewrite_initial_settings(self):
        self.initial_project()
        before = p00.source_hash(self.root)
        p00.audit(self.root, strict=True)
        self.assertEqual(before, p00.source_hash(self.root))

    def test_missing_authoring_packages_are_blockers_not_static_failures(self):
        self.initial_project()
        report = p00.audit(self.root)
        self.assertEqual("PASS", report["status"])
        self.assertTrue(any("com.unity.cinemachine" in x for x in report["blockers"]))
        editor_report = p00.audit(self.root, for_build=False)
        self.assertFalse(any("com.unity.cinemachine" in x for x in editor_report["blockers"]))
        self.assertFalse(any(p00.PROFILE in x for x in editor_report["blockers"]))

    def test_missing_core_package_remains_an_error(self):
        self.initial_project()
        self.change_json("Packages/manifest.json", lambda x: x["dependencies"].pop("com.unity.inputsystem"))
        self.assertEqual("FAIL", p00.audit(self.root, for_build=False)["status"])

    def test_existing_package_lock_mismatch_is_rejected(self):
        self.initial_project()
        self.change_json("Packages/packages-lock.json", lambda x: x["dependencies"]["com.unity.inputsystem"].update(version="1.0.0"))
        self.assertEqual("FAIL", p00.audit(self.root)["status"])

    def test_existing_project_cannot_be_adopted_over(self):
        self.initial_project()
        with self.assertRaises(ValueError):
            p00.adopt(self.root, self.root, True)

    def test_original_assets_affect_fingerprint(self):
        self.initial_project()
        path = self.root / "Assets/UserScene.unity"
        path.write_text("synthetic user scene A")
        before = p00.source_hash(self.root)
        path.write_text("synthetic user scene B")
        self.assertNotEqual(before, p00.source_hash(self.root))

    def test_python_and_csharp_pins_are_identical(self):
        source = (self.root / "Assets/_Game/Editor/ProjectSetupValidator.cs").read_text()
        match = re.search(r'ExpectedEditorVersion = "([^"]+)"', source)
        self.assertIsNotNone(match)
        self.assertEqual(p00.EXPECTED_EDITOR, match[1])

    def test_editor_only_run_never_switches_platform_or_builds(self):
        editor = self.root / "synthetic-editor"; editor.touch()
        calls = []
        def mocked_run(args, **kwargs):
            calls.append(args)
            self.assertFalse(kwargs["shell"])
            if "-runTests" in args:
                Path(args[args.index("-testResults") + 1]).write_text(original.XML)
        with patch.object(p00, "audit", return_value={"status": "PASS"}), patch.object(p00.subprocess, "run", side_effect=mocked_run):
            result = p00.run_unity(self.root, editor, "editor-only", 30, tests_only=True)
        self.assertEqual("PASS", result["status"])
        self.assertEqual("P00-EDITOR-TESTS", result["scope"])
        self.assertEqual("NOT_RUN", result["windowsBuild"])
        self.assertEqual(2, len(calls))
        for args in calls:
            self.assertNotIn("-buildTarget", args)
            self.assertNotIn("-activeBuildProfile", args)
            self.assertNotIn("RP.Editor.BuildCommand.BuildP00", args)
        receipt = p00.read_json(self.root / "Artifacts/Evidence/editor-only/test-receipt.json")
        self.assertEqual(2, receipt["schemaVersion"])
        self.assertEqual("P00-EDITOR-TESTS", receipt["scope"])

    def test_missing_test_output_never_gets_a_passing_receipt(self):
        editor = self.root / "synthetic-editor"; editor.touch()
        with patch.object(p00, "audit", return_value={"status": "PASS"}), patch.object(p00.subprocess, "run"):
            with self.assertRaises(ValueError):
                p00.run_unity(self.root, editor, "no-tests", 30, tests_only=True)
        folder = self.root / "Artifacts/Evidence/no-tests"
        self.assertFalse((folder / "test-receipt.json").exists())
        self.assertEqual("FAIL", p00.read_json(folder / "runner.json")["status"])

    def test_running_editor_lock_is_preserved(self):
        editor = self.root / "synthetic-editor"; editor.touch()
        lock = self.root / "Temp/UnityLockfile"; lock.parent.mkdir(); lock.write_text("busy")
        with self.assertRaises(ValueError):
            p00.run_unity(self.root, editor, "locked", 30, tests_only=True)
        self.assertEqual("busy", lock.read_text())

    def test_setup_test_family_is_required(self):
        result = self.root / "tests.xml"
        result.write_text(original.XML.replace("P00ProjectSetupTests.One", "UnrelatedTests.One"))
        self.assertFalse(p00.passing_tests(result))


if __name__ == "__main__":
    unittest.main()
