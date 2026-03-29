using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class LevelUpUI : MonoBehaviour
{
    [Serializable]
    public sealed class StatRow
    {
        public string statName;

        [Header("UI")]
        public TMP_Text nameText;
        public Button nameButton;
        public GameObject selectedOutline;

        [Header("Value")]
        public TMP_Text valueText;
        public Button decreaseButton;
        public Button increaseButton;

        [Header("Defaults")]
        public int baseValue = 5;

        public float NormalStatValue;
        public float EnhancedStatValue;

        [NonSerialized]
        public int currentValue;

        [NonSerialized]
        public UnityAction selectAction;

        [NonSerialized]
        public UnityAction decreaseAction;

        [NonSerialized]
        public UnityAction increaseAction;

        public int Allocated => Mathf.Max(0, currentValue - baseValue);
    }

    private void SyncStatDefaultsToPrefs()
    {
        if (Stats == null)
        {
            return;
        }

        PlayerPrefs.SetInt("LevelUpUI.RequiredPointsForEnhanced", Mathf.Max(1, RequiredPointsForEnhanced));

        for (int i = 0; i < Stats.Count; i++)
        {
            StatRow row = Stats[i];
            if (row == null) continue;

            string key = NormalizeStatKey(string.IsNullOrEmpty(row.statName) ? $"Stat{i}" : row.statName);

            if (string.Equals(key, "Vitality", StringComparison.Ordinal))
            {
                row.NormalStatValue = 5f;
                row.EnhancedStatValue = 0.5f;
            }

            int newBaseValue = Mathf.Max(0, row.baseValue);
            string baseValueKey = $"LevelUpUI.{key}.BaseValue";
            string currentValueKey = $"LevelUpUI.{key}";

            if (string.Equals(key, "Vitality", StringComparison.Ordinal))
            {
                const string vitalityReworkKey = "LevelUpUI.Vitality.ReworkVersion";
                int version = PlayerPrefs.GetInt(vitalityReworkKey, 0);
                if (version < 2)
                {
                    PlayerPrefs.SetFloat($"LevelUpUI.{key}.NormalStatValue", 5f);
                    PlayerPrefs.SetFloat($"LevelUpUI.{key}.EnhancedStatValue", 0.5f);
                    PlayerPrefs.SetInt(vitalityReworkKey, 2);
                }
            }

            if (PlayerPrefs.HasKey(currentValueKey))
            {
                int oldBaseValue;
                if (PlayerPrefs.HasKey(baseValueKey))
                {
                    oldBaseValue = PlayerPrefs.GetInt(baseValueKey, newBaseValue);
                }
                else
                {
                    switch (key)
                    {
                        case "Intelligence":
                        case "Agility":
                        case "Willpower":
                        case "Vitality":
                            oldBaseValue = 5;
                            break;
                        default:
                            oldBaseValue = newBaseValue;
                            break;
                    }
                }
                int oldCurrentValue = PlayerPrefs.GetInt(currentValueKey, newBaseValue);
                int allocated = Mathf.Max(0, oldCurrentValue - oldBaseValue);
                PlayerPrefs.SetInt(currentValueKey, newBaseValue + allocated);
            }

            PlayerPrefs.SetInt(baseValueKey, newBaseValue);
            PlayerPrefs.SetFloat($"LevelUpUI.{key}.NormalStatValue", row.NormalStatValue);
            PlayerPrefs.SetFloat($"LevelUpUI.{key}.EnhancedStatValue", row.EnhancedStatValue);
        }

        PlayerPrefs.Save();
    }

    [Header("Root")]
    public GameObject Root;
    public CanvasGroup RootCanvasGroup;

    [Header("Background")]
    public Image BackgroundImage;
    public Sprite BackgroundSprite;
    public RawImage BackgroundRawImage;
    public Texture BackgroundTexture;
    public Color BackgroundColor = Color.black;

    [Header("Player Stats Preview UI (optional)")]
    public LevelUpPlayerStatsPreviewUI PlayerStatsPreviewUI;

    [Header("Stats")]
    public List<StatRow> Stats = new List<StatRow>();

    [Header("Points")]
    public int TotalStatPoints = 10;

    public int RequiredPointsForEnhanced = 5;

    [Tooltip("Optional: label text (e.g. 'Remaining Stat Points :')")]
    public TMP_Text RemainingPointsLabelText;

    [Tooltip("Optional: numeric value text (e.g. '10' aligned right)")]
    public TMP_Text RemainingPointsValueText;

    [Header("Buttons")]
    public Button LevelUpButton;
    public Button PlayButton;

    [Header("Events")]
    public UnityEvent OnLevelUpConfirmed;

    public event Action<Dictionary<string, int>> OnLevelUpConfirmedWithStats;

    public event Action OnStatAllocationChanged;

    private StartingScreen startingScreen;
    private int selectedIndex = -1;
    private bool listenersRegistered;

    private UnityAction levelUpAction;
    private UnityAction playAction;

    private void Awake()
    {
        InitializeDefaults();
        SyncStatDefaultsToPrefs();
        LoadPersistedCurrentValues();
        RegisterListeners();
        SetVisible(false);

        ApplyBackground();
        SetPlayerStatsPreviewUIEnabled(false);

        if (LevelUpButton != null)
        {
            LevelUpButton.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        RegisterListeners();
        RefreshAll();
    }

    private void OnDisable()
    {
        UnregisterListeners();
    }

    public void Show(StartingScreen screen)
    {
        startingScreen = screen;

        SyncStatDefaultsToPrefs();
        LoadPersistedCurrentValues();
        if (selectedIndex < 0 && Stats != null && Stats.Count > 0)
        {
            SelectStat(0);
        }

        SetVisible(true);
        ApplyBackground();
        SetPlayerStatsPreviewUIEnabled(true);

        TruePlayerLevelUI trueLevelUI = FindObjectOfType<TruePlayerLevelUI>(true);
        if (trueLevelUI != null)
        {
            trueLevelUI.SetRootVisible(true);
        }

        RefreshAll();
        OnStatAllocationChanged?.Invoke();
    }

    public void Hide()
    {
        SetPlayerStatsPreviewUIEnabled(false);
        SetVisible(false);

        TruePlayerLevelUI ui = FindObjectOfType<TruePlayerLevelUI>(true);
        if (ui != null)
        {
            ui.SetRootVisible(false);
        }
    }

    private int GetEffectiveTotalStatPoints()
    {
        int basePoints = Mathf.Max(0, TotalStatPoints);

        TruePlayerLevel trueLevel = TruePlayerLevel.Instance;
        if (trueLevel == null)
        {
            trueLevel = FindObjectOfType<TruePlayerLevel>(true);
        }

        int bonus = trueLevel != null ? Mathf.Max(0, trueLevel.CurrentLevel - 1) : 0;
        return basePoints + bonus;
    }

    private void ApplyBackground()
    {
        if (startingScreen != null && startingScreen.MenuCamera != null)
        {
            startingScreen.MenuCamera.clearFlags = CameraClearFlags.SolidColor;
            startingScreen.MenuCamera.backgroundColor = BackgroundColor;
        }

        if (BackgroundRawImage != null && BackgroundTexture != null)
        {
            BackgroundRawImage.texture = BackgroundTexture;
            BackgroundRawImage.color = BackgroundColor;
            BackgroundRawImage.enabled = true;
            return;
        }

        if (BackgroundImage == null)
        {
            return;
        }

        if (BackgroundSprite != null)
        {
            BackgroundImage.sprite = BackgroundSprite;
            BackgroundImage.preserveAspect = true;
        }

        BackgroundImage.color = BackgroundColor;
        BackgroundImage.enabled = true;
    }

    private void SetPlayerStatsPreviewUIEnabled(bool enabled)
    {
        if (PlayerStatsPreviewUI == null)
        {
            PlayerStatsPreviewUI = FindObjectOfType<LevelUpPlayerStatsPreviewUI>(true);
        }

        if (PlayerStatsPreviewUI != null)
        {
            PlayerStatsPreviewUI.enabled = enabled;
        }
    }

    public int GetRemainingPoints()
    {
        int allocated = 0;
        if (Stats != null)
        {
            for (int i = 0; i < Stats.Count; i++)
            {
                StatRow row = Stats[i];
                if (row == null) continue;
                allocated += row.Allocated;
            }
        }

        return Mathf.Max(0, GetEffectiveTotalStatPoints() - allocated);
    }

    public Dictionary<string, int> GetCurrentStatValues()
    {
        Dictionary<string, int> dict = new Dictionary<string, int>(StringComparer.Ordinal);
        if (Stats == null) return dict;

        for (int i = 0; i < Stats.Count; i++)
        {
            StatRow row = Stats[i];
            if (row == null) continue;

            string rawKey = string.IsNullOrEmpty(row.statName) ? $"Stat{i}" : row.statName;
            string key = NormalizeStatKey(rawKey);
            dict[key] = row.currentValue;
        }

        return dict;
    }

    private static string NormalizeStatKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return key;

        string trimmed = key.Trim();
        if (trimmed.Equals("Intelligence", StringComparison.OrdinalIgnoreCase)) return "Intelligence";
        if (trimmed.Equals("Agility", StringComparison.OrdinalIgnoreCase)) return "Agility";
        if (trimmed.Equals("Willpower", StringComparison.OrdinalIgnoreCase)) return "Willpower";
        if (trimmed.Equals("Vitality", StringComparison.OrdinalIgnoreCase)) return "Vitality";
        return trimmed;
    }

    private void InitializeDefaults()
    {
        if (Stats == null) return;

        for (int i = 0; i < Stats.Count; i++)
        {
            StatRow row = Stats[i];
            if (row == null) continue;

            string normalized = NormalizeStatKey(row.statName);
            row.baseValue = Mathf.Max(0, row.baseValue);

            row.currentValue = Mathf.Max(0, row.baseValue);

            if (Mathf.Approximately(row.NormalStatValue, 0f) && Mathf.Approximately(row.EnhancedStatValue, 0f))
            {
                switch (normalized)
                {
                    case "Intelligence":
                        row.NormalStatValue = 0.25f;
                        row.EnhancedStatValue = 5f;
                        break;
                    case "Agility":
                        row.NormalStatValue = 0.5f;
                        row.EnhancedStatValue = 2f;
                        break;
                    case "Willpower":
                        row.NormalStatValue = 0.05f;
                        row.EnhancedStatValue = 20f;
                        break;
                    case "Vitality":
                        row.NormalStatValue = 5f;
                        row.EnhancedStatValue = 0.5f;
                        break;
                }
            }

            if (string.Equals(normalized, "Vitality", StringComparison.Ordinal))
            {
                row.NormalStatValue = 5f;
                row.EnhancedStatValue = 0.5f;
            }

            if (row.nameText != null)
            {
                row.nameText.text = string.IsNullOrEmpty(row.statName) ? row.nameText.text : row.statName;
            }
        }

        if (selectedIndex < 0 && Stats.Count > 0)
        {
            selectedIndex = 0;
        }
    }

    private void LoadPersistedCurrentValues()
    {
        if (Stats == null) return;

        RequiredPointsForEnhanced = Mathf.Max(1, PlayerPrefs.GetInt("LevelUpUI.RequiredPointsForEnhanced", RequiredPointsForEnhanced));

        for (int i = 0; i < Stats.Count; i++)
        {
            StatRow row = Stats[i];
            if (row == null) continue;

            string rawKey = string.IsNullOrEmpty(row.statName) ? $"Stat{i}" : row.statName;
            string key = NormalizeStatKey(rawKey);
            string legacyKey = rawKey;

            row.baseValue = Mathf.Max(0, row.baseValue);

            string prefsKey = $"LevelUpUI.{key}";
            if (PlayerPrefs.HasKey(prefsKey))
            {
                int saved = PlayerPrefs.GetInt(prefsKey, row.baseValue);
                row.currentValue = Mathf.Max(row.baseValue, saved);
            }
            else if (!string.Equals(legacyKey, key, StringComparison.Ordinal) && PlayerPrefs.HasKey($"LevelUpUI.{legacyKey}"))
            {
                int saved = PlayerPrefs.GetInt($"LevelUpUI.{legacyKey}", row.baseValue);
                row.currentValue = Mathf.Max(row.baseValue, saved);
                PlayerPrefs.SetInt(prefsKey, row.currentValue);
            }
            else
            {
                row.currentValue = Mathf.Max(row.baseValue, row.currentValue);
            }
        }

        PlayerPrefs.Save();
    }

    private void CommitCurrentValuesToPrefs()
    {
        Dictionary<string, int> values = GetCurrentStatValues();

        PlayerPrefs.SetInt("LevelUpUI.RequiredPointsForEnhanced", Mathf.Max(1, RequiredPointsForEnhanced));

        foreach (var kvp in values)
        {
            PlayerPrefs.SetInt($"LevelUpUI.{kvp.Key}", kvp.Value);
        }

        PlayerPrefs.Save();

        PlayerStats stats = null;

        if (AdvancedPlayerController.Instance != null)
        {
            stats = AdvancedPlayerController.Instance.GetComponent<PlayerStats>();
        }

        if (stats == null)
        {
            PlayerStats[] all = FindObjectsOfType<PlayerStats>(true);
            for (int i = 0; i < all.Length; i++)
            {
                PlayerStats candidate = all[i];
                if (candidate == null) continue;
                if (candidate.gameObject == null) continue;
                if ((candidate.gameObject.hideFlags & HideFlags.DontSave) != 0) continue;
                if (candidate.GetComponent<PlayerHealth>() == null && candidate.GetComponent<PlayerMana>() == null)
                {
                    continue;
                }

                stats = candidate;
                break;
            }
        }

        if (stats != null)
        {
            stats.ReapplyLevelUpAllocationsFromPrefs(fillToMax: true, refillMana: true);
        }

        OnLevelUpConfirmed?.Invoke();
        OnLevelUpConfirmedWithStats?.Invoke(values);
    }

    private void RegisterListeners()
    {
        if (listenersRegistered)
        {
            return;
        }

        listenersRegistered = true;

        if (Stats != null)
        {
            for (int i = 0; i < Stats.Count; i++)
            {
                int index = i;
                StatRow row = Stats[i];
                if (row == null) continue;

                if (row.nameButton != null)
                {
                    row.selectAction ??= () => SelectStat(index);
                    row.nameButton.onClick.AddListener(row.selectAction);
                }

                if (row.decreaseButton != null)
                {
                    row.decreaseAction ??= () => DecreaseStat(index);
                    row.decreaseButton.onClick.AddListener(row.decreaseAction);
                }

                if (row.increaseButton != null)
                {
                    row.increaseAction ??= () => IncreaseStat(index);
                    row.increaseButton.onClick.AddListener(row.increaseAction);
                }
            }
        }

        // LevelUpButton is deprecated: stats are committed when pressing Play.

        if (PlayButton != null)
        {
            playAction ??= HandlePlayClicked;
            PlayButton.onClick.AddListener(playAction);
        }
    }

    private void UnregisterListeners()
    {
        if (!listenersRegistered)
        {
            return;
        }

        listenersRegistered = false;

        if (Stats != null)
        {
            for (int i = 0; i < Stats.Count; i++)
            {
                StatRow row = Stats[i];
                if (row == null) continue;

                if (row.nameButton != null)
                {
                    if (row.selectAction != null)
                    {
                        row.nameButton.onClick.RemoveListener(row.selectAction);
                    }
                }

                if (row.decreaseButton != null)
                {
                    if (row.decreaseAction != null)
                    {
                        row.decreaseButton.onClick.RemoveListener(row.decreaseAction);
                    }
                }

                if (row.increaseButton != null)
                {
                    if (row.increaseAction != null)
                    {
                        row.increaseButton.onClick.RemoveListener(row.increaseAction);
                    }
                }
            }
        }

        if (LevelUpButton != null && levelUpAction != null)
        {
            LevelUpButton.onClick.RemoveListener(levelUpAction);
        }

        if (PlayButton != null)
        {
            if (playAction != null)
            {
                PlayButton.onClick.RemoveListener(playAction);
            }
        }
    }

    private void SetVisible(bool visible)
    {
        GameObject root = Root != null ? Root : gameObject;

        if (RootCanvasGroup != null)
        {
            RootCanvasGroup.alpha = visible ? 1f : 0f;
            RootCanvasGroup.interactable = visible;
            RootCanvasGroup.blocksRaycasts = visible;
        }

        if (root != null)
        {
            root.SetActive(visible);
        }
    }

    private void RefreshAll()
    {
        RefreshSelectionVisuals();
        RefreshValues();
        RefreshRemainingPoints();
    }

    private void RefreshSelectionVisuals()
    {
        if (Stats == null) return;

        for (int i = 0; i < Stats.Count; i++)
        {
            StatRow row = Stats[i];
            if (row == null) continue;

            if (row.selectedOutline != null)
            {
                row.selectedOutline.SetActive(i == selectedIndex);
            }
        }
    }

    private void RefreshValues()
    {
        if (Stats == null) return;

        for (int i = 0; i < Stats.Count; i++)
        {
            StatRow row = Stats[i];
            if (row == null) continue;

            if (row.valueText != null)
            {
                row.valueText.text = row.currentValue.ToString();
            }
        }
    }

    private void RefreshRemainingPoints()
    {
        int remaining = GetRemainingPoints();

        if (RemainingPointsLabelText != null)
        {
            RemainingPointsLabelText.text = "Remaining Stat Points :";
        }

        if (RemainingPointsValueText != null)
        {
            RemainingPointsValueText.text = remaining.ToString();
        }
    }

    private void SelectStat(int index)
    {
        if (Stats == null || Stats.Count == 0)
        {
            selectedIndex = -1;
            return;
        }

        int clamped = Mathf.Clamp(index, 0, Stats.Count - 1);
        if (selectedIndex == clamped)
        {
            return;
        }

        selectedIndex = clamped;
        RefreshSelectionVisuals();
    }

    private void IncreaseStat(int index)
    {
        if (Stats == null) return;
        if (index < 0 || index >= Stats.Count) return;

        StatRow row = Stats[index];
        if (row == null) return;

        int remaining = GetRemainingPoints();
        if (remaining <= 0)
        {
            return;
        }

        row.currentValue++;
        RefreshValues();
        RefreshRemainingPoints();
        OnStatAllocationChanged?.Invoke();
    }

    private void DecreaseStat(int index)
    {
        if (Stats == null) return;
        if (index < 0 || index >= Stats.Count) return;

        StatRow row = Stats[index];
        if (row == null) return;

        if (row.currentValue <= row.baseValue)
        {
            row.currentValue = row.baseValue;
            return;
        }

        row.currentValue--;
        RefreshValues();
        RefreshRemainingPoints();
        OnStatAllocationChanged?.Invoke();
    }

    private void HandleLevelUpClicked() { }

    private void HandlePlayClicked()
    {
        CommitCurrentValuesToPrefs();

        if (startingScreen != null)
        {
            startingScreen.StartNewGame();
            return;
        }

        Debug.LogWarning("LevelUpUI: StartingScreen reference missing. Play button can't start the game.");
    }
}
