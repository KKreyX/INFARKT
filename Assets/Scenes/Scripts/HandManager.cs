using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class HandManager : MonoBehaviour, IPointerEnterHandler, IPointerMoveHandler, IPointerExitHandler, IPointerDownHandler, IPointerClickHandler
{
    [SerializeField] private Transform cardsContainer;
    [SerializeField] private CardView cardViewPrefab;
    
    [Header("Layout Settings")]
    [SerializeField] private float preferredSpacing = 120f;
    [SerializeField] private float maxHandWidth = 900f;
    
    [Header("Preview")]
    [SerializeField] private CardPreviewUI cardPreviewUI;

    private List<Card> cardsInHand = new List<Card>();
    private List<CardView> cardViews = new List<CardView>();
    private const float DealDuration = 0.28f;
    private const float DealInterval = 0.075f;
    private readonly HashSet<CardView> arrivingCards = new HashSet<CardView>();
    private Coroutine dealAnimation;
    private CardView selectedCardView;
    private CardView hoveredCardView;
    private CardView pressedCardView;
    private Image handHitArea;
    private float cardStep;
    private bool handInteractable = true;
    private int playerId = 1;

    private CardView deselectingCard;
    private System.Action onDeselectComplete;

    public int PlayerId => playerId;
    public void SetPlayerId(int id) => playerId = id;
    
    public void SetInteractable(bool interactable)
    {
        handInteractable = interactable;
        foreach (var view in cardViews)
            if (view != null) view.SetInteractable(interactable);

        if (handHitArea != null)
            handHitArea.raycastTarget = interactable;

        if (!interactable)
        {
            pressedCardView = null;
            SetHoveredCard(null);
        }
    }
    
    public void SetFace(bool showFace) { foreach (var v in cardViews) if (v != null) v.SetFace(showFace); }
    public bool HasSelectedCard() => selectedCardView != null;

    private void ShowPreview(Card card)
    {
        CardPreviewUI preview = cardPreviewUI;
        if (preview == null && BoardManager.Instance != null) preview = BoardManager.Instance.cardPreviewUI;
        if (preview != null) preview.Show(card);
    }

    public void DeselectCurrentCard(bool animate = true)
    {
        if (selectedCardView != null)
        {
            selectedCardView.SetSelected(false, animate);
            selectedCardView.SetHovered(false, false);
            selectedCardView = null;
        }
        foreach (var v in cardViews) 
            if (v != null) v.SetHovered(false);

        UpdatePositions();
        ForceNormalizeOrder();
    }

    public void DeselectCurrentCardWithCallback(System.Action callback, bool animate = true)
    {
        if (selectedCardView != null)
        {
            if (animate)
            {
                deselectingCard = selectedCardView;
                onDeselectComplete = callback;
                deselectingCard.OnAnimationComplete += OnDeselectAnimationComplete;
                selectedCardView.SetSelected(false, true);
                selectedCardView.SetHovered(false, false);
                selectedCardView = null;
            }
            else
            {
                selectedCardView.SetSelected(false, false);
                selectedCardView.SetHovered(false, false);
                selectedCardView = null;
                callback?.Invoke();
            }
        }
        else callback?.Invoke();
        
        UpdatePositions();
        ForceNormalizeOrder();
    }

    private void OnDeselectAnimationComplete()
    {
        if (deselectingCard != null) { deselectingCard.OnAnimationComplete -= OnDeselectAnimationComplete; deselectingCard = null; }
        var cb = onDeselectComplete; onDeselectComplete = null; cb?.Invoke();
    }

    public void CancelSelection()
    {
        DeselectCurrentCard(true);
        if (BoardManager.Instance != null) 
        {
            BoardManager.Instance.CancelPendingPlacement();
        }
    }

    public void SetSacrificeMode(bool active, bool highlightNormal = true, bool highlightSpecial = true, Color? highlightColor = null)
    {
        Color defaultColor = Color.yellow;
        foreach (var view in cardViews)
        {
            if (view == null) continue;
            Outline outline = view.GetComponent<Outline>();
            if (outline == null) outline = view.gameObject.AddComponent<Outline>();
            
            if (active)
            {
                var card = view.GetCurrentCard();
                bool shouldHighlight = false;
                if (card != null && card.category == CardCategory.Unit)
                {
                    if (card.cardType == CardType.Number && highlightNormal) shouldHighlight = true;
                    else if (card.cardType != CardType.Number && highlightSpecial) shouldHighlight = true;
                }
                
                outline.enabled = shouldHighlight;
                if (shouldHighlight) 
                { 
                    outline.effectColor = highlightColor ?? defaultColor; 
                    outline.effectDistance = new Vector2(3, 3); 
                }
            }
            else
            {
                outline.enabled = false;
            }
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && selectedCardView != null) CancelSelection();
    }

    public void InitializeHand() 
    { 
        ClearHand(); 
    }

    public void ClearHand()
    {
        if (dealAnimation != null) StopCoroutine(dealAnimation);
        dealAnimation = null;
        arrivingCards.Clear();
        cardsInHand = new List<Card>();
        foreach (var v in cardViews) if (v != null) Destroy(v.gameObject);
        cardViews.Clear();
        hoveredCardView = null;
        pressedCardView = null;
        if (handHitArea != null) handHitArea.gameObject.SetActive(false);
        selectedCardView = null;
    }

    public void UpdateHand(List<Card> cards, Transform deckOrigin = null)
    {
        string previouslySelectedId = selectedCardView != null ? selectedCardView.GetCurrentCard().cardId : null;
        
        bool isSameHand = cardsInHand.Count == cards.Count;
        if (isSameHand)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                if (cardsInHand[i].cardId != cards[i].cardId) { isSameHand = false; break; }
            }
        }

        if (isSameHand)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                cardViews[i].DisplayCard(cards[i]);
            }
            cardsInHand = cards;
            
            if (!string.IsNullOrEmpty(previouslySelectedId))
            {
                var newSelectedView = cardViews.Find(v => v.GetCurrentCard().cardId == previouslySelectedId);
                if (newSelectedView != null && selectedCardView == null)
                {
                    selectedCardView = newSelectedView;
                    selectedCardView.SetSelected(true);
                    ShowPreview(selectedCardView.GetCurrentCard());
                    ForceNormalizeOrder();
                }
            }
            return;
        }

        var previousIds = new HashSet<string>();
        foreach (var card in cardsInHand) previousIds.Add(card.cardId);

        ClearHand();
        cardsInHand = cards;
        foreach (var card in cards) CreateCardView(card);
        UpdatePositions();

        if (!string.IsNullOrEmpty(previouslySelectedId))
        {
            var newSelectedView = cardViews.Find(v => v.GetCurrentCard().cardId == previouslySelectedId);
            if (newSelectedView != null)
            {
                selectedCardView = newSelectedView;
                selectedCardView.SetSelected(true);
                ShowPreview(selectedCardView.GetCurrentCard());
                ForceNormalizeOrder();
            }
        }
        if (deckOrigin != null)
        {
            var incoming = cardViews.FindAll(view => !previousIds.Contains(view.GetCurrentCard().cardId));
            if (incoming.Count > 0)
                dealAnimation = StartCoroutine(AnimateIncomingCards(incoming, deckOrigin));
        }
    }

    private System.Collections.IEnumerator AnimateIncomingCards(List<CardView> incoming, Transform deckOrigin)
    {
        var rects = new RectTransform[incoming.Count];
        var destinations = new Vector2[incoming.Count];
        var flightCanvases = new Canvas[incoming.Count];
        for (int i = 0; i < incoming.Count; i++)
        {
            var card = incoming[i];
            arrivingCards.Add(card);
            var flightCanvas = card.gameObject.AddComponent<Canvas>();
            flightCanvas.overrideSorting = true;
            flightCanvas.sortingOrder = 100;
            flightCanvases[i] = flightCanvas;
            var rect = (RectTransform)card.transform;
            rects[i] = rect;
            destinations[i] = rect.anchoredPosition;
            rect.position = deckOrigin.position;
        }

        float interval = Mathf.Min(DealInterval, 0.45f / Mathf.Max(1, incoming.Count - 1));
        float elapsed = 0f;
        while (arrivingCards.Count > 0)
        {
            for (int i = 0; i < incoming.Count; i++)
            {
                if (!arrivingCards.Contains(incoming[i])) continue;

                var rect = rects[i];
                float progress = Mathf.Clamp01((elapsed - i * interval) / DealDuration);
                rect.anchoredPosition = destinations[i];
                Vector3 destination = rect.position;
                rect.position = Vector3.Lerp(deckOrigin.position, destination, Mathf.SmoothStep(0f, 1f, progress));
                
                if (progress >= 1f)
                {
                    rect.anchoredPosition = destinations[i];
                    incoming[i].SaveOriginalState(destinations[i], rect.localScale);
                    
                    flightCanvases[i].overrideSorting = false;
                    Destroy(flightCanvases[i]);
                    arrivingCards.Remove(incoming[i]);
                }
            }

            if (arrivingCards.Count == 0) break;
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }

        dealAnimation = null;
    }

    private void CreateCardView(Card card)
    {
        if (cardViewPrefab == null || cardsContainer == null) return;
        var view = Instantiate(cardViewPrefab, cardsContainer);
        view.DisplayCard(card);
        view.SetInteractable(handInteractable);

        var group = view.GetComponent<CanvasGroup>();
        if (group == null) group = view.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;

        cardViews.Add(view);
        RectTransform rect = view.GetComponent<RectTransform>();
        view.SaveOriginalState(rect.anchoredPosition, rect.localScale);
    }

    private void UpdatePositions()
    {
        if (cardViews.Count == 0) return;

        var container = (RectTransform)cardsContainer;
        var cardRect = (RectTransform)cardViewPrefab.transform;
        float availableWidth = Mathf.Min(container.rect.width, maxHandWidth);
        float cardWidth = cardRect.rect.width * Mathf.Abs(cardRect.localScale.x);
        int gaps = cardViews.Count - 1;
        float actualSpacing = gaps > 0
            ? Mathf.Min(preferredSpacing, Mathf.Max(0f, availableWidth - cardWidth) / gaps)
            : 0f;

        cardStep = actualSpacing;
        float totalWidth = gaps * actualSpacing;
        UpdateHandHitArea(totalWidth + cardWidth, cardRect.rect.height * Mathf.Abs(cardRect.localScale.y));
        float startX = -totalWidth / 2f;

        for (int i = 0; i < cardViews.Count; i++)
        {
            var view = cardViews[i];
            if (view == null) continue;
            var rect = view.GetComponent<RectTransform>();
            float xPos = startX + i * actualSpacing;
            
            view.SaveOriginalState(new Vector2(xPos, 0), cardRect.localScale);

            if (!view.IsSelected() && !view.IsHovered() && !arrivingCards.Contains(view))
            {
                rect.anchoredPosition = new Vector2(xPos, 0);
            }
        }
        
        UpdateOrder();
    }

    public void OnCardSelected(CardView cardView)
    {
        if (BoardManager.Instance != null && PlayerId != BoardManager.Instance.LocalPlayerId) return;

        Card card = cardView.GetCurrentCard();
        if (card == null) return;

        if (BoardManager.Instance != null && BoardManager.Instance.IsSacrificeModeActive())
        {
            if (card.category == CardCategory.Unit)
            {
                BoardManager.Instance.OnCardFromHandSelected(card);
                cardView.SetHovered(false, true);
                UpdateOrder();
            }
            return;
        }

        if (selectedCardView == cardView) { CancelSelection(); return; }

        if (selectedCardView != null) DeselectCurrentCard(true);

        if (BoardManager.Instance != null) 
        {
            BoardManager.Instance.SaveModeBeforePlacement();
            BoardManager.Instance.DeactivateCurrent();
        }

        selectedCardView = cardView;
        cardView.SetSelected(true);
        UpdateOrder();

        ShowPreview(card);

        bool isLocalTurn = (PlayerId == TurnManager.Instance?.GetCurrentPlayer());

        if (card.category == CardCategory.Unit && isLocalTurn)
        {
            bool needsSacrifice = card.requiresSacrifice && !card.isSacrificePaid;
            if (needsSacrifice)
            {
                if (BoardManager.Instance != null) BoardManager.Instance.StartSacrificeMode(card);
            }
            else
            {
                if (BoardManager.Instance != null && BoardManager.Instance.HasEmptySlot(PlayerId))
                    BoardManager.Instance.StartPlacementFromHand(cardView, PlayerId);
                else
                    DeselectCurrentCard(true);
            }
        }
    }

    private void UpdateHandHitArea(float width, float height)
    {
        if (handHitArea == null)
        {
            var hitObject = new GameObject("Hand Hit Area", typeof(RectTransform), typeof(Image));
            hitObject.layer = cardsContainer.gameObject.layer;
            hitObject.transform.SetParent(cardsContainer, false);
            handHitArea = hitObject.GetComponent<Image>();
            handHitArea.color = Color.clear;
            handHitArea.canvasRenderer.cullTransparentMesh = false;
        }

        var rect = handHitArea.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(width, height);
        handHitArea.raycastTarget = handInteractable;
        handHitArea.gameObject.SetActive(true);
    }

    public void OnPointerEnter(PointerEventData eventData) => OnPointerMove(eventData);

    public void OnPointerMove(PointerEventData eventData)
    {
        var card = GetCardUnderPointer(eventData);
        SetHoveredCard(card == selectedCardView ? null : card);
    }

    public void OnPointerExit(PointerEventData eventData) => SetHoveredCard(null);

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            pressedCardView = GetCardUnderPointer(eventData);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        var card = GetCardUnderPointer(eventData);
        if (card != null && card == pressedCardView) OnCardSelected(card);
        pressedCardView = null;
    }

    private CardView GetCardUnderPointer(PointerEventData eventData)
    {
        if (!handInteractable || handHitArea == null || cardViews.Count == 0) return null;
        if (BoardManager.Instance != null && PlayerId != BoardManager.Instance.LocalPlayerId) return null;

        var rect = handHitArea.rectTransform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect, eventData.position, eventData.enterEventCamera, out var point) ||
            !rect.rect.Contains(point))
        {
            return null;
        }

        int index = cardStep > 0f
            ? Mathf.Min(Mathf.FloorToInt((point.x - rect.rect.xMin) / cardStep), cardViews.Count - 1)
            : cardViews.Count - 1;
        
        if (index < 0 || index >= cardViews.Count) return null;
        
        var card = cardViews[index];
        
        if (arrivingCards.Contains(card)) return null;
        
        return card;
    }

    private void SetHoveredCard(CardView card)
    {
        if (hoveredCardView == card && (card == null || card.IsHovered())) return;

        if (hoveredCardView != null && hoveredCardView.IsHovered()) hoveredCardView.SetHovered(false);
        hoveredCardView = card;
        if (hoveredCardView != null) hoveredCardView.SetHovered(true);
        UpdateOrder();
    }

    private void ForceNormalizeOrder()
    {
        for (int i = 0; i < cardViews.Count; i++)
        {
            if (cardViews[i] != null) cardViews[i].transform.SetSiblingIndex(i);
        }
    }

    private void UpdateOrder()
    {
        ForceNormalizeOrder();

        if (selectedCardView != null) 
            selectedCardView.transform.SetAsLastSibling();
        
        foreach (var v in cardViews)
        {
            if (v != null && v.IsHovered() && v != selectedCardView) 
                v.transform.SetAsLastSibling();
        }
    }
}