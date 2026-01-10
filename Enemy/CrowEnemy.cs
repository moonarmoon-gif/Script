using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CapsuleCollider2D))]
[RequireComponent(typeof(EnemyHealth))]
public class CrowEnemy : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 2f;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float stopDistance = 1.4f; // Stops moving when this close
    [SerializeField] private float attackAnimSpeed = 1.0f;
    [SerializeField] private float attackCooldown = 1.0f;
    [SerializeField] private float attackDuration = 0.5f;
    [SerializeField] private float deathCleanupDelay = 0.7f;
    [Tooltip("Duration of fade out effect on death (seconds)")]
    [SerializeField] private float deathFadeOutDuration = 0.5f;

    [Header("Damage Settings")]
    [SerializeField] private float attackDamage = 15f;
    [Tooltip("Delay before FIRST damage instance (seconds)")]
    [SerializeField] private float firstAttackDamageDelay = 0.2f;
    [Tooltip("Delay between subsequent damage instances (seconds)")]
    [SerializeField] private float restAttackDamageDelay = 0.15f;
    [Tooltip("Number of damage instances per attack (1 = normal, 2+ = multiple hits)")]
    [SerializeField] private int damageInstancesPerAttack = 1;

    [SerializeField] private float attackDamageV2 = -1f;
    [SerializeField] private float firstAttackDamageDelayV2 = -1f;
    [SerializeField] private float restAttackDamageDelayV2 = -1f;
    [SerializeField] private int damageInstancesPerAttackV2 = -1;
    
    [Header("Knockback Settings")]
    [Tooltip("Knockback force when hit by projectiles")]
    public float knockbackIntensity = 5f;
    [Tooltip("How long knockback lasts")]
    public float knockbackDuration = 0.2f;

    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Animator animator;
    [SerializeField] private CapsuleCollider2D capsuleCollider;

    private EnemyHealth health;
    private StatusController statusController;
    private IDamageable playerDamageable;
    private bool isDead;
    private bool isAttacking;
    private bool attackOnCooldown;
    private bool hasDealtDamageThisAttack = false; // Prevent multiple damage in single attack
    private Coroutine attackRoutine;
    private Vector2 knockbackVelocity = Vector2.zero;
    private float knockbackEndTime = 0f;

    private int attackActionToken = 0;

    void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (animator == null) animator = GetComponent<Animator>();
        if (capsuleCollider == null) capsuleCollider = GetComponent<CapsuleCollider2D>();
        health = GetComponent<EnemyHealth>();
        statusController = GetComponent<StatusController>();

        // Phantom settings - only collide with Projectiles and Player
        capsuleCollider.isTrigger = false; // Regular collider for projectiles
        Physics2D.IgnoreLayerCollision(gameObject.layer, LayerMask.NameToLayer("Default"), true);
        Physics2D.IgnoreLayerCollision(gameObject.layer, LayerMask.NameToLayer("Enemy"), true);
        
        // No collider modifications - let Unity handle it

        if (AdvancedPlayerController.Instance != null)
        {
            playerDamageable = AdvancedPlayerController.Instance.GetComponent<IDamageable>();
            // Subscribe to player death
            AdvancedPlayerController.Instance.GetComponent<PlayerHealth>().OnDeath += OnPlayerDeath;
        }

        if (attackDamageV2 < 0f)
        {
            attackDamageV2 = attackDamage;
        }
        if (firstAttackDamageDelayV2 < 0f)
        {
            firstAttackDamageDelayV2 = firstAttackDamageDelay;
        }
        if (restAttackDamageDelayV2 < 0f)
        {
            restAttackDamageDelayV2 = restAttackDamageDelay;
        }
        if (damageInstancesPerAttackV2 < 0)
        {
            damageInstancesPerAttackV2 = damageInstancesPerAttack;
        }

        float referenceX = transform.position.x;
        if (AdvancedPlayerController.Instance != null)
        {
            referenceX = AdvancedPlayerController.Instance.transform.position.x;
        }
        else if (Camera.main != null)
        {
            referenceX = Camera.main.transform.position.x;
        }

        spriteRenderer.flipX = transform.position.x >= referenceX;
    }

    private int BeginAttackAction()
    {
        attackActionToken++;
        return attackActionToken;
    }

    private void CancelAttackAction()
    {
        attackActionToken++;
    }

    void OnEnable() => health.OnDeath += HandleDeath;

    void OnDisable()
    {
        health.OnDeath -= HandleDeath;
        if (AdvancedPlayerController.Instance != null)
        {
            var playerHealth = AdvancedPlayerController.Instance.GetComponent<PlayerHealth>();
            if (playerHealth != null)
                playerHealth.OnDeath -= OnPlayerDeath;
        }
    }

    void Update()
    {
        if (isDead) return;

        bool isMoving = rb.velocity.sqrMagnitude > 0.0001f && !isAttacking;
        animator.SetBool("moving", isMoving);
        
        // Idle state only during attack cooldown
        animator.SetBool("idle", !isMoving && !isAttacking && attackOnCooldown);

        if (!isAttacking && !attackOnCooldown && AdvancedPlayerController.Instance != null)
        {
            Vector3 targetPos = CinderbloomTauntTarget.GetTargetPositionForEnemy(gameObject);
            float distance = Vector2.Distance(transform.position, targetPos);
            if (distance <= attackRange)
            {
                attackRoutine = StartCoroutine(AttackRoutine());
            }
        }
    }

    void OnPlayerDeath()
    {
        // Stop all movement and attacks when player dies
        rb.velocity = Vector2.zero;
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }
        isAttacking = false;
        attackOnCooldown = false;
        animator.SetBool("attack", false);
        animator.SetBool("moving", false);
        animator.SetBool("idle", true);
        CancelAttackAction();
    }

    /// <summary>
    /// Apply knockback to the enemy (called from damage system)
    /// </summary>
    public void ApplyKnockback(Vector2 direction, float force)
    {
        if (isDead) return;
        
        knockbackVelocity = direction.normalized * force * knockbackIntensity;
        knockbackEndTime = Time.time + knockbackDuration;

        CancelAttackAction();
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }
        isAttacking = false;
        attackOnCooldown = false;
        animator.SetBool("attack", false);
    }

    void FixedUpdate()
    {
        if (isDead)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        // Handle knockback
        if (Time.time < knockbackEndTime)
        {
            rb.velocity = knockbackVelocity;
            return;
        }
        else if (knockbackVelocity != Vector2.zero)
        {
            knockbackVelocity = Vector2.zero;
        }

        if (isAttacking || AdvancedPlayerController.Instance == null || !AdvancedPlayerController.Instance.enabled)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        Vector3 targetPos = CinderbloomTauntTarget.GetTargetPositionForEnemy(gameObject);
        Vector3 toTarget = targetPos - transform.position;
        float distance = toTarget.magnitude;

        // Stop moving when close enough (but not too close)
        if (distance <= stopDistance)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        spriteRenderer.flipX = toTarget.x <= 0;

        float speedMult = 1f;
        if (statusController != null)
        {
            speedMult = statusController.GetEnemyMoveSpeedMultiplier();
        }
        rb.velocity = toTarget.normalized * (moveSpeed * speedMult);
    }

    IEnumerator AttackRoutine()
    {
        int myToken = BeginAttackAction();
        isAttacking = true;
        hasDealtDamageThisAttack = false; // Reset damage flag for this attack
        animator.SetBool("attack", true);
        float originalSpeed = animator.speed;
        animator.speed = attackAnimSpeed;

        // Wait for FIRST damage delay
        if (firstAttackDamageDelayV2 > 0f)
        {
            yield return new WaitForSeconds(firstAttackDamageDelayV2);
        }

        if (isDead || myToken != attackActionToken)
        {
            animator.SetBool("attack", false);
            animator.speed = originalSpeed;
            isAttacking = false;
            attackRoutine = null;
            yield break;
        }

        // Check range before dealing damage
        if (AdvancedPlayerController.Instance != null)
        {
            float distance = Vector2.Distance(transform.position, AdvancedPlayerController.Instance.transform.position);
            if (distance > attackRange)
            {
                animator.SetBool("attack", false);
                animator.speed = originalSpeed;
                isAttacking = false;
                yield break;
            }
        }

        // Deal damage instances
        int instances = Mathf.Max(1, damageInstancesPerAttackV2);
        for (int i = 0; i < instances; i++)
        {
            if (isDead || myToken != attackActionToken)
            {
                animator.SetBool("attack", false);
                animator.speed = originalSpeed;
                isAttacking = false;
                attackRoutine = null;
                yield break;
            }

            if (playerDamageable != null && playerDamageable.IsAlive && AdvancedPlayerController.Instance != null && AdvancedPlayerController.Instance.enabled)
            {
                Vector3 hitPoint = AdvancedPlayerController.Instance.transform.position; // Use PLAYER position for damage number
                Vector3 hitNormal = (AdvancedPlayerController.Instance.transform.position - transform.position).normalized;
                PlayerHealth.RegisterPendingAttacker(gameObject);
                playerDamageable.TakeDamage(attackDamageV2, hitPoint, hitNormal);
                Debug.Log($"<color=cyan>Crow dealt {attackDamageV2} damage (instance {i + 1}/{instances})</color>");
                
                // Wait before next instance using REST delay (except for last one)
                if (i < instances - 1)
                {
                    if (restAttackDamageDelayV2 > 0f)
                    {
                        yield return new WaitForSeconds(restAttackDamageDelayV2);
                    }
                }
            }
            else
            {
                // Player died during attack, stop immediately
                animator.SetBool("attack", false);
                animator.speed = originalSpeed;
                isAttacking = false;
                attackRoutine = null;
                yield break;
            }
        }

        // Wait for remaining attack duration
        float totalDamageTime = firstAttackDamageDelayV2 + (restAttackDamageDelayV2 * (instances - 1));
        float remainingTime = attackDuration - totalDamageTime;
        if (remainingTime > 0)
        {
            yield return new WaitForSeconds(remainingTime);
        }

        animator.SetBool("attack", false);
        animator.speed = originalSpeed;
        isAttacking = false;
        attackOnCooldown = true;
        
        // Enter idle state during cooldown
        animator.SetBool("idle", true);

        float cooldown = attackCooldown;
        if (statusController != null)
        {
            cooldown += statusController.GetLethargyAttackCooldownBonus();
        }
        if (cooldown < 0f)
        {
            cooldown = 0f;
        }
        if (cooldown > 0f)
        {
            yield return new WaitForSeconds(cooldown);
        }
        
        attackOnCooldown = false;
        animator.SetBool("idle", false);
    }

    void HandleDeath()
    {
        if (isDead) return;
        isDead = true;

        CancelAttackAction();

        if (attackRoutine != null) StopCoroutine(attackRoutine);

        animator.SetBool("dead", true);
        animator.SetBool("moving", false);
        animator.SetBool("idle", false);
        animator.SetBool("attack", false);
        
        // Stop movement and make static
        rb.velocity = Vector2.zero;
        knockbackVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Static;
        
        capsuleCollider.enabled = false;

        // Start fade out effect
        StartCoroutine(FadeOutAndDestroy());
    }
    
    IEnumerator FadeOutAndDestroy()
    {
        // Wait for death animation to play a bit
        float animationDelay = Mathf.Max(0f, deathCleanupDelay - deathFadeOutDuration);
        if (animationDelay > 0)
        {
            yield return new WaitForSeconds(animationDelay);
        }
        
        // Fade out
        if (spriteRenderer != null)
        {
            float elapsed = 0f;
            Color startColor = spriteRenderer.color;
            
            while (elapsed < deathFadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsed / deathFadeOutDuration);
                spriteRenderer.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
                yield return null;
            }
        }
        
        Destroy(gameObject);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDead) return;

        // Only collide with Projectiles and Player
        if (collision.gameObject.CompareTag("Projectile"))
        {
            // Projectiles handle damage via EnemyHealth component
        }
        else if (collision.gameObject.CompareTag("Player"))
        {
            // Physical contact with player (no auto-death)
        }
    }

    public class CollisionDebugger : MonoBehaviour
    {
    }
}
 