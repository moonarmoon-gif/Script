using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Manages game state changes like player death
/// </summary>
public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }
    
    private bool playerIsDead = false;
    public bool PlayerIsDead => playerIsDead;
    
    // Track all active projectiles
    private List<GameObject> activeProjectiles = new List<GameObject>();
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        // Subscribe to player death
        if (AdvancedPlayerController.Instance != null)
        {
            var playerHealth = AdvancedPlayerController.Instance.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.OnDeath += HandlePlayerDeath;
            }
        }
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from player death
        if (AdvancedPlayerController.Instance != null)
        {
            var playerHealth = AdvancedPlayerController.Instance.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.OnDeath -= HandlePlayerDeath;
            }
        }
    }
    
    /// <summary>
    /// Register a projectile so it can be frozen on player death
    /// </summary>
    public void RegisterProjectile(GameObject projectile)
    {
        if (projectile != null && !activeProjectiles.Contains(projectile))
        {
            activeProjectiles.Add(projectile);
        }
    }
    
    /// <summary>
    /// Unregister a projectile (when it's destroyed)
    /// </summary>
    public void UnregisterProjectile(GameObject projectile)
    {
        if (projectile != null && activeProjectiles.Contains(projectile))
        {
            activeProjectiles.Remove(projectile);
        }
    }
    
    /// <summary>
    /// Handle player death - force all enemies to idle, make them immune, disable exp
    /// </summary>
    private void HandlePlayerDeath()
    {
        playerIsDead = true;
        Debug.Log("<color=red>GameStateManager: Player died! Forcing enemies to idle and making them immune</color>");
        
        // DON'T freeze projectiles - let them continue
        // Instead, force all enemies to idle state
        ForceAllEnemiesToIdle();
        
        // Make all enemies immune to damage
        MakeAllEnemiesImmune();
        
        // Disable EXP gain (handled by EnemyHealth checking PlayerIsDead)
    }

    private static bool IsDeathStateName(AnimatorStateInfo stateInfo)
    {
        // Keep original names + include deathflip variants
        return stateInfo.IsName("dead")
               || stateInfo.IsName("death")
               || stateInfo.IsName("Death")
               || stateInfo.IsName("deathflip")
               || stateInfo.IsName("DeathFlip")
               || stateInfo.IsName("DEATHFLIP");
    }
    
    /// <summary>
    /// Force all enemies to idle state (unless they're dying)
    /// </summary>
    private void ForceAllEnemiesToIdle()
    {
        // Find all enemy scripts in the scene
        MonoBehaviour[] allEnemies = FindObjectsOfType<MonoBehaviour>();

        int scheduledCount = 0;
        foreach (MonoBehaviour enemy in allEnemies)
        {
            // Check if this is an enemy script (has "Enemy" in the type name)
            if (!enemy.GetType().Name.Contains("Enemy")) continue;

            // Special-case skeleton enemies: let their own OnPlayerDeath logic
            // handle freezing so that digout/digoutflip summon animations can
            // finish naturally instead of being snapped to idle here.
            if (enemy is SkeletonEnemy || enemy is SkellySwordEnemy || enemy is SkellySmithEnemy || enemy is SkellyArcherEnemy)
            {
                continue;
            }
            
            // Get animator
            Animator animator = enemy.GetComponent<Animator>();
            if (animator == null) continue;
            
            // Check if enemy is dying (death/deathflip animation playing)
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (IsDeathStateName(stateInfo))
            {
                Debug.Log($"<color=yellow>{enemy.name} is dying, letting death animation play</color>");
                continue;
            }

            // Wait for current animation to finish, then force idle + freeze
            StartCoroutine(ForceEnemyToIdleAfterCurrentAnim(enemy, animator));
            scheduledCount++;
        }

        Debug.Log($"<color=yellow>Scheduled {scheduledCount} enemies to go idle after finishing current animation</color>");
    }

    private IEnumerator ForceEnemyToIdleAfterCurrentAnim(MonoBehaviour enemy, Animator animator)
    {
        if (enemy == null || animator == null)
        {
            yield break;
        }

        const int layer = 0;

        // Track the current state so if it changes naturally we still wait for that
        // new state's first full cycle to complete.
        int currentStateHash = animator.GetCurrentAnimatorStateInfo(layer).shortNameHash;

        while (enemy != null && animator != null)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(layer);

            // If it became a death animation while waiting, do nothing.
            if (IsDeathStateName(stateInfo))
            {
                yield break;
            }

            // If the state changed, start waiting for the new state.
            if (stateInfo.shortNameHash != currentStateHash)
            {
                currentStateHash = stateInfo.shortNameHash;
            }

            // Wait for animation to finish (or one full loop).
            if (!animator.IsInTransition(layer) && stateInfo.normalizedTime >= 1f)
            {
                break;
            }

            yield return null;
        }

        if (enemy == null || animator == null)
        {
            yield break;
        }

        FreezeEnemyAndForceIdle(enemy, animator);
    }

    private void FreezeEnemyAndForceIdle(MonoBehaviour enemy, Animator animator)
    {
        if (enemy == null || animator == null) return;

        // FORCE STOP ALL COROUTINES
        enemy.StopAllCoroutines();

        // DISABLE enemy script to stop Update() from running
        enemy.enabled = false;

        // Reset ALL animator parameters to ensure clean state
        animator.SetBool("IsIdle", true); // CORRECT idle parameter for ArcaneArcher, FemaleNecromancer, FireWorm, ShadowEnemy
        animator.SetBool("idle", true);
        animator.SetBool("moving", false);
        animator.SetBool("movingflip", false);
        animator.SetBool("attack", false);
        animator.SetBool("attackspell", false);
        animator.SetBool("teleport", false);
        animator.SetBool("arrival", false);
        animator.SetBool("summon", false);
        animator.SetBool("walk", false);
        animator.SetBool("run", false);
        animator.SetBool("isWalking", false);
        animator.SetBool("IsWalking", false);
        animator.SetBool("isAttacking", false);
        animator.SetBool("IsAttacking", false);
        animator.SetBool("IsBouncing", false);
        animator.SetBool("IsBounceAttacking", false);
        animator.SetBool("IsBounceInterrupted", false);
        animator.SetBool("reload", false);

        // Reset any speed/trigger parameters that might interfere
        animator.SetFloat("speed", 0f);
        animator.SetFloat("Speed", 0f);
        animator.ResetTrigger("attack");
        animator.ResetTrigger("Attack");

        // FORCE play idle animation after current anim finishes
        // Try multiple possible idle state names
        if (animator.HasState(0, Animator.StringToHash("idle")))
        {
            animator.CrossFade("idle", 0f, 0, 0f);
            Debug.Log($"<color=green>  → Forced 'idle' animation</color>");
        }
        else if (animator.HasState(0, Animator.StringToHash("Idle")))
        {
            animator.CrossFade("Idle", 0f, 0, 0f);
            Debug.Log($"<color=green>  → Forced 'Idle' animation</color>");
        }
        else if (animator.HasState(0, Animator.StringToHash("IDLE")))
        {
            animator.CrossFade("IDLE", 0f, 0, 0f);
            Debug.Log($"<color=green>  → Forced 'IDLE' animation</color>");
        }
        else
        {
            // Fallback: just play whatever is at index 0
            animator.Play(0, 0, 0f);
            Debug.Log($"<color=yellow>  → No idle state found, playing default state</color>");
        }

        // Stop movement COMPLETELY
        Rigidbody2D rb = enemy.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic; // Make kinematic to prevent any physics
        }

        // CRITICAL: Enable SpriteFlipOffset collider and shadow offsets for proper positioning
        SpriteFlipOffset spriteFlipOffset = enemy.GetComponent<SpriteFlipOffset>();
        if (spriteFlipOffset != null)
        {
            spriteFlipOffset.SetColliderOffsetEnabled(true);
            spriteFlipOffset.SetShadowOffsetEnabled(true);
            Debug.Log($"<color=cyan>  → Enabled SpriteFlipOffset collider and shadow offsets for {enemy.name}</color>");
        }

        Debug.Log($"<color=yellow>Forced {enemy.name} to idle after finishing current animation</color>");
    }
    
    /// <summary>
    /// Make all enemies immune to damage
    /// </summary>
    private void MakeAllEnemiesImmune()
    {
        EnemyHealth[] allEnemies = FindObjectsOfType<EnemyHealth>();
        
        foreach (EnemyHealth enemy in allEnemies)
        {
            if (enemy != null)
            {
                enemy.SetImmuneToPlayerDeath(true);
            }
        }
        
        Debug.Log($"<color=yellow>Made {allEnemies.Length} enemies immune to damage</color>");
    }
}
