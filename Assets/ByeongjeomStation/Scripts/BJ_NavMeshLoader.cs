using UnityEngine;
using UnityEngine.AI;

namespace Byeongjeom
{
    /// <summary>
    /// AI Navigation 패키지(NavMeshSurface)가 없는 프로젝트용.
    /// 맵 생성기가 구운 NavMeshData 를 씬이 켜질 때 NavMesh 에 등록한다. (에디터에서도 표시됨)
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class BJ_NavMeshLoader : MonoBehaviour
    {
        public NavMeshData data;
        NavMeshDataInstance instance;

        void OnEnable() { Load(); }
        void OnDisable() { Unload(); }

        public void Load()
        {
            Unload();
            if (data != null) instance = NavMesh.AddNavMeshData(data, transform.position, transform.rotation);
        }

        public void Unload()
        {
            if (instance.valid) instance.Remove();
            instance = new NavMeshDataInstance();
        }
    }
}
