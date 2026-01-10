using UnityEngine;

[CreateAssetMenu(fileName = "CritGainOnNotCritFavour", menuName = "Favour Effects 2/Crit Gain On Not Crit")] 
public class CritGainOnNotCritFavour : FavourEffect
{
    [Header("Crit Gain On Not Crit Settings")]
    [Tooltip("FRENZY stacks gained when damage that can crit does NOT crit.")]
    public float FrenzyGain = 1f;

    private PlayerStats playerStats;
    private StatusController playerStatus;
    private int sourceKey;
    private int lastProcessedNuclearStrikeFrame = -1;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (player != null && playerStats == null)
        {
            playerStats = player.GetComponent<PlayerStats>();
        }

        if (player != null)
        {
            playerStatus = player.GetComponent<StatusController>();
        }

        sourceKey = Mathf.Abs(GetInstanceID());
        if (sourceKey == 0)
        {
            sourceKey = 1;
        }

        lastProcessedNuclearStrikeFrame = -1;

        // One-time favour: prevent this card from appearing again this run.
        if (sourceCard != null && CardSelectionManager.Instance != null)
        {
            CardSelectionManager.Instance.RegisterOneTimeFavourUsed(sourceCard);
        }
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
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

        if (playerStatus == null && player != null)
        {
            playerStatus = player.GetComponent<StatusController>();
        }
        if (playerStatus == null)
        {
            return;
        }

        ProjectileCards currentCard = manager.CurrentProjectileCard;
        if (!StatusDamageScope.IsStatusTick && currentCard != null && currentCard.projectileType == ProjectileCards.ProjectileType.NuclearStrike)
        {
            if (lastProcessedNuclearStrikeFrame == Time.frameCount)
            {
                return;
            }
            lastProcessedNuclearStrikeFrame = Time.frameCount;
        }

        bool didCrit = playerStats.lastHitWasCrit;
        if (didCrit)
        {
            while (playerStatus.ConsumeStacks(StatusId.Frenzy, 1, sourceKey)) { }
        }
        else
        {
            int stacksToAdd = Mathf.RoundToInt(Mathf.Max(0f, FrenzyGain));
            if (stacksToAdd > 0)
            {
                playerStatus.AddStatus(StatusId.Frenzy, stacksToAdd, -1f, 0f, null, sourceKey);
            }
        }

    }

    public override void OnCritResolved(GameObject player, ProjectileCards sourceCard, bool canCrit, bool didCrit, FavourEffectManager manager)
    {
        if (!canCrit || manager == null)
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

        if (playerStatus == null && player != null)
        {
            playerStatus = player.GetComponent<StatusController>();
        }
        if (playerStatus == null)
        {
            return;
        }

        if (didCrit)
        {
            while (playerStatus.ConsumeStacks(StatusId.Frenzy, 1, sourceKey)) { }
        }
        else
        {
            int stacksToAdd = Mathf.RoundToInt(Mathf.Max(0f, FrenzyGain));
            if (stacksToAdd > 0)
            {
                playerStatus.AddStatus(StatusId.Frenzy, stacksToAdd, -1f, 0f, null, sourceKey);
            }
        }
    }
}
