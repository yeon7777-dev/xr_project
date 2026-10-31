using UnityEngine;
using UnityEngine.Rendering;
using Byeongjeom;

namespace Byeongjeom.MapGen
{
    /// <summary>재사용 소품 (벤치, 쓰레기통, 차량, 가로등, 고사목 ...). 모두 로컬 좌표 기준.</summary>
    public static partial class BJ
    {
        static readonly string[] FloorJunk = { "D_Papers", "D_Grime", "D_Leaves", "D_Grime", "D_Papers" };

        public static void Bench(Transform p, Vector3 pos, float yaw, bool fallen = false, string mat = "StainlessDark")
        {
            var g = Group("Bench", p, pos, yaw);
            if (fallen)
            {
                g.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(-95f, 0f, 0f);
                g.localPosition = pos + Vector3.up * 0.25f;
            }
            Box(g, "Seat", new Vector3(0f, 0.45f, 0f), new Vector3(1.8f, 0.05f, 0.45f), M(mat));
            Box(g, "Back", new Vector3(0f, 0.78f, -0.21f), new Vector3(1.8f, 0.5f, 0.04f), M(mat), true, new Vector3(-10f, 0f, 0f));
            for (int i = -1; i <= 1; i += 2)
            {
                Box(g, "Leg", new Vector3(i * 0.8f, 0.22f, 0f), new Vector3(0.06f, 0.44f, 0.4f), M("DarkMetal"), false);
                Box(g, "Arm", new Vector3(i * 0.88f, 0.62f, 0f), new Vector3(0.05f, 0.04f, 0.42f), M("DarkMetal"), false);
            }
        }

        public static void TrashBin(Transform p, Vector3 pos, bool tipped = false)
        {
            var g = Group("TrashBin", p, pos, R(0f, 360f));
            if (tipped)
            {
                g.localRotation = Quaternion.Euler(0f, R(0f, 360f), 90f);
                g.localPosition = pos + Vector3.up * 0.26f;
                FloorDecal(p, "D_Papers", pos + new Vector3(R(-0.6f, 0.6f), 0.01f, R(-0.6f, 0.6f)), 1.4f);
            }
            Cyl(g, "Body", Vector3.zero, new Vector3(0f, 0.85f, 0f), 0.5f, M("Stainless"), true);
            Cyl(g, "Lid", new Vector3(0f, 0.85f, 0f), new Vector3(0f, 0.95f, 0f), 0.54f, M("DarkMetal"));
        }

        public static void TrashBags(Transform p, Vector3 c, int count)
        {
            var g = Group("TrashBags", p, c);
            for (int i = 0; i < count; i++)
            {
                var s = new Vector3(R(0.45f, 0.75f), R(0.35f, 0.55f), R(0.45f, 0.75f));
                Sphere(g, "Bag", new Vector3(R(-0.6f, 0.6f), s.y * 0.45f, R(-0.6f, 0.6f)), s, M("TrashBag"));
            }
        }

        public static void Boxes(Transform p, Vector3 c, int count)
        {
            var g = Group("Boxes", p, c, R(0f, 360f));
            float y = 0f;
            for (int i = 0; i < count; i++)
            {
                var s = new Vector3(R(0.35f, 0.7f), R(0.25f, 0.5f), R(0.35f, 0.7f));
                bool stack = i > 0 && Chance(0.4f);
                var pos = stack ? new Vector3(R(-0.1f, 0.1f), y + s.y * 0.5f, R(-0.1f, 0.1f)) : new Vector3(R(-0.8f, 0.8f), s.y * 0.5f, R(-0.8f, 0.8f));
                Box(g, "Box", pos, s, M("Cardboard"), true, new Vector3(0f, R(-30f, 30f), 0f));
                y = stack ? y + s.y : s.y;
            }
        }

        public static void Scatter(Transform p, Vector3 min, Vector3 max, int count, params string[] tex)
        {
            if (tex == null || tex.Length == 0) tex = FloorJunk;
            for (int i = 0; i < count; i++)
            {
                var pos = new Vector3(R(min.x, max.x), max.y, R(min.z, max.z));
                FloorDecal(p, Pick(tex), pos, R(0.8f, 2.2f));
            }
        }

        public static void WetFloorSign(Transform p, Vector3 pos, float yaw)
        {
            var g = Group("WetFloorSign", p, pos, yaw);
            Box(g, "A", new Vector3(0f, 0.32f, 0.1f), new Vector3(0.3f, 0.65f, 0.02f), M("Yellow"), false, new Vector3(-12f, 0f, 0f));
            Box(g, "B", new Vector3(0f, 0.32f, -0.1f), new Vector3(0.3f, 0.65f, 0.02f), M("Yellow"), false, new Vector3(12f, 0f, 0f));
        }

        public static void Barricade(Transform p, Vector3 pos, float yaw, bool knocked = false)
        {
            var g = Group("Barricade", p, pos, yaw);
            if (knocked) { g.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(80f, 0f, 0f); g.localPosition = pos + Vector3.up * 0.15f; }
            Box(g, "PanelTop", new Vector3(0f, 0.95f, 0f), new Vector3(2.0f, 0.25f, 0.04f), M("Hazard"), false);
            Box(g, "PanelLow", new Vector3(0f, 0.55f, 0f), new Vector3(2.0f, 0.18f, 0.04f), M("Hazard"), false);
            for (int i = -1; i <= 1; i += 2)
            {
                Box(g, "LegA", new Vector3(i * 0.9f, 0.55f, 0.12f), new Vector3(0.05f, 1.1f, 0.05f), M("DarkMetal"), false, new Vector3(12f, 0f, 0f));
                Box(g, "LegB", new Vector3(i * 0.9f, 0.55f, -0.12f), new Vector3(0.05f, 1.1f, 0.05f), M("DarkMetal"), false, new Vector3(-12f, 0f, 0f));
            }
            var col = g.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.6f, 0f);
            col.size = new Vector3(2.05f, 1.2f, 0.45f);
        }

        public static void Cone(Transform p, Vector3 pos)
        {
            var g = Group("Cone", p, pos, R(0f, 90f));
            Box(g, "Base", new Vector3(0f, 0.02f, 0f), new Vector3(0.38f, 0.04f, 0.38f), M("Black"), false);
            Cyl(g, "Body", new Vector3(0f, 0.04f, 0f), new Vector3(0f, 0.7f, 0f), 0.22f, M("Orange"));
            Cyl(g, "Stripe", new Vector3(0f, 0.42f, 0f), new Vector3(0f, 0.52f, 0f), 0.235f, M("LightGray"));
        }

        public static void Tape(Transform p, Vector3 a, Vector3 b)
        {
            Vector3 d = b - a; float len = d.magnitude;
            var g = Go("CautionTape", p);
            g.transform.localPosition = (a + b) * 0.5f + Vector3.down * 0.05f;
            g.transform.localRotation = Quaternion.LookRotation(d / len, Vector3.up);
            g.AddComponent<MeshFilter>().sharedMesh = BoxMesh(new Vector3(0.005f, 0.07f, len));
            var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = M("Hazard"); r.shadowCastingMode = ShadowCastingMode.Off;
        }

        public static void StreetLight(Transform p, Vector3 pos, float yaw, LampState state, bool shadows = false)
        {
            var g = Group("StreetLight", p, pos, yaw);
            Cyl(g, "Pole", Vector3.zero, new Vector3(0f, 8f, 0f), 0.2f, M("DarkMetal"), true);
            Cyl(g, "Arm", new Vector3(0f, 7.8f, 0f), new Vector3(0f, 8.05f, 1.9f), 0.1f, M("DarkMetal"));
            var head = Box(g, "Head", new Vector3(0f, 7.95f, 2.0f), new Vector3(0.38f, 0.16f, 0.75f), M("DarkMetal"), false);
            bool lit = state != LampState.Off;
            var lens = Box(g, "Lens", new Vector3(0f, 7.86f, 2.0f), new Vector3(0.3f, 0.03f, 0.6f), M(lit ? "LampSodium" : "LampOff"), false);
            lens.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            if (!lit) return;
            var l = SpotLight(g, "Light", new Vector3(0f, 7.7f, 2.0f), Vector3.down, Sodium, 4.5f, 20f, 110f, shadows);
            if (state != LampState.On)
                Flicker(head, state == LampState.Flicker ? BJ_FlickerLight.Mode.Flicker : state == LampState.Dying ? BJ_FlickerLight.Mode.Dying : BJ_FlickerLight.Mode.Buzz,
                        M("LampSodium").GetColor("_EmissionColor"), new[] { l }, lens.GetComponent<MeshRenderer>());
        }

        public static void DeadTree(Transform p, Vector3 pos, float scale = 1f)
        {
            var g = Group("DeadTree", p, pos, R(0f, 360f));
            g.localScale = Vector3.one * scale;
            float h = R(3.2f, 4.5f);
            var trunk = Cyl(g, "Trunk", Vector3.zero, new Vector3(R(-0.2f, 0.2f), h, R(-0.2f, 0.2f)), 0.28f, M("Bark"));
            var cc = g.gameObject.AddComponent<CapsuleCollider>();
            cc.center = new Vector3(0f, h * 0.5f, 0f); cc.height = h; cc.radius = 0.2f;
            int n = RI(5, 9);
            for (int i = 0; i < n; i++)
            {
                float y = R(h * 0.45f, h * 0.95f);
                float ang = R(0f, Mathf.PI * 2f);
                Vector3 a = new Vector3(0f, y, 0f);
                Vector3 b = a + new Vector3(Mathf.Cos(ang) * R(0.8f, 1.8f), R(0.6f, 1.6f), Mathf.Sin(ang) * R(0.8f, 1.8f));
                Cyl(g, "Branch", a, b, R(0.06f, 0.12f), M("Bark"));
                for (int k = 0; k < 2; k++)
                {
                    float a2 = ang + R(-1f, 1f);
                    Vector3 c = b + new Vector3(Mathf.Cos(a2) * R(0.4f, 0.9f), R(0.2f, 0.8f), Mathf.Sin(a2) * R(0.4f, 0.9f));
                    Cyl(g, "Twig", b, c, 0.04f, M("Bark"));
                }
            }
            if (trunk != null) trunk.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;
        }

        public static void Planter(Transform p, Vector3 c, Vector2 size, int trees)
        {
            var g = Group("Planter", p, c);
            float hx = size.x * 0.5f, hz = size.y * 0.5f;
            Box(g, "WallN", new Vector3(0f, 0.25f, hz), new Vector3(size.x + 0.3f, 0.5f, 0.3f), M("ConcreteLight"));
            Box(g, "WallS", new Vector3(0f, 0.25f, -hz), new Vector3(size.x + 0.3f, 0.5f, 0.3f), M("ConcreteLight"));
            Box(g, "WallE", new Vector3(hx, 0.25f, 0f), new Vector3(0.3f, 0.5f, size.y), M("ConcreteLight"));
            Box(g, "WallW", new Vector3(-hx, 0.25f, 0f), new Vector3(0.3f, 0.5f, size.y), M("ConcreteLight"));
            Box(g, "Soil", new Vector3(0f, 0.2f, 0f), new Vector3(size.x, 0.4f, size.y), M("Dirt"));
            for (int i = 0; i < trees; i++)
                DeadTree(g, new Vector3(R(-hx + 0.6f, hx - 0.6f), 0.4f, R(-hz + 0.6f, hz - 0.6f)), R(0.9f, 1.25f));
            Decal(g, "D_Leaves", new Vector3(0f, 0.41f, 0f), new Vector2(size.x * 0.8f, size.y * 0.8f), Vector3.up, R(0f, 360f));
        }

        public static void Car(Transform p, Vector3 pos, float yaw, string paint, bool hazard = false, bool doorOpen = false)
        {
            var g = Group("Car_" + paint, p, pos, yaw);
            g.localRotation = Quaternion.Euler(R(-1.5f, 1.5f), yaw, R(-2.5f, 2.5f));
            Box(g, "Body", new Vector3(0f, 0.72f, 0f), new Vector3(1.8f, 0.72f, 4.5f), M(paint));
            Box(g, "Cabin", new Vector3(0f, 1.36f, -0.25f), new Vector3(1.62f, 0.58f, 2.4f), M(paint));
            Box(g, "Windows", new Vector3(0f, 1.38f, -0.25f), new Vector3(1.64f, 0.4f, 2.2f), M("CarBlack"), false);
            Box(g, "Windshield", new Vector3(0f, 1.36f, 0.97f), new Vector3(1.5f, 0.5f, 0.05f), M("CarBlack"), false, new Vector3(-35f, 0f, 0f));
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    Cyl(g, "Wheel", new Vector3(sx * 0.82f, 0.34f, sz * 1.45f), new Vector3(sx * 1.0f, 0.34f, sz * 1.45f), 0.68f, M("Rubber"));
            Box(g, "HeadL", new Vector3(-0.6f, 0.82f, 2.25f), new Vector3(0.35f, 0.12f, 0.03f), M("LampOff"), false);
            Box(g, "HeadR", new Vector3(0.6f, 0.82f, 2.25f), new Vector3(0.35f, 0.12f, 0.03f), M("LampOff"), false);
            var t1 = Box(g, "TailL", new Vector3(-0.65f, 0.86f, -2.26f), new Vector3(0.3f, 0.12f, 0.03f), M(hazard ? "SignalYellow" : "Red"), false);
            var t2 = Box(g, "TailR", new Vector3(0.65f, 0.86f, -2.26f), new Vector3(0.3f, 0.12f, 0.03f), M(hazard ? "SignalYellow" : "Red"), false);
            if (doorOpen)
                Box(g, "Door", new Vector3(1.25f, 0.95f, 0.75f), new Vector3(0.06f, 0.9f, 1.1f), M(paint), true, new Vector3(0f, 55f, 0f));
            if (hazard)
            {
                var l = PointLight(g, "HazardLight", new Vector3(0f, 0.9f, -2.6f), new Color(1f, 0.55f, 0.1f), 1.5f, 6f);
                var f = Flicker(g.gameObject, BJ_FlickerLight.Mode.Strobe, M("SignalYellow").GetColor("_EmissionColor"), new[] { l },
                                t1.GetComponent<MeshRenderer>(), t2.GetComponent<MeshRenderer>());
                f.strobePeriod = 1.0f; f.speed = 1f;
            }
        }

        public static void Bus(Transform p, Vector3 pos, float yaw, string paint, float tilt = 0f)
        {
            var g = Group("Bus", p, pos, yaw);
            g.localRotation = Quaternion.Euler(0f, yaw, tilt);
            Box(g, "Body", new Vector3(0f, 1.85f, 0f), new Vector3(2.5f, 2.9f, 11f), M(paint));
            Box(g, "Windows", new Vector3(0f, 2.45f, 0.3f), new Vector3(2.52f, 1.05f, 9.6f), M("CarBlack"), false);
            Box(g, "Front", new Vector3(0f, 2.1f, 5.51f), new Vector3(2.3f, 1.6f, 0.02f), M("CarBlack"), false);
            Box(g, "Roof", new Vector3(0f, 3.35f, 0f), new Vector3(2.3f, 0.1f, 10.5f), M("LightGray"), false);
            for (int sx = -1; sx <= 1; sx += 2)
                foreach (float z in new[] { -3.6f, 3.4f })
                    Cyl(g, "Wheel", new Vector3(sx * 1.05f, 0.5f, z), new Vector3(sx * 1.3f, 0.5f, z), 1.0f, M("Rubber"));
        }

        public static void BusShelter(Transform p, Vector3 pos, float yaw, LampState adLight)
        {
            var g = Group("BusShelter", p, pos, yaw);
            Box(g, "Roof", new Vector3(0f, 2.65f, 0f), new Vector3(4.2f, 0.12f, 1.7f), M("DarkMetal"));
            foreach (float x in new[] { -2f, 2f })
                Box(g, "Post", new Vector3(x, 1.3f, -0.7f), new Vector3(0.1f, 2.6f, 0.1f), M("DarkMetal"));
            Box(g, "BackGlass", new Vector3(0f, 1.4f, -0.72f), new Vector3(3.9f, 2.1f, 0.03f), M("Glass"));
            Box(g, "SideGlass", new Vector3(-2.02f, 1.4f, -0.1f), new Vector3(0.03f, 2.1f, 1.2f), M("Glass"));
            Bench(g, new Vector3(0.3f, 0f, -0.35f), 0f);
            // 광고 라이트박스
            var box = Box(g, "AdBox", new Vector3(2.02f, 1.35f, -0.1f), new Vector3(0.18f, 1.9f, 1.25f), M("DarkMetal"));
            var ad = Quad(g, "Ad", new Vector3(2.12f, 1.35f, -0.1f), new Vector2(1.1f, 1.5f), Vector3.right, SignMat("S_Ad1", adLight == LampState.Off ? 0f : 0.9f));
            Quad(g, "Stop", new Vector3(1.92f, 1.35f, -0.1f), new Vector2(0.75f, 1.5f), Vector3.left, SignMat("S_BusStop", 0f));
            if (adLight != LampState.Off)
            {
                var l = PointLight(g, "AdLight", new Vector3(2.6f, 1.4f, -0.1f), new Color(1f, 0.95f, 0.85f), 1.2f, 5f);
                if (adLight != LampState.On) Flicker(box, BJ_FlickerLight.Mode.Buzz, new Color(0.9f, 0.9f, 0.9f), new[] { l }, ad.GetComponent<MeshRenderer>());
            }
        }

        public static void VendingMachine(Transform p, Vector3 pos, float yaw, bool lit)
        {
            var g = Group("VendingMachine", p, pos, yaw);
            var body = Box(g, "Body", new Vector3(0f, 0.92f, 0f), new Vector3(1.0f, 1.85f, 0.75f), M("FireRed"));
            var face = Quad(g, "Front", new Vector3(0f, 0.92f, 0.38f), new Vector2(0.95f, 1.8f), Vector3.forward, SignMat("S_Vending", lit ? 0.9f : 0f));
            if (!lit) return;
            var l = PointLight(g, "Glow", new Vector3(0f, 1.2f, 0.9f), new Color(1f, 0.85f, 0.8f), 1.3f, 4.5f);
            Flicker(body, BJ_FlickerLight.Mode.Buzz, new Color(0.9f, 0.9f, 0.9f), new[] { l }, face.GetComponent<MeshRenderer>());
        }

        public static void ClockPole(Transform p, Vector3 pos, float yaw)
        {
            var g = Group("ClockPole", p, pos, yaw);
            Cyl(g, "Pole", Vector3.zero, new Vector3(0f, 4.2f, 0f), 0.2f, M("DarkMetal"), true);
            Box(g, "Case", new Vector3(0f, 4.6f, 0f), new Vector3(0.95f, 0.95f, 0.25f), M("DarkMetal"), false);
            Quad(g, "FaceA", new Vector3(0f, 4.6f, 0.13f), new Vector2(0.85f, 0.85f), Vector3.forward, SignMat("S_Clock", 0f));
            Quad(g, "FaceB", new Vector3(0f, 4.6f, -0.13f), new Vector2(0.85f, 0.85f), Vector3.back, SignMat("S_Clock", 0f));
        }

        public static void UtilityPole(Transform p, Vector3 pos, float yaw)
        {
            var g = Group("UtilityPole", p, pos, yaw);
            Cyl(g, "Pole", Vector3.zero, new Vector3(0f, 10f, 0f), 0.32f, M("ConcreteLight"), true);
            Box(g, "Arm", new Vector3(0f, 9.4f, 0f), new Vector3(2.4f, 0.12f, 0.12f), M("DarkMetal"), false);
            Cyl(g, "Transformer", new Vector3(0.3f, 7.2f, 0.3f), new Vector3(0.3f, 8.3f, 0.3f), 0.5f, M("MidGray"));
        }

        public static void Dumpster(Transform p, Vector3 pos, float yaw)
        {
            var g = Group("Dumpster", p, pos, yaw);
            Box(g, "Body", new Vector3(0f, 0.6f, 0f), new Vector3(1.9f, 1.1f, 1.1f), M("SteelGreen"));
            Box(g, "Lid", new Vector3(0f, 1.3f, -0.35f), new Vector3(1.9f, 0.05f, 1.1f), M("DarkMetal"), false, new Vector3(-40f, 0f, 0f));
            TrashBags(p, pos + Quaternion.Euler(0f, yaw, 0f) * new Vector3(1.4f, 0f, 0.4f), 3);
        }

        public static void Bike(Transform p, Vector3 pos, float yaw, bool fallen)
        {
            var g = Group("Bike", p, pos, yaw);
            if (fallen) { g.localRotation = Quaternion.Euler(0f, yaw, 85f); g.localPosition = pos + Vector3.up * 0.05f; }
            Cyl(g, "WheelF", new Vector3(-0.02f, 0.33f, 0.55f), new Vector3(0.02f, 0.33f, 0.55f), 0.66f, M("Rubber"));
            Cyl(g, "WheelR", new Vector3(-0.02f, 0.33f, -0.5f), new Vector3(0.02f, 0.33f, -0.5f), 0.66f, M("Rubber"));
            Cyl(g, "Frame", new Vector3(0f, 0.33f, -0.5f), new Vector3(0f, 0.75f, 0.35f), 0.04f, M("RustMetal"));
            Cyl(g, "Frame2", new Vector3(0f, 0.33f, 0.55f), new Vector3(0f, 0.95f, 0.4f), 0.04f, M("RustMetal"));
            Box(g, "Seat", new Vector3(0f, 0.85f, -0.25f), new Vector3(0.1f, 0.05f, 0.25f), M("Black"), false);
        }

        public static void FireCabinet(Transform p, Vector3 pos, Vector3 facing)
        {
            var g = Go("FireCabinet", p);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.LookRotation(facing, Vector3.up);
            Box(g.transform, "Box", new Vector3(0f, 0f, 0.12f), new Vector3(0.6f, 0.9f, 0.24f), M("FireRed"), false);
        }

        public static void Cctv(Transform p, Vector3 pos, float yaw)
        {
            var g = Group("CCTV", p, pos, yaw);
            Box(g, "Mount", new Vector3(0f, 0.1f, 0f), new Vector3(0.06f, 0.2f, 0.06f), M("DarkMetal"), false);
            Box(g, "Cam", new Vector3(0f, -0.02f, 0.12f), new Vector3(0.12f, 0.12f, 0.32f), M("LightGray"), false, new Vector3(20f, 0f, 0f));
            Box(g, "Led", new Vector3(0f, -0.05f, 0.29f), new Vector3(0.02f, 0.02f, 0.01f), M("SignalRed"), false);
        }

        public static void Shelf(Transform p, Vector3 pos, float yaw, float w, float h, bool stocked)
        {
            var g = Group("Shelf", p, pos, yaw);
            for (int sx = -1; sx <= 1; sx += 2)
                Box(g, "Upright", new Vector3(sx * w * 0.5f, h * 0.5f, 0f), new Vector3(0.05f, h, 0.45f), M("SteelGreen"), false);
            var col = g.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, h * 0.5f, 0f); col.size = new Vector3(w, h, 0.48f);
            for (float y = 0.15f; y < h; y += 0.55f)
            {
                Box(g, "Board", new Vector3(0f, y, 0f), new Vector3(w, 0.03f, 0.45f), M("SteelGreen"), false);
                if (!stocked) continue;
                for (float x = -w * 0.5f + 0.25f; x < w * 0.5f - 0.2f; x += R(0.25f, 0.5f))
                    if (Chance(0.6f))
                    {
                        float bh = R(0.12f, 0.35f);
                        Box(g, "Item", new Vector3(x, y + 0.015f + bh * 0.5f, R(-0.08f, 0.08f)), new Vector3(R(0.12f, 0.3f), bh, R(0.15f, 0.3f)),
                            M(Chance(0.5f) ? "Cardboard" : Pick(new[] { "Plastic", "PlasticBlue", "Red", "Yellow" })), false, new Vector3(0f, R(-15f, 15f), 0f));
                    }
            }
        }

        public static void Desk(Transform p, Vector3 pos, float yaw, bool monitorOn)
        {
            var g = Group("Desk", p, pos, yaw);
            Box(g, "Top", new Vector3(0f, 0.74f, 0f), new Vector3(1.4f, 0.04f, 0.7f), M("Wood"));
            Box(g, "Drawer", new Vector3(0.5f, 0.36f, 0f), new Vector3(0.4f, 0.72f, 0.65f), M("LightGray"), false);
            Box(g, "Leg", new Vector3(-0.66f, 0.36f, 0f), new Vector3(0.04f, 0.72f, 0.65f), M("LightGray"), false);
            Box(g, "Monitor", new Vector3(0f, 1.0f, -0.15f), new Vector3(0.55f, 0.36f, 0.05f), M("Black"), false);
            Box(g, "Stand", new Vector3(0f, 0.8f, -0.17f), new Vector3(0.08f, 0.1f, 0.12f), M("Black"), false);
            Box(g, "Keyboard", new Vector3(0f, 0.77f, 0.1f), new Vector3(0.45f, 0.02f, 0.15f), M("DarkGray"), false);
            if (monitorOn)
            {
                var s = Quad(g, "Screen", new Vector3(0f, 1.0f, -0.12f), new Vector2(0.5f, 0.3f), Vector3.forward, M("ScreenStatic"));
                var l = PointLight(g, "ScreenGlow", new Vector3(0f, 1.0f, 0.3f), new Color(0.6f, 0.75f, 1f), 0.6f, 3f);
                Flicker(s, BJ_FlickerLight.Mode.Buzz, M("ScreenStatic").GetColor("_EmissionColor"), new[] { l }, s.GetComponent<MeshRenderer>());
            }
            Chair(p, pos + Quaternion.Euler(0f, yaw, 0f) * new Vector3(R(-0.3f, 0.3f), 0f, 0.75f), yaw + 180f + R(-40f, 40f), Chance(0.3f));
            if (Chance(0.6f)) Box(g, "Papers", new Vector3(R(-0.4f, 0.2f), 0.77f, R(-0.1f, 0.2f)), new Vector3(0.3f, 0.02f, 0.22f), M("Paper"), false, new Vector3(0f, R(0f, 90f), 0f));
        }

        public static void Chair(Transform p, Vector3 pos, float yaw, bool fallen)
        {
            var g = Group("Chair", p, pos, yaw);
            if (fallen) { g.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(-90f, 0f, 0f); g.localPosition = pos + Vector3.up * 0.3f; }
            Box(g, "Seat", new Vector3(0f, 0.47f, 0f), new Vector3(0.48f, 0.08f, 0.46f), M("Black"));
            Box(g, "Back", new Vector3(0f, 0.8f, -0.22f), new Vector3(0.46f, 0.55f, 0.05f), M("Black"), false);
            Cyl(g, "Post", new Vector3(0f, 0.08f, 0f), new Vector3(0f, 0.45f, 0f), 0.05f, M("DarkMetal"));
            Box(g, "Base", new Vector3(0f, 0.05f, 0f), new Vector3(0.55f, 0.04f, 0.55f), M("DarkMetal"), false, new Vector3(0f, 45f, 0f));
        }

        public static void Lockers(Transform p, Vector3 pos, float yaw, int count)
        {
            var g = Group("Lockers", p, pos, yaw);
            for (int i = 0; i < count; i++)
            {
                float x = (i - (count - 1) * 0.5f) * 0.46f;
                Box(g, "Locker", new Vector3(x, 0.9f, 0f), new Vector3(0.44f, 1.8f, 0.5f), M("SteelGreen"));
                if (Chance(0.25f))
                    Box(g, "OpenDoor", new Vector3(x + 0.2f, 0.95f, 0.43f), new Vector3(0.42f, 1.6f, 0.02f), M("SteelGreen"), false, new Vector3(0f, -70f, 0f));
            }
        }
    }
}
