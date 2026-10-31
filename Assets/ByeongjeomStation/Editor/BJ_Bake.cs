using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Byeongjeom;
using Object = UnityEngine.Object;

namespace Byeongjeom.MapGen
{
    /// <summary>
    /// 조명 굽기 (라이트맵). 메뉴: Tools > 병점역 맵 > 조명 굽기 / 조명 굽기 되돌리기 / 맵 생성 때 조명도 굽기
    ///  - 깜빡이지 않는 포인트 · 스포트 라이트(BJ_FlickerLight 가 다루지 않는 것)와 달빛을 Baked 로 바꿔 라이트맵에 굽는다.
    ///    구운 라이트는 Play 중 비용이 없고(디퍼드 라이트 패스 · 그림자 맵이 사라짐), 그림자도 공짜로 생긴다.
    ///  - 깜빡이는 조명은 실시간 그대로이고 BJ_LightCuller 가 계속 거리에 따라 끈다 (구운 라이트는 끌 필요가 없어 빠진다).
    ///  - 생성 메쉬는 UV 가 m 단위 월드 UV 라서 라이트맵 UV(UV2)를 따로 만든다 (BuildBoxLightmapUVs).
    ///  - 작은 소품 · 투명 물체 · 움직이는 물체(괴물 · 플레이어)는 라이트맵 대신 자동으로 깐 라이트 프로브로 구운 조명을 받는다.
    ///  - 환경광 · 달빛도 라이트맵에 들어가므로 '분위기' 메뉴로 밝기를 바꾸면 다시 구워야 한다 (안개는 Play 중 계산이라 상관없음).
    /// </summary>
    public static partial class BJ
    {
        public enum MoonBake { Realtime, Mixed, Baked }

        /// <summary>달빛 굽기 방식. Baked = 직접광 · 그림자까지 라이트맵에, Mixed = 간접광만 굽고 직접광 · 그림자는 실시간.</summary>
        public static MoonBake BakeMoonMode = MoonBake.Baked;
        /// <summary>라이트맵 해상도 (텍셀/m). 라이트맵 UV 의 차트 여백도 이 값 기준으로 잡는다.</summary>
        public static float BakeTexelsPerMeter = 4f;

        const int UV2Pad = 3;   // 차트마다 3텍셀: 배치 반올림 · 큰 메쉬 축소 후에도 필터링 여백 확보
        const string BakedLightingPath = GenDir + "/BJ_LightingBaked.lighting";
        const string RealtimeLightingPath = GenDir + "/BJ_Lighting.lighting";
        const string ProbeGroupName = "BJ_LightProbes (조명 굽기)";
        const string RecordName = "BJ_BakeRecord (조명 굽기 - 되돌리기용)";
        const string BakeOnBuildKey = "Byeongjeom.BakeOnBuild";
        const string BakeOnBuildMenu = "Tools/병점역 맵/맵 생성 때 조명도 굽기";

        public static bool BakeOnBuild { get { return EditorPrefs.GetBool(BakeOnBuildKey, false); } }

        // ------------------------------------------------------------------ menus
        [MenuItem(BakeOnBuildMenu, false, 25)]
        static void ToggleBakeOnBuild() { EditorPrefs.SetBool(BakeOnBuildKey, !BakeOnBuild); }

        [MenuItem(BakeOnBuildMenu, true)]
        static bool ToggleBakeOnBuildValidate()
        {
            Menu.SetChecked(BakeOnBuildMenu, BakeOnBuild);
            return true;
        }

        [MenuItem("Tools/병점역 맵/조명 굽기 (라이트맵)", false, 23)]
        static void BakeLightingMenu()
        {
            var root = GameObject.Find("ByeongjeomStation_병점역");
            if (root == null) { EditorUtility.DisplayDialog("조명 굽기", "병점역 씬을 먼저 열어주세요.", "확인"); return; }
            if (Lightmapping.isRunning) { EditorUtility.DisplayDialog("조명 굽기", "이미 굽는 중입니다. 오른쪽 아래 진행 막대를 확인하세요.", "확인"); return; }
            var scene = root.scene;
            if (string.IsNullOrEmpty(scene.path)) { EditorUtility.DisplayDialog("조명 굽기", "씬을 먼저 저장해 주세요 (라이트맵은 씬 옆 폴더에 저장됩니다).", "확인"); return; }
            if (RenderSettings.fog && RenderSettings.fogDensity < 0.015f)
            {
                int c = EditorUtility.DisplayDialogComplex("조명 굽기",
                    "지금 분위기가 '확인용 (밝게)' 입니다 (안개 " + RenderSettings.fogDensity.ToString("0.###") + ").\n\n" +
                    "환경광과 달빛도 라이트맵에 함께 구워지므로, 구운 뒤 '분위기' 메뉴로 바꿔도 벽 · 바닥 밝기는 그대로입니다 (안개만 바뀜).\n" +
                    "분위기를 바꾼 뒤에는 다시 구워야 합니다.",
                    "공포 기본으로 바꾸고 굽기", "취소", "지금 분위기로 굽기");
                if (c == 1) return;
                if (c == 0) EditorApplication.ExecuteMenuItem("Tools/병점역 맵/분위기: 공포 기본 (어둡게)");
            }
            float freeGB = AvailableMemoryGB();
            if (freeGB >= 0f && freeGB < LowMemoryGB &&
                !EditorUtility.DisplayDialog("조명 굽기 - 메모리 부족",
                    "남은 메모리가 " + freeGB.ToString("F1") + "GB 입니다. 라이트매퍼는 메모리가 모자라면 오류 없이 멈춘 채 기다립니다 " +
                    "(Console 에 'Progressive Lightmapper: ... out of memory').\n\n" +
                    "브라우저 · 채팅 앱 등을 닫아 " + LowMemoryGB.ToString("F0") + "GB 이상 비운 뒤 굽는 것을 권장합니다.", "그래도 굽기", "취소"))
                return;
            int n = BakeCandidates(root).Count;
            if (!EditorUtility.DisplayDialog("조명 굽기",
                    "깜빡이지 않는 포인트 · 스포트 라이트 " + n + "개와 달빛을 라이트맵에 굽습니다. 깜빡이는 조명은 실시간 그대로입니다.\n\n" +
                    "· 씬을 저장하고 굽기 시작합니다. 굽는 동안 에디터를 쓸 수 있고(오른쪽 아래 진행 막대), 끝나면 다시 저장합니다.\n" +
                    "· 라이트맵은 " + LightmapFolder(scene) + " 폴더에 생깁니다.\n" +
                    "· 되돌리기: Tools > 병점역 맵 > 조명 굽기 되돌리기", "굽기", "취소"))
                return;

            string rep;
            try
            {
                EditorUtility.DisplayProgressBar("조명 굽기", "라이트맵 UV · 라이트 프로브 준비 중...", 0.3f);
                rep = PrepareBake(root, BakeMoonMode);
            }
            finally { EditorUtility.ClearProgressBar(); }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[병점역] 조명 굽기 준비: " + rep);

            var started = DateTime.Now;
            if (!Lightmapping.BakeAsync())
            {
                EditorUtility.DisplayDialog("조명 굽기", "굽기를 시작하지 못했습니다. Console 창을 확인하세요.", "확인");
                return;
            }
            EditorApplication.CallbackFunction poll = null;
            float lastProgress = -1f;
            var lastChange = DateTime.Now;
            bool warned = false;
            poll = () =>
            {
                if (Lightmapping.isRunning)
                {
                    // 메모리가 모자라면 라이트매퍼가 진행률 0 에서 멈춘 채 기다린다 - 한 번 알려 준다
                    float pr = Lightmapping.buildProgress;
                    if (Mathf.Abs(pr - lastProgress) > 1e-4f) { lastProgress = pr; lastChange = DateTime.Now; }
                    else if (!warned && (DateTime.Now - lastChange).TotalMinutes > 3)
                    {
                        warned = true;
                        Debug.LogWarning("[병점역] 조명 굽기 진행률이 3분째 " + (pr * 100f).ToString("F0") + "% 에서 멈춰 있습니다. Console 에 'out of memory' 가 있으면 " +
                                         "메모리가 모자란 것입니다 (남은 메모리 " + AvailableMemoryGB().ToString("F1") + "GB). 다른 프로그램을 닫거나, 오른쪽 아래 진행 막대에서 취소하세요.");
                    }
                    return;
                }
                EditorApplication.update -= poll;
                if (root == null) return;   // 굽는 동안 씬을 닫음
                string done = FinishBake(root, (DateTime.Now - started).TotalSeconds);
                if (done == null)
                {
                    // 안 구운 Baked 라이트는 실시간으로(켠 그림자까지) 그려지므로 라이트 설정만 원래대로 돌려 둔다
                    var rec = root.GetComponentInChildren<BJ_BakeRecord>(true);
                    if (rec != null) RestoreLights(rec);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    EditorUtility.DisplayDialog("조명 굽기 취소됨",
                        "라이트맵이 만들어지지 않아 라이트 설정을 원래대로(실시간) 돌려 놓았습니다.\n" +
                        "다시 굽거나, Tools > 병점역 맵 > 조명 굽기 되돌리기 로 라이트 프로브 · 라이트맵 UV 까지 지우세요.", "확인");
                    return;
                }
                EditorUtility.DisplayDialog("조명 굽기 완료", rep + "\n\n" + done + "\n\n씬을 저장했습니다.", "확인");
            };
            EditorApplication.update += poll;
        }

        [MenuItem("Tools/병점역 맵/조명 굽기 되돌리기 (모두 실시간)", false, 24)]
        static void RevertBakeMenu()
        {
            var root = GameObject.Find("ByeongjeomStation_병점역");
            if (root == null) { EditorUtility.DisplayDialog("조명 굽기 되돌리기", "병점역 씬을 먼저 열어주세요.", "확인"); return; }
            if (!EditorUtility.DisplayDialog("조명 굽기 되돌리기",
                    "구운 라이트를 실시간으로 되돌리고 라이트맵 · 라이트 프로브 · 라이트맵 UV 를 지운 뒤 씬을 저장합니다.", "되돌리기", "취소"))
                return;
            if (Lightmapping.isRunning) Lightmapping.Cancel();
            string rep = RevertBake(root);
            EditorSceneManager.MarkSceneDirty(root.scene);
            if (!string.IsNullOrEmpty(root.scene.path)) EditorSceneManager.SaveScene(root.scene);
            Debug.Log("[병점역] 조명 굽기 되돌리기: " + rep);
            EditorUtility.DisplayDialog("조명 굽기 되돌리기 완료", rep, "확인");
        }

        /// <summary>맵 생성 · 테스트용: 굽기 준비 후 바로(동기) 굽는다. 씬은 미리 저장돼 있어야 하고, 끝난 뒤 저장은 호출한 쪽에서.</summary>
        public static string BakeLighting(GameObject root, MoonBake moon)
        {
            string rep = PrepareBake(root, moon);
            var started = DateTime.Now;
            Lightmapping.Bake();
            string done = FinishBake(root, (DateTime.Now - started).TotalSeconds);
            if (done == null)
            {
                var rec = root.GetComponentInChildren<BJ_BakeRecord>(true);
                if (rec != null) RestoreLights(rec);
                return rep + "\n라이트맵이 만들어지지 않아(취소 또는 실패) 라이트 설정을 원래대로 돌려 놓았습니다";
            }
            return rep + "\n" + done;
        }

        /// <summary>이보다 메모리가 적으면 굽기 전에 경고 (GB). 2.5GB 남은 PC 에서 이 맵 굽기가 'out of memory' 로 멈췄다.</summary>
        const float LowMemoryGB = 4f;

#if UNITY_EDITOR_WIN
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        class MemoryStatusEx { public uint length = 64, load; public ulong totalPhys, availPhys, totalPage, availPage, totalVirtual, availVirtual, availExtended; }
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern bool GlobalMemoryStatusEx([System.Runtime.InteropServices.In, System.Runtime.InteropServices.Out] MemoryStatusEx m);
#endif

        /// <summary>남은 실제 메모리 (GB). 알 수 없으면 -1.</summary>
        static float AvailableMemoryGB()
        {
#if UNITY_EDITOR_WIN
            try
            {
                var m = new MemoryStatusEx();
                if (GlobalMemoryStatusEx(m)) return (float)(Math.Min(m.availPhys, m.availPage) / (1024.0 * 1024.0 * 1024.0));
            }
            catch (Exception) { }
#endif
            return -1f;
        }

        // ------------------------------------------------------------------ prepare / finish / revert
        /// <summary>
        /// 굽기 준비: 라이트를 Baked 로, Static 렌더러에 Contribute GI · 라이트맵 UV, 라이트 프로브, 굽기용 Lighting Settings.
        /// 원래 라이트 설정은 BJ_BakeRecord 에 남긴다. 실제 굽기(Lightmapping.Bake)는 하지 않는다.
        /// </summary>
        public static string PrepareBake(GameObject root, MoonBake moon)
        {
            var rec = root.GetComponentInChildren<BJ_BakeRecord>(true);
            if (rec != null) RestoreLights(rec);   // 다시 구울 때는 원래 상태에서 후보를 고른다
            else
            {
                var g = new GameObject(RecordName) { tag = "EditorOnly" };
                g.transform.SetParent(AtmosphereGroup(root), false);
                rec = g.AddComponent<BJ_BakeRecord>();
                LightingSettings cur;
                rec.lightingSettingsPath = Lightmapping.TryGetLightingSettings(out cur) && cur != null ? AssetDatabase.GetAssetPath(cur) : "";
            }
            rec.lights.Clear(); rec.bakeTypes.Clear(); rec.shadows.Clear();

            // 1. 라이트
            var casters = ShadowCasterBoxes(root);
            var baked = BakeCandidates(root);
            int shadowed = 0;
            foreach (var l in baked)
            {
                Record(rec, l);
                l.lightmapBakeType = LightmapBakeType.Baked;
                // 구운 그림자는 Play 중 비용이 없으므로 켠다. 단 라이트가 박스 안에 들어가 있으면(신호기 렌즈 등) 빛이 다 막히므로 그대로 둔다
                if (l.shadows == LightShadows.None && !InsideAnyBox(l.transform.position, casters)) l.shadows = LightShadows.Soft;
                if (l.shadows != LightShadows.None) { l.shadowRadius = 0.08f; shadowed++; }
                EditorUtility.SetDirty(l);
            }
            var sun = MoonLight(root);
            if (sun != null)
            {
                Record(rec, sun);
                sun.lightmapBakeType = moon == MoonBake.Baked ? LightmapBakeType.Baked : moon == MoonBake.Mixed ? LightmapBakeType.Mixed : LightmapBakeType.Realtime;
                sun.shadowAngle = 1.5f;
                EditorUtility.SetDirty(sun);
            }

            // 2. Static 렌더러: Contribute GI + 라이트맵 UV (작은 것 · UV 를 못 만드는 것은 프로브로)
            var st = SetupGIStatic(root, baked);

            // 3. 라이트 프로브 (움직이는 물체 · 프로브로 칠하는 렌더러용)
            int probes = PlaceLightProbes(root, baked);

            // 4. 굽기용 Lighting Settings
            Lightmapping.lightingSettings = BakedLightingSettings(moon);
            EditorUtility.SetDirty(rec);

            return string.Format(
                "라이트 {0}개 Baked (구운 그림자 {1}개), 달빛 {2}\n" +
                "Static 렌더러: 라이트맵 {3}개 (구운 라이트가 안 닿아 해상도 {12}배 {13}개), 굽기에서 빼고 프로브로 칠함 {4}개 (작은 소품 {5} · UV 없는 내장 메쉬 {6}) · 투명 {7}개\n" +
                "라이트맵 UV 새로 만든 메쉬 {8}개, 라이트 프로브 {9}개, 예상 라이트맵 {10:F1}M 텍셀 ({11}텍셀/m)",
                baked.Count, shadowed, sun == null ? "없음" : moon.ToString(),
                st.lightmapped, st.small + st.noUV, st.small, st.noUV, st.transparent,
                st.newUV, probes, st.texels / 1e6, BakeTexelsPerMeter, FarLightmapScale, st.far);
        }

        static void Record(BJ_BakeRecord rec, Light l)
        {
            rec.lights.Add(l);
            rec.bakeTypes.Add(l.lightmapBakeType);
            rec.shadows.Add(l.shadows);
        }

        static void RestoreLights(BJ_BakeRecord rec)
        {
            for (int i = 0; i < rec.lights.Count; i++)
            {
                var l = rec.lights[i];
                if (l == null) continue;
                l.lightmapBakeType = rec.bakeTypes[i];
                l.shadows = rec.shadows[i];
                EditorUtility.SetDirty(l);
            }
        }

        /// <summary>굽기가 끝난 뒤 결과 보고. 라이트맵이 안 만들어졌으면(취소) null.</summary>
        static string FinishBake(GameObject root, double seconds)
        {
            var maps = LightmapSettings.lightmaps;
            var rec = root.GetComponentInChildren<BJ_BakeRecord>(true);
            bool ok = maps.Length > 0 && rec != null && rec.lights.Exists(l => l != null && l.bakingOutput.isBaked);
            if (!ok) return null;
            long bytes = 0, mem = 0;
            int texels = 0;
            foreach (var m in maps)
                foreach (var t in new Texture2D[] { m.lightmapColor, m.lightmapDir, m.shadowMask })
                {
                    if (t == null) continue;
                    texels += t.width * t.height;
                    mem += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t);
                    string p = AssetDatabase.GetAssetPath(t);
                    if (!string.IsNullOrEmpty(p) && File.Exists(p)) bytes += new FileInfo(p).Length;
                }
            var maxW = 0;
            foreach (var m in maps) if (m.lightmapColor != null) maxW = Mathf.Max(maxW, m.lightmapColor.width);
            string rep = string.Format("굽기 {0:F0}초 ({1:F1}분), 라이트맵 {2}장 (최대 {3}px, {4:F1}M 텍셀), 메모리 {5:F1}MB, 디스크 {6:F1}MB",
                seconds, seconds / 60.0, maps.Length, maxW, texels / 1e6, mem / 1048576.0, bytes / 1048576.0);
            rec.lastBake = DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "  " + rep;
            EditorUtility.SetDirty(rec);
            var scene = root.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);
            Debug.Log("[병점역] 조명 굽기 완료: " + rep);
            return rep;
        }

        /// <summary>굽기 전 상태로: 라이트 원래대로, Contribute GI · 프로브 · 라이트맵 · 라이트맵 UV 제거, Lighting Settings 원래대로.</summary>
        public static string RevertBake(GameObject root)
        {
            var rec = root.GetComponentInChildren<BJ_BakeRecord>(true);
            int lights = 0;
            if (rec != null)
            {
                RestoreLights(rec);
                lights = rec.lights.Count;
            }
            else
            {
                // 기록이 없으면 (손으로 구운 씬 등) 맵의 라이트를 모두 실시간으로
                foreach (var l in root.GetComponentsInChildren<Light>(true))
                {
                    if (l.lightmapBakeType == LightmapBakeType.Realtime) continue;
                    l.lightmapBakeType = LightmapBakeType.Realtime;
                    EditorUtility.SetDirty(l);
                    lights++;
                }
            }

            int gi = 0;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var g = r.gameObject;
                var f = GameObjectUtility.GetStaticEditorFlags(g);
                if ((f & StaticEditorFlags.ContributeGI) != 0)
                {
                    GameObjectUtility.SetStaticEditorFlags(g, f & ~StaticEditorFlags.ContributeGI);
                    gi++;
                }
                if (r.receiveGI != ReceiveGI.Lightmaps) r.receiveGI = ReceiveGI.Lightmaps;
                if (r.scaleInLightmap != 1f) r.scaleInLightmap = 1f;
            }
            int probes = 0;
            foreach (var pg in root.GetComponentsInChildren<LightProbeGroup>(true))
                if (pg.name == ProbeGroupName) { probes += pg.probePositions.Length; Object.DestroyImmediate(pg.gameObject); }

            LightingSettings ls = null;
            if (rec != null && !string.IsNullOrEmpty(rec.lightingSettingsPath)) ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(rec.lightingSettingsPath);
            if (ls == null) ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(RealtimeLightingPath);
            if (ls == null)
            {
                EnsureFolder(GenDir);
                ls = new LightingSettings { name = "BJ_Lighting", bakedGI = false, realtimeGI = false };
                AssetDatabase.CreateAsset(ls, RealtimeLightingPath);
            }
            Lightmapping.Clear();
            Lightmapping.ClearLightingDataAsset();
            Lightmapping.lightingSettings = ls;
            string folder = LightmapFolder(root.scene);
            if (AssetDatabase.IsValidFolder(folder) && AssetDatabase.FindAssets("", new[] { folder }).Length == 0) AssetDatabase.DeleteAsset(folder);

            int uv = StripLightmapUVs(root);
            if (rec != null) Object.DestroyImmediate(rec.gameObject);
            return string.Format("라이트 {0}개 원래대로, Contribute GI 해제 {1}개, 라이트 프로브 {2}개 · 라이트맵 삭제, 라이트맵 UV 지운 메쉬 {3}개, Lighting Settings → {4}",
                lights, gi, probes, uv, ls.name);
        }

        // ------------------------------------------------------------------ lights
        /// <summary>구울 라이트: 켜져 있는 포인트 · 스포트 중 BJ_FlickerLight 가 다루지 않고, 움직이는 물체에 달리지 않은 것.</summary>
        public static List<Light> BakeCandidates(GameObject root)
        {
            var flicker = new HashSet<Light>();
            foreach (var f in root.GetComponentsInChildren<BJ_FlickerLight>(true))
                if (f.lights != null) foreach (var l in f.lights) if (l != null) flicker.Add(l);
            var list = new List<Light>();
            foreach (var l in root.GetComponentsInChildren<Light>(false))
            {
                if (!l.enabled || (l.type != LightType.Point && l.type != LightType.Spot)) continue;
                if (flicker.Contains(l) || l.GetComponent<BJ_FlickerLight>() != null) continue;
                var p = l.transform.parent;
                if (p != null && GameObjectUtility.GetStaticEditorFlags(p.gameObject) == 0) continue;
                list.Add(l);
            }
            return list;
        }

        static Light MoonLight(GameObject root)
        {
            var sun = RenderSettings.sun;
            if (sun != null && sun.type == LightType.Directional && sun.transform.IsChildOf(root.transform)) return sun;
            foreach (var l in root.GetComponentsInChildren<Light>(false))
                if (l.type == LightType.Directional) return l;
            return null;
        }

        static Transform AtmosphereGroup(GameObject root)
        {
            var t = root.transform.Find("Atmosphere_조명_안개_후처리");
            return t != null ? t : root.transform;
        }

        static string LightmapFolder(UnityEngine.SceneManagement.Scene scene)
        {
            return Path.ChangeExtension(scene.path, null).Replace('\\', '/');
        }

        /// <summary>그림자를 드리우는 Static 박스들 (월드 좌표 면 평면 6개씩) - 라이트가 박스 안에 갇혔는지 볼 때 쓴다.</summary>
        static List<Plane[]> ShadowCasterBoxes(GameObject root)
        {
            var boxes = new List<Plane[]>();
            var cache = new Dictionary<Mesh, List<Plane[]>>();
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (!r.enabled || r.shadowCastingMode == ShadowCastingMode.Off || IsTransparent(r)) continue;
                var mf = r.GetComponent<MeshFilter>();
                var mesh = mf != null ? mf.sharedMesh : null;
                if (mesh == null || !mesh.isReadable) continue;
                List<Plane[]> local;
                if (!cache.TryGetValue(mesh, out local)) { local = MeshBoxes(mesh); cache[mesh] = local; }
                var m = r.transform.localToWorldMatrix;
                foreach (var b in local)
                {
                    var w = new Plane[6];
                    for (int i = 0; i < 6; i++) w[i] = m.TransformPlane(b[i]);
                    boxes.Add(w);
                }
            }
            return boxes;
        }

        /// <summary>정점 24개(면 4개씩 6면)짜리 박스들로 된 메쉬면 박스마다 바깥을 향한 면 평면 6개. 아니면 빈 목록.</summary>
        static List<Plane[]> MeshBoxes(Mesh mesh)
        {
            var list = new List<Plane[]>();
            var v = mesh.vertices;
            var n = mesh.normals;
            if (v.Length == 0 || v.Length % 24 != 0 || n.Length != v.Length) return list;
            for (int k = 0; k < v.Length; k += 24)
            {
                var pl = new Plane[6];
                for (int f = 0; f < 6; f++) pl[f] = new Plane(n[k + f * 4], v[k + f * 4]);
                list.Add(pl);
            }
            return list;
        }

        static bool InsideAnyBox(Vector3 p, List<Plane[]> boxes)
        {
            foreach (var b in boxes)
            {
                bool inside = true;
                for (int i = 0; i < 6 && inside; i++) inside = b[i].GetDistanceToPoint(p) < -0.01f;
                if (inside) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ GI static + lightmap UV
        /// <summary>구운 라이트가 닿지 않는 렌더러(달빛 · 환경광만 받음)의 라이트맵 해상도 배율</summary>
        const float FarLightmapScale = 0.5f;

        struct GIStats { public int lightmapped, small, noUV, transparent, newUV, far; public double texels; }

        /// <summary>라이트맵 UV 정보. used = UV 가 차지하는 사각형 텍셀 수(아틀라스는 이 범위만 잡는다), real = 실제 면적 텍셀 수. 둘 다 texelsPerMeter 기준.</summary>
        struct UVInfo { public bool ok, own; public float texelsPerMeter, realTexels, usedTexels, side; }

        static GIStats SetupGIStatic(GameObject root, List<Light> baked)
        {
            var st = new GIStats();
            var items = new List<MeshRenderer>();
            var minScale = new Dictionary<Mesh, float>();
            var previousLayout = new Dictionary<MeshRenderer, Vector2>();
            var maps = LightmapSettings.lightmaps;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var g = r.gameObject;
                var flags = GameObjectUtility.GetStaticEditorFlags(g);
                var mf = r.GetComponent<MeshFilter>();
                var mesh = mf != null ? mf.sharedMesh : null;
                // 움직이는 물체(깜빡이는 등 등) · 꺼진 것 · 유리 · 데칼은 굽기에서 빼고(빛을 가리지 않게) 프로브로 칠한다
                bool skip = flags == 0 || !r.enabled || !g.activeInHierarchy || mesh == null || IsTransparent(r);
                if (skip)
                {
                    if ((flags & StaticEditorFlags.ContributeGI) != 0) GameObjectUtility.SetStaticEditorFlags(g, flags & ~StaticEditorFlags.ContributeGI);
                    if (flags != 0 && r.enabled && g.activeInHierarchy && mesh != null) st.transparent++;
                    continue;
                }
                GameObjectUtility.SetStaticEditorFlags(g, flags | StaticEditorFlags.ContributeGI);
                if (r.lightmapIndex >= 0 && r.lightmapIndex < maps.Length && maps[r.lightmapIndex].lightmapColor != null)
                {
                    var map = maps[r.lightmapIndex].lightmapColor;
                    float pixels = Mathf.Min(map.width * r.lightmapScaleOffset.x, map.height * r.lightmapScaleOffset.y);
                    if (pixels > 0f) previousLayout[r] = new Vector2(r.scaleInLightmap, pixels);
                }
                float s = NearAnyLight(r.bounds, baked) ? 1f : FarLightmapScale;
                r.scaleInLightmap = s;
                items.Add(r);
                float m;
                minScale[mesh] = minScale.TryGetValue(mesh, out m) ? Mathf.Min(m, s) : s;
            }

            // 메쉬마다 그 메쉬를 쓰는 렌더러 중 가장 낮은 해상도 기준으로 여백을 잡아 UV 를 만든다
            var uvs = new Dictionary<Mesh, UVInfo>();
            foreach (var kv in minScale)
            {
                var info = EnsureLightmapUVs(kv.Key, BakeTexelsPerMeter * kv.Value);
                uvs[kv.Key] = info;
                if (info.own && info.usedTexels > 0f) { EditorUtility.SetDirty(kv.Key); st.newUV++; }
            }

            foreach (var r in items)
            {
                var mesh = r.GetComponent<MeshFilter>().sharedMesh;
                var info = uvs[mesh];
                float requestedScale = r.scaleInLightmap;
                bool lightmap = info.ok && !SmallForLightmap(r, info);
                if (!lightmap)
                {
                    // 작은 소품 · UV 없는 메쉬(내장 원기둥 · 판 등)는 굽기에서 아예 빼고 움직이는 물체처럼 프로브로 칠한다.
                    // 지금도 실시간 조명 대부분이 그림자가 없어 소품 그림자는 원래 없고, 굽기 인스턴스가 1/3 로 줄어 메모리 · 시간이 준다
                    var g = r.gameObject;
                    GameObjectUtility.SetStaticEditorFlags(g, GameObjectUtility.GetStaticEditorFlags(g) & ~StaticEditorFlags.ContributeGI);
                    r.scaleInLightmap = 1f;
                }
                else
                {
                    r.receiveGI = ReceiveGI.Lightmaps;
                    // 실제 아틀라스 배치로 보정하여 차트 간격과 요청한 텍셀 밀도를 유지한다.
                    Vector2 previous;
                    if (info.side > 0f && previousLayout.TryGetValue(r, out previous))
                    {
                        float desired = info.side * r.scaleInLightmap * BakeTexelsPerMeter / info.texelsPerMeter;
                        desired = Mathf.Min(1000f, desired);
                        r.scaleInLightmap = Mathf.Max(r.scaleInLightmap, previous.x * desired / previous.y);
                    }
                }
                if (!info.ok) st.noUV++;
                else if (!lightmap) st.small++;
                else
                {
                    st.lightmapped++;
                    float s = requestedScale;
                    if (s < 1f) st.far++;
                    double k = s * BakeTexelsPerMeter / info.texelsPerMeter;
                    st.texels += info.usedTexels > 0f ? info.usedTexels * k * k : WorldArea(r, mesh) * 2.0 * (s * BakeTexelsPerMeter) * (s * BakeTexelsPerMeter);
                }
            }
            AssetDatabase.SaveAssets();
            return st;
        }

        static bool NearAnyLight(Bounds b, List<Light> lights)
        {
            foreach (var l in lights)
                if (b.SqrDistance(l.transform.position) < l.range * l.range) return true;
            return false;
        }

        static bool OwnMesh(Mesh mesh)
        {
            string p = AssetDatabase.GetAssetPath(mesh);
            return p.StartsWith(GenDir + "/");
        }

        /// <summary>생성 메쉬는 라이트맵 UV 를 (다시) 만들고, 그 밖의 메쉬는 이미 UV2 가 있는지만 본다.</summary>
        static UVInfo EnsureLightmapUVs(Mesh mesh, float texelsPerMeter)
        {
            var info = new UVInfo { texelsPerMeter = texelsPerMeter };
            var uv2 = new List<Vector2>();
            if (!OwnMesh(mesh) || !mesh.isReadable)
            {
                mesh.GetUVs(1, uv2);
                info.ok = uv2.Count > 0;
                return info;
            }
            info.own = true;
            int w, h; float real;
            if (BuildBoxLightmapUVs(mesh, texelsPerMeter, out w, out h, out real))
            {
                info.ok = true; info.usedTexels = (float)w * h; info.realTexels = real; info.side = Mathf.Max(w, h);
                return info;
            }
            // 박스로 나눌 수 없는 메쉬 (곡면 등) - 유니티 기본 언랩
            Unwrapping.GenerateSecondaryUVSet(mesh);
            mesh.GetUVs(1, uv2);
            info.ok = uv2.Count > 0;
            return info;
        }

        /// <summary>
        /// 라이트맵에 넣기엔 작은 렌더러 (면적이 몇 텍셀 안 되거나 차트 여백이 대부분인 것) - 굽기에서 빼고 라이트 프로브로 칠한다.
        /// 길게 뻗은 물체(레일 · 전차선 · 난간 등)는 프로브 하나로는 위치마다 밝기가 달라지지 않으므로 라이트맵에 둔다.
        /// </summary>
        static bool SmallForLightmap(MeshRenderer r, UVInfo info)
        {
            var size = r.bounds.size;
            float longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            if (longest >= 3f) return false;
            if (info.usedTexels <= 0f) return longest < 1f;
            float k = r.scaleInLightmap * BakeTexelsPerMeter / info.texelsPerMeter;
            return info.realTexels * k * k < 48f || info.usedTexels > 5f * info.realTexels;
        }

        static double WorldArea(Renderer r, Mesh mesh)
        {
            var v = mesh.vertices;
            var t = mesh.triangles;
            var m = r.transform.localToWorldMatrix;
            double a = 0;
            for (int i = 0; i < t.Length; i += 3)
                a += 0.5 * Vector3.Cross(m.MultiplyVector(v[t[i + 1]] - v[t[i]]), m.MultiplyVector(v[t[i + 2]] - v[t[i]])).magnitude;
            return a;
        }

        static int StripLightmapUVs(GameObject root)
        {
            var seen = new HashSet<Mesh>();
            var tmp = new List<Vector2>();
            int n = 0;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                if (mesh == null || !seen.Add(mesh) || !OwnMesh(mesh) || !mesh.isReadable) continue;
                mesh.GetUVs(1, tmp);
                if (tmp.Count == 0) continue;
                mesh.uv2 = new Vector2[0];
                EditorUtility.SetDirty(mesh);
                n++;
            }
            if (n > 0) AssetDatabase.SaveAssets();
            return n;
        }

        // ------------------------------------------------------------------ box lightmap UV
        /// <summary>
        /// 박스를 이어 붙인 메쉬(평평한 면 조각들)의 라이트맵 UV(UV2).
        ///  - 삼각형으로 이어진 정점 묶음 = 면 하나 (MeshBuilder 는 면마다 정점 4개).
        ///    별도 정점으로 된 면은 같은 평면에서도 UV를 공유하지 않고 독립된 차트로 배치한다.
        ///  - 차트는 실제 m 크기 그대로(텍셀 밀도 균일) 사각형 선반 채우기로 0~1 에 담는다. 여백은 texelsPerMeter 기준 UV2Pad 텍셀.
        ///  - 1텍셀보다 얇은 면(두께 면)도 실제 크기 그대로 두고 자리만 1텍셀을 잡는다. UV 면적이 월드 면적에 정확히 비례해야
        ///    유니티가 잡는 텍셀 밀도(해상도 × √(월드 면적 / UV 면적))가 의도대로 나오고 여백도 텍셀 단위로 맞는다.
        /// 평평하지 않은 면이 있으면(곡면 메쉬) false. usedW × usedH = UV 가 차지하는 사각형(텍셀) - 라이트맵 아틀라스는 이 범위만 잡는다.
        /// realTexels = 실제 면적 텍셀 수.
        /// </summary>
        public static bool BuildBoxLightmapUVs(Mesh mesh, float texelsPerMeter, out int usedW, out int usedH, out float realTexels)
        {
            usedW = 0; usedH = 0; realTexels = 0f;
            var v = mesh.vertices;
            var tri = mesh.triangles;
            int nv = v.Length;
            if (nv == 0 || tri.Length == 0) return false;
            float T = texelsPerMeter;

            // 1. 면 = 삼각형으로 이어진 정점 묶음
            var uf = new int[nv];
            for (int i = 0; i < nv; i++) uf[i] = i;
            for (int i = 0; i < tri.Length; i += 3) { BakeUnion(uf, tri[i], tri[i + 1]); BakeUnion(uf, tri[i], tri[i + 2]); }
            var faceId = new int[nv];
            for (int i = 0; i < nv; i++) faceId[i] = -1;
            var faceRoot = new List<int>();
            var normal = new List<Vector3>();
            for (int i = 0; i < tri.Length; i += 3)
            {
                int r = BakeFind(uf, tri[i]);
                if (faceId[r] < 0) { faceId[r] = faceRoot.Count; faceRoot.Add(r); normal.Add(Vector3.zero); }
                int f = faceId[r];
                normal[f] += Vector3.Cross(v[tri[i + 1]] - v[tri[i]], v[tri[i + 2]] - v[tri[i]]);
            }
            int nf = faceRoot.Count;
            var verts = new List<int>[nf];
            for (int i = 0; i < nv; i++)
            {
                int r = BakeFind(uf, i);
                int f = faceId[r];
                if (f < 0) continue;   // 삼각형에 안 쓰인 정점
                if (verts[f] == null) verts[f] = new List<int>(4);
                verts[f].Add(i);
            }
            var planeD = new float[nf];
            for (int f = 0; f < nf; f++)
            {
                if (normal[f].sqrMagnitude < 1e-12f) return false;
                var nn = normal[f].normalized;
                normal[f] = nn;
                var p0 = v[verts[f][0]];
                planeD[f] = Vector3.Dot(nn, p0);
                foreach (int i in verts[f])
                    if (Mathf.Abs(Vector3.Dot(nn, v[i]) - planeD[f]) > 2e-3f) return false;   // 곡면
            }
            for (int i = 0; i < tri.Length; i += 3)   // 접힌 면(삼각형 법선이 면 법선과 다름)도 곡면 취급
            {
                var c = Vector3.Cross(v[tri[i + 1]] - v[tri[i]], v[tri[i + 2]] - v[tri[i]]);
                if (c.sqrMagnitude < 1e-12f) continue;
                if (Vector3.Dot(c.normalized, normal[faceId[BakeFind(uf, tri[i])]]) < 0.999f) return false;
            }

            // 2. 같은 평면끼리 묶고, 평면마다 2D 축을 정해 면을 투영
            var groups = new Dictionary<long, List<int>>();
            for (int f = 0; f < nf; f++)
            {
                var nn = normal[f];
                long key = ((long)Mathf.RoundToInt(nn.x * 200f) + 256) | (((long)Mathf.RoundToInt(nn.y * 200f) + 256) << 10) |
                           (((long)Mathf.RoundToInt(nn.z * 200f) + 256) << 20) | ((long)Mathf.RoundToInt(planeD[f] * 200f) << 30);
                List<int> list;
                if (!groups.TryGetValue(key, out list)) { list = new List<int>(); groups[key] = list; }
                list.Add(f);
            }
            var rmin = new Vector2[nf];
            var rmax = new Vector2[nf];
            var axU = new Vector3[nf];
            var axV = new Vector3[nf];
            var charts = new List<List<int>>();
            foreach (var list in groups.Values)
            {
                Vector3 u, w;
                PlaneAxes(v, verts[list[0]], normal[list[0]], out u, out w);
                foreach (int f in list)
                {
                    axU[f] = u; axV[f] = w;
                    Vector2 a = new Vector2(float.MaxValue, float.MaxValue), b = new Vector2(float.MinValue, float.MinValue);
                    foreach (int i in verts[f])
                    {
                        var q = new Vector2(Vector3.Dot(v[i], u), Vector3.Dot(v[i], w));
                        a = Vector2.Min(a, q); b = Vector2.Max(b, q);
                    }
                    rmin[f] = a; rmax[f] = b;
                }
                // 별도 정점으로 된 면은 같은 평면이어도 별도 차트다.
                // UV를 공유하면 Unity가 경계 주변과 겹친 면을 UV overlap으로 판정한다.
                foreach (int f in list) charts.Add(new List<int> { f });
            }

            // 3. 차트 크기 (텍셀) - 가로가 길게 눕혀서 선반 채우기
            int nc = charts.Count;
            var cMin = new Vector2[nc];
            var cTex = new Vector2[nc];   // 차트 안쪽 크기 (텍셀)
            var cRot = new bool[nc];
            var fw = new int[nc];
            var fh = new int[nc];
            double area = 0;
            for (int c = 0; c < nc; c++)
            {
                Vector2 a = new Vector2(float.MaxValue, float.MaxValue), b = new Vector2(float.MinValue, float.MinValue);
                foreach (int f in charts[c]) { a = Vector2.Min(a, rmin[f]); b = Vector2.Max(b, rmax[f]); area += (rmax[f].x - rmin[f].x) * (rmax[f].y - rmin[f].y); }
                var sz = b - a;
                cMin[c] = a;
                var tex = sz * T;
                cRot[c] = tex.y > tex.x;
                cTex[c] = cRot[c] ? new Vector2(tex.y, tex.x) : tex;
                fw[c] = Mathf.Max(1, Mathf.CeilToInt(cTex[c].x - 1e-3f)) + 2 * UV2Pad;
                fh[c] = Mathf.Max(1, Mathf.CeilToInt(cTex[c].y - 1e-3f)) + 2 * UV2Pad;
            }
            realTexels = (float)(area * T * T);

            // 4. 선반 채우기 - 폭 후보 몇 개 중 차지하는 사각형이 가장 작은 것
            var order = new int[nc];
            for (int c = 0; c < nc; c++) order[c] = c;
            Array.Sort(order, (x, y) => fh[y] != fh[x] ? fh[y].CompareTo(fh[x]) : fw[y].CompareTo(fw[x]));
            long padded = 0;
            int widest = 0;
            for (int c = 0; c < nc; c++) { padded += (long)fw[c] * fh[c]; widest = Mathf.Max(widest, fw[c]); }
            var px = new int[nc];
            var py = new int[nc];
            long best = long.MaxValue;
            int bestW = widest;
            foreach (float k in new[] { 1f, 1.08f, 1.18f, 1.3f, 1.5f, 2f })
            {
                int W = Mathf.Max(widest, Mathf.CeilToInt(Mathf.Sqrt(padded) * k));
                long a = (long)W * ShelfPack(order, fw, fh, W, null, null);
                if (a < best) { best = a; bestW = W; }
            }
            usedH = ShelfPack(order, fw, fh, bestW, px, py);
            for (int c = 0; c < nc; c++) usedW = Mathf.Max(usedW, px[c] + fw[c]);
            int side = Mathf.Max(usedW, usedH);   // u · v 를 같은 배율로 나눠야 텍셀이 정사각형

            // 5. UV
            var uv2 = new Vector2[nv];
            float inv = 1f / side;
            for (int c = 0; c < nc; c++)
            {
                foreach (int f in charts[c])
                    foreach (int i in verts[f])
                    {
                        float lx = (Vector3.Dot(v[i], axU[f]) - cMin[c].x) * T;
                        float ly = (Vector3.Dot(v[i], axV[f]) - cMin[c].y) * T;
                        if (cRot[c]) { float t = lx; lx = ly; ly = t; }
                        uv2[i] = new Vector2((px[c] + UV2Pad + lx) * inv, (py[c] + UV2Pad + ly) * inv);
                    }
            }
            mesh.uv2 = uv2;
            return true;
        }

        /// <summary>평면의 2D 축: 첫 면의 변 중 면을 가장 작은 사각형으로 감싸는 방향을 u 로 (박스 면이면 변과 나란해짐).</summary>
        static void PlaneAxes(Vector3[] v, List<int> face, Vector3 n, out Vector3 u, out Vector3 w)
        {
            u = Vector3.zero; w = Vector3.zero;
            float bestArea = float.MaxValue;
            for (int a = 0; a < face.Count; a++)
                for (int b = a + 1; b < face.Count; b++)
                {
                    var e = v[face[b]] - v[face[a]];
                    e -= n * Vector3.Dot(e, n);
                    if (e.sqrMagnitude < 1e-10f) continue;
                    var cu = e.normalized;
                    var cw = Vector3.Cross(n, cu);
                    float x0 = float.MaxValue, x1 = float.MinValue, y0 = float.MaxValue, y1 = float.MinValue;
                    foreach (int i in face)
                    {
                        float x = Vector3.Dot(v[i], cu), y = Vector3.Dot(v[i], cw);
                        x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y);
                    }
                    float ar = (x1 - x0) * (y1 - y0);
                    if (ar < bestArea - 1e-6f) { bestArea = ar; u = cu; w = cw; }
                }
            if (u == Vector3.zero)
            {
                u = Vector3.Cross(n, Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.forward).normalized;
                w = Vector3.Cross(n, u);
            }
        }

        /// <summary>높이순으로 정렬된 사각형을 폭 W 선반에 채운다. 채운 높이(텍셀)를 돌려준다.</summary>
        static int ShelfPack(int[] order, int[] fw, int[] fh, int W, int[] px, int[] py)
        {
            int x = 0, y = 0, shelf = 0;
            foreach (int c in order)
            {
                if (x + fw[c] > W) { y += shelf; x = 0; shelf = 0; }
                if (px != null) { px[c] = x; py[c] = y; }
                x += fw[c];
                shelf = Mathf.Max(shelf, fh[c]);
            }
            return y + shelf;
        }

        static int BakeFind(int[] uf, int i)
        {
            while (uf[i] != i) { uf[i] = uf[uf[i]]; i = uf[i]; }
            return i;
        }

        static void BakeUnion(int[] uf, int a, int b)
        {
            a = BakeFind(uf, a); b = BakeFind(uf, b);
            if (a != b) uf[b] = a;
        }

        // ------------------------------------------------------------------ light probes
        /// <summary>
        /// 바닥(콜라이더) 위 0.4m · 1.9m 에 라이트 프로브를 깐다. 구운 라이트 근처는 2.5m 간격, 나머지는 7.5m 간격.
        /// 위에서 아래로 레이를 이어 쏘아 여러 층(승강장 · 대합실 · 지붕)을 모두 찾는다.
        /// </summary>
        static int PlaceLightProbes(GameObject root, List<Light> baked)
        {
            foreach (var pg in root.GetComponentsInChildren<LightProbeGroup>(true))
                if (pg.name == ProbeGroupName) Object.DestroyImmediate(pg.gameObject);
            Physics.SyncTransforms();

            var b = new Bounds(root.transform.position, Vector3.zero);
            bool any = false;
            foreach (var c in root.GetComponentsInChildren<Collider>(false))
            {
                if (c.isTrigger) continue;
                if (!any) { b = c.bounds; any = true; } else b.Encapsulate(c.bounds);
            }
            if (!any) return 0;

            const float step = 2.5f;
            const int sparse = 3;
            const float near = 1.5f;
            var pts = new List<Vector3>();
            float top = b.max.y + 1f;
            for (int ix = 0; ; ix++)
            {
                float x = b.min.x + ix * step;
                if (x > b.max.x) break;
                for (int iz = 0; ; iz++)
                {
                    float z = b.min.z + iz * step;
                    if (z > b.max.z) break;
                    bool use = ix % sparse == 0 && iz % sparse == 0;
                    for (int i = 0; i < baked.Count && !use; i++)
                    {
                        var lp = baked[i].transform.position;
                        float r = baked[i].range + near;
                        use = (lp.x - x) * (lp.x - x) + (lp.z - z) * (lp.z - z) < r * r;
                    }
                    if (!use) continue;

                    float y = top, ceiling = float.PositiveInfinity;
                    for (int layer = 0; layer < 16; layer++)
                    {
                        RaycastHit hit;
                        if (!Physics.Raycast(new Vector3(x, y, z), Vector3.down, out hit, y - b.min.y + 1f, ~0, QueryTriggerInteraction.Ignore)) break;
                        if (hit.normal.y > 0.5f)
                            foreach (float h in new[] { 0.4f, 1.9f })
                            {
                                var p = hit.point + Vector3.up * h;
                                if (p.y < ceiling - 0.15f && !Physics.CheckSphere(p, 0.12f, ~0, QueryTriggerInteraction.Ignore)) pts.Add(p);
                            }
                        ceiling = hit.point.y;
                        y = hit.point.y - 0.02f;
                    }
                }
            }
            if (pts.Count == 0) return 0;
            var go = new GameObject(ProbeGroupName);
            go.transform.SetParent(AtmosphereGroup(root), false);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            var group = go.AddComponent<LightProbeGroup>();
            var local = new Vector3[pts.Count];
            for (int i = 0; i < pts.Count; i++) local[i] = go.transform.InverseTransformPoint(pts[i]);
            group.probePositions = local;
            return pts.Count;
        }

        // ------------------------------------------------------------------ lighting settings
        static LightingSettings BakedLightingSettings(MoonBake moon)
        {
            EnsureFolder(GenDir);
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(BakedLightingPath);
            if (ls == null)
            {
                ls = new LightingSettings { name = "BJ_LightingBaked" };
                AssetDatabase.CreateAsset(ls, BakedLightingPath);
            }
            ls.bakedGI = true;
            ls.realtimeGI = false;
            ls.autoGenerate = false;
            ls.mixedBakeMode = moon == MoonBake.Mixed ? MixedLightingMode.Shadowmask : MixedLightingMode.IndirectOnly;
            ls.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;   // GPU 를 못 쓰면 유니티가 CPU 로 바꿔 굽는다
            ls.lightmapResolution = BakeTexelsPerMeter;
            ls.lightmapPadding = 2;
            ls.lightmapMaxSize = 1024;   // 한 번에 굽는 아틀라스가 작아 메모리를 덜 쓴다 (내장 GPU · 메모리 8~16GB PC)
            ls.directionalityMode = LightmapsMode.NonDirectional;   // 용량 절반. 어두운 맵이라 노멀맵 반응 차이가 작다
            ls.lightmapCompression = LightmapCompression.NormalQuality;
            ls.ao = false;
            ls.maxBounces = 2;
            ls.directSampleCount = 32;
            ls.indirectSampleCount = 128;
            ls.environmentSampleCount = 64;
            ls.filteringMode = LightingSettings.FilterMode.Auto;
            ls.prioritizeView = false;
            EditorUtility.SetDirty(ls);
            AssetDatabase.SaveAssets();
            return ls;
        }
    }
}
