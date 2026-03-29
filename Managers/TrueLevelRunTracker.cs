using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class TrueLevelRunTracker : MonoBehaviour
{
    public static TrueLevelRunTracker Instance { get; private set; }

    [Header("Damage Tracking")]
    public bool IncludeStatusTickDamage = true;

    private float totalDamageDealt;
    private float totalTimeSurvived;

    private int[] killsByRarity;
    private int[] trueExpByRarity;

    private StartingScreen cachedStartingScreen;
    private LevelUpUI cachedLevelUpUI;

    private bool runActive;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static TrueLevelRunTracker EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        TrueLevelRunTracker existing = FindObjectOfType<TrueLevelRunTracker>(true);
        if (existing != null)
        {
            return existing;
        }

        GameObject go = new GameObject(nameof(TrueLevelRunTracker));
        return go.AddComponent<TrueLevelRunTracker>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        int rarityCount = Enum.GetValues(typeof(CardRarity)).Length;
        killsByRarity = new int[rarityCount];
        trueExpByRarity = new int[rarityCount];

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;

        HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
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
        runActive = scene.IsValid() && scene.name == "Game";
        if (runActive)
        {
            ResetRun();
        }
    }

    public void ResetRun()
    {
        totalDamageDealt = 0f;
        totalTimeSurvived = 0f;

        if (killsByRarity != null)
        {
            Array.Clear(killsByRarity, 0, killsByRarity.Length);
        }

        if (trueExpByRarity != null)
        {
            Array.Clear(trueExpByRarity, 0, trueExpByRarity.Length);
        }
    }

    private void Update()
    {
        if (!runActive)
        {
            return;
        }

        if (GameStateManager.Instance != null && GameStateManager.Instance.PlayerIsDead)
        {
            return;
        }

        if (IsNonGameplayUIActive())
        {
            return;
        }

        float dt = GameStateManager.GetPauseSafeDeltaTime();
        if (dt <= 0f)
        {
            return;
        }

        totalTimeSurvived += dt;
    }

    private bool IsNonGameplayUIActive()
    {
        if (cachedStartingScreen == null)
        {
            cachedStartingScreen = FindObjectOfType<StartingScreen>(true);
        }

        if (cachedStartingScreen != null)
        {
            GameObject root = cachedStartingScreen.Root != null ? cachedStartingScreen.Root : cachedStartingScreen.gameObject;
            if (root != null && root.activeInHierarchy)
            {
                return true;
            }
        }

        if (cachedLevelUpUI == null)
        {
            cachedLevelUpUI = FindObjectOfType<LevelUpUI>(true);
        }

        if (cachedLevelUpUI != null)
        {
            GameObject root = cachedLevelUpUI.Root != null ? cachedLevelUpUI.Root : cachedLevelUpUI.gameObject;
            if (root != null && root.activeInHierarchy)
            {
                return true;
            }
        }

        return false;
    }

    public void NotifyEnemyDamageFinalized(GameObject enemy, float finalDamage, bool isStatusTick)
    {
        if (!runActive)
        {
            return;
        }

        if (finalDamage <= 0f)
        {
            return;
        }

        if (isStatusTick && !IncludeStatusTickDamage)
        {
            return;
        }

        if (GameStateManager.Instance != null && GameStateManager.Instance.PlayerIsDead)
        {
            return;
        }

        totalDamageDealt += finalDamage;
    }

    public void NotifyEnemyKilled(GameObject enemy)
    {
        if (!runActive)
        {
            return;
        }

        if (GameStateManager.Instance != null && GameStateManager.Instance.PlayerIsDead)
        {
            return;
        }

        CardRarity rarity = CardRarity.Common;
        int baseExp = 0;

        if (enemy != null)
        {
            EnemyExpData expData = enemy.GetComponent<EnemyExpData>();
            if (expData == null)
            {
                expData = enemy.GetComponentInParent<EnemyExpData>();
            }

            if (expData != null)
            {
                rarity = expData.EnemyRarity;
                if (expData.GrantsExpToPlayer)
                {
                    baseExp = expData.GetTrueLevelBaseExpReward();
                }
            }
        }

        int index = (int)rarity;
        if (killsByRarity != null && index >= 0 && index < killsByRarity.Length)
        {
            killsByRarity[index]++;
        }

        if (trueExpByRarity != null && index >= 0 && index < trueExpByRarity.Length)
        {
            trueExpByRarity[index] += Mathf.Max(0, baseExp);
        }
    }

    public float TotalDamageDealt => totalDamageDealt;
    public float TotalTimeSurvived => totalTimeSurvived;

    public int GetKills(CardRarity rarity)
    {
        int index = (int)rarity;
        if (killsByRarity == null || index < 0 || index >= killsByRarity.Length)
        {
            return 0;
        }

        return killsByRarity[index];
    }

    public int GetTrueExp(CardRarity rarity)
    {
        int index = (int)rarity;
        if (trueExpByRarity == null || index < 0 || index >= trueExpByRarity.Length)
        {
            return 0;
        }

        return trueExpByRarity[index];
    }

    public int TotalTrueExp
    {
        get
        {
            if (trueExpByRarity == null)
            {
                return 0;
            }

            int total = 0;
            for (int i = 0; i < trueExpByRarity.Length; i++)
            {
                total += Mathf.Max(0, trueExpByRarity[i]);
            }

            return total;
        }
    }
}
