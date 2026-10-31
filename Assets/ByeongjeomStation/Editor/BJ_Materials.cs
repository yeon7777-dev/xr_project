using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Byeongjeom.MapGen
{
    public static partial class BJ
    {
        static Dictionary<string, Material> mats = new Dictionary<string, Material>();
        static readonly HashSet<Material> twoSided = new HashSet<Material>();
        static Shader litShader;
        /// <summary>true = URP(Lit 셰이더), false = Built-in(Standard 셰이더)</summary>
        public static bool IsURP;

        /// <summary>로드된 어셈블리에서 타입을 이름으로 찾는다 (패키지가 없으면 null).</summary>
        public static System.Type FindType(string fullName)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                System.Type t = null;
                try { t = asm.GetType(fullName, false); } catch { }
                if (t != null) return t;
            }
            return null;
        }

        public static Material M(string name)
        {
            Material m;
            if (mats.TryGetValue(name, out m)) return m;
            Debug.LogWarning("[병점역] 머티리얼 없음: " + name + " (기본 회색으로 대체)");
            m = NewLit(name, new Color(0.5f, 0.5f, 0.5f), 0.3f);
            return Save(m);
        }

        static Texture2D LoadTex(string rel, bool warn = true)
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/" + rel);
            if (t == null && warn) Debug.LogWarning("[병점역] 텍스처를 찾을 수 없음: " + TexDir + "/" + rel);
            return t;
        }

        static Material NewLit(string name, Color color, float smooth, float metal = 0f)
        {
            var m = new Material(litShader) { name = name };
            m.SetColor("_BaseColor", color);
            m.SetColor("_Color", color);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Glossiness", smooth);
            m.SetFloat("_Metallic", metal);
            m.enableInstancing = true;
            return m;
        }

        static Material Save(Material m)
        {
            AssetDatabase.CreateAsset(m, MatDir + "/" + m.name + ".mat");
            mats[m.name] = m;
            return m;
        }

        static void SetBase(Material m, Texture2D tex, float tilingMeters)
        {
            if (tex == null) return;
            m.SetTexture("_BaseMap", tex);
            m.SetTexture("_MainTex", tex);
            var s = new Vector2(1f / tilingMeters, 1f / tilingMeters);
            m.SetTextureScale("_BaseMap", s);
            m.SetTextureScale("_MainTex", s);
        }

        static void SetNormal(Material m, Texture2D n, float strength)
        {
            if (n == null) return;
            m.SetTexture("_BumpMap", n);
            m.SetFloat("_BumpScale", strength);
            m.EnableKeyword("_NORMALMAP");
        }

        static void SetEmission(Material m, Color c, Texture2D map = null)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c);
            if (map != null) m.SetTexture("_EmissionMap", map);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        static void MakeTransparent(Material m)
        {
            if (!IsURP)
            {
                // Standard 셰이더 Fade 모드
                m.SetFloat("_Mode", 2f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.DisableKeyword("_ALPHATEST_ON");
                m.EnableKeyword("_ALPHABLEND_ON");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                m.renderQueue = (int)RenderQueue.Transparent;
                m.SetShaderPassEnabled("ShadowCaster", false);
                return;
            }
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_BlendModePreserveSpecular", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetShaderPassEnabled("DepthOnly", false);
            m.SetShaderPassEnabled("ShadowCaster", false);
        }

        static void MakeCutout(Material m, float cutoff)
        {
            m.SetFloat("_Mode", 1f);
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", cutoff);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "TransparentCutout");
            m.renderQueue = (int)RenderQueue.AlphaTest;
        }

        /// <summary>타일링 표면 머티리얼. tex = Surfaces 폴더의 파일 이름(확장자 제외).</summary>
        static Material Surf(string name, string tex, float meters, Color tint, float smooth, float metal = 0f, float bump = 1f)
        {
            var m = NewLit(name, tint, smooth, metal);
            var t = LoadTex("Surfaces/" + tex + ".jpg", false) ?? LoadTex("Surfaces/" + tex + ".png");
            SetBase(m, t, meters);
            SetNormal(m, LoadTex("Surfaces/" + tex + "_N.png", false), bump);
            return Save(m);
        }

        static Material Plain(string name, Color c, float smooth, float metal = 0f)
        {
            return Save(NewLit(name, c, smooth, metal));
        }

        static Material Emissive(string name, Color baseCol, Color emission)
        {
            var m = NewLit(name, baseCol, 0.5f);
            SetEmission(m, emission);
            return Save(m);
        }

        static Material Glass(string name, string tex, float meters, Color tint, float smooth = 0.92f)
        {
            var m = NewLit(name, tint, smooth, 0.1f);
            SetBase(m, LoadTex("Surfaces/" + tex + ".png"), meters);
            MakeTransparent(m);
            return Save(m);
        }

        /// <summary>표지판 머티리얼 (Signs 폴더). glow &gt; 0 이면 라이트박스처럼 은은하게 발광.</summary>
        public static Material SignMat(string tex, float glow)
        {
            string key = "Sign_" + tex + (glow > 0f ? "_G" + Mathf.RoundToInt(glow * 100) : "");
            Material m;
            if (mats.TryGetValue(key, out m)) return m;
            m = NewLit(key, Color.white, 0.35f);
            var t = LoadTex("Signs/" + tex + ".png");
            SetBase(m, t, 1f);
            if (glow > 0f) SetEmission(m, new Color(glow, glow, glow), t);
            var ti = AssetImporter.GetAtPath(TexDir + "/Signs/" + tex + ".png") as TextureImporter;
            if (ti != null && ti.DoesSourceTextureHaveAlpha()) MakeCutout(m, 0.4f);
            return Save(m);
        }

        /// <summary>데칼 머티리얼 (Decals 폴더, 반투명).</summary>
        public static Material DecalMat(string tex, float smooth)
        {
            string key = "Decal_" + tex + "_" + Mathf.RoundToInt(smooth * 100);
            Material m;
            if (mats.TryGetValue(key, out m)) return m;
            m = NewLit(key, Color.white, smooth);
            SetBase(m, LoadTex("Decals/" + tex + ".png"), 1f);
            MakeTransparent(m);
            m.renderQueue = (int)RenderQueue.Transparent - 50;
            return Save(m);
        }

        // ------------------------------------------------------------------ palette
        static readonly Color CoolWhite = new Color(0.82f, 0.92f, 1f);
        static readonly Color WarmWhite = new Color(1f, 0.92f, 0.78f);
        static readonly Color Sodium = new Color(1f, 0.62f, 0.28f);
        static readonly Color SickGreen = new Color(0.75f, 1f, 0.82f);

        static void CreateMaterials()
        {
            twoSided.Clear();
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            IsURP = GraphicsSettings.currentRenderPipeline != null && urpLit != null;
            litShader = IsURP ? urpLit : Shader.Find("Standard");
            if (litShader == null) throw new System.Exception("사용할 셰이더(URP Lit 또는 Standard)를 찾을 수 없습니다.");
            Debug.Log("[병점역] 렌더 파이프라인: " + (IsURP ? "URP (Lit 셰이더)" : "Built-in (Standard 셰이더)"));

            // 바닥/구조
            Surf("FloorTile", "T_FloorTile", 2.4f, Color.white, 0.5f, 0f, 0.8f);
            Surf("PlatformFloor", "T_PlatformFloor", 3f, Color.white, 0.2f, 0f, 1f);
            Surf("Tactile", "T_Tactile", 0.6f, Color.white, 0.25f, 0f, 1.2f);
            Surf("TactileLine", "T_TactileLine", 0.6f, Color.white, 0.25f, 0f, 1.2f);
            Surf("Concrete", "T_Concrete", 4f, Color.white, 0.1f);
            Surf("ConcreteDark", "T_Concrete", 4f, new Color(0.55f, 0.55f, 0.55f), 0.1f);
            Surf("ConcreteLight", "T_ConcreteLight", 2f, Color.white, 0.15f);
            Surf("Coping", "T_ConcreteLight", 1f, new Color(0.95f, 0.95f, 0.92f), 0.2f);
            Surf("WallPanel", "T_WallPanel", 3f, Color.white, 0.35f, 0.1f);
            Surf("Cladding", "T_WallPanel", 3f, new Color(0.72f, 0.76f, 0.8f), 0.4f, 0.2f);
            Surf("WhiteTile", "T_WhiteTile", 1.2f, Color.white, 0.65f);
            Surf("Ceiling", "T_CeilingPanel", 2.4f, Color.white, 0.1f);
            Surf("Ballast", "T_Ballast", 2f, Color.white, 0.05f, 0f, 1.3f);
            Surf("Sleeper", "T_ConcreteLight", 1f, new Color(0.8f, 0.78f, 0.74f), 0.1f);
            Surf("Rail", "T_RustMetal", 1f, new Color(0.65f, 0.6f, 0.55f), 0.35f, 0.6f);
            Plain("RailTop", new Color(0.5f, 0.5f, 0.52f), 0.7f, 1f);
            Surf("RustMetal", "T_RustMetal", 2f, Color.white, 0.25f, 0.4f);
            Surf("PaintedMetal", "T_PaintedMetal", 2f, new Color(0.78f, 0.8f, 0.84f), 0.35f, 0.3f);
            Surf("SteelBlue", "T_PaintedMetal", 2f, new Color(0.38f, 0.48f, 0.62f), 0.35f, 0.3f);
            Surf("SteelGreen", "T_PaintedMetal", 2f, new Color(0.35f, 0.48f, 0.4f), 0.3f, 0.3f);
            Surf("DarkMetal", "T_PaintedMetal", 2f, new Color(0.22f, 0.23f, 0.25f), 0.35f, 0.5f);
            Surf("Asphalt", "T_Asphalt", 4f, Color.white, 0.18f);
            Surf("Sidewalk", "T_Sidewalk", 2.4f, Color.white, 0.15f);
            Surf("Dirt", "T_Dirt", 4f, Color.white, 0.05f);
            Surf("Wood", "T_Wood", 1.2f, Color.white, 0.15f);
            Surf("Stainless", "T_Stainless", 1f, Color.white, 0.55f, 0.75f);
            Surf("StainlessDark", "T_Stainless", 1f, new Color(0.45f, 0.46f, 0.48f), 0.5f, 0.7f);
            Surf("TrainFloor", "T_TrainFloor", 1f, Color.white, 0.3f);
            Surf("SeatFabric", "T_SeatFabric", 0.5f, Color.white, 0.1f);
            Surf("Hazard", "T_Hazard", 1f, Color.white, 0.3f);
            Surf("Facade", "T_BuildingFacade", 6f, Color.white, 0.2f);
            Surf("FacadeDark", "T_BuildingFacade", 6f, new Color(0.6f, 0.6f, 0.62f), 0.2f);
            Surf("Shutter", "T_Shutter", 1f, Color.white, 0.35f, 0.5f);
            Surf("Brick", "T_Brick", 2f, Color.white, 0.1f);
            var chain = NewLit("ChainLink", Color.white, 0.4f, 0.6f);
            SetBase(chain, LoadTex("Surfaces/T_ChainLink.png"), 1f);
            MakeCutout(chain, 0.45f);
            chain.SetFloat("_Cull", 0f);
            Save(chain);
            twoSided.Add(chain);

            // 유리
            Glass("Glass", "T_Glass", 2f, new Color(0.75f, 0.85f, 0.88f, 1f));
            Glass("GlassDark", "T_Glass", 2f, new Color(0.25f, 0.3f, 0.32f, 1f));
            var broken = Glass("GlassBroken", "T_GlassBroken", 1f, new Color(0.8f, 0.88f, 0.9f, 1f));
            broken.SetFloat("_Cull", 0f);
            twoSided.Add(broken);
            Plain("Mirror", new Color(0.7f, 0.72f, 0.74f), 0.95f, 1f);

            // 단색
            Plain("Black", new Color(0.02f, 0.02f, 0.02f), 0.3f);
            Plain("Rubber", new Color(0.03f, 0.03f, 0.03f), 0.35f);
            Plain("DarkGray", new Color(0.09f, 0.09f, 0.1f), 0.3f);
            Plain("MidGray", new Color(0.3f, 0.3f, 0.31f), 0.3f);
            Plain("LightGray", new Color(0.62f, 0.62f, 0.6f), 0.35f);
            Plain("SignFrame", new Color(0.16f, 0.17f, 0.19f), 0.5f, 0.6f);
            Plain("Porcelain", new Color(0.86f, 0.86f, 0.84f), 0.85f);
            Plain("Line1Blue", new Color(0.0f, 0.24f, 0.55f), 0.4f, 0.2f);
            Plain("SkyBlue", new Color(0.3f, 0.6f, 0.85f), 0.4f, 0.2f);
            Plain("Red", new Color(0.5f, 0.04f, 0.04f), 0.4f);
            Plain("FireRed", new Color(0.65f, 0.05f, 0.04f), 0.5f, 0.2f);
            Plain("Yellow", new Color(0.85f, 0.65f, 0.08f), 0.4f);
            Plain("Orange", new Color(0.85f, 0.3f, 0.05f), 0.4f);
            Plain("Cardboard", new Color(0.42f, 0.31f, 0.19f), 0.1f);
            Plain("TrashBag", new Color(0.03f, 0.03f, 0.035f), 0.75f);
            Plain("Bark", new Color(0.16f, 0.13f, 0.1f), 0.1f);
            Plain("DeadGrass", new Color(0.3f, 0.26f, 0.15f), 0.05f);
            Plain("Plastic", new Color(0.75f, 0.75f, 0.72f), 0.4f);
            Plain("PlasticBlue", new Color(0.12f, 0.25f, 0.45f), 0.4f);
            Plain("Sofa", new Color(0.18f, 0.12f, 0.1f), 0.2f);
            Plain("Paper", new Color(0.8f, 0.78f, 0.7f), 0.1f);
            Plain("Puddle", new Color(0.02f, 0.02f, 0.02f), 0.97f);
            Plain("CarWhite", new Color(0.7f, 0.7f, 0.68f), 0.6f, 0.3f);
            Plain("CarSilver", new Color(0.45f, 0.46f, 0.47f), 0.65f, 0.6f);
            Plain("CarBlack", new Color(0.03f, 0.03f, 0.035f), 0.7f, 0.4f);
            Plain("CarBlue", new Color(0.06f, 0.12f, 0.25f), 0.65f, 0.4f);
            Plain("CarRed", new Color(0.3f, 0.03f, 0.03f), 0.6f, 0.4f);
            Plain("BusGreen", new Color(0.1f, 0.35f, 0.2f), 0.55f, 0.3f);
            Plain("BusBlue", new Color(0.08f, 0.25f, 0.55f), 0.55f, 0.3f);
            Plain("MaintYellow", new Color(0.8f, 0.6f, 0.05f), 0.4f, 0.2f);

            // 발광 (조명/신호)
            Emissive("LampOn", new Color(0.9f, 0.9f, 0.9f), new Color(2.4f, 2.6f, 2.8f));
            Emissive("LampWarm", new Color(0.9f, 0.85f, 0.75f), new Color(2.6f, 2.3f, 1.8f));
            Emissive("LampSodium", new Color(0.9f, 0.7f, 0.4f), new Color(3.2f, 1.8f, 0.7f));
            Emissive("LampSick", new Color(0.8f, 0.9f, 0.8f), new Color(1.6f, 2.4f, 1.8f));
            Plain("LampOff", new Color(0.55f, 0.56f, 0.55f), 0.7f);
            Emissive("SignalRed", new Color(0.3f, 0f, 0f), new Color(6f, 0.15f, 0.05f));
            Emissive("SignalYellow", new Color(0.3f, 0.2f, 0f), new Color(5f, 2.8f, 0.2f));
            Emissive("EmergencyRed", new Color(0.3f, 0f, 0f), new Color(3f, 0.1f, 0.05f));
            Emissive("ScreenStatic", Color.white, new Color(0.5f, 0.55f, 0.6f));
            var staticTex = LoadTex("Surfaces/T_Static.png");
            SetBase(mats["ScreenStatic"], staticTex, 1f);
            mats["ScreenStatic"].SetTexture("_EmissionMap", staticTex);
            Emissive("FridgeGlow", new Color(0.6f, 0.7f, 0.7f), new Color(0.5f, 0.8f, 0.85f));
        }
    }
}
