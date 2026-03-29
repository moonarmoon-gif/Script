using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "ActivePoisonWoundFavour", menuName = "Favour Effects/Active Poison Wound")]
public class ActivePoisonWoundFavour : FavourEffect
{
    [Header("Active Poison Wound Settings")]
    [FormerlySerializedAs("StatusChance")]
    [Tooltip("Base percent chance for ACTIVE projectiles to inflict Poison per hit (e.g. 5 = 5% chance).")]
    public float PoisonChance = 5f;

    [Tooltip("Number of Poison stacks to apply on a successful proc.")]
    public int PoisonStack = 5;

    [Tooltip("Duration in seconds for Poison stacks applied by this favour.")]
    public float PoisonDuration = 5f;

    [Header("Enhanced")]
    [Tooltip("Additional percent chance granted when this favour is enhanced.")]
    [FormerlySerializedAs("BonusStatusChance")]
    public float BonusPoisonChance = 5f;

    [Tooltip("Additional Poison stacks granted when this favour is enhanced.")]
    public int BonusPoisonStack = 5;

    [FormerlySerializedAs("WoundStack"), SerializeField, HideInInspector]
    private int legacyWoundStack = 0;

    [FormerlySerializedAs("BonusWoundStack"), SerializeField, HideInInspector]
    private int legacyBonusWoundStack = 0;

    private float currentPoisonChance;
    private int currentPoisonStacks;
    private float currentPoisonDuration;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        currentPoisonChance = Mathf.Max(0f, PoisonChance);
        currentPoisonStacks = Mathf.Max(0, PoisonStack);
        currentPoisonDuration = Mathf.Max(0f, PoisonDuration);
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        currentPoisonChance += Mathf.Max(0f, BonusPoisonChance);
        currentPoisonStacks += Mathf.Max(0, BonusPoisonStack);
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

        if (currentPoisonChance <= 0f || currentPoisonStacks <= 0 || currentPoisonDuration <= 0f)
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

        float rollPoison = Random.Range(0f, 100f);
        if (rollPoison < currentPoisonChance)
        {
            status.AddStatus(StatusId.Poison, currentPoisonStacks, currentPoisonDuration);
        }
    }
}
