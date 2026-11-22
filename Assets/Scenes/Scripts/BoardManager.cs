using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class BoardManager : MonoBehaviour
{
    public static BoardManager Instance { get; private set; }

    [System.Serializable]
    public class SlotData
    {
        public BoardPosition position;
        public Card currentCard;
        public bool IsOccupied => currentCard != null;
        public int ownerId;
    }

    public SlotData[] player1Slots = new SlotData[5];
    public SlotData[] player2Slots = new SlotData[5];
    public BoardSlotView[] slotViews;
    public HandManager playerHand;
    public HandManager opponentHand;
    public CardView cardViewPrefab;
    
    [Header("Preview UI")]
    public CardPreviewUI cardPreviewUI;

    [Header("Eternal Sleep UI")]
    public EternalSleepUI eternalSleepUI;

    public Button selfPortraitButton;
    public Button opponentPortraitButton;
    public Color portraitHighlightColor = Color.red;
    
    [Header("Avatars")]
    public Sprite player1Avatar;
    public Sprite player2Avatar;

    [Header("Highlight Colors")]
    public Color attackHighlightColor = Color.red;
    public Color moveHighlightColor = Color.cyan;
    public Color selectedUnitColor = Color.yellow;
    public Color unavailableUnitColor = Color.gray;
    public Color sacrificeHighlightColor = Color.magenta;
    public Color selectedSacrificeColor = Color.green;

    [Header("Sacrifice UI")]
    [SerializeField] private SacrificeConfirmationUI sacrificeUI;

    [Header("Card Actions")]
    [SerializeField] private Sprite cardSelectionSprite;
    [SerializeField] private Sprite combatPopupBackground;
    [SerializeField] private Sprite attackIcon;
    [SerializeField] private Sprite moveIcon;
    [SerializeField] private Sprite skillIcon;

    private RectTransform combatPopup;
    private BoardPosition popupPosition;
    private string popupCardId;
    private TMPro.TextMeshProUGUI moveCostLabel;
    private BoardSlotView moveCostSlot;

    private BoardPosition moveFrom;
    private BoardPosition moveTo;
    private string movingCardId;
    private Coroutine moveAnimation;
    private readonly List<CardView> animatedCards = new List<CardView>();

    private AttackController attackController;
    private MoveController moveController;
    private SacrificeController sacrificeController;

    private int lastCardHitSequence;
    private CardView raisedCard;
    private Coroutine liftAnimation;
    private Coroutine attackAnimation;
    private CardView lungingCard;
    private CardView defeatedTarget;
    private bool destroyLungingCard;
    private Canvas attackCanvas;
    private ServerGameState.CardHit pendingImpact;
    private IBoardMode currentMode;
    private Card pendingCard;
    private Card pendingRevivalCard;

    private enum CombatMode { None, Move, Attack }
    private CombatMode modeBeforePlacement = CombatMode.None;

    private int localPlayerId = 1;
    private bool slotsSwapped;

    void Awake()
    {
        Instance = this;
        attackController = new AttackController(this);
        moveController = new MoveController(this);
        sacrificeController = new SacrificeController(this, sacrificeUI);
        currentMode = null;
    }

    void Start()
    {
        InitializeBoard();
        StartDefaultMode();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (playerHand != null && playerHand.HasSelectedCard()) playerHand.CancelSelection();
            if (currentMode != null) currentMode.ClearSelection();
            HideCombatPopup();
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (UnityEngine.EventSystems.EventSystem.current != null && 
                !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                if (pendingCard != null || pendingRevivalCard != null)
                {
                    if (playerHand != null) playerHand.CancelSelection();
                    else DeactivateCurrent();
                }
                else if (currentMode != null)
                {
                    if (currentMode == sacrificeController) sacrificeController.Cancel();
                    else currentMode.ClearSelection();
                    HideCombatPopup();
                }
                else
                {
                    if (cardPreviewUI != null) cardPreviewUI.Hide();
                }
            }
        }

        currentMode?.Update();
    }

    public void SaveModeBeforePlacement()
    {
        if (currentMode == moveController) modeBeforePlacement = CombatMode.Move;
        else if (currentMode == attackController) modeBeforePlacement = CombatMode.Attack;
    }

    public void StartDefaultMode()
    {
        if (currentMode != null) return;
        if (TurnManager.Instance == null) return;
        if (TurnManager.Instance.GetCurrentPlayer() != localPlayerId) return;

        if (modeBeforePlacement != CombatMode.Move)
        {
            attackController.Activate();
            currentMode = attackController;
        }
        else
        {
            moveController.Activate();
            currentMode = moveController;
        }
        modeBeforePlacement = CombatMode.None;
        TurnManager.Instance.UpdateUI();
    }

    public int LocalPlayerId => localPlayerId;
    public BoardSlotView[] SlotViews => slotViews;
    public HandManager PlayerHand => playerHand;
    public HandManager OpponentHand => opponentHand;
    public CardView CardViewPrefab => cardViewPrefab;
    public SacrificeConfirmationUI SacrificeUI => sacrificeUI;

    public ServerGameState State => GameManager.Instance?.GetCurrentState();
    public bool IsSlotFree(int playerId, BoardPosition pos) => State != null && State.IsSlotFree(playerId, pos);
    public Card GetUnitAt(int playerId, BoardPosition pos) => State?.GetUnitAt(playerId, pos);
    
    public bool HasEmptySlot(int playerId)
    {
        if (State == null) return false;
        foreach (BoardPosition p in System.Enum.GetValues(typeof(BoardPosition)))
            if (p != BoardPosition.None && State.IsSlotFree(playerId, p)) return true;
        return false;
    }

    public void UpdateAvatars(int localId)
    {
        if (selfPortraitButton != null && selfPortraitButton.GetComponent<Image>() != null)
            selfPortraitButton.GetComponent<Image>().sprite = (localId == 1) ? player1Avatar : player2Avatar;

        if (opponentPortraitButton != null && opponentPortraitButton.GetComponent<Image>() != null)
            opponentPortraitButton.GetComponent<Image>().sprite = (localId == 1) ? player2Avatar : player1Avatar;
    }

    public void ClearHighlights()
    {
        ResetRaisedCard();
        foreach (var view in slotViews)
        {
            view.SetSelection(false);
            view.SetHighlightMode(false);
        }
    }
    public void HighlightSlotsForPlayer(int playerId, Color? color = null) { foreach (var view in slotViews) view.SetHighlightMode(view.ownerId == playerId, color); }
    public void HighlightSelectedCard(BoardPosition pos, Color fallbackColor)
    {
        var slot = GetSlotView(localPlayerId, pos);
        if (slot == null) return;
        RaiseCard(slot);

        if (cardSelectionSprite != null)
            slot.SetSelection(true, cardSelectionSprite);
        else
            slot.SetHighlightMode(true, fallbackColor);
    }

    public void HighlightAttackingCard(BoardPosition pos)
    {
        var slot = GetSlotView(localPlayerId, pos);
        if (slot == null) return;
        slot.SetHighlightMode(true, attackHighlightColor, true);
        RaiseCard(slot);
    }

    public void SetAttackTargetHover(BoardSlotView slot, bool hovered)
    {
        if (IsAttackMode() && attackController.IsCardTarget(slot.ownerId, slot.position))
            slot.SetHighlightMode(true, hovered ? new Color(1f, 0.45f, 0.35f) : attackHighlightColor, hovered);
    }

    private void RaiseCard(BoardSlotView slot)
    {
        var card = slot.GetComponentInChildren<CardView>();
        if (card == null || card == raisedCard || card == lungingCard ||
            card == defeatedTarget || animatedCards.Contains(card)) return;

        ResetRaisedCard();
        raisedCard = card;
        liftAnimation = StartCoroutine(LiftCard(card.transform));
    }

    private static System.Collections.IEnumerator LiftCard(Transform card)
    {
        Vector3 start = card.localPosition;
        Vector3 raised = Vector3.up * 16f;
        const float duration = 0.12f;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            if (card == null) yield break;
            card.localPosition = Vector3.Lerp(start, raised, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            yield return null;
        }
        if (card != null) card.localPosition = raised;
    }

    private void ResetRaisedCard()
    {
        if (liftAnimation != null) StopCoroutine(liftAnimation);
        liftAnimation = null;
        if (raisedCard != null && raisedCard != lungingCard)
            raisedCard.transform.localPosition = Vector3.zero;
        raisedCard = null;
    }

    private void ShowImpact(ServerGameState.CardHit hit)
    {
        GetSlotView(hit.targetOwner, hit.targetPosition)?.PlayHit(hit.damage);
        if (hit.counterDamage >= 0)
            GetSlotView(hit.attackerOwner, hit.attackerPosition)?.PlayHit(hit.counterDamage);
    }

    private System.Collections.IEnumerator PlayAttack(BoardSlotView source, BoardSlotView target)
    {
        var card = lungingCard.transform;
        var start = card.position;
        var impact = Vector3.Lerp(source.transform.position, target.transform.position, 0.9f);
        const float approach = 0.14f;
        for (float elapsed = 0f; elapsed < approach; elapsed += Time.unscaledDeltaTime)
        {
            float t = elapsed / approach;
            card.position = Vector3.Lerp(start, impact, t * t);
            yield return null;
        }

        card.position = impact;
        ShowImpact(pendingImpact);
        pendingImpact = null;
        RemoveDefeatedTarget();

        if (!destroyLungingCard)
        {
            const float retreat = 0.2f;
            for (float elapsed = 0f; elapsed < retreat; elapsed += Time.unscaledDeltaTime)
            {
                card.position = Vector3.Lerp(impact, source.transform.position,
                    Mathf.SmoothStep(0f, 1f, elapsed / retreat));
                yield return null;
            }
        }

        attackAnimation = null;
        FinishAttack();
    }

    private void RemoveDefeatedTarget()
    {
        if (defeatedTarget == null) return;
        defeatedTarget.gameObject.SetActive(false);
        Destroy(defeatedTarget.gameObject);
        defeatedTarget = null;
    }

    private void FinishAttack()
    {
        if (attackAnimation != null) StopCoroutine(attackAnimation);
        attackAnimation = null;
        if (pendingImpact != null) ShowImpact(pendingImpact);
        pendingImpact = null;
        RemoveDefeatedTarget();

        if (attackCanvas != null)
        {
            attackCanvas.overrideSorting = false;
            Destroy(attackCanvas);
        }
        attackCanvas = null;

        if (lungingCard != null)
        {
            if (destroyLungingCard)
            {
                lungingCard.gameObject.SetActive(false);
                Destroy(lungingCard.gameObject);
            }
            else
            {
                lungingCard.transform.localPosition = Vector3.zero;
            }
        }
        lungingCard = null;
        destroyLungingCard = false;
    }

    public void SetPortraitHighlight(bool active, Color? color = null)
    {
        if (opponentPortraitButton == null) return;
        var outline = opponentPortraitButton.GetComponent<Outline>();
        if (outline != null) { outline.enabled = active; if (active) outline.effectColor = color ?? portraitHighlightColor; }
    }
    public void SetPortraitInteractable(bool interactable) { if (opponentPortraitButton != null) opponentPortraitButton.interactable = interactable; }
    public void SetHandInteractable(bool interactable) { if (playerHand != null) playerHand.SetInteractable(interactable); }
    
    public void SetHandSacrificeMode(bool active, bool highlightNormal = true, bool highlightSpecial = true, Color? highlightColor = null) 
    { 
        if (playerHand != null) playerHand.SetSacrificeMode(active, highlightNormal, highlightSpecial, highlightColor); 
    }
    
    public void DeselectHandCard() { if (playerHand != null) playerHand.DeselectCurrentCard(false); }

    public bool IsAttackMode() => currentMode == attackController && attackController.IsActive;
    public bool IsMoveMode() => currentMode == moveController && moveController.IsActive;
    public bool IsSacrificeModeActive() => currentMode == sacrificeController && sacrificeController.IsActive;
    public int PreviewActionPointCost
    {
        get
        {
            if (State?.currentPlayerId != localPlayerId) return 0;
            if (pendingCard != null) return pendingCard.isSacrificePaid ? 0 : pendingCard.actionPointCost;
            if (pendingRevivalCard != null)
                return pendingRevivalCard.cardType == CardType.K || pendingRevivalCard.cardType == CardType.T ? 3 : 2;
            if (IsSacrificeModeActive()) return sacrificeController.ActionPointCost;
            if (IsMoveMode() && moveCostSlot != null) return moveController.GetTargetCost(moveCostSlot.position);
            if (IsAttackMode() && attackController.SelectedAttacker != BoardPosition.None) return 1;
            return 0;
        }
    }

    public bool IsPlacementMode() => pendingCard != null || pendingRevivalCard != null;

    public bool IsAnyModeActive()
    {
        return currentMode != null || IsPlacementMode();
    }

    public void CancelAttackMode() => DeactivateCurrent();
    public void CancelMoveMode() => DeactivateCurrent();

    public void StartPlacementMode(Card card, CardView cardView)
    {
        HideCombatPopup();
        if (card == null) return;
        if (currentMode != null) { currentMode.OnExit(); currentMode = null; }
        ClearHighlights(); SetHandSacrificeMode(false); SetPortraitInteractable(false); SetPortraitHighlight(false);

        if (card.requiresSacrifice && !card.isSacrificePaid)
        {
            sacrificeController.Activate(card);
            currentMode = sacrificeController;
        }
        else
        {
            pendingCard = card;
            pendingRevivalCard = null;
            HighlightSlotsForPlayer(localPlayerId, moveHighlightColor);
        }
    }

    public void StartSacrificeMode(Card card)
    {
        HideCombatPopup();
        if (currentMode != null) { currentMode.OnExit(); currentMode = null; }
        ClearHighlights(); SetHandSacrificeMode(false); SetPortraitInteractable(false); SetPortraitHighlight(false);
        sacrificeController.Activate(card);
        currentMode = sacrificeController;
    }

    public void StartRevivalMode(Card card)
    {
        HideCombatPopup();
        SaveModeBeforePlacement();
        if (currentMode != null) { currentMode.OnExit(); currentMode = null; }
        ClearHighlights(); SetHandSacrificeMode(false); SetPortraitInteractable(false); SetPortraitHighlight(false);
        if (cardPreviewUI != null) cardPreviewUI.Show(card); 
        pendingRevivalCard = card;
        pendingCard = null;
        HighlightSlotsForPlayer(localPlayerId, moveHighlightColor);
    }

    public void DeactivateCurrent()
    {
        HideCombatPopup();
        if (currentMode != null) { currentMode.OnExit(); currentMode = null; }
        pendingCard = null;
        pendingRevivalCard = null;
        ClearHighlights(); SetHandInteractable(true); SetHandSacrificeMode(false); SetPortraitInteractable(false); SetPortraitHighlight(false);
        DeselectHandCard();
    }

    public void CancelPendingPlacement()
    {
        pendingCard = null;
        pendingRevivalCard = null;
        ClearHighlights();
        DeselectHandCard();
        SetHandSacrificeMode(false);
        StartDefaultMode();
    }

    public void OnSlotClicked(int playerId, BoardPosition pos)
    {
        if (pendingCard != null || pendingRevivalCard != null)
        {
            if (playerId != localPlayerId)
            {
                if (IsSlotFree(playerId, pos)) return;
                CancelPendingPlacement();
                if (cardPreviewUI != null) cardPreviewUI.Show(GetUnitAt(playerId, pos));
                return;
            }

            if (IsSlotFree(playerId, pos))
            {
                Card placedCard = pendingCard;
                if (pendingRevivalCard != null) GameClient.Instance.SendReviveUnit(pendingRevivalCard.cardId, pos);
                else if (pendingCard != null)
                {
                    var action = new GameActionMessage { actionType = "PlaceUnit", cardId = pendingCard.cardId, position = pos };
                    GameClient.Instance.SendGameAction(action);
                }
                DeactivateCurrent();
                StartDefaultMode();
                if (placedCard != null && cardPreviewUI != null) cardPreviewUI.Show(placedCard);
            }
            else
            {
                Card clickedUnit = GetUnitAt(playerId, pos);
                CancelPendingPlacement();
                if (currentMode != null && clickedUnit != null) 
                {
                    HandleCombatSlotClick(playerId, pos, clickedUnit);
                    if (cardPreviewUI != null) cardPreviewUI.Show(clickedUnit);
                }
            }
            return;
        }

        Card cardOnSlot = GetUnitAt(playerId, pos);

        if (cardOnSlot != null && cardPreviewUI != null) 
            cardPreviewUI.Show(cardOnSlot);
        else if (cardOnSlot == null && currentMode == null && cardPreviewUI != null) 
            cardPreviewUI.Hide();

        HandleCombatSlotClick(playerId, pos, cardOnSlot);
    }

    private void HandleCombatSlotClick(int playerId, BoardPosition pos, Card card)
    {
        if (moveAnimation != null || attackAnimation != null) return;

        bool canOpenPopup = playerId == localPlayerId && card != null &&
            !card.hasAttackedThisTurn && (IsAttackMode() || IsMoveMode());

        if (canOpenPopup && IsMoveMode() &&
            (moveController.SelectedMover == BoardPosition.None ||
             !moveController.IsValidTarget(pos)))
        {
            if (popupPosition == pos && popupCardId == card.cardId)
            {
                moveController.ClearSelection();
                HideCombatPopup();
                return;
            }
            SetCombatAction(pos, false);
        }
        else
        {
            currentMode?.OnSlotClicked(playerId, pos);
        }

        bool isSelected = (IsAttackMode() && attackController.SelectedAttacker == pos) ||
            (IsMoveMode() && moveController.SelectedMover == pos);
        if (canOpenPopup && isSelected)
            ShowCombatPopup(pos, card.cardId);
        else
            HideCombatPopup();
    }

    public void ShowMoveCost(BoardSlotView slot)
    {
        int cost = slot.ownerId == localPlayerId ? moveController.GetTargetCost(slot.position) : 0;
        if (cost == 0) return;

        if (moveCostLabel == null)
        {
            var label = new GameObject("Move Cost", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            label.layer = slot.gameObject.layer;
            moveCostLabel = label.GetComponent<TMPro.TextMeshProUGUI>();
            if (cardPreviewUI != null && cardPreviewUI.MoveLabelFont != null)
                moveCostLabel.font = cardPreviewUI.MoveLabelFont;
            moveCostLabel.fontSize = 26f;
            moveCostLabel.alignment = TMPro.TextAlignmentOptions.Center;
            moveCostLabel.raycastTarget = false;
        }

        moveCostSlot = slot;
        var rect = moveCostLabel.rectTransform;
        rect.SetParent(slot.transform, false);
        rect.SetAsLastSibling();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(120f, 36f);
        rect.anchoredPosition = new Vector2(0f, 20f);
        moveCostLabel.text = $"{cost} ОД";
        moveCostLabel.gameObject.SetActive(true);
    }

    public void HideMoveCost(BoardSlotView slot = null)
    {
        if (slot != null && slot != moveCostSlot) return;
        if (moveCostLabel != null) moveCostLabel.gameObject.SetActive(false);
        moveCostSlot = null;
    }

    public void TrackMove(BoardPosition from, BoardPosition to, string cardId)
    {
        moveFrom = from;
        moveTo = to;
        movingCardId = cardId;
    }

    private void SetCombatAction(BoardPosition pos, bool move)
    {
        if (currentMode != null) currentMode.OnExit();

        if (move)
        {
            moveController.Activate();
            currentMode = moveController;
            moveController.SelectFromExternal(pos);
        }
        else
        {
            attackController.Activate();
            currentMode = attackController;
            attackController.SelectFromExternal(pos);
        }
    }

    private void ShowCombatPopup(BoardPosition pos, string cardId)
    {
        var slot = GetSlotView(localPlayerId, pos);
        if (slot == null || combatPopupBackground == null) return;
        var canvas = slot.GetComponentInParent<Canvas>();
        if (canvas == null) return;

        if (combatPopup == null)
        {
            var panel = new GameObject("Card Actions", typeof(RectTransform), typeof(Image));
            panel.layer = canvas.gameObject.layer;
            var background = panel.GetComponent<Image>();
            background.sprite = combatPopupBackground;
            combatPopup = background.rectTransform;
            combatPopup.SetParent(canvas.transform, false);
            combatPopup.anchorMin = combatPopup.anchorMax = new Vector2(0.5f, 0.5f);
            combatPopup.sizeDelta = new Vector2(64f, 144f);

            CreateActionButton("Атака", attackIcon, 44f, () => ChoosePopupAction(false));
            CreateActionButton("Перемещение", moveIcon, 0f, () => ChoosePopupAction(true));
            CreateActionButton("Скилл", skillIcon, -44f, null);
        }

        var slotRect = (RectTransform)slot.transform;
        var panelOffset = new Vector3(slotRect.rect.xMax + combatPopup.rect.width / 2f + 8f, 0f);
        combatPopup.position = slotRect.TransformPoint(panelOffset);
        combatPopup.SetAsLastSibling();
        popupPosition = pos;
        popupCardId = cardId;
        combatPopup.gameObject.SetActive(true);
    }

    private void CreateActionButton(string label, Sprite icon, float y, UnityEngine.Events.UnityAction action)
    {
        var buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.layer = combatPopup.gameObject.layer;
        var image = buttonObject.GetComponent<Image>();
        image.sprite = icon;
        image.preserveAspect = true;
        var rect = image.rectTransform;
        rect.SetParent(combatPopup, false);
        rect.sizeDelta = new Vector2(36f, 36f);
        rect.anchoredPosition = new Vector2(0f, y);

        var button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        var colors = button.colors;
        colors.normalColor = new Color(0.78f, 0.78f, 0.78f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.63f, 0.63f, 0.63f);
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.55f);
        colors.colorMultiplier = 1.25f;
        button.colors = colors;
        button.interactable = action != null;
        if (action != null) button.onClick.AddListener(action);
    }

    private void ChoosePopupAction(bool move)
    {
        var card = GetUnitAt(localPlayerId, popupPosition);
        if (TurnManager.Instance == null || TurnManager.Instance.GetCurrentPlayer() != localPlayerId ||
            card == null || card.cardId != popupCardId || card.hasAttackedThisTurn)
        {
            HideCombatPopup();
            return;
        }

        SetCombatAction(popupPosition, move);
    }

    private void HideCombatPopup()
    {
        if (combatPopup != null) combatPopup.gameObject.SetActive(false);
        popupPosition = BoardPosition.None;
        popupCardId = null;
    }

    public void OnCardFromHandSelected(Card card) { if (currentMode != null) currentMode.OnCardFromHandSelected(card); }

    public void SetLocalPlayerId(int id)
    {
        localPlayerId = id;
        if (selfPortraitButton != null) { selfPortraitButton.interactable = false; selfPortraitButton.enabled = false; var outline = selfPortraitButton.GetComponent<Outline>(); if (outline != null) outline.enabled = false; }
        SetPortraitInteractable(false); SetPortraitHighlight(false);
    }

    public void InitializeBoard()
    {
        CreateDefaultSlots(1); CreateDefaultSlots(2);
        if (slotViews != null) foreach (var view in slotViews) view.Initialize(view.position, view.ownerId, this);
        SetLocalPlayerId(localPlayerId);
        if (opponentPortraitButton != null) { opponentPortraitButton.onClick.RemoveAllListeners(); opponentPortraitButton.onClick.AddListener(AttackPlayer); }
    }

    private void CreateDefaultSlots(int playerId)
    {
        BoardPosition[] positions = { BoardPosition.L, BoardPosition.P, BoardPosition.C, BoardPosition.S, BoardPosition.G };
        var slots = new SlotData[5];
        for (int i = 0; i < positions.Length; i++) slots[i] = new SlotData { position = positions[i], currentCard = null, ownerId = playerId };
        if (playerId == 1) player1Slots = slots; else player2Slots = slots;
    }

    public void SwapSlots()
    {
        if (slotsSwapped || slotViews == null) return;
        var player2Slots = new Dictionary<BoardPosition, BoardSlotView>();
        foreach (var view in slotViews) if (view.ownerId == 2) player2Slots[view.position] = view;
        foreach (var view in slotViews)
            if (view.ownerId == 1 && player2Slots.TryGetValue(view.position, out var opposite)) { Vector3 temp = view.transform.position; view.transform.position = opposite.transform.position; opposite.transform.position = temp; }
        slotsSwapped = true;
    }

    public void UpdateBoard(BoardState boardState, ServerGameState.CardHit hit = null)
    {
        if (slotViews == null || cardViewPrefab == null) return;

        if (popupCardId != null)
        {
            var card = boardState.GetUnitAt(localPlayerId, popupPosition);
            if (card == null || card.cardId != popupCardId || card.hasAttackedThisTurn)
                HideCombatPopup();
        }

        FinishAttack();
        StopMoveAnimation();

        bool isNewHit = hit != null && hit.sequence > lastCardHitSequence;
        var attackSource = isNewHit ? GetSlotView(hit.attackerOwner, hit.attackerPosition) : null;
        var attackTarget = isNewHit ? GetSlotView(hit.targetOwner, hit.targetPosition) : null;
        var attackerView = attackSource != null ? attackSource.GetComponentInChildren<CardView>() : null;
        var targetView = attackTarget != null ? attackTarget.GetComponentInChildren<CardView>() : null;
        if (isNewHit) lastCardHitSequence = hit.sequence;

        var movedCard = boardState.GetUnitAt(localPlayerId, moveTo);
        bool animateMove = movingCardId != null && movedCard?.cardId == movingCardId &&
            boardState.GetUnitAt(localPlayerId, moveFrom)?.cardId != movingCardId;
        movingCardId = null;

        Dictionary<string, CardView> existingByCardId = new Dictionary<string, CardView>();
        foreach (var view in slotViews)
        {
            var cv = view.GetComponentInChildren<CardView>();
            if (cv != null && cv.GetCurrentCard() != null)
                existingByCardId[cv.GetCurrentCard().cardId] = cv;
        }

        Dictionary<BoardSlotView, Card> desiredCards = new Dictionary<BoardSlotView, Card>();
        foreach (var slotData in boardState.slots)
        {
            var view = GetSlotView(slotData.ownerId, slotData.position);
            if (view != null) desiredCards[view] = slotData.card;
        }

        HashSet<CardView> usedViews = new HashSet<CardView>();

        foreach (var kvp in desiredCards)
        {
            var slot = kvp.Key;
            var card = kvp.Value;
            if (!existingByCardId.TryGetValue(card.cardId, out var view) || !usedViews.Add(view))
            {
                view = Instantiate(cardViewPrefab, slot.transform);
                SetupCardView(view, card);
                usedViews.Add(view);
                continue;
            }

            if (view.transform.parent != slot.transform)
                view.transform.SetParent(slot.transform, false);
            if (view != raisedCard || view == attackerView || animateMove)
                view.transform.localPosition = Vector3.zero;
            view.DisplayCard(card);
        }

        foreach (var kvp in existingByCardId)
        {
            if (!usedViews.Contains(kvp.Value))
            {
                cardPreviewUI?.HideIfShowing(kvp.Key);
                if (attackerView != null && attackTarget != null && kvp.Value == attackerView)
                    destroyLungingCard = true;
                else if (attackerView != null && attackTarget != null && kvp.Value == targetView)
                    defeatedTarget = targetView;
                else
                {
                    kvp.Value.gameObject.SetActive(false);
                    Destroy(kvp.Value.gameObject);
                }
            }
        }
        moveController.RefreshTargets();
        if (moveCostSlot != null)
        {
            var slot = moveCostSlot;
            HideMoveCost();
            ShowMoveCost(slot);
        }

        if (animateMove)
            AnimateConfirmedMove(boardState, existingByCardId, movedCard.cardId);

        if (!isNewHit) return;
        ResetRaisedCard();
        if (attackerView == null || attackTarget == null)
        {
            ShowImpact(hit);
            return;
        }

        lungingCard = attackerView;
        pendingImpact = hit;
        attackCanvas = attackerView.GetComponent<Canvas>();
        if (attackCanvas == null) attackCanvas = attackerView.gameObject.AddComponent<Canvas>();
        attackCanvas.overrideSorting = true;
        attackCanvas.sortingOrder = 100;
        attackAnimation = StartCoroutine(PlayAttack(attackSource, attackTarget));
    }

    private void AnimateConfirmedMove(BoardState state, Dictionary<string, CardView> views, string cardId)
    {
        if (!views.TryGetValue(cardId, out var mover)) return;
        var from = GetSlotView(localPlayerId, moveFrom).transform.position;
        var to = GetSlotView(localPlayerId, moveTo).transform.position;
        var center = GetSlotView(localPlayerId, BoardPosition.C).transform.position;
        bool viaCenter = moveFrom != BoardPosition.C && moveTo != BoardPosition.C;
        var firstTarget = state.GetUnitAt(localPlayerId, moveFrom);
        CardView firstSwap = null;
        if (firstTarget != null) views.TryGetValue(firstTarget.cardId, out firstSwap);
        var secondTarget = viaCenter ? state.GetUnitAt(localPlayerId, BoardPosition.C) : null;
        CardView secondSwap = null;
        if (secondTarget != null) views.TryGetValue(secondTarget.cardId, out secondSwap);

        mover.transform.position = from;
        animatedCards.Add(mover);
        if (firstSwap != null)
        {
            firstSwap.transform.position = viaCenter ? center : to;
            animatedCards.Add(firstSwap);
        }
        if (secondSwap != null)
        {
            animatedCards.Add(secondSwap);
            secondSwap.transform.position = to;
        }

        moveAnimation = StartCoroutine(PlayMove(mover, firstSwap, secondSwap, from, viaCenter ? center : to, to, viaCenter));
    }

    private System.Collections.IEnumerator PlayMove(CardView mover, CardView firstSwap, CardView secondSwap,
        Vector3 from, Vector3 firstStop, Vector3 to, bool viaCenter)
    {
        yield return AnimateSwap(mover, firstSwap, from, firstStop);
        if (viaCenter) yield return AnimateSwap(mover, secondSwap, firstStop, to);

        moveAnimation = null;
        StopMoveAnimation();
    }

    private static System.Collections.IEnumerator AnimateSwap(CardView mover, CardView target, Vector3 from, Vector3 to)
    {
        const float duration = 0.16f;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            mover.transform.position = Vector3.Lerp(from, to, t);
            if (target != null) target.transform.position = Vector3.Lerp(to, from, t);
            yield return null;
        }
        mover.transform.position = to;
        if (target != null) target.transform.position = from;
    }

    private void StopMoveAnimation()
    {
        if (moveAnimation != null) StopCoroutine(moveAnimation);
        moveAnimation = null;
        foreach (var card in animatedCards)
            if (card != null) card.transform.localPosition = Vector3.zero;
        animatedCards.Clear();
    }

    private void SetupCardView(CardView newCard, Card card)
    {
        newCard.transform.localPosition = Vector3.zero;
        newCard.transform.localRotation = Quaternion.identity;
        newCard.transform.localScale = Vector3.one;
        newCard.DisplayCard(card);
        newCard.SetInteractable(false);
        foreach (var img in newCard.GetComponentsInChildren<UnityEngine.UI.Image>()) img.raycastTarget = false;
        foreach (var txt in newCard.GetComponentsInChildren<TMPro.TextMeshProUGUI>()) txt.raycastTarget = false;
    }

    private BoardSlotView GetSlotView(int playerId, BoardPosition pos) { foreach (var view in slotViews) if (view.ownerId == playerId && view.position == pos) return view; return null; }
    public SlotData[] GetPlayerSlots(int playerId) => playerId == 1 ? player1Slots : player2Slots;
    public SlotData GetSlot(int playerId, BoardPosition pos) { var slots = GetPlayerSlots(playerId); foreach (var slot in slots) if (slot.position == pos) return slot; return null; }
    private void AttackPlayer() { if (currentMode == attackController) attackController.AttackPlayer(); }
    
    public void StartPlacementFromHand(CardView cardView, int playerId)
    {
        if (cardView == null) return;
        Card card = cardView.GetCurrentCard();
        if (card == null || card.category != CardCategory.Unit) return;
        if (playerId != localPlayerId) return;
        StartPlacementMode(card, cardView);
    }
}