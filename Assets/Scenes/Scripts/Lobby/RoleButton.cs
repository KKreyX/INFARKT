using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RoleButton : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI roleText;
    public TextMeshProUGUI statusText;
    public Image background;

    [Header("Customizable Texts")]
    public string youText = "Вы";
    public string occupiedText = "Занято";
    public string freeText = "Свободно";

    public int PlayerId { get; private set; }
    public string Role { get; private set; }

    private System.Action<int, string> onClickCallback;
    private Button button;
    private bool isInteractable = true;

    void Awake()
    {
        button = GetComponent<Button>();
        background = GetComponent<Image>();
        
        TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>();
        if (texts.Length >= 2)
        {
            roleText = texts[0];
            statusText = texts[1];
        }
        else if (texts.Length == 1)
        {
            roleText = texts[0];
        }
    }

    public void Initialize(int playerId, string role, System.Action<int, string> callback)
    {
        PlayerId = playerId;
        Role = role;
        onClickCallback = callback;

        if (roleText != null)
        {
            if (role == "player")
                roleText.text = $"Игрок {playerId + 1}";
            else
                roleText.text = $"Зритель {playerId - 1}";
        }

        if (button != null)
            button.onClick.AddListener(OnClick);
    }

    public void UpdateState(bool isOccupied, bool isCurrentPlayer, bool hasValidPlayerId = true)
    {
        if (background == null) return;

        bool canInteract = hasValidPlayerId && (!isOccupied || isCurrentPlayer);

        if (isCurrentPlayer)
        {
            background.color = new Color(0f, 0.4f, 1f, 0.9f);
            if (statusText != null)
            {
                statusText.text = youText;
                statusText.color = Color.white;
            }
            isInteractable = false;
        }
        else if (isOccupied)
        {
            background.color = new Color(0f, 0.5f, 0f, 0.9f);
            if (statusText != null)
            {
                statusText.text = occupiedText;
                statusText.color = Color.green;
            }
            isInteractable = false;
        }
        else
        {
            background.color = new Color(0.12f, 0.12f, 0.24f, 0.9f);
            if (statusText != null)
            {
                statusText.text = canInteract ? freeText : occupiedText;
                statusText.color = canInteract ? Color.gray : Color.yellow;
            }
            isInteractable = canInteract;
        }

        if (button != null)
            button.interactable = isInteractable;
    }

    private void OnClick()
    {
        if (isInteractable)
        {
            onClickCallback?.Invoke(PlayerId, Role);
        }
    }

    void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(OnClick);
    }
}