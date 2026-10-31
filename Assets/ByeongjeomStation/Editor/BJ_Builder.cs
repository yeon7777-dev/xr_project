using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace Byeongjeom.MapGen
{
    /// <summary>
    /// 메뉴: Tools > 병점역 맵 > 맵 생성
    /// Assets/ByeongjeomStation/Scenes/ByeongjeomStation.unity 씬을 통째로 생성한다.
    /// </summary>
    public static partial class BJ
    {
        static readonly Color FogColor = new Color(0.032f, 0.036f, 0.042f);

        [MenuItem("Tools/병점역 맵/맵 생성 (Build Byeongjeom Station)", false, 1)]
        public static void BuildMenu()
        {
            if (!EditorUtility.DisplayDialog("병점역 맵 생성",
                    "병점역 공포맵 씬을 새로 생성합니다.\n\n" + ScenePath + "\n\n이전에 생성한 결과물(Generated 폴더)은 덮어씁니다.\n텍스처 임포트와 NavMesh 굽기 때문에 1~3분 정도 걸릴 수 있습니다.",
                    "생성", "취소"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build(true);
        }

        [MenuItem("Tools/병점역 맵/맵 생성 - NavMesh 제외 (빠름)", false, 2)]
        public static void BuildFastMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build(false);
        }

        [MenuItem("Tools/병점역 맵/NavMesh 다시 굽기", false, 20)]
        public static void RebakeMenu()
        {
            GameObject root = GameObject.Find("ByeongjeomStation_병점역");
            if (root == null) { EditorUtility.DisplayDialog("병점역 맵", "병점역 씬을 먼저 열어주세요.", "확인"); return; }
            try
            {
                EditorUtility.DisplayProgressBar("병점역 맵", "NavMesh 굽는 중...", 0.5f);
                BakeNavMesh(root);
                EditorSceneManager.MarkSceneDirty(root.scene);
                EditorSceneManager.SaveScene(root.scene);
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        static void Progress(string msg, float t)
        {
            EditorUtility.DisplayProgressBar("병점역 맵 생성", msg, t);
        }

        public static void Build(bool bakeNav)
        {
            var started = DateTime.Now;
            try
            {
                Progress("씬 준비", 0.02f);
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                PrepareFolders();
                Progress("텍스처 임포트 설정", 0.05f);
                ConfigureTextureImporters();
                ResetRegistries();
                Progress("머티리얼 생성", 0.1f);
                CreateMaterials();

                var root = new GameObject("ByeongjeomStation_병점역").transform;
                Progress("선로", 0.15f); BuildTrackBed(root);
                Progress("승강장 · 스크린도어 · 지붕", 0.22f); BuildPlatforms(root);
                Progress("선상 대합실 골조", 0.32f); BuildConcourseShell(root);
                Progress("계단 · 에스컬레이터 · 엘리베이터", 0.4f); BuildVerticalCirculation(root);
                Progress("대합실 내부 (개찰구 · 화장실 · 역무실)", 0.48f); BuildConcourseInterior(root);
                Progress("출구 1 · 2 · 3", 0.55f); BuildExits(root);
                Progress("지면 · 역앞광장", 0.6f); BuildGround(root); BuildEastSide(root);
                Progress("서측 (주차타워 · 통학버스 정류장)", 0.65f); BuildWestSide(root);
                Progress("울타리 · 경계", 0.68f); BuildBounds(root);
                Progress("전차선 · 신호기 · 과선교", 0.7f); BuildRailwayEquipment(root);
                Progress("멈춘 전동차", 0.74f); BuildTrain(root);
                Progress("조명 · 안개 · 후처리", 0.78f); SetupAtmosphere(root);
                Progress("조명 최적화", 0.8f); Debug.Log("[병점역] " + OptimizeLights(root.gameObject, false, meshList));
                Progress("메쉬 저장", 0.82f); SaveMeshes();
                MarkStatic(root.gameObject);
                Progress("콜라이더 최적화", 0.84f);
                Debug.Log("[병점역] " + OptimizeColliders(new[] { root.gameObject }, false));
                if (bakeNav) { Progress("NavMesh 굽는 중 (조금 걸립니다)", 0.86f); BakeNavMesh(root.gameObject); }

                if (BJ_VRSetup.VRModeEnabled) { Progress("VR 플레이어 (메타 퀘스트)", 0.95f); BJ_VRSetup.AddVRPlayer(scene); }
                else { Progress("3인칭 플레이어 (ThirdPersonController)", 0.95f); BJ_ThirdPersonPlayer.AddThirdPersonPlayer(scene); }

                Progress("씬 저장", 0.97f);
                AssetDatabase.SaveAssets();
                EditorSceneManager.SaveScene(scene, ScenePath);
                if (BakeOnBuild) { Progress("조명 굽기 (몇 분 걸립니다)", 0.98f); Debug.Log("[병점역] 조명 굽기: " + BakeLighting(root.gameObject, BakeMoonMode)); }
                AddToBuildSettings();
                FrameSceneView();
                int objs = root.GetComponentsInChildren<Transform>(true).Length;
                int lights = root.GetComponentsInChildren<Light>(true).Length;
                Debug.Log(string.Format("[병점역] 생성 완료: 오브젝트 {0}개, 라이트 {1}개, 메쉬 {2}개, 머티리얼 {3}개 ({4:F1}초) → {5}",
                    objs, lights, meshList.Count, mats.Count, (DateTime.Now - started).TotalSeconds, ScenePath));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("병점역 맵 생성 실패", e.Message + "\n\n자세한 내용은 Console 창을 확인하세요.", "확인");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // ------------------------------------------------------------------ assets
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static void PrepareFolders()
        {
            if (AssetDatabase.IsValidFolder(GenDir)) AssetDatabase.DeleteAsset(GenDir);
            EnsureFolder(GenDir);
            EnsureFolder(MatDir);
            EnsureFolder(SceneDir);
        }

        static void ConfigureTextureImporters()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { TexDir });
            var changed = new List<string>();
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;
                string file = Path.GetFileNameWithoutExtension(path);
                bool normal = file.EndsWith("_N");
                bool surface = path.Contains("/Surfaces/") && file != "T_GlassBroken" && file != "T_Static";
                bool dirty = false;
                if (normal && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; dirty = true; }
                var wrap = surface ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                if (ti.wrapMode != wrap) { ti.wrapMode = wrap; dirty = true; }
                if (surface && ti.anisoLevel < 4) { ti.anisoLevel = 4; dirty = true; }
                if (!normal && ti.DoesSourceTextureHaveAlpha() && !ti.alphaIsTransparency) { ti.alphaIsTransparency = true; dirty = true; }
                if (dirty) { EditorUtility.SetDirty(ti); changed.Add(path); }
            }
            if (changed.Count == 0) return;
            AssetDatabase.StartAssetEditing();
            try { foreach (var p in changed) { AssetDatabase.WriteImportSettingsIfDirty(p); AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate); } }
            finally { AssetDatabase.StopAssetEditing(); }
        }

        static void SaveMeshes()
        {
            if (meshList.Count == 0) return;
            string path = GenDir + "/BJ_Meshes.asset";
            AssetDatabase.CreateAsset(meshList[0], path);
            for (int i = 1; i < meshList.Count; i++) AssetDatabase.AddObjectToAsset(meshList[i], meshList[0]);
            AssetDatabase.SaveAssets();
        }

        static void AddToBuildSettings()
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (list.Exists(s => s.path == ScenePath)) return;
            list.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        static void FrameSceneView()
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv == null) return;
            sv.LookAt(new Vector3(10f, 6f, -8f), Quaternion.Euler(32f, -58f, 0f), 70f);
            sv.Repaint();
        }

        // ------------------------------------------------------------------ atmosphere
        static void SetupAtmosphere(Transform root)
        {
            var g = Group("Atmosphere_조명_안개_후처리", root);

            // 달빛
            var moonGo = Go("Moonlight", g);
            moonGo.transform.rotation = Quaternion.Euler(36f, -38f, 0f);
            var moon = moonGo.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.55f, 0.64f, 0.85f);
            moon.intensity = 0.13f;
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = 0.75f;
            RenderSettings.sun = moon;

            // 환경광 (아주 어둡게)
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.05f, 0.056f, 0.072f);
            RenderSettings.ambientEquatorColor = new Color(0.034f, 0.036f, 0.04f);
            RenderSettings.ambientGroundColor = new Color(0.016f, 0.015f, 0.014f);
            RenderSettings.ambientIntensity = 1f;

            // 안개
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.028f;
            RenderSettings.fogColor = FogColor;

            // 밤하늘 + 어두운 반사
            var skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                var sky = new Material(skyShader) { name = "BJ_NightSky" };
                sky.SetFloat("_SunDisk", 0f);
                sky.SetFloat("_AtmosphereThickness", 0.35f);
                sky.SetColor("_SkyTint", new Color(0.22f, 0.25f, 0.32f));
                sky.SetColor("_GroundColor", new Color(0.03f, 0.03f, 0.035f));
                sky.SetFloat("_Exposure", 0.06f);
                AssetDatabase.CreateAsset(sky, GenDir + "/BJ_NightSky.mat");
                RenderSettings.skybox = sky;
            }
            var cube = new Cubemap(16, TextureFormat.RGBA32, false) { name = "BJ_DarkReflection" };
            var px = new Color[16 * 16];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0.03f, 0.034f, 0.042f, 1f);
            for (int f = 0; f < 6; f++) cube.SetPixels(px, (CubemapFace)f);
            cube.Apply();
            AssetDatabase.CreateAsset(cube, GenDir + "/BJ_DarkReflection.cubemap");
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
#if UNITY_2022_1_OR_NEWER
            RenderSettings.customReflectionTexture = cube;
#else
            RenderSettings.customReflection = cube;
#endif
            RenderSettings.reflectionIntensity = 1f;

            // 실시간 조명만 사용 (베이크 없음)
            try
            {
                var ls = new LightingSettings { name = "BJ_Lighting", bakedGI = false, realtimeGI = false };
                AssetDatabase.CreateAsset(ls, GenDir + "/BJ_Lighting.lighting");
                Lightmapping.lightingSettings = ls;
            }
            catch (Exception e) { Debug.LogWarning("[병점역] Lighting Settings 지정 실패 (무시해도 됨): " + e.Message); }

            // 후처리 (Devour 느낌: 탈색 · 비네팅 · 필름 그레인) - URP 일 때만
            if (IsURP)
            {
                try { SetupUrpPostProcess(g); }
                catch (Exception e) { Debug.LogWarning("[병점역] 후처리 볼륨 생성 실패 (무시해도 됨): " + e.Message); }
            }

            // 미리보기 카메라 (플레이어 카메라를 넣으면 꺼두세요)
            var camGo = Go("PreviewCamera (플레이어 넣으면 비활성화)", g);
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(80f, 1.7f, -4f);
            camGo.transform.rotation = Quaternion.LookRotation(new Vector3(38f, 9f, -6f) - camGo.transform.position);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = FogColor;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 260f;
            cam.fieldOfView = 70f;
            cam.allowHDR = true;
            if (IsURP)
            {
                var camDataT = FindType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData");
                if (camDataT != null)
                {
                    var data = cam.GetComponent(camDataT);
                    if (data == null) data = camGo.AddComponent(camDataT);
                    SetMember(data, "renderPostProcessing", true);
                    SetMember(data, "antialiasing", "SubpixelMorphologicalAntiAliasing");
                }
            }
            else
            {
                // Built-in: 실시간 라이트가 많으므로 디퍼드 + 디퍼드용 안개 이펙트
                // (씬 뷰도 메인 카메라의 렌더링 경로/이미지 이펙트를 따라감)
                cam.renderingPath = RenderingPath.DeferredShading;
                var fog = camGo.AddComponent<BJ_DeferredFog>();
                fog.fogShader = Shader.Find("Hidden/BJ_DeferredFog");
                if (fog.fogShader == null) Debug.LogWarning("[병점역] Hidden/BJ_DeferredFog 셰이더를 찾을 수 없습니다. Shaders 폴더를 확인하세요.");
            }
            camGo.AddComponent<AudioListener>();
        }

        // ------------------------------------------------------------------ reflection helpers (URP / AI Navigation 패키지가 없어도 컴파일되도록)
        static void SetMember(object target, string name, object value)
        {
            if (target == null) return;
            var t = target.GetType();
            var prop = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.CanWrite) { prop.SetValue(target, ConvertTo(value, prop.PropertyType)); return; }
            var field = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (field != null) field.SetValue(target, ConvertTo(value, field.FieldType));
        }

        static object GetMember(object target, string name)
        {
            if (target == null) return null;
            var t = target.GetType();
            var prop = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null) return prop.GetValue(target);
            var field = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            return field != null ? field.GetValue(target) : null;
        }

        static object ConvertTo(object value, Type type)
        {
            if (value == null) return null;
            if (type.IsEnum && value is string s) return Enum.Parse(type, s);
            if (type.IsInstanceOfType(value)) return value;
            if (value is IConvertible && typeof(IConvertible).IsAssignableFrom(type)) return Convert.ChangeType(value, type);
            return value;
        }

        /// <summary>VolumeComponent 의 파라미터(field)에 Override(value) 호출.</summary>
        static void VolParam(object comp, string field, object value)
        {
            var param = GetMember(comp, field);
            if (param == null) return;
            var m = param.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(x => x.Name == "Override" && x.GetParameters().Length == 1);
            if (m == null) return;
            m.Invoke(param, new[] { ConvertTo(value, m.GetParameters()[0].ParameterType) });
        }

        static void SetupUrpPostProcess(Transform g)
        {
            var profileT = FindType("UnityEngine.Rendering.VolumeProfile");
            var volumeT = FindType("UnityEngine.Rendering.Volume");
            if (profileT == null || volumeT == null) return;
            var profile = ScriptableObject.CreateInstance(profileT);
            profile.name = "BJ_PostProcess";
            AssetDatabase.CreateAsset(profile, GenDir + "/BJ_PostProcess.asset");
            var add = profileT.GetMethod("Add", new[] { typeof(Type), typeof(bool) });
            Func<string, object> Add = n =>
            {
                var t = FindType("UnityEngine.Rendering.Universal." + n);
                return t == null ? null : add.Invoke(profile, new object[] { t, false });
            };
            var tone = Add("Tonemapping"); VolParam(tone, "mode", "ACES");
            var ca = Add("ColorAdjustments");
            VolParam(ca, "postExposure", 0.6f); VolParam(ca, "contrast", 20f); VolParam(ca, "saturation", -40f); VolParam(ca, "colorFilter", new Color(0.88f, 0.97f, 0.94f));
            var smh = Add("ShadowsMidtonesHighlights"); VolParam(smh, "shadows", new Vector4(0.92f, 0.98f, 1.06f, -0.04f));
            var vig = Add("Vignette"); VolParam(vig, "intensity", 0.42f); VolParam(vig, "smoothness", 0.5f); VolParam(vig, "color", Color.black);
            var grain = Add("FilmGrain"); VolParam(grain, "type", "Medium3"); VolParam(grain, "intensity", 0.38f); VolParam(grain, "response", 0.75f);
            var bloom = Add("Bloom"); VolParam(bloom, "threshold", 0.9f); VolParam(bloom, "intensity", 0.8f); VolParam(bloom, "scatter", 0.7f);
            var chroma = Add("ChromaticAberration"); VolParam(chroma, "intensity", 0.12f);
            if (GetMember(profile, "components") is System.Collections.IEnumerable comps)
                foreach (var c in comps) if (c is UnityEngine.Object o) AssetDatabase.AddObjectToAsset(o, profile);
            EditorUtility.SetDirty(profile);
            var volGo = Go("PostProcessVolume", g);
            var vol = volGo.AddComponent(volumeT);
            SetMember(vol, "isGlobal", true);
            SetMember(vol, "priority", 10f);
            SetMember(vol, "sharedProfile", profile);
        }

        // ------------------------------------------------------------------ navmesh
        static void BakeNavMesh(GameObject root)
        {
            EnsureFolder(GenDir);
            string path = GenDir + "/BJ_NavMesh.asset";
            NavMeshData data = null;
            var surfT = FindType("Unity.AI.Navigation.NavMeshSurface");
            if (surfT != null)
            {
                // AI Navigation 패키지가 있으면 NavMeshSurface 사용
                var surf = root.GetComponent(surfT);
                if (surf == null) surf = root.AddComponent(surfT);
                SetMember(surf, "collectObjects", "Children");
                SetMember(surf, "useGeometry", NavMeshCollectGeometry.PhysicsColliders);
                SetMember(surf, "layerMask", (LayerMask)(~0));
                surfT.GetMethod("RemoveData", Type.EmptyTypes)?.Invoke(surf, null);
                SetMember(surf, "navMeshData", null);
                surfT.GetMethod("BuildNavMesh", Type.EmptyTypes).Invoke(surf, null);
                data = GetMember(surf, "navMeshData") as NavMeshData;
                if (data == null) { Debug.LogWarning("[병점역] NavMesh 생성 실패"); return; }
                surfT.GetMethod("RemoveData", Type.EmptyTypes)?.Invoke(surf, null);
                data = StoreNavMeshData(data, path);
                SetMember(surf, "navMeshData", data);
                surfT.GetMethod("AddData", Type.EmptyTypes)?.Invoke(surf, null);
                EditorUtility.SetDirty(surf);
                return;
            }

            // 패키지가 없으면 내장 NavMeshBuilder 로 직접 굽고, 씬 자체의 NavMesh 로 넣는다
            var sources = new List<NavMeshBuildSource>();
            UnityEngine.AI.NavMeshBuilder.CollectSources(root.transform, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
            var settings = NavMesh.GetSettingsByID(0);
            var bounds = new Bounds(new Vector3(12f, 15f, 0f), new Vector3(240f, 50f, 260f));
            data = UnityEngine.AI.NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data == null) { Debug.LogWarning("[병점역] NavMesh 생성 실패"); return; }
            data.name = "BJ_NavMesh";
            // 예전 방식(BJ_NavMeshLoader 가 Play 때 등록)은 AI 의 NavMeshAgent 보다 늦어 'no valid NavMesh' 가 났다
            var loader = root.GetComponent<BJ_NavMeshLoader>();
            if (loader != null) { loader.Unload(); UnityEngine.Object.DestroyImmediate(loader); }
            SetSceneNavMesh(null);
            data = StoreNavMeshData(data, path);
            SetSceneNavMesh(data);
        }

        /// <summary>
        /// 씬 자체의 NavMesh(Navigation 창에서 굽는 것과 같은 자리)를 바꾼다.
        /// 씬과 함께 로드되므로 씬 안의 NavMeshAgent 보다 먼저 준비된다.
        /// </summary>
        static void SetSceneNavMesh(NavMeshData data)
        {
            var so = new SerializedObject(UnityEditor.AI.NavMeshBuilder.navMeshSettingsObject);
            so.FindProperty("m_NavMeshData").objectReferenceValue = data;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static NavMeshData SceneNavMesh()
        {
            var so = new SerializedObject(UnityEditor.AI.NavMeshBuilder.navMeshSettingsObject);
            return so.FindProperty("m_NavMeshData").objectReferenceValue as NavMeshData;
        }

        // Re-baking colliders must preserve the asset GUID used by existing scenes/loaders.
        static NavMeshData StoreNavMeshData(NavMeshData data, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
            if (existing == null) { AssetDatabase.CreateAsset(data, path); return data; }
            EditorUtility.CopySerialized(data, existing);
            UnityEngine.Object.DestroyImmediate(data);
            EditorUtility.SetDirty(existing);
            return existing;
        }
    }
}
