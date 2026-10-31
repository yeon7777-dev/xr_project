using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Byeongjeom;

namespace Byeongjeom.MapGen
{
    /// <summary>
    /// 메타 퀘스트(PC 연결: Quest Link / Air Link)용 VR 플레이어 추가.
    /// 메뉴: Tools > 병점역 맵 > VR (메타 퀘스트) > ...
    /// VR 모드를 켜 두면 이후 '맵 생성'을 다시 해도 VR 플레이어가 자동으로 들어간다. (PC별 설정)
    /// </summary>
    public static class BJ_VRSetup
    {
        const string PrefKey = "BJ_ByeongjeomStation_VRMode";
        const string Menu = "Tools/병점역 맵/VR (메타 퀘스트)/";
        const string RigName = "VR_Player (Meta Quest)";
        const string RootName = "ByeongjeomStation_병점역";
        const string FootstepDir = "Assets/Standard Assets/Characters/FirstPersonCharacter/Audio/";
        internal static readonly Vector3 SpawnPos = new Vector3(70f, 0.08f, -12f);   // 역앞광장, 1번 출구를 바라봄
        internal const float SpawnYaw = -90f;
        static readonly string[] VRUncomfortableEffects = { "FilmGrain", "ChromaticAberration", "Vignette" };

        public static bool VRModeEnabled { get { return EditorPrefs.GetBool(PrefKey, false); } }

        [MenuItem(Menu + "VR 플레이어 추가 (VR 모드 켜기)", false, 80)]
        static void AddMenu()
        {
            var root = GameObject.Find(RootName);
            if (root == null)
            {
                EditorUtility.DisplayDialog("병점역 VR", "먼저 'Tools > 병점역 맵 > 맵 생성'으로 병점역 씬을 만들거나 열어 주세요.", "확인");
                return;
            }
            EditorPrefs.SetBool(PrefKey, true);
            AddVRPlayer(root.scene);
            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveScene(root.scene);
            ShowChecklist();
        }

        [MenuItem(Menu + "VR 모드 끄기 (데스크톱 미리보기 카메라로)", false, 81)]
        static void RemoveMenu()
        {
            EditorPrefs.SetBool(PrefKey, false);
            var rig = GameObject.Find(RigName);
            if (rig != null) Object.DestroyImmediate(rig);
            var preview = FindPreviewCamera();
            if (preview != null) preview.SetActive(true);
            SetVRComfortPostFX(false);
            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        [MenuItem(Menu + "설정 체크리스트 보기", false, 82)]
        static void ShowChecklist()
        {
            bool urp = GraphicsSettings.currentRenderPipeline != null;
            bool xrMgmt = BJ.FindType("UnityEngine.XR.Management.XRGeneralSettings") != null;
            bool openxr = BJ.FindType("UnityEngine.XR.OpenXR.OpenXRSettings") != null;
            bool oculus = BJ.FindType("Unity.XR.Oculus.OculusLoader") != null;
            string msg =
                "[유니티 설정]\n" +
                "1. Edit > Project Settings > XR Plug-in Management" + (xrMgmt ? "  (설치됨)" : "  → 'Install XR Plugin Management' 클릭") + "\n" +
                "2. PC(모니터 아이콘) 탭에서 'OpenXR' 체크" + (openxr ? "  (설치됨)" : "") + (oculus ? "\n   (또는 Oculus 체크도 가능 - 설치됨)" : "") + "\n" +
                "3. OpenXR > Enabled Interaction Profiles 에 '+' → 'Oculus Touch Controller Profile' 추가\n" +
                "4. Render Mode: " + (urp ? "Single Pass Instanced (기본값 그대로)" : "Multi-pass 권장 (Built-in 파이프라인)") + "\n" +
                "   노란 경고 아이콘이 있으면 'Fix All' 클릭\n\n" +
                "[PC / 퀘스트 설정]\n" +
                "5. PC에 Meta Quest Link 앱 설치 → 설정 > 일반 > 'OpenXR 런타임'을 Meta Quest Link 로 설정\n" +
                "6. 퀘스트를 케이블(Link) 또는 Air Link 로 연결하고 Link 모드 진입\n" +
                "7. 유니티에서 Play ▶ (헤드셋이 없으면 WASD+마우스 데스크톱 테스트 모드)\n\n" +
                "[조작]\n" +
                "왼쪽 스틱 이동 / 왼쪽 스틱 누르기 달리기 / 오른쪽 스틱 좌우 45도 회전 / A 버튼 손전등";
            EditorUtility.DisplayDialog("병점역 VR (메타 퀘스트) 설정", msg, "확인");
        }

        static GameObject FindPreviewCamera()
        {
            var root = GameObject.Find(RootName);
            if (root == null) return null;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("PreviewCamera")) return t.gameObject;
            return null;
        }

        static Material LoadMat(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Material>(BJ.MatDir + "/" + name + ".mat");
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale, Quaternion localRot, string mat)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name;
            var col = g.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            g.transform.localRotation = localRot;
            g.transform.localScale = localScale;
            var m = LoadMat(mat);
            var r = g.GetComponent<MeshRenderer>();
            if (m != null) r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return g;
        }

        /// <summary>현재 씬에 VR 플레이어를 만든다 (이미 있으면 다시 만든다).</summary>
        public static void AddVRPlayer(Scene scene)
        {
            var old = GameObject.Find(RigName);
            if (old != null) Object.DestroyImmediate(old);
            // 1인칭 · 3인칭 플레이어와 카메라 리그 (VR 은 항상 역앞광장에서 시작)
            Vector3 feet = SpawnPos;
            float yaw = SpawnYaw;
            bool inherited;
            BJ_FPSPlayer.RemovePlayers(scene, ref feet, ref yaw, out inherited);
            var preview = FindPreviewCamera();
            if (preview != null) preview.SetActive(false);
            bool urp = GraphicsSettings.currentRenderPipeline != null;

            var rig = new GameObject(RigName);
            SceneManager.MoveGameObjectToScene(rig, scene);
            rig.transform.position = SpawnPos;
            rig.transform.rotation = Quaternion.Euler(0f, SpawnYaw, 0f);
            var cc = rig.AddComponent<CharacterController>();
            cc.height = 1.7f; cc.radius = 0.25f; cc.center = new Vector3(0f, 0.88f, 0f);
            cc.stepOffset = 0.35f; cc.slopeLimit = 50f; cc.skinWidth = 0.03f; cc.minMoveDistance = 0f;

            var offset = new GameObject("CameraOffset").transform;
            offset.SetParent(rig.transform, false);

            // 머리(HMD) 카메라
            var camGo = new GameObject("Main Camera (HMD)");
            camGo.transform.SetParent(offset, false);
            camGo.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 260f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
            cam.stereoTargetEye = StereoTargetEyeMask.Both;
            cam.allowHDR = true;
            camGo.AddComponent<AudioListener>();
            if (urp)
            {
                var camDataT = BJ.FindType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData");
                if (camDataT != null)
                {
                    var data = camGo.GetComponent(camDataT);
                    if (data == null) data = camGo.AddComponent(camDataT);
                    SetProp(data, "renderPostProcessing", true);
                }
                SetVRComfortPostFX(true);
            }
            else
            {
                cam.allowMSAA = false;
                var fog = camGo.AddComponent<BJ_DeferredFog>();
                fog.fogShader = Shader.Find("Hidden/BJ_DeferredFog");
            }

            // 멀미 완화용 터널 비네트 (실행 중에만 보임)
            var tunnel = GameObject.CreatePrimitive(PrimitiveType.Quad);
            tunnel.name = "ComfortVignette";
            Object.DestroyImmediate(tunnel.GetComponent<Collider>());
            tunnel.transform.SetParent(camGo.transform, false);
            tunnel.transform.localPosition = new Vector3(0f, 0f, 0.1f);
            tunnel.transform.localScale = new Vector3(0.6f, 0.6f, 1f);
            var tr = tunnel.GetComponent<MeshRenderer>();
            tr.shadowCastingMode = ShadowCastingMode.Off;
            tr.receiveShadows = false;
            tr.enabled = false;

            // 왼손 (작은 컨트롤러 모양)
            var left = new GameObject("LeftHand").transform;
            left.SetParent(offset, false);
            left.localPosition = new Vector3(-0.2f, 1.2f, 0.3f);
            Prim(PrimitiveType.Cube, "Grip", left, new Vector3(0f, -0.02f, -0.02f), new Vector3(0.045f, 0.1f, 0.06f), Quaternion.Euler(-20f, 0f, 0f), "Rubber");
            Prim(PrimitiveType.Cylinder, "Ring", left, new Vector3(0f, 0.035f, 0.02f), new Vector3(0.09f, 0.008f, 0.09f), Quaternion.Euler(40f, 0f, 0f), "DarkMetal");

            // 오른손 = 손전등 (Devour 처럼 항상 들고 다님)
            var right = new GameObject("RightHand").transform;
            right.SetParent(offset, false);
            right.localPosition = new Vector3(0.2f, 1.2f, 0.3f);
            Prim(PrimitiveType.Cylinder, "FlashlightBody", right, new Vector3(0f, 0f, 0.03f), new Vector3(0.035f, 0.09f, 0.035f), Quaternion.Euler(90f, 0f, 0f), "Rubber");
            Prim(PrimitiveType.Cylinder, "FlashlightHead", right, new Vector3(0f, 0f, 0.13f), new Vector3(0.05f, 0.025f, 0.05f), Quaternion.Euler(90f, 0f, 0f), "DarkMetal");
            var lens = Prim(PrimitiveType.Cylinder, "Lens", right, new Vector3(0f, 0f, 0.157f), new Vector3(0.042f, 0.003f, 0.042f), Quaternion.Euler(90f, 0f, 0f), "LampWarm");
            var lightGo = new GameObject("FlashlightBeam");
            lightGo.transform.SetParent(right, false);
            lightGo.transform.localPosition = new Vector3(0f, 0f, 0.17f);
            var spot = lightGo.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.color = new Color(1f, 0.94f, 0.82f);
            spot.intensity = 3.2f;
            spot.range = 24f;
            spot.spotAngle = 55f;
            spot.innerSpotAngle = 22f;
            spot.shadows = LightShadows.Soft;
            spot.shadowStrength = 0.85f;
            spot.shadowNearPlane = 0.1f;

            var vr = rig.AddComponent<BJ_VRRig>();
            vr.cameraOffset = offset;
            vr.head = cam;
            vr.leftHand = left;
            vr.rightHand = right;
            vr.flashlight = spot;
            vr.flashlightLens = lens.GetComponent<MeshRenderer>();
            vr.tunnelVignette = tr;

            // 'Player' 태그로 플레이어를 찾는 스크립트(트리거 · 순찰 AI · ai_wander)가 VR 플레이어도 찾도록
            rig.tag = "Player";
            // AI 가 쫓는 발밑 (룸스케일로 걸어도 BJ_VRRig 가 머리 아래로 옮긴다)
            var feetT = new GameObject("Feet").transform;
            feetT.SetParent(rig.transform, false);
            vr.feet = feetT;

            // 발소리 (몸 모델이 없어 걸은 거리로 걸음을 센다. 내 발소리라 2D)
            var stepSource = rig.AddComponent<AudioSource>();
            stepSource.playOnAwake = false;
            stepSource.spatialBlend = 0f;
            var steps = rig.AddComponent<BJ_VRFootsteps>();
            var clips = new System.Collections.Generic.List<AudioClip>();
            for (int i = 1; i <= 4; i++)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(FootstepDir + "Footstep0" + i + ".wav");
                if (clip != null) clips.Add(clip);
            }
            steps.clips = clips.ToArray();

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[병점역] VR 플레이어 추가 완료: 역앞광장(1번 출구 앞)에서 시작합니다. (" + (urp ? "URP" : "Built-in") + ")");
        }

        /// <summary>URP 후처리 중 VR 에서 멀미/어색함을 주는 효과(필름그레인·색수차·비네트)를 끄거나 켠다.</summary>
        static void SetVRComfortPostFX(bool vr)
        {
            var profile = AssetDatabase.LoadAssetAtPath<ScriptableObject>(BJ.GenDir + "/BJ_PostProcess.asset");
            if (profile == null) return;
            var compsField = profile.GetType().GetField("components", BindingFlags.Public | BindingFlags.Instance);
            var comps = compsField != null ? compsField.GetValue(profile) as IEnumerable : null;
            if (comps == null) return;
            foreach (var c in comps)
            {
                if (c == null) continue;
                if (System.Array.IndexOf(VRUncomfortableEffects, c.GetType().Name) < 0) continue;
                var active = c.GetType().GetField("active", BindingFlags.Public | BindingFlags.Instance);
                if (active != null) active.SetValue(c, !vr);
                if (c is Object o) EditorUtility.SetDirty(o);
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        static void SetProp(object target, string name, object value)
        {
            var p = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (p != null && p.CanWrite) { p.SetValue(target, value); return; }
            var f = target.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (f != null) f.SetValue(target, value);
        }
    }
}
