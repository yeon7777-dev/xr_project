using UnityEngine;
using UnityEngine.Rendering;
using Byeongjeom;

namespace Byeongjeom.MapGen
{
    /// <summary>
    /// 역 바깥: 1번 출구(동쪽 역앞광장), 2·3번 출구(서쪽), 도로/건물/주차타워, 울타리·경계,
    /// 전차선, 신호기, 양 끝 과선교, 화차·보수차량.
    /// </summary>
    public static partial class BJ
    {
        const float GY = 0.08f;   // 보도 포장 높이

        // ------------------------------------------------------------------ exits
        static void BuildExits(Transform root)
        {
            var g = Group("Exits_출구", root);
            float rise = CF - GY;

            // ---------------- 1번 출구 (동쪽, 역앞광장)
            var e1 = Group("Exit1_역앞광장", g);
            Deck(e1, 38f, 42f, -18f, -8f);
            DeckRail(e1, new Vector3(38f, CF, -8f), new Vector3(42f, CF, -8f));
            DeckRail(e1, new Vector3(38f, CF, -18f), new Vector3(42f, CF, -18f));
            DeckRail(e1, new Vector3(42f, CF, -9.0f), new Vector3(42f, CF, -8f));
            Stair(e1, "Stair", new Vector3(42f, CF, -15f), 90f, 5f, rise, 2, 2.5f, M("Cladding"));
            Escalator(e1, "EscalatorUp", new Vector3(42f, CF, -11.3f), 90f, rise, 1.1f);
            Escalator(e1, "EscalatorDown", new Vector3(42f, CF, -9.7f), 90f, rise, 1.1f);
            foreach (float z in new[] { -17.6f, -8.4f }) Column(e1, new Vector3(41.6f, 0f, z), SLAB);
            foreach (float z in new[] { -17.3f, -12.7f }) Column(e1, new Vector3(50.45f, 0f, z), CF - 25 * (rise / 50f) - 0.3f, 0.5f);
            ExitCanopy(e1, new Vector3(42f, 12.4f, -13f), 90f, 11.2f, 18f, 8.8f, new Vector3(37.8f, 12.3f, -18.4f), new Vector3(42.2f, 12.5f, -7.6f));
            Totem(e1, new Vector3(61.2f, GY, -19.4f), Vector3.right, "S_Exit1");
            Barricade(e1, new Vector3(61f, GY, -21.5f), 30f);
            Barricade(e1, new Vector3(62.2f, GY, -8.5f), -20f, true);
            Barricade(e1, new Vector3(58.5f, GY, -23f), 80f);
            Vector3 bn = Quaternion.Euler(0f, 30f, 0f) * Vector3.forward;
            Quad(e1, "Notice", new Vector3(61f, GY + 0.95f, -21.5f) + bn * 0.035f, new Vector2(0.45f, 0.6f), bn, SignMat("S_Notice", 0f), 0f);
            Scatter(e1, new Vector3(38.5f, CF + 0.01f, -17.5f), new Vector3(41.5f, CF + 0.01f, -8.5f), 3);

            // ---------------- 2·3번 출구 (서쪽)
            var e2 = Group("Exit2-3_서측", g);
            Deck(e2, -44f, -38f, -22f, -6f);
            DeckRail(e2, new Vector3(-44f, CF, -6f), new Vector3(-38f, CF, -6f));
            DeckRail(e2, new Vector3(-44f, CF, -22f), new Vector3(-44f, CF, -13.3f));
            DeckRail(e2, new Vector3(-44f, CF, -7.4f), new Vector3(-44f, CF, -6f));
            DeckRail(e2, new Vector3(-39.4f, CF, -22f), new Vector3(-38f, CF, -22f));
            DeckRail(e2, new Vector3(-44f, CF, -22f), new Vector3(-43.6f, CF, -22f));
            Stair(e2, "Exit2_Stair", new Vector3(-44f, CF, -9.5f), -90f, 4f, rise, 2, 2.5f, M("Cladding"));
            Escalator(e2, "Exit2_Escalator", new Vector3(-44f, CF, -12.6f), -90f, rise, 1.1f);
            Stair(e2, "Exit3_Stair", new Vector3(-41.5f, CF, -22f), 180f, 4f, rise, 2, 2.5f, M("Cladding"));
            foreach (var c in new[] { new Vector2(-43.6f, -21.6f), new Vector2(-43.6f, -6.4f) }) Column(e2, new Vector3(c.x, 0f, c.y), SLAB);
            float landY = CF - 25 * (rise / 50f) - 0.3f;
            foreach (float z in new[] { -11.3f, -7.7f }) Column(e2, new Vector3(-52.45f, 0f, z), landY, 0.5f);
            foreach (float x in new[] { -43.3f, -39.7f }) Column(e2, new Vector3(x, 0f, -30.45f), landY, 0.5f);
            ExitCanopy(e2, new Vector3(-44f, 12.4f, -10.3f), -90f, 7.0f, 18f, 8.8f, new Vector3(-44.2f, 12.3f, -22.4f), new Vector3(-37.8f, 12.5f, -5.6f));
            ExitCanopy(e2, new Vector3(-41.5f, 12.4f, -22f), 180f, 5.2f, 18f, 8.8f, Vector3.zero, Vector3.zero);
            Totem(e2, new Vector3(-63.2f, GY, -14.3f), Vector3.left, "S_Exit2");
            Totem(e2, new Vector3(-45.2f, GY, -41.2f), Vector3.back, "S_Exit3");
            Decal(e2, "G_GoBack", new Vector3(-43.6f, CF + 0.012f, -16f), new Vector2(3.2f, 1.6f), Vector3.up, 90f);
            Barricade(e2, new Vector3(-63f, GY, -10f), 100f, true);
            Scatter(e2, new Vector3(-43.5f, CF + 0.01f, -21.5f), new Vector3(-38.5f, CF + 0.01f, -6.5f), 4);
        }

        static void Deck(Transform p, float x0, float x1, float z0, float z1)
        {
            BoxMM(p, "DeckSlab", new Vector3(x0, SLAB, z0), new Vector3(x1, CF - 0.05f, z1), M("ConcreteDark"));
            BoxMM(p, "DeckFloor", new Vector3(x0, CF - 0.05f, z0), new Vector3(x1, CF, z1), M("FloorTile"));
            BoxMM(p, "DeckEdge", new Vector3(x0, SLAB - 0.3f, z0), new Vector3(x1, SLAB, z1), M("Cladding"), false);
        }

        static void DeckRail(Transform p, Vector3 a, Vector3 b)
        {
            Vector3 c = (a + b) * 0.5f;
            bool alongX = Mathf.Abs(a.z - b.z) < 0.01f;
            float len = alongX ? Mathf.Abs(b.x - a.x) : Mathf.Abs(b.z - a.z);
            Vector3 s = alongX ? new Vector3(len, 1.0f, 0.03f) : new Vector3(0.03f, 1.0f, len);
            var gl = Box(p, "RailGlass", c + Vector3.up * 0.55f, s, M("Glass"));
            gl.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            Box(p, "RailTop", c + Vector3.up * 1.1f, alongX ? new Vector3(len, 0.06f, 0.07f) : new Vector3(0.07f, 0.06f, len), M("Stainless"), false);
        }

        static void Column(Transform p, Vector3 basePos, float top, float size = 0.7f)
        {
            BoxMM(p, "Column", new Vector3(basePos.x - size * 0.5f, basePos.y, basePos.z - size * 0.5f), new Vector3(basePos.x + size * 0.5f, top, basePos.z + size * 0.5f), M("Concrete"));
        }

        /// <summary>출구 계단 위 경사 지붕 + 기둥 + 조명. top = 경사 지붕 시작점(대합실 쪽), yaw 방향으로 내려감.</summary>
        static void ExitCanopy(Transform p, Vector3 top, float yaw, float width, float run, float drop, Vector3 deckMin, Vector3 deckMax)
        {
            if (deckMax != deckMin) BoxMM(p, "DeckRoof", deckMin, deckMax, M("SteelBlue"), false);
            float len = Mathf.Sqrt(run * run + drop * drop), ang = Mathf.Atan2(drop, run) * Mathf.Rad2Deg;
            var g = Group("ExitCanopy", p, top, yaw);
            Box(g, "Roof", new Vector3(0f, -drop * 0.5f, run * 0.5f), new Vector3(width, 0.2f, len), M("SteelBlue"), false, new Vector3(ang, 0f, 0f));
            Box(g, "Gutter", new Vector3(width * 0.5f, -drop * 0.5f - 0.1f, run * 0.5f), new Vector3(0.15f, 0.25f, len), M("DarkMetal"), false, new Vector3(ang, 0f, 0f));
            Box(g, "Gutter", new Vector3(-width * 0.5f, -drop * 0.5f - 0.1f, run * 0.5f), new Vector3(0.15f, 0.25f, len), M("DarkMetal"), false, new Vector3(ang, 0f, 0f));
            foreach (float zf in new[] { run * 0.47f, run * 0.98f })
            {
                float y = -drop * zf / run;
                foreach (int s in new[] { -1, 1 })
                {
                    var c = Go("CanopyPost", g);
                    c.transform.localPosition = new Vector3(s * (width * 0.5f + 0.1f), 0f, zf);
                    float h = top.y + y - 0.1f;
                    Box(c.transform, "Post", new Vector3(0f, y - h * 0.5f, 0f), new Vector3(0.22f, h, 0.22f), M("SteelBlue"));
                }
            }
            Fixture(g, "CanopyLamp", new Vector3(0f, -drop * 0.25f - 0.2f, run * 0.25f), new Vector3(0.3f, 0.08f, 1.2f), RandomLamp(0.2f, 0.3f, 0.3f), CoolWhite, 1.8f, 10f);
            Fixture(g, "CanopyLamp", new Vector3(0f, -drop * 0.7f - 0.2f, run * 0.7f), new Vector3(0.3f, 0.08f, 1.2f), RandomLamp(0.1f, 0.2f, 0.4f), CoolWhite, 1.8f, 10f);
        }

        static void Totem(Transform p, Vector3 pos, Vector3 facing, string tex)
        {
            var g = Go("ExitTotem", p);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.LookRotation(facing, Vector3.up);
            Box(g.transform, "Body", new Vector3(0f, 1.5f, 0f), new Vector3(0.9f, 3.0f, 0.4f), M("DarkMetal"));
            Sign(g.transform, "Sign", new Vector3(0f, 2.55f, 0.22f), 1.6f, 0.4f, Vector3.forward, tex, 0.35f, null, false);
            Sign(g.transform, "SignBack", new Vector3(0f, 2.55f, -0.22f), 1.6f, 0.4f, Vector3.back, tex, 0f, null, false);
        }

        // ------------------------------------------------------------------ ground, plaza, roads
        static void BuildGround(Transform root)
        {
            var g = Group("Ground", root);
            BoxMM(g, "GroundE", new Vector3(FENCEX, -0.5f, -TRACKZ), new Vector3(150f, 0f, TRACKZ), M("Dirt"));
            BoxMM(g, "GroundW", new Vector3(-150f, -0.5f, -TRACKZ), new Vector3(-FENCEX, 0f, TRACKZ), M("Dirt"));
        }

        static void BuildEastSide(Transform root)
        {
            var g = Group("East_역앞광장", root);
            BoxMM(g, "Plaza", new Vector3(26f, 0f, -62.5f), new Vector3(100f, GY, 40f), M("Sidewalk"));
            BoxMM(g, "ParkingLot", new Vector3(26f, 0f, 40f), new Vector3(100f, 0.06f, 62.5f), M("Asphalt"));
            for (float x = 30f; x < 98f; x += 2.8f)
                foreach (float z in new[] { 44f, 55f })
                    BoxMM(g, "ParkLine", new Vector3(x - 0.06f, 0.06f, z - 2.5f), new Vector3(x + 0.06f, 0.065f, z + 2.5f), M("LightGray"), false);
            Road(g, 100f, 116f, true);
            BoxMM(g, "FarSidewalk", new Vector3(116f, 0f, -62.5f), new Vector3(119f, GY, 62.5f), M("Sidewalk"));

            // 건물 (도로 건너편)
            float[] zs = { -62.5f, -37f, -13f, 9f, 35f, 62.5f };
            float[] hs = { 14f, 22f, 11f, 18f, 15f };
            string[] shops = { "S_Shop1", "S_Shop2", "S_Shop3", "S_Shop4", "S_Shop5", "S_Shop6" };
            for (int i = 0; i < 5; i++)
                Building(g, new Vector3(119f, 0f, zs[i] + 0.5f), new Vector3(139f, hs[i], zs[i + 1] - 0.5f), Vector3.left, shops[i], i % 2 == 0 ? "Facade" : "FacadeDark");
            Building(g, new Vector3(26f, 0f, 63f), new Vector3(70f, 16f, 82f), Vector3.back, null, "FacadeDark");
            Building(g, new Vector3(71f, 0f, 63f), new Vector3(119f, 12f, 82f), Vector3.back, null, "Facade");
            Building(g, new Vector3(26f, 0f, -82f), new Vector3(64f, 12f, -63f), Vector3.forward, "S_Shop6", "Facade");
            Building(g, new Vector3(65f, 0f, -82f), new Vector3(119f, 18f, -63f), Vector3.forward, "S_Shop2", "FacadeDark");

            // 광장 소품
            Planter(g, new Vector3(72f, GY, -36f), new Vector2(8f, 3f), 2);
            Planter(g, new Vector3(72f, GY, 12f), new Vector2(8f, 3f), 2);
            Planter(g, new Vector3(86f, GY, -8f), new Vector2(3f, 8f), 1);
            Planter(g, new Vector3(50f, GY, 26f), new Vector2(6f, 3f), 1);
            Planter(g, new Vector3(50f, GY, -45f), new Vector2(6f, 3f), 2);
            foreach (var b in new[] { new Vector4(72f, -33.8f, 0f, 0f), new Vector4(72f, 14.2f, 0f, 0f), new Vector4(84f, -8f, -90f, 0f), new Vector4(72f, -38.2f, 180f, 1f), new Vector4(50f, 28.2f, 0f, 0f) })
                Bench(g, new Vector3(b.x, GY, b.y), b.z, b.w > 0.5f, "Wood");
            StreetLight(g, new Vector3(64f, GY, -52f), 0f, LampState.On, true);
            StreetLight(g, new Vector3(64f, GY, -24f), 180f, LampState.Off);
            StreetLight(g, new Vector3(64f, GY, 22f), 0f, LampState.Flicker);
            StreetLight(g, new Vector3(91f, GY, -52f), 90f, LampState.Off);
            StreetLight(g, new Vector3(91f, GY, -24f), 90f, LampState.Dying);
            StreetLight(g, new Vector3(91f, GY, 4f), 90f, LampState.On, true);
            StreetLight(g, new Vector3(91f, GY, 32f), 90f, LampState.Off);
            StreetLight(g, new Vector3(70f, 0.06f, 50f), 180f, LampState.Buzz);
            ClockPole(g, new Vector3(70f, GY, -22f), -90f);
            BusShelter(g, new Vector3(97.6f, GY, -32f), 90f, LampState.Buzz);
            BusShelter(g, new Vector3(97.6f, GY, 8f), 90f, LampState.Off);
            UtilityPole(g, new Vector3(99.4f, GY, -56f), 0f);
            UtilityPole(g, new Vector3(99.4f, GY, 0f), 0f);
            UtilityPole(g, new Vector3(99.4f, GY, 56f), 0f);
            Wire(g, new Vector3(99.4f, 9.4f, -56f), new Vector3(99.4f, 9.4f, 0f));
            Wire(g, new Vector3(99.4f, 9.4f, 0f), new Vector3(99.4f, 9.4f, 56f));
            TrafficLight(g, new Vector3(99.6f, GY, -18.5f));
            // 차량
            Car(g, new Vector3(104f, 0.04f, -46f), 0f, "CarSilver");
            Car(g, new Vector3(112f, 0.04f, -20f), 180f, "CarWhite", true);
            Car(g, new Vector3(104.5f, 0.04f, 21f), 14f, "CarRed", false, true);
            Car(g, new Vector3(95.5f, GY, -11f), 2f, "CarBlack");
            Car(g, new Vector3(95.5f, GY, 26f), -4f, "CarBlue", true);
            Bus(g, new Vector3(108f, 0.04f, 44f), 68f, "BusGreen", 2f);
            string[] paints = { "CarWhite", "CarSilver", "CarBlack", "CarBlue", "CarRed" };
            for (int i = 0; i < 9; i++)
                Car(g, new Vector3(31.4f + RI(0, 23) * 2.8f, 0.06f, Chance(0.5f) ? 44f : 55f), Chance(0.5f) ? 0f : 180f, Pick(paints), false, Chance(0.15f));
            FloorDecal(g, "D_Blood", new Vector3(106.2f, 0.06f, 21.6f), 2.2f);
            Decal(g, "D_BloodSmear", new Vector3(103.5f, 0.06f, 24.5f), new Vector2(4f, 1.2f), Vector3.up, 75f);
            // 고가 아래 (대합실 하부)
            for (int i = 0; i < 9; i++) Bike(g, new Vector3(27.5f + i * 0.9f, GY, -4.5f), 0f, Chance(0.3f));
            BoxMM(g, "BikeRack", new Vector3(27f, GY, -4.6f), new Vector3(35.5f, GY + 0.5f, -4.4f), M("Stainless"), false);
            Dumpster(g, new Vector3(34f, GY, 11.5f), 180f);
            Box(g, "Container", new Vector3(30f, GY + 1.3f, 6.5f), new Vector3(6f, 2.6f, 2.4f), M("RustMetal"));
            Decal(g, "G_Help", new Vector3(30f, GY + 1.5f, 5.29f - 0.0f), new Vector2(2.4f, 1.2f), Vector3.back);
            TrashBags(g, new Vector3(97f, GY, -29f), 3);
            TrashBags(g, new Vector3(37f, GY, 14f), 5);
            Boxes(g, new Vector3(29f, GY, -14f), 5);
            for (int i = 0; i < 6; i++) Cone(g, new Vector3(R(56f, 66f), GY, R(-26f, -5f)));
            Scatter(g, new Vector3(28f, GY + 0.01f, -60f), new Vector3(98f, GY + 0.01f, 38f), 40, "D_Leaves", "D_Papers", "D_Grime", "D_Leaves");
            Tape(g, new Vector3(58f, 1.0f, -24f), new Vector3(66f, 1.0f, -26f));
            Tape(g, new Vector3(66f, 1.0f, -26f), new Vector3(66f, 1.0f, -3f));
        }

        static void BuildWestSide(Transform root)
        {
            var g = Group("West_서측", root);
            BoxMM(g, "Sidewalk", new Vector3(-77f, 0f, -62.5f), new Vector3(-26f, GY, 62.5f), M("Sidewalk"));
            Road(g, -92f, -77f, false);
            BoxMM(g, "FarSidewalk", new Vector3(-95f, 0f, -62.5f), new Vector3(-92f, GY, 62.5f), M("Sidewalk"));
            float[] zs = { -62.5f, -30f, 5f, 35f, 62.5f };
            float[] hs = { 12f, 20f, 14f, 16f };
            string[] shops = { "S_Shop3", "S_Shop5", "S_Shop1", "S_Shop4" };
            for (int i = 0; i < 4; i++)
                Building(g, new Vector3(-115f, 0f, zs[i] + 0.5f), new Vector3(-95f, hs[i], zs[i + 1] - 0.5f), Vector3.right, shops[i], i % 2 == 0 ? "FacadeDark" : "Facade");
            Building(g, new Vector3(-119f, 0f, 63f), new Vector3(-26f, 15f, 82f), Vector3.back, null, "Facade");
            Building(g, new Vector3(-119f, 0f, -82f), new Vector3(-26f, 13f, -63f), Vector3.forward, null, "FacadeDark");

            ParkingTower(g, new Vector3(-72f, 0f, 14f), new Vector3(-50f, 0f, 44f));

            // 대학 통학버스 정류장 (3번 출구 앞)
            BusShelter(g, new Vector3(-48f, GY, -50f), -90f, LampState.Flicker);
            var pole = Group("ShuttleSign", g, new Vector3(-48.3f, GY, -45.5f), -90f);
            Cyl(pole, "Pole", Vector3.zero, new Vector3(0f, 2.6f, 0f), 0.1f, M("DarkMetal"), true);
            Sign(pole, "Sign", new Vector3(0f, 2.75f, 0f), 1.6f, 0.4f, Vector3.forward, "S_Shuttle", 0f, "S_Shuttle");
            Bus(g, new Vector3(-80.5f, 0.04f, -42f), 0f, "BusBlue");
            Car(g, new Vector3(-88f, 0.04f, 12f), 180f, "CarSilver", true);
            Car(g, new Vector3(-80.5f, 0.04f, 30f), 8f, "CarWhite", false, true);
            Car(g, new Vector3(-74.5f, GY, -20f), 0f, "CarBlack");

            Planter(g, new Vector3(-58f, GY, -30f), new Vector2(3f, 9f), 2);
            Planter(g, new Vector3(-58f, GY, 2f), new Vector2(3f, 7f), 1);
            Planter(g, new Vector3(-36f, GY, -52f), new Vector2(7f, 3f), 2);
            Bench(g, new Vector3(-56.2f, GY, -30f), 90f, false, "Wood");
            Bench(g, new Vector3(-36f, GY, -50.2f), 0f, true, "Wood");
            StreetLight(g, new Vector3(-75f, GY, -50f), -90f, LampState.Off);
            StreetLight(g, new Vector3(-75f, GY, -22f), -90f, LampState.On, true);
            StreetLight(g, new Vector3(-75f, GY, 6f), -90f, LampState.Dying);
            StreetLight(g, new Vector3(-40f, GY, 30f), 0f, LampState.Off);
            StreetLight(g, new Vector3(-66f, GY, -6f), 90f, LampState.Flicker);
            UtilityPole(g, new Vector3(-76.4f, GY, -40f), 0f);
            UtilityPole(g, new Vector3(-76.4f, GY, 20f), 0f);
            Wire(g, new Vector3(-76.4f, 9.4f, -40f), new Vector3(-76.4f, 9.4f, 20f));
            Dumpster(g, new Vector3(-30f, GY, 10f), 90f);
            Boxes(g, new Vector3(-33f, GY, -3f), 6);
            TrashBags(g, new Vector3(-36f, GY, 14f), 4);
            for (int i = 0; i < 5; i++) Bike(g, new Vector3(-36f + i * 0.9f, GY, -14.5f), 180f, Chance(0.4f));
            for (int i = 0; i < 5; i++) Cone(g, new Vector3(R(-66f, -60f), GY, R(-18f, -8f)));
            Scatter(g, new Vector3(-75f, GY + 0.01f, -60f), new Vector3(-28f, GY + 0.01f, 60f), 30, "D_Leaves", "D_Papers", "D_Grime");
            FloorDecal(g, "D_Blood", new Vector3(-47.5f, GY + 0.01f, -47.5f), 1.8f);
        }

        static void Road(Transform p, float x0, float x1, bool eastSide)
        {
            BoxMM(p, "Road", new Vector3(x0, 0f, -62.5f), new Vector3(x1, 0.04f, 62.5f), M("Asphalt"));
            float curbX = eastSide ? x0 : x1;
            BoxMM(p, "Curb", new Vector3(curbX - 0.15f, 0f, -62.5f), new Vector3(curbX + 0.15f, 0.18f, 62.5f), M("ConcreteLight"));
            float mid = (x0 + x1) * 0.5f;
            var marks = new MeshBuilder();
            var yellow = new MeshBuilder();
            yellow.AddBox(new Vector3(mid - 0.12f, 0.045f, 0f), new Vector3(0.12f, 0.01f, 125f));
            yellow.AddBox(new Vector3(mid + 0.12f, 0.045f, 0f), new Vector3(0.12f, 0.01f, 125f));
            foreach (float lx in new[] { (x0 + mid) * 0.5f, (mid + x1) * 0.5f })
                for (float z = -60f; z < 60f; z += 9f)
                    marks.AddBox(new Vector3(lx, 0.045f, z), new Vector3(0.12f, 0.01f, 3f));
            if (eastSide)
                for (float x = x0 + 0.8f; x < x1 - 0.5f; x += 1.0f)
                    marks.AddBox(new Vector3(x, 0.045f, -16f), new Vector3(0.5f, 0.01f, 4f));
            Combined(p, "LaneMarks", marks, M("LightGray"), false);
            Combined(p, "CenterLine", yellow, M("Yellow"), false);
        }

        static void Building(Transform p, Vector3 min, Vector3 max, Vector3 front, string shopSign, string mat)
        {
            var b = BoxMM(p, "Building", min, max, M(mat));
            b.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;
            BoxMM(p, "Roof", new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y + 0.8f, max.z), M("ConcreteDark"), false);
            if (shopSign == null) return;
            bool alongZ = Mathf.Abs(front.x) > 0.5f;
            float faceCoord = alongZ ? (front.x < 0 ? min.x : max.x) : (front.z < 0 ? min.z : max.z);
            float a0 = alongZ ? min.z : min.x, a1 = alongZ ? max.z : max.x;
            float len = a1 - a0;
            int n = Mathf.Max(1, Mathf.FloorToInt(len / 7f));
            float w = len / n;
            for (int i = 0; i < n; i++)
            {
                float c = a0 + (i + 0.5f) * w;
                float shutterH = Chance(0.2f) ? R(1.2f, 2.0f) : 3.0f;
                Vector3 off = front * 0.08f;
                if (alongZ)
                {
                    BoxMM(p, "Shutter", new Vector3(faceCoord + front.x * 0.02f, 3.2f - shutterH, c - w * 0.42f) + off, new Vector3(faceCoord + front.x * 0.1f, 3.2f, c + w * 0.42f) + off, M("Shutter"), false);
                    if (shutterH < 2.9f) BoxMM(p, "Dark", new Vector3(faceCoord + front.x * 0.01f, 0f, c - w * 0.42f), new Vector3(faceCoord + front.x * 0.02f, 3.2f - shutterH, c + w * 0.42f), M("Black"), false);
                    Sign(p, "ShopSign", new Vector3(faceCoord + front.x * 0.15f, 3.95f, c), Mathf.Min(5.5f, w * 0.85f), 1.3f, front, i == 0 ? shopSign : Pick(new[] { "S_Shop1", "S_Shop2", "S_Shop3", "S_Shop4", "S_Shop5", "S_Shop6" }), Chance(0.25f) ? 0.4f : 0f);
                }
                else
                {
                    BoxMM(p, "Shutter", new Vector3(c - w * 0.42f, 3.2f - shutterH, faceCoord + front.z * 0.02f) + off, new Vector3(c + w * 0.42f, 3.2f, faceCoord + front.z * 0.1f) + off, M("Shutter"), false);
                    if (shutterH < 2.9f) BoxMM(p, "Dark", new Vector3(c - w * 0.42f, 0f, faceCoord + front.z * 0.01f), new Vector3(c + w * 0.42f, 3.2f - shutterH, faceCoord + front.z * 0.02f), M("Black"), false);
                    Sign(p, "ShopSign", new Vector3(c, 3.95f, faceCoord + front.z * 0.15f), Mathf.Min(5.5f, w * 0.85f), 1.3f, front, i == 0 ? shopSign : Pick(new[] { "S_Shop1", "S_Shop2", "S_Shop3", "S_Shop4", "S_Shop5", "S_Shop6" }), Chance(0.25f) ? 0.4f : 0f);
                }
            }
        }

        static void Wire(Transform p, Vector3 a, Vector3 b)
        {
            Vector3 mid = (a + b) * 0.5f + Vector3.down * 1.2f;
            Cyl(p, "Wire", a, mid, 0.025f, M("Black"));
            Cyl(p, "Wire", mid, b, 0.025f, M("Black"));
        }

        static void TrafficLight(Transform p, Vector3 pos)
        {
            var g = Group("TrafficLight", p, pos);
            Cyl(g, "Pole", Vector3.zero, new Vector3(0f, 6f, 0f), 0.22f, M("DarkMetal"), true);
            Cyl(g, "Arm", new Vector3(0f, 5.8f, 0f), new Vector3(7f, 5.8f, 0f), 0.14f, M("DarkMetal"));
            Box(g, "Head", new Vector3(6.5f, 5.5f, 0f), new Vector3(1.1f, 0.35f, 0.3f), M("Black"), false);
            var lamp = Box(g, "Amber", new Vector3(6.5f, 5.5f, -0.16f), new Vector3(0.24f, 0.24f, 0.02f), M("SignalYellow"), false);
            var l = PointLight(g, "AmberLight", new Vector3(6.5f, 5.3f, -0.8f), new Color(1f, 0.65f, 0.15f), 2.0f, 9f);
            var f = Flicker(lamp, BJ_FlickerLight.Mode.Strobe, M("SignalYellow").GetColor("_EmissionColor"), new[] { l }, lamp.GetComponent<MeshRenderer>());
            f.strobePeriod = 1.4f; f.speed = 1f;
        }

        static void ParkingTower(Transform p, Vector3 min, Vector3 max)
        {
            var g = Group("ParkingTower_주차타워", p);
            float[] levels = { 0f, 3.2f, 6.4f, 9.6f, 12.8f };
            for (int i = 1; i < levels.Length; i++)
            {
                BoxMM(g, "Slab", new Vector3(min.x, levels[i] - 0.3f, min.z), new Vector3(max.x, levels[i], max.z), M("ConcreteDark"));
                {
                    BoxMM(g, "Parapet", new Vector3(min.x, levels[i], min.z), new Vector3(max.x, levels[i] + 1.1f, min.z + 0.25f), M("ConcreteLight"));
                    BoxMM(g, "Parapet", new Vector3(min.x, levels[i], max.z - 0.25f), new Vector3(max.x, levels[i] + 1.1f, max.z), M("ConcreteLight"));
                    BoxMM(g, "Parapet", new Vector3(min.x, levels[i], min.z), new Vector3(min.x + 0.25f, levels[i] + 1.1f, max.z), M("ConcreteLight"));
                    BoxMM(g, "Parapet", new Vector3(max.x - 0.25f, levels[i], min.z), new Vector3(max.x, levels[i] + 1.1f, max.z), M("ConcreteLight"));
                }
            }
            BoxMM(g, "GroundSlab", new Vector3(min.x, 0f, min.z), new Vector3(max.x, GY + 0.02f, max.z), M("ConcreteDark"));
            for (float x = min.x + 0.4f; x <= max.x; x += 7.2f)
                for (float z = min.z + 0.4f; z <= max.z; z += 7.4f)
                    BoxMM(g, "Column", new Vector3(x - 0.3f, 0f, z - 0.3f), new Vector3(x + 0.3f, 12.8f, z + 0.3f), M("Concrete"));
            // 북/서쪽 막힘 벽 (1층)
            BoxMM(g, "BackWall", new Vector3(min.x, 0f, max.z - 0.25f), new Vector3(max.x, 2.9f, max.z), M("Concrete"));
            BoxMM(g, "BackWall", new Vector3(min.x, 0f, min.z), new Vector3(min.x + 0.25f, 2.9f, max.z), M("Concrete"));
            // 램프는 셔터로 막힘
            BoxMM(g, "RampShutter", new Vector3(max.x - 4.2f, 0f, max.z - 0.6f), new Vector3(max.x - 0.3f, 2.9f, max.z - 0.4f), M("Shutter"));
            Sign(g, "ParkingSign", new Vector3((min.x + max.x) * 0.5f, 3.75f, min.z - 0.12f), 3.2f, 0.8f, Vector3.back, "S_Parking", 0.3f);
            Box(g, "BarrierArm", new Vector3(max.x - 3f, 1.0f, min.z + 1f), new Vector3(3.5f, 0.08f, 0.08f), M("Hazard"), false, new Vector3(0f, 0f, 35f));
            for (int i = 0; i < 6; i++)
                Car(g, new Vector3(min.x + 3f + i * 3f, GY + 0.02f, min.z + R(6f, 22f)), Chance(0.5f) ? 0f : 180f, Pick(new[] { "CarWhite", "CarSilver", "CarBlack", "CarBlue" }));
            Fixture(g, "GarageLamp", new Vector3((min.x + max.x) * 0.5f, 2.85f, (min.z + max.z) * 0.5f), new Vector3(0.25f, 0.06f, 1.2f), LampState.Flicker, SickGreen, 2.0f, 12f, false, "LampSick");
            Fixture(g, "GarageLamp", new Vector3(min.x + 4f, 2.85f, max.z - 5f), new Vector3(0.25f, 0.06f, 1.2f), LampState.Off, SickGreen, 2.0f, 12f);
            Decal(g, "G_LastTrain", new Vector3(min.x + 0.27f, 1.6f, (min.z + max.z) * 0.5f), new Vector2(4f, 2f), Vector3.right);
        }

        // ------------------------------------------------------------------ fences & bounds
        static void BuildBounds(Transform root)
        {
            var g = Group("Bounds_울타리_경계", root);
            foreach (int s in new[] { -1, 1 })
            {
                float x = s * FENCEX;
                float gap0 = s > 0 ? -37f : 38f, gap1 = gap0 + 4f;
                var posts = new MeshBuilder();
                var mesh = new MeshBuilder();
                for (float z = -60f; z <= 60.01f; z += 3f)
                {
                    if (z > gap0 + 0.1f && z < gap1 - 0.1f) continue;
                    posts.AddBox(new Vector3(x, 1.2f, z), new Vector3(0.08f, 2.4f, 0.08f));
                }
                foreach (var span in new[] { new Vector2(-60f, gap0), new Vector2(gap1, 60f) })
                {
                    mesh.AddBox(new Vector3(x, 1.25f, (span.x + span.y) * 0.5f), new Vector3(0.02f, 2.3f, span.y - span.x));
                    posts.AddBox(new Vector3(x, 2.4f, (span.x + span.y) * 0.5f), new Vector3(0.06f, 0.06f, span.y - span.x));
                    Blocker(g, "FenceBlock", new Vector3(x - 0.15f, 0f, span.x), new Vector3(x + 0.15f, 8f, span.y));
                }
                Combined(g, "FencePosts", posts, M("SteelGreen"), false);
                var fm = Combined(g, "FenceMesh", mesh, M("ChainLink"), false);
                // 쓰러진 펜스 (구멍)
                Box(g, "FallenFence", new Vector3(x + s * 1.2f, GY + 0.05f, (gap0 + gap1) * 0.5f), new Vector3(2.3f, 0.02f, 3.8f), M("ChainLink"), false, new Vector3(0f, R(-10f, 10f), s * 4f));
                Tape(g, new Vector3(x, 1.0f, gap0), new Vector3(x + s * 0.4f, 0.6f, gap1));
                // 승강장 바깥 옹벽 (|z| > 60)
                foreach (int t in new[] { -1, 1 })
                {
                    float za = t * 60f, zb = t * BOUNDZ;
                    BoxMM(g, "RetainingWall", new Vector3(x - 0.25f, -0.5f, Mathf.Min(za, zb)), new Vector3(x + 0.25f, 4.2f, Mathf.Max(za, zb)), M("Concrete"));
                    Blocker(g, "WallBlock", new Vector3(x - 0.25f, 4.2f, Mathf.Min(za, zb)), new Vector3(x + 0.25f, 30f, Mathf.Max(za, zb)));
                    BoxMM(g, "WallTopFence", new Vector3(x - 0.01f, 4.2f, Mathf.Min(za, zb)), new Vector3(x + 0.01f, 6.0f, Mathf.Max(za, zb)), M("ChainLink"), false);
                }
                Decal(g, "G_DontLook", new Vector3(x - s * 0.27f, 2.2f, s * 85f), new Vector2(4f, 2f), new Vector3(-s, 0f, 0f));
            }
            // 바깥 경계
            Blocker(g, "BoundE", new Vector3(119f, -1f, -64f), new Vector3(120f, 40f, 64f));
            Blocker(g, "BoundW", new Vector3(-96f, -1f, -64f), new Vector3(-95f, 40f, 64f));
            Blocker(g, "BoundN_E", new Vector3(FENCEX, -1f, 62.5f), new Vector3(120f, 40f, 63.5f));
            Blocker(g, "BoundN_W", new Vector3(-96f, -1f, 62.5f), new Vector3(-FENCEX, 40f, 63.5f));
            Blocker(g, "BoundS_E", new Vector3(FENCEX, -1f, -63.5f), new Vector3(120f, 40f, -62.5f));
            Blocker(g, "BoundS_W", new Vector3(-96f, -1f, -63.5f), new Vector3(-FENCEX, 40f, -62.5f));
            Blocker(g, "BoundTrackN", new Vector3(-FENCEX, -1f, BOUNDZ), new Vector3(FENCEX, 40f, BOUNDZ + 1f));
            Blocker(g, "BoundTrackS", new Vector3(-FENCEX, -1f, -BOUNDZ - 1f), new Vector3(FENCEX, 40f, -BOUNDZ));
            // 선로 끝 바리케이드
            foreach (int t in new[] { -1, 1 })
            {
                float z = t * (BOUNDZ - 2.5f);
                for (float x = -23f; x <= 23f; x += 2.3f)
                {
                    if (Chance(0.75f)) Barricade(g, new Vector3(x + R(-0.3f, 0.3f), 0.3f, z + R(-0.5f, 0.5f)), R(-15f, 15f), Chance(0.15f));
                    if (Chance(0.4f)) Box(g, "JerseyBarrier", new Vector3(x, 0.7f, z + t * 1.5f), new Vector3(2.0f, 0.8f, 0.6f), M("ConcreteLight"), true, new Vector3(0f, R(-10f, 10f), 0f));
                }
                Sign(g, "EndNotice", new Vector3(0f, 1.8f, z - t * 0.3f), 0.9f, 1.2f, new Vector3(0f, 0f, -t), "S_Notice", 0f);
                Cyl(g, "NoticePole", new Vector3(0f, 0.3f, z - t * 0.36f), new Vector3(0f, 1.25f, z - t * 0.36f), 0.06f, M("DarkMetal"));
            }
        }

        // ------------------------------------------------------------------ railway equipment
        static void BuildRailwayEquipment(Transform root)
        {
            var g = Group("Railway_전차선_신호", root);
            float[] mastZ = { -112.5f, -87.5f, -62.5f, -37.5f, 37.5f, 62.5f, 87.5f, 112.5f };
            float[] mastX = { -23.6f, -5f, 5f, 23.6f };
            var steel = new MeshBuilder();
            foreach (float z in mastZ)
            {
                foreach (float x in mastX)
                {
                    steel.AddBox(new Vector3(x, 4.55f, z), new Vector3(0.3f, 8.5f, 0.3f));
                    Blocker(g, "MastBlock", new Vector3(x - 0.15f, 0f, z - 0.15f), new Vector3(x + 0.15f, 8.8f, z + 0.15f));
                }
                steel.AddBox(new Vector3(0f, 8.6f, z), new Vector3(47.5f, 0.4f, 0.35f));
                foreach (float tx in TrackX)
                    steel.AddBox(new Vector3(tx, 7.6f, z), new Vector3(0.05f, 2.0f, 0.05f));
            }
            Combined(g, "CatenaryMasts", steel, M("DarkMetal"), false);
            var wires = new MeshBuilder();
            foreach (float tx in TrackX)
            {
                wires.AddBox(new Vector3(tx, 5.4f, 0f), new Vector3(0.02f, 0.02f, BOUNDZ * 2f + 30f));
                wires.AddBox(new Vector3(tx, 6.6f, 0f), new Vector3(0.025f, 0.025f, BOUNDZ * 2f + 30f));
            }
            var wr = Combined(g, "CatenaryWires", wires, M("Black"), false);
            wr.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            Cyl(g, "SnappedWire", new Vector3(21.15f, 5.4f, -90f), new Vector3(20.1f, 0.4f, -83f), 0.025f, M("Black"));
            Cyl(g, "SnappedWire", new Vector3(20.1f, 0.4f, -83f), new Vector3(19.0f, 0.35f, -80f), 0.025f, M("Black"));

            // 신호기 (상행 1·2번선은 북쪽 끝, 하행 3·4번선은 남쪽 끝)
            Signal(g, new Vector3(-23.4f, 0.3f, 80f), -1, false);
            Signal(g, new Vector3(-4.6f, 0.3f, 80f), -1, false);
            Signal(g, new Vector3(4.6f, 0.3f, -80f), 1, true);
            Signal(g, new Vector3(23.4f, 0.3f, -80f), 1, false);

            // 과선교 (양 끝)
            foreach (int t in new[] { -1, 1 })
            {
                float zc = t * 105f;
                BoxMM(g, "BridgeDeck", new Vector3(-31f, 7.2f, zc - 6f), new Vector3(31f, 8.4f, zc + 6f), M("ConcreteDark"));
                BoxMM(g, "BridgeParapet", new Vector3(-31f, 8.4f, zc - 6f), new Vector3(31f, 9.6f, zc - 5.7f), M("ConcreteLight"), false);
                BoxMM(g, "BridgeParapet", new Vector3(-31f, 8.4f, zc + 5.7f), new Vector3(31f, 9.6f, zc + 6f), M("ConcreteLight"), false);
                foreach (float x in new[] { -24.6f, -5f, 5f, 24.6f })
                    BoxMM(g, "BridgePier", new Vector3(x - 0.6f, 0.3f, zc - 1.5f), new Vector3(x + 0.6f, 7.2f, zc + 1.5f), M("Concrete"));
                Decal(g, Chance(0.5f) ? "G_GoBack" : "G_NotExit", new Vector3(-5f, 3.2f, zc - t * 1.52f), new Vector2(2.2f, 1.1f), new Vector3(0f, 0f, -t), 0f);
                StreetLight(g, new Vector3(-20f, 9.6f, zc + t * 5.4f), 180f, LampState.Off);
                StreetLight(g, new Vector3(10f, 9.6f, zc + t * 5.4f), 0f, LampState.Off);
            }

            // 버려진 화차 (2번 통과선, 북쪽) / 보수 차량 (1번 통과선, 남쪽)
            var wagon = Group("FreightWagon", g, new Vector3(TrackX[3], 0f, 88f));
            Box(wagon, "Body", new Vector3(0f, 2.5f, 0f), new Vector3(2.9f, 2.6f, 14f), M("RustMetal"));
            Box(wagon, "Frame", new Vector3(0f, 1.05f, 0f), new Vector3(2.6f, 0.3f, 14.4f), M("DarkMetal"), false);
            foreach (float bz in new[] { -5f, 5f }) Bogie(wagon, new Vector3(0f, 0f, bz));
            Decal(wagon, "G_LastTrain", new Vector3(-1.46f, 2.6f, 0f), new Vector2(4f, 2f), Vector3.left);
            Decal(wagon, "G_Tally", new Vector3(1.46f, 2.4f, 3f), new Vector2(1.6f, 1.6f), Vector3.right);

            var mv = Group("MaintenanceCar", g, new Vector3(TrackX[2], 0f, -92f));
            Box(mv, "Deck", new Vector3(0f, 1.3f, 0f), new Vector3(2.8f, 0.4f, 9f), M("MaintYellow"));
            Box(mv, "Cab", new Vector3(0f, 2.6f, 2.8f), new Vector3(2.7f, 2.2f, 3f), M("MaintYellow"));
            Box(mv, "CabGlass", new Vector3(0f, 3.0f, 4.31f), new Vector3(2.3f, 0.9f, 0.02f), M("CarBlack"), false);
            Box(mv, "Crane", new Vector3(0f, 2.2f, -2.5f), new Vector3(0.5f, 1.4f, 0.5f), M("MaintYellow"), false);
            Box(mv, "Boom", new Vector3(0f, 3.0f, -1.0f), new Vector3(0.3f, 0.3f, 4f), M("MaintYellow"), false, new Vector3(-15f, 0f, 0f));
            foreach (float bz in new[] { -3f, 3f }) Bogie(mv, new Vector3(0f, 0f, bz));
            var beacon = Box(mv, "Beacon", new Vector3(0f, 3.85f, 2.8f), new Vector3(0.25f, 0.25f, 0.25f), M("SignalYellow"), false);
            var bl = PointLight(mv, "BeaconLight", new Vector3(0f, 4.3f, 2.8f), new Color(1f, 0.6f, 0.1f), 3f, 14f);
            var bf = Flicker(beacon, BJ_FlickerLight.Mode.Strobe, M("SignalYellow").GetColor("_EmissionColor"), new[] { bl }, beacon.GetComponent<MeshRenderer>());
            bf.strobePeriod = 0.9f; bf.speed = 1f;

            // 야드 조명 (북쪽)
            StreetLight(g, new Vector3(24.4f, 0.3f, 95f), -90f, LampState.Dying);
            StreetLight(g, new Vector3(-24.4f, 0.3f, -70f), 90f, LampState.Buzz);
        }

        static void Bogie(Transform p, Vector3 c)
        {
            Box(p, "BogieFrame", c + new Vector3(0f, 0.85f, 0f), new Vector3(2.3f, 0.35f, 2.6f), M("DarkMetal"), false);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    Cyl(p, "Wheel", c + new Vector3(sx * 0.62f, 1.0f, sz * 1.0f), c + new Vector3(sx * 0.82f, 1.0f, sz * 1.0f), 0.84f, M("Rail"));
        }

        static void Signal(Transform p, Vector3 pos, int facingZ, bool dying)
        {
            var g = Group("Signal", p, pos);
            Cyl(g, "Pole", Vector3.zero, new Vector3(0f, 4.4f, 0f), 0.16f, M("DarkMetal"), true);
            Box(g, "Head", new Vector3(0f, 4.9f, 0f), new Vector3(0.45f, 1.1f, 0.3f), M("Black"), false);
            Box(g, "Visor", new Vector3(0f, 5.2f, facingZ * 0.2f), new Vector3(0.3f, 0.04f, 0.15f), M("Black"), false);
            var lamp = Box(g, "RedAspect", new Vector3(0f, 5.05f, facingZ * 0.16f), new Vector3(0.2f, 0.2f, 0.02f), M("SignalRed"), false);
            Box(g, "OtherAspect", new Vector3(0f, 4.65f, facingZ * 0.16f), new Vector3(0.2f, 0.2f, 0.02f), M("LampOff"), false);
            var l = PointLight(g, "RedGlow", new Vector3(0f, 5.0f, facingZ * 0.7f), new Color(1f, 0.05f, 0.02f), 1.8f, 7f);
            if (dying) Flicker(lamp, BJ_FlickerLight.Mode.Dying, M("SignalRed").GetColor("_EmissionColor"), new[] { l }, lamp.GetComponent<MeshRenderer>());
        }
    }
}
