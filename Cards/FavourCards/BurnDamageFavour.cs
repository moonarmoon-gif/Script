using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "BurnDamageFavour", menuName = "Favour Effects/Burn Damage")] 
public class BurnDamageFavour : FavourEffect
{
    [Header("Burn Damage Settings")]
    [Tooltip("Bonus burn damage per tick per card as a percent (10 = +10% burn tick damage).")]
    [FormerlySerializedAs("BonusTotalDamage")]
    public float BonusBurnDamagePerTick = 10f;

    private int stacks = 0;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        stacks = 1;
        float delta = Mathf.Max(0f, BonusBurnDamagePerTick) / 100f;
        if (StatusControllerManager.Instance != null)
        {
            StatusControllerManager.Instance.AddBurnTickDamageMultiplier(delta);
        }
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        stacks++;
        float delta = Mathf.Max(0f, BonusBurnDamagePerTick) / 100f;
        if (StatusControllerManager.Instance != null)
        {
            StatusControllerManager.Instance.AddBurnTickDamageMultiplier(delta);
        }
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        if (stacks <= 0 || StatusControllerManager.Instance == null)
        {
            return;
        }

        float delta = Mathf.Max(0f, BonusBurnDamagePerTick) / 100f;
        float total = delta * stacks;
        StatusControllerManager.Instance.AddBurnTickDamageMultiplier(-total);
    }
}
