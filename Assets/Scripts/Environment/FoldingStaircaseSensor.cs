using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class FoldingStaircaseSensor : MonoBehaviour
{
    public enum SensorRole { Approach, Occupancy }

    [SerializeField] private FoldingStaircaseController controller;
    [SerializeField] private SensorRole role;

    public void Configure(FoldingStaircaseController configuredController, SensorRole configuredRole)
    {
        controller = configuredController;
        role = configuredRole;
    }

    private void Reset() => GetComponent<BoxCollider>().isTrigger = true;

    private void OnTriggerEnter(Collider other)
    {
        Transform actor = ResolveCatActor(other);
        if (actor == null || controller == null) return;
        if (role == SensorRole.Approach) controller.RequestOpen();
        else controller.RegisterOccupant(actor);
    }

    private void OnTriggerStay(Collider other)
    {
        if (role == SensorRole.Approach && controller != null && ResolveCatActor(other) != null)
            controller.RequestOpen();
    }

    private void OnTriggerExit(Collider other)
    {
        if (role != SensorRole.Occupancy || controller == null) return;
        Transform actor = ResolveCatActor(other);
        if (actor != null) controller.UnregisterOccupant(actor);
    }

    private static Transform ResolveCatActor(Collider other)
    {
        if (other == null) return null;
        CatController cat = other.GetComponentInParent<CatController>();
        return cat != null ? cat.transform : null;
    }
}
