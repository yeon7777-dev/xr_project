using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Byeongjeom.MapGen
{
    /// <summary>
    /// 편집 편의 메뉴. 공포맵 기본 조명은 일부러 매우 어둡고 안개가 짙어서
    /// 멀리서 씬 뷰로 보면 전부 검게 보인다. 아래 메뉴로 확인용으로 밝게 볼 수 있다.
    /// </summary>
    public static class BJ_ViewTools
    {
        const string Root = "Tools/병점역 맵/";

        [MenuItem(Root + "씬 뷰: 밝게 보기 (편집용 - 조명·안개 끄기)", false, 40)]
        static void SceneViewBright() { SetSceneView(false); }

        [MenuItem(Root + "씬 뷰: 게임처럼 보기 (조명·안개 켜기)", false, 41)]
        static void SceneViewGame() { SetSceneView(true); }

        static void SetSceneView(bool game)
        {
            foreach (SceneView sv in SceneView.sceneViews)
            {
                sv.sceneLighting = game;
                sv.sceneViewState.showFog = game;
                sv.sceneViewState.showSkybox = game;
                sv.sceneViewState.showImageEffects = game;
                sv.Repaint();
            }
        }

        [MenuItem(Root + "분위기: 확인용 (밝게)", false, 60)]
        static void AtmosphereBright()
        {
            Apply(new Color(0.34f, 0.36f, 0.42f), new Color(0.24f, 0.25f, 0.27f), new Color(0.1f, 0.1f, 0.1f), 0.006f, 0.7f);
        }

        [MenuItem(Root + "분위기: 공포 기본 (어둡게)", false, 61)]
        static void AtmosphereDark()
        {
            Apply(new Color(0.05f, 0.056f, 0.072f), new Color(0.034f, 0.036f, 0.04f), new Color(0.016f, 0.015f, 0.014f), 0.028f, 0.13f);
        }

        static void Apply(Color sky, Color equator, Color ground, float fogDensity, float moonIntensity)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = ground;
            RenderSettings.fogDensity = fogDensity;
            var moon = RenderSettings.sun;
            if (moon == null)
            {
                var go = GameObject.Find("Moonlight");
                if (go != null) moon = go.GetComponent<Light>();
            }
            if (moon != null)
            {
                moon.intensity = moonIntensity;
                EditorUtility.SetDirty(moon);
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            SceneView.RepaintAll();
        }
    }
}
