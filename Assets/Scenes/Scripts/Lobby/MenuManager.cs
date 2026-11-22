using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [Header("Main Buttons")]
    public Button playButton;
    public Button classesButton;
    public Button rulesButton;
    public Button settingsButton;
    public Button exitButton;

    [Header("Play Submenu")]
    public GameObject playSubmenu;

    [Header("Submenu Buttons")]
    public Button createLobbyButton;
    public Button joinLobbyButton;

    private void Start()
    {
        InitializeButtons();
        
        if (playSubmenu != null)
            playSubmenu.SetActive(false);
    }

    private void InitializeButtons()
    {
        // Основные кнопки меню
        if (playButton != null)
            playButton.onClick.AddListener(OnPlayClicked);
        if (classesButton != null)
            classesButton.onClick.AddListener(OnClassesClicked);
        if (rulesButton != null)
            rulesButton.onClick.AddListener(OnRulesClicked);
        if (settingsButton != null)
            settingsButton.onClick.AddListener(OnSettingsClicked);
        if (exitButton != null)
            exitButton.onClick.AddListener(OnExitClicked);

        // Кнопки подменю
        if (createLobbyButton != null)
            createLobbyButton.onClick.AddListener(OnCreateLobbyClicked);
        if (joinLobbyButton != null)
            joinLobbyButton.onClick.AddListener(OnJoinLobbyClicked);
    }

    private void OnPlayClicked()
    {
        ToggleSubmenu();
    }

    private void OnClassesClicked()
    {
        HideSubmenu();
        Debug.Log("Классы - заглушка");
    }

    private void OnRulesClicked()
    {
        HideSubmenu();
        Debug.Log("Правила - заглушка");
    }

    private void OnSettingsClicked()
    {
        HideSubmenu();
        Debug.Log("Настройки - заглушка");
    }

    private void OnExitClicked()
    {
        HideSubmenu();
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void OnCreateLobbyClicked()
    {
        HideSubmenu();
        
        // Создаем GameServer если его нет
        if (GameServer.Instance == null)
        {
            GameObject serverObj = new GameObject("GameServer");
            serverObj.AddComponent<GameServer>();
        }
        
        // Создаем GameClient если его нет
        if (GameClient.Instance == null)
        {
            GameObject clientObj = new GameObject("GameClient");
            clientObj.AddComponent<GameClient>();
        }
        
        // Запускаем сервер и подключаемся к нему как создатель лобби
        GameServer.Instance.StartServer();
        GameClient.Instance.ConnectToServer("127.0.0.1");
        SceneManager.LoadScene("LobbyScene");
    }

    private void OnJoinLobbyClicked()
    {
        HideSubmenu();
        SceneManager.LoadScene("ConnectScene");
    }

    private void ToggleSubmenu()
    {
        if (playSubmenu != null)
        {
            playSubmenu.SetActive(!playSubmenu.activeSelf);
        }
    }

    private void HideSubmenu()
    {
        if (playSubmenu != null)
            playSubmenu.SetActive(false);
    }

    private void Update()
    {
        // Закрываем подменю при нажатии Escape
        if (playSubmenu != null && playSubmenu.activeSelf && Input.GetKeyDown(KeyCode.Escape))
        {
            HideSubmenu();
        }
        
        // Закрываем подменю при клике в любую область экрана
        if (playSubmenu != null && playSubmenu.activeSelf && Input.GetMouseButtonDown(0))
        {
            // Проверяем, был ли клик по подменю
            bool clickedOnSubmenu = false;
            
            if (createLobbyButton != null && 
                RectTransformUtility.RectangleContainsScreenPoint(
                    createLobbyButton.GetComponent<RectTransform>(), 
                    Input.mousePosition))
            {
                clickedOnSubmenu = true;
            }
            
            if (joinLobbyButton != null && 
                RectTransformUtility.RectangleContainsScreenPoint(
                    joinLobbyButton.GetComponent<RectTransform>(), 
                    Input.mousePosition))
            {
                clickedOnSubmenu = true;
            }
            
            // Если клик был не по подменю, закрываем его
            if (!clickedOnSubmenu)
            {
                HideSubmenu();
            }
        }
    }

    private void OnDestroy()
    {
        // Очищаем все обработчики событий
        if (playButton != null)
            playButton.onClick.RemoveAllListeners();
        if (classesButton != null)
            classesButton.onClick.RemoveAllListeners();
        if (rulesButton != null)
            rulesButton.onClick.RemoveAllListeners();
        if (settingsButton != null)
            settingsButton.onClick.RemoveAllListeners();
        if (exitButton != null)
            exitButton.onClick.RemoveAllListeners();
        if (createLobbyButton != null)
            createLobbyButton.onClick.RemoveAllListeners();
        if (joinLobbyButton != null)
            joinLobbyButton.onClick.RemoveAllListeners();
    }
}