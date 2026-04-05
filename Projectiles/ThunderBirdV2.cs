using UnityEngine;

[RequireComponent(typeof(ThunderBird))]
public class ThunderBirdV2 : MonoBehaviour
{
    [Header("Enhanced Variant 2 - Global Strike")]
    [Tooltip("Base cooldown for Enhanced Variant 2 (seconds). If 0, falls back to ProjectileCards.runtimeSpawnInterval or script cooldown.")]
    [SerializeField] private float variant2BaseCooldown = 0f;

    [Range(0f, 100f)]
    [SerializeField] private float globalStrikeChance = 10f;

    private bool isActive;

    public void Configure(bool active)
    {
        isActive = active;
    }

    public bool IsActive => isActive;

    public float GlobalStrikeChancePercent => globalStrikeChance;

    public bool TryGetBaseCooldownOverride(out float baseCooldownOverride)
    {
        baseCooldownOverride = variant2BaseCooldown;
        return isActive && variant2BaseCooldown > 0f;
    }
}
