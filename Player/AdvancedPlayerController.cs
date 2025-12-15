using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Advanced player controller with projectile switching and side-swapping
/// </summary>
public class AdvancedPlayerController : MonoBehaviour
{
    public static AdvancedPlayerController Instance;

    [Header("Components")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Animator animator;
    [SerializeField] private float movespeed;
    [SerializeField] private Transform firePoint;
    [Tooltip("Firepoint for ElementalBeam when firing at enemies on the LEFT side of screen")]
    public Transform elementalBeamFirePointLeft;
    [Tooltip("Firepoint for ElementalBeam when firing at enemies on the RIGHT side of screen")]
    public Transform elementalBeamFirePointRight;

    [Header("Projectile Pairs - Set 1 (Fireball/Icicle)")]
    [SerializeField] private GameObject fireballPrefab;
    [SerializeField] private GameObject iciclePrefab;
    [Tooltip("Enhanced version of Fireball (FireBomb) - auto-swaps when enhanced")]
    [SerializeField] private GameObject fireballEnhancedPrefab;
    [Tooltip("Enhanced version of Icicle (IceLancer) - auto-swaps when enhanced")]
    [SerializeField] private GameObject icicleEnhancedPrefab;
    
    [Header("Auto-Fire Settings for Set 1")]
    [Tooltip("Enable auto-firing for Projectile Pair Set 1")]
    public bool enableAutoFire = true;
    [Tooltip("Show auto-fire gizmo in Scene view")]
    [SerializeField] private bool showAutoFireGizmo = true;
    [Tooltip("When CHECKED: Fire/Ice have independent cooldowns. When UNCHECKED: They share cooldown")]
    [SerializeField] private bool useIndependentCooldowns = true;
    private float lastAutoFireTime = -999f;

    [Header("Auto-Fire Area Points (Optional Square)")]
    [Tooltip("Optional corner A of auto-fire area (e.g., top-left)")]
    [SerializeField] private Transform autoFirePointA;
    [Tooltip("Optional corner B of auto-fire area (e.g., top-right)")]
    [SerializeField] private Transform autoFirePointB;
    [Tooltip("Optional corner C of auto-fire area (e.g., bottom-right)")]
    [SerializeField] private Transform autoFirePointC;
    [Tooltip("Optional corner D of auto-fire area (e.g., bottom-left)")]
    [SerializeField] private Transform autoFirePointD;

    [Header("Projectile Pairs - Set 2 (Tornado)")]
    [SerializeField] private GameObject fireTornadoPrefab;
    [SerializeField] private GameObject iceTornadoPrefab;

    [Header("Projectile Pairs - Set 3 (Custom)")]
    [SerializeField] private GameObject fireCustom1Prefab;
    [SerializeField] private GameObject iceCustom1Prefab;

    [Header("References")]
    public Camera cam;
    public Input_System playerControls;
    
    [Tooltip("Offset for determining if objects are 'on-camera' (0.1 = 10% outside viewport)")]
    public float onCameraOffset = 0.1f;
    
    [Header("Offscreen Damage Settings")]
    [Tooltip("Offset for offscreen damage prevention. LOWER = stricter (0.0 = only on-screen, 0.15 = 15% outside viewport allowed). Enemies outside this area won't take damage.")]
    public float offscreenDamageOffset = 0.15f;

    private InputAction move;
    private InputAction fire;
    private PlayerHealth playerHealth;
    private PlayerMana playerMana;
    private Collider2D playerCollider;
    private PlayerStats playerStats;
    private SwipeDetector swipeDetector;
    private AdvancedGestureDetector advancedGestureDetector;
    private bool isDead = false;
    private float lastFireTime = -999f;
    private const float minFireInterval = 0.05f; // Prevent duplicate inputs within 50ms

    // Projectile system
    private int currentProjectileSet = 0; // 0 = Fireball/Icicle, 1 = Tornado, 2 = Custom
    private bool sidesSwapped = false; // false = left fire/right ice, true = left ice/right fire
    
    // Individual cooldowns for each projectile (no sharing)
    private float lastFireballFireTime = -999f;
    private float lastIcicleFireTime = -999f;
    private float lastFireTornadoFireTime = -999f;
    private float lastIceTornadoFireTime = -999f;
    private float lastFireCustom1FireTime = -999f;
    private float lastIceCustom1FireTime = -999f;
    
    // Track cooldowns by card to preserve cooldown when enhanced variants swap
    private Dictionary<ProjectileCards, float> cardCooldownTimes = new Dictionary<ProjectileCards, float>();

    [Header("Auto-Fire Doomed Logic")]
    [Tooltip("If enabled, left side uses FIRE guaranteed damage and right side uses ICE guaranteed damage separately. If disabled, both sides share a single pool so you can reuse the same projectile type on both sides.")]
    [SerializeField] private bool useSplitFireIceDoomedLogic = true;

    [Tooltip("Enemies marked as doomed are only skipped while farther than this distance. Once they come closer than this, auto-fire can target them again.")]
    public float doomedRetargetDistance = 3f;

    [Tooltip("Additional doomed retarget distance 2: behaves the same as doomedRetargetDistance when used by gameplay logic.")]
    public float doomedRetargetDistance2 = 3f;

    [Tooltip("Additional doomed retarget distance 3: behaves the same as doomedRetargetDistance when used by gameplay logic.")]
    public float doomedRetargetDistance3 = 3f;
    
    [SerializeField] private float defaultDoomedSkipDuration = 0.75f;

    // Track GUARANTEED incoming damage per enemy from auto-fire projectiles.
    // When useSplitFireIceDoomedLogic is TRUE:
    //   - Left side (fire) looks at guaranteedIncomingFireDamage
    //   - Right side (ice) looks at guaranteedIncomingIceDamage
    // When FALSE:
    //   - Both sides consult a single shared pool (guaranteedIncomingFireDamage)
    private Dictionary<EnemyHealth, float> guaranteedIncomingFireDamage = new Dictionary<EnemyHealth, float>();
    private Dictionary<EnemyHealth, float> guaranteedIncomingIceDamage = new Dictionary<EnemyHealth, float>();

    // Cumulative lower-bound predicted damage currently "in flight" toward
    // each enemy from auto-fire shots. Once this exceeds the enemy's HP at
    // fire time, we mark them as doomed and stop targeting them until the
    // doomed window expires or they move close enough for retargeting.
    private Dictionary<EnemyHealth, float> cumulativeIncomingFireDamage = new Dictionary<EnemyHealth, float>();
    private Dictionary<EnemyHealth, float> cumulativeIncomingIceDamage = new Dictionary<EnemyHealth, float>();

    // Active projectile system (card-driven auto-fire)
    private ProjectileCards activeProjectileCard;

    // Applies per-card projectile modifiers (piercing, etc.) to active projectiles
    private ProjectileModifierApplier projectileModifierApplier;

    private void OnEnable()
    {
        move.Enable();
        fire.Enable();
    }

    private void OnDisable()
    {
        move.Disable();
        fire.Disable();
    }

    private void Awake()
    {
        playerControls = new Input_System();
        move = playerControls.Player.Move;
        fire = playerControls.Player.Fire;

        if (Instance != null && Instance != this)
        {
            Destroy(this);
        }
        else
        {
            Instance = this;
        }

        playerHealth = GetComponent<PlayerHealth>();
        playerMana = GetComponent<PlayerMana>();
        playerCollider = GetComponent<Collider2D>();
        playerStats = GetComponent<PlayerStats>();
        swipeDetector = GetComponent<SwipeDetector>();
        advancedGestureDetector = GetComponent<AdvancedGestureDetector>();

        if (playerMana == null)
        {
            Debug.LogError("PlayerMana component missing!");
            playerMana = gameObject.AddComponent<PlayerMana>();
        }
        
        if (playerCollider == null)
        {
            Debug.LogError("Player Collider2D missing!");
        }
        
        if (swipeDetector == null)
        {
            Debug.Log("<color=cyan>SwipeDetector component missing. Auto-adding...</color>");
            swipeDetector = gameObject.AddComponent<SwipeDetector>();
        }
        
        if (advancedGestureDetector == null)
        {
            Debug.Log("<color=cyan>AdvancedGestureDetector component missing. Auto-adding...</color>");
            advancedGestureDetector = gameObject.AddComponent<AdvancedGestureDetector>();
        }

        // Ensure we have a ProjectileModifierApplier so ACTIVE projectiles (Fireball/Icicle,
        // Tornado, etc.) receive per-card modifiers like piercing, just like passive spawns
        // from ProjectileSpawner.
        projectileModifierApplier = GetComponent<ProjectileModifierApplier>();
        if (projectileModifierApplier == null)
        {
            projectileModifierApplier = gameObject.AddComponent<ProjectileModifierApplier>();
        }
    }

    void Start()
    {
        if (rb != null)
        {
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        if (playerHealth != null)
        {
            playerHealth.OnDeath += HandlePlayerDeath;
        }

        // Subscribe to OLD swipe events (deprecated, keeping for compatibility)
        if (swipeDetector != null)
        {
            swipeDetector.OnSwipe += HandleSwipe;
        }
        
        // Subscribe to ADVANCED gesture events
        if (advancedGestureDetector != null)
        {
            advancedGestureDetector.OnDualHorizontalSwipe += HandleDualHorizontalSwipe;
            advancedGestureDetector.OnDiagonalSwipe += HandleDiagonalSwipe;
        }

        // REMOVED: fire.started subscription - causes duplicate projectiles!
        // All input is now handled through Update() or HandleTouchInput()
    }

    void Update()
    {
        if (isDead) return;

        // Check if card selection is active - if so, disable player input
        if (CardSelectionManager.Instance != null && CardSelectionManager.Instance.IsSelectionActive())
        {
            return; // Don't process any player input while card selection is active
        }

        // Movement
        Vector2 moveInput = move.ReadValue<Vector2>();

        StatusController statusController = GetComponent<StatusController>();
        if (statusController != null && StatusControllerManager.Instance != null && moveInput != Vector2.zero)
        {
            int amnesiaStacks = statusController.GetStacks(StatusId.Amnesia);
            if (amnesiaStacks > 0)
            {
                float perStack = StatusControllerManager.Instance.AmnesiaChancePerStackPercent;
                float chance = Mathf.Clamp01((perStack * amnesiaStacks) / 100f);
                if (Random.value < chance)
                {
                    moveInput = Vector2.zero;
                }
            }
        }

        animator.SetFloat("moveX", moveInput.x);
        animator.SetFloat("moveY", moveInput.y);
        animator.SetBool("moving", moveInput != Vector2.zero);
        
        float speedMult = 1f;
        if (playerStats != null)
        {
            speedMult = Mathf.Max(0f, playerStats.moveSpeedMultiplier);
        }

        if (statusController != null && StatusControllerManager.Instance != null)
        {
            int slowStacks = statusController.GetStacks(StatusId.Slow);
            if (slowStacks > 0)
            {
                float perStack = StatusControllerManager.Instance.PlayerSlowPercentPerStack;
                float total = Mathf.Max(0f, perStack * slowStacks);
                speedMult *= Mathf.Max(0f, 1f - total / 100f);
            }
        }
        
        // Store for FixedUpdate
        rb.velocity = new Vector2(moveInput.x * movespeed * speedMult, moveInput.y * movespeed * speedMult);
        
        // Handle mouse click for firing (desktop)
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            // Check if clicking on UI - if so, ignore
            if (!IsPointerOverUI())
            {
                FireProjectile();
            }
        }

        // Projectile switching (1, 2, 3 keys)
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            currentProjectileSet = 0;
        }
        else if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            currentProjectileSet = 1;
        }
        else if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            currentProjectileSet = 2;
        }

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            sidesSwapped = !sidesSwapped;
            SwapActiveTornadoes();
        }
        
        // Auto-fire system for Projectile Pair Set 1
        if (enableAutoFire && activeProjectileCard != null)
        {
            // Get actual cooldowns from projectiles
            float fireCooldown = GetProjectileCooldown(fireballPrefab);
            float iceCooldown = GetProjectileCooldown(iciclePrefab);
            float cooldownToUse = useIndependentCooldowns ? Mathf.Min(fireCooldown, iceCooldown) : Mathf.Max(fireCooldown, iceCooldown);
            
            if (Time.time - lastAutoFireTime >= cooldownToUse)
            {
                TryAutoFire();
            }
        }
    }

    // Movement now handled in Update() to avoid one-frame delay

    /// <summary>
    /// Called by TestTouch when screen is touched
    /// </summary>
    public void HandleTouchInput(Vector2 screenPosition)
    {
        if (Time.time - lastFireTime < minFireInterval)
        {
            return;
        }
        
        FireProjectileAtScreenPosition(screenPosition);
    }
    
    /// <summary>
    /// Handle OLD swipe gestures - switch to tornado on swipe (DEPRECATED)
    /// This is kept for backward compatibility with keyboard key "2"
    /// </summary>
    private void HandleSwipe(Vector2 swipeDirection)
    {
        // Old behavior: Any swipe switches to tornado (projectile set 1)
        // This is now ONLY triggered by non-diagonal swipes
        // Diagonal swipes are handled by HandleDiagonalSwipe
    }
    
    /// <summary>
    /// Handle dual horizontal swipe - swap fire/ice (like spacebar)
    /// </summary>
    private void HandleDualHorizontalSwipe()
    {
        sidesSwapped = !sidesSwapped;
        SwapActiveTornadoes();
    }
    
    [Header("Directional Swipe Projectiles")]
    [Tooltip("Projectile to spawn when swiping LEFT (swipe direction X < 0)")]
    public GameObject leftDiagonalProjectile;
    
    [Tooltip("Projectile to spawn when swiping RIGHT (swipe direction X > 0)")]
    public GameObject rightDiagonalProjectile;
    
    /// <summary>
    /// Handle directional swipe - spawn projectile based on swipe direction (works in ANY direction!)
    /// </summary>
    private void HandleDiagonalSwipe(bool isLeftDiagonal, Vector2 swipeDirection, Vector2 startPos, Vector2 endPos)
    {
        GameObject projectileToSpawn = isLeftDiagonal ? leftDiagonalProjectile : rightDiagonalProjectile;
        
        if (projectileToSpawn == null)
        {
            return;
        }
        
        SpawnDiagonalProjectileWithDirection(swipeDirection, endPos, projectileToSpawn);
    }

    void FireProjectile()
    {
        if (isDead || playerMana == null) return;
        
        if (Time.time - lastFireTime < minFireInterval)
        {
            return;
        }

        // Get mouse position
        Vector2 screenPos = Mouse.current.position.ReadValue();
        FireProjectileAtScreenPosition(screenPos);
    }

    void FireProjectileAtScreenPosition(Vector2 screenPosition)
    {
        if (isDead || playerMana == null)
        {
            return;
        }
        
        // Check minimum fire interval to prevent double-firing
        if (Time.time - lastFireTime < minFireInterval)
        {
            Debug.Log($"<color=orange>FireProjectile blocked: Too soon! {Time.time - lastFireTime:F3}s < {minFireInterval}s</color>");
            return;
        }
        
        // No shared cooldown - each projectile handles its own cooldown
        lastFireTime = Time.time;

        // Convert screen position to world position
        Ray ray = cam.ScreenPointToRay(screenPosition);
        Plane gamePlane = new Plane(Vector3.forward, firePoint.position.z);

        if (!gamePlane.Raycast(ray, out float enter)) return;

        Vector3 worldTouchPosition = ray.GetPoint(enter);
        Vector2 fireDirection = (worldTouchPosition - firePoint.position).normalized;

        // Determine which side of screen was clicked
        bool isLeftSide = screenPosition.x < Screen.width / 2f;

        // Determine projectile type based on side and swap state
        bool shouldBeFire = (isLeftSide && !sidesSwapped) || (!isLeftSide && sidesSwapped);

        // Get the correct prefab
        GameObject prefabToUse = GetProjectilePrefab(shouldBeFire);

        if (prefabToUse == null)
        {
            Debug.LogWarning($"No prefab assigned for set {currentProjectileSet}!");
            return;
        }

        // Get spawn offset from projectile prefab if it has one
        Vector3 spawnPosition = firePoint.position;
        Vector2 spawnOffset = Vector2.zero;
        PlayerProjectiles prefabScript = prefabToUse.GetComponent<PlayerProjectiles>();
        if (prefabScript != null)
        {
            spawnOffset = prefabScript.GetSpawnOffset(fireDirection);
            spawnPosition += (Vector3)spawnOffset;
            Debug.Log($"<color=cyan>Manual Fire: Applied spawn offset {spawnOffset} before instantiation</color>");
        }
        
        // Instantiate projectile at final spawn position
        GameObject projectileObj = Instantiate(prefabToUse, spawnPosition, Quaternion.identity);

        // Tag projectile with the active projectile card so modifiers can be applied
        if (activeProjectileCard != null && ProjectileCardModifiers.Instance != null)
        {
            ProjectileCardModifiers.Instance.TagProjectileWithCard(projectileObj, activeProjectileCard);

            // Apply per-card modifiers (especially piercing) so PlayerProjectiles like
            // Ice (icicle) gain ProjectilePiercing just like Talon projectiles.
            if (projectileModifierApplier != null)
            {
                projectileModifierApplier.ApplyModifiersToProjectile(projectileObj, activeProjectileCard);
            }
        }

        // Check if it's a Tornado or regular projectile
        TornadoController tornado = projectileObj.GetComponent<TornadoController>();
        if (tornado != null)
        {
            // Handle Tornado - check mana and cooldown first
            if (!TornadoController.CanCast(playerMana))
            {
                Destroy(projectileObj);
                return;
            }

            // Set the tornado type based on which side was clicked
            tornado.isFireTornado = shouldBeFire;
            tornado.SetTargetPosition(worldTouchPosition);
            TornadoController.RecordCast();
            Debug.Log($"Tornado spawned at {firePoint.position}, target: {worldTouchPosition}, isFire: {shouldBeFire}");
        }
        else
        {
            // Handle regular projectiles (PlayerProjectiles or Talon variants)
            PlayerProjectiles projectile = projectileObj.GetComponent<PlayerProjectiles>();
            ProjectileFireTalon fireTalon = projectileObj.GetComponent<ProjectileFireTalon>();
            ProjectileIceTalon iceTalon = projectileObj.GetComponent<ProjectileIceTalon>();
            
            if (projectile != null)
            {
                projectile.Launch(fireDirection, playerCollider, playerMana);
                Debug.Log($"Projectile launched: {prefabToUse.name}");
            }
            else if (fireTalon != null)
            {
                // Get direction-based spawn offset for fire talon
                Vector2 talonOffset = fireTalon.GetSpawnOffset(fireDirection);
                projectileObj.transform.position += (Vector3)talonOffset;
                fireTalon.Launch(fireDirection, playerCollider, playerMana);
                Debug.Log($"ProjectileFireTalon launched: {prefabToUse.name} with offset {talonOffset}");
            }
            else if (iceTalon != null)
            {
                // Get direction-based spawn offset for ice talon
                Vector2 talonOffset = iceTalon.GetSpawnOffset(fireDirection);
                projectileObj.transform.position += (Vector3)talonOffset;
                iceTalon.Launch(fireDirection, playerCollider, playerMana);
                Debug.Log($"ProjectileIceTalon launched: {prefabToUse.name} with offset {talonOffset}");
            }
            else
            {
                Debug.LogError($"Prefab {prefabToUse.name} has no recognized projectile component!");
                Destroy(projectileObj);
            }
        }

        Debug.DrawRay(firePoint.position, fireDirection * 5f, shouldBeFire ? Color.red : Color.cyan, 2f);
    }

    private GameObject GetProjectilePrefab(bool isFire)
    {
        switch (currentProjectileSet)
        {
            case 0: // Fireball/Icicle
                return isFire ? fireballPrefab : iciclePrefab;
            case 1: // Tornado
                return isFire ? fireTornadoPrefab : iceTornadoPrefab;
            case 2: // Custom
                return isFire ? fireCustom1Prefab : iceCustom1Prefab;
            default:
                return isFire ? fireballPrefab : iciclePrefab;
        }
    }
    
    /// <summary>
    /// Auto-fire system: Finds closest enemy on each side and fires at them
    /// </summary>
    private void TryAutoFire()
    {
        if (activeProjectileCard == null || activeProjectileCard.projectilePrefab == null)
        {
            Debug.LogWarning("<color=yellow>Auto-fire: Active projectile card or its prefab is not assigned!</color>");
            return;
        }

        // Clean up guaranteed damage maps (remove dead/null or expired enemies)
        if (guaranteedIncomingFireDamage.Count > 0)
        {
            var keys = new List<EnemyHealth>(guaranteedIncomingFireDamage.Keys);
            float now = Time.time;
            foreach (var eh in keys)
            {
                if (eh == null || !eh.IsAlive)
                {
                    guaranteedIncomingFireDamage.Remove(eh);
                    cumulativeIncomingFireDamage.Remove(eh);
                    continue;
                }

                float expiryTime;
                if (guaranteedIncomingFireDamage.TryGetValue(eh, out expiryTime) && now > expiryTime)
                {
                    guaranteedIncomingFireDamage.Remove(eh);
                    cumulativeIncomingFireDamage.Remove(eh);
                }
            }
        }

        // Only maintain the ICE map when split logic is enabled; otherwise we
        // rely solely on the unified FIRE map for both sides.
        if (useSplitFireIceDoomedLogic && guaranteedIncomingIceDamage.Count > 0)
        {
            var keys = new List<EnemyHealth>(guaranteedIncomingIceDamage.Keys);
            float now = Time.time;
            foreach (var eh in keys)
            {
                if (eh == null || !eh.IsAlive)
                {
                    guaranteedIncomingIceDamage.Remove(eh);
                    cumulativeIncomingIceDamage.Remove(eh);
                    continue;
                }

                float expiryTime;
                if (guaranteedIncomingIceDamage.TryGetValue(eh, out expiryTime) && now > expiryTime)
                {
                    guaranteedIncomingIceDamage.Remove(eh);
                    cumulativeIncomingIceDamage.Remove(eh);
                }
            }
        }

        // Determine prefab to use for auto-fire from the ACTIVE projectile card.
        GameObject currentFireballPrefab = activeProjectileCard.projectilePrefab;
        
        // IMPORTANT: For now, do NOT auto-swap Icicle to an enhanced IceLance
        // version for the ICE side. Always use the base iciclePrefab so attack
        // speed/cooldown logic is stable. Fireball/FireBomb enhancement remains
        // unchanged.
        GameObject currentIciclePrefab = activeProjectileCard.projectilePrefab;

        if (currentFireballPrefab == null)
        {
            Debug.LogWarning("<color=yellow>Auto-fire: Active projectile card has no projectile prefab assigned!</color>");
            return;
        }
        
        if (autoFirePointA == null || autoFirePointB == null || autoFirePointC == null || autoFirePointD == null)
        {
            return;
        }

        float minX = Mathf.Min(autoFirePointA.position.x, autoFirePointB.position.x, autoFirePointC.position.x, autoFirePointD.position.x);
        float maxX = Mathf.Max(autoFirePointA.position.x, autoFirePointB.position.x, autoFirePointC.position.x, autoFirePointD.position.x);
        float minY = Mathf.Min(autoFirePointA.position.y, autoFirePointB.position.y, autoFirePointC.position.y, autoFirePointD.position.y);
        float maxY = Mathf.Max(autoFirePointA.position.y, autoFirePointB.position.y, autoFirePointC.position.y, autoFirePointD.position.y);

        Vector2 bottomLeft = new Vector2(minX, minY);
        Vector2 topRight = new Vector2(maxX, maxY);

        Collider2D[] enemiesInRange = Physics2D.OverlapAreaAll(bottomLeft, topRight, LayerMask.GetMask("Enemy"));
        Debug.Log($"<color=cyan>Auto-fire: Found {enemiesInRange.Length} enemies in rectangle [{bottomLeft} → {topRight}]</color>");
        
        if (enemiesInRange.Length == 0) return;
        
        // Separate enemies by side (left/right of player) and keep up to the 3
        // nearest valid candidates per side for immediate retargeting.
        List<GameObject> leftCandidates = new List<GameObject>();
        List<float> leftDistances = new List<float>();
        List<GameObject> rightCandidates = new List<GameObject>();
        List<float> rightDistances = new List<float>();
        
        foreach (Collider2D enemyCol in enemiesInRange)
        {
            if (enemyCol == null || enemyCol.gameObject == null) continue;
            
            // Check if enemy is alive
            EnemyHealth enemyHealth = enemyCol.GetComponent<EnemyHealth>() ?? enemyCol.GetComponentInParent<EnemyHealth>();
            if (enemyHealth == null || !enemyHealth.IsAlive) continue;

            // Use collider bounds center for accurate aiming
            Vector2 enemyCenter = enemyCol.bounds.center;
            float distToPlayer = Vector2.Distance(transform.position, enemyCenter);
            bool isOnLeft = enemyCenter.x < transform.position.x;

            if (isOnLeft)
            {
                float now = Time.time;
                float expiryTime;
                if (guaranteedIncomingFireDamage.TryGetValue(enemyHealth, out expiryTime))
                {
                    if (now > expiryTime || distToPlayer <= doomedRetargetDistance)
                    {
                        guaranteedIncomingFireDamage.Remove(enemyHealth);
                        cumulativeIncomingFireDamage.Remove(enemyHealth);
                        Debug.Log($"<color=cyan>Auto-fire (LEFT): Clearing doomed flag for {enemyCol.gameObject.name} (dist={distToPlayer:F2})</color>");
                    }
                    else
                    {
                        Debug.Log($"<color=cyan>Auto-fire (LEFT): Skipping {enemyCol.gameObject.name} (doomed and still far: {distToPlayer:F2} > {doomedRetargetDistance:F2})</color>");
                        continue;
                    }
                }

                InsertAutoFireCandidate(leftCandidates, leftDistances, enemyCol.gameObject, distToPlayer, 3);
            }
            else
            {
                Dictionary<EnemyHealth, float> doomedDict = useSplitFireIceDoomedLogic
                    ? guaranteedIncomingIceDamage
                    : guaranteedIncomingFireDamage;
                Dictionary<EnemyHealth, float> cumulativeDict = useSplitFireIceDoomedLogic
                    ? cumulativeIncomingIceDamage
                    : cumulativeIncomingFireDamage;

                float expiryTime;
                if (doomedDict.TryGetValue(enemyHealth, out expiryTime))
                {
                    float now = Time.time;
                    if (now > expiryTime || distToPlayer <= doomedRetargetDistance)
                    {
                        doomedDict.Remove(enemyHealth);
                        cumulativeDict.Remove(enemyHealth);
                        Debug.Log($"<color=cyan>Auto-fire (RIGHT): Clearing doomed flag for {enemyCol.gameObject.name} (dist={distToPlayer:F2})</color>");
                    }
                    else
                    {
                        Debug.Log($"<color=cyan>Auto-fire (RIGHT): Skipping {enemyCol.gameObject.name} (doomed and still far: {distToPlayer:F2} > {doomedRetargetDistance:F2})</color>");
                        continue;
                    }
                }

                InsertAutoFireCandidate(rightCandidates, rightDistances, enemyCol.gameObject, distToPlayer, 3);
            }
        }
        
        float fireCooldown = GetProjectileCooldown(currentFireballPrefab);
        float iceCooldown = GetProjectileCooldown(currentIciclePrefab);
        
        // Get card references to track cooldowns (ACTIVE projectile system only)
        ProjectileCards fireCard = activeProjectileCard;
        ProjectileCards iceCard = activeProjectileCard;
        
        bool firedLeft = false;
        bool firedRight = false;
        
        if (useIndependentCooldowns)
        {
            // INDEPENDENT COOLDOWNS: Fire/Ice have their own cooldowns
            if (leftCandidates.Count > 0 && IsCardReady(fireCard, fireCooldown))
            {
                for (int i = 0; i < leftCandidates.Count; i++)
                {
                    GameObject target = leftCandidates[i];
                    if (target == null) continue;
                    Vector2 predictedPos = PredictEnemyPosition(target);
                    Vector2 fireDir = (predictedPos - (Vector2)firePoint.position).normalized;
                    if (FireAutoProjectile(currentFireballPrefab, fireDir, true, target.transform))
                    {
                        firedLeft = true;
                        SetCardCooldown(fireCard);
                        break;
                    }
                }
            }

            if (rightCandidates.Count > 0 && IsCardReady(iceCard, iceCooldown))
            {
                for (int i = 0; i < rightCandidates.Count; i++)
                {
                    GameObject target = rightCandidates[i];
                    if (target == null) continue;
                    Vector2 predictedPos = PredictEnemyPosition(target);
                    Vector2 fireDir = (predictedPos - (Vector2)firePoint.position).normalized;
                    if (FireAutoProjectile(currentIciclePrefab, fireDir, false, target.transform))
                    {
                        firedRight = true;
                        SetCardCooldown(iceCard);
                        break;
                    }
                }
            }

            if (firedLeft || firedRight)
            {
                lastAutoFireTime = Time.time;
                Debug.Log($"<color=cyan>Auto-fired (INDEPENDENT): Left={firedLeft}, Right={firedRight}</color>");
            }
        }
        else
        {
            GameObject sharedTarget = null;
            bool sharedIsLeft = false;
            float sharedLeftDist = leftDistances.Count > 0 ? leftDistances[0] : float.MaxValue;
            float sharedRightDist = rightDistances.Count > 0 ? rightDistances[0] : float.MaxValue;

            if (sharedLeftDist < sharedRightDist && leftCandidates.Count > 0)
            {
                sharedTarget = leftCandidates[0];
                sharedIsLeft = true;
            }
            else if (rightCandidates.Count > 0)
            {
                sharedTarget = rightCandidates[0];
                sharedIsLeft = false;
            }

            GameObject prefabToUse = sharedIsLeft ? currentFireballPrefab : currentIciclePrefab;
            float cooldownToUse = sharedIsLeft ? fireCooldown : iceCooldown;
            ProjectileCards cardToUse = sharedIsLeft ? fireCard : iceCard;
            
            if (sharedTarget != null && IsCardReady(cardToUse, cooldownToUse))
            {
                Vector2 predictedPos = PredictEnemyPosition(sharedTarget);
                Vector2 fireDir = (predictedPos - (Vector2)firePoint.position).normalized;
                if (FireAutoProjectile(prefabToUse, fireDir, sharedIsLeft, sharedTarget.transform))
                {
                    SetCardCooldown(fireCard);
                    SetCardCooldown(iceCard);
                    lastAutoFireTime = Time.time;
                    Debug.Log($"<color=cyan>Auto-fired (SHARED): {(sharedIsLeft ? "Fire" : "Ice")} side, cooldown={cooldownToUse:F2}s</color>");
                }
            }
        }
    }

    private void InsertAutoFireCandidate(List<GameObject> list, List<float> distances, GameObject enemy, float distance, int maxCount)
    {
        int insertIndex = 0;
        int count = distances.Count;
        while (insertIndex < count && distances[insertIndex] <= distance)
        {
            insertIndex++;
        }

        list.Insert(insertIndex, enemy);
        distances.Insert(insertIndex, distance);

        if (list.Count > maxCount)
        {
            int last = list.Count - 1;
            list.RemoveAt(last);
            distances.RemoveAt(last);
        }
    }
    
    /// <summary>
    /// Predict enemy position based on their movement toward player
    /// </summary>
    private Vector2 PredictEnemyPosition(GameObject enemy)
    {
        if (enemy == null) return Vector2.zero;
        
        // Use collider center for more accurate targeting
        Vector2 enemyPos;
        Collider2D enemyCollider = enemy.GetComponent<Collider2D>();
        if (enemyCollider != null)
        {
            enemyPos = enemyCollider.bounds.center;
        }
        else
        {
            enemyPos = enemy.transform.position;
        }
        
        // Get enemy's Rigidbody2D to check velocity
        Rigidbody2D enemyRb = enemy.GetComponent<Rigidbody2D>();
        if (enemyRb != null && enemyRb.velocity.sqrMagnitude > 0.01f)
        {
            // Enemy is moving, predict based on velocity
            // Assume projectile takes ~0.5s to reach enemy
            float predictionTime = 0.5f;
            return enemyPos + enemyRb.velocity * predictionTime;
        }
        else
        {
            // Enemy not moving or no rigidbody, assume moving toward player
            Vector2 dirToPlayer = ((Vector2)transform.position - enemyPos).normalized;
            
            // Estimate enemy speed (most enemies move at ~2-3 units/s)
            float estimatedSpeed = 2.5f;
            float predictionTime = 0.5f;
            
            return enemyPos + dirToPlayer * estimatedSpeed * predictionTime;
        }
    }
    
    /// <summary>
    /// Fire a projectile from auto-fire system
    /// Returns true if fired successfully, false if not enough mana
    /// </summary>
    private bool FireAutoProjectile(GameObject prefab, Vector2 direction, bool isFire, Transform target = null)
    {
        if (prefab == null || playerMana == null) return false;

        // If we had a target but it died or got destroyed between selection and fire,
        // cancel this shot so auto-fire can immediately retarget on the next frame
        // without starting a cooldown.
        if (target != null)
        {
            EnemyHealth enemyHealth = target.GetComponent<EnemyHealth>() ?? target.GetComponentInParent<EnemyHealth>();
            if (enemyHealth == null || !enemyHealth.IsAlive)
            {
                return false;
            }
        }

        // Spawn position is already calculated with offset in TryAutoFire
        // DO NOT apply offset again here to prevent repositioning
        Vector3 spawnPosition = firePoint.position;
        Vector2 spawnOffset = Vector2.zero;
        PlayerProjectiles prefabScript = prefab.GetComponent<PlayerProjectiles>();
        if (prefabScript != null)
        {
            spawnOffset = prefabScript.GetSpawnOffset(direction);
            spawnPosition += (Vector3)spawnOffset;
            Debug.Log($"<color=magenta>Auto-Fire: Applied spawn offset {spawnOffset} ONCE before instantiation</color>");
        }
        
        GameObject projectileObj = Instantiate(prefab, spawnPosition, Quaternion.identity);

        // Tag projectile with the active projectile card so modifiers can be applied
        if (activeProjectileCard != null && ProjectileCardModifiers.Instance != null)
        {
            ProjectileCardModifiers.Instance.TagProjectileWithCard(projectileObj, activeProjectileCard);

            // Apply per-card modifiers (especially piercing) to ACTIVE shots so
            // PlayerProjectiles (Fireball/Icelance) behave like Talon projectiles
            // when they have pierce.
            if (projectileModifierApplier != null)
            {
                projectileModifierApplier.ApplyModifiersToProjectile(projectileObj, activeProjectileCard);
            }
        }
        
        // Launch projectile - check for different types
        PlayerProjectiles projectile = projectileObj.GetComponent<PlayerProjectiles>();
        ProjectileFireTalon fireTalon = projectileObj.GetComponent<ProjectileFireTalon>();
        ProjectileIceTalon iceTalon = projectileObj.GetComponent<ProjectileIceTalon>();
        IceLancer iceLancer = projectileObj.GetComponent<IceLancer>();
        FireBomb fireBomb = projectileObj.GetComponent<FireBomb>();
        
        if (iceLancer != null)
        {
            // IceLancer needs target
            iceLancer.Launch(direction, target, playerCollider, playerMana);
            RegisterGuaranteedDamage(target, projectileObj, isFire);
        }
        else if (fireBomb != null)
        {
            // FireBomb needs target
            fireBomb.Launch(direction, target, playerCollider, playerMana);
            RegisterGuaranteedDamage(target, projectileObj, isFire);
        }
        else if (projectile != null)
        {
            projectile.Launch(direction, playerCollider, playerMana);
            RegisterGuaranteedDamage(target, projectileObj, isFire);
        }
        else if (fireTalon != null)
        {
            Vector2 talonOffset = fireTalon.GetSpawnOffset(direction);
            projectileObj.transform.position += (Vector3)talonOffset;
            fireTalon.Launch(direction, playerCollider, playerMana);
        }
        else if (iceTalon != null)
        {
            Vector2 talonOffset = iceTalon.GetSpawnOffset(direction);
            projectileObj.transform.position += (Vector3)talonOffset;
            iceTalon.Launch(direction, playerCollider, playerMana);
        }
        else
        {
            Debug.LogError($"Auto-fire prefab {prefab.name} has no recognized projectile component!");
            Destroy(projectileObj);
            return false;
        }
        
        return true; // Successfully fired
    }

    private float GetCurrentDoomedSkipDuration()
    {
        if (activeProjectileCard != null && activeProjectileCard.doomedSkipDuration > 0f)
        {
            return activeProjectileCard.doomedSkipDuration;
        }

        return defaultDoomedSkipDuration;
    }

    private void RegisterGuaranteedDamage(Transform target, GameObject projectileObj, bool isFire)
    {
        // Always register guaranteed damage from auto-fire so doomed-logic
        // can function. When split logic is disabled, both sides share the
        // FIRE map; when enabled, FIRE/ICE use separate maps.

        if (target == null || projectileObj == null)
        {
            return;
        }

        EnemyHealth enemyHealth = target.GetComponent<EnemyHealth>() ?? target.GetComponentInParent<EnemyHealth>();
        if (enemyHealth == null || !enemyHealth.IsAlive)
        {
            return;
        }

        // Start from the projectile's current damage after card modifiers.
        // For PlayerProjectiles and IceLancer this is BEFORE PlayerStats and
        // favour effects (they run through PlayerDamageHelper per hit).
        // For FireBomb this already includes PlayerStats.CalculateDamage.
        float rawProjectileDamage = 0f;
        bool damageAlreadyIncludesStats = false;

        PlayerProjectiles pp = projectileObj.GetComponent<PlayerProjectiles>();
        if (pp != null)
        {
            rawProjectileDamage = pp.GetCurrentDamage();
            damageAlreadyIncludesStats = false;
        }
        else
        {
            FireBomb fb = projectileObj.GetComponent<FireBomb>();
            if (fb != null)
            {
                rawProjectileDamage = fb.GetCurrentDamage();
                damageAlreadyIncludesStats = true; // FireBomb bakes PlayerStats in Launch
            }
            else
            {
                IceLancer il = projectileObj.GetComponent<IceLancer>();
                if (il != null)
                {
                    rawProjectileDamage = il.GetCurrentDamage();
                    damageAlreadyIncludesStats = false;
                }
            }
        }

        if (rawProjectileDamage <= 0f)
        {
            return;
        }

        // Build a deterministic LOWER-BOUND estimate of final damage that this
        // shot will deal, including PlayerStats multipliers but WITHOUT crit
        // randomness. This ensures we only mark an enemy as doomed when a
        // non-crit hit should already be lethal.
        float predictedDamage = rawProjectileDamage;

        // Only apply PlayerStats-based multipliers when they have NOT already
        // been applied inside the projectile logic itself.
        if (playerStats != null && !damageAlreadyIncludesStats)
        {
            float damageAfterStats = (rawProjectileDamage
                                      + playerStats.projectileFlatDamage
                                      + playerStats.flatDamage)
                                     * playerStats.damageMultiplier
                                     * playerStats.favourDamageMultiplier
                                     * playerStats.projectileDamageMultiplier;
            predictedDamage = Mathf.Max(0f, damageAfterStats);
        }

        if (predictedDamage <= 0f)
        {
            return;
        }

        // Apply favour-based outgoing modifiers in preview mode so we respect
        // thresholds like Corruption, distance bonuses, boss flags, etc.
        if (playerStats != null)
        {
            FavourEffectManager favourManager = playerStats.GetComponent<FavourEffectManager>();
            if (favourManager != null)
            {
                GameObject enemyGO = enemyHealth.gameObject;
                predictedDamage = favourManager.PreviewBeforeDealDamage(enemyGO, predictedDamage);
            }
        }

        if (predictedDamage <= 0f)
        {
            return;
        }

        // Use CUMULATIVE predicted in-flight damage for doom checks instead of
        // requiring a single shot to be individually lethal. As soon as the
        // sum of predicted damage from auto-fire shots toward this enemy meets
        // or exceeds its current HP, we treat it as doomed.
        float currentHpAtFire = enemyHealth.CurrentHealth;

        Dictionary<EnemyHealth, float> doomedDict;
        Dictionary<EnemyHealth, float> cumulativeDict;

        if (useSplitFireIceDoomedLogic)
        {
            if (isFire)
            {
                doomedDict = guaranteedIncomingFireDamage;
                cumulativeDict = cumulativeIncomingFireDamage;
            }
            else
            {
                doomedDict = guaranteedIncomingIceDamage;
                cumulativeDict = cumulativeIncomingIceDamage;
            }
        }
        else
        {
            doomedDict = guaranteedIncomingFireDamage;
            cumulativeDict = cumulativeIncomingFireDamage;
        }

        float existing;
        cumulativeDict.TryGetValue(enemyHealth, out existing);
        float newTotal = Mathf.Max(0f, existing) + predictedDamage;
        cumulativeDict[enemyHealth] = newTotal;

        if (newTotal < currentHpAtFire)
        {
            return;
        }

        doomedDict[enemyHealth] = Time.time + GetCurrentDoomedSkipDuration();
    }

    void HandlePlayerDeath()
    {
        Debug.Log("Player died!");
        isDead = true;
        rb.velocity = Vector2.zero;
        enabled = false;
        StartCoroutine(WaitForRestart());
    }

    private void OnDrawGizmosSelected()
    {
        // Draw auto-fire detection radius
        if (enableAutoFire && showAutoFireGizmo &&
            autoFirePointA != null && autoFirePointB != null &&
            autoFirePointC != null && autoFirePointD != null)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.5f); // Green

            Vector3 a = autoFirePointA.position;
            Vector3 b = autoFirePointB.position;
            Vector3 c = autoFirePointC.position;
            Vector3 d = autoFirePointD.position;

            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, d);
            Gizmos.DrawLine(d, a);
        }
    }
    
    private IEnumerator WaitForRestart()
    {
        yield return new WaitForSeconds(1f);
        
        // Use Input System instead of old Input class
        while (true)
        {
            bool mouseClicked = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            bool touchPressed = Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
            
            if (mouseClicked || touchPressed)
            {
                break;
            }
            yield return null;
        }
        
        HolyShield.ResetRunState();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>
    /// Spawn projectile directly at screen position from diagonal swipe
    /// </summary>
    private void SpawnDiagonalProjectile(Vector2 screenPosition, GameObject projectilePrefab)
    {
        Debug.Log($"<color=yellow>═══════════════════════════════════════</color>");
        Debug.Log($"<color=yellow>SpawnDiagonalProjectile called!</color>");
        Debug.Log($"<color=yellow>ScreenPos: {screenPosition}</color>");
        Debug.Log($"<color=yellow>Projectile: {projectilePrefab.name}</color>");
        Debug.Log($"<color=yellow>═══════════════════════════════════════</color>");
        
        if (playerMana == null)
        {
            Debug.LogError("❌ PlayerMana is null!");
            return;
        }
        
        if (cam == null)
        {
            Debug.LogError("❌ Camera is null! Cannot spawn tornado.");
            return;
        }
        
        if (firePoint == null)
        {
            Debug.LogError("❌ FirePoint is null! Cannot spawn tornado.");
            return;
        }
        
        Debug.Log($"<color=cyan>Current Mana: {playerMana.CurrentMana}/{playerMana.MaxMana}</color>");
        
        // Convert screen position to world position
        Ray ray = cam.ScreenPointToRay(screenPosition);
        Plane gamePlane = new Plane(Vector3.forward, firePoint.position.z);
        
        if (!gamePlane.Raycast(ray, out float enter))
        {
            Debug.LogWarning("Raycast failed to hit game plane!");
            return;
        }
        
        Vector3 worldTouchPosition = ray.GetPoint(enter);
        Debug.Log($"<color=green>World touch position: {worldTouchPosition}</color>");
        
        // Calculate direction from fire point to touch position
        Vector2 direction = (worldTouchPosition - firePoint.position).normalized;
        
        // Spawn projectile at fire point
        GameObject projectileObj = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
        Debug.Log($"<color=green>✅ Projectile instantiated: {projectileObj.name}</color>");
        
        // Try to launch it with different projectile types
        // Check for TornadoController
        TornadoController tornado = projectileObj.GetComponent<TornadoController>();
        if (tornado != null)
        {
            Debug.Log($"<color=cyan>Found TornadoController, setting target position</color>");
            tornado.SetTargetPosition(worldTouchPosition);
            TornadoController.RecordCast();
            Debug.Log($"<color=cyan>✅ Tornado spawned via diagonal swipe! Target: {worldTouchPosition}</color>");
            return;
        }
        
        // Check for PlayerProjectiles
        PlayerProjectiles fireBolt = projectileObj.GetComponent<PlayerProjectiles>();
        if (fireBolt != null)
        {
            Debug.Log($"<color=cyan>Found PlayerProjectiles, launching</color>");
            fireBolt.Launch(direction, GetComponent<Collider2D>(), playerMana);
            Debug.Log($"<color=cyan>✅ PlayerProjectiles spawned via diagonal swipe! Direction: {direction}</color>");
            return;
        }
        
        // Check for Talon variants
        ProjectileFireTalon fireTalon = projectileObj.GetComponent<ProjectileFireTalon>();
        ProjectileIceTalon iceTalon = projectileObj.GetComponent<ProjectileIceTalon>();
        
        if (fireTalon != null)
        {
            Debug.Log($"<color=cyan>Found ProjectileFireTalon, launching</color>");
            Vector2 talonOffset = fireTalon.GetSpawnOffset(direction);
            projectileObj.transform.position += (Vector3)talonOffset;
            fireTalon.Launch(direction, GetComponent<Collider2D>(), playerMana);
            Debug.Log($"<color=cyan>✅ ProjectileFireTalon spawned via diagonal swipe! Direction: {direction}, Offset: {talonOffset}</color>");
            return;
        }
        
        if (iceTalon != null)
        {
            Debug.Log($"<color=cyan>Found ProjectileIceTalon, launching</color>");
            Vector2 talonOffset = iceTalon.GetSpawnOffset(direction);
            projectileObj.transform.position += (Vector3)talonOffset;
            iceTalon.Launch(direction, GetComponent<Collider2D>(), playerMana);
            Debug.Log($"<color=cyan>✅ ProjectileIceTalon spawned via diagonal swipe! Direction: {direction}, Offset: {talonOffset}</color>");
            return;
        }
        
        // Check for LaserBeamProjectile
        LaserBeamProjectile laser = projectileObj.GetComponent<LaserBeamProjectile>();
        if (laser != null)
        {
            Debug.Log($"<color=cyan>Found LaserBeamProjectile, initializing</color>");
            laser.Initialize(direction);
            Debug.Log($"<color=cyan>✅ Laser spawned via diagonal swipe! Direction: {direction}</color>");
            return;
        }
        
        Debug.LogWarning($"<color=orange>⚠️ No recognized projectile component found on {projectilePrefab.name}</color>");
    }
    
    /// <summary>
    /// Spawn projectile using the actual swipe direction vector
    /// </summary>
    private void SpawnDiagonalProjectileWithDirection(Vector2 swipeDirection, Vector2 endScreenPos, GameObject projectilePrefab)
    {
        Debug.Log($"<color=yellow>═══════════════════════════════════════</color>");
        Debug.Log($"<color=yellow>SpawnDiagonalProjectileWithDirection called!</color>");
        Debug.Log($"<color=yellow>Swipe Direction: {swipeDirection}</color>");
        Debug.Log($"<color=yellow>End Screen Position: {endScreenPos}</color>");
        Debug.Log($"<color=yellow>Projectile: {projectilePrefab.name}</color>");
        Debug.Log($"<color=yellow>═══════════════════════════════════════</color>");
        
        if (playerMana == null)
        {
            Debug.LogError("❌ PlayerMana is null!");
            return;
        }
        
        if (cam == null)
        {
            Debug.LogError("❌ Camera is null!");
            return;
        }
        
        if (firePoint == null)
        {
            Debug.LogError("❌ FirePoint is null!");
            return;
        }
        
        Debug.Log($"<color=cyan>Current Mana: {playerMana.CurrentMana}/{playerMana.MaxMana}</color>");
        
        // Convert end screen position to world position for target
        Ray ray = cam.ScreenPointToRay(endScreenPos);
        Plane gamePlane = new Plane(Vector3.forward, firePoint.position.z);
        
        Vector3 worldTargetPosition = firePoint.position;
        if (gamePlane.Raycast(ray, out float enter))
        {
            worldTargetPosition = ray.GetPoint(enter);
            Debug.Log($"<color=green>World target position: {worldTargetPosition}</color>");
        }
        
        // Swipe direction is in screen space, but screen Y is inverted!
        // Screen space: Y increases downward (0 at top, Screen.height at bottom)
        // World space: Y increases upward
        // So we need to INVERT the Y component!
        Vector2 worldDirection2D = new Vector2(swipeDirection.x, swipeDirection.y); // Keep as-is, already normalized
        
        Debug.Log($"<color=cyan>Screen swipe direction: {swipeDirection}</color>");
        Debug.Log($"<color=cyan>World direction (for projectile): {worldDirection2D}</color>");
        
        // Spawn projectile at fire point
        GameObject projectileObj = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
        Debug.Log($"<color=green>✅ Projectile instantiated: {projectileObj.name}</color>");
        
        // Try to launch it with different projectile types
        // Check for TornadoController
        TornadoController tornado = projectileObj.GetComponent<TornadoController>();
        if (tornado != null)
        {
            Debug.Log($"<color=cyan>Found TornadoController, checking mana and cooldown</color>");
            
            // Check if tornado can be cast (mana + cooldown)
            if (!TornadoController.CanCast(playerMana))
            {
                Debug.Log($"<color=red>Cannot cast tornado - insufficient mana or on cooldown!</color>");
                Destroy(projectileObj);
                return;
            }
            
            // Determine tornado type based on swipe direction (left = ice, right = fire)
            bool isFire = swipeDirection.x > 0;
            tornado.isFireTornado = isFire;
            
            // Use the world target position calculated from swipe end
            tornado.SetTargetPosition(worldTargetPosition);
            TornadoController.RecordCast();
            Debug.Log($"<color=cyan>✅ Tornado spawned via diagonal swipe! Type: {(isFire ? "Fire" : "Ice")}, Target: {worldTargetPosition}</color>");
            return;
        }
        
        // Check for PlayerProjectiles
        PlayerProjectiles fireBolt = projectileObj.GetComponent<PlayerProjectiles>();
        if (fireBolt != null)
        {
            Debug.Log($"<color=cyan>Found PlayerProjectiles, launching with direction: {worldDirection2D}</color>");
            fireBolt.Launch(worldDirection2D, GetComponent<Collider2D>(), playerMana);
            Debug.Log($"<color=cyan>✅ PlayerProjectiles spawned via diagonal swipe! Direction: {worldDirection2D}</color>");
            return;
        }
        
        // Check for Talon variants
        ProjectileFireTalon fireTalon = projectileObj.GetComponent<ProjectileFireTalon>();
        ProjectileIceTalon iceTalon = projectileObj.GetComponent<ProjectileIceTalon>();
        
        if (fireTalon != null)
        {
            Debug.Log($"<color=cyan>Found ProjectileFireTalon, launching with direction: {worldDirection2D}</color>");
            Vector2 talonOffset = fireTalon.GetSpawnOffset(worldDirection2D);
            projectileObj.transform.position += (Vector3)talonOffset;
            fireTalon.Launch(worldDirection2D, GetComponent<Collider2D>(), playerMana);
            Debug.Log($"<color=cyan>✅ ProjectileFireTalon spawned via diagonal swipe! Direction: {worldDirection2D}, Offset: {talonOffset}</color>");
            return;
        }
        
        if (iceTalon != null)
        {
            Debug.Log($"<color=cyan>Found ProjectileIceTalon, launching with direction: {worldDirection2D}</color>");
            Vector2 talonOffset = iceTalon.GetSpawnOffset(worldDirection2D);
            projectileObj.transform.position += (Vector3)talonOffset;
            iceTalon.Launch(worldDirection2D, GetComponent<Collider2D>(), playerMana);
            Debug.Log($"<color=cyan>✅ ProjectileIceTalon spawned via diagonal swipe! Direction: {worldDirection2D}, Offset: {talonOffset}</color>");
            return;
        }
        
        // Check for LaserBeamProjectile
        LaserBeamProjectile laser = projectileObj.GetComponent<LaserBeamProjectile>();
        if (laser != null)
        {
            Debug.Log($"<color=cyan>Found LaserBeamProjectile, initializing with direction: {worldDirection2D}</color>");
            laser.Initialize(worldDirection2D);
            Debug.Log($"<color=cyan>✅ Laser spawned via diagonal swipe! Direction: {worldDirection2D}</color>");
            return;
        }
        
        Debug.LogWarning($"<color=orange>⚠️ No recognized projectile component found on {projectilePrefab.name}</color>");
    }
    
    void SwapActiveTornadoes()
    {
        // Find all active tornadoes and swap their types
        TornadoController[] tornadoes = FindObjectsOfType<TornadoController>();
        foreach (TornadoController tornado in tornadoes)
        {
            tornado.SwapType();
        }
        Debug.Log($"Swapped {tornadoes.Length} active tornadoes");
    }
    
    /// <summary>
    /// Compute cooldown from the ACTIVE projectile card and its modifiers.
    /// </summary>
    private float GetActiveProjectileCooldown()
    {
        if (activeProjectileCard == null)
        {
            return 1f;
        }

        // Base interval from card (rarity-adjusted)
        float baseInterval = activeProjectileCard.runtimeSpawnInterval > 0f
            ? activeProjectileCard.runtimeSpawnInterval
            : activeProjectileCard.spawnInterval;

        CardModifierStats modifiers = new CardModifierStats();
        if (ProjectileCardModifiers.Instance != null)
        {
            modifiers = ProjectileCardModifiers.Instance.GetCardModifiers(activeProjectileCard);
        }

        float attackSpeedFromCard = Mathf.Max(0f, modifiers.attackSpeedPercent);

        if (playerStats == null)
        {
            playerStats = GetComponent<PlayerStats>();
        }

        float attackSpeedFromStats = 0f;
        if (playerStats != null)
        {
            attackSpeedFromStats = Mathf.Max(0f, playerStats.attackSpeedPercent);
        }

        float attackSpeedFromAcceleration = 0f;
        StatusController statusController = GetComponent<StatusController>();
        if (statusController != null)
        {
            int accelStacks = statusController.GetStacks(StatusId.Acceleration);
            if (accelStacks > 0)
            {
                float bonusPercent = 25f;
                if (StatusControllerManager.Instance != null)
                {
                    bonusPercent = StatusControllerManager.Instance.AccelerationAttackSpeedPercent;
                }

                // Each stack of Acceleration adds its full bonus percent to
                // the player's attack speed.
                attackSpeedFromAcceleration = Mathf.Max(0f, bonusPercent * accelStacks);
            }
        }

        float totalAttackSpeedPercent = attackSpeedFromCard + attackSpeedFromStats + attackSpeedFromAcceleration;
        float denominator = 1f + (totalAttackSpeedPercent / 100f);
        float interval = denominator > 0f ? baseInterval / denominator : baseInterval;

        float reduction = 0f;
        if (playerStats != null && playerStats.projectileCooldownReduction > 0f)
        {
            reduction = Mathf.Clamp01(playerStats.projectileCooldownReduction);
        }

        float finalInterval = interval * (1f - reduction);

        // Hard cap: at most 60 attacks per second
        float minInterval = 1f / 60f;
        return Mathf.Max(minInterval, finalInterval);
    }

    /// <summary>
    /// Get actual cooldown from projectile card or prefab.
    /// When an active projectile card exists, its cooldown always wins.
    /// </summary>
    private float GetProjectileCooldown(GameObject prefab)
    {
        return GetActiveProjectileCooldown();
    }

    /// <summary>
    /// Get remaining cooldown time for a card (preserves cooldown across enhanced variant swaps)
    /// </summary>
    private float GetCardCooldownRemaining(ProjectileCards card)
    {
        if (card == null) return 0f;
        
        if (cardCooldownTimes.ContainsKey(card))
        {
            float timeSinceLastFire = Time.time - cardCooldownTimes[card];
            float cooldown = GetActiveProjectileCooldown(); // Use active projectile cooldown
            float remaining = Mathf.Max(0f, cooldown - timeSinceLastFire);
            return remaining;
        }
        
        return 0f; // No cooldown active
    }
    
    /// <summary>
    /// Set cooldown time for a card
    /// </summary>
    private void SetCardCooldown(ProjectileCards card)
    {
        if (card != null)
        {
            cardCooldownTimes[card] = Time.time;
            Debug.Log($"<color=cyan>Set cooldown for {card.cardName} at {Time.time:F2}</color>");
        }
    }
    
    /// <summary>
    /// Check if card is ready to fire (cooldown finished)
    /// </summary>
    private bool IsCardReady(ProjectileCards card, float cooldown)
    {
        if (card == null) return true;
        
        if (cardCooldownTimes.ContainsKey(card))
        {
            float timeSinceLastFire = Time.time - cardCooldownTimes[card];
            bool ready = timeSinceLastFire >= cooldown;
            if (!ready)
            {
                Debug.Log($"<color=orange>{card.cardName} on cooldown: {timeSinceLastFire:F2}s / {cooldown:F2}s</color>");
            }
            return ready;
        }
        
        return true; // Never fired, ready to go
    }

    /// <summary>
    /// Register the active projectile card used by the auto-fire system.
    /// Only one active system is allowed; subsequent different cards are ignored.
    /// Selecting the same card again (by name) will update the reference.
    /// </summary>
    public void RegisterActiveProjectileCard(ProjectileCards card)
    {
        if (card == null) return;

        if (activeProjectileCard == null)
        {
            activeProjectileCard = card;
            Debug.Log($"<color=cyan>AdvancedPlayerController: Active projectile card set to {card.cardName}</color>");
            return;
        }

        // Allow updates if it's effectively the same card (same name), but block different systems
        if (activeProjectileCard.cardName == card.cardName)
        {
            activeProjectileCard = card;
            Debug.Log($"<color=cyan>AdvancedPlayerController: Active projectile card {card.cardName} updated</color>");
        }
        else
        {
            Debug.Log($"<color=yellow>AdvancedPlayerController: Active projectile card {activeProjectileCard.cardName} already set, ignoring {card.cardName}</color>");
        }
    }

    void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.OnDeath -= HandlePlayerDeath;
        }
        
        if (swipeDetector != null)
        {
            swipeDetector.OnSwipe -= HandleSwipe;
        }
        
        // REMOVED: fire.started unsubscription - no longer using it
    }
    
    // ============================================
    // PUBLIC METHODS FOR UI BUTTONS
    // ============================================
    
    /// <summary>
    /// Switch to a specific projectile set (called by UI buttons)
    /// </summary>
    public void SwitchToProjectileSet(int setIndex)
    {
        currentProjectileSet = setIndex;
        string setName = setIndex == 0 ? "Fireball/Icicle" : setIndex == 1 ? "Tornado" : "Custom";
        Debug.Log($"<color=cyan>Switched to {setName} (Set {setIndex})</color>");
    }
    
    /// <summary>
    /// Swap projectile sides (Fire ↔ Ice) - called by UI buttons
    /// </summary>
    public void SwapProjectileSides()
    {
        sidesSwapped = !sidesSwapped;
        Debug.Log(sidesSwapped ? "<color=cyan>Sides Swapped: Left=Ice, Right=Fire</color>" : "<color=cyan>Sides Normal: Left=Fire, Right=Ice</color>");
        SwapActiveTornadoes();
    }
    
    /// <summary>
    /// Check if pointer is over UI element
    /// </summary>
    private bool IsPointerOverUI()
    {
        // Check for mouse
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return true;
        }
        
        // Check for touch
        if (Touchscreen.current != null)
        {
            for (int i = 0; i < Touchscreen.current.touches.Count; i++)
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(Touchscreen.current.touches[i].touchId.ReadValue()))
                {
                    return true;
                }
            }
        }
        
        return false;
    }
}
