using SensingWorld;
using Unity.Behavior;
using UnityEngine;

public class LlmTargetResolver : MonoBehaviour
{
    [SerializeField]
    private BehaviorGraphAgent agent;
        public string Variable = "Result";

    [SerializeField]
    private GameObject targetObject;
    public GameObject TargetObject { get => targetObject; private set => targetObject = value; }

    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        agent.GetVariable<string>(Variable, out var variable);

        if (LlmResponseParser.TryParse(variable, out var result))
        {

            if (result.HasTarget)
            {
                Debug.DrawLine(transform.position, result.TargetObject.transform.position, Color.red);
                TargetObject = result.TargetObject;
            }
        }
    }
}
