using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FavourExpUI : MonoBehaviour
{
    public static FavourExpUI Instance { get; private set; }

    [Header("Config")]
    [Tooltip("Base Soul required to gain the FIRST Soul level (Level 0 -> 1).")]
    [SerializeField] private float baseSoulRequirement = 100f;

    [Tooltip("Additional Soul required per Soul level after the first (Level N requires base + increment * N).")]
    [SerializeField] private float soulRequirementIncrement = 100f;

    [Header("State (runtime)")]
    [SerializeField] private int currentSoulLevel = 0;
    [SerializeField] private float currentSoulExp = 0f;
    [SerializeField] private float currentRequirement = 100f;

    [Header("UI Elements")]
    [SerializeField] private Slider soulSlider;
    [SerializeField] private TextMeshProUGUI soulLevelText;
    [SerializeField] private TextMeshProUGUI soulExpText;
    [SerializeField] private Image fillImage;

    [Header("Colors")] 
    [SerializeField] private Color soulColor = new Color(0.5f, 0.9f, 1f);
    [SerializeField] private Gradient soulGradient;
    [SerializeField] private bool useGradient = false;

    private bool isInitialized = false;

    private bool suppressAutoOnAddSoul = false;
    private bool isAutoFavourRoutineRunning = false;

    public int CurrentSoulLevel => currentSoulLevel;
    public float CurrentSoulExp => currentSoulExp;
    public float CurrentRequirement => currentRequirement;
    public float BaseSoulRequirement => baseSoulRequirement;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        currentRequirement = GetRequirementForLevel(currentSoulLevel);
    }

    private void Start()
    {
        InitializeUI();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void InitializeUI()
    {
        isInitialized = true;
        RefreshUI();
    }

    private float GetRequirementForLevel(int level)
    {
        if (baseSoulRequirement <= 0f)
        {
            return 1f;
        }
        if (level < 0) level = 0;
        return baseSoulRequirement + soulRequirementIncrement * level;
    }

    private void RefreshUI()
    {
        if (!isInitialized) return;

        if (soulSlider != null)
        {
            soulSlider.minValue = 0;
            soulSlider.maxValue = currentRequirement;
            soulSlider.value = currentSoulExp;
        }

        if (soulLevelText != null)
        {
            soulLevelText.text = $"Soul Lv {currentSoulLevel}";
        }

        if (soulExpText != null)
        {
            int displayCurrent = Mathf.RoundToInt(currentSoulExp);
            int displayRequirement = Mathf.RoundToInt(currentRequirement);
            soulExpText.text = $"{displayCurrent} / {displayRequirement} Souls";
        }

        if (fillImage != null)
        {
            if (useGradient && soulGradient != null)
            {
                float progress = currentRequirement > 0f ? currentSoulExp / currentRequirement : 0f;
                fillImage.color = soulGradient.Evaluate(progress);
            }
            else
            {
                fillImage.color = soulColor;
            }
        }
    }

    public void AddSoul(float amount)
    {
        if (amount <= 0f) return;

        var manager = CardSelectionManager.Instance;
        bool autoSystem = manager != null && manager.AutomaticLevelingFavourSystem;

        if (manager != null && !manager.UseFavourSoulSystem && !autoSystem)
        {
            return;
        }

        currentSoulExp += amount;
        bool leveledUp = false;
        int levelsGained = 0;

        while (currentSoulExp >= currentRequirement)
        {
            currentSoulExp -= currentRequirement;
            currentSoulLevel++;
            currentRequirement = GetRequirementForLevel(currentSoulLevel);
            leveledUp = true;
            levelsGained++;
        }

        if (leveledUp)
        {
            currentSoulExp = Mathf.Clamp(currentSoulExp, 0f, currentRequirement);
        }

        RefreshUI();

        if (!suppressAutoOnAddSoul && autoSystem && levelsGained > 0 && !isAutoFavourRoutineRunning)
        {
            StartCoroutine(AutoFavourOnSoulLevelUpRoutine(levelsGained));
        }
    }

    private float ComputeTotalSoulProgress()
    {
        float total = currentSoulExp;
        for (int level = 0; level < currentSoulLevel; level++)
        {
            total += GetRequirementForLevel(level);
        }
        return total;
    }

    public void RebaseAfterFavourSelection()
    {
        float total = ComputeTotalSoulProgress();
        float cost = Mathf.Max(0f, baseSoulRequirement);
        float leftover = Mathf.Max(0f, total - cost);

        currentSoulLevel = 0;
        currentSoulExp = 0f;
        currentRequirement = GetRequirementForLevel(currentSoulLevel);

        if (leftover > 0)
        {
            AddSoul(leftover);
        }
        else
        {
            RefreshUI();
        }
    }

    private System.Collections.IEnumerator AutoFavourOnSoulLevelUpRoutine(int levelsGained)
    {
        isAutoFavourRoutineRunning = true;

        var manager = CardSelectionManager.Instance;
        if (manager == null)
        {
            isAutoFavourRoutineRunning = false;
            yield break;
        }

        int iterations = Mathf.Max(1, levelsGained);

        for (int i = 0; i < iterations; i++)
        {
            float delay = manager.favourSelectionDelay;
            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }

            int soulLevelAtTrigger = currentSoulLevel;
            if (soulLevelAtTrigger < 1)
            {
                continue;
            }

            int count = manager.SoulFavourChoices;
            if (count <= 0) count = 3;

            manager.ShowSoulFavourCardsForSoulLevel(soulLevelAtTrigger, count);

            while (CardSelectionManager.Instance != null && CardSelectionManager.Instance.IsSelectionActive())
            {
                yield return null;
            }
        }

        isAutoFavourRoutineRunning = false;
    }
}
