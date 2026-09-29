using TMPro;
using Unity.Behavior;
using UnityEngine;

public class DialogueController : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] Vector3 worldOffset = new(0, 2f, 0);
    [SerializeField] Vector2 screenOffset;

    public RectTransform panel;
    private TMP_Text text;

    Camera cam;
    public Canvas canvas;

    [SerializeField]
    private BehaviorGraphAgent agent;

    public string Variable = "Result";

    [SerializeField] Vector2 padding = new(40, 24);
    [SerializeField] float maxWidth = 400f;
    string lastText;

    void Awake()
    {
        // canvas = GetComponentInParent<Canvas>();
        cam = Camera.main;
        text = panel.GetComponentInChildren<TMP_Text>();

    }

    void UpdateText(string s)
    {
        s ??= "";
        if (s == lastText) return;
        lastText = s;

        text.text = s;
        float innerMax = maxWidth - padding.x;
        Vector2 pref = text.GetPreferredValues(s, innerMax, 0);
        panel.sizeDelta = new Vector2(Mathf.Min(pref.x, innerMax) + padding.x, pref.y + padding.y);
    }

    void LateUpdate()
    {
        if (target == null) return;

        agent.GetVariable<string>(Variable, out var variable);
        UpdateText(variable?.Value);   // 위치 계산 전에 크기 확정

        Vector3 sp = cam.WorldToScreenPoint(target.position + worldOffset);

        // 카메라 뒤: 좌우/상하 반전 후 테두리로 밀어냄
        if (sp.z < 0)
        {
            Vector2 c = new(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 dir = c - (Vector2)sp;
            if (dir.sqrMagnitude < 0.01f) dir = Vector2.down;
            sp = c + dir.normalized * 100000f;
        }

        // 패널이 화면 밖으로 잘리지 않게 패널 반크기만큼 안쪽으로 clamp
        Vector2 half = panel.rect.size * canvas.scaleFactor * 0.5f;
        sp.x = Mathf.Clamp(sp.x, half.x, Screen.width - half.x);
        sp.y = Mathf.Clamp(sp.y, half.y, Screen.height - half.y);

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            panel.position = sp + (Vector3)screenOffset;
        }
        else
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)panel.parent, sp, canvas.worldCamera, out var local);
            panel.localPosition = local + screenOffset;
        }

        text.text = variable?.Value;
    }
}
