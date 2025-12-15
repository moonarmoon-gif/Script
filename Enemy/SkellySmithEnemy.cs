using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CapsuleCollider2D))]
[RequireComponent(typeof(EnemyHealth))]
public class SkellySmithEnemy : MonoBehaviour, IStaticInterruptHandler
{
    [Header("Movement Settings")]
    public float moveSpeed = 2f;
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float stopDistance = 1.4f;
    [SerializeField] private float attackAnimSpeed = 1.0f;
    [SerializeField] private float attackCooldown = 1.0f;
    [SerializeField] private float attackDuration = 0.5f;
    [SerializeField] private float deathCleanupDelay = 0.7f;

    [Header("Charge Settings")]
    [Tooltip("Speed during dash/charge towards the player")]
    [SerializeField] private float chargeSpeed = 4.0f;
    [Tooltip("Max distance at which SkellySmith will perform a charge")]
    [SerializeField] private float chargeRange = 5.0f;
    [Tooltip("Time to wait (windup) before starting the dash")]
    [SerializeField] private float chargeWindup = 0.25f;
    [Tooltip("Duration of the dash movement")]
    [SerializeField] private float chargeDashDuration = 0.35f;
    [Tooltip("Cooldown between charges")]
    [SerializeField] private float chargeCooldown = 1.0f;
    [Tooltip("Time after a charge before a melee attack is allowed")]
    [SerializeField] private float postChargeAttackCooldown = 0.0f;

    [Header("Charge End Settings")]
    [Tooltip("Distance from target at which charge loop stops and charge end begins")]
    public float ChargeEndRadius = 1.8f;

    [Tooltip("How long the charge end animation (and slide) should play")]
    public float ChargeEndDuration = 0.3f;

    [Tooltip("Slide distance multiplier during charge end based on chargeSpeed")]
    public float chargeEndSlideMultiplier = 0.3f;

    [Tooltip("Override for charge end slide duration (0 = use ChargeEndDuration)")]
    public float chargeEndSlideDuration = 0f;

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
    private bool isCharging;
    private bool chargeOnCooldown;
    private float lastChargeEndTime = -999f;
    private Coroutine attackRoutine;
    private Coroutine chargeRoutine;
    private Coroutine staticAttackCooldownRoutine;
    private Coroutine staticChargeCooldownRoutine;
    private bool isStaticFrozen;
    private Vector2 knockbackVelocity = Vector2.zero;
    private float knockbackEndTime = 0f;
    private SpriteFlipOffset spriteFlipOffset;
    private bool freezeOnPlayerDeathPending = false;
    private Coroutine idleTransitionRoutine;
    private bool isPlayerDead;
    private const float IdleTransitionDuration = 0.5f;

    private int attackActionToken = 0;
    private int chargeActionToken = 0;

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

    private int BeginChargeAction()
    {
        chargeActionToken++;
        return chargeActionToken;
    }

    private void CancelChargeAction()
    {
        chargeActionToken++;
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

        if (isStaticFrozen) return;

        // While charging, suppress walk/idle and let charge-specific animations play
        if (isCharging)
        {
            animator.SetBool("moving", false);
            animator.SetBool("movingflip", false);
            animator.SetBool("idle", false);
            return;
        }

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

        if (!isAttacking && !attackOnCooldown && !isCharging && AdvancedPlayerController.Instance != null)
        {
            // Use taunt-aware target position
            Vector3 targetPos = CinderbloomTauntTarget.GetTargetPositionForEnemy(gameObject);
            float distance = Vector2.Distance(transform.position, targetPos);
            bool canAttackAfterCharge = (Time.time - lastChargeEndTime) > postChargeAttackCooldown;
            if (distance <= attackRange && canAttackAfterCharge)
            {
                attackRoutine = StartCoroutine(AttackRoutine());
            }
        }
    }

    void OnPlayerDeath()
    {
        if (isDead || isPlayerDead) return;

        isPlayerDead = true;
        rb.velocity = Vector2.zero;
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }
        if (chargeRoutine != null)
        {
            StopCoroutine(chargeRoutine);
            chargeRoutine = null;
        }
        isAttacking = false;
        attackOnCooldown = false;
        isCharging = false;
        chargeOnCooldown = false;

        CancelAttackAction();
        CancelChargeAction();

        if (staticAttackCooldownRoutine != null)
        {
            StopCoroutine(staticAttackCooldownRoutine);
            staticAttackCooldownRoutine = null;
        }
        if (staticChargeCooldownRoutine != null)
        {
            StopCoroutine(staticChargeCooldownRoutine);
            staticChargeCooldownRoutine = null;
        }
        
        // If currently in summon/digout, defer freeze until after summon
        if (isSummoning || (animator != null && (animator.GetBool("summon") || animator.GetBool("digout") || animator.GetBool("digoutflip"))))
        {
            freezeOnPlayerDeathPending = true;
            Debug.Log("<color=yellow>SkellySmith will freeze after summon completes (player died during digout)</color>");
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
            animator.SetBool("chargestart", false);
            animator.SetBool("chargeloop", false);
            animator.SetBool("chargeend", false);
            animator.SetBool("chargestartflip", false);
            animator.SetBool("chargeloopflip", false);
            animator.SetBool("chargeendflip", false);
            animator.SetBool("digout", false);
            animator.SetBool("digoutflip", false);
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
        CancelChargeAction();

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }
        if (chargeRoutine != null)
        {
            StopCoroutine(chargeRoutine);
            chargeRoutine = null;
        }

        if (staticAttackCooldownRoutine != null)
        {
            StopCoroutine(staticAttackCooldownRoutine);
            staticAttackCooldownRoutine = null;
        }
        if (staticChargeCooldownRoutine != null)
        {
            StopCoroutine(staticChargeCooldownRoutine);
            staticChargeCooldownRoutine = null;
        }

        isAttacking = false;
        attackOnCooldown = false;
        isCharging = false;
        chargeOnCooldown = false;

        if (animator != null)
        {
            animator.SetBool("attack", false);
            animator.SetBool("chargestart", false);
            animator.SetBool("chargeloop", false);
            animator.SetBool("chargeend", false);
            animator.SetBool("chargestartflip", false);
            animator.SetBool("chargeloopflip", false);
            animator.SetBool("chargeendflip", false);
        }
    }

    void FixedUpdate()
    {
        if (isDead || isSummoning || isPlayerDead)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        if (isStaticFrozen)
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

        // During charge, let ChargeRoutine control movement
        if (isCharging)
        {
            return;
        }

        // Decide whether to start a charge: within chargeRange but outside melee range
        if (!chargeOnCooldown && !isCharging && !isAttacking && distance > attackRange && distance <= chargeRange)
        {
            if (chargeRoutine == null)
            {
                chargeRoutine = StartCoroutine(ChargeRoutine());
            }
            return;
        }

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

    IEnumerator ChargeRoutine()
    {
        int myToken = BeginChargeAction();
        isCharging = true;
        rb.velocity = Vector2.zero;

        // Face target immediately
        Vector3 targetPos = CinderbloomTauntTarget.GetTargetPositionForEnemy(gameObject);
        Vector2 dir = (targetPos - transform.position).normalized;
        if (dir.sqrMagnitude > 0.0001f)
        {
            spriteRenderer.flipX = dir.x <= 0;
        }

        // CHARGESTART: windup
        bool flipped = spriteRenderer.flipX;
        animator.SetBool("chargestart", !flipped);
        animator.SetBool("chargestartflip", flipped);
        animator.SetBool("chargeloop", false);
        animator.SetBool("chargeloopflip", false);
        animator.SetBool("chargeend", false);
        animator.SetBool("chargeendflip", false);

        if (chargeWindup > 0f)
        {
            float elapsedWindup = 0f;
            while (elapsedWindup < chargeWindup)
            {
                if (isDead || isSummoning || isPlayerDead || isStaticFrozen || myToken != chargeActionToken || AdvancedPlayerController.Instance == null || !AdvancedPlayerController.Instance.enabled)
                {
                    isCharging = false;
                    animator.SetBool("chargestart", false);
                    animator.SetBool("chargeloop", false);
                    animator.SetBool("chargeend", false);
                    animator.SetBool("chargestartflip", false);
                    animator.SetBool("chargeloopflip", false);
                    animator.SetBool("chargeendflip", false);
                    chargeRoutine = null;
                    yield break;
                }

                // Keep facing target during windup
                targetPos = CinderbloomTauntTarget.GetTargetPositionForEnemy(gameObject);
                dir = (targetPos - transform.position).normalized;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    spriteRenderer.flipX = dir.x <= 0;
                }

                elapsedWindup += Time.deltaTime;
                yield return null;
            }
        }
        else if (isDead || isSummoning || isPlayerDead || isStaticFrozen || myToken != chargeActionToken || AdvancedPlayerController.Instance == null || !AdvancedPlayerController.Instance.enabled)
        {
            isCharging = false;
            animator.SetBool("chargestart", false);
            animator.SetBool("chargeloop", false);
            animator.SetBool("chargeend", false);
            animator.SetBool("chargestartflip", false);
            animator.SetBool("chargeloopflip", false);
            animator.SetBool("chargeendflip", false);
            chargeRoutine = null;
            yield break;
        }

        // CHARGELOOP: dash forward until within ChargeEndRadius or until max dash duration
        flipped = spriteRenderer.flipX;
        animator.SetBool("chargestart", false);
        animator.SetBool("chargestartflip", false);
        animator.SetBool("chargeloop", !flipped);
        animator.SetBool("chargeloopflip", flipped);
        animator.SetBool("chargeend", false);
        animator.SetBool("chargeendflip", false);

        // Lock direction at start of dash
        targetPos = CinderbloomTauntTarget.GetTargetPositionForEnemy(gameObject);
        dir = (targetPos - transform.position).normalized;
        if (dir.sqrMagnitude > 0.0001f)
        {
            spriteRenderer.flipX = dir.x <= 0;
        }

        float t = 0f;
        while (t < chargeDashDuration)
        {
            if (isDead || isSummoning || isPlayerDead || isStaticFrozen || myToken != chargeActionToken || AdvancedPlayerController.Instance == null || !AdvancedPlayerController.Instance.enabled)
            {
                break;
            }

            targetPos = CinderbloomTauntTarget.GetTargetPositionForEnemy(gameObject);
            float distance = Vector2.Distance(transform.position, targetPos);

            // Stop charge loop when we reach the configured end radius
            if (distance <= ChargeEndRadius)
            {
                break;
            }

            rb.velocity = dir * chargeSpeed;
            t += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        if (isDead || isSummoning || isPlayerDead || isStaticFrozen || myToken != chargeActionToken || AdvancedPlayerController.Instance == null || !AdvancedPlayerController.Instance.enabled)
        {
            isCharging = false;
            animator.SetBool("chargestart", false);
            animator.SetBool("chargeloop", false);
            animator.SetBool("chargeend", false);
            animator.SetBool("chargestartflip", false);
            animator.SetBool("chargeloopflip", false);
            animator.SetBool("chargeendflip", false);
            rb.velocity = Vector2.zero;
            chargeRoutine = null;
            yield break;
        }

        rb.velocity = Vector2.zero;

        // CHARGEEND: play charge end animation for full ChargeEndDuration,
        // with optional slide for a shorter portion using chargeEndSlideDuration
        flipped = spriteRenderer.flipX;
        animator.SetBool("chargeloop", false);
        animator.SetBool("chargeloopflip", false);
        animator.SetBool("chargeend", !flipped);
        animator.SetBool("chargeendflip", flipped);

        float slideDuration = chargeEndSlideDuration > 0f
            ? Mathf.Min(chargeEndSlideDuration, ChargeEndDuration)
            : ChargeEndDuration;
        float slideSpeed = chargeSpeed * chargeEndSlideMultiplier;
        float chargeEndElapsed = 0f;

        while (chargeEndElapsed < ChargeEndDuration)
        {
            if (isDead || isSummoning || isPlayerDead || isStaticFrozen || myToken != chargeActionToken || AdvancedPlayerController.Instance == null || !AdvancedPlayerController.Instance.enabled)
            {
                break;
            }

            if (chargeEndElapsed < slideDuration)
            {
                rb.velocity = dir * slideSpeed;
            }
            else
            {
                rb.velocity = Vector2.zero;
            }

            chargeEndElapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        if (isDead || isSummoning || isPlayerDead || isStaticFrozen || myToken != chargeActionToken || AdvancedPlayerController.Instance == null || !AdvancedPlayerController.Instance.enabled)
        {
            isCharging = false;
            animator.SetBool("chargestart", false);
            animator.SetBool("chargeloop", false);
            animator.SetBool("chargeend", false);
            animator.SetBool("chargestartflip", false);
            animator.SetBool("chargeloopflip", false);
            animator.SetBool("chargeendflip", false);
            rb.velocity = Vector2.zero;
            chargeRoutine = null;
            yield break;
        }

        rb.velocity = Vector2.zero;
        animator.SetBool("chargeend", false);
        animator.SetBool("chargeendflip", false);

        isCharging = false;
        chargeOnCooldown = true;
        lastChargeEndTime = Time.time;

        // Charge cooldown
        if (chargeCooldown > 0f)
        {
            yield return new WaitForSeconds(chargeCooldown);
        }

        if (isDead || isSummoning || isPlayerDead || myToken != chargeActionToken)
        {
            chargeRoutine = null;
            yield break;
        }

        chargeOnCooldown = false;
        chargeRoutine = null;
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

        if (isDead || isSummoning || isPlayerDead || isStaticFrozen || myToken != attackActionToken)
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
            if (isDead || isSummoning || isPlayerDead || isStaticFrozen || myToken != attackActionToken)
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
        bool isChargingNormal = animator.GetBool("chargestart") || animator.GetBool("chargeloop") || animator.GetBool("chargeend");
        bool isChargingFlipped = animator.GetBool("chargestartflip") || animator.GetBool("chargeloopflip") || animator.GetBool("chargeendflip");
        bool isChargingAny = isChargingNormal || isChargingFlipped;

        // Disable SpriteFlipOffset during walking, death, or ANY summon/charge animations that should not move the collider/shadow
        if (isWalking || isDying || isSpawningFlipped || isChargingAny)
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
        CancelChargeAction();

        if (attackRoutine != null) StopCoroutine(attackRoutine);
        if (chargeRoutine != null) StopCoroutine(chargeRoutine);
        if (staticAttackCooldownRoutine != null) StopCoroutine(staticAttackCooldownRoutine);
        if (staticChargeCooldownRoutine != null) StopCoroutine(staticChargeCooldownRoutine);

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
        animator.SetBool("chargestart", false);
        animator.SetBool("chargeloop", false);
        animator.SetBool("chargeend", false);
        
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
    
    // IStaticInterruptHandler implementation: handle StaticEffect interruptions
    public void OnStaticStart(float staticPeriodSeconds)
    {
        if (isDead || isSummoning) return;

        isStaticFrozen = true;

        // Interrupt melee attack and enforce full cooldown
        if (attackRoutine != null)
        {
            CancelAttackAction();
            StopCoroutine(attackRoutine);
            attackRoutine = null;
            isAttacking = false;
            animator.SetBool("attack", false);

            if (!attackOnCooldown && attackCooldown > 0f)
            {
                attackOnCooldown = true;
                if (staticAttackCooldownRoutine != null)
                {
                    StopCoroutine(staticAttackCooldownRoutine);
                }
                staticAttackCooldownRoutine = StartCoroutine(AttackCooldownAfterStatic());
            }
        }

        // Interrupt active charge and enforce full charge cooldown
        if (chargeRoutine != null)
        {
            CancelChargeAction();
            StopCoroutine(chargeRoutine);
            chargeRoutine = null;
            rb.velocity = Vector2.zero;

            isCharging = false;
            chargeOnCooldown = true;
            lastChargeEndTime = Time.time;

            if (staticChargeCooldownRoutine != null)
            {
                StopCoroutine(staticChargeCooldownRoutine);
            }
            staticChargeCooldownRoutine = StartCoroutine(ChargeCooldownAfterStatic());
        }

    }

    public void OnStaticEnd()
    {
        if (isDead || isSummoning) return;

        isStaticFrozen = false;

        // Ensure interrupted attack/charge animations do not resume once
        // StaticStatus restores animator speed.
        animator.SetBool("attack", false);
        animator.SetBool("chargestart", false);
        animator.SetBool("chargeloop", false);
        animator.SetBool("chargeend", false);
        animator.SetBool("chargestartflip", false);
        animator.SetBool("chargeloopflip", false);
        animator.SetBool("chargeendflip", false);
    }

    private IEnumerator AttackCooldownAfterStatic()
    {
        float wait = attackCooldown;
        if (statusController != null)
        {
            wait += statusController.GetLethargyAttackCooldownBonus();
        }
        if (wait > 0f)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, wait));
        }

        attackOnCooldown = false;
        animator.SetBool("idle", false);
        staticAttackCooldownRoutine = null;
    }

    private IEnumerator ChargeCooldownAfterStatic()
    {
        float wait = Mathf.Max(0f, chargeCooldown);
        if (wait > 0f)
        {
            yield return new WaitForSeconds(wait);
        }

        chargeOnCooldown = false;
        staticChargeCooldownRoutine = null;
    }
}
