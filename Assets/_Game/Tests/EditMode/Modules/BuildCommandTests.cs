// /Assets/_Game/Tests/EditMode/Modules/BuildCommandTests.cs
// 공용코드 수정: UNIT-F139. 경로 보호·시험 영수증 게이트 회귀.
using System;
using System.IO;
using NUnit.Framework;
using RP.Editor;

namespace RP.Tests.EditMode
{
    public sealed class BuildCommandTests
    {
        private const string Xml = "<test-run result='Passed' total='3' passed='3' failed='0' skipped='0' inconclusive='0'>" +
            "<test-case fullname='RP.Tests.EditMode.ProjectSetupValidatorTests.One' result='Passed'/>" +
            "<test-case fullname='RP.Tests.EditMode.BuildCommandTests.One' result='Passed'/><test-case fullname='RP.Tests.EditMode.P00ProjectSetupTests.One' result='Passed'/></test-run>";
        [TestCase("p00-20260929-a1")][TestCase("A")][TestCase("run_01.2")]
        public void SafeRunIdsAreAccepted(string value)
        { Assert.That(BuildCommand.IsValidRunId(value), Is.True); }
        [TestCase("../bad")][TestCase("/tmp")][TestCase("a\\b")][TestCase("NUL")]
        [TestCase("COM1.txt")][TestCase("run.")][TestCase("")][TestCase("bad\n")]
        public void UnsafeRunIdsAreRejected(string value)
        { Assert.That(BuildCommand.IsValidRunId(value), Is.False); }
        [Test] public void OversizedRunIdIsRejected()
        { Assert.That(BuildCommand.IsValidRunId(new string('a', 65)), Is.False); }
        [TestCase("../other")][TestCase("Artifacts/../Assets")][TestCase("/absolute")]
        [TestCase("Artifacts//double")][TestCase("C:/output")][TestCase("Artifacts\\output")]
        public void UnsafeOutputPathsAreRejected(string relative)
        { Assert.Throws<ArgumentException>(() => BuildCommand.ContainedPath(Path.GetFullPath("."), relative)); }
        [Test] public void ValidOutputPathIsContained()
        {
            string root = Path.GetFullPath(".");
            Assert.That(BuildCommand.ContainedPath(root, "Artifacts/Builds/P00/run-1"),
                Is.EqualTo(Path.Combine(root, "Artifacts", "Builds", "P00", "run-1")));
        }
        [Test] public void BothPassingFixturesAreRequired()
        { Assert.That(BuildCommand.HasPassingP00Tests(Xml), Is.True); }
        [Test] public void EmptyRunCannotPass()
        { Assert.That(BuildCommand.HasPassingP00Tests("<test-run result='Passed' total='0' passed='0' failed='0' skipped='0' inconclusive='0'/>"), Is.False); }
        [Test] public void SkippedTestsCannotPass()
        { Assert.That(BuildCommand.HasPassingP00Tests(Xml.Replace("skipped='0'", "skipped='1'")), Is.False); }
        [Test] public void MissingFixtureCannotPass()
        { Assert.That(BuildCommand.HasPassingP00Tests(Xml.Replace("BuildCommandTests.One", "UnrelatedTests.One")), Is.False); }
        [Test] public void FailedCaseCannotPassWithGreenRoot()
        { Assert.That(BuildCommand.HasPassingP00Tests(Xml.Replace("One' result='Passed'", "One' result='Failed'")), Is.False); }
        [Test] public void InconsistentCaseCountCannotPass()
        { Assert.That(BuildCommand.HasPassingP00Tests(Xml.Replace("total='3' passed='3'", "total='4' passed='4'")), Is.False); }
        [Test] public void DtdAndMalformedXmlAreRejected()
        {
            Assert.That(BuildCommand.HasPassingP00Tests("<!DOCTYPE test-run [<!ENTITY x SYSTEM 'file:///etc/passwd'>]>" + Xml), Is.False);
            Assert.That(BuildCommand.HasPassingP00Tests("<test-run"), Is.False);
        }
    }
}
