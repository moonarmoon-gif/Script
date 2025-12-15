using UnityEngine;

/// <summary>
/// Trait cards - Special abilities and passive effects
/// </summary>
[CreateAssetMenu(fileName = "New Trait Card", menuName = "Cards/Trait Card")]
public class TraitCards : BaseCard
{
    [Header("Trait Type")]
    public TraitType traitType;

    [Header("Trait Values")]
    [Tooltip("Primary value for this trait (usage depends on trait type)")]
    public float primaryValue = 10f;

    [Tooltip("Secondary value for this trait (usage depends on trait type)")]
    public float secondaryValue = 5f;

    [Header("Duration Settings")]
    [Tooltip("Duration in seconds (0 = permanent)")]
    public float duration = 0f;

    public enum TraitType
    {
        Vampirism,          // Lifesteal on hit
        Thorns,             // Reflect damage
        SpeedBoost,         // Movement speed increase
        DoubleJump,         // Gain double jump ability
        DashAbility,        // Gain dash ability
        ShieldOnHit,        // Gain temporary shield when hit
        DamageOverTime,     // Enemies take DOT
        ChainLightning,     // Attacks can chain to nearby enemies
        Regeneration,       // Passive health regeneration
        ManaShield,         // Convert mana to shield
        LuckyStrikes,       // Chance for double damage
        Berserker,          // More damage at low health
        GlassCannon,        // High damage, low health
        Tank,               // High health, low damage
        Evasion             // Chance to dodge attacks
    }

    public override void ApplyEffect(GameObject player)
    {
        float multiplier = GetRarityMultiplier();
        float scaledPrimary = primaryValue * multiplier;
        float scaledSecondary = secondaryValue * multiplier;

        PlayerStats stats = player.GetComponent<PlayerStats>();
        if (stats == null)
        {
            Debug.LogWarning("PlayerStats component not found! Creating one...");
            stats = player.AddComponent<PlayerStats>();
        }
    }

    public override string GetFormattedDescription()
    {
        float multiplier = GetRarityMultiplier();
        float scaledPrimary = primaryValue * multiplier;
        float scaledSecondary = secondaryValue * multiplier;

        switch (traitType)
        {
            case TraitType.Vampirism:
                return $"Heal for {scaledPrimary}% of damage dealt";
            case TraitType.Thorns:
                return $"Reflect {scaledPrimary}% of damage taken";
            case TraitType.SpeedBoost:
                return $"Increase movement speed by {scaledPrimary}%";
            case TraitType.DoubleJump:
                return "Gain the ability to double jump";
            case TraitType.DashAbility:
                return $"Gain dash ability with {Mathf.Max(1f, 5f - scaledPrimary)}s cooldown";
            case TraitType.ShieldOnHit:
                return $"Gain {scaledPrimary} shield for {scaledSecondary}s when hit";
            case TraitType.DamageOverTime:
                return $"Enemies take {scaledPrimary} damage/s for {scaledSecondary}s";
            case TraitType.ChainLightning:
                return $"Attacks chain to {(int)scaledPrimary} nearby enemies";
            case TraitType.Regeneration:
                return $"Regenerate {scaledPrimary} HP per second";
            case TraitType.ManaShield:
                return $"{scaledPrimary}% of damage taken from mana instead";
            case TraitType.LuckyStrikes:
                return $"{scaledPrimary}% chance to deal {scaledSecondary}x damage";
            case TraitType.Berserker:
                return $"+{scaledPrimary}% damage when below 30% health";
            case TraitType.GlassCannon:
                return $"+{scaledPrimary}% damage, -50% max health";
            case TraitType.Tank:
                return $"+{scaledPrimary}% max health, -{scaledSecondary}% damage";
            case TraitType.Evasion:
                return $"{scaledPrimary}% chance to dodge attacks";
            default:
                return description;
        }
    }
}