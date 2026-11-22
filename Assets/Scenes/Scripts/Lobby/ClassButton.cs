using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ClassButton : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI classNameText;
    public Image background;

    public int ClassId { get; private set; }
    public string ClassName { get; private set; }

    private System.Action<int> onClickCallback;
    private Button button;
    private bool isAvailable = false;

    void Awake()
    {
        button = GetComponent<Button>();
        background = GetComponent<Image>();
        classNameText = GetComponentInChildren<TextMeshProUGUI>();
    }

    public void Initialize(int classId, string className, System.Action<int> callback)
    {
        ClassId = classId;
        ClassName = className;
        onClickCallback = callback;

        if (classNameText != null)
            classNameText.text = className;

        if (button != null)
            button.onClick.AddListener(OnClick);

        UpdateState(false, false, false);
    }

    public void UpdateState(bool isBanned, bool isSelected, bool isAvailable)
    {
        this.isAvailable = isAvailable;

        if (background == null) return;

        if (isBanned)
        {
            background.color = Color.red;
            if (button != null) button.interactable = false;
            if (classNameText != null) classNameText.color = Color.white;
        }
        else if (isSelected)
        {
            background.color = Color.green;
            if (button != null) button.interactable = false;
            if (classNameText != null) classNameText.color = Color.white;
        }
        else if (isAvailable)
        {
            background.color = new Color(0.2f, 0.2f, 0.4f, 0.9f);
            if (button != null) button.interactable = true;
            if (classNameText != null) classNameText.color = Color.white;
        }
        else
        {
            background.color = new Color(0.12f, 0.12f, 0.24f, 0.9f);
            if (button != null) button.interactable = false;
            if (classNameText != null) classNameText.color = Color.gray;
        }
    }

    private void OnClick()
    {
        if (isAvailable)
        {
            onClickCallback?.Invoke(ClassId);
        }
    }

    void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(OnClick);
    }
}