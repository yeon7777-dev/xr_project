using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Byeongjeom;

namespace Byeongjeom.MapGen
{
    /// <summary>
    /// 1인칭 플레이어 (Standard Assets FPSController).
    /// 메뉴: Tools > 병점역 맵 > 1인칭 플레이어 (FPSController)로 교체
    ///  - 씬에 있는 ThirdPersonController 와 그것을 따라가던 카메라 리그(Multipurpose / FreeLook)를 지우고
    ///    같은 자리에 FPSController 를 놓는다. (ThirdPersonController 가 없으면 역앞광장에서 시작)
    ///  - '맵 생성' 때도 VR 모드가 아니면 자동으로 들어간다.
    /// </summary>
    public static class BJ_FPSPlayer
    {
        const string PrefabPath = "Assets/Standard Assets/Characters/FirstPersonCharacter/Prefabs/FPSController.prefab";
        const string RootName = "ByeongjeomStation_병점역";
        const float Radius = 0.3f;   // 프리팹 기본 0.5 는 개찰구 · 좁은 통로를 못 지나간다

        [MenuItem("Tools/병점역 맵/1인칭 플레이어 (FPSController)로 교체", false, 70)]
        static void ReplaceMenu()
        {
            var root = GameObject.Find(RootName);
            if (root == null)
            {
                EditorUtility.DisplayDialog("병점역 1인칭", "먼저 'Tools > 병점역 맵 > 맵 생성'으로 병점역 씬을 만들거나 열어 주세요.", "확인");
                return;
            }
            if (BJ_VRSetup.VRModeEnabled)
            {
                EditorUtility.DisplayDialog("병점역 1인칭", "VR 모드가 켜져 있습니다.\n'Tools > 병점역 맵 > VR (메타 퀘스트) > VR 모드 끄기'를 먼저 실행해 주세요.", "확인");
                return;
            }
            if (AddFPSPlayer(root.scene) == null) return;
            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveScene(root.scene);
        }

        /// <summary>
        /// 현재 씬의 3인칭 플레이어 · 카메라 리그 · 이전 FPSController 를 지우고 FPSController 를 놓는다.
        /// 3인칭 플레이어가 있으면 그 자리 · 방향을 이어받는다.
        /// </summary>
        public static GameObject AddFPSPlayer(Scene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[병점역] FPSController 프리팹을 찾을 수 없습니다: " + PrefabPath);
                return null;
            }

            Vector3 feet = BJ_VRSetup.SpawnPos;
            float yaw = BJ_VRSetup.SpawnYaw;
            bool inherited;
            int removedCount = RemovePlayers(scene, ref feet, ref yaw, out inherited);
            feet = Ground(feet);

            // FPSController 배치
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            player.name = "FPSController";
            player.tag = "Player";
            var ctrl = player.GetComponent<CharacterController>();
            ctrl.radius = Radius;
            player.transform.position = feet + Vector3.up * (ctrl.height * 0.5f - ctrl.center.y + ctrl.skinWidth + 0.02f);
            player.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            var cam = player.GetComponentInChildren<Camera>(true);
            if (cam != null) SetupCamera(cam);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(string.Format("[병점역] 1인칭 플레이어(FPSController) 배치: {0} (지운 오브젝트 {1}개)",
                inherited ? "기존 플레이어 자리" : "역앞광장(1번 출구 앞)", removedCount));
            return player;
        }

        /// <summary>
        /// 씬의 플레이어(3인칭 ThirdPersonController · 1인칭 FPSController)와 그것을 따라가던 카메라 리그를 지우고
        /// 미리보기 카메라를 끈다. 플레이어가 있으면 그 발 위치 · 방향을 feet · yaw 로 돌려준다. 반환값은 지운 오브젝트 수.
        /// </summary>
        internal static int RemovePlayers(Scene scene, ref Vector3 feet, ref float yaw, out bool inherited)
        {
            inherited = false;
            var removed = new HashSet<Transform>();
            var toDestroy = new List<GameObject>();

            // 3인칭 플레이어 (ThirdPersonController) 와 1인칭 플레이어 (FPSController)
            foreach (string typeName in new[] {
                         "UnityStandardAssets.Characters.ThirdPerson.ThirdPersonUserControl",
                         "UnityStandardAssets.Characters.FirstPerson.FirstPersonController" })
            {
                var t = BJ.FindType(typeName);
                if (t == null) continue;
                foreach (Component c in Object.FindObjectsOfType(t, true))
                {
                    if (c.gameObject.scene != scene) continue;
                    var go = PrefabRoot(c.gameObject);
                    if (!inherited)
                    {
                        // 3인칭은 발 위치가 원점, 1인칭은 캡슐 중심이 원점
                        var cc = go.GetComponent<CharacterController>();
                        feet = cc != null ? go.transform.TransformPoint(cc.center) - Vector3.up * (cc.height * 0.5f) : go.transform.position;
                        yaw = go.transform.eulerAngles.y;
                        inherited = true;
                    }
                    foreach (Transform tr in go.GetComponentsInChildren<Transform>(true)) removed.Add(tr);
                    toDestroy.Add(go);
                }
            }

            // 플레이어를 따라가던 카메라 리그 (대상이 비어 있으면 Play 때 새 플레이어를 자동으로 잡아 시점을 빼앗는다)
            var followerT = BJ.FindType("UnityStandardAssets.Cameras.AbstractTargetFollower");
            if (followerT != null)
            {
                foreach (Component c in Object.FindObjectsOfType(followerT, true))
                {
                    if (c.gameObject.scene != scene) continue;
                    var target = new SerializedObject(c).FindProperty("m_Target");
                    var tr = target != null ? target.objectReferenceValue as Transform : null;
                    if (tr == null || removed.Contains(tr)) toDestroy.Add(PrefabRoot(c.gameObject));
                }
            }

            foreach (var go in toDestroy)
                if (go != null) Object.DestroyImmediate(go);

            var root = GameObject.Find(RootName);
            if (root != null)
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith("PreviewCamera")) t.gameObject.SetActive(false);
            return toDestroy.Count;
        }

        /// <summary>발 위치를 바닥에 붙인다 (다시 실행해도 높이가 쌓이지 않도록).</summary>
        internal static Vector3 Ground(Vector3 feet)
        {
            Physics.SyncTransforms();
            RaycastHit hit;
            if (Physics.Raycast(feet + Vector3.up * 1f, Vector3.down, out hit, 3f, ~0, QueryTriggerInteraction.Ignore))
                feet = hit.point;
            return feet;
        }

        internal static GameObject PrefabRoot(GameObject go)
        {
            var r = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
            return r != null ? r : go;
        }

        /// <summary>미리보기 카메라와 같은 설정 (안개색 배경 · 디퍼드 + 안개 이펙트 / URP 후처리).</summary>
        internal static void SetupCamera(Camera cam)
        {
            var camGo = cam.gameObject;
            camGo.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 260f;
            cam.fieldOfView = 70f;
            cam.allowHDR = true;
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                var camDataT = BJ.FindType("UnityEngine.Rendering.Universal.UniversalAdditionalCameraData");
                if (camDataT != null)
                {
                    var data = camGo.GetComponent(camDataT);
                    if (data == null) data = camGo.AddComponent(camDataT);
                    var p = camDataT.GetProperty("renderPostProcessing");
                    if (p != null) p.SetValue(data, true);
                }
            }
            else
            {
                cam.renderingPath = RenderingPath.DeferredShading;
                var fog = camGo.GetComponent<BJ_DeferredFog>();
                if (fog == null) fog = camGo.AddComponent<BJ_DeferredFog>();
                fog.fogShader = Shader.Find("Hidden/BJ_DeferredFog");
            }
        }
    }
}
