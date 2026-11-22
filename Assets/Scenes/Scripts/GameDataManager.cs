using UnityEngine;

[System.Serializable]
public class PlayerStats
{
    public int playerId;
    public int maxHealth = 25;
    public int currentHealth;
    public int maxActionPoints = 5;
    public int currentActionPoints;
    public bool isAlive = true;

    public PlayerStats(int playerId)
    {
        this.playerId = playerId;
        currentHealth = maxHealth;
        currentActionPoints = maxActionPoints;
    }

    public void Reset()
    {
        currentHealth = maxHealth;
        currentActionPoints = maxActionPoints;
        isAlive = true;
    }

    public bool CanSpendActionPoints(int amount)
    {
        return currentActionPoints >= amount;
    }

    public bool SpendActionPoints(int amount)
    {
        if (CanSpendActionPoints(amount))
        {
            currentActionPoints -= amount;
            return true;
        }
        return false;
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            isAlive = false;
        }
    }

    public void Heal(int amount)
    {
        currentHealth += amount;
        if (currentHealth > maxHealth)
        {
            currentHealth = maxHealth;
        }
    }

    public void AddActionPoints(int amount)
    {
        currentActionPoints += amount;
        if (currentActionPoints > maxActionPoints)
        {
            currentActionPoints = maxActionPoints;
        }
    }

    public void ResetActionPoints()
    {
        currentActionPoints = maxActionPoints;
    }
}

public class GameDataManager : MonoBehaviour
{
    public static GameDataManager Instance { get; private set; }
    
    [SerializeField] private bool _isNetworkGame = false;
    [SerializeField] private int _localPlayerNumber = 0;
    [SerializeField] private int[] _localPlayerClasses = { -1, -1 };
    [SerializeField] private int[] _remotePlayerClasses = { -1, -1 };
    
    [SerializeField] private PlayerStats player1Stats;
    [SerializeField] private PlayerStats player2Stats;
    
    public bool IsNetworkGame => _isNetworkGame;
    public bool IsLocalPlayerOne => _localPlayerNumber == 0;
    public int[] LocalPlayerClasses => _localPlayerClasses;
    public int[] RemotePlayerClasses => _remotePlayerClasses;
    
    public PlayerStats GetPlayerStats(int playerId)
    {
        return playerId == 1 ? player1Stats : player2Stats;
    }
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }

        if (player1Stats == null) player1Stats = new PlayerStats(1);
        if (player2Stats == null) player2Stats = new PlayerStats(2);
    }
    
    public void InitializePlayerStats()
    {
        player1Stats.Reset();
        player2Stats.Reset();
    }
    
    public void SetNetworkGameData(int[] playerClasses, int[] opponentClasses, bool isLocalPlayerOne)
    {
        _isNetworkGame = true;
        _localPlayerNumber = isLocalPlayerOne ? 0 : 1;
        _localPlayerClasses = playerClasses;
        _remotePlayerClasses = opponentClasses;
    }
    
    public void ResetGameData()
    {
        _isNetworkGame = false;
        _localPlayerNumber = 0;
        _localPlayerClasses = new int[] { -1, -1 };
        _remotePlayerClasses = new int[] { -1, -1 };
    }
}