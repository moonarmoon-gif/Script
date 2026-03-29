using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class TruePlayerLevelUI : MonoBehaviour
{
    [Header("Root")]
    public GameObject Root;

    [Header("References")]
    [SerializeField] private TruePlayerLevel source;

    [Header("UI Elements")]
    [SerializeField] private Slider expSlider;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI expText;
    [SerializeField] private Image fillImage;

    [Header("Colors")]
    [SerializeField] private Color expColor = new Color(1f, 0.84f, 0f);
    [SerializeField] private Gradient expGradient;
    [SerializeField] private bool useGradient = false;

    [Header("Animation")]
    [SerializeField] private bool animateOnAward = true;
    [SerializeField] private float minTotalAwardDuration = 1.25f;
    [SerializeField] private float maxTotalAwardDuration = 8f;
    [SerializeField] private float levelUpPauseSeconds = 0.1f;
    [SerializeField] private float levelUpFlashDuration = 0.35f;
    [SerializeField] private Color levelUpFlashColor = Color.white;

    private Coroutine awardRoutine;
    private bool isAnimating;
    private bool isFlashing;

    private CanvasGroup rootCanvasGroup;
    private float lastAwardDurationSeconds;

    public event Action OnAwardAnimationFinished;

    public bool IsAnimating => isAnimating;
    public float LastAwardDurationSeconds => lastAwardDurationSeconds;

    private void Awake()
    {
        ResolveRoot();
        SetRootVisible(false);
    }

    private void ResolveRoot()
    {
        if (Root == null)
        {
            Transform child = transform.Find("Root");
            if (child != null)
            {
                Root = child.gameObject;
            }
        }

        if (Root == null)
        {
            Root = gameObject;
        }

        if (Root != null)
        {
            rootCanvasGroup = Root.GetComponent<CanvasGroup>();
        }
    }

    public void SetRootVisible(bool visible)
    {
        if (visible)
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            if (!enabled)
            {
                enabled = true;
            }
        }

        ResolveRoot();

        if (Root == null)
        {
            return;
        }

        if (Root != gameObject)
        {
            Root.SetActive(visible);
            return;
        }

        if (rootCanvasGroup != null)
        {
            rootCanvasGroup.alpha = visible ? 1f : 0f;
            rootCanvasGroup.interactable = visible;
            rootCanvasGroup.blocksRaycasts = visible;
            return;
        }

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child != null)
            {
                child.gameObject.SetActive(visible);
            }
        }
    }

    private void OnEnable()
    {
        ResolveSource();

        if (source != null)
        {
            source.OnExpChanged += HandleExpChanged;
            source.OnLevelUp += HandleLevelUp;
        }

        RefreshFromSource();
    }

    private void OnDisable()
    {
        if (source != null)
        {
            source.OnExpChanged -= HandleExpChanged;
            source.OnLevelUp -= HandleLevelUp;
        }
    }

    private void ResolveSource()
    {
        if (source != null)
        {
            return;
        }

        if (TruePlayerLevel.Instance != null)
        {
            source = TruePlayerLevel.Instance;
            return;
        }

        source = FindObjectOfType<TruePlayerLevel>(true);
    }

    private void HandleExpChanged(int currentExp, int expToNextLevel, int currentLevel)
    {
        if (isAnimating)
        {
            return;
        }

        RefreshExp(currentExp, expToNextLevel, currentLevel);
    }

    private void HandleLevelUp(int newLevel)
    {
        if (!isAnimating && fillImage != null)
        {
            StartCoroutine(LevelUpFlashRoutine());
        }
    }

    private void RefreshFromSource()
    {
        if (source == null)
        {
            return;
        }

        RefreshExp(source.CurrentExp, source.ExpToNextLevel, source.CurrentLevel);
    }

    private void RefreshExp(int currentExp, int expToNextLevel, int currentLevel)
    {
        if (expToNextLevel <= 0)
        {
            return;
        }

        if (expSlider != null)
        {
            expSlider.minValue = 0;
            expSlider.maxValue = expToNextLevel;
            expSlider.value = Mathf.Clamp(currentExp, 0, expToNextLevel);
        }

        if (levelText != null)
        {
            levelText.text = $"True Level {currentLevel}";
        }

        if (expText != null)
        {
            expText.text = $"{currentExp} / {expToNextLevel} TRUE EXP";
        }

        if (fillImage != null && !isFlashing)
        {
            if (useGradient && expGradient != null)
            {
                float progress = expToNextLevel <= 0 ? 0f : (float)currentExp / expToNextLevel;
                fillImage.color = expGradient.Evaluate(progress);
            }
            else
            {
                fillImage.color = expColor;
            }
        }
    }

    public void AwardTrueExp(int amount)
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (!enabled)
        {
            enabled = true;
        }

        if (amount <= 0)
        {
            lastAwardDurationSeconds = 0f;
            OnAwardAnimationFinished?.Invoke();
            return;
        }

        ResolveSource();
        if (source == null)
        {
            return;
        }

        if (awardRoutine != null)
        {
            StopCoroutine(awardRoutine);
            awardRoutine = null;
        }

        if (!animateOnAward)
        {
            source.GainExperience(amount, raiseEvents: true);
            RefreshFromSource();
            lastAwardDurationSeconds = 0f;
            OnAwardAnimationFinished?.Invoke();
            return;
        }

        int startLevel = source.CurrentLevel;
        float startExp = source.CurrentExpExact;

        source.GainExperience(amount, raiseEvents: false);

        int endLevel = source.CurrentLevel;
        float endExp = source.CurrentExpExact;

        awardRoutine = StartCoroutine(AnimateAwardRoutine(startLevel, startExp, endLevel, endExp));
    }

    private IEnumerator AnimateAwardRoutine(int startLevel, float startExp, int endLevel, float endExp)
    {
        isAnimating = true;

        int levelsGained = Mathf.Max(0, endLevel - startLevel);
        int segments = Mathf.Max(1, levelsGained + 1);

        float total = Mathf.Clamp(minTotalAwardDuration + levelsGained * 0.25f, minTotalAwardDuration, maxTotalAwardDuration);
        float segmentDuration = Mathf.Max(0.05f, total / segments);

        lastAwardDurationSeconds = segmentDuration * segments;
        lastAwardDurationSeconds += levelsGained * (Mathf.Max(0f, levelUpPauseSeconds) + Mathf.Max(0f, levelUpFlashDuration));

        int displayLevel = startLevel;
        float displayExp = Mathf.Max(0f, startExp);

        while (displayLevel < endLevel)
        {
            float expToNext = source.GetExpRequirementForLevel(displayLevel);
            yield return AnimateFill(displayLevel, displayExp, expToNext, expToNext, segmentDuration);

            displayLevel++;
            displayExp = 0f;

            if (fillImage != null)
            {
                yield return LevelUpFlashRoutine();
            }

            float pause = Mathf.Max(0f, levelUpPauseSeconds);
            if (pause > 0f)
            {
                yield return new WaitForSecondsRealtime(pause);
            }
        }

        float finalExpToNext = source.GetExpRequirementForLevel(displayLevel);
        yield return AnimateFill(displayLevel, displayExp, finalExpToNext, Mathf.Clamp(endExp, 0f, finalExpToNext), segmentDuration);

        isAnimating = false;
        awardRoutine = null;

        source.NotifyChanged();
        RefreshFromSource();

        OnAwardAnimationFinished?.Invoke();
    }

    private IEnumerator AnimateFill(int level, float startExp, float expToNext, float targetExp, float duration)
    {
        float start = Mathf.Clamp(startExp, 0f, expToNext);
        float end = Mathf.Clamp(targetExp, 0f, expToNext);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);

            float value = Mathf.Lerp(start, end, t);
            RefreshExp(Mathf.RoundToInt(value), Mathf.RoundToInt(expToNext), level);
            yield return null;
        }

        RefreshExp(Mathf.RoundToInt(end), Mathf.RoundToInt(expToNext), level);
    }

    private IEnumerator LevelUpFlashRoutine()
    {
        if (fillImage == null)
        {
            yield break;
        }

        isFlashing = true;
        Color originalColor = useGradient && expGradient != null ? expGradient.Evaluate(0f) : expColor;

        float elapsed = 0f;
        float duration = Mathf.Max(0f, levelUpFlashDuration);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.PingPong(elapsed * 4f, 1f);
            fillImage.color = Color.Lerp(originalColor, levelUpFlashColor, t);
            yield return null;
        }

        fillImage.color = originalColor;
        isFlashing = false;
    }
}
