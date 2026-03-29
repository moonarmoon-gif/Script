using UnityEngine;

[RequireComponent(typeof(ThunderBird))]
public class ThunderBirdV3 : MonoBehaviour
{
    [Header("Enhanced Variant 3 - Static Chance")]
    [Tooltip("Additional static chance (0-100%) granted by Enhanced Variant 3. This is ADDED on top of any existing static chance.")]
    [Range(0f, 100f)]
    [SerializeField] private float increasedStaticChance = 25f;

    [Header("Enhanced Variant 3 - Base Cooldown")]
    [Tooltip("Base cooldown for Enhanced Variant 3 (seconds). If 0, falls back to ProjectileCards.runtimeSpawnInterval or script cooldown.")]
    public float Variant3BaseCooldown = 0f;

    private bool isActive;

    public void Configure(bool active)
    {
        isActive = active;
    }

    public bool IsActive => isActive;

    public float GetStaticChanceIncrease()
    {
        return isActive ? increasedStaticChance : 0f;
    }

    public bool TryGetBaseCooldownOverride(out float baseCooldownOverride)
    {
        baseCooldownOverride = Variant3BaseCooldown;
        return isActive && Variant3BaseCooldown > 0f;
    }
}
