#if UNITY_EDITOR
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Generates persistent upward NavMeshLinks after the base NavMesh has been baked.
/// This is intentionally an editor tool: runtime agents only consume the generated links.
/// </summary>
public sealed class JumpUpLinkBakeProcessor : EditorWindow
{
    private const string GeneratedRootName = "_GeneratedJumpUpLinks";

    [SerializeField, Min(0.1f)] private float sampleSpacing = 0.75f;
    [SerializeField, Min(0.01f)] private float stepHeight = 0.45f;
    [SerializeField, Min(0.01f)] private float maxJumpHeight = 2.5f;
    [SerializeField, Min(0.1f)] private float jumpDistance = 1.5f;
    [SerializeField, Min(0f)] private float linkCost = 3f;
    [SerializeField] private NavMeshSurface navMeshSurface;
    [SerializeField] private int area;
    [SerializeField] private LayerMask groundMask = ~0;
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
            "Bake the base NavMesh first. This tool scans walkable floor samples, finds higher nearby floors, and stores Jump-Up NavMeshLinks in the scene.",
            MessageType.Info);

        sampleSpacing = EditorGUILayout.FloatField("Sample Spacing", sampleSpacing);
        stepHeight = EditorGUILayout.FloatField("Step Height", stepHeight);
        maxJumpHeight = EditorGUILayout.FloatField("Max Jump Height", maxJumpHeight);
        jumpDistance = EditorGUILayout.FloatField("Jump Distance", jumpDistance);
        linkCost = EditorGUILayout.FloatField("Link Cost (0 = default)", linkCost);
        navMeshSurface = (NavMeshSurface)EditorGUILayout.ObjectField(
            "NavMesh Surface",
            navMeshSurface,
            typeof(NavMeshSurface),
            true);

        NavMeshSurface detectedSurface = ResolveSurface();
        if (detectedSurface != null)
        {
            EditorGUILayout.HelpBox(
                $"Using Agent Type ID {detectedSurface.agentTypeID} from {detectedSurface.name}.",
                MessageType.None);
        }
        else
        {
            EditorGUILayout.HelpBox("Assign a NavMeshSurface or enable one in the active scene.", MessageType.Warning);
        }

        area = EditorGUILayout.IntField("Area", area);
        groundMask = EditorGUILayout.MaskField("Ground Layers", groundMask, UnityEditorInternal.InternalEditorUtility.layers);
        bidirectional = EditorGUILayout.Toggle("Bidirectional", bidirectional);
        clearPreviousLinks = EditorGUILayout.Toggle("Clear Previous Generated Links", clearPreviousLinks);

        using (new EditorGUI.DisabledScope(!Application.isEditor))
        {
            if (GUILayout.Button("Generate Jump-Up Links"))
                Generate();
        }
    }

    private void Generate()
    {
        if (sampleSpacing <= 0f || jumpDistance <= 0f || maxJumpHeight <= stepHeight)
        {
            EditorUtility.DisplayDialog("Jump-Up Links", "Check the spacing and jump height values.", "OK");
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

        Bounds bounds;
        if (!TryGetGroundBounds(out bounds))
        {
            EditorUtility.DisplayDialog("Jump-Up Links", "No enabled ground colliders were found for the selected layers.", "OK");
            return;
        }

        GameObject root = GetOrCreateGeneratedRoot(scene);
        if (clearPreviousLinks)
            ClearGeneratedLinks(root);

        int agentTypeId = surface.agentTypeID;
        Dictionary<Vector2Int, FloorSample> samples = SampleFloors(bounds);
        HashSet<string> created = new HashSet<string>();
        int generated = 0;

        foreach (KeyValuePair<Vector2Int, FloorSample> pair in samples)
        {
            FloorSample lower = pair.Value;

            foreach (Vector2 direction in Directions)
            {
                Vector3 probePosition = lower.position + new Vector3(direction.x, 0f, direction.y) * jumpDistance;
                Vector2Int probeKey = ToKey(probePosition, bounds.min);

                if (!samples.TryGetValue(probeKey, out FloorSample upper))
                    continue;

                float heightDifference = upper.position.y - lower.position.y;
                if (heightDifference <= stepHeight || heightDifference > maxJumpHeight)
                    continue;

                if (!IsValidJump(lower.position, upper.position, direction, heightDifference))
                    continue;

                string key = BuildLinkKey(lower.position, upper.position);
                if (!created.Add(key))
                    continue;

                CreateLink(root.transform, lower.position, upper.position, agentTypeId);
                generated++;
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Jump-Up Links", $"Generated {generated} jump-up link(s).", "OK");
    }

    private static readonly Vector2[] Directions =
    {
        Vector2.right,
        Vector2.left,
        Vector2.up,
        Vector2.down
    };

    private Dictionary<Vector2Int, FloorSample> SampleFloors(Bounds bounds)
    {
        Dictionary<Vector2Int, FloorSample> result = new Dictionary<Vector2Int, FloorSample>();
        int xCount = Mathf.CeilToInt(bounds.size.x / sampleSpacing);
        int zCount = Mathf.CeilToInt(bounds.size.z / sampleSpacing);
        float rayStart = bounds.max.y + maxJumpHeight + 2f;
        float rayDistance = bounds.size.y + maxJumpHeight + 4f;

        for (int x = 0; x <= xCount; x++)
        {
            for (int z = 0; z <= zCount; z++)
            {
                Vector3 origin = new Vector3(
                    bounds.min.x + x * sampleSpacing,
                    rayStart,
                    bounds.min.z + z * sampleSpacing);

                if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, rayDistance, groundMask, QueryTriggerInteraction.Ignore))
                    continue;

                Vector3 navmeshPosition = hit.point + Vector3.up * 0.05f;
                if (!NavMesh.SamplePosition(navmeshPosition, out NavMeshHit navHit, sampleSpacing * 0.75f, NavMesh.AllAreas))
                    continue;

                result[new Vector2Int(x, z)] = new FloorSample(navHit.position);
            }
        }

        return result;
    }

    private bool IsValidJump(Vector3 lower, Vector3 upper, Vector2 direction, float heightDifference)
    {
        Vector3 horizontal = new Vector3(direction.x, 0f, direction.y).normalized;
        Vector3 start = lower + Vector3.up * 0.1f;
        Vector3 end = upper + Vector3.up * 0.1f;
        float radius = 0.25f;
        float capsuleHeight = 1.8f;

        if (Physics.CheckCapsule(
                start + Vector3.up * radius,
                start + Vector3.up * Mathf.Max(radius, capsuleHeight - radius),
                radius,
                groundMask,
                QueryTriggerInteraction.Ignore))
            return false;

        if (Physics.CheckCapsule(
                end + Vector3.up * radius,
                end + Vector3.up * Mathf.Max(radius, capsuleHeight - radius),
                radius,
                groundMask,
                QueryTriggerInteraction.Ignore))
            return false;

        Vector3 trajectoryStart = lower + Vector3.up * 0.2f;
        Vector3 trajectoryEnd = upper + Vector3.up * 0.2f;
        Vector3 apex = Vector3.Lerp(trajectoryStart, trajectoryEnd, 0.5f) + Vector3.up * Mathf.Max(0.5f, heightDifference * 0.5f);

        if (Physics.Linecast(trajectoryStart, apex, groundMask, QueryTriggerInteraction.Ignore))
            return false;

        if (Physics.Linecast(apex, trajectoryEnd, groundMask, QueryTriggerInteraction.Ignore))
            return false;

        return true;
    }

    private void CreateLink(Transform root, Vector3 start, Vector3 end, int agentTypeId)
    {
        GameObject linkObject = new GameObject("JumpUpLink");
        Undo.RegisterCreatedObjectUndo(linkObject, "Create Jump-Up Link");
        linkObject.transform.SetParent(root, false);

        NavMeshLink link = linkObject.AddComponent<NavMeshLink>();
        link.agentTypeID = agentTypeId;
        link.area = area;
        link.startPoint = start - linkObject.transform.position;
        link.endPoint = end - linkObject.transform.position;
        link.bidirectional = bidirectional;
        // Unity requires a positive override to be at least 1. A value of 0 means
        // use the normal area cost instead of forcing an extra link cost.
        link.costModifier = linkCost > 0f ? Mathf.Max(1f, linkCost) : -1f;
        link.autoUpdate = false;

        linkObject.AddComponent<JumpUpLinkMarker>();
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

    private bool TryGetGroundBounds(out Bounds bounds)
    {
        Collider[] colliders = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None);
        bool hasBounds = false;
        bounds = default;

        foreach (Collider collider in colliders)
        {
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                continue;

            if ((groundMask.value & (1 << collider.gameObject.layer)) == 0)
                continue;

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return hasBounds;
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

    private GameObject GetOrCreateGeneratedRoot(Scene scene)
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

    private Vector2Int ToKey(Vector3 position, Vector3 boundsMin)
    {
        return new Vector2Int(
            Mathf.RoundToInt((position.x - boundsMin.x) / sampleSpacing),
            Mathf.RoundToInt((position.z - boundsMin.z) / sampleSpacing));
    }

    private static string BuildLinkKey(Vector3 start, Vector3 end)
    {
        Vector3 roundedStart = new Vector3(
            Mathf.Round(start.x * 10f) / 10f,
            Mathf.Round(start.y * 10f) / 10f,
            Mathf.Round(start.z * 10f) / 10f);
        Vector3 roundedEnd = new Vector3(
            Mathf.Round(end.x * 10f) / 10f,
            Mathf.Round(end.y * 10f) / 10f,
            Mathf.Round(end.z * 10f) / 10f);
        return $"{roundedStart}->{roundedEnd}";
    }

    private readonly struct FloorSample
    {
        public readonly Vector3 position;

        public FloorSample(Vector3 position)
        {
            this.position = position;
        }
    }
}
#endif
