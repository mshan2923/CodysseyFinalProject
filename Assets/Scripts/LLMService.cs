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
    private string model = "gemma4:e2b";

    public float temperature = 0.2f;

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
        Action<string> onError = null)
    {
        StartCoroutine(
            ChatCoroutine(
                systemPrompt,
                userMessage,
                onSuccess,
                onError));
    }

    private IEnumerator ChatCoroutine(
        string systemPrompt,
        string userMessage,
        Action<string> onSuccess,
        Action<string> onError)
    {
        OllamaChatRequest requestData = new OllamaChatRequest
        {
            model = model,
            stream = false,
            options = new OllamaOptions
            {
                temperature = temperature
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

            Debug.LogError(
                $"[LLMService]\n{error}");

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

            Debug.LogError(
                $"[LLMService]\n{error}");

            onError?.Invoke(error);
            yield break;
        }

        if (response == null ||
            response.message == null)
        {
            string error =
                "Ollama 응답에 message가 없습니다.\n\n" +
                responseJson;

            Debug.LogError(
                $"[LLMService]\n{error}");

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
