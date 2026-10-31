using System.Collections.Generic;
using UnityEngine;

namespace Byeongjeom
{
    /// <summary>
    /// 조명 최적화 (Play 중에만 동작). 맵 루트에 붙는다 - Tools > 병점역 맵 > 조명 최적화
    ///  - 먼 포인트/스포트 라이트를 끈다. 끄는 거리 = 안개 너머라 안 보이는 거리와 Cull Distance 중 가까운 쪽.
    ///    공포 기본 안개(0.028)면 안개 거리(약 58m), 확인용 밝은 안개(0.006)면 Cull Distance(60m) 가 쓰인다.
    ///  - 그림자 거리를 같은 식으로(안개 거리 · Max Shadow Distance 중 가까운 쪽) 줄이고, 많이 줄면 캐스케이드도 줄인다.
    ///    현재 품질 설정보다 늘리지는 않고, 씬을 떠나면 원래대로 돌려 놓는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class BJ_LightCuller : MonoBehaviour
    {
        [Tooltip("안개 때문에 이 비율 이하로만 보이는 거리부터 조명을 끈다 (0.07 = 7%)")]
        [Range(0.01f, 0.3f)] public float fogVisibility = 0.07f;
        [Tooltip("조명이 비추는 범위의 가장자리가 이 거리(m)보다 멀면 안개와 상관없이 끈다 (안개가 옅을 때용). 0 이면 안개 거리만 사용")]
        public float cullDistance = DefaultCullDistance;
        [Tooltip("그림자 거리를 줄인다")]
        public bool limitShadowDistance = true;
        [Tooltip("그림자 거리 최대값(m). 안개 거리가 더 가까우면 그쪽을 쓴다. 0 이면 안개 거리만 사용")]
        public float maxShadowDistance = DefaultShadowDistance;
        [Tooltip("그림자 거리가 원래의 60% 이하로 줄었을 때 방향광(달빛) 그림자 캐스케이드 최대 개수")]
        [Range(1, 4)] public int maxShadowCascades = 2;
        [Tooltip("비워 두면 Main Camera")]
        public Camera targetCamera;

        // 확인용 밝은 분위기에서 측정해 고른 값 (Iris Xe, 시점별 GPU 시간 5~25% 감소, 먼 곳에서 조명이 켜지고 꺼지는 게 조금 보임)
        public const float DefaultCullDistance = 60f, DefaultShadowDistance = 80f;
        const float Hysteresis = 2f;   // 경계에서 깜빡이며 껐다 켰다 하지 않도록

        readonly List<Light> lights = new List<Light>();
        readonly List<bool> culled = new List<bool>();
        float savedShadowDistance;
        int savedShadowCascades;
        bool qualitySaved;

        public int ManagedCount { get { return lights.Count; } }
        public int CulledCount { get { int n = 0; foreach (bool c in culled) if (c) n++; return n; } }

        void OnEnable()
        {
            lights.Clear();
            culled.Clear();
            foreach (var l in GetComponentsInChildren<Light>(false))
            {
                if (!IsManaged(l)) continue;
                lights.Add(l);
                culled.Add(false);
            }
            savedShadowDistance = QualitySettings.shadowDistance;
            savedShadowCascades = QualitySettings.shadowCascades;
            qualitySaved = true;
        }

        void OnDisable()
        {
            for (int i = 0; i < lights.Count; i++)
                if (culled[i] && lights[i] != null) lights[i].gameObject.SetActive(true);
            for (int i = 0; i < culled.Count; i++) culled[i] = false;
            if (qualitySaved)
            {
                QualitySettings.shadowDistance = savedShadowDistance;
                QualitySettings.shadowCascades = savedShadowCascades;
                qualitySaved = false;
            }
        }

        void LateUpdate()
        {
            var cam = targetCamera != null && targetCamera.isActiveAndEnabled ? targetCamera : Camera.main;
            if (cam == null) return;
            Refresh(cam.transform.position, cam.farClipPlane);
        }

        /// <summary>
        /// 끄고 켤 수 있는 조명인지. 오브젝트째 껐다 켜므로 라이트만 달린 오브젝트만 다룬다
        /// (BJ_FlickerLight 가 Light.enabled 를 매 프레임 덮어쓰기 때문).
        /// </summary>
        public static bool IsManaged(Light l)
        {
            if (l.type != LightType.Point && l.type != LightType.Spot) return false;
            if (l.bakingOutput.isBaked && l.bakingOutput.lightmapBakeType == LightmapBakeType.Baked) return false;   // 구운 라이트 (굽기 전엔 실시간으로 그려지므로 계속 다룸)
            var g = l.gameObject;
            return g.transform.childCount == 0 && g.GetComponent<Renderer>() == null && g.GetComponent<BJ_FlickerLight>() == null;
        }

        /// <summary>조명을 끄는 거리 (m): 안개 거리 · Cull Distance · 카메라 Far 중 가장 가까운 것.</summary>
        public float EffectiveDistance(float farClip)
        {
            float d = Mathf.Min(FogDistance(fogVisibility), farClip);
            return cullDistance > 0f ? Mathf.Min(d, cullDistance) : d;
        }

        public static float FogDistance(float visibility)
        {
            if (!RenderSettings.fog) return float.PositiveInfinity;
            float k = -Mathf.Log(Mathf.Clamp(visibility, 1e-4f, 0.999f));
            switch (RenderSettings.fogMode)
            {
                case FogMode.ExponentialSquared: return Mathf.Sqrt(k) / Mathf.Max(RenderSettings.fogDensity, 1e-5f);
                case FogMode.Exponential: return k / Mathf.Max(RenderSettings.fogDensity, 1e-5f);
                default: return RenderSettings.fogEndDistance;
            }
        }

        public void Refresh(Vector3 camPos, float farClip)
        {
            float dist = EffectiveDistance(farClip);
            for (int i = 0; i < lights.Count; i++)
            {
                var l = lights[i];
                if (l == null) continue;
                float edge = (l.transform.position - camPos).magnitude - l.range;
                bool off = culled[i] ? edge > dist : edge > dist + Hysteresis;
                if (off == culled[i]) continue;
                culled[i] = off;
                l.gameObject.SetActive(!off);
            }
            if (!qualitySaved) return;
            float sd = ShadowDistance(savedShadowDistance, farClip);
            if (!Mathf.Approximately(QualitySettings.shadowDistance, sd)) QualitySettings.shadowDistance = sd;
            int casc = ShadowCascades(savedShadowDistance, savedShadowCascades, sd);
            if (QualitySettings.shadowCascades != casc) QualitySettings.shadowCascades = casc;
        }

        /// <summary>원래 그림자 거리가 original 일 때 쓸 그림자 거리.</summary>
        public float ShadowDistance(float original, float farClip)
        {
            if (!limitShadowDistance) return original;
            float d = Mathf.Min(original, Mathf.Min(FogDistance(fogVisibility), farClip));
            return maxShadowDistance > 0f ? Mathf.Min(d, maxShadowDistance) : d;
        }

        /// <summary>그림자 거리가 많이 줄었을 때만 캐스케이드도 줄인다 (가까운 그림자 해상도가 비슷하게 유지됨).</summary>
        public int ShadowCascades(float originalDistance, int originalCascades, float newDistance)
        {
            if (originalCascades <= maxShadowCascades || newDistance > originalDistance * 0.6f) return originalCascades;
            return maxShadowCascades >= 4 ? 4 : maxShadowCascades >= 2 ? 2 : 1;
        }
    }
}
