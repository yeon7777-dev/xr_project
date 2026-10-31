using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Byeongjeom.MapGen
{
    public static partial class BJ
    {
        const string BatchScenePath = "Assets/ByeongjeomStation/Scenes/ByeongjeomStation.unity";
        const string BatchReportDir = "Logs/LightingBake";

        public static void AuditLightingBatch()
        {
            try
            {
                EditorSceneManager.OpenScene(BatchScenePath, OpenSceneMode.Single);
                var root = GameObject.Find("ByeongjeomStation_병점역");
                var report = new StringBuilder("name\tscale\tuvMin\tactualPixels\tuvArea\tworldArea\tmesh\n");
                foreach (var r in root.GetComponentsInChildren<MeshRenderer>(false))
                {
                    if (r.lightmapIndex < 0 || r.lightmapIndex >= LightmapSettings.lightmaps.Length) continue;
                    var mesh = r.GetComponent<MeshFilter>().sharedMesh;
                    var uv = mesh.uv2;
                    var tri = mesh.triangles;
                    double area = 0;
                    for (int i = 0; i < tri.Length; i += 3)
                    {
                        var a = uv[tri[i + 1]] - uv[tri[i]];
                        var b = uv[tri[i + 2]] - uv[tri[i]];
                        area += Math.Abs(a.x * b.y - a.y * b.x) * 0.5;
                    }
                    var t = LightmapSettings.lightmaps[r.lightmapIndex].lightmapColor;
                    report.AppendLine(string.Join("\t", r.name, r.scaleInLightmap, uv.Min(v => v.x),
                        (t.width * r.lightmapScaleOffset.x) + "," + (t.height * r.lightmapScaleOffset.y),
                        area, WorldArea(r, mesh), mesh.name));
                }
                File.WriteAllText(BatchReportDir + "/uv-audit.tsv", report.ToString());
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        // Unity.exe -batchmode -projectPath <project> -executeMethod
        // Byeongjeom.MapGen.BJ.OptimizeAndBakeBatch -logFile Logs/LightingBake/unity.log
        public static void OptimizeAndBakeBatch()
        {
            try
            {
                if (!Application.isBatchMode) throw new InvalidOperationException("Use this entry point in batch mode.");
                Directory.CreateDirectory(BatchReportDir);
                var scene = EditorSceneManager.OpenScene(BatchScenePath, OpenSceneMode.Single);
                var root = GameObject.Find("ByeongjeomStation_병점역");
                RequireLighting(root != null, "Station root is missing.");
                var report = new StringBuilder();
                report.AppendLine("Scene: " + scene.path);
                report.AppendLine("GPU: " + SystemInfo.graphicsDeviceName);
                report.AppendLine("GPU memory MB: " + SystemInfo.graphicsMemorySize);
                report.AppendLine("RAM MB: " + SystemInfo.systemMemorySize);
                report.AppendLine("Available RAM GB: " + AvailableMemoryGB().ToString("F1"));
                var lights = root.GetComponentsInChildren<Light>(true);
                int colliders = root.GetComponentsInChildren<Collider>(true).Length;
                report.AppendLine("Lights before: " + lights.Length);
                report.AppendLine("Realtime before: " + lights.Count(l => l.lightmapBakeType == LightmapBakeType.Realtime));
                report.AppendLine("Colliders before: " + colliders);
                CaptureLightingPreview("before.png");

                var meshes = new List<Mesh>();
                report.AppendLine(OptimizeLights(root, false, meshes));
                foreach (var mesh in meshes)
                {
                    if (AssetDatabase.LoadMainAssetAtPath(SplitMeshPath) == null) AssetDatabase.CreateAsset(mesh, SplitMeshPath);
                    else AssetDatabase.AddObjectToAsset(mesh, SplitMeshPath);
                }
                AssetDatabase.SaveAssets();
                EditorSceneManager.SaveScene(scene);
                File.WriteAllText(BatchReportDir + "/progress.txt", report.ToString());
                Debug.Log("[LightingBatch] Preparing optimized bake.\n" + report);

                string prepared = PrepareBake(root, BakeMoonMode);
                report.AppendLine(prepared);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                File.WriteAllText(BatchReportDir + "/progress.txt", report.ToString());
                Debug.Log("[LightingBatch] " + prepared);
                var start = DateTime.UtcNow;
                bool baked = Lightmapping.Bake();
                string finished = FinishBake(root, (DateTime.UtcNow - start).TotalSeconds);
                RequireLighting(baked && finished != null, "Lightmapping did not produce a completed bake.");
                report.AppendLine(finished);
                AssetDatabase.SaveAssets();

                // Reload the saved scene to check that the result survives editor restarts.
                scene = EditorSceneManager.OpenScene(BatchScenePath, OpenSceneMode.Single);
                root = GameObject.Find("ByeongjeomStation_병점역");
                RequireLighting(root != null, "Station root missing after reload.");
                ValidateLightingBake(root, report);
                RequireLighting(root.GetComponentsInChildren<Collider>(true).Length == colliders, "Collider count changed.");
                CaptureLightingPreview("after.png");
                File.WriteAllText(BatchReportDir + "/result.txt", report.ToString());
                Debug.Log("[LightingBatch] SUCCESS\n" + report);
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Directory.CreateDirectory(BatchReportDir);
                File.WriteAllText(BatchReportDir + "/failure.txt", e.ToString());
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static void ValidateLightingBake(GameObject root, StringBuilder report, bool reloaded = true)
        {
            var maps = LightmapSettings.lightmaps;
            RequireLighting(maps.Length > 0, "No lightmaps after reload.");
            RequireLighting(Lightmapping.lightingDataAsset != null, "No saved lighting data asset.");
            var lights = root.GetComponentsInChildren<Light>(true);
            int baked = lights.Count(l => l.lightmapBakeType == LightmapBakeType.Baked && l.bakingOutput.isBaked);
            RequireLighting(baked > 0, "No lights have baked output.");
            foreach (var light in lights.Where(l => l.lightmapBakeType == LightmapBakeType.Baked))
                RequireLighting(light.bakingOutput.isBaked, "Missing baked output: " + light.name);
            int flicker = 0;
            foreach (var effect in root.GetComponentsInChildren<BJ_FlickerLight>(true))
                foreach (var light in effect.lights ?? new Light[0])
                {
                    if (light == null) continue;
                    flicker++;
                    RequireLighting(light.lightmapBakeType == LightmapBakeType.Realtime, "Flickering light was baked.");
                }
            int mapped = 0;
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (!renderer.enabled || renderer.receiveGI != ReceiveGI.Lightmaps ||
                    (GameObjectUtility.GetStaticEditorFlags(renderer.gameObject) & StaticEditorFlags.ContributeGI) == 0) continue;
                RequireLighting(renderer.lightmapIndex >= 0 && renderer.lightmapIndex < maps.Length,
                    "Missing lightmap on renderer: " + renderer.name);
                mapped++;
            }
            RequireLighting(mapped > 0, "No mapped renderers.");
            RequireLighting(LightmapSettings.lightProbes != null && LightmapSettings.lightProbes.count > 0, "No baked probes.");
            foreach (var map in maps)
                RequireLighting(map.lightmapColor != null && File.Exists(AssetDatabase.GetAssetPath(map.lightmapColor)), "Lightmap file missing.");
            report.AppendLine(reloaded ? "Reload validation: PASS" : "Saved scene validation: PASS");
            report.AppendLine("Baked lights: " + baked);
            report.AppendLine("Realtime lights: " + lights.Count(l => l.lightmapBakeType == LightmapBakeType.Realtime));
            report.AppendLine("Flickering light references preserved: " + flicker);
            report.AppendLine("Mapped renderers: " + mapped);
            report.AppendLine("Baked light probes: " + LightmapSettings.lightProbes.count);
            report.AppendLine("Lightmaps: " + maps.Length);
            report.AppendLine("Lighting data: " + AssetDatabase.GetAssetPath(Lightmapping.lightingDataAsset));
            report.AppendLine("Realtime lights eligible for distance culling: " + lights.Count(BJ_LightCuller.IsManaged));
        }

        static void CaptureLightingPreview(string filename)
        {
            var camera = Camera.main;
            if (camera == null) return;
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var target = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                texture.Apply();
                File.WriteAllBytes(BatchReportDir + "/" + filename, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        static void RequireLighting(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
