using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class ConnectSceneManager : MonoBehaviour
{
    public TMP_InputField ipInputField;
    public Button connectButton;
    public Button backButton;
    public TextMeshProUGUI statusText;
    public GameObject loadingIndicator;

    private GameClient gameClient;

    void Start()
    {
        // Создаем MainThreadDispatcher если его нет
        if (MainThreadDispatcher.Instance == null)
        {
            GameObject dispatcherObj = new GameObject("MainThreadDispatcher");
            dispatcherObj.AddComponent<MainThreadDispatcher>();
        }
        
        gameClient = GameClient.Instance;
        
        if (gameClient == null)
        {
            GameObject clientObj = new GameObject("GameClient");
            gameClient = clientObj.AddComponent<GameClient>();
            DontDestroyOnLoad(clientObj);
        }

        gameClient.OnConnected += OnConnectedToServer;
        gameClient.OnConnectionFailed += OnConnectionFailed;
        gameClient.OnDisconnected += OnDisconnectedFromServer;

        SetupUI();
    }

    private void SetupUI()
    {
        if (ipInputField != null)
        {
            ipInputField.text = "127.0.0.1";
            ipInputField.onValueChanged.AddListener(OnIPInputChanged);
            ipInputField.onSubmit.AddListener((text) => OnConnectClicked());
        }

        if (connectButton != null)
        {
            connectButton.onClick.AddListener(OnConnectClicked);
            connectButton.interactable = true;
        }

        if (backButton != null)
        {
            backButton.onClick.AddListener(OnBackClicked);
        }

        if (loadingIndicator != null)
        {
            loadingIndicator.SetActive(false);
        }

        UpdateStatus("Введите IP адрес сервера", Color.white);
        UpdateConnectButton();
    }

    private void OnIPInputChanged(string newText)
    {
        UpdateConnectButton();
    }

    private void UpdateConnectButton()
    {
        if (connectButton != null && ipInputField != null)
        {
            bool hasValidInput = !string.IsNullOrEmpty(ipInputField.text.Trim());
            bool isNotConnecting = gameClient != null && !gameClient.IsConnecting && !gameClient.IsConnected;
            connectButton.interactable = hasValidInput && isNotConnecting;
        }
    }

    private void OnConnectClicked()
    {
        if (gameClient == null || gameClient.IsConnecting || gameClient.IsConnected)
            return;

        string ip = ipInputField != null ? ipInputField.text.Trim() : "127.0.0.1";
        
        if (string.IsNullOrEmpty(ip))
        {
            UpdateStatus("Введите IP адрес", Color.red);
            return;
        }

        try
        {
            UpdateStatus("Подключение...", Color.yellow);
            SetUIInteractable(false);
            
            if (loadingIndicator != null)
                loadingIndicator.SetActive(true);

            gameClient.ConnectToServer(ip);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Ошибка при подключении: {e.Message}");
            UpdateStatus($"Ошибка: {e.Message}", Color.red);
            SetUIInteractable(true);
        }
    }

    private void OnBackClicked()
    {
        if (gameClient != null && (gameClient.IsConnecting || gameClient.IsConnected))
        {
            gameClient.Disconnect();
        }

        SceneManager.LoadScene("MainMenu");
    }

    private void SetUIInteractable(bool interactable)
    {
        if (ipInputField != null)
            ipInputField.interactable = interactable;
        if (connectButton != null)
            connectButton.interactable = interactable;
        if (backButton != null)
            backButton.interactable = interactable;

        if (loadingIndicator != null)
            loadingIndicator.SetActive(!interactable && gameClient.IsConnecting);
    }

    private void UpdateStatus(string message, Color color)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.color = color;
        }
    }

    private void OnConnectedToServer(int assignedPlayerId)
    {
        Debug.Log($"✅ Успешно подключено к серверу как Player {assignedPlayerId}");
        UpdateStatus("Успешно подключено! Переход в лобби...", Color.green);

        StartCoroutine(LoadLobbyAfterDelay());
    }

    private void OnConnectionFailed(string errorMessage)
    {
        Debug.LogError($"❌ Ошибка подключения: {errorMessage}");
        UpdateStatus($"Ошибка подключения: {errorMessage}", Color.red);
        SetUIInteractable(true);
    }

    private void OnDisconnectedFromServer()
    {
        Debug.Log("❌ Отключено от сервера");
        UpdateStatus("Отключено от сервера", Color.red);
        SetUIInteractable(true);
    }

    private IEnumerator LoadLobbyAfterDelay()
    {
        yield return new WaitForSeconds(0.5f);
        SceneManager.LoadScene("LobbyScene");
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return) && connectButton != null && connectButton.interactable)
        {
            OnConnectClicked();
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            OnBackClicked();
        }

        UpdateConnectButton();
        
        if (loadingIndicator != null)
        {
            loadingIndicator.SetActive(gameClient != null && gameClient.IsConnecting);
        }
    }

    void OnDestroy()
    {
        if (gameClient != null)
        {
            gameClient.OnConnected -= OnConnectedToServer;
            gameClient.OnConnectionFailed -= OnConnectionFailed;
            gameClient.OnDisconnected -= OnDisconnectedFromServer;
        }

        if (ipInputField != null)
        {
            ipInputField.onValueChanged.RemoveAllListeners();
            ipInputField.onSubmit.RemoveAllListeners();
        }
        if (connectButton != null)
            connectButton.onClick.RemoveAllListeners();
        if (backButton != null)
            backButton.onClick.RemoveAllListeners();
    }
}