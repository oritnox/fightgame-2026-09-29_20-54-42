# /Tools/tests/test_p01.py
# 공용코드 수정: P01 실행기 직접 시험. Unity subprocess는 명시적으로 mock 처리한다.
import importlib.util
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest
from unittest.mock import patch

SPEC = importlib.util.spec_from_file_location('rp_p01', Path(__file__).resolve().parents[1] / 'p01.py')
p01 = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(p01)


def passing_xml(inventory=None):
    inventory = p01.EXPECTED_CASES if inventory is None else inventory
    cases = ''.join("<test-case fullname='RP.Tests.Core." + name + ".SyntheticFixture" + str(index) + "' result='Passed'/>"
                    for name, count in inventory.items() for index in range(count))
    total = sum(inventory.values())
    return f"<test-run result='Passed' total='{total}' passed='{total}' failed='0' skipped='0' inconclusive='0'>" + cases + '</test-run>'


class P01RunnerTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='rp-p01-test-')
        self.root = Path(self.temp.name).resolve() / 'project'
        self.root.mkdir()
        manifest = p01.p00.read_json(p01.ROOT / p01.MANIFEST)
        for name in list(manifest['files']) + [p01.MANIFEST]:
            destination = self.root / name
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(p01.ROOT / name, destination)
        self.editor = self.root / 'synthetic-editor-not-executable'
        self.editor.touch()

    def tearDown(self):
        self.temp.cleanup()

    def write_xml(self, text):
        path = self.root / 'tests.xml'; path.write_text(text, encoding='utf-8'); return path

    def mock_unity(self, args, **kwargs):
        self.assertFalse(kwargs['shell'])
        self.assertNotIn('-buildTarget', args)
        self.assertNotIn('-activeBuildProfile', args)
        self.assertNotIn('RP.Editor.BuildCommand.BuildP00', args)
        folder = Path(args[args.index('-logFile') + 1]).parent
        if '-runTests' in args:
            self.assertNotIn('-quit', args)
            self.assertEqual(p01.ASSEMBLY, args[args.index('-assemblyNames') + 1])
            (folder / 'core-editmode.xml').write_text(passing_xml())
        else:
            (folder / 'setup.json').write_text(json.dumps({'scope': 'P00-EDITOR-SETUP', 'status': 'PASS',
                'sourceHash': p01.p00.source_hash(self.root), 'editorVersion': p01.p00.EXPECTED_EDITOR}))

    def run_mocked(self, run_id='valid'):
        with patch.object(p01.p00, 'audit', return_value={'status': 'PASS'}), patch.object(p01.subprocess, 'run', side_effect=self.mock_unity):
            return p01.run_unity(self.root, self.editor, run_id, 30)

    def test_packet_hashes_are_checked_without_claiming_compilation(self):
        result = p01.inspect_sources(self.root)
        self.assertEqual('PASS', result['status'])
        self.assertEqual('NOT_RUN', result['csharpCompilation'])

    def test_changed_source_is_rejected(self):
        path = self.root / 'Assets/_Game/Scripts/Core/Timing/ClockBridge.cs'
        path.write_text(path.read_text() + '\n// injected test change\n')
        self.assertEqual('FAIL', p01.inspect_sources(self.root)['status'])

    def test_missing_source_is_rejected(self):
        (self.root / 'Assets/_Game/Scripts/Core/Timing/ClockBridge.cs').unlink()
        self.assertEqual('FAIL', p01.inspect_sources(self.root)['status'])

    def test_manifest_traversal_is_rejected(self):
        (self.root / p01.MANIFEST).write_text(json.dumps({'files': {'../outside': 'abc'}}))
        self.assertEqual('FAIL', p01.inspect_sources(self.root)['status'])

    def test_passing_result_requires_nine_real_families(self):
        result = p01.validate_results(self.write_xml(passing_xml()))
        self.assertEqual(sum(p01.EXPECTED_CASES.values()), result['passed'])
        self.assertEqual(list(p01.FIXTURES), result['fixtures'])

    def test_old_eighty_case_inventory_is_rejected(self):
        old = dict(p01.EXPECTED_CASES, ClockBridgeTests=10, CommandBufferTests=13)
        self.assertEqual(80, sum(old.values()))
        with self.assertRaises(ValueError):
            p01.validate_results(self.write_xml(passing_xml(old)))

    def test_empty_result_is_rejected(self):
        with self.assertRaises(ValueError):
            p01.validate_results(self.write_xml("<test-run result='Passed' total='0' passed='0' failed='0' skipped='0' inconclusive='0'/>"))

    def test_skipped_or_failed_tests_never_pass(self):
        for text in (passing_xml().replace("skipped='0'", "skipped='1'"), passing_xml().replace("SyntheticFixture0' result='Passed'", "SyntheticFixture0' result='Failed'")):
            with self.subTest(text=text), self.assertRaises(ValueError):
                p01.validate_results(self.write_xml(text))

    def test_missing_or_duplicate_fixture_is_rejected(self):
        for text in (passing_xml().replace('ClockBridgeTests.', 'UnknownTests.'), passing_xml().replace('ClockBridgeTests.', 'TempoMapTests.')):
            with self.subTest(text=text), self.assertRaises(ValueError):
                p01.validate_results(self.write_xml(text))

    def test_inconsistent_case_count_is_rejected(self):
        total = sum(p01.EXPECTED_CASES.values())
        with self.assertRaises(ValueError):
            p01.validate_results(self.write_xml(passing_xml().replace(f"total='{total}' passed='{total}'", f"total='{total + 1}' passed='{total + 1}'")))

    def test_dtd_and_broken_xml_are_rejected(self):
        for text in ('<!DOCTYPE test-run [<!ENTITY x SYSTEM "file:///etc/passwd">]>' + passing_xml(), '<broken'):
            with self.subTest(text=text), self.assertRaises(ValueError):
                p01.validate_results(self.write_xml(text))

    def test_oversized_xml_is_rejected(self):
        with self.assertRaises(ValueError):
            p01.validate_results(self.write_xml(' ' * (8 * 1024 * 1024 + 1)))

    def test_utf16_xml_cannot_hide_a_dtd(self):
        path = self.root / 'tests.xml'
        path.write_bytes(('<?xml version="1.0" encoding="UTF-16"?><!DOCTYPE test-run>' + passing_xml()).encode('utf-16'))
        with self.assertRaises(ValueError): p01.validate_results(path)

    def test_partial_run_is_rejected_even_with_all_families(self):
        import xml.etree.ElementTree as ET
        root = ET.fromstring(passing_xml())
        root.remove(root.find('test-case'))
        total = str(len(root.findall('test-case')))
        root.set('total', total); root.set('passed', total)
        with self.assertRaises(ValueError):
            p01.validate_results(self.write_xml(ET.tostring(root, encoding='unicode')))

    def test_successful_mocked_run_has_separate_receipt_scope(self):
        result = self.run_mocked()
        self.assertEqual('PASS', result['status'])
        self.assertEqual('P01-CORE-EDITOR', result['scope'])
        self.assertEqual('NOT_RUN', result['windowsBuild'])
        folder = self.root / 'Artifacts/Evidence/valid'
        receipt = p01.p00.read_json(folder / 'core-test-receipt.json')
        self.assertFalse(receipt['isP00WindowsReceipt'])
        self.assertFalse((folder / 'test-receipt.json').exists())

    def test_missing_test_output_cannot_generate_a_receipt(self):
        def no_output(args, **kwargs):
            if '-runTests' not in args: self.mock_unity(args, **kwargs)
        with patch.object(p01.p00, 'audit', return_value={'status': 'PASS'}), patch.object(p01.subprocess, 'run', side_effect=no_output):
            with self.assertRaises(ValueError): p01.run_unity(self.root, self.editor, 'missing', 30)
        folder = self.root / 'Artifacts/Evidence/missing'
        self.assertFalse((folder / 'core-test-receipt.json').exists())
        self.assertEqual('FAIL', p01.p00.read_json(folder / 'core-runner.json')['status'])

    def test_stale_setup_receipt_is_rejected(self):
        def stale(args, **kwargs):
            self.mock_unity(args, **kwargs)
            folder = Path(args[args.index('-logFile') + 1]).parent
            if '-runTests' not in args:
                path = folder / 'setup.json'; value = p01.p00.read_json(path); value['sourceHash'] = 'old'; path.write_text(json.dumps(value))
        with patch.object(p01.p00, 'audit', return_value={'status': 'PASS'}), patch.object(p01.subprocess, 'run', side_effect=stale):
            with self.assertRaises(ValueError): p01.run_unity(self.root, self.editor, 'stale', 30)
        self.assertFalse((self.root / 'Artifacts/Evidence/stale/core-test-receipt.json').exists())

    def test_source_change_during_test_invalidates_receipt(self):
        def changing(args, **kwargs):
            self.mock_unity(args, **kwargs)
            if '-runTests' in args:
                (self.root / 'Assets/changed.txt').write_text('simulated importer mutation')
        with patch.object(p01.p00, 'audit', return_value={'status': 'PASS'}), patch.object(p01.subprocess, 'run', side_effect=changing):
            with self.assertRaises(ValueError): p01.run_unity(self.root, self.editor, 'changed', 30)
        self.assertFalse((self.root / 'Artifacts/Evidence/changed/core-test-receipt.json').exists())

    def test_existing_run_id_is_not_overwritten(self):
        self.run_mocked('same')
        before = (self.root / 'Artifacts/Evidence/same/core-runner.json').read_bytes()
        with self.assertRaises(FileExistsError): self.run_mocked('same')
        self.assertEqual(before, (self.root / 'Artifacts/Evidence/same/core-runner.json').read_bytes())

    def test_editor_lock_is_preserved(self):
        lock = self.root / 'Temp/UnityLockfile'; lock.parent.mkdir(); lock.write_text('busy')
        with self.assertRaises(ValueError): p01.run_unity(self.root, self.editor, 'busy', 30)
        self.assertEqual('busy', lock.read_text())

    def test_missing_editor_cannot_claim_a_pass(self):
        with self.assertRaises(FileNotFoundError): p01.run_unity(self.root, self.root / 'missing-editor', 'absent', 30)
        self.assertFalse((self.root / 'Artifacts').exists())

    def test_invalid_run_id_rejected_before_execution(self):
        with self.assertRaises(ValueError): p01.run_unity(self.root, self.editor, '../bad', 30)

    def test_timeout_records_failure_not_a_passing_receipt(self):
        with patch.object(p01.p00, 'audit', return_value={'status': 'PASS'}), patch.object(p01.subprocess, 'run', side_effect=subprocess.TimeoutExpired('synthetic-editor', 1)):
            with self.assertRaises(subprocess.TimeoutExpired): p01.run_unity(self.root, self.editor, 'timeout', 1)
        folder = self.root / 'Artifacts/Evidence/timeout'
        self.assertEqual('FAIL', p01.p00.read_json(folder / 'core-runner.json')['status'])
        self.assertFalse((folder / 'core-test-receipt.json').exists())


if __name__ == '__main__':
    unittest.main()
