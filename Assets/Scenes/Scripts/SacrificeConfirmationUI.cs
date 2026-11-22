using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class SacrificeConfirmationUI : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject panel;
    public Button confirmButton;
    public Button closeButton;
    
    [Header("Slots")]
    public Transform regularSlot; // Слот для обычных (1-5)
    public Transform specialSlot; // Слот для специальных (J, Q, D, K, T)

    [Header("Texts")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;

    [Header("Card Prefab")]
    public CardView cardPrefab;

    public System.Action OnConfirmed;
    public System.Action OnCancelled;
    public System.Action<bool> OnSlotCardClicked; // true = обычный, false = специальный

    private CardView displayedRegularCard;
    private CardView displayedSpecialCard;

    void Awake()
    {
        if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
        if (closeButton != null) closeButton.onClick.AddListener(Cancel);
        if (panel != null) panel.SetActive(false);
    }

    void Update()
    {
        if (panel != null && panel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            Cancel();
    }

    public void Show(Card pendingCard, int apCost)
    {
        if (panel != null) panel.SetActive(true);
        if (titleText != null) titleText.text = $"Оплата призыва: {pendingCard.cardName}";
        if (descriptionText != null) descriptionText.text = $"Будет потрачено {apCost} ОД.";
        
        ClearSlots();
    }

    public void SetCardInSlot(Card card, bool isRegular)
    {
        if (isRegular)
        {
            if (displayedRegularCard != null) Destroy(displayedRegularCard.gameObject);
            displayedRegularCard = Instantiate(cardPrefab, regularSlot);
            SetupSlotCard(displayedRegularCard, card, true);
        }
        else
        {
            if (displayedSpecialCard != null) Destroy(displayedSpecialCard.gameObject);
            displayedSpecialCard = Instantiate(cardPrefab, specialSlot);
            SetupSlotCard(displayedSpecialCard, card, false);
        }
    }

    private void SetupSlotCard(CardView view, Card card, bool isRegular)
    {
        view.DisplayCard(card);
        view.SetInteractable(true); // Разрешаем клики, чтобы убрать карту
        
        RectTransform rt = view.GetComponent<RectTransform>();
        if (rt != null) { rt.localPosition = Vector3.zero; rt.localScale = Vector3.one; }

        Button btn = view.gameObject.GetComponent<Button>();
        if (btn == null) btn = view.gameObject.AddComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => OnSlotCardClicked?.Invoke(isRegular));
    }

    public void ClearSlots()
    {
        if (displayedRegularCard != null) { Destroy(displayedRegularCard.gameObject); displayedRegularCard = null; }
        if (displayedSpecialCard != null) { Destroy(displayedSpecialCard.gameObject); displayedSpecialCard = null; }
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
        ClearSlots();
    }

    private void Confirm() { Hide(); OnConfirmed?.Invoke(); }
    private void Cancel() { Hide(); OnCancelled?.Invoke(); }

    void OnDestroy()
    {
        if (confirmButton != null) confirmButton.onClick.RemoveListener(Confirm);
        if (closeButton != null) closeButton.onClick.RemoveListener(Cancel);
    }
}