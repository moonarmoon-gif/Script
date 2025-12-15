using UnityEngine;

[CreateAssetMenu(fileName = "NullifyEnemyProjectilesFavour", menuName = "Favour Effects/Nullify Enemy Projectiles")]
public class NullifyEnemyProjectilesFavour : FavourEffect
{
    [Header("Nullify Projectile Settings")]
    [Tooltip("Base number of charges available.")]
    public int Charges = 2;

    [Tooltip("Seconds required for a used charge to recharge.")]
    public float RechargeTime = 25f;

    private float[] nextReadyTimes;
    private int totalCharges = 0;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        int baseCharges = Mathf.Max(0, Charges);
        if (baseCharges <= 0)
        {
            return;
        }

        totalCharges = baseCharges;
        nextReadyTimes = new float[totalCharges];
        for (int i = 0; i < totalCharges; i++)
        {
            nextReadyTimes[i] = 0f;
        }
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        int baseCharges = Mathf.Max(0, Charges);
        if (baseCharges <= 0)
        {
            return;
        }

        int oldTotal = totalCharges;
        int newTotal = Mathf.Max(totalCharges + baseCharges, baseCharges);
        float[] newTimes = new float[newTotal];

        for (int i = 0; i < newTotal; i++)
        {
            if (i < oldTotal && nextReadyTimes != null)
            {
                newTimes[i] = nextReadyTimes[i];
            }
            else
            {
                newTimes[i] = 0f;
            }
        }

        totalCharges = newTotal;
        nextReadyTimes = newTimes;
    }

    public override void OnPlayerHit(GameObject player, GameObject attacker, ref float damage, FavourEffectManager manager)
    {
        if (nextReadyTimes == null || totalCharges <= 0)
        {
            return;
        }

        if (DamageAoeScope.IsAoeDamage)
        {
            return;
        }

        if (damage <= 0f)
        {
            return;
        }

        for (int i = 0; i < totalCharges; i++)
        {
            if (Time.time >= nextReadyTimes[i])
            {
                nextReadyTimes[i] = Time.time + Mathf.Max(0f, RechargeTime);
                damage = 0f;
                break;
            }
        }
    }
}
