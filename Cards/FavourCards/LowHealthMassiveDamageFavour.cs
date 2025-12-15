using UnityEngine;

[CreateAssetMenu(fileName = "LowHealthMassiveDamageFavour", menuName = "Favour Effects/Low Health Massive Damage")]
public class LowHealthMassiveDamageFavour : FavourEffect
{
    [Header("Low Health Trigger Settings")]
    [Tooltip("Health percentage threshold to start channeling (e.g. 25 = 25% max health).")]
    [Range(0f, 100f)]
    public float HealthThresholdPercent = 25f;

    [Header("Channel Settings")]
    [Tooltip("Duration of the channel before the burst triggers (seconds).")]
    public float ChannelDuration = 3f;

    [Header("Burst Damage Settings")]
    [Tooltip("Damage dealt as a percentage of the player's Attack stat (1000 = 1000% = 10x Attack).")]
    public float BurstDamagePercent = 1000f;

    [Tooltip("Cooldown between successful bursts (seconds).")]
    public float CooldownSeconds = 60f;

    private PlayerHealth playerHealth;
    private PlayerStats playerStats;
    private bool isChanneling;
    private float channelEndTime;
    private float nextAvailableTime;

    public override void OnApply(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (player == null)
        {
            return;
        }

        if (playerHealth == null)
        {
            playerHealth = player.GetComponent<PlayerHealth>();
        }

        if (playerStats == null)
        {
            playerStats = player.GetComponent<PlayerStats>();
        }

        nextAvailableTime = 0f;
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        OnApply(player, manager, sourceCard);
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        isChanneling = false;
    }

    public override void OnUpdate(GameObject player, FavourEffectManager manager, float deltaTime)
    {
        if (player == null || playerHealth == null)
        {
            return;
        }

        if (!playerHealth.IsAlive)
        {
            isChanneling = false;
            return;
        }

        float threshold = Mathf.Clamp(HealthThresholdPercent, 0f, 100f);
        float healthPercent = playerHealth.MaxHealth > 0f
            ? (playerHealth.CurrentHealth / playerHealth.MaxHealth) * 100f
            : 0f;

        if (!isChanneling)
        {
            if (healthPercent <= threshold && Time.time >= nextAvailableTime)
            {
                float duration = Mathf.Max(0.01f, ChannelDuration);
                isChanneling = true;
                channelEndTime = Time.time + duration;
            }
        }
        else
        {
            if (Time.time >= channelEndTime)
            {
                isChanneling = false;
                TriggerBurst(player);
            }
        }
    }

    private void TriggerBurst(GameObject player)
    {
        if (playerHealth == null || !playerHealth.IsAlive)
        {
            return;
        }

        if (playerStats == null && player != null)
        {
            playerStats = player.GetComponent<PlayerStats>();
        }

        if (playerStats == null)
        {
            return;
        }

        float percent = Mathf.Max(0f, BurstDamagePercent);
        if (percent <= 0f)
        {
            return;
        }

        nextAvailableTime = Time.time + Mathf.Max(0f, CooldownSeconds);

        EnemyHealth[] enemies = Object.FindObjectsOfType<EnemyHealth>();
        if (enemies == null || enemies.Length == 0)
        {
            return;
        }

        foreach (EnemyHealth enemyHealth in enemies)
        {
            if (enemyHealth == null || !enemyHealth.IsAlive)
            {
                continue;
            }

            float finalDamage = PlayerDamageHelper.ComputeAttackDamage(playerStats, enemyHealth.gameObject, percent);
            if (finalDamage <= 0f)
            {
                continue;
            }

            enemyHealth.TakeDamage(finalDamage, enemyHealth.transform.position, Vector3.zero);
        }
    }
}
