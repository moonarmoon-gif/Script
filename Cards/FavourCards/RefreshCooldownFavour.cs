using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "RefreshCooldownFavour", menuName = "Favour Effects/Refresh Cooldown")]
public class RefreshCooldownFavour : FavourEffect
{
    [Header("Refresh Cooldown Settings")]
    [Tooltip("Base chance (percent) to refresh the cooldown of the passive projectile that killed an enemy.")]
    public float RefreshChancePercent = 2f;

    private float currentChancePercent = 0f;
    private readonly Dictionary<ProjectileCards, float> nextAllowedRefreshTimes = new Dictionary<ProjectileCards, float>();

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        float step = Mathf.Max(0f, RefreshChancePercent);
        currentChancePercent = step;
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        float step = Mathf.Max(0f, RefreshChancePercent);
        currentChancePercent += step;
    }

    public override void OnEnemyKilled(GameObject player, GameObject enemy, FavourEffectManager manager)
    {
        if (player == null || enemy == null)
        {
            return;
        }

        if (currentChancePercent <= 0f)
        {
            return;
        }

        EnemyLastHitSource marker = enemy.GetComponentInParent<EnemyLastHitSource>();
        ProjectileCards card = marker != null ? marker.lastProjectileCard : null;
        if (card == null)
        {
            return;
        }

        if (card.projectileSystem != ProjectileCards.ProjectileSystemType.Passive)
        {
            return;
        }

        float roll = Random.Range(0f, 100f);
        if (roll > currentChancePercent)
        {
            return;
        }

        float now = Time.time;
        if (nextAllowedRefreshTimes.TryGetValue(card, out float nextAllowed) && now < nextAllowed)
        {
            return;
        }

        ProjectileSpawner spawner = player.GetComponent<ProjectileSpawner>();
        if (spawner == null)
        {
            return;
        }

        spawner.RefreshCooldownForPassiveCard(card);

        float baseInterval = card.runtimeSpawnInterval > 0f ? card.runtimeSpawnInterval : card.spawnInterval;
        if (baseInterval <= 0f)
        {
            baseInterval = 0.1f;
        }

        nextAllowedRefreshTimes[card] = now + baseInterval;
    }
}
