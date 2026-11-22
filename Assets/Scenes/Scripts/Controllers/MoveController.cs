public class MoveController : IBoardMode
{
    private readonly BoardManager board;
    private string selectedCardId;

    public bool IsActive { get; private set; }
    public BoardPosition SelectedMover { get; private set; }

    public MoveController(BoardManager boardManager)
    {
        board = boardManager;
    }

    public bool CanActivate() =>
        TurnManager.Instance != null && TurnManager.Instance.GetCurrentPlayer() == board.LocalPlayerId;

    public void Activate()
    {
        if (!CanActivate()) return;
        IsActive = true;
        ClearSelection();
        board.SetHandSacrificeMode(false);
    }

    public void OnEnter() { }
    public void OnExit()
    {
        IsActive = false;
        ClearSelection();
    }

    public void Update() { }
    public void OnCardFromHandSelected(Card card) { }

    public int GetTargetCost(BoardPosition pos)
    {
        if (!IsActive || SelectedMover == BoardPosition.None ||
            board.GetUnitAt(board.LocalPlayerId, SelectedMover)?.cardId != selectedCardId)
            return 0;

        return board.State?.GetMoveCost(board.LocalPlayerId, SelectedMover, pos) ?? 0;
    }

    public bool IsValidTarget(BoardPosition pos) => GetTargetCost(pos) > 0;

    public void OnSlotClicked(int playerId, BoardPosition pos)
    {
        if (!IsActive) return;
        if (playerId == board.LocalPlayerId && IsValidTarget(pos))
        {
            var client = GameClient.Instance;
            if (client != null && client.IsConnected)
            {
                board.TrackMove(SelectedMover, pos, selectedCardId);
                client.SendMoveUnit(selectedCardId, SelectedMover, pos);
            }
        }
        ClearSelection();
    }

    public void SelectFromExternal(BoardPosition pos)
    {
        if (!IsActive) return;
        var card = board.GetUnitAt(board.LocalPlayerId, pos);
        if (card == null || card.hasAttackedThisTurn)
        {
            ClearSelection();
            return;
        }

        SelectedMover = pos;
        selectedCardId = card.cardId;
        RefreshTargets();
    }

    public void RefreshTargets()
    {
        if (!IsActive || SelectedMover == BoardPosition.None) return;
        if (!CanActivate() || board.GetUnitAt(board.LocalPlayerId, SelectedMover)?.cardId != selectedCardId)
        {
            ClearSelection();
            return;
        }

        board.ClearHighlights();
        foreach (var view in board.SlotViews)
        {
            if (view.ownerId == board.LocalPlayerId && IsValidTarget(view.position))
                view.SetHighlightMode(true, board.moveHighlightColor);
        }
        board.HighlightSelectedCard(SelectedMover, board.selectedUnitColor);
    }

    public void ClearSelection()
    {
        SelectedMover = BoardPosition.None;
        selectedCardId = null;
        board.ClearHighlights();
        board.HideMoveCost();
    }
}
