using UnityEngine;

[CreateAssetMenu(fileName = "BurdenOnDebuffFavour", menuName = "Favour Effects 2/Burden On Debuff")]
public class BurdenOnDebuffFavour : FavourEffect
{
    [Header("Burden On Debuff")]
    public int BurdenStack = 1;
    public int MaxStacks = 5;

    [Header("Enhanced")]
    public int BonusBurdenStack = 1;
    public int BonusMaxStacks = 5;

    [Header("Pick Limit")]
    public int MaxPickLimit = 0;

    private int sourceKey;

    protected override int GetMaxPickLimit()
    {
        return MaxPickLimit;
    }

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        sourceKey = Mathf.Abs(GetInstanceID());
        if (sourceKey == 0)
        {
            sourceKey = 1;
        }
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        BurdenStack += Mathf.Max(0, BonusBurdenStack);
        MaxStacks += Mathf.Max(0, BonusMaxStacks);
    }

    public override void OnStatusApplied(GameObject player, GameObject enemy, StatusId statusId, FavourEffectManager manager)
    {
        if (enemy == null)
        {
            return;
        }

        StatusController statusController = enemy.GetComponent<StatusController>() ?? enemy.GetComponentInParent<StatusController>();
        if (statusController == null)
        {
            return;
        }

        int toAdd = Mathf.Max(0, BurdenStack);
        if (toAdd <= 0)
        {
            return;
        }

        int cap = Mathf.Max(0, MaxStacks);
        if (cap <= 0)
        {
            return;
        }

        if (!IsDebuff(statusId))
        {
            return;
        }

        int current = statusController.GetStacks(StatusId.Burden);
        int allowed = cap - current;
        if (allowed <= 0)
        {
            return;
        }

        int finalAdd = Mathf.Min(toAdd, allowed);
        if (finalAdd <= 0)
        {
            return;
        }

        statusController.AddStatus(StatusId.Burden, finalAdd, -1f, 0f, null, sourceKey);
    }

    private bool IsDebuff(StatusId id)
    {
        switch (id)
        {
            case StatusId.Lethargy:
            case StatusId.Curse:
            case StatusId.Vulnerable:
            case StatusId.Decay:
            case StatusId.Slow:
            case StatusId.Frostbite:
            case StatusId.Freeze:
            case StatusId.Burn:
            case StatusId.Scorched:
            case StatusId.Immolation:
            case StatusId.Static:
            case StatusId.StaticReapply:
            case StatusId.Shocked:
            case StatusId.Poison:
            case StatusId.Bleed:
            case StatusId.Wound:
            case StatusId.Amnesia:
            case StatusId.Weak:
            case StatusId.Overweight:
                return true;
            default:
                return false;
        }
    }
}
