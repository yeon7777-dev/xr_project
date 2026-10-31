using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Byeongjeom;

namespace Byeongjeom.MapGen
{
    /// <summary>1번선(광운대·청량리 방면)에 멈춰 선 4량 편성 전동차. 승강장 쪽 문으로 들어갈 수 있다.</summary>
    public static partial class BJ
    {
        const float TL = 19.5f, THL = 9.75f, THW = 1.53f;
        const float TFloor = PF + 0.02f, TBody0 = 1.1f, TWin0 = 2.55f, TWin1 = 3.45f, TRoof = 4.15f, TDoorTop = 3.62f;

        static void BuildTrain(Transform root)
        {
            var g = Group("Train_멈춘전동차 (1번선)", root);
            for (int i = 0; i < TrainCars.Length; i++)
            {
                var car = Group("Car_" + (i + 1), g, new Vector3(TrackX[0], 0f, TrainCars[i]));
                bool cabSouth = i == 0, cabNorth = i == TrainCars.Length - 1;
                BuildCar(car, i, cabSouth, cabNorth);
                if (!cabNorth) Gangway(car);
            }
        }

        static void BuildCar(Transform car, int idx, bool cabSouth, bool cabNorth)
        {
            var shell = new MeshBuilder();
            // 바닥/하부
            Box(car, "Floor", new Vector3(0f, (1.45f + TFloor) * 0.5f, 0f), new Vector3(THW * 2f - 0.04f, TFloor - 1.45f, TL - 0.1f), M("TrainFloor"));
            Box(car, "Underframe", new Vector3(0f, 1.18f, 0f), new Vector3(2.7f, 0.55f, TL - 1.0f), M("DarkMetal"), false);
            for (int k = 0; k < 4; k++)
                Box(car, "Equipment", new Vector3(R(-0.6f, 0.6f), 1.0f, R(-4.5f, 4.5f)), new Vector3(R(0.6f, 1.4f), 0.45f, R(0.8f, 2.0f)), M("DarkGray"), false);
            Bogie(car, new Vector3(0f, 0f, -6.9f));
            Bogie(car, new Vector3(0f, 0f, 6.9f));
            // 지붕
            Box(car, "Ceiling", new Vector3(0f, 3.97f, 0f), new Vector3(THW * 2f - 0.06f, 0.05f, TL - 0.1f), M("Plastic"), false);
            Box(car, "Roof", new Vector3(0f, TRoof + 0.06f, 0f), new Vector3(3.12f, 0.12f, TL), M("Stainless"));
            Box(car, "RoofTop", new Vector3(0f, TRoof + 0.19f, 0f), new Vector3(2.4f, 0.14f, TL - 0.4f), M("StainlessDark"), false);
            foreach (float z in new[] { -5f, 5f }) Box(car, "AC", new Vector3(0f, TRoof + 0.43f, z), new Vector3(2.0f, 0.35f, 3.0f), M("LightGray"), false);
            if (idx == 1)
            {
                Cyl(car, "Panto", new Vector3(-0.6f, TRoof + 0.3f, -2f), new Vector3(0f, TRoof + 1.3f, -2.6f), 0.05f, M("DarkMetal"));
                Cyl(car, "Panto", new Vector3(0.6f, TRoof + 0.3f, -2f), new Vector3(0f, TRoof + 1.3f, -2.6f), 0.05f, M("DarkMetal"));
                Box(car, "PantoHead", new Vector3(0f, TRoof + 1.32f, -2.6f), new Vector3(1.6f, 0.05f, 0.1f), M("DarkMetal"), false);
            }

            // 옆벽 (s=+1 : 승강장 쪽)
            var doors = new List<float>(DoorOffsets);
            float[] segA = { -THL, -6.7f, -1.8f, 3.1f, 8.0f };
            float[] segB = { -8.0f, -3.1f, 1.8f, 6.7f, THL };
            var glassMB = new MeshBuilder();
            var stripeMB = new MeshBuilder();
            var stripe2MB = new MeshBuilder();
            foreach (int s in new[] { -1, 1 })
            {
                float x = s * THW;
                for (int k = 0; k < segA.Length; k++)
                {
                    float a = segA[k], b = segB[k], c = (a + b) * 0.5f, w = b - a;
                    bool cabSeg = (cabSouth && k == 0) || (cabNorth && k == segA.Length - 1);
                    shell.AddBox(new Vector3(x, (TBody0 + TWin0) * 0.5f, c), new Vector3(0.06f, TWin0 - TBody0, w));
                    shell.AddBox(new Vector3(x, (TWin1 + TRoof) * 0.5f, c), new Vector3(0.06f, TRoof - TWin1, w));
                    shell.AddBox(new Vector3(x, TWin0, c), new Vector3(0.08f, 0.06f, w));
                    shell.AddBox(new Vector3(x, TWin1, c), new Vector3(0.08f, 0.06f, w));
                    int panes = Mathf.Max(1, Mathf.RoundToInt(w / 1.6f));
                    for (int pI = 0; pI <= panes; pI++)
                        shell.AddBox(new Vector3(x, (TWin0 + TWin1) * 0.5f, a + w * pI / panes), new Vector3(0.08f, TWin1 - TWin0, 0.09f));
                    glassMB.AddBox(new Vector3(x, (TWin0 + TWin1) * 0.5f, c), new Vector3(0.02f, TWin1 - TWin0, w));
                    stripeMB.AddBox(new Vector3(s * (THW + 0.035f), 2.38f, c), new Vector3(0.01f, 0.16f, w));
                    stripe2MB.AddBox(new Vector3(s * (THW + 0.035f), 2.25f, c), new Vector3(0.01f, 0.05f, w));
                    Blocker(car, "WallBlock", new Vector3(x - 0.05f, TFloor, a), new Vector3(x + 0.05f, TRoof, b));
                    if (!cabSeg) Seats(car, s, a + (k == 0 ? 0.15f : 0.12f), b - (k == segA.Length - 1 ? 0.15f : 0.12f), idx);
                }
                foreach (float d in doors)
                {
                    shell.AddBox(new Vector3(x, (TDoorTop + TRoof) * 0.5f, d), new Vector3(0.06f, TRoof - TDoorTop, 1.3f));
                    shell.AddBox(new Vector3(x, TBody0 + (TFloor - TBody0) * 0.5f, d), new Vector3(0.06f, TFloor - TBody0, 1.3f));
                    bool open = s > 0 ? Chance(0.8f) : (idx == 2 && Mathf.Abs(d + 2.45f) < 0.01f);
                    if (open)
                    {
                        if (s > 0) BoxMM(car, "GapPlate", new Vector3(THW - 0.05f, TFloor - 0.04f, d - 0.6f), new Vector3(THW + 0.3f, TFloor, d + 0.6f), M("StainlessDark"));
                        continue;
                    }
                    foreach (int k in new[] { -1, 1 })
                    {
                        float zc = d + k * 0.33f;
                        Box(car, "DoorLeaf", new Vector3(x, TFloor + 0.95f, zc), new Vector3(0.05f, 1.9f, 0.64f), M("Stainless"), false);
                        Box(car, "DoorWindow", new Vector3(x, 3.05f, zc), new Vector3(0.06f, 0.75f, 0.42f), M("CarBlack"), false);
                    }
                    Blocker(car, "DoorBlock", new Vector3(x - 0.05f, TFloor, d - 0.65f), new Vector3(x + 0.05f, TDoorTop, d + 0.65f));
                }
            }
            // 끝벽 (통로문)
            foreach (int e in new[] { -1, 1 })
            {
                bool cab = (e < 0 && cabSouth) || (e > 0 && cabNorth);
                float z = e * (THL - 0.03f);
                if (cab) { Cab(car, e, idx); continue; }
                shell.AddBox(new Vector3(-0.99f, (TBody0 + TRoof) * 0.5f, z), new Vector3(1.08f, TRoof - TBody0, 0.06f));
                shell.AddBox(new Vector3(0.99f, (TBody0 + TRoof) * 0.5f, z), new Vector3(1.08f, TRoof - TBody0, 0.06f));
                shell.AddBox(new Vector3(0f, (3.65f + TRoof) * 0.5f, z), new Vector3(0.9f, TRoof - 3.65f, 0.06f));
                Blocker(car, "EndBlock", new Vector3(-THW, TFloor, z - 0.04f), new Vector3(-0.45f, TRoof, z + 0.04f));
                Blocker(car, "EndBlock", new Vector3(0.45f, TFloor, z - 0.04f), new Vector3(THW, TRoof, z + 0.04f));
            }
            Combined(car, "Body", shell, M("Stainless"), false);
            var gl = Combined(car, "Windows", glassMB, M("GlassDark"), false);
            if (gl != null) gl.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            Combined(car, "Stripe", stripeMB, M("Line1Blue"), false);
            Combined(car, "Stripe2", stripe2MB, M("SkyBlue"), false);

            // 실내 조명
            var strips = new List<Renderer>();
            var lights = new List<Light>();
            bool lit = idx == 1 || idx == 3;
            foreach (float ox in new[] { -0.6f, 0.6f })
            {
                var st = Box(car, "LightStrip", new Vector3(ox, 3.93f, 0f), new Vector3(0.16f, 0.04f, TL - 2f), M(lit ? "LampOn" : "LampOff"), false);
                st.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                strips.Add(st.GetComponent<MeshRenderer>());
            }
            if (lit)
            {
                foreach (float z in new[] { -5.5f, 0f, 5.5f })
                    lights.Add(PointLight(car, "CarLight", new Vector3(0f, 3.6f, z), CoolWhite, idx == 3 ? 0.7f : 1.1f, 6.5f));
                Flicker(car.gameObject, idx == 1 ? BJ_FlickerLight.Mode.Flicker : BJ_FlickerLight.Mode.Buzz, M("LampOn").GetColor("_EmissionColor"), lights.ToArray(), strips.ToArray());
            }
            else if (idx == 0)
            {
                foreach (float z in new[] { -7.8f, 7.8f })
                {
                    Box(car, "EmergencyLamp", new Vector3(0f, 3.9f, z), new Vector3(0.3f, 0.08f, 0.15f), M("EmergencyRed"), false);
                    PointLight(car, "EmergencyGlow", new Vector3(0f, 3.6f, z), new Color(1f, 0.1f, 0.05f), 0.8f, 5f);
                }
            }

            // 실내 소품/연출
            for (int k = 0; k < 4; k++)
                Quad(car, "Ad", new Vector3((k % 2 == 0 ? 1 : -1) * (THW - 0.04f), 3.72f, -5f + k * 3.3f), new Vector2(0.95f, 0.24f),
                     new Vector3(k % 2 == 0 ? -1 : 1, 0f, 0f), SignMat("S_TrainAd", 0f));
            Scatter(car, new Vector3(-1.2f, TFloor + 0.01f, -9f), new Vector3(1.2f, TFloor + 0.01f, 9f), 4, "D_Papers", "D_Grime", "D_Papers");
            if (idx == 2)
            {
                FloorDecal(car, "D_Blood", new Vector3(0.2f, TFloor + 0.012f, 1.5f), 2.2f);
                Decal(car, "D_BloodSmear", new Vector3(0f, TFloor + 0.012f, -3f), new Vector2(4.5f, 1.2f), Vector3.up, 90f);
                Decal(car, "D_Handprints", new Vector3(-THW + 0.04f, 3.0f, 4.9f), new Vector2(1.1f, 1.1f), Vector3.right);
                Decal(car, "D_Handprints", new Vector3(THW - 0.04f, 3.0f, -4.9f), new Vector2(1.1f, 1.1f), Vector3.left);
                Decal(car, "G_Help", new Vector3(-0.99f, 2.9f, THL - 0.08f), new Vector2(1.0f, 0.5f), Vector3.back);
                TrashBags(car, new Vector3(-0.8f, TFloor, 8.5f), 2);
            }
            if (idx == 0) Decal(car, "G_Tally", new Vector3(0.99f, 2.8f, THL - 0.08f), new Vector2(0.9f, 0.9f), Vector3.back);
            // 바깥(선로 쪽) 낙서
            Decal(car, idx % 2 == 0 ? "G_GoBack" : "G_DontLook", new Vector3(-THW - 0.05f, 2.4f, R(-4f, 4f)), new Vector2(3.4f, 1.7f), Vector3.left);
        }

        static void Seats(Transform car, int s, float za, float zb, int idx)
        {
            if (zb - za < 0.8f) return;
            float c = (za + zb) * 0.5f, w = zb - za;
            Box(car, "SeatBase", new Vector3(s * 1.27f, TFloor + 0.2f, c), new Vector3(0.45f, 0.4f, w), M("StainlessDark"));
            Box(car, "SeatCushion", new Vector3(s * 1.25f, TFloor + 0.47f, c), new Vector3(0.5f, 0.12f, w - 0.04f), M("SeatFabric"), false);
            Box(car, "SeatBack", new Vector3(s * 1.46f, TFloor + 0.85f, c), new Vector3(0.08f, 0.62f, w - 0.04f), M("SeatFabric"), false);
            Cyl(car, "Stanchion", new Vector3(s * 0.98f, TFloor, za + 0.05f), new Vector3(s * 0.98f, 3.95f, za + 0.05f), 0.04f, M("Stainless"));
            Cyl(car, "Stanchion", new Vector3(s * 0.98f, TFloor, zb - 0.05f), new Vector3(s * 0.98f, 3.95f, zb - 0.05f), 0.04f, M("Stainless"));
            Cyl(car, "GrabRail", new Vector3(s * 0.98f, 3.62f, za), new Vector3(s * 0.98f, 3.62f, zb), 0.035f, M("Stainless"));
            var straps = new MeshBuilder();
            for (float z = za + 0.3f; z < zb - 0.2f; z += 0.42f)
            {
                bool missing = Chance(0.12f);
                if (missing) continue;
                straps.AddBox(new Vector3(s * 0.98f, 3.48f, z), new Vector3(0.02f, 0.24f, 0.03f));
                straps.AddBox(new Vector3(s * 0.98f, 3.34f, z), new Vector3(0.03f, 0.08f, 0.12f));
            }
            Combined(car, "Straps", straps, M("Plastic"), false);
            if (idx == 2 && Chance(0.5f)) Box(car, "FallenStrap", new Vector3(s * 0.6f, TFloor + 0.02f, c), new Vector3(0.12f, 0.03f, 0.3f), M("Plastic"), false, new Vector3(0f, R(0f, 180f), 0f));
        }

        static void Cab(Transform car, int e, int idx)
        {
            float zf = e * THL, zp = e * (THL - 1.6f);
            var cab = Group("Cab", car);
            // 운전실 칸막이 (닫힌 문)
            Box(cab, "Partition", new Vector3(-0.9f, (TFloor + TRoof) * 0.5f, zp), new Vector3(1.26f, TRoof - TFloor, 0.06f), M("Stainless"));
            Box(cab, "Partition", new Vector3(0.95f, (TFloor + TRoof) * 0.5f, zp), new Vector3(1.16f, TRoof - TFloor, 0.06f), M("Stainless"));
            Box(cab, "CabDoor", new Vector3(-0.0f, TFloor + 1.0f, zp), new Vector3(0.74f, 2.0f, 0.05f), M("StainlessDark"));
            Box(cab, "CabDoorTop", new Vector3(0f, (TFloor + 2.0f + TRoof) * 0.5f, zp), new Vector3(0.74f, TRoof - TFloor - 2.0f, 0.06f), M("Stainless"), false);
            Box(cab, "CabDoorWin", new Vector3(0f, TFloor + 1.5f, zp - e * 0.03f), new Vector3(0.4f, 0.5f, 0.01f), M("CarBlack"), false);
            // 앞면
            Box(cab, "FrontLower", new Vector3(0f, (TBody0 + 2.6f) * 0.5f, zf), new Vector3(3.08f, 2.6f - TBody0, 0.1f), M("Stainless"));
            Box(cab, "FrontUpper", new Vector3(0f, (3.7f + TRoof) * 0.5f, zf), new Vector3(3.08f, TRoof - 3.7f, 0.1f), M("Stainless"));
            Box(cab, "Windshield", new Vector3(0f, 3.15f, zf), new Vector3(2.9f, 1.1f, 0.05f), M("GlassDark"));
            Box(cab, "FrontStripe", new Vector3(0f, 2.38f, zf + e * 0.055f), new Vector3(3.08f, 0.16f, 0.01f), M("Line1Blue"), false);
            Box(cab, "Console", new Vector3(0f, TFloor + 0.55f, zf - e * 0.5f), new Vector3(2.6f, 1.1f, 0.6f), M("DarkGray"));
            Chair(cab, new Vector3(-0.6f, TFloor, zf - e * 1.15f), e > 0 ? 0f : 180f, idx == 0);
            foreach (int s in new[] { -1, 1 })
            {
                Box(cab, "HeadLamp", new Vector3(s * 1.05f, 2.05f, zf + e * 0.06f), new Vector3(0.35f, 0.18f, 0.02f), M("LampOff"), false);
                Box(cab, "TailLamp", new Vector3(s * 1.05f, 1.8f, zf + e * 0.06f), new Vector3(0.22f, 0.12f, 0.02f), M(idx == 0 ? "SignalRed" : "Red"), false);
            }
            Box(cab, "Coupler", new Vector3(0f, 1.05f, zf + e * 0.35f), new Vector3(0.35f, 0.3f, 0.7f), M("DarkMetal"), false);
            Box(cab, "AntiClimber", new Vector3(0f, 1.3f, zf + e * 0.08f), new Vector3(2.6f, 0.2f, 0.12f), M("DarkMetal"), false);
            // 행선 표시기 "회송"
            var dest = Quad(cab, "DestSign", new Vector3(0f, 3.92f, zf + e * 0.056f), new Vector2(1.6f, 0.25f), new Vector3(0f, 0f, e), SignMat("S_TrainDest", 0.9f));
            Flicker(dest, BJ_FlickerLight.Mode.Flicker, new Color(0.9f, 0.9f, 0.9f), null, dest.GetComponent<MeshRenderer>());
            if (idx == 0) PointLight(cab, "TailGlow", new Vector3(0f, 1.8f, zf + e * 0.8f), new Color(1f, 0.05f, 0.02f), 1.2f, 5f);
        }

        static void Gangway(Transform car)
        {
            float z = THL + 0.25f;
            Box(car, "Bellows", new Vector3(-0.56f, 2.7f, z), new Vector3(0.1f, 2.1f, 0.55f), M("Rubber"));
            Box(car, "Bellows", new Vector3(0.56f, 2.7f, z), new Vector3(0.1f, 2.1f, 0.55f), M("Rubber"));
            Box(car, "BellowsTop", new Vector3(0f, 3.72f, z), new Vector3(1.22f, 0.1f, 0.55f), M("Rubber"), false);
            Box(car, "GangPlate", new Vector3(0f, TFloor - 0.02f, z), new Vector3(1.0f, 0.04f, 0.7f), M("StainlessDark"));
        }
    }
}
