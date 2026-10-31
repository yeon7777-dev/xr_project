using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Byeongjeom.MapGen
{
    public static partial class BJ
    {
        class FloorProbe { public Vector3 from; public float height; }
        class PassageProbe { public string name; public Vector3 a, b; public bool clear; }
        class PreservedCollider { public Collider collider; public string json; }

        [MenuItem("Tools/병점역 맵/내부 중심 콜라이더 적용 및 검증 저장 %#F8", false, 23)]
        public static void ApplyAndValidateStationColliders()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var root = GameObject.Find(StationColliderRoot);
            if (root == null || root.scene.path != ScenePath)
                throw new InvalidOperationException("Open the ByeongjeomStation scene before applying colliders.");
            Directory.CreateDirectory("Backups/ColliderOptimization_20261019");
            string backup = "Backups/ColliderOptimization_20261019/Before-apply-" + DateTime.Now.ToString("HHmmss") + ".unity";
            if (!EditorSceneManager.SaveScene(root.scene, backup, true))
                throw new IOException("Could not back up the open scene.");
            WriteColliderAudit("Logs/Colliders-before-apply.json");
            Physics.SyncTransforms();
            var floors = CaptureFloorProbes(root.transform);
            var passages = CapturePassages(root.transform);
            var protectedColliders = new List<PreservedCollider>();
            foreach (var c in root.GetComponentsInChildren<Collider>(true))
                if (!ColEligible(c)) protectedColliders.Add(new PreservedCollider { collider = c, json = EditorJsonUtility.ToJson(c) });
            string visuals = VisualSignature(root);
            int undoGroup = Undo.GetCurrentGroup() + 1;
            try
            {
                var report = OptimizeColliders(new[] { root }, true);
                Physics.SyncTransforms();
                var checks = new List<string>();
                ValidateFloors(root.transform, floors);
                checks.Add("바닥 지지점 " + floors.Count + "곳 유지");
                int clear = 0;
                foreach (var p in passages)
                {
                    if (!p.clear) continue;
                    Require(PassageClear(root.transform, p.a, p.b), "Passage closed: " + p.name + " by " + passageBlocker);
                    clear++;
                }
                checks.Add("플레이어 크기 통로 " + clear + "곳 유지 (반지름 0.3m, 높이 2m)");
                int stairs = ValidateStairSupport(root.transform);
                checks.Add("계단 " + stairs + "곳: 경사/참/상하부 지지면 연속성 확인");
                foreach (var item in protectedColliders)
                    Require(item.collider != null && EditorJsonUtility.ToJson(item.collider) == item.json, "Protected collider changed.");
                checks.Add("비활성/트리거/동적/스크립트 보호 대상 " + protectedColliders.Count + "개 유지");
                Require(VisualSignature(root) == visuals, "Render meshes or transforms changed.");
                checks.Add("렌더링 메쉬·머티리얼·위치 유지");
                string once = ColliderSignature(root);
                var repeat = OptimizeColliders(new[] { root }, true);
                Require(ColliderSignature(root) == once && repeat.Before == repeat.After, "Optimizer is not idempotent.");
                checks.Add("재실행 시 콜라이더 수·형상 동일");
                ValidateColliderSafetyFixtures();
                checks.Add("트리거/비활성/Rigidbody/레이어/물리 재질/통로 보존 회귀 검증 통과");

                BakeNavMesh(root);
                var navData = SceneNavMesh();
                Require(navData != null && NavMesh.CalculateTriangulation().indices.Length > 0,
                    "NavMesh bake produced no navigation data.");
                checks.Add("PhysicsColliders 기준 NavMesh 재생성 완료");
                EditorUtility.SetDirty(navData);
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(root.scene);
                Require(EditorSceneManager.SaveScene(root.scene), "Scene save failed.");
                WriteColliderAudit("Logs/Colliders-after.json");
                var text = new StringBuilder("# 병점역 콜라이더 최적화\n\n");
                text.AppendLine("적용 씬: `" + ScenePath + "`\n");
                text.AppendLine("```\n" + report + "\n```\n");
                text.AppendLine("## 적용 기준\n");
                text.AppendLine("- 내부: 대합실, 화장실/역무실/휴게실/편의점, 승강장, 대기실, 열차. 책상·의자·벤치·선반·열차 좌석과 봉·난간 등의 실물 충돌 보강.");
                text.AppendLine("- 출입 동선: 출구 1/2/3, 계단·에스컬레이터·승강장 연결부는 내부와 같은 보존 기준. 계단은 경사로, 구멍 난 슬래브는 분할 박스.");
                text.AppendLine("- 외부: 차량·화단·벤치의 단순 박스, 외부 중복 충돌 정리, 접근 불가한 주차타워 상층 충돌 제외. 주차장 1층과 버스정류장 내부는 개방 유지.");
                text.AppendLine("- 종이/데칼/얼룩/얇은 장식과 작은 선반 상품에는 충돌을 추가하지 않음. 엘리베이터는 기존의 막힌 샤프트 유지.\n");
                text.AppendLine("## 검증\n");
                foreach (var check in checks) text.AppendLine("- " + check);
                text.AppendLine("\n검증은 편집기 물리 쿼리와 회귀 검사 기준입니다. 실제 추격 플레이, 이동형 문, 멀티플레이 및 프레임 시간 프로파일링은 별도 확인이 필요합니다.");
                text.AppendLine("\n## 재적용 및 복구\n");
                text.AppendLine("- 메뉴: `Tools > 병점역 맵 > 내부 중심 콜라이더 적용 및 검증 저장` (Ctrl+Shift+F8).");
                text.AppendLine("- 맵을 새로 생성해도 같은 내부/외부 정책이 자동 적용됩니다.");
                text.AppendLine("- 적용 직전 현재 씬 백업: `" + backup + "`.");
                text.AppendLine("- 최초 디스크 씬/원본 코드/NavMesh: `Backups/ColliderOptimization_20261019/`.");
                text.AppendLine("- 씬 백업을 복구한 경우 기존 메뉴의 `NavMesh 다시 굽기`도 실행하세요.");
                Directory.CreateDirectory("Reports");
                File.WriteAllText("Reports/ColliderOptimization.md", text.ToString(), new UTF8Encoding(false));
                Debug.Log("[병점역 콜라이더 적용 완료]\n" + report + "\n" + string.Join("\n", checks));
            }
            catch (Exception e)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                Debug.LogError("[병점역] 콜라이더 검증 실패. 씬 충돌 변경을 되돌렸습니다. " + e);
                throw;
            }
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        static bool StationRay(Transform root, Vector3 from, float distance, out RaycastHit best)
        {
            best = new RaycastHit();
            bool found = false;
            foreach (var hit in Physics.RaycastAll(from, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider.transform.IsChildOf(root) && (!found || hit.distance < best.distance)) { best = hit; found = true; }
            return found;
        }

        static List<FloorProbe> CaptureFloorProbes(Transform root)
        {
            var probes = new List<FloorProbe>();
            for (float x = CX0 + 0.5f; x < CX1; x += 1f)
                for (float z = CZ0 + 0.5f; z < CZ1; z += 1f)
                    AddFloorProbe(root, new Vector3(x, CF + 0.15f, z), probes);
            foreach (float x in IslandX)
                for (float dx = -4.5f; dx <= 4.5f; dx += 1f)
                    for (float z = PZ0 + 0.5f; z < PZ1; z += 2f)
                        AddFloorProbe(root, new Vector3(x + dx, PF + 0.15f, z), probes);
            for (float x = -90f; x < 116f; x += 4f)
                for (float z = -60f; z < 62f; z += 4f)
                    if (Mathf.Abs(x) > 26f) AddFloorProbe(root, new Vector3(x, GY + 0.15f, z), probes);
            return probes;
        }

        static void AddFloorProbe(Transform root, Vector3 local, List<FloorProbe> probes)
        {
            Vector3 p = root.TransformPoint(local);
            RaycastHit hit;
            if (StationRay(root, p, 0.4f, out hit)) probes.Add(new FloorProbe { from = p, height = hit.point.y });
        }

        static void ValidateFloors(Transform root, List<FloorProbe> probes)
        {
            foreach (var p in probes)
            {
                RaycastHit hit;
                // New low furniture bases can intentionally be above the old floor. Never allow lost/lowered support.
                Require(StationRay(root, p.from, 0.4f, out hit) && hit.point.y >= p.height - 0.025f,
                    "Floor support changed at " + p.from);
            }
        }

        static List<PassageProbe> CapturePassages(Transform root)
        {
            var probes = new List<PassageProbe>();
            foreach (float x in new[] { -17.5f, 16.5f, -35.9f, -26.9f, 35.2f })
                probes.Add(new PassageProbe { name = "Gate/room " + x, a = new Vector3(x, CF, -9f), b = new Vector3(x, CF, -7.4f) });
            for (int lane = 0; lane < 8; lane++)
            {
                float x = -6.5f + (lane + 0.5f) * 13f / 8f;
                probes.Add(new PassageProbe { name = "Fare gate " + lane, a = new Vector3(x, CF, -9.2f), b = new Vector3(x, CF, -6.8f) });
            }
            probes.Add(new PassageProbe { name = "Machine room", a = new Vector3(-23f, CF, 9.6f), b = new Vector3(-25f, CF, 9.6f) });
            probes.Add(new PassageProbe { name = "Office", a = new Vector3(23f, CF, -2.4f), b = new Vector3(25f, CF, -2.4f) });
            probes.Add(new PassageProbe { name = "Lounge", a = new Vector3(30.6f, CF, 3f), b = new Vector3(30.6f, CF, 5f) });
            foreach (float x in new[] { -38f, 38f })
                // Probe the centers of the actual door bays, not the newly collidable metal mullions at odd z.
                // The southern bay (-16) contains authored trash bags; the two central bays are the walking route.
                foreach (float z in new[] { -14f, -12f })
                    probes.Add(new PassageProbe { name = "Station exit " + x + "/" + z, a = new Vector3(x - 1f, CF, z), b = new Vector3(x + 1f, CF, z) });
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "WaitingRoom")
                    probes.Add(new PassageProbe { name = "Waiting room", a = root.InverseTransformPoint(t.TransformPoint(new Vector3(0.8f, 0.1f, 0f))), b = root.InverseTransformPoint(t.TransformPoint(new Vector3(2.4f, 0.1f, 0f))) });
                if (t.name != "GapPlate") continue;
                Vector3 p = root.InverseTransformPoint(t.position);
                p.y = TFloor;
                probes.Add(new PassageProbe { name = "Train door", a = p + Vector3.left * 0.8f, b = p + Vector3.right * 0.8f });
            }
            foreach (var p in probes) { p.a = root.TransformPoint(p.a); p.b = root.TransformPoint(p.b); p.clear = PassageClear(root, p.a, p.b); }
            return probes;
        }

        static string passageBlocker;
        static bool PassageClear(Transform root, Vector3 a, Vector3 b)
        {
            // Slight clearance above the floor; sample capsule occupancy including both endpoints.
            int steps = Mathf.CeilToInt(Vector3.Distance(a, b) / 0.1f);
            for (int i = 0; i <= steps; i++)
            {
                Vector3 feet = Vector3.Lerp(a, b, steps == 0 ? 0f : (float)i / steps);
                foreach (var c in Physics.OverlapCapsule(feet + Vector3.up * 0.34f, feet + Vector3.up * 1.74f,
                    0.3f, ~0, QueryTriggerInteraction.Ignore))
                    if (c.transform.IsChildOf(root))
                    {
                        passageBlocker = ColliderObjectPath(c.transform) + " at " + feet + " bounds=" + c.bounds;
                        return false;
                    }
            }
            return true;
        }

        static int ValidateStairSupport(Transform root)
        {
            int count = 0;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.name != "Steps" || mf.sharedMesh == null || mf.sharedMesh.name != "Steps") continue;
                var bounds = mf.sharedMesh.bounds;
                int ramps;
                var original = DecomposeBoxMesh(mf.sharedMesh);
                Require(original != null, "Unknown stair mesh.");
                var surfaces = StairStepsToRamps(original, out ramps);
                float previous = float.NaN;
                for (float z = 0.01f; z < bounds.max.z; z += 0.1f)
                {
                    float expected = LocalSurfaceHeight(surfaces, z, bounds.max.y + 1f);
                    if (float.IsNegativeInfinity(expected)) continue;
                    // Start below the overhead concourse slab while following the descending stair.
                    var from = mf.transform.TransformPoint(new Vector3(0f, expected + 0.15f, z));
                    RaycastHit hit;
                    Require(StationRay(root, from, 0.4f, out hit), "Missing stair support: " + ColliderObjectPath(mf.transform) + " z=" + z);
                    if (!float.IsNaN(previous)) Require(Mathf.Abs(hit.point.y - previous) < 0.25f, "Stair discontinuity: " + ColliderObjectPath(mf.transform) + " z=" + z);
                    Require(Vector3.Angle(hit.normal, Vector3.up) < 46f, "Stair too steep.");
                    previous = hit.point.y;
                }
                count++;
            }
            return count;
        }

        static float LocalSurfaceHeight(List<LocalBox> boxes, float z, float fromY)
        {
            float height = float.NegativeInfinity;
            foreach (var box in boxes)
            {
                var inverse = Quaternion.Inverse(box.rot);
                Vector3 o = inverse * (new Vector3(0f, fromY, z) - box.c);
                Vector3 d = inverse * Vector3.down, h = box.size * 0.5f;
                float enter = 0f, leave = float.PositiveInfinity;
                bool hit = true;
                for (int i = 0; i < 3; i++)
                {
                    if (Mathf.Abs(d[i]) < 1e-6f) { if (Mathf.Abs(o[i]) > h[i]) hit = false; continue; }
                    float a = (-h[i] - o[i]) / d[i], b = (h[i] - o[i]) / d[i];
                    enter = Mathf.Max(enter, Mathf.Min(a, b));
                    leave = Mathf.Min(leave, Mathf.Max(a, b));
                }
                if (hit && enter <= leave) height = Mathf.Max(height, fromY - enter);
            }
            return height;
        }

        static string VisualSignature(GameObject root)
        {
            var b = new StringBuilder();
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                b.Append(r.GetInstanceID()).Append(r.transform.localToWorldMatrix.ToString("R"));
                var mf = r.GetComponent<MeshFilter>();
                b.Append(mf != null && mf.sharedMesh != null ? mf.sharedMesh.GetInstanceID() : 0);
                foreach (var m in r.sharedMaterials) b.Append(m != null ? m.GetInstanceID() : 0).Append(',');
            }
            return b.ToString();
        }

        static string ColliderSignature(GameObject root)
        {
            var b = new StringBuilder();
            foreach (var c in root.GetComponentsInChildren<Collider>(true)) b.Append(EditorJsonUtility.ToJson(c));
            return b.ToString();
        }

        static void ValidateColliderSafetyFixtures()
        {
            var root = new GameObject(StationColliderRoot);
            var material = new PhysicMaterial("Collider validation");
            root.transform.position = Vector3.one * 10000f;
            try
            {
                var region = new GameObject("East_역앞광장");
                region.transform.SetParent(root.transform, false);
                var boxes = new List<BoxCollider>();
                for (int i = 0; i < 8; i++)
                {
                    var go = new GameObject("Safety_" + i);
                    go.transform.SetParent(region.transform, false);
                    GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
                    boxes.Add(go.AddComponent<BoxCollider>());
                }
                boxes[0].isTrigger = true;
                boxes[1].enabled = false;
                boxes[2].gameObject.AddComponent<Rigidbody>().isKinematic = true;
                boxes[3].gameObject.layer = 2;
                boxes[4].sharedMaterial = material;
                boxes[5].size = Vector3.one * 2f; // Contains 0..4, but must not remove them.
                boxes[6].transform.localPosition = new Vector3(10f, 0f, 0f);
                boxes[7].transform.localPosition = new Vector3(12f, 0f, 0f); // 1m doorway, not mergeable.
                var report = OptimizeColliders(new[] { root }, false);
                foreach (var box in boxes) Require(box != null, "Protected fixture collider was removed.");
                Require(boxes[6].size == Vector3.one && boxes[7].size == Vector3.one, "Door gap was filled.");
                Require(report.Before == report.After, "Safety fixture changed collider count.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(material); }
        }
    }
}
