using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class LLMService : MonoBehaviour
{
    private enum Provider
    {
        Ollama,
        OpenAI,
        Anthropic
    }

    public static LLMService Instance { get; private set; }

    [Header("Provider")]
    [SerializeField]
    private Provider provider = Provider.Ollama;

    [SerializeField]
    private Provider fallbackProvider = Provider.Ollama;

    [Header("Ollama")]
    [SerializeField]
    private string ollamaUrl = "http://localhost:11434/api/chat";

    [SerializeField]
    private string model = "codyssey-cat-q4-test1024:latest";

    [SerializeField]
    private string fallbackModel = "gemma4:e2b";

    [Header("OpenAI / GPT")]
    [SerializeField]
    private string openAiUrl = "https://api.openai.com/v1/chat/completions";

    private string openAiApiKey;

    [SerializeField]
    private string openAiModel = "gpt-4o-mini";

    [Header("Anthropic / Claude")]
    [SerializeField]
    private string anthropicUrl = "https://api.anthropic.com/v1/messages";

    private string anthropicApiKey;

    [SerializeField]
    private string anthropicModel = "claude-3-5-haiku-latest";

    [SerializeField]
    private string anthropicVersion = "2023-06-01";

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
        LoadApiKeysFromDotEnv();
    }

    private void LoadApiKeysFromDotEnv()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL 빌드에는 프로젝트 루트의 .env가 존재하지 않으며,
        // 브라우저 파일 시스템을 직접 읽을 수도 없습니다.
        return;
#else
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string envPath = Path.Combine(projectRoot, ".env");
        if (!File.Exists(envPath))
        {
            Debug.LogWarning($"[LLMService] .env 파일을 찾을 수 없습니다: {envPath}");
            return;
        }

        Dictionary<string, string> values = new Dictionary<string, string>();
        foreach (string rawLine in File.ReadAllLines(envPath))
        {
            string line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                continue;

            int separator = line.IndexOf('=');
            if (separator <= 0)
                continue;

            string key = line.Substring(0, separator).Trim();
            string value = line.Substring(separator + 1).Trim().Trim('"');
            values[key] = value;
        }

        if (string.IsNullOrWhiteSpace(openAiApiKey) && values.TryGetValue("OPENAI_API_KEY", out string openAiKey))
            openAiApiKey = openAiKey;
        if (string.IsNullOrWhiteSpace(anthropicApiKey) && values.TryGetValue("ANTHROPIC_API_KEY", out string anthropicKey))
            anthropicApiKey = anthropicKey;
#endif
    }

    public void ReloadApiKeysFromDotEnv()
    {
        openAiApiKey = null;
        anthropicApiKey = null;
        LoadApiKeysFromDotEnv();
        Debug.Log("[LLMService] .env에서 API 키를 다시 읽었습니다.");
    }

    // WebGL 데모에서 사용자가 입력한 키를 실행 중 메모리에만 보관합니다.
    // PlayerPrefs, .env, 로그에는 저장하지 않습니다.
    public void SetApiKeyForProvider(string providerName, string apiKey)
    {
        string trimmedKey = apiKey == null ? string.Empty : apiKey.Trim();

        if (string.Equals(providerName, "OpenAI", StringComparison.OrdinalIgnoreCase))
            openAiApiKey = trimmedKey;
        else if (string.Equals(providerName, "Anthropic", StringComparison.OrdinalIgnoreCase))
            anthropicApiKey = trimmedKey;
        else
            return;

        Debug.Log($"[LLMService] {providerName} 키를 현재 실행에만 적용했습니다.");
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
            provider, GetModel(provider, false), systemPrompt, userMessage, onSuccess,
            message => error = message, temperatureOverride);

        if (error == null)
        {
            yield break;
        }

        if (fallbackProvider != provider || GetModel(fallbackProvider, true) != GetModel(provider, false))
        {
            Debug.LogWarning(
                $"[LLMService] {provider}/{GetModel(provider, false)} 실패. " +
                $"{fallbackProvider}/{GetModel(fallbackProvider, true)}로 재시도합니다.\n{error}");

            string primaryError = error;
            error = null;
            yield return SendChatRequest(
                fallbackProvider, GetModel(fallbackProvider, true), systemPrompt, userMessage, onSuccess,
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
        Provider requestProvider,
        string requestedModel,
        string systemPrompt,
        string userMessage,
        Action<string> onSuccess,
        Action<string> onError,
        float? temperatureOverride)
    {
        string url;
        string json;

        if (requestProvider == Provider.Ollama)
        {
            OllamaChatRequest requestData = new OllamaChatRequest
            {
                model = requestedModel,
                stream = false,
                think = false,
                options = new OllamaOptions { temperature = temperatureOverride ?? temperature },
                messages = new[]
                {
                    new OllamaMessage { role = "system", content = systemPrompt },
                    new OllamaMessage { role = "user", content = userMessage }
                }
            };
            url = ollamaUrl;
            json = JsonUtility.ToJson(requestData);
        }
        else if (requestProvider == Provider.OpenAI)
        {
            OpenAiChatRequest requestData = new OpenAiChatRequest
            {
                model = requestedModel,
                temperature = temperatureOverride ?? temperature,
                messages = new[]
                {
                    new OllamaMessage { role = "system", content = systemPrompt },
                    new OllamaMessage { role = "user", content = userMessage }
                }
            };
            url = openAiUrl;
            json = JsonUtility.ToJson(requestData);
        }
        else
        {
            AnthropicMessageRequest requestData = new AnthropicMessageRequest
            {
                model = requestedModel,
                max_tokens = 4096,
                temperature = temperatureOverride ?? temperature,
                system = systemPrompt,
                messages = new[] { new OllamaMessage { role = "user", content = userMessage } }
            };
            url = anthropicUrl;
            json = JsonUtility.ToJson(requestData);
        }

        using UnityWebRequest request =
            new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);

        request.uploadHandler =
            new UploadHandlerRaw(
                Encoding.UTF8.GetBytes(json));

        request.downloadHandler =
            new DownloadHandlerBuffer();

        request.timeout = Mathf.Max(1, timeoutSeconds);

        request.SetRequestHeader("Content-Type", "application/json");
        if (requestProvider == Provider.OpenAI)
            request.SetRequestHeader("Authorization", "Bearer " + openAiApiKey);
        else if (requestProvider == Provider.Anthropic)
        {
            request.SetRequestHeader("x-api-key", anthropicApiKey);
            request.SetRequestHeader("anthropic-version", anthropicVersion);
        }

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            string error =
                $"{requestProvider} Error\n" +
                $"Code: {request.responseCode}\n" +
                $"Error: {request.error}";

            onError?.Invoke(error);
            yield break;
        }

        string responseText = request.downloadHandler.text;

        try
        {
            string content = ExtractContent(requestProvider, responseText);
            if (string.IsNullOrWhiteSpace(content))
                throw new Exception("응답에 내용이 없습니다.");
            onSuccess?.Invoke(content);
            yield break;
        }
        catch (Exception e)
        {
            string error =
                $"응답 JSON 파싱 실패\n{e.Message}\n\n" +
                responseText;

            onError?.Invoke(error);
            yield break;
        }

    }

    private string GetModel(Provider selectedProvider, bool isFallback) =>
        selectedProvider == Provider.Ollama && isFallback ? fallbackModel :
        selectedProvider == Provider.Ollama ? model :
        selectedProvider == Provider.OpenAI ? openAiModel : anthropicModel;

    public void SetModel(string modelName)
    {
        if (Provider.TryParse(modelName, true, out Provider parsedProvider))
        {
            provider = parsedProvider;
            Debug.Log($"[LLMService] 모델이 {provider}로 설정되었습니다.");
        }
        else
        {
            Debug.LogWarning($"[LLMService] 알 수 없는 모델 이름: {modelName}");
        }
    }

    private string ExtractContent(Provider selectedProvider, string json)
    {
        if (selectedProvider == Provider.Ollama)
            return JsonUtility.FromJson<OllamaChatResponse>(json)?.message?.content;
        if (selectedProvider == Provider.OpenAI)
        {
            OpenAiChatResponse openAiResponse =
                JsonUtility.FromJson<OpenAiChatResponse>(json);
            return openAiResponse != null &&
                   openAiResponse.choices != null &&
                   openAiResponse.choices.Length > 0
                ? openAiResponse.choices[0].message?.content : null;
        }
        AnthropicResponse anthropicResponse =
            JsonUtility.FromJson<AnthropicResponse>(json);
        return anthropicResponse != null &&
               anthropicResponse.content != null &&
               anthropicResponse.content.Length > 0
            ? anthropicResponse.content[0].text : null;
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
    private class OpenAiChatRequest
    {
        public string model;
        public float temperature;
        public OllamaMessage[] messages;
    }

    [Serializable]
    private class OpenAiChatResponse { public OpenAiChoice[] choices; }
    [Serializable]
    private class OpenAiChoice { public OllamaMessage message; }

    [Serializable]
    private class AnthropicMessageRequest
    {
        public string model;
        public int max_tokens;
        public float temperature;
        public string system;
        public OllamaMessage[] messages;
    }

    [Serializable]
    private class AnthropicResponse { public AnthropicContent[] content; }
    [Serializable]
    private class AnthropicContent { public string type; public string text; }

    [Serializable]
    private class OllamaMessage
    {
        public string role;
        public string content;
    }
}
