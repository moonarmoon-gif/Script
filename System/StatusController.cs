using System.Collections.Generic;
using UnityEngine;

public enum StatusId
{
    Vulnerable,
    Defense,
    Poison,
    Acceleration,
    Thorn,
    Curse,
    Absorption,
    Reflect,
    Nullify,
    Immune,
    Decay,
    FirstStrike,
    Slow,
    Frostbite,
    Freeze,
    Burn,
    Scorched,
    Immolation,
    Static,
    StaticReapply,
    Shocked,
    Bleed,
    Wound,
    Condemn,
    DeathMark,
    Amnesia,
    Shield,
    ShieldStrength,
    Shatter,
    Armor,
    Revival,
    Absolution,
    Blessing,
    Execute,
    Lethargy,
    Haste,
    Burden,
    Hatred,
    Focus,
    Fury,
    Weak,
    Rage,
    Overweight,
    Frenzy,
    Corruption,
}

public struct IncomingDamageContext
{
    public bool isStatusTick;
    public bool isAoeDamage;

    // True when the source of this damage is a player projectile. This is
    // used by certain statuses (e.g., NULLIFY) that should only react to
    // projectile hits and ignore melee or other sources.
    public bool isPlayerProjectile;

    // Set to true by StatusController.ModifyIncomingDamage when WOUND adds
    // bonus damage to this hit. Callers can use this to choose Wound damage
    // colors for the final combined damage number.
    public bool wasWoundApplied;
}

public static class StatusDamageScope
{
    public static bool IsStatusTick { get; private set; }

    public static DamageNumberManager.DamageType CurrentDamageNumberType { get; private set; }

    public static bool CurrentIsBurn { get; private set; }

    public static float LastResolvedDamage;

    public static void BeginStatusTick()
    {
        IsStatusTick = true;
        LastResolvedDamage = 0f;
    }

    public static void EndStatusTick()
    {
        IsStatusTick = false;
        CurrentIsBurn = false;
    }

    public static void BeginStatusTick(DamageNumberManager.DamageType damageType, bool isBurn)
    {
        IsStatusTick = true;
        CurrentDamageNumberType = damageType;
        CurrentIsBurn = isBurn;
        LastResolvedDamage = 0f;
    }
}

public static class DamageAoeScope
{
    public static bool IsAoeDamage { get; private set; }

    public static void BeginAoeDamage() => IsAoeDamage = true;
    public static void EndAoeDamage() => IsAoeDamage = false;
}

public static class EnemyDamagePopupScope
{
    private static int suppressCount;

    public static bool SuppressPopups => suppressCount > 0;

    public static void BeginSuppressPopups()
    {
        suppressCount++;
    }

    public static void EndSuppressPopups()
    {
        suppressCount = Mathf.Max(0, suppressCount - 1);
    }
}

public static class OffscreenDamageBypassScope
{
    private static int bypassCount;

    public static bool AllowOffscreenDamage => bypassCount > 0;

    public static void BeginAllowOffscreenDamage()
    {
        bypassCount++;
    }

    public static void EndAllowOffscreenDamage()
    {
        bypassCount = Mathf.Max(0, bypassCount - 1);
    }
}

[System.Serializable]
public class ActiveStatus
{
    public StatusId id;
    public int stacks;
    public float remainingDuration; // -1 = permanent
    public float tickTimer;
    public float baseDamagePerTick;
    public int sourceKey;
    public ProjectileCards sourceCard;
}

public class StatusController : MonoBehaviour
{
    [SerializeField]
    private List<ActiveStatus> activeStatuses = new List<ActiveStatus>();

    // -------------------------------
    // NEW: Persistent shield pool
    // -------------------------------
    [Header("Shield (persistent pool)")]
    [SerializeField, Tooltip("Current shield amount. Absorbs incoming damage before health. Persists until broken.")]
    private float shieldAmount = 0f;

    /// <summary>Adds persistent shield amount (clamped to >= 0).</summary>
    public void AddShield(float amount)
    {
        if (amount <= 0f) return;
        shieldAmount += amount;
    }

    /// <summary>Returns current shield amount.</summary>
    public float GetShieldAmount()
    {
        return shieldAmount;
    }

    /// <summary>Clears all shield immediately.</summary>
    public void ClearShield()
    {
        shieldAmount = 0f;
    }

    /// <summary>
    /// Consume shield against incoming damage. Returns remaining damage after shield absorption.
    /// </summary>
    public float ApplyShield(float damage)
    {
        if (damage <= 0f) return damage;
        if (shieldAmount <= 0f) return damage;

        float absorbed = Mathf.Min(shieldAmount, damage);
        shieldAmount -= absorbed;
        damage -= absorbed;

        return damage;
    }

    /// <summary>
    /// Call this near the end of the damage pipeline (after ModifyIncomingDamage + Absorption),
    /// right before HP is actually reduced, so shield behaves like a true pre-health buffer.
    ///
    /// IMPORTANT: This is NOT called automatically by StatusController; you must call it from
    /// EnemyHealth.TakeDamage (and PlayerHealth if you want the player to have the same shield mechanic).
    /// </summary>
    public void ApplyFinalIncomingDamageMitigation(ref float damage, bool isStatusTick)
    {
        if (damage <= 0f) return;

        // Shield absorbs first. (Persists until broken; no decay.)
        float before = damage;
        float after = ApplyShield(damage);

        // If any amount of damage was actually absorbed by shield, show a
        // shield-colored damage number for enemies so players can see shield
        // depletion separately from HP loss.
        if (before > after)
        {
            float absorbed = before - after;
            if (absorbed > 0f && !StatusDamageScope.IsStatusTick && DamageNumberManager.Instance != null && !EnemyDamagePopupScope.SuppressPopups)
            {
                EnemyHealth enemyHealth = cachedEnemyHealth;
                if (enemyHealth != null && enemyHealth.IsAlive)
                {
                    Vector3 anchor = DamageNumberManager.Instance.GetAnchorWorldPosition(enemyHealth.gameObject, enemyHealth.transform.position);
                    DamageNumberManager.Instance.ShowDamage(absorbed, anchor, DamageNumberManager.DamageType.Shield);
                }
            }
        }

        damage = after;

        if (damage <= 0f)
        {
            return;
        }

        // Keep place for any future final-pass mitigation.
    }

    // Tracks the end time of the current WEAK "continuous" window for this unit.
    private float weakContinuousWindowEndTime = -1f;

    // Tracks which debuff types have already granted HATRED stacks for this unit.
    private HashSet<StatusId> hatredDebuffSources;
    private bool hasOffCameraSpeedBoost;
    private float offCameraSpeedBoostMultiplier = 1f;
    private float offCameraSpeedBoostEndTime = 0f;
    private float offCameraSpeedBoostViewportOffset = -1f;

    private EnemyHealth cachedEnemyHealth;

    private bool freezeDeathEffectPlayed;

    private float burnTickTimer;

    private void OnEnable()
    {
        cachedEnemyHealth = GetComponent<EnemyHealth>() ?? GetComponentInParent<EnemyHealth>();
        if (cachedEnemyHealth != null)
        {
            cachedEnemyHealth.OnDeath += HandleOwnerDeath;
        }
    }

    private void OnDisable()
    {
        if (cachedEnemyHealth != null)
        {
            cachedEnemyHealth.OnDeath -= HandleOwnerDeath;
        }
    }

    public void RemoveStatus(StatusId id)
    {
        for (int i = activeStatuses.Count - 1; i >= 0; i--)
        {
            if (activeStatuses[i].id == id)
            {
                activeStatuses.RemoveAt(i);
            }
        }

        // When HATRED is explicitly removed, clear the per-debuff tracking so
        // future externally granted HATRED can start fresh.
        if (id == StatusId.Hatred && hatredDebuffSources != null)
        {
            hatredDebuffSources.Clear();
        }
    }

    public float ApplyAbsorption(float damage)
    {
        if (damage <= 0f || activeStatuses.Count == 0)
        {
            return damage;
        }

        int index = activeStatuses.FindIndex(s => s.id == StatusId.Absorption);
        if (index < 0)
        {
            return damage;
        }

        ActiveStatus status = activeStatuses[index];
        if (status.stacks <= 0)
        {
            return damage;
        }

        float maxPercent = 10f;
        if (StatusControllerManager.Instance != null)
        {
            maxPercent = StatusControllerManager.Instance.AbsorptionMaxHitPercent;
        }

        float maxHealth = 0f;
        PlayerHealth ph = GetComponent<PlayerHealth>();
        if (ph != null)
        {
            maxHealth = ph.MaxHealth;
        }
        else
        {
            EnemyHealth eh = GetComponent<EnemyHealth>();
            if (eh != null)
            {
                maxHealth = eh.MaxHealth;
            }
        }

        if (maxHealth <= 0f || maxPercent <= 0f)
        {
            return damage;
        }

        float maxDamage = maxHealth * (maxPercent / 100f);
        return Mathf.Min(damage, maxDamage);
    }

    /// <summary>
    /// Estimate a conservative lower bound on remaining Poison damage this unit
    /// will take within the next <paramref name="windowSeconds"/> seconds,
    /// based on current stacks and remaining Poison duration.
    /// </summary>
    public float EstimateRemainingPoisonDamageWithinWindow(float windowSeconds)
    {
        if (windowSeconds <= 0f || activeStatuses.Count == 0)
        {
            return 0f;
        }

        if (StatusControllerManager.Instance == null)
        {
            return 0f;
        }

        float interval = Mathf.Max(0.01f, StatusControllerManager.Instance.PoisonTickInterval);
        float damagePerStack = Mathf.Max(0f, StatusControllerManager.Instance.PoisonDamagePerStack);
        if (damagePerStack <= 0f)
        {
            return 0f;
        }

        float window = Mathf.Max(0f, windowSeconds);
        float totalDamage = 0f;

        for (int i = 0; i < activeStatuses.Count; i++)
        {
            ActiveStatus status = activeStatuses[i];
            if (status.id != StatusId.Poison || status.stacks <= 0)
            {
                continue;
            }

            float effectiveWindow = window;
            if (status.remainingDuration >= 0f)
            {
                effectiveWindow = Mathf.Min(effectiveWindow, Mathf.Max(0f, status.remainingDuration));
            }

            if (effectiveWindow <= 0f)
            {
                continue;
            }

            float tickTimer = status.tickTimer;
            if (tickTimer < 0f)
            {
                tickTimer = 0f;
            }
            if (tickTimer > interval)
            {
                tickTimer = interval;
            }

            float timeUntilNextTick = interval - tickTimer;
            int ticks = 0;
            if (effectiveWindow >= timeUntilNextTick)
            {
                float remainingAfterFirst = effectiveWindow - timeUntilNextTick;
                ticks = 1 + Mathf.FloorToInt(remainingAfterFirst / interval);
            }
            if (ticks <= 0)
            {
                continue;
            }

            float perTick = Mathf.Max(0f, status.stacks * damagePerStack);
            if (perTick <= 0f)
            {
                continue;
            }

            totalDamage += perTick * ticks;
        }

        return totalDamage;
    }

    private static bool UsesIndependentDurationInstances(StatusId id, float resolvedDuration)
    {
        if (resolvedDuration < 0f)
        {
            return false;
        }

        switch (id)
        {
            case StatusId.Poison:
            case StatusId.Bleed:
            case StatusId.Blessing:
            case StatusId.Wound:
            case StatusId.Acceleration:
            case StatusId.Burn:
            case StatusId.Slow:
            case StatusId.Static:
            case StatusId.Frenzy:
                return true;
            default:
                return false;
        }
    }

    public int GetStacks(StatusId id)
    {
        int total = 0;
        for (int i = 0; i < activeStatuses.Count; i++)
        {
            ActiveStatus status = activeStatuses[i];
            if (status.id != id)
            {
                continue;
            }

            if (status.stacks > 0)
            {
                total += status.stacks;
            }
        }
        return total;
    }

    public int GetStacks(StatusId id, int sourceKey)
    {
        if (sourceKey == 0)
        {
            return GetStacks(id);
        }

        int total = 0;
        for (int i = 0; i < activeStatuses.Count; i++)
        {
            ActiveStatus status = activeStatuses[i];
            if (status.id != id || status.sourceKey != sourceKey)
            {
                continue;
            }

            if (status.stacks > 0)
            {
                total += status.stacks;
            }
        }
        return total;
    }

    public bool HasStatus(StatusId id) => GetStacks(id) > 0;

    public bool ConsumeStacks(StatusId id, int count)
    {
        if (count <= 0 || activeStatuses.Count == 0)
        {
            return false;
        }

        int remaining = count;
        bool consumedAny = false;

        for (int i = activeStatuses.Count - 1; i >= 0; i--)
        {
            if (remaining <= 0)
            {
                break;
            }

            ActiveStatus status = activeStatuses[i];
            if (status.id != id || status.stacks <= 0)
            {
                continue;
            }

            int consume = Mathf.Min(status.stacks, remaining);
            status.stacks -= consume;
            remaining -= consume;
            consumedAny = consumedAny || consume > 0;

            if (status.stacks <= 0)
            {
                if (status.remainingDuration <= 0f)
                {
                    activeStatuses.RemoveAt(i);
                }
                else
                {
                    status.stacks = 0;
                    activeStatuses[i] = status;
                }
            }
            else
            {
                activeStatuses[i] = status;
            }
        }

        return consumedAny;
    }

    public bool ConsumeStacks(StatusId id, int count, int sourceKey)
    {
        if (sourceKey == 0)
        {
            return ConsumeStacks(id, count);
        }

        if (count <= 0 || activeStatuses.Count == 0)
        {
            return false;
        }

        int remaining = count;
        bool consumedAny = false;

        for (int i = activeStatuses.Count - 1; i >= 0; i--)
        {
            if (remaining <= 0)
            {
                break;
            }

            ActiveStatus status = activeStatuses[i];
            if (status.id != id || status.stacks <= 0 || status.sourceKey != sourceKey)
            {
                continue;
            }

            int consume = Mathf.Min(status.stacks, remaining);
            status.stacks -= consume;
            remaining -= consume;
            consumedAny = consumedAny || consume > 0;

            if (status.stacks <= 0)
            {
                if (status.remainingDuration <= 0f)
                {
                    activeStatuses.RemoveAt(i);
                }
                else
                {
                    activeStatuses[i] = status;
                }
            }
            else
            {
                activeStatuses[i] = status;
            }
        }

        return consumedAny;
    }

    public void ApplyOffCameraSpeedBoost(float multiplier, float durationSeconds)
    {
        ApplyOffCameraSpeedBoost(multiplier, durationSeconds, -1f);
    }

    public void ApplyOffCameraSpeedBoost(float multiplier, float durationSeconds, float viewportOffset)
    {
        if (multiplier <= 1f || durationSeconds <= 0f)
        {
            return;
        }

        hasOffCameraSpeedBoost = true;
        offCameraSpeedBoostMultiplier = multiplier;
        offCameraSpeedBoostEndTime = GameStateManager.PauseSafeTime + durationSeconds;
        offCameraSpeedBoostViewportOffset = viewportOffset;
    }

    public float GetEnemyMoveSpeedMultiplier()
    {
        // NEW: Freeze = 100% move speed reduction (enemy speed becomes 0).
        // This works with enemies like CrowEnemy that multiply their base moveSpeed by this multiplier.
        if (HasStatus(StatusId.Freeze))
        {
            return 0f;
        }

        if (StatusControllerManager.Instance == null)
        {
            return 1f;
        }

        int hasteStacks = GetStacks(StatusId.Haste);
        int burdenStacks = GetStacks(StatusId.Burden);

        float multiplier = 1f;

        if (hasteStacks > 0)
        {
            float per = StatusControllerManager.Instance.EnemyHasteMoveSpeedPercentPerStack;
            float total = Mathf.Max(0f, per * hasteStacks);
            multiplier *= 1f + total / 100f;
        }

        if (burdenStacks > 0)
        {
            float per = StatusControllerManager.Instance.EnemyBurdenMoveSpeedPercentPerStack;
            float total = Mathf.Max(0f, per * burdenStacks);
            multiplier *= Mathf.Max(0f, 1f - total / 100f);
        }

        int slowStacks = GetStacks(StatusId.Slow);
        if (slowStacks > 0)
        {
            float slowStrength = Mathf.Clamp01(slowStacks / 4f);
            multiplier *= Mathf.Max(0f, 1f - slowStrength);
        }

        if (hasOffCameraSpeedBoost)
        {
            bool onCamera;
            if (offCameraSpeedBoostViewportOffset >= 0f)
            {
                Camera cam = Camera.main;
                onCamera = OffscreenDamageChecker.CanTakeDamage(transform.position, cam, offCameraSpeedBoostViewportOffset);
            }
            else
            {
                onCamera = OffscreenDamageChecker.CanTakeDamage(transform.position);
            }

            bool expired = GameStateManager.PauseSafeTime >= offCameraSpeedBoostEndTime;
            if (expired || onCamera)
            {
                hasOffCameraSpeedBoost = false;
                offCameraSpeedBoostMultiplier = 1f;
                offCameraSpeedBoostViewportOffset = -1f;
            }
            else
            {
                multiplier *= offCameraSpeedBoostMultiplier;
            }
        }

        return multiplier;
    }

    public float GetEnemyEffectiveMass(float baseMass)
    {
        float mass = Mathf.Max(0.01f, baseMass);

        int overweightStacks = GetStacks(StatusId.Overweight);
        if (overweightStacks <= 0)
        {
            return mass;
        }

        float per = 1f;
        if (StatusControllerManager.Instance != null)
        {
            per = StatusControllerManager.Instance.MassPerStack;
        }

        mass += Mathf.Max(0f, per) * overweightStacks;
        return Mathf.Max(0.01f, mass);
    }

    public float GetLethargyAttackCooldownBonus()
    {
        if (StatusControllerManager.Instance == null)
        {
            return 0f;
        }

        int stacks = GetStacks(StatusId.Lethargy);
        if (stacks <= 0)
        {
            return 0f;
        }

        float per = StatusControllerManager.Instance.LethargyAttackCooldownSecondsPerStack;
        return Mathf.Max(0f, per * stacks);
    }

    private System.Collections.Generic.Dictionary<int, bool> firstStrikeConsumedByTarget;

    public void ModifyOutgoingDamage(ref float damage, bool isStatusTick)
    {
        ApplyOutgoingDamageBonuses(ref damage, isStatusTick, null);
    }

    public void ModifyOutgoingDamageAgainstTarget(ref float damage, bool isStatusTick, GameObject target)
    {
        ApplyOutgoingDamageBonuses(ref damage, isStatusTick, target);
    }

    private void ApplyOutgoingDamageBonuses(ref float damage, bool isStatusTick, GameObject target)
    {
        if (damage <= 0f || activeStatuses.Count == 0 || isStatusTick)
        {
            return;
        }

        int firstStrikeStacks = 0;
        int hatredStacks = 0;
        int focusStacks = 0;
        int furyStacks = 0;
        int rageStacks = 0;

        for (int i = 0; i < activeStatuses.Count; i++)
        {
            ActiveStatus status = activeStatuses[i];
            if (status.stacks <= 0)
            {
                continue;
            }

            switch (status.id)
            {
                case StatusId.FirstStrike:
                    firstStrikeStacks = status.stacks;
                    break;
                case StatusId.Hatred:
                    hatredStacks = status.stacks;
                    break;
                case StatusId.Focus:
                    focusStacks = status.stacks;
                    break;
                case StatusId.Fury:
                    furyStacks = status.stacks;
                    break;
                case StatusId.Rage:
                    rageStacks = status.stacks;
                    break;
            }
        }

        if (firstStrikeStacks <= 0 && hatredStacks <= 0 && focusStacks <= 0 && furyStacks <= 0 && rageStacks <= 0)
        {
            return;
        }

        float totalBonusPercent = 0f;

        if (firstStrikeStacks > 0)
        {
            bool shouldApplyFirstStrike = false;

            if (target == null)
            {
                shouldApplyFirstStrike = true;
            }
            else
            {
                int id = target.GetInstanceID();
                if (firstStrikeConsumedByTarget == null)
                {
                    firstStrikeConsumedByTarget = new System.Collections.Generic.Dictionary<int, bool>();
                }

                if (!firstStrikeConsumedByTarget.ContainsKey(id))
                {
                    shouldApplyFirstStrike = true;
                    firstStrikeConsumedByTarget[id] = true;
                }
            }

            if (shouldApplyFirstStrike)
            {
                float bonusPerStack = 10f;
                if (StatusControllerManager.Instance != null)
                {
                    bonusPerStack = StatusControllerManager.Instance.FirstStrikeBonusPercent;
                }

                totalBonusPercent += Mathf.Max(0f, bonusPerStack * firstStrikeStacks);
            }
        }

        // HATRED: only affects *enemies* and only does anything if they already
        // have HATRED stacks (e.g. granted by a Favour or other system). The
        // debuff -> HATRED stacking logic is handled in AddStatus; here we just
        // convert total stacks into a damage bonus.
        if (hatredStacks > 0 && GetComponent<PlayerHealth>() == null)
        {
            float per = 1f;
            if (StatusControllerManager.Instance != null)
            {
                per = StatusControllerManager.Instance.HatredBonusPercentPerDebuffPerStack;
            }

            float hatredBonus = Mathf.Max(0f, per * hatredStacks);
            totalBonusPercent += hatredBonus;
        }

        if (furyStacks > 0 && GetComponent<PlayerHealth>() == null)
        {
            float per = 1f;
            if (StatusControllerManager.Instance != null)
            {
                per = StatusControllerManager.Instance.FuryEnemyBaseDamageBonusPerStack;
            }

            float flatBonus = Mathf.Max(0f, per * furyStacks);
            if (flatBonus > 0f)
            {
                damage += flatBonus;
            }
        }

        float normalizedHealth = -1f;
        bool hasHealth = false;

        if (focusStacks > 0 || rageStacks > 0)
        {
            float currentHealth = 0f;
            float maxHealth = 0f;

            PlayerHealth ph = GetComponent<PlayerHealth>();
            if (ph != null)
            {
                currentHealth = ph.CurrentHealth;
                maxHealth = ph.MaxHealth;
            }
            else
            {
                EnemyHealth eh = GetComponent<EnemyHealth>();
                if (eh != null)
                {
                    currentHealth = eh.CurrentHealth;
                    maxHealth = eh.MaxHealth;
                }
            }

            if (maxHealth > 0f)
            {
                normalizedHealth = currentHealth / maxHealth;
                hasHealth = true;
            }
        }

        if (focusStacks > 0 && hasHealth && Mathf.Approximately(normalizedHealth, 1f))
        {
            float per = 10f;
            if (StatusControllerManager.Instance != null)
            {
                per = StatusControllerManager.Instance.FocusBonusPercentPerStack;
            }

            float focusBonus = Mathf.Max(0f, per * focusStacks);
            totalBonusPercent += focusBonus;
        }

        if (rageStacks > 0 && hasHealth)
        {
            float thresholdFraction = 0.5f;
            float per = 10f;
            if (StatusControllerManager.Instance != null)
            {
                thresholdFraction = StatusControllerManager.Instance.RageLowHealthThresholdPercent / 100f;
                per = StatusControllerManager.Instance.RageBonusPercentPerStack;
            }

            if (normalizedHealth <= thresholdFraction)
            {
                float rageBonus = Mathf.Max(0f, per * rageStacks);
                totalBonusPercent += rageBonus;
            }
        }

        if (totalBonusPercent > 0f)
        {
            damage *= 1f + totalBonusPercent / 100f;
        }
    }

    public void ApplyWeakOutgoing(ref float damage, bool isStatusTick, bool isContinuousProjectile)
    {
        if (damage <= 0f || isStatusTick)
        {
            return;
        }

        if (StatusControllerManager.Instance == null)
        {
            return;
        }

        float reductionPercent = StatusControllerManager.Instance.WeakDamageReductionPercent;
        if (reductionPercent <= 0f)
        {
            return;
        }

        float multiplier = Mathf.Max(0f, 1f - reductionPercent / 100f);

        if (isContinuousProjectile)
        {
            float now = GameStateManager.PauseSafeTime;

            if (weakContinuousWindowEndTime > 0f && now < weakContinuousWindowEndTime)
            {
                damage *= multiplier;
                return;
            }

            if (activeStatuses.Count == 0)
            {
                return;
            }

            int weakIndex = activeStatuses.FindIndex(s => s.id == StatusId.Weak);
            if (weakIndex < 0)
            {
                return;
            }

            ActiveStatus weakStatus = activeStatuses[weakIndex];
            if (weakStatus.stacks <= 0)
            {
                return;
            }

            weakStatus.stacks -= 1;
            if (weakStatus.stacks <= 0 && weakStatus.remainingDuration <= 0f)
            {
                activeStatuses.RemoveAt(weakIndex);
            }
            else
            {
                activeStatuses[weakIndex] = weakStatus;
            }

            weakContinuousWindowEndTime = now + 1f;
            damage *= multiplier;
            return;
        }

        if (activeStatuses.Count == 0)
        {
            return;
        }

        int index = activeStatuses.FindIndex(s => s.id == StatusId.Weak);
        if (index < 0)
        {
            return;
        }

        ActiveStatus status = activeStatuses[index];
        if (status.stacks <= 0)
        {
            return;
        }

        damage *= multiplier;
        status.stacks -= 1;
        if (status.stacks <= 0 && status.remainingDuration <= 0f)
        {
            activeStatuses.RemoveAt(index);
        }
        else
        {
            activeStatuses[index] = status;
        }
    }

    public void ModifyIncomingDamage(ref float damage, ref IncomingDamageContext ctx)
    {
        if (damage <= 0f || activeStatuses.Count == 0)
        {
            return;
        }

        bool isPlayerOwner = GetComponent<PlayerHealth>() != null;

        for (int i = activeStatuses.Count - 1; i >= 0; i--)
        {
            ActiveStatus status = activeStatuses[i];

            switch (status.id)
            {
                case StatusId.Vulnerable:
                    if (!ctx.isStatusTick && status.stacks > 0 && damage > 0f && StatusControllerManager.Instance != null)
                    {
                        float baseMul = StatusControllerManager.Instance.VulnerableDamageMultiplier;
                        float totalMul = Mathf.Max(0f, baseMul);
                        damage *= totalMul;

                        status.stacks -= 1;
                        activeStatuses[i] = status;
                        if (status.stacks <= 0 && status.remainingDuration <= 0f)
                        {
                            activeStatuses.RemoveAt(i);
                        }
                    }
                    break;

                case StatusId.Defense:
                    if (!ctx.isStatusTick && status.stacks > 0)
                    {
                        float multiplier = 0.5f;
                        if (StatusControllerManager.Instance != null)
                        {
                            multiplier = StatusControllerManager.Instance.DefenseDamageMultiplier;
                        }
                        damage *= multiplier;
                        status.stacks -= 1;
                        activeStatuses[i] = status;
                        if (status.stacks <= 0 && status.remainingDuration <= 0f)
                        {
                            activeStatuses.RemoveAt(i);
                        }
                    }
                    break;

                case StatusId.Curse:
                    if (!ctx.isStatusTick && status.stacks > 0 && damage > 0f)
                    {
                        bool hasElemental =
                            HasStatus(StatusId.Burn) ||
                            HasStatus(StatusId.Poison) ||
                            HasStatus(StatusId.Frostbite) ||
                            HasStatus(StatusId.Static) ||
                            HasStatus(StatusId.Shocked) ||
                            HasStatus(StatusId.Scorched);
                        if (hasElemental)
                        {
                            float bonusPerStack = 25f;
                            if (StatusControllerManager.Instance != null)
                            {
                                bonusPerStack = StatusControllerManager.Instance.CurseBonusElementalDamagePercentPerStack;
                            }
                            float totalBonus = Mathf.Max(0f, bonusPerStack * status.stacks);
                            damage *= 1f + totalBonus / 100f;
                        }
                    }
                    break;

                case StatusId.Nullify:
                    if (!ctx.isStatusTick && !ctx.isAoeDamage && ctx.isPlayerProjectile && status.stacks > 0)
                    {
                        damage = 0f;
                        status.stacks -= 1;
                        activeStatuses[i] = status;
                        if (status.stacks <= 0 && status.remainingDuration <= 0f)
                        {
                            activeStatuses.RemoveAt(i);
                        }
                    }
                    break;

                case StatusId.Immune:
                    damage = 0f;
                    break;

                case StatusId.Decay:
                    if (status.stacks > 0 && damage > 0f)
                    {
                        float perStack = 1f;
                        if (StatusControllerManager.Instance != null)
                        {
                            perStack = StatusControllerManager.Instance.DecayDamageReductionPercentPerStack;
                        }
                        float total = Mathf.Max(0f, perStack * status.stacks);
                        float mul = Mathf.Max(0f, 1f - total / 100f);
                        damage *= mul;
                    }
                    break;

                case StatusId.Condemn:
                    if (!ctx.isStatusTick && status.stacks > 0 && damage > 0f)
                    {
                        float perStack = 1f;
                        if (StatusControllerManager.Instance != null)
                        {
                            perStack = StatusControllerManager.Instance.CondemnDamageTakenPercentPerStack;
                        }
                        float total = Mathf.Max(0f, perStack * status.stacks);
                        damage *= 1f + total / 100f;
                    }
                    break;

                case StatusId.DeathMark:
                    if (!ctx.isStatusTick && status.stacks > 0 && damage > 0f)
                    {
                        float perStack = 1f;
                        if (StatusControllerManager.Instance != null)
                        {
                            perStack = StatusControllerManager.Instance.DeathMarkDamageTakenPercentPerStack;
                        }
                        float total = Mathf.Max(0f, perStack * status.stacks);
                        damage *= 1f + total / 100f;
                    }
                    break;

                case StatusId.Wound:
                    if (!ctx.isStatusTick && status.stacks > 0 && damage > 0f)
                    {
                        float perStack = 1f;
                        if (StatusControllerManager.Instance != null)
                        {
                            perStack = StatusControllerManager.Instance.WoundFlatDamagePerStack;
                        }
                        float bonus = Mathf.Max(0f, perStack * status.stacks);
                        if (bonus > 0f)
                        {
                            damage += bonus;
                            ctx.wasWoundApplied = true;
                        }
                    }
                    break;

                case StatusId.Armor:
                    if (status.stacks > 0 && damage > 0f)
                    {
                        float perStack = 1f;
                        if (StatusControllerManager.Instance != null)
                        {
                            perStack = StatusControllerManager.Instance.ArmorFlatReductionPerStack;
                        }
                        float reduction = Mathf.Max(0f, perStack * status.stacks);
                        damage = Mathf.Max(0f, damage - reduction);
                    }
                    break;

                case StatusId.Poison:
                case StatusId.Acceleration:
                case StatusId.Thorn:
                    break;
            }
        }
    }

    private void Update()
    {
        if (hasOffCameraSpeedBoost && GameStateManager.PauseSafeTime >= offCameraSpeedBoostEndTime)
        {
            hasOffCameraSpeedBoost = false;
            offCameraSpeedBoostMultiplier = 1f;
            offCameraSpeedBoostViewportOffset = -1f;
        }

        if (activeStatuses.Count == 0)
        {
            return;
        }

        float dt = GameStateManager.GetPauseSafeDeltaTime();
        if (dt <= 0f)
        {
            return;
        }

        IDamageable damageable = GetComponent<IDamageable>();

        float burnInterval = 0.25f;
        if (StatusControllerManager.Instance != null)
        {
            burnInterval = StatusControllerManager.Instance.BurnTickIntervalSeconds;
        }
        burnInterval = Mathf.Max(0.01f, burnInterval);

        bool canBurnTick = damageable != null && damageable.IsAlive;
        bool hasBurn = false;
        for (int i = 0; i < activeStatuses.Count; i++)
        {
            ActiveStatus s = activeStatuses[i];
            if (s.id != StatusId.Burn || s.stacks <= 0 || s.baseDamagePerTick <= 0f)
            {
                continue;
            }

            if (s.remainingDuration < 0f || s.remainingDuration > 0f)
            {
                hasBurn = true;
                break;
            }
        }

        if (!hasBurn || !canBurnTick)
        {
            burnTickTimer = 0f;
        }
        else
        {
            burnTickTimer += dt;
            while (burnTickTimer >= burnInterval)
            {
                burnTickTimer -= burnInterval;

                ProjectileCards lastSourceCard = null;
                float totalTickDamage = 0f;

                for (int j = 0; j < activeStatuses.Count; j++)
                {
                    ActiveStatus burn = activeStatuses[j];
                    if (burn.id != StatusId.Burn || burn.stacks <= 0 || burn.baseDamagePerTick <= 0f)
                    {
                        continue;
                    }

                    if (burn.remainingDuration == 0f)
                    {
                        continue;
                    }

                    totalTickDamage += Mathf.Max(0f, burn.baseDamagePerTick) * Mathf.Max(0, burn.stacks);

                    if (burn.sourceCard != null)
                    {
                        lastSourceCard = burn.sourceCard;
                    }
                }

                if (totalTickDamage <= 0f)
                {
                    continue;
                }

                if (lastSourceCard != null)
                {
                    EnemyHealth eh = cachedEnemyHealth;
                    if (eh != null)
                    {
                        EnemyLastHitSource marker = eh.GetComponent<EnemyLastHitSource>();
                        if (marker == null)
                        {
                            marker = eh.gameObject.AddComponent<EnemyLastHitSource>();
                        }
                        marker.lastProjectileCard = lastSourceCard;
                    }
                }

                DemonSlimeEnemy slime = GetComponent<DemonSlimeEnemy>() ?? GetComponentInParent<DemonSlimeEnemy>();
                if (slime != null)
                {
                    float factor = 1f - (slime.FireResistance / 100f);
                    if (factor <= 0f)
                    {
                        factor = 0.01f;
                    }
                    totalTickDamage *= factor;
                }

                bool isCrit = false;
                PlayerStats stats = Object.FindObjectOfType<PlayerStats>();
                if (stats != null && stats.burnImmolationCanCrit)
                {
                    float frenzyBonus = 0f;
                    if (StatusControllerManager.Instance != null)
                    {
                        StatusController playerStatus = stats.GetComponent<StatusController>();
                        if (playerStatus != null)
                        {
                            int frenzyStacks = playerStatus.GetStacks(StatusId.Frenzy);
                            if (frenzyStacks > 0)
                            {
                                frenzyBonus = StatusControllerManager.Instance.CritPerStack * frenzyStacks;
                            }
                        }
                    }

                    float chance = Mathf.Clamp(stats.critChance + frenzyBonus + Mathf.Max(0f, stats.burnImmolationCritChanceBonus), 0f, 100f);
                    if (chance > 0f)
                    {
                        float roll = Random.Range(0f, 100f);
                        if (roll < chance)
                        {
                            float critDamage = stats.critDamage + Mathf.Max(0f, stats.burnImmolationCritDamageBonusPercent);
                            totalTickDamage *= Mathf.Max(0f, critDamage / 100f);
                            isCrit = true;
                        }
                    }
                }

                Vector3 anchor = transform.position;
                if (DamageNumberManager.Instance != null)
                {
                    anchor = DamageNumberManager.Instance.GetAnchorWorldPosition(gameObject, anchor);
                }

                StatusDamageScope.BeginStatusTick(DamageNumberManager.DamageType.Fire, true);

                if (stats != null && stats.burnImmolationCanCrit)
                {
                    stats.lastHitWasCrit = isCrit;
                    FavourEffectManager favourManager = stats.GetComponent<FavourEffectManager>();
                    if (favourManager != null)
                    {
                        ProjectileCards previousCard = favourManager.CurrentProjectileCard;
                        favourManager.CurrentProjectileCard = lastSourceCard;
                        favourManager.NotifyCritResolved(lastSourceCard, true, isCrit);
                        favourManager.CurrentProjectileCard = previousCard;
                    }
                }

                damageable.TakeDamage(totalTickDamage, anchor, Vector3.zero);
                float resolved = StatusDamageScope.LastResolvedDamage;
                StatusDamageScope.EndStatusTick();

                if (DamageNumberManager.Instance != null && resolved > 0f && !EnemyDamagePopupScope.SuppressPopups)
                {
                    DamageNumberManager.Instance.ShowDamage(resolved, anchor, DamageNumberManager.DamageType.Fire, isCrit, true);
                }
            }
        }

        for (int i = activeStatuses.Count - 1; i >= 0; i--)
        {
            ActiveStatus status = activeStatuses[i];

            if (status.id != StatusId.Immolation && status.remainingDuration > 0f)
            {
                status.remainingDuration -= dt;
                if (status.remainingDuration < 0f)
                {
                    status.remainingDuration = 0f;
                }
            }

            if (status.id == StatusId.Poison && damageable != null && damageable.IsAlive)
            {
                status.tickTimer += dt;
                float interval = 1f;
                float damagePerStack = 1f;
                if (StatusControllerManager.Instance != null)
                {
                    interval = Mathf.Max(0.01f, StatusControllerManager.Instance.PoisonTickInterval);
                    damagePerStack = StatusControllerManager.Instance.PoisonDamagePerStack;
                }
                while (status.tickTimer >= interval)
                {
                    status.tickTimer -= interval;
                    float poisonDamage = status.stacks * damagePerStack;

                    Vector3 anchor = transform.position;
                    if (DamageNumberManager.Instance != null)
                    {
                        anchor = DamageNumberManager.Instance.GetAnchorWorldPosition(gameObject, anchor);
                    }

                    StatusDamageScope.BeginStatusTick(DamageNumberManager.DamageType.Poison, true);
                    damageable.TakeDamage(poisonDamage, anchor, Vector3.zero);
                    float resolved = StatusDamageScope.LastResolvedDamage;
                    if (DamageNumberManager.Instance != null && resolved > 0f && !EnemyDamagePopupScope.SuppressPopups)
                    {
                        DamageNumberManager.Instance.ShowDamage(resolved, anchor, DamageNumberManager.DamageType.Poison, false, true);
                    }
                    StatusDamageScope.EndStatusTick();
                }
            }

            bool hasFiniteDuration = status.remainingDuration >= 0f;
            bool expiredByDuration = hasFiniteDuration && status.remainingDuration <= 0f;
            bool noStacksAndNoDuration = status.stacks <= 0 && !hasFiniteDuration;

            if (status.id == StatusId.Immolation)
            {
                expiredByDuration = false;
            }

            if (expiredByDuration || noStacksAndNoDuration)
            {
                if (status.id == StatusId.Freeze)
                {
                    SpawnFreezeOnEndEffect();
                }

                activeStatuses.RemoveAt(i);
            }
            else
            {
                activeStatuses[i] = status;
            }
        }

        UpdateImmolationDerivedDurationAndPresence();
    }

    public float GetMaxRemainingDurationSeconds(StatusId id)
    {
        return GetMaxRemainingDuration(id);
    }

    public float GetCurrentBurnTickDamage()
    {
        float total = 0f;
        for (int i = 0; i < activeStatuses.Count; i++)
        {
            ActiveStatus s = activeStatuses[i];
            if (s.id != StatusId.Burn || s.stacks <= 0 || s.baseDamagePerTick <= 0f)
            {
                continue;
            }

            if (s.remainingDuration == 0f)
            {
                continue;
            }

            total += Mathf.Max(0f, s.baseDamagePerTick) * Mathf.Max(0, s.stacks);
        }

        return total;
    }

    public static bool TryApplyBurnFromProjectile(GameObject projectile, GameObject enemy, Vector3 hitPoint, float hitDamage, bool forceApply = false)
    {
        if (projectile == null || enemy == null)
        {
            return false;
        }

        BurnEffect burn = projectile.GetComponent<BurnEffect>();
        if (burn == null)
        {
            return false;
        }

        bool ignorePreForThisProjectile =
            projectile.GetComponent<ElementalBeam>() != null ||
            projectile.GetComponent<ProjectileFireTalon>() != null ||
            projectile.GetComponent<ProjectileIceTalon>() != null ||
            projectile.GetComponent<DwarfStar>() != null ||
            projectile.GetComponent<NovaStar>() != null;

        PredeterminedStatusRoll pre = ignorePreForThisProjectile
            ? null
            : projectile.GetComponent<PredeterminedStatusRoll>();

        if (pre != null)
        {
            pre.EnsureRolled();
        }

        PlayerStats stats = Object.FindObjectOfType<PlayerStats>();

        ProjectileCards sourceCard = null;
        if (ProjectileCardModifiers.Instance != null)
        {
            sourceCard = ProjectileCardModifiers.Instance.GetCardFromProjectile(projectile);
        }

        bool isActiveSource =
            sourceCard != null &&
            sourceCard.projectileSystem == ProjectileCards.ProjectileSystemType.Active;

        float effectiveChance = burn.burnChance;
        if (stats != null && stats.hasProjectileStatusEffect)
        {
            effectiveChance += Mathf.Max(0f, stats.statusEffectChance);
            if (isActiveSource)
            {
                effectiveChance += Mathf.Max(0f, stats.activeProjectileStatusEffectChanceBonus);
            }
        }

        ProjectileStatusChanceAdditiveBonus additiveBonus = projectile.GetComponent<ProjectileStatusChanceAdditiveBonus>();
        if (additiveBonus != null)
        {
            effectiveChance += Mathf.Max(0f, additiveBonus.burnBonusPercent);
        }
        effectiveChance = Mathf.Clamp(effectiveChance, 0f, 100f);

        if (!forceApply)
        {
            if (pre != null && pre.burnRolled)
            {
                if (!pre.burnWillApply)
                {
                    return false;
                }
            }
            else
            {
                float roll = Random.Range(0f, 100f);
                if (roll > effectiveChance)
                {
                    return false;
                }
            }
        }

        EnemyHealth ownerHealth = enemy.GetComponent<EnemyHealth>() ?? enemy.GetComponentInParent<EnemyHealth>();
        GameObject ownerGO = ownerHealth != null ? ownerHealth.gameObject : enemy;

        StatusController immuneCheck = ownerGO.GetComponent<StatusController>() ?? ownerGO.GetComponentInParent<StatusController>();
        if (immuneCheck != null && immuneCheck.HasStatus(StatusId.Immune))
        {
            return false;
        }

        float baseDamage = Mathf.Max(1f, hitDamage);
        float damagePerTick = Mathf.Max(1f, baseDamage * burn.burnDamageMultiplier);
        float duration = burn.burnDuration;

        if (stats != null)
        {
            float totalMultiplier = 1f + Mathf.Max(0f, stats.burnTotalDamageMultiplier);
            if (!Mathf.Approximately(totalMultiplier, 1f))
            {
                damagePerTick *= totalMultiplier;
            }

            if (!Mathf.Approximately(stats.burnDurationBonus, 0f))
            {
                duration = Mathf.Max(0f, duration + stats.burnDurationBonus);
            }
        }

        StatusController statusController = ownerGO.GetComponent<StatusController>() ?? ownerGO.GetComponentInParent<StatusController>();
        if (statusController == null)
        {
            statusController = ownerGO.AddComponent<StatusController>();
        }

        int stacks = Mathf.Clamp(burn.burnStacksPerHit, 1, 4);
        statusController.AddStatus(StatusId.Burn, stacks, duration, damagePerTick, sourceCard);

        if (DamageNumberManager.Instance != null)
        {
            Vector3 anchor = DamageNumberManager.Instance.GetAnchorWorldPosition(ownerGO, ownerGO.transform.position);
            if (!EnemyDamagePopupScope.SuppressPopups)
            {
                DamageNumberManager.Instance.ShowBurn(anchor);
            }
        }

        return true;
    }

    private void UpdateImmolationDerivedDurationAndPresence()
    {
        int immolationIndex = activeStatuses.FindIndex(s => s.id == StatusId.Immolation);
        if (immolationIndex < 0)
        {
            return;
        }

        int burnStacks = GetStacks(StatusId.Burn);
        if (burnStacks <= 0)
        {
            RemoveStatus(StatusId.Immolation);
            return;
        }

        float duration = GetMaxRemainingDuration(StatusId.Burn);
        if (duration == 0f)
        {
            RemoveStatus(StatusId.Immolation);
            return;
        }

        ActiveStatus immolation = activeStatuses[immolationIndex];
        immolation.stacks = Mathf.Max(1, immolation.stacks);
        immolation.remainingDuration = duration;
        activeStatuses[immolationIndex] = immolation;
    }

    private float ComputeTotalRemainingBurnDamage(float burnIntervalSeconds)
    {
        float interval = Mathf.Max(0.01f, burnIntervalSeconds);
        float total = 0f;

        for (int i = 0; i < activeStatuses.Count; i++)
        {
            ActiveStatus s = activeStatuses[i];
            if (s.id != StatusId.Burn || s.stacks <= 0 || s.baseDamagePerTick <= 0f)
            {
                continue;
            }

            float dur = s.remainingDuration;
            if (dur <= 0f)
            {
                continue;
            }

            float perTick = Mathf.Max(0f, s.baseDamagePerTick) * Mathf.Max(0, s.stacks);
            if (perTick <= 0f)
            {
                continue;
            }

            float remainingTicks = dur / interval;
            total += perTick * Mathf.Max(0f, remainingTicks);
        }

        return total;
    }

    private void ShowStatusApplied(StatusId id)
    {
        if (DamageNumberManager.Instance == null)
        {
            return;
        }

        if (EnemyDamagePopupScope.SuppressPopups)
        {
            return;
        }

        Vector3 pos = transform.position;
        pos = DamageNumberManager.Instance.GetAnchorWorldPosition(gameObject, pos);

        switch (id)
        {
            case StatusId.Poison:
                DamageNumberManager.Instance.ShowPoison(pos);
                break;
            case StatusId.Wound:
                DamageNumberManager.Instance.ShowWound(pos);
                break;
            case StatusId.Weak:
                DamageNumberManager.Instance.ShowWeak(pos);
                break;
        }
    }

    private float GetMaxRemainingDuration(StatusId id)
    {
        float max = 0f;
        bool hasAny = false;

        for (int i = 0; i < activeStatuses.Count; i++)
        {
            ActiveStatus s = activeStatuses[i];
            if (s.id != id || s.stacks <= 0)
            {
                continue;
            }

            if (s.remainingDuration < 0f)
            {
                return -1f;
            }

            hasAny = true;
            if (s.remainingDuration > max)
            {
                max = s.remainingDuration;
            }
        }

        return hasAny ? max : 0f;
    }

    private void TryApplyFreezeFromSlow()
    {
        if (HasStatus(StatusId.Freeze))
        {
            return;
        }

        int slowStacks = GetStacks(StatusId.Slow);
        if (slowStacks < 4)
        {
            return;
        }

        float duration = GetMaxRemainingDuration(StatusId.Slow);
        if (duration == 0f)
        {
            return;
        }

        AddStatus(StatusId.Freeze, 1, duration);
    }

    private void TryApplyImmolationFromBurn(int previousBurnStacks)
    {
        if (HasStatus(StatusId.Immolation))
        {
            return;
        }

        if (previousBurnStacks >= 4)
        {
            return;
        }

        int burnStacks = GetStacks(StatusId.Burn);
        if (burnStacks < 4)
        {
            return;
        }

        float duration = GetMaxRemainingDuration(StatusId.Burn);
        if (duration == 0f)
        {
            return;
        }

        AddStatus(StatusId.Immolation, 1, duration);
    }

    public void AddStatus(StatusId id, int stacks, float durationSeconds = -1f, float baseDamagePerTick = 0f, ProjectileCards sourceCard = null, int sourceKey = 0)
    {
        if (stacks <= 0)
        {
            return;
        }

        int previousStacks = 0;
        if (id == StatusId.Burn || id == StatusId.Slow)
        {
            previousStacks = GetStacks(id);
        }

        if (id == StatusId.Burn || id == StatusId.Slow)
        {
            int current = previousStacks;
            int max = 4;
            int allowed = Mathf.Max(0, max - current);
            if (allowed <= 0)
            {
                return;
            }
            stacks = Mathf.Min(stacks, allowed);
        }

        float resolvedDuration = durationSeconds;
        if (durationSeconds <= -9999f)
        {
            resolvedDuration = -1f;
        }
        else if (durationSeconds < 0f)
        {
            if (StatusControllerManager.Instance != null)
            {
                switch (id)
                {
                    case StatusId.Poison:
                        resolvedDuration = StatusControllerManager.Instance.PoisonDurationSeconds;
                        break;
                    case StatusId.Bleed:
                        resolvedDuration = StatusControllerManager.Instance.BleedDurationSeconds;
                        break;
                    case StatusId.Blessing:
                        resolvedDuration = StatusControllerManager.Instance.BlessingDurationSeconds;
                        break;
                    case StatusId.Wound:
                        resolvedDuration = StatusControllerManager.Instance.WoundDurationSeconds;
                        break;
                    case StatusId.Acceleration:
                        resolvedDuration = StatusControllerManager.Instance.AccelerationDurationSeconds;
                        break;
                }
            }
            else
            {
                switch (id)
                {
                    case StatusId.Poison:
                    case StatusId.Bleed:
                    case StatusId.Blessing:
                    case StatusId.Wound:
                        resolvedDuration = 5f;
                        break;
                    case StatusId.Acceleration:
                        resolvedDuration = 3f;
                        break;
                }
            }
        }

        bool useIndependentInstances = UsesIndependentDurationInstances(id, resolvedDuration);

        if (id != StatusId.Absolution)
        {
            ActiveStatus absolution = activeStatuses.Find(s => s.id == StatusId.Absolution);
            if (absolution != null && absolution.stacks > 0 && IsDebuff(id))
            {
                absolution.stacks -= 1;
                if (absolution.stacks <= 0 && absolution.remainingDuration <= 0f)
                {
                    activeStatuses.Remove(absolution);
                }
                return;
            }
        }

        // HATRED-from-debuffs: only allow debuffs to add extra HATRED stacks
        // if this unit ALREADY has at least 1 HATRED stack (e.g. granted by a
        // Favour or other external system). Normal enemies that spawn with 0
        // HATRED will NOT gain stacks just from taking debuffs.
        if (IsDebuff(id))
        {
            EnemyHealth enemy = GetComponent<EnemyHealth>() ?? GetComponentInParent<EnemyHealth>();
            int currentHatredStacks = GetStacks(StatusId.Hatred);
            if (enemy != null && currentHatredStacks > 0)
            {
                if (hatredDebuffSources == null)
                {
                    hatredDebuffSources = new HashSet<StatusId>();
                }

                if (!hatredDebuffSources.Contains(id))
                {
                    hatredDebuffSources.Add(id);

                    int hatredIndex = activeStatuses.FindIndex(s => s.id == StatusId.Hatred);
                    if (hatredIndex >= 0)
                    {
                        ActiveStatus hatredStatus = activeStatuses[hatredIndex];
                        hatredStatus.stacks += 1;
                        hatredStatus.remainingDuration = -1f;
                        activeStatuses[hatredIndex] = hatredStatus;
                    }
                }
            }
        }

        if (useIndependentInstances)
        {
            if (id == StatusId.Acceleration && stacks > 1)
            {
                for (int i = 0; i < stacks; i++)
                {
                    ActiveStatus newStatus = new ActiveStatus
                    {
                        id = id,
                        stacks = 1,
                        remainingDuration = resolvedDuration,
                        tickTimer = 0f,
                        baseDamagePerTick = baseDamagePerTick,
                        sourceKey = sourceKey,
                        sourceCard = sourceCard
                    };
                    activeStatuses.Add(newStatus);
                }
            }
            else
            {
                ActiveStatus newStatus = new ActiveStatus
                {
                    id = id,
                    stacks = stacks,
                    remainingDuration = resolvedDuration,
                    tickTimer = 0f,
                    baseDamagePerTick = baseDamagePerTick,
                    sourceKey = sourceKey,
                    sourceCard = sourceCard
                };
                activeStatuses.Add(newStatus);
            }

            ShowStatusApplied(id);
            NotifyStatusAppliedToFavours(id);
            SpawnStatusOnApplyEffect(id);

            if (id == StatusId.Slow)
            {
                if (previousStacks < 4 && GetStacks(StatusId.Slow) >= 4)
                {
                    TryApplyFreezeFromSlow();
                }
            }
            else if (id == StatusId.Burn)
            {
                TryApplyImmolationFromBurn(previousStacks);
            }
            return;
        }

        ActiveStatus status = activeStatuses.Find(s => s.id == id && s.sourceKey == sourceKey);

        if (status == null)
        {
            status = new ActiveStatus
            {
                id = id,
                stacks = stacks,
                remainingDuration = resolvedDuration,
                tickTimer = 0f,
                baseDamagePerTick = baseDamagePerTick,
                sourceKey = sourceKey,
                sourceCard = sourceCard
            };
            activeStatuses.Add(status);

            ShowStatusApplied(id);
            NotifyStatusAppliedToFavours(id);
            SpawnStatusOnApplyEffect(id);

            if (id == StatusId.Slow)
            {
                if (previousStacks < 4 && GetStacks(StatusId.Slow) >= 4)
                {
                    TryApplyFreezeFromSlow();
                }
            }
            else if (id == StatusId.Burn)
            {
                TryApplyImmolationFromBurn(previousStacks);
            }
        }
        else
        {
            status.stacks += stacks;
            if (resolvedDuration >= 0f)
            {
                if (status.remainingDuration < 0f)
                {
                    status.remainingDuration = resolvedDuration;
                }
                else
                {
                    status.remainingDuration = Mathf.Max(status.remainingDuration, resolvedDuration);
                }
            }

            if (baseDamagePerTick > 0f)
            {
                status.baseDamagePerTick = Mathf.Max(status.baseDamagePerTick, baseDamagePerTick);
            }

            if (sourceCard != null)
            {
                status.sourceCard = sourceCard;
            }

            ShowStatusApplied(id);
            NotifyStatusAppliedToFavours(id);
            SpawnStatusOnApplyEffect(id);

            if (id == StatusId.Slow)
            {
                if (previousStacks < 4 && GetStacks(StatusId.Slow) >= 4)
                {
                    TryApplyFreezeFromSlow();
                }
            }
            else if (id == StatusId.Burn)
            {
                TryApplyImmolationFromBurn(previousStacks);
            }
        }
    }

    private void NotifyStatusAppliedToFavours(StatusId id)
    {
        EnemyHealth enemy = GetComponent<EnemyHealth>() ?? GetComponentInParent<EnemyHealth>();
        if (enemy == null)
        {
            return;
        }

        FavourEffectManager favourManager = Object.FindObjectOfType<FavourEffectManager>();
        if (favourManager == null)
        {
            return;
        }

        favourManager.NotifyStatusApplied(enemy.gameObject, id);
    }

    private bool IsDebuff(StatusId id)
    {
        switch (id)
        {
            case StatusId.Lethargy:
            case StatusId.Curse:
            case StatusId.Vulnerable:
            case StatusId.Decay:
            case StatusId.Slow:
            case StatusId.Frostbite:
            case StatusId.Freeze:
            case StatusId.Burn:
            case StatusId.Scorched:
            case StatusId.Immolation:
            case StatusId.Static:
            case StatusId.StaticReapply:
            case StatusId.Shocked:
            case StatusId.Poison:
            case StatusId.Bleed:
            case StatusId.Wound:
            case StatusId.Amnesia:
            case StatusId.Weak:
            case StatusId.Overweight:
            case StatusId.Corruption:
                return true;
            default:
                return false;
        }
    }

    private Vector3 GetColliderCenter()
    {
        Collider2D col = GetComponent<Collider2D>() ?? GetComponentInParent<Collider2D>();
        if (col != null)
        {
            return col.bounds.center;
        }
        return transform.position;
    }

    private void SpawnStatusEffectAtColliderCenter(GameObject prefab, float sizeMultiplier)
    {
        if (prefab == null)
        {
            return;
        }

        Vector3 pos = GetColliderCenter();
        GameObject instance = Object.Instantiate(prefab, pos, Quaternion.identity);
        if (instance != null && sizeMultiplier > 0f)
        {
            instance.transform.localScale *= sizeMultiplier;
        }
    }

    private void SpawnStatusOnApplyEffect(StatusId id)
    {
        if (StatusControllerManager.Instance == null)
        {
            return;
        }

        switch (id)
        {
            case StatusId.Freeze:
                SpawnStatusEffectAtColliderCenter(
                    StatusControllerManager.Instance.FreezeOnApplyEffectPrefab,
                    StatusControllerManager.Instance.FreezeOnApplyEffectSizeMultiplier);
                break;
            case StatusId.Immolation:
                SpawnStatusEffectAtColliderCenter(
                    StatusControllerManager.Instance.ImmolationOnApplyEffectPrefab,
                    StatusControllerManager.Instance.ImmolationOnApplyEffectSizeMultiplier);
                break;
        }
    }

    private void SpawnFreezeOnEndEffect()
    {
        if (freezeDeathEffectPlayed)
        {
            return;
        }

        if (StatusControllerManager.Instance == null)
        {
            return;
        }

        SpawnStatusEffectAtColliderCenter(
            StatusControllerManager.Instance.FreezeOnDeathEffectPrefab,
            StatusControllerManager.Instance.FreezeOnDeathEffectSizeMultiplier);

        freezeDeathEffectPlayed = true;
    }

    private void HandleOwnerDeath()
    {
        if (GetStacks(StatusId.Freeze) > 0)
        {
            SpawnFreezeOnEndEffect();
        }

        if (!HasStatus(StatusId.Immolation))
        {
            return;
        }

        if (StatusControllerManager.Instance == null)
        {
            return;
        }

        float burnInterval = Mathf.Max(0.01f, StatusControllerManager.Instance.BurnTickIntervalSeconds);
        float totalDamage = ComputeTotalRemainingBurnDamage(burnInterval);
        if (totalDamage <= 0f)
        {
            return;
        }

        ProjectileCards lastSourceCard = null;
        for (int i = 0; i < activeStatuses.Count; i++)
        {
            ActiveStatus s = activeStatuses[i];
            if (s.id != StatusId.Burn || s.stacks <= 0 || s.baseDamagePerTick <= 0f)
            {
                continue;
            }

            if (s.sourceCard != null)
            {
                lastSourceCard = s.sourceCard;
            }
        }

        bool isCrit = false;
        PlayerStats stats = Object.FindObjectOfType<PlayerStats>();
        if (stats != null && stats.burnImmolationCanCrit)
        {
            float frenzyBonus = 0f;
            if (StatusControllerManager.Instance != null)
            {
                StatusController playerStatus = stats.GetComponent<StatusController>();
                if (playerStatus != null)
                {
                    int frenzyStacks = playerStatus.GetStacks(StatusId.Frenzy);
                    if (frenzyStacks > 0)
                    {
                        frenzyBonus = StatusControllerManager.Instance.CritPerStack * frenzyStacks;
                    }
                }
            }

            float chance = Mathf.Clamp(stats.critChance + frenzyBonus + Mathf.Max(0f, stats.burnImmolationCritChanceBonus), 0f, 100f);
            if (chance > 0f)
            {
                float roll = Random.Range(0f, 100f);
                if (roll < chance)
                {
                    float critDamage = stats.critDamage + Mathf.Max(0f, stats.burnImmolationCritDamageBonusPercent);
                    totalDamage *= Mathf.Max(0f, critDamage / 100f);
                    isCrit = true;
                }
            }

            stats.lastHitWasCrit = isCrit;
            FavourEffectManager favourManager = stats.GetComponent<FavourEffectManager>();
            if (favourManager != null)
            {
                ProjectileCards previousCard = favourManager.CurrentProjectileCard;
                favourManager.CurrentProjectileCard = lastSourceCard;
                favourManager.NotifyCritResolved(lastSourceCard, true, isCrit);
                favourManager.CurrentProjectileCard = previousCard;
            }
        }

        float radius = StatusControllerManager.Instance.ImmolationRadius;
        if (radius <= 0f)
        {
            return;
        }

        SpawnStatusEffectAtColliderCenter(
            StatusControllerManager.Instance.ImmolationOnDeathEffectPrefab,
            StatusControllerManager.Instance.ImmolationOnDeathEffectSizeMultiplier);

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius, LayerMask.GetMask("Enemy"));
        if (hits == null || hits.Length == 0)
        {
            return;
        }

        DamageAoeScope.BeginAoeDamage();
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i];
            if (col == null)
            {
                continue;
            }

            GameObject target = col.gameObject;
            if (target == null || target == gameObject)
            {
                continue;
            }

            EnemyHealth eh = target.GetComponent<EnemyHealth>() ?? target.GetComponentInParent<EnemyHealth>();
            if (eh == null || !eh.IsAlive)
            {
                continue;
            }

            if (lastSourceCard != null)
            {
                EnemyLastHitSource marker = eh.GetComponent<EnemyLastHitSource>();
                if (marker == null)
                {
                    marker = eh.gameObject.AddComponent<EnemyLastHitSource>();
                }
                marker.lastProjectileCard = lastSourceCard;
            }

            Vector3 pos = col.bounds.center;
            StatusDamageScope.BeginStatusTick(DamageNumberManager.DamageType.Fire, true);
            eh.TakeDamage(totalDamage, pos, Vector3.zero);
            float resolved = StatusDamageScope.LastResolvedDamage;
            StatusDamageScope.EndStatusTick();

            if (DamageNumberManager.Instance != null && resolved > 0f && !EnemyDamagePopupScope.SuppressPopups)
            {
                DamageNumberManager.Instance.ShowDamage(resolved, pos, DamageNumberManager.DamageType.Fire, isCrit, true);
            }
        }
        DamageAoeScope.EndAoeDamage();
    }
}