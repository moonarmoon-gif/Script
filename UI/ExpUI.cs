using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ExpUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerLevel source;

    [Header("UI Elements")]
    [SerializeField] private Slider expSlider;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI expText;
    [SerializeField] private Image fillImage;

    [Header("Colors")]
    [SerializeField] private Color expColor = new Color(1f, 0.84f, 0f); // Gold color
    [SerializeField] private Gradient expGradient;
    [SerializeField] private bool useGradient = false;

    [Header("Animation")]
    [SerializeField] private bool animateOnLevelUp = true;
    [SerializeField] private float levelUpFlashDuration = 0.5f;
    [SerializeField] private Color levelUpFlashColor = Color.white;

    private bool isFlashing = false;

    private void OnEnable()
    {
        if (source != null)
        {
            source.OnExpChanged += RefreshExp;
            source.OnLevelUp += OnLevelUp;
            InitializeUI();
        }
        else
        {
            Debug.LogWarning("ExpUI: No PlayerLevel source assigned!");
        }
    }

    private void OnDisable()
    {
        if (source != null)
        {
            source.OnExpChanged -= RefreshExp;
            source.OnLevelUp -= OnLevelUp;
        }
    }

    private void Start()
    {
        InitializeUI();
    }

    private void InitializeUI()
    {
        if (source != null)
        {
            RefreshExp(source.CurrentExp, source.ExpToNextLevel, source.CurrentLevel);
        }
    }

    private void RefreshExp(int currentExp, int expToNextLevel, int currentLevel)
    {
        
        if (expToNextLevel <= 0)
        {
            return;
        }

        // Update slider
        if (expSlider != null)
        {
            expSlider.minValue = 0;
            expSlider.maxValue = expToNextLevel;
            expSlider.value = currentExp;
        }

        // Update level text
        if (levelText != null)
        {
            levelText.text = $"Level {currentLevel}";
        }

        // Update exp text
        if (expText != null)
        {
            expText.text = $"{currentExp} / {expToNextLevel} EXP";
        }

        // Update color
        if (fillImage != null && !isFlashing)
        {
            if (useGradient && expGradient != null)
            {
                float progress = (float)currentExp / expToNextLevel;
                fillImage.color = expGradient.Evaluate(progress);
            }
            else
            {
                fillImage.color = expColor;
            }
        }
    }

    private void OnLevelUp(int newLevel)
    {
        if (animateOnLevelUp && fillImage != null)
        {
            StartCoroutine(LevelUpFlashRoutine());
        }
    }

    private System.Collections.IEnumerator LevelUpFlashRoutine()
    {
        isFlashing = true;
        Color originalColor = useGradient && expGradient != null ? expGradient.Evaluate(0) : expColor;

        float elapsed = 0f;
        while (elapsed < levelUpFlashDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.PingPong(elapsed * 4f, 1f);
            fillImage.color = Color.Lerp(originalColor, levelUpFlashColor, t);
            yield return null;
        }

        fillImage.color = originalColor;
        isFlashing = false;
    }
}
