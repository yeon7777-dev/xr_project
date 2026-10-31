using UnityEngine;

namespace Byeongjeom
{
    /// <summary>
    /// 고장난 형광등/가로등 연출. 라이트 세기와 발광 머티리얼(_EmissionColor)을 함께 깜빡인다.
    ///  - Flicker : 대부분 켜져 있다가 가끔 툭툭 꺼짐 (수명 다한 형광등)
    ///  - Dying   : 대부분 꺼져 있다가 가끔 번쩍 (거의 죽은 등)
    ///  - Buzz    : 은은하게 흔들림 (전압 불안정)
    ///  - Strobe  : 일정 주기 점멸 (신호등 황색 점멸, 비상등)
    /// </summary>
    [DisallowMultipleComponent]
    public class BJ_FlickerLight : MonoBehaviour
    {
        public enum Mode { Flicker, Dying, Buzz, Strobe }

        public Mode mode = Mode.Flicker;
        public Light[] lights = new Light[0];
        public Renderer[] emissiveRenderers = new Renderer[0];
        [ColorUsage(false, true)] public Color emissionColor = Color.white;
        [Range(0.1f, 4f)] public float speed = 1f;
        public float seed;
        [Tooltip("Strobe 모드 주기(초)")] public float strobePeriod = 1.2f;

        float[] baseIntensity;
        MaterialPropertyBlock mpb;
        float timer;
        float target = 1f;
        float current = 1f;
        float applied = -1f;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        void Awake()
        {
            baseIntensity = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++)
                baseIntensity[i] = lights[i] != null ? lights[i].intensity : 0f;
            mpb = new MaterialPropertyBlock();
            if (seed == 0f) seed = Random.Range(0f, 1000f);
        }

        void Update()
        {
            if (!AnythingToShow()) return;
            float t = Time.time * speed + seed;
            float f;
            switch (mode)
            {
                case Mode.Buzz:
                    f = 0.7f + 0.3f * Mathf.PerlinNoise(t * 9f, seed);
                    break;
                case Mode.Strobe:
                    f = Mathf.Repeat(t, strobePeriod) < strobePeriod * 0.5f ? 1f : 0f;
                    break;
                case Mode.Dying:
                    timer -= Time.deltaTime * speed;
                    if (timer <= 0f)
                    {
                        if (Random.value < 0.3f) { target = Random.Range(0.35f, 1f); timer = Random.Range(0.02f, 0.25f); }
                        else { target = 0f; timer = Random.Range(0.4f, 3.5f); }
                    }
                    f = target;
                    break;
                default: // Flicker
                    timer -= Time.deltaTime * speed;
                    if (timer <= 0f)
                    {
                        if (Random.value < 0.2f) { target = Random.Range(0f, 0.25f); timer = Random.Range(0.03f, 0.14f); }
                        else { target = 1f; timer = Random.Range(0.05f, 2.2f); }
                    }
                    f = target * (0.9f + 0.1f * Mathf.PerlinNoise(t * 25f, seed));
                    break;
            }
            // 너무 딱딱 끊기지 않도록 아주 살짝 보간
            current = Mathf.Lerp(current, f, 1f - Mathf.Exp(-Time.deltaTime * 60f));
            if (Mathf.Abs(current - applied) > 0.002f) Apply(current);
        }

        /// <summary>라이트가 모두 꺼져 있고(BJ_LightCuller 가 끈 먼 조명) 발광 물체도 화면에 없으면 계산하지 않는다.</summary>
        bool AnythingToShow()
        {
            for (int i = 0; i < lights.Length; i++)
                if (lights[i] != null && lights[i].gameObject.activeInHierarchy) return true;
            for (int i = 0; i < emissiveRenderers.Length; i++)
                if (emissiveRenderers[i] != null && emissiveRenderers[i].isVisible) return true;
            return false;
        }

        void Apply(float f)
        {
            applied = f;
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] == null) continue;
                lights[i].intensity = baseIntensity[i] * f;
                lights[i].enabled = f > 0.01f;
            }
            if (emissiveRenderers.Length == 0) return;
            mpb.SetColor(EmissionId, emissionColor * f);
            for (int i = 0; i < emissiveRenderers.Length; i++)
                if (emissiveRenderers[i] != null) emissiveRenderers[i].SetPropertyBlock(mpb);
        }

        void OnDisable()
        {
            // 꺼질 때 원래 상태로 복구
            applied = -1f;
            if (baseIntensity == null) return;
            for (int i = 0; i < lights.Length; i++)
                if (lights[i] != null) { lights[i].intensity = baseIntensity[i]; lights[i].enabled = true; }
            for (int i = 0; i < emissiveRenderers.Length; i++)
                if (emissiveRenderers[i] != null) emissiveRenderers[i].SetPropertyBlock(null);
        }
    }
}
