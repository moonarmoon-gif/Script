using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class TruePlayerLevel : MonoBehaviour
{
    public static TruePlayerLevel Instance { get; private set; }

    [Header("Level Settings")]
    [SerializeField] private int currentLevel = 1;
    [SerializeField] private float currentExp = 0f;
    [SerializeField] private float expToNextLevel = 1000f;
    [SerializeField] private int baseExpRequirement = 1000;
    [SerializeField] private float expScalingFactor = 1f;

    public event Action<int> OnLevelUp;
    public event Action<int, int, int> OnExpChanged;

    public int CurrentLevel => currentLevel;
    public int CurrentExp => Mathf.RoundToInt(currentExp);
    public int ExpToNextLevel => Mathf.RoundToInt(expToNextLevel);
    public float CurrentExpExact => currentExp;
    public float ExpToNextLevelExact => expToNextLevel;
    public float ExpProgress => expToNextLevel <= 0f ? 0f : currentExp / expToNextLevel;

    private const string PrefKeyLevel = "TruePlayerLevel.CurrentLevel";
    private const string PrefKeyExp = "TruePlayerLevel.CurrentExp";
    private const string PrefKeyBaseReq = "TruePlayerLevel.BaseExpRequirement";
    private const string PrefKeyScaling = "TruePlayerLevel.ExpScalingFactor";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static TruePlayerLevel EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        TruePlayerLevel existing = FindObjectOfType<TruePlayerLevel>(true);
        if (existing != null)
        {
            return existing;
        }

        GameObject go = new GameObject(nameof(TruePlayerLevel));
        return go.AddComponent<TruePlayerLevel>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        if (IsAttachedToPlayerObject())
        {
            if (Instance == null)
            {
                TruePlayerLevel[] all = FindObjectsOfType<TruePlayerLevel>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    TruePlayerLevel candidate = all[i];
                    if (candidate == null || candidate == this)
                    {
                        continue;
                    }

                    if (!candidate.IsAttachedToPlayerObject())
                    {
                        Instance = candidate;
                        DontDestroyOnLoad(candidate.gameObject);
                        break;
                    }
                }

                if (Instance == null)
                {
                    GameObject go = new GameObject(nameof(TruePlayerLevel));
                    Instance = go.AddComponent<TruePlayerLevel>();
                }
            }

            Destroy(this);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadFromPrefs();
        CalculateExpRequirement();

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private bool IsAttachedToPlayerObject()
    {
        if (CompareTag("Player"))
        {
            return true;
        }

        if (GetComponent<PlayerController>() != null)
        {
            return true;
        }

        if (GetComponent<AdvancedPlayerController>() != null)
        {
            return true;
        }

        return false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            Instance = null;
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        NotifyChanged();
    }

    public void NotifyChanged()
    {
        OnExpChanged?.Invoke(CurrentExp, ExpToNextLevel, currentLevel);
    }

    public void GainExperience(int amount, bool raiseEvents = true)
    {
        GainExperience((float)amount, raiseEvents);
    }

    public void GainExperience(float amount, bool raiseEvents = true)
    {
        if (amount <= 0f)
        {
            return;
        }

        currentExp += amount;

        while (currentExp >= expToNextLevel)
        {
            currentExp -= expToNextLevel;
            currentLevel++;
            CalculateExpRequirement();

            if (raiseEvents)
            {
                OnLevelUp?.Invoke(currentLevel);
            }
        }

        SaveToPrefs();

        if (raiseEvents)
        {
            OnExpChanged?.Invoke(CurrentExp, ExpToNextLevel, currentLevel);
        }
    }

    public float GetExpRequirementForLevel(int level)
    {
        int lvl = Mathf.Max(1, level);
        float clampedFactor = Mathf.Max(0f, expScalingFactor);
        float required = baseExpRequirement * (1f + clampedFactor * (lvl - 1));
        return Mathf.Max(1f, required);
    }

    private void CalculateExpRequirement()
    {
        expToNextLevel = GetExpRequirementForLevel(currentLevel);
    }

    private void LoadFromPrefs()
    {
        currentLevel = Mathf.Max(1, PlayerPrefs.GetInt(PrefKeyLevel, currentLevel));
        currentExp = Mathf.Max(0f, PlayerPrefs.GetFloat(PrefKeyExp, currentExp));

        int loadedBase = PlayerPrefs.GetInt(PrefKeyBaseReq, baseExpRequirement);
        if (loadedBase > 0)
        {
            baseExpRequirement = loadedBase;
        }

        float loadedScaling = PlayerPrefs.GetFloat(PrefKeyScaling, expScalingFactor);
        if (loadedScaling >= 0f)
        {
            expScalingFactor = loadedScaling;
        }
    }

    private void SaveToPrefs()
    {
        PlayerPrefs.SetInt(PrefKeyLevel, currentLevel);
        PlayerPrefs.SetFloat(PrefKeyExp, currentExp);
        PlayerPrefs.SetInt(PrefKeyBaseReq, baseExpRequirement);
        PlayerPrefs.SetFloat(PrefKeyScaling, expScalingFactor);
        PlayerPrefs.Save();
    }
}
