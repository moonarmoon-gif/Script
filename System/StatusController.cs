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

    public static void BeginStatusTick() => IsStatusTick = true;
    public static void EndStatusTick() => IsStatusTick = false;
}

public static class DamageAoeScope
{
    public static bool IsAoeDamage { get; private set; }

    public static void BeginAoeDamage() => IsAoeDamage = true;
    public static void EndAoeDamage() => IsAoeDamage = false;
}

[System.Serializable]
public class ActiveStatus
{
    public StatusId id;
    public int stacks;
    public float remainingDuration; // -1 = permanent
    public float tickTimer;
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
        damage = ApplyShield(damage);

        if (damage <= 0f)
        {
            // Optional: if you want to show a shield-block popup later, you can detect:
            // float absorbed = before - damage;
            return;
        }

        // Keep place for any future final-pass mitigation.
    }

    // Tracks the end time of the current WEAK "continuous" window for this unit.
    private float weakContinuousWindowEndTime = -1f;

    // Tracks which debuff types have already granted HATRED stacks for this unit.
    private HashSet<StatusId> hatredDebuffSources;

    private EnemyHealth cachedEnemyHealth;
    private bool freezeDeathEffectPlayed;

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

        float cap = maxHealth * (maxPercent / 100f);
        if (damage > cap)
        {
            damage = cap;

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

        return damage;
    }

    public int GetStacks(StatusId id)
    {
        for (int i = 0; i < activeStatuses.Count; i++)
        {
            if (activeStatuses[i].id == id)
            {
                return activeStatuses[i].stacks;
            }
        }
        return 0;
    }

    public bool HasStatus(StatusId id) => GetStacks(id) > 0;

    public bool ConsumeStacks(StatusId id, int count)
    {
        if (count <= 0 || activeStatuses.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < activeStatuses.Count; i++)
        {
            ActiveStatus status = activeStatuses[i];
            if (status.id != id)
            {
                continue;
            }

            status.stacks -= count;
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

            return true;
        }

        return false;
    }

    public float GetEnemyMoveSpeedMultiplier()
    {
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

        return multiplier;
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
            }
        }

        if (firstStrikeStacks <= 0 && hatredStacks <= 0 && focusStacks <= 0 && furyStacks <= 0)
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

        float normalizedHealth = -1f;
        bool hasHealth = false;

        if (focusStacks > 0 || furyStacks > 0)
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

        if (furyStacks > 0 && hasHealth)
        {
            float thresholdFraction = 0.5f;
            float per = 10f;
            if (StatusControllerManager.Instance != null)
            {
                thresholdFraction = StatusControllerManager.Instance.FuryLowHealthThresholdPercent / 100f;
                per = StatusControllerManager.Instance.FuryBonusPercentPerStack;
            }

            if (normalizedHealth <= thresholdFraction)
            {
                float furyBonus = Mathf.Max(0f, per * furyStacks);
                totalBonusPercent += furyBonus;
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
            float now = Time.time;

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
                    if (!ctx.isStatusTick)
                    {
                        damage = 0f;
                    }
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
                        if (isPlayerOwner)
                        {
                            break;
                        }
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
        if (activeStatuses.Count == 0)
        {
            return;
        }

        float dt = Time.deltaTime;
        if (dt <= 0f)
        {
            return;
        }

        IDamageable damageable = GetComponent<IDamageable>();

        for (int i = activeStatuses.Count - 1; i >= 0; i--)
        {
            ActiveStatus status = activeStatuses[i];

            if (status.remainingDuration > 0f)
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
                    damagePerStack = Mathf.Max(0f, StatusControllerManager.Instance.PoisonDamagePerStack);
                }
                while (status.tickTimer >= interval)
                {
                    status.tickTimer -= interval;
                    float poisonDamage = Mathf.Max(0f, status.stacks * damagePerStack);
                    if (poisonDamage > 0f)
                    {
                        Vector3 anchor = transform.position;
                        if (DamageNumberManager.Instance != null)
                        {
                            anchor = DamageNumberManager.Instance.GetAnchorWorldPosition(gameObject, anchor);
                        }

                        StatusDamageScope.BeginStatusTick();
                        damageable.TakeDamage(poisonDamage, anchor, Vector3.zero);
                        if (DamageNumberManager.Instance != null)
                        {
                            DamageNumberManager.Instance.ShowDamage(poisonDamage, anchor, DamageNumberManager.DamageType.Poison, false, true);
                        }
                        StatusDamageScope.EndStatusTick();
                    }
                }
            }

            bool hasFiniteDuration = status.remainingDuration >= 0f;
            bool expiredByDuration = hasFiniteDuration && status.remainingDuration <= 0f;
            bool noStacksAndNoDuration = status.stacks <= 0 && !hasFiniteDuration;

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
    }

    private void ShowStatusApplied(StatusId id)
    {
        if (DamageNumberManager.Instance == null)
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

    public void AddStatus(StatusId id, int stacks, float durationSeconds = -1f)
    {
        if (stacks <= 0)
        {
            return;
        }

        float resolvedDuration = durationSeconds;
        if (durationSeconds < 0f)
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

        if (IsDebuff(id))
        {
            EnemyHealth enemy = GetComponent<EnemyHealth>() ?? GetComponentInParent<EnemyHealth>();
            if (enemy != null)
            {
                if (hatredDebuffSources == null)
                {
                    hatredDebuffSources = new HashSet<StatusId>();
                }

                if (!hatredDebuffSources.Contains(id))
                {
                    hatredDebuffSources.Add(id);

                    int hatredIndex = activeStatuses.FindIndex(s => s.id == StatusId.Hatred);
                    if (hatredIndex < 0)
                    {
                        ActiveStatus hatredStatus = new ActiveStatus
                        {
                            id = StatusId.Hatred,
                            stacks = 1,
                            remainingDuration = -1f,
                            tickTimer = 0f
                        };
                        activeStatuses.Add(hatredStatus);
                    }
                    else
                    {
                        ActiveStatus hatredStatus = activeStatuses[hatredIndex];
                        hatredStatus.stacks += 1;
                        hatredStatus.remainingDuration = -1f;
                        activeStatuses[hatredIndex] = hatredStatus;
                    }
                }
            }
        }

        ActiveStatus status = activeStatuses.Find(s => s.id == id);
        if (status == null)
        {
            status = new ActiveStatus
            {
                id = id,
                stacks = stacks,
                remainingDuration = resolvedDuration,
                tickTimer = 0f
            };
            activeStatuses.Add(status);

            ShowStatusApplied(id);
            NotifyStatusAppliedToFavours(id);
            SpawnStatusOnApplyEffect(id);
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
            ShowStatusApplied(id);
            NotifyStatusAppliedToFavours(id);
            SpawnStatusOnApplyEffect(id);
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
        if (GetStacks(StatusId.Freeze) <= 0)
        {
            return;
        }

        SpawnFreezeOnEndEffect();
    }
}