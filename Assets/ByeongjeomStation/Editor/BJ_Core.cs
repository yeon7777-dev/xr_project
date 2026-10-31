using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Byeongjeom;

namespace Byeongjeom.MapGen
{
    /// <summary>
    /// 지오메트리 생성 공용 도구 모음.
    /// 좌표계: +X = 동쪽(1번 출구·역앞광장), +Z = 북쪽(서울·청량리 방면), 단위 m.
    /// </summary>
    public static partial class BJ
    {
        public const string RootDir = "Assets/ByeongjeomStation";
        public const string TexDir = RootDir + "/Textures";
        public const string GenDir = RootDir + "/Generated";
        public const string MatDir = GenDir + "/Materials";
        public const string SceneDir = RootDir + "/Scenes";
        public const string ScenePath = SceneDir + "/ByeongjeomStation.unity";

        // ------------------------------------------------------------------ random
        static System.Random rng = new System.Random(1905);
        static float R(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
        static bool Chance(float p) { return rng.NextDouble() < p; }
        static int RI(int a, int bExclusive) { return rng.Next(a, bExclusive); }
        static T Pick<T>(IList<T> list) { return list[rng.Next(list.Count)]; }

        // ------------------------------------------------------------------ registries
        static Dictionary<string, Mesh> boxCache = new Dictionary<string, Mesh>();
        static List<Mesh> meshList = new List<Mesh>();
        static Mesh cylMesh, sphereMesh, quadMesh;
        static readonly List<GameObject> dynamicObjects = new List<GameObject>();

        static void ResetRegistries()
        {
            rng = new System.Random(1905);
            boxCache = new Dictionary<string, Mesh>();
            meshList = new List<Mesh>();
            mats = new Dictionary<string, Material>();
            dynamicObjects.Clear();
            cylMesh = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
            sphereMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            quadMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        }

        // ------------------------------------------------------------------ mesh building
        /// <summary>여러 박스를 하나의 메쉬로 합친다. UV는 미터 단위(월드 스케일)라 텍스처가 늘어나지 않는다.</summary>
        public class MeshBuilder
        {
            readonly List<Vector3> v = new List<Vector3>();
            readonly List<Vector3> n = new List<Vector3>();
            readonly List<Vector2> uv = new List<Vector2>();
            readonly List<int> t = new List<int>();
            public int VertexCount { get { return v.Count; } }

            static readonly Vector3[] FaceN = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            static readonly Vector3[] FaceV = { Vector3.up, Vector3.up, Vector3.forward, Vector3.back, Vector3.up, Vector3.up };

            public void AddBox(Vector3 center, Vector3 size) { AddBox(center, size, Quaternion.identity, true); }

            /// <param name="worldUV">true면 UV에 center를 더해 인접 박스끼리 텍스처가 이어진다.</param>
            public void AddBox(Vector3 center, Vector3 size, Quaternion rot, bool worldUV)
            {
                Vector3 h = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)) * 0.5f;
                Vector3 uvOff = worldUV ? center : Vector3.zero;
                for (int f = 0; f < 6; f++)
                {
                    Vector3 nn = FaceN[f], vv = FaceV[f];
                    Vector3 uu = Vector3.Cross(nn, vv);
                    float dn = Mathf.Abs(Vector3.Dot(h, nn));
                    float du = Mathf.Abs(Vector3.Dot(h, uu));
                    float dv = Mathf.Abs(Vector3.Dot(h, vv));
                    Vector3 c = nn * dn;
                    int i0 = v.Count;
                    Vector3 p0 = c - uu * du - vv * dv, p1 = c + uu * du - vv * dv, p2 = c + uu * du + vv * dv, p3 = c - uu * du + vv * dv;
                    AddV(p0, nn, uu, vv, rot, center, uvOff);
                    AddV(p1, nn, uu, vv, rot, center, uvOff);
                    AddV(p2, nn, uu, vv, rot, center, uvOff);
                    AddV(p3, nn, uu, vv, rot, center, uvOff);
                    t.Add(i0); t.Add(i0 + 2); t.Add(i0 + 1);
                    t.Add(i0); t.Add(i0 + 3); t.Add(i0 + 2);
                }
            }

            void AddV(Vector3 p, Vector3 nn, Vector3 uu, Vector3 vv, Quaternion rot, Vector3 center, Vector3 uvOff)
            {
                v.Add(rot * p + center);
                n.Add(rot * nn);
                Vector3 q = p + uvOff;
                uv.Add(new Vector2(Vector3.Dot(q, uu), Vector3.Dot(q, vv)));
            }

            public Mesh Build(string name)
            {
                var m = new Mesh { name = name };
                if (v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
                m.SetVertices(v);
                m.SetNormals(n);
                m.SetUVs(0, uv);
                m.SetTriangles(t, 0);
                m.RecalculateBounds();
                m.RecalculateTangents();
                meshList.Add(m);
                return m;
            }
        }

        static Mesh BoxMesh(Vector3 s)
        {
            s = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
            string key = s.x.ToString("F3") + "_" + s.y.ToString("F3") + "_" + s.z.ToString("F3");
            Mesh m;
            if (boxCache.TryGetValue(key, out m)) return m;
            var mb = new MeshBuilder();
            mb.AddBox(Vector3.zero, s, Quaternion.identity, false);
            m = mb.Build("Box_" + key);
            boxCache[key] = m;
            return m;
        }

        // ------------------------------------------------------------------ game objects
        public static GameObject Go(string name, Transform parent)
        {
            var g = new GameObject(name);
            if (parent != null) g.transform.SetParent(parent, false);
            return g;
        }

        public static Transform Group(string name, Transform parent, Vector3 pos, float yaw = 0f)
        {
            var g = Go(name, parent);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return g.transform;
        }

        public static Transform Group(string name, Transform parent) { return Group(name, parent, Vector3.zero, 0f); }

        /// <summary>박스 (부모 로컬 좌표, 중심+크기).</summary>
        public static GameObject Box(Transform p, string name, Vector3 center, Vector3 size, Material mat, bool collider = true, Vector3? euler = null)
        {
            var g = Go(name, p);
            g.transform.localPosition = center;
            if (euler.HasValue) g.transform.localRotation = Quaternion.Euler(euler.Value);
            g.AddComponent<MeshFilter>().sharedMesh = BoxMesh(size);
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            if (collider)
            {
                var c = g.AddComponent<BoxCollider>();
                c.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            }
            return g;
        }

        /// <summary>박스 (최소점~최대점).</summary>
        public static GameObject BoxMM(Transform p, string name, Vector3 a, Vector3 b, Material mat, bool collider = true)
        {
            Vector3 min = Vector3.Min(a, b), max = Vector3.Max(a, b);
            return Box(p, name, (min + max) * 0.5f, max - min, mat, collider);
        }

        /// <summary>보이지 않는 충돌벽.</summary>
        public static GameObject Blocker(Transform p, string name, Vector3 a, Vector3 b)
        {
            Vector3 min = Vector3.Min(a, b), max = Vector3.Max(a, b);
            var g = Go(name, p);
            g.transform.localPosition = (min + max) * 0.5f;
            g.AddComponent<BoxCollider>().size = max - min;
            return g;
        }

        public static GameObject Combined(Transform p, string name, MeshBuilder mb, Material mat, bool collider)
        {
            if (mb.VertexCount == 0) return null;
            var g = Go(name, p);
            var mesh = mb.Build(name);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (collider) g.AddComponent<MeshCollider>().sharedMesh = mesh;
            return g;
        }

        /// <summary>두 점 사이 원기둥.</summary>
        public static GameObject Cyl(Transform p, string name, Vector3 a, Vector3 b, float diameter, Material mat, bool collider = false)
        {
            var g = Go(name, p);
            Vector3 d = b - a;
            float len = d.magnitude;
            g.transform.localPosition = (a + b) * 0.5f;
            g.transform.localRotation = len > 1e-4f ? Quaternion.FromToRotation(Vector3.up, d / len) : Quaternion.identity;
            g.transform.localScale = new Vector3(diameter, len * 0.5f, diameter);
            g.AddComponent<MeshFilter>().sharedMesh = cylMesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (collider) g.AddComponent<CapsuleCollider>();
            return g;
        }

        public static GameObject Sphere(Transform p, string name, Vector3 c, Vector3 scale, Material mat, bool collider = false)
        {
            var g = Go(name, p);
            g.transform.localPosition = c;
            g.transform.localScale = scale;
            g.AddComponent<MeshFilter>().sharedMesh = sphereMesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (collider) g.AddComponent<SphereCollider>();
            return g;
        }

        static Quaternion FacingRot(Vector3 facing, float roll = 0f)
        {
            Vector3 up = Mathf.Abs(facing.normalized.y) > 0.9f ? Vector3.forward : Vector3.up;
            return Quaternion.LookRotation(-facing, up) * Quaternion.Euler(0f, 0f, roll);
        }

        /// <summary>facing 방향을 바라보는 사각 판 (UV 0~1).</summary>
        public static GameObject Quad(Transform p, string name, Vector3 center, Vector2 size, Vector3 facing, Material mat, float roll = 0f)
        {
            var g = Go(name, p);
            g.transform.localPosition = center;
            g.transform.localRotation = FacingRot(facing, roll);
            g.transform.localScale = new Vector3(size.x, size.y, 1f);
            g.AddComponent<MeshFilter>().sharedMesh = quadMesh;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            if (!IsURP && twoSided.Contains(mat))
            {
                // Built-in Standard 셰이더는 양면 렌더링이 없으므로 뒷면을 하나 더 붙인다
                var back = Go("BackFace", g.transform);
                back.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                back.AddComponent<MeshFilter>().sharedMesh = quadMesh;
                back.AddComponent<MeshRenderer>().sharedMaterial = mat;
            }
            return g;
        }

        /// <summary>표지판: 뒷판 + 앞면 그림(+선택적 뒷면 그림).</summary>
        public static GameObject Sign(Transform p, string name, Vector3 center, float w, float h, Vector3 facing, string tex,
                                      float glow = 0f, string backTex = null, bool frame = true)
        {
            facing = facing.normalized;
            var g = Go(name, p);
            g.transform.localPosition = center;
            g.transform.localRotation = FacingRot(facing);
            float th = 0.1f;
            if (frame) Box(g.transform, "Frame", Vector3.zero, new Vector3(w + 0.08f, h + 0.08f, th), M("SignFrame"), false);
            var front = Quad(g.transform, "Face", new Vector3(0f, 0f, -(th * 0.5f + 0.005f)), new Vector2(w, h), Vector3.back, SignMat(tex, glow));
            front.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            if (backTex != null)
            {
                var back = Quad(g.transform, "Back", new Vector3(0f, 0f, th * 0.5f + 0.005f), new Vector2(w, h), Vector3.forward, SignMat(backTex, glow));
                back.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            return g;
        }

        /// <summary>천장에 매다는 표지판 (줄 2개 포함).</summary>
        public static GameObject HangingSign(Transform p, string name, Vector3 center, float w, float h, Vector3 facing, string tex, string backTex, float ceilingY, float glow = 0.35f)
        {
            var s = Sign(p, name, center, w, h, facing, tex, glow, backTex);
            Vector3 side = Vector3.Cross(Vector3.up, facing.normalized) * (w * 0.4f);
            Cyl(p, name + "_Rod", center + side + Vector3.up * h * 0.5f, new Vector3(center.x, ceilingY, center.z) + side, 0.03f, M("DarkMetal"));
            Cyl(p, name + "_Rod", center - side + Vector3.up * h * 0.5f, new Vector3(center.x, ceilingY, center.z) - side, 0.03f, M("DarkMetal"));
            return s;
        }

        /// <summary>바닥/벽에 붙이는 데칼 (투명 판).</summary>
        public static GameObject Decal(Transform p, string tex, Vector3 pos, Vector2 size, Vector3 facing, float roll = 0f, float smooth = 0.25f)
        {
            var g = Quad(p, "Decal_" + tex, pos + facing.normalized * 0.012f, size, facing, DecalMat(tex, smooth), roll);
            var r = g.GetComponent<MeshRenderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            return g;
        }

        public static GameObject FloorDecal(Transform p, string tex, Vector3 pos, float size)
        {
            return Decal(p, tex, pos, new Vector2(size, size), Vector3.up, R(0f, 360f));
        }

        // ------------------------------------------------------------------ lights
        public static Light PointLight(Transform p, string name, Vector3 pos, Color c, float intensity, float range, bool shadows = false)
        {
            var g = Go(name, p);
            g.transform.localPosition = pos;
            var l = g.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.intensity = intensity;
            l.range = range;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            l.shadowStrength = 0.9f;
            if (shadows) LowShadowTier(l);   // 포인트 라이트 그림자는 6장씩 쓰므로 저해상도로
            return l;
        }

        /// <summary>URP 추가 라이트 그림자 해상도를 Low 티어로 (그림자 아틀라스 초과 경고 방지).</summary>
        static void LowShadowTier(Light l)
        {
            if (!IsURP) return;
            try
            {
                var t = FindType("UnityEngine.Rendering.Universal.UniversalAdditionalLightData");
                if (t == null) return;
                var data = l.GetComponent(t);
                if (data == null) data = l.gameObject.AddComponent(t);
                var so = new SerializedObject(data);
                var prop = so.FindProperty("m_AdditionalLightsShadowResolutionTier");
                if (prop == null) return;
                prop.intValue = 0; // Low
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            catch (System.Exception) { }
        }

        public static Light SpotLight(Transform p, string name, Vector3 pos, Vector3 dir, Color c, float intensity, float range, float angle, bool shadows = false)
        {
            var g = Go(name, p);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.LookRotation(dir.normalized, Mathf.Abs(dir.normalized.y) > 0.9f ? Vector3.forward : Vector3.up);
            var l = g.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = c;
            l.intensity = intensity;
            l.range = range;
            l.spotAngle = angle;
            l.innerSpotAngle = angle * 0.45f;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            l.shadowStrength = 0.9f;
            return l;
        }

        public static BJ_FlickerLight Flicker(GameObject host, BJ_FlickerLight.Mode mode, Color emission, Light[] lights, params Renderer[] emissive)
        {
            var f = host.GetComponent<BJ_FlickerLight>();
            if (f == null) f = host.AddComponent<BJ_FlickerLight>();
            f.mode = mode;
            f.lights = lights ?? new Light[0];
            f.emissiveRenderers = emissive ?? new Renderer[0];
            f.emissionColor = emission;
            f.seed = R(1f, 999f);
            f.speed = R(0.7f, 1.3f);
            dynamicObjects.Add(host);
            foreach (var r in f.emissiveRenderers) if (r != null) dynamicObjects.Add(r.gameObject);
            return f;
        }

        public enum LampState { Off, On, Flicker, Dying, Buzz }

        static LampState RandomLamp(float on, float flicker, float dying)
        {
            double x = rng.NextDouble();
            if (x < on) return LampState.On;
            if (x < on + flicker) return LampState.Flicker;
            if (x < on + flicker + dying) return LampState.Dying;
            return LampState.Off;
        }

        /// <summary>형광등 기구 + 라이트 + 깜빡임까지 한번에.</summary>
        public static GameObject Fixture(Transform p, string name, Vector3 pos, Vector3 size, LampState state, Color lightColor,
                                         float intensity, float range, bool shadows = false, string onMat = "LampOn", Vector3? lightOffset = null)
        {
            bool lit = state != LampState.Off;
            var g = Box(p, name, pos, size, M(lit ? onMat : "LampOff"), false);
            g.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            if (!lit) return g;
            var l = PointLight(g.transform, "Light", lightOffset ?? new Vector3(0f, -0.35f, 0f), lightColor, intensity, range, shadows);
            if (state == LampState.On) return g;
            var mode = state == LampState.Flicker ? BJ_FlickerLight.Mode.Flicker : state == LampState.Dying ? BJ_FlickerLight.Mode.Dying : BJ_FlickerLight.Mode.Buzz;
            Color emi = M(onMat).GetColor("_EmissionColor");
            Flicker(g, mode, emi, new[] { l }, g.GetComponent<MeshRenderer>());
            return g;
        }

        // ------------------------------------------------------------------ static flags
        static void MarkStatic(GameObject root)
        {
            var skip = new HashSet<GameObject>(dynamicObjects);
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (skip.Contains(t.gameObject)) continue;
                if (t.GetComponent<Light>() != null) continue;
                GameObjectUtility.SetStaticEditorFlags(t.gameObject,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
            }
        }
    }
}
