#if UNITY_EDITOR
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Generates persistent upward links from the baked NavMesh boundary.
/// The generator is shape-agnostic: boxes, cylinders and arbitrary meshes use the same path.
/// </summary>
public sealed class JumpUpLinkBakeProcessor : EditorWindow
{
    private const string GeneratedRootName = "_GeneratedJumpUpLinks";

    [SerializeField, Min(0.05f)] private float linkSpacing = 1f;
    [SerializeField, Min(0f)] private float edgePadding = 0.1f;
    [SerializeField, Min(0.01f)] private float startOffset = 1.5f;
    [SerializeField, Min(0f)] private float landingOffset = 0.25f;
    [SerializeField, Min(0.01f)] private float stepHeight = 0.45f;
    [SerializeField, Min(0.01f)] private float maxJumpHeight = 2.5f;
    [SerializeField, Min(0f)] private float linkCost = 3f;
    [SerializeField] private NavMeshSurface navMeshSurface;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private int area;
    [SerializeField] private bool bidirectional;
    [SerializeField] private bool clearPreviousLinks = true;

    [MenuItem("Tools/NavMesh/Jump-Up Link Generator")]
    private static void Open()
    {
        GetWindow<JumpUpLinkBakeProcessor>("Jump-Up Links");
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Bake the base NavMesh first. Links are generated from NavMesh boundary edges, not collider types.",
            MessageType.Info);

        linkSpacing = EditorGUILayout.FloatField("Link Spacing", linkSpacing);
        edgePadding = EditorGUILayout.FloatField("Edge Padding", edgePadding);
        startOffset = EditorGUILayout.FloatField("Start Offset", startOffset);
        landingOffset = EditorGUILayout.FloatField("Landing Offset", landingOffset);
        stepHeight = EditorGUILayout.FloatField("Minimum Height", stepHeight);
        maxJumpHeight = EditorGUILayout.FloatField("Maximum Height", maxJumpHeight);
        linkCost = EditorGUILayout.FloatField("Link Cost (0 = default)", linkCost);
        navMeshSurface = (NavMeshSurface)EditorGUILayout.ObjectField(
            "NavMesh Surface",
            navMeshSurface,
            typeof(NavMeshSurface),
            true);

        NavMeshSurface surface = ResolveSurface();
        if (surface != null)
        {
            EditorGUILayout.HelpBox(
                $"Using Agent Type ID {surface.agentTypeID} from {surface.name}.",
                MessageType.None);
        }
        else
        {
            EditorGUILayout.HelpBox("Assign or enable a NavMeshSurface in the active scene.", MessageType.Warning);
        }

        groundMask = EditorGUILayout.MaskField(
            "Ground Layers",
            groundMask,
            UnityEditorInternal.InternalEditorUtility.layers);
        area = EditorGUILayout.IntField("Area", area);
        bidirectional = EditorGUILayout.Toggle("Bidirectional", bidirectional);
        clearPreviousLinks = EditorGUILayout.Toggle("Clear Previous Generated Links", clearPreviousLinks);

        if (GUILayout.Button("Generate Jump-Up Links"))
            Generate();
    }

    private void Generate()
    {
        if (linkSpacing <= 0f || startOffset <= 0f || maxJumpHeight <= stepHeight)
        {
            EditorUtility.DisplayDialog("Jump-Up Links", "Check the spacing, offsets and height values.", "OK");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog("Jump-Up Links", "Open a valid scene before generating links.", "OK");
            return;
        }

        NavMeshSurface surface = ResolveSurface();
        if (surface == null)
        {
            EditorUtility.DisplayDialog("Jump-Up Links", "No NavMeshSurface was found in the active scene.", "OK");
            return;
        }

        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
        if (triangulation.vertices == null || triangulation.vertices.Length == 0)
        {
            EditorUtility.DisplayDialog("Jump-Up Links", "Bake the base NavMesh before generating links.", "OK");
            return;
        }

        GameObject root = GetOrCreateGeneratedRoot(scene);
        if (clearPreviousLinks)
            ClearGeneratedLinks(root);

        List<BoundaryEdge> boundaries = FindBoundaryEdges(triangulation);
        List<JumpCandidate> candidates = BuildCandidates(triangulation, boundaries);
        int generated = CreateLinks(root.transform, candidates, surface.agentTypeID);

        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Jump-Up Links", $"Generated {generated} jump-up link(s).", "OK");
    }

    private List<BoundaryEdge> FindBoundaryEdges(NavMeshTriangulation triangulation)
    {
        Dictionary<EdgeKey, BoundaryEdge> edges = new Dictionary<EdgeKey, BoundaryEdge>();
        int[] indices = triangulation.indices;

        for (int i = 0; i < indices.Length; i += 3)
        {
            int a = indices[i];
            int b = indices[i + 1];
            int c = indices[i + 2];
            Vector3 center = (triangulation.vertices[a] + triangulation.vertices[b] + triangulation.vertices[c]) / 3f;

            RegisterEdge(edges, a, b, center);
            RegisterEdge(edges, b, c, center);
            RegisterEdge(edges, c, a, center);
        }

        List<BoundaryEdge> boundaries = new List<BoundaryEdge>();
        foreach (BoundaryEdge edge in edges.Values)
        {
            if (edge.UsageCount == 1)
                boundaries.Add(edge);
        }

        return boundaries;
    }

    private static void RegisterEdge(
        Dictionary<EdgeKey, BoundaryEdge> edges,
        int first,
        int second,
        Vector3 triangleCenter)
    {
        EdgeKey key = new EdgeKey(first, second);
        if (edges.TryGetValue(key, out BoundaryEdge existing))
        {
            existing.UsageCount++;
            edges[key] = existing;
            return;
        }

        edges.Add(key, new BoundaryEdge(first, second, triangleCenter));
    }

    private List<JumpCandidate> BuildCandidates(
        NavMeshTriangulation triangulation,
        List<BoundaryEdge> boundaries)
    {
        List<JumpCandidate> candidates = new List<JumpCandidate>();

        foreach (BoundaryEdge boundary in boundaries)
        {
            Vector3 first = triangulation.vertices[boundary.FirstIndex];
            Vector3 second = triangulation.vertices[boundary.SecondIndex];
            Vector3 edge = second - first;
            Vector3 horizontalEdge = Vector3.ProjectOnPlane(edge, Vector3.up);

            if (horizontalEdge.sqrMagnitude < 0.0001f || Mathf.Abs(edge.y) > 0.1f)
                continue;

            Vector3 tangent = horizontalEdge.normalized;
            Vector3 midpoint = (first + second) * 0.5f;
            Vector3 inward = Vector3.ProjectOnPlane(boundary.TriangleCenter - midpoint, Vector3.up);

            if (inward.sqrMagnitude < 0.0001f)
                continue;

            inward.Normalize();
            float edgeLength = horizontalEdge.magnitude;
            float usableLength = Mathf.Max(0f, edgeLength - edgePadding * 2f);
            int count = Mathf.Max(1, Mathf.CeilToInt(usableLength / linkSpacing));

            for (int i = 0; i <= count; i++)
            {
                float t = count == 0 ? 0.5f : i / (float)count;
                float alongEdge = Mathf.Lerp(edgePadding, edgeLength - edgePadding, t);
                Vector3 edgePoint = first + tangent * alongEdge;
                Vector3 upper = edgePoint + inward * landingOffset + Vector3.up * 0.05f;
                Vector3 lowerProbe = edgePoint - inward * startOffset;

                if (!TryFindLowerFloor(lowerProbe, upper.y, out Vector3 lower))
                    continue;

                float heightDifference = upper.y - lower.y;
                if (heightDifference <= stepHeight || heightDifference > maxJumpHeight)
                    continue;

                if (!IsNavMeshPoint(lower, 0.2f) || !IsNavMeshPoint(upper, landingOffset + 0.1f))
                    continue;

                candidates.Add(new JumpCandidate(lower, upper));
            }
        }

        return candidates;
    }

    private bool TryFindLowerFloor(Vector3 probe, float upperY, out Vector3 lower)
    {
        Vector3 rayOrigin = new Vector3(probe.x, upperY + maxJumpHeight + 1f, probe.z);
        float rayDistance = maxJumpHeight * 2f + 2f;

        if (Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                rayDistance,
                groundMask,
                QueryTriggerInteraction.Ignore))
        {
            lower = hit.point + Vector3.up * 0.05f;
            return true;
        }

        lower = default;
        return false;
    }

    private static bool IsNavMeshPoint(Vector3 point, float sampleRadius)
    {
        float radius = Mathf.Max(0.2f, sampleRadius);
        return NavMesh.SamplePosition(point, out NavMeshHit hit, radius, NavMesh.AllAreas) &&
               Vector3.Distance(hit.position, point) <= radius;
    }

    private int CreateLinks(Transform root, List<JumpCandidate> candidates, int agentTypeId)
    {
        HashSet<string> created = new HashSet<string>();
        int generated = 0;

        foreach (JumpCandidate candidate in candidates)
        {
            string key = BuildLinkKey(candidate.Start, candidate.End);
            if (!created.Add(key))
                continue;

            GameObject linkObject = new GameObject("JumpUpLink");
            Undo.RegisterCreatedObjectUndo(linkObject, "Create Jump-Up Link");
            linkObject.transform.SetParent(root, false);

            NavMeshLink link = linkObject.AddComponent<NavMeshLink>();
            link.agentTypeID = agentTypeId;
            link.area = area;
            link.startPoint = candidate.Start - linkObject.transform.position;
            link.endPoint = candidate.End - linkObject.transform.position;
            link.bidirectional = bidirectional;
            link.costModifier = linkCost > 0f ? Mathf.Max(1f, linkCost) : -1f;
            link.autoUpdate = false;

            linkObject.AddComponent<JumpUpLinkMarker>();
            generated++;
        }

        return generated;
    }

    [MenuItem("Tools/NavMesh/Repair Generated Jump-Up Link Agent Types")]
    private static void RepairGeneratedLinkAgentTypes()
    {
        Scene scene = SceneManager.GetActiveScene();
        NavMeshSurface[] surfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);
        NavMeshSurface surface = null;

        foreach (NavMeshSurface candidate in surfaces)
        {
            if (candidate != null && candidate.gameObject.scene == scene)
            {
                surface = candidate;
                break;
            }
        }

        if (surface == null)
        {
            EditorUtility.DisplayDialog("Jump-Up Links", "No NavMeshSurface was found in the active scene.", "OK");
            return;
        }

        JumpUpLinkMarker[] markers = Object.FindObjectsByType<JumpUpLinkMarker>(FindObjectsSortMode.None);
        int repaired = 0;

        foreach (JumpUpLinkMarker marker in markers)
        {
            if (marker == null || marker.gameObject.scene != scene)
                continue;

            NavMeshLink link = marker.GetComponent<NavMeshLink>();
            if (link == null)
                continue;

            Undo.RecordObject(link, "Repair Jump-Up Link Agent Type");
            link.agentTypeID = surface.agentTypeID;
            repaired++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorUtility.DisplayDialog(
            "Jump-Up Links",
            $"Repaired {repaired} generated link(s) to Agent Type ID {surface.agentTypeID}.",
            "OK");
    }

    private NavMeshSurface ResolveSurface()
    {
        if (navMeshSurface != null && navMeshSurface.gameObject.scene == SceneManager.GetActiveScene())
            return navMeshSurface;

        NavMeshSurface[] surfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);
        foreach (NavMeshSurface surface in surfaces)
        {
            if (surface != null && surface.gameObject.scene == SceneManager.GetActiveScene())
                return surface;
        }

        return null;
    }

    private static GameObject GetOrCreateGeneratedRoot(Scene scene)
    {
        GameObject root = GameObject.Find(GeneratedRootName);
        if (root != null && root.scene == scene)
            return root;

        root = new GameObject(GeneratedRootName);
        Undo.RegisterCreatedObjectUndo(root, "Create Jump-Up Link Root");
        SceneManager.MoveGameObjectToScene(root, scene);
        return root;
    }

    private static void ClearGeneratedLinks(GameObject root)
    {
        List<GameObject> children = new List<GameObject>();
        for (int i = 0; i < root.transform.childCount; i++)
            children.Add(root.transform.GetChild(i).gameObject);

        foreach (GameObject child in children)
            Undo.DestroyObjectImmediate(child);
    }

    private static string BuildLinkKey(Vector3 start, Vector3 end)
    {
        Vector3 roundedStart = Round(start);
        Vector3 roundedEnd = Round(end);
        return $"{roundedStart}->{roundedEnd}";
    }

    private static Vector3 Round(Vector3 value)
    {
        return new Vector3(
            Mathf.Round(value.x * 100f) / 100f,
            Mathf.Round(value.y * 100f) / 100f,
            Mathf.Round(value.z * 100f) / 100f);
    }

    private readonly struct EdgeKey : System.IEquatable<EdgeKey>
    {
        private readonly int first;
        private readonly int second;

        public EdgeKey(int first, int second)
        {
            if (first < second)
            {
                this.first = first;
                this.second = second;
            }
            else
            {
                this.first = second;
                this.second = first;
            }
        }

        public bool Equals(EdgeKey other)
        {
            return first == other.first && second == other.second;
        }

        public override bool Equals(object obj)
        {
            return obj is EdgeKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return first * 397 ^ second;
            }
        }
    }

    private struct BoundaryEdge
    {
        public readonly int FirstIndex;
        public readonly int SecondIndex;
        public readonly Vector3 TriangleCenter;
        public int UsageCount;

        public BoundaryEdge(int firstIndex, int secondIndex, Vector3 triangleCenter)
        {
            FirstIndex = firstIndex;
            SecondIndex = secondIndex;
            TriangleCenter = triangleCenter;
            UsageCount = 1;
        }
    }

    private readonly struct JumpCandidate
    {
        public readonly Vector3 Start;
        public readonly Vector3 End;

        public JumpCandidate(Vector3 start, Vector3 end)
        {
            Start = start;
            End = end;
        }
    }
}
#endif
