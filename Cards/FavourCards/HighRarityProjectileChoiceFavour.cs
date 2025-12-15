using UnityEngine;
using System.Collections;

[CreateAssetMenu(fileName = "HighRarityProjectileChoiceFavour", menuName = "Favour Effects/High-Rarity Projectile Choice")]
public class HighRarityProjectileChoiceFavour : FavourEffect
{
    [Header("Projectile Choice Settings")]
    [Tooltip("Lowest rarity that can appear in the projectile choice.")]
    public CardRarity LowestRarity = CardRarity.Rare;

    [Tooltip("Number of passive projectile choices to show.")]
    public int Choices = 3;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (player == null || manager == null)
        {
            return;
        }

        if (CardSelectionManager.Instance == null)
        {
            return;
        }

        manager.StartCoroutine(ShowProjectileChoiceRoutine());
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        OnApply(player, manager, sourceCard);
    }

    private IEnumerator ShowProjectileChoiceRoutine()
    {
        float delay = 0f;
        if (CardSelectionManager.Instance != null)
        {
            delay = CardSelectionManager.Instance.cardDisplayDelay;
        }

        if (delay > 0f)
        {
            yield return new WaitForSecondsRealtime(delay);
        }
        else
        {
            yield return null;
        }

        CardSelectionManager selectionManager = CardSelectionManager.Instance;
        if (selectionManager == null)
        {
            yield break;
        }

        int count = Choices > 0 ? Choices : 3;
        selectionManager.ShowPassiveProjectileChoiceWithMinRarity(count, LowestRarity);
    }
}
