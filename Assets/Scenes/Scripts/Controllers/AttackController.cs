using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackController : IBoardMode
{
    private BoardManager board;
    private bool isActive = false;
    private BoardPosition selectedAttacker = BoardPosition.None;
    private List<BoardPosition> validTargets = new List<BoardPosition>();

    public bool IsActive => isActive;
    public BoardPosition SelectedAttacker => selectedAttacker;

    public bool IsCardTarget(int ownerId, BoardPosition pos) =>
        isActive && CanActivate() && selectedAttacker != BoardPosition.None &&
        ownerId != board.LocalPlayerId && validTargets.Contains(pos) &&
        board.GetUnitAt(ownerId, pos) != null;

    public AttackController(BoardManager boardManager)
    {
        board = boardManager;
    }

    public bool CanActivate()
    {
        return TurnManager.Instance != null &&
               TurnManager.Instance.GetCurrentPlayer() == board.LocalPlayerId;
    }

    public void Activate()
    {
        if (!CanActivate()) return;
        isActive = true;
        board.ClearHighlights();
        selectedAttacker = BoardPosition.None;
        validTargets.Clear();
        board.SetPortraitInteractable(false);
        board.SetPortraitHighlight(false);
        board.SetHandSacrificeMode(false);
    }

    public void OnEnter() { }
    public void OnExit() => Exit();

    public void Exit()
    {
        isActive = false;
        selectedAttacker = BoardPosition.None;
        validTargets.Clear();
        board.ClearHighlights();
        board.SetPortraitInteractable(false);
        board.SetPortraitHighlight(false);
    }

    public void Update() { }

    public void OnSlotClicked(int playerId, BoardPosition pos)
    {
        if (!isActive) return;

        if (selectedAttacker == BoardPosition.None)
        {
            if (playerId == board.LocalPlayerId)
                SelectAttacker(pos);
        }
        else
        {
            if (playerId != board.LocalPlayerId && validTargets.Contains(pos))
            {
                var attacker = board.GetUnitAt(board.LocalPlayerId, selectedAttacker);
                if (attacker != null)
                {
                    GameClient.Instance.SendAttack(attacker.cardId, selectedAttacker, pos);

                    selectedAttacker = BoardPosition.None;
                    validTargets.Clear();
                    board.ClearHighlights();
                    board.SetPortraitInteractable(false);
                    board.SetPortraitHighlight(false);

                    board.StartCoroutine(ReSelectAfterDelay());
                }
            }
            else if (playerId == board.LocalPlayerId)
            {
                if (pos == selectedAttacker)
                    ClearSelection();
                else
                    SelectAttacker(pos);
            }
            else
            {
                ClearSelection();
            }
        }
    }

    private IEnumerator ReSelectAfterDelay()
    {
        yield return null;
        yield return null;

        if (!isActive) yield break;

        if (selectedAttacker != BoardPosition.None) yield break;

        foreach (var view in board.SlotViews)
        {
            if (view.ownerId != board.LocalPlayerId) continue;
            var unit = board.GetUnitAt(board.LocalPlayerId, view.position);
            if (unit != null && !unit.hasAttackedThisTurn)
            {
                SelectAttacker(view.position);
                break;
            }
        }
    }

    public void SelectFromExternal(BoardPosition pos)
    {
        if (!isActive) return;
        SelectAttacker(pos);
    }

    public void OnCardFromHandSelected(Card card) { }

    private void SelectAttacker(BoardPosition pos)
    {
        var unit = board.GetUnitAt(board.LocalPlayerId, pos);
        if (unit == null || unit.hasAttackedThisTurn) 
        {
            ClearSelection();
            return;
        }

        selectedAttacker = pos;
        validTargets = GetValidAttackTargets(pos, board.LocalPlayerId);
        HighlightValidTargets(validTargets);
        board.HighlightAttackingCard(pos);

        bool canAttackPlayer = validTargets.Contains(BoardPosition.None);
        board.SetPortraitInteractable(canAttackPlayer);
        board.SetPortraitHighlight(canAttackPlayer, board.portraitHighlightColor);
    }

    private List<BoardPosition> GetValidAttackTargets(BoardPosition attackerPos, int ownerId)
    {
        var targets = new List<BoardPosition>();
        int enemyId = (ownerId == 1) ? 2 : 1;

        switch (attackerPos)
        {
            case BoardPosition.L:
                if (board.GetUnitAt(enemyId, BoardPosition.L) != null) targets.Add(BoardPosition.L);
                else
                {
                    targets.Add(BoardPosition.None);
                    if (board.GetUnitAt(enemyId, BoardPosition.C) != null) targets.Add(BoardPosition.C);
                    if (board.GetUnitAt(enemyId, BoardPosition.S) != null) targets.Add(BoardPosition.S);
                }
                break;
            case BoardPosition.P:
                if (board.GetUnitAt(enemyId, BoardPosition.P) != null) targets.Add(BoardPosition.P);
                else
                {
                    targets.Add(BoardPosition.None);
                    if (board.GetUnitAt(enemyId, BoardPosition.C) != null) targets.Add(BoardPosition.C);
                    if (board.GetUnitAt(enemyId, BoardPosition.G) != null) targets.Add(BoardPosition.G);
                }
                break;
            case BoardPosition.C:
                if (board.GetUnitAt(enemyId, BoardPosition.C) != null) targets.Add(BoardPosition.C);
                else
                {
                    targets.Add(BoardPosition.None);
                    if (board.GetUnitAt(enemyId, BoardPosition.L) != null) targets.Add(BoardPosition.L);
                    if (board.GetUnitAt(enemyId, BoardPosition.P) != null) targets.Add(BoardPosition.P);
                    if (board.GetUnitAt(enemyId, BoardPosition.S) != null) targets.Add(BoardPosition.S);
                    if (board.GetUnitAt(enemyId, BoardPosition.G) != null) targets.Add(BoardPosition.G);
                }
                break;
            case BoardPosition.G:
                if (board.GetUnitAt(enemyId, BoardPosition.L) != null) targets.Add(BoardPosition.L);
                if (board.GetUnitAt(enemyId, BoardPosition.P) != null) targets.Add(BoardPosition.P);
                if (board.GetUnitAt(enemyId, BoardPosition.C) != null) targets.Add(BoardPosition.C);
                break;
            case BoardPosition.S:
                break;
        }
        return targets;
    }

    private void HighlightValidTargets(List<BoardPosition> targets)
    {
        board.ClearHighlights();
        int enemyId = (board.LocalPlayerId == 1) ? 2 : 1;
        foreach (var view in board.SlotViews)
        {
            if (view.ownerId == enemyId && targets.Contains(view.position))
                view.SetHighlightMode(true, board.attackHighlightColor);
        }
    }

    public void ClearSelection()
    {
        selectedAttacker = BoardPosition.None;
        validTargets.Clear();
        board.ClearHighlights();
        board.SetPortraitInteractable(false);
        board.SetPortraitHighlight(false);
    }

    public void AttackPlayer()
    {
        if (!isActive || selectedAttacker == BoardPosition.None) return;
        if (!validTargets.Contains(BoardPosition.None)) return;

        var attacker = board.GetUnitAt(board.LocalPlayerId, selectedAttacker);
        if (attacker != null)
        {
            GameClient.Instance.SendAttack(attacker.cardId, selectedAttacker, BoardPosition.None);
            ClearSelection();
        }
    }
}