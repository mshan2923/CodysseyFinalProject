using System;
using Unity.Behavior;
using UnityEngine;

[Serializable]
[NodeDescription(name: "LLM Chat", story: "Send [Message] to the local LLM -> [Reslut]", category: "Action/LLM", id: "69bb9246f09d5128ac54a5c068f7b509")]
public class LlmChatAction : Unity.Behavior.Action
{
    [SerializeReference]
    public BlackboardVariable<string> systemMessage;

    [SerializeReference]
    public BlackboardVariable<string> Message;

    [SerializeReference]
    public BlackboardVariable<string> Reslut;


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

        LLMService.Instance.Chat(
            systemMessage.Value,
            Message.Value,

            response =>
            {
                Debug.Log(
                    $"[LLMChat] Response Received\n" +
                    $"Response: [{response}]");

                Reslut.Value = response;

                Debug.Log(
                    $"[LLMChat] Result Saved\n" +
                    $"Result: [{Reslut.Value}]");

                isSuccess = true;
                isCompleted = true;
            },

            error =>
            {
                Debug.LogError(
                    $"[LLMChat] Error: {error}");

                isSuccess = false;
                isCompleted = true;
            });

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

}
