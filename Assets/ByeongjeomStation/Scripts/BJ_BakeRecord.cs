using System.Collections.Generic;
using UnityEngine;

namespace Byeongjeom
{
    /// <summary>
    /// Tools > 병점역 맵 > 조명 굽기 가 바꾼 라이트의 원래 설정 (되돌리기용).
    /// EditorOnly 태그 오브젝트에 붙으므로 빌드에는 들어가지 않는다.
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public class BJ_BakeRecord : MonoBehaviour
    {
        public List<Light> lights = new List<Light>();
        public List<LightmapBakeType> bakeTypes = new List<LightmapBakeType>();
        public List<LightShadows> shadows = new List<LightShadows>();
        [Tooltip("굽기 전에 씬에 지정돼 있던 Lighting Settings 에셋 경로")]
        public string lightingSettingsPath;
        [Tooltip("구운 시각 · 걸린 시간 · 라이트맵 용량")]
        [TextArea] public string lastBake;
    }
}
