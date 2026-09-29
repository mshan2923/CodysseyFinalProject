using UnityEngine;
using UnityEngine.AI;

public class DirectLocation : MonoBehaviour
{
    public NavMeshAgent navMeshAgent;
    public Animator animator;
    public LayerMask layerMask;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit, float.MaxValue, layerMask))
            {
                navMeshAgent.SetDestination(hit.point);
            }
        }

        // animator.SetFloat("X", navMeshAgent.velocity.x);
        animator.SetFloat("SpeedMagnitude", navMeshAgent.velocity.magnitude);
    }
}