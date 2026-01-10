using UnityEngine;

[CreateAssetMenu(fileName = "ManaRefreshOnLowFavour", menuName = "Favour Effects/Mana Refresh On Low")] 
public class ManaRefreshOnLowFavour : FavourEffect
{
    [Header("Mana Refresh On Low Settings")]
    [Tooltip("Mana threshold fraction 0-1. When current mana falls at or below this fraction, mana is instantly refilled once per boss phase.")]
    public float ManaThreshold = 0.1f;

    private PlayerMana playerMana;
    private EnemyCardSpawner enemyCardSpawner;
    private EnemySpawner enemySpawner;

    private bool hasTriggeredThisPhase;
    private bool lastBossEventActive;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (player == null)
        {
            return;
        }

        if (playerMana == null)
        {
            playerMana = player.GetComponent<PlayerMana>();
        }

        if (enemySpawner == null)
        {
            enemySpawner = Object.FindObjectOfType<EnemySpawner>();
        }

        if (enemyCardSpawner == null)
        {
            enemyCardSpawner = Object.FindObjectOfType<EnemyCardSpawner>();
        }

        hasTriggeredThisPhase = false;
        lastBossEventActive = (enemySpawner != null && enemySpawner.IsBossEventActive)
                              || (enemyCardSpawner != null && enemyCardSpawner.IsBossEventActive);

        // Mark this favour as one-time so it will not appear again in this run.
        if (sourceCard != null && CardSelectionManager.Instance != null)
        {
            CardSelectionManager.Instance.RegisterOneTimeFavourUsed(sourceCard);
        }
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        // This favour has no explicit enhanced parameters; upgrades simply
        // re-run the same setup logic.
        OnApply(player, manager, sourceCard);
    }

    public override void OnUpdate(GameObject player, FavourEffectManager manager, float deltaTime)
    {
        if (playerMana == null)
        {
            if (player != null)
            {
                playerMana = player.GetComponent<PlayerMana>();
            }
            if (playerMana == null)
            {
                return;
            }
        }

        // Lazy-acquire spawners so we can observe boss event phases.
        if (enemySpawner == null)
        {
            enemySpawner = Object.FindObjectOfType<EnemySpawner>();
        }

        if (enemyCardSpawner == null)
        {
            enemyCardSpawner = Object.FindObjectOfType<EnemyCardSpawner>();
        }

        // Reset the one-per-phase trigger after each completed boss event
        bool bossActiveNow = (enemySpawner != null && enemySpawner.IsBossEventActive)
                             || (enemyCardSpawner != null && enemyCardSpawner.IsBossEventActive);

        if (lastBossEventActive && !bossActiveNow)
        {
            hasTriggeredThisPhase = false;
        }

        lastBossEventActive = bossActiveNow;

        // If we've already triggered in this phase, do nothing until reset
        if (hasTriggeredThisPhase)
        {
            return;
        }

        float maxMana = playerMana.MaxManaExact;
        if (maxMana <= 0f)
        {
            return;
        }

        float currentMana = playerMana.CurrentManaExact;
        float fraction = currentMana / maxMana;
        float threshold = Mathf.Clamp01(ManaThreshold);

        if (fraction > threshold)
        {
            return;
        }

        // Instantly refill mana to full once this phase when dropping below threshold
        int intMax = playerMana.MaxMana;
        int intCurrent = playerMana.CurrentMana;
        int missing = Mathf.Max(0, intMax - intCurrent);
        if (missing > 0)
        {
            playerMana.AddMana(missing);
        }

        hasTriggeredThisPhase = true;
    }
}
