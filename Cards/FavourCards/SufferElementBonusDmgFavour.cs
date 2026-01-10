using UnityEngine;

[CreateAssetMenu(fileName = "SufferElementBonusDmgFavour", menuName = "Favour Effects/Suffer Element Bonus Damage")]
public class SufferElementBonusDmgFavour : FavourEffect
{
    [Header("Suffer Element Bonus Damage Settings")]
    public int VulnerableStack = 1;

    [Header("Enhanced")]
    public int BonusVulnerableStack = 1;

    private int sourceKey;

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
        VulnerableStack += Mathf.Max(0, BonusVulnerableStack);
    }

    public override void OnStatusApplied(GameObject player, GameObject enemy, StatusId statusId, FavourEffectManager manager)
    {
        if (enemy == null)
        {
            return;
        }

        if (statusId == StatusId.Vulnerable)
        {
            return;
        }

        int stacks = Mathf.Max(0, VulnerableStack);
        if (stacks <= 0)
        {
            return;
        }

        StatusController statusController = enemy.GetComponent<StatusController>() ?? enemy.GetComponentInParent<StatusController>();
        if (statusController == null)
        {
            return;
        }

        statusController.AddStatus(StatusId.Vulnerable, stacks, -1f, 0f, null, sourceKey);
    }
}
