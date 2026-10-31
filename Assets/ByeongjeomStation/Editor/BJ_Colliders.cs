using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Byeongjeom.MapGen
{
    /// <summary>
    /// 콜라이더 최적화. 메뉴: Tools > 병점역 맵 > 콜라이더 최적화
    /// 맵 생성 때도 NavMesh 를 굽기 전에 자동으로 실행된다.
    /// 병점역 생성 계층만 대상으로 내부/출입 동선과 외부를 구분한다.
    /// 내부는 누락된 실물 충돌을 보강하고 계단만 경사로로 변환한다.
    /// 외부에만 아래의 단순화 규칙을 적용한다. 트리거/동적/게임플레이 오브젝트는 보존한다.
    ///  1. 박스를 이어 붙인 MeshCollider(슬래브 · 계단) → BoxCollider 로 분해, 계단 단들은 경사로 하나로
    ///  2. 다른 박스 안에 완전히 들어간 콜라이더 제거
    ///  3. 바닥에 깔린 얇은 잔해(쓰러진 문짝 등) 콜라이더 제거 - CharacterController 가 그냥 넘어간다
    ///  4. 작은 소품(벤치 등)의 콜라이더 여러 개 → 감싸는 박스 하나
    ///  5. 같은 부모 아래에서 면이 맞닿은 박스들을 하나로 (스크린도어 막이 · 승강장 바닥층 등)
    /// </summary>
    public static partial class BJ
    {
        const string ColUndoName = "콜라이더 최적화";
        const float ColMergeTol = 0.08f;     // 합칠 때 허용하는 면 어긋남 (m). 플레이어 반지름 0.25 에 비해 무시할 수준
        const float ColMergeGap = 0.02f;     // 이 이하의 틈은 붙은 것으로 본다
        const float ColContainTol = 0.01f;
        const float ThinMaxHeight = 0.06f, ThinMaxArea = 3f, ThinSupportGap = 0.05f;
        const float PropMaxMinDim = 0.6f, PropMaxDim = 3f, PropMaxVolume = 2f;
        const float RampMaxAngle = 45f;

        public class ColliderReport
        {
            public int boxBefore, capsuleBefore, sphereBefore, meshBefore;
            public int boxAfter, capsuleAfter, sphereAfter, meshAfter;
            public int meshReplaced, ramps, contained, thin, props, merged;
            public int interiorAdded, interiorProxiesRemoved, exteriorProxies, exteriorRemoved;
            public int interiorBefore, interiorAfter, exteriorBefore, exteriorAfter;
            public int Before { get { return boxBefore + capsuleBefore + sphereBefore + meshBefore; } }
            public int After { get { return boxAfter + capsuleAfter + sphereAfter + meshAfter; } }

            public override string ToString()
            {
                return string.Format(
                    "콜라이더 {0}개 → {1}개\n  Box {2}→{3}, Capsule {4}→{5}, Sphere {6}→{7}, Mesh {8}→{9}\n" +
                    "  MeshCollider→박스 {10}개 (계단 경사로 {11}개), 안에 들어간 것 제거 {12}, 바닥 잔해 제거 {13}, 소품 묶음 {14}, 맞닿은 박스 합침 {15}",
                    Before, After, boxBefore, boxAfter, capsuleBefore, capsuleAfter, sphereBefore, sphereAfter, meshBefore, meshAfter,
                    meshReplaced, ramps, contained, thin, props, merged)
                    + string.Format("\n  내부/출입 동선 {0}→{1}, 외부 {2}→{3}\n  내부 보강 {4}, 내부 통합박스 해제 {5}, 외부 단순박스 {6}, 외부 제거 {7}",
                        interiorBefore, interiorAfter, exteriorBefore, exteriorAfter,
                        interiorAdded, interiorProxiesRemoved, exteriorProxies, exteriorRemoved);
            }
        }

        [MenuItem("Tools/병점역 맵/콜라이더 최적화", false, 21)]
        static void OptimizeCollidersMenu()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorUtility.DisplayDialog("콜라이더 최적화",
                    "현재 씬(" + scene.name + ")의 병점역 콜라이더를 정리합니다.\n\n" +
                    "· 내부/열차/출입 동선: 세부 충돌 보강, 계단 경사로 유지\n" +
                    "· 외부: 차량/화단/벤치 단순화, 중복 충돌 정리\n\n" +
                    "Ctrl+Z 로 되돌릴 수 있습니다.", "최적화", "취소"))
                return;
            ColliderReport rep;
            try
            {
                EditorUtility.DisplayProgressBar("콜라이더 최적화", "분석 중...", 0.5f);
                rep = OptimizeColliders(scene.GetRootGameObjects(), true);
            }
            finally { EditorUtility.ClearProgressBar(); }
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[병점역] " + rep);

            var root = GameObject.Find("ByeongjeomStation_병점역");
            if (root != null && EditorUtility.DisplayDialog("콜라이더 최적화 완료", rep + "\n\nNavMesh 는 콜라이더로 굽기 때문에 다시 굽는 것을 권장합니다.",
                    "NavMesh 다시 굽고 저장", "나중에"))
                RebakeMenu();
        }

        /// <summary>roots 아래 콜라이더를 최적화한다. undo = true 면 Ctrl+Z 로 되돌릴 수 있다.</summary>
        public static ColliderReport OptimizeColliders(IList<GameObject> roots, bool undo)
        {
            var detailed = new List<GameObject>();
            var exterior = new List<GameObject>();
            CollectColliderRegions(roots, detailed, exterior);
            var targets = new List<GameObject>(detailed);
            targets.AddRange(exterior);
            if (undo)
            {
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName(ColUndoName);
            }
            int group = Undo.GetCurrentGroup();
            var rep = new ColliderReport();
            CountColliders(targets, out rep.boxBefore, out rep.capsuleBefore, out rep.sphereBefore, out rep.meshBefore);
            rep.interiorBefore = ColliderTotal(detailed);
            rep.exteriorBefore = ColliderTotal(exterior);

            RepairInteriorColliders(detailed, rep, undo);
            ReplaceBoxMeshColliders(targets, rep, undo);
            SimplifyExteriorColliders(exterior, rep, undo);
            rep.contained += RemoveContained(exterior, undo);
            rep.thin = RemoveThinClutter(exterior, undo);
            rep.props = CompactSmallProps(exterior, undo);
            // 합친 박스가 다른 박스를 덮거나 새로 맞닿을 수 있으므로 더 줄지 않을 때까지 반복
            for (int pass = 0; pass < 4; pass++)
            {
                int m = MergeSiblingBoxes(exterior, undo), c = RemoveContained(exterior, undo);
                rep.merged += m;
                rep.contained += c;
                if (m + c == 0) break;
            }

            // 남은 MeshCollider 는 빌드 때 미리 쿠킹해서 씬 로딩 시간을 줄인다
            if (targets.Count > 0 && !PlayerSettings.bakeCollisionMeshes) PlayerSettings.bakeCollisionMeshes = true;

            CountColliders(targets, out rep.boxAfter, out rep.capsuleAfter, out rep.sphereAfter, out rep.meshAfter);
            rep.interiorAfter = ColliderTotal(detailed);
            rep.exteriorAfter = ColliderTotal(exterior);
            if (undo) Undo.CollapseUndoOperations(group);
            return rep;
        }

        static void CountColliders(IList<GameObject> roots, out int box, out int capsule, out int sphere, out int mesh)
        {
            box = capsule = sphere = mesh = 0;
            foreach (var r in roots)
                foreach (var c in r.GetComponentsInChildren<Collider>(true))
                {
                    if (c is BoxCollider) box++;
                    else if (c is CapsuleCollider) capsule++;
                    else if (c is SphereCollider) sphere++;
                    else if (c is MeshCollider) mesh++;
                }
        }

        // ------------------------------------------------------------------ helpers
        static bool ColEligible(Collider c)
        {
            if (c == null || !c.enabled || c.isTrigger || !c.gameObject.activeInHierarchy) return false;
            return GoEligible(c.gameObject);
        }

        static bool GoEligible(GameObject g)
        {
            return g.activeInHierarchy && !PrefabUtility.IsPartOfPrefabInstance(g) && GameObjectUtility.GetStaticEditorFlags(g) != 0
                   && !HasColliderBehaviour(g);
        }

        static T AddCol<T>(GameObject g, bool undo) where T : Component
        {
            return undo ? Undo.AddComponent<T>(g) : g.AddComponent<T>();
        }

        static void Kill(Object o, bool undo)
        {
            if (undo) Undo.DestroyObjectImmediate(o); else Object.DestroyImmediate(o);
        }

        /// <summary>콜라이더를 지우고, 콜라이더만 있던 빈 오브젝트(투명 벽 등)면 오브젝트도 지운다.</summary>
        static void RemoveCollider(Collider c, bool undo)
        {
            var g = c.gameObject;
            Kill(c, undo);
            if (g.transform.childCount == 0 && g.GetComponents<Component>().Length == 1) Kill(g, undo);
        }

        static void CopyColliderSettings(Collider from, Collider to)
        {
            to.sharedMaterial = from.sharedMaterial;
            to.contactOffset = from.contactOffset;
        }

        // ------------------------------------------------------------------ world-space box (OBB)
        class ColShape
        {
            public Collider col;
            public BoxCollider box;     // 박스일 때만
            public Vector3 c, h;        // 월드 중심, 축 방향 반크기
            public Vector3[] ax;        // 월드 단위축 3개 (박스면 콜라이더 로컬 x,y,z 순서)
            public bool alive = true, dirty;

            public float Volume { get { return h.x * h.y * h.z * 8f; } }

            public Bounds Aabb
            {
                get
                {
                    var e = Vector3.zero;
                    for (int j = 0; j < 3; j++)
                        for (int i = 0; i < 3; i++) e[i] += Mathf.Abs(ax[j][i]) * h[j];
                    return new Bounds(c, e * 2f);
                }
            }

            public IEnumerable<Vector3> Corners()
            {
                for (int i = 0; i < 8; i++)
                    yield return c + ax[0] * ((i & 1) == 0 ? -h.x : h.x) + ax[1] * ((i & 2) == 0 ? -h.y : h.y) + ax[2] * ((i & 4) == 0 ? -h.z : h.z);
            }
        }

        /// <summary>Box / Capsule / Sphere 를 감싸는 월드 OBB. (캡슐 · 구는 바깥 박스라서 '안에 들어감' 판정에만 쓴다)</summary>
        static ColShape MakeShape(Collider col)
        {
            var m = col.transform.localToWorldMatrix;
            var cols = new Vector3[] { m.GetColumn(0), m.GetColumn(1), m.GetColumn(2) };
            var len = new Vector3(cols[0].magnitude, cols[1].magnitude, cols[2].magnitude);
            if (len.x < 1e-6f || len.y < 1e-6f || len.z < 1e-6f) return null;
            var ax = new[] { cols[0] / len.x, cols[1] / len.y, cols[2] / len.z };
            // 전단(기울어진 비균등 스케일)이 있으면 박스가 아니므로 건드리지 않는다
            if (Mathf.Abs(Vector3.Dot(ax[0], ax[1])) > 1e-3f || Mathf.Abs(Vector3.Dot(ax[0], ax[2])) > 1e-3f || Mathf.Abs(Vector3.Dot(ax[1], ax[2])) > 1e-3f)
                return null;

            Vector3 center, half;
            var box = col as BoxCollider;
            if (box != null)
            {
                center = box.center;
                half = Vector3.Scale(new Vector3(Mathf.Abs(box.size.x), Mathf.Abs(box.size.y), Mathf.Abs(box.size.z)), len) * 0.5f;
            }
            else if (col is CapsuleCollider cap)
            {
                center = cap.center;
                int d = cap.direction, a = (d + 1) % 3, b = (d + 2) % 3;
                float r = Mathf.Abs(cap.radius) * Mathf.Max(len[a], len[b]);
                half = new Vector3(r, r, r);
                half[d] = Mathf.Max(Mathf.Abs(cap.height) * len[d], 2f * r) * 0.5f;
            }
            else if (col is SphereCollider sph)
            {
                center = sph.center;
                float r = Mathf.Abs(sph.radius) * Mathf.Max(len.x, Mathf.Max(len.y, len.z));
                half = new Vector3(r, r, r);
            }
            else return null;
            return new ColShape { col = col, box = box, c = m.MultiplyPoint3x4(center), h = half, ax = ax };
        }

        static List<ColShape> GatherShapes(IList<GameObject> roots)
        {
            var list = new List<ColShape>();
            foreach (var r in roots)
                foreach (var c in r.GetComponentsInChildren<Collider>(true))
                {
                    if (c is MeshCollider || !ColEligible(c)) continue;
                    var s = MakeShape(c);
                    if (s != null) list.Add(s);
                }
            return list;
        }

        static bool Inside(ColShape inner, ColShape outer, float tol)
        {
            foreach (var p in inner.Corners())
            {
                Vector3 d = p - outer.c;
                for (int j = 0; j < 3; j++)
                    if (Mathf.Abs(Vector3.Dot(d, outer.ax[j])) > outer.h[j] + tol) return false;
            }
            return true;
        }

        /// <summary>b 의 각 축이 a 의 몇 번째 축과 평행한지. 평행하지 않으면 null.</summary>
        static int[] AxisMap(ColShape a, ColShape b)
        {
            var map = new int[3];
            for (int j = 0; j < 3; j++)
            {
                map[j] = -1;
                for (int i = 0; i < 3; i++)
                    if (Mathf.Abs(Vector3.Dot(a.ax[i], b.ax[j])) > 0.9995f) { map[j] = i; break; }
                if (map[j] < 0) return null;
            }
            return map;
        }

        /// <summary>위에서 아래로 쏜 광선이 처음 닿는 높이. 시작점이 박스 안이면 시작 높이.</summary>
        static float RayDown(List<ColShape> shapes, ColShape skip, Vector3 from)
        {
            float best = float.NegativeInfinity;
            foreach (var s in shapes)
            {
                if (!s.alive || s == skip || s.box == null) continue;
                Vector3 o = from - s.c;
                float tmin = 0f, tmax = float.PositiveInfinity;
                bool hit = true;
                for (int j = 0; j < 3 && hit; j++)
                {
                    float oj = Vector3.Dot(o, s.ax[j]), dj = -s.ax[j].y;
                    if (Mathf.Abs(dj) < 1e-6f) { if (Mathf.Abs(oj) > s.h[j]) hit = false; continue; }
                    float t0 = (-s.h[j] - oj) / dj, t1 = (s.h[j] - oj) / dj;
                    if (t0 > t1) { float t = t0; t0 = t1; t1 = t; }
                    tmin = Mathf.Max(tmin, t0); tmax = Mathf.Min(tmax, t1);
                    if (tmin > tmax) hit = false;
                }
                if (hit) best = Mathf.Max(best, from.y - tmin);
            }
            return best;
        }

        /// <summary>합친 OBB 를 BoxCollider 의 로컬 center/size 로 되돌려 쓴다. (s.ax 는 그 오브젝트의 로컬 축)</summary>
        static void ApplyShape(ColShape s, bool undo)
        {
            var t = s.box.transform;
            var m = t.localToWorldMatrix;
            var len = new Vector3(((Vector3)m.GetColumn(0)).magnitude, ((Vector3)m.GetColumn(1)).magnitude, ((Vector3)m.GetColumn(2)).magnitude);
            if (undo) Undo.RecordObject(s.box, ColUndoName);
            s.box.center = t.worldToLocalMatrix.MultiplyPoint3x4(s.c);
            s.box.size = new Vector3(s.h.x * 2f / len.x, s.h.y * 2f / len.y, s.h.z * 2f / len.z);
        }

        // ------------------------------------------------------------------ 1. MeshCollider → BoxCollider
        struct LocalBox
        {
            public Vector3 c, size;    // size 는 rot 기준
            public Quaternion rot;
            public bool aligned;       // 로컬 축과 평행 (rot 무시하고 min/max 사용 가능)
            public Vector3 min, max;   // aligned 일 때 로컬 AABB
        }

        static void ReplaceBoxMeshColliders(IList<GameObject> roots, ColliderReport rep, bool undo)
        {
            var targets = new List<MeshCollider>();
            foreach (var r in roots)
                foreach (var mc in r.GetComponentsInChildren<MeshCollider>(true))
                    if (!mc.convex && ColEligible(mc)) targets.Add(mc);

            foreach (var mc in targets)
            {
                var boxes = DecomposeBoxMesh(mc.sharedMesh);
                if (boxes == null) continue;   // 박스로 이루어진 메쉬가 아니면 그대로 둔다
                var g = mc.gameObject;
                int ramps;
                boxes = StairStepsToRamps(boxes, out ramps);
                rep.ramps += ramps;
                foreach (var b in boxes)
                {
                    BoxCollider bc;
                    if (b.aligned)
                    {
                        bc = AddCol<BoxCollider>(g, undo);
                        bc.center = (b.min + b.max) * 0.5f;
                        bc.size = b.max - b.min;
                    }
                    else
                    {
                        // 기울어진 박스(경사로 등)는 회전된 자식 오브젝트에 붙인다
                        var child = new GameObject(b.size.z > b.size.x * 1.5f ? "Collider_Ramp" : "Collider_Box");
                        child.transform.SetParent(g.transform, false);
                        child.transform.localPosition = b.c;
                        child.transform.localRotation = b.rot;
                        child.layer = g.layer;
                        GameObjectUtility.SetStaticEditorFlags(child, GameObjectUtility.GetStaticEditorFlags(g));
                        bc = child.AddComponent<BoxCollider>();
                        bc.center = Vector3.zero;
                        bc.size = b.size;
                        if (undo) Undo.RegisterCreatedObjectUndo(child, ColUndoName);
                    }
                    CopyColliderSettings(mc, bc);
                }
                Kill(mc, undo);
                rep.meshReplaced++;
            }
        }

        /// <summary>BJ.MeshBuilder 처럼 박스(정점 24개 · 삼각형 12개)를 이어 붙인 메쉬면 박스 목록, 아니면 null.</summary>
        static List<LocalBox> DecomposeBoxMesh(Mesh mesh)
        {
            if (mesh == null || !mesh.isReadable || mesh.subMeshCount != 1) return null;
            var v = mesh.vertices;
            var n = mesh.normals;
            var tri = mesh.triangles;
            if (v.Length == 0 || v.Length % 24 != 0 || n.Length != v.Length || tri.Length != v.Length / 24 * 36) return null;
            for (int i = 0; i < tri.Length; i++)
                if (tri[i] / 24 != i / 36) return null;   // 삼각형이 자기 박스의 정점만 써야 한다

            var list = new List<LocalBox>();
            for (int b = 0; b < v.Length; b += 24)
            {
                Vector3 a0 = n[b].normalized, a1 = Vector3.zero;
                for (int k = b + 1; k < b + 24; k++)
                    if (Mathf.Abs(Vector3.Dot(n[k].normalized, a0)) < 1e-3f) { a1 = n[k].normalized; break; }
                if (a1 == Vector3.zero) return null;
                var ax = new[] { a0, a1, Vector3.Cross(a0, a1) };
                Vector3 lo = Vector3.one * float.MaxValue, hi = -lo;
                for (int k = b; k < b + 24; k++)
                    for (int j = 0; j < 3; j++)
                    {
                        float d = Vector3.Dot(v[k], ax[j]);
                        lo[j] = Mathf.Min(lo[j], d); hi[j] = Mathf.Max(hi[j], d);
                    }
                for (int k = b; k < b + 24; k++)
                    for (int j = 0; j < 3; j++)
                    {
                        float d = Vector3.Dot(v[k], ax[j]);
                        if (Mathf.Min(Mathf.Abs(d - lo[j]), Mathf.Abs(d - hi[j])) > 1e-3f) return null;   // 꼭짓점이 아닌 정점
                    }
                Vector3 mid = (lo + hi) * 0.5f;
                var lb = new LocalBox
                {
                    c = ax[0] * mid.x + ax[1] * mid.y + ax[2] * mid.z,
                    size = hi - lo,
                    rot = Quaternion.LookRotation(ax[2], ax[1]),
                    aligned = true,
                };
                var e = Vector3.zero;
                for (int j = 0; j < 3; j++)
                {
                    int axis = -1;
                    for (int i = 0; i < 3; i++) if (Mathf.Abs(ax[j][i]) > 0.9999f) axis = i;
                    if (axis < 0) { lb.aligned = false; break; }
                    e[axis] = lb.size[j] * 0.5f;
                }
                if (lb.aligned) { lb.min = lb.c - e; lb.max = lb.c + e; }
                list.Add(lb);
            }
            return list;
        }

        /// <summary>
        /// 같은 크기의 박스가 한 단씩(높이 rise, 깊이 tread) 내려가며 이어진 줄을 찾아 경사로 박스 하나로 바꾼다.
        /// 경사로 윗면은 각 단의 모서리를 지나고 위층 바닥 끝 ~ 아래층 바닥에 끊김 없이 이어진다.
        /// 단 아래의 기울어진 하부 슬래브도 경사로 두께 안에 들어오면 함께 흡수한다.
        /// </summary>
        static List<LocalBox> StairStepsToRamps(List<LocalBox> boxes, out int ramps)
        {
            ramps = 0;
            var used = new bool[boxes.Count];
            var order = new List<int>();
            for (int i = 0; i < boxes.Count; i++) if (boxes[i].aligned) order.Add(i);
            order.Sort((a, b) => boxes[b].max.y.CompareTo(boxes[a].max.y));
            var ramp = new List<LocalBox>();

            foreach (int start in order)
            {
                if (used[start]) continue;
                var chain = new List<int> { start };
                int axis = -1, other = -1;
                float sign = 0f, rise = 0f;
                int cur = start;
                while (true)
                {
                    int next = -1;
                    var a = boxes[cur];
                    foreach (int j in order)
                    {
                        if (used[j] || chain.Contains(j)) continue;
                        var b = boxes[j];
                        if (((a.max - a.min) - (b.max - b.min)).sqrMagnitude > 1e-4f) continue;
                        float dy = a.max.y - b.max.y;
                        if (dy < 0.05f || dy > 0.3f || (rise > 0f && Mathf.Abs(dy - rise) > 0.005f)) continue;
                        for (int ax = 0; ax <= 2; ax += 2)
                        {
                            int ot = 2 - ax;
                            if (axis >= 0 && ax != axis) continue;
                            if (Mathf.Abs(a.min[ot] - b.min[ot]) > 0.01f || Mathf.Abs(a.max[ot] - b.max[ot]) > 0.01f) continue;
                            float sg = Mathf.Abs(a.max[ax] - b.min[ax]) < 0.01f ? 1f : Mathf.Abs(a.min[ax] - b.max[ax]) < 0.01f ? -1f : 0f;
                            if (sg == 0f || (axis >= 0 && sg != sign)) continue;
                            next = j; axis = ax; other = ot; sign = sg; rise = dy;
                            break;
                        }
                        if (next >= 0) break;
                    }
                    if (next < 0) break;
                    chain.Add(next);
                    cur = next;
                }
                if (chain.Count < 3) continue;

                // 내려가는 방향 기준 좌표 s = sign * p[axis]
                var first = boxes[chain[0]];
                var last = boxes[chain[chain.Count - 1]];
                float depth = first.max[axis] - first.min[axis];
                if (Mathf.Atan2(rise, depth) * Mathf.Rad2Deg > RampMaxAngle) continue;   // 너무 가파르면 단 그대로
                float sTop = Mathf.Min(sign * first.min[axis], sign * first.max[axis]);
                float sBot = Mathf.Max(sign * last.min[axis], sign * last.max[axis]) + depth;
                float wMid = (first.min[other] + first.max[other]) * 0.5f, width = first.max[other] - first.min[other];
                Vector3 eA = Vector3.zero, eW = Vector3.zero;
                eA[axis] = 1f; eW[other] = 1f;
                Vector3 pTop = eA * (sign * sTop) + eW * wMid + Vector3.up * (first.max.y + rise);
                Vector3 pBot = eA * (sign * sBot) + eW * wMid + Vector3.up * (last.max.y - rise);
                Vector3 u = (pBot - pTop).normalized;
                float length = (pBot - pTop).magnitude;
                Vector3 nrm = (Vector3.up - u * Vector3.Dot(Vector3.up, u)).normalized;

                float thick = 0.1f;
                foreach (int k in chain)
                    foreach (var p in BoxCorners(boxes[k]))
                        thick = Mathf.Max(thick, Vector3.Dot(pTop - p, nrm));
                // 같은 기울기의 하부 슬래브가 경사로 아래 1.5m 안에 들어오면 흡수
                var absorbed = new List<int>();
                for (int i = 0; i < boxes.Count; i++)
                {
                    if (used[i] || boxes[i].aligned) continue;
                    Vector3 bf = boxes[i].rot * Vector3.forward, bu = boxes[i].rot * Vector3.up;
                    if (Mathf.Abs(Vector3.Dot(bf, u)) < 0.9995f || Mathf.Abs(Vector3.Dot(bu, nrm)) < 0.9995f) continue;
                    float deep = 0f;
                    bool fits = true;
                    foreach (var p in BoxCorners(boxes[i]))
                    {
                        float hgt = Vector3.Dot(p - pTop, nrm), along = Vector3.Dot(p - pTop, u), across = Vector3.Dot(p - pTop, eW);
                        if (hgt > 0.01f || hgt < -1.5f || along < -0.01f || along > length + 0.01f || Mathf.Abs(across) > width * 0.5f + 0.01f) { fits = false; break; }
                        deep = Mathf.Max(deep, -hgt);
                    }
                    if (!fits) continue;
                    absorbed.Add(i);
                    thick = Mathf.Max(thick, deep);
                }
                foreach (int i in absorbed) used[i] = true;
                foreach (int k in chain) used[k] = true;
                ramp.Add(new LocalBox
                {
                    c = (pTop + pBot) * 0.5f - nrm * (thick * 0.5f),
                    rot = Quaternion.LookRotation(u, nrm),
                    size = new Vector3(width, thick, length),
                    aligned = false,
                });
                ramps++;
            }

            var result = new List<LocalBox>();
            for (int i = 0; i < boxes.Count; i++) if (!used[i]) result.Add(boxes[i]);
            result.AddRange(ramp);
            return result;
        }

        static IEnumerable<Vector3> BoxCorners(LocalBox b)
        {
            Vector3 h = b.size * 0.5f;
            for (int i = 0; i < 8; i++)
                yield return b.c + b.rot * new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z);
        }

        // ------------------------------------------------------------------ 2. 다른 박스 안에 들어간 콜라이더
        static int RemoveContained(IList<GameObject> roots, bool undo)
        {
            var shapes = GatherShapes(roots);
            int removed = 0;
            foreach (var a in shapes)
            {
                var ab = a.Aabb;
                foreach (var b in shapes)
                {
                    if (b == a || !b.alive || b.box == null) continue;
                    if (!SameColliderSemantics(a.col, b.col)) continue;
                    var bb = b.Aabb;
                    bb.Expand(ColContainTol * 2f);
                    if (!bb.Contains(ab.min) || !bb.Contains(ab.max)) continue;
                    if (!Inside(a, b, ColContainTol)) continue;
                    a.alive = false;
                    break;
                }
            }
            foreach (var s in shapes)
                if (!s.alive) { RemoveCollider(s.col, undo); removed++; }
            return removed;
        }

        // ------------------------------------------------------------------ 3. 바닥에 깔린 얇은 잔해
        static int RemoveThinClutter(IList<GameObject> roots, bool undo)
        {
            var shapes = GatherShapes(roots);
            int removed = 0;
            foreach (var a in shapes)
            {
                if (a.box == null) continue;
                var ab = a.Aabb;
                if (ab.size.y > ThinMaxHeight) continue;
                // 가장 수평에 가까운 두 축 = 바닥 면
                int up = 0;
                for (int j = 1; j < 3; j++) if (Mathf.Abs(a.ax[j].y) > Mathf.Abs(a.ax[up].y)) up = j;
                int i0 = (up + 1) % 3, i1 = (up + 2) % 3;
                if (a.h[i0] * a.h[i1] * 4f > ThinMaxArea) continue;
                // 가운데와 네 모서리 아래가 모두 바로 밑의 다른 콜라이더에 받쳐져 있어야 한다 (틈을 잇는 발판은 남긴다)
                bool supported = true;
                for (int k = 0; k < 5 && supported; k++)
                {
                    Vector3 p = a.c;
                    if (k > 0) p += a.ax[i0] * (a.h[i0] * 0.8f * ((k & 1) == 0 ? -1f : 1f)) + a.ax[i1] * (a.h[i1] * 0.8f * (k < 3 ? -1f : 1f));
                    p.y = ab.max.y + 0.01f;
                    supported = RayDown(shapes, a, p) >= ab.min.y - ThinSupportGap;
                }
                if (!supported) continue;
                a.alive = false;
                RemoveCollider(a.col, undo);
                removed++;
            }
            return removed;
        }

        // ------------------------------------------------------------------ 4. 작은 소품 → 박스 하나
        static int CompactSmallProps(IList<GameObject> roots, bool undo)
        {
            var shapes = GatherShapes(roots);
            var groups = new Dictionary<Transform, List<ColShape>>();
            var order = new List<Transform>();
            foreach (var s in shapes)
            {
                var t = s.col.transform;
                // 그룹 오브젝트 자신의 콜라이더와 바로 아래 (자식 없는) 오브젝트들의 콜라이더를 한 소품으로 본다
                var key = t.parent != null && t.childCount == 0 ? t.parent : t;
                List<ColShape> list;
                if (!groups.TryGetValue(key, out list)) { list = new List<ColShape>(); groups[key] = list; order.Add(key); }
                list.Add(s);
            }
            int saved = 0;
            foreach (var g in order)
            {
                var list = groups[g];
                if (list.Count < 2 || !GoEligible(g.gameObject)) continue;
                // 그룹 안에 최적화 대상이 아닌 콜라이더(트리거 등)가 섞여 있으면 손대지 않는다
                int total = g.GetComponents<Collider>().Length;
                foreach (Transform ch in g) total += ch.GetComponents<Collider>().Length;
                if (total != list.Count) continue;
                var first = list[0].col;
                bool same = true;
                foreach (var s in list)
                    if (!SameColliderSemantics(s.col, first) || s.col.gameObject.layer != g.gameObject.layer
                        || s.col.gameObject.tag != g.gameObject.tag) same = false;
                if (!same) continue;

                var w2l = g.worldToLocalMatrix;
                Vector3 lo = Vector3.one * float.MaxValue, hi = -lo;
                foreach (var s in list)
                    foreach (var p in s.Corners())
                    {
                        Vector3 q = w2l.MultiplyPoint3x4(p);
                        lo = Vector3.Min(lo, q); hi = Vector3.Max(hi, q);
                    }
                Vector3 size = Vector3.Scale(hi - lo, Abs(g.lossyScale));
                float minDim = Mathf.Min(size.x, Mathf.Min(size.y, size.z)), maxDim = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
                if (minDim > PropMaxMinDim || maxDim > PropMaxDim || size.x * size.y * size.z > PropMaxVolume) continue;

                var bc = AddCol<BoxCollider>(g.gameObject, undo);
                bc.center = (lo + hi) * 0.5f;
                bc.size = hi - lo;
                CopyColliderSettings(first, bc);
                foreach (var s in list) RemoveCollider(s.col, undo);
                saved += list.Count - 1;
            }
            return saved;
        }

        static Vector3 Abs(Vector3 v) { return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z)); }

        // ------------------------------------------------------------------ 5. 맞닿은 박스 합치기
        /// <summary>
        /// 같은 부모 아래의 BoxCollider 두 개가 두 축의 면이 (허용오차 안에서) 같고 나머지 한 축으로 붙어 있거나 겹치면,
        /// 합집합이 곧 박스이므로 큰 쪽 하나로 합친다. 문 · 통로 같은 빈 공간은 메워지지 않는다.
        /// </summary>
        static int MergeSiblingBoxes(IList<GameObject> roots, bool undo)
        {
            var shapes = GatherShapes(roots);
            var groups = new Dictionary<Transform, List<ColShape>>();
            var order = new List<Transform>();
            foreach (var s in shapes)
            {
                if (s.box == null) continue;
                var key = s.col.transform.parent != null ? s.col.transform.parent : s.col.transform;
                List<ColShape> list;
                if (!groups.TryGetValue(key, out list)) { list = new List<ColShape>(); groups[key] = list; order.Add(key); }
                list.Add(s);
            }
            int merged = 0;
            foreach (var key in order)
            {
                var list = groups[key];
                if (list.Count < 2) continue;
                bool changed = true;
                while (changed)
                {
                    changed = false;
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (!list[i].alive) continue;
                        for (int j = i + 1; j < list.Count; j++)
                        {
                            var a = list[i];
                            var b = list[j];
                            if (!b.alive || !SameColliderSemantics(a.col, b.col)) continue;
                            var host = a.Volume >= b.Volume ? a : b;
                            var other = host == a ? b : a;
                            if (!TryMergeInto(host, other)) continue;
                            other.alive = false;
                            host.dirty = true;
                            changed = true;
                            merged++;
                            if (!a.alive) break;
                        }
                    }
                }
                foreach (var s in list)
                {
                    if (!s.alive) RemoveCollider(s.col, undo);
                    else if (s.dirty) ApplyShape(s, undo);
                }
            }
            return merged;
        }

        static bool TryMergeInto(ColShape a, ColShape b)
        {
            var map = AxisMap(a, b);
            if (map == null) return false;
            Vector3 d = b.c - a.c;
            Vector3 aMin = -a.h, aMax = a.h, bMin = Vector3.zero, bMax = Vector3.zero;
            for (int j = 0; j < 3; j++)
            {
                int i = map[j];
                float cc = Vector3.Dot(d, a.ax[i]);
                bMin[i] = cc - b.h[j];
                bMax[i] = cc + b.h[j];
            }
            for (int k = 0; k < 3; k++)
            {
                bool ok = true;
                for (int i = 0; i < 3 && ok; i++)
                {
                    if (i == k) ok = aMin[i] <= bMax[i] + ColMergeGap && bMin[i] <= aMax[i] + ColMergeGap;
                    else ok = Mathf.Abs(aMin[i] - bMin[i]) <= ColMergeTol && Mathf.Abs(aMax[i] - bMax[i]) <= ColMergeTol;
                }
                if (!ok) continue;
                Vector3 lo = Vector3.Min(aMin, bMin), hi = Vector3.Max(aMax, bMax);
                Vector3 mid = (lo + hi) * 0.5f;
                a.c += a.ax[0] * mid.x + a.ax[1] * mid.y + a.ax[2] * mid.z;
                a.h = (hi - lo) * 0.5f;
                return true;
            }
            return false;
        }
    }
}
