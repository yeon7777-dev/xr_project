using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Byeongjeom.MapGen
{
    /// <summary>
    /// 3인칭 플레이어 (Standard Assets ThirdPersonController + FreeLookCameraRig).
    /// 메뉴: Tools > 병점역 맵 > 3인칭 플레이어 (ThirdPersonController)로 교체
    ///  - 씬에 있는 FPSController · 이전 3인칭 플레이어와 그것을 따라가던 카메라 리그를 지우고
    ///    같은 자리에 ThirdPersonController 와 그것을 따라가는 FreeLookCameraRig 를 놓는다. (플레이어가 없으면 역앞광장에서 시작)
    ///  - '맵 생성' 때도 VR 모드가 아니면 자동으로 들어간다.
    /// </summary>
    public static class BJ_ThirdPersonPlayer
    {
        const string PlayerPrefabPath = "Assets/Standard Assets/Characters/ThirdPersonCharacter/Prefabs/ThirdPersonController.prefab";
        const string RigPrefabPath = "Assets/Standard Assets/Cameras/Prefabs/FreeLookCameraRig.prefab";
        const string RootName = "ByeongjeomStation_병점역";
        const float PivotHeight = 1.6f;     // 프리팹 기본 2m · 4m 뒤는 대합실 · 전동차 천장에 자꾸 걸린다
        const float CameraDistance = 3f;
        const float FollowSpeed = 8f;       // 프리팹 기본 1 은 달릴 때 카메라가 몇 m 씩 뒤처진다

        [MenuItem("Tools/병점역 맵/3인칭 플레이어 (ThirdPersonController)로 교체", false, 71)]
        static void ReplaceMenu()
        {
            var root = GameObject.Find(RootName);
            if (root == null)
            {
                EditorUtility.DisplayDialog("병점역 3인칭", "먼저 'Tools > 병점역 맵 > 맵 생성'으로 병점역 씬을 만들거나 열어 주세요.", "확인");
                return;
            }
            if (BJ_VRSetup.VRModeEnabled)
            {
                EditorUtility.DisplayDialog("병점역 3인칭", "VR 모드가 켜져 있습니다.\n'Tools > 병점역 맵 > VR (메타 퀘스트) > VR 모드 끄기'를 먼저 실행해 주세요.", "확인");
                return;
            }
            if (AddThirdPersonPlayer(root.scene) == null) return;
            EditorSceneManager.MarkSceneDirty(root.scene);
            EditorSceneManager.SaveScene(root.scene);
        }

        /// <summary>
        /// 현재 씬의 플레이어 · 카메라 리그를 지우고 ThirdPersonController + FreeLookCameraRig 를 놓는다.
        /// 기존 플레이어가 있으면 그 자리 · 방향을 이어받는다.
        /// </summary>
        public static GameObject AddThirdPersonPlayer(Scene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefabPath);
            if (prefab == null || rigPrefab == null)
            {
                Debug.LogWarning("[병점역] 3인칭 프리팹을 찾을 수 없습니다: " + (prefab == null ? PlayerPrefabPath : RigPrefabPath));
                return null;
            }

            Vector3 feet = BJ_VRSetup.SpawnPos;
            float yaw = BJ_VRSetup.SpawnYaw;
            bool inherited;
            int removedCount = BJ_FPSPlayer.RemovePlayers(scene, ref feet, ref yaw, out inherited);
            feet = BJ_FPSPlayer.Ground(feet);

            // ThirdPersonController 배치 (발 위치가 원점, 캡슐 반지름 0.3 이라 개찰구도 지나간다)
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            player.name = "ThirdPersonController";
            player.tag = "Player";
            player.transform.position = feet + Vector3.up * 0.02f;
            player.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            // 플레이어를 따라가는 카메라 리그 (리그 → Pivot → Main Camera)
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab, scene);
            rig.name = "FreeLookCameraRig";
            rig.transform.position = player.transform.position;
            rig.transform.rotation = player.transform.rotation;
            var freeLookT = BJ.FindType("UnityStandardAssets.Cameras.FreeLookCam");
            var freeLook = freeLookT != null ? rig.GetComponent(freeLookT) : null;
            if (freeLook != null)
            {
                var so = new SerializedObject(freeLook);
                so.FindProperty("m_Target").objectReferenceValue = player.transform;
                so.FindProperty("m_MoveSpeed").floatValue = FollowSpeed;
                so.FindProperty("m_LockCursor").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var cam = rig.GetComponentInChildren<Camera>(true);
            if (cam != null)
            {
                cam.transform.parent.localPosition = new Vector3(0f, PivotHeight, 0f);
                cam.transform.localPosition = new Vector3(0f, 0f, -CameraDistance);
                BJ_FPSPlayer.SetupCamera(cam);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log(string.Format("[병점역] 3인칭 플레이어(ThirdPersonController) 배치: {0} (지운 오브젝트 {1}개)",
                inherited ? "기존 플레이어 자리" : "역앞광장(1번 출구 앞)", removedCount));
            return player;
        }
    }
}
