using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class EternalSleepUI : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject panel;
    public Button closeButton;
    public Button player1Button;
    public Button player2Button;
    
    [Header("Content")]
    public Transform cardsContainer;
    public CardView cardPrefab;
    public TextMeshProUGUI titleText;

    private int currentViewPlayerId = -1;
    private List<CardView> spawnedCards = new List<CardView>();

    void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Hide);
        if (player1Button != null) player1Button.onClick.AddListener(() => OpenForPlayer(1));
        if (player2Button != null) player2Button.onClick.AddListener(() => OpenForPlayer(2));
        if (panel != null) panel.SetActive(false);
    }

    public void OpenForPlayer(int playerId)
    {
        currentViewPlayerId = playerId;
        if (panel != null) panel.SetActive(true);
        RefreshCurrentView();
    }

    public void RefreshCurrentView()
    {
        if (currentViewPlayerId == -1) return;
        var state = GameManager.Instance.GetCurrentState();
        if (state == null) return;

        var targetPlayer = currentViewPlayerId == 1 ? state.player1 : state.player2;
        if (titleText != null) titleText.text = $"Вечный сон (Игрок {currentViewPlayerId})";
        
        RefreshUI(targetPlayer.eternalSleep);
    }

    public void RefreshUI(List<Card> cards)
    {
        foreach (var c in spawnedCards) if (c != null) Destroy(c.gameObject);
        spawnedCards.Clear();

        foreach (var card in cards)
        {
            var view = Instantiate(cardPrefab, cardsContainer);
            view.DisplayCard(card);
            view.SetSleepVisual(card.sleepTimer);
            
            Button btn = view.GetComponent<Button>();
            if (btn == null) btn = view.gameObject.AddComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnCardClicked(card));
            
            spawnedCards.Add(view);
        }
    }

    private void OnCardClicked(Card card)
    {
        bool isLocalPlayerCard = (currentViewPlayerId == BoardManager.Instance.LocalPlayerId);

        if (isLocalPlayerCard && card.sleepTimer <= 0)
        {
            Hide();
            if (BoardManager.Instance != null)
            {
                BoardManager.Instance.StartRevivalMode(card);
            }
        }
        else
        {
            if (BoardManager.Instance != null && BoardManager.Instance.cardPreviewUI != null)
            {
                BoardManager.Instance.cardPreviewUI.Show(card);
            }
        }
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
        currentViewPlayerId = -1;
        foreach (var c in spawnedCards) if (c != null) Destroy(c.gameObject);
        spawnedCards.Clear();
        
        if (BoardManager.Instance != null && BoardManager.Instance.cardPreviewUI != null)
        {
            BoardManager.Instance.cardPreviewUI.Hide();
        }
    }
}