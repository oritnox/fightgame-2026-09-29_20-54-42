# /Tools/p01.py
# 공용코드 수정: P01-A 시험 실행·증거 검증. P00/Windows 빌드 영수증과 분리한다.
"""P01 pure-core policy checks and actual Unity EditMode execution; no fake engine pass."""
from __future__ import annotations
import argparse
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import uuid
import xml.etree.ElementTree as ET

_SPEC = importlib.util.spec_from_file_location('_rp_p00_dependency', Path(__file__).with_name('p00.py'))
if _SPEC is None or _SPEC.loader is None:
    raise RuntimeError('Existing Tools/p00.py is required; do not replace the tested P00 tool.')
p00 = importlib.util.module_from_spec(_SPEC)
_SPEC.loader.exec_module(p00)
ROOT = Path(__file__).resolve().parents[1]
ASSEMBLY = 'RP.Tests.Core.EditMode'
FIXTURES = (
    'CommonTypesTests', 'ClockBridgeTests', 'TempoMapTests', 'CombatClockTests',
    'CalibrationEstimatorTests', 'TimingRecoveryPolicyTests', 'InputContractsTests',
    'CommandBufferTests', 'InputArbitratorTests',
)
EXPECTED_CASES = {
    'CommonTypesTests': 13, 'ClockBridgeTests': 26, 'TempoMapTests': 11, 'CombatClockTests': 6,
    'CalibrationEstimatorTests': 6, 'TimingRecoveryPolicyTests': 9, 'InputContractsTests': 4,
    'CommandBufferTests': 22, 'InputArbitratorTests': 8,
}
MANIFEST = 'Tools/p01-source-manifest.json'


def inspect_sources(root: Path) -> dict:
    errors = []
    try:
        manifest = p00.read_json(p00.contained(root, MANIFEST))
        files = manifest.get('files')
        if not isinstance(files, dict) or not files:
            raise ValueError('Source manifest must contain a non-empty files object')
        for name, expected in files.items():
            path = p00.contained(root, name)
            if not path.is_file() or p00.hash_file(path) != expected:
                errors.append('Missing or changed packet file: ' + name)
        asm = p00.read_json(p00.contained(root, 'Assets/_Game/Tests/EditMode/Core/' + ASSEMBLY + '.asmdef'))
        if (asm.get('name') != ASSEMBLY or asm.get('references') != ['RP.Core'] or
                asm.get('includePlatforms') != ['Editor'] or asm.get('autoReferenced') is not False or
                'TestAssemblies' not in asm.get('optionalUnityReferences', [])):
            errors.append('Core test assembly must remain isolated from the player and P00 test assembly')
    except (OSError, ValueError, TypeError) as exc:
        errors.append(type(exc).__name__ + ': ' + str(exc))
    return {'scope': 'P01-SOURCE-PACKET', 'status': 'FAIL' if errors else 'PASS',
            'errors': errors, 'csharpCompilation': 'NOT_RUN', 'unityExecution': 'NOT_RUN'}


def validate_results(path: Path) -> dict:
    if not path.is_file() or path.stat().st_size > 8 * 1024 * 1024:
        raise ValueError('Missing or oversized Core EditMode result')
    raw = path.read_bytes()
    try:
        text = raw.decode('utf-8-sig')
    except UnicodeDecodeError as exc:
        raise ValueError('Only UTF-8 Unity XML is accepted') from exc
    if '\x00' in text or '<!DOCTYPE' in text.upper() or '<!ENTITY' in text.upper():
        raise ValueError('DTD/entity declarations or binary XML are not accepted')
    try:
        root = ET.fromstring(text)
        if root.tag != 'test-run' or root.get('result') != 'Passed':
            raise ValueError('Core run did not pass')
        total, passed = int(root.get('total', '-1')), int(root.get('passed', '-1'))
        if total <= 0 or total != passed or any(int(root.get(k, '-1')) != 0 for k in ('failed', 'skipped', 'inconclusive')):
            raise ValueError('Empty, failed, skipped or inconsistent Core tests')
        cases = list(root.iter('test-case'))
        if len(cases) != total or any(c.get('result') != 'Passed' for c in cases):
            raise ValueError('Core test cases disagree with root summary')
        names = [c.get('fullname', '') for c in cases]
        if any(not name for name in names) or len(names) != len(set(names)):
            raise ValueError('Missing or duplicate Core test identity')
        missing = [f for f in FIXTURES if not any(n.startswith('RP.Tests.Core.' + f + '.') for n in names)]
        if missing:
            raise ValueError('Core test families missing: ' + ', '.join(missing))
        observed = {f: sum(n.startswith('RP.Tests.Core.' + f + '.') for n in names) for f in FIXTURES}
        if observed != EXPECTED_CASES or total != sum(EXPECTED_CASES.values()):
            raise ValueError('Partial or changed Core test inventory; review the test manifest')
        return {'status': 'PASS', 'total': total, 'passed': passed, 'failed': 0, 'skipped': 0, 'fixtures': list(FIXTURES)}
    except (ET.ParseError, OverflowError) as exc:
        raise ValueError('Malformed Core test results') from exc


def run_unity(root: Path, editor: Path, run_id: str, timeout: int = 1800) -> dict:
    if not p00.valid_run_id(run_id) or timeout <= 0:
        raise ValueError('Safe run ID and positive timeout required')
    if not editor.is_file():
        raise FileNotFoundError('Actual licensed Unity executable is required')
    if p00.contained(root, 'Temp/UnityLockfile').exists():
        raise ValueError('Close the editor first; its lock is not removed')
    packet = inspect_sources(root)
    if packet['status'] != 'PASS':
        raise ValueError(json.dumps(packet, ensure_ascii=False))
    setup = p00.audit(root, strict=True, for_build=False)
    if setup['status'] != 'PASS':
        raise ValueError('Open/import the project and validate P00 editor settings first: ' + json.dumps(setup, ensure_ascii=False))
    folder = p00.contained(root, 'Artifacts/Evidence/' + run_id)
    folder.mkdir(parents=True, exist_ok=False)
    before = p00.source_hash(root)
    result = {'scope': 'P01-CORE-EDITOR', 'runId': run_id, 'status': 'FAIL', 'startedUtc': p00.utc(),
              'sourceHash': before, 'windowsBuild': 'NOT_RUN', 'windowsSmoke': 'NOT_RUN', 'exitP01': 'NOT_COMPLETE'}
    base = [str(editor.absolute()), '-batchmode', '-projectPath', str(root.absolute()), '-rpRunId', run_id]
    def execute(label: str, args: list[str]) -> None:
        subprocess.run(base + ['-logFile', str(folder / (label + '.log'))] + args,
                       check=True, timeout=timeout, cwd=root, shell=False)
    try:
        execute('validate', ['-executeMethod', 'RP.Editor.ProjectSetupValidator.ValidateEditorBatch', '-quit'])
        editor_setup = p00.read_json(folder / 'setup.json')
        if (editor_setup.get('status') != 'PASS' or editor_setup.get('scope') != 'P00-EDITOR-SETUP' or
                editor_setup.get('sourceHash') != before or editor_setup.get('editorVersion') != p00.EXPECTED_EDITOR):
            raise ValueError('Actual editor setup receipt is missing, failed or stale')
        execute('core-editmode', ['-runTests', '-testPlatform', 'EditMode', '-assemblyNames', ASSEMBLY,
                                 '-testResults', str(folder / 'core-editmode.xml')])
        # No -quit while runTests is active; the test runner owns its completion.
        summary = validate_results(folder / 'core-editmode.xml')
        if p00.source_hash(root) != before:
            raise ValueError('Project changed during tests; review generated files and rerun with a new ID')
        receipt = {'schemaVersion': 1, 'scope': 'P01-CORE-EDITOR', 'status': 'PASS', 'runId': run_id,
                   'sourceHash': before, 'resultsSha256': p00.hash_file(folder / 'core-editmode.xml'),
                   'summary': summary, 'createdUtc': p00.utc(), 'isP00WindowsReceipt': False}
        p00.new_json(folder / 'core-test-receipt.json', receipt)
        result['status'] = 'PASS'; result['tests'] = summary
    except (OSError, ValueError, subprocess.SubprocessError) as exc:
        # Raw engine logs remain local; do not serialize arbitrary local paths or credentials.
        result['errorType'] = type(exc).__name__
        raise
    finally:
        result['finishedUtc'] = p00.utc()
        p00.new_json(folder / 'core-runner.json', result)
    return result


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=ROOT)
    commands = parser.add_subparsers(dest='command', required=True)
    commands.add_parser('audit')
    unity = commands.add_parser('unity')
    unity.add_argument('--editor', type=Path, required=True)
    unity.add_argument('--run-id', default='p01-' + uuid.uuid4().hex[:12])
    unity.add_argument('--timeout', type=int, default=1800)
    args = parser.parse_args(argv)
    try:
        root = args.root.absolute()
        result = inspect_sources(root) if args.command == 'audit' else run_unity(root, args.editor, args.run_id, args.timeout)
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 0 if result['status'] == 'PASS' else 1
    except (OSError, ValueError, subprocess.SubprocessError) as exc:
        print(json.dumps({'scope': 'P01-TOOL', 'status': 'BLOCKED' if isinstance(exc, FileNotFoundError) else 'FAIL',
                          'error': str(exc)}, ensure_ascii=False), file=sys.stderr)
        return 2 if isinstance(exc, FileNotFoundError) else 1


if __name__ == '__main__':
    raise SystemExit(main())
