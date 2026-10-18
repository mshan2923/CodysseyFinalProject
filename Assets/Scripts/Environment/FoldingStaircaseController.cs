using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FoldingStaircaseController : MonoBehaviour
{
    public enum StairState { Stored, Opening, Open, Closing }

    [Header("Folding mechanism")]
    [SerializeField] private Transform upperPivot;
    [SerializeField] private Transform lowerPivot;
    [SerializeField] private Quaternion deployedUpperRotation = Quaternion.identity;
    [SerializeField] private Quaternion storedUpperRotation = Quaternion.identity;
    [SerializeField] private Quaternion deployedLowerRotation = Quaternion.identity;
    [SerializeField] private Quaternion storedLowerRotation = Quaternion.identity;

    [Header("Safety")]
    [SerializeField] private Collider[] traversalColliders;
    [SerializeField, Min(0.25f)] private float transitionSeconds = 1.4f;
    [SerializeField, Min(0.5f)] private float autoCloseDelay = 3.0f;
    [SerializeField] private bool startStored = true;
    [SerializeField] private AnimationCurve motionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private readonly Dictionary<int, int> occupantContacts = new();
    private float progress;
    private float lastOpenRequestTime;
    private StairState state;

    public StairState State => state;
    public bool HasOccupant => occupantContacts.Count > 0;
    public bool IsTraversalReady => state == StairState.Open && progress >= 0.999f;

    private void Awake()
    {
        progress = startStored ? 0f : 1f;
        state = startStored ? StairState.Stored : StairState.Open;
        ApplyPose(progress);
        SetTraversalEnabled(!startStored);
        lastOpenRequestTime = Time.time;
    }

    private void OnDisable() => occupantContacts.Clear();

    private void Update()
    {
        switch (state)
        {
            case StairState.Opening:
                progress = Mathf.MoveTowards(progress, 1f, Time.deltaTime / transitionSeconds);
                ApplyPose(progress);
                if (progress >= 0.999f)
                {
                    progress = 1f;
                    ApplyPose(progress);
                    SetTraversalEnabled(true);
                    state = StairState.Open;
                    lastOpenRequestTime = Time.time;
                }
                break;

            case StairState.Open:
                if (!HasOccupant && Time.time - lastOpenRequestTime >= autoCloseDelay)
                    BeginClosing();
                break;

            case StairState.Closing:
                if (HasOccupant)
                {
                    BeginOpening();
                    break;
                }

                progress = Mathf.MoveTowards(progress, 0f, Time.deltaTime / transitionSeconds);
                ApplyPose(progress);
                if (progress <= 0.001f)
                {
                    progress = 0f;
                    ApplyPose(progress);
                    state = StairState.Stored;
                }
                break;
        }
    }

    public void RequestOpen()
    {
        lastOpenRequestTime = Time.time;
        if (state == StairState.Stored || state == StairState.Closing)
            BeginOpening();
    }

    public void RegisterOccupant(Transform actor)
    {
        if (actor == null) return;
        int id = actor.GetInstanceID();
        occupantContacts.TryGetValue(id, out int count);
        occupantContacts[id] = count + 1;
        RequestOpen();
    }

    public void UnregisterOccupant(Transform actor)
    {
        if (actor == null) return;
        int id = actor.GetInstanceID();
        if (!occupantContacts.TryGetValue(id, out int count)) return;
        if (count <= 1) occupantContacts.Remove(id);
        else occupantContacts[id] = count - 1;
        lastOpenRequestTime = Time.time;
    }

    public void Configure(Transform configuredUpperPivot, Transform configuredLowerPivot,
        Quaternion configuredStoredUpperRotation, Quaternion configuredStoredLowerRotation,
        Collider[] configuredTraversalColliders, float configuredTransitionSeconds,
        float configuredAutoCloseDelay)
    {
        upperPivot = configuredUpperPivot;
        lowerPivot = configuredLowerPivot;
        deployedUpperRotation = Quaternion.identity;
        deployedLowerRotation = Quaternion.identity;
        storedUpperRotation = configuredStoredUpperRotation;
        storedLowerRotation = configuredStoredLowerRotation;
        traversalColliders = configuredTraversalColliders;
        transitionSeconds = Mathf.Max(0.25f, configuredTransitionSeconds);
        autoCloseDelay = Mathf.Max(0.5f, configuredAutoCloseDelay);
        startStored = true;
    }

    public bool ValidateConfiguration(out string report)
    {
        List<string> issues = new();
        if (upperPivot == null) issues.Add("upper pivot missing");
        if (lowerPivot == null) issues.Add("lower pivot missing");
        if (traversalColliders == null || traversalColliders.Length < 2) issues.Add("two traversal colliders required");
        if (transitionSeconds <= 0f) issues.Add("transition time must be positive");
        if (autoCloseDelay < 0.5f) issues.Add("auto-close delay is too short");
        report = issues.Count == 0 ? "configuration valid" : string.Join(", ", issues);
        return issues.Count == 0;
    }

    [ContextMenu("Preview/Deploy Immediately")]
    private void PreviewDeploy()
    {
        progress = 1f;
        state = StairState.Open;
        ApplyPose(progress);
        SetTraversalEnabled(true);
    }

    [ContextMenu("Preview/Store Immediately")]
    private void PreviewStore()
    {
        if (HasOccupant) return;
        progress = 0f;
        state = StairState.Stored;
        SetTraversalEnabled(false);
        ApplyPose(progress);
    }

    private void BeginOpening()
    {
        state = StairState.Opening;
        SetTraversalEnabled(false);
    }

    private void BeginClosing()
    {
        if (HasOccupant) return;
        state = StairState.Closing;
        SetTraversalEnabled(false);
    }

    private void ApplyPose(float linearProgress)
    {
        if (upperPivot == null || lowerPivot == null) return;
        float curvedProgress = motionCurve == null ? linearProgress : motionCurve.Evaluate(linearProgress);
        upperPivot.localRotation = Quaternion.Slerp(storedUpperRotation, deployedUpperRotation, curvedProgress);
        lowerPivot.localRotation = Quaternion.Slerp(storedLowerRotation, deployedLowerRotation, curvedProgress);
    }

    private void SetTraversalEnabled(bool enabled)
    {
        if (traversalColliders == null) return;
        foreach (Collider traversalCollider in traversalColliders)
            if (traversalCollider != null) traversalCollider.enabled = enabled;
    }

    private void OnValidate()
    {
        transitionSeconds = Mathf.Max(0.25f, transitionSeconds);
        autoCloseDelay = Mathf.Max(0.5f, autoCloseDelay);
    }
}
