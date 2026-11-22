using UnityEngine;
using System.Collections.Generic;
using TMPro;

public class Deck : MonoBehaviour
{
    public bool isPlayerDeck = true;
    [SerializeField] private TextMeshProUGUI deckCountText;
    
    public List<Card> mainDeck { get; private set; } = new List<Card>();
    
    private int[] _selectedClasses = new int[] { -1, -1 };
    private bool _isInitialized = false;
    private System.Random _random;
    
    void Start()
    {
        InitializeDeck();
    }
    
    public void SetSelectedClasses(int[] classIds)
    {
        if (classIds != null && classIds.Length == 2)
        {
            _selectedClasses = classIds;
        }
    }
    
    public void InitializeDeck()
    {
        if (_isInitialized) return;
        
        mainDeck.Clear();
        _random = new System.Random(System.Guid.NewGuid().GetHashCode());
        
        int[] selectedClassIds = GetSelectedClasses();
        
        if (selectedClassIds == null || selectedClassIds.Length == 0) return;
        
        foreach (int classId in selectedClassIds)
        {
            var classConfig = CardLibrary.Instance.GetClassConfig(classId);
            if (classConfig != null)
            {
                List<Card> classCards = classConfig.GenerateAllCards();
                foreach (Card card in classCards)
                {
                    mainDeck.Add(card);
                }
            }
        }
        
        ShuffleMainDeck();
        _isInitialized = true;
        UpdateDeckUI();
    }
    
    public void ShuffleMainDeck()
    {
        if (mainDeck.Count <= 1) return;
        
        int n = mainDeck.Count;
        while (n > 1)
        {
            n--;
            int k = _random.Next(n + 1);
            Card value = mainDeck[k];
            mainDeck[k] = mainDeck[n];
            mainDeck[n] = value;
        }
    }
    
    private int[] GetSelectedClasses()
    {
        if (_selectedClasses[0] != -1 && _selectedClasses[1] != -1) return _selectedClasses;
        
        if (GameDataManager.Instance != null)
        {
            if (GameDataManager.Instance.IsNetworkGame)
            {
                return isPlayerDeck ? 
                    GameDataManager.Instance.LocalPlayerClasses : 
                    GameDataManager.Instance.RemotePlayerClasses;
            }
        }
        
        return isPlayerDeck ? new int[] { 0, 1 } : new int[] { 2, 3 };
    }
    
    private void UpdateDeckUI()
    {
        if (deckCountText != null)
        {
            deckCountText.text = GetTotalCardCount().ToString();
        }
    }
    
    public Card DrawCard()
    {
        if (mainDeck.Count > 0)
        {
            Card drawnCard = mainDeck[0];
            mainDeck.RemoveAt(0);
            UpdateDeckUI();
            return drawnCard;
        }
        
        UpdateDeckUI();
        return null;
    }
    
    public List<Card> DrawCards(int count)
    {
        List<Card> drawnCards = new List<Card>();
        
        for (int i = 0; i < count; i++)
        {
            Card card = DrawCard();
            if (card != null) drawnCards.Add(card);
            else break;
        }
        
        return drawnCards;
    }
    
    public int GetTotalCardCount() => mainDeck.Count;
    public int GetMainDeckCount() => mainDeck.Count;
    public bool IsInitialized() => _isInitialized;
    
    public void AddCardToMainDeck(Card card)
    {
        if (card != null)
        {
            mainDeck.Add(card);
            UpdateDeckUI();
        }
    }
    
    public void AddCardsToMainDeck(List<Card> cards)
    {
        if (cards != null)
        {
            mainDeck.AddRange(cards);
            UpdateDeckUI();
        }
    }
    
    public void Shuffle() => ShuffleMainDeck();
}