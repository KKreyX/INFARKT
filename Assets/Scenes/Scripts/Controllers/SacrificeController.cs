using System.Collections.Generic;
using UnityEngine;

public class SacrificeController : IBoardMode
{
    private BoardManager board;
    private SacrificeConfirmationUI ui;
    private bool isActive = false;

    private Card pendingCard;
    private Card selectedNormalUnit;
    private Card selectedSpecialUnit;

    public bool IsActive => isActive;
    public int ActionPointCost => pendingCard?.sacrificeAPCost ?? 0;

    public SacrificeController(BoardManager boardManager, SacrificeConfirmationUI confirmationUI)
    {
        board = boardManager;
        ui = confirmationUI;
    }

    public void Activate(Card card)
    {
        if (card == null) return;
        isActive = true;
        pendingCard = card;
        selectedNormalUnit = null;
        selectedSpecialUnit = null;

        board.ClearHighlights();
        
        if (ui != null)
        {
            ui.Show(pendingCard, pendingCard.sacrificeAPCost);
            ui.OnConfirmed += OnConfirmed;
            ui.OnCancelled += OnCancelled;
            ui.OnSlotCardClicked += OnSlotCardClicked;
        }

        UpdateHighlights();
    }

    public void OnEnter() { }
    public void OnExit() => Cancel();

    public void Cancel()
    {
        if (!isActive) return;
        isActive = false;
        pendingCard = null;
        selectedNormalUnit = null;
        selectedSpecialUnit = null;
        
        board.ClearHighlights();
        board.SetHandSacrificeMode(false);
        
        if (ui != null) 
        { 
            ui.Hide(); 
            ui.OnConfirmed -= OnConfirmed; 
            ui.OnCancelled -= OnCancelled; 
            ui.OnSlotCardClicked -= OnSlotCardClicked;
        }
        board.PlayerHand?.CancelSelection(); 
    }

    public void ClearSelection()
    {
        selectedNormalUnit = null;
        selectedSpecialUnit = null;

        if (ui != null)
        {
            ui.ClearSlots();
        }

        UpdateHighlights();
    }

    public void Update() { }

    public void OnSlotClicked(int playerId, BoardPosition pos)
    {
        if (!isActive || playerId != board.LocalPlayerId) return;
        var unit = board.GetUnitAt(board.LocalPlayerId, pos);
        if (unit == null || unit.category != CardCategory.Unit) return; // ФИКС
        
        if (unit.cardType == CardType.Number && selectedNormalUnit != null) return;
        if (unit.cardType != CardType.Number && selectedSpecialUnit != null) return;

        TrySelectSacrifice(unit);
    }

    public void OnCardFromHandSelected(Card card)
    {
        if (!isActive || card == pendingCard) return;
        if (card.category != CardCategory.Unit) return; // ФИКС
        
        if (card.cardType == CardType.Number && selectedNormalUnit != null) return;
        if (card.cardType != CardType.Number && selectedSpecialUnit != null) return;

        TrySelectSacrifice(card);
    }

    private void TrySelectSacrifice(Card card)
    {
        if (card.category != CardCategory.Unit) return; // ФИКС

        if (card.cardType == CardType.Number)
        {
            if (selectedNormalUnit == null)
            {
                selectedNormalUnit = card;
                if (ui != null) ui.SetCardInSlot(card, true);
            }
        }
        else
        {
            if (selectedSpecialUnit == null)
            {
                selectedSpecialUnit = card;
                if (ui != null) ui.SetCardInSlot(card, false);
            }
        }

        UpdateHighlights();
    }

    private void OnSlotCardClicked(bool isRegular)
    {
        if (isRegular) selectedNormalUnit = null;
        else selectedSpecialUnit = null;

        if (ui != null)
        {
            ui.ClearSlots();
            if (selectedNormalUnit != null) ui.SetCardInSlot(selectedNormalUnit, true);
            if (selectedSpecialUnit != null) ui.SetCardInSlot(selectedSpecialUnit, false);
        }

        UpdateHighlights();
    }

    private void UpdateHighlights()
    {
        board.ClearHighlights();

        bool needNormal = selectedNormalUnit == null;
        bool needSpecial = selectedSpecialUnit == null;

        foreach (var view in board.SlotViews)
        {
            if (view.ownerId != board.LocalPlayerId) continue;
            var unit = board.GetUnitAt(board.LocalPlayerId, view.position);
            
            if (unit != null && unit.category == CardCategory.Unit) // ФИКС
            {
                if (unit.cardType == CardType.Number && needNormal)
                    view.SetHighlightMode(true, board.sacrificeHighlightColor);
                else if (unit.cardType != CardType.Number && needSpecial)
                    view.SetHighlightMode(true, board.sacrificeHighlightColor);
            }
        }

        board.SetHandSacrificeMode(true, needNormal, needSpecial, board.sacrificeHighlightColor);
    }

    private void OnConfirmed()
    {
        if (pendingCard != null && selectedNormalUnit != null && selectedSpecialUnit != null)
        {
            string[] sacrificeIds = { selectedNormalUnit.cardId, selectedSpecialUnit.cardId };
            var action = new GameActionMessage
            {
                actionType = "SacrificeForSpecial",
                cardId = pendingCard.cardId,
                sacrificeIds = sacrificeIds
            };
            GameClient.Instance.SendGameAction(action);
        }

        isActive = false;
        if (ui != null) 
        { 
            ui.Hide(); 
            ui.OnConfirmed -= OnConfirmed; 
            ui.OnCancelled -= OnCancelled; 
            ui.OnSlotCardClicked -= OnSlotCardClicked;
        }
        board.ClearHighlights();
        board.SetHandSacrificeMode(false);
    }

    private void OnCancelled()
    {
        Cancel();
    }
}