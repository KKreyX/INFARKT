using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class GameServer : MonoBehaviour
{
    public int port = 7777;

    private TcpListener tcpListener;
    private Thread listenerThread;
    private bool isRunning = false;
    private readonly object stateLock = new object();

    private Dictionary<int, ClientHandler> clients = new Dictionary<int, ClientHandler>();
    private int nextClientId = 0;

    private LobbyGameState lobbyState = new LobbyGameState();
    private Dictionary<int, PlayerState> playerStates = new Dictionary<int, PlayerState>();

    public ServerGameState gameState;
    private Dictionary<int, ClassConfig> classConfigs = new Dictionary<int, ClassConfig>();

    private static GameServer instance;
    public static GameServer Instance => instance;

    void Awake()
    {
        if (instance == null) { instance = this; DontDestroyOnLoad(gameObject); }
        else Destroy(gameObject);
        LoadClassConfigs();
    }

    private void LoadClassConfigs()
    {
        classConfigs.Clear();
        if (CardLibrary.Instance == null) return;
        foreach (var config in CardLibrary.Instance.classConfigs)
            if (config != null) classConfigs[config.classId] = config;
    }

    public void StartServer()
    {
        if (isRunning) return;
        try
        {
            tcpListener = new TcpListener(IPAddress.Any, port);
            tcpListener.Start();
            listenerThread = new Thread(ListenForClients);
            listenerThread.IsBackground = true;
            listenerThread.Start();
            isRunning = true;
            lock (stateLock) { lobbyState.playerExists[0] = true; }
        }
        catch (Exception ex) { Debug.LogError($"Ошибка запуска сервера: {ex.Message}"); }
    }

    public void StopServer()
    {
        if (!isRunning) return;
        isRunning = false;
        tcpListener?.Stop();
        lock (stateLock)
        {
            var clientsCopy = new List<ClientHandler>(clients.Values);
            foreach (var client in clientsCopy) client.Disconnect();
            clients.Clear();
            playerStates.Clear();
        }
    }

    private void ListenForClients()
    {
        while (isRunning)
        {
            try
            {
                TcpClient client = tcpListener.AcceptTcpClient();
                int clientId = nextClientId++;
                var handler = new ClientHandler(clientId, client, this);
                var thread = new Thread(handler.Run);
                thread.IsBackground = true;
                thread.Start();

                lock (stateLock)
                {
                    clients[clientId] = handler;
                    if (clients.Count == 1)
                        playerStates[clientId] = new PlayerState { PlayerId = clientId, Role = "player", RoleSlot = 0 };
                    else
                        playerStates[clientId] = new PlayerState { PlayerId = clientId, Role = "", RoleSlot = -1 };
                }
                SendLobbyStateToClient(clientId);
            }
            catch (Exception ex) { Debug.LogError($"Ошибка при приёме клиента: {ex.Message}"); break; }
        }
    }

    public void ProcessClientMessage(int clientId, NetworkMessage message)
    {
        lock (stateLock)
        {
            switch (message.type)
            {
                case "Connect": HandleConnect(clientId); break;
                case "Ready": HandleReady(clientId, message); break;
                case "ChangeRole": HandleChangeRole(clientId, message); break;
                case "ClassAction": HandleClassAction(clientId, message); break;
                case "GameAction": HandleGameAction(clientId, message); break;
            }
        }
    }

    private void HandleConnect(int clientId)
    {
        SendToClient(clientId, new NetworkMessage
        {
            type = "ConnectionResult",
            data = JsonUtility.ToJson(new ConnectionResultMessage { success = true, playerId = clientId })
        });
    }

    private void HandleReady(int clientId, NetworkMessage message)
    {
        var readyData = JsonUtility.FromJson<ReadyMessage>(message.data);
        if (!playerStates.ContainsKey(clientId) || playerStates[clientId].Role != "player") return;
        int slot = playerStates[clientId].RoleSlot;
        if (slot < 0 || slot >= 2) return;

        lobbyState.playerReady[slot] = readyData.isReady;
        playerStates[clientId].IsReady = readyData.isReady;
        BroadcastLobbyState();

        if (lobbyState.playerReady[0] && lobbyState.playerReady[1])
        {
            if (lobbyState.player0Classes[0] == -1) { lobbyState.player0Classes[0] = 0; lobbyState.player0Classes[1] = 1; }
            if (lobbyState.player1Classes[0] == -1) { lobbyState.player1Classes[0] = 2; lobbyState.player1Classes[1] = 3; }
            StartGame();
        }
    }

    private void HandleChangeRole(int clientId, NetworkMessage message)
    {
        var roleData = JsonUtility.FromJson<ChangeRoleMessage>(message.data);
        if (!playerStates.ContainsKey(clientId)) return;

        int oldSlot = playerStates[clientId].RoleSlot;
        if (oldSlot >= 0 && oldSlot < 2)
        {
            lobbyState.playerExists[oldSlot] = false;
            lobbyState.playerReady[oldSlot] = false;
        }
        if (roleData.targetSlot >= 0 && roleData.targetSlot < 2 && !lobbyState.playerExists[roleData.targetSlot])
        {
            playerStates[clientId].Role = "player";
            playerStates[clientId].RoleSlot = roleData.targetSlot;
            lobbyState.playerExists[roleData.targetSlot] = true;
        }
        BroadcastLobbyState();
    }

    private void HandleClassAction(int clientId, NetworkMessage message) { }

    private void StartGame()
    {
        foreach (var kvp in clients)
        {
            int playerId = kvp.Key;
            bool isPlayerOne = playerStates[playerId].RoleSlot == 0;
            int[] playerClasses = isPlayerOne ? lobbyState.player0Classes : lobbyState.player1Classes;
            int[] opponentClasses = isPlayerOne ? lobbyState.player1Classes : lobbyState.player0Classes;
            SendToClient(playerId, new NetworkMessage
            {
                type = "StartGame",
                data = JsonUtility.ToJson(new StartGameMessage
                {
                    playerNumber = isPlayerOne ? 0 : 1,
                    playerClasses = playerClasses,
                    opponentClasses = opponentClasses
                })
            });
        }

        gameState = BuildGameState();
        if (gameState != null)
            BroadcastGameState();
    }

    private ServerGameState BuildGameState()
    {
        var state = new ServerGameState();
        var p1Deck = GenerateDeck(lobbyState.player0Classes);
        var p2Deck = GenerateDeck(lobbyState.player1Classes);
        ShuffleDeck(p1Deck); ShuffleDeck(p2Deck);

        state.player1.hand = DrawCards(p1Deck, 5);
        state.player2.hand = DrawCards(p2Deck, 5);
        state.player1.deck = p1Deck;
        state.player2.deck = p2Deck;
        state.player1.health = 25;
        state.player1.actionPoints = 5;
        state.player2.health = 25;
        state.player2.actionPoints = 5;
        state.currentPlayerId = 1;
        state.round = 0;
        return state;
    }

    private List<Card> GenerateDeck(int[] classIds)
    {
        var deck = new List<Card>();
        if (classConfigs == null) return deck;
        foreach (int classId in classIds)
            if (classConfigs.TryGetValue(classId, out var config))
                deck.AddRange(config.GenerateAllCards());
        return deck;
    }

    private void ShuffleDeck(List<Card> deck)
    {
        System.Random rng = new System.Random();
        int n = deck.Count;
        while (n > 1) { n--; int k = rng.Next(n + 1); Card value = deck[k]; deck[k] = deck[n]; deck[n] = value; }
    }

    private List<Card> DrawCards(List<Card> deck, int count)
    {
        var drawn = new List<Card>();
        for (int i = 0; i < count && deck.Count > 0; i++)
        {
            drawn.Add(deck[0]);
            deck.RemoveAt(0);
        }
        return drawn;
    }

    private void BroadcastGameState()
    {
        if (gameState == null) return;
        string json = JsonUtility.ToJson(gameState);
        var msg = new NetworkMessage { type = "GameState", data = json };
        foreach (var kvp in clients) SendToClient(kvp.Key, msg);
    }

    private void HandleGameAction(int clientId, NetworkMessage message)
    {
        if (gameState == null) return;

        var action = JsonUtility.FromJson<GameActionMessage>(message.data);
        if (!playerStates.ContainsKey(clientId)) return;

        int playerSlot = playerStates[clientId].RoleSlot;
        if (playerSlot < 0 || playerSlot > 1) return;
        int playerId = playerSlot + 1;

        if (playerId != gameState.currentPlayerId) return;

        var player = gameState.GetPlayer(playerId);
        if (player == null) return;

        bool success = false;

        switch (action.actionType)
        {
            case "PlaceUnit": success = HandlePlaceUnit(playerId, player, action); break;
            case "SacrificeForSpecial": success = HandleSacrificeForSpecial(playerId, player, action); break;
            case "ReviveUnit": success = HandleReviveUnit(playerId, player, action); break;
            case "ResetHand": success = HandleResetHand(player); break;
            case "EndTurn": success = HandleEndTurn(playerId); break;
            case "Attack": success = HandleAttack(playerId, action); break;
            case "MoveUnit": success = HandleMoveUnit(playerId, action); break;
        }

        if (success)
        {
            if (action.actionType != "EndTurn" && action.actionType != "ResetHand")
                player.actedThisTurn = true;
            BroadcastGameState();
        }
    }

    private bool HandleResetHand(ServerGameState.PlayerState player)
    {
        if (!player.CanResetHand()) return false;

        var oldHand = player.hand;
        player.hand = DrawCards(player.deck, oldHand.Count);
        player.deck.AddRange(oldHand);
        ShuffleDeck(player.deck);
        player.hasResetHand = true;
        return true;
    }

    private bool HandlePlaceUnit(int playerId, ServerGameState.PlayerState player, GameActionMessage action)
    {
        var card = player.hand.Find(c => c.cardId == action.cardId);
        if (card == null) return false;

        if (card.requiresSacrifice && !card.isSacrificePaid) return false;
        int costToPay = card.isSacrificePaid ? 0 : card.actionPointCost;
        if (player.actionPoints < costToPay) return false;

        if (!gameState.IsSlotFree(playerId, action.position)) return false;
        
        gameState.PlaceUnit(playerId, action.position, card);
        player.hand.Remove(card);
        player.actionPoints -= costToPay;
        return true;
    }

    private bool HandleSacrificeForSpecial(int playerId, ServerGameState.PlayerState player, GameActionMessage action)
    {
        var card = player.hand.Find(c => c.cardId == action.cardId);
        if (card == null || !card.requiresSacrifice || card.isSacrificePaid) return false;

        int apCost = card.sacrificeAPCost;
        if (player.actionPoints < apCost) return false;
        if (action.sacrificeIds == null || action.sacrificeIds.Length != 2) return false;

        Card normalSac = null;
        Card specialSac = null;

        foreach (string id in action.sacrificeIds)
        {
            Card unitOnBoard = null;
            foreach (var slot in gameState.board.slots)
                if (slot.ownerId == playerId && slot.card != null && slot.card.cardId == id) unitOnBoard = slot.card;
                
            Card unitInHand = player.hand.Find(c => c.cardId == id);
            Card found = unitOnBoard ?? unitInHand;
            
            // ФИКС: приносить в жертву можно только юнитов
            if (found == null || found.category != CardCategory.Unit) return false;

            if (found.cardType == CardType.Number) normalSac = found;
            else specialSac = found;
        }

        if (normalSac == null || specialSac == null) return false;

        foreach (string id in action.sacrificeIds)
        {
            bool removedFromBoard = false;
            foreach (var slot in gameState.board.slots)
            {
                if (slot.ownerId == playerId && slot.card != null && slot.card.cardId == id)
                {
                    gameState.RemoveUnit(playerId, slot.position);
                    SendToEternalSleep(player, slot.card, 5);
                    removedFromBoard = true;
                    break;
                }
            }
            if (!removedFromBoard)
            {
                var toRemove = player.hand.Find(c => c.cardId == id);
                if (toRemove != null) 
                {
                    player.hand.Remove(toRemove);
                    SendToEternalSleep(player, toRemove, 5);
                }
            }
        }

        player.actionPoints -= apCost;
        card.isSacrificePaid = true;
        return true;
    }

    private bool HandleReviveUnit(int playerId, ServerGameState.PlayerState player, GameActionMessage action)
    {
        var card = player.eternalSleep.Find(c => c.cardId == action.cardId);
        if (card == null || card.sleepTimer > 0) return false;

        int apCost = (card.cardType == CardType.K || card.cardType == CardType.T) ? 3 : 2;
        if (player.actionPoints < apCost) return false;
        if (!gameState.IsSlotFree(playerId, action.position)) return false;

        card.health = card.maxHealth;
        card.attack = card.maxAttack;
        card.hasAttackedThisTurn = true;
        
        player.eternalSleep.Remove(card);
        gameState.PlaceUnit(playerId, action.position, card);
        player.actionPoints -= apCost;
        return true;
    }

    private void SendToEternalSleep(ServerGameState.PlayerState player, Card card, int turns)
    {
        if (card.cardType == CardType.Number) return;
        card.sleepTimer = turns;
        player.eternalSleep.Add(card);
    }

    private List<BoardPosition> GetValidAttackTargets(BoardPosition attackerPos, int attackerOwnerId)
    {
        var targets = new List<BoardPosition>();
        int enemyId = (attackerOwnerId == 1) ? 2 : 1;

        switch (attackerPos)
        {
            case BoardPosition.L:
                if (gameState.GetUnitAt(enemyId, BoardPosition.L) != null)
                    targets.Add(BoardPosition.L);
                else
                {
                    targets.Add(BoardPosition.None);
                    if (gameState.GetUnitAt(enemyId, BoardPosition.C) != null) targets.Add(BoardPosition.C);
                    if (gameState.GetUnitAt(enemyId, BoardPosition.S) != null) targets.Add(BoardPosition.S);
                }
                break;
            case BoardPosition.P:
                if (gameState.GetUnitAt(enemyId, BoardPosition.P) != null)
                    targets.Add(BoardPosition.P);
                else
                {
                    targets.Add(BoardPosition.None);
                    if (gameState.GetUnitAt(enemyId, BoardPosition.C) != null) targets.Add(BoardPosition.C);
                    if (gameState.GetUnitAt(enemyId, BoardPosition.G) != null) targets.Add(BoardPosition.G);
                }
                break;
            case BoardPosition.C:
                if (gameState.GetUnitAt(enemyId, BoardPosition.C) != null)
                    targets.Add(BoardPosition.C);
                else
                {
                    targets.Add(BoardPosition.None);
                    if (gameState.GetUnitAt(enemyId, BoardPosition.L) != null) targets.Add(BoardPosition.L);
                    if (gameState.GetUnitAt(enemyId, BoardPosition.P) != null) targets.Add(BoardPosition.P);
                    if (gameState.GetUnitAt(enemyId, BoardPosition.S) != null) targets.Add(BoardPosition.S);
                    if (gameState.GetUnitAt(enemyId, BoardPosition.G) != null) targets.Add(BoardPosition.G);
                }
                break;
            case BoardPosition.G:
                if (gameState.GetUnitAt(enemyId, BoardPosition.L) != null) targets.Add(BoardPosition.L);
                if (gameState.GetUnitAt(enemyId, BoardPosition.P) != null) targets.Add(BoardPosition.P);
                if (gameState.GetUnitAt(enemyId, BoardPosition.C) != null) targets.Add(BoardPosition.C);
                break;
            case BoardPosition.S:
                break;
        }
        return targets;
    }

    private bool HandleAttack(int playerId, GameActionMessage action)
    {
        var player = gameState.GetPlayer(playerId);
        if (player == null) return false;

        var attacker = gameState.GetUnitAt(playerId, action.fromPos);
        if (attacker == null || attacker.hasAttackedThisTurn) return false;
        if (player.actionPoints < 1) return false;

        var validTargets = GetValidAttackTargets(action.fromPos, playerId);
        if (!validTargets.Contains(action.targetPosition)) return false;

        int enemyId = (playerId == 1) ? 2 : 1;
        var enemyPlayer = gameState.GetPlayer(enemyId);

        if (action.targetPosition == BoardPosition.None)
        {
            int playerDamage = Mathf.Max(0, attacker.attack);
            enemyPlayer.health -= playerDamage;
            if (enemyPlayer.health < 0) enemyPlayer.health = 0;

            player.actionPoints -= 1;
            attacker.hasAttackedThisTurn = true;
            player.attackedThisTurn = true;
            return true;
        }

        var targetUnit = gameState.GetUnitAt(enemyId, action.targetPosition);
        if (targetUnit == null) return false;

        int armor = (attacker.attackType == AttackType.Physical) ? targetUnit.armorPhysical : targetUnit.armorMagic;
        int attackDamage = Mathf.Max(0, attacker.attack - armor);
        targetUnit.health -= attackDamage;
        int overkill = 0;
        bool targetDied = false;

        if (targetUnit.health <= 0)
        {
            overkill = -targetUnit.health;
            targetDied = true;
            targetUnit.health = 0;
        }

        gameState.lastCardHit = new ServerGameState.CardHit
        {
            sequence = (gameState.lastCardHit?.sequence ?? 0) + 1,
            targetOwner = enemyId,
            targetPosition = action.targetPosition,
            damage = attackDamage,
            attackerOwner = playerId,
            attackerPosition = action.fromPos,
            counterDamage = -1
        };

        bool counterAttack = (!targetDied || overkill < 2);
        if (counterAttack)
        {
            int counterArmor = (targetUnit.attackType == AttackType.Physical) ? attacker.armorPhysical : attacker.armorMagic;
            int counterDamage = Mathf.Max(0, targetUnit.attack - counterArmor);
            gameState.lastCardHit.counterDamage = counterDamage;
            attacker.health -= counterDamage;
            if (attacker.health < 0) attacker.health = 0;
        }

        if (targetDied)
        {
            enemyPlayer.health -= overkill;
            if (enemyPlayer.health < 0) enemyPlayer.health = 0;
            gameState.RemoveUnit(enemyId, action.targetPosition);
            int turns = (targetUnit.cardType == CardType.K || targetUnit.cardType == CardType.T) ? 4 : 3;
            SendToEternalSleep(enemyPlayer, targetUnit, turns);
        }

        if (attacker.health <= 0)
        {
            attacker.health = 0;
            gameState.RemoveUnit(playerId, action.fromPos);
            int turns = (attacker.cardType == CardType.K || attacker.cardType == CardType.T) ? 4 : 3;
            SendToEternalSleep(player, attacker, turns);
        }

        player.actionPoints -= 1;
        attacker.hasAttackedThisTurn = true;
        player.attackedThisTurn = true;
        return true;
    }

    private bool HandleEndTurn(int playerId)
    {
        int nextPlayer = (gameState.currentPlayerId == 1) ? 2 : 1;
        gameState.currentPlayerId = nextPlayer;

        if (nextPlayer == 1) gameState.round++;

        var next = gameState.GetPlayer(nextPlayer);
        if (next != null)
        {
            next.actionPoints = 5;
            next.actedThisTurn = false;
            next.attackedThisTurn = false;
        }

        foreach (var slot in gameState.board.slots)
            if (slot.card != null)
                slot.card.hasAttackedThisTurn = false;

        if (gameState.round > 0 && next != null && next.deck.Count > 0)
        {
            var drawn = next.deck[0];
            next.deck.RemoveAt(0);
            next.hand.Add(drawn);
        }

        foreach (var p in new[] { gameState.player1, gameState.player2 })
        {
            foreach (var card in p.eternalSleep)
            {
                if (card.sleepTimer > 0) card.sleepTimer--;
            }
        }

        return true;
    }

    private bool HandleMoveUnit(int playerId, GameActionMessage action) =>
        gameState.TryMoveUnit(playerId, action.fromPos, action.toPos, action.cardId);

    private void SendLobbyStateToClient(int clientId)
    {
        if (playerStates.ContainsKey(clientId))
        {
            SendToClient(clientId, new NetworkMessage
            {
                type = "LobbyState",
                data = JsonUtility.ToJson(new LobbyStateMessage
                {
                    gameState = lobbyState.Clone(),
                    currentPlayerSlot = playerStates[clientId].RoleSlot
                })
            });
        }
    }

    private void BroadcastLobbyState()
    {
        foreach (var kvp in clients)
            SendLobbyStateToClient(kvp.Key);
    }

    private void SendToClient(int clientId, NetworkMessage message)
    {
        if (clients.ContainsKey(clientId))
            clients[clientId].SendMessage(message);
    }

    public void HandlePlayerDisconnected(int clientId)
    {
        lock (stateLock)
        {
            if (clients.ContainsKey(clientId))
            {
                clients.Remove(clientId);
                if (playerStates.ContainsKey(clientId))
                {
                    int slot = playerStates[clientId].RoleSlot;
                    if (slot >= 0 && slot < 2)
                    {
                        lobbyState.playerExists[slot] = false;
                        lobbyState.playerReady[slot] = false;
                    }
                    playerStates.Remove(clientId);
                }
                BroadcastLobbyState();
            }
        }
    }

    void OnDestroy() { StopServer(); if (instance == this) instance = null; }

    public class PlayerState
    {
        public int PlayerId;
        public bool IsReady;
        public string Role;
        public int RoleSlot;
    }
}

public class ClientHandler
{
    private readonly int clientId;
    private readonly TcpClient tcpClient;
    private readonly NetworkStream stream;
    private readonly GameServer server;

    public ClientHandler(int id, TcpClient client, GameServer server)
    {
        clientId = id;
        tcpClient = client;
        stream = client.GetStream();
        this.server = server;
    }

    public void Run()
    {
        byte[] buffer = new byte[8192];
        StringBuilder messageBuffer = new StringBuilder();
        try
        {
            while (true)
            {
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;
                
                string data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                messageBuffer.Append(data);

                string all = messageBuffer.ToString();
                string[] messages = all.Split('\n');

                for (int i = 0; i < messages.Length - 1; i++)
                {
                    string trimmed = messages[i].Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        try
                        {
                            var msg = JsonUtility.FromJson<NetworkMessage>(trimmed);
                            if (msg != null) server.ProcessClientMessage(clientId, msg);
                        }
                        catch (Exception ex) 
                        { 
                            Debug.LogError($"[Server] Ошибка парсинга: {ex.Message}"); 
                        }
                    }
                }

                messageBuffer.Clear();
                messageBuffer.Append(messages[messages.Length - 1]);
            }
        }
        catch { }
        finally { Disconnect(); }
    }

    public void SendMessage(NetworkMessage message)
    {
        try
        {
            string json = JsonUtility.ToJson(message) + "\n";
            byte[] data = Encoding.UTF8.GetBytes(json);
            stream.Write(data, 0, data.Length);
            stream.Flush();
        }
        catch { Disconnect(); }
    }

    public void Disconnect()
    {
        try { stream?.Close(); tcpClient?.Close(); } catch { }
        server.HandlePlayerDisconnected(clientId);
    }
}