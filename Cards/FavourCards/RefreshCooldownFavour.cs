using UnityEngine;

[CreateAssetMenu(fileName = "RefreshCooldownFavour", menuName = "Favour Effects/Refresh Cooldown")]
public class RefreshCooldownFavour : FavourEffect
{
    [Header("Refresh Cooldown Settings")]
    [Tooltip("Base chance (percent) to refresh the cooldown of ALL projectiles when an enemy is killed.")]
    public float RefreshChancePercent = 2f;

    public float BonusRefreshChancePercent = 0f;

    private float currentChancePercent = 0f;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        float step = Mathf.Max(0f, RefreshChancePercent);
        currentChancePercent = step;
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        float step = Mathf.Max(0f, BonusRefreshChancePercent);
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

        float roll = Random.Range(0f, 100f);
        if (roll > currentChancePercent)
        {
            return;
        }

        ProjectileSpawner spawner = player.GetComponent<ProjectileSpawner>();
        if (spawner != null)
        {
            spawner.RefreshCooldownForAllPassiveCards();
        }

        AdvancedPlayerController controller = player.GetComponent<AdvancedPlayerController>();
        if (controller != null)
        {
            controller.RefreshAllActiveProjectileCooldowns();
        }
    }
}
