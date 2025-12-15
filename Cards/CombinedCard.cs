using UnityEngine;

public class CombinedCard
{
    public CoreCards basicCard;
    public ProjectileModifierCoreCards[] modifierCards;
    public ProjectileCards projectileCard;
    public CardRarity rarity;
    
    public CombinedCard(CoreCards basic, ProjectileModifierCoreCards[] modifiers, CardRarity cardRarity)
    {
        // Create runtime copies to avoid modifying the ScriptableObject assets
        if (basic != null)
        {
            basicCard = Object.Instantiate(basic);
            basicCard.rarity = cardRarity;
        }
        else
        {
            basicCard = null;
        }
        
        rarity = cardRarity;
        projectileCard = null;
        
        if (modifiers != null && modifiers.Length > 0)
        {
            modifierCards = new ProjectileModifierCoreCards[modifiers.Length];
            for (int i = 0; i < modifiers.Length; i++)
            {
                if (modifiers[i] != null)
                {
                    modifierCards[i] = Object.Instantiate(modifiers[i]);
                    modifierCards[i].rarity = cardRarity;
                }
            }
        }
        else
        {
            modifierCards = null;
        }
    }
    
    public void ApplyAllEffects(GameObject player)
    {
        if (projectileCard != null)
        {
            projectileCard.ApplyEffect(player);
            return;
        }
        
        if (basicCard != null)
        {
            basicCard.ApplyEffect(player);
        }
        
        if (modifierCards != null)
        {
            foreach (var modifier in modifierCards)
            {
                if (modifier != null)
                {
                    modifier.ApplyEffect(player);
                }
            }
        }
    }
    
    public string GetCombinedDescription()
    {
        if (projectileCard != null)
        {
            return projectileCard.GetFormattedDescription();
        }
        
        string desc = "";
        
        if (basicCard != null)
        {
            desc += basicCard.GetFormattedDescription();
        }
        
        if (modifierCards != null && modifierCards.Length > 0)
        {
            foreach (var modifier in modifierCards)
            {
                if (modifier != null)
                {
                    string modDesc = modifier.GetFormattedDescription();
                    desc += "\n" + modDesc;
                }
            }
        }
        
        return desc;
    }
    
    public string GetCardName()
    {
        if (projectileCard != null)
        {
            return projectileCard.cardName;
        }
        
        if (basicCard != null)
        {
            return basicCard.cardName;
        }
        return "Unknown Card";
    }
}
