using UnityEngine;

[CreateAssetMenu(fileName = "BossDeathMarkFavour", menuName = "Favour Effects/Boss Death Mark")]
public class BossDeathMarkFavour : FavourEffect
{
    [Header("Boss Death Mark Settings")]
    public int DeathMarkStack = 1;

    [Header("Enhanced")]
    public int BonusDeathMarkStack = 0;

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
        DeathMarkStack += Mathf.Max(0, BonusDeathMarkStack);
    }

    public override void OnBeforeDealDamage(GameObject player, GameObject enemy, ref float damage, FavourEffectManager manager)
    {
        if (enemy == null || damage <= 0f || StatusDamageScope.IsStatusTick)
        {
            return;
        }

        ProjectileCards card = manager != null ? manager.CurrentProjectileCard : null;
        if (card == null || card.projectileSystem != ProjectileCards.ProjectileSystemType.Active)
        {
            return;
        }

        EnemyCardTag tag = enemy.GetComponent<EnemyCardTag>() ?? enemy.GetComponentInParent<EnemyCardTag>();
        if (tag == null || tag.rarity != CardRarity.Boss)
        {
            return;
        }

        StatusController status = enemy.GetComponent<StatusController>() ?? enemy.GetComponentInParent<StatusController>();
        if (status == null)
        {
            return;
        }

        int toApply = Mathf.Max(0, DeathMarkStack);
        if (toApply <= 0)
        {
            return;
        }

        status.AddStatus(StatusId.DeathMark, toApply, -1f, 0f, card, sourceKey);
    }
}
