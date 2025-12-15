using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ManaUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMana source;

    [Header("UI Elements")]
    [SerializeField] private Slider manaSlider;
    [SerializeField] private TextMeshProUGUI manaText;
    [SerializeField] private Image fillImage;

    [Header("Colors")]
    [SerializeField] private Color highManaColor = new Color(0.3f, 0.5f, 1f); // Blue
    [SerializeField] private Color mediumManaColor = new Color(0.6f, 0.4f, 0.8f); // Purple
    [SerializeField] private Color lowManaColor = new Color(0.8f, 0.2f, 0.8f); // Magenta
    [SerializeField] private float mediumManaThreshold = 0.5f;
    [SerializeField] private float lowManaThreshold = 0.25f;

    private void OnEnable()
    {
        if (source != null)
        {
            source.OnManaChanged += Refresh;
            InitializeUI();
        }
        else
        {
            Debug.LogWarning("ManaUI: No PlayerMana source assigned!");
        }
    }

    private void OnDisable()
    {
        if (source != null)
        {
            source.OnManaChanged -= Refresh;
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
            Refresh(source.CurrentManaExact, source.MaxManaExact);
        }
    }

    private void Refresh(float currentMana, float maxMana)
    {
        if (maxMana <= 0f) return; // Prevent division by zero

        // Update slider
        if (manaSlider != null)
        {
            manaSlider.maxValue = maxMana;
            manaSlider.value = currentMana;
        }

        // Update text (show fractional mana with two decimal places)
        if (manaText != null)
        {
            manaText.text = $"{currentMana:F2} / {maxMana:F2}";
        }

        // Update color based on mana percentage
        if (fillImage != null)
        {
            float manaPercent = maxMana > 0f ? currentMana / maxMana : 0f;

            if (manaPercent <= lowManaThreshold)
            {
                fillImage.color = lowManaColor;
            }
            else if (manaPercent <= mediumManaThreshold)
            {
                fillImage.color = mediumManaColor;
            }
            else
            {
                fillImage.color = highManaColor;
            }
        }
    }
}
