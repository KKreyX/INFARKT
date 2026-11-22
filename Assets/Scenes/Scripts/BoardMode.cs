public interface IBoardMode
{
    bool IsActive { get; }
    void OnEnter();
    void OnExit();
    void OnSlotClicked(int playerId, BoardPosition pos);
    void OnCardFromHandSelected(Card card);
    void Update();
    void ClearSelection();
}