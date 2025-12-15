using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class IceLancer : MonoBehaviour, IInstantModifiable
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
    [SerializeField] private ProjectileType projectileType = ProjectileType.Ice;

    [Header("Sequential Spawn Settings")]
    [Tooltip("Number of lances to spawn in sequence")]
    [SerializeField] private int lanceCount = 10;
    [Tooltip("Distance between each lance")]
    [SerializeField] private float lanceSpacing = 0.5f;
    [Tooltip("Delay between spawning each lance (seconds)")]
    [SerializeField] private float spawnDelay = 0.1f;
    [Tooltip("Lifetime of each lance (seconds)")]
    [SerializeField] private float lanceLifetime = 1f;
    [Tooltip("Lance visual prefab (spawned stationary)")]
    [SerializeField] private GameObject lancePrefab;
    
    [Header("Damage Cooldown")]
    [Tooltip("Minimum time between damage instances")]
    [SerializeField] private float damageCooldown = 0.1f;
    
    [Header("Collider Scaling")]
    [SerializeField] private float colliderSizeOffset = 0f;

    private Rigidbody2D _rigidbody2D;
    private float baseSpeed; private float baseLifetime; private float baseDamage; private Vector3 baseScale;
    private PlayerStats cachedPlayerStats;
    private float baseDamageAfterCards;
    private Collider2D _collider2D;
    private float lastDamageTime = -999f;
    private bool isSpawning = false;
    
    private static System.Collections.Generic.Dictionary<string, float> lastFireTimes = new System.Collections.Generic.Dictionary<string, float>();
    private string prefabKey;

    private void Awake()
    {
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _collider2D = GetComponent<Collider2D>();
        baseSpeed=speed; baseLifetime=lifetimeSeconds; baseDamage=damage; baseScale=transform.localScale;
        
        if (_rigidbody2D != null)
        {
            _rigidbody2D.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }
        
        if (_collider2D != null)
        {
            _collider2D.isTrigger = true;
        }
    }

    private void Start()
    {
        // IceLancer is just a controller, it doesn't move
        if (_rigidbody2D != null)
        {
            _rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
            _rigidbody2D.velocity = Vector2.zero;
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
            Debug.Log($"<color=cyan>IceLancer using modifiers from {card.cardName}</color>");
        }

        // Apply card modifiers (NO PlayerStats yet – we defer that per hit via PlayerDamageHelper)
        float finalSpeed = speed + modifiers.speedIncrease;
        float finalLifetime = lifetimeSeconds + modifiers.lifetimeIncrease;
        float finalCooldown = Mathf.Max(0.1f, cooldown * (1f - modifiers.cooldownReductionPercent / 100f));
        int finalManaCost = Mathf.Max(1, Mathf.CeilToInt(manaCost * (1f - modifiers.manaCostReduction)));
        damage += modifiers.damageFlat;

        // Cache PlayerStats and post-card base damage so we can apply full
        // projectile damage + favour pipeline per enemy hit.
        cachedPlayerStats = FindObjectOfType<PlayerStats>();
        baseDamageAfterCards = damage;

        // When this projectile is driven by an ACTIVE ProjectileCard (auto-fire system),
        // rely on AdvancedPlayerController's attack-speed cooldown instead of this
        // internal per-prefab cooldown gate.
        bool useInternalCooldown = (card == null || card.projectileSystem != ProjectileCards.ProjectileSystemType.Active);
        
        // Apply size multiplier
        if (modifiers.sizeMultiplier != 1f)
        {
            transform.localScale *= modifiers.sizeMultiplier;
            ColliderScaler.ScaleCollider(_collider2D, modifiers.sizeMultiplier, colliderSizeOffset);
        }
        
        // Do NOT call PlayerStats.CalculateDamage here; PlayerDamageHelper
        // will handle stats + favours per hit using baseDamageAfterCards.

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
        prefabKey = "IceLancer";
        
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
        
        // Don't set velocity - IceLancer spawns lances sequentially
        Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        
        // Start spawning lances in sequence
        if (!isSpawning)
        {
            StartCoroutine(SpawnLancesSequentially(dir, target));
        }
        
        // Destroy controller after all lances are spawned
        Destroy(gameObject, spawnDelay * lanceCount + lanceLifetime + 1f);
    }

    private IEnumerator SpawnLancesSequentially(Vector2 direction, Transform target)
    {
        isSpawning = true;
        Vector3 startPos = transform.position;
        
        // Calculate target position
        Vector3 targetPos = target != null ? target.position : startPos + (Vector3)(direction * lanceCount * lanceSpacing);
        
        // Calculate direction to target
        Vector3 spawnDir = (targetPos - startPos).normalized;
        float angle = Mathf.Atan2(spawnDir.y, spawnDir.x) * Mathf.Rad2Deg;
        Quaternion rotation = Quaternion.Euler(0f, 0f, angle);
        
        Debug.Log($"<color=cyan>IceLancer: Spawning {lanceCount} lances towards {targetPos}</color>");
        
        // Spawn lances one by one
        for (int i = 0; i < lanceCount; i++)
        {
            // Calculate spawn position
            Vector3 spawnPos = startPos + spawnDir * (i * lanceSpacing);
            
            // Spawn lance
            if (lancePrefab != null)
            {
                GameObject lance = Instantiate(lancePrefab, spawnPos, rotation);
                
                // Make lance stationary
                Rigidbody2D lanceRb = lance.GetComponent<Rigidbody2D>();
                if (lanceRb != null)
                {
                    lanceRb.bodyType = RigidbodyType2D.Kinematic;
                    lanceRb.velocity = Vector2.zero;
                }
                
                // Add collider for damage
                Collider2D lanceCol = lance.GetComponent<Collider2D>();
                if (lanceCol != null)
                {
                    lanceCol.isTrigger = true;
                }
                
                // Add damage component
                IceLanceDamage damageComp = lance.AddComponent<IceLanceDamage>();
                damageComp.damage = damage;
                damageComp.enemyLayer = enemyLayer;
                
                // Destroy lance after lifetime (first spawned = first destroyed)
                float destroyTime = lanceLifetime + (i * spawnDelay);
                Destroy(lance, destroyTime);
                
                Debug.Log($"<color=cyan>IceLancer: Spawned lance {i + 1}/{lanceCount} at {spawnPos}, will destroy in {destroyTime:F2}s</color>");
            }
            
            // Wait before spawning next lance
            if (i < lanceCount - 1)
            {
                yield return new WaitForSeconds(spawnDelay);
            }
        }
        
        isSpawning = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (((1 << other.gameObject.layer) & enemyLayer) != 0)
        {
            IDamageable damageable = other.GetComponent<IDamageable>() ?? other.GetComponentInParent<IDamageable>();
            
            if (damageable != null && damageable.IsAlive)
            {
                // Check damage cooldown
                if (Time.time - lastDamageTime < damageCooldown)
                {
                    return;
                }
                
                Vector3 hitPoint = other.ClosestPoint(transform.position);
                Vector3 hitNormal = (transform.position - hitPoint).normalized;

                float baseForHit = baseDamageAfterCards > 0f ? baseDamageAfterCards : damage;
                float finalDamage = baseForHit;

                Component damageableComponent = damageable as Component;
                GameObject enemyObject = damageableComponent != null ? damageableComponent.gameObject : other.gameObject;

                if (cachedPlayerStats != null)
                {
                    finalDamage = PlayerDamageHelper.ComputeProjectileDamage(cachedPlayerStats, enemyObject, baseForHit, gameObject);
                }

                // Tag EnemyHealth so EnemyHealth.TakeDamage renders this hit with
                // the ice damage color instead of the default fire fallback.
                if (enemyObject != null)
                {
                    EnemyHealth enemyHealth = enemyObject.GetComponent<EnemyHealth>() ?? enemyObject.GetComponentInParent<EnemyHealth>();
                    if (enemyHealth != null)
                    {
                        enemyHealth.SetLastIncomingDamageType(DamageNumberManager.DamageType.Ice);
                    }
                }

                damageable.TakeDamage(finalDamage, hitPoint, hitNormal);
                lastDamageTime = Time.time;
                
                // Check for pierce
                ProjectilePiercing piercing = GetComponent<ProjectilePiercing>();
                if (piercing != null)
                {
                    // ProjectilePiercing.OnEnemyHit implements the semantics:
                    // pierceCount = 1 → hit 2 enemies (pierce 1st, destroy on 2nd), etc.
                    bool shouldContinue = piercing.OnEnemyHit(other.gameObject);
                    if (!shouldContinue)
                    {
                        Destroy(gameObject);
                    }
                }
                else
                {
                    // No pierce - destroy on first hit
                    Destroy(gameObject);
                }
            }
        }
    }
    public void ApplyInstantModifiers(CardModifierStats mods) { Debug.Log($"<color=lime>╔ ICELANCER ╗</color>"); float ns=baseSpeed+mods.speedIncrease; if(ns!=speed){speed=ns; if(_rigidbody2D!=null)_rigidbody2D.velocity=_rigidbody2D.velocity.normalized*speed; Debug.Log($"<color=lime>Speed:{baseSpeed:F2}+{mods.speedIncrease:F2}={speed:F2}</color>");} float nl=baseLifetime+mods.lifetimeIncrease; if(nl!=lifetimeSeconds){lifetimeSeconds=nl; Debug.Log($"<color=lime>Lifetime:{baseLifetime:F2}+{mods.lifetimeIncrease:F2}={lifetimeSeconds:F2}</color>");} float nd=baseDamage*mods.damageMultiplier; if(nd!=damage){damage=nd; Debug.Log($"<color=lime>Damage:{baseDamage:F2}*{mods.damageMultiplier:F2}x={damage:F2}</color>");} if(mods.sizeMultiplier!=1f){transform.localScale=baseScale*mods.sizeMultiplier; Debug.Log($"<color=lime>Size:{baseScale}*{mods.sizeMultiplier:F2}x={transform.localScale}</color>");} Debug.Log($"<color=lime>╚═══════════════════════════════════════╝</color>"); }

    public float GetCurrentDamage()
    {
        return damage;
    }
}
