using UnityEngine;

/// <summary>
/// Central helper for applying player projectile damage with PlayerStats and
/// Favour effects. This ensures all projectiles share the same damage
/// pipeline: base damage -> PlayerStats.CalculateDamage ->
/// FavourEffectManager.NotifyBeforeDealDamage.
/// </summary>
public static class PlayerDamageHelper
{
    /// <summary>
    /// Compute final damage for a player-owned projectile hit.
    /// </summary>
    /// <param name="stats">PlayerStats component for the owning player (optional).</param>
    /// <param name="enemy">Enemy GameObject being hit (optional; used for Favour checks).</param>
    /// <param name="baseDamage">Damage value before PlayerStats and favour effects.</param>
    /// <returns>Final damage after stats and favour modifications.</returns>
    public static float ComputeProjectileDamage(PlayerStats stats, GameObject enemy, float baseDamage)
    {
        return ComputeProjectileDamage(stats, enemy, baseDamage, null);
    }

    public static float ComputeProjectileDamage(PlayerStats stats, GameObject enemy, float baseDamage, GameObject projectile)
    {
        return ComputeProjectileDamageInternal(stats, enemy, baseDamage, projectile, false);
    }

    /// <summary>
    /// Variant of ComputeProjectileDamage for continuous / duration-based
    /// projectiles (ElementalBeam, Thunderbird, NovaStar, DwarfStar).
    /// These should treat WEAK as consuming at most one stack per second
    /// while still applying damage reduction to all ticks in that window.
    /// </summary>
    public static float ComputeContinuousProjectileDamage(PlayerStats stats, GameObject enemy, float baseDamage, GameObject projectile)
    {
        return ComputeProjectileDamageInternal(stats, enemy, baseDamage, projectile, true);
    }

    private static float ComputeProjectileDamageInternal(PlayerStats stats, GameObject enemy, float baseDamage, GameObject projectile, bool isContinuousProjectile)
    {
        if (baseDamage <= 0f)
        {
            return baseDamage;
        }

        float damage = baseDamage;

        ProjectileCards card = null;
        if (projectile != null && ProjectileCardModifiers.Instance != null)
        {
            card = ProjectileCardModifiers.Instance.GetCardFromProjectile(projectile);
        }

        // Allow per-projectile Attack scaling to modify the pre-stats base damage
        // before the standard PlayerStats.CalculateDamage pipeline runs.
        if (stats != null)
        {
            float attackBonus = ProjectileAttackDamageScalingManager.GetAttackBonus(stats, card, projectile);
            if (attackBonus > 0f)
            {
                damage += attackBonus;
            }
        }

        // Apply core PlayerStats damage pipeline (flat damage, multipliers, crit, etc.).
        if (stats != null)
        {
            damage = stats.CalculateDamage(damage, true); // true = isProjectile
        }

        // Apply favour-based outgoing damage modifiers, which may depend on
        // the specific enemy (health threshold, distance, boss tag, etc.).
        if (stats != null && enemy != null)
        {
            FavourEffectManager favourManager = stats.GetComponent<FavourEffectManager>();
            if (favourManager != null)
            {
                if (card != null)
                {
                    EnemyLastHitSource marker = enemy.GetComponent<EnemyLastHitSource>();
                    if (marker == null)
                    {
                        marker = enemy.AddComponent<EnemyLastHitSource>();
                    }
                    marker.lastProjectileCard = card;
                }

                favourManager.CurrentProjectileCard = card;
                favourManager.NotifyBeforeDealDamage(enemy, ref damage);
                favourManager.CurrentProjectileCard = null;
            }
        }

        if (stats != null)
        {
            StatusController statusController = stats.GetComponent<StatusController>();
            if (statusController != null)
            {
                bool isStatusTick = StatusDamageScope.IsStatusTick;
                // Use target-aware outgoing modifiers so FIRST STRIKE and
                // HATRED can reason about the specific enemy being hit.
                statusController.ModifyOutgoingDamageAgainstTarget(ref damage, isStatusTick, enemy);
                statusController.ApplyWeakOutgoing(ref damage, isStatusTick, isContinuousProjectile);
            }
        }

        ApplyElementalVulnerabilities(enemy, projectile, ref damage);

        return damage;
    }

    public static float ComputeAttackDamage(PlayerStats stats, GameObject enemy, float burstDamagePercent)
    {
        if (stats == null)
        {
            return 0f;
        }

        float percent = Mathf.Max(0f, burstDamagePercent);
        if (percent <= 0f)
        {
            return 0f;
        }

        float effectiveAttack = ProjectileAttackDamageScalingManager.GetEffectiveAttack(stats);
        if (effectiveAttack <= 0f)
        {
            return 0f;
        }

        float baseDamage = effectiveAttack * (percent / 100f);
        if (baseDamage <= 0f)
        {
            return 0f;
        }

        float damage = stats.CalculateDamage(baseDamage, false);

        if (enemy != null)
        {
            FavourEffectManager favourManager = stats.GetComponent<FavourEffectManager>();
            if (favourManager != null)
            {
                favourManager.NotifyBeforeDealDamage(enemy, ref damage);
            }
        }

        StatusController statusController = stats.GetComponent<StatusController>();
        if (statusController != null)
        {
            bool isStatusTick = StatusDamageScope.IsStatusTick;
            statusController.ModifyOutgoingDamageAgainstTarget(ref damage, isStatusTick, enemy);
            statusController.ApplyWeakOutgoing(ref damage, isStatusTick, false);
        }

        return damage;
    }

    private static void ApplyElementalVulnerabilities(GameObject enemy, GameObject projectile, ref float damage)
    {
        if (enemy == null || projectile == null || damage <= 0f)
        {
            return;
        }

        if (StatusControllerManager.Instance == null)
        {
            return;
        }

        ProjectileType element;
        if (!TryGetProjectileElement(projectile, out element))
        {
            return;
        }

        float multiplier = 1f;

        switch (element)
        {
            case ProjectileType.Fire:
            case ProjectileType.NovaStar:
                {
                    StatusController status = enemy.GetComponent<StatusController>();
                    int stacks = status != null ? status.GetStacks(StatusId.Scorched) : 0;
                    if (stacks > 0)
                    {
                        float per = StatusControllerManager.Instance.ScorchedFireDamageTakenPercentPerStack;
                        float total = Mathf.Max(0f, per * stacks);
                        multiplier *= 1f + total / 100f;
                    }

                    // Apply DemonSlime-specific fire resistance (or vulnerability).
                    DemonSlimeEnemy slime = enemy.GetComponent<DemonSlimeEnemy>();
                    if (slime != null)
                    {
                        float fireResist = slime.FireResistance;
                        // FireResistance is a percentage. Positive values reduce
                        // damage, negative values increase it. We only clamp at
                        // 0 so damage can never become negative.
                        float factor = 1f - (fireResist / 100f);
                        if (factor < 0f)
                        {
                            factor = 0f;
                        }
                        multiplier *= factor;
                    }
                }
                break;
            case ProjectileType.Ice:
            case ProjectileType.DwarfStar:
                {
                    StatusController status = enemy.GetComponent<StatusController>();
                    int stacks = status != null ? status.GetStacks(StatusId.Frostbite) : 0;
                    if (stacks > 0)
                    {
                        float per = StatusControllerManager.Instance.FrostbiteIceDamageTakenPercentPerStack;
                        float total = Mathf.Max(0f, per * stacks);
                        multiplier *= 1f + total / 100f;
                    }
                }
                break;
            case ProjectileType.Thunder:
                {
                    StatusController status = enemy.GetComponent<StatusController>();
                    int stacks = status != null ? status.GetStacks(StatusId.Shocked) : 0;
                    if (stacks > 0)
                    {
                        float per = StatusControllerManager.Instance.ShockedLightningDamageTakenPercentPerStack;
                        float total = Mathf.Max(0f, per * stacks);
                        multiplier *= 1f + total / 100f;
                    }
                }
                break;
        }

        if (!Mathf.Approximately(multiplier, 1f))
        {
            damage *= multiplier;
        }
    }

    private static bool TryGetProjectileElement(GameObject projectile, out ProjectileType element)
    {
        PlayerProjectiles pp = projectile.GetComponent<PlayerProjectiles>();
        if (pp != null)
        {
            element = pp.ProjectileElement;
            return true;
        }

        ClawProjectile claw = projectile.GetComponent<ClawProjectile>();
        if (claw != null)
        {
            element = claw.ProjectileElement;
            return true;
        }

        ElementalBeam beam = projectile.GetComponent<ElementalBeam>();
        if (beam != null)
        {
            // ElementalBeam exposes its elemental type via a serialized
            // projectileType field.
            var beamTypeField = typeof(ElementalBeam).GetField("projectileType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (beamTypeField != null)
            {
                element = (ProjectileType)beamTypeField.GetValue(beam);
                return true;
            }
        }

        NovaStar nova = projectile.GetComponent<NovaStar>();
        if (nova != null)
        {
            element = nova.starType;
            return true;
        }

        DwarfStar dwarf = projectile.GetComponent<DwarfStar>();
        if (dwarf != null)
        {
            element = dwarf.starType;
            return true;
        }

        NuclearStrike nuke = projectile.GetComponent<NuclearStrike>();
        if (nuke != null)
        {
            element = ProjectileType.Nuclear;
            return true;
        }

        ProjectileFireTalon fireTalon = projectile.GetComponent<ProjectileFireTalon>();
        if (fireTalon != null)
        {
            element = ProjectileType.Fire;
            return true;
        }

        ProjectileIceTalon iceTalon = projectile.GetComponent<ProjectileIceTalon>();
        if (iceTalon != null)
        {
            element = ProjectileType.Ice;
            return true;
        }

        element = default;
        return false;
    }
}
