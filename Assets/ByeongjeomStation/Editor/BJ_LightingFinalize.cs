using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Byeongjeom.MapGen
{
    public static partial class BJ
    {
        [MenuItem("Tools/병점역 맵/조작 안내판 작성 및 조명 마무리 %#F9", false, 26)]
        static void FinalizeLightingAndGuide()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning) return;
            var root = GameObject.Find("ByeongjeomStation_병점역");
            var guide = GameObject.Find("guide");
            if (root == null || guide == null) throw new InvalidOperationException("Station root or guide is missing.");
            Directory.CreateDirectory(BatchReportDir);
            var scene = root.scene;
            RequireLighting(scene == guide.scene, "guide belongs to a different scene.");
            WriteGuide(guide);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            var report = new StringBuilder("Live editor finalization\n");
            report.AppendLine("Guide: two readable faces, existing transform and collider preserved.");
            report.AppendLine(PrepareBake(root, BakeMoonMode));
            EditorSceneManager.SaveScene(scene);
            File.WriteAllText(BatchReportDir + "/live-progress.txt", report.ToString());
            Debug.Log("[LightingFinalize] Prepared.\n" + report);
            var start = DateTime.UtcNow;
            string uvWarning = "";
            Application.LogCallback onLog = (message, stack, type) =>
            {
                if (message.Contains("objects in the Scene with overlapping UV")) uvWarning = message;
            };
            Application.logMessageReceived += onLog;
            if (!Lightmapping.BakeAsync())
            {
                Application.logMessageReceived -= onLog;
                throw new InvalidOperationException("Could not start the bake.");
            }
            EditorApplication.CallbackFunction poll = null;
            poll = () =>
            {
                if (Lightmapping.isRunning) return;
                EditorApplication.update -= poll;
                Application.logMessageReceived -= onLog;
                try
                {
                    RequireLighting(root != null && guide != null, "Scene was closed during bake.");
                    string done = FinishBake(root, (DateTime.UtcNow - start).TotalSeconds);
                    RequireLighting(done != null, "Bake failed or was cancelled.");
                    report.AppendLine(done);
                    ValidateLightingBake(root, report, false);
                    RequireLighting(guide.transform.Find("Controls_Front") != null && guide.transform.Find("Controls_Back") != null,
                        "Guide text faces are missing.");
                    report.AppendLine(string.IsNullOrEmpty(uvWarning) ? "UV overlap warnings: 0" : uvWarning);
                    AssetDatabase.SaveAssets();
                    EditorSceneManager.SaveScene(scene);
                    CaptureLightingPreview("after.png");
                    CaptureGuidePreview(guide);
                    File.WriteAllText(BatchReportDir + "/result.txt", report.ToString());
                    Debug.Log("[LightingFinalize] SUCCESS\n" + report);
                    Selection.activeGameObject = guide;
                }
                catch (Exception e)
                {
                    File.WriteAllText(BatchReportDir + "/live-failure.txt", e.ToString());
                    Debug.LogException(e);
                }
            };
            EditorApplication.update += poll;
        }

        static void WriteGuide(GameObject guide)
        {
            const string texturePath = "Assets/ByeongjeomStation/Textures/Guide/GuideControls.png";
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            var material = AssetDatabase.LoadAssetAtPath<Material>(GenDir + "/Materials/GuideControls.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Unlit/Texture"));
                AssetDatabase.CreateAsset(material, GenDir + "/Materials/GuideControls.mat");
            }
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            EditorUtility.SetDirty(material);
            for (int face = 0; face < 2; face++)
            {
                string name = face == 0 ? "Controls_Front" : "Controls_Back";
                var child = guide.transform.Find(name);
                if (child == null)
                {
                    child = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)).transform;
                    child.SetParent(guide.transform, false);
                }
                float x = face == 0 ? -0.515f : 0.515f;
                float z = face == 0 ? 0.48f : -0.48f;
                string path = GenDir + "/Guide" + face + ".asset";
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh == null) { mesh = new Mesh { name = name }; AssetDatabase.CreateAsset(mesh, path); }
                mesh.Clear();
                mesh.vertices = new[] { new Vector3(x,-.48f,z), new Vector3(x,-.48f,-z), new Vector3(x,.48f,z), new Vector3(x,.48f,-z) };
                mesh.uv = new[] { new Vector2(0,0), new Vector2(1,0), new Vector2(0,1), new Vector2(1,1) };
                mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                EditorUtility.SetDirty(mesh);
                child.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = child.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
        }

        static void CaptureGuidePreview(GameObject guide)
        {
            var go = new GameObject("GuidePreviewCamera");
            try
            {
                var camera = go.AddComponent<Camera>();
                camera.transform.position = guide.transform.position - guide.transform.right * 5.5f;
                camera.transform.rotation = Quaternion.LookRotation(guide.transform.right, guide.transform.up);
                camera.fieldOfView = 28f;
                camera.nearClipPlane = .05f;
                camera.farClipPlane = 30f;
                var savedMain = Camera.main;
                string tag = savedMain != null ? savedMain.tag : null;
                if (savedMain != null) savedMain.tag = "Untagged";
                camera.tag = "MainCamera";
                try { CaptureLightingPreview("guide.png"); }
                finally { if (savedMain != null) savedMain.tag = tag; }
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
