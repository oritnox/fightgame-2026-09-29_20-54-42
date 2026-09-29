// /Assets/_Game/Editor/ProjectSetupValidator.cs
// 공용코드 수정: P00 설정 검사·증거·해시 계약. F131/F139 및 Tools/p00.py 영향.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using UnityEngine;
using UnityEngine.Rendering;

namespace RP.Editor
{
    /// <summary>Read-only setup validation. Never installs packages or invents Unity metadata.</summary>
    public static class ProjectSetupValidator
    {
        public const string ExpectedEditorVersion = "6000.6.3f1";
        public static readonly string[] EditorPackages = {
            "com.unity.render-pipelines.universal", "com.unity.inputsystem", "com.unity.test-framework"
        };
        public static readonly string[] RequiredPackages = {
            "com.unity.render-pipelines.universal", "com.unity.inputsystem",
            "com.unity.cinemachine", "com.unity.animation.rigging", "com.unity.test-framework"
        };
        public static readonly string[] RequiredLayers = {
            "RP_World", "RP_Player", "RP_Enemy", "RP_Hitbox", "RP_Hurtbox", "RP_Interactable"
        };
        public static readonly string[] AssemblyPaths = {
            "Scripts/Core/RP.Core.asmdef", "Scripts/Data/RP.Data.asmdef",
            "Scripts/UnityRuntime/RP.UnityRuntime.asmdef", "Editor/RP.Editor.asmdef",
            "Tests/EditMode/RP.Tests.EditMode.asmdef", "Tests/PlayMode/RP.Tests.PlayMode.asmdef"
        };

        [Serializable] public sealed class Issue
        {
            public string code, path, expected, actual;
            public Issue(string code, string path, string expected, string actual)
            { this.code = code; this.path = path; this.expected = expected; this.actual = actual; }
        }
        [Serializable] public sealed class Snapshot
        {
            public string editorVersion, recordedVersion, packageLockHash;
            public string[] packages = Array.Empty<string>();
            public string[] layers = Array.Empty<string>();
            public bool forceText, inputSystem, urp, windowsModule, windowsTarget;
        }
        [Serializable] public sealed class Report
        {
            public string scope = "P00-UNITY-SETUP";
            public string status, createdUtc, editorVersion, packageLockHash, sourceHash;
            public List<Issue> issues = new List<Issue>();
        }
        [Serializable] private sealed class AssemblyDefinition
        {
            public string name;
            public string[] references, includePlatforms, optionalUnityReferences;
            public bool noEngineReferences, autoReferenced;
        }

        public static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        public static List<Issue> ValidateSnapshot(Snapshot value, bool requireWindowsBuild = true)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            var issues = new List<Issue>();
            if (value.editorVersion != ExpectedEditorVersion)
                issues.Add(new Issue("EDITOR_SERIES", "Editor", ExpectedEditorVersion, value.editorVersion));
            if (value.editorVersion != value.recordedVersion || string.IsNullOrEmpty(value.recordedVersion))
                issues.Add(new Issue("EDITOR_PIN", "ProjectSettings/ProjectVersion.txt", value.editorVersion, value.recordedVersion));
            if (!Regex.IsMatch(value.packageLockHash ?? "", "^[a-f0-9]{64}$"))
                issues.Add(new Issue("PACKAGE_LOCK", "Packages/packages-lock.json", "actual resolved lock", "missing"));
            foreach (string id in requireWindowsBuild ? RequiredPackages : EditorPackages)
                if (!(value.packages ?? Array.Empty<string>()).Contains(id))
                    issues.Add(new Issue("PACKAGE_MISSING", "Packages/manifest.json", id, "not registered"));
            for (int i = 0; i < RequiredLayers.Length; i++)
            {
                string actual = value.layers != null && i < value.layers.Length ? value.layers[i] : "missing";
                if (actual != RequiredLayers[i])
                    issues.Add(new Issue("LAYER", "TagManager/layers/" + (i + 8), RequiredLayers[i], actual));
            }
            CheckFlag(issues, value.forceText, "SERIALIZATION", "EditorSettings", "ForceText");
            CheckFlag(issues, value.inputSystem, "INPUT_SYSTEM", "PlayerSettings", "Input System enabled");
            CheckFlag(issues, value.urp, "URP_ASSET", "GraphicsSettings", "UniversalRenderPipelineAsset");
            if (requireWindowsBuild) CheckFlag(issues, value.windowsModule, "WINDOWS_MODULE", "Editor installation", "Windows x64 module");
            if (requireWindowsBuild) CheckFlag(issues, value.windowsTarget, "WINDOWS_TARGET", "Build Profiles", "active Windows x64");
            return issues;
        }

        private static void CheckFlag(List<Issue> issues, bool actual, string code, string path, string expected)
        {
            if (!actual) issues.Add(new Issue(code, path, expected, "not configured"));
        }

        public static Report CaptureAndValidate(bool requireWindowsBuild = true)
        {
            string root = ProjectRoot;
            string versionPath = Path.Combine(root, "ProjectSettings/ProjectVersion.txt");
            string recorded = File.Exists(versionPath)
                ? Regex.Match(File.ReadAllText(versionPath), @"(?m)^m_EditorVersion:\s*(\S+)").Groups[1].Value : "";
            bool inputEnabled = false;
#if ENABLE_INPUT_SYSTEM
            inputEnabled = true;
#endif
            var snapshot = new Snapshot {
                editorVersion = Application.unityVersion, recordedVersion = recorded,
                packageLockHash = HashFileOrEmpty(Path.Combine(root, "Packages/packages-lock.json")),
                packages = (PackageInfo.GetAllRegisteredPackages() ?? Array.Empty<PackageInfo>()).Select(p => p.name).ToArray(),
                layers = Enumerable.Range(8, RequiredLayers.Length).Select(LayerMask.LayerToName).ToArray(),
                forceText = EditorSettings.serializationMode == SerializationMode.ForceText,
                inputSystem = inputEnabled,
                urp = GraphicsSettings.defaultRenderPipeline != null &&
                    GraphicsSettings.defaultRenderPipeline.GetType().FullName == "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset",
                windowsModule = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64),
                windowsTarget = EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows64
            };
            var report = new Report {
                createdUtc = DateTime.UtcNow.ToString("O"), editorVersion = snapshot.editorVersion,
                packageLockHash = snapshot.packageLockHash, sourceHash = ComputeSourceHash(root),
                issues = ValidateSnapshot(snapshot, requireWindowsBuild),
                scope = requireWindowsBuild ? "P00-WINDOWS-SETUP" : "P00-EDITOR-SETUP"
            };
            ValidateAssemblies(root, report.issues);
            foreach (string path in EnumerateProjectFiles(root).Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) && !p.EndsWith(".meta", StringComparison.Ordinal)))
                if (!File.Exists(Path.Combine(root, path + ".meta")))
                    report.issues.Add(new Issue("META_MISSING", path, "Unity-generated .meta", "missing"));
            report.status = report.issues.Count == 0 ? "PASS" : "BLOCKED";
            return report;
        }

        private static void ValidateAssemblies(string root, List<Issue> issues)
        {
            var graph = new Dictionary<string, string[]>(StringComparer.Ordinal);
            foreach (string relative in AssemblyPaths)
            {
                string path = "Assets/_Game/" + relative;
                try
                {
                    var value = JsonUtility.FromJson<AssemblyDefinition>(File.ReadAllText(Path.Combine(root, path)));
                    string expected = Path.GetFileNameWithoutExtension(path);
                    if (value == null || value.name != expected) throw new InvalidDataException("name mismatch");
                    var refs = value.references ?? Array.Empty<string>();
                    graph.Add(value.name, refs);
                    if (value.name == "RP.Core" && (!value.noEngineReferences || refs.Length != 0))
                        issues.Add(new Issue("CORE_BOUNDARY", path, "noEngineReferences + no assembly references", "invalid"));
                    bool editorOnly = value.name == "RP.Editor" || value.name == "RP.Tests.EditMode";
                    if (editorOnly && !(value.includePlatforms ?? Array.Empty<string>()).SequenceEqual(new[] { "Editor" }))
                        issues.Add(new Issue("EDITOR_BOUNDARY", path, "Editor only", "invalid"));
                    bool test = value.name.StartsWith("RP.Tests.", StringComparison.Ordinal);
                    if (test && (value.autoReferenced || !(value.optionalUnityReferences ?? Array.Empty<string>()).Contains("TestAssemblies")))
                        issues.Add(new Issue("TEST_BOUNDARY", path, "TestAssemblies; autoReferenced=false", "invalid"));
                    if (!editorOnly && !test && refs.Any(r => r.Contains("Editor") || r.StartsWith("RP.Tests.", StringComparison.Ordinal)))
                        issues.Add(new Issue("PLAYER_EDITOR_REFERENCE", path, "no Editor/test references", "invalid"));
                }
                catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is UnauthorizedAccessException)
                { issues.Add(new Issue("ASSEMBLY_INVALID", path, "readable valid asmdef", ex.GetType().Name)); }
            }
            var visiting = new HashSet<string>(); var visited = new HashSet<string>();
            foreach (string name in graph.Keys)
                if (HasCycle(name, graph, visiting, visited))
                { issues.Add(new Issue("ASSEMBLY_CYCLE", name, "acyclic references", "cycle")); break; }
            foreach (var entry in graph)
                foreach (string dep in entry.Value.Where(r => r.StartsWith("RP.", StringComparison.Ordinal)))
                    if (!graph.ContainsKey(dep)) issues.Add(new Issue("ASSEMBLY_REFERENCE", entry.Key, dep, "missing"));
        }

        private static bool HasCycle(string name, Dictionary<string, string[]> graph, HashSet<string> visiting, HashSet<string> visited)
        {
            if (visited.Contains(name) || !graph.ContainsKey(name)) return false;
            if (!visiting.Add(name)) return true;
            foreach (string dep in graph[name]) if (HasCycle(dep, graph, visiting, visited)) return true;
            visiting.Remove(name); visited.Add(name); return false;
        }

        public static string[] EnumerateProjectFiles(string root)
        {
            var paths = new List<string>();
            foreach (string directory in new[] { "Assets", "ProjectSettings" })
            {
                string full = Path.Combine(root, directory);
                if (!Directory.Exists(full)) continue;
                Collect(root, full, paths);
            }
            foreach (string file in new[] { "Packages/manifest.json", "Packages/packages-lock.json" })
                if (File.Exists(Path.Combine(root, file))) paths.Add(file);
            return paths.OrderBy(p => p, StringComparer.Ordinal).ToArray();
        }

        private static void Collect(string root, string directory, List<string> paths)
        {
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Project symbolic links are not supported for evidence hashing.");
            foreach (string entry in Directory.GetFileSystemEntries(directory))
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Project symbolic link rejected.");
                if ((attributes & FileAttributes.Directory) != 0) Collect(root, entry, paths);
                else paths.Add(entry.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace('\\', '/'));
            }
        }

        public static string ComputeSourceHash(string root)
        {
            var text = new StringBuilder();
            foreach (string path in EnumerateProjectFiles(root))
                text.Append(path).Append('\0').Append(HashFileOrEmpty(Path.Combine(root, path))).Append('\n');
            using (var sha = SHA256.Create()) return Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())));
        }
        public static string HashFileOrEmpty(string path)
        {
            if (!File.Exists(path)) return "";
            using (var sha = SHA256.Create()) using (var input = File.OpenRead(path)) return Hex(sha.ComputeHash(input));
        }
        private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();

        [MenuItem("Resonance/P00/Validate setup (read only)")]
        public static void ValidateMenu()
        {
            var report = CaptureAndValidate();
            string path = BuildCommand.WriteNewEvidence("setup-" + Guid.NewGuid().ToString("N"), "setup.json", JsonUtility.ToJson(report, true));
            Debug.Log("P00 setup " + report.status + "; issues=" + report.issues.Count + "; " + path);
        }
        public static void ValidateEditorBatch()
        {
            var report = CaptureAndValidate(false);
            BuildCommand.WriteNewEvidence(BuildCommand.ReadRunId(), "setup.json", JsonUtility.ToJson(report, true));
            if (report.issues.Count != 0) throw new UnityEditor.Build.BuildFailedException("P00 editor setup blocked; see setup.json.");
        }
        public static void ValidateBatch()
        {
            var report = CaptureAndValidate();
            BuildCommand.WriteNewEvidence(BuildCommand.ReadRunId(), "setup.json", JsonUtility.ToJson(report, true));
            if (report.issues.Count != 0) throw new UnityEditor.Build.BuildFailedException("P00 setup blocked; see setup.json.");
        }
    }
}
