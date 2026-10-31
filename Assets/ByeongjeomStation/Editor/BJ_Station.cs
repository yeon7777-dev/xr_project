using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Byeongjeom;

namespace Byeongjeom.MapGen
{
    /// <summary>
    /// 병점역 본체: 선로, 쌍섬식 승강장 2면(1·2번 / 3·4번), 승강장 지붕, 스크린도어,
    /// 2층 선상 대합실 골조(바닥·외벽·지붕·천장), 계단/에스컬레이터/엘리베이터.
    /// </summary>
    public static partial class BJ
    {
        // ------------------------------------------------------------------ layout (m)
        public const float PF = 1.70f;      // 승강장 바닥 높이
        public const float CF = 8.60f;      // 2층 대합실 바닥 높이
        public const float SLAB = 8.00f;    // 대합실 슬래브 하단
        public const float CEIL = 12.8f;    // 대합실 천장
        public const float WALLTOP = 14.2f; // 대합실 외벽 상단
        public const float CX0 = -38f, CX1 = 38f, CZ0 = -18f, CZ1 = 16f; // 대합실 외곽
        public const float GATEZ = -8f;     // 개찰구 라인 (남쪽 = 자유통로/맞이방, 북쪽 = 대합실)
        public const float ROOMX = 24f;     // |x| > 24 : 양 끝 실(화장실·기계실 / 역무실·휴게실)
        public const float PZ0 = -75f, PZ1 = 75f;   // 승강장 길이 150m
        public const float TRACKZ = 160f;           // 선로 시각적 끝
        public const float BOUNDZ = 121f;           // 선로 구간 이동 한계
        public const float FENCEX = 25.5f;
        public static readonly float[] TrackX = { -21.15f, -7.75f, -2.25f, 2.25f, 7.75f, 21.15f };
        public static readonly float[] IslandX = { -14.45f, 14.45f };
        public const float ISLW = 10f;
        static readonly float[] CarSlots = { -60f, -40f, -20f, 0f, 20f, 40f, 60f };
        static readonly float[] DoorOffsets = { -7.35f, -2.45f, 2.45f, 7.35f };
        static readonly float[] TrainCars = { 0f, 20f, 40f, 60f };  // 1번선에 버려진 전동차 4량

        // ------------------------------------------------------------------ tracks
        static void BuildTrackBed(Transform root)
        {
            var g = Group("Tracks", root);
            BoxMM(g, "Ballast", new Vector3(-FENCEX, -0.5f, -TRACKZ), new Vector3(FENCEX, 0.3f, TRACKZ), M("Ballast"));
            for (int i = 0; i < TrackX.Length; i++)
            {
                float tx = TrackX[i];
                var tg = Group("Track_" + (i + 1), g);
                var sleepers = new MeshBuilder();
                for (float z = -TRACKZ + 0.3f; z < TRACKZ; z += 0.6f)
                    sleepers.AddBox(new Vector3(tx, 0.335f, z), new Vector3(2.6f, 0.17f, 0.24f), Quaternion.Euler(0f, R(-1.5f, 1.5f), 0f), false);
                var sl = Combined(tg, "Sleepers", sleepers, M("Sleeper"), false);
                sl.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.On;
                for (int s = -1; s <= 1; s += 2)
                {
                    float rx = tx + s * 0.7175f;
                    Box(tg, "Rail", new Vector3(rx, 0.5f, 0f), new Vector3(0.075f, 0.16f, TRACKZ * 2f), M("Rail"));
                    Box(tg, "RailHead", new Vector3(rx, 0.584f, 0f), new Vector3(0.076f, 0.012f, TRACKZ * 2f), M("RailTop"), false);
                }
            }
            // 선로 위 쓰레기
            Scatter(g, new Vector3(-24f, 0.3f, -110f), new Vector3(24f, 0.3f, 110f), 60, "D_Papers", "D_Grime", "D_Leaves", "D_Grime");
        }

        // ------------------------------------------------------------------ platforms
        static void BuildPlatforms(Transform root)
        {
            for (int i = 0; i < 2; i++)
            {
                float cx = IslandX[i];
                float e0 = cx - ISLW * 0.5f, e1 = cx + ISLW * 0.5f;
                var g = Group(i == 0 ? "Platform_1-2 (광운대·청량리 방면)" : "Platform_3-4 (천안·신창·서동탄 방면)", root);

                BoxMM(g, "Body", new Vector3(e0 + 0.06f, -0.3f, PZ0), new Vector3(e1 - 0.06f, PF - 0.08f, PZ1), M("Concrete"));
                BoxMM(g, "Floor", new Vector3(e0 + 0.65f, PF - 0.08f, PZ0), new Vector3(e1 - 0.65f, PF, PZ1), M("PlatformFloor"));
                BoxMM(g, "CopingW", new Vector3(e0, PF - 0.08f, PZ0), new Vector3(e0 + 0.35f, PF, PZ1), M("Coping"));
                BoxMM(g, "TactileW", new Vector3(e0 + 0.35f, PF - 0.08f, PZ0), new Vector3(e0 + 0.65f, PF + 0.005f, PZ1), M("Tactile"));
                BoxMM(g, "CopingE", new Vector3(e1 - 0.35f, PF - 0.08f, PZ0), new Vector3(e1, PF, PZ1), M("Coping"));
                BoxMM(g, "TactileE", new Vector3(e1 - 0.65f, PF - 0.08f, PZ0), new Vector3(e1 - 0.35f, PF + 0.005f, PZ1), M("Tactile"));
                // 선형 점자블록 (계단 쪽으로 안내)
                BoxMM(g, "TactileGuide", new Vector3(cx - 3.3f, PF, -60f), new Vector3(cx - 3.0f, PF + 0.006f, -20f), M("TactileLine"), false);
                BoxMM(g, "TactileGuide", new Vector3(cx - 3.3f, PF, 17f), new Vector3(cx - 3.0f, PF + 0.006f, 60f), M("TactileLine"), false);

                // 스크린도어 (반높이)
                BuildPSD(g, e0 + 0.18f, +1, i == 0 ? "W1" : "W3", i == 0);
                BuildPSD(g, e1 - 0.18f, -1, i == 0 ? "E2" : "E4", false);

                // 승강장 양 끝 경사로 + 관계자외 출입금지
                for (int s = -1; s <= 1; s += 2) PlatformEnd(g, cx, e0, e1, s);

                // 지붕 (대합실 남/북)
                Canopy(g, cx, -62f, -19f, i);
                Canopy(g, cx, 17f, 62f, i + 2);

                // 대합실 아래 승강장 조명
                for (float z = -15f; z <= 14f; z += 6f)
                    foreach (float ox in new[] { -2.9f, 3.0f })
                        Fixture(g, "UnderLamp", new Vector3(cx + ox, SLAB - 0.05f, z), new Vector3(0.25f, 0.08f, 1.25f),
                                RandomLamp(0.25f, 0.1f, 0.1f), CoolWhite, 1.6f, 8f);

                // 대기실(맞이방) 유리부스
                WaitingRoom(g, new Vector3(cx, PF, i == 0 ? 34.5f : -35.5f), i == 0);
                VendingMachine(g, new Vector3(cx - 3.15f, PF, -12.5f), 90f, i == 0);
                if (i == 1) VendingMachine(g, new Vector3(cx - 3.15f, PF, -11.3f), 90f, false);

                // 벤치/쓰레기통/소품
                foreach (float z in new[] { -53.5f, -44.5f, -26.5f, 25.5f, 43.5f, 52.5f })
                {
                    bool fallen = Chance(0.2f);
                    Bench(g, new Vector3(cx + 0.35f, PF, z), 90f, fallen);
                    Bench(g, new Vector3(cx - 0.35f, PF, z), -90f, !fallen && Chance(0.15f));
                }
                foreach (float z in new[] { -57f, -30f, 21.5f, 47f, 66f, -66f })
                    TrashBin(g, new Vector3(cx + R(-1.5f, 1.5f), PF, z), Chance(0.35f));
                Scatter(g, new Vector3(e0 + 1f, PF + 0.01f, PZ0 + 2f), new Vector3(e1 - 1f, PF + 0.01f, PZ1 - 2f), 26);
                TrashBags(g, new Vector3(cx + 3f, PF, -63f), 4);
                Boxes(g, new Vector3(cx - 2.5f, PF, 63.5f), 5);
                WetFloorSign(g, new Vector3(cx + 2.6f, PF, 12.0f), 30f);
                FloorDecal(g, "D_Blood", new Vector3(cx + 3.6f, PF + 0.01f, i == 0 ? 52f : -48f), 2.2f);
                Decal(g, "D_BloodSmear", new Vector3(cx + 2.4f, PF + 0.01f, i == 0 ? 49.5f : -45.5f), new Vector2(4.5f, 1.6f), Vector3.up, 90f);
                FloorDecal(g, "D_Leak", new Vector3(cx, PF + 0.01f, -16f), 3f);
            }
        }

        static void PlatformEnd(Transform g, float cx, float e0, float e1, int s)
        {
            float zEnd = s > 0 ? PZ1 : PZ0;
            float run = 7f, drop = PF - 0.3f;
            float len = Mathf.Sqrt(run * run + drop * drop);
            float ang = Mathf.Atan2(drop, run) * Mathf.Rad2Deg;
            var rg = Group("EndRamp", g, new Vector3(cx, (PF + 0.3f) * 0.5f - 0.1f, zEnd + s * run * 0.5f), 0f);
            rg.localRotation = Quaternion.Euler(s * ang, 0f, 0f);
            Box(rg, "Ramp", Vector3.zero, new Vector3(1.8f, 0.2f, len), M("ConcreteLight"));
            for (int k = -1; k <= 1; k += 2)
            {
                Box(rg, "Rail", new Vector3(k * 0.95f, 1.05f, 0f), new Vector3(0.05f, 0.05f, len), M("Yellow"), false);
                Box(rg, "PostA", new Vector3(k * 0.95f, 0.55f, len * 0.45f), new Vector3(0.05f, 1.0f, 0.05f), M("Yellow"), false);
                Box(rg, "PostB", new Vector3(k * 0.95f, 0.55f, -len * 0.45f), new Vector3(0.05f, 1.0f, 0.05f), M("Yellow"), false);
            }
            // 끝 난간 (경사로 입구만 열림)
            var railMat = M("Yellow");
            BoxMM(g, "EndRail", new Vector3(e0 + 0.5f, PF, zEnd - 0.04f), new Vector3(cx - 0.95f, PF + 1.1f, zEnd + 0.04f), railMat);
            BoxMM(g, "EndRail", new Vector3(cx + 0.95f, PF, zEnd - 0.04f), new Vector3(e1 - 0.5f, PF + 1.1f, zEnd + 0.04f), railMat);
            // 반쯤 열린 문짝
            var hinge = Group("Gate", g, new Vector3(cx - 0.9f, PF, zEnd), 0f);
            hinge.localRotation = Quaternion.Euler(0f, s > 0 ? -70f : 250f, 0f);
            Box(hinge, "Leaf", new Vector3(0.45f, 0.55f, 0f), new Vector3(0.9f, 1.0f, 0.04f), M("Yellow"));
            Sign(g, "StaffOnly", new Vector3(cx + 2.6f, PF + 1.45f, zEnd - s * 0.1f), 1.4f, 0.35f, new Vector3(0f, 0f, -s), "S_StaffOnly");
        }

        // ------------------------------------------------------------------ platform screen doors
        static void BuildPSD(Transform p, float x, int inward, string id, bool trainSide)
        {
            var g = Group("ScreenDoor_" + id, p);
            var frames = new MeshBuilder();
            var glass = new MeshBuilder();
            var doors = new List<float>();
            foreach (float c in CarSlots) foreach (float o in DoorOffsets) doors.Add(c + o);
            doors.Sort();
            float z0 = PZ0 + 3f, z1 = PZ1 - 3f, half = 1.05f, H = 1.55f;
            float start = z0;
            int doorNo = 0;
            foreach (float d in doors)
            {
                FixedPanels(g, frames, glass, x, start, d - half, H, inward);
                doorNo++;
                frames.AddBox(new Vector3(x, PF + H * 0.5f, d - half), new Vector3(0.16f, H + 0.05f, 0.16f));
                frames.AddBox(new Vector3(x, PF + H * 0.5f, d + half), new Vector3(0.16f, H + 0.05f, 0.16f));
                bool trainDoor = trainSide && d > TrainCars[0] - 10.5f && d < TrainCars[TrainCars.Length - 1] + 10.5f;
                double roll = rng.NextDouble();
                if (trainDoor || roll < 0.15)
                {
                    // 열린 문 - 통과 가능
                }
                else if (roll < 0.25)
                {
                    // 부서진 문: 한 짝은 바닥에 쓰러짐
                    Box(g, "FallenLeaf", new Vector3(x + inward * R(0.9f, 1.4f), PF + 0.03f, d + R(-0.4f, 0.4f)), new Vector3(1.03f, 0.05f, 1.45f),
                        M("StainlessDark"), true, new Vector3(0f, R(-30f, 30f), 0f));
                    frames.AddBox(new Vector3(x, PF + 0.75f, d - half + 0.55f), new Vector3(0.05f, 1.45f, 0.06f));
                    glass.AddBox(new Vector3(x, PF + 0.75f, d - half + 0.3f), new Vector3(0.02f, 1.3f, 0.45f));
                    Blocker(g, "DoorBlock", new Vector3(x - 0.08f, PF, d - half), new Vector3(x + 0.08f, PF + 1.6f, d - half + 0.6f));
                }
                else
                {
                    for (int k = -1; k <= 1; k += 2)
                    {
                        float lc = d + k * 0.52f;
                        frames.AddBox(new Vector3(x, PF + 1.44f, lc), new Vector3(0.06f, 0.08f, 1.02f));
                        frames.AddBox(new Vector3(x, PF + 0.06f, lc), new Vector3(0.06f, 0.12f, 1.02f));
                        frames.AddBox(new Vector3(x, PF + 0.75f, d + k * 0.03f), new Vector3(0.06f, 1.45f, 0.06f));
                        frames.AddBox(new Vector3(x, PF + 0.75f, d + k * 1.0f), new Vector3(0.06f, 1.45f, 0.05f));
                        glass.AddBox(new Vector3(x, PF + 0.76f, lc), new Vector3(0.02f, 1.26f, 0.9f));
                    }
                    Blocker(g, "DoorBlock", new Vector3(x - 0.08f, PF, d - half), new Vector3(x + 0.08f, PF + 1.6f, d + half));
                }
                start = d + half;
            }
            FixedPanels(g, frames, glass, x, start, z1, H, inward);
            Combined(g, "Frames", frames, M("Stainless"), false);
            var gl = Combined(g, "Glass", glass, M("Glass"), false);
            if (gl != null) gl.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        static void FixedPanels(Transform g, MeshBuilder frames, MeshBuilder glass, float x, float za, float zb, float H, int inward)
        {
            float len = zb - za;
            if (len < 0.2f) return;
            int n = Mathf.Max(1, Mathf.RoundToInt(len / 1.6f));
            float w = len / n;
            for (int i = 0; i < n; i++)
            {
                float a = za + i * w, b = a + w, m = (a + b) * 0.5f;
                frames.AddBox(new Vector3(x, PF + H * 0.5f, a), new Vector3(0.12f, H, 0.12f));
                frames.AddBox(new Vector3(x, PF + H - 0.04f, m), new Vector3(0.14f, 0.08f, w));
                frames.AddBox(new Vector3(x, PF + 0.06f, m), new Vector3(0.12f, 0.12f, w));
                if (Chance(0.08f))
                    Quad(g, "BrokenPanel", new Vector3(x, PF + 0.79f, m), new Vector2(w - 0.12f, 1.33f), new Vector3(inward, 0f, 0f), M("GlassBroken"));
                else
                    glass.AddBox(new Vector3(x, PF + 0.79f, m), new Vector3(0.02f, 1.33f, w - 0.12f));
                if (Chance(0.07f))
                    Quad(g, "Poster", new Vector3(x + inward * 0.02f, PF + 0.85f, m), new Vector2(0.75f, 0.56f), new Vector3(inward, 0f, 0f),
                         SignMat(Chance(0.5f) ? "S_Ad2" : "S_Ad3", 0f));
            }
            frames.AddBox(new Vector3(x, PF + H * 0.5f, zb), new Vector3(0.12f, H, 0.12f));
            Blocker(g, "PanelBlock", new Vector3(x - 0.08f, PF, za), new Vector3(x + 0.08f, PF + 1.6f, zb));
        }

        // ------------------------------------------------------------------ canopy
        static void Canopy(Transform p, float cx, float z0, float z1, int idx)
        {
            var g = Group("Canopy", p);
            var steel = M("SteelBlue");
            var cols = new List<float>();
            for (float z = z0 + 4f; z < z1 - 2f; z += 9f) cols.Add(z);
            foreach (float z in cols)
            {
                BoxMM(g, "Column", new Vector3(cx - 0.18f, PF, z - 0.18f), new Vector3(cx + 0.18f, 7.05f, z + 0.18f), steel);
                BoxMM(g, "Base", new Vector3(cx - 0.3f, PF, z - 0.3f), new Vector3(cx + 0.3f, PF + 0.12f, z + 0.3f), M("DarkMetal"), false);
                BoxMM(g, "CrossBeam", new Vector3(cx - 4.3f, 7.05f, z - 0.12f), new Vector3(cx + 4.3f, 7.4f, z + 0.12f), steel, false);
                if (Chance(0.3f)) FireCabinet(g, new Vector3(cx + 0.19f, PF + 1.2f, z), Vector3.right);
            }
            BoxMM(g, "Beam", new Vector3(cx - 0.2f, 7.0f, z0), new Vector3(cx + 0.2f, 7.4f, z1), steel, false);
            BoxMM(g, "Deck", new Vector3(cx - 4.4f, 7.4f, z0), new Vector3(cx + 4.4f, 7.52f, z1), M("PaintedMetal"), false);
            BoxMM(g, "FasciaW", new Vector3(cx - 4.5f, 7.2f, z0), new Vector3(cx - 4.4f, 7.75f, z1), M("Cladding"), false);
            BoxMM(g, "FasciaE", new Vector3(cx + 4.4f, 7.2f, z0), new Vector3(cx + 4.5f, 7.75f, z1), M("Cladding"), false);

            bool shadowUsed = false;
            for (int k = 0; k < cols.Count; k++)
            {
                float zf = cols[k] + 4.5f;
                if (zf > z1 - 1f) continue;
                foreach (float ox in new[] { -1.6f, 1.6f })
                {
                    var st = RandomLamp(0.3f, 0.15f, 0.1f);
                    bool sh = !shadowUsed && st == LampState.On && idx == 0;
                    if (sh) shadowUsed = true;
                    Fixture(g, "CanopyLamp", new Vector3(cx + ox, 6.98f, zf), new Vector3(0.22f, 0.08f, 1.25f), st, CoolWhite, 2.0f, 9f, sh);
                }
            }
            // 역명판 (선로 쪽을 바라봄)
            float zs = cols.Count > 2 ? cols[2] + 4.5f : (z0 + z1) * 0.5f;
            if (zs > z1 - 3f) zs = (z0 + z1) * 0.5f;
            for (int s = -1; s <= 1; s += 2)
                HangingSign(g, "StationName", new Vector3(cx + s * 4.05f, 5.9f, zs - 3.0f), 4.0f, 1.0f, new Vector3(s, 0f, 0f), "S_StationName", null, 7.4f, 0.25f);
            // 타는 곳 / LED 안내기
            bool south = z1 < 0f;
            string plat = cx < 0f ? "S_Platform12" : "S_Platform34";
            float zSign = south ? z1 - 2f : z0 + 2f;
            HangingSign(g, "PlatformSign", new Vector3(cx, 5.8f, zSign), 3.0f, 0.75f, new Vector3(0f, 0f, south ? -1f : 1f), plat, plat, 7.4f, 0.3f);
            float zLed = south ? z1 - 6.5f : z0 + 6.5f;
            var led = HangingSign(g, "LEDBoard", new Vector3(cx, 5.6f, zLed), 2.6f, 0.33f, new Vector3(0f, 0f, south ? -1f : 1f), "S_LED", "S_LED", 7.4f, 1.2f);
            var faces = led.GetComponentsInChildren<MeshRenderer>();
            var list = new List<Renderer>();
            foreach (var r in faces) if (r.gameObject.name != "Frame") list.Add(r);
            Flicker(led, BJ_FlickerLight.Mode.Flicker, new Color(1.2f, 1.2f, 1.2f), null, list.ToArray());
            Cctv(g, new Vector3(cx + 3.5f, 7.0f, zLed + 3f), south ? 180f : 0f);
        }

        static void WaitingRoom(Transform p, Vector3 c, bool creepy)
        {
            var g = Group("WaitingRoom", p, c);
            float hx = 1.6f, hz = 3.0f, H = 2.7f;
            Box(g, "Curb", new Vector3(0f, 0.05f, 0f), new Vector3(hx * 2f, 0.1f, hz * 2f), M("ConcreteLight"));
            Box(g, "Roof", new Vector3(0f, H + 0.08f, 0f), new Vector3(hx * 2f + 0.2f, 0.16f, hz * 2f + 0.2f), M("DarkMetal"));
            foreach (var cpos in new[] { new Vector3(-hx, 0f, -hz), new Vector3(hx, 0f, -hz), new Vector3(-hx, 0f, hz), new Vector3(hx, 0f, hz), new Vector3(-hx, 0f, 0f), new Vector3(hx, 0f, 0f) })
                Box(g, "Post", cpos + Vector3.up * H * 0.5f, new Vector3(0.08f, H, 0.08f), M("Stainless"));
            Box(g, "GlassW", new Vector3(-hx, H * 0.5f, 0f), new Vector3(0.03f, H - 0.1f, hz * 2f), M("Glass"));
            Box(g, "GlassN", new Vector3(0f, H * 0.5f, hz), new Vector3(hx * 2f, H - 0.1f, 0.03f), M("Glass"));
            Box(g, "GlassS", new Vector3(0f, H * 0.5f, -hz), new Vector3(hx * 2f, H - 0.1f, 0.03f), M("Glass"));
            // 동쪽 면: 문 자리 비움
            Box(g, "GlassE1", new Vector3(hx, H * 0.5f, -hz * 0.5f - 0.6f), new Vector3(0.03f, H - 0.1f, hz - 1.2f), M("Glass"));
            Box(g, "GlassE2", new Vector3(hx, H * 0.5f, hz * 0.5f + 0.6f), new Vector3(0.03f, H - 0.1f, hz - 1.2f), M("Glass"));
            Bench(g, new Vector3(-hx + 0.4f, 0.1f, -1.2f), 90f);
            Bench(g, new Vector3(-hx + 0.4f, 0.1f, 1.2f), 90f, creepy);
            Fixture(g, "Lamp", new Vector3(0f, H - 0.02f, 0f), new Vector3(0.25f, 0.06f, 1.2f), creepy ? LampState.Dying : LampState.Flicker, SickGreen, 1.2f, 5f, false, "LampSick");
            if (creepy)
            {
                Decal(g, "D_Handprints", new Vector3(hx + 0.02f, 1.4f, -1.5f), new Vector2(1.2f, 1.2f), Vector3.right);
                Decal(g, "D_Handprints", new Vector3(0f, 1.3f, hz + 0.02f), new Vector2(1.2f, 1.2f), Vector3.forward, 10f);
                FloorDecal(g, "D_Blood", new Vector3(0f, 0.11f, 0.5f), 1.6f);
            }
            else
            {
                Decal(g, "G_DontLook", new Vector3(-hx + 0.03f, 1.5f, 0f), new Vector2(2.4f, 1.2f), Vector3.right);
                Decal(g, "G_DontLook", new Vector3(-hx - 0.03f, 1.5f, 0f), new Vector2(2.4f, 1.2f), Vector3.left);
            }
        }

        // ------------------------------------------------------------------ concourse structure
        struct Hole { public float x0, z0, x1, z1; public Hole(float a, float b, float c, float d) { x0 = a; z0 = b; x1 = c; z1 = d; } }

        static List<Hole> ConcourseHoles()
        {
            var h = new List<Hole>();
            foreach (float cx in IslandX)
            {
                h.Add(new Hole(cx - 2.6f, -3.0f, cx + 2.5f, 3.0f));       // 계단+에스컬레이터
                h.Add(new Hole(cx - 1.1f, 13.5f, cx + 1.1f, 15.7f));      // 엘리베이터 샤프트
            }
            return h;
        }

        static void SlabWithHoles(MeshBuilder mb, float x0, float x1, float z0, float z1, float y0, float y1, List<Hole> holes)
        {
            var xs = new List<float> { x0, x1 };
            foreach (var h in holes) { if (h.x0 > x0 && h.x0 < x1) xs.Add(h.x0); if (h.x1 > x0 && h.x1 < x1) xs.Add(h.x1); }
            xs.Sort();
            for (int i = 0; i < xs.Count - 1; i++)
            {
                float xa = xs[i], xb = xs[i + 1];
                if (xb - xa < 0.001f) continue;
                var cuts = new List<Vector2>();
                foreach (var h in holes) if (h.x0 < xb - 0.001f && h.x1 > xa + 0.001f) cuts.Add(new Vector2(h.z0, h.z1));
                cuts.Sort((a, b) => a.x.CompareTo(b.x));
                float z = z0;
                foreach (var c in cuts)
                {
                    if (c.x > z + 0.001f) mb.AddBox(new Vector3((xa + xb) * 0.5f, (y0 + y1) * 0.5f, (z + c.x) * 0.5f), new Vector3(xb - xa, y1 - y0, c.x - z));
                    z = Mathf.Max(z, c.y);
                }
                if (z1 > z + 0.001f) mb.AddBox(new Vector3((xa + xb) * 0.5f, (y0 + y1) * 0.5f, (z + z1) * 0.5f), new Vector3(xb - xa, y1 - y0, z1 - z));
            }
        }

        static void BuildConcourseShell(Transform root)
        {
            var g = Group("Concourse_Structure", root);
            var holes = ConcourseHoles();
            var slab = new MeshBuilder();
            SlabWithHoles(slab, CX0, CX1, CZ0, CZ1, SLAB, CF - 0.05f, holes);
            Combined(g, "Slab", slab, M("ConcreteDark"), true);
            var floor = new MeshBuilder();
            SlabWithHoles(floor, CX0, CX1, CZ0, CZ1, CF - 0.05f, CF, holes);
            Combined(g, "FloorTile", floor, M("FloorTile"), true);
            BoxMM(g, "SlabEdgeS", new Vector3(CX0 - 0.3f, SLAB - 0.4f, CZ0 - 0.3f), new Vector3(CX1 + 0.3f, CF, CZ0), M("Cladding"), false);
            BoxMM(g, "SlabEdgeN", new Vector3(CX0 - 0.3f, SLAB - 0.4f, CZ1), new Vector3(CX1 + 0.3f, CF, CZ1 + 0.3f), M("Cladding"), false);

            // 기둥 (승강장 위/선로 사이/바깥)
            var colX = new List<float> { -24f, -5f, 5f, 24f, IslandX[0] - 3.6f, IslandX[0] + 3.6f, IslandX[1] - 3.6f, IslandX[1] + 3.6f };
            foreach (float x in colX)
                foreach (float z in new[] { -17f, -7f, 3f, 13f })
                {
                    bool onIsland = Mathf.Abs(Mathf.Abs(x) - 14.45f) < 5f;
                    float yb = onIsland ? PF : (Mathf.Abs(x) > FENCEX ? 0f : 0.3f);
                    BoxMM(g, "Column", new Vector3(x - 0.35f, yb, z - 0.35f), new Vector3(x + 0.35f, SLAB, z + 0.35f), M("Concrete"));
                    if (onIsland && Chance(0.35f))
                        Sign(g, "Missing", new Vector3(x + (x < Mathf.Sign(x) * 14.45f ? 0.36f : -0.36f), PF + 1.5f, z), 0.45f, 0.6f,
                             new Vector3(x < Mathf.Sign(x) * 14.45f ? 1f : -1f, 0f, 0f), "S_Missing", 0f, null, false);
                }
            foreach (float x in new[] { -37.6f, -31f, 31f, 37.6f })
                foreach (float z in new[] { -17.6f, -8f, 4f, 15.6f })
                    BoxMM(g, "Column", new Vector3(x - 0.35f, 0f, z - 0.35f), new Vector3(x + 0.35f, SLAB, z + 0.35f), M("Concrete"));

            // 외벽: 남/북 커튼월
            CurtainWall(g, "Facade_S", CZ0, CX0, CX1, -1);
            CurtainWall(g, "Facade_N", CZ1, CX0, CX1, +1);
            // 동/서 벽 (출구 개구부 z -17 ~ -9)
            foreach (int s in new[] { -1, 1 })
            {
                float x = s * CX1;
                var ext = M("Cladding");
                BoxMM(g, "EndWall", new Vector3(x - 0.15f, CF, CZ0), new Vector3(x + 0.15f, WALLTOP, -17f), ext);
                BoxMM(g, "EndWall", new Vector3(x - 0.15f, CF, -9f), new Vector3(x + 0.15f, WALLTOP, CZ1), ext);
                BoxMM(g, "EndWallLintel", new Vector3(x - 0.15f, 12.0f, -17f), new Vector3(x + 0.15f, WALLTOP, -9f), ext);
                // 부서진 출입문 프레임
                foreach (float z in new[] { -17f, -15f, -13f, -11f, -9f })
                    BoxMM(g, "DoorMullion", new Vector3(x - 0.06f, CF, z - 0.06f), new Vector3(x + 0.06f, 12f, z + 0.06f), M("DarkMetal"), false);
                BoxMM(g, "DoorTransom", new Vector3(x - 0.06f, 11.0f, -17f), new Vector3(x + 0.06f, 11.1f, -9f), M("DarkMetal"), false);
                Quad(g, "BrokenGlass", new Vector3(x, 11.55f, -14f), new Vector2(1.9f, 0.85f), new Vector3(s, 0f, 0f), M("GlassBroken"));
                var leaf = Group("HangingDoor", g, new Vector3(x, CF, -11f), s > 0 ? 70f : -110f);
                Box(leaf, "Leaf", new Vector3(0f, 1.1f, 0.95f), new Vector3(0.05f, 2.2f, 1.9f), M("GlassDark"), true, new Vector3(0f, 0f, 6f));
                Decal(g, "D_Papers", new Vector3(x - s * 2f, CF + 0.01f, -12f), new Vector2(3f, 3f), Vector3.up, R(0f, 360f));
                // 비상구 유도등
                var ex = Sign(g, "EmergencyExit", new Vector3(x - s * 0.2f, 12.35f, -13f), 0.9f, 0.45f, new Vector3(-s, 0f, 0f), "S_EmergencyExit", 1.2f);
                var gl = PointLight(g, "ExitGlow", new Vector3(x - s * 0.8f, 12.1f, -13f), new Color(0.2f, 1f, 0.45f), 0.8f, 4.5f);
                if (s < 0) Flicker(ex, BJ_FlickerLight.Mode.Flicker, new Color(1.2f, 1.2f, 1.2f), new[] { gl }, ex.transform.Find("Face").GetComponent<MeshRenderer>());
            }
            // 동쪽 벽면 대형 역명 간판 (광장에서 보임)
            var facade = Sign(g, "FacadeLetters", new Vector3(CX1 + 0.25f, 11.9f, 3.5f), 15f, 3.75f, Vector3.right, "S_Facade", 0.55f, null, false);
            var letters = facade.transform.Find("Face").GetComponent<MeshRenderer>();
            Flicker(facade, BJ_FlickerLight.Mode.Dying, new Color(0.55f, 0.55f, 0.55f), null, letters);
            Sign(g, "FacadeLettersW", new Vector3(CX0 - 0.25f, 11.9f, 3.5f), 15f, 3.75f, Vector3.left, "S_Facade", 0f, null, false);

            // 지붕: 평지붕 + 아치 지붕
            BoxMM(g, "RoofSlab", new Vector3(CX0 - 0.6f, WALLTOP, CZ0 - 0.6f), new Vector3(CX1 + 0.6f, WALLTOP + 0.35f, CZ1 + 0.6f), M("ConcreteDark"), false);
            ArchRoof(g, CX0 - 1f, CX1 + 1f, CZ0 - 0.8f, CZ1 + 0.8f, WALLTOP + 0.35f, 2.8f, 12);

            // 천장 + 조명
            BuildCeiling(g);
            // 천장 속 덕트
            BoxMM(g, "Duct", new Vector3(CX0 + 1f, 13.2f, -11f), new Vector3(CX1 - 1f, 13.9f, -9.8f), M("StainlessDark"), false);
            BoxMM(g, "Duct", new Vector3(CX0 + 1f, 13.2f, 6f), new Vector3(CX1 - 1f, 13.9f, 7.2f), M("StainlessDark"), false);
        }

        static void CurtainWall(Transform p, string name, float z, float x0, float x1, int outward)
        {
            var g = Group(name, p);
            float yGlass0 = CF + 0.9f, yT = 11.0f, yGlass1 = 12.6f;
            BoxMM(g, "Spandrel", new Vector3(x0, CF, z - 0.12f), new Vector3(x1, yGlass0, z + 0.12f), M("Cladding"));
            BoxMM(g, "TopBand", new Vector3(x0, yGlass1, z - 0.12f), new Vector3(x1, WALLTOP, z + 0.12f), M("Cladding"), false);
            Blocker(g, "WallBlock", new Vector3(x0, CF, z - 0.15f), new Vector3(x1, WALLTOP, z + 0.15f));
            var mull = new MeshBuilder();
            var glass = new MeshBuilder();
            mull.AddBox(new Vector3((x0 + x1) * 0.5f, yT, z), new Vector3(x1 - x0, 0.1f, 0.18f));
            for (float x = x0; x <= x1 + 0.01f; x += 2f)
                mull.AddBox(new Vector3(x, (yGlass0 + yGlass1) * 0.5f, z), new Vector3(0.1f, yGlass1 - yGlass0, 0.2f));
            for (float x = x0; x < x1 - 0.01f; x += 2f)
            {
                foreach (var span in new[] { new Vector2(yGlass0, yT), new Vector2(yT, yGlass1) })
                {
                    Vector3 c = new Vector3(x + 1f, (span.x + span.y) * 0.5f, z);
                    float h = span.y - span.x - 0.1f;
                    double r = rng.NextDouble();
                    if (r < 0.08)
                        Quad(g, "BrokenPane", c, new Vector2(1.9f, h), new Vector3(0f, 0f, outward), M("GlassBroken"));
                    else if (r < 0.15)
                    {
                        for (int k = 0; k < 4; k++)
                            Box(g, "Board", new Vector3(c.x + R(-0.1f, 0.1f), span.x + 0.25f + k * (h / 4f), z - outward * 0.12f),
                                new Vector3(2.1f, 0.22f, 0.03f), M("Wood"), false, new Vector3(0f, 0f, R(-12f, 12f)));
                    }
                    else glass.AddBox(c, new Vector3(1.9f, h, 0.02f));
                }
            }
            Combined(g, "Mullions", mull, M("DarkMetal"), false);
            var gl = Combined(g, "Glass", glass, M("GlassDark"), false);
            if (gl != null) gl.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        static void ArchRoof(Transform p, float x0, float x1, float z0, float z1, float baseY, float rise, int segs)
        {
            var g = Group("ArchRoof", p);
            float half = (z1 - z0) * 0.5f, zc = (z0 + z1) * 0.5f;
            float Rr = (half * half + rise * rise) / (2f * rise);
            float alpha = Mathf.Asin(half / Rr);
            var mb = new MeshBuilder();
            var gable = new MeshBuilder();
            for (int i = 0; i < segs; i++)
            {
                float t0 = -alpha + 2f * alpha * i / segs, t1 = -alpha + 2f * alpha * (i + 1) / segs;
                Vector2 a = new Vector2(zc + Rr * Mathf.Sin(t0), baseY + Rr * Mathf.Cos(t0) - Rr * Mathf.Cos(alpha));
                Vector2 b = new Vector2(zc + Rr * Mathf.Sin(t1), baseY + Rr * Mathf.Cos(t1) - Rr * Mathf.Cos(alpha));
                Vector2 d = b - a;
                float pitch = -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                mb.AddBox(new Vector3((x0 + x1) * 0.5f, (a.y + b.y) * 0.5f, (a.x + b.x) * 0.5f), new Vector3(x1 - x0, 0.22f, d.magnitude + 0.05f), Quaternion.Euler(pitch, 0f, 0f), false);
                float hh = Mathf.Min(a.y, b.y) - baseY;
                if (hh > 0.02f)
                    foreach (float x in new[] { x0 + 0.1f, x1 - 0.1f })
                        gable.AddBox(new Vector3(x, baseY + hh * 0.5f, (a.x + b.x) * 0.5f), new Vector3(0.2f, hh, d.magnitude));
            }
            Combined(g, "Arch", mb, M("SteelBlue"), false);
            Combined(g, "Gable", gable, M("Cladding"), false);
        }

        static void BuildCeiling(Transform p)
        {
            var g = Group("Ceiling", p);
            var tiles = new MeshBuilder();
            int nx = 19, nz = 8;
            float bw = (CX1 - CX0) / nx, bd = (CZ1 - CZ0) / nz;
            int shadowLeft = 1;
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    float x = CX0 + (i + 0.5f) * bw, z = CZ0 + (j + 0.5f) * bd;
                    double r = rng.NextDouble();
                    if (r < 0.07)
                    {
                        // 천장 판 빠짐 + 매달린 등기구
                        if (Chance(0.6f))
                        {
                            var hang = Group("DanglingLamp", g, new Vector3(x, CEIL - 1.1f, z), R(0f, 360f));
                            hang.localRotation = Quaternion.Euler(R(35f, 70f), R(0f, 360f), 0f);
                            var st = Chance(0.6f) ? LampState.Dying : LampState.Off;
                            Fixture(hang, "Lamp", Vector3.zero, new Vector3(1.25f, 0.06f, 0.3f), st, CoolWhite, 1.6f, 8f);
                            Cyl(g, "Wire", new Vector3(x, CEIL + 0.2f, z), new Vector3(x + R(-0.3f, 0.3f), CEIL - 1.0f, z + R(-0.3f, 0.3f)), 0.015f, M("Black"));
                        }
                        continue;
                    }
                    if (r < 0.11)
                    {
                        // 떨어진 천장 판
                        Box(g, "FallenTile", new Vector3(x + R(-1f, 1f), CF + 0.15f, z + R(-1f, 1f)), new Vector3(1.2f, 0.03f, 0.6f), M("Ceiling"), false,
                            new Vector3(R(-12f, 12f), R(0f, 360f), R(-12f, 12f)));
                        tiles.AddBox(new Vector3(x - bw * 0.25f, CEIL + 0.02f, z), new Vector3(bw * 0.5f, 0.04f, bd));
                        continue;
                    }
                    tiles.AddBox(new Vector3(x, CEIL + 0.02f, z), new Vector3(bw, 0.04f, bd));
                    var state = RandomLamp(0.14f, 0.07f, 0.06f);
                    bool sh = state == LampState.On && shadowLeft > 0 && Mathf.Abs(x) < 20f && Chance(0.5f);
                    if (sh) shadowLeft--;
                    Fixture(g, "CeilingLamp", new Vector3(x, CEIL - 0.03f, z), new Vector3(1.25f, 0.06f, 0.3f), state, CoolWhite, 1.8f, 10f, sh);
                }
            var c = Combined(g, "CeilingTiles", tiles, M("Ceiling"), false);
            if (c != null) c.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
        }

        // ------------------------------------------------------------------ stairs / escalators / elevators
        /// <summary>계단. topEdge = 계단 시작점(위층 바닥 높이), yaw 방향(+Z 로컬)으로 내려간다. 반환 = 수평 길이.</summary>
        static float Stair(Transform p, string name, Vector3 topEdge, float yaw, float width, float totalRise, int flights, float landing, Material sides)
        {
            var g = Group(name, p, topEdge, yaw);
            int n = Mathf.Max(2, Mathf.RoundToInt(totalRise / 0.17f / flights));
            float rise = totalRise / (n * flights), tread = 0.3f;
            var steps = new MeshBuilder();
            var nosing = new MeshBuilder();
            float z = 0f, y = 0f, hw = width * 0.5f;
            for (int f = 0; f < flights; f++)
            {
                float zStart = z, yStart = y;
                for (int k = 0; k < n - 1; k++)
                {
                    float sy = yStart - (k + 1) * rise;
                    float za = zStart + k * tread;
                    steps.AddBox(new Vector3(0f, sy - 0.15f, za + tread * 0.5f), new Vector3(width, 0.3f, tread));
                    nosing.AddBox(new Vector3(0f, sy + 0.003f, za + 0.04f), new Vector3(width - 0.1f, 0.006f, 0.06f));
                }
                float zEnd = zStart + (n - 1) * tread;
                float yEnd = yStart - n * rise;
                // 하부 경사 슬래브
                float run = zEnd - zStart, drop = yStart - yEnd;
                float len = Mathf.Sqrt(run * run + drop * drop), ang = Mathf.Atan2(drop, run) * Mathf.Rad2Deg;
                steps.AddBox(new Vector3(0f, (yStart + yEnd) * 0.5f - 0.4f, (zStart + zEnd) * 0.5f), new Vector3(width, 0.2f, len), Quaternion.Euler(ang, 0f, 0f), false);
                // 옆벽 + 손잡이
                foreach (int s in new[] { -1, 1 })
                {
                    float sx = s * (hw + 0.08f);
                    Box(g, "Side", new Vector3(sx, (yStart + yEnd) * 0.5f + 0.35f, (zStart + zEnd) * 0.5f), new Vector3(0.15f, 1.7f, len), sides, true, new Vector3(ang, 0f, 0f));
                    Cyl(g, "Handrail", new Vector3(sx - s * 0.12f, yStart + 0.95f, zStart), new Vector3(sx - s * 0.12f, yEnd + 0.95f, zEnd), 0.05f, M("Stainless"));
                }
                z = zEnd; y = yEnd;
                if (f < flights - 1)
                {
                    steps.AddBox(new Vector3(0f, y - 0.15f, z + landing * 0.5f), new Vector3(width, 0.3f, landing));
                    foreach (int s in new[] { -1, 1 })
                        Box(g, "LandingSide", new Vector3(s * (hw + 0.08f), y + 0.5f, z + landing * 0.5f), new Vector3(0.15f, 1.2f, landing), sides);
                    z += landing;
                }
            }
            Combined(g, "Steps", steps, M("ConcreteLight"), true);
            var ns = Combined(g, "Nosing", nosing, M("Yellow"), false);
            if (ns != null) ns.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            return z;
        }

        /// <summary>멈춘 에스컬레이터 (30도). 경사면 위를 걸을 수 있다.</summary>
        static float Escalator(Transform p, string name, Vector3 topEdge, float yaw, float totalRise, float width = 1.1f)
        {
            var g = Group(name, p, topEdge, yaw);
            float flat = 1.2f, run = totalRise / Mathf.Tan(30f * Mathf.Deg2Rad);
            float len = Mathf.Sqrt(run * run + totalRise * totalRise);
            Vector3 mid = new Vector3(0f, -totalRise * 0.5f, flat + run * 0.5f);
            var rot = new Vector3(30f, 0f, 0f);
            Box(g, "TopPlate", new Vector3(0f, -0.03f, flat * 0.5f), new Vector3(width, 0.06f, flat), M("StainlessDark"));
            Box(g, "Steps", mid + new Vector3(0f, -0.03f, 0f), new Vector3(width, 0.06f, len), M("StainlessDark"), true, rot);
            Box(g, "BottomPlate", new Vector3(0f, -totalRise - 0.03f, flat * 1.5f + run), new Vector3(width, 0.06f, flat), M("StainlessDark"));
            Box(g, "Truss", mid + new Vector3(0f, -0.65f, 0f), new Vector3(width + 0.25f, 1.1f, len), M("DarkMetal"), false, rot);
            foreach (int s in new[] { -1, 1 })
            {
                float sx = s * (width * 0.5f + 0.07f);
                Box(g, "Balustrade", mid + new Vector3(sx, 0.55f, 0f), new Vector3(0.03f, 0.9f, len), M("Glass"), true, rot);
                Box(g, "Handrail", mid + new Vector3(sx, 1.02f, 0f), new Vector3(0.09f, 0.07f, len), M("Rubber"), false, rot);
                Box(g, "Skirt", mid + new Vector3(sx, 0.08f, 0f), new Vector3(0.08f, 0.22f, len), M("Stainless"), false, rot);
                Box(g, "TopNewel", new Vector3(sx, 0.5f, flat * 0.5f), new Vector3(0.09f, 1.0f, flat), M("Glass"), true);
                Box(g, "BotNewel", new Vector3(sx, -totalRise + 0.5f, flat * 1.5f + run), new Vector3(0.09f, 1.0f, flat), M("Glass"), true);
            }
            Tape(g, new Vector3(-width * 0.5f - 0.1f, 0.95f, 0.3f), new Vector3(width * 0.5f + 0.1f, 0.95f, 0.5f));
            Sign(g, "OutOfOrder", new Vector3(0f, 0.75f, 0.2f), 0.9f, 0.23f, Vector3.back, "S_OutOfOrder");
            return flat * 2f + run;
        }

        static void Elevator(Transform p, string name, Vector3 baseCenter, float topY, bool creepy)
        {
            var g = Group(name, p, baseCenter);
            float hw = 1.1f, H = topY - baseCenter.y;
            foreach (var c in new[] { new Vector3(-hw, 0f, -hw), new Vector3(hw, 0f, -hw), new Vector3(-hw, 0f, hw), new Vector3(hw, 0f, hw) })
                Box(g, "Post", c + Vector3.up * H * 0.5f, new Vector3(0.14f, H, 0.14f), M("DarkMetal"), false);
            for (float y = 3f; y < H; y += 3f)
                Box(g, "Ring", new Vector3(0f, y, 0f), new Vector3(hw * 2f + 0.1f, 0.1f, hw * 2f + 0.1f), M("DarkMetal"), false);
            Box(g, "GlassN", new Vector3(0f, H * 0.5f, hw), new Vector3(hw * 2f, H, 0.03f), M("Glass"), false);
            Box(g, "GlassE", new Vector3(hw, H * 0.5f, 0f), new Vector3(0.03f, H, hw * 2f), M("Glass"), false);
            Box(g, "GlassW", new Vector3(-hw, H * 0.5f, 0f), new Vector3(0.03f, H, hw * 2f), M("Glass"), false);
            Box(g, "FrontPanel", new Vector3(0f, H * 0.5f, -hw), new Vector3(hw * 2f, H, 0.06f), M("StainlessDark"), false);
            Box(g, "Cap", new Vector3(0f, H + 0.15f, 0f), new Vector3(hw * 2f + 0.3f, 0.3f, hw * 2f + 0.3f), M("DarkMetal"), false);
            var bc = g.gameObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, H * 0.5f, 0f); bc.size = new Vector3(hw * 2f + 0.1f, H, hw * 2f + 0.1f);
            // 승강장층 / 대합실층 문
            foreach (float y in new[] { 0f, CF - baseCenter.y })
            {
                Box(g, "DoorL", new Vector3(-0.45f, y + 1.05f, -hw - 0.04f), new Vector3(0.88f, 2.1f, 0.03f), M("Stainless"), false);
                Box(g, "DoorR", new Vector3(0.45f, y + 1.05f, -hw - 0.04f), new Vector3(0.88f, 2.1f, 0.03f), M("Stainless"), false);
                Sign(g, "ElevSign", new Vector3(0f, y + 2.45f, -hw - 0.08f), 1.2f, 0.3f, Vector3.back, "S_Elevator", 0.15f);
                Sign(g, "OutOfOrder", new Vector3(0f, y + 1.3f, -hw - 0.06f), 0.8f, 0.2f, Vector3.back, "S_OutOfOrder", 0f, null, false);
            }
            // 중간에 멈춘 엘리베이터 카
            float cy = creepy ? 3.4f : CF - baseCenter.y + 0.1f;
            var car = Group("StuckCar", g, new Vector3(0f, cy, 0f));
            Box(car, "Floor", new Vector3(0f, 0f, 0f), new Vector3(1.9f, 0.12f, 1.9f), M("DarkMetal"), false);
            Box(car, "Roof", new Vector3(0f, 2.3f, 0f), new Vector3(1.9f, 0.12f, 1.9f), M("DarkMetal"), false);
            Box(car, "Back", new Vector3(0f, 1.15f, 0.9f), new Vector3(1.9f, 2.3f, 0.05f), M("Stainless"), false);
            Fixture(car, "CarLamp", new Vector3(0f, 2.2f, 0f), new Vector3(0.6f, 0.04f, 0.6f), creepy ? LampState.Flicker : LampState.Dying, SickGreen, 1.4f, 4f, false, "LampSick", new Vector3(0f, -0.3f, 0f));
            if (creepy)
            {
                Decal(car, "D_Handprints", new Vector3(1.13f, 1.3f, 0f), new Vector2(1.2f, 1.2f), Vector3.right);
                FloorDecal(car, "D_Blood", new Vector3(0f, 0.07f, 0f), 1.5f);
            }
        }

        static void BuildVerticalCirculation(Transform root)
        {
            var g = Group("Stairs_Escalators_Elevators", root);
            for (int i = 0; i < 2; i++)
            {
                float cx = IslandX[i];
                var ig = Group(i == 0 ? "To_Platform_1-2" : "To_Platform_3-4", g);
                Stair(ig, "Stair", new Vector3(cx - 1.0f, CF, -3.0f), 0f, 3.0f, CF - PF, 2, 2.0f, M("Cladding"));
                Escalator(ig, "Escalator", new Vector3(cx + 1.6f, CF, -3.0f), 0f, CF - PF, 1.1f);
                Elevator(ig, "Elevator", new Vector3(cx, PF, 14.6f), CF + 3.0f, i == 0);
                // 구멍 주변 난간
                HoleRailing(ig, cx - 2.6f, -3.0f, cx - 2.6f, 3.0f);
                HoleRailing(ig, cx + 2.5f, -3.0f, cx + 2.5f, 3.0f);
                HoleRailing(ig, cx - 2.6f, 3.0f, cx + 2.5f, 3.0f);
                // 계단 위 '타는 곳' 표지
                string plat = i == 0 ? "S_Platform12" : "S_Platform34";
                HangingSign(ig, "PlatformSign", new Vector3(cx, 11.2f, -4.2f), 3.2f, 0.8f, Vector3.back, plat, null, CEIL, 0.35f);
                // 계단 아래 출구 안내
                HangingSign(ig, "ExitSign", new Vector3(cx - 1.0f, 5.6f, 12.6f), 3.4f, 0.64f, Vector3.forward, "S_ExitDir_A", null, SLAB, 0.3f);
                Decal(ig, "G_LastTrain", new Vector3(cx + 1.13f, PF + 2.3f, 14.6f), new Vector2(2.0f, 1.0f), Vector3.right);
            }
        }

        static void HoleRailing(Transform p, float xa, float za, float xb, float zb)
        {
            bool alongZ = Mathf.Abs(xa - xb) < 0.01f;
            Vector3 c = new Vector3((xa + xb) * 0.5f, CF + 0.55f, (za + zb) * 0.5f);
            Vector3 s = alongZ ? new Vector3(0.03f, 1.0f, Mathf.Abs(zb - za)) : new Vector3(Mathf.Abs(xb - xa), 1.0f, 0.03f);
            var gl = Box(p, "RailGlass", c, s, M("Glass"));
            gl.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            Box(p, "RailTop", c + Vector3.up * 0.55f, alongZ ? new Vector3(0.07f, 0.06f, s.z) : new Vector3(s.x, 0.06f, 0.07f), M("Stainless"), false);
            Box(p, "RailBase", new Vector3(c.x, CF + 0.04f, c.z), alongZ ? new Vector3(0.12f, 0.08f, s.z) : new Vector3(s.x, 0.08f, 0.12f), M("StainlessDark"), false);
        }
    }
}
