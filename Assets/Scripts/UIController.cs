using System;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class UIController : MonoBehaviour
{
    public LLMService llmServer;
    public GameObject UI;
    public TMP_Dropdown dropdownMenu;

    private bool waitingForEnvInput;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        dropdownMenu.onValueChanged.AddListener(OnDropdownValueChanged);
    }

    // Update is called once per frame
    void Update()
    {
        if (Keyboard.current[Key.Tab].wasPressedThisFrame)
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

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus || !waitingForEnvInput)
            return;

        waitingForEnvInput = false;
        llmServer.ReloadApiKeysFromDotEnv();
    }

    void OnDropdownValueChanged(int index)
    {
        string selectedOption = dropdownMenu.options[index].text;

        if (string.Equals(selectedOption, "OpenAI", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(selectedOption, "Anthropic", StringComparison.OrdinalIgnoreCase))
        {
            waitingForEnvInput = true;
            OpenOrCreateDotEnv();
        }

        llmServer.SetModel(selectedOption);
    }

    private void OpenOrCreateDotEnv()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string envPath = Path.Combine(projectRoot, ".env");

        try
        {
            if (!File.Exists(envPath))
            {
                const string template =
                    "# Add the API key for the provider you want to use.\r\n" +
                    "OPENAI_API_KEY=\r\n" +
                    "ANTHROPIC_API_KEY=\r\n";

                File.WriteAllText(envPath, template, new UTF8Encoding(false));
                Debug.Log($"[UIController] .env 파일을 생성했습니다: {envPath}");
            }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = envPath,
                UseShellExecute = true
            });
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            System.Diagnostics.Process.Start("open", envPath);
#elif UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
            System.Diagnostics.Process.Start("xdg-open", envPath);
#else
            Application.OpenURL("file://" + envPath.Replace("\\", "/"));
#endif
        }
        catch (Exception exception)
        {
            Debug.LogError($"[UIController] .env 파일을 열 수 없습니다: {envPath}\n{exception}");
        }
    }
}
