using UnityEngine;

/// <summary>
/// Applies modifiers from PlayerStats to spawned projectiles
/// Attach this to the player or projectile spawner
/// </summary>
public class ProjectileModifierApplier : MonoBehaviour
{
    private PlayerStats playerStats;
    
    private void Awake()
    {
        playerStats = GetComponent<PlayerStats>();
    }
    
    /// <summary>
    /// Apply all active modifiers to a projectile
    /// Call this right after spawning a projectile
    /// </summary>
    public void ApplyModifiersToProjectile(GameObject projectile, ProjectileCards card)
    {
        if (projectile == null || card == null) return;
        
        // Get per-card modifiers
        CardModifierStats modifiers = ProjectileCardModifiers.Instance.GetCardModifiers(card);

        ProjectileCardModifiers.ApplyStatusChanceModifiersToProjectile(projectile, modifiers);
        
        // Check if this is an ElementalBeam (doesn't use piercing)
        ElementalBeam beam = projectile.GetComponent<ElementalBeam>();
        bool isBeam = beam != null;

        // Check if this is a Talon projectile (handles pierce internally in Launch)
        ProjectileFireTalon fireTalon = projectile.GetComponent<ProjectileFireTalon>();
        ProjectileIceTalon iceTalon = projectile.GetComponent<ProjectileIceTalon>();
        bool isTalon = fireTalon != null || iceTalon != null;
        
        // Apply Piercing from card modifiers (skip for ElementalBeam and Talons)
        if (!isBeam && !isTalon && modifiers.pierceCount > 0)
        {
            ProjectilePiercing piercing = projectile.GetComponent<ProjectilePiercing>();
            if (piercing == null)
            {
                piercing = projectile.AddComponent<ProjectilePiercing>();
                piercing.pierceCount = modifiers.pierceCount;
                Debug.Log($"<color=cyan>Added ProjectilePiercing to {projectile.name} with pierce count: {modifiers.pierceCount} (from {card.cardName})</color>");
            }
            else
            {
                // CRITICAL FIX: ADD modifier to existing pierce count, don't replace it!
                int existingPierceCount = piercing.pierceCount;
                piercing.pierceCount += modifiers.pierceCount;
                Debug.Log($"<color=cyan>{projectile.name} already has ProjectilePiercing: {existingPierceCount} + {modifiers.pierceCount} = {piercing.pierceCount} (from {card.cardName})</color>");
            }
        }
        else if (isBeam)
        {
            Debug.Log($"<color=cyan>{projectile.name} is ElementalBeam, skipping piercing (damages all in area)</color>");
        }
        else if (isTalon)
        {
            // Talon projectiles compute final pierce (including modifiers) inside their own Launch methods.
            Debug.Log($"<color=cyan>{projectile.name} is Talon, skipping generic pierce application (handled in Launch)</color>");
        }
        else
        {
            Debug.Log($"<color=yellow>{card.cardName} has no pierce modifiers for {projectile.name}</color>");
        }
        
        // Note: Speed, Size, Damage, Lifetime multipliers are handled by the projectile scripts themselves
        // They read from the per-card modifiers in ProjectileCardModifiers
    }
}
