using UnityEngine;

[CreateAssetMenu(
    fileName = "LLMPrompt",
    menuName = "SensingWorld/LLM Prompt"
)]
public class LLMPrompt : ScriptableObject
{
    [TextArea(15, 40)]
    public string SystemPrompt;

    [TextArea(10, 30)]
    public string NPCPrompt;
}