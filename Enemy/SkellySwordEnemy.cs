using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CapsuleCollider2D))]
[RequireComponent(typeof(EnemyHealth))]
public class SkellySwordEnemy : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 2f;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float stopDistance = 1.4f;
    [SerializeField] private float attackAnimSpeed = 1.0f;
    [SerializeField] private float attackCooldown = 1.0f;
    [SerializeField] private float attackDuration = 0.5f;
    [SerializeField] private float deathCleanupDelay = 0.7f;

    [Header("Death Settings")]
    [SerializeField] private float deathFadeOutDuration = 0.5f;

    [Header("Summon Settings")]
    [Tooltip("Duration of summon animation (enemy is immobile and invulnerable)")]
    [SerializeField] private float summonAnimationDuration = 1f;
    
    [Tooltip("Minimum idle time after summon animation")]
    [SerializeField] private float postSummonIdleTimeMin = 0.5f;
    
    [Tooltip("Maximum idle time after summon animation")]
    [SerializeField] private float postSummonIdleTimeMax = 1.5f;

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
    private EnemyTauntAttackHelper tauntHelper;
    private bool isDead;
    private bool isAttacking;
    private bool attackOnCooldown;
    private bool isSummoning = true; // Start in summon state
    private bool hasDealtDamageThisAttack = false;
    private Coroutine attackRoutine;
    private Vector2 knockbackVelocity = Vector2.zero;
    private float knockbackEndTime = 0f;
    private SpriteFlipOffset spriteFlipOffset;
    private Coroutine idleTransitionRoutine;
    private bool isPlayerDead;
    private const float IdleTransitionDuration = 0.5f;

    private int attackActionToken = 0;

    void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (animator == null) animator = GetComponent<Animator>();
        if (capsuleCollider == null) capsuleCollider = GetComponent<CapsuleCollider2D>();
        health = GetComponent<EnemyHealth>();
        statusController = GetComponent<StatusController>();

        capsuleCollider.isTrigger = false;
        Physics2D.IgnoreLayerCollision(gameObject.layer, LayerMask.NameToLayer("Default"), true);
        Physics2D.IgnoreLayerCollision(gameObject.layer, LayerMask.NameToLayer("Enemy"), true);

        if (AdvancedPlayerController.Instance != null)
        {
            playerDamageable = AdvancedPlayerController.Instance.GetComponent<IDamageable>();
            AdvancedPlayerController.Instance.GetComponent<PlayerHealth>().OnDeath += OnPlayerDeath;
        }
        
        // Add taunt attack helper
        tauntHelper = GetComponent<EnemyTauntAttackHelper>();
        if (tauntHelper == null)
        {
            tauntHelper = gameObject.AddComponent<EnemyTauntAttackHelper>();
        }
        
        spriteFlipOffset = GetComponent<SpriteFlipOffset>();

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

    void Start()
    {
        // Start summon animation
        StartCoroutine(SummonRoutine());
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

    IEnumerator SummonRoutine()
    {
        isSummoning = true;
        animator.SetBool("summon", true);
        
        // Choose appropriate digout animation based on current flip state
        bool isFlippedAtSummon = spriteRenderer != null && spriteRenderer.flipX;

        // If we have a valid player target, determine flip based on their horizontal position
        if (AdvancedPlayerController.Instance != null && AdvancedPlayerController.Instance.enabled)
        {
            Vector3 targetPos = CinderbloomTauntTarget.GetTargetPositionForEnemy(gameObject);
            isFlippedAtSummon = targetPos.x <= transform.position.x;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = isFlippedAtSummon;
        }

        animator.SetBool("digout", !isFlippedAtSummon);
        animator.SetBool("digoutflip", isFlippedAtSummon);
        
        // Invulnerable during summon
        health.enabled = false;
        
        Debug.Log($"<color=green>Skeleton summoning for {summonAnimationDuration}s (invulnerable)</color>");
        
        yield return new WaitForSeconds(summonAnimationDuration);
        
        animator.SetBool("summon", false);
        animator.SetBool("digout", false);
        animator.SetBool("digoutflip", false);
        
        // Enable damage after summon animation
        health.enabled = true;

        // If the player died during summon, freeze immediately now that
        // the digout/digoutflip animation has finished.
        if (freezeOnPlayerDeathPending)
        {
            freezeOnPlayerDeathPending = false;
            isSummoning = false;
            FreezeImmediatelyOnPlayerDeath();
            yield break;
        }

        // Post-summon idle time (random between min and max)
        float postSummonIdleTime = Random.Range(postSummonIdleTimeMin, postSummonIdleTimeMax);
        Debug.Log($"<color=green>Skeleton post-summon idle for {postSummonIdleTime:F2}s</color>");
        
        animator.SetBool("idle", true);
        yield return new WaitForSeconds(postSummonIdleTime);
        animator.SetBool("idle", false);
        
        isSummoning = false;
        Debug.Log("<color=green>Skeleton now active</color>");
    }

    void Update()
    {
        if (isDead || isSummoning || isPlayerDead) return;

        bool ismoving = false;
        if (!isAttacking && AdvancedPlayerController.Instance != null && AdvancedPlayerController.Instance.enabled)
        {
            // Use taunt-aware target position to derive movement intent, so
            // external forces (e.g., Collapse) do not cause walk/idle
            // flickering when the AI is still trying to move.
            Vector3 moveTargetPos = CinderbloomTauntTarget.GetTargetPositionForEnemy(gameObject);
            float moveDistance = Vector2.Distance(transform.position, moveTargetPos);
            ismoving = moveDistance > stopDistance;
        }

        bool isFlipped = spriteRenderer.flipX;
        
        // Set moving or movingflip based on flip state
        if (ismoving && isFlipped)
        {
            animator.SetBool("movingflip", true);
            animator.SetBool("moving", false);
        }
        else if (ismoving && !isFlipped)
        {
            animator.SetBool("moving", true);
            animator.SetBool("movingflip", false);
        }
        else
        {
            animator.SetBool("moving", false);
            animator.SetBool("movingflip", false);
        }
        
        animator.SetBool("idle", !ismoving && !isAttacking && attackOnCooldown);

        if (!isAttacking && !attackOnCooldown && AdvancedPlayerController.Instance != null)
        {
            // Use taunt-aware target position
            Vector3 targetPos = CinderbloomTauntTarget.GetTargetPositionForEnemy(gameObject);
            float distance = Vector2.Distance(transform.position, targetPos);
            if (distance <= attackRange)
            {
                attackRoutine = StartCoroutine(AttackRoutine());
            }
        }
    }

    private bool freezeOnPlayerDeathPending = false;

    void OnPlayerDeath()
    {
        if (isDead) return;

        isPlayerDead = true;
        rb.velocity = Vector2.zero;
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }
        isAttacking = false;
        attackOnCooldown = false;

        CancelAttackAction();
        
        // If currently in summon/digout, defer freeze until after summon
        if (isSummoning || (animator != null && (animator.GetBool("summon") || animator.GetBool("digout") || animator.GetBool("digoutflip"))))
        {
            freezeOnPlayerDeathPending = true;
            Debug.Log("<color=yellow>SkellySword will freeze after summon completes (player died during digout)</color>");
            return;
        }

        FreezeImmediatelyOnPlayerDeath();
    }

    private void FreezeImmediatelyOnPlayerDeath()
    {
        if (animator == null) return;

        if (idleTransitionRoutine != null)
        {
            StopCoroutine(idleTransitionRoutine);
        }
        idleTransitionRoutine = StartCoroutine(IdleTransitionCoroutine());
    }

    private IEnumerator IdleTransitionCoroutine()
    {
        float elapsed = 0f;
        while (elapsed < IdleTransitionDuration)
        {
            if (isDead)
            {
                idleTransitionRoutine = null;
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (animator != null && !isDead)
        {
            animator.SetBool("attack", false);
            animator.SetBool("moving", false);
            animator.SetBool("movingflip", false);
            animator.SetBool("idle", true);
            Debug.Log("<color=yellow>Skeleton frozen on player death</color>");
        }

        idleTransitionRoutine = null;
    }

    public void ApplyKnockback(Vector2 direction, float force)
    {
        if (isDead || isSummoning || isPlayerDead) return;
        
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
        if (isDead || isSummoning || isPlayerDead)
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

        // Use taunt-aware target position
        Vector3 targetPos = CinderbloomTauntTarget.GetTargetPositionForEnemy(gameObject);
        Vector3 toTarget = targetPos - transform.position;
        float distance = toTarget.magnitude;

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
        hasDealtDamageThisAttack = false;
        animator.SetBool("attack", true);
        float originalSpeed = animator.speed;
        animator.speed = attackAnimSpeed;

        // Wait for FIRST damage delay
        if (firstAttackDamageDelayV2 > 0f)
        {
            yield return new WaitForSeconds(firstAttackDamageDelayV2);
        }

        if (isDead || isSummoning || isPlayerDead || myToken != attackActionToken)
        {
            animator.SetBool("attack", false);
            animator.speed = originalSpeed;
            isAttacking = false;
            attackRoutine = null;
            yield break;
        }

        // Check if target still in range (taunt-aware)
        if (AdvancedPlayerController.Instance != null)
        {
            Vector3 targetPos = CinderbloomTauntTarget.GetTargetPositionForEnemy(gameObject);
            float distance = Vector2.Distance(transform.position, targetPos);
            if (distance > attackRange)
            {
                Debug.Log($"<color=orange>Skeleton: Target out of range ({distance:F2} > {attackRange}), cancelling attack</color>");
                animator.SetBool("attack", false);
                animator.speed = originalSpeed;
                isAttacking = false;
                attackRoutine = null;
                yield break;
            }
        }

        // Deal damage instances (taunt-aware)
        int instances = Mathf.Max(1, damageInstancesPerAttackV2);
        for (int i = 0; i < instances; i++)
        {
            if (isDead || isSummoning || isPlayerDead || myToken != attackActionToken)
            {
                animator.SetBool("attack", false);
                animator.speed = originalSpeed;
                isAttacking = false;
                attackRoutine = null;
                yield break;
            }

            Transform attackTarget = tauntHelper != null ? tauntHelper.GetAttackTarget() : null;
            if (attackTarget != null)
            {
                Vector3 hitPoint = attackTarget.position;
                Vector3 hitNormal = (attackTarget.position - transform.position).normalized;
                
                if (tauntHelper != null)
                {
                    tauntHelper.DealDamageToTarget(attackDamageV2, hitPoint, hitNormal);
                }
                else
                {
                    // Fallback to player
                    if (playerDamageable != null && playerDamageable.IsAlive)
                    {
                        PlayerHealth.RegisterPendingAttacker(gameObject);
                        playerDamageable.TakeDamage(attackDamageV2, hitPoint, hitNormal);
                    }
                }
                Debug.Log($"<color=green>Skeleton dealt {attackDamageV2} damage (instance {i + 1}/{instances})</color>");
                
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
        attackRoutine = null;
    }

    void LateUpdate()
    {
        if (spriteFlipOffset == null) return;

        // Check animation states
        bool isWalking = animator.GetBool("moving") || animator.GetBool("movingflip");
        bool isDying = animator.GetBool("dead") || animator.GetBool("deadflip");
        bool isSpawningFlipped = animator.GetBool("digoutflip");

        // Disable SpriteFlipOffset during walking or death
        if (isWalking || isDying || isSpawningFlipped)
        {
            spriteFlipOffset.SetColliderOffsetEnabled(false);
            spriteFlipOffset.SetShadowOffsetEnabled(false);
        }
        else
        {
            // Enable SpriteFlipOffset for all other states
            spriteFlipOffset.SetColliderOffsetEnabled(true);
            spriteFlipOffset.SetShadowOffsetEnabled(true);
        }
    }

    void HandleDeath()
    {
        if (isDead) return;
        isDead = true;

        CancelAttackAction();

        if (attackRoutine != null) StopCoroutine(attackRoutine);

        // CRITICAL: Set death animation based on flip state
        bool isFlipped = spriteRenderer.flipX;
        animator.SetBool("dead", !isFlipped);      // Normal death when NOT flipped
        animator.SetBool("deadflip", isFlipped);   // Flipped death when flipped
        
        // Disable all other animation states
        animator.SetBool("moving", false);
        animator.SetBool("movingflip", false);
        animator.SetBool("idle", false);
        animator.SetBool("attack", false);
        animator.SetBool("summon", false);
        
        rb.velocity = Vector2.zero;
        knockbackVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Static;
        
        capsuleCollider.enabled = false;

        // Fade out sprite over time, then destroy
        StartCoroutine(FadeOutAndDestroy());
    }
    
    IEnumerator FadeOutAndDestroy()
    {
        float animationDelay = Mathf.Max(0f, deathCleanupDelay - deathFadeOutDuration);
        if (animationDelay > 0f)
        {
            yield return new WaitForSeconds(animationDelay);
        }

        if (spriteRenderer != null && deathFadeOutDuration > 0f)
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
        if (isDead || isSummoning) return;

        if (collision.gameObject.CompareTag("Projectile"))
        {
            // Projectiles handle damage via EnemyHealth component
        }
        else if (collision.gameObject.CompareTag("Player"))
        {
            // Physical contact with player
        }
    }

    public void SetBaseDamageFromBoss(float bossAttackDamage, float percent)
    {
        if (bossAttackDamage <= 0f || percent <= 0f) return;
        float newDamage = bossAttackDamage * percent;
        attackDamage = newDamage;
        attackDamageV2 = newDamage;
        Debug.Log($"<color=green>SkeletonEnemy base attackDamage set from boss: {bossAttackDamage:F1} * {percent:P0} = {newDamage:F1}</color>");
    }

    public void MultiplyAttackDamage(float multiplier)
    {
        if (multiplier <= 0f) return;
        attackDamage *= multiplier;
        attackDamageV2 *= multiplier;
        Debug.Log($"<color=green>SkeletonEnemy attackDamage scaled by x{multiplier:F2} (new: {attackDamageV2:F1})</color>");
    }
}
