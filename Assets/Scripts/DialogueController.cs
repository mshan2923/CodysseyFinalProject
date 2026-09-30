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
        UpdateText(variable?.Value);   // 위치 계산 전에 패널 크기 확정

        Vector3 sp = cam.WorldToScreenPoint(target.position + worldOffset);

        // 카메라 뒤에 있는 경우
        if (sp.z < 0)
        {
            Vector2 center = new(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 dir = center - (Vector2)sp;

            if (dir.sqrMagnitude < 0.01f)
                dir = Vector2.down;

            sp = center + dir.normalized * 100000f;
        }

        // 화면 오프셋 적용
        sp += (Vector3)screenOffset;

        // 패널의 실제 크기
        Vector2 size = panel.rect.size * canvas.scaleFactor;
        Vector2 pivot = panel.pivot;

        // Pivot 위치를 기준으로 필요한 화면 여백 계산
        float left = size.x * pivot.x;
        float right = size.x * (1f - pivot.x);

        float bottom = size.y * pivot.y;
        float top = size.y * (1f - pivot.y);

        // 패널 전체가 화면 안에 들어오도록 Clamp
        sp.x = Mathf.Clamp(
            sp.x,
            left,
            Screen.width - right
        );

        sp.y = Mathf.Clamp(
            sp.y,
            bottom,
            Screen.height - top
        );

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            panel.position = sp;
        }
        else
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)panel.parent,
                sp,
                canvas.worldCamera,
                out var local
            );

            panel.localPosition = local;
        }
    }
}
