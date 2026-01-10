using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI component for displaying and selecting cards
/// </summary>
[RequireComponent(typeof(Button))]
public class CardButton : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI cardNameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI rarityText;
    [SerializeField] private Image cardIcon;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image borderImage;
    [SerializeField] private TextMeshProUGUI cardLevelText;
    [SerializeField] private TextMeshProUGUI baseDamageText;
    [SerializeField] private TextMeshProUGUI modifiersText;
    
    [Header("Position Offsets")]
    [Tooltip("Vertical offset for card title")]
    public float titleVerticalOffset = 0f;
    [Tooltip("Vertical offset for card description")]
    public float descriptionVerticalOffset = 0f;
    
    private BaseCard card;
    private CombinedCard combinedCard;
    private CardSelectionManager manager;
    private Button button;
    
    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnCardSelected);
    }
    
    public void SetCard(BaseCard cardData, CardSelectionManager selectionManager)
    {
        card = cardData;
        manager = selectionManager;
        
        if (card == null) return;

        ProjectileCards projectileCard = card as ProjectileCards;
        
        if (cardNameText != null)
        {
            cardNameText.text = card.cardName;
            cardNameText.fontSize = card.cardNameFontSize;
            cardNameText.color = card.cardNameColor;
            
            // Apply outline
            if (card.enableNameOutline)
            {
                cardNameText.outlineWidth = card.nameOutlineWidth;
                cardNameText.outlineColor = card.nameOutlineColor;
            }
            else
            {
                cardNameText.outlineWidth = 0f;
            }
            
            // Apply title offset
            RectTransform titleRect = cardNameText.GetComponent<RectTransform>();
            if (titleRect != null && titleVerticalOffset != 0f)
            {
                Vector2 pos = titleRect.anchoredPosition;
                pos.y += titleVerticalOffset;
                titleRect.anchoredPosition = pos;
            }
        }
        
        if (descriptionText != null)
        {
            if (projectileCard != null)
            {
                descriptionText.text = projectileCard.GetBaseDescriptionOnly();
            }
            else
            {
                descriptionText.text = card.GetFormattedDescription();
            }
            descriptionText.fontSize = card.descriptionFontSize;
            descriptionText.color = card.descriptionColor;
            
            // Apply outline
            if (card.enableDescriptionOutline)
            {
                descriptionText.outlineWidth = card.descriptionOutlineWidth;
                descriptionText.outlineColor = card.descriptionOutlineColor;
            }
            else
            {
                descriptionText.outlineWidth = 0f;
            }
            
            // Apply description offset
            RectTransform descRect = descriptionText.GetComponent<RectTransform>();
            if (descRect != null && descriptionVerticalOffset != 0f)
            {
                Vector2 pos = descRect.anchoredPosition;
                pos.y += descriptionVerticalOffset;
                descRect.anchoredPosition = pos;
            }
        }

        if (modifiersText != null)
        {
            if (projectileCard != null)
            {
                modifiersText.text = projectileCard.GetModifiersDescription();
            }
            else
            {
                modifiersText.text = string.Empty;
            }
        }
        
        if (rarityText != null)
        {
            rarityText.text = card.rarity.ToString();
            // Use manager's color if available (for customization), otherwise use default
            rarityText.color = manager != null ? manager.GetRarityColor(card.rarity) : card.GetRarityColor();
        }
        
        if (cardIcon != null && card.cardIcon != null)
        {
            cardIcon.sprite = card.cardIcon;
            cardIcon.enabled = true;
        }
        else if (cardIcon != null)
        {
            cardIcon.enabled = false;
        }
        
        // Use manager's color if available (for customization), otherwise use default
        Color rarityColor = manager != null ? manager.GetRarityColor(card.rarity) : card.GetRarityColor();
        
        if (backgroundImage != null)
        {
            backgroundImage.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.3f);
        }
        
        if (borderImage != null)
        {
            borderImage.color = rarityColor;
        }

        UpdateCardLevelText();

        AppendBaseDamagePreview(card);
    }
    
    public void SetCombinedCard(CombinedCard cardData, CardSelectionManager selectionManager)
    {
        combinedCard = cardData;
        manager = selectionManager;
        
        if (combinedCard == null) return;
        
        if (combinedCard.projectileCard != null)
        {
            card = combinedCard.projectileCard;
        }
        else if (combinedCard.basicCard != null)
        {
            card = combinedCard.basicCard;
        }
        else
        {
            return;
        }

        ProjectileCards projectileCard = combinedCard.projectileCard;
        
        if (cardNameText != null)
        {
            cardNameText.text = combinedCard.GetCardName();
            cardNameText.fontSize = card.cardNameFontSize;
            cardNameText.color = card.cardNameColor;
            
            // Apply outline
            if (card.enableNameOutline)
            {
                cardNameText.outlineWidth = card.nameOutlineWidth;
                cardNameText.outlineColor = card.nameOutlineColor;
            }
            else
            {
                cardNameText.outlineWidth = 0f;
            }
            
            // Apply title offset
            RectTransform titleRect = cardNameText.GetComponent<RectTransform>();
            if (titleRect != null && titleVerticalOffset != 0f)
            {
                Vector2 pos = titleRect.anchoredPosition;
                pos.y += titleVerticalOffset;
                titleRect.anchoredPosition = pos;
            }
        }
        
        if (descriptionText != null)
        {
            if (projectileCard != null)
            {
                descriptionText.text = projectileCard.GetBaseDescriptionOnly();
            }
            else
            {
                descriptionText.text = combinedCard.GetCombinedDescription();
            }
            descriptionText.fontSize = card.descriptionFontSize;
            descriptionText.color = card.descriptionColor;
            
            // Apply outline
            if (card.enableDescriptionOutline)
            {
                descriptionText.outlineWidth = card.descriptionOutlineWidth;
                descriptionText.outlineColor = card.descriptionOutlineColor;
            }
            else
            {
                descriptionText.outlineWidth = 0f;
            }
            
            // Apply description offset
            RectTransform descRect = descriptionText.GetComponent<RectTransform>();
            if (descRect != null && descriptionVerticalOffset != 0f)
            {
                Vector2 pos = descRect.anchoredPosition;
                pos.y += descriptionVerticalOffset;
                descRect.anchoredPosition = pos;
            }
        }
        
        if (modifiersText != null)
        {
            if (projectileCard != null)
            {
                modifiersText.text = projectileCard.GetModifiersDescription();
            }
            else
            {
                modifiersText.text = string.Empty;
            }
        }

        AppendBaseDamagePreview(card);
        
        if (rarityText != null)
        {
            rarityText.text = combinedCard.rarity.ToString();
            rarityText.color = manager != null ? manager.GetRarityColor(combinedCard.rarity) : CardRarityHelper.GetRarityColor(combinedCard.rarity);
        }
        
        if (cardIcon != null && card.cardIcon != null)
        {
            cardIcon.sprite = card.cardIcon;
            cardIcon.enabled = true;
        }
        else if (cardIcon != null)
        {
            cardIcon.enabled = false;
        }
        
        Color rarityColor = manager != null ? manager.GetRarityColor(combinedCard.rarity) : CardRarityHelper.GetRarityColor(combinedCard.rarity);
        
        if (backgroundImage != null)
        {
            backgroundImage.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.3f);
        }
        
        if (borderImage != null)
        {
            borderImage.color = rarityColor;
        }

        UpdateCardLevelText();
    }
    
    private void OnCardSelected()
    {
        if (combinedCard != null && manager != null)
        {
            manager.SelectCombinedCard(combinedCard);
        }
        else if (card != null && manager != null)
        {
            // Route FavourCards through a dedicated path so they can close the
            // UI and unpause the game cleanly without using the CombinedCard
            // pipeline.
            if (card is FavourCards)
            {
                manager.SelectFavourCard(card);
            }
            else
            {
                manager.SelectExternalCard(card);
            }
        }
    }
    
    private void UpdateCardLevelText()
    {
        if (cardLevelText == null)
        {
            return;
        }

        ProjectileCards projectileCard = card as ProjectileCards;
        if (projectileCard == null)
        {
            cardLevelText.text = string.Empty;
            return;
        }

        if (ProjectileCardLevelSystem.Instance == null)
        {
            cardLevelText.text = string.Empty;
            return;
        }

        int currentLevel = ProjectileCardLevelSystem.Instance.GetLevel(projectileCard);
        int levelGain = ProjectileCardLevelSystem.Instance.GetLevelGainByRarity(projectileCard.rarity);

        if (currentLevel <= 0)
        {
            int nextLevel = currentLevel + levelGain;
            if (nextLevel > 0)
            {
                cardLevelText.text = "Level " + nextLevel;
            }
            else
            {
                cardLevelText.text = string.Empty;
            }
        }
        else
        {
            int nextLevel = currentLevel + levelGain;
            cardLevelText.text = "Level " + currentLevel + " --> Level " + nextLevel;
        }
    }

    private void AppendBaseDamagePreview(BaseCard baseCard)
    {
        if (baseDamageText == null)
        {
            return;
        }

        baseDamageText.text = string.Empty;
    }
    
    public void OnPointerEnter()
    {
        if (backgroundImage != null)
        {
            Color rarityColor;
            if (combinedCard != null && manager != null)
            {
                rarityColor = manager.GetRarityColor(combinedCard.rarity);
            }
            else if (combinedCard != null)
            {
                rarityColor = CardRarityHelper.GetRarityColor(combinedCard.rarity);
            }
            else if (card != null && manager != null)
            {
                rarityColor = manager.GetRarityColor(card.rarity);
            }
            else
            {
                rarityColor = card.GetRarityColor();
            }
            backgroundImage.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.5f);
        }
        
        transform.localScale = Vector3.one * 1.05f;
    }
    
    public void OnPointerExit()
    {
        if (backgroundImage != null)
        {
            Color rarityColor;
            if (combinedCard != null && manager != null)
            {
                rarityColor = manager.GetRarityColor(combinedCard.rarity);
            }
            else if (combinedCard != null)
            {
                rarityColor = CardRarityHelper.GetRarityColor(combinedCard.rarity);
            }
            else if (card != null && manager != null)
            {
                rarityColor = manager.GetRarityColor(card.rarity);
            }
            else
            {
                rarityColor = card.GetRarityColor();
            }
            backgroundImage.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.3f);
        }
        
        transform.localScale = Vector3.one;
    }
}
