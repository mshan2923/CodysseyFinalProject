using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Unity.Behavior;
using UnityEngine;

[Serializable]
[NodeDescription(name: "LLM Chat", story: "Send [Message] to the local LLM -> [Reslut]", category: "Action/LLM", id: "69bb9246f09d5128ac54a5c068f7b509")]
public class LlmChatAction : Unity.Behavior.Action
{
    private static readonly Regex SelectedEntityPattern = new Regex(
        @"(?<name>[^,:;\r\n]+?)\s*(?:\(InstanceID:\s*|\()(?<id>-?\d+)\)",
        RegexOptions.Compiled);

    [SerializeReference]
    public BlackboardVariable<string> systemMessage;

    [SerializeReference]
    public BlackboardVariable<string> Message;

    [SerializeReference]
    public BlackboardVariable<string> Reslut;

    [SerializeReference]
    public BlackboardVariable<float> Temperature = new BlackboardVariable<float>(0.7f);

    [Tooltip("SelectedEntities와 InstanceID가 응답에 포함되어야 하는지 여부입니다.")]
    [SerializeReference]
    public BlackboardVariable<bool> requireSelectedEntities = new BlackboardVariable<bool>(true);


    [SerializeReference]
    public BlackboardVariable<string> Temp;

    private bool isCompleted;
    private bool isSuccess;

    protected override Status OnStart()
    {
        Debug.Log("[LLMChat] Start");

        if (LLMService.Instance == null)
        {
            Debug.LogError("[LLMChat] LLMService가 없습니다.");
            return Status.Failure;
        }


        if (Message == null)
        {
            Debug.LogError("[LLMChat] Message가 null입니다.");
            return Status.Failure;
        }

        

        isCompleted = false;
        isSuccess = false;

        Debug.Log(
            $"[LLMChat] Request\n" +
            $"System: {systemMessage.Value}\n" +
            $"Message: {Message.Value}");

        float? temperatureOverride = Temperature != null
            ? Mathf.Clamp(Temperature.Value, 0f, 2f)
            : null;

        LLMService.Instance.Chat(
            systemMessage.Value,
            Message.Value,

            response =>
            {
                Debug.Log(
                    $"[LLMChat] Response Received\n" +
                    $"Response: [{response}]");

                if (requireSelectedEntities.Value)
                    LogSelectedEntities(response);

                if (requireSelectedEntities.Value)
                {
                    string recognition = ExtractLabeledText(response, "Recognition:");

                    if (string.IsNullOrWhiteSpace(recognition))
                        Debug.LogWarning("[LLM Recognition] Recognition을 찾지 못했습니다.");

                    Reslut.Value = recognition;
                }else
                {
                    string dialogue = ExtractLabeledText(response, "Dialogue:");
                    Reslut.Value = string.IsNullOrWhiteSpace(dialogue)
                        ? response.Trim()
                        : dialogue;
                }

                Debug.Log(
                    $"[LLMChat] Result Saved\n" +
                    $"{Temp.Value} -- Result: [{Reslut.Value}]");

                isSuccess = true;
                isCompleted = true;
            },

            error =>
            {
                Debug.LogError(
                    $"[LLMChat] Error: {error}");

                isSuccess = false;
                isCompleted = true;
            },
            temperatureOverride: temperatureOverride);

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (!isCompleted)
            return Status.Running;

        return isSuccess
            ? Status.Success
            : Status.Failure;
    }

    private static void LogSelectedEntities(string response)
    {
        var selectedIds = new HashSet<int>();

        foreach (Match match in SelectedEntityPattern.Matches(response ?? string.Empty))
        {
            if (!int.TryParse(match.Groups["id"].Value, out int instanceId))
                continue;

            if (!selectedIds.Add(instanceId))
                continue;

            string entityName = match.Groups["name"].Value.Trim();
            Debug.Log($"[LLM Recognition] Target: {entityName} (InstanceID: {instanceId})");
        }

        if (selectedIds.Count == 0)
            Debug.LogWarning("[LLM Recognition] SelectedEntities에서 InstanceID를 찾지 못했습니다.");
        else
            Debug.Log($"[LLM Recognition] Selected InstanceIDs: {string.Join(", ", selectedIds)}");
    }

    private static string ExtractLabeledText(string response, string marker)
    {
        if (string.IsNullOrWhiteSpace(response))
            return string.Empty;

        int markerIndex = response.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
            return string.Empty;

        return response.Substring(markerIndex + marker.Length).Trim();
    }

}
