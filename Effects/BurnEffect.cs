using UnityEngine;
using System.Collections;

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

    [Tooltip("Number of damage ticks per second")]
    public float burnDamageInstancesPerSecond = 4f;

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
    /// Initialize burn effect with damage and projectile type
    /// </summary>
    public void Initialize(float damage, ProjectileType type)
    {
        baseDamage = damage;
        projectileType = type;
    }

    /// <summary>
    /// Try to apply burn to an enemy
    /// Returns true if burn was applied
    /// </summary>
    public bool TryApplyBurn(GameObject enemy, Vector3 hitPoint)
    {
        if (enemy == null) return false;

        // Check burn chance
        float roll = Random.Range(0f, 100f);
        if (roll > burnChance)
        {
            // Burn roll failed (log removed for cleaner console)
            return false;
        }

        // Get or add BurnStatus component
        BurnStatus burnStatus = enemy.GetComponent<BurnStatus>();
        if (burnStatus == null)
        {
            burnStatus = enemy.AddComponent<BurnStatus>();
        }

        burnStatus.SetStacksPerHit(burnStacksPerHit);

        ProjectileCards sourceCard = null;
        if (ProjectileCardModifiers.Instance != null)
        {
            sourceCard = ProjectileCardModifiers.Instance.GetCardFromProjectile(gameObject);
        }
        if (sourceCard != null)
        {
            burnStatus.SetSourceCard(sourceCard);
        }

        // Calculate damage per tick
        float damagePerTick = baseDamage * burnDamageMultiplier;
        float duration = burnDuration;

        PlayerStats stats = Object.FindObjectOfType<PlayerStats>();
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

        // Compute VFX spawn position with optional left/right offsets based on
        // enemy position relative to the camera. This position will be passed
        // via hitPoint into BurnStatus so the VFX can spawn offset but still be
        // parented to and move with the enemy.
        Vector3 vfxPosition = enemy.transform.position;
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            bool enemyOnLeftSide = enemy.transform.position.x < mainCam.transform.position.x;
            Vector2 offset = enemyOnLeftSide ? burnVFXOffsetLeft : burnVFXOffsetRight;
            vfxPosition += (Vector3)offset;
        }
        else
        {
            vfxPosition = hitPoint;
        }

        // Apply burn
        burnStatus.ApplyBurn(damagePerTick, burnDamageInstancesPerSecond, duration, projectileType, burnVFXPrefab, vfxPosition);

        // Show BURN status popup at enemy position
        if (DamageNumberManager.Instance != null)
        {
            Vector3 anchor = DamageNumberManager.Instance.GetAnchorWorldPosition(enemy, enemy.transform.position);
            DamageNumberManager.Instance.ShowBurn(anchor);
        }

        // If the enemy was actually killed by the same hit that applied this
        // burn (e.g., FireMine base damage), EnemyHealth.OnDeath may have
        // already been raised BEFORE BurnStatus subscribed to it. In that
        // case, ensure Immolation still triggers by explicitly invoking the
        // death handler once when the owner is already dead.
        EnemyHealth appliedHealth = enemy.GetComponent<EnemyHealth>() ?? enemy.GetComponentInParent<EnemyHealth>();
        if (appliedHealth != null && !appliedHealth.IsAlive)
        {
            burnStatus.HandleOwnerDeath();
        }

        // Inform favour effects that the player has successfully inflicted
        // the BURN status on this enemy so they can react (e.g., by adding
        // Scorched stacks via StatusEffectsDebuffFavour).
        FavourEffectManager favourManager = Object.FindObjectOfType<FavourEffectManager>();
        if (favourManager != null)
        {
            favourManager.NotifyStatusApplied(enemy, StatusId.Burn);
        }

        Debug.Log($"<color=orange>🔥 BURN APPLIED! Damage/tick: {damagePerTick:F1}, Duration: {duration}s, Ticks/sec: {burnDamageInstancesPerSecond}</color>");

        return true;
    }
    /// <summary>
    /// Component attached to enemies that are burning
    /// Handles damage over time and visual effects
    /// </summary>
    public class BurnStatus : MonoBehaviour
    {
        private float baseDamagePerTick = 0f;
        private float ticksPerSecond = 1f;
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

        public void SetStacksPerHit(int stacks)
        {
            stacksPerHit = Mathf.Clamp(stacks, 1, 4);
        }

        /// <summary>
        /// Apply or refresh burn effect
        /// </summary>
        public void ApplyBurn(float damage, float tickRate, float duration, ProjectileType type, GameObject vfxPrefab, Vector3 hitPoint)
        {
            if (!isBurning)
            {
                baseDamagePerTick = damage;
                ticksPerSecond = tickRate;
                remainingDuration = duration;
                damageType = type;
                isBurning = true;
                nextTickTime = Time.time + (1f / ticksPerSecond);
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

                Debug.Log($"<color=orange>🔥 Burn STARTED! Stacks={stacks}, BaseDamage={baseDamagePerTick:F1}/tick, Duration: {duration}s, Ticks/sec: {ticksPerSecond}</color>");
            }
            else
            {
                stacks = Mathf.Clamp(stacks + stacksPerHit, 1, 4);
                remainingDuration = Mathf.Max(remainingDuration, duration);
                Debug.Log($"<color=orange>🔥 Burn STACK ADDED! Stacks={stacks}, Duration now={remainingDuration:F1}s</color>");
            }

            if (stacks >= 4 && !hasImmolation)
            {
                stacks = 4;
                hasImmolation = true;

                // IMMOLATION just became active on this enemy – spawn the
                // configured OnApply VFX at the collider center.
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

        void Update()
        {
            if (!isBurning) return;

            // Tick damage
            if (Time.time >= nextTickTime)
            {
                DealBurnDamage();
                nextTickTime = Time.time + (1f / ticksPerSecond);
            }

            // Update duration
            remainingDuration -= Time.deltaTime;
            if (remainingDuration <= 0f)
            {
                EndBurn();
            }
        }

        void DealBurnDamage()
        {
            EnemyHealth health = GetComponent<EnemyHealth>();
            if (health != null && health.IsAlive)
            {
                if (sourceCard != null)
                {
                    EnemyLastHitSource marker = health.GetComponent<EnemyLastHitSource>();
                    if (marker == null)
                    {
                        marker = health.gameObject.AddComponent<EnemyLastHitSource>();
                    }
                    marker.lastProjectileCard = sourceCard;
                }

                float scaling = 1f + 0.5f * Mathf.Max(0, stacks - 1);
                float tickDamage = baseDamagePerTick * scaling;

                StatusDamageScope.BeginStatusTick();
                Vector3 anchor = DamageNumberManager.Instance != null
                    ? DamageNumberManager.Instance.GetAnchorWorldPosition(gameObject, transform.position)
                    : transform.position;
                health.TakeDamage(tickDamage, anchor, Vector3.zero);
                StatusDamageScope.EndStatusTick();

                if (DamageNumberManager.Instance != null)
                {
                    DamageNumberManager.Instance.ShowDamage(tickDamage, anchor, DamageNumberManager.DamageType.Fire, false, true);
                }

                Debug.Log($"<color=orange>🔥 Burn tick: {tickDamage:F1} damage (stacks={stacks}, base={baseDamagePerTick:F1})</color>");
            }
            else
            {
                // Enemy dead, end burn
                EndBurn();
            }
        }

        void EndBurn()
        {
            isBurning = false;
            remainingDuration = 0f;

            // Destroy VFX
            if (burnVFX != null)
            {
                Destroy(burnVFX);
            }

            Debug.Log("<color=orange>🔥 Burn ENDED</color>");

            // Remove component
            Destroy(this);
        }

        void OnDestroy()
        {
            // Clean up VFX if component is destroyed
            if (burnVFX != null)
            {
                Destroy(burnVFX);
            }

            if (ownerHealth != null && subscribedToDeath)
            {
                ownerHealth.OnDeath -= HandleOwnerDeath;
            }
        }

        public void HandleOwnerDeath()
        {
            if (!hasImmolation || baseDamagePerTick <= 0f || ticksPerSecond <= 0f)
            {
                return;
            }

            float scaling = 1f + 0.5f * Mathf.Max(0, stacks - 1);
            float tickDamage = baseDamagePerTick * scaling;
            float remainingTicks = Mathf.Max(0f, remainingDuration) * ticksPerSecond;
            float totalDamage = tickDamage * remainingTicks;
            if (totalDamage <= 0f)
            {
                return;
            }

            float radius = 3f;
            if (StatusControllerManager.Instance != null)
            {
                radius = StatusControllerManager.Instance.ImmolationRadius;

                // Spawn the Immolation OnDeath effect at the collider center
                // of the dying owner before applying damage.
                GameObject deathPrefab = StatusControllerManager.Instance.ImmolationOnDeathEffectPrefab;
                float deathSizeMult = StatusControllerManager.Instance.ImmolationOnDeathEffectSizeMultiplier;
                SpawnImmolationEffect(deathPrefab, deathSizeMult);
            }

            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius, LayerMask.GetMask("Enemy"));
            if (hits == null || hits.Length == 0)
            {
                return;
            }

            foreach (Collider2D col in hits)
            {
                if (col == null) continue;
                GameObject target = col.gameObject;
                if (target == gameObject) continue;

                EnemyHealth eh = target.GetComponent<EnemyHealth>() ?? target.GetComponentInParent<EnemyHealth>();
                if (eh != null && eh.IsAlive)
                {
                    Vector3 pos = col.bounds.center;
                    StatusDamageScope.BeginStatusTick();
                    eh.TakeDamage(totalDamage, pos, Vector3.zero);
                    StatusDamageScope.EndStatusTick();

                    if (DamageNumberManager.Instance != null)
                    {
                        DamageNumberManager.Instance.ShowDamage(totalDamage, pos, DamageNumberManager.DamageType.Fire, false, true);
                    }
                }
            }
        }

        private void SpawnImmolationEffect(GameObject prefab, float sizeMultiplier)
        {
            if (prefab == null || sizeMultiplier <= 0f)
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
                instance.transform.localScale *= sizeMultiplier;
            }
        }
    }
}
