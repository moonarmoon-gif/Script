using UnityEngine;

/// <summary>
/// Burn effect that deals damage over time to enemies
/// Can be applied by any projectile with burn chance
/// </summary>
public class BurnEffect : MonoBehaviour
{
    [Header("Burn Settings")]
    [Tooltip("Chance to apply burn (0-100%)")]
    [Range(0f, 100f)]
    public float burnChance = 30f;

    [Tooltip("Burn stacks granted per successful hit (1-4). 4 total stacks enable Immolation on death.")]
    [Range(1, 4)]
    public int burnStacksPerHit = 1;

    [Tooltip("Damage multiplier per tick (1.0 = 100% of base damage)")]
    public float burnDamageMultiplier = 1.0f;

    [Tooltip("How long burn lasts (seconds)")]
    public float burnDuration = 2f;

    [Header("Visual Settings")]
    [Tooltip("Burn VFX prefab to spawn on enemy")]
    public GameObject burnVFXPrefab;

    [Tooltip("Optional burn VFX offset when enemy is on the LEFT side of the camera")]
    public Vector2 burnVFXOffsetLeft = Vector2.zero;

    [Tooltip("Optional burn VFX offset when enemy is on the RIGHT side of the camera")]
    public Vector2 burnVFXOffsetRight = Vector2.zero;

    private float baseDamage = 0f;
    private ProjectileType projectileType = ProjectileType.Fire;

    /// <summary>
    /// Initialize burn effect with damage and projectile type.
    /// NOTE: In your project this is called at HIT TIME (right before TryApplyBurn).
    /// </summary>
    public void Initialize(float damage, ProjectileType type)
    {
        baseDamage = Mathf.Max(1f, damage);
        projectileType = type;
    }

    /// <summary>
    /// Try to apply burn to an enemy
    /// Returns true if burn was applied
    /// </summary>
    public bool TryApplyBurn(GameObject enemy, Vector3 hitPoint)
    {
        if (enemy == null) return false;

        // NEW: predetermined (pre-rolled) support. For most projectiles this
        // makes burn application deterministic per projectile instance. For
        // ElementalBeam and Fire/Ice Talons, we WANT a fresh roll per enemy,
        // so we deliberately ignore PredeterminedStatusRoll for those.
        bool ignorePreForThisProjectile =
            GetComponent<ElementalBeam>() != null ||
            GetComponent<ProjectileFireTalon>() != null ||
            GetComponent<ProjectileIceTalon>() != null ||
            GetComponent<DwarfStar>() != null ||
            GetComponent<NovaStar>() != null;

        PredeterminedStatusRoll pre = ignorePreForThisProjectile
            ? null
            : GetComponent<PredeterminedStatusRoll>();

        if (pre != null)
        {
            pre.EnsureRolled();

            if (pre.burnRolled)
            {
                if (!pre.burnWillApply)
                {
                    return false;
                }

                // IMPORTANT: stacksPerHit is NOT owned by PredeterminedStatusRoll.
                // Keep using this BurnEffect's burnStacksPerHit as configured.
            }
        }

        PlayerStats stats = Object.FindObjectOfType<PlayerStats>();

        // Determine whether THIS projectile came from an ACTIVE projectile card.
        ProjectileCards sourceCard = null;
        if (ProjectileCardModifiers.Instance != null)
        {
            sourceCard = ProjectileCardModifiers.Instance.GetCardFromProjectile(gameObject);
        }
        bool isActiveSource =
            sourceCard != null &&
            sourceCard.projectileSystem == ProjectileCards.ProjectileSystemType.Active;

        float effectiveChance = burnChance;

        // Global chance bonus applies to both ACTIVE and PASSIVE.
        // ACTIVE-only chance bonus applies ONLY when the source is ACTIVE.
        if (stats != null && stats.hasProjectileStatusEffect)
        {
            effectiveChance += Mathf.Max(0f, stats.statusEffectChance);

            if (isActiveSource)
            {
                effectiveChance += Mathf.Max(0f, stats.activeProjectileStatusEffectChanceBonus);
            }
        }

        ProjectileStatusChanceAdditiveBonus additiveBonus = GetComponent<ProjectileStatusChanceAdditiveBonus>();
        if (additiveBonus != null)
        {
            effectiveChance += Mathf.Max(0f, additiveBonus.burnBonusPercent);
        }

        effectiveChance = Mathf.Clamp(effectiveChance, 0f, 100f);

        // Only roll RNG if we do NOT have a predetermined roll.
        if (pre == null || !pre.burnRolled)
        {
            float roll = Random.Range(0f, 100f);
            if (roll > effectiveChance)
            {
                return false;
            }
        }

        // IMPORTANT ROBUSTNESS CHANGE:
        // Always attach BurnStatus to the SAME GameObject that owns EnemyHealth (if available).
        // This makes auto-fire checks reliable and prevents "burn exists but can't be found".
        EnemyHealth ownerHealth = enemy.GetComponent<EnemyHealth>() ?? enemy.GetComponentInParent<EnemyHealth>();
        GameObject ownerGO = ownerHealth != null ? ownerHealth.gameObject : enemy;

        StatusController immuneCheck = ownerGO.GetComponent<StatusController>() ?? ownerGO.GetComponentInParent<StatusController>();
        if (immuneCheck != null && immuneCheck.HasStatus(StatusId.Immune))
        {
            return false;
        }

        float damagePerTick = Mathf.Max(1f, baseDamage * burnDamageMultiplier);
        float duration = burnDuration;

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

        Vector3 vfxPosition = ownerGO.transform.position;
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            bool enemyOnLeftSide = ownerGO.transform.position.x < mainCam.transform.position.x;
            Vector2 offset = enemyOnLeftSide ? burnVFXOffsetLeft : burnVFXOffsetRight;
            vfxPosition += (Vector3)offset;
        }
        else
        {
            vfxPosition = hitPoint;
        }

        StatusController statusController = ownerGO.GetComponent<StatusController>() ?? ownerGO.GetComponentInParent<StatusController>();
        if (statusController == null)
        {
            statusController = ownerGO.AddComponent<StatusController>();
        }

        statusController.AddStatus(StatusId.Burn, burnStacksPerHit, duration, damagePerTick, sourceCard);

        if (DamageNumberManager.Instance != null && !EnemyDamagePopupScope.SuppressPopups)
        {
            Vector3 anchor = DamageNumberManager.Instance.GetAnchorWorldPosition(ownerGO, ownerGO.transform.position);
            DamageNumberManager.Instance.ShowBurn(anchor);
        }

        return true;
    }

    /// <summary>
    /// Component attached to enemies that are burning
    /// Handles damage over time and visual effects
    /// </summary>
    public class BurnStatus : MonoBehaviour
    {
        private float baseDamagePerTick = 0f;

        // burn tick is interval-based (seconds per tick), globally enforced.
        private float tickIntervalSeconds = 0.25f;

        private float remainingDuration = 0f;
        private float nextTickTime = 0f;

        private ProjectileType damageType = ProjectileType.Fire;
        private GameObject burnVFX;
        private bool isBurning = false;

        private ProjectileCards sourceCard;
        private int stacks = 0;
        private bool hasImmolation = false;

        [SerializeField, Range(1, 4)]
        private int stacksPerHit = 1;

        private EnemyHealth ownerHealth;
        private bool subscribedToDeath = false;

        // Getters used by auto-fire targeting logic
        public bool IsBurning => isBurning;
        public bool HasImmolation => hasImmolation;
        public float RemainingDuration => remainingDuration;
        public float TickIntervalSeconds => Mathf.Max(0.01f, tickIntervalSeconds);

        // NEW: expose tick scheduling info so auto-fire can compute "ticks remaining" correctly.
        public float NextTickTime => nextTickTime;

        /// <summary>
        /// Burn tick damage INCLUDING stack scaling (but BEFORE mitigation).
        /// Mirrors DealBurnDamage() logic.
        /// </summary>
        public float GetRawTickDamageWithScaling()
        {
            float scaling = 1f + 0.5f * Mathf.Max(0, stacks - 1);
            return Mathf.Max(0f, baseDamagePerTick * scaling);
        }

        public void SetStacksPerHit(int stacks)
        {
            stacksPerHit = Mathf.Clamp(stacks, 1, 4);
        }

        public void ApplyBurn(float damage, float tickInterval, float duration, ProjectileType type, GameObject vfxPrefab, Vector3 hitPoint)
        {
            if (StatusControllerManager.Instance != null)
            {
                tickIntervalSeconds = StatusControllerManager.Instance.BurnTickIntervalSeconds;
            }
            else
            {
                tickIntervalSeconds = Mathf.Max(0.01f, tickInterval);
            }

            if (!isBurning)
            {
                baseDamagePerTick = damage;
                remainingDuration = duration;
                damageType = type;
                isBurning = true;

                nextTickTime = Time.time + tickIntervalSeconds;
                stacks = Mathf.Clamp(stacksPerHit, 1, 4);

                if (vfxPrefab != null)
                {
                    Vector3 spawnPos = hitPoint;
                    if (spawnPos == Vector3.zero)
                    {
                        spawnPos = transform.position;
                    }

                    burnVFX = Instantiate(vfxPrefab, spawnPos, Quaternion.identity, transform);
                }

                Debug.Log($"<color=orange>🔥 Burn STARTED! Stacks={stacks}, BaseDamage={baseDamagePerTick:F1}/tick, Duration: {duration}s, TickInterval: {tickIntervalSeconds:F2}s</color>");
            }
            else
            {
                stacks = Mathf.Clamp(stacks + stacksPerHit, 1, 4);
                remainingDuration = Mathf.Max(remainingDuration, duration);

                if (StatusControllerManager.Instance != null)
                {
                    tickIntervalSeconds = StatusControllerManager.Instance.BurnTickIntervalSeconds;
                }

                Debug.Log($"<color=orange>🔥 Burn STACK ADDED! Stacks={stacks}, Duration now={remainingDuration:F1}s, TickInterval={tickIntervalSeconds:F2}s</color>");
            }

            if (stacks >= 4 && !hasImmolation)
            {
                stacks = 4;
                hasImmolation = true;

                if (StatusControllerManager.Instance != null)
                {
                    GameObject prefab = StatusControllerManager.Instance.ImmolationOnApplyEffectPrefab;
                    float sizeMult = StatusControllerManager.Instance.ImmolationOnApplyEffectSizeMultiplier;
                    SpawnImmolationEffect(prefab, sizeMult);
                }
            }

            if (!subscribedToDeath)
            {
                ownerHealth = GetComponent<EnemyHealth>();
                if (ownerHealth != null)
                {
                    ownerHealth.OnDeath += HandleOwnerDeath;
                    subscribedToDeath = true;
                }
            }
        }

        public void SetSourceCard(ProjectileCards card)
        {
            sourceCard = card;
        }

        private void Update()
        {
            if (!isBurning) return;

            if (StatusControllerManager.Instance != null)
            {
                tickIntervalSeconds = StatusControllerManager.Instance.BurnTickIntervalSeconds;
            }
            tickIntervalSeconds = Mathf.Max(0.01f, tickIntervalSeconds);

            if (Time.time >= nextTickTime)
            {
                DealBurnDamage();
                nextTickTime = Time.time + tickIntervalSeconds;
            }

            remainingDuration -= Time.deltaTime;
            if (remainingDuration <= 0f)
            {
                EndBurn();
            }
        }

        private void DealBurnDamage()
        {
            EnemyHealth health = GetComponent<EnemyHealth>();
            if (health != null && health.IsAlive)
            {
                // Preserve last-hit attribution for favour and on-death logic.
                if (sourceCard != null)
                {
                    EnemyLastHitSource marker = health.GetComponent<EnemyLastHitSource>();
                    if (marker == null)
                    {
                        marker = health.gameObject.AddComponent<EnemyLastHitSource>();
                    }
                    marker.lastProjectileCard = sourceCard;
                }

                float tickDamage = GetRawTickDamageWithScaling();

                // Apply DemonSlime-specific FireResistance (or vulnerability) to
                // burn ticks so ALL Fire-type damage follows the same rules as
                // direct Fire/NovaStar hits in PlayerDamageHelper.
                DemonSlimeEnemy slime = health.GetComponent<DemonSlimeEnemy>() ?? health.GetComponentInParent<DemonSlimeEnemy>();
                if (slime != null)
                {
                    float fireResist = slime.FireResistance;
                    // Positive FireResistance reduces damage; negative values
                    // increase it. However, status effects must always be able
                    // to chip for at least 1 via the core status-tick min-1
                    // rule, so we never allow the resistance factor to reach
                    // exactly 0.
                    float factor = 1f - (fireResist / 100f);
                    if (factor <= 0f)
                    {
                        factor = 0.01f;
                    }
                    tickDamage *= factor;
                }

                bool isCrit = false;
                PlayerStats stats = Object.FindObjectOfType<PlayerStats>();
                if (stats != null && stats.burnImmolationCanCrit)
                {
                    float chance = Mathf.Clamp(stats.critChance + Mathf.Max(0f, stats.burnImmolationCritChanceBonus), 0f, 100f);
                    if (chance > 0f)
                    {
                        float roll = Random.Range(0f, 100f);
                        if (roll < chance)
                        {
                            float critDamage = stats.critDamage + Mathf.Max(0f, stats.burnImmolationCritDamageBonusPercent);
                            tickDamage *= Mathf.Max(0f, critDamage / 100f);
                            isCrit = true;
                        }
                    }
                }

                StatusDamageScope.BeginStatusTick(DamageNumberManager.DamageType.Fire, true);

                Vector3 anchor = DamageNumberManager.Instance != null
                    ? DamageNumberManager.Instance.GetAnchorWorldPosition(gameObject, transform.position)
                    : transform.position;

                health.TakeDamage(tickDamage, anchor, Vector3.zero);

                float resolved = StatusDamageScope.LastResolvedDamage;
                StatusDamageScope.EndStatusTick();

                if (DamageNumberManager.Instance != null && resolved > 0f && !EnemyDamagePopupScope.SuppressPopups)
                {
                    DamageNumberManager.Instance.ShowDamage(resolved, anchor, DamageNumberManager.DamageType.Fire, isCrit, true);
                }

                Debug.Log($"<color=orange>🔥 Burn tick: {resolved:F1} damage (raw={tickDamage:F1}, stacks={stacks}, base={baseDamagePerTick:F1}, interval={tickIntervalSeconds:F2}s)</color>");
            }
            else
            {
                EndBurn();
            }
        }

        private void EndBurn()
        {
            if (!isBurning)
            {
                return;
            }

            isBurning = false;
            remainingDuration = 0f;
            stacks = 0;
            hasImmolation = false;

            if (burnVFX != null)
            {
                Destroy(burnVFX);
                burnVFX = null;
            }

            if (subscribedToDeath && ownerHealth != null)
            {
                ownerHealth.OnDeath -= HandleOwnerDeath;
            }
            subscribedToDeath = false;
        }

        private void HandleOwnerDeath()
        {
            EndBurn();
        }

        private void OnDestroy()
        {
            if (subscribedToDeath && ownerHealth != null)
            {
                ownerHealth.OnDeath -= HandleOwnerDeath;
            }
        }

        private void SpawnImmolationEffect(GameObject prefab, float sizeMultiplier)
        {
            if (prefab == null)
            {
                return;
            }

            Collider2D col = GetComponent<Collider2D>() ?? GetComponentInParent<Collider2D>();
            Vector3 pos = transform.position;
            if (col != null)
            {
                pos = col.bounds.center;
            }

            GameObject instance = Instantiate(prefab, pos, Quaternion.identity);
            if (instance != null)
            {
                instance.transform.localScale *= Mathf.Max(0f, sizeMultiplier);
            }
        }
    }
}