using UnityEngine;

public class TornadoController : MonoBehaviour, IInstantModifiable
{
    // Static properties for global configuration
    private static float lastCastTime = -999f;

    [Header("Mana & Cooldown")]
    [SerializeField] private int manaCost = 25;
    [SerializeField] private float cooldown = 1.5f;
    
    // Static accessors for shared cooldown
    public static int ManaCost { get; private set; } = 25;
    public static float Cooldown { get; private set; } = 1.5f;

    [Header("Movement Settings")]
    [SerializeField] private float speed = 10f;

    [Header("Type Settings")]
    public bool isFireTornado;

    [Header("Damage Settings")]
    public float damage = 15f; // Single damage amount per instance
    public float damageInterval = 0.5f; // Time between damage instances (e.g., 0.2 or 1 second)
    [SerializeField] private LayerMask enemyLayer;

    [Header("Duration Settings")]
    [SerializeField] private float lifetime = 5f;

    [Header("Collider Scaling")]
    [Tooltip("Offset for collider size relative to visual size (0 = same as visual, -0.2 = 20% smaller, 0.2 = 20% larger)")]
    [SerializeField] private float colliderSizeOffset = 0f;

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private GameObject impactEffect;
    [SerializeField] private float effectDuration = 1.5f;

    private PlayerStats cachedPlayerStats;
    private Vector3 targetPosition;
    private bool targetSet = false;
    private float baseSpeed; private float baseLifetime; private float baseDamage; private Vector3 baseScale;
    private bool reachedTarget = false;
    private float lifeTimer = 0f;
    private System.Collections.Generic.Dictionary<int, float> lastDamageTimes = new System.Collections.Generic.Dictionary<int, float>(); // Use instanceID instead of Collider2D reference
    private Collider2D triggerCollider; // Can be Circle or Capsule

    private void Awake()
    {
        baseSpeed=speed; baseLifetime=lifetime; baseDamage=damage; baseScale=transform.localScale;
        // Sync instance values to static properties
        ManaCost = manaCost;
        Cooldown = cooldown;
    }

    private void Start()
    {
        UpdateAnimator();
        Debug.Log($"TornadoController started - isFireTornado: {isFireTornado}");
        
        // Get card-specific modifiers
        ProjectileCards card = ProjectileCardModifiers.Instance.GetCardFromProjectile(gameObject);
        CardModifierStats modifiers = new CardModifierStats(); // Default values
        
        if (card != null)
        {
            modifiers = ProjectileCardModifiers.Instance.GetCardModifiers(card);
            Debug.Log($"<color=yellow>TornadoController using modifiers from {card.cardName}</color>");
        }

        // Apply card modifiers
        speed += modifiers.speedIncrease; // RAW value
        lifetime += modifiers.lifetimeIncrease; // RAW seconds
        damage += modifiers.damageFlat; // FLAT damage bonus per hit
        
        // Apply size multiplier
        if (modifiers.sizeMultiplier != 1f)
        {
            transform.localScale *= modifiers.sizeMultiplier;
        }
        
        Debug.Log($"<color=green>Tornado Modifiers Applied: Speed=+{modifiers.speedIncrease:F2}, Size={modifiers.sizeMultiplier:F2}x, DamageFlat=+{modifiers.damageFlat:F1}, Lifetime=+{modifiers.lifetimeIncrease:F2}s</color>");
        
        // Still get PlayerStats for base damage calculation
        PlayerStats stats = FindObjectOfType<PlayerStats>();
        if (stats != null)
        {
            cachedPlayerStats = stats;
        }
        
        // Add or get Rigidbody2D (required for trigger detection)
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic; // Kinematic so it doesn't fall
            rb.gravityScale = 0f;
            Debug.Log("Added Rigidbody2D to tornado");
        }
        
        // Check if collider already exists (from prefab)
        triggerCollider = GetComponent<Collider2D>();
        
        if (triggerCollider == null)
        {
            // Add circle collider if none exists
            CircleCollider2D circleCollider = gameObject.AddComponent<CircleCollider2D>();
            circleCollider.radius = 2.5f;
            circleCollider.isTrigger = true;
            circleCollider.enabled = true;
            triggerCollider = circleCollider;
            Debug.Log("<color=yellow>Created new CircleCollider2D for tornado</color>");
        }
        else
        {
            // Use existing collider from prefab
            triggerCollider.isTrigger = true;
            triggerCollider.enabled = true;
            Debug.Log($"<color=yellow>Using existing {triggerCollider.GetType().Name} from prefab</color>");
        }
        
        // Scale collider with size multiplier and offset
        if (modifiers.sizeMultiplier != 1f && triggerCollider != null)
        {
            ColliderScaler.ScaleCollider(triggerCollider, modifiers.sizeMultiplier, colliderSizeOffset);
        }
        
        // Log collider info
        if (triggerCollider is CircleCollider2D circle)
        {
            Debug.Log($"<color=yellow>Tornado initialized: CircleCollider radius={circle.radius}, isTrigger={triggerCollider.isTrigger}</color>");
        }
        else if (triggerCollider is CapsuleCollider2D capsule)
        {
            Debug.Log($"<color=yellow>Tornado initialized: CapsuleCollider size={capsule.size}, direction={capsule.direction}, isTrigger={triggerCollider.isTrigger}</color>");
        }
        else if (triggerCollider is BoxCollider2D box)
        {
            Debug.Log($"<color=yellow>Tornado initialized: BoxCollider size={box.size}, isTrigger={triggerCollider.isTrigger}</color>");
        }
    }

    private void Update()
    {
        lifeTimer += GameStateManager.GetPauseSafeDeltaTime();
        if (lifeTimer >= lifetime)
        {
            Debug.Log("Tornado lifetime expired");
            Destroy(gameObject);
            return;
        }

        if (!targetSet) return;

        if (!reachedTarget)
        {
            Vector3 newPosition = Vector3.MoveTowards(transform.position, targetPosition, speed * GameStateManager.GetPauseSafeDeltaTime());
            if (Vector3.Distance(newPosition, targetPosition) < 0.01f)
            {
                transform.position = targetPosition;
                ReachedTarget();
            }
            else
            {
                transform.position = newPosition;
            }
        }
    }

    private void ReachedTarget()
    {
        reachedTarget = true;
        Debug.Log("<color=cyan>Tornado reached target - stopped moving, continuing to deal damage</color>");

        // DON'T disable rb.simulated - it prevents trigger detection!
        // Just stop the velocity instead
        if (TryGetComponent<Rigidbody2D>(out var rb))
        {
            rb.velocity = Vector2.zero;
            // Keep rb.simulated = true so triggers continue to work!
            Debug.Log("<color=green>Tornado stopped, Rigidbody2D still simulated for trigger detection</color>");
        }

        if (impactEffect != null)
        {
            GameObject effect = Instantiate(impactEffect, transform.position, Quaternion.identity);
            PauseSafeSelfDestruct.Schedule(effect, effectDuration);
        }
        
        // Trigger collider remains active to continue dealing damage
        Debug.Log($"<color=green>Tornado ready to damage enemies at position {transform.position}</color>");
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Transform t = other != null ? other.transform : null;
        while (t != null)
        {
            if (t.name == "ClickHitbox")
            {
                return;
            }
            t = t.parent;
        }

        Debug.Log($"<color=yellow>Tornado OnTriggerEnter2D: {other.gameObject.name}, layer: {other.gameObject.layer}, stationary: {reachedTarget}</color>");
        ApplyDamage(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        Transform t = other != null ? other.transform : null;
        while (t != null)
        {
            if (t.name == "ClickHitbox")
            {
                return;
            }
            t = t.parent;
        }

        // Only log occasionally to avoid spam
        if (Time.frameCount % 30 == 0) // Log every 30 frames (~0.5s)
        {
            Debug.Log($"<color=yellow>Tornado OnTriggerStay2D: {other.gameObject.name}, stationary: {reachedTarget}</color>");
        }
        ApplyDamage(other);
    }

    private void ApplyDamage(Collider2D other)
    {
        // Safety check: Ensure our collider hasn't been modified
        if (triggerCollider != null && triggerCollider.enabled == false)
        {
            Debug.LogWarning("<color=red>Tornado collider was disabled! Re-enabling...</color>");
            triggerCollider.enabled = true;
        }
        
        // Check if it's an enemy
        if (((1 << other.gameObject.layer) & enemyLayer) == 0)
        {
            return; // Not an enemy, skip silently
        }
        
        // Additional check: Calculate actual distance to ensure we're within range
        float distance = Vector2.Distance(transform.position, other.transform.position);
        float maxRange = 3f; // Maximum range for damage (adjust based on your collider size)
        
        if (triggerCollider is CircleCollider2D circle)
        {
            maxRange = circle.radius * Mathf.Max(transform.localScale.x, transform.localScale.y);
        }
        else if (triggerCollider is CapsuleCollider2D capsule)
        {
            maxRange = Mathf.Max(capsule.size.x, capsule.size.y) * 0.5f * Mathf.Max(transform.localScale.x, transform.localScale.y);
        }
        
        if (distance > maxRange)
        {
            Debug.LogWarning($"<color=orange>Enemy {other.name} triggered but is too far ({distance:F2} > {maxRange:F2}). Ignoring.</color>");
            return;
        }

        // Check if enough time has passed since last damage TO THIS SPECIFIC ENEMY
        int enemyID = other.GetInstanceID();
        if (lastDamageTimes.ContainsKey(enemyID))
        {
            if (GameStateManager.PauseSafeTime - lastDamageTimes[enemyID] < damageInterval)
            {
                return; // Too soon to damage this enemy again
            }
        }

        IDamageable damageable = other.GetComponent<IDamageable>() ?? other.GetComponentInParent<IDamageable>();
        
        // Check rigidbody state
        Rigidbody2D enemyRb = other.GetComponent<Rigidbody2D>();
        if (enemyRb != null && enemyRb.bodyType != RigidbodyType2D.Kinematic)
        {
            Debug.LogWarning($"<color=red>⚠️ [KNOCKBACK] {other.gameObject.name} has BodyType: {enemyRb.bodyType} (should be Kinematic!)</color>");
        }
        
        if (damageable != null && damageable.IsAlive)
        {
            // Check if enemy is within damageable area (on-screen or slightly offscreen)
            if (!OffscreenDamageChecker.CanTakeDamage(other.transform.position))
            {
                Debug.Log($"<color=yellow>Tornado: Enemy {other.gameObject.name} too far offscreen, no damage dealt</color>");
                return;
            }
            
            Vector3 hitPoint = other.ClosestPoint(transform.position);
            Vector3 hitNormal = (transform.position - hitPoint).normalized;

            // Use damage value that was already processed in Start (includes card modifiers)
            float damageBeforeStats = damage;

            // Route through central PlayerDamageHelper so PlayerStats and
            // favour effects (OnBeforeDealDamage) are applied consistently.
            float finalDamage = damageBeforeStats;
            Component damageableComponent = damageable as Component;
            GameObject enemyObject = damageableComponent != null ? damageableComponent.gameObject : null;
            if (cachedPlayerStats != null)
            {
                finalDamage = PlayerDamageHelper.ComputeProjectileDamage(cachedPlayerStats, enemyObject, damageBeforeStats, gameObject);
            }

            // Tag EnemyHealth so EnemyHealth.TakeDamage renders the numeric
            // popup using the correct Fire/Ice color instead of the default
            // fire fallback.
            if (enemyObject != null)
            {
                EnemyHealth enemyHealth = enemyObject.GetComponent<EnemyHealth>() ?? enemyObject.GetComponentInParent<EnemyHealth>();
                if (enemyHealth != null)
                {
                    DamageNumberManager.DamageType damageType = isFireTornado
                        ? DamageNumberManager.DamageType.Fire
                        : DamageNumberManager.DamageType.Ice;
                    enemyHealth.SetLastIncomingDamageType(damageType);
                }
            }

            damageable.TakeDamage(finalDamage, hitPoint, hitNormal);
            
            // Update last damage time for THIS SPECIFIC ENEMY
            lastDamageTimes[enemyID] = GameStateManager.PauseSafeTime;

            // Apply status effects using shared BurnEffect/SlowEffect components
            StatusController.TryApplyBurnFromProjectile(gameObject, other.gameObject, hitPoint, finalDamage);

            SlowEffect slowEffect = GetComponent<SlowEffect>();
            if (slowEffect != null)
            {
                slowEffect.TryApplySlow(other.gameObject, hitPoint);
            }

            StaticEffect staticEffect = GetComponent<StaticEffect>();
            if (staticEffect != null)
            {
                staticEffect.TryApplyStatic(other.gameObject, hitPoint);
            }
        }
        else if (damageable != null && !damageable.IsAlive)
        {
            Debug.Log($"<color=yellow>[TORNADO] Hit dead enemy {other.gameObject.name}, skipping damage</color>");
        }
    }

    public void SetTargetPosition(Vector3 target)
    {
        targetPosition = new Vector3(target.x, target.y, 0f);
        targetSet = true;
        UpdateAnimator();
        Debug.Log($"Target set to {targetPosition}, isFireTornado: {isFireTornado}");
    }

    public void SwapType()
    {
        isFireTornado = !isFireTornado;
        UpdateAnimator();
        Debug.Log($"Tornado type swapped to: {(isFireTornado ? "Fire" : "Ice")}");
    }

    private void UpdateAnimator()
    {
        if (animator != null)
        {
            animator.SetBool("IsFire", isFireTornado);
        }
    }

    public static bool CanCast(PlayerMana playerMana)
    {
        if (GameStateManager.PauseSafeTime - lastCastTime < Cooldown) return false;
        return true;
    }

    public static void RecordCast() => lastCastTime = GameStateManager.PauseSafeTime;

    public static void ApplyBossCooldownReduction(float reductionPercent)
    {
        if (reductionPercent <= 0f)
        {
            return;
        }

        float baseCooldown = Cooldown;
        float reducedCooldown = baseCooldown * (1f - reductionPercent);
        Cooldown = Mathf.Max(0.1f, reducedCooldown);
        lastCastTime = GameStateManager.PauseSafeTime;

        Debug.Log($"<color=cyan>[Tornado] Boss cooldown reduction applied: base {baseCooldown:F2}s -> {Cooldown:F2}s (reduction {reductionPercent * 100f:F0}%)</color>");
    }

    public void ApplyInstantModifiers(CardModifierStats mods) { Debug.Log($"<color=lime>╔ TORNADO ╗</color>"); float ns=baseSpeed+mods.speedIncrease; if(ns!=speed){speed=ns; Debug.Log($"<color=lime>Speed:{baseSpeed:F2}+{mods.speedIncrease:F2}={speed:F2}</color>");} float nl=baseLifetime+mods.lifetimeIncrease; if(nl!=lifetime){lifetime=nl; Debug.Log($"<color=lime>Lifetime:{baseLifetime:F2}+{mods.lifetimeIncrease:F2}={lifetime:F2}</color>");} float nd=baseDamage+mods.damageFlat; if(nd!=damage){damage=nd; Debug.Log($"<color=lime>Damage:{baseDamage:F2}+{mods.damageFlat:F2}={damage:F2}</color>");} if(mods.sizeMultiplier!=1f){transform.localScale=baseScale*mods.sizeMultiplier; Debug.Log($"<color=lime>Size:{baseScale}*{mods.sizeMultiplier:F2}x={transform.localScale}</color>");} Debug.Log($"<color=lime>╚═══════════════════════════════════════╝</color>"); }
}