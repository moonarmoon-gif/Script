using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class CinderCryoBloom : MonoBehaviour, IDamageable, IInstantModifiable
{
    [Header("Bloom Settings")]
    [SerializeField] private float armDelay = 0.5f;
    [Tooltip("Time before bloom disappears after being armed")]
    [SerializeField] private float lifetimeSeconds = 5f;
    
    [Header("Fire Spitting")]
    [SerializeField] private float damage = 20f;
    [Tooltip("Interval between fire spits (in seconds)")]
    [SerializeField] private float spitInterval = 1f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private ProjectileType projectileType = ProjectileType.Fire;
    
    [Header("Spawn Area - 4 Point System")]
    [Tooltip("Tag for point A (top-left): determines minX and minY")]
    [SerializeField] private string pointATag = "CinderBloom_PointA";
    [Tooltip("Tag for point B (top-right): determines maxX and minY")]
    [SerializeField] private string pointBTag = "CinderBloom_PointB";
    [Tooltip("Tag for point C (bottom-right): determines maxX and maxY")]
    [SerializeField] private string pointCTag = "CinderBloom_PointC";
    [Tooltip("Tag for point D (bottom-left): determines minX and maxY")]
    [SerializeField] private string pointDTag = "CinderBloom_PointD";
    
    [Header("Overlap Prevention")]
    [Tooltip("Minimum distance allowed between CinderBlooms (multiplier of collider radius)")]
    [SerializeField] private float minDistanceBetweenBlooms = 2f;
    
    [Header("Collider Scaling")]
    [Tooltip("Offset for collider size relative to visual size (0 = same as visual, -0.2 = 20% smaller, 0.2 = 20% larger)")]
    [SerializeField] private float colliderSizeOffset = 0f;
    
    [Header("Enhanced Variant 1 - Enduring Bloom")]
    [Tooltip("Additional lifetime in seconds for Enhanced Variant 1")]
    [SerializeField] private float enhancedLifetimeBonus = 10f;
    [Tooltip("Damage multiplier for Enhanced Variant 1 (1.25 = +25%)")]
    [SerializeField] private float enhancedDamageMultiplier = 1.25f;
    
    [Header("Taunt System")]
    [Tooltip("Enable taunt feature (enemies attack Cinderbloom instead of player)")]
    [SerializeField] private bool enableTaunt = true;
    [Tooltip("Radius for taunt detection")]
    [SerializeField] private float tauntRadius = 5f;
    [Tooltip("Offset for taunt detection center")]
    [SerializeField] private Vector2 tauntOffset = Vector2.zero;
    [Tooltip("Cinderbloom health (0 = invincible)")]
    [SerializeField] private float bloomHealth = 100f;
    [Tooltip("Interval for taunt pulse (how often to check for enemies)")]
    [SerializeField] private float tauntPulseInterval = 0.5f;
    
    private Transform pointA;
    private Transform pointB;
    private Transform pointC;
    private Transform pointD;
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject armingEffectPrefab;
    
    [Header("Audio")]
    [SerializeField] private AudioClip spitClip;
    [Range(0f, 1f)][SerializeField] private float spitVolume = 0.7f;
    [SerializeField] private AudioClip armingClip;
    [Range(0f, 1f)][SerializeField] private float armingVolume = 0.5f;
    
    [Header("Mana & Cooldown")]
    [SerializeField] private int manaCost = 15;
    [SerializeField] private float cooldown = 1f;
    
    [Header("Fire Children")]
    [Tooltip("5 fire child objects that will deal damage")]
    [SerializeField] private Transform[] fireChildren = new Transform[5];
    
    private Rigidbody2D _rigidbody2D;
    private Collider2D _collider2D;
    
    private PlayerStats cachedPlayerStats;
    
    // Base values for instant modifier recalculation
    private float baseLifetimeSeconds;
    private float baseDamage;
    private Vector3 baseScale;
    private bool isArmed = false;
    private bool isActive = true;
    
    // Track enemies that have been damaged this spit cycle
    private HashSet<GameObject> damagedEnemiesThisCycle = new HashSet<GameObject>();
    
    // Track last damage time per enemy to enforce spit interval
    private Dictionary<GameObject, float> enemyLastDamageTimes = new Dictionary<GameObject, float>();
    
    // Instance-based cooldown tracking
    private static System.Collections.Generic.Dictionary<string, float> lastFireTimes = new System.Collections.Generic.Dictionary<string, float>();
    private string prefabKey;
    
    // Taunt system
    private float currentHealth;
    private HashSet<GameObject> tauntedEnemies = new HashSet<GameObject>();
    private Coroutine tauntCoroutine;
    
    // IDamageable implementation
    public bool IsAlive => currentHealth > 0 || bloomHealth <= 0; // Invincible if bloomHealth is 0
    
    private void Awake()
    {
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _collider2D = GetComponent<Collider2D>();
        
        // Store base values
        baseLifetimeSeconds = lifetimeSeconds;
        baseDamage = damage;
        baseScale = transform.localScale;
        
        // Make bloom stationary
        if (_rigidbody2D != null)
        {
            _rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
            _rigidbody2D.velocity = Vector2.zero;
        }
        
        // Set to a layer that enemies can attack (NOT Enemy layer to avoid friendly fire)
        // We'll use Default layer or create a "Damageable" layer
        // For now, keep on current layer but ensure collider is NOT a trigger
        if (_collider2D != null && enableTaunt && bloomHealth > 0)
        {
            _collider2D.isTrigger = false; // Must be solid for enemy attacks to hit
            Debug.Log($"<color=orange>Cinderbloom collider set to solid for enemy attacks</color>");
        }
    }
    
    public void Initialize(Vector3 spawnPosition, Collider2D playerCollider, bool skipCooldownCheck = false)
    {
        // Find spawn area GameObjects by tag (4-point system)
        if (!string.IsNullOrEmpty(pointATag))
        {
            GameObject pointAObj = GameObject.FindGameObjectWithTag(pointATag);
            if (pointAObj != null) pointA = pointAObj.transform;
        }
        
        if (!string.IsNullOrEmpty(pointBTag))
        {
            GameObject pointBObj = GameObject.FindGameObjectWithTag(pointBTag);
            if (pointBObj != null) pointB = pointBObj.transform;
        }
        
        if (!string.IsNullOrEmpty(pointCTag))
        {
            GameObject pointCObj = GameObject.FindGameObjectWithTag(pointCTag);
            if (pointCObj != null) pointC = pointCObj.transform;
        }
        
        if (!string.IsNullOrEmpty(pointDTag))
        {
            GameObject pointDObj = GameObject.FindGameObjectWithTag(pointDTag);
            if (pointDObj != null) pointD = pointDObj.transform;
        }
        
        // Determine spawn position using 4-point system
        if (pointA != null && pointB != null && pointC != null && pointD != null)
        {
            float minX1 = pointA.position.x;
            float maxX1 = pointB.position.x;
            float minX2 = pointD.position.x;
            float maxX2 = pointC.position.x;
            
            float minY1 = pointA.position.y;
            float maxY1 = pointD.position.y;
            float minY2 = pointB.position.y;
            float maxY2 = pointC.position.y;
            
            // Random X between the min and max of all X values
            float finalMinX = Mathf.Min(minX1, maxX1, minX2, maxX2);
            float finalMaxX = Mathf.Max(minX1, maxX1, minX2, maxX2);
            
            // Random Y between the min and max of all Y values
            float finalMinY = Mathf.Min(minY1, maxY1, minY2, maxY2);
            float finalMaxY = Mathf.Max(minY1, maxY1, minY2, maxY2);
            
            // Try to find non-overlapping position
            Vector3 finalPosition = spawnPosition;
            bool foundValidPosition = false;
            int maxAttempts = 20;
            
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                float spawnX = Random.Range(finalMinX, finalMaxX);
                float spawnY = Random.Range(finalMinY, finalMaxY);
                Vector3 testPosition = new Vector3(spawnX, spawnY, spawnPosition.z);
                
                // Check if this position overlaps with any existing CinderBloom
                if (!IsOverlappingWithOtherBlooms(testPosition))
                {
                    finalPosition = testPosition;
                    foundValidPosition = true;
                    Debug.Log($"<color=orange>CinderBloom spawned at ({spawnX:F2}, {spawnY:F2}) - no overlap (attempt {attempt + 1})</color>");
                    break;
                }
            }
            
            if (!foundValidPosition)
            {
                float spawnX = Random.Range(finalMinX, finalMaxX);
                float spawnY = Random.Range(finalMinY, finalMaxY);
                finalPosition = new Vector3(spawnX, spawnY, spawnPosition.z);
                Debug.LogWarning($"<color=yellow>CinderBloom: Could not find non-overlapping position after {maxAttempts} attempts. Spawning anyway at ({spawnX:F2}, {spawnY:F2})</color>");
            }
            
            transform.position = finalPosition;
        }
        else
        {
            transform.position = spawnPosition;
            Debug.LogWarning("<color=yellow>CinderBloom: Not all 4 spawn points found, using default position</color>");
        }
        
        // Get card-specific modifiers
        ProjectileCards card = ProjectileCardModifiers.Instance.GetCardFromProjectile(gameObject);
        CardModifierStats modifiers = new CardModifierStats();
        
        if (card != null)
        {
            modifiers = ProjectileCardModifiers.Instance.GetCardModifiers(card);
            Debug.Log($"<color=orange>CinderBloom using modifiers from {card.cardName}</color>");
        }
        
        // Check for enhanced variant
        int enhancedVariant = 0;
        if (ProjectileCardLevelSystem.Instance != null && card != null)
        {
            enhancedVariant = ProjectileCardLevelSystem.Instance.GetEnhancedVariant(card);
        }
        
        // Apply enhanced variant bonuses
        float enhancedLifetimeAdd = 0f;
        float enhancedDamageMult = 1f;
        if (enhancedVariant == 1)
        {
            enhancedLifetimeAdd = enhancedLifetimeBonus;
            enhancedDamageMult = enhancedDamageMultiplier;
            Debug.Log($"<color=gold>Enhanced Enduring Bloom: +{enhancedLifetimeBonus}s lifetime, x{enhancedDamageMultiplier} damage</color>");
        }

        // CRITICAL: Use ProjectileCards spawnInterval if available
        float baseCooldown = cooldown;
        if (card != null && card.runtimeSpawnInterval > 0f)
        {
            baseCooldown = card.runtimeSpawnInterval;
            Debug.Log($"<color=gold>CinderBloom using ProjectileCards spawnInterval: {baseCooldown:F2}s</color>");
        }
        
        // Apply card modifiers using new RAW value system
        float finalLifetime = lifetimeSeconds + modifiers.lifetimeIncrease + enhancedLifetimeAdd; // RAW seconds + enhanced
        float finalCooldown = Mathf.Max(0.1f, baseCooldown * (1f - modifiers.cooldownReductionPercent / 100f)); // % from base
        int finalManaCost = Mathf.Max(1, Mathf.CeilToInt(manaCost * (1f - modifiers.manaCostReduction)));
        damage = (baseDamage + modifiers.damageFlat) * modifiers.damageMultiplier * enhancedDamageMult;
        
        // Apply size multiplier
        float totalSizeMultiplier = modifiers.sizeMultiplier;
        if (totalSizeMultiplier != 1f)
        {
            transform.localScale *= totalSizeMultiplier;
            
            // Scale collider
            float colliderScale = 1f + (colliderSizeOffset / totalSizeMultiplier);
            
            if (_collider2D is CircleCollider2D circle)
            {
                circle.radius *= colliderScale;
            }
            else if (_collider2D is BoxCollider2D box)
            {
                box.size *= colliderScale;
            }
            else if (_collider2D is CapsuleCollider2D capsule)
            {
                capsule.size *= colliderScale;
            }
        }
        
        Debug.Log($"<color=orange>CinderBloom Modifiers Applied: Size={modifiers.sizeMultiplier:F2}x, Damage={modifiers.damageMultiplier:F2}x, Lifetime=+{modifiers.lifetimeIncrease:F2}s</color>");
        
        // Get PlayerStats for base damage calculation
        cachedPlayerStats = FindObjectOfType<PlayerStats>();

        float effectiveCooldown = finalCooldown;
        if (cachedPlayerStats != null && cachedPlayerStats.projectileCooldownReduction > 0f)
        {
            float totalCdr = Mathf.Max(0f, cachedPlayerStats.projectileCooldownReduction);
            effectiveCooldown = finalCooldown / (1f + totalCdr);
            if (MinCooldownManager.Instance != null && card != null)
            {
                effectiveCooldown = MinCooldownManager.Instance.ClampCooldown(card, effectiveCooldown);
            }
            else
            {
                effectiveCooldown = Mathf.Max(0.1f, effectiveCooldown);
            }
        }
        
        // Allow the global "enhanced first spawn" reduction system to bypass this
        // internal cooldown gate exactly once for PASSIVE projectile cards.
        bool bypassEnhancedFirstSpawnCooldown = false;
        if (!skipCooldownCheck && card != null && card.applyEnhancedFirstSpawnReduction && card.pendingEnhancedFirstSpawn)
        {
            if (card.projectileSystem == ProjectileCards.ProjectileSystemType.Passive)
            {
                bypassEnhancedFirstSpawnCooldown = true;
                card.pendingEnhancedFirstSpawn = false;
            }
        }

        // Generate key based ONLY on projectile type (so all CinderBlooms share same cooldown)
        prefabKey = "CinderBloom";
        
        // Only check cooldown/mana for first projectile in multi-spawn
        if (!skipCooldownCheck)
        {
            // Check cooldown
            if (!bypassEnhancedFirstSpawnCooldown && lastFireTimes.ContainsKey(prefabKey))
            {
                if (GameStateManager.PauseSafeTime - lastFireTimes[prefabKey] < effectiveCooldown)
                {
                    Debug.Log($"<color=yellow>CinderBloom on cooldown - {GameStateManager.PauseSafeTime - lastFireTimes[prefabKey]:F2}s / {effectiveCooldown}s</color>");
                    Destroy(gameObject);
                    return;
                }
            }

            // Record fire time
            lastFireTimes[prefabKey] = GameStateManager.PauseSafeTime;
        }
        else
        {
            Debug.Log($"<color=gold>CinderBloom: Skipping cooldown/mana check (multi-projectile spawn)</color>");
        }
        
        // Ignore collision with player
        if (_collider2D != null && playerCollider != null)
        {
            Physics2D.IgnoreCollision(_collider2D, playerCollider, true);
        }
        
        // Start arming sequence
        StartCoroutine(ArmingSequence(finalLifetime));
    }
    
    private IEnumerator ArmingSequence(float lifetime)
    {
        // Initialize health
        currentHealth = bloomHealth;
        
        // Wait for arming delay
        yield return GameStateManager.WaitForPauseSafeSeconds(armDelay);
        
        isArmed = true;
        
        // Play arming effect
        if (armingEffectPrefab != null)
        {
            GameObject armEffect = Instantiate(armingEffectPrefab, transform.position, Quaternion.identity, transform);
            PauseSafeSelfDestruct.Schedule(armEffect, lifetime);
        }
        
        // Play arming sound
        if (armingClip != null)
        {
            AudioSource.PlayClipAtPoint(armingClip, transform.position, armingVolume);
        }
        
        // Start taunt system
        if (enableTaunt)
        {
            tauntCoroutine = StartCoroutine(TauntNearbyEnemies());
            Debug.Log("<color=orange>Cinderbloom taunt system activated!</color>");
        }
        
        // Start spitting fire
        StartCoroutine(SpitFireRoutine(lifetime - armDelay));
    }
    
    private IEnumerator SpitFireRoutine(float remainingLifetime)
    {
        float elapsedTime = 0f;
        
        while (elapsedTime < remainingLifetime && isActive)
        {
            // Spit fire (damage enemies in contact with fire children)
            SpitFire();
            
            // Wait for spit interval
            yield return GameStateManager.WaitForPauseSafeSeconds(spitInterval);
            elapsedTime += spitInterval;
            
            // Clear damaged enemies for next cycle
            damagedEnemiesThisCycle.Clear();
        }
        
        // Lifetime expired
        Debug.Log("<color=yellow>CinderBloom lifetime expired - destroying</color>");
        Destroy(gameObject);
    }
    
    private void SpitFire()
    {
        if (fireChildren == null || fireChildren.Length == 0)
        {
            Debug.LogWarning("<color=yellow>CinderBloom: No fire children assigned!</color>");
            return;
        }
        
        // Play spit sound
        if (spitClip != null)
        {
            AudioSource.PlayClipAtPoint(spitClip, transform.position, spitVolume);
        }
        
        // Check each fire child for colliding enemies
        foreach (Transform fireChild in fireChildren)
        {
            if (fireChild == null) continue;
            
            Collider2D fireCollider = fireChild.GetComponent<Collider2D>();
            if (fireCollider == null) continue;
            
            // Get all overlapping colliders
            Collider2D[] hitColliders = new Collider2D[10];
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(enemyLayer);
            filter.useLayerMask = true;
            
            int count = fireCollider.OverlapCollider(filter, hitColliders);
            
            for (int i = 0; i < count; i++)
            {
                Collider2D hitCollider = hitColliders[i];
                if (hitCollider == null) continue;
                
                GameObject enemy = hitCollider.gameObject;
                
                // CRITICAL: Check if already damaged this cycle FIRST (prevents multiple fire children from hitting same enemy)
                if (damagedEnemiesThisCycle.Contains(enemy)) continue;
                
                // CRITICAL: Check if enough time has passed since last damage to THIS SPECIFIC ENEMY
                if (enemyLastDamageTimes.ContainsKey(enemy))
                {
                    float timeSinceLastDamage = GameStateManager.PauseSafeTime - enemyLastDamageTimes[enemy];
                    if (timeSinceLastDamage < spitInterval)
                    {
                        // Too soon to damage this enemy again
                        continue;
                    }
                }
                
                // CRITICAL: Mark as damaged IMMEDIATELY to prevent other fire children from hitting this enemy
                damagedEnemiesThisCycle.Add(enemy);
                
                IDamageable damageable = enemy.GetComponent<IDamageable>() ?? enemy.GetComponentInParent<IDamageable>();
                
                if (damageable != null && damageable.IsAlive)
                {
                    // Check if enemy is within damageable area
                    if (!OffscreenDamageChecker.CanTakeDamage(enemy.transform.position))
                    {
                        continue;
                    }
                    
                    Vector3 hitPoint = enemy.transform.position;
                    Vector3 hitNormal = (transform.position - hitPoint).normalized;
                    
                    // Use damage value that was already processed in Initialize (includes PlayerStats)
                    float baseDamageForEnemy = damage;
                    float finalDamage = baseDamageForEnemy;

                    Component damageableComponent = damageable as Component;
                    GameObject enemyObject = damageableComponent != null ? damageableComponent.gameObject : enemy;

                    if (cachedPlayerStats != null)
                    {
                        finalDamage = PlayerDamageHelper.ComputeProjectileDamage(cachedPlayerStats, enemyObject, baseDamageForEnemy, gameObject);
                    }

                    // Tag EnemyHealth so spit damage uses the correct Fire/Ice
                    // damage color in EnemyHealth.TakeDamage.
                    if (enemyObject != null)
                    {
                        EnemyHealth enemyHealth = enemyObject.GetComponent<EnemyHealth>() ?? enemyObject.GetComponentInParent<EnemyHealth>();
                        if (enemyHealth != null)
                        {
                            DamageNumberManager.DamageType damageType = projectileType == ProjectileType.Fire
                                ? DamageNumberManager.DamageType.Fire
                                : (projectileType == ProjectileType.Thunder || projectileType == ProjectileType.ThunderDisc
                                    ? DamageNumberManager.DamageType.Thunder
                                    : DamageNumberManager.DamageType.Ice);
                            enemyHealth.SetLastIncomingDamageType(damageType);
                        }
                    }

                    damageable.TakeDamage(finalDamage, hitPoint, hitNormal);
                    
                    // CRITICAL: Update last damage time for this specific enemy
                    enemyLastDamageTimes[enemy] = GameStateManager.PauseSafeTime;
                    
                    StatusController.TryApplyBurnFromProjectile(gameObject, enemy, hitPoint, finalDamage);
                    
                    SlowEffect slowEffect = GetComponent<SlowEffect>();
                    if (slowEffect != null)
                    {
                        slowEffect.TryApplySlow(enemy, hitPoint);
                    }
                    
                    StaticEffect staticEffect = GetComponent<StaticEffect>();
                    if (staticEffect != null)
                    {
                        staticEffect.TryApplyStatic(enemy, hitPoint);
                    }
                    
                    Debug.Log($"<color=orange>CinderBloom dealt {finalDamage} damage to {enemy.name} (last damage: {(enemyLastDamageTimes.ContainsKey(enemy) ? (GameStateManager.PauseSafeTime - enemyLastDamageTimes[enemy]).ToString("F2") : "never")}s ago)</color>");
                }
            }
        }
    }
    
    private bool IsOverlappingWithOtherBlooms(Vector3 testPosition)
    {
        CinderCryoBloom[] allBlooms = FindObjectsOfType<CinderCryoBloom>();
        
        float checkRadius = 1f;
        if (_collider2D != null)
        {
            if (_collider2D is CircleCollider2D circle)
            {
                checkRadius = circle.radius * transform.localScale.x;
            }
            else if (_collider2D is BoxCollider2D box)
            {
                checkRadius = Mathf.Max(box.size.x, box.size.y) * 0.5f * transform.localScale.x;
            }
            else if (_collider2D is CapsuleCollider2D capsule)
            {
                checkRadius = Mathf.Max(capsule.size.x, capsule.size.y) * 0.5f * transform.localScale.x;
            }
        }
        
        foreach (CinderCryoBloom otherBloom in allBlooms)
        {
            if (otherBloom == this) continue;
            
            float distance = Vector3.Distance(testPosition, otherBloom.transform.position);
            float minDistance = checkRadius * minDistanceBetweenBlooms;
            
            if (distance < minDistance)
            {
                return true;
            }
        }
        
        return false;
    }
    
    private void OnDrawGizmosSelected()
    {
        // Draw spawn area points A, B, C, D
        GameObject pointAObj = GameObject.FindGameObjectWithTag(pointATag);
        GameObject pointBObj = GameObject.FindGameObjectWithTag(pointBTag);
        GameObject pointCObj = GameObject.FindGameObjectWithTag(pointCTag);
        GameObject pointDObj = GameObject.FindGameObjectWithTag(pointDTag);
        
        if (pointAObj != null && pointBObj != null && pointCObj != null && pointDObj != null)
        {
            Vector3 posA = pointAObj.transform.position;
            Vector3 posB = pointBObj.transform.position;
            Vector3 posC = pointCObj.transform.position;
            Vector3 posD = pointDObj.transform.position;
            
            // Draw points
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(posA, 0.3f);
            Gizmos.DrawSphere(posB, 0.3f);
            Gizmos.DrawSphere(posC, 0.3f);
            Gizmos.DrawSphere(posD, 0.3f);
            
            // Draw lines
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(posA, posB);
            Gizmos.DrawLine(posB, posD);
            Gizmos.DrawLine(posD, posC);
            Gizmos.DrawLine(posC, posA);
            
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(posA + Vector3.up * 0.5f, "A (Top-Left)");
            UnityEditor.Handles.Label(posB + Vector3.up * 0.5f, "B (Top-Right)");
            UnityEditor.Handles.Label(posC + Vector3.down * 0.5f, "C (Bottom-Left)");
            UnityEditor.Handles.Label(posD + Vector3.down * 0.5f, "D (Bottom-Right)");
            #endif
        }
        
        // Draw taunt radius (always show when selected, not just when playing)
        if (enableTaunt)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
            Vector3 tauntCenter = transform.position + (Vector3)tauntOffset;
            Gizmos.DrawWireSphere(tauntCenter, tauntRadius);
            
            // Draw center point
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(tauntCenter, 0.2f);
        }
    }
    
    // IDamageable implementation
    public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (bloomHealth <= 0) return; // Invincible
        
        currentHealth -= amount;
        Debug.Log($"<color=orange>Cinderbloom took {amount} damage! Health: {currentHealth}/{bloomHealth}</color>");
        
        if (currentHealth <= 0)
        {
            OnBloomDestroyed();
        }
    }
    
    private void OnBloomDestroyed()
    {
        Debug.Log("<color=orange>Cinderbloom destroyed! Releasing taunted enemies...</color>");
        
        // Release all taunted enemies
        foreach (GameObject enemy in tauntedEnemies)
        {
            if (enemy != null)
            {
                CinderbloomTauntTarget tauntTarget = enemy.GetComponent<CinderbloomTauntTarget>();
                if (tauntTarget != null)
                {
                    Destroy(tauntTarget);
                }
            }
        }
        tauntedEnemies.Clear();
        
        // Stop taunt coroutine
        if (tauntCoroutine != null)
        {
            StopCoroutine(tauntCoroutine);
        }
        
        // Destroy bloom
        Destroy(gameObject);
    }
    
    private IEnumerator TauntNearbyEnemies()
    {
        while (isActive && IsAlive)
        {
            Vector2 tauntCenter = (Vector2)transform.position + tauntOffset;
            Collider2D[] enemies = Physics2D.OverlapCircleAll(tauntCenter, tauntRadius, enemyLayer);
            
            foreach (Collider2D enemyCol in enemies)
            {
                if (enemyCol == null || tauntedEnemies.Contains(enemyCol.gameObject)) continue;
                
                // Add taunt component to enemy
                CinderbloomTauntTarget tauntTarget = enemyCol.gameObject.GetComponent<CinderbloomTauntTarget>();
                if (tauntTarget == null)
                {
                    tauntTarget = enemyCol.gameObject.AddComponent<CinderbloomTauntTarget>();
                    tauntTarget.SetTarget(transform);
                    tauntedEnemies.Add(enemyCol.gameObject);
                    Debug.Log($"<color=orange>Cinderbloom taunted {enemyCol.gameObject.name}</color>");
                }
            }
            
            yield return GameStateManager.WaitForPauseSafeSeconds(tauntPulseInterval);
        }
    }
    
    public void ApplyInstantModifiers(CardModifierStats modifiers)
    {
        Debug.Log($"<color=lime>╔═══ CINDERCRYOBLOOM INSTANT MODIFIERS ═══╗</color>");
        float newLifetime = baseLifetimeSeconds + modifiers.lifetimeIncrease;
        if (newLifetime != lifetimeSeconds) { lifetimeSeconds = newLifetime; Debug.Log($"<color=lime>  Lifetime: {baseLifetimeSeconds:F2} + {modifiers.lifetimeIncrease:F2} = {lifetimeSeconds:F2}</color>"); }

        float enhancedDamageMult = 1f;
        ProjectileCards card = ProjectileCardModifiers.Instance != null
            ? ProjectileCardModifiers.Instance.GetCardFromProjectile(gameObject)
            : null;
        if (card != null && ProjectileCardLevelSystem.Instance != null)
        {
            int enhancedVariant = ProjectileCardLevelSystem.Instance.GetEnhancedVariant(card);
            if (enhancedVariant == 1)
            {
                enhancedDamageMult = enhancedDamageMultiplier;
            }
        }

        float newDamage = (baseDamage + modifiers.damageFlat) * modifiers.damageMultiplier * enhancedDamageMult;
        if (newDamage != damage) { damage = newDamage; Debug.Log($"<color=lime>  Damage: ({baseDamage:F2} + {modifiers.damageFlat:F2}) * {modifiers.damageMultiplier:F2}x * {enhancedDamageMult:F2}x = {damage:F2}</color>"); }
        if (modifiers.sizeMultiplier != 1f) { transform.localScale = baseScale * modifiers.sizeMultiplier; Debug.Log($"<color=lime>  Size: {baseScale} * {modifiers.sizeMultiplier:F2}x = {transform.localScale}</color>"); }
        Debug.Log($"<color=lime>╚═══════════════════════════════════════╝</color>");
    }
}
