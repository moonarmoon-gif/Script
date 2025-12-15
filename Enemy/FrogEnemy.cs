using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(EnemyHealth))]
public class FrogEnemy : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 1.8f;
    public float chargeSpeed = 3.5f;
    public float chargeRange = 4.0f;
    public float attackRange = 1.2f;
    public float chargeAttackCastRadius = 1.8f;
    public float attackAnimSpeed = 1.35f;
    public float chargeWindup = 0.2f;
    public float chargeDashDuration = 0.3f;
    public float chargeCooldown = 0.8f;
    [Tooltip("Cooldown after charge attack before normal attack can trigger")]
    public float postChargeAttackCooldown = 0.5f;
    public float attackCooldown = 0.8f;
    public float attackDuration = 0.3f;
    [Tooltip("Duration of death animation before enemy is destroyed")]
    public float deathCleanupDelay = 0.5f;
    [Tooltip("Duration of fade out effect on death (seconds)")]
    [SerializeField] private float deathFadeOutDuration = 0.5f;

    [Header("Damage Settings")]
    public float attackDamage = 12f;
    public float chargeDamage = 8f;
    [Tooltip("Delay in NORMAL attack animation before FIRST damage is dealt (seconds)")]
    public float attackDamageDelay = 0.1f;
    [Tooltip("Delay between first and second damage in normal attack (seconds)")]
    public float attackSecondDamageDelay = 0.2f;
    [Tooltip("Delay in CHARGE attack animation before damage is dealt (seconds)")]
    public float chargeAttackDamageDelay = 0.15f;

    [SerializeField] private float attackDamageV2 = -1f;
    [SerializeField] private float chargeDamageV2 = -1f;
    [SerializeField] private float attackDamageDelayV2 = -1f;
    [SerializeField] private float attackSecondDamageDelayV2 = -1f;
    [SerializeField] private float chargeAttackDamageDelayV2 = -1f;

    [Header("Physics Settings")]
    [Tooltip("Normal mass when not charging")]
    public float normalMass = 1f;
    [Tooltip("Heavy mass during charge to prevent sliding")]
    public float chargeMass = 1000f;

    // Component references
    SpriteRenderer sr;
    Rigidbody2D rb;
    Animator anim;
    Collider2D col;
    EnemyHealth health;
    StatusController statusController;

    // Player references
    Transform player;
    IDamageable playerDamageable;

    // State flags
    bool isDead;
    bool isAttacking;
    bool isCharging;
    bool chargeOnCooldown;
    bool attackOnCooldown;

    // Coroutine handles
    Coroutine chargeRoutine;
    Coroutine attackRoutine;

    // Track when charge ended (to detect charge attacks)
    float lastChargeEndTime = -999f;

    private int attackActionToken = 0;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        col = GetComponent<Collider2D>();
        health = GetComponent<EnemyHealth>();
        statusController = GetComponent<StatusController>();

        if (AdvancedPlayerController.Instance != null)
        {
            player = AdvancedPlayerController.Instance.transform;
            playerDamageable = player.GetComponent<IDamageable>();
            player.GetComponent<PlayerHealth>().OnDeath += OnPlayerDeath;
        }

        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.gravityScale = 0f;
        rb.mass = normalMass;
        if (chargeAttackCastRadius < attackRange) chargeAttackCastRadius = attackRange;

        // Phase-through mechanics - Frogs don't collide with other enemies
        Physics2D.IgnoreLayerCollision(gameObject.layer, LayerMask.NameToLayer("Enemy"), true);

        if (attackDamageV2 < 0f)
        {
            attackDamageV2 = attackDamage;
        }
        if (chargeDamageV2 < 0f)
        {
            chargeDamageV2 = chargeDamage;
        }
        if (attackDamageDelayV2 < 0f)
        {
            attackDamageDelayV2 = attackDamageDelay;
        }
        if (attackSecondDamageDelayV2 < 0f)
        {
            attackSecondDamageDelayV2 = attackSecondDamageDelay;
        }
        if (chargeAttackDamageDelayV2 < 0f)
        {
            chargeAttackDamageDelayV2 = chargeAttackDamageDelay;
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

    void OnEnable()
    {
        if (health != null) health.OnDeath += HandleDeath;
    }

    void OnDisable()
    {
        if (health != null) health.OnDeath -= HandleDeath;
        if (player != null && player.TryGetComponent<PlayerHealth>(out var playerHealth))
        {
            playerHealth.OnDeath -= OnPlayerDeath;
        }
    }

    void Update()
    {
        bool playerDead = player == null || (AdvancedPlayerController.Instance != null && !AdvancedPlayerController.Instance.enabled);

        anim.SetBool("dead", isDead);

        // Idle state only during attack cooldown and charge cooldown
        bool isMoving = rb.velocity.sqrMagnitude > 0.0001f && !isAttacking && !isCharging;
        bool isIdle = !isMoving && !isAttacking && !isCharging && (attackOnCooldown || chargeOnCooldown);
        anim.SetBool("idle", isIdle);
        anim.SetBool("moving", !isDead && !playerDead && rb.velocity.sqrMagnitude > 0.0001f && !isCharging && !isAttacking);
        anim.SetBool("chargeRadius", !isDead && !playerDead && isCharging);

        if (!isDead && !playerDead && player != null)
            sr.flipX = !(player.position.x > transform.position.x);
    }

    void FixedUpdate()
    {
        bool playerDead = player == null || (AdvancedPlayerController.Instance != null && !AdvancedPlayerController.Instance.enabled);

        if (isDead || playerDead)
        {
            rb.velocity = Vector2.zero;
            if (isCharging) StopCharge();
            if (isAttacking) StopAttack();
            return;
        }

        float dist = Vector2.Distance(transform.position, player.position);
        Vector2 toPlayer = ((Vector2)player.position - (Vector2)transform.position).normalized;
        anim.SetBool("attack", dist <= chargeAttackCastRadius);

        // Stop movement during attack only (not during charge - charge has its own movement)
        if (isAttacking)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        // During charge, let ChargeRoutine handle movement
        if (isCharging)
        {
            return;
        }

        // If in charge attack cast radius, try to stay in range without moving forward
        if (dist <= chargeAttackCastRadius)
        {
            anim.SetBool("chargeRadius", false);

            // Check if enough time has passed since last charge (post-charge cooldown)
            bool canAttackAfterCharge = (Time.time - lastChargeEndTime) > postChargeAttackCooldown;

            if (!attackOnCooldown && !isAttacking && attackRoutine == null && canAttackAfterCharge)
            {
                attackRoutine = StartCoroutine(AttackRoutine());
            }
            else if (!canAttackAfterCharge && !isAttacking)
            {
                Debug.Log($"<color=cyan>Frog attack blocked by post-charge cooldown. Time since charge: {Time.time - lastChargeEndTime:F2}s, Required: {postChargeAttackCooldown}s</color>");
            }

            // Stop forward movement, but allow being pushed
            rb.velocity = Vector2.zero;
            return;
        }

        // If in charge range but not in charge attack cast radius, charge
        if (dist <= chargeRange && dist > chargeAttackCastRadius && !chargeOnCooldown && !isCharging)
        {
            anim.SetBool("chargeRadius", true);
            if (chargeRoutine == null)
                chargeRoutine = StartCoroutine(ChargeRoutine());
            return;
        }

        if (dist > chargeRange && isCharging)
        {
            StopCharge();
            return;
        }

        // Move towards player if not in any special state
        if (!isCharging && !isAttacking)
        {
            float speedMult = 1f;
            if (statusController != null)
            {
                speedMult = statusController.GetEnemyMoveSpeedMultiplier();
            }
            rb.velocity = toPlayer * (moveSpeed * speedMult);
        }
    }

    IEnumerator ChargeRoutine()
    {
        isCharging = true;
        rb.velocity = Vector2.zero;

        rb.mass = chargeMass;

        yield return new WaitForSeconds(chargeWindup);

        if (isDead || player == null || (AdvancedPlayerController.Instance != null && !AdvancedPlayerController.Instance.enabled))
        {
            StopCharge();
            yield break;
        }

        Vector2 dir = ((Vector2)player.position - (Vector2)transform.position).normalized;
        float t = 0f;
        bool hitPlayer = false;

        while (t < chargeDashDuration && !isDead)
        {
            // Check if player is still alive
            if (player == null || (AdvancedPlayerController.Instance != null && !AdvancedPlayerController.Instance.enabled))
            {
                StopCharge();
                yield break;
            }
            
            float distToPlayer = Vector2.Distance(transform.position, player.position);

            // Cancel charge if player moved out of charge range
            if (distToPlayer > chargeRange)
            {
                Debug.Log("<color=yellow>Frog: Player out of charge range, canceling charge</color>");
                StopCharge();
                yield break;
            }

            if (distToPlayer <= chargeAttackCastRadius)
            {
                rb.velocity = Vector2.zero;
                // Trigger charge attack animation
                anim.SetBool("chargeattack", true);
                anim.SetBool("attack", false);
                Debug.Log("<color=magenta>Frog CHARGE ATTACK animation triggered in ChargeRoutine</color>");
                break;
            }

            if (!hitPlayer && distToPlayer <= attackRange && playerDamageable != null && playerDamageable.IsAlive)
            {
                Vector3 hitPoint = player.position;
                Vector3 hitNormal = (transform.position - hitPoint).normalized;
                PlayerHealth.RegisterPendingAttacker(gameObject);
                playerDamageable.TakeDamage(chargeDamageV2, hitPoint, hitNormal);
                hitPlayer = true;
            }

            rb.velocity = dir * chargeSpeed;
            t += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        rb.velocity = Vector2.zero;

        rb.mass = normalMass;

        isCharging = false;
        anim.SetBool("chargeRadius", false);

        // Wait for charge attack animation to complete if triggered
        if (anim.GetBool("chargeattack"))
        {
            yield return new WaitForSeconds(attackDuration);
            anim.SetBool("chargeattack", false);
        }

        chargeOnCooldown = true;
        chargeRoutine = null;

        // Mark when charge ended (for detecting charge attacks)
        lastChargeEndTime = Time.time;

        // Enter idle state during cooldown
        anim.SetBool("idle", true);

        yield return new WaitForSeconds(chargeCooldown);

        chargeOnCooldown = false;
        anim.SetBool("idle", false);
    }

    IEnumerator AttackRoutine()
    {
        int myToken = BeginAttackAction();
        float attackStartTime = Time.time;
        Debug.Log($"<color=yellow>Frog AttackRoutine STARTED at {attackStartTime:F2}s</color>");

        isAttacking = true;
        anim.SetBool("chargeRadius", false);
        StopCharge();

        float prevSpeed = anim.speed;
        anim.speed = attackAnimSpeed;

        // Detect if this attack came from a charge (within 0.1s of charge ending)
        bool isChargeAttack = (Time.time - lastChargeEndTime) < 0.1f;

        // Use different animator boolean for charge vs normal attack
        if (isChargeAttack)
        {
            anim.SetBool("chargeattack", true);
            Debug.Log($"<color=magenta>Frog CHARGE ATTACK animation triggered</color>");
        }
        else
        {
            anim.SetBool("attack", true);
            Debug.Log($"<color=cyan>Frog NORMAL ATTACK animation triggered</color>");
        }

        // Use different delay for charge attack vs normal attack
        float firstDamageDelay = isChargeAttack ? chargeAttackDamageDelayV2 : attackDamageDelayV2;
        bool doSecondHit = !isChargeAttack; // Only normal attacks do 2 hits

        Debug.Log($"<color=yellow>Frog attack delay: {firstDamageDelay}s, isChargeAttack: {isChargeAttack}, timeSinceCharge: {Time.time - lastChargeEndTime:F2}s</color>");

        // If delay is 0, deal damage IMMEDIATELY
        if (firstDamageDelay <= 0f)
        {
            Debug.Log($"<color=orange>Frog FIRST HIT path: delay is 0, dealing instant damage. PlayerAlive: {playerDamageable != null && playerDamageable.IsAlive}</color>");
            if (!isDead && myToken == attackActionToken && playerDamageable != null && playerDamageable.IsAlive)
            {
                Vector3 hitPoint = transform.position;
                Vector3 hitNormal = (player.position - transform.position).normalized;
                PlayerHealth.RegisterPendingAttacker(gameObject);
                playerDamageable.TakeDamage(attackDamageV2, hitPoint, hitNormal);
                float damageTime = Time.time - attackStartTime;
                Debug.Log($"<color=green>Frog dealt {attackDamageV2} damage (FIRST HIT - INSTANT) after {damageTime:F3}s</color>");
            }
            else
            {
                Debug.LogWarning($"<color=red>Frog FIRST HIT FAILED: playerDamageable null or dead!</color>");
            }
        }
        else
        {
            Debug.Log($"<color=orange>Frog FIRST HIT path: waiting {firstDamageDelay}s before damage</color>");
            // Wait for FIRST damage delay (in real time, not affected by anim speed)
            yield return new WaitForSeconds(firstDamageDelay);

            if (isDead || myToken != attackActionToken)
            {
                StopAttack();
                yield break;
            }

            // FIRST damage instance
            if (playerDamageable != null && playerDamageable.IsAlive)
            {
                Vector3 hitPoint = transform.position;
                Vector3 hitNormal = (player.position - transform.position).normalized;
                PlayerHealth.RegisterPendingAttacker(gameObject);
                playerDamageable.TakeDamage(attackDamageV2, hitPoint, hitNormal);
                Debug.Log($"<color=green>Frog dealt {attackDamageV2} damage (FIRST HIT) at {firstDamageDelay}s</color>");
            }
            else
            {
                Debug.LogWarning($"<color=red>Frog FIRST HIT FAILED after delay: playerDamageable null or dead!</color>");
            }
        }

        // SECOND damage instance (only for normal attacks)
        float totalDamageTime = firstDamageDelay;

        if (doSecondHit)
        {
            // Wait for SECOND damage delay (in real time)
            if (attackSecondDamageDelayV2 > 0f)
            {
                yield return new WaitForSeconds(attackSecondDamageDelayV2);
            }

            if (isDead || myToken != attackActionToken)
            {
                StopAttack();
                yield break;
            }

            // SECOND damage instance
            if (playerDamageable != null && playerDamageable.IsAlive)
            {
                Vector3 hitPoint = transform.position;
                Vector3 hitNormal = (player.position - transform.position).normalized;
                PlayerHealth.RegisterPendingAttacker(gameObject);
                playerDamageable.TakeDamage(attackDamageV2, hitPoint, hitNormal);
                Debug.Log($"<color=green>Frog dealt {attackDamageV2} damage (SECOND HIT) at {attackDamageDelayV2 + attackSecondDamageDelayV2}s</color>");
            }

            totalDamageTime = attackDamageDelayV2 + attackSecondDamageDelayV2;
        }

        // Wait for rest of attack duration
        float remainingTime = (attackDuration / Mathf.Max(0.01f, attackAnimSpeed)) - totalDamageTime;
        if (remainingTime > 0)
        {
            yield return new WaitForSeconds(remainingTime);
        }

        // Reset both attack booleans
        anim.SetBool("attack", false);
        anim.SetBool("chargeattack", false);
        anim.speed = prevSpeed;

        isAttacking = false;
        attackOnCooldown = true;

        // Enter idle state during cooldown
        anim.SetBool("idle", true);

        attackRoutine = null;
        float cooldown = attackCooldown;
        if (statusController != null)
        {
            cooldown += statusController.GetLethargyAttackCooldownBonus();
        }
        if (cooldown < 0f)
        {
            cooldown = 0f;
        }
        yield return new WaitForSeconds(cooldown);

        attackOnCooldown = false;
        anim.SetBool("idle", false);
    }

    void OnPlayerDeath()
    {
        rb.velocity = Vector2.zero;
        StopCharge();
        StopAttack();
    }

    void StopCharge()
    {
        if (chargeRoutine != null)
        {
            StopCoroutine(chargeRoutine);
            chargeRoutine = null;
        }
        isCharging = false;
        chargeOnCooldown = false;
        anim.SetBool("chargeRadius", false);

        rb.mass = normalMass;
    }

    void StopAttack()
    {
        CancelAttackAction();
        if (isAttacking)
        {
            isAttacking = false;
            if (attackRoutine != null)
            {
                StopCoroutine(attackRoutine);
                attackRoutine = null;
            }
            anim.SetBool("attack", false);
            anim.speed = 1f;
        }
        attackOnCooldown = false;
    }

    void HandleDeath()
    {
        if (isDead) return;
        isDead = true;

        StopCharge();
        StopAttack();
        rb.velocity = Vector2.zero;

        anim.SetBool("dead", true);
        anim.SetBool("moving", false);
        anim.SetBool("chargeRadius", false);
        anim.SetBool("attack", false);

        rb.constraints = RigidbodyConstraints2D.FreezeAll;
        if (col != null) col.enabled = false;

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
        if (sr != null)
        {
            float elapsed = 0f;
            Color startColor = sr.color;
            
            while (elapsed < deathFadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsed / deathFadeOutDuration);
                sr.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
                yield return null;
            }
        }
        
        Destroy(gameObject);
    }
}