using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class LLMService : MonoBehaviour
{
    public static LLMService Instance { get; private set; }

    [Header("Ollama")]
    [SerializeField]
    private string ollamaUrl = "http://localhost:11434/api/chat";

    [SerializeField]
    private string model = "codyssey-cat-q4-test1024:latest";

    [SerializeField]
    private string fallbackModel = "gemma4:e2b";

    public float temperature = 0.2f;

    [SerializeField]
    [Min(1)]
    private int timeoutSeconds = 60;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void Chat(
        string systemPrompt,
        string userMessage,
        Action<string> onSuccess,
        Action<string> onError = null,
        float? temperatureOverride = null)
    {
        StartCoroutine(
            ChatCoroutine(
                systemPrompt,
                userMessage,
                onSuccess,
                onError,
                temperatureOverride));
    }

    private IEnumerator ChatCoroutine(
        string systemPrompt,
        string userMessage,
        Action<string> onSuccess,
        Action<string> onError,
        float? temperatureOverride)
    {
        string error = null;
        yield return SendChatRequest(
            model, systemPrompt, userMessage, onSuccess,
            message => error = message, temperatureOverride);

        if (error == null)
        {
            yield break;
        }

        if (!string.IsNullOrWhiteSpace(fallbackModel) && fallbackModel != model)
        {
            Debug.LogWarning(
                $"[LLMService] 모델 '{model}' 실패. '{fallbackModel}'로 재시도합니다.\n{error}");

            string primaryError = error;
            error = null;
            yield return SendChatRequest(
                fallbackModel, systemPrompt, userMessage, onSuccess,
                message => error = message, temperatureOverride);

            if (error == null)
            {
                yield break;
            }

            error = $"기본 모델 실패:\n{primaryError}\n\n대체 모델 실패:\n{error}";
        }

        Debug.LogError($"[LLMService]\n{error}");
        onError?.Invoke(error);
    }

    private IEnumerator SendChatRequest(
        string requestedModel,
        string systemPrompt,
        string userMessage,
        Action<string> onSuccess,
        Action<string> onError,
        float? temperatureOverride)
    {
        OllamaChatRequest requestData = new OllamaChatRequest
        {
            model = requestedModel,
            stream = false,
            think = false,
            options = new OllamaOptions
            {
                temperature = temperatureOverride ?? temperature
            },
            messages = new[]
            {
                new OllamaMessage
                {
                    role = "system",
                    content = systemPrompt
                },

                new OllamaMessage
                {
                    role = "user",
                    content = userMessage
                }
            }
        };

        string json = JsonUtility.ToJson(requestData);

        using UnityWebRequest request =
            new UnityWebRequest(
                ollamaUrl,
                UnityWebRequest.kHttpVerbPOST);

        request.uploadHandler =
            new UploadHandlerRaw(
                Encoding.UTF8.GetBytes(json));

        request.downloadHandler =
            new DownloadHandlerBuffer();

        request.timeout = Mathf.Max(1, timeoutSeconds);

        request.SetRequestHeader(
            "Content-Type",
            "application/json");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            string error =
                $"Ollama Error\n" +
                $"Code: {request.responseCode}\n" +
                $"Error: {request.error}";

            onError?.Invoke(error);
            yield break;
        }

        string responseJson =
            request.downloadHandler.text;

        OllamaChatResponse response;

        try
        {
            response =
                JsonUtility.FromJson<OllamaChatResponse>(
                    responseJson);
        }
        catch (Exception e)
        {
            string error =
                $"응답 JSON 파싱 실패\n{e.Message}\n\n" +
                responseJson;

            onError?.Invoke(error);
            yield break;
        }

        if (response == null ||
            response.message == null ||
            string.IsNullOrWhiteSpace(response.message.content))
        {
            string error =
                "Ollama 응답에 message 또는 내용이 없습니다.\n\n" +
                responseJson;

            onError?.Invoke(error);
            yield break;
        }

        onSuccess?.Invoke(
            response.message.content);
    }

    [Serializable]
    private class OllamaChatRequest
    {
        public string model;
        public bool stream;
        public bool think;
        public OllamaOptions options;
        public OllamaMessage[] messages;
    }

    [Serializable]
    private class OllamaOptions
    {
        public float temperature;
    }

    [Serializable]
    private class OllamaChatResponse
    {
        public OllamaMessage message;
    }

    [Serializable]
    private class OllamaMessage
    {
        public string role;
        public string content;
    }
}
