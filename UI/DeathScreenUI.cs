using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class DeathScreenUI : MonoBehaviour
{
    public static bool RestartAllowed { get; private set; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= BootstrapOnSceneLoaded;
        SceneManager.sceneLoaded += BootstrapOnSceneLoaded;
    }

    private static void BootstrapOnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        DeathScreenUI ui = FindObjectOfType<DeathScreenUI>(true);
        if (ui == null)
        {
            return;
        }

        if (!ui.gameObject.activeSelf)
        {
            ui.gameObject.SetActive(true);
        }
    }

    [Header("Root")]
    public GameObject Root;
    public CanvasGroup RootCanvasGroup;

    [Header("Panels")]
    public GameObject GameOverPanel;
    public GameObject PostMatchPanel;

    [Header("Timing")]
    public float PostMatchDelaySeconds = 1f;

    public float RestartButtonDelay = 0.5f;

    [Header("Scene Switching")]
    public string MenuSceneName = "Menu";

    [Header("Texts")]
    public TMP_Text GameOverText;

    [Header("Combat Stats")]
    public TMP_Text DamageDealtText;
    public TMP_Text DamageDealtValueText;
    public TMP_Text TotalTimeText;
    public TMP_Text TotalTimeValueText;

    [Header("True Level EXP")]
    public TMP_Text CommonKilledText;
    public TMP_Text CommonKilledValueText;
    public TMP_Text CommonExpText;
    public TMP_Text CommonExpValueText;

    public TMP_Text UncommonKilledText;
    public TMP_Text UncommonKilledValueText;
    public TMP_Text UncommonExpText;
    public TMP_Text UncommonExpValueText;

    public TMP_Text RareKilledText;
    public TMP_Text RareKilledValueText;
    public TMP_Text RareExpText;
    public TMP_Text RareExpValueText;

    public TMP_Text EpicKilledText;
    public TMP_Text EpicKilledValueText;
    public TMP_Text EpicExpText;
    public TMP_Text EpicExpValueText;

    public TMP_Text LegendaryKilledText;
    public TMP_Text LegendaryKilledValueText;
    public TMP_Text LegendaryExpText;
    public TMP_Text LegendaryExpValueText;

    public TMP_Text MythicKilledText;
    public TMP_Text MythicKilledValueText;
    public TMP_Text MythicExpText;
    public TMP_Text MythicExpValueText;

    public TMP_Text BossKilledText;
    public TMP_Text BossKilledValueText;
    public TMP_Text BossExpText;
    public TMP_Text BossExpValueText;

    public TMP_Text TotalTrueExpText;
    public TMP_Text TotalTrueExpValueText;

    [Header("Buttons")]
    public Button RetryButton;
    public Button CharacterButton;

    private PlayerHealth boundPlayerHealth;
    private Coroutine showRoutine;
    private bool trueExpAwarded;

    private UnityEngine.Events.UnityAction retryAction;
    private UnityEngine.Events.UnityAction characterAction;

    private void Awake()
    {
        if (Root == null)
        {
            Root = gameObject;
        }

        if (Root == gameObject)
        {
            Transform childRoot = transform.Find("Root");
            if (childRoot != null)
            {
                Root = childRoot.gameObject;
            }
            else if (GameOverPanel != null && GameOverPanel.transform.parent != null && GameOverPanel.transform.parent != transform)
            {
                Root = GameOverPanel.transform.parent.gameObject;
            }
            else if (PostMatchPanel != null && PostMatchPanel.transform.parent != null && PostMatchPanel.transform.parent != transform)
            {
                Root = PostMatchPanel.transform.parent.gameObject;
            }
        }

        if (RootCanvasGroup == null && Root != null)
        {
            RootCanvasGroup = Root.GetComponent<CanvasGroup>();
        }

        SetVisible(false);

        if (GameOverPanel != null)
        {
            GameOverPanel.SetActive(false);
        }

        if (PostMatchPanel != null)
        {
            PostMatchPanel.SetActive(false);
        }

        RestartAllowed = false;
        SetButtonsVisible(false);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;

        RegisterButtonHandlers();
        TryBindToPlayerDeath();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnregisterButtonHandlers();
        UnbindFromPlayerDeath();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SetVisible(false);
        trueExpAwarded = false;
        SetButtonsVisible(false);
        SetTrueLevelUIVisible(false);
        RestartAllowed = false;
        TryBindToPlayerDeath();
    }

    private void TryBindToPlayerDeath()
    {
        UnbindFromPlayerDeath();

        PlayerHealth playerHealth = null;

        if (AdvancedPlayerController.Instance != null)
        {
            playerHealth = AdvancedPlayerController.Instance.GetComponent<PlayerHealth>();
        }

        if (playerHealth == null)
        {
            playerHealth = FindObjectOfType<PlayerHealth>(true);
        }

        if (playerHealth == null)
        {
            return;
        }

        boundPlayerHealth = playerHealth;
        boundPlayerHealth.OnDeath -= HandlePlayerDeath;
        boundPlayerHealth.OnDeath += HandlePlayerDeath;
    }

    private void UnbindFromPlayerDeath()
    {
        if (boundPlayerHealth != null)
        {
            boundPlayerHealth.OnDeath -= HandlePlayerDeath;
            boundPlayerHealth = null;
        }

        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
            showRoutine = null;
        }
    }

    private void HandlePlayerDeath()
    {
        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
        }

        trueExpAwarded = false;
        SetButtonsVisible(false);
        SetTrueLevelUIVisible(false);
        RestartAllowed = false;
        showRoutine = StartCoroutine(ShowSequence());
    }

    private IEnumerator ShowSequence()
    {
        SetVisible(true);

        if (GameOverText != null)
        {
            GameOverText.gameObject.SetActive(true);
            GameOverText.text = "GAME OVER";
        }

        if (GameOverPanel != null)
        {
            GameOverPanel.SetActive(true);
        }

        if (PostMatchPanel != null)
        {
            PostMatchPanel.SetActive(false);
        }

        float delay = Mathf.Max(0f, PostMatchDelaySeconds);
        if (delay > 0f)
        {
            yield return new WaitForSecondsRealtime(delay);
        }

        if (GameOverText != null)
        {
            GameOverText.gameObject.SetActive(false);
        }

        RefreshPostMatchTexts();

        if (PostMatchPanel != null)
        {
            PostMatchPanel.SetActive(true);
        }

        TrueLevelRunTracker tracker = TrueLevelRunTracker.Instance;
        int totalTrueExp = tracker != null ? tracker.TotalTrueExp : 0;
        yield return AwardTrueExpRoutine(totalTrueExp);

        float btnDelay = Mathf.Max(0f, RestartButtonDelay);
        if (btnDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(btnDelay);
        }

        SetButtonsVisible(true);

        showRoutine = null;
    }

    private void RefreshPostMatchTexts()
    {
        TrueLevelRunTracker tracker = TrueLevelRunTracker.Instance;
        float damageDealt = tracker != null ? tracker.TotalDamageDealt : 0f;
        float totalTime = tracker != null ? tracker.TotalTimeSurvived : 0f;

        if (DamageDealtText != null)
        {
            if (DamageDealtValueText != null)
            {
                DamageDealtText.text = "Damage Dealt";
                DamageDealtValueText.text = Mathf.RoundToInt(damageDealt).ToString();
            }
            else
            {
                DamageDealtText.text = $"Damage Dealt: {Mathf.RoundToInt(damageDealt)}";
            }
        }

        if (TotalTimeText != null)
        {
            TimeSpan ts = TimeSpan.FromSeconds(Mathf.Max(0f, totalTime));
            string formatted = $"{ts.Minutes:D2}:{ts.Seconds:D2}";

            if (TotalTimeValueText != null)
            {
                TotalTimeText.text = "Total Time";
                TotalTimeValueText.text = formatted;
            }
            else
            {
                TotalTimeText.text = $"Total Time: {formatted}";
            }
        }

        SetRarityLine(CardRarity.Common, CommonKilledText, CommonKilledValueText, CommonExpText, CommonExpValueText, "Common", tracker);
        SetRarityLine(CardRarity.Uncommon, UncommonKilledText, UncommonKilledValueText, UncommonExpText, UncommonExpValueText, "Uncommon", tracker);
        SetRarityLine(CardRarity.Rare, RareKilledText, RareKilledValueText, RareExpText, RareExpValueText, "Rare", tracker);
        SetRarityLine(CardRarity.Epic, EpicKilledText, EpicKilledValueText, EpicExpText, EpicExpValueText, "Epic", tracker);
        SetRarityLine(CardRarity.Legendary, LegendaryKilledText, LegendaryKilledValueText, LegendaryExpText, LegendaryExpValueText, "Legendary", tracker);
        SetRarityLine(CardRarity.Mythic, MythicKilledText, MythicKilledValueText, MythicExpText, MythicExpValueText, "Mythic", tracker);
        SetRarityLine(CardRarity.Boss, BossKilledText, BossKilledValueText, BossExpText, BossExpValueText, "Boss", tracker);

        if (TotalTrueExpText != null)
        {
            int totalTrueExp = tracker != null ? tracker.TotalTrueExp : 0;
            if (TotalTrueExpValueText != null)
            {
                TotalTrueExpText.text = "Total True Exp";
                TotalTrueExpValueText.text = totalTrueExp.ToString();
            }
            else
            {
                TotalTrueExpText.text = $"Total True Exp: {totalTrueExp}";
            }

        }
    }

    private IEnumerator AwardTrueExpRoutine(int totalTrueExp)
    {
        if (trueExpAwarded)
        {
            yield break;
        }

        trueExpAwarded = true;

        TruePlayerLevel level = TruePlayerLevel.EnsureInstance();
        TruePlayerLevelUI ui = FindObjectOfType<TruePlayerLevelUI>(true);

        if (ui == null)
        {
            Debug.LogWarning("DeathScreenUI: TruePlayerLevelUI not found. Skipping true EXP award animation.");
            yield break;
        }

        if (!ui.gameObject.activeSelf)
        {
            ui.gameObject.SetActive(true);
        }

        if (!ui.enabled)
        {
            ui.enabled = true;
        }

        ui.SetRootVisible(true);

        bool finished = false;
        void HandleFinished() => finished = true;

        ui.OnAwardAnimationFinished -= HandleFinished;
        ui.OnAwardAnimationFinished += HandleFinished;

        ui.AwardTrueExp(totalTrueExp);

        if (!ui.IsAnimating)
        {
            finished = true;
        }

        while (!finished)
        {
            yield return null;
        }

        ui.OnAwardAnimationFinished -= HandleFinished;
    }

    private void SetTrueLevelUIVisible(bool visible)
    {
        TruePlayerLevelUI ui = FindObjectOfType<TruePlayerLevelUI>(true);
        if (ui != null)
        {
            ui.SetRootVisible(visible);
        }
    }

    private void SetButtonsVisible(bool visible)
    {
        RestartAllowed = visible;

        if (RetryButton != null)
        {
            RetryButton.gameObject.SetActive(visible);
            RetryButton.interactable = visible;
        }

        if (CharacterButton != null)
        {
            CharacterButton.gameObject.SetActive(visible);
            CharacterButton.interactable = visible;
        }
    }

    private void RegisterButtonHandlers()
    {
        UnregisterButtonHandlers();

        if (RetryButton != null)
        {
            retryAction ??= HandleRetryClicked;
            RetryButton.onClick.AddListener(retryAction);
        }

        if (CharacterButton != null)
        {
            characterAction ??= HandleCharacterClicked;
            CharacterButton.onClick.AddListener(characterAction);
        }
    }

    private void UnregisterButtonHandlers()
    {
        if (RetryButton != null && retryAction != null)
        {
            RetryButton.onClick.RemoveListener(retryAction);
        }

        if (CharacterButton != null && characterAction != null)
        {
            CharacterButton.onClick.RemoveListener(characterAction);
        }
    }

    private void HandleRetryClicked()
    {
        if (Time.timeScale == 0f)
        {
            Time.timeScale = 1f;
        }

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ResetRunState();
        }

        FavourEffect.ResetPickCounts();

        if (EnemyScalingSystem.Instance != null)
        {
            EnemyScalingSystem.Instance.ResetScaling();
        }

        if (ProjectileCardLevelSystem.Instance != null)
        {
            ProjectileCardLevelSystem.Instance.ResetAllLevels();
        }

        if (ProjectileCardModifiers.Instance != null)
        {
            ProjectileCardModifiers.Instance.ResetRunState();
        }

        HolyShield.ResetRunState();
        ReflectShield.ResetRunState();
        NullifyShield.ResetRunState();

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void HandleCharacterClicked()
    {
        if (Time.timeScale == 0f)
        {
            Time.timeScale = 1f;
        }

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ResetRunState();
        }

        FavourEffect.ResetPickCounts();

        if (EnemyScalingSystem.Instance != null)
        {
            EnemyScalingSystem.Instance.ResetScaling();
        }

        if (ProjectileCardLevelSystem.Instance != null)
        {
            ProjectileCardLevelSystem.Instance.ResetAllLevels();
        }

        if (ProjectileCardModifiers.Instance != null)
        {
            ProjectileCardModifiers.Instance.ResetRunState();
        }

        HolyShield.ResetRunState();
        ReflectShield.ResetRunState();
        NullifyShield.ResetRunState();

        StartingScreen.RequestAutoOpenLevelUpUIOnNextMenuLoad();
        SceneManager.LoadSceneAsync(MenuSceneName, LoadSceneMode.Single);
    }

    private static void SetRarityLine(CardRarity rarity, TMP_Text killedLabelText, TMP_Text killedValueText, TMP_Text expLabelText, TMP_Text expValueText, string label, TrueLevelRunTracker tracker)
    {
        int kills = tracker != null ? tracker.GetKills(rarity) : 0;
        int exp = tracker != null ? tracker.GetTrueExp(rarity) : 0;

        if (killedLabelText != null)
        {
            if (killedValueText != null)
            {
                killedLabelText.text = $"{label} Enemies Killed";
                killedValueText.text = kills.ToString();
            }
            else
            {
                killedLabelText.text = $"{label} Enemies Killed: {kills}";
            }
        }

        if (expLabelText != null)
        {
            if (expValueText != null)
            {
                expLabelText.text = $"{label} True Exp";
                expValueText.text = exp.ToString();
            }
            else
            {
                expLabelText.text = $"{label} True Exp: {exp}";
            }
        }
    }

    private void SetVisible(bool visible)
    {
        GameObject rootObject = Root != null ? Root : gameObject;

        if (rootObject != null && rootObject != gameObject)
        {
            rootObject.SetActive(visible);
        }

        if (RootCanvasGroup != null)
        {
            RootCanvasGroup.alpha = visible ? 1f : 0f;
            RootCanvasGroup.interactable = visible;
            RootCanvasGroup.blocksRaycasts = visible;
        }

        if (!visible)
        {
            if (GameOverPanel != null) GameOverPanel.SetActive(false);
            if (PostMatchPanel != null) PostMatchPanel.SetActive(false);
        }
    }
}
