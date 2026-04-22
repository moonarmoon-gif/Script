using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "VulnerableOnCritFavour", menuName = "Favour Effects/Vulnerable On Crit")]
public class VulnerableOnCritFavour : FavourEffect
{
    [Header("Vulnerable On Crit Settings")]
    public int VulnerableStacks = 1;
    public int MaxStacks = 2;

    [Header("Enhanced")]
    public int BonusMaxStacks = 2;

    private PlayerStats playerStats;
    private int sourceKey;
    private int currentMaxStacks;

    private readonly Dictionary<int, int> pendingStacksByEnemyId = new Dictionary<int, int>();

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (playerStats == null && player != null)
        {
            playerStats = player.GetComponent<PlayerStats>();
        }

        sourceKey = Mathf.Abs(GetInstanceID());
        if (sourceKey == 0)
        {
            sourceKey = 1;
        }

        currentMaxStacks = Mathf.Max(0, MaxStacks);
        pendingStacksByEnemyId.Clear();
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (playerStats == null || sourceKey == 0)
        {
            OnApply(player, manager, sourceCard);
        }

        currentMaxStacks += Mathf.Max(0, BonusMaxStacks);
    }

    public override void OnBeforeDealDamage(GameObject player, GameObject enemy, ref float damage, FavourEffectManager manager)
    {
        if (damage <= 0f || enemy == null)
        {
            return;
        }

        if (playerStats == null && player != null)
        {
            playerStats = player.GetComponent<PlayerStats>();
        }

        if (playerStats == null || !playerStats.lastHitWasCrit)
        {
            return;
        }

        int stacks = Mathf.Max(0, VulnerableStacks);
        if (stacks <= 0)
        {
            return;
        }

        int enemyId = enemy.GetInstanceID();
        if (pendingStacksByEnemyId.TryGetValue(enemyId, out int existing))
        {
            pendingStacksByEnemyId[enemyId] = existing + stacks;
        }
        else
        {
            pendingStacksByEnemyId[enemyId] = stacks;
        }
    }

    public override void OnEnemyDamageFinalized(GameObject player, GameObject enemy, float finalDamage, bool isStatusTick, FavourEffectManager manager)
    {
        if (enemy == null)
        {
            return;
        }

        int enemyId = enemy.GetInstanceID();
        if (!pendingStacksByEnemyId.TryGetValue(enemyId, out int pendingStacks))
        {
            return;
        }

        pendingStacksByEnemyId.Remove(enemyId);

        int maxStacks = Mathf.Max(0, currentMaxStacks);
        if (maxStacks <= 0)
        {
            return;
        }

        StatusController statusController = enemy.GetComponent<StatusController>() ?? enemy.GetComponentInParent<StatusController>();
        if (statusController == null)
        {
            return;
        }

        int current = statusController.GetStacks(StatusId.Vulnerable, sourceKey);
        int room = maxStacks - current;
        if (room <= 0)
        {
            return;
        }

        int toAdd = Mathf.Min(room, Mathf.Max(0, pendingStacks));
        if (toAdd <= 0)
        {
            return;
        }

        statusController.AddStatus(StatusId.Vulnerable, toAdd, -1f, 0f, manager != null ? manager.CurrentProjectileCard : null, sourceKey);
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        pendingStacksByEnemyId.Clear();
    }
}
