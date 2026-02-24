using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

/// <summary>
/// Manages game state changes like player death
/// </summary>
public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }

    private static int skipPauseSafeDeltaTimeUntilFrame = -1;

    private static EnemyCardSpawner cachedEnemyCardSpawner;
    private static EnemySpawner cachedEnemySpawner;

    private static bool manualPauseActive = false;
    public static bool ManualPauseActive => manualPauseActive;

    private static float pauseSafeTime = 0f;
    public static float PauseSafeTime => pauseSafeTime;

    public static float GetPauseSafeDeltaTime()
    {
        if (CardSelectionManager.Instance != null && CardSelectionManager.Instance.IsSelectionActive())
        {
            return 0f;
        }

        if (Time.timeScale <= 0.0001f)
        {
            return 0f;
        }

        if (Time.frameCount <= skipPauseSafeDeltaTimeUntilFrame)
        {
            return 0f;
        }

        return Time.deltaTime;
    }

    public static float GetRunTimerDeltaTime()
    {
        float dt = GetPauseSafeDeltaTime();
        if (dt <= 0f)
        {
            return 0f;
        }

        if (Instance != null && Instance.PlayerIsDead)
        {
            return 0f;
        }

        if (IsBossOrPostBossDelayActive())
        {
            return 0f;
        }

        return dt;
    }

    private static bool IsBossOrPostBossDelayActive()
    {
        if (cachedEnemyCardSpawner == null)
        {
            cachedEnemyCardSpawner = Object.FindObjectOfType<EnemyCardSpawner>();
        }

        if (cachedEnemyCardSpawner != null)
        {
            if (cachedEnemyCardSpawner.IsBossEventActive || cachedEnemyCardSpawner.IsPostBossRefillDelayActive)
            {
                return true;
            }
        }

        if (cachedEnemySpawner == null)
        {
            cachedEnemySpawner = Object.FindObjectOfType<EnemySpawner>();
        }

        if (cachedEnemySpawner != null)
        {
            if (cachedEnemySpawner.IsBossEventActive || cachedEnemySpawner.IsPostBossRefillDelayActive || cachedEnemySpawner.IsPostBossSpawnDelayActive)
            {
                return true;
            }
        }

        return false;
    }

    public static IEnumerator WaitForPauseSafeSeconds(float seconds)
    {
        if (seconds <= 0f)
        {
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += GetPauseSafeDeltaTime();
            yield return null;
        }
    }

    private bool playerIsDead = false;
    public bool PlayerIsDead => playerIsDead;

    public int ForceIdlePerFrame = 25;

    private Coroutine forceIdleBatchRoutine;

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

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            return;
        }

        skipPauseSafeDeltaTimeUntilFrame = Mathf.Max(skipPauseSafeDeltaTimeUntilFrame, Time.frameCount + 1);
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            return;
        }

        skipPauseSafeDeltaTimeUntilFrame = Mathf.Max(skipPauseSafeDeltaTimeUntilFrame, Time.frameCount + 1);
    }

    private void Start()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TryBindToPlayerDeath();
        DisableJoiningIfNoPlayerPrefab();
    }

    private void Update()
    {
        if (!playerIsDead && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            if (SkillTreeUI.Instance != null && SkillTreeUI.Instance.IsOpen)
            {
                return;
            }

            CardSelectionManager selection = CardSelectionManager.Instance;
            if (selection == null || !selection.IsSelectionActive())
            {
                ToggleManualPause();
            }
        }

        pauseSafeTime += GetPauseSafeDeltaTime();
    }

    public static void ToggleManualPause()
    {
        SetManualPause(!manualPauseActive);
    }

    public static void SetManualPause(bool active)
    {
        manualPauseActive = active;

        if (manualPauseActive)
        {
            Time.timeScale = 0f;
        }
        else
        {
            CardSelectionManager selection = CardSelectionManager.Instance;
            if (selection == null || !selection.IsSelectionActive())
            {
                Time.timeScale = 1f;
            }
        }

        skipPauseSafeDeltaTimeUntilFrame = Mathf.Max(skipPauseSafeDeltaTimeUntilFrame, Time.frameCount + 1);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnbindFromPlayerDeath();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        playerIsDead = false;
        UnbindFromPlayerDeath();
        TryBindToPlayerDeath();
        DisableJoiningIfNoPlayerPrefab();
    }

    private void DisableJoiningIfNoPlayerPrefab()
    {
        PlayerInputManager mgr = FindObjectOfType<PlayerInputManager>();
        if (mgr == null)
        {
            return;
        }

        if (mgr.playerPrefab != null)
        {
            return;
        }

        mgr.DisableJoining();
        mgr.joinBehavior = PlayerJoinBehavior.JoinPlayersManually;
    }

    private void TryBindToPlayerDeath()
    {
        if (AdvancedPlayerController.Instance == null)
        {
            return;
        }

        var playerHealth = AdvancedPlayerController.Instance.GetComponent<PlayerHealth>();
        if (playerHealth == null)
        {
            return;
        }

        playerHealth.OnDeath -= HandlePlayerDeath;
        playerHealth.OnDeath += HandlePlayerDeath;
    }

    private void UnbindFromPlayerDeath()
    {
        if (AdvancedPlayerController.Instance == null)
        {
            return;
        }

        var playerHealth = AdvancedPlayerController.Instance.GetComponent<PlayerHealth>();
        if (playerHealth == null)
        {
            return;
        }

        playerHealth.OnDeath -= HandlePlayerDeath;
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

    public void ResetRunState()
    {
        playerIsDead = false;
        activeProjectiles.Clear();
        SetAllEnemiesImmuneToPlayerDeath(false);
        RuntimeProjectileRadiusGizmoManager.ResetGlobalStarGizmoState();

        GameObject player = null;
        if (AdvancedPlayerController.Instance != null)
        {
            player = AdvancedPlayerController.Instance.gameObject;
        }
        else if (PlayerController.Instance != null)
        {
            player = PlayerController.Instance.gameObject;
        }
        else
        {
            player = GameObject.FindGameObjectWithTag("Player");
        }

        if (player != null)
        {
            FavourEffectManager favourManager = player.GetComponent<FavourEffectManager>();
            if (favourManager != null)
            {
                favourManager.ClearAllEffects();
            }
        }

        ExtraExpPerRarityFavour.ResetRunState();
    }

    /// <summary>
    /// Handle player death - force all enemies to idle, make them immune, disable exp
    /// </summary>
    private void HandlePlayerDeath()
    {
        playerIsDead = true;
        Debug.Log("<color=red>GameStateManager: Player died! Forcing enemies to idle and making them immune</color>");

        if (CardSelectionManager.Instance != null)
        {
            CardSelectionManager.Instance.ForceCloseSelectionUI();
        }

        // DON'T freeze projectiles - let them continue
        // Instead, force all enemies to idle state (after they finish current anim)
        ForceAllEnemiesToIdle();

        // Make all enemies immune to damage
        SetAllEnemiesImmuneToPlayerDeath(true);

        // Disable EXP gain (handled by EnemyHealth checking PlayerIsDead)
    }

    private static bool IsDeathStateName(AnimatorStateInfo stateInfo)
    {
        // Keep your original names + add deathflip variants
        return stateInfo.IsName("dead")
            || stateInfo.IsName("death")
            || stateInfo.IsName("Death")
            || stateInfo.IsName("deathflip")
            || stateInfo.IsName("DeathFlip")
            || stateInfo.IsName("DEATHFLIP");
    }

    /// <summary>
    /// Force all enemies to idle state (unless they're dying).
    /// Change: wait for current animation to finish before snapping to idle.
    /// </summary>
    private void ForceAllEnemiesToIdle()
    {
        // Find all enemy scripts in the scene
        MonoBehaviour[] allEnemies = FindObjectsOfType<MonoBehaviour>();

        List<EnemyIdleRequest> requests = new List<EnemyIdleRequest>();

        foreach (MonoBehaviour enemy in allEnemies)
        {
            if (!enemy.GetType().Name.Contains("Enemy")) continue;

            if (enemy is SkeletonEnemy || enemy is SkellySwordEnemy || enemy is SkellySmithEnemy || enemy is SkellyArcherEnemy)
            {
                continue;
            }

            Animator animator = enemy.GetComponent<Animator>();
            if (animator == null) continue;

            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (IsDeathStateName(stateInfo))
            {
                Debug.Log($"<color=yellow>{enemy.name} is dying, letting death animation play</color>");
                continue;
            }

            requests.Add(new EnemyIdleRequest(enemy, animator));
        }

        if (forceIdleBatchRoutine != null)
        {
            StopCoroutine(forceIdleBatchRoutine);
            forceIdleBatchRoutine = null;
        }

        if (requests.Count == 0)
        {
            Debug.Log("<color=yellow>Scheduled 0 enemies to go idle after finishing current animation</color>");
            return;
        }

        if (ForceIdlePerFrame <= 0)
        {
            int scheduledCount = 0;
            for (int i = 0; i < requests.Count; i++)
            {
                EnemyIdleRequest r = requests[i];
                if (r.enemy == null || r.animator == null) continue;
                StartCoroutine(ForceEnemyToIdleAfterCurrentAnim(r.enemy, r.animator));
                scheduledCount++;
            }
            Debug.Log($"<color=yellow>Scheduled {scheduledCount} enemies to go idle after finishing current animation</color>");
            return;
        }

        forceIdleBatchRoutine = StartCoroutine(ForceAllEnemiesToIdleBatched(requests));
    }

    private readonly struct EnemyIdleRequest
    {
        public readonly MonoBehaviour enemy;
        public readonly Animator animator;

        public EnemyIdleRequest(MonoBehaviour enemy, Animator animator)
        {
            this.enemy = enemy;
            this.animator = animator;
        }
    }

    private IEnumerator ForceAllEnemiesToIdleBatched(List<EnemyIdleRequest> requests)
    {
        int count = requests.Count;
        for (int i = 0; i < count - 1; i++)
        {
            int j = Random.Range(i, count);
            EnemyIdleRequest tmp = requests[i];
            requests[i] = requests[j];
            requests[j] = tmp;
        }

        int perFrame = Mathf.Max(1, ForceIdlePerFrame);
        int scheduledCount = 0;

        for (int i = 0; i < count; i++)
        {
            EnemyIdleRequest r = requests[i];
            if (r.enemy != null && r.animator != null)
            {
                StartCoroutine(ForceEnemyToIdleAfterCurrentAnim(r.enemy, r.animator));
                scheduledCount++;
            }

            if ((i + 1) % perFrame == 0)
            {
                yield return null;
            }
        }

        Debug.Log($"<color=yellow>Scheduled {scheduledCount} enemies to go idle after finishing current animation</color>");
        forceIdleBatchRoutine = null;
    }

    private IEnumerator ForceEnemyToIdleAfterCurrentAnim(MonoBehaviour enemy, Animator animator)
    {
        if (enemy == null || animator == null)
        {
            yield break;
        }

        // Capture current state at the moment of death.
        int layer = 0;
        int startingStateHash = animator.GetCurrentAnimatorStateInfo(layer).shortNameHash;

        // Wait until the current animation finishes (or the animator transitions elsewhere).
        // We avoid forcing idle during transitions; we also bail if the enemy enters a death state.
        while (enemy != null && animator != null)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(layer);

            // If it became a death animation while waiting, do nothing.
            if (IsDeathStateName(stateInfo))
            {
                yield break;
            }

            // If the animator has already changed to a different state naturally, update what we consider "current".
            // This prevents getting stuck if the enemy loops between states.
            if (stateInfo.shortNameHash != startingStateHash)
            {
                startingStateHash = stateInfo.shortNameHash;
            }

            // Only consider "finished" when not transitioning and normalizedTime has reached the end.
            // normalizedTime can exceed 1 for non-looping clips; for looping clips it keeps increasing.
            // This will wait for the first full cycle to complete.
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

        // Now apply your existing "freeze and force idle" behavior.
        FreezeEnemyAndForceIdle(enemy, animator);
    }

    private void FreezeEnemyAndForceIdle(MonoBehaviour enemy, Animator animator)
    {
        if (enemy == null || animator == null) return;

        // FORCE STOP ALL COROUTINES (enemy-specific coroutines)
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

        // FORCE play idle animation (use your state fallbacks). Keep CrossFade time at 0f for instant switch *after* anim finished.
        if (animator.HasState(0, Animator.StringToHash("idle")))
        {
            animator.CrossFade("idle", 0f, 0, 0f);
        }
        else if (animator.HasState(0, Animator.StringToHash("Idle")))
        {
            animator.CrossFade("Idle", 0f, 0, 0f);
        }
        else if (animator.HasState(0, Animator.StringToHash("IDLE")))
        {
            animator.CrossFade("IDLE", 0f, 0, 0f);
        }
        else
        {
            animator.Play(0, 0, 0f);
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
        }

        Debug.Log($"<color=yellow>Forced {enemy.name} to idle AFTER finishing current animation</color>");
    }

    /// <summary>
    /// Make all enemies immune to damage
    /// </summary>
    private void SetAllEnemiesImmuneToPlayerDeath(bool immune)
    {
        EnemyHealth[] allEnemies = FindObjectsOfType<EnemyHealth>();

        foreach (EnemyHealth enemy in allEnemies)
        {
            if (enemy != null)
            {
                enemy.SetImmuneToPlayerDeath(immune);
            }
        }

        Debug.Log($"<color=yellow>Set immuneToPlayerDeath={immune} for {allEnemies.Length} enemies</color>");
    }
}