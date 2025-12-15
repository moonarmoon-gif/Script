using UnityEngine;

[CreateAssetMenu(fileName = "ActivePoisonWoundFavour", menuName = "Favour Effects/Active Poison Wound")]
public class ActivePoisonWoundFavour : FavourEffect
{
    [Header("Active Poison Wound Settings")]
    [Tooltip("Base percent chance for ACTIVE projectiles to inflict each status (Poison and Wound) per hit (e.g. 5 = 5% chance each).")]
    public float StatusChance = 5f;

    [Tooltip("Number of Poison stacks to apply on a successful proc.")]
    public int PoisonStack = 5;

    [Tooltip("Number of Wound stacks to apply on a successful proc.")]
    public int WoundStack = 5;

    [Header("Enhanced")]
    [Tooltip("Additional percent chance granted when this favour is enhanced.")]
    public float BonusStatusChance = 5f;

    [Tooltip("Additional Poison stacks granted when this favour is enhanced.")]
    public int BonusPoisonStack = 5;

    [Tooltip("Additional Wound stacks granted when this favour is enhanced.")]
    public int BonusWoundStack = 5;

    private float currentStatusChance;
    private int currentPoisonStacks;
    private int currentWoundStacks;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        currentStatusChance = Mathf.Max(0f, StatusChance);
        currentPoisonStacks = Mathf.Max(0, PoisonStack);
        currentWoundStacks = Mathf.Max(0, WoundStack);
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        currentStatusChance += Mathf.Max(0f, BonusStatusChance);
        currentPoisonStacks += Mathf.Max(0, BonusPoisonStack);
        currentWoundStacks += Mathf.Max(0, BonusWoundStack);
    }

    public override void OnBeforeDealDamage(GameObject player, GameObject enemy, ref float damage, FavourEffectManager manager)
    {
        TryApplyStatuses(enemy, manager);
    }

    private void TryApplyStatuses(GameObject enemy, FavourEffectManager manager)
    {
        if (enemy == null || manager == null)
        {
            return;
        }

        if (currentStatusChance <= 0f || (currentPoisonStacks <= 0 && currentWoundStacks <= 0))
        {
            return;
        }

        ProjectileCards currentCard = manager.CurrentProjectileCard;
        if (currentCard == null || currentCard.projectileSystem != ProjectileCards.ProjectileSystemType.Active)
        {
            return;
        }

        StatusController status = enemy.GetComponent<StatusController>() ?? enemy.GetComponentInParent<StatusController>();
        if (status == null)
        {
            return;
        }

        // Resolve the finite durations for Poison and Wound from the central
        // StatusControllerManager so these statuses tick only for their
        // configured duration instead of behaving like permanent stacks.
        // Roll independently for Poison and Wound so each has its own
        // chance to be applied on hit.
        if (currentPoisonStacks > 0)
        {
            float rollPoison = Random.Range(0f, 100f);
            if (rollPoison < currentStatusChance)
            {
                status.AddStatus(StatusId.Poison, currentPoisonStacks);
            }
        }

        if (currentWoundStacks > 0)
        {
            float rollWound = Random.Range(0f, 100f);
            if (rollWound < currentStatusChance)
            {
                status.AddStatus(StatusId.Wound, currentWoundStacks);
            }
        }
    }
}
