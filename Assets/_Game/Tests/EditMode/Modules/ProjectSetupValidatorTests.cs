// /Assets/_Game/Tests/EditMode/Modules/ProjectSetupValidatorTests.cs
// 공용코드 수정: UNIT-F131. 설정 검사·해시 계약 회귀.
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RP.Editor;
using UnityEngine;

namespace RP.Tests.EditMode
{
    public sealed class ProjectSetupValidatorTests
    {
        private static ProjectSetupValidator.Snapshot Valid()
        {
            return new ProjectSetupValidator.Snapshot {
                // Synthetic fixture, not a selected or certified editor/package version.
                editorVersion = ProjectSetupValidator.ExpectedEditorVersion, recordedVersion = ProjectSetupValidator.ExpectedEditorVersion,
                packageLockHash = new string('a', 64),
                packages = ProjectSetupValidator.RequiredPackages.ToArray(),
                layers = ProjectSetupValidator.RequiredLayers.ToArray(),
                forceText = true, inputSystem = true, urp = true, windowsModule = true, windowsTarget = true
            };
        }
        [Test] public void CompleteSnapshotHasNoIssues()
        { Assert.That(ProjectSetupValidator.ValidateSnapshot(Valid()), Is.Empty); }
        [Test] public void ValidationDoesNotMutateSnapshot()
        {
            var value = Valid(); string before = JsonUtility.ToJson(value);
            ProjectSetupValidator.ValidateSnapshot(value);
            Assert.That(JsonUtility.ToJson(value), Is.EqualTo(before));
        }
        [Test] public void NullSnapshotIsRejected()
        { Assert.Throws<ArgumentNullException>(() => ProjectSetupValidator.ValidateSnapshot(null)); }
        [TestCase("6000.2.1f1")][TestCase("6000.3.1b1")][TestCase("")]
        public void WrongEditorSeriesIsRejected(string version)
        {
            var value = Valid(); value.editorVersion = version;
            Assert.That(ProjectSetupValidator.ValidateSnapshot(value).Any(i => i.code == "EDITOR_SERIES"), Is.True);
        }
        [Test] public void DifferentRecordedVersionIsRejected()
        {
            var value = Valid(); value.recordedVersion = "6000.3.998f1";
            Assert.That(ProjectSetupValidator.ValidateSnapshot(value).Any(i => i.code == "EDITOR_PIN"), Is.True);
        }
        [Test] public void MissingPackageAndLockAreReported()
        {
            var value = Valid(); value.packages = Array.Empty<string>(); value.packageLockHash = "";
            var issues = ProjectSetupValidator.ValidateSnapshot(value);
            Assert.That(issues.Count(i => i.code == "PACKAGE_MISSING"), Is.EqualTo(ProjectSetupValidator.RequiredPackages.Length));
            Assert.That(issues.Any(i => i.code == "PACKAGE_LOCK"), Is.True);
        }
        [Test] public void WrongLayerDoesNotGetSilentlyReassigned()
        {
            var value = Valid(); value.layers[0] = "ExistingUserLayer";
            var issues = ProjectSetupValidator.ValidateSnapshot(value);
            Assert.That(issues.Any(i => i.code == "LAYER" && i.actual == "ExistingUserLayer"), Is.True);
            Assert.That(value.layers[0], Is.EqualTo("ExistingUserLayer"));
        }
        [Test] public void MissingRuntimeConfigurationBlocksSetup()
        {
            var value = Valid(); value.inputSystem = value.urp = value.forceText = value.windowsModule = value.windowsTarget = false;
            Assert.That(ProjectSetupValidator.ValidateSnapshot(value).Count, Is.EqualTo(5));
        }
        [Test] public void EditorTestsDoNotRequireWindowsOrFutureAuthoringPackages()
        {
            var value = Valid(); value.windowsModule = false; value.windowsTarget = false;
            value.packages = ProjectSetupValidator.EditorPackages.ToArray();
            Assert.That(ProjectSetupValidator.ValidateSnapshot(value, false), Is.Empty);
            Assert.That(ProjectSetupValidator.ValidateSnapshot(value, true), Is.Not.Empty);
        }
        [Test] public void DifferentFinalPatchIsRejectedWithoutChangingProject()
        {
            var value = Valid(); value.editorVersion = "6000.6.4f1";
            Assert.That(ProjectSetupValidator.ValidateSnapshot(value).Any(i => i.code == "EDITOR_SERIES"), Is.True);
        }
        [Test] public void HashChangesWithProjectSourceNotBuildArtifacts()
        {
            string root = Path.Combine(Path.GetTempPath(), "rp-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "Assets"));
            try
            {
                string file = Path.Combine(root, "Assets/a.txt"); File.WriteAllText(file, "a");
                string first = ProjectSetupValidator.ComputeSourceHash(root);
                Directory.CreateDirectory(Path.Combine(root, "Artifacts"));
                File.WriteAllText(Path.Combine(root, "Artifacts/log.txt"), "log");
                Assert.That(ProjectSetupValidator.ComputeSourceHash(root), Is.EqualTo(first));
                File.WriteAllText(file, "b");
                Assert.That(ProjectSetupValidator.ComputeSourceHash(root), Is.Not.EqualTo(first));
            }
            finally { Directory.Delete(root, true); }
        }
    }
}
