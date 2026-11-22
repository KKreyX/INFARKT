using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class GameClient : MonoBehaviour
{
    public string serverIP = "127.0.0.1";
    public int serverPort = 7777;

    private TcpClient socket;
    private NetworkStream stream;
    private Thread receiveThread;
    private bool isConnected = false;
    private bool isConnecting = false;
    private int playerId = -1;
    private StringBuilder messageBuffer = new StringBuilder();

    private ServerGameState pendingGameState;

    public event Action<int> OnConnected;
    public event Action<string> OnConnectionFailed;
    public event Action OnDisconnected;
    public event Action<NetworkMessage> OnMessageReceived;
    public event Action<ServerGameState> OnGameStateReceived;

    private static GameClient instance;
    public static GameClient Instance => instance;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
    }

    void Start() => MainThreadDispatcher.EnsureCreated();

    public void ConnectToServer(string ip = null)
    {
        if (isConnected || isConnecting) return;
        if (!string.IsNullOrEmpty(ip)) serverIP = ip;
        isConnecting = true;
        Thread connectThread = new Thread(AttemptConnection);
        connectThread.IsBackground = true;
        connectThread.Start();
    }

    private void AttemptConnection()
    {
        try
        {
            socket = new TcpClient();
            socket.Connect(serverIP, serverPort);
            stream = socket.GetStream();
            receiveThread = new Thread(ReceiveData);
            receiveThread.IsBackground = true;
            receiveThread.Start();
            isConnected = true;
            isConnecting = false;
            SendMessage(new NetworkMessage { type = "Connect", data = "{}" });
        }
        catch (Exception ex)
        {
            isConnecting = false;
            EnqueueToMainThread(() => OnConnectionFailed?.Invoke(ex.Message));
        }
    }

    public void SendMessage(NetworkMessage message)
    {
        if (!isConnected || stream == null) return;
        try
        {
            string json = JsonUtility.ToJson(message) + "\n";
            byte[] data = Encoding.UTF8.GetBytes(json);
            stream.Write(data, 0, data.Length);
            stream.Flush();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Ошибка отправки: {ex.Message}");
            EnqueueToMainThread(Disconnect);
        }
    }

    public void SendGameAction(GameActionMessage action)
    {
        SendMessage(new NetworkMessage { type = "GameAction", data = JsonUtility.ToJson(action) });
    }

    public void SendAttack(string cardId, BoardPosition fromPos, BoardPosition targetPos)
    {
        var action = new GameActionMessage
        {
            actionType = "Attack",
            cardId = cardId,
            fromPos = fromPos,
            targetPosition = targetPos
        };
        SendGameAction(action);
    }

    public void SendMoveUnit(string cardId, BoardPosition fromPos, BoardPosition toPos)
    {
        var action = new GameActionMessage
        {
            actionType = "MoveUnit",
            cardId = cardId,
            fromPos = fromPos,
            toPos = toPos
        };
        SendGameAction(action);
    }

    public void SendReviveUnit(string cardId, BoardPosition position)
    {
        var action = new GameActionMessage
        {
            actionType = "ReviveUnit",
            cardId = cardId,
            position = position
        };
        SendGameAction(action);
    }

    public void SendReady(bool isReady)
    {
        SendMessage(new NetworkMessage
        {
            type = "Ready",
            data = JsonUtility.ToJson(new ReadyMessage { playerId = playerId, isReady = isReady })
        });
    }

    public void SendChangeRole(string newRole, int targetSlot)
    {
        SendMessage(new NetworkMessage
        {
            type = "ChangeRole",
            data = JsonUtility.ToJson(new ChangeRoleMessage { playerId = playerId, newRole = newRole, targetSlot = targetSlot })
        });
    }

    public void SendClassAction(string actionType, int classId, int slotIndex)
    {
        SendMessage(new NetworkMessage
        {
            type = "ClassAction",
            data = JsonUtility.ToJson(new ClassActionMessage { playerId = playerId, actionType = actionType, classId = classId, slotIndex = slotIndex })
        });
    }

    private void ReceiveData()
    {
        byte[] buffer = new byte[4096];
        try
        {
            while (isConnected && socket.Connected)
            {
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;
                string receivedData = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                messageBuffer.Append(receivedData);
                string[] messages = messageBuffer.ToString().Split('\n');
                for (int i = 0; i < messages.Length - 1; i++)
                {
                    string msg = messages[i].Trim();
                    if (!string.IsNullOrEmpty(msg))
                        ProcessServerMessage(msg);
                }
                messageBuffer.Clear();
                messageBuffer.Append(messages[messages.Length - 1]);
            }
        }
        catch (Exception ex) { Debug.LogError($"Ошибка приёма: {ex.Message}"); }
        finally { EnqueueToMainThread(HandleDisconnection); }
    }

    private void ProcessServerMessage(string jsonData)
    {
        try
        {
            NetworkMessage message = JsonUtility.FromJson<NetworkMessage>(jsonData);
            EnqueueToMainThread(() => HandleServerMessage(message));
        }
        catch (Exception ex) { Debug.LogError($"Ошибка парсинга: {ex.Message}"); }
    }

    private void HandleServerMessage(NetworkMessage message)
    {
        switch (message.type)
        {
            case "ConnectionResult": HandleConnectionResult(message); break;
            case "LobbyState":
            case "PlayerDisconnected":
            case "StartGame":
                OnMessageReceived?.Invoke(message);
                break;
            case "GameState": HandleGameState(message); break;
        }
    }

    private void HandleConnectionResult(NetworkMessage message)
    {
        var result = JsonUtility.FromJson<ConnectionResultMessage>(message.data);
        if (result.success)
        {
            playerId = result.playerId;
            OnConnected?.Invoke(playerId);
        }
        else OnConnectionFailed?.Invoke(result.message);
    }

    private void HandleGameState(NetworkMessage message)
    {
        try
        {
            var state = JsonUtility.FromJson<ServerGameState>(message.data);
            pendingGameState = state;
            OnGameStateReceived?.Invoke(state);
        }
        catch (Exception ex) { Debug.LogError($"Ошибка десериализации: {ex.Message}"); }
    }

    public ServerGameState GetPendingGameState()
    {
        var state = pendingGameState;
        pendingGameState = null;
        return state;
    }

    private void EnqueueToMainThread(Action action)
    {
        var dispatcher = MainThreadDispatcher.Instance;
        if (dispatcher != null) dispatcher.Enqueue(action);
        else action();
    }

    public void Disconnect()
    {
        if (!isConnected && !isConnecting) return;
        isConnected = false;
        isConnecting = false;
        try { receiveThread?.Interrupt(); stream?.Close(); socket?.Close(); } catch { }
        playerId = -1;
        OnDisconnected?.Invoke();
    }

    private void HandleDisconnection()
    {
        if (isConnected || isConnecting)
        {
            isConnected = false;
            isConnecting = false;
            OnDisconnected?.Invoke();
        }
    }

    void OnDestroy() { Disconnect(); if (instance == this) instance = null; }

    public bool IsConnected => isConnected;
    public bool IsConnecting => isConnecting;
    public int PlayerId => playerId;
}