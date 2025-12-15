using UnityEngine;
using System.Collections;

[CreateAssetMenu(fileName = "ManaShieldFavour", menuName = "Favour Effects/Mana Shield")]
public class ManaShieldFavour : FavourEffect
{
    [Header("Mana Shield Settings")]
    [Tooltip("Fraction of player's max mana converted into shield health per stack (0.2 = 20%).")]
    public float ManaToShieldRatio = 0.2f;

    [Tooltip("Percent of max shield restored per tick.")]
    public float ShieldRegenPercentPerTick = 5f;

    [Tooltip("Seconds between shield regen ticks.")]
    public float ShieldRegenTickInterval = 0.25f;

    [Tooltip("Seconds without taking damage before shield starts regenerating.")]
    public float RegenTimer = 3f;

    private PlayerHealth playerHealth;
    private PlayerMana playerMana;
    private int stacks = 0;
    private float maxShield;
    private float currentShield;
    private float lastDamageTime;
    private float nextRegenTime;
    private bool subscribedToMana;

    public float CurrentShield => currentShield;
    public float MaxShield => maxShield;

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

        if (playerMana == null)
        {
            playerMana = player.GetComponent<PlayerMana>();
        }

        if (playerHealth == null || playerMana == null)
        {
            return;
        }

        if (!subscribedToMana)
        {
            playerMana.OnManaChanged += OnManaChanged;
            subscribedToMana = true;
        }

        if (stacks <= 0)
        {
            stacks = 1;
            lastDamageTime = Time.time;
            RecalculateMaxShield(true);
        }
        else
        {
            stacks++;
            RecalculateMaxShield(false);
        }

        nextRegenTime = Time.time + ShieldRegenTickInterval;
    }

    public override void OnUpgrade(GameObject player, FavourEffectManager manager, FavourCards sourceCard)
    {
        OnApply(player, manager, sourceCard);
    }

    public override void OnPlayerHit(GameObject player, GameObject attacker, ref float damage, FavourEffectManager manager)
    {
        if (stacks <= 0 || maxShield <= 0f)
        {
            return;
        }

        if (damage <= 0f)
        {
            return;
        }

        lastDamageTime = Time.time;

        if (currentShield <= 0f)
        {
            return;
        }

        float absorbed = Mathf.Min(currentShield, damage);
        currentShield -= absorbed;
        damage -= absorbed;

        if (currentShield < 0f)
        {
            currentShield = 0f;
        }
    }

    public override void OnUpdate(GameObject player, FavourEffectManager manager, float deltaTime)
    {
        if (stacks <= 0 || maxShield <= 0f)
        {
            return;
        }

        if (ShieldRegenPercentPerTick <= 0f || ShieldRegenTickInterval <= 0f)
        {
            return;
        }

        if (currentShield >= maxShield)
        {
            return;
        }

        if (Time.time - lastDamageTime < RegenTimer)
        {
            return;
        }

        if (Time.time < nextRegenTime)
        {
            return;
        }

        float amount = maxShield * (ShieldRegenPercentPerTick / 100f);
        if (amount > 0f)
        {
            currentShield = Mathf.Min(maxShield, currentShield + amount);
        }

        nextRegenTime = Time.time + ShieldRegenTickInterval;
    }

    public override void OnRemove(GameObject player, FavourEffectManager manager)
    {
        stacks = 0;
        maxShield = 0f;
        currentShield = 0f;

        if (playerMana != null && subscribedToMana)
        {
            playerMana.OnManaChanged -= OnManaChanged;
        }

        subscribedToMana = false;
    }

    private void OnManaChanged(float current, float max)
    {
        if (stacks <= 0)
        {
            return;
        }

        RecalculateMaxShield(false);
    }

    private void RecalculateMaxShield(bool resetToFull)
    {
        float oldMax = maxShield;
        float oldCurrent = currentShield;
        float ratio = oldMax > 0f ? Mathf.Clamp01(oldCurrent / oldMax) : 1f;

        float manaMax = playerMana != null ? playerMana.MaxManaExact : 0f;
        if (manaMax <= 0f)
        {
            maxShield = 0f;
            currentShield = 0f;
            return;
        }

        maxShield = Mathf.Max(0f, manaMax * ManaToShieldRatio * stacks);

        if (resetToFull)
        {
            currentShield = maxShield;
        }
        else
        {
            currentShield = Mathf.Min(maxShield, maxShield * ratio);
        }
    }
}
