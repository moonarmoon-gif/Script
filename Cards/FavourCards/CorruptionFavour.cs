using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "CorruptionFavour", menuName = "Favour Effects/Corruption")]
public class CorruptionFavour : FavourEffect
{
    [Header("Corruption Settings")]
    public int CorruptionStack = 1;

    [Header("Enhanced")]
    public int BonusCorruptionStack = 1;

    private int currentCorruptionStacks;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        currentCorruptionStacks = Mathf.Max(0, CorruptionStack);
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        currentCorruptionStacks += Mathf.Max(0, BonusCorruptionStack);
    }

    public override void OnEnemyDamageFinalized(GameObject player, GameObject enemy, float finalDamage, bool isStatusTick, FavourEffectManager manager)
    {
        if (isStatusTick)
        {
            return;
        }

        if (finalDamage <= 0f)
        {
            return;
        }

        if (currentCorruptionStacks <= 0)
        {
            return;
        }

        if (enemy == null)
        {
            return;
        }

        EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>() ?? enemy.GetComponentInParent<EnemyHealth>();
        if (enemyHealth == null || !enemyHealth.IsAlive)
        {
            return;
        }

        EnemyLastHitSource hitSource = enemyHealth.GetComponent<EnemyLastHitSource>() ?? enemyHealth.GetComponentInParent<EnemyLastHitSource>();
        ProjectileCards sourceCard = hitSource != null ? hitSource.lastProjectileCard : null;
        if (sourceCard == null)
        {
            return;
        }

        StatusController statusController = enemyHealth.GetComponent<StatusController>() ?? enemyHealth.GetComponentInParent<StatusController>();
        if (statusController == null)
        {
            return;
        }

        statusController.AddStatus(StatusId.Corruption, currentCorruptionStacks, -1f);
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        currentCorruptionStacks = 0;
    }
}
