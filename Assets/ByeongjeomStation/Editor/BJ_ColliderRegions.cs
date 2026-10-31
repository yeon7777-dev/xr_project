using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Byeongjeom.MapGen
{
    public static partial class BJ
    {
        const string StationColliderRoot = "ByeongjeomStation_병점역";

        // Explicit station hierarchy: unknown objects and other scenes are never simplified.
        static void CollectColliderRegions(IList<GameObject> roots, List<GameObject> detailed, List<GameObject> exterior)
        {
            var visited = new HashSet<Transform>();
            foreach (var root in roots)
            {
                if (root == null) continue;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.parent == null || t.parent.name != StationColliderRoot || !visited.Add(t)) continue;
                    string n = t.name;
                    if (n == "Concourse_Structure" || n == "Concourse_Interior" || n == "Stairs_Escalators_Elevators"
                        || n == "Exits_출구" || n.StartsWith("Platform_") || n.StartsWith("Train_"))
                        detailed.Add(t.gameObject);
                    else if (n == "Ground" || n == "Tracks" || n == "East_역앞광장" || n == "West_서측"
                             || n == "Bounds_울타리_경계" || n == "Railway_전차선_신호")
                        exterior.Add(t.gameObject);
                }
            }
        }

        static int ColliderTotal(IList<GameObject> roots)
        {
            int b, c, s, m;
            CountColliders(roots, out b, out c, out s, out m);
            return b + c + s + m;
        }

        static bool HasColliderBehaviour(GameObject g)
        {
            if (g.GetComponentInParent<Rigidbody>() != null || g.GetComponentInParent<ArticulationBody>() != null
                || g.GetComponentInParent<CharacterController>() != null || g.GetComponentInParent<Animator>() != null
                || g.GetComponentInParent<Animation>() != null || g.GetComponentInParent<Joint>() != null) return true;
            // These existing map scripts affect only lights/fog/NavMesh registration.
            foreach (var script in g.GetComponentsInParent<MonoBehaviour>(true))
            {
                if (script == null) return true; // Missing scripts may own collision callbacks.
                string type = script.GetType().FullName;
                if (type != "Byeongjeom.BJ_FlickerLight" && type != "Byeongjeom.BJ_LightCuller"
                    && type != "Byeongjeom.BJ_NavMeshLoader" && type != "Byeongjeom.BJ_DeferredFog") return true;
            }
            return false;
        }

        static bool SameColliderSemantics(Collider a, Collider b)
        {
            return a.gameObject.layer == b.gameObject.layer && a.gameObject.tag == b.gameObject.tag
                && a.sharedMaterial == b.sharedMaterial && Mathf.Approximately(a.contactOffset, b.contactOffset)
                && GameObjectUtility.GetStaticEditorFlags(a.gameObject) == GameObjectUtility.GetStaticEditorFlags(b.gameObject)
                && GameObjectUtility.GetNavMeshArea(a.gameObject) == GameObjectUtility.GetNavMeshArea(b.gameObject);
        }

        static readonly HashSet<string> InteriorSolidParts = new HashSet<string>
        {
            "Seat", "Back", "Leg", "Arm", "Base", "Post", "Top", "Drawer", "Monitor", "TV",
            "Upright", "Board", "SeatCushion", "SeatBack", "Stanchion", "GrabRail", "SofaBack",
            "Tank", "Basin", "Counter", "CounterBase", "UrinalDivider", "Lid", "OpenDoor",
            "GeneratorExhaust", "Ladder", "NoticeBoard", "GateTop", "Hood", "Bag",
            "RailTop", "RailBase", "Handrail", "PartitionTop", "PartitionBase", "PartitionPost",
            "DoorMullion", "DoorTransom", "CabDoorTop", "BellowsTop", "Ceiling", "RoofSlab",
            "Shutter"
        };

        static void RepairInteriorColliders(IList<GameObject> roots, ColliderReport report, bool undo)
        {
            foreach (var root in roots)
            {
                // Undo an old solid proxy on known furniture, so seats/shelf gaps remain real gaps.
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name != "Bench" && t.name != "Shelf" && t.name != "Desk" && t.name != "Chair") continue;
                    if (!GoEligible(t.gameObject)) continue;
                    var cols = t.GetComponents<Collider>();
                    bool safe = true;
                    foreach (var child in t.GetComponentsInChildren<Transform>(true))
                        if (!GoEligible(child.gameObject)) { safe = false; break; }
                    foreach (var col in t.GetComponentsInChildren<Collider>(true))
                        if (!(col is BoxCollider) || !ColEligible(col)) { safe = false; break; }
                    if (!safe) continue;
                    foreach (var col in cols) { Kill(col, undo); report.interiorProxiesRemoved++; }
                }

                foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null || !GoEligible(mf.gameObject) || mf.GetComponents<Collider>().Length != 0) continue;
                    // A shaft, barricade or other intentionally solid assembly already owns its shape.
                    if (mf.transform.parent != null && mf.transform.parent.GetComponentInParent<Collider>() != null) continue;
                    bool ceilingTiles = mf.name == "CeilingTiles";
                    if (!ceilingTiles && !InteriorSolidParts.Contains(mf.name)) continue;
                    var mesh = mf.sharedMesh;
                    if (mesh.name.StartsWith("Box_"))
                    {
                        var box = AddCol<BoxCollider>(mf.gameObject, undo);
                        box.center = mesh.bounds.center;
                        box.size = mesh.bounds.size;
                        report.interiorAdded++;
                    }
                    else if (mesh.name == "Cylinder" || mesh.name == "pCylinder1")
                    {
                        var cap = AddCol<CapsuleCollider>(mf.gameObject, undo);
                        cap.center = mesh.bounds.center;
                        cap.direction = 1;
                        cap.radius = Mathf.Max(mesh.bounds.extents.x, mesh.bounds.extents.z);
                        cap.height = mesh.bounds.size.y;
                        report.interiorAdded++;
                    }
                    else if (mesh.name == "Sphere" || mesh.name == "pSphere1")
                    {
                        // Scaled bags are better represented by their local box than an oversized sphere.
                        var box = AddCol<BoxCollider>(mf.gameObject, undo);
                        box.center = mesh.bounds.center;
                        box.size = mesh.bounds.size;
                        report.interiorAdded++;
                    }
                    else if (ceilingTiles && DecomposeBoxMesh(mesh) != null)
                    {
                        // The existing converter preserves missing ceiling panels, just as floor openings.
                        AddCol<MeshCollider>(mf.gameObject, undo).sharedMesh = mesh;
                        report.interiorAdded++;
                    }
                }
            }
        }

        static void SimplifyExteriorColliders(IList<GameObject> roots, ColliderReport report, bool undo)
        {
            foreach (var root in roots)
            {
                // Only solid standalone props are enclosed. Never enclose a shelter, garage, doorway or stairwell.
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!GoEligible(t.gameObject)) continue;
                    if (t.name.StartsWith("Car_") || t.name == "Bus" || t.name == "Bench" || t.name == "Planter")
                        SimplifyExteriorProp(t, report, undo);
                }
                foreach (var col in root.GetComponentsInChildren<BoxCollider>(true))
                {
                    if (!ColEligible(col) || col.transform.parent == null) continue;
                    if (col.transform.parent.name != "ParkingTower_주차타워") continue;
                    // The shutter already blocks access to upper floors; retain the first ceiling and open ground floor.
                    if (col.name == "Parapet" || (col.name == "Slab" && col.transform.localPosition.y > 4f))
                    {
                        RemoveCollider(col, undo);
                        report.exteriorRemoved++;
                    }
                }
            }
        }

        static void SimplifyExteriorProp(Transform group, ColliderReport report, bool undo)
        {
            var cols = new List<Collider>(group.GetComponents<Collider>());
            foreach (Transform child in group) cols.AddRange(child.GetComponents<Collider>());
            if (cols.Count < 2) return;
            var first = cols[0];
            Vector3 lo = Vector3.one * float.MaxValue, hi = -lo;
            foreach (var col in cols)
            {
                if (!ColEligible(col) || !SameColliderSemantics(first, col)
                    || col.gameObject.layer != group.gameObject.layer || col.gameObject.tag != group.gameObject.tag) return;
                var shape = MakeShape(col);
                if (shape == null) return;
                foreach (var corner in shape.Corners())
                {
                    Vector3 p = group.InverseTransformPoint(corner);
                    lo = Vector3.Min(lo, p); hi = Vector3.Max(hi, p);
                }
            }
            var box = AddCol<BoxCollider>(group.gameObject, undo);
            box.center = (lo + hi) * 0.5f;
            box.size = hi - lo;
            CopyColliderSettings(first, box);
            foreach (var col in cols) RemoveCollider(col, undo);
            report.exteriorProxies++;
            report.exteriorRemoved += cols.Count;
        }
    }
}
