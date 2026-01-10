using UnityEngine;

[CreateAssetMenu(fileName = "DefenseOnHpFavour", menuName = "Favour Effects 2/Defense On Hp")]
public class DefenseOnHpFavour : FavourEffect
{
    [Header("Defense On Hp Settings")]
    public int DefenseGain = 1;

    [Tooltip("Percent of max health damage required to grant Defense (e.g., 10 = 10%).")]
    public float HealthThreshold = 10f;

    [Header("Enhanced")]
    public int BonusDefenseGain = 1;

    [Header("Pick Limit")]
    public int MaxPickLimit = 0;

    private PlayerHealth playerHealth;
    private StatusController statusController;

    private int sourceKey;
    private float accumulatedDamagePercent;

    protected override int GetMaxPickLimit()
    {
        return MaxPickLimit;
    }

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (player == null)
        {
            return;
        }

        playerHealth = player.GetComponent<PlayerHealth>();
        statusController = player.GetComponent<StatusController>();

        sourceKey = Mathf.Abs(GetInstanceID());
        if (sourceKey == 0)
        {
            sourceKey = 1;
        }

        accumulatedDamagePercent = 0f;
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        DefenseGain += Mathf.Max(0, BonusDefenseGain);
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        if (statusController != null)
        {
            statusController.ConsumeStacks(StatusId.Defense, 999999, sourceKey);
        }

        accumulatedDamagePercent = 0f;
        playerHealth = null;
        statusController = null;
    }

    public override void OnPlayerDamageFinalized(GameObject player, GameObject attacker, float finalDamage, bool isStatusTick, bool isAoeDamage, FavourEffectManager manager)
    {
        if (playerHealth == null || statusController == null)
        {
            return;
        }

        if (!playerHealth.IsAlive)
        {
            return;
        }

        if (finalDamage <= 0f)
        {
            return;
        }

        float maxHealth = playerHealth.MaxHealth;
        if (maxHealth <= 0f)
        {
            return;
        }

        float thresholdPercent = Mathf.Max(0.01f, HealthThreshold);
        float damagePercent = (finalDamage / maxHealth) * 100f;
        if (damagePercent <= 0f)
        {
            return;
        }

        accumulatedDamagePercent += damagePercent;

        int triggers = Mathf.FloorToInt(accumulatedDamagePercent / thresholdPercent);
        if (triggers <= 0)
        {
            return;
        }

        accumulatedDamagePercent -= triggers * thresholdPercent;

        int gainPerTrigger = Mathf.Max(0, DefenseGain);
        int stacksToAdd = triggers * gainPerTrigger;
        if (stacksToAdd <= 0)
        {
            return;
        }

        statusController.AddStatus(StatusId.Defense, stacksToAdd, -1f, 0f, null, sourceKey);
    }
}
