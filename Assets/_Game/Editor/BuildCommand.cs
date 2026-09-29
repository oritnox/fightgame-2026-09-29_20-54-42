// /Assets/_Game/Editor/BuildCommand.cs
// 공용코드 수정: P00 빌드·시험 증거 계약. F131, Windows Build Profile 경로 및 Tools/p00.py 영향.
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RP.Editor
{
    /// <summary>Only a P00 empty Windows build. Does not claim BUILD-01 or Windows smoke completion.</summary>
    public static class BuildCommand
    {
        public const string ScenePath = "Assets/_Game/Scenes/P00_EMPTY.unity";
        public const string ProfilePath = "Assets/Settings/Build Profiles/Windows.asset";
        [Serializable] private sealed class TestReceipt
        { public int schemaVersion; public string runId, sourceHash, resultsSha256, status, scope; }
        [Serializable] private sealed class BuildEvidence
        {
            public int schemaVersion = 1;
            public string scope = "P00-EMPTY-BUILD";
            public string runId, status, createdUtc, editorVersion, packageLockHash, sourceHash, executableSha256;
            public string playerExecution = "NOT_RUN";
            public string fullBuild01 = "NOT_RUN";
            public string detail;
        }

        public static bool IsValidRunId(string value)
        {
            if (value == null || !Regex.IsMatch(value, @"\A[A-Za-z0-9][A-Za-z0-9._-]{0,63}\z") || value.EndsWith(".", StringComparison.Ordinal)) return false;
            string stem = value.Split('.')[0];
            return !Regex.IsMatch(stem, @"\A(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
        public static string ReadRunId()
        {
            string[] args = Environment.GetCommandLineArgs();
            var hits = Enumerable.Range(0, args.Length).Where(i => args[i] == "-rpRunId").ToArray();
            if (hits.Length != 1 || hits[0] + 1 >= args.Length || !IsValidRunId(args[hits[0] + 1]))
                throw new BuildFailedException("Supply exactly one safe -rpRunId (1-64 ASCII characters).");
            return args[hits[0] + 1];
        }

        public static string ContainedPath(string root, string relative)
        {
            if (string.IsNullOrEmpty(relative) || Path.IsPathRooted(relative) || relative.Contains("\\") || relative.Contains(":"))
                throw new ArgumentException("Relative slash path required.", nameof(relative));
            string[] parts = relative.Split('/');
            if (parts.Any(p => string.IsNullOrEmpty(p) || p == "." || p == "..")) throw new ArgumentException("Traversal rejected.");
            string fullRoot = Path.GetFullPath(root);
            string full = Path.GetFullPath(Path.Combine(fullRoot, relative));
            var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!full.StartsWith(fullRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, comparison))
                throw new ArgumentException("Path escapes project.");
            string current = fullRoot;
            if (Directory.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Symbolic root rejected.");
            foreach (string part in parts)
            {
                current = Path.Combine(current, part);
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Symbolic output path rejected.");
            }
            return full;
        }
        public static string WriteNewEvidence(string runId, string name, string json)
        {
            if (!IsValidRunId(runId) || !Regex.IsMatch(name ?? "", @"\A[A-Za-z0-9_-]+\.json\z")) throw new ArgumentException("Invalid evidence name.");
            string path = ContainedPath(ProjectSetupValidator.ProjectRoot, "Artifacts/Evidence/" + runId + "/" + name);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) writer.Write(json);
            return path;
        }

        public static bool HasPassingP00Tests(string xml)
        {
            if (string.IsNullOrEmpty(xml) || xml.Length > 4 * 1024 * 1024) return false;
            try
            {
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4 * 1024 * 1024 };
                using (var input = new StringReader(xml)) using (var reader = XmlReader.Create(input, settings))
                {
                    var root = XDocument.Load(reader).Root;
                    if (root == null || root.Name.LocalName != "test-run" || (string)root.Attribute("result") != "Passed") return false;
                    if (!int.TryParse((string)root.Attribute("total"), out int total) || total <= 0) return false;
                    if (!int.TryParse((string)root.Attribute("passed"), out int passed) || total != passed) return false;
                    foreach (string name in new[] { "failed", "skipped", "inconclusive" })
                        if (!int.TryParse((string)root.Attribute(name), out int count) || count != 0) return false;
                    var cases = root.Descendants("test-case").ToArray();
                    if (cases.Length != total || cases.Any(c => (string)c.Attribute("result") != "Passed")) return false;
                    return new[] { "RP.Tests.EditMode.ProjectSetupValidatorTests.", "RP.Tests.EditMode.BuildCommandTests.", "RP.Tests.EditMode.P00ProjectSetupTests." }
                        .All(prefix => cases.Any(c => ((string)c.Attribute("fullname") ?? "").StartsWith(prefix, StringComparison.Ordinal)));
                }
            }
            catch (XmlException) { return false; }
        }

        private static void RequirePassingTests(string runId, string sourceHash)
        {
            string root = ProjectSetupValidator.ProjectRoot;
            string xmlPath = ContainedPath(root, "Artifacts/Evidence/" + runId + "/editmode.xml");
            string receiptPath = ContainedPath(root, "Artifacts/Evidence/" + runId + "/test-receipt.json");
            if (!File.Exists(receiptPath) || !File.Exists(xmlPath) || new FileInfo(receiptPath).Length > 65536 || new FileInfo(xmlPath).Length > 4 * 1024 * 1024)
                throw new BuildFailedException("Missing/bounded P00 test evidence. Run Tools/p00.py unity first.");
            var receipt = JsonUtility.FromJson<TestReceipt>(File.ReadAllText(receiptPath));
            if (receipt == null || receipt.schemaVersion != 2 || receipt.scope != "P00-WINDOWS-PREBUILD" || receipt.status != "PASS" || receipt.runId != runId || receipt.sourceHash != sourceHash ||
                receipt.resultsSha256 != ProjectSetupValidator.HashFileOrEmpty(xmlPath) || !HasPassingP00Tests(File.ReadAllText(xmlPath)))
                throw new BuildFailedException("P00 tests failed, skipped, stale, or belong to another source/run.");
        }

        [MenuItem("Resonance/P00/Create empty validation scene")]
        public static void CreateEmptyScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new InvalidOperationException("Editor must be idle.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save or discard scene edits before scene preparation.");
            string path = ContainedPath(ProjectSetupValidator.ProjectRoot, ScenePath);
            if (File.Exists(path)) { Debug.Log("P00 scene already exists; left unchanged."); return; }
            if (File.Exists(path + ".meta")) throw new IOException("Orphan scene metadata; inspect before creating.");
            EnsureAssetFolder("Assets/_Game/Scenes"); EnsureAssetFolder("Assets/_Game/Config");
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                var cameraObject = new GameObject("P00 Validation Camera");
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                cameraObject.AddComponent<Camera>(); cameraObject.AddComponent<AudioListener>();
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Scene save failed.");
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
            Debug.Log("P00 scene created with Unity metadata. Create/activate a Windows BuildProfile at " + ProfilePath + " and enable only " + ScenePath);
        }
        private static void EnsureAssetFolder(string path)
        {
            string[] parts = path.Split('/'); string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next) && string.IsNullOrEmpty(AssetDatabase.CreateFolder(current, parts[i])))
                    throw new IOException("Unable to create Unity asset folder: " + next);
                current = next;
            }
        }

        public static void BuildP00()
        {
            string runId = ReadRunId();
            string root = ProjectSetupValidator.ProjectRoot;
            string buildDir = ContainedPath(root, "Artifacts/Builds/P00/" + runId);
            if (Directory.Exists(buildDir) || File.Exists(buildDir)) throw new BuildFailedException("Build output already exists; use a new run ID.");
            string evidencePath = ContainedPath(root, "Artifacts/Evidence/" + runId + "/build.json");
            if (File.Exists(evidencePath)) throw new BuildFailedException("Build evidence already exists; no overwrite.");
            var evidence = new BuildEvidence { runId = runId, status = "FAIL", createdUtc = DateTime.UtcNow.ToString("O"), editorVersion = Application.unityVersion };
            try
            {
                if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) throw new BuildFailedException("Editor must be idle.");
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    if (SceneManager.GetSceneAt(i).isDirty) throw new BuildFailedException("Save or discard scene edits explicitly before building.");
                var setup = ProjectSetupValidator.CaptureAndValidate();
                evidence.sourceHash = setup.sourceHash; evidence.packageLockHash = setup.packageLockHash;
                if (setup.issues.Count != 0) throw new BuildFailedException("Project setup has blocking issues; run Validate setup.");
                var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(ProfilePath);
                if (profile == null || BuildProfile.GetActiveBuildProfile() != profile || !profile.overrideGlobalScenes)
                    throw new BuildFailedException("Activate the native P00 Windows BuildProfile with its own scene list.");
                string[] scenes = (profile.scenes ?? Array.Empty<EditorBuildSettingsScene>()).Where(s => s.enabled).Select(s => s.path).ToArray();
                if (scenes.Length != 1 || scenes[0] != ScenePath || AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                    throw new BuildFailedException("Only the P00 validation scene is allowed; training/product builds are not yet implemented.");
                if ((profile.scriptingDefines ?? Array.Empty<string>()).Length != 0)
                    throw new BuildFailedException("P00 profile-specific defines require a separate reviewed change.");
                RequirePassingTests(runId, setup.sourceHash);
                Directory.CreateDirectory(buildDir);
                string executable = Path.Combine(buildDir, "fightgame.exe");
                var result = BuildPipeline.BuildPlayer(new BuildPlayerWithProfileOptions {
                    buildProfile = profile, locationPathName = executable,
                    options = BuildOptions.StrictMode | BuildOptions.DetailedBuildReport
                });
                if (result == null || result.summary.result != BuildResult.Succeeded || !File.Exists(executable))
                    throw new BuildFailedException("BuildPipeline did not produce a successful Windows executable.");
                if (ProjectSetupValidator.ComputeSourceHash(root) != setup.sourceHash)
                    throw new BuildFailedException("Source changed during build; evidence invalid, rerun tests.");
                evidence.status = "PASS"; evidence.executableSha256 = ProjectSetupValidator.HashFileOrEmpty(executable);
                evidence.detail = "Empty build only. Windows launch/exit and BUILD-01 remain NOT_RUN.";
            }
            catch (Exception ex) { evidence.detail = ex.GetType().Name + ": " + ex.Message; throw; }
            finally { WriteNewEvidence(runId, "build.json", JsonUtility.ToJson(evidence, true)); }
        }
    }
}
