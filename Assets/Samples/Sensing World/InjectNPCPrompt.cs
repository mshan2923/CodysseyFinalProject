using Unity.Behavior;
using UnityEngine;

public class InjectNPCPrompt : MonoBehaviour
{

    [SerializeField]
    private BehaviorGraphAgent agent;
    public LLMPrompt lLMPrompt;
    void Start()
    {

        SetLLMContext(lLMPrompt.SystemPrompt, lLMPrompt.NPCPrompt);
    }

    public void SetLLMContext(string systemPrompt, string npcPrompt)
    {
        agent.SetVariableValue("SystemPrompt", systemPrompt);
        agent.SetVariableValue("NPCPrompt", npcPrompt);
    }
}
