using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CritGainOnNotCritFavour", menuName = "Favour Effects/Crit Gain On Not Crit")] 
public class CritGainOnNotCritFavour : FavourEffect
{
    [Header("Crit Gain On Not Crit Settings")]
    [Tooltip("Crit chance gain (in percent) added to the current ACTIVE projectile when it fails to crit.")]
    public float CritGain = 5f;

    private PlayerStats playerStats;

    // Track extra crit chance per ACTIVE projectile card. Keyed by the
    // runtime ProjectileCards instance currently firing.
    private readonly Dictionary<ProjectileCards, float> bonusCritByCard = new Dictionary<ProjectileCards, float>();

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (player != null && playerStats == null)
        {
            playerStats = player.GetComponent<PlayerStats>();
        }

        bonusCritByCard.Clear();

        // One-time favour: prevent this card from appearing again this run.
        if (sourceCard != null && CardSelectionManager.Instance != null)
        {
            CardSelectionManager.Instance.RegisterOneTimeFavourUsed(sourceCard);
        }
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        // No explicit enhanced parameters; upgrades simply re-run setup.
        OnApply(player, manager, sourceCard);
    }

    public override void OnBeforeDealDamage(GameObject player, GameObject enemy, ref float damage, FavourEffectManager manager)
    {
        if (damage <= 0f || manager == null)
        {
            return;
        }

        if (playerStats == null && player != null)
        {
            playerStats = player.GetComponent<PlayerStats>();
        }

        if (playerStats == null)
        {
            return;
        }

        ProjectileCards currentCard = manager.CurrentProjectileCard;
        if (currentCard == null)
        {
            return;
        }

        // Only affect ACTIVE projectile systems; passive projectiles should
        // never receive this crit chance bonus.
        if (currentCard.projectileSystem != ProjectileCards.ProjectileSystemType.Active)
        {
            return;
        }

        float gain = Mathf.Max(0f, CritGain);
        if (gain <= 0f)
        {
            return;
        }

        if (!bonusCritByCard.TryGetValue(currentCard, out float accumulated))
        {
            accumulated = 0f;
        }

        // If the base damage pipeline already produced a crit, treat this as a
        // successful crit and reset the accumulated bonus for this card.
        if (playerStats.lastHitWasCrit)
        {
            if (accumulated > 0f)
            {
                bonusCritByCard[currentCard] = 0f;
            }
            return;
        }

        // Base hit was NOT a crit: increase this ACTIVE projectile's stored
        // crit chance for future hits.
        accumulated = Mathf.Clamp(accumulated + gain, 0f, 100f);
        bonusCritByCard[currentCard] = accumulated;

        // Optional overlay: allow this same non-crit hit to "convert" into a
        // bonus crit using the accumulated chance, without affecting any other
        // projectiles. This simulates the increased crit chance for this card
        // only.
        if (accumulated <= 0f)
        {
            return;
        }

        float roll = Random.Range(0f, 100f);
        if (roll >= accumulated)
        {
            return;
        }

        // Convert this hit into a crit for this ACTIVE projectile only.
        float critDamagePercent = playerStats.projectileCritDamage;
        if (critDamagePercent <= 0f)
        {
            critDamagePercent = 150f;
        }

        damage *= critDamagePercent / 100f;
        playerStats.lastHitWasCrit = true;

        // After a successful crit, reset the accumulated bonus for this card.
        bonusCritByCard[currentCard] = 0f;
    }
}
