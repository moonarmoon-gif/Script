using UnityEngine;

[CreateAssetMenu(fileName = "New Favour Card", menuName = "Cards/Favour Card")]
public class FavourCards : BaseCard
{
    [Header("Favour Settings")]
    [Tooltip("Effect asset that will be applied when this favour card is selected.")]
    public FavourEffect favourEffect;

    [Header("Favour Descriptions")]
    [TextArea(2, 4)]
    public string publicDescription;

    [TextArea(4, 8)]
    public string privateDescription;

    public override void ApplyEffect(GameObject player)
    {
        // NOTE: Concrete favour effects should be implemented per card.
        // This placeholder just logs selection so the system pipeline works.
        Debug.Log($"<color=cyan>Favour Card selected: {cardName} ({rarity})</color>");

        if (player == null)
        {
            return;
        }

        if (favourEffect == null)
        {
            Debug.LogWarning($"<color=yellow>FavourCards '{cardName}' has no FavourEffect assigned.</color>");
            return;
        }

        FavourEffectManager manager = player.GetComponent<FavourEffectManager>();
        if (manager == null)
        {
            manager = player.AddComponent<FavourEffectManager>();
        }

        manager.AddEffect(favourEffect, this);
    }

    public override string GetFormattedDescription()
    {
        if (!string.IsNullOrEmpty(publicDescription))
        {
            return publicDescription;
        }
        return $"Favour effect ({rarity})";
    }
}
