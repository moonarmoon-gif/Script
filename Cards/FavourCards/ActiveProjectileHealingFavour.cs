using UnityEngine;

[CreateAssetMenu(fileName = "ActiveProjectileHealingFavour", menuName = "Favour Effects/Active Projectile Healing")]
public class ActiveProjectileHealingFavour : FavourEffect
{
    [Header("Healing Settings")]
    [Tooltip("Health restored each time a qualifying projectile hit deals at least 1 damage.")]
    public float HealOnDamage = 1f;

    private PlayerHealth playerHealth;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (player == null)
        {
            return;
        }

        if (playerHealth == null)
        {
            playerHealth = player.GetComponent<PlayerHealth>();
        }
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        OnApply(player, manager, sourceCard);
    }

    public override void OnBeforeDealDamage(GameObject player, GameObject enemy, ref float damage, FavourEffectManager manager)
    {
        if (manager == null)
        {
            return;
        }

        ProjectileCards currentCard = manager.CurrentProjectileCard;
        if (currentCard == null)
        {
            return;
        }

        if (currentCard.projectileSystem != ProjectileCards.ProjectileSystemType.Active)
        {
            return;
        }

        if (damage < 1f)
        {
            return;
        }

        if (playerHealth == null && player != null)
        {
            playerHealth = player.GetComponent<PlayerHealth>();
        }

        if (playerHealth == null || !playerHealth.IsAlive)
        {
            return;
        }

        if (HealOnDamage > 0f)
        {
            playerHealth.Heal(HealOnDamage);
        }
    }
}
