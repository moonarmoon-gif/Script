using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;

public sealed class LevelUpPlayerStatsPreviewUI : MonoBehaviour
{
    private static readonly HashSet<string> AllowedAllStatsFields = new HashSet<string>(StringComparer.Ordinal)
    {
        nameof(PlayerStats.maxHealth),
        nameof(PlayerStats.maxMana),
        nameof(PlayerStats.baseAttack),
        nameof(PlayerStats.critChance),
        nameof(PlayerStats.critDamage),
        nameof(PlayerStats.luck),
        nameof(PlayerStats.experienceMultiplier),
        nameof(PlayerStats.damageMultiplier),
        nameof(PlayerStats.armor),
        nameof(PlayerStats.manaRegenPerSecond),
        nameof(PlayerStats.healthRegenPerSecond),
        nameof(PlayerStats.AttackSpeedBonus),
        nameof(PlayerStats.FavourInterval),
        nameof(PlayerStats.Cooldown)
    };

    [Serializable]
    private sealed class StatTextOverride
    {
        public string FieldName;
        public string DisplayName;
        public bool OverrideSuffix;
        public string Suffix;
    }
    [Serializable]
    public struct StatContributions
    {
        public float Intelligence;
        public float Agility;
        public float Willpower;
        public float Vitality;

        public float Evaluate(IReadOnlyDictionary<string, int> points)
        {
            float total = 0f;
            total += Intelligence * Get(points, "Intelligence");
            total += Agility * Get(points, "Agility");
            total += Willpower * Get(points, "Willpower");
            total += Vitality * Get(points, "Vitality");
            return total;
        }

        private static int Get(IReadOnlyDictionary<string, int> dict, string key)
        {
            if (dict == null) return 0;
            if (dict.TryGetValue(key, out int value)) return value;
            return 0;
        }
    }

    public enum DisplayFormat
    {
        Float,
        Integer,
        Percent,
        Seconds
    }

    [Serializable]
    public sealed class StatLine
    {
        public TMP_Text ValueText;

        public DisplayFormat Format = DisplayFormat.Float;

        public float BaseValue;

        public StatContributions PerPoint;

        public int Decimals = 0;

        public bool NegativeDeltaIsPositive = false;
    }

    [Header("References")]
    [SerializeField] private LevelUpUI levelUpUI;
    [SerializeField] private GameObject statsSourcePrefab;
    [SerializeField] private PlayerStats statsSource;
    [SerializeField] private TMP_Text allStatsText;
    public TMP_Text AllStatsValues;

    [Header("Colors")]
    [SerializeField] private Color positiveDeltaColor = new Color(0f, 1f, 0f, 1f);
    [SerializeField] private Color negativeDeltaColor = new Color(1f, 0f, 0f, 1f);

    [Header("Lines")]
    [SerializeField] private List<StatLine> statLines = new List<StatLine>();

    [Header("Stat Text Overrides")]
    [SerializeField] private List<StatTextOverride> statTextOverrides = new List<StatTextOverride>();

    private readonly Dictionary<string, int> basePoints = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly Dictionary<string, int> currentPoints = new Dictionary<string, int>(StringComparer.Ordinal);

    private sealed class FieldCache
    {
        public readonly List<FieldInfo> Fields = new List<FieldInfo>();

        public void Rebuild(Type type)
        {
            Fields.Clear();
            if (type == null) return;

            FieldInfo[] infos = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
            if (infos == null) return;

            Array.Sort(infos, static (a, b) => a.MetadataToken.CompareTo(b.MetadataToken));
            for (int i = 0; i < infos.Length; i++)
            {
                FieldInfo fi = infos[i];
                if (fi == null) continue;
                Fields.Add(fi);
            }
        }
    }

    private readonly FieldCache fieldCache = new FieldCache();

    private void Awake()
    {
        if (levelUpUI == null)
        {
            levelUpUI = FindObjectOfType<LevelUpUI>(true);
        }

        if (statsSource == null)
        {
            statsSource = CreatePreviewStatsSource();
        }

        if (statsSource != null)
        {
            fieldCache.Rebuild(statsSource.GetType());
        }
    }

    private PlayerStats CreatePreviewStatsSource()
    {
        GameObject temp = new GameObject("__PlayerStatsPreview__");
        temp.hideFlags = HideFlags.HideAndDontSave;

        PlayerStats created = temp.AddComponent<PlayerStats>();

        if (statsSourcePrefab != null)
        {
            PlayerStats prefabStats = statsSourcePrefab.GetComponent<PlayerStats>();
            if (prefabStats != null)
            {
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(prefabStats), created);
            }
        }

        return created;
    }

    private void OnEnable()
    {
        if (levelUpUI == null)
        {
            levelUpUI = FindObjectOfType<LevelUpUI>(true);
        }

        if (statsSource == null)
        {
            statsSource = CreatePreviewStatsSource();
        }

        if (levelUpUI != null)
        {
            levelUpUI.OnStatAllocationChanged += HandleAllocationChanged;
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (levelUpUI != null)
        {
            levelUpUI.OnStatAllocationChanged -= HandleAllocationChanged;
        }
    }

    private void HandleAllocationChanged()
    {
        Refresh();
    }

    private bool IsPositiveDeltaForField(string fieldName, float delta)
    {
        if (delta == 0f)
        {
            return false;
        }

        switch (fieldName)
        {
            case nameof(PlayerStats.Cooldown):
            case nameof(PlayerStats.FavourInterval):
                return delta < 0f;
            default:
                return delta > 0f;
        }
    }

    private struct AttributeBonuses
    {
        public float MaxHealth;
        public float MaxMana;
        public float BaseAttack;
        public float CritChance;
        public float AttackSpeedBonus;
        public float ManaRegenPerSecond;
        public float HealthRegenPerSecond;
        public float Cooldown;
    }

    private int GetRequiredPointsForEnhanced()
    {
        int fallback = 5;
        if (levelUpUI != null)
        {
            fallback = Mathf.Max(1, levelUpUI.RequiredPointsForEnhanced);
        }

        return Mathf.Max(1, PlayerPrefs.GetInt("LevelUpUI.RequiredPointsForEnhanced", fallback));
    }

    private void GetStatTuning(string statName, float defaultNormal, float defaultEnhanced, out float normal, out float enhanced)
    {
        normal = defaultNormal;
        enhanced = defaultEnhanced;

        string prefsNormalKey = $"LevelUpUI.{statName}.NormalStatValue";
        if (PlayerPrefs.HasKey(prefsNormalKey))
        {
            normal = PlayerPrefs.GetFloat(prefsNormalKey, defaultNormal);
        }

        string prefsEnhancedKey = $"LevelUpUI.{statName}.EnhancedStatValue";
        if (PlayerPrefs.HasKey(prefsEnhancedKey))
        {
            enhanced = PlayerPrefs.GetFloat(prefsEnhancedKey, defaultEnhanced);
        }

        if (PlayerPrefs.HasKey(prefsNormalKey) || PlayerPrefs.HasKey(prefsEnhancedKey))
        {
            return;
        }

        if (levelUpUI != null && levelUpUI.Stats != null)
        {
            for (int i = 0; i < levelUpUI.Stats.Count; i++)
            {
                LevelUpUI.StatRow row = levelUpUI.Stats[i];
                if (row == null) continue;
                if (!string.Equals(row.statName, statName, StringComparison.Ordinal)) continue;

                if (!Mathf.Approximately(row.NormalStatValue, 0f))
                {
                    normal = row.NormalStatValue;
                }

                if (!Mathf.Approximately(row.EnhancedStatValue, 0f))
                {
                    enhanced = row.EnhancedStatValue;
                }

                return;
            }
        }
    }

    private AttributeBonuses EvaluateAttributeBonuses(IReadOnlyDictionary<string, int> points)
    {
        int intelligence = Get(points, "Intelligence");
        int agility = Get(points, "Agility");
        int willpower = Get(points, "Willpower");
        int vitality = Get(points, "Vitality");

        int requiredPointsForEnhanced = GetRequiredPointsForEnhanced();

        GetStatTuning("Intelligence", 0.2f, 5f, out float intelligenceNormal, out float intelligenceEnhanced);
        GetStatTuning("Agility", 0.5f, 2f, out float agilityNormal, out float agilityEnhanced);
        GetStatTuning("Willpower", 0.05f, 20f, out float willpowerNormal, out float willpowerEnhanced);
        GetStatTuning("Vitality", 5f, 0.5f, out float vitalityNormal, out float vitalityEnhanced);

        int intelligenceEnhancedCount = intelligence / requiredPointsForEnhanced;
        int agilityEnhancedCount = agility / requiredPointsForEnhanced;
        int willpowerEnhancedCount = willpower / requiredPointsForEnhanced;
        int vitalityEnhancedCount = vitality / 5;

        AttributeBonuses bonuses = new AttributeBonuses
        {
            // Intelligence
            Cooldown = intelligence * -intelligenceNormal,
            BaseAttack = intelligenceEnhancedCount * intelligenceEnhanced,

            // Agility
            AttackSpeedBonus = agility * agilityNormal,
            CritChance = agilityEnhancedCount * agilityEnhanced,

            // Willpower
            ManaRegenPerSecond = willpower * willpowerNormal,
            MaxMana = willpowerEnhancedCount * willpowerEnhanced,

            // Vitality
            MaxHealth = vitality * vitalityNormal,
            HealthRegenPerSecond = vitalityEnhancedCount * vitalityEnhanced
        };

        return bonuses;
    }

    private void Refresh()
    {
        BuildPointCaches();

        if (allStatsText != null || AllStatsValues != null)
        {
            RefreshAllStatsText();
            return;
        }

        if (statLines == null) return;

        for (int i = 0; i < statLines.Count; i++)
        {
            StatLine line = statLines[i];
            if (line == null || line.ValueText == null) continue;

            float baseValue = line.BaseValue + EvaluateStatLineContribution(line, basePoints);
            float currentValue = line.BaseValue + EvaluateStatLineContribution(line, currentPoints);
            float delta = currentValue - baseValue;

            string currentStr = FormatValue(currentValue, line.Format, line.Decimals);

            if (Mathf.Abs(delta) <= 0.0001f)
            {
                line.ValueText.text = currentStr;
                continue;
            }

            string deltaStr = FormatValue(delta, line.Format, line.Decimals);
            bool isPositive = line.NegativeDeltaIsPositive ? (delta < 0f) : (delta > 0f);
            string sign = (delta > 0f) ? "+" : string.Empty;
            Color c = isPositive ? positiveDeltaColor : negativeDeltaColor;
            string color = ColorUtility.ToHtmlStringRGBA(c);

            line.ValueText.text = $"{currentStr}<color=#{color}>({sign}{deltaStr})</color>";
        }
    }

    private float EvaluateStatLineContribution(StatLine line, IReadOnlyDictionary<string, int> points)
    {
        if (line == null)
        {
            return 0f;
        }

        float i = line.PerPoint.Intelligence;
        float a = line.PerPoint.Agility;
        float w = line.PerPoint.Willpower;
        float v = line.PerPoint.Vitality;

        int nonZero = 0;
        string attr = null;
        float coeff = 0f;

        if (Mathf.Abs(i) > 0.0001f)
        {
            nonZero++;
            attr = "Intelligence";
            coeff = i;
        }

        if (Mathf.Abs(a) > 0.0001f)
        {
            nonZero++;
            attr = "Agility";
            coeff = a;
        }

        if (Mathf.Abs(w) > 0.0001f)
        {
            nonZero++;
            attr = "Willpower";
            coeff = w;
        }

        if (Mathf.Abs(v) > 0.0001f)
        {
            nonZero++;
            attr = "Vitality";
            coeff = v;
        }

        if (nonZero == 1)
        {
            int requiredPointsForEnhanced = GetRequiredPointsForEnhanced();

            if (string.Equals(attr, "Intelligence", StringComparison.Ordinal))
            {
                GetStatTuning("Intelligence", 0.2f, 5f, out _, out float enhanced);
                if (Mathf.Abs(coeff - enhanced) <= 0.0001f)
                {
                    return (Get(points, "Intelligence") / requiredPointsForEnhanced) * coeff;
                }
            }
            else if (string.Equals(attr, "Agility", StringComparison.Ordinal))
            {
                GetStatTuning("Agility", 0.5f, 2f, out _, out float enhanced);
                if (Mathf.Abs(coeff - enhanced) <= 0.0001f)
                {
                    return (Get(points, "Agility") / requiredPointsForEnhanced) * coeff;
                }
            }
            else if (string.Equals(attr, "Willpower", StringComparison.Ordinal))
            {
                GetStatTuning("Willpower", 0.05f, 20f, out _, out float enhanced);
                if (Mathf.Abs(coeff - enhanced) <= 0.0001f)
                {
                    return (Get(points, "Willpower") / requiredPointsForEnhanced) * coeff;
                }
            }
            else if (string.Equals(attr, "Vitality", StringComparison.Ordinal))
            {
                GetStatTuning("Vitality", 5f, 0.5f, out _, out float enhanced);
                if (Mathf.Abs(coeff - enhanced) <= 0.0001f)
                {
                    return (Get(points, "Vitality") / 5) * coeff;
                }
            }
        }

        return line.PerPoint.Evaluate(points);
    }

    private void RefreshAllStatsText()
    {
        if (statsSource == null)
        {
            if (allStatsText != null) allStatsText.text = string.Empty;
            if (AllStatsValues != null) AllStatsValues.text = string.Empty;
            return;
        }

        if (fieldCache.Fields.Count == 0)
        {
            fieldCache.Rebuild(statsSource.GetType());
        }

        AttributeBonuses baseBonus = EvaluateAttributeBonuses(basePoints);
        AttributeBonuses currentBonus = EvaluateAttributeBonuses(currentPoints);

        StringBuilder labelsSb = new StringBuilder(2048);
        StringBuilder valuesSb = new StringBuilder(2048);

        for (int i = 0; i < fieldCache.Fields.Count; i++)
        {
            FieldInfo fi = fieldCache.Fields[i];
            if (fi == null) continue;

            string fieldName = fi.Name;

            if (!AllowedAllStatsFields.Contains(fieldName))
            {
                continue;
            }

            if (fieldName == nameof(PlayerStats.FavourInterval) && CardSelectionManager.Instance != null)
            {
                float interval = CardSelectionManager.Instance.FavourCardInterval;
                AppendLine(labelsSb, valuesSb, fieldName, interval, 0f);
                continue;
            }

            object raw = fi.GetValue(statsSource);

            bool isFloat = raw is float;
            bool isInt = raw is int;
            bool isNumber = isFloat || isInt;
            if (!isNumber) continue;

            if (isInt)
            {
                float asFloat = (int)raw;
                AppendLine(labelsSb, valuesSb, fieldName, asFloat, 0f);
                continue;
            }

            float baseValue = (float)raw;
            float currentValue = baseValue;

            ApplyBonuses(fieldName, baseBonus, ref baseValue);
            ApplyBonuses(fieldName, currentBonus, ref currentValue);

            float delta = currentValue - baseValue;

            AppendLine(labelsSb, valuesSb, fieldName, currentValue, delta);
        }

        if (allStatsText != null)
        {
            allStatsText.text = labelsSb.ToString();
        }

        if (AllStatsValues != null)
        {
            AllStatsValues.text = valuesSb.ToString();
        }
    }

    private static void ApplyBonuses(string fieldName, AttributeBonuses bonuses, ref float value)
    {
        switch (fieldName)
        {
            case nameof(PlayerStats.maxHealth):
                value += bonuses.MaxHealth;
                break;
            case nameof(PlayerStats.maxMana):
                value += bonuses.MaxMana;
                break;
            case nameof(PlayerStats.baseAttack):
                value += bonuses.BaseAttack;
                break;
            case nameof(PlayerStats.critChance):
                value += bonuses.CritChance;
                break;
            case nameof(PlayerStats.AttackSpeedBonus):
                value += bonuses.AttackSpeedBonus;
                break;
            case nameof(PlayerStats.manaRegenPerSecond):
                value += bonuses.ManaRegenPerSecond;
                break;
            case nameof(PlayerStats.healthRegenPerSecond):
                value += bonuses.HealthRegenPerSecond;
                break;
            case nameof(PlayerStats.armor):
                break;
            case nameof(PlayerStats.Cooldown):
                value += bonuses.Cooldown;
                break;
        }
    }

    private static bool IsPercentFieldName(string fieldName)
    {
        if (string.IsNullOrEmpty(fieldName)) return false;

        switch (fieldName)
        {
            case nameof(PlayerStats.AttackSpeedBonus):
            case nameof(PlayerStats.critChance):
            case nameof(PlayerStats.critDamage):
            case nameof(PlayerStats.experienceMultiplier):
            case nameof(PlayerStats.damageMultiplier):
            case nameof(PlayerStats.Cooldown):
                return true;
            default:
                return false;
        }
    }

    private static bool IsFractionPercentFieldName(string fieldName)
    {
        return fieldName == nameof(PlayerStats.projectileManaCostReduction);
    }

    private static bool IsSecondsFieldName(string fieldName)
    {
        return fieldName == nameof(PlayerStats.FavourInterval);
    }

    private static bool IsIntegerFieldName(string fieldName)
    {
        return fieldName == nameof(PlayerStats.baseAttack)
               || fieldName == nameof(PlayerStats.armor)
               || fieldName == nameof(PlayerStats.luck)
               || fieldName == nameof(PlayerStats.maxHealth)
               || fieldName == nameof(PlayerStats.maxMana);
    }

    private bool TryGetTextOverride(string fieldName, out StatTextOverride found)
    {
        found = null;
        if (statTextOverrides == null || string.IsNullOrEmpty(fieldName))
        {
            return false;
        }

        for (int i = 0; i < statTextOverrides.Count; i++)
        {
            StatTextOverride item = statTextOverrides[i];
            if (item == null) continue;
            if (!string.Equals(item.FieldName, fieldName, StringComparison.Ordinal)) continue;
            found = item;
            return true;
        }

        return false;
    }

    private void AppendLine(StringBuilder labelsSb, StringBuilder valuesSb, string fieldName, float currentValue, float delta)
    {
        if (labelsSb.Length > 0)
        {
            labelsSb.Append('\n');
            valuesSb.Append('\n');
        }

        StatTextOverride textOverride;
        bool hasOverride = TryGetTextOverride(fieldName, out textOverride);

        string displayName = fieldName;
        if (hasOverride && !string.IsNullOrEmpty(textOverride.DisplayName))
        {
            displayName = textOverride.DisplayName;
        }

        string suffix = null;
        bool overrideSuffix = false;
        if (hasOverride)
        {
            suffix = textOverride.Suffix;
            overrideSuffix = textOverride.OverrideSuffix;
        }

        string currentStr = FormatFieldValue(fieldName, currentValue, overrideSuffix, suffix);
        string deltaValueStr = FormatFieldDeltaValue(fieldName, delta, overrideSuffix, suffix);
        labelsSb.Append(displayName);
        labelsSb.Append(':');

        valuesSb.Append(currentStr);

        if (Mathf.Abs(delta) <= 0.0001f)
        {
            return;
        }

        string deltaStr = deltaValueStr;
        bool isPositive = IsPositiveDeltaForField(fieldName, delta);
        string sign = delta > 0f ? "+" : string.Empty;
        Color c = isPositive ? positiveDeltaColor : negativeDeltaColor;
        string color = ColorUtility.ToHtmlStringRGBA(c);
        valuesSb.Append("<color=#");
        valuesSb.Append(color);
        valuesSb.Append(">(");
        valuesSb.Append(sign);
        valuesSb.Append(deltaStr);
        valuesSb.Append(")</color>");
    }

    private static string FormatFieldValue(string fieldName, float value, bool overrideSuffix, string suffixOverride)
    {
        if (IsIntegerFieldName(fieldName))
        {
            string suffix = overrideSuffix ? (suffixOverride ?? string.Empty) : string.Empty;
            return Mathf.RoundToInt(value).ToString() + suffix;
        }

        if (IsFractionPercentFieldName(fieldName))
        {
            string suffix = overrideSuffix ? (suffixOverride ?? string.Empty) : "%";
            return (value * 100f).ToString("F2") + suffix;
        }

        if (IsPercentFieldName(fieldName))
        {
            string suffix = overrideSuffix ? (suffixOverride ?? string.Empty) : "%";
            return value.ToString("F2") + suffix;
        }

        if (IsSecondsFieldName(fieldName))
        {
            string suffix = overrideSuffix ? (suffixOverride ?? string.Empty) : "s";
            return value.ToString("F2") + suffix;
        }

        if (fieldName == nameof(PlayerStats.manaRegenPerSecond) || fieldName == nameof(PlayerStats.healthRegenPerSecond))
        {
            string suffix = overrideSuffix ? (suffixOverride ?? string.Empty) : "/s";
            return value.ToString("F2") + suffix;
        }

        string defaultSuffix = overrideSuffix ? (suffixOverride ?? string.Empty) : string.Empty;
        return value.ToString("F2") + defaultSuffix;
    }

    private static string FormatFieldDeltaValue(string fieldName, float delta, bool overrideSuffix, string suffixOverride)
    {
        if (IsIntegerFieldName(fieldName))
        {
            string suffix = overrideSuffix ? (suffixOverride ?? string.Empty) : string.Empty;
            return Mathf.RoundToInt(delta).ToString() + suffix;
        }

        if (IsFractionPercentFieldName(fieldName))
        {
            string suffix = overrideSuffix ? (suffixOverride ?? string.Empty) : "%";
            return (delta * 100f).ToString("F2") + suffix;
        }

        if (IsPercentFieldName(fieldName))
        {
            string suffix = overrideSuffix ? (suffixOverride ?? string.Empty) : "%";
            return delta.ToString("F2") + suffix;
        }

        if (IsSecondsFieldName(fieldName))
        {
            string suffix = overrideSuffix ? (suffixOverride ?? string.Empty) : "s";
            return delta.ToString("F2") + suffix;
        }

        if (fieldName == nameof(PlayerStats.manaRegenPerSecond) || fieldName == nameof(PlayerStats.healthRegenPerSecond))
        {
            string suffix = overrideSuffix ? (suffixOverride ?? string.Empty) : "/s";
            return delta.ToString("F2") + suffix;
        }

        string defaultSuffix = overrideSuffix ? (suffixOverride ?? string.Empty) : string.Empty;
        return delta.ToString("F2") + defaultSuffix;
    }

    private void BuildPointCaches()
    {
        basePoints.Clear();
        currentPoints.Clear();

        if (levelUpUI == null || levelUpUI.Stats == null) return;

        for (int i = 0; i < levelUpUI.Stats.Count; i++)
        {
            LevelUpUI.StatRow row = levelUpUI.Stats[i];
            if (row == null) continue;

            string key = string.IsNullOrEmpty(row.statName) ? $"Stat{i}" : row.statName;
            if (string.IsNullOrEmpty(key)) continue;

            key = NormalizeStatKey(key);

            int baseValue = Mathf.Max(0, row.baseValue);

            int savedTotal = PlayerPrefs.GetInt($"LevelUpUI.{key}", baseValue);
            savedTotal = Mathf.Max(baseValue, savedTotal);

            basePoints[key] = Mathf.Max(0, savedTotal - baseValue);
            currentPoints[key] = Mathf.Max(0, row.currentValue - baseValue);
        }
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

    private static int Get(IReadOnlyDictionary<string, int> dict, string key)
    {
        if (dict == null) return 0;
        if (dict.TryGetValue(key, out int value)) return value;
        return 0;
    }

    private static string FormatValue(float value, DisplayFormat format, int decimals)
    {
        switch (format)
        {
            case DisplayFormat.Integer:
                return Mathf.RoundToInt(value).ToString();
            case DisplayFormat.Percent:
                return value.ToString($"F{Mathf.Clamp(decimals, 0, 6)}") + "%";
            case DisplayFormat.Seconds:
                return value.ToString($"F{Mathf.Clamp(decimals, 0, 6)}") + "s";
            default:
                return value.ToString($"F{Mathf.Clamp(decimals, 0, 6)}");
        }
    }
}
