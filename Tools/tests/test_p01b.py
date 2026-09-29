# /Tools/tests/test_p01b.py
# 공용코드 수정: P01-B 증거 parser와 Unity 미실행 경계 회귀.
import importlib.util
from pathlib import Path
import tempfile
import unittest

MODULE_PATH = Path(__file__).resolve().parents[1] / 'p01b.py'
spec = importlib.util.spec_from_file_location('p01b_under_test', MODULE_PATH)
p01b = importlib.util.module_from_spec(spec)
spec.loader.exec_module(p01b)


def good_xml():
    cases=[]
    for fixture,count in p01b.EXPECTED.items():
        for i in range(count):
            cases.append(f"<test-case fullname='RP.Tests.UnityAdapters.{fixture}.Case{i}' result='Passed'/>")
    total=len(cases)
    return f"<test-run result='Passed' total='{total}' passed='{total}' failed='0' skipped='0' inconclusive='0'>"+''.join(cases)+"</test-run>"


class P01BToolTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory(prefix='rp-p01b-')
        self.root=Path(self.temp.name)
    def tearDown(self): self.temp.cleanup()
    def write(self,text):
        p=self.root/'result.xml'; p.write_text(text,encoding='utf-8'); return p

    def test_exact_inventory_passes(self):
        result=p01b.validate_results(self.write(good_xml()))
        self.assertEqual(20,result['total']); self.assertEqual('PASS',result['status'])

    def test_skip_is_rejected(self):
        with self.assertRaises(ValueError): p01b.validate_results(self.write(good_xml().replace("skipped='0'","skipped='1'")))

    def test_missing_fixture_is_rejected(self):
        text=good_xml().replace('TestFixtureBuilderTests.Case0','OtherTests.Case0')
        with self.assertRaises(ValueError): p01b.validate_results(self.write(text))

    def test_duplicate_identity_is_rejected(self):
        text=good_xml().replace('UnityInputAdapterTests.Case1','UnityInputAdapterTests.Case0')
        with self.assertRaises(ValueError): p01b.validate_results(self.write(text))

    def test_failed_case_is_rejected(self):
        text=good_xml().replace("Case0' result='Passed'","Case0' result='Failed'",1)
        with self.assertRaises(ValueError): p01b.validate_results(self.write(text))

    def test_dtd_is_rejected(self):
        with self.assertRaises(ValueError): p01b.validate_results(self.write("<!DOCTYPE test-run [<!ENTITY x SYSTEM 'file:///etc/passwd'>]>"+good_xml()))

    def test_missing_editor_is_blocking(self):
        with self.assertRaises(FileNotFoundError): p01b.run_unity(self.root,self.root/'missing-unity','p01b-test',30)


if __name__=='__main__': unittest.main()
