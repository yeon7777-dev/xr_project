using UnityEngine;

namespace Byeongjeom
{
    /// <summary>
    /// Built-in 렌더 파이프라인 전용 카메라 세팅.
    ///  - 실시간 조명이 많은 맵이라 카메라를 디퍼드(Deferred) 렌더링으로 바꾼다.
    ///  - 디퍼드에서는 Lighting 창의 안개가 불투명 물체에 안 먹으므로, 같은 값으로 안개를 직접 입힌다.
    /// 플레이어 카메라에 이 컴포넌트를 붙이면 된다. (URP 프로젝트에서는 필요 없음)
    /// </summary>
    [ExecuteAlways]
    [ImageEffectAllowedInSceneView]
    [RequireComponent(typeof(Camera))]
    public class BJ_DeferredFog : MonoBehaviour
    {
        public Shader fogShader;
        Material mat;

        void OnEnable()
        {
            var cam = GetComponent<Camera>();
            cam.renderingPath = RenderingPath.DeferredShading;
            cam.depthTextureMode |= DepthTextureMode.Depth;
            if (fogShader == null) fogShader = Shader.Find("Hidden/BJ_DeferredFog");
        }

        void OnDisable()
        {
            if (mat != null) DestroyImmediate(mat);
            mat = null;
        }

        [ImageEffectOpaque]
        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (!RenderSettings.fog || fogShader == null || !fogShader.isSupported)
            {
                Graphics.Blit(src, dst);
                return;
            }
            if (mat == null) mat = new Material(fogShader) { hideFlags = HideFlags.HideAndDontSave };
            mat.SetColor("_BJFogColor", RenderSettings.fogColor);
            mat.SetFloat("_BJFogDensity", RenderSettings.fogDensity);
            Graphics.Blit(src, dst, mat);
        }
    }
}
