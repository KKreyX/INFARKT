using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ServerGameState
{
    [System.Serializable]
    public class PlayerState
    {
        public int playerId = 1;
        public int health = 25;
        public int actionPoints = 5;
        public List<Card> hand = new List<Card>();
        public List<Card> deck = new List<Card>();
        public List<Card> graveyard = new List<Card>();
        public List<Card> eternalSleep = new List<Card>();
        public bool attackedThisTurn = false;
        public bool hasResetHand;
        public bool actedThisTurn;

        public bool CanResetHand() => !hasResetHand && !actedThisTurn && hand.Count > 0 && deck.Count > 0;
    }

    public PlayerState player1 = new PlayerState { playerId = 1 };
    public PlayerState player2 = new PlayerState { playerId = 2 };
    [System.Serializable]
    public class CardHit
    {
        public int sequence;
        public int targetOwner;
        public BoardPosition targetPosition;
        public int damage;
        public int attackerOwner;
        public BoardPosition attackerPosition;
        public int counterDamage = -1;
    }

    public BoardState board = new BoardState();
    public CardHit lastCardHit;
    public int currentPlayerId = 1;
    public int round = 0;

    public PlayerState GetPlayer(int playerId) => playerId == 1 ? player1 : player2;
    
    public int GetMoveCost(int playerId, BoardPosition from, BoardPosition to)
    {
        if (currentPlayerId != playerId || from == to ||
            !IsBoardPosition(from) || !IsBoardPosition(to))
            return 0;

        bool viaCenter = from != BoardPosition.C && to != BoardPosition.C;
        int cost = viaCenter ? 2 : 1;
        var card = GetUnitAt(playerId, from);
        if (GetPlayer(playerId).actionPoints < cost || card == null ||
            card.hasAttackedThisTurn || GetUnitAt(playerId, to)?.hasAttackedThisTurn == true ||
            (viaCenter && GetUnitAt(playerId, BoardPosition.C)?.hasAttackedThisTurn == true))
            return 0;

        return cost;
    }

    public bool TryMoveUnit(int playerId, BoardPosition from, BoardPosition to, string cardId)
    {
        var unit = GetUnitAt(playerId, from);
        if (unit == null || unit.cardId != cardId) return false;

        int cost = GetMoveCost(playerId, from, to);
        if (cost == 0) return false;

        if (cost == 2)
        {
            SwapUnits(playerId, from, BoardPosition.C);
            from = BoardPosition.C;
        }

        SwapUnits(playerId, from, to);
        GetPlayer(playerId).actionPoints -= cost;
        return true;
    }

    private static bool IsBoardPosition(BoardPosition position) =>
        position == BoardPosition.L || position == BoardPosition.P || position == BoardPosition.C ||
        position == BoardPosition.S || position == BoardPosition.G;

    private void SwapUnits(int playerId, BoardPosition from, BoardPosition to)
    {
        var mover = GetUnitAt(playerId, from);
        var target = GetUnitAt(playerId, to);
        RemoveUnit(playerId, from);
        RemoveUnit(playerId, to);
        PlaceUnit(playerId, to, mover);
        if (target != null) PlaceUnit(playerId, from, target);
    }

    public bool IsSlotFree(int ownerId, BoardPosition pos) => board.IsSlotFree(ownerId, pos);
    public void PlaceUnit(int ownerId, BoardPosition pos, Card card) => board.PlaceUnit(ownerId, pos, card);
    public Card GetUnitAt(int ownerId, BoardPosition pos) => board.GetUnitAt(ownerId, pos);
    public void RemoveUnit(int ownerId, BoardPosition pos) => board.RemoveUnit(ownerId, pos);
}

[System.Serializable]
public class BoardState
{
    [System.Serializable]
    public class Slot
    {
        public int ownerId;
        public BoardPosition position;
        public Card card;
    }

    public List<Slot> slots = new List<Slot>();

    public bool IsSlotFree(int ownerId, BoardPosition pos)
    {
        return !slots.Exists(s => s.ownerId == ownerId && s.position == pos);
    }

    public void PlaceUnit(int ownerId, BoardPosition pos, Card card)
    {
        if (IsSlotFree(ownerId, pos))
        {
            card.ownerId = ownerId;
            slots.Add(new Slot { ownerId = ownerId, position = pos, card = card });
        }
    }

    public void RemoveUnit(int ownerId, BoardPosition pos)
    {
        slots.RemoveAll(s => s.ownerId == ownerId && s.position == pos);
    }

    public Card GetUnitAt(int ownerId, BoardPosition pos)
    {
        var slot = slots.Find(s => s.ownerId == ownerId && s.position == pos);
        return slot?.card;
    }
}