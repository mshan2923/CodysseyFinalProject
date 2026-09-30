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
    [SerializeField, Min(0.01f)] private float startOffset = 0.5f;
    [SerializeField, Min(0f)] private float landingOffset = 0.05f;
    [SerializeField, Min(0.01f)] private float stepHeight = 0.45f;
    [SerializeField, Min(0.01f)] private float maxJumpHeight = 2.5f;
    [SerializeField, Min(0f)] private float linkCost = 1f;
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

        List<BoundaryEdge> boundaries = FindBoundaryEdges(triangulation, out List<Vector3> vertices);
        List<BoundaryChain> chains = BuildChains(vertices, boundaries);
        List<JumpCandidate> candidates = BuildCandidates(chains);
        int generated = CreateLinks(root.transform, candidates, surface.agentTypeID);

        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Jump-Up Links", $"Generated {generated} jump-up link(s).", "OK");
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


    private sealed class BoundaryChain
    {
        public readonly List<Vector3> Points = new List<Vector3>();
        public readonly List<Vector3> Normals = new List<Vector3>(); // 세그먼트당 1개 (Points.Count - 1)
    }

    private static List<BoundaryEdge> FindBoundaryEdges(NavMeshTriangulation triangulation, out List<Vector3> vertices)
    {
        // 중복 정점 weld
        vertices = new List<Vector3>();
        Dictionary<Vector3Int, int> weld = new Dictionary<Vector3Int, int>();
        int[] remap = new int[triangulation.vertices.Length];

        for (int i = 0; i < remap.Length; i++)
        {
            Vector3 v = triangulation.vertices[i];
            Vector3Int key = new Vector3Int(
                Mathf.RoundToInt(v.x * 100f),
                Mathf.RoundToInt(v.y * 100f),
                Mathf.RoundToInt(v.z * 100f));

            if (!weld.TryGetValue(key, out int id))
            {
                id = vertices.Count;
                weld.Add(key, id);
                vertices.Add(v);
            }

            remap[i] = id;
        }

        Dictionary<EdgeKey, BoundaryEdge> edges = new Dictionary<EdgeKey, BoundaryEdge>();
        int[] indices = triangulation.indices;

        for (int i = 0; i < indices.Length; i += 3)
        {
            int a = remap[indices[i]];
            int b = remap[indices[i + 1]];
            int c = remap[indices[i + 2]];
            if (a == b || b == c || c == a)
                continue;

            Vector3 center = (vertices[a] + vertices[b] + vertices[c]) / 3f;
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

    private static List<BoundaryChain> BuildChains(List<Vector3> vertices, List<BoundaryEdge> edges)
    {
        const float minDirectionDot = 0.9f;   // 약 25도 이상 꺾이면 체인 분리
        const float minNormalDot = 0.7f;
        const float maxEdgeHeight = 0.1f;

        Vector3[] normals = new Vector3[edges.Count];
        bool[] valid = new bool[edges.Count];
        bool[] used = new bool[edges.Count];
        Dictionary<int, List<int>> adjacency = new Dictionary<int, List<int>>();

        for (int i = 0; i < edges.Count; i++)
        {
            BoundaryEdge e = edges[i];
            Vector3 a = vertices[e.FirstIndex];
            Vector3 b = vertices[e.SecondIndex];
            Vector3 d = b - a;
            Vector3 h = Vector3.ProjectOnPlane(d, Vector3.up);

            if (h.sqrMagnitude < 0.0001f || Mathf.Abs(d.y) > maxEdgeHeight)
                continue;

            Vector3 tangent = h.normalized;
            Vector3 normal = new Vector3(-tangent.z, 0f, tangent.x); // 엣지에 수직
            Vector3 toCenter = Vector3.ProjectOnPlane(e.TriangleCenter - (a + b) * 0.5f, Vector3.up);
            if (Vector3.Dot(normal, toCenter) < 0f)
                normal = -normal;

            normals[i] = normal;
            valid[i] = true;
            AddAdjacency(e.FirstIndex, i);
            AddAdjacency(e.SecondIndex, i);
        }

        List<BoundaryChain> chains = new List<BoundaryChain>();

        for (int i = 0; i < edges.Count; i++)
        {
            if (!valid[i] || used[i])
                continue;

            used[i] = true;
            BoundaryEdge seed = edges[i];
            Vector3 seedDir = Vector3.ProjectOnPlane(vertices[seed.SecondIndex] - vertices[seed.FirstIndex], Vector3.up).normalized;

            List<int> forwardVertices = new List<int>();
            List<Vector3> forwardNormals = new List<Vector3>();
            List<int> backwardVertices = new List<int>();
            List<Vector3> backwardNormals = new List<Vector3>();

            Extend(seed.SecondIndex, seedDir, normals[i], forwardVertices, forwardNormals);
            Extend(seed.FirstIndex, -seedDir, normals[i], backwardVertices, backwardNormals);

            BoundaryChain chain = new BoundaryChain();
            for (int k = backwardVertices.Count - 1; k >= 0; k--)
            {
                chain.Points.Add(vertices[backwardVertices[k]]);
                chain.Normals.Add(backwardNormals[k]);
            }

            chain.Points.Add(vertices[seed.FirstIndex]);
            chain.Normals.Add(normals[i]);
            chain.Points.Add(vertices[seed.SecondIndex]);

            for (int k = 0; k < forwardVertices.Count; k++)
            {
                chain.Points.Add(vertices[forwardVertices[k]]);
                chain.Normals.Add(forwardNormals[k]);
            }

            // backward 쪽 법선은 "이 점으로 들어오는 세그먼트" 기준이라 순서를 맞춰야 함
            chains.Add(FixChainNormals(chain, normals[i], backwardNormals, forwardNormals));
        }

        return chains;

        void AddAdjacency(int vertex, int edgeIndex)
        {
            if (!adjacency.TryGetValue(vertex, out List<int> list))
            {
                list = new List<int>();
                adjacency.Add(vertex, list);
            }

            list.Add(edgeIndex);
        }

        void Extend(int vertex, Vector3 direction, Vector3 normal, List<int> outVertices, List<Vector3> outNormals)
        {
            while (true)
            {
                int best = -1;
                int bestOther = -1;
                float bestDot = minDirectionDot;
                Vector3 bestDir = default;

                foreach (int ei in adjacency[vertex])
                {
                    if (used[ei])
                        continue;

                    BoundaryEdge e = edges[ei];
                    int other = e.FirstIndex == vertex ? e.SecondIndex : e.FirstIndex;
                    Vector3 step = vertices[other] - vertices[vertex];
                    if (Mathf.Abs(step.y) > maxEdgeHeight)
                        continue;

                    Vector3 dir = Vector3.ProjectOnPlane(step, Vector3.up).normalized;
                    float dot = Vector3.Dot(dir, direction);
                    if (dot < bestDot || Vector3.Dot(normals[ei], normal) < minNormalDot)
                        continue;

                    best = ei;
                    bestOther = other;
                    bestDot = dot;
                    bestDir = dir;
                }

                if (best < 0)
                    return;

                used[best] = true;
                outVertices.Add(bestOther);
                outNormals.Add(normals[best]);
                vertex = bestOther;
                direction = bestDir;
                normal = normals[best];
            }
        }
    }

    private static BoundaryChain FixChainNormals(
        BoundaryChain chain,
        Vector3 seedNormal,
        List<Vector3> backwardNormals,
        List<Vector3> forwardNormals)
    {
        // Normals 순서: [backward 역순][seed][forward] — 세그먼트 수와 일치
        chain.Normals.Clear();
        for (int k = backwardNormals.Count - 1; k >= 0; k--)
            chain.Normals.Add(backwardNormals[k]);
        chain.Normals.Add(seedNormal);
        chain.Normals.AddRange(forwardNormals);
        return chain;
    }

    private List<JumpCandidate> BuildCandidates(List<BoundaryChain> chains)
    {
        List<JumpCandidate> candidates = new List<JumpCandidate>();

        foreach (BoundaryChain chain in chains)
        {
            List<Vector3> points = chain.Points;
            if (points.Count < 2)
                continue;

            float[] cumulative = new float[points.Count];
            for (int i = 1; i < points.Count; i++)
            {
                cumulative[i] = cumulative[i - 1] +
                                Vector3.ProjectOnPlane(points[i] - points[i - 1], Vector3.up).magnitude;
            }

            float usableLength = cumulative[points.Count - 1] - edgePadding * 2f;
            if (usableLength <= 0.01f)
                continue;

            // 체인 전체를 균등 분할 → 간격/폭 일정
            int count = Mathf.Max(1, Mathf.CeilToInt(usableLength / linkSpacing));
            float step = usableLength / count;
            int segment = 0;

            for (int i = 0; i < count; i++)
            {
                float distance = edgePadding + step * (i + 0.5f);
                while (segment < points.Count - 2 && cumulative[segment + 1] < distance)
                    segment++;

                float t = Mathf.InverseLerp(cumulative[segment], cumulative[segment + 1], distance);
                Vector3 edgePoint = Vector3.Lerp(points[segment], points[segment + 1], t);
                Vector3 inward = chain.Normals[segment];

                Vector3 upper = edgePoint + inward * landingOffset + Vector3.up * 0.05f;
                Vector3 lowerProbe = edgePoint - inward * startOffset;

                if (!TryFindLowerFloor(lowerProbe, upper.y, out Vector3 lower))
                    continue;

                float heightDifference = upper.y - lower.y;
                if (heightDifference <= stepHeight || heightDifference > maxJumpHeight)
                    continue;

                if (!TryProjectToNavMeshHeight(lower, 0.2f, out Vector3 alignedLower) ||
                    !TryProjectToNavMeshHeight(upper, landingOffset + 0.1f, out Vector3 alignedUpper))
                    continue;

                candidates.Add(new JumpCandidate(alignedLower, alignedUpper, step));
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

    private static bool TryProjectToNavMeshHeight(Vector3 point, float sampleRadius, out Vector3 alignedPoint)
    {
        float radius = Mathf.Max(0.2f, sampleRadius);
        if (NavMesh.SamplePosition(point, out NavMeshHit hit, radius, NavMesh.AllAreas))
        {
            Vector2 horizontalOffset = new Vector2(hit.position.x - point.x, hit.position.z - point.z);
            float maxHorizontalOffset = Mathf.Min(0.15f, radius * 0.5f);
            if (horizontalOffset.sqrMagnitude <= maxHorizontalOffset * maxHorizontalOffset)
            {
                alignedPoint = new Vector3(point.x, hit.position.y, point.z);
                return true;
            }
        }

        alignedPoint = default;
        return false;
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
            PlaceLinkTransform(linkObject.transform, candidate.Start, candidate.End);

            NavMeshLink link = linkObject.AddComponent<NavMeshLink>();
            link.agentTypeID = agentTypeId;
            link.area = area;
            link.startPoint = linkObject.transform.InverseTransformPoint(candidate.Start);
            link.endPoint = linkObject.transform.InverseTransformPoint(candidate.End);
            link.width = candidate.Width;
            link.bidirectional = bidirectional;
            link.costModifier = linkCost > 0f ? Mathf.Max(1f, linkCost) : -1f;
            link.autoUpdate = false;

            linkObject.AddComponent<JumpUpLinkMarker>();
            generated++;
        }

        return generated;
    }

    private static void PlaceLinkTransform(Transform linkTransform, Vector3 start, Vector3 end)
    {
        linkTransform.position = (start + end) * 0.5f;

        Vector3 horizontalDirection = Vector3.ProjectOnPlane(end - start, Vector3.up);
        if (horizontalDirection.sqrMagnitude > 0.0001f)
            linkTransform.rotation = Quaternion.LookRotation(horizontalDirection.normalized, Vector3.up);
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
        public readonly float Width;

        public JumpCandidate(Vector3 start, Vector3 end, float width)
        {
            Start = start;
            End = end;
            Width = width;
        }
    }
}
#endif
