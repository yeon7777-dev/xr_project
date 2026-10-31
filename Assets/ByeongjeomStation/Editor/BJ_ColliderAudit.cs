using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Byeongjeom.MapGen
{
    public static partial class BJ
    {
        [Serializable]
        public class ColliderAuditEntry
        {
            public string path, mesh;
            public Vector3 position, size;
            public int colliders, ancestorColliders;
            public bool active;
        }

        [Serializable]
        public class ColliderAudit
        {
            public int boxes, capsules, spheres, meshes;
            public List<ColliderAuditEntry> renderers = new List<ColliderAuditEntry>();
            public List<string> groups = new List<string>();
        }

        static string ColliderObjectPath(Transform t)
        {
            return t.parent == null ? t.name : ColliderObjectPath(t.parent) + "/" + t.name;
        }

        public static void AuditCollidersBatch()
        {
            EditorSceneManager.OpenScene(ScenePath);
            WriteColliderAudit("Logs/Colliders-before.json");
        }

        [MenuItem("Tools/병점역 맵/콜라이더 점검 보고서 %#F7", false, 22)]
        public static void AuditCurrentColliders()
        {
            var root = GameObject.Find("ByeongjeomStation_병점역");
            if (root == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            Directory.CreateDirectory("Backups/ColliderOptimization_20261019");
            const string backup = "Backups/ColliderOptimization_20261019/ByeongjeomStation-open-scene.unity";
            if (!File.Exists(backup)) EditorSceneManager.SaveScene(root.scene, backup, true);
            WriteColliderAudit("Logs/Colliders-before.json");
        }

        static void WriteColliderAudit(string path)
        {
            var root = GameObject.Find("ByeongjeomStation_병점역");
            if (root == null) throw new InvalidOperationException("Station root not found.");
            Physics.SyncTransforms();
            var audit = new ColliderAudit();
            CountColliders(new[] { root }, out audit.boxes, out audit.capsules, out audit.spheres, out audit.meshes);
            foreach (Transform group in root.transform)
            {
                int b, c, s, m;
                CountColliders(new[] { group.gameObject }, out b, out c, out s, out m);
                audit.groups.Add(group.name + ": Box=" + b + ", Capsule=" + c + ", Sphere=" + s + ", Mesh=" + m);
            }
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var r = mf.GetComponent<Renderer>();
                audit.renderers.Add(new ColliderAuditEntry {
                    path = ColliderObjectPath(mf.transform), mesh = mf.sharedMesh.name,
                    position = mf.transform.position, size = r != null ? r.bounds.size : Vector3.zero,
                    colliders = mf.GetComponents<Collider>().Length,
                    ancestorColliders = mf.transform.parent != null ? mf.transform.parent.GetComponentsInParent<Collider>(true).Length : 0,
                    active = mf.gameObject.activeInHierarchy
                });
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(audit, true));
            Debug.Log("[병점역 콜라이더 점검] " + string.Join("\n", audit.groups));
        }
    }
}
