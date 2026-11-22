using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    [Header("Round Text")]
    public TextMeshProUGUI roundText;
    public string roundTextFormat = "РАУНД: {0}";

    [Header("Turn Announcement")]
    [SerializeField] private TextMeshProUGUI turnAnnouncement;
    private const float AnnouncementSeconds = 2f;
    private bool hasReceivedTurn;
    private Coroutine announcementRoutine;

    [Header("Current Player Indicator")]
    public Image currentPlayerIndicator;
    public Color player1IndicatorColor = Color.blue;
    public Color player2IndicatorColor = Color.red;

    [Header("End Turn Button")]
    public Button endTurnButton;

    private int currentPlayer = 1;
    private int currentRound = 0;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        if (endTurnButton)
            endTurnButton.onClick.AddListener(OnEndTurnClicked);
        UpdateUI();
    }

    private void OnEndTurnClicked()
    {
        if (GameClient.Instance != null && GameClient.Instance.IsConnected)
            GameClient.Instance.SendGameAction(new GameActionMessage { actionType = "EndTurn" });
    }

    public void SetCurrentPlayer(int player)
    {
        if (!hasReceivedTurn || currentPlayer != player)
        {
            hasReceivedTurn = true;
            ShowTurnAnnouncement(player);
        }

        if (currentPlayer != player)
        {
            currentPlayer = player;

            if (BoardManager.Instance != null)
            {
                BoardManager.Instance.DeactivateCurrent();

                int localId = GetLocalPlayerId();
                if (player == localId)
                {
                    BoardManager.Instance.StartDefaultMode();
                }
            }
            UpdateUI();
        }
    }

    private void ShowTurnAnnouncement(int player)
    {
        if (turnAnnouncement == null) return;

        if (announcementRoutine != null) StopCoroutine(announcementRoutine);
        turnAnnouncement.text = player == GetLocalPlayerId() ? "Ваш ход" : "Ход противника";
        turnAnnouncement.gameObject.SetActive(true);
        announcementRoutine = StartCoroutine(HideTurnAnnouncement());
    }

    private System.Collections.IEnumerator HideTurnAnnouncement()
    {
        yield return new WaitForSecondsRealtime(AnnouncementSeconds);
        turnAnnouncement.gameObject.SetActive(false);
        announcementRoutine = null;
    }

    private static int GetLocalPlayerId() =>
        GameDataManager.Instance != null && GameDataManager.Instance.IsLocalPlayerOne ? 1 : 2;


    public void SetRound(int round)
    {
        if (currentRound != round)
        {
            currentRound = round;
            UpdateUI();
        }
    }

    public void UpdateUI()
    {
        if (roundText != null) 
            roundText.text = string.Format(roundTextFormat, currentRound);

        if (currentPlayerIndicator != null)
            currentPlayerIndicator.color = currentPlayer == 1 ? player1IndicatorColor : player2IndicatorColor;

        int localId = GetLocalPlayerId();
        bool isLocalTurn = (currentPlayer == localId);

        if (endTurnButton != null)
        {
            endTurnButton.gameObject.SetActive(isLocalTurn);
            endTurnButton.interactable = isLocalTurn;
        }

        if (GameManager.Instance) GameManager.Instance.UpdateUI();
    }

    public int GetCurrentPlayer() => currentPlayer;
}