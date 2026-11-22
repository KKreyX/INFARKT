using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class LobbyManager : MonoBehaviour
{
    [Header("Role Buttons")]
    public RoleButton player1Button;
    public RoleButton player2Button;
    
    [Header("Class Buttons")]
    public ClassButton[] classButtons;
    
    [Header("Ready Buttons")]
    public Button readyButtonPlayer1;
    public Button readyButtonPlayer2;
    public TextMeshProUGUI readyStatusTextPlayer1;
    public TextMeshProUGUI readyStatusTextPlayer2;
    
    [Header("UI Elements")]
    public Button exitButton;
    public TextMeshProUGUI phaseText;
    public TextMeshProUGUI selectedClassesText;
    public TextMeshProUGUI connectionInfoText;
    public TextMeshProUGUI bannedClassesText; // можно оставить, но обновление закомментировано

    private LobbyGameState gameState = new LobbyGameState();
    private GameClient gameClient;
    private int currentPlayerSlot = -1;
    private string[] classNames;

    void Start()
    {
        gameClient = GameClient.Instance;
        if (gameClient == null)
        {
            Debug.LogError("GameClient not found!");
            return;
        }

        gameClient.OnMessageReceived += OnNetworkMessage;
        gameClient.OnConnected += OnConnected;
        gameClient.OnDisconnected += OnDisconnectedFromServer;

        InitializeClassNames();
        InitializeUI();
        Debug.Log("LobbyManager запущен");
    }

    private void InitializeClassNames()
    {
        classNames = new string[]
        {
            "Чародеи", "Воры Мэла", "Одиночество Анаэль", "Сад Авеля", "Кровавый закат", "Рыцари",
            "Лесные", "Мертвые", "Огненные", "Морские", "Бродячие артисты", "Водные", "Художник",
            "Алхимик", "Мутанты", "Светлые", "Темные", "Фехтовальщики", "Горные", "Маги",
            "Дети леса", "Морозные", "Жрецы Исаи", "Клирики"
        };
    }

    private void InitializeUI()
    {
        if (player1Button != null)
            player1Button.Initialize(0, "player", OnRoleButtonClicked);
        if (player2Button != null)
            player2Button.Initialize(1, "player", OnRoleButtonClicked);

        if (classButtons != null)
        {
            for (int i = 0; i < Mathf.Min(classButtons.Length, classNames.Length); i++)
            {
                if (classButtons[i] != null)
                {
                    classButtons[i].Initialize(i, classNames[i], OnClassButtonClicked);
                }
            }
        }

        if (readyButtonPlayer1 != null)
            readyButtonPlayer1.onClick.AddListener(() => OnReadyButtonClicked(0));
        if (readyButtonPlayer2 != null)
            readyButtonPlayer2.onClick.AddListener(() => OnReadyButtonClicked(1));

        if (exitButton != null)
            exitButton.onClick.AddListener(OnExitButtonClicked);

        UpdateUI();
    }

    private void OnConnected(int playerId)
    {
        Debug.Log($"LobbyManager: подключен как игрок {playerId}");
        UpdateConnectionInfo();
    }

    private void OnRoleButtonClicked(int slotId, string role)
    {
        if (gameClient == null || !gameClient.IsConnected) return;
        if (IsPlayerExists(slotId) && currentPlayerSlot != slotId) return;

        gameClient.SendChangeRole("player", slotId);
        Debug.Log($"LobbyManager: попытка занять слот {slotId}");
    }

    private void OnClassButtonClicked(int classId)
    {
        if (gameClient == null || !gameClient.IsConnected) return;
        if (!CanInteractWithClasses()) return;

        string actionType = GetCurrentClassAction();
        if (string.IsNullOrEmpty(actionType)) return;

        int slotIndex = GetClassSelectionSlotIndex();
        gameClient.SendClassAction(actionType, classId, slotIndex);
        Debug.Log($"LobbyManager: отправлен выбор класса {classId}, действие {actionType}, слот {slotIndex}");
    }

    private void OnReadyButtonClicked(int playerSlot)
    {
        Debug.Log($"LobbyManager: нажата готовность для слота {playerSlot}");
        if (gameClient == null || !gameClient.IsConnected) return;
        if (currentPlayerSlot != playerSlot) return;

        bool isPlayerReady = IsPlayerReady(playerSlot);
        bool bothPlayersReady = IsBothPlayersReady();
        if (bothPlayersReady && isPlayerReady) return;

        bool newReadyState = !isPlayerReady;
        gameClient.SendReady(newReadyState);
        Debug.Log($"LobbyManager: отправлен Ready = {newReadyState}");
    }

    private void OnExitButtonClicked()
    {
        if (gameClient != null) gameClient.Disconnect();
        if (GameServer.Instance != null) GameServer.Instance.StopServer();
        SceneManager.LoadScene("MainMenu");
    }

    private void OnNetworkMessage(NetworkMessage message)
    {
        Debug.Log($"LobbyManager: получено сообщение типа {message.type}");
        switch (message.type)
        {
            case "LobbyState":
                HandleLobbyState(message);
                break;
            case "StartGame":
                HandleStartGame(message);
                break;
            case "PlayerDisconnected":
                UpdateUI();
                break;
            default:
                Debug.LogWarning($"LobbyManager: неизвестный тип {message.type}");
                break;
        }
    }

    private void HandleLobbyState(NetworkMessage message)
    {
        var stateData = JsonUtility.FromJson<LobbyStateMessage>(message.data);
        gameState = stateData.gameState;
        currentPlayerSlot = stateData.currentPlayerSlot;
        Debug.Log($"LobbyManager: обновлено состояние лобби, текущий слот {currentPlayerSlot}");
        UpdateUI();
    }

    private void HandleStartGame(NetworkMessage message)
    {
        var startData = JsonUtility.FromJson<StartGameMessage>(message.data);
        Debug.Log($"LobbyManager: получен StartGame, playerNumber={startData.playerNumber}");
        if (GameDataManager.Instance != null)
        {
            GameDataManager.Instance.SetNetworkGameData(
                startData.playerClasses,
                startData.opponentClasses,
                startData.playerNumber == 0
            );
        }
        SceneManager.LoadScene("GameScene");
    }

    private void OnDisconnectedFromServer()
    {
        Debug.Log("LobbyManager: отключено от сервера, возврат в меню");
        SceneManager.LoadScene("MainMenu");
    }

    private void UpdateUI()
    {
        UpdateRoleButtons();
        UpdateClassButtons();
        UpdateReadyButtons();
        UpdatePhaseText();
        UpdateSelectedClasses();
        // UpdateBannedClasses(); // закомментировано
        UpdateConnectionInfo();
    }

    private void UpdateRoleButtons()
    {
        if (player1Button != null)
        {
            bool isOccupied = gameState.playerExists[0];
            bool isCurrent = currentPlayerSlot == 0;
            player1Button.UpdateState(isOccupied, isCurrent, !isOccupied || isCurrent);
        }

        if (player2Button != null)
        {
            bool isOccupied = gameState.playerExists[1];
            bool isCurrent = currentPlayerSlot == 1;
            player2Button.UpdateState(isOccupied, isCurrent, !isOccupied || isCurrent);
        }
    }

    private void UpdateClassButtons()
    {
        if (classButtons == null) return;

        bool canInteract = CanInteractWithClasses();
        
        foreach (ClassButton button in classButtons)
        {
            if (button == null) continue;

            bool isSelected = IsClassSelected(button.ClassId);
            // bool isBanned = IsClassBanned(button.ClassId); // закомментировано
            bool isAvailable = canInteract && !isSelected; // && !isBanned;

            button.UpdateState(false, isSelected, isAvailable);
        }
    }

    private void UpdateReadyButtons()
    {
        // Игрок 1
        if (readyButtonPlayer1 != null)
        {
            bool isOccupied = gameState.playerExists[0];
            bool isReady = IsPlayerReady(0);
            bool isCurrentPlayer = currentPlayerSlot == 0;
            bool bothPlayersReady = IsBothPlayersReady();
            
            readyButtonPlayer1.gameObject.SetActive(isOccupied);
            
            if (isOccupied)
            {
                bool canInteract = isCurrentPlayer && (!bothPlayersReady || !isReady);
                readyButtonPlayer1.interactable = canInteract;
                
                TextMeshProUGUI buttonText = readyButtonPlayer1.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null)
                    buttonText.text = isReady ? "Отменить" : "Готов";
            }
        }
        
        if (readyStatusTextPlayer1 != null)
        {
            if (gameState.playerExists[0])
            {
                bool isReady = IsPlayerReady(0);
                readyStatusTextPlayer1.text = isReady ? "Готов" : "Не готов";
                readyStatusTextPlayer1.color = isReady ? Color.green : Color.red;
            }
            else
            {
                readyStatusTextPlayer1.text = "Нет игрока";
                readyStatusTextPlayer1.color = Color.gray;
            }
        }
        
        // Игрок 2
        if (readyButtonPlayer2 != null)
        {
            bool isOccupied = gameState.playerExists[1];
            bool isReady = IsPlayerReady(1);
            bool isCurrentPlayer = currentPlayerSlot == 1;
            bool bothPlayersReady = IsBothPlayersReady();
            
            readyButtonPlayer2.gameObject.SetActive(isOccupied);
            
            if (isOccupied)
            {
                bool canInteract = isCurrentPlayer && (!bothPlayersReady || !isReady);
                readyButtonPlayer2.interactable = canInteract;
                
                TextMeshProUGUI buttonText = readyButtonPlayer2.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null)
                    buttonText.text = isReady ? "Отменить" : "Готов";
            }
        }
        
        if (readyStatusTextPlayer2 != null)
        {
            if (gameState.playerExists[1])
            {
                bool isReady = IsPlayerReady(1);
                readyStatusTextPlayer2.text = isReady ? "Готов" : "Не готов";
                readyStatusTextPlayer2.color = isReady ? Color.green : Color.red;
            }
            else
            {
                readyStatusTextPlayer2.text = "Нет игрока";
                readyStatusTextPlayer2.color = Color.gray;
            }
        }
    }

    private void UpdatePhaseText()
    {
        if (phaseText == null) return;

        // Фазы с ban удалены
        string phaseDisplay = gameState.classSelectionPhase switch
        {
            "pick1_player1" => "Игрок 1 выбирает 1 класс",
            "pick1_player2" => "Игрок 2 выбирает 1 класс",
            "pick2_player1" => "Игрок 1 выбирает 2 класс",
            "pick2_player2" => "Игрок 2 выбирает 2 класс",
            "completed" => "Все классы выбраны! Запуск игры...",
            _ => "Ожидание готовности игроков"
        };
        
        phaseText.text = phaseDisplay;
    }

    private void UpdateSelectedClasses()
    {
        if (selectedClassesText == null) return;

        string player1Classes = GetClassNames(gameState.player0Classes);
        string player2Classes = GetClassNames(gameState.player1Classes);
        
        selectedClassesText.text = $"Игрок 1: {player1Classes}\nИгрок 2: {player2Classes}";
    }

    /*
    private void UpdateBannedClasses()
    {
        if (bannedClassesText != null)
        {
            string bannedText = "Запрещенные классы: ";
            bool hasBanned = false;
            for (int i = 0; i < gameState.bannedClasses.Length; i++)
            {
                if (gameState.bannedClasses[i] != -1)
                {
                    if (hasBanned) bannedText += ", ";
                    bannedText += classNames[gameState.bannedClasses[i]];
                    hasBanned = true;
                }
            }
            if (!hasBanned) bannedText += "нет";
            bannedClassesText.text = bannedText;
        }
    }
    */

    private string GetClassNames(int[] classIds)
    {
        if (classIds == null || classNames == null) return "не выбраны";
        string result = "";
        bool first = true;
        for (int i = 0; i < classIds.Length; i++)
        {
            if (classIds[i] != -1 && classIds[i] < classNames.Length)
            {
                if (!first) result += ", ";
                result += classNames[classIds[i]];
                first = false;
            }
        }
        return string.IsNullOrEmpty(result) ? "не выбраны" : result;
    }

    private void UpdateConnectionInfo()
    {
        if (connectionInfoText == null) return;

        if (gameClient == null || !gameClient.IsConnected)
        {
            connectionInfoText.text = "Не подключено";
            connectionInfoText.color = Color.red;
        }
        else
        {
            int connectedPlayers = (gameState.playerExists[0] ? 1 : 0) + (gameState.playerExists[1] ? 1 : 0);
            connectionInfoText.text = $"Подключено ({connectedPlayers}/2)";
            connectionInfoText.color = Color.green;
        }
    }

    private bool IsPlayerExists(int slotId)
    {
        return slotId >= 0 && slotId < 2 && gameState.playerExists[slotId];
    }

    private bool IsPlayerReady(int slotId)
    {
        return slotId >= 0 && slotId < 2 && gameState.playerReady[slotId];
    }

    private bool IsBothPlayersReady()
    {
        return IsPlayerReady(0) && IsPlayerReady(1);
    }

    private bool CanInteractWithClasses()
    {
        if (currentPlayerSlot == -1) return false;
        if (!IsPlayerExists(currentPlayerSlot)) return false;
        
        string currentPhase = gameState.classSelectionPhase;
        
        // Разрешены только фазы pick
        return (currentPhase == "pick1_player1" && currentPlayerSlot == 0) ||
               (currentPhase == "pick1_player2" && currentPlayerSlot == 1) ||
               (currentPhase == "pick2_player1" && currentPlayerSlot == 0) ||
               (currentPhase == "pick2_player2" && currentPlayerSlot == 1);
    }

    private string GetCurrentClassAction()
    {
        string phase = gameState.classSelectionPhase;
        if (phase.StartsWith("pick")) return "pick";
        return "";
    }

    private int GetClassSelectionSlotIndex()
    {
        string phase = gameState.classSelectionPhase;
        if (phase == "pick1_player1" || phase == "pick1_player2") return 0;
        if (phase == "pick2_player1" || phase == "pick2_player2") return 1;
        return 0;
    }

    /*
    private bool IsClassBanned(int classId)
    {
        foreach (int bannedClass in gameState.bannedClasses)
            if (bannedClass == classId) return true;
        return false;
    }
    */

    private bool IsClassSelected(int classId)
    {
        foreach (int playerClass in gameState.player0Classes)
            if (playerClass == classId) return true;
        foreach (int playerClass in gameState.player1Classes)
            if (playerClass == classId) return true;
        return false;
    }

    void OnDestroy()
    {
        if (gameClient != null)
        {
            gameClient.OnMessageReceived -= OnNetworkMessage;
            gameClient.OnConnected -= OnConnected;
            gameClient.OnDisconnected -= OnDisconnectedFromServer;
        }
    }
}