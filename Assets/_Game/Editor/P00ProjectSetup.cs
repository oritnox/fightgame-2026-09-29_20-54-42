// /Assets/_Game/Editor/P00ProjectSetup.cs
// 공용코드 수정: P00 초기화. 기존 씬/패키지/버전을 보존하며 F131/F139와 함께 검증한다.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RP.Editor
{
    /// <summary>Explicit, idempotent local-editor preparation; never runs on import.</summary>
    public static class P00ProjectSetup
    {
        public static string[] PlanLayers(string[] existing)
        {
            if (existing == null || existing.Length != 32)
                throw new ArgumentException("Expected exactly 32 Unity layer slots.");
            string[] planned = existing.ToArray();
            for (int i = 0; i < ProjectSetupValidator.RequiredLayers.Length; i++)
            {
                int slot = 8 + i;
                string required = ProjectSetupValidator.RequiredLayers[i];
                if (!string.IsNullOrEmpty(existing[slot]) && existing[slot] != required)
                    throw new InvalidOperationException("Layer " + slot + " is occupied; no existing layer was replaced.");
                for (int j = 0; j < existing.Length; j++)
                    if (j != slot && existing[j] == required)
                        throw new InvalidOperationException("Required layer already uses another index: " + required);
                planned[slot] = required;
            }
            return planned;
        }

        [MenuItem("Resonance/P00/Prepare project (preserve existing assets)")]
        public static void PrepareMenu()
        {
            if (!EditorUtility.DisplayDialog("P00 preparation",
                "Create a separate P00 validation scene and fill only empty RP layer slots 8-13. " +
                "Use Force Text serialization. Existing scenes, packages, editor version and global build scene list stay unchanged.",
                "Prepare", "Cancel")) return;
            PrepareBatch();
        }

        public static void PrepareBatch()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Editor must be idle.");
            if (Application.unityVersion != ProjectSetupValidator.ExpectedEditorVersion)
                throw new InvalidOperationException("Use the existing project's pinned editor; no version migration is performed.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save or discard scene edits explicitly before preparation.");
            string root = ProjectSetupValidator.ProjectRoot;
            string version = File.ReadAllText(Path.Combine(root, "ProjectSettings/ProjectVersion.txt"));
            if (!System.Text.RegularExpressions.Regex.IsMatch(version,
                @"(?m)^m_EditorVersion: " + System.Text.RegularExpressions.Regex.Escape(Application.unityVersion) + @"\s*$"))
                throw new InvalidOperationException("ProjectVersion does not match the running editor.");
            string[] protectedPaths = { "Packages/manifest.json", "Packages/packages-lock.json", "ProjectSettings/ProjectVersion.txt" };
            string[] protectedHashes = protectedPaths.Select(p => ProjectSetupValidator.HashFileOrEmpty(Path.Combine(root, p))).ToArray();
            if (protectedHashes.Any(string.IsNullOrEmpty))
                throw new InvalidOperationException("Resolve the existing project in Unity before preparation.");
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets.Length == 0) throw new IOException("TagManager is unavailable.");
            var settings = new SerializedObject(assets[0]);
            var layers = settings.FindProperty("layers");
            if (layers == null || layers.arraySize != 32) throw new InvalidOperationException("Unexpected layer settings layout.");
            string[] existing = Enumerable.Range(0, 32).Select(i => layers.GetArrayElementAtIndex(i).stringValue).ToArray();
            string[] planned = PlanLayers(existing); // Validate ALL conflicts before any mutation.
            BuildCommand.CreateEmptyScene(); // Separate additive scene, never replaces SampleScene.
            for (int i = 8; i < 8 + ProjectSetupValidator.RequiredLayers.Length; i++)
                layers.GetArrayElementAtIndex(i).stringValue = planned[i];
            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(assets[0]);
            EditorSettings.serializationMode = SerializationMode.ForceText;
            AssetDatabase.Refresh();
            for (int i = 0; i < protectedPaths.Length; i++)
                if (protectedHashes[i] != ProjectSetupValidator.HashFileOrEmpty(Path.Combine(root, protectedPaths[i])))
                    throw new IOException("An external change affected " + protectedPaths[i] + "; review it, no automatic rollback.");
            var report = ProjectSetupValidator.CaptureAndValidate(false);
            string path = BuildCommand.WriteNewEvidence("prepare-" + Guid.NewGuid().ToString("N"), "setup.json", JsonUtility.ToJson(report, true));
            Debug.Log("P00 editor preparation " + report.status + ". Report: " + path +
                ". Run EditMode tests next. This is not a Windows build or EXIT-P00 approval.");
            if (report.issues.Count != 0)
                throw new UnityEditor.Build.BuildFailedException("Preparation completed with unresolved setup blockers; see report.");
        }

        [MenuItem("Resonance/P00/Validate editor (no Windows required)")]
        public static void ValidateEditorMenu()
        {
            var report = ProjectSetupValidator.CaptureAndValidate(false);
            string path = BuildCommand.WriteNewEvidence("editor-" + Guid.NewGuid().ToString("N"), "setup.json", JsonUtility.ToJson(report, true));
            Debug.Log("P00 editor setup " + report.status + "; issues=" + report.issues.Count + "; " + path);
        }
    }
}
