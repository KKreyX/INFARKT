using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class NetworkMessage
{
    public string type;
    public string data;
}

[Serializable]
public class LobbyGameState
{
    public bool[] playerExists = new bool[2];
    public bool[] playerReady = new bool[2];
    public string classSelectionPhase = "waiting";
    public int[] bannedClasses = new int[4] { -1, -1, -1, -1 };
    public int[] player0Classes = new int[2] { -1, -1 };
    public int[] player1Classes = new int[2] { -1, -1 };

    public LobbyGameState Clone()
    {
        return new LobbyGameState
        {
            playerExists = (bool[])playerExists.Clone(),
            playerReady = (bool[])playerReady.Clone(),
            classSelectionPhase = classSelectionPhase,
            bannedClasses = (int[])bannedClasses.Clone(),
            player0Classes = (int[])player0Classes.Clone(),
            player1Classes = (int[])player1Classes.Clone()
        };
    }
}

[Serializable]
public class LobbyStateMessage
{
    public LobbyGameState gameState;
    public int currentPlayerSlot;
}

[Serializable]
public class ConnectionResultMessage
{
    public bool success;
    public string message;
    public int playerId;
}

[Serializable]
public class ReadyMessage
{
    public int playerId;
    public bool isReady;
}

[Serializable]
public class ChangeRoleMessage
{
    public int playerId;
    public string newRole;
    public int targetSlot;
}

[Serializable]
public class ClassActionMessage
{
    public int playerId;
    public string actionType;
    public int classId;
    public int slotIndex;
}

[Serializable]
public class StartGameMessage
{
    public int playerNumber;
    public int[] playerClasses = new int[2];
    public int[] opponentClasses = new int[2];
}

[Serializable]
public class GameActionMessage
{
    public string actionType;
    public string cardId;
    public string sacrificeCardId; // для обратной совместимости (не используется)
    public BoardPosition position;
    public BoardPosition fromPos;
    public BoardPosition toPos;
    public BoardPosition targetPosition;
    public string[] sacrificeIds; // НОВОЕ: массив ID жертв
}