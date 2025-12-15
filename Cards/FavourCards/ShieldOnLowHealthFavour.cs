using UnityEngine;

[CreateAssetMenu(fileName = "ShieldOnLowHealthFavour", menuName = "Favour Effects/Shield On Low Health")]
public class ShieldOnLowHealthFavour : FavourEffect
{
    [Header("Low Health Trigger Settings")]
    [Tooltip("Health percentage threshold to trigger the shield (e.g. 25 = 25% max health).")]
    [Range(0f, 100f)]
    public float HealthThresholdPercent = 25f;

    [Header("Shield Settings")]
    [Tooltip("Amount of shield health granted when triggered.")]
    public float ShieldHealth = 1000f;

    [Tooltip("Shield health lost per tick.")]
    public float ShieldDecayPerTick = 10f;

    [Tooltip("Time between shield decay ticks (seconds). 0.1s with 10 damage = 100 shield per second.")]
    public float ShieldDecayTickInterval = 0.1f;

    [Tooltip("Cooldown between shield activations (seconds).")]
    public float CooldownSeconds = 60f;

    private PlayerHealth playerHealth;
    private int cardStacks = 0;
    private float currentShield;
    private bool shieldActive;
    private float nextDecayTime;
    private float lastTriggerTime = -999f;

    public float CurrentShield => currentShield;
    public bool ShieldActive => shieldActive;

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

        if (playerHealth == null)
        {
            return;
        }

        if (cardStacks <= 0)
        {
            cardStacks = 1;
        }
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        if (cardStacks <= 0)
        {
            OnApply(player, manager, sourceCard);
        }
        else
        {
            cardStacks++;
        }
    }

    public override void OnUpdate(GameObject player, FavourEffectManager manager, float deltaTime)
    {
        if (playerHealth == null || !playerHealth.IsAlive)
        {
            shieldActive = false;
            currentShield = 0f;
            return;
        }

        float threshold = Mathf.Clamp(HealthThresholdPercent, 0f, 100f);
        float healthPercent = playerHealth.MaxHealth > 0f
            ? (playerHealth.CurrentHealth / playerHealth.MaxHealth) * 100f
            : 0f;

        if (!shieldActive)
        {
            if (healthPercent <= threshold && Time.time >= lastTriggerTime + CooldownSeconds)
            {
                ActivateShield();
            }
        }
        else
        {
            if (ShieldDecayPerTick > 0f && ShieldDecayTickInterval > 0f && Time.time >= nextDecayTime)
            {
                currentShield -= ShieldDecayPerTick;

                if (currentShield <= 0f)
                {
                    currentShield = 0f;
                    shieldActive = false;
                }

                nextDecayTime = Time.time + ShieldDecayTickInterval;
            }
        }
    }

    public override void OnPlayerHit(GameObject player, GameObject attacker, ref float damage, FavourEffectManager manager)
    {
        if (!shieldActive || currentShield <= 0f)
        {
            return;
        }

        if (damage <= 0f)
        {
            return;
        }

        float absorbed = Mathf.Min(currentShield, damage);
        currentShield -= absorbed;
        damage -= absorbed;

        if (currentShield <= 0f)
        {
            currentShield = 0f;
            shieldActive = false;
        }
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        shieldActive = false;
        currentShield = 0f;
        cardStacks = 0;
    }

    private void ActivateShield()
    {
        float baseShield = Mathf.Max(0f, ShieldHealth);
        if (baseShield <= 0f)
        {
            return;
        }

        int multiplier = Mathf.Max(1, cardStacks);
        currentShield = baseShield * multiplier;
        shieldActive = true;
        lastTriggerTime = Time.time;
        nextDecayTime = Time.time + ShieldDecayTickInterval;
    }
}
