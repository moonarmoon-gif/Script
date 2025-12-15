using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class FireBomb : MonoBehaviour
{
    [Header("Motion")]
    [SerializeField] private float speed = 15f;
    [SerializeField] private float lifetimeSeconds = 5f;
    [SerializeField] private Vector2 spawnOffset = Vector2.zero;

    [Header("Damage Settings")]
    [SerializeField] private float damage = 20f;
    [SerializeField] private int manaCost = 10;
    [SerializeField] private float cooldown = 0.5f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private ProjectileType projectileType = ProjectileType.Fire;

    [Header("Explosion Settings")]
    [Tooltip("Radius of AOE explosion")]
    [SerializeField] private float explosionRadius = 3f;
    [Tooltip("Offset for explosion center")]
    [SerializeField] private Vector2 explosionOffset = Vector2.zero;
    [Tooltip("Explosion VFX prefab")]
    [SerializeField] private GameObject explosionEffectPrefab;
    [Tooltip("Explosion sound")]
    [SerializeField] private AudioClip explosionClip;
    [Range(0f, 1f)][SerializeField] private float explosionVolume = 1f;

    [Header("Burn Effect")]
    [Tooltip("Chance to apply burn effect (0-1)")]
    [SerializeField] private float burnChance = 0.2f;
    [Tooltip("Burn damage as percent of total damage (0.1 = 10%)")]
    [SerializeField] private float burnDamagePercent = 0.1f;
    [Tooltip("Burn tick interval in seconds")]
    [SerializeField] private float burnTickInterval = 0.5f;
    [Tooltip("Burn duration in seconds")]
    [SerializeField] private float burnDuration = 5f;

    [Header("Damage Cooldown")]
    [Tooltip("Minimum time between damage instances")]
    [SerializeField] private float damageCooldown = 0.1f;

    [Header("Collider Scaling")]
    [SerializeField] private float colliderSizeOffset = 0f;

    private Rigidbody2D _rigidbody2D;
    private Collider2D _collider2D;
    private float lastDamageTime = -999f;
    private bool hasExploded = false;
    
    private static System.Collections.Generic.Dictionary<string, float> lastFireTimes = new System.Collections.Generic.Dictionary<string, float>();
    private string prefabKey;

    private void Awake()
    {
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _collider2D = GetComponent<Collider2D>();
        
        if (_rigidbody2D != null)
        {
            _rigidbody2D.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }
        
        if (_collider2D != null)
        {
            _collider2D.isTrigger = true;
        }
    }

    public void Launch(Vector2 direction, Transform target, Collider2D colliderToIgnore, PlayerMana playerMana = null)
    {
        if (_rigidbody2D == null)
        {
            Destroy(gameObject);
            return;
        }

        // Get card-specific modifiers
        ProjectileCards card = ProjectileCardModifiers.Instance.GetCardFromProjectile(gameObject);
        CardModifierStats modifiers = new CardModifierStats();
        
        if (card != null)
        {
            modifiers = ProjectileCardModifiers.Instance.GetCardModifiers(card);
            Debug.Log($"<color=orange>FireBomb using modifiers from {card.cardName}</color>");
        }

        // Apply modifiers
        float finalSpeed = speed + modifiers.speedIncrease;
        float finalLifetime = lifetimeSeconds + modifiers.lifetimeIncrease;
        float finalCooldown = Mathf.Max(0.1f, cooldown * (1f - modifiers.cooldownReductionPercent / 100f));
        int finalManaCost = Mathf.Max(1, Mathf.CeilToInt(manaCost * (1f - modifiers.manaCostReduction)));
        damage += modifiers.damageFlat;

        // When this projectile is driven by an ACTIVE ProjectileCard (auto-fire system),
        // rely on AdvancedPlayerController's attack-speed cooldown instead of this
        // internal per-prefab cooldown gate.
        bool useInternalCooldown = (card == null || card.projectileSystem != ProjectileCards.ProjectileSystemType.Active);
        
        // Apply size multiplier (affects explosion radius too)
        if (modifiers.sizeMultiplier != 1f)
        {
            transform.localScale *= modifiers.sizeMultiplier;
            explosionRadius *= modifiers.sizeMultiplier;
            ColliderScaler.ScaleCollider(_collider2D, modifiers.sizeMultiplier, colliderSizeOffset);
        }

        // Apply PlayerStats damage
        PlayerStats stats = FindObjectOfType<PlayerStats>();
        if (stats != null)
        {
            damage = stats.CalculateDamage(damage, true);
        }

        // Allow the global "enhanced first spawn" reduction system to bypass this
        // internal cooldown gate exactly once for PASSIVE projectile cards.
        bool bypassEnhancedFirstSpawnCooldown = false;
        if (useInternalCooldown && card != null && card.applyEnhancedFirstSpawnReduction && card.pendingEnhancedFirstSpawn)
        {
            if (card.projectileSystem == ProjectileCards.ProjectileSystemType.Passive)
            {
                bypassEnhancedFirstSpawnCooldown = true;
                card.pendingEnhancedFirstSpawn = false;
            }
        }
        
        // Generate key
        prefabKey = "FireBomb";
        
        // Check cooldown only when not driven by ACTIVE projectile card attack-speed system
        if (useInternalCooldown && !bypassEnhancedFirstSpawnCooldown && lastFireTimes.ContainsKey(prefabKey))
        {
            if (Time.time - lastFireTimes[prefabKey] < finalCooldown)
            {
                Destroy(gameObject);
                return;
            }
        }
        
        // Check mana (always)
        if (playerMana != null && !playerMana.Spend(finalManaCost))
        {
            Destroy(gameObject);
            return;
        }
        
        if (useInternalCooldown)
        {
            lastFireTimes[prefabKey] = Time.time;
        }
        
        if (_collider2D != null && colliderToIgnore != null)
        {
            Physics2D.IgnoreCollision(_collider2D, colliderToIgnore, true);
        }
        
        // Set velocity towards target
        Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        _rigidbody2D.velocity = dir * finalSpeed;
        
        // Rotate to face direction
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
        
        Destroy(gameObject, finalLifetime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasExploded) return;
        
        if (((1 << other.gameObject.layer) & enemyLayer) != 0)
        {
            IDamageable damageable = other.GetComponent<IDamageable>() ?? other.GetComponentInParent<IDamageable>();
            
            if (damageable != null && damageable.IsAlive)
            {
                // Explode on first enemy hit
                Explode();
            }
        }
    }

    private void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;
        
        Vector2 explosionCenter = (Vector2)transform.position + explosionOffset;
        
        // Spawn explosion VFX
        if (explosionEffectPrefab != null)
        {
            GameObject vfx = Instantiate(explosionEffectPrefab, explosionCenter, Quaternion.identity);
            Destroy(vfx, 2f);
        }
        
        // Play explosion sound
        if (explosionClip != null)
        {
            AudioSource.PlayClipAtPoint(explosionClip, explosionCenter, explosionVolume);
        }
        
        // Deal AOE damage - track damaged enemies to prevent multiple hits
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(explosionCenter, explosionRadius, enemyLayer);
        System.Collections.Generic.HashSet<GameObject> damagedEnemies = new System.Collections.Generic.HashSet<GameObject>();
        
        Debug.Log($"<color=orange>FireBomb exploded! Hit {hitEnemies.Length} enemies in radius {explosionRadius}</color>");
        
        foreach (Collider2D enemyCollider in hitEnemies)
        {
            if (enemyCollider == null) continue;
            
            // Skip if already damaged this enemy
            if (damagedEnemies.Contains(enemyCollider.gameObject)) continue;
            
            IDamageable damageable = enemyCollider.GetComponent<IDamageable>() ?? enemyCollider.GetComponentInParent<IDamageable>();
            
            if (damageable != null && damageable.IsAlive)
            {
                Vector3 hitPoint = enemyCollider.ClosestPoint(explosionCenter);
                Vector3 hitNormal = (explosionCenter - (Vector2)hitPoint).normalized;

                float finalDamage = damage;

                // Apply favour-based outgoing damage modifiers so FireBomb
                // respects the same damage pipeline as other projectiles.
                PlayerStats stats = FindObjectOfType<PlayerStats>();
                if (stats != null)
                {
                    FavourEffectManager favourManager = stats.GetComponent<FavourEffectManager>();
                    if (favourManager != null)
                    {
                        Component damageableComponent = damageable as Component;
                        GameObject enemyObject = damageableComponent != null ? damageableComponent.gameObject : null;
                        if (enemyObject != null)
                        {
                            favourManager.NotifyBeforeDealDamage(enemyObject, ref finalDamage);

                            // Tag EnemyHealth so EnemyHealth.TakeDamage renders the
                            // explosion using the fire damage color.
                            EnemyHealth enemyHealth = enemyObject.GetComponent<EnemyHealth>() ?? enemyObject.GetComponentInParent<EnemyHealth>();
                            if (enemyHealth != null)
                            {
                                enemyHealth.SetLastIncomingDamageType(DamageNumberManager.DamageType.Fire);
                            }
                        }
                    }
                }

                damageable.TakeDamage(finalDamage, hitPoint, hitNormal);
                damagedEnemies.Add(enemyCollider.gameObject);
                
                // Apply burn effect
                if (Random.value <= burnChance)
                {
                    StatusEffectManager statusManager = enemyCollider.GetComponent<StatusEffectManager>();
                    if (statusManager == null)
                    {
                        statusManager = enemyCollider.gameObject.AddComponent<StatusEffectManager>();
                    }
                    statusManager.ApplyBurn(damage, burnDamagePercent, burnTickInterval, burnDuration);
                    Debug.Log($"<color=orange>FireBomb applied BURN to {enemyCollider.gameObject.name} ({burnDamagePercent * 100}% damage every {burnTickInterval}s for {burnDuration}s)</color>");
                }
            }
        }
        
        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        // Draw explosion radius in editor
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f); // Orange transparent
        Vector2 explosionCenter = (Vector2)transform.position + explosionOffset;
        Gizmos.DrawSphere(explosionCenter, explosionRadius);
        
        // Draw explosion center
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(explosionCenter, 0.1f);
    }

    public float GetCurrentDamage()
    {
        return damage;
    }
}
