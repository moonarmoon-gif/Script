using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class HealthUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerHealth source;

    [Header("UI Elements")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private Image fillImage;

    [Header("Colors")]
    [SerializeField] private Color highHealthColor = Color.green;
    [SerializeField] private Color mediumHealthColor = Color.yellow;
    [SerializeField] private Color lowHealthColor = Color.red;
    [SerializeField] private float mediumHealthThreshold = 0.5f;
    [SerializeField] private float lowHealthThreshold = 0.25f;

    private void OnEnable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        ResolveAndBindSource();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnbindSource();
    }

    private void Start()
    {
        InitializeUI();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResolveAndBindSource();
    }

    private void ResolveAndBindSource()
    {
        PlayerHealth resolved = source;

        if (resolved == null && AdvancedPlayerController.Instance != null)
        {
            resolved = AdvancedPlayerController.Instance.GetComponent<PlayerHealth>();
        }

        if (resolved == null)
        {
            resolved = FindObjectOfType<PlayerHealth>(true);
        }

        if (resolved == null)
        {
            Debug.LogWarning("HealthUI: No PlayerHealth source assigned!");
            return;
        }

        if (source == resolved)
        {
            source.OnHealthChanged -= Refresh;
            source.OnHealthChanged += Refresh;
            InitializeUI();
            return;
        }

        UnbindSource();
        source = resolved;
        source.OnHealthChanged += Refresh;
        InitializeUI();
    }

    private void UnbindSource()
    {
        if (source != null)
        {
            source.OnHealthChanged -= Refresh;
        }
    }

    private void InitializeUI()
    {
        if (source != null)
        {
            Refresh(source.CurrentHealth, source.MaxHealth);
        }
    }

    private void Refresh(float currentHealth, float maxHealth)
    {
        if (maxHealth <= 0) return; // Prevent division by zero

        // Update slider
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }

        // Update text
        if (healthText != null)
        {
            healthText.text = $"{Mathf.CeilToInt(currentHealth)} / {Mathf.CeilToInt(maxHealth)}";
        }

        // Update color based on health percentage
        if (fillImage != null)
        {
            float healthPercent = currentHealth / maxHealth;

            if (healthPercent <= lowHealthThreshold)
            {
                fillImage.color = lowHealthColor;
            }
            else if (healthPercent <= mediumHealthThreshold)
            {
                fillImage.color = mediumHealthColor;
            }
            else
            {
                fillImage.color = highHealthColor;
            }
        }
    }
}