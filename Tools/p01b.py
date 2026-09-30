# /Tools/p01b.py
# 공용코드 수정: P01-B 35개 adapter 시험·증거 검증. P01-A/P00/Windows 영수증과 분리한다.
"""Run actual Unity EditMode tests for P01-B Unity adapters; not device timing acceptance."""
from __future__ import annotations
import argparse
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import uuid
import xml.etree.ElementTree as ET

_SPEC = importlib.util.spec_from_file_location('_rp_p01_dependency', Path(__file__).with_name('p01.py'))
if _SPEC is None or _SPEC.loader is None:
    raise RuntimeError('Existing Tools/p01.py is required.')
p01 = importlib.util.module_from_spec(_SPEC)
_SPEC.loader.exec_module(p01)
p00 = p01.p00
ROOT = Path(__file__).resolve().parents[1]
ASSEMBLY = 'RP.Tests.UnityAdapters.EditMode'
EXPECTED = {
    'UnityClockAdapterTests': 7,
    'UnityInputAdapterTests': 6,
    'LocalTraceRecorderTests': 5,
    'TestFixtureBuilderTests': 2,
    'AdapterBoundaryTests': 15,
}


def validate_results(path: Path) -> dict:
    if not path.is_file() or path.stat().st_size > 8 * 1024 * 1024:
        raise ValueError('Missing or oversized adapter EditMode result')
    raw = path.read_bytes()
    if b'<!DOCTYPE' in raw.upper() or b'<!ENTITY' in raw.upper() or b'\x00' in raw:
        raise ValueError('Unsafe or binary XML')
    try:
        root = ET.fromstring(raw.decode('utf-8-sig'))
        total = int(root.get('total', '-1'))
        passed = int(root.get('passed', '-1'))
        if root.tag != 'test-run' or root.get('result') != 'Passed' or total <= 0 or passed != total:
            raise ValueError('Adapter run did not fully pass')
        if any(int(root.get(k, '-1')) != 0 for k in ('failed', 'skipped', 'inconclusive')):
            raise ValueError('Failed/skipped/inconclusive adapter tests are not accepted')
        cases = list(root.iter('test-case'))
        names = [c.get('fullname', '') for c in cases]
        if len(cases) != total or len(names) != len(set(names)) or any(c.get('result') != 'Passed' for c in cases):
            raise ValueError('Adapter test cases disagree with summary')
        observed = {f: sum(n.startswith('RP.Tests.UnityAdapters.' + f + '.') for n in names) for f in EXPECTED}
        if observed != EXPECTED or total != sum(EXPECTED.values()):
            raise ValueError('Partial/changed adapter test inventory')
        return {'status': 'PASS', 'total': total, 'passed': passed, 'failed': 0,
                'skipped': 0, 'fixtures': list(EXPECTED)}
    except (ET.ParseError, UnicodeDecodeError, ValueError, OverflowError) as exc:
        if isinstance(exc, ValueError):
            raise
        raise ValueError('Malformed adapter test results') from exc


def run_unity(root: Path, editor: Path, run_id: str, timeout: int) -> dict:
    if not p00.valid_run_id(run_id) or timeout <= 0:
        raise ValueError('Safe run ID and positive timeout required')
    if not editor.is_file():
        raise FileNotFoundError('Actual licensed Unity executable is required')
    if p00.contained(root, 'Temp/UnityLockfile').exists():
        raise ValueError('Close Unity first; lock is preserved')
    p01_packet = p01.inspect_sources(root)
    if p01_packet['status'] != 'PASS':
        raise ValueError(json.dumps(p01_packet, ensure_ascii=False))
    setup = p00.audit(root, strict=True, for_build=False)
    if setup['status'] != 'PASS':
        raise ValueError('P00 editor setup is not ready: ' + json.dumps(setup, ensure_ascii=False))
    folder = p00.contained(root, 'Artifacts/Evidence/' + run_id)
    folder.mkdir(parents=True, exist_ok=False)
    before = p00.source_hash(root)
    result = {
        'scope': 'P01-UNITY-ADAPTERS-EDITOR', 'runId': run_id, 'status': 'FAIL',
        'startedUtc': p00.utc(), 'sourceHash': before, 'windowsBuild': 'NOT_RUN',
        'windowsSmoke': 'NOT_RUN', 'exitP01': 'NOT_COMPLETE',
    }
    base = [str(editor.absolute()), '-batchmode', '-projectPath', str(root.absolute()), '-rpRunId', run_id]

    def execute(label: str, args: list[str]) -> None:
        subprocess.run(base + ['-logFile', str(folder / (label + '.log'))] + args,
                       check=True, timeout=timeout, cwd=root, shell=False)
    try:
        execute('adapter-editmode', ['-runTests', '-testPlatform', 'EditMode', '-assemblyNames', ASSEMBLY,
                                    '-testResults', str(folder / 'adapter-editmode.xml')])
        summary = validate_results(folder / 'adapter-editmode.xml')
        if p00.source_hash(root) != before:
            raise ValueError('Project changed during adapter tests; review and rerun')
        p00.new_json(folder / 'adapter-test-receipt.json', {
            'schemaVersion': 1, 'scope': result['scope'], 'status': 'PASS', 'runId': run_id,
            'sourceHash': before, 'resultsSha256': p00.hash_file(folder / 'adapter-editmode.xml'),
            'summary': summary, 'createdUtc': p00.utc(), 'isWindowsReceipt': False,
        })
        result['status'] = 'PASS'
        result['tests'] = summary
    except (OSError, ValueError, subprocess.SubprocessError) as exc:
        result['errorType'] = type(exc).__name__
        raise
    finally:
        result['finishedUtc'] = p00.utc()
        p00.new_json(folder / 'adapter-runner.json', result)
    return result


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=ROOT)
    sub = parser.add_subparsers(dest='command', required=True)
    unity = sub.add_parser('unity')
    unity.add_argument('--editor', type=Path, required=True)
    unity.add_argument('--run-id', default='p01b-' + uuid.uuid4().hex[:12])
    unity.add_argument('--timeout', type=int, default=1800)
    args = parser.parse_args(argv)
    try:
        result = run_unity(args.root.absolute(), args.editor, args.run_id, args.timeout)
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 0
    except (OSError, ValueError, subprocess.SubprocessError) as exc:
        print(json.dumps({'scope': 'P01B-TOOL',
                          'status': 'BLOCKED' if isinstance(exc, FileNotFoundError) else 'FAIL',
                          'error': str(exc)}, ensure_ascii=False), file=sys.stderr)
        return 2 if isinstance(exc, FileNotFoundError) else 1


if __name__ == '__main__':
    raise SystemExit(main())
