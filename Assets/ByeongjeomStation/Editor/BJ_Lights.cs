using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Byeongjeom;

namespace Byeongjeom.MapGen
{
    /// <summary>
    /// 조명 최적화. 메뉴: Tools > 병점역 맵 > 조명 최적화 (맵 생성 때도 자동으로 실행)
    ///  1. 루트에 BJ_LightCuller 를 붙인다 - Play 중 안개 너머의 먼 조명을 끄고, 그림자 거리 · 캐스케이드를 안개 거리에 맞춘다.
    ///  2. 길이 수십~백여 m 짜리 유리(투명) 메쉬를 20m 단위로 나눈다.
    ///     투명 물체는 포워드로 그려져 물체마다 가까운 라이트 몇 개만 고르는데, 144m 스크린도어 유리 한 장은
    ///     가운데 근처 라이트로 전체가 칠해지고, 먼 조명을 끌 때마다 유리 전체 밝기가 바뀐다.
    ///     나누면 조각마다 주변 라이트로 칠해지고, 화면 밖 조각은 그리지 않는다.
    /// </summary>
    public static partial class BJ
    {
        const float GlassChunk = 20f;
        const string SplitMeshPath = GenDir + "/BJ_SplitMeshes.asset";

        [MenuItem("Tools/병점역 맵/조명 최적화", false, 22)]
        static void OptimizeLightsMenu()
        {
            var root = GameObject.Find("ByeongjeomStation_병점역");
            if (root == null) { EditorUtility.DisplayDialog("조명 최적화", "병점역 씬을 먼저 열어주세요.", "확인"); return; }
            var meshes = new List<Mesh>();
            string rep = OptimizeLights(root, true, meshes);
            if (meshes.Count > 0)
            {
                if (AssetDatabase.LoadMainAssetAtPath(SplitMeshPath) == null) AssetDatabase.CreateAsset(meshes[0], SplitMeshPath);
                else AssetDatabase.AddObjectToAsset(meshes[0], SplitMeshPath);
                for (int i = 1; i < meshes.Count; i++) AssetDatabase.AddObjectToAsset(meshes[i], SplitMeshPath);
                AssetDatabase.SaveAssets();
            }
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log("[병점역] " + rep);
            EditorUtility.DisplayDialog("조명 최적화 완료", rep + "\n\n씬을 저장(Ctrl+S)하면 적용됩니다.", "확인");
        }

        /// <summary>루트에 조명 최적화를 적용한다. 새로 만든 메쉬는 newMeshes 에 담기므로 호출한 쪽에서 에셋으로 저장해야 한다.</summary>
        public static string OptimizeLights(GameObject root, bool undo, List<Mesh> newMeshes)
        {
            if (undo)
            {
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName("조명 최적화");
            }
            int group = Undo.GetCurrentGroup();

            var culler = root.GetComponent<BJ_LightCuller>();
            if (culler == null) culler = undo ? Undo.AddComponent<BJ_LightCuller>(root) : root.AddComponent<BJ_LightCuller>();
            int managed = 0, total = 0;
            foreach (var l in root.GetComponentsInChildren<Light>(false))
            {
                if (l.type == LightType.Directional) continue;
                total++;
                if (BJ_LightCuller.IsManaged(l)) managed++;
            }
            int before = newMeshes.Count;
            int split = SplitLargeTransparent(root, undo, newMeshes);
            if (undo) Undo.CollapseUndoOperations(group);

            float far = float.PositiveInfinity;
            float lightDist = culler.EffectiveDistance(far);
            float sd0 = QualitySettings.shadowDistance, sd = culler.ShadowDistance(sd0, far);
            return string.Format(
                "포인트 · 스포트 라이트 {0}개 중 {1}개를 Play 중 거리에 따라 끄고 켭니다 (BJ_LightCuller).\n" +
                "  지금 안개 기준: 비추는 범위가 카메라에서 {2} 밖이면 꺼짐, 그림자 거리 {3}m → {4}m, 캐스케이드 {5} → {6}\n" +
                "긴 유리 메쉬 {7}개를 {8}조각으로 나눴습니다.",
                total, managed,
                float.IsInfinity(lightDist) ? "(제한 없음)" : lightDist.ToString("F0") + "m",
                sd0.ToString("F0"), sd.ToString("F0"),
                QualitySettings.shadowCascades, culler.ShadowCascades(sd0, QualitySettings.shadowCascades, sd),
                split, newMeshes.Count - before);
        }

        static bool IsTransparent(Renderer r)
        {
            foreach (var m in r.sharedMaterials)
                if (m != null && m.renderQueue >= 2500) return true;
            return false;
        }

        /// <summary>박스를 이어 붙인 긴 투명 메쉬를 GlassChunk(m) 단위 조각으로 나눈다. 나눈 개수를 돌려준다.</summary>
        static int SplitLargeTransparent(GameObject root, bool undo, List<Mesh> newMeshes)
        {
            // 깜빡임 연출이 참조하는 렌더러는 바꾸지 않는다
            var referenced = new HashSet<Renderer>();
            foreach (var f in root.GetComponentsInChildren<BJ_FlickerLight>(true))
                if (f.emissiveRenderers != null) foreach (var r in f.emissiveRenderers) if (r != null) referenced.Add(r);

            int count = 0;
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (!mr.enabled || referenced.Contains(mr) || !IsTransparent(mr) || PrefabUtility.IsPartOfPrefabInstance(mr)) continue;
                var mf = mr.GetComponent<MeshFilter>();
                var mesh = mf != null ? mf.sharedMesh : null;
                var b = mr.bounds;
                float longest = Mathf.Max(b.size.x, b.size.z);
                if (mesh == null || longest <= GlassChunk * 1.25f) continue;
                var parts = SplitBoxMesh(mesh, mr.transform, b, longest);
                if (parts == null) continue;

                var g = mr.gameObject;
                for (int i = 0; i < parts.Count; i++)
                {
                    var child = new GameObject(g.name + "_" + (i + 1));
                    child.transform.SetParent(g.transform, false);
                    child.layer = g.layer;
                    GameObjectUtility.SetStaticEditorFlags(child, GameObjectUtility.GetStaticEditorFlags(g));
                    child.AddComponent<MeshFilter>().sharedMesh = parts[i];
                    var r = child.AddComponent<MeshRenderer>();
                    UnityEditorInternal.ComponentUtility.CopyComponent(mr);
                    UnityEditorInternal.ComponentUtility.PasteComponentValues(r);
                    if (undo) Undo.RegisterCreatedObjectUndo(child, "조명 최적화");
                    newMeshes.Add(parts[i]);
                }
                Kill(mr, undo);
                Kill(mf, undo);
                count++;
            }
            return count;
        }

        /// <summary>정점 24개 = 박스 하나인 메쉬를 월드 기준 가장 긴 수평축을 따라 나눈다. 박스 메쉬가 아니면 null.</summary>
        static List<Mesh> SplitBoxMesh(Mesh mesh, Transform t, Bounds wb, float longest)
        {
            if (!mesh.isReadable || mesh.subMeshCount != 1) return null;
            var v = mesh.vertices;
            var tri = mesh.triangles;
            if (v.Length == 0 || v.Length % 24 != 0 || tri.Length != v.Length / 24 * 36) return null;
            for (int i = 0; i < tri.Length; i++)
                if (tri[i] / 24 != i / 36) return null;
            var n = mesh.normals;
            var tan = mesh.tangents;
            var uv = mesh.uv;

            int axis = wb.size.x >= wb.size.z ? 0 : 2;
            int parts = Mathf.CeilToInt(longest / GlassChunk);
            float step = longest / parts;
            var bins = new List<int>[parts];
            for (int k = 0; k < v.Length; k += 24)
            {
                Vector3 c = Vector3.zero;
                for (int j = 0; j < 24; j++) c += v[k + j];
                float w = t.TransformPoint(c / 24f)[axis];
                int bin = Mathf.Clamp(Mathf.FloorToInt((w - wb.min[axis]) / step), 0, parts - 1);
                if (bins[bin] == null) bins[bin] = new List<int>();
                bins[bin].Add(k);
            }

            var result = new List<Mesh>();
            for (int p = 0; p < parts; p++)
            {
                if (bins[p] == null) continue;
                var pv = new List<Vector3>(); var pn = new List<Vector3>(); var pt = new List<Vector4>(); var pu = new List<Vector2>(); var pi = new List<int>();
                foreach (int k in bins[p])
                {
                    int baseIdx = pv.Count;
                    for (int j = 0; j < 24; j++)
                    {
                        pv.Add(v[k + j]);
                        if (n.Length == v.Length) pn.Add(n[k + j]);
                        if (tan.Length == v.Length) pt.Add(tan[k + j]);
                        if (uv.Length == v.Length) pu.Add(uv[k + j]);
                    }
                    int ti = k / 24 * 36;
                    for (int j = 0; j < 36; j++) pi.Add(tri[ti + j] - k + baseIdx);
                }
                var m = new Mesh { name = mesh.name + "_" + (result.Count + 1) };
                m.SetVertices(pv);
                if (pn.Count > 0) m.SetNormals(pn);
                if (pt.Count > 0) m.SetTangents(pt);
                if (pu.Count > 0) m.SetUVs(0, pu);
                m.SetTriangles(pi, 0);
                m.RecalculateBounds();
                result.Add(m);
            }
            return result.Count > 1 ? result : null;
        }
    }
}
