using UnityEngine;

[CreateAssetMenu(fileName = "FrenzyOnActive", menuName = "Favour Effects/Frenzy On Active")]
public class FrenzyOnActive : FavourEffect
{
    [Header("Frenzy On Active Settings")]
    [Tooltip("FRENZY stacks gained when dealing a critical hit with an ACTIVE projectile.")]
    public int FrenzyGain = 1;

    [Tooltip("Duration in seconds for each gained FRENZY stack.")]
    public float Duration = 5f;

    [Tooltip("Maximum number of FRENZY stacks that can be gained from this favour at once.")]
    public int MaxStacks = 5;

    [Header("Enhanced")]
    [Tooltip("Extra max stacks gained when this favour is chosen again.")]
    public int BonusMaxStacks = 5;

    private PlayerStats playerStats;
    private StatusController playerStatus;
    private int sourceKey;

    private int currentMaxStacks;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (player == null)
        {
            return;
        }

        if (playerStats == null)
        {
            playerStats = player.GetComponent<PlayerStats>();
        }

        if (playerStatus == null)
        {
            playerStatus = player.GetComponent<StatusController>();
        }

        sourceKey = Mathf.Abs(GetInstanceID());
        if (sourceKey == 0)
        {
            sourceKey = 1;
        }

        currentMaxStacks = Mathf.Max(0, MaxStacks);
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (playerStats == null || playerStatus == null)
        {
            OnApply(player, manager, sourceCard);
        }

        currentMaxStacks += Mathf.Max(0, BonusMaxStacks);
    }

    public override void OnBeforeDealDamage(GameObject player, GameObject enemy, ref float damage, FavourEffectManager manager)
    {
        if (damage <= 0f || manager == null)
        {
            return;
        }

        if (playerStats == null || playerStatus == null)
        {
            OnApply(player, manager, null);
        }

        if (playerStats == null || playerStatus == null)
        {
            return;
        }

        ProjectileCards currentCard = manager.CurrentProjectileCard;
        if (currentCard == null || currentCard.projectileSystem != ProjectileCards.ProjectileSystemType.Active)
        {
            return;
        }

        if (!playerStats.lastHitWasCrit)
        {
            return;
        }

        TryGrantFrenzyStacks(currentCard, true);
    }

    public override void OnCritResolved(GameObject player, ProjectileCards sourceCard, bool canCrit, bool didCrit, FavourEffectManager manager)
    {
        if (!canCrit || !didCrit)
        {
            return;
        }

        if (manager == null)
        {
            return;
        }

        if (playerStats == null || playerStatus == null)
        {
            OnApply(player, manager, null);
        }

        if (playerStatus == null)
        {
            return;
        }

        ProjectileCards card = sourceCard != null ? sourceCard : manager.CurrentProjectileCard;
        if (card == null || card.projectileSystem != ProjectileCards.ProjectileSystemType.Active)
        {
            return;
        }

        TryGrantFrenzyStacks(card, didCrit);
    }

    private void TryGrantFrenzyStacks(ProjectileCards sourceCard, bool didCrit)
    {
        if (!didCrit)
        {
            return;
        }

        int gain = Mathf.Max(0, FrenzyGain);
        float duration = Mathf.Max(0f, Duration);
        int maxStacks = Mathf.Max(0, currentMaxStacks);

        if (gain <= 0 || duration <= 0f || maxStacks <= 0)
        {
            return;
        }

        int current = playerStatus != null ? playerStatus.GetStacks(StatusId.Frenzy, sourceKey) : 0;
        int room = maxStacks - current;
        if (room <= 0)
        {
            return;
        }

        int toAdd = Mathf.Min(room, gain);
        for (int i = 0; i < toAdd; i++)
        {
            playerStatus.AddStatus(StatusId.Frenzy, 1, duration, 0f, sourceCard, sourceKey);
        }
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        if (playerStatus == null && player != null)
        {
            playerStatus = player.GetComponent<StatusController>();
        }

        if (playerStatus != null)
        {
            while (playerStatus.ConsumeStacks(StatusId.Frenzy, 1, sourceKey)) { }
        }
    }
}
