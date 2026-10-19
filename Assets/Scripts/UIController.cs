using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    public LLMService llmServer;
    public GameObject UI;
    public TMP_Dropdown dropdownMenu;
    public TMP_FontAsset koreanFont;

    private GameObject apiKeyPanel;
    private TMP_InputField apiKeyInput;
    private TMP_Text apiKeyLabel;
    private TMP_Text apiKeyStatus;
    private string selectedProviderName;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Start()
    {
        dropdownMenu.onValueChanged.AddListener(OnDropdownValueChanged);
        CreateApiKeyPanel();

#if UNITY_WEBGL && !UNITY_EDITOR
        // 공개 WebGL에서는 방문자의 localhost Ollama를 기본 호출하지 않고
        // 사용자 키 입력이 필요한 OpenAI 경로를 먼저 보여줍니다.
        if (dropdownMenu.options.Count > 1)
            dropdownMenu.SetValueWithoutNotify(1);
#endif

        OnDropdownValueChanged(dropdownMenu.value);
    }

    // Update is called once per frame
    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current[Key.Tab].wasPressedThisFrame)
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                UI.SetActive(true);
            }else if (Cursor.lockState == CursorLockMode.None)
            {
                UI.SetActive(false);
            }   
        }


    }

    private void OnDropdownValueChanged(int index)
    {
        string selectedOption = dropdownMenu.options[index].text;
        selectedProviderName = selectedOption;

        if (selectedOption == "OpenAI" || selectedOption == "Anthropic")
            ShowApiKeyPanel(selectedOption);
        else
            HideApiKeyPanel();

        llmServer.SetModel(selectedOption);
    }

    private void CreateApiKeyPanel()
    {
        if (UI == null)
        {
            Debug.LogWarning("[UIController] API 키 UI를 만들 부모 UI가 연결되지 않았습니다.");
            return;
        }

        apiKeyPanel = new GameObject("ApiKeyPanel", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        apiKeyPanel.transform.SetParent(UI.transform, false);
        RectTransform panelRect = apiKeyPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.05f, 0.48f);
        panelRect.anchorMax = new Vector2(0.95f, 0.72f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        apiKeyPanel.GetComponent<UnityEngine.UI.Image>().color = new Color(0.04f, 0.05f, 0.08f, 0.92f);

        apiKeyLabel = CreateText("Provider API Key", apiKeyPanel.transform, new Vector2(0.05f, 0.72f), new Vector2(0.95f, 0.96f), 18);
        apiKeyInput = CreateInputField(apiKeyPanel.transform);
        apiKeyStatus = CreateText("키는 현재 실행 중에만 보관됩니다.", apiKeyPanel.transform, new Vector2(0.05f, 0.04f), new Vector2(0.58f, 0.28f), 12);

        GameObject applyObject = new GameObject("ApplyApiKeyButton", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
        applyObject.transform.SetParent(apiKeyPanel.transform, false);
        RectTransform applyRect = applyObject.GetComponent<RectTransform>();
        applyRect.anchorMin = new Vector2(0.63f, 0.04f);
        applyRect.anchorMax = new Vector2(0.95f, 0.28f);
        applyRect.offsetMin = Vector2.zero;
        applyRect.offsetMax = Vector2.zero;
        applyObject.GetComponent<UnityEngine.UI.Image>().color = new Color(0.16f, 0.42f, 0.78f, 1f);
        CreateText("적용", applyObject.transform, Vector2.zero, Vector2.one, 14).alignment = TextAlignmentOptions.Center;
        applyObject.GetComponent<UnityEngine.UI.Button>().onClick.AddListener(ApplyApiKey);
        apiKeyPanel.SetActive(false);
    }

    private TMP_Text CreateText(string value, Transform parent, Vector2 anchorMin, Vector2 anchorMax, float fontSize)
    {
        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = new Vector2(4f, 2f);
        rect.offsetMax = new Vector2(-4f, -2f);
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.font = koreanFont;
        text.text = value;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
        return text;
    }

    private TMP_InputField CreateInputField(Transform parent)
    {
        GameObject inputObject = new GameObject("ApiKeyInput", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        inputObject.SetActive(false);
        inputObject.transform.SetParent(parent, false);
        RectTransform inputRect = inputObject.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0.05f, 0.34f);
        inputRect.anchorMax = new Vector2(0.95f, 0.68f);
        inputRect.offsetMin = Vector2.zero;
        inputRect.offsetMax = Vector2.zero;
        inputObject.GetComponent<UnityEngine.UI.Image>().color = Color.white;

        TMP_Text text = CreateText(string.Empty, inputObject.transform, Vector2.zero, Vector2.one, 14);
        text.color = Color.black;
        text.raycastTarget = true;
        TMP_Text placeholder = CreateText("sk-... 또는 Anthropic 키 입력", inputObject.transform, Vector2.zero, Vector2.one, 14);
        placeholder.color = new Color(0.35f, 0.35f, 0.35f, 1f);

        TMP_InputField input = inputObject.AddComponent<TMP_InputField>();
        input.textComponent = text;
        input.placeholder = placeholder;
        input.targetGraphic = inputObject.GetComponent<UnityEngine.UI.Image>();
        input.contentType = TMP_InputField.ContentType.Password;
        input.lineType = TMP_InputField.LineType.SingleLine;
        inputObject.SetActive(true);
        return input;
    }

    private void ShowApiKeyPanel(string providerName)
    {
        if (apiKeyPanel == null)
            return;
        apiKeyPanel.SetActive(true);
        apiKeyLabel.text = providerName + " API Key";
        apiKeyStatus.text = "키는 현재 실행 중에만 보관됩니다.";
        apiKeyInput.text = string.Empty;
    }

    private void HideApiKeyPanel()
    {
        if (apiKeyPanel != null)
            apiKeyPanel.SetActive(false);
    }

    private void ApplyApiKey()
    {
        if (llmServer == null || apiKeyInput == null || string.IsNullOrWhiteSpace(apiKeyInput.text))
        {
            if (apiKeyStatus != null)
                apiKeyStatus.text = "API 키를 입력해주세요.";
            return;
        }

        llmServer.SetApiKeyForProvider(selectedProviderName, apiKeyInput.text);
        apiKeyStatus.text = "적용되었습니다. 키는 이 실행이 끝나면 사라집니다.";
        apiKeyInput.text = string.Empty;
    }
}
