using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CapsuleCollider2D))]
[RequireComponent(typeof(EnemyHealth))]
public class DeathBringerEnemy : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 2f;
    [SerializeField] private float attackRange = 1.5f; // Normal attack range
    [SerializeField] private float stopDistance = 1.4f; // Stops moving when this close
    [SerializeField] private float attackSpellRange = 5f; // Range to cast spell
    [SerializeField] private float attackAnimSpeed = 1.0f;
    [SerializeField] private float attackCooldown = 1.0f;
    [SerializeField] private float attackDuration = 0.5f;
    [SerializeField] private float deathCleanupDelay = 0.7f;

    [Header("Sprite Settings")]
    [Tooltip("Invert sprite flip direction (if sprite is backwards)")]
    [SerializeField] private bool invertFlip = false;

    [Header("Spell Settings")]
    [Tooltip("ATTACKSPELL ANIMATION DURATION: How long DeathBringer's attackspell animation plays")]
    [SerializeField] private float spellAnimationDuration = 1f;
    
    [Tooltip("SPELL DAMAGE DELAY: Delay for the SPELL EFFECT itself before dealing damage (independent of DeathBringer)")]
    [SerializeField] private float spellDamageDelay = 0.2f;
    
    [SerializeField] private float spellDamage = 20f;

    [SerializeField] private float spellDamageDelayV2 = -1f;
    [SerializeField] private float spellDamageV2 = -1f;
    
    [Tooltip("Projectile/effect to spawn on player's head")]
    [SerializeField] private GameObject spellEffectPrefab;
    
    [Tooltip("Duration of the spawned spell effect animation")]
    [SerializeField] private float spellEffectDuration = 2f;
    
    [Tooltip("Offset above player's head for spell effect")]
    [SerializeField] private Vector2 spellEffectOffset = new Vector2(0f, 1.5f);
    
    [Tooltip("TELEPORT ANIMATION DURATION: How long the teleport animation plays")]
    [SerializeField] private float teleportAnimationDuration = 0.5f;
    [Tooltip("ARRIVAL ANIMATION DURATION: Duration of arrival animation (plays AFTER teleporting to new position)")]
    [SerializeField] private float arrivalAnimationDuration = 0.5f;
    [Tooltip("TELEPORT DELAY: Idle time BEFORE teleport animation starts (after attackspell finishes). DeathBringer stays idle during this time.")]
    [SerializeField] private float teleportDelay = 0.3f;
    [SerializeField] private float postTeleportIdleTime = 0.5f;
    [Tooltip("Cooldown after teleporting before can cast spell again")]
    [SerializeField] private float teleportCooldown = 3f;
    
    [Header("Teleport Position Offsets")]
    [Tooltip("Offset from player when teleporting to left side (X = horizontal, Y = vertical)")]
    [SerializeField] private Vector2 teleportOffsetLeft = new Vector2(-1.5f, 0f);
    [Tooltip("Offset from player when teleporting to right side (X = horizontal, Y = vertical)")]
    [SerializeField] private Vector2 teleportOffsetRight = new Vector2(1.5f, 0f);

    [Header("Damage Settings")]
    [SerializeField] private float attackDamage = 15f;
    [Tooltip("Delay in attack animation before damage is dealt (seconds)")]
    [SerializeField] private float attackDamageDelay = 0.2f;

    [SerializeField] private float attackDamageV2 = -1f;
    [SerializeField] private float attackDamageDelayV2 = -1f;
    
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
    private bool isCastingSpell;
    private bool attackOnCooldown;
    private bool spellOnCooldown; // Spell cooldown after teleport
    private bool hasDealtDamageThisAttack = false;
    private Coroutine attackRoutine;
    private Coroutine spellRoutine;
    private Vector2 knockbackVelocity = Vector2.zero;
    private float knockbackEndTime = 0f;
    private float lastTeleportTime = -999f;
    private SpriteFlipOffset spriteFlipOffset;

    private int attackActionToken = 0;
    private int spellActionToken = 0;

    void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (animator == null) animator = GetComponent<Animator>();
        if (capsuleCollider == null) capsuleCollider = GetComponent<CapsuleCollider2D>();
        health = GetComponent<EnemyHealth>();
        statusController = GetComponent<StatusController>();

        // Phantom settings - only collide with Projectiles and Player
        capsuleCollider.isTrigger = false;
        Physics2D.IgnoreLayerCollision(gameObject.layer, LayerMask.NameToLayer("Default"), true);
        Physics2D.IgnoreLayerCollision(gameObject.layer, LayerMask.NameToLayer("Enemy"), true);

        if (AdvancedPlayerController.Instance != null)
        {
            playerDamageable = AdvancedPlayerController.Instance.GetComponent<IDamageable>();
            AdvancedPlayerController.Instance.GetComponent<PlayerHealth>().OnDeath += OnPlayerDeath;
        }
        
        spriteFlipOffset = GetComponent<SpriteFlipOffset>();

        if (attackDamageV2 < 0f)
        {
            attackDamageV2 = attackDamage;
        }
        if (attackDamageDelayV2 < 0f)
        {
            attackDamageDelayV2 = attackDamageDelay;
        }

        if (spellDamageV2 < 0f)
        {
            spellDamageV2 = spellDamage;
        }
        if (spellDamageDelayV2 < 0f)
        {
            spellDamageDelayV2 = spellDamageDelay;
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

    private int BeginSpellAction()
    {
        spellActionToken++;
        return spellActionToken;
    }

    private void CancelSpellAction()
    {
        spellActionToken++;
    }

    public bool IsSpellActionTokenValid(int token)
    {
        return !isDead && token == spellActionToken;
    }
    
    void Start()
    {
        // DISABLED: Don't auto-sync invertFlip - let user set it manually in SpriteFlipOffset Inspector
        // User can configure invertFlip directly in SpriteFlipOffset component
        SpriteFlipOffset flipOffset = GetComponent<SpriteFlipOffset>();
        if (flipOffset != null)
        {
            // Just recapture base offsets, don't modify invertFlip
            flipOffset.RecaptureBaseOffsets();
            Debug.Log("<color=purple>DeathBringer: Recaptured SpriteFlipOffset base offsets after spawn</color>");
        }
    }
    
    void OnEnable() => health.OnDeath += HandleDeath;

    void OnDisable()
    {
        health.OnDeath -= HandleDeath;
        if (AdvancedPlayerController.Instance != null)
        {
            var playerHealth = AdvancedPlayerController.Instance.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.OnDeath -= OnPlayerDeath;
            }
        }
    }

    void Update()
    {
        if (isDead || AdvancedPlayerController.Instance == null) return;
        
        // CRITICAL: Ensure collider is always enabled when alive
        if (!isDead && !capsuleCollider.enabled)
        {
            capsuleCollider.enabled = true;
            Debug.LogWarning("<color=red>DeathBringer: Collider was disabled! Re-enabling...</color>");
        }

        float distanceToPlayer = Vector2.Distance(transform.position, AdvancedPlayerController.Instance.transform.position);

        // Check if in spell range and spell is off cooldown
        if (!spellOnCooldown && !isCastingSpell && distanceToPlayer <= attackSpellRange && distanceToPlayer > attackRange)
        {
            // Cast spell (can only cast when NOT in melee range)
            if (spellRoutine == null)
            {
                spellRoutine = StartCoroutine(SpellRoutine());
            }
            return;
        }

        // If in melee range and spell just came off cooldown, wait for attack to finish then cast
        if (!spellOnCooldown && !isCastingSpell && distanceToPlayer <= attackRange && !isAttacking && !attackOnCooldown)
        {
            // Spell is ready and we're in melee - cast spell in place and reposition
            // Only cast if attack is fully finished (not on cooldown)
            if (spellRoutine == null)
            {
                spellRoutine = StartCoroutine(SpellRoutineInPlace());
            }
            return;
        }

        // Normal attack behavior (only if spell is on cooldown or out of spell range)
        if (!isAttacking && !isCastingSpell && !attackOnCooldown)
        {
            if (distanceToPlayer <= attackRange)
            {
                if (attackRoutine == null)
                {
                    attackRoutine = StartCoroutine(AttackRoutine());
                }
                return;
            }
        }

        // Movement
        if (!isAttacking && !isCastingSpell)
        {
            MoveTowardsPlayer();
        }

        // Update animator - handle moving/movingflip based on flip state
        bool isMoving = rb.velocity.sqrMagnitude > 0.0001f && !isAttacking && !isCastingSpell;
        
        // CRITICAL: Since DeathBringer uses invertFlip, we need to check the ACTUAL flip state
        // When invertFlip is true, the sprite is flipped when flipX is FALSE (inverted logic)
        bool isFlipped = spriteRenderer.flipX;
        
        if (isMoving)
        {
            animator.SetBool("moving", !isFlipped);      // Normal moving when NOT flipped
            animator.SetBool("movingflip", isFlipped);   // Flipped moving when flipped
        }
        else
        {
            animator.SetBool("moving", false);
            animator.SetBool("movingflip", false);
        }
    }

    void LateUpdate()
    {
        if (spriteFlipOffset == null) return;

        // Check animation states
        bool isWalking = animator.GetBool("moving") || animator.GetBool("movingflip");
        bool isDying = animator.GetBool("dead") || animator.GetBool("deadflip");

        // Disable SpriteFlipOffset during walking or death
        if (isWalking || isDying)
        {
            spriteFlipOffset.SetColliderOffsetEnabled(false);
            spriteFlipOffset.SetShadowOffsetEnabled(false);
        }
        else
        {
            // Enable SpriteFlipOffset for all other states (idle, attack, attackspell, teleport, arrival)
            spriteFlipOffset.SetColliderOffsetEnabled(true);
            spriteFlipOffset.SetShadowOffsetEnabled(true);
        }
    }

    void OnPlayerDeath()
    {
        rb.velocity = Vector2.zero;
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }
        if (spellRoutine != null)
        {
            StopCoroutine(spellRoutine);
            spellRoutine = null;
        }
        isAttacking = false;
        isCastingSpell = false;
        attackOnCooldown = false;
        spellOnCooldown = false;

        CancelAttackAction();
        CancelSpellAction();

        animator.SetBool("attack", false);
        animator.SetBool("attackspell", false);
        animator.SetBool("moving", false);
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
    }

    void MoveTowardsPlayer()
    {
        if (AdvancedPlayerController.Instance == null || isDead) return;

        Vector3 toPlayer = AdvancedPlayerController.Instance.transform.position - transform.position;
        float distanceToPlayer = toPlayer.magnitude;
        
        // Flip sprite based on direction (with invertFlip for backwards sprite)
        bool shouldFlip = toPlayer.x <= 0;
        spriteRenderer.flipX = invertFlip ? !shouldFlip : shouldFlip;
        
        // Stop moving if within stop distance
        if (distanceToPlayer <= stopDistance)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        float speedMult = 1f;
        if (statusController != null)
        {
            speedMult = statusController.GetEnemyMoveSpeedMultiplier();
        }
        
        rb.velocity = toPlayer.normalized * (moveSpeed * speedMult);
    }

    IEnumerator SpellRoutine()
    {
        int mySpellToken = BeginSpellAction();
        isCastingSpell = true;
        spellOnCooldown = true;
        rb.velocity = Vector2.zero;
        
        // CRITICAL: Ensure collider stays enabled during spell casting
        capsuleCollider.enabled = true;
        
        Debug.Log("<color=purple>DeathBringer: Casting spell (ranged)!</color>");
        animator.SetBool("attackspell", true);
        
        // Wait for attackspell animation to complete
        yield return new WaitForSeconds(spellAnimationDuration);

        if (isDead || mySpellToken != spellActionToken)
        {
            animator.SetBool("attackspell", false);
            isCastingSpell = false;
            spellRoutine = null;
            spellOnCooldown = false;
            yield break;
        }
        
        animator.SetBool("attackspell", false);
        
        // IMMEDIATELY spawn spell effect (independent of DeathBringer's actions)
        if (playerDamageable != null && playerDamageable.IsAlive && AdvancedPlayerController.Instance != null)
        {
            Vector3 playerPos = AdvancedPlayerController.Instance.transform.position;
            Vector3 spellPos = playerPos + (Vector3)spellEffectOffset;
            
            // Spawn spell effect with its own independent timing
            if (spellEffectPrefab != null)
            {
                GameObject spellEffect = Instantiate(spellEffectPrefab, spellPos, Quaternion.identity);
                
                // Start independent coroutine for spell damage
                StartCoroutine(SpellEffectDamageRoutine(spellEffect, mySpellToken));
            }
        }
        
        // DeathBringer immediately teleports (doesn't wait for spell)
        // Wait for teleport delay BEFORE teleport animation (idle state)
        if (teleportDelay > 0)
        {
            animator.SetBool("idle", true);
            Debug.Log($"<color=purple>DeathBringer: Idle for {teleportDelay}s before teleport</color>");
            yield return new WaitForSeconds(teleportDelay);
            animator.SetBool("idle", false);

            if (isDead || mySpellToken != spellActionToken)
            {
                isCastingSpell = false;
                spellRoutine = null;
                spellOnCooldown = false;
                yield break;
            }
        }
        
        // Play teleport animation
        animator.SetBool("teleport", true);
        // CRITICAL: Keep collider enabled during teleport
        capsuleCollider.enabled = true;
        yield return new WaitForSeconds(teleportAnimationDuration);
        animator.SetBool("teleport", false);

        if (isDead || mySpellToken != spellActionToken)
        {
            isCastingSpell = false;
            spellRoutine = null;
            spellOnCooldown = false;
            yield break;
        }
        
        // Teleport to player's side
        TeleportToPlayerSide();
        lastTeleportTime = Time.time;
        
        // CRITICAL: Re-enable collider after teleport
        capsuleCollider.enabled = true;
        
        // Play arrival animation
        animator.SetBool("arrival", true);
        // CRITICAL: Keep collider enabled during arrival
        capsuleCollider.enabled = true;
        yield return new WaitForSeconds(arrivalAnimationDuration);
        animator.SetBool("arrival", false);

        if (isDead || mySpellToken != spellActionToken)
        {
            isCastingSpell = false;
            spellRoutine = null;
            spellOnCooldown = false;
            yield break;
        }
        
        // Post-teleport idle (if duration > 0)
        if (postTeleportIdleTime > 0)
        {
            animator.SetBool("idle", true);
            yield return new WaitForSeconds(postTeleportIdleTime);
            animator.SetBool("idle", false);

            if (isDead || mySpellToken != spellActionToken)
            {
                isCastingSpell = false;
                spellRoutine = null;
                spellOnCooldown = false;
                yield break;
            }
        }
        
        isCastingSpell = false;
        spellRoutine = null;
        
        // CRITICAL: Ensure collider is enabled after spell routine completes
        capsuleCollider.enabled = true;
        
        // Start teleport cooldown
        yield return new WaitForSeconds(teleportCooldown);
        spellOnCooldown = false;
        Debug.Log("<color=purple>DeathBringer: Spell off cooldown!</color>");
    }
    
    IEnumerator SpellRoutineInPlace()
    {
        int mySpellToken = BeginSpellAction();
        isCastingSpell = true;
        spellOnCooldown = true;
        rb.velocity = Vector2.zero;
        
        // CRITICAL: Ensure collider stays enabled during spell casting
        capsuleCollider.enabled = true;
        
        Debug.Log("<color=purple>DeathBringer: Casting spell IN PLACE (melee range)!</color>");
        animator.SetBool("attackspell", true);
        
        // Wait for attackspell animation to complete
        yield return new WaitForSeconds(spellAnimationDuration);

        if (isDead || mySpellToken != spellActionToken)
        {
            animator.SetBool("attackspell", false);
            isCastingSpell = false;
            spellRoutine = null;
            spellOnCooldown = false;
            yield break;
        }
        
        animator.SetBool("attackspell", false);
        
        // IMMEDIATELY spawn spell effect (independent of DeathBringer's actions)
        if (playerDamageable != null && playerDamageable.IsAlive && AdvancedPlayerController.Instance != null)
        {
            Vector3 playerPos = AdvancedPlayerController.Instance.transform.position;
            Vector3 spellPos = playerPos + (Vector3)spellEffectOffset;
            
            // Spawn spell effect with its own independent timing
            if (spellEffectPrefab != null)
            {
                GameObject spellEffect = Instantiate(spellEffectPrefab, spellPos, Quaternion.identity);
                
                // Start independent coroutine for spell damage
                StartCoroutine(SpellEffectDamageRoutine(spellEffect, mySpellToken));
            }
        }
        
        // DeathBringer immediately teleports (doesn't wait for spell)
        // Wait for teleport delay BEFORE teleport animation (idle state)
        if (teleportDelay > 0)
        {
            animator.SetBool("idle", true);
            Debug.Log($"<color=purple>DeathBringer: Idle for {teleportDelay}s before teleport</color>");
            yield return new WaitForSeconds(teleportDelay);
            animator.SetBool("idle", false);

            if (isDead || mySpellToken != spellActionToken)
            {
                isCastingSpell = false;
                spellRoutine = null;
                spellOnCooldown = false;
                yield break;
            }
        }
        
        // Play teleport animation
        animator.SetBool("teleport", true);
        // CRITICAL: Keep collider enabled during teleport
        capsuleCollider.enabled = true;
        yield return new WaitForSeconds(teleportAnimationDuration);
        animator.SetBool("teleport", false);

        if (isDead || mySpellToken != spellActionToken)
        {
            isCastingSpell = false;
            spellRoutine = null;
            spellOnCooldown = false;
            yield break;
        }
        
        // Teleport to OPPOSITE side of player
        TeleportToOppositeSide();
        lastTeleportTime = Time.time;
        
        // CRITICAL: Re-enable collider after teleport
        capsuleCollider.enabled = true;
        
        // Play arrival animation
        animator.SetBool("arrival", true);
        // CRITICAL: Keep collider enabled during arrival
        capsuleCollider.enabled = true;
        yield return new WaitForSeconds(arrivalAnimationDuration);
        animator.SetBool("arrival", false);

        if (isDead || mySpellToken != spellActionToken)
        {
            isCastingSpell = false;
            spellRoutine = null;
            spellOnCooldown = false;
            yield break;
        }
        
        // Post-teleport idle (if duration > 0)
        if (postTeleportIdleTime > 0)
        {
            animator.SetBool("idle", true);
            yield return new WaitForSeconds(postTeleportIdleTime);
            animator.SetBool("idle", false);

            if (isDead || mySpellToken != spellActionToken)
            {
                isCastingSpell = false;
                spellRoutine = null;
                spellOnCooldown = false;
                yield break;
            }
        }
        
        isCastingSpell = false;
        spellRoutine = null;
        
        // CRITICAL: Ensure collider is enabled after spell routine completes
        capsuleCollider.enabled = true;
        
        // Start teleport cooldown
        yield return new WaitForSeconds(teleportCooldown);
        spellOnCooldown = false;
        Debug.Log("<color=purple>DeathBringer: Spell off cooldown!</color>");
    }

    /// <summary>
    /// Independent coroutine for spell effect damage timing
    /// Attached to spell effect itself so it persists even if DeathBringer dies
    /// </summary>
    IEnumerator SpellEffectDamageRoutine(GameObject spellEffect, int mySpellToken)
    {
        // Add independent component to spell effect to handle damage
        SpellEffectController controller = spellEffect.AddComponent<SpellEffectController>();
        controller.Initialize(spellDamageV2, spellDamageDelayV2, spellEffectDuration, playerDamageable, gameObject, transform.position, health, this, mySpellToken);
        
        yield break; // Exit immediately - controller handles everything
    }
    
    void TeleportToPlayerSide()
    {
        if (AdvancedPlayerController.Instance == null) return;
        
        Vector3 playerPos = AdvancedPlayerController.Instance.transform.position;
        
        // Determine which side is closer (left or right)
        Vector3 leftPos = playerPos + new Vector3(teleportOffsetLeft.x, teleportOffsetLeft.y, 0f);
        Vector3 rightPos = playerPos + new Vector3(teleportOffsetRight.x, teleportOffsetRight.y, 0f);
        
        float distToLeft = Vector3.Distance(transform.position, leftPos);
        float distToRight = Vector3.Distance(transform.position, rightPos);
        
        bool teleportingToLeft = distToLeft < distToRight;
        Vector3 teleportPos = teleportingToLeft ? leftPos : rightPos;
        
        // IMPORTANT: Set position BEFORE flipping sprite
        transform.position = teleportPos;
        
        // CRITICAL: Ensure collider is enabled
        if (capsuleCollider != null)
        {
            capsuleCollider.enabled = true;
            Debug.Log($"<color=green>DeathBringer: Collider enabled = {capsuleCollider.enabled}</color>");
        }
        
        // CRITICAL: Force physics system to update collider position immediately
        // Call multiple times to ensure it takes effect
        Physics2D.SyncTransforms();
        Physics2D.SyncTransforms();
        
        Debug.Log($"<color=purple>DeathBringer teleported to {(teleportingToLeft ? "LEFT" : "RIGHT")} side at {teleportPos}</color>");
        
        // Determine flip state based on player direction
        Vector3 newToPlayer = playerPos - transform.position;
        bool shouldFlip = newToPlayer.x <= 0;
        bool newFlipState = invertFlip ? !shouldFlip : shouldFlip;
        
        Debug.Log($"<color=purple>DeathBringer flip logic: toPlayer.x={newToPlayer.x}, shouldFlip={shouldFlip}, invertFlip={invertFlip}, newFlipState={newFlipState}</color>");
        
        // Change sprite flip state
        spriteRenderer.flipX = newFlipState;
        
        // Force reapply offset after teleport
        SpriteFlipOffset flipOffset = GetComponent<SpriteFlipOffset>();
        if (flipOffset != null)
        {
            flipOffset.ForceReapplyOffset();
            Debug.Log("<color=green>DeathBringer: Forced SpriteFlipOffset reapply after teleport</color>");
        }
        
        // CRITICAL: Sync physics again after all changes
        Physics2D.SyncTransforms();
    }
    
    void TeleportToOppositeSide()
    {
        if (AdvancedPlayerController.Instance == null) return;
        
        Vector3 playerPos = AdvancedPlayerController.Instance.transform.position;
        Vector3 currentOffset = transform.position - playerPos;
        
        // Teleport to opposite side with offsets
        bool isOnLeft = currentOffset.x < 0;
        Vector3 teleportPos = isOnLeft ? 
            playerPos + new Vector3(teleportOffsetRight.x, teleportOffsetRight.y, 0f) : 
            playerPos + new Vector3(teleportOffsetLeft.x, teleportOffsetLeft.y, 0f);
        
        // IMPORTANT: Set position BEFORE flipping sprite
        transform.position = teleportPos;
        
        // CRITICAL: Ensure collider is enabled
        if (capsuleCollider != null)
        {
            capsuleCollider.enabled = true;
            Debug.Log($"<color=green>DeathBringer: Collider enabled = {capsuleCollider.enabled}</color>");
        }
        
        // CRITICAL: Force physics system to update collider position immediately
        // Call multiple times to ensure it takes effect
        Physics2D.SyncTransforms();
        Physics2D.SyncTransforms();
        
        Debug.Log($"<color=purple>DeathBringer teleported to OPPOSITE side (was {(isOnLeft ? "LEFT" : "RIGHT")}, now {(isOnLeft ? "RIGHT" : "LEFT")}) at {teleportPos}</color>");
        
        // Determine flip state based on player direction
        Vector3 newToPlayer = playerPos - transform.position;
        bool shouldFlip = newToPlayer.x <= 0;
        bool newFlipState = invertFlip ? !shouldFlip : shouldFlip;
        
        Debug.Log($"<color=purple>DeathBringer flip logic: toPlayer.x={newToPlayer.x}, shouldFlip={shouldFlip}, invertFlip={invertFlip}, newFlipState={newFlipState}</color>");
        
        // Change sprite flip state
        spriteRenderer.flipX = newFlipState;
        
        // Force reapply offset after teleport
        SpriteFlipOffset flipOffset = GetComponent<SpriteFlipOffset>();
        if (flipOffset != null)
        {
            flipOffset.ForceReapplyOffset();
            Debug.Log("<color=green>DeathBringer: Forced SpriteFlipOffset reapply after teleport</color>");
        }
        
        // CRITICAL: Sync physics again after all changes
        Physics2D.SyncTransforms();
    }

    IEnumerator AttackRoutine()
    {
        int myAttackToken = BeginAttackAction();
        isAttacking = true;
        attackOnCooldown = true; // Set cooldown immediately to prevent attack spam
        hasDealtDamageThisAttack = false;
        animator.SetBool("attack", true);
        float originalSpeed = animator.speed;
        animator.speed = attackAnimSpeed;

        // Wait for damage delay
        if (attackDamageDelayV2 > 0f)
        {
            yield return new WaitForSeconds(attackDamageDelayV2);
        }

        if (isDead || myAttackToken != attackActionToken)
        {
            animator.SetBool("attack", false);
            animator.speed = originalSpeed;
            isAttacking = false;
            attackOnCooldown = false;
            attackRoutine = null;
            yield break;
        }

        // Deal damage
        if (!hasDealtDamageThisAttack && playerDamageable != null && playerDamageable.IsAlive && 
            AdvancedPlayerController.Instance != null && AdvancedPlayerController.Instance.enabled)
        {
            Vector3 hitPoint = AdvancedPlayerController.Instance.transform.position;
            Vector3 hitNormal = (AdvancedPlayerController.Instance.transform.position - transform.position).normalized;
            PlayerHealth.RegisterPendingAttacker(gameObject);
            playerDamageable.TakeDamage(attackDamageV2, hitPoint, hitNormal);
            hasDealtDamageThisAttack = true;
            Debug.Log($"<color=cyan>DeathBringer dealt {attackDamageV2} damage</color>");
        }

        // Wait for rest of attack duration
        float remainingAttackTime = attackDuration - attackDamageDelayV2;
        if (remainingAttackTime > 0f)
        {
            yield return new WaitForSeconds(remainingAttackTime);
        }

        animator.SetBool("attack", false);
        animator.speed = originalSpeed;
        isAttacking = false;
        
        // Set idle state during cooldown
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
        
        // Cooldown complete, exit idle
        animator.SetBool("idle", false);
        
        attackOnCooldown = false;
        attackRoutine = null;
    }

    void HandleDeath()
    {
        if (isDead) return;
        isDead = true;

        CancelAttackAction();
        CancelSpellAction();

        if (attackRoutine != null) StopCoroutine(attackRoutine);
        if (spellRoutine != null) StopCoroutine(spellRoutine);

        // CRITICAL: Set death animation based on flip state
        // DeathBringer uses invertFlip, so flipX logic is inverted
        bool isFlipped = spriteRenderer.flipX;
        animator.SetBool("dead", !isFlipped);      // Normal death when NOT flipped
        animator.SetBool("deadflip", isFlipped);   // Flipped death when flipped
        
        // Disable all other animation states
        animator.SetBool("moving", false);
        animator.SetBool("movingflip", false);
        animator.SetBool("attack", false);
        animator.SetBool("attackspell", false);
        animator.SetBool("idle", false);
        animator.SetBool("teleport", false);
        animator.SetBool("arrival", false);
        
        rb.velocity = Vector2.zero;
        knockbackVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Static;
        
        capsuleCollider.enabled = false;

        Destroy(gameObject, deathCleanupDelay);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (isDead) return;

        if (collision.gameObject.CompareTag("Projectile"))
        {
            // Projectiles handle damage via EnemyHealth component
        }
    }

    public void ApplyKnockback(Vector2 direction, float force)
    {
        if (isDead) return;
        
        knockbackVelocity = direction.normalized * force * knockbackIntensity;
        knockbackEndTime = Time.time + knockbackDuration;

        CancelAttackAction();
        CancelSpellAction();

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }
        if (spellRoutine != null)
        {
            StopCoroutine(spellRoutine);
            spellRoutine = null;
        }

        isAttacking = false;
        isCastingSpell = false;
        attackOnCooldown = false;
        spellOnCooldown = false;

        animator.SetBool("attack", false);
        animator.SetBool("attackspell", false);
        animator.SetBool("teleport", false);
        animator.SetBool("arrival", false);
        animator.SetBool("idle", false);
        
        // If knocked out of spell range and spell is off cooldown, allow recasting
        if (!spellOnCooldown && AdvancedPlayerController.Instance != null)
        {
            float distAfterKnockback = Vector2.Distance(transform.position, AdvancedPlayerController.Instance.transform.position);
            if (distAfterKnockback > attackRange && distAfterKnockback <= attackSpellRange)
            {
                Debug.Log("<color=purple>DeathBringer knocked back - will recast spell when in range</color>");
            }
        }
    }
}
