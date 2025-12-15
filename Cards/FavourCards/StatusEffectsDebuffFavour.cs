using UnityEngine;

[CreateAssetMenu(fileName = "StatusEffectsDebuffFavour", menuName = "Favour Effects/Status Effects Debuff")] 
public class StatusEffectsDebuffFavour : FavourEffect
{
    [Header("Status Effects Debuff Settings")]
    [Tooltip("Scorched stacks granted when the player inflicts Burn on an enemy.")]
    public int ScorchedStacks = 5;

    [Tooltip("Frostbite stacks granted when the player inflicts Slow on an enemy.")]
    public int FrostBiteStacks = 5;

    [Tooltip("Shocked stacks granted when the player inflicts Static on an enemy.")]
    public int ShockedStacks = 5;

    [Tooltip("Maximum stacks of each elemental debuff that this favour will push an enemy to.")]
    public int MaxStacks = 10;

    [Header("Enhanced")]
    [Tooltip("Additional Scorched stacks granted when enhanced.")]
    public int BonusScorchedStacks = 5;

    [Tooltip("Additional Frostbite stacks granted when enhanced.")]
    public int BonusFrostBiteStacks = 5;

    [Tooltip("Additional Shocked stacks granted when enhanced.")]
    public int BonusStaticStacks = 5;

    [Tooltip("Additional maximum stacks allowed for each debuff when enhanced.")]
    public int BonusMaxStacks = 10;

    private int currentScorchedStacks;
    private int currentFrostBiteStacks;
    private int currentShockedStacks;
    private int currentMaxStacks;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        currentScorchedStacks = Mathf.Max(0, ScorchedStacks);
        currentFrostBiteStacks = Mathf.Max(0, FrostBiteStacks);
        currentShockedStacks = Mathf.Max(0, ShockedStacks);
        currentMaxStacks = Mathf.Max(0, MaxStacks);
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        currentScorchedStacks += Mathf.Max(0, BonusScorchedStacks);
        currentFrostBiteStacks += Mathf.Max(0, BonusFrostBiteStacks);
        currentShockedStacks += Mathf.Max(0, BonusStaticStacks);
        currentMaxStacks += Mathf.Max(0, BonusMaxStacks);
    }

    public override void OnStatusApplied(GameObject player, GameObject enemy, StatusId statusId, FavourEffectManager manager)
    {
        if (enemy == null)
        {
            return;
        }

        StatusController status = enemy.GetComponent<StatusController>() ?? enemy.GetComponentInParent<StatusController>();
        if (status == null)
        {
            return;
        }

        switch (statusId)
        {
            case StatusId.Burn:
                ApplyAdditionalStacks(status, StatusId.Scorched, currentScorchedStacks);
                break;
            case StatusId.Slow:
                ApplyAdditionalStacks(status, StatusId.Frostbite, currentFrostBiteStacks);
                break;
            case StatusId.Static:
                ApplyAdditionalStacks(status, StatusId.Shocked, currentShockedStacks);
                break;
        }
    }

    private void ApplyAdditionalStacks(StatusController status, StatusId targetId, int stacksToAdd)
    {
        if (stacksToAdd <= 0)
        {
            return;
        }

        int existing = status.GetStacks(targetId);
        int maxAllowed = Mathf.Max(0, currentMaxStacks);

        if (maxAllowed > 0 && existing >= maxAllowed)
        {
            return;
        }

        int room = maxAllowed > 0 ? Mathf.Max(0, maxAllowed - existing) : stacksToAdd;
        int toAdd = maxAllowed > 0 ? Mathf.Min(room, stacksToAdd) : stacksToAdd;

        if (toAdd > 0)
        {
            status.AddStatus(targetId, toAdd, -1f);
        }
    }
}
