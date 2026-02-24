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
    [SerializeField] private Transform activeFirePoint;

    [Tooltip("Firepoint for ElementalBeam when firing at enemies on the LEFT side of screen")]
    public Transform elementalBeamFirePointLeft;
    [Tooltip("Firepoint for ElementalBeam when firing at enemies on the RIGHT side of screen")]
    public Transform elementalBeamFirePointRight;

    public Transform ActiveFirePoint => activeFirePoint != null ? activeFirePoint : (firePoint != null ? firePoint : transform);
    public Transform FirePoint => firePoint != null ? firePoint : transform;

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

    [Header("Auto-Fire Status Skip Settings")]
    [Tooltip("If an enemy is farther than this range AND matches certain status conditions (burn-doomed / slow / freeze / poison-doomed), auto-fire will ignore them as targets (unless Immolation is present or they are a boss).")]
    public float StatusRange = 7f;

    [Tooltip("If an enemy is farther than StatusRange and poison alone will kill it within this many seconds, auto-fire will ignore it as a target (non-boss only).")]
    public float PoisonDoomIgnoreDuration = 2f;

    [Header("Auto-Fire Area Points (Optional Square)")]
    [Tooltip("Optional corner A of auto-fire area (e.g., top-left)")]
    [SerializeField] private Transform autoFirePointA;
    [Tooltip("Optional corner B of auto-fire area (e.g., top-right)")]
    [SerializeField] private Transform autoFirePointB;
    [Tooltip("Optional corner C of auto-fire area (e.g., bottom-right)")]
    [SerializeField] private Transform autoFirePointC;
    [Tooltip("Optional corner D of auto-fire area (e.g., bottom-left)")]
    [SerializeField] private Transform autoFirePointD;

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
    private const float minFireInterval = 0.05f;

    private int currentProjectileSet = 0;
    private bool sidesSwapped = false;

    private float lastFireballFireTime = -999f;
    private float lastIcicleFireTime = -999f;

    private Dictionary<string, float> cardCooldownTimes = new Dictionary<string, float>();

    [Header("Auto-Fire Doomed Logic")]
    [Tooltip("If enabled, left side uses FIRE guaranteed damage and right side uses ICE guaranteed damage separately. If disabled, both sides share a single pool so you can reuse the same projectile type on both sides.")]
    [SerializeField] private bool useSplitFireIceDoomedLogic = true;

    [Tooltip("Enemies marked as doomed are only skipped while farther than this distance. Once they come closer than this, auto-fire can target them again.")]
    public float doomedRetargetDistance = 3f;

    public float doomedRetargetDistance2 = 3f;
    public float doomedRetargetDistance3 = 3f;

    [Tooltip("Enemies within this radius will never be registered as doomed (auto-fire can still target them).")]
    public float doomSkipRadius = 0f;

    [Header("Doomed Skip Duration")]
    [SerializeField] private float defaultDoomedSkipDuration = 0.75f;
    [SerializeField] private float singleEnemyDoomSkipDuration = 0.25f;
    [SerializeField] private float singleEnemyDoomSkipCheckInterval = 0.35f;
    private float nextSingleEnemyDoomSkipCheckTime = 0f;
    private bool cachedSingleNonBossEnemyAlive = false;

    [Tooltip("Global exchange rate for active projectiles: +1 speedIncrease on the active card reduces doomedSkipDuration by this many seconds.")]
    public float doomedSkipDurationPerSpeed = 0.1f;

    private Dictionary<EnemyHealth, float> guaranteedIncomingFireDamage = new Dictionary<EnemyHealth, float>();
    private Dictionary<EnemyHealth, float> guaranteedIncomingIceDamage = new Dictionary<EnemyHealth, float>();

    private Dictionary<EnemyHealth, float> cumulativeIncomingFireDamage = new Dictionary<EnemyHealth, float>();
    private Dictionary<EnemyHealth, float> cumulativeIncomingIceDamage = new Dictionary<EnemyHealth, float>();

    private ProjectileCards activeProjectileCard;
    private ProjectileModifierApplier projectileModifierApplier;

    // ====
    // NEW: auto-fire-only incoming status prediction
    // ====
    private class PendingStatusPrediction
    {
        public EnemyHealth enemy;
        public float expiresAt;

        public bool willApplySlow;
        public int slowStacksPerHit;

        public bool willApplyBurn;
        public int burnStacksPerHit;

        // Burn damage prediction uses per-shot predictedDamage
        public float predictedHitDamageLowerBound;
        public ProjectileType projectileType;

        // Store the firing projectile's burn tuning so prediction doesn't rely on active prefab
        public float burnDamageMultiplier;
        public float burnDurationSeconds;
    }

    private readonly List<PendingStatusPrediction> pendingPredictions = new List<PendingStatusPrediction>();
    private readonly Dictionary<EnemyHealth, int> incomingSlowStacks = new Dictionary<EnemyHealth, int>();
    private readonly Dictionary<EnemyHealth, int> incomingBurnStacks = new Dictionary<EnemyHealth, int>();
    private readonly Dictionary<EnemyHealth, float> incomingHitDamageLowerBound = new Dictionary<EnemyHealth, float>();

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

        if (swipeDetector != null)
        {
            swipeDetector.OnSwipe += HandleSwipe;
        }

        if (advancedGestureDetector != null)
        {
            advancedGestureDetector.OnDualHorizontalSwipe += HandleDualHorizontalSwipe;
            advancedGestureDetector.OnDiagonalSwipe += HandleDiagonalSwipe;
        }
    }

    void Update()
    {
        if (isDead) return;

        if (CardSelectionManager.Instance != null && CardSelectionManager.Instance.IsSelectionActive())
        {
            return;
        }

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

        rb.velocity = new Vector2(moveInput.x * movespeed * speedMult, moveInput.y * movespeed * speedMult);

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (!IsPointerOverUI())
            {
                if (!RuntimeProjectileRadiusGizmoManager.WasClickHandledThisFrame)
                {
                    FireProjectile();
                }
            }
        }

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            currentProjectileSet = 0;
        }

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            sidesSwapped = !sidesSwapped;
        }

        if (enableAutoFire && activeProjectileCard != null)
        {
            float fireCooldown = GetProjectileCooldown(fireballPrefab);
            float iceCooldown = GetProjectileCooldown(iciclePrefab);
            float cooldownToUse = useIndependentCooldowns ? Mathf.Min(fireCooldown, iceCooldown) : Mathf.Max(fireCooldown, iceCooldown);

            if (GameStateManager.PauseSafeTime - lastAutoFireTime >= cooldownToUse)
            {
                TryAutoFire();
            }
        }
    }

    private StatusController FindEnemyStatusController(EnemyHealth enemyHealth)
    {
        if (enemyHealth == null) return null;

        StatusController sc = enemyHealth.GetComponent<StatusController>();
        if (sc != null) return sc;

        sc = enemyHealth.GetComponentInParent<StatusController>();
        if (sc != null) return sc;

        sc = enemyHealth.GetComponentInChildren<StatusController>();
        return sc;
    }

    private int GetConservativeRemainingBurnTicks(float remainingDuration, float tickIntervalSeconds)
    {
        float dur = Mathf.Max(0f, remainingDuration);
        float interval = Mathf.Max(0.01f, tickIntervalSeconds);

        if (dur <= 0f) return 0;

        int ticks = Mathf.CeilToInt(dur / interval);
        return Mathf.Max(1, ticks);
    }

    private void CleanupIncomingPredictions()
    {
        if (pendingPredictions.Count == 0) return;

        float now = GameStateManager.PauseSafeTime;
        bool changed = false;

        for (int i = pendingPredictions.Count - 1; i >= 0; i--)
        {
            PendingStatusPrediction p = pendingPredictions[i];
            if (p == null || p.enemy == null || !p.enemy.IsAlive || now >= p.expiresAt)
            {
                pendingPredictions.RemoveAt(i);
                changed = true;
            }
        }

        if (!changed) return;

        incomingSlowStacks.Clear();
        incomingBurnStacks.Clear();
        incomingHitDamageLowerBound.Clear();

        for (int i = 0; i < pendingPredictions.Count; i++)
        {
            PendingStatusPrediction p = pendingPredictions[i];
            if (p == null || p.enemy == null) continue;

            float hit = Mathf.Max(0f, p.predictedHitDamageLowerBound);
            if (hit > 0f)
            {
                incomingHitDamageLowerBound.TryGetValue(p.enemy, out float d);
                incomingHitDamageLowerBound[p.enemy] = d + hit;
            }

            if (p.willApplySlow && p.slowStacksPerHit > 0)
            {
                incomingSlowStacks.TryGetValue(p.enemy, out int s);
                incomingSlowStacks[p.enemy] = s + p.slowStacksPerHit;
            }

            if (p.willApplyBurn && p.burnStacksPerHit > 0)
            {
                incomingBurnStacks.TryGetValue(p.enemy, out int b);
                incomingBurnStacks[p.enemy] = b + p.burnStacksPerHit;
            }
        }
    }

    private bool IsConservativelyBurnDoomed(EnemyHealth enemyHealth, StatusController statusController, float incomingHitDamageLowerBound)
    {
        if (enemyHealth == null || statusController == null)
        {
            return false;
        }

        EnemyCardTag cardTag = enemyHealth.GetComponent<EnemyCardTag>() ?? enemyHealth.GetComponentInParent<EnemyCardTag>();
        if (cardTag != null && cardTag.rarity == CardRarity.Boss)
        {
            return false;
        }

        if (!enemyHealth.IsAlive)
        {
            return true;
        }

        float hpNow = enemyHealth.CurrentHealth;
        if (hpNow <= 0f)
        {
            return true;
        }

        float burnDurationRemaining = statusController.GetMaxRemainingDurationSeconds(StatusId.Burn);
        if (burnDurationRemaining <= 0f)
        {
            return false;
        }

        float tickInterval = 0.25f;
        if (StatusControllerManager.Instance != null)
        {
            tickInterval = StatusControllerManager.Instance.BurnTickIntervalSeconds;
        }

        int ticksRemaining = GetConservativeRemainingBurnTicks(burnDurationRemaining, tickInterval);
        if (ticksRemaining <= 0)
        {
            return false;
        }

        float tickDamage = statusController.GetCurrentBurnTickDamage();
        if (tickDamage <= 0f)
        {
            return false;
        }

        float futureBurnDamage = tickDamage * ticksRemaining;
        float totalLowerBound = futureBurnDamage + Mathf.Max(0f, incomingHitDamageLowerBound);

        return totalLowerBound >= hpNow;
    }

    private bool ShouldIgnoreTargetByStatusRules(EnemyHealth enemyHealth, float distToPlayer)
    {
        if (enemyHealth == null)
        {
            return false;
        }

        EnemyCardTag cardTag = enemyHealth.GetComponent<EnemyCardTag>() ?? enemyHealth.GetComponentInParent<EnemyCardTag>();
        if (cardTag != null && cardTag.rarity == CardRarity.Boss)
        {
            return false;
        }

        CleanupIncomingPredictions();

        if (distToPlayer <= StatusRange)
        {
            return false;
        }

        StatusController sc = FindEnemyStatusController(enemyHealth);
        if (sc != null && sc.HasStatus(StatusId.Immolation))
        {
            return false;
        }

        int slowStacks = sc != null ? sc.GetStacks(StatusId.Slow) : 0;
        int freezeStacks = sc != null ? sc.GetStacks(StatusId.Freeze) : 0;

        incomingSlowStacks.TryGetValue(enemyHealth, out int incomingSlow);
        incomingBurnStacks.TryGetValue(enemyHealth, out int incomingBurn);
        incomingHitDamageLowerBound.TryGetValue(enemyHealth, out float incomingHitDamage);

        if (freezeStacks > 0 || slowStacks > 0 || incomingSlow > 0)
        {
            return true;
        }

        bool doomedByBurn = false;
        if (sc != null && sc.GetStacks(StatusId.Burn) > 0)
        {
            doomedByBurn = IsConservativelyBurnDoomed(enemyHealth, sc, incomingHitDamage);
        }

        if (!doomedByBurn && incomingBurn > 0 && incomingHitDamage >= enemyHealth.CurrentHealth)
        {
            doomedByBurn = true;
        }

        bool doomedByPoison = false;
        if (sc != null && PoisonDoomIgnoreDuration > 0f)
        {
            float remainingPoison = sc.EstimateRemainingPoisonDamageWithinWindow(PoisonDoomIgnoreDuration);
            if (remainingPoison > 0f && remainingPoison >= enemyHealth.CurrentHealth)
            {
                doomedByPoison = true;
            }
        }

        return doomedByBurn || doomedByPoison;
    }

    private void HandleSwipe(Vector2 swipeDirection)
    {
    }

    private void HandleDualHorizontalSwipe()
    {
        sidesSwapped = !sidesSwapped;
    }

    [Header("Directional Swipe Projectiles")]
    public GameObject leftDiagonalProjectile;
    public GameObject rightDiagonalProjectile;

    private void HandleDiagonalSwipe(bool isLeftDiagonal, Vector2 swipeDirection, Vector2 startPos, Vector2 endPos)
    {
        GameObject projectileToSpawn = isLeftDiagonal ? leftDiagonalProjectile : rightDiagonalProjectile;
        if (projectileToSpawn == null) return;
        SpawnDiagonalProjectileWithDirection(swipeDirection, endPos, projectileToSpawn);
    }

    public void HandleTouchInput(Vector2 screenPosition)
    {
        FireProjectileAtScreenPosition(screenPosition);
    }

    void FireProjectile()
    {
        if (isDead || playerMana == null) return;

        if (GameStateManager.PauseSafeTime - lastFireTime < minFireInterval)
        {
            return;
        }

        Vector2 screenPos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        FireProjectileAtScreenPosition(screenPos);
    }

    void FireProjectileAtScreenPosition(Vector2 screenPosition)
    {
        if (isDead || playerMana == null)
        {
            return;
        }

        if (!RuntimeProjectileRadiusGizmoManager.WasClickHandledThisFrame)
        {
            if (RuntimeProjectileRadiusGizmoManager.TryHandleClick(screenPosition))
            {
                return;
            }
        }
        else
        {
            return;
        }

        if (GameStateManager.PauseSafeTime - lastFireTime < minFireInterval)
        {
            Debug.Log($"<color=orange>FireProjectile blocked: Too soon! {GameStateManager.PauseSafeTime - lastFireTime:F3}s < {minFireInterval}s</color>");
            return;
        }

        Transform spawnFirePoint = ActiveFirePoint;

        Ray ray = cam.ScreenPointToRay(screenPosition);
        Plane gamePlane = new Plane(Vector3.forward, spawnFirePoint.position.z);

        if (!gamePlane.Raycast(ray, out float enter)) return;

        Vector3 worldTouchPosition = ray.GetPoint(enter);
        Vector2 fireDirection = (worldTouchPosition - spawnFirePoint.position).normalized;

        bool isLeftSide = screenPosition.x < Screen.width / 2f;
        bool shouldBeFire = (isLeftSide && !sidesSwapped) || (!isLeftSide && sidesSwapped);

        GameObject prefabToUse = GetProjectilePrefab(shouldBeFire);

        if (prefabToUse == null)
        {
            Debug.LogWarning($"No prefab assigned for set {currentProjectileSet}!");
            return;
        }

        lastFireTime = GameStateManager.PauseSafeTime;

        Vector3 spawnPosition = spawnFirePoint.position;
        Vector2 spawnOffset = Vector2.zero;
        PlayerProjectiles prefabScript = prefabToUse.GetComponent<PlayerProjectiles>();
        FireBall prefabFireBall = prefabToUse.GetComponent<FireBall>();
        ThunderDisc prefabThunderDisc = prefabToUse.GetComponent<ThunderDisc>();
        if (prefabScript != null)
        {
            spawnOffset = prefabScript.GetSpawnOffset(fireDirection);
            spawnPosition += (Vector3)spawnOffset;
            Debug.Log($"<color=cyan>Manual Fire: Applied spawn offset {spawnOffset} before instantiation</color>");
        }
        else if (prefabFireBall != null)
        {
            spawnOffset = prefabFireBall.GetSpawnOffset(fireDirection);
            spawnPosition += (Vector3)spawnOffset;
            Debug.Log($"<color=cyan>Manual Fire: Applied spawn offset {spawnOffset} before instantiation</color>");
        }
        else if (prefabThunderDisc != null)
        {
            spawnOffset = prefabThunderDisc.GetSpawnOffset(fireDirection);
            spawnPosition += (Vector3)spawnOffset;
            Debug.Log($"<color=cyan>Manual Fire: Applied spawn offset {spawnOffset} before instantiation</color>");
        }

        GameObject projectileObj = Instantiate(prefabToUse, spawnPosition, Quaternion.identity);

        if (activeProjectileCard != null && ProjectileCardModifiers.Instance != null)
        {
            ProjectileCardModifiers.Instance.TagProjectileWithCard(projectileObj, activeProjectileCard);

            if (projectileModifierApplier != null)
            {
                projectileModifierApplier.ApplyModifiersToProjectile(projectileObj, activeProjectileCard);
            }
        }

        PlayerProjectiles projectile = projectileObj.GetComponent<PlayerProjectiles>();
        FireBall fireBall = projectileObj.GetComponent<FireBall>();
        ProjectileFireTalon fireTalon = projectileObj.GetComponent<ProjectileFireTalon>();
        ProjectileIceTalon iceTalon = projectileObj.GetComponent<ProjectileIceTalon>();
        ThunderDisc thunderDisc = projectileObj.GetComponent<ThunderDisc>();

        if (projectile != null)
        {
            projectile.Launch(fireDirection, playerCollider, playerMana);
        }
        else if (fireBall != null)
        {
            fireBall.Launch(fireDirection, playerCollider, playerMana);
        }
        else if (fireTalon != null)
        {
            Vector2 talonOffset = fireTalon.GetSpawnOffset(fireDirection);
            projectileObj.transform.position += (Vector3)talonOffset;
            fireTalon.Launch(fireDirection, playerCollider, playerMana);
        }
        else if (iceTalon != null)
        {
            Vector2 talonOffset = iceTalon.GetSpawnOffset(fireDirection);
            projectileObj.transform.position += (Vector3)talonOffset;
            iceTalon.Launch(fireDirection, playerCollider, playerMana);
        }
        else if (thunderDisc != null)
        {
            thunderDisc.Launch(fireDirection, playerCollider, playerMana);
        }
        else
        {
            Debug.LogError($"Prefab {prefabToUse.name} has no recognized projectile component!");
            Destroy(projectileObj);
        }

        Debug.DrawRay(spawnFirePoint.position, fireDirection * 5f, shouldBeFire ? Color.red : Color.cyan, 2f);
    }

    private GameObject GetProjectilePrefab(bool isFire)
    {
        return isFire ? fireballPrefab : iciclePrefab;
    }

    private void TryAutoFire()
    {
        if (activeProjectileCard == null || activeProjectileCard.projectilePrefab == null)
        {
            Debug.LogWarning("<color=yellow>Auto-fire: Active projectile card or its prefab is not assigned!</color>");
            return;
        }

        // clean doomed maps
        if (guaranteedIncomingFireDamage.Count > 0)
        {
            var keys = new List<EnemyHealth>(guaranteedIncomingFireDamage.Keys);
            float now = GameStateManager.PauseSafeTime;
            foreach (var eh in keys)
            {
                if (eh == null || !eh.IsAlive)
                {
                    guaranteedIncomingFireDamage.Remove(eh);
                    cumulativeIncomingFireDamage.Remove(eh);
                    continue;
                }

                EnemyCardTag tag = eh.GetComponent<EnemyCardTag>() ?? eh.GetComponentInParent<EnemyCardTag>();
                if (tag != null && tag.rarity == CardRarity.Boss)
                {
                    guaranteedIncomingFireDamage.Remove(eh);
                    guaranteedIncomingIceDamage.Remove(eh);
                    cumulativeIncomingFireDamage.Remove(eh);
                    cumulativeIncomingIceDamage.Remove(eh);
                    continue;
                }

                float expiryTime;
                if (guaranteedIncomingFireDamage.TryGetValue(eh, out expiryTime))
                {
                    if (now > expiryTime || Vector2.Distance(transform.position, eh.transform.position) <= doomedRetargetDistance)
                    {
                        guaranteedIncomingFireDamage.Remove(eh);
                        cumulativeIncomingFireDamage.Remove(eh);
                    }
                }
            }
        }

        if (guaranteedIncomingIceDamage.Count > 0)
        {
            var keys = new List<EnemyHealth>(guaranteedIncomingIceDamage.Keys);
            float now = GameStateManager.PauseSafeTime;
            foreach (var eh in keys)
            {
                if (eh == null || !eh.IsAlive)
                {
                    guaranteedIncomingIceDamage.Remove(eh);
                    cumulativeIncomingIceDamage.Remove(eh);
                    continue;
                }

                EnemyCardTag tag = eh.GetComponent<EnemyCardTag>() ?? eh.GetComponentInParent<EnemyCardTag>();
                if (tag != null && tag.rarity == CardRarity.Boss)
                {
                    guaranteedIncomingFireDamage.Remove(eh);
                    guaranteedIncomingIceDamage.Remove(eh);
                    cumulativeIncomingFireDamage.Remove(eh);
                    cumulativeIncomingIceDamage.Remove(eh);
                    continue;
                }

                float expiryTime;
                if (guaranteedIncomingIceDamage.TryGetValue(eh, out expiryTime))
                {
                    if (now > expiryTime || Vector2.Distance(transform.position, eh.transform.position) <= doomedRetargetDistance)
                    {
                        guaranteedIncomingIceDamage.Remove(eh);
                        cumulativeIncomingIceDamage.Remove(eh);
                    }
                }
            }
        }

        GameObject currentFireballPrefab = activeProjectileCard.projectilePrefab;
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

        List<GameObject> leftCandidates = new List<GameObject>();
        List<float> leftDistances = new List<float>();
        List<GameObject> rightCandidates = new List<GameObject>();
        List<float> rightDistances = new List<float>();

        foreach (Collider2D enemyCol in enemiesInRange)
        {
            if (enemyCol == null || enemyCol.gameObject == null) continue;

            EnemyHealth enemyHealth = enemyCol.GetComponent<EnemyHealth>() ?? enemyCol.GetComponentInParent<EnemyHealth>();
            if (enemyHealth == null || !enemyHealth.IsAlive) continue;

            EnemyCardTag enemyTag = enemyHealth.GetComponent<EnemyCardTag>() ?? enemyHealth.GetComponentInParent<EnemyCardTag>();
            if (enemyTag != null && enemyTag.rarity == CardRarity.Boss)
            {
                guaranteedIncomingFireDamage.Remove(enemyHealth);
                guaranteedIncomingIceDamage.Remove(enemyHealth);
                cumulativeIncomingFireDamage.Remove(enemyHealth);
                cumulativeIncomingIceDamage.Remove(enemyHealth);
            }

            Vector2 enemyCenter = enemyCol.bounds.center;
            float distToPlayer = Vector2.Distance(transform.position, enemyCenter);

            if (doomSkipRadius > 0f && distToPlayer <= doomSkipRadius)
            {
                guaranteedIncomingFireDamage.Remove(enemyHealth);
                guaranteedIncomingIceDamage.Remove(enemyHealth);
                cumulativeIncomingFireDamage.Remove(enemyHealth);
                cumulativeIncomingIceDamage.Remove(enemyHealth);
            }

            if (ShouldIgnoreTargetByStatusRules(enemyHealth, distToPlayer))
            {
                continue;
            }

            bool isOnLeft = enemyCenter.x < transform.position.x;

            if (isOnLeft)
            {
                Dictionary<EnemyHealth, float> doomedDict = guaranteedIncomingFireDamage;
                Dictionary<EnemyHealth, float> cumulativeDict = cumulativeIncomingFireDamage;
                if (useSplitFireIceDoomedLogic)
                {
                    doomedDict = guaranteedIncomingFireDamage;
                    cumulativeDict = cumulativeIncomingFireDamage;
                }

                float now = GameStateManager.PauseSafeTime;
                if (doomedDict.TryGetValue(enemyHealth, out float expiryTime))
                {
                    if (now > expiryTime || distToPlayer <= doomedRetargetDistance)
                    {
                        doomedDict.Remove(enemyHealth);
                        cumulativeDict.Remove(enemyHealth);
                    }
                    else
                    {
                        continue;
                    }
                }

                InsertAutoFireCandidate(leftCandidates, leftDistances, enemyCol.gameObject, distToPlayer, 3);
            }
            else
            {
                Dictionary<EnemyHealth, float> doomedDict = guaranteedIncomingFireDamage;
                Dictionary<EnemyHealth, float> cumulativeDict = cumulativeIncomingFireDamage;
                if (useSplitFireIceDoomedLogic)
                {
                    doomedDict = guaranteedIncomingIceDamage;
                    cumulativeDict = cumulativeIncomingIceDamage;
                }

                float now = GameStateManager.PauseSafeTime;
                if (doomedDict.TryGetValue(enemyHealth, out float expiryTime))
                {
                    if (now > expiryTime || distToPlayer <= doomedRetargetDistance)
                    {
                        doomedDict.Remove(enemyHealth);
                        cumulativeDict.Remove(enemyHealth);
                    }
                    else
                    {
                        continue;
                    }
                }

                InsertAutoFireCandidate(rightCandidates, rightDistances, enemyCol.gameObject, distToPlayer, 3);
            }
        }

        float fireCooldown = GetProjectileCooldown(currentFireballPrefab);
        float iceCooldown = GetProjectileCooldown(currentIciclePrefab);

        ProjectileCards fireCard = activeProjectileCard;
        ProjectileCards iceCard = activeProjectileCard;

        bool firedLeft = false;
        bool firedRight = false;

        if (useIndependentCooldowns)
        {
            if (leftCandidates.Count > 0 && IsCardReady(fireCard, fireCooldown))
            {
                for (int i = 0; i < leftCandidates.Count; i++)
                {
                    GameObject target = leftCandidates[i];
                    if (target == null) continue;

                    Vector2 predictedPos = PredictEnemyPosition(target);
                    Vector2 fireDir = (predictedPos - (Vector2)ActiveFirePoint.position).normalized;

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
                    Vector2 fireDir = (predictedPos - (Vector2)ActiveFirePoint.position).normalized;

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
                lastAutoFireTime = GameStateManager.PauseSafeTime;
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
                Vector2 fireDir = (predictedPos - (Vector2)ActiveFirePoint.position).normalized;
                if (FireAutoProjectile(prefabToUse, fireDir, sharedIsLeft, sharedTarget.transform))
                {
                    SetCardCooldown(fireCard);
                    SetCardCooldown(iceCard);
                    lastAutoFireTime = GameStateManager.PauseSafeTime;
                }
            }
        }
    }

    private bool FireAutoProjectile(GameObject prefab, Vector2 direction, bool isFire, Transform target = null)
    {
        if (prefab == null || playerMana == null) return false;

        if (target != null)
        {
            EnemyHealth enemyHealth = target.GetComponent<EnemyHealth>() ?? target.GetComponentInParent<EnemyHealth>();
            if (enemyHealth == null || !enemyHealth.IsAlive)
            {
                return false;
            }
        }

        Vector3 spawnPosition = ActiveFirePoint.position;
        Vector2 spawnOffset = Vector2.zero;
        PlayerProjectiles prefabScript = prefab.GetComponent<PlayerProjectiles>();
        FireBall prefabFireBall = prefab.GetComponent<FireBall>();
        ThunderDisc prefabThunderDisc = prefab.GetComponent<ThunderDisc>();
        if (prefabScript != null)
        {
            spawnOffset = prefabScript.GetSpawnOffset(direction);
            spawnPosition += (Vector3)spawnOffset;
        }
        else if (prefabFireBall != null)
        {
            spawnOffset = prefabFireBall.GetSpawnOffset(direction);
            spawnPosition += (Vector3)spawnOffset;
        }
        else if (prefabThunderDisc != null)
        {
            spawnOffset = prefabThunderDisc.GetSpawnOffset(direction);
            spawnPosition += (Vector3)spawnOffset;
        }

        GameObject projectileObj = Instantiate(prefab, spawnPosition, Quaternion.identity);

        if (activeProjectileCard != null && ProjectileCardModifiers.Instance != null)
        {
            ProjectileCardModifiers.Instance.TagProjectileWithCard(projectileObj, activeProjectileCard);

            if (projectileModifierApplier != null)
            {
                projectileModifierApplier.ApplyModifiersToProjectile(projectileObj, activeProjectileCard);
            }
        }

        PlayerProjectiles projectile = projectileObj.GetComponent<PlayerProjectiles>();
        FireBall fireBall = projectileObj.GetComponent<FireBall>();
        ProjectileFireTalon fireTalon = projectileObj.GetComponent<ProjectileFireTalon>();
        ProjectileIceTalon iceTalon = projectileObj.GetComponent<ProjectileIceTalon>();
        ThunderDisc thunderDisc = projectileObj.GetComponent<ThunderDisc>();

        if (projectile != null)
        {
            projectile.Launch(direction, playerCollider, playerMana);
            RegisterGuaranteedDamage(target, projectileObj, isFire);
        }
        else if (fireBall != null)
        {
            fireBall.Launch(direction, playerCollider, playerMana);
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
        else if (thunderDisc != null)
        {
            thunderDisc.Launch(direction, playerCollider, playerMana);
        }
        else
        {
            Debug.LogError($"Auto-fire prefab {prefab.name} has no recognized projectile component!");
            Destroy(projectileObj);
            return false;
        }

        return true;
    }

    private float EstimateAutoProjectileHitDelaySeconds(GameObject projectileObj, Transform target)
    {
        float fallback = 0.55f;
        if (projectileObj == null || target == null) return fallback;

        PlayerProjectiles pp = projectileObj.GetComponent<PlayerProjectiles>();
        FireBall fb = projectileObj.GetComponent<FireBall>();
        if (pp == null && fb == null) return fallback;

        float speed = Mathf.Max(0.01f, pp != null ? pp.GetProjectileSpeed() : fb.GetProjectileSpeed());

        Vector2 targetPos;
        Collider2D enemyCollider = target.GetComponent<Collider2D>() ?? target.GetComponentInParent<Collider2D>();
        if (enemyCollider != null)
        {
            targetPos = enemyCollider.bounds.center;
        }
        else
        {
            targetPos = target.transform.position;
        }

        float dist = Vector2.Distance(ActiveFirePoint.position, targetPos);

        float travel = dist / speed;
        travel += 0.10f; // buffer
        return Mathf.Clamp(travel, 0.15f, 1.25f);
    }

    private void RegisterIncomingStatusPrediction(GameObject projectileObj, Transform target)
    {
        if (projectileObj == null || target == null) return;

        EnemyHealth eh = target.GetComponent<EnemyHealth>() ?? target.GetComponentInParent<EnemyHealth>();
        if (eh == null || !eh.IsAlive) return;

        SlowEffect slow = projectileObj.GetComponent<SlowEffect>();
        BurnEffect burn = projectileObj.GetComponent<BurnEffect>();
        PlayerProjectiles pp = projectileObj.GetComponent<PlayerProjectiles>();
        FireBall fb = projectileObj.GetComponent<FireBall>();

        bool willSlow = slow != null;
        int slowStacks = willSlow ? Mathf.Clamp(slow.slowStacksPerHit, 1, 4) : 0;

        bool willBurn = burn != null;
        int burnStacks = willBurn ? Mathf.Clamp(burn.burnStacksPerHit, 1, 4) : 0;

        if (!willSlow && !willBurn) return;

        float expiresAt = GameStateManager.PauseSafeTime + EstimateAutoProjectileHitDelaySeconds(projectileObj, target);
        float predictedHitDamageLowerBound = ComputePredictedHitDamageLowerBound(projectileObj, eh);

        PendingStatusPrediction p = new PendingStatusPrediction
        {
            enemy = eh,
            expiresAt = expiresAt,

            willApplySlow = willSlow,
            slowStacksPerHit = slowStacks,

            willApplyBurn = willBurn,
            burnStacksPerHit = burnStacks,

            predictedHitDamageLowerBound = predictedHitDamageLowerBound,
            projectileType = pp != null ? pp.ProjectileElement : (fb != null ? fb.ProjectileElement : ProjectileType.Fire),

            burnDamageMultiplier = burn != null ? burn.burnDamageMultiplier : 0f,
            burnDurationSeconds = burn != null ? burn.burnDuration : 0f
        };

        pendingPredictions.Add(p);

        if (predictedHitDamageLowerBound > 0f)
        {
            incomingHitDamageLowerBound.TryGetValue(eh, out float d);
            incomingHitDamageLowerBound[eh] = d + predictedHitDamageLowerBound;
        }

        if (willSlow && slowStacks > 0)
        {
            incomingSlowStacks.TryGetValue(eh, out int s);
            incomingSlowStacks[eh] = s + slowStacks;
        }

        if (willBurn && burnStacks > 0)
        {
            incomingBurnStacks.TryGetValue(eh, out int b);
            incomingBurnStacks[eh] = b + burnStacks;
        }
    }

    private float ComputePredictedHitDamageLowerBound(GameObject projectileObj, EnemyHealth enemyHealth)
    {
        if (projectileObj == null || enemyHealth == null) return 0f;

        float rawProjectileDamage = 0f;
        bool damageAlreadyIncludesStats = false;

        PlayerProjectiles pp = projectileObj.GetComponent<PlayerProjectiles>();
        FireBall fb = projectileObj.GetComponent<FireBall>();
        if (pp != null)
        {
            rawProjectileDamage = pp.GetCurrentDamage();
            damageAlreadyIncludesStats = false;
        }
        else if (fb != null)
        {
            rawProjectileDamage = fb.GetCurrentDamage();
            damageAlreadyIncludesStats = false;
        }

        if (rawProjectileDamage <= 0f) return 0f;

        float predictedDamage = rawProjectileDamage;

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

        if (predictedDamage <= 0f) return 0f;

        if (playerStats != null)
        {
            FavourEffectManager favourManager = playerStats.GetComponent<FavourEffectManager>();
            if (favourManager != null)
            {
                predictedDamage = favourManager.PreviewBeforeDealDamage(enemyHealth.gameObject, predictedDamage);
            }
        }

        return Mathf.Max(0f, predictedDamage);
    }

    private float GetCurrentDoomedSkipDuration()
    {
        if (activeProjectileCard != null && activeProjectileCard.projectileSystem == ProjectileCards.ProjectileSystemType.Active)
        {
            if (GameStateManager.PauseSafeTime >= nextSingleEnemyDoomSkipCheckTime)
            {
                nextSingleEnemyDoomSkipCheckTime = GameStateManager.PauseSafeTime + Mathf.Max(0.05f, singleEnemyDoomSkipCheckInterval);
                cachedSingleNonBossEnemyAlive = IsExactlyOneNonBossEnemyAlive();
            }

            if (cachedSingleNonBossEnemyAlive && singleEnemyDoomSkipDuration > 0f)
            {
                return singleEnemyDoomSkipDuration;
            }
        }

        // Base duration comes from the active projectile card when set,
        // otherwise fall back to the global default.
        float baseDuration = defaultDoomedSkipDuration;
        if (activeProjectileCard != null && activeProjectileCard.doomedSkipDuration > 0f)
        {
            baseDuration = activeProjectileCard.doomedSkipDuration;
        }

        // If we have no active card or no exchange rate, just return the base value.
        if (activeProjectileCard == null || doomedSkipDurationPerSpeed <= 0f || ProjectileCardModifiers.Instance == null)
        {
            return baseDuration;
        }

        // Use the card's current speedIncrease modifier as the "+speed" value
        // that shrinks doomedSkipDuration. Example: baseDuration=3s,
        // speedIncrease=+10, exchangeRate=0.1s → 3 - (10*0.1) = 2s.
        CardModifierStats modifiers = ProjectileCardModifiers.Instance.GetCardModifiers(activeProjectileCard);
        float speedIncrease = Mathf.Max(0f, modifiers.speedIncrease);

        if (speedIncrease <= 0f)
        {
            return baseDuration;
        }

        float reduction = speedIncrease * doomedSkipDurationPerSpeed;
        float minDuration = 0.1f;
        return Mathf.Max(minDuration, baseDuration - reduction);
    }

    private bool IsExactlyOneNonBossEnemyAlive()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        int aliveNonBossCount = 0;

        for (int i = 0; i < enemies.Length; i++)
        {
            GameObject enemy = enemies[i];
            if (enemy == null) continue;

            EnemyHealth eh = enemy.GetComponent<EnemyHealth>() ?? enemy.GetComponentInChildren<EnemyHealth>();
            if (eh == null || !eh.IsAlive) continue;

            EnemyCardTag tag = enemy.GetComponent<EnemyCardTag>() ?? enemy.GetComponentInChildren<EnemyCardTag>();
            if (tag != null && tag.rarity == CardRarity.Boss)
            {
                continue;
            }

            aliveNonBossCount++;
            if (aliveNonBossCount > 1)
            {
                return false;
            }
        }

        return aliveNonBossCount == 1;
    }

    private void RegisterGuaranteedDamage(Transform target, GameObject projectileObj, bool isFire)
    {
        if (target == null || projectileObj == null)
        {
            return;
        }

        EnemyHealth enemyHealth = target.GetComponent<EnemyHealth>() ?? target.GetComponentInParent<EnemyHealth>();
        if (enemyHealth == null || !enemyHealth.IsAlive)
        {
            return;
        }

        EnemyCardTag cardTag = enemyHealth.GetComponent<EnemyCardTag>() ?? enemyHealth.GetComponentInParent<EnemyCardTag>();
        if (cardTag != null && cardTag.rarity == CardRarity.Boss)
        {
            guaranteedIncomingFireDamage.Remove(enemyHealth);
            guaranteedIncomingIceDamage.Remove(enemyHealth);
            cumulativeIncomingFireDamage.Remove(enemyHealth);
            cumulativeIncomingIceDamage.Remove(enemyHealth);
            return;
        }

        if (doomSkipRadius > 0f)
        {
            Collider2D targetCollider = target.GetComponent<Collider2D>() ?? target.GetComponentInParent<Collider2D>();
            Vector2 targetPos = targetCollider != null ? (Vector2)targetCollider.bounds.center : (Vector2)target.position;
            float distToPlayer = Vector2.Distance(transform.position, targetPos);

            if (distToPlayer <= doomSkipRadius)
            {
                guaranteedIncomingFireDamage.Remove(enemyHealth);
                guaranteedIncomingIceDamage.Remove(enemyHealth);
                cumulativeIncomingFireDamage.Remove(enemyHealth);
                cumulativeIncomingIceDamage.Remove(enemyHealth);
                return;
            }
        }

        float rawProjectileDamage = 0f;
        bool damageAlreadyIncludesStats = false;

        PlayerProjectiles pp = projectileObj.GetComponent<PlayerProjectiles>();
        FireBall fb = projectileObj.GetComponent<FireBall>();
        if (pp != null)
        {
            rawProjectileDamage = pp.GetCurrentDamage();
            damageAlreadyIncludesStats = false;
        }
        else if (fb != null)
        {
            rawProjectileDamage = fb.GetCurrentDamage();
            damageAlreadyIncludesStats = false;
        }

        if (rawProjectileDamage <= 0f)
        {
            return;
        }

        float predictedDamage = rawProjectileDamage;

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

        float currentHpAtFire = enemyHealth.CurrentHealth;

        Dictionary<EnemyHealth, float> doomedDict = guaranteedIncomingFireDamage;
        Dictionary<EnemyHealth, float> cumulativeDict = cumulativeIncomingFireDamage;

        if (useSplitFireIceDoomedLogic && !isFire)
        {
            doomedDict = guaranteedIncomingIceDamage;
            cumulativeDict = cumulativeIncomingIceDamage;
        }

        float existing;
        cumulativeDict.TryGetValue(enemyHealth, out existing);
        float newTotal = Mathf.Max(0f, existing) + predictedDamage;
        cumulativeDict[enemyHealth] = newTotal;

        if (newTotal < currentHpAtFire)
        {
            return;
        }

        doomedDict[enemyHealth] = GameStateManager.PauseSafeTime + GetCurrentDoomedSkipDuration();
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

    private Vector2 PredictEnemyPosition(GameObject enemy)
    {
        if (enemy == null) return Vector2.zero;

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

        Rigidbody2D enemyRb = enemy.GetComponent<Rigidbody2D>();
        if (enemyRb != null && enemyRb.velocity.sqrMagnitude > 0.01f)
        {
            float predictionTime = 0.5f;
            return enemyPos + enemyRb.velocity * predictionTime;
        }
        else
        {
            Vector2 dirToPlayer = ((Vector2)transform.position - enemyPos).normalized;
            float estimatedSpeed = 2.5f;
            float predictionTime = 0.5f;

            return enemyPos + dirToPlayer * estimatedSpeed * predictionTime;
        }
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
        if (enableAutoFire && showAutoFireGizmo &&
            autoFirePointA != null && autoFirePointB != null &&
            autoFirePointC != null && autoFirePointD != null)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.5f);

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
        yield return GameStateManager.WaitForPauseSafeSeconds(1f);

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

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ResetRunState();
        }

        FavourEffect.ResetPickCounts();

        if (EnemyScalingSystem.Instance != null)
        {
            EnemyScalingSystem.Instance.ResetScaling();
        }

        if (ProjectileCardLevelSystem.Instance != null)
        {
            ProjectileCardLevelSystem.Instance.ResetAllLevels();
        }

        if (ProjectileCardModifiers.Instance != null)
        {
            ProjectileCardModifiers.Instance.ResetRunState();
        }

        HolyShield.ResetRunState();
        ReflectShield.ResetRunState();
        NullifyShield.ResetRunState();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void SpawnDiagonalProjectile(Vector2 screenPosition, GameObject projectilePrefab)
    {
        // unchanged (your debug-heavy method)
    }

    private void SpawnDiagonalProjectileWithDirection(Vector2 swipeDirection, Vector2 endScreenPos, GameObject projectilePrefab)
    {
        // unchanged (your debug-heavy method)
    }

    private float GetActiveProjectileCooldown()
    {
        if (activeProjectileCard == null)
        {
            return 1f;
        }

        float baseInterval = activeProjectileCard.runtimeSpawnInterval > 0f
            ? activeProjectileCard.runtimeSpawnInterval
            : activeProjectileCard.spawnInterval;

        CardModifierStats modifiers = new CardModifierStats();
        if (ProjectileCardModifiers.Instance != null)
        {
            modifiers = ProjectileCardModifiers.Instance.GetCardModifiers(activeProjectileCard);
        }

        float attackSpeedFromCard = modifiers.attackSpeedPercent;

        if (playerStats == null)
        {
            playerStats = GetComponent<PlayerStats>();
        }

        float attackSpeedFromStats = playerStats != null ? playerStats.attackSpeedPercent : 0f;

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
                attackSpeedFromAcceleration = Mathf.Max(0f, bonusPercent * accelStacks);
            }
        }

        float totalAttackSpeedPercent = attackSpeedFromCard + attackSpeedFromStats + attackSpeedFromAcceleration;
        float denominator = 1f + (totalAttackSpeedPercent / 100f);
        denominator = Mathf.Max(0.0001f, denominator);
        float interval = baseInterval / denominator;

        float finalInterval = interval;
        if (playerStats != null && playerStats.projectileCooldownReduction > 0f)
        {
            float totalCdr = Mathf.Max(0f, playerStats.projectileCooldownReduction);
            finalInterval = interval / (1f + totalCdr);
        }

        float minInterval = 1f / 60f;
        return Mathf.Max(minInterval, finalInterval);
    }

    private float GetProjectileCooldown(GameObject prefab)
    {
        return GetActiveProjectileCooldown();
    }

    private float GetCardCooldownRemaining(ProjectileCards card)
    {
        if (card == null) return 0f;

        string key = card.cardName;
        if (string.IsNullOrEmpty(key)) return 0f;

        if (cardCooldownTimes.ContainsKey(key))
        {
            float timeSinceLastFire = GameStateManager.PauseSafeTime - cardCooldownTimes[key];
            float cooldown = GetActiveProjectileCooldown();
            float remaining = Mathf.Max(0f, cooldown - timeSinceLastFire);
            return remaining;
        }

        return 0f;
    }

    private void SetCardCooldown(ProjectileCards card)
    {
        if (card != null)
        {
            string key = card.cardName;
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            cardCooldownTimes[key] = GameStateManager.PauseSafeTime;
            Debug.Log($"<color=cyan>Set cooldown for {card.cardName} at {GameStateManager.PauseSafeTime:F2}</color>");
        }
    }

    private bool IsCardReady(ProjectileCards card, float cooldown)
    {
        if (card == null) return true;

        string key = card.cardName;
        if (string.IsNullOrEmpty(key)) return true;

        if (cardCooldownTimes.ContainsKey(key))
        {
            float timeSinceLastFire = GameStateManager.PauseSafeTime - cardCooldownTimes[key];
            bool ready = timeSinceLastFire >= cooldown;
            if (!ready)
            {
                Debug.Log($"<color=orange>{card.cardName} on cooldown: {timeSinceLastFire:F2}s / {cooldown:F2}s</color>");
            }

            return ready;
        }

        return true;
    }

    public void RefreshAllActiveProjectileCooldowns()
    {
        cardCooldownTimes.Clear();
        lastAutoFireTime = -999f;
    }

    public void RegisterActiveProjectileCard(ProjectileCards card)
    {
        if (card == null) return;

        if (activeProjectileCard == null)
        {
            activeProjectileCard = card;
            Debug.Log($"<color=cyan>AdvancedPlayerController: Active projectile card set to {card.cardName}</color>");
            return;
        }

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
    }

    public void SwitchToProjectileSet(int setIndex)
    {
        currentProjectileSet = 0;
        Debug.Log("<color=cyan>Switched to Fireball/Icicle (Set 0)</color>");
    }

    public void SwapProjectileSides()
    {
        sidesSwapped = !sidesSwapped;
        Debug.Log(sidesSwapped ? "<color=cyan>Sides Swapped: Left=Ice, Right=Fire</color>" : "<color=cyan>Sides Normal: Left=Fire, Right=Ice</color>");
    }

    private bool IsPointerOverUI()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return true;
        }

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