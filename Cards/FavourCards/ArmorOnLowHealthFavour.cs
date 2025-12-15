using UnityEngine;

[CreateAssetMenu(fileName = "ArmorOnLowHealthFavour", menuName = "Favour Effects/Armor On Low Health")] 
public class ArmorOnLowHealthFavour : FavourEffect
{
    [Header("Armor On Low Health Settings")]
    [Tooltip("Flat armor granted when health falls below the threshold.")]
    public float ArmorGain = 5f;

    [Tooltip("Duration in seconds for the temporary armor buff.")]
    public float Duration = 20f;

    [Tooltip("Health threshold fraction 0-1 at which the armor buff triggers (0.5 = 50% health).")]
    public float HealthThreshold = 0.5f;

    [Tooltip("Cooldown in seconds between armor buff activations.")]
    public float Cooldown = 60f;

    [Tooltip("Thorn stacks granted for the same duration when health falls below the threshold.")]
    public int ThornGain = 15;

    [Header("Enhanced")]
    [Tooltip("Additional armor granted when this favour is enhanced.")]
    public float BonusArmorGain = 5f;

    [Tooltip("Additional Thorn stacks granted when this favour is enhanced.")]
    public int BonusThornGain = 15;

    private PlayerHealth playerHealth;
    private PlayerStats playerStats;
    private StatusController statusController;
    private float currentArmorGain;
    private int currentThornGain;
    // Tracks the armor and Thorn granted by the currently active buff so we
    // can safely remove ONLY this favour's contribution without touching any
    // permanent armor or Thorn from other sources.
    private float activeArmorBonus;
    private int activeArmorStacks;
    private int activeThornStacks;
    private float buffEndTime = -1f;
    private float lastTriggerTime = -999f;
    private bool buffActive;

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

        if (statusController == null)
        {
            statusController = player.GetComponent<StatusController>();
        }

        if (playerHealth == null || playerStats == null)
        {
            return;
        }

        currentArmorGain = Mathf.Max(0f, ArmorGain);
        currentThornGain = Mathf.Max(0, ThornGain);
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        currentArmorGain += Mathf.Max(0f, BonusArmorGain);
        currentThornGain += Mathf.Max(0, BonusThornGain);
    }

    public override void OnUpdate(GameObject player, FavourEffectManager manager, float deltaTime)
    {
        if (playerHealth == null || playerStats == null || !playerHealth.IsAlive)
        {
            if (buffActive)
            {
                RemoveArmorBuff();
            }
            return;
        }

        float threshold = Mathf.Clamp01(HealthThreshold);
        float healthFraction = playerHealth.MaxHealth > 0f
            ? playerHealth.CurrentHealth / playerHealth.MaxHealth
            : 0f;

        if (!buffActive)
        {
            if (healthFraction <= threshold && Time.time >= lastTriggerTime + Cooldown)
            {
                ActivateArmorBuff();
            }
        }
        else
        {
            if (Time.time >= buffEndTime)
            {
                RemoveArmorBuff();
            }
        }
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        if (buffActive)
        {
            RemoveArmorBuff();
        }
    }

    private void ActivateArmorBuff()
    {
        if (playerStats == null)
        {
            return;
        }

        float armorGain = Mathf.Max(0f, currentArmorGain);
        int thornStacks = Mathf.Max(0, currentThornGain);

        // If neither armor nor Thorn would be granted, skip activating the buff.
        if (armorGain <= 0f && thornStacks <= 0)
        {
            return;
        }

        if (armorGain > 0f)
        {
            playerStats.armor += armorGain;
            activeArmorBonus = armorGain;

            if (statusController != null)
            {
                int armorStacks = Mathf.RoundToInt(armorGain);
                if (armorStacks > 0)
                {
                    statusController.AddStatus(StatusId.Armor, armorStacks, -1f);
                    activeArmorStacks = armorStacks;
                }
                else
                {
                    activeArmorStacks = 0;
                }
            }
            else
            {
                activeArmorStacks = 0;
            }
        }
        else
        {
            activeArmorBonus = 0f;
            activeArmorStacks = 0;
        }

        // Apply temporary Thorn stacks via StatusController for the same
        // duration as the armor buff, if available.
        if (thornStacks > 0 && statusController != null)
        {
            statusController.AddStatus(StatusId.Thorn, thornStacks, -1f);
            activeThornStacks = thornStacks;
        }
        else
        {
            activeThornStacks = 0;
        }

        buffActive = true;
        buffEndTime = Time.time + Mathf.Max(0f, Duration);
        lastTriggerTime = Time.time;
    }

    private int GetCurrentThornStacks()
    {
        return Mathf.Max(0, currentThornGain);
    }

    private void RemoveArmorBuff()
    {
        if (playerStats == null)
        {
            buffActive = false;
            return;
        }

        // Remove only the armor that this favour added for the active buff,
        // leaving any permanent armor from other favours or sources intact.
        float gain = Mathf.Max(0f, activeArmorBonus);
        if (gain > 0f)
        {
            playerStats.armor = Mathf.Max(0f, playerStats.armor - gain);
            activeArmorBonus = 0f;
        }

        if (statusController != null && activeArmorStacks > 0)
        {
            statusController.ConsumeStacks(StatusId.Armor, activeArmorStacks);
            activeArmorStacks = 0;
        }

        // Similarly, remove only the Thorn stacks granted by this buff using
        // ConsumeStacks so that any existing permanent Thorn stacks from
        // other favours remain.
        if (statusController != null && activeThornStacks > 0)
        {
            statusController.ConsumeStacks(StatusId.Thorn, activeThornStacks);
            activeThornStacks = 0;
        }

        buffActive = false;
    }
}
