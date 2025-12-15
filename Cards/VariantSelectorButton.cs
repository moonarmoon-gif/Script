using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Simple button component used by VariantSelectorCardButtonPrefab.
/// It displays one variant option (name/description/icon) and notifies
/// CardSelectionManager when clicked so the chosen variant can be
/// applied to the appropriate ProjectileCards.
/// </summary>
[RequireComponent(typeof(Button))]
public class VariantSelectorButton : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Image iconImage;

    private CardSelectionManager manager;
    private ProjectileCards targetCard;
    private ProjectileVariantSet.VariantInfo variantInfo;
    private int tierIndex;
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(OnClicked);
        }
    }

    public void Initialize(CardSelectionManager mgr, ProjectileCards card, ProjectileVariantSet.VariantInfo info, int tier)
    {
        manager = mgr;
        targetCard = card;
        variantInfo = info;
        tierIndex = tier;

        if (titleText != null)
        {
            titleText.text = !string.IsNullOrEmpty(info.displayName)
                ? info.displayName
                : $"Variant {info.variantIndex}";

            // Apply optional per-variant styling overrides
            if (info.cardNameFontSize > 0f)
            {
                titleText.fontSize = info.cardNameFontSize;
            }

            titleText.color = info.cardNameColor;

            if (info.enableNameOutline)
            {
                titleText.outlineWidth = info.nameOutlineWidth;
                titleText.outlineColor = info.nameOutlineColor;
            }
            else
            {
                titleText.outlineWidth = 0f;
            }
        }

        if (descriptionText != null)
        {
            descriptionText.text = info.description;

            if (info.descriptionFontSize > 0f)
            {
                descriptionText.fontSize = info.descriptionFontSize;
            }

            descriptionText.color = info.descriptionColor;

            if (info.enableDescriptionOutline)
            {
                descriptionText.outlineWidth = info.descriptionOutlineWidth;
                descriptionText.outlineColor = info.descriptionOutlineColor;
            }
            else
            {
                descriptionText.outlineWidth = 0f;
            }
        }

        if (iconImage != null)
        {
            if (info.icon != null)
            {
                iconImage.sprite = info.icon;
                iconImage.enabled = true;
            }
            else
            {
                iconImage.enabled = false;
            }
        }
    }

    private void OnClicked()
    {
        if (manager == null || targetCard == null || variantInfo == null)
        {
            return;
        }

        // Apply the chosen enhanced variant via ProjectileCardLevelSystem
        if (ProjectileCardLevelSystem.Instance != null)
        {
            int variantIndex = Mathf.Clamp(variantInfo.variantIndex, 1, 3);
            ProjectileCardLevelSystem.Instance.SetEnhancedVariant(targetCard, variantIndex);
        }

        // Close the variant selection UI and resume game
        manager.OnVariantSelected();
    }
}
