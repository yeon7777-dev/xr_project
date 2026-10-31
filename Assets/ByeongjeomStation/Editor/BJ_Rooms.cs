using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Byeongjeom;

namespace Byeongjeom.MapGen
{
    /// <summary>
    /// 2층 대합실 내부: 개찰구, 자유통로(맞이방), 편의점, 화장실(남/여), 기계실, 역무실, 직원 휴게실.
    /// </summary>
    public static partial class BJ
    {
        // ------------------------------------------------------------------ wall helpers
        /// <summary>z 고정, x0~x1 벽. openings: (xa, xb, 개구부 상단 y)</summary>
        static void WallX(Transform p, string name, float z, float x0, float x1, Material mat, float th, params Vector3[] openings)
        {
            var ops = new List<Vector3>(openings);
            ops.Sort((a, b) => a.x.CompareTo(b.x));
            float x = x0, h = th * 0.5f;
            foreach (var o in ops)
            {
                if (o.x > x + 0.01f) BoxMM(p, name, new Vector3(x, CF, z - h), new Vector3(o.x, CEIL, z + h), mat);
                BoxMM(p, name + "_Lintel", new Vector3(o.x, o.z, z - h), new Vector3(o.y, CEIL, z + h), mat);
                x = o.y;
            }
            if (x1 > x + 0.01f) BoxMM(p, name, new Vector3(x, CF, z - h), new Vector3(x1, CEIL, z + h), mat);
        }

        /// <summary>x 고정, z0~z1 벽. openings: (za, zb, 개구부 상단 y)</summary>
        static void WallZ(Transform p, string name, float x, float z0, float z1, Material mat, float th, params Vector3[] openings)
        {
            var ops = new List<Vector3>(openings);
            ops.Sort((a, b) => a.x.CompareTo(b.x));
            float z = z0, h = th * 0.5f;
            foreach (var o in ops)
            {
                if (o.x > z + 0.01f) BoxMM(p, name, new Vector3(x - h, CF, z), new Vector3(x + h, CEIL, o.x), mat);
                BoxMM(p, name + "_Lintel", new Vector3(x - h, o.z, o.x), new Vector3(x + h, CEIL, o.y), mat);
                z = o.y;
            }
            if (z1 > z + 0.01f) BoxMM(p, name, new Vector3(x - h, CF, z), new Vector3(x + h, CEIL, z1), mat);
        }

        static void Overlay(Transform p, Vector3 a, Vector3 b, Material mat)
        {
            var o = BoxMM(p, "Overlay", a, b, mat, false);
            o.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>경첩 기준 회전하는 문. yaw = 최종 회전각 (0 이면 +X 방향으로 닫힌 상태).</summary>
        static Transform Door(Transform p, string name, Vector3 hinge, float width, float yaw, Material mat, float height = 2.1f)
        {
            var h = Group(name, p, hinge, yaw);
            Box(h, "Panel", new Vector3(width * 0.5f, height * 0.5f, 0f), new Vector3(width - 0.04f, height, 0.05f), mat);
            Box(h, "Handle", new Vector3(width - 0.12f, 1.0f, 0.05f), new Vector3(0.14f, 0.03f, 0.04f), M("Stainless"), false);
            Box(h, "Handle2", new Vector3(width - 0.12f, 1.0f, -0.05f), new Vector3(0.14f, 0.03f, 0.04f), M("Stainless"), false);
            return h;
        }

        // ------------------------------------------------------------------ concourse interior
        static void BuildConcourseInterior(Transform root)
        {
            var g = Group("Concourse_Interior", root);
            BuildGateLine(Group("FareGates_개찰구", g));
            BuildFreePassage(Group("FreePassage_맞이방", g));
            BuildPaidArea(Group("PaidConcourse_대합실", g));
            BuildWestRooms(Group("WestBlock_화장실_기계실", g));
            BuildEastRooms(Group("EastBlock_역무실_휴게실", g));
            BuildStore(Group("Store_편의점", g));
        }

        static void GlassPartition(Transform p, float x0, float x1, float z)
        {
            var gl = BoxMM(p, "PartitionGlass", new Vector3(x0, CF + 0.1f, z - 0.015f), new Vector3(x1, CF + 1.15f, z + 0.015f), M("Glass"));
            gl.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            BoxMM(p, "PartitionTop", new Vector3(x0, CF + 1.15f, z - 0.04f), new Vector3(x1, CF + 1.21f, z + 0.04f), M("Stainless"), false);
            BoxMM(p, "PartitionBase", new Vector3(x0, CF, z - 0.06f), new Vector3(x1, CF + 0.1f, z + 0.06f), M("StainlessDark"), false);
            for (float x = x0; x <= x1 + 0.01f; x += 1.5f)
                BoxMM(p, "PartitionPost", new Vector3(x - 0.03f, CF, z - 0.03f), new Vector3(x + 0.03f, CF + 1.2f, z + 0.03f), M("Stainless"), false);
        }

        static void BuildGateLine(Transform g)
        {
            float z = GATEZ;
            GlassPartition(g, -ROOMX, -18.5f, z);
            GlassPartition(g, -16.5f, -6.6f, z);
            GlassPartition(g, 6.6f, 15.5f, z);
            GlassPartition(g, 17.5f, ROOMX, z);
            // 부서진 구간 (동선 연결)
            foreach (float x in new[] { -17.5f, 16.5f })
            {
                Box(g, "BrokenRail", new Vector3(x, CF + 0.2f, z + 0.4f), new Vector3(2.0f, 0.06f, 0.08f), M("Stainless"), false, new Vector3(0f, R(-30f, 30f), R(-8f, 8f)));
                for (int i = 0; i < 4; i++)
                    Quad(g, "Shard", new Vector3(x + R(-1f, 1f), CF + 0.015f, z + R(-1.2f, 1.2f)), new Vector2(R(0.3f, 0.7f), R(0.3f, 0.7f)), Vector3.up, M("GlassBroken"), R(0f, 360f));
            }
            // 개찰구 9대 (8 통로)
            float x0 = -6.5f, pitch = 13f / 8f;
            for (int i = 0; i <= 8; i++)
            {
                float x = x0 + i * pitch;
                var cab = Box(g, "GateCabinet", new Vector3(x, CF + 0.5f, z), new Vector3(0.24f, 1.0f, 1.7f), M("Stainless"));
                Box(g, "GateTop", new Vector3(x, CF + 1.02f, z), new Vector3(0.28f, 0.04f, 1.74f), M("DarkGray"), false);
                Box(g, "Reader", new Vector3(x, CF + 1.06f, z - 0.55f), new Vector3(0.2f, 0.05f, 0.25f), M(Chance(0.2f) ? "SignalRed" : "LampOff"), false);
            }
            int[] closedLanes = { 1, 4, 6 };
            foreach (int lane in closedLanes)
            {
                float cx = x0 + (lane + 0.5f) * pitch;
                Box(g, "Flap", new Vector3(cx - 0.42f, CF + 0.75f, z), new Vector3(0.6f, 0.45f, 0.03f), M("FireRed"));
                Box(g, "Flap", new Vector3(cx + 0.42f, CF + 0.75f, z), new Vector3(0.6f, 0.45f, 0.03f), M("FireRed"));
            }
            HangingSign(g, "GateSign", new Vector3(0f, 11.55f, z - 0.6f), 6.0f, 1.125f, Vector3.back, "S_ToPlatform", "S_ExitDir_B", CEIL, 0.4f);
            foreach (float x in new[] { -7.6f, 7.6f })
                Quad(g, "Notice", new Vector3(x, CF + 0.75f, z - 0.03f), new Vector2(0.5f, 0.67f), Vector3.back, SignMat("S_Notice", 0f));
            Tape(g, new Vector3(-6.5f, CF + 0.95f, z - 0.9f), new Vector3(6.5f, CF + 0.9f, z - 0.95f));
        }

        static void BuildFreePassage(Transform g)
        {
            // 승차권 발매기 / 교통카드 충전기
            foreach (float x in new[] { -12f, -10.8f, -9.6f, 9.6f, 10.8f, 12f })
            {
                var m = Group("TicketMachine", g, new Vector3(x, CF, GATEZ - 0.45f), 180f);
                Box(m, "Body", new Vector3(0f, 0.9f, 0f), new Vector3(1.0f, 1.8f, 0.6f), M("LightGray"));
                Box(m, "Hood", new Vector3(0f, 1.55f, 0.32f), new Vector3(0.9f, 0.08f, 0.2f), M("DarkGray"), false);
                bool on = Chance(0.35f);
                var scr = Quad(m, "Screen", new Vector3(0f, 1.2f, 0.31f), new Vector2(0.55f, 0.55f), Vector3.forward, SignMat("S_Ticket", on ? 0.6f : 0f));
                if (on) Flicker(scr, BJ_FlickerLight.Mode.Buzz, new Color(0.6f, 0.6f, 0.6f), null, scr.GetComponent<MeshRenderer>());
            }
            // 벤치
            foreach (float x in new[] { -32f, -26f, -6f, 0f, 6f, 20f, 26f, 32f })
                Bench(g, new Vector3(x, CF, CZ0 + 0.8f), 0f, Chance(0.15f), "SteelBlue");
            // 기둥
            foreach (var c in new[] { new Vector2(-8f, -13f), new Vector2(8f, -13f), new Vector2(-30f, -13f), new Vector2(30f, -13f) })
            {
                Cyl(g, "Column", new Vector3(c.x, CF, c.y), new Vector3(c.x, CEIL, c.y), 0.8f, M("Cladding"), true);
                if (Chance(0.6f)) Quad(g, "Missing", new Vector3(c.x, CF + 1.6f, c.y - 0.41f), new Vector2(0.45f, 0.6f), Vector3.back, SignMat("S_Missing", 0f));
            }
            // 출구 방향 표지 (양면)
            HangingSign(g, "ExitSignW", new Vector3(-21f, 11.6f, -12.5f), 3.2f, 0.8f, Vector3.right, "S_Exit2", "S_Exit1", CEIL, 0.45f);
            HangingSign(g, "ExitSignE", new Vector3(21f, 11.6f, -12.5f), 3.2f, 0.8f, Vector3.left, "S_Exit1", "S_Exit2", CEIL, 0.45f);
            HangingSign(g, "ExitSign3", new Vector3(-34f, 11.6f, -13f), 3.2f, 0.8f, Vector3.right, "S_Exit3", null, CEIL, 0.45f);
            HangingSign(g, "ToiletSign", new Vector3(-21f, 11.6f, -9.3f), 2.4f, 0.6f, Vector3.back, "S_Toilet", null, CEIL, 0.3f);
            // 역 안내도 / 광고
            Sign(g, "StationMap", new Vector3(-31.4f, CF + 1.6f, GATEZ - 0.16f), 2.0f, 1.5f, Vector3.back, "S_Map", 0.1f);
            Sign(g, "StationMap", new Vector3(26f, CF + 1.6f, GATEZ - 0.16f), 2.0f, 1.5f, Vector3.back, "S_Map", 0.1f);
            Sign(g, "AdRiceCake", new Vector3(33.3f, CF + 1.7f, GATEZ - 0.16f), 1.3f, 0.98f, Vector3.back, "S_Ad1", 0f);
            Sign(g, "AdNotice", new Vector3(-29.0f, CF + 1.7f, GATEZ - 0.16f), 1.2f, 0.9f, Vector3.back, "S_Ad3", 0f);
            // 낙서/데칼/쓰레기
            Decal(g, "G_NotExit", new Vector3(36.9f, CF + 2.6f, GATEZ - 0.12f), new Vector2(2.0f, 1.0f), Vector3.back);
            Scatter(g, new Vector3(CX0 + 1f, CF + 0.01f, CZ0 + 1f), new Vector3(CX1 - 1f, CF + 0.01f, GATEZ - 1f), 28);
            FloorDecal(g, "D_Blood", new Vector3(-2.5f, CF + 0.012f, -11.5f), 1.8f);
            Decal(g, "D_BloodSmear", new Vector3(-6f, CF + 0.012f, -11.8f), new Vector2(5f, 1.4f), Vector3.up, 8f);
            TrashBags(g, new Vector3(-36.5f, CF, -16.8f), 4);
            TrashBags(g, new Vector3(36.5f, CF, -17f), 3);
            Boxes(g, new Vector3(-24f, CF, -16.5f), 4);
            TrashBin(g, new Vector3(-3f, CF, -16.8f), true);
            TrashBin(g, new Vector3(3f, CF, -16.8f), false);
            WetFloorSign(g, new Vector3(-36f, CF, -10f), 40f);
            Barricade(g, new Vector3(CX1 - 1.4f, CF, -15.6f), 90f, false);
            Barricade(g, new Vector3(CX0 + 1.4f, CF, -10.8f), 90f, true);
            Quad(g, "Notice", new Vector3(CX1 - 1.37f, CF + 0.95f, -15.6f), new Vector2(0.45f, 0.6f), Vector3.right, SignMat("S_Notice", 0f));
        }

        static void BuildPaidArea(Transform g)
        {
            foreach (var c in new[] { new Vector2(-8f, 6f), new Vector2(8f, 6f) })
            {
                Cyl(g, "Column", new Vector3(c.x, CF, c.y), new Vector3(c.x, CEIL, c.y), 0.8f, M("Cladding"), true);
                Quad(g, "Missing", new Vector3(c.x, CF + 1.6f, c.y - 0.41f), new Vector2(0.45f, 0.6f), Vector3.back, SignMat("S_Missing", 0f));
            }
            foreach (float x in new[] { -4f, 4f })
                for (int k = 0; k < 2; k++)
                    Bench(g, new Vector3(x + k * 2.2f - 1.1f, CF, 10f), 180f, Chance(0.2f), "SteelBlue");
            TrashBin(g, new Vector3(-1.5f, CF, 13.5f), true);
            TrashBin(g, new Vector3(20f, CF, 14.5f), false);
            Decal(g, "G_LastTrain", new Vector3(-ROOMX + 0.13f, CF + 2.3f, 1.5f), new Vector2(3.2f, 1.6f), Vector3.right);
            Sign(g, "AdNotice", new Vector3(-ROOMX + 0.16f, CF + 1.7f, -4f), 1.2f, 0.9f, Vector3.right, "S_Ad3", 0f);
            Sign(g, "AdRiceCake", new Vector3(ROOMX - 0.16f, CF + 1.7f, 9f), 1.3f, 0.98f, Vector3.left, "S_Ad1", 0f);
            // 역무실에서 3·4번 계단으로 끌려간 핏자국
            for (int i = 0; i < 4; i++)
                Decal(g, "D_BloodSmear", new Vector3(22f - i * 1.9f, CF + 0.012f, -2.4f + R(-0.2f, 0.2f)), new Vector2(2.2f, 0.9f), Vector3.up, R(-8f, 8f));
            FloorDecal(g, "D_Blood", new Vector3(14.4f, CF + 0.012f, -3.8f), 2.0f);
            Scatter(g, new Vector3(-ROOMX + 1f, CF + 0.01f, GATEZ + 1f), new Vector3(ROOMX - 1f, CF + 0.01f, CZ1 - 1f), 22);
            Boxes(g, new Vector3(-22f, CF, 14.5f), 5);
            WetFloorSign(g, new Vector3(-10f, CF, -4.5f), -20f);
            Tape(g, new Vector3(IslandX[0] - 2.6f, CF + 0.95f, -3.4f), new Vector3(IslandX[0] + 2.5f, CF + 0.9f, -3.3f));
            Sign(g, "StaffOnly", new Vector3(-ROOMX + 0.14f, CF + 2.6f, 9.6f), 1.4f, 0.35f, Vector3.right, "S_StaffOnly");
            Sign(g, "MachineSign", new Vector3(-ROOMX + 0.14f, CF + 2.95f, 9.6f), 1.2f, 0.3f, Vector3.right, "S_Machine");
            Sign(g, "OfficeSign", new Vector3(ROOMX - 0.14f, CF + 2.55f, -2.4f), 1.2f, 0.3f, Vector3.left, "S_Office", 0.2f);
        }

        // ------------------------------------------------------------------ west block (restrooms + machine room)
        static void BuildWestRooms(Transform g)
        {
            float x0 = CX0, x1 = -ROOMX;
            var tile = M("WhiteTile");
            WallX(g, "WallS", GATEZ, x0 + 0.15f, x1, tile, 0.2f, new Vector3(-36.6f, -35.2f, CF + 2.3f), new Vector3(-27.6f, -26.2f, CF + 2.3f));
            WallZ(g, "WallE", x1, GATEZ, CZ1, M("WallPanel"), 0.2f, new Vector3(9f, 10.2f, CF + 2.2f));
            WallZ(g, "Divider", -31f, GATEZ, 4f, tile, 0.15f);
            WallX(g, "WallMid", 4f, x0 + 0.15f, x1, tile, 0.2f);
            WallX(g, "MachineInner", CZ1 - 0.35f, x0 + 0.15f, x1, M("ConcreteDark"), 0.15f);
            Overlay(g, new Vector3(x1 - 0.12f, CF, GATEZ + 0.1f), new Vector3(x1 - 0.1f, CEIL, 3.9f), tile);
            Overlay(g, new Vector3(x0 + 0.15f, CF, GATEZ + 0.1f), new Vector3(x0 + 0.17f, CEIL, 3.9f), tile);
            Overlay(g, new Vector3(x0 + 0.15f, CF + 0.001f, 4.1f), new Vector3(x1 - 0.1f, CEIL, 4.12f), M("ConcreteDark"));
            Overlay(g, new Vector3(x0 + 0.15f, CF, GATEZ + 0.1f), new Vector3(x1 - 0.1f, CF + 0.012f, 3.9f), tile);
            Sign(g, "MenSign", new Vector3(-35.9f, CF + 2.75f, GATEZ - 0.16f), 0.6f, 0.6f, Vector3.back, "S_Men", 0.15f);
            Sign(g, "WomenSign", new Vector3(-26.9f, CF + 2.75f, GATEZ - 0.16f), 0.6f, 0.6f, Vector3.back, "S_Women", 0.15f);

            // --- 남자 화장실 (x -38 ~ -31)
            var men = Group("Restroom_Men", g);
            BoxMM(men, "Baffle", new Vector3(-37.2f, CF, -6.7f), new Vector3(-34.6f, CF + 2.2f, -6.55f), tile);
            for (int i = 0; i < 4; i++)
            {
                float z = -4.6f + i * 1.0f;
                Box(men, "Urinal", new Vector3(-37.62f, CF + 0.75f, z), new Vector3(0.35f, 0.75f, 0.45f), M("Porcelain"));
                if (i < 3) Box(men, "UrinalDivider", new Vector3(-37.45f, CF + 1.0f, z + 0.5f), new Vector3(0.7f, 1.0f, 0.04f), M("PlasticBlue"), false);
            }
            Stalls(men, -37.85f, -33.6f, 2, 2.2f);
            Sinks(men, -31.08f, -5.5f, -1.6f, -1, false);
            Fixture(men, "RestroomLamp", new Vector3(-34.5f, CEIL - 0.04f, -2f), new Vector3(1.2f, 0.06f, 0.3f), LampState.Dying, SickGreen, 1.4f, 7f, false, "LampSick");
            FloorDecal(men, "D_Grime", new Vector3(-36.5f, CF + 0.015f, -3f), 2.4f);
            FloorDecal(men, "D_Leak", new Vector3(-33f, CF + 0.015f, 1f), 2f);
            Decal(men, "G_Tally", new Vector3(-31.08f, CF + 1.6f, 0.6f), new Vector2(1.2f, 1.2f), Vector3.left);

            // --- 여자 화장실 (x -31 ~ -24)
            var wom = Group("Restroom_Women", g);
            BoxMM(wom, "Baffle", new Vector3(-28.2f, CF, -6.7f), new Vector3(-25.6f, CF + 2.2f, -6.55f), tile);
            Stalls(wom, -30.85f, -24.25f, 3, 2.2f);
            Sinks(wom, -30.92f, -5.5f, -1.6f, +1, true);
            Fixture(wom, "RestroomLamp", new Vector3(-27.5f, CEIL - 0.04f, -2f), new Vector3(1.2f, 0.06f, 0.3f), LampState.Flicker, SickGreen, 1.4f, 7f, false, "LampSick");
            // 닫힌 칸 아래로 흘러나온 피
            FloorDecal(wom, "D_Blood", new Vector3(-27.5f, CF + 0.016f, 1.4f), 1.7f);
            Decal(wom, "G_Help", new Vector3(-24.125f, CF + 1.9f, -3.4f), new Vector2(2.2f, 1.1f), Vector3.left);

            // --- 기계실 (x -38 ~ -24, z 4 ~ 16)
            var mr = Group("MachineRoom_기계실", g);
            Door(mr, "Door_MachineRoom", new Vector3(x1, CF, 9f), 1.2f, -90f + 65f, M("DarkMetal"));
            for (int i = 0; i < 5; i++)
            {
                float z = 5.2f + i * 1.25f;
                Box(mr, "ElecPanel", new Vector3(-37.4f, CF + 1.0f, z), new Vector3(0.6f, 2.0f, 1.15f), M("LightGray"));
                Box(mr, "PanelStripe", new Vector3(-37.08f, CF + 1.75f, z), new Vector3(0.02f, 0.12f, 1.0f), M("Hazard"), false);
                if (Chance(0.4f)) Box(mr, "PanelLed", new Vector3(-37.08f, CF + 1.5f, z + 0.3f), new Vector3(0.02f, 0.04f, 0.04f), M("SignalRed"), false);
            }
            Box(mr, "Generator", new Vector3(-31f, CF + 0.8f, 9.5f), new Vector3(3.2f, 1.6f, 1.4f), M("SteelGreen"));
            Box(mr, "GeneratorExhaust", new Vector3(-30f, CF + 2.2f, 9.5f), new Vector3(0.35f, 1.2f, 0.35f), M("RustMetal"), false);
            Cyl(mr, "Pipe", new Vector3(-37.8f, CF + 3.4f, 6f), new Vector3(-24.3f, CF + 3.4f, 6f), 0.3f, M("RustMetal"));
            Cyl(mr, "Pipe", new Vector3(-37.8f, CF + 3.0f, 14.5f), new Vector3(-24.3f, CF + 3.0f, 14.5f), 0.22f, M("SteelGreen"));
            Cyl(mr, "PipeDrop", new Vector3(-27f, CF + 3.4f, 6f), new Vector3(-27f, CF, 6f), 0.25f, M("RustMetal"), true);
            Shelf(mr, new Vector3(-34f, CF, 15.0f), 180f, 2.4f, 2.0f, true);
            Shelf(mr, new Vector3(-30.5f, CF, 15.0f), 180f, 2.4f, 2.0f, true);
            Boxes(mr, new Vector3(-26f, CF, 13.5f), 6);
            Box(mr, "Ladder", new Vector3(-24.4f, CF + 1.8f, 14.8f), new Vector3(0.5f, 3.6f, 0.08f), M("Yellow"), false, new Vector3(-12f, 0f, 0f));
            Decal(mr, "G_Tally", new Vector3(-24.105f, CF + 1.7f, 6.0f), new Vector2(1.6f, 1.6f), Vector3.left);
            Decal(mr, "D_BloodSmear", new Vector3(-30f, CF + 0.015f, 7.4f), new Vector2(4f, 1.3f), Vector3.up, 160f);
            FloorDecal(mr, "D_Leak", new Vector3(-35f, CF + 0.015f, 12f), 3f);
            var red = PointLight(mr, "EmergencyRed", new Vector3(-31f, CEIL - 0.4f, 12f), new Color(1f, 0.08f, 0.04f), 2.2f, 10f);
            var lamp = Box(mr, "EmergencyLamp", new Vector3(-31f, CEIL - 0.1f, 12f), new Vector3(0.25f, 0.18f, 0.25f), M("EmergencyRed"), false);
            var f = Flicker(lamp, BJ_FlickerLight.Mode.Strobe, M("EmergencyRed").GetColor("_EmissionColor"), new[] { red }, lamp.GetComponent<MeshRenderer>());
            f.strobePeriod = 1.6f;
        }

        static void Stalls(Transform p, float xa, float xb, int count, float depth)
        {
            float w = (xb - xa) / count;
            float zf = 4f - depth;
            for (int i = 0; i < count; i++)
            {
                float a = xa + i * w, b = a + w, c = (a + b) * 0.5f;
                if (i > 0) BoxMM(p, "StallWall", new Vector3(a - 0.02f, CF + 0.15f, zf), new Vector3(a + 0.02f, CF + 2.1f, 3.9f), M("PlasticBlue"));
                BoxMM(p, "StallFront", new Vector3(a, CF + 0.15f, zf - 0.02f), new Vector3(c - 0.35f, CF + 2.1f, zf + 0.02f), M("PlasticBlue"));
                BoxMM(p, "StallFront", new Vector3(c + 0.35f, CF + 0.15f, zf - 0.02f), new Vector3(b, CF + 2.1f, zf + 0.02f), M("PlasticBlue"));
                Box(p, "Toilet", new Vector3(c, CF + 0.22f, 3.5f), new Vector3(0.42f, 0.44f, 0.6f), M("Porcelain"));
                Box(p, "Tank", new Vector3(c, CF + 0.6f, 3.82f), new Vector3(0.48f, 0.4f, 0.18f), M("Porcelain"), false);
                bool closed = (count == 3 && i == 1) || Chance(0.3f);
                Door(p, "StallDoor", new Vector3(c - 0.35f, CF + 0.15f, zf), 0.7f, closed ? 0f : -70f - R(0f, 30f), M("PlasticBlue"), 1.95f);
            }
        }

        static void Sinks(Transform p, float wallX, float z0, float z1, int dir, bool graffiti)
        {
            float x = wallX + dir * 0.32f;
            BoxMM(p, "Counter", new Vector3(x - 0.3f, CF + 0.78f, z0), new Vector3(x + 0.3f, CF + 0.88f, z1), M("Porcelain"));
            BoxMM(p, "CounterBase", new Vector3(x - 0.25f, CF, z0), new Vector3(x + 0.25f, CF + 0.78f, z0 + 0.1f), M("Porcelain"), false);
            for (float z = z0 + 0.8f; z < z1 - 0.3f; z += 1.2f)
                Box(p, "Basin", new Vector3(x, CF + 0.9f, z), new Vector3(0.4f, 0.05f, 0.5f), M("LightGray"), false);
            float mx = wallX + dir * 0.03f;
            BoxMM(p, "Mirror", new Vector3(mx - 0.01f, CF + 1.2f, z0 + 0.2f), new Vector3(mx + 0.01f, CF + 2.2f, z1 - 0.2f), M("Mirror"), false);
            Vector3 facing = new Vector3(dir, 0f, 0f);
            Decal(p, "D_Cracks", new Vector3(mx, CF + 1.7f, (z0 + z1) * 0.5f + 0.8f), new Vector2(1.2f, 1.2f), facing);
            if (graffiti) Decal(p, "G_DontLook", new Vector3(mx, CF + 1.7f, (z0 + z1) * 0.5f - 0.3f), new Vector2(2.2f, 1.1f), facing);
        }

        // ------------------------------------------------------------------ east block (office + lounge)
        static void BuildEastRooms(Transform g)
        {
            float x0 = ROOMX, x1 = CX1;
            var panel = M("WallPanel");
            // 남쪽 벽: 매표 창구 + 문
            float z = GATEZ, h = 0.1f;
            BoxMM(g, "WallS", new Vector3(x0, CF, z - h), new Vector3(28f, CEIL, z + h), panel);
            BoxMM(g, "WallS_Sill", new Vector3(28f, CF, z - h), new Vector3(32f, CF + 1.0f, z + h), panel);
            BoxMM(g, "WallS_Head", new Vector3(28f, CF + 2.2f, z - h), new Vector3(32f, CEIL, z + h), panel);
            var win = BoxMM(g, "ServiceWindow", new Vector3(28f, CF + 1.0f, z - 0.02f), new Vector3(32f, CF + 2.2f, z + 0.02f), M("Glass"));
            win.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            Quad(g, "ServiceWindowCrack", new Vector3(29.2f, CF + 1.6f, z - 0.03f), new Vector2(1.1f, 1.1f), Vector3.back, M("GlassBroken"));
            BoxMM(g, "WallS", new Vector3(32f, CF, z - h), new Vector3(34.6f, CEIL, z + h), panel);
            BoxMM(g, "WallS_Lintel", new Vector3(34.6f, CF + 2.2f, z - h), new Vector3(35.8f, CEIL, z + h), panel);
            BoxMM(g, "WallS", new Vector3(35.8f, CF, z - h), new Vector3(x1 - 0.15f, CEIL, z + h), panel);
            BoxMM(g, "Counter", new Vector3(28f, CF + 0.95f, z - 0.25f), new Vector3(32f, CF + 1.02f, z + 0.45f), M("Wood"), false);
            Sign(g, "OfficeSign", new Vector3(30f, CF + 2.6f, z - 0.16f), 1.6f, 0.4f, Vector3.back, "S_Office", 0.25f);
            WallZ(g, "WallW", x0, GATEZ, CZ1, panel, 0.2f, new Vector3(-3f, -1.8f, CF + 2.2f));
            WallX(g, "WallMid", 4f, x0 + 0.1f, x1 - 0.15f, panel, 0.15f, new Vector3(30f, 31.2f, CF + 2.2f));

            // --- 역무실
            var off = Group("StationOffice_역무실", g);
            Door(off, "Door_Office_Front", new Vector3(34.6f, CF, GATEZ), 1.2f, -100f, M("DarkMetal"));
            Door(off, "Door_Office_Paid", new Vector3(x0, CF, -3f), 1.2f, -90f + 80f, M("DarkMetal"));
            Desk(off, new Vector3(28f, CF, -5.5f), 180f, false);
            Desk(off, new Vector3(30.5f, CF, -5.5f), 180f, true);
            Desk(off, new Vector3(33f, CF, -0.5f), 0f, false);
            for (int i = 0; i < 4; i++) Box(off, "FileCabinet", new Vector3(37.5f, CF + 0.65f, -6.5f + i * 0.55f), new Vector3(0.6f, 1.3f, 0.5f), M("LightGray"));
            Box(off, "Safe", new Vector3(37.4f, CF + 0.45f, -3.6f), new Vector3(0.7f, 0.9f, 0.7f), M("DarkMetal"));
            // CCTV 모니터 벽
            Box(off, "MonitorRack", new Vector3(27f, CF + 1.4f, 3.7f), new Vector3(3.2f, 2.0f, 0.4f), M("DarkMetal"));
            var screens = new List<Renderer>();
            for (int r = 0; r < 2; r++)
                for (int c = 0; c < 3; c++)
                {
                    var pos = new Vector3(26f + c * 1.0f, CF + 1.0f + r * 0.85f, 3.48f);
                    Box(off, "Monitor", pos + new Vector3(0f, 0f, 0.04f), new Vector3(0.9f, 0.7f, 0.08f), M("Black"), false);
                    if ((r + c) % 2 == 0)
                        screens.Add(Quad(off, "Screen", pos + new Vector3(0f, 0f, -0.005f), new Vector2(0.8f, 0.6f), Vector3.back, M("ScreenStatic")).GetComponent<MeshRenderer>());
                }
            var cctvLight = PointLight(off, "CCTVGlow", new Vector3(27f, CF + 1.5f, 2.5f), new Color(0.55f, 0.7f, 1f), 1.2f, 6f);
            Flicker(off.Find("MonitorRack").gameObject, BJ_FlickerLight.Mode.Buzz, M("ScreenStatic").GetColor("_EmissionColor"), new[] { cctvLight }, screens.ToArray());
            var board = Box(off, "NoticeBoard", new Vector3(x0 + 0.14f, CF + 1.7f, 1.5f), new Vector3(0.04f, 1.1f, 1.8f), M("Wood"), false);
            Quad(off, "Notice", new Vector3(x0 + 0.17f, CF + 1.7f, 1.0f), new Vector2(0.5f, 0.66f), Vector3.right, SignMat("S_Notice", 0f), R(-5f, 5f));
            Quad(off, "Missing", new Vector3(x0 + 0.17f, CF + 1.7f, 2.0f), new Vector2(0.5f, 0.66f), Vector3.right, SignMat("S_Missing", 0f), R(-5f, 5f));
            Quad(off, "Clock", new Vector3(34f, CF + 2.6f, 3.9f), new Vector2(0.5f, 0.5f), Vector3.back, SignMat("S_Clock", 0f));
            Scatter(off, new Vector3(25f, CF + 0.012f, -7f), new Vector3(37f, CF + 0.012f, 3f), 8, "D_Papers", "D_Papers", "D_Grime");
            FloorDecal(off, "D_Blood", new Vector3(31f, CF + 0.014f, -3f), 1.6f);
            Sign(off, "LoungeSign", new Vector3(30.6f, CF + 2.45f, 3.9f), 1.2f, 0.3f, Vector3.back, "S_Lounge");
            Fixture(off, "DeskLamp", new Vector3(33.4f, CF + 1.15f, -0.6f), new Vector3(0.25f, 0.05f, 0.15f), LampState.Flicker, WarmWhite, 1.4f, 5f, false, "LampWarm", new Vector3(0f, 0.1f, 0f));

            // --- 직원 휴게실
            var lo = Group("StaffLounge_휴게실", g);
            Door(lo, "Door_Lounge", new Vector3(30f, CF, 4f), 1.2f, -75f, M("Wood"));
            Lockers(lo, new Vector3(37.5f, CF, 9f), -90f, 8);
            Box(lo, "SofaBase", new Vector3(28f, CF + 0.22f, 9f), new Vector3(2.2f, 0.44f, 0.9f), M("Sofa"));
            Box(lo, "SofaBack", new Vector3(28f, CF + 0.65f, 9.4f), new Vector3(2.2f, 0.6f, 0.2f), M("Sofa"), false);
            Box(lo, "Table", new Vector3(28f, CF + 0.22f, 7.8f), new Vector3(1.2f, 0.44f, 0.6f), M("Wood"));
            Box(lo, "TVStand", new Vector3(28f, CF + 0.3f, 5.1f), new Vector3(1.6f, 0.6f, 0.45f), M("Wood"));
            Box(lo, "TV", new Vector3(28f, CF + 1.0f, 5.15f), new Vector3(1.2f, 0.72f, 0.08f), M("Black"), false);
            var tv = Quad(lo, "TVScreen", new Vector3(28f, CF + 1.0f, 5.2f), new Vector2(1.1f, 0.62f), Vector3.forward, M("ScreenStatic"));
            var tvl = PointLight(lo, "TVGlow", new Vector3(28f, CF + 1.0f, 6.2f), new Color(0.6f, 0.75f, 1f), 1.4f, 6f);
            Flicker(tv, BJ_FlickerLight.Mode.Flicker, M("ScreenStatic").GetColor("_EmissionColor"), new[] { tvl }, tv.GetComponent<MeshRenderer>());
            Box(lo, "BunkFrame", new Vector3(24.9f, CF + 0.9f, 12.5f), new Vector3(0.9f, 1.8f, 2.0f), M("SteelGreen"), true);
            Box(lo, "Mattress", new Vector3(24.9f, CF + 0.45f, 12.5f), new Vector3(0.85f, 0.15f, 1.95f), M("Paper"), false);
            Box(lo, "Mattress2", new Vector3(24.9f, CF + 1.45f, 12.5f), new Vector3(0.85f, 0.15f, 1.95f), M("Paper"), false);
            Box(lo, "Kitchenette", new Vector3(33f, CF + 0.45f, 15.3f), new Vector3(3f, 0.9f, 0.6f), M("LightGray"));
            Box(lo, "Fridge", new Vector3(35.2f, CF + 0.85f, 15.3f), new Vector3(0.7f, 1.7f, 0.65f), M("Plastic"));
            for (int i = 0; i < 4; i++)
                Decal(lo, "D_BloodSmear", new Vector3(30.6f - i * 0.25f, CF + 0.013f, 5.0f + i * 1.0f), new Vector2(1.6f, 0.8f), Vector3.up, 80f + R(-15f, 15f));
            FloorDecal(lo, "D_Blood", new Vector3(29.3f, CF + 0.013f, 8.7f), 1.5f);
        }

        // ------------------------------------------------------------------ convenience store
        static void BuildStore(Transform g)
        {
            float xa = -20f, xb = -12f, za = CZ0 + 0.15f, zf = -14.5f;
            var panel = M("WallPanel");
            BoxMM(g, "WallW", new Vector3(xa - 0.1f, CF, za), new Vector3(xa + 0.1f, CEIL, zf), panel);
            BoxMM(g, "WallE", new Vector3(xb - 0.1f, CF, za), new Vector3(xb + 0.1f, CEIL, zf), panel);
            BoxMM(g, "Transom", new Vector3(xa, CF + 2.6f, zf - 0.1f), new Vector3(xb, CEIL, zf + 0.1f), panel);
            var g1 = BoxMM(g, "Front", new Vector3(xa + 0.1f, CF, zf - 0.02f), new Vector3(-14.6f, CF + 2.6f, zf + 0.02f), M("Glass"));
            var g2 = BoxMM(g, "Front", new Vector3(-13.4f, CF, zf - 0.02f), new Vector3(xb - 0.1f, CF + 2.6f, zf + 0.02f), M("Glass"));
            g1.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            g2.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            BoxMM(g, "Shutter", new Vector3(xa, CF + 2.0f, zf + 0.05f), new Vector3(xb, CF + 2.6f, zf + 0.14f), M("Shutter"), false);
            Sign(g, "StoreSign", new Vector3(-16f, CF + 3.15f, zf + 0.16f), 3.2f, 0.8f, Vector3.forward, "S_Store", 0.2f);
            Quad(g, "Notice", new Vector3(-17.5f, CF + 1.3f, zf + 0.03f), new Vector2(0.45f, 0.6f), Vector3.forward, SignMat("S_Notice", 0f));
            Shelf(g, new Vector3(-16.8f, CF, -16.0f), 0f, 3.0f, 1.6f, true);
            Shelf(g, new Vector3(-16.8f, CF, -17.3f), 0f, 3.0f, 1.6f, true);
            Box(g, "Counter", new Vector3(-13.0f, CF + 0.5f, -16.6f), new Vector3(0.7f, 1.0f, 2.0f), M("LightGray"));
            var fridge = Box(g, "Fridge", new Vector3(-19.55f, CF + 1.0f, -16.2f), new Vector3(0.7f, 2.0f, 3.0f), M("FridgeGlow"));
            var fl = PointLight(g, "FridgeLight", new Vector3(-18.6f, CF + 1.2f, -16.2f), new Color(0.6f, 0.95f, 1f), 1.2f, 5f);
            Flicker(fridge, BJ_FlickerLight.Mode.Buzz, M("FridgeGlow").GetColor("_EmissionColor"), new[] { fl }, fridge.GetComponent<MeshRenderer>());
            for (int i = 0; i < 8; i++)
                Box(g, "FallenItem", new Vector3(R(-19f, -13.5f), CF + 0.08f, R(-17.6f, -14.8f)), new Vector3(R(0.12f, 0.3f), R(0.1f, 0.2f), R(0.1f, 0.25f)),
                    M(Pick(new[] { "Cardboard", "Plastic", "Red", "PlasticBlue" })), false, new Vector3(R(0f, 90f), R(0f, 360f), 0f));
            Scatter(g, new Vector3(-19.5f, CF + 0.012f, -17.8f), new Vector3(-12.5f, CF + 0.012f, -14.8f), 4, "D_Papers", "D_Grime");
            Decal(g, "G_GoBack", new Vector3(xa - 0.12f, CF + 2.0f, -16.2f), new Vector2(2.6f, 1.3f), Vector3.left);
        }
    }
}
