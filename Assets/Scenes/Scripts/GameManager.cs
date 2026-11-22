using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Hand References")]
    public HandManager playerHand;
    public HandManager opponentHand;

    [Header("Deck UI")]
    public TextMeshProUGUI playerDeckCountText;
    public TextMeshProUGUI opponentDeckCountText;

    [Header("Turn & Board")]
    public TurnManager turnManager;
    public BoardManager boardManager;

    [Header("Player UI")]
    public TextMeshProUGUI playerHealthText;
    public TextMeshProUGUI playerActionPointsText;
    public TextMeshProUGUI opponentHealthText;
    public TextMeshProUGUI opponentActionPointsText;

    [SerializeField] private Button resetHandButton;
    [SerializeField] private Sprite actionPointsBackground;
    [SerializeField] private Sprite actionPointsFill;

    private ActionPointsView localPointsView;
    private ActionPointsView opponentPointsView;
    private ServerGameState currentState;
    private bool isInitialized = false;
    private int localPlayerId = 1;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        if (resetHandButton != null)
            resetHandButton.onClick.AddListener(OnResetHandClicked);

        if (GameDataManager.Instance == null || !GameDataManager.Instance.IsNetworkGame) return;

        GameClient.Instance.OnGameStateReceived += OnGameStateReceived;
        var pending = GameClient.Instance.GetPendingGameState();
        if (pending != null) OnGameStateReceived(pending);
    }

    private void OnGameStateReceived(ServerGameState state)
    {
        if (state == null) return;
        if (!isInitialized) InitializeFromState(state);
        else UpdateGameState(state);
    }

    public void InitializeFromState(ServerGameState state)
    {
        currentState = state;
        isInitialized = true;

        localPlayerId = GameDataManager.Instance.IsLocalPlayerOne ? 1 : 2;

        if (boardManager != null) 
        {
            boardManager.SetLocalPlayerId(localPlayerId);
            boardManager.UpdateAvatars(localPlayerId);
        }

        playerHand.SetPlayerId(localPlayerId);
        opponentHand.SetPlayerId(localPlayerId == 1 ? 2 : 1);

        if (localPlayerId == 2) boardManager.SwapSlots();

        if (actionPointsBackground != null && actionPointsFill != null && boardManager != null)
        {
            if (boardManager.selfPortraitButton != null && playerActionPointsText != null)
                localPointsView = ActionPointsView.Create(boardManager.selfPortraitButton, playerActionPointsText,
                    actionPointsBackground, actionPointsFill, true);
            if (boardManager.opponentPortraitButton != null && opponentActionPointsText != null)
                opponentPointsView = ActionPointsView.Create(boardManager.opponentPortraitButton, opponentActionPointsText,
                    actionPointsBackground, actionPointsFill, false);
        }

        UpdateHands(state);
        boardManager.UpdateBoard(state.board);
        turnManager.SetCurrentPlayer(state.currentPlayerId);
        turnManager.SetRound(state.round);
        UpdateUI();
    }

    public void UpdateGameState(ServerGameState newState)
    {
        bool handWasReset = !currentState.GetPlayer(localPlayerId).hasResetHand &&
            newState.GetPlayer(localPlayerId).hasResetHand;
        currentState = newState;
        if (handWasReset && boardManager != null)
        {
            boardManager.DeactivateCurrent();
            boardManager.cardPreviewUI?.Hide();
            boardManager.StartDefaultMode();
        }
        UpdateUI();
        UpdateHands(newState);
        boardManager.UpdateBoard(newState.board, newState.lastCardHit);
        turnManager.SetCurrentPlayer(newState.currentPlayerId);
        turnManager.SetRound(newState.round);
        
        if (boardManager.eternalSleepUI != null && boardManager.eternalSleepUI.panel.activeSelf)
        {
            boardManager.eternalSleepUI.RefreshCurrentView();
        }
    }

    private void UpdateHands(ServerGameState state)
    {
        playerHand.UpdateHand(state.GetPlayer(localPlayerId).hand, playerDeckCountText?.transform.parent);
        opponentHand.UpdateHand(state.GetPlayer(3 - localPlayerId).hand, opponentDeckCountText?.transform.parent);

        playerHand.SetInteractable(true);
        playerHand.SetFace(true);
        opponentHand.SetInteractable(false);
        opponentHand.SetFace(false);
    }

    public void UpdateUI()
    {
        if (currentState == null) return;

        var local = localPlayerId == 1 ? currentState.player1 : currentState.player2;
        var opponent = localPlayerId == 1 ? currentState.player2 : currentState.player1;

        if (resetHandButton != null)
            resetHandButton.interactable = currentState.currentPlayerId == localPlayerId && local.CanResetHand();

        if (playerHealthText) playerHealthText.text = $"HP: {local.health}/25";
        if (localPointsView == null && playerActionPointsText) playerActionPointsText.text = $"ОД: {local.actionPoints}/5";
        if (opponentHealthText) opponentHealthText.text = $"HP: {opponent.health}/25";
        if (opponentPointsView == null && opponentActionPointsText) opponentActionPointsText.text = $"ОД: {opponent.actionPoints}/5";
        if (playerDeckCountText) playerDeckCountText.text = local.deck.Count.ToString();
        if (opponentDeckCountText) opponentDeckCountText.text = opponent.deck.Count.ToString();
    }

    private void OnResetHandClicked()
    {
        if (currentState == null || currentState.currentPlayerId != localPlayerId ||
            !currentState.GetPlayer(localPlayerId).CanResetHand()) return;

        var client = GameClient.Instance;
        if (client != null && client.IsConnected)
            client.SendGameAction(new GameActionMessage { actionType = "ResetHand" });
    }

    private void LateUpdate()
    {
        if (currentState == null) return;
        localPointsView?.Display(currentState.GetPlayer(localPlayerId).actionPoints, boardManager.PreviewActionPointCost);
        opponentPointsView?.Display(currentState.GetPlayer(3 - localPlayerId).actionPoints, 0);
    }

    public ServerGameState GetCurrentState() => currentState;
    public bool IsGameInitialized() => isInitialized;

    void OnDestroy()
    {
        if (resetHandButton != null) resetHandButton.onClick.RemoveListener(OnResetHandClicked);
        if (GameClient.Instance != null)
            GameClient.Instance.OnGameStateReceived -= OnGameStateReceived;
        if (Instance == this) Instance = null;
    }
}