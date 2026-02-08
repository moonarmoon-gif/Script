using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System.Collections; // Added for IEnumerator

public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance;

    [Header("Components")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Animator animator;
    [SerializeField] private float movespeed;
    [SerializeField] private PlayerProjectiles projectilePrefab;
    [SerializeField] private Transform firePoint;

    [Header("References")]
    public Camera cam;
    public Input_System playerControls;
    public Vector2 playerMoveDirection;

    private InputAction move;
    private InputAction fire;
    private PlayerHealth playerHealth;
    private PlayerMana playerMana;
    private bool isDead = false;

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

        if (playerMana == null)
        {
            Debug.LogError("PlayerMana component missing from PlayerController!");
            playerMana = gameObject.AddComponent<PlayerMana>();
        }

        playerMana = GetComponent<PlayerMana>();
        if (playerMana == null)
        {
            playerMana = gameObject.AddComponent<PlayerMana>();
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

        fire.performed += ctx => FireProjectile();
    }

    void Update()
    {
        if (isDead) return;

        playerMoveDirection = move.ReadValue<Vector2>();

        StatusController statusController = GetComponent<StatusController>();
        if (statusController != null && StatusControllerManager.Instance != null && playerMoveDirection != Vector2.zero)
        {
            int amnesiaStacks = statusController.GetStacks(StatusId.Amnesia);
            if (amnesiaStacks > 0)
            {
                float perStack = StatusControllerManager.Instance.AmnesiaChancePerStackPercent;
                float chance = Mathf.Clamp01((perStack * amnesiaStacks) / 100f);
                if (Random.value < chance)
                {
                    playerMoveDirection = Vector2.zero;
                }
            }
        }
        animator.SetFloat("moveX", playerMoveDirection.x);
        animator.SetFloat("moveY", playerMoveDirection.y);

        if (playerMoveDirection == Vector2.zero)
        {
            animator.SetBool("moving", false);
        }
        else
        {
            animator.SetBool("moving", true);
        }
    }

    void FixedUpdate()
    {
        float speedMult = 1f;
        PlayerStats stats = GetComponent<PlayerStats>();
        if (stats != null)
        {
            speedMult = Mathf.Max(0f, stats.moveSpeedMultiplier);
        }

        StatusController statusController = GetComponent<StatusController>();
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

        rb.velocity = new Vector2(playerMoveDirection.x * movespeed * speedMult,
            playerMoveDirection.y * movespeed * speedMult);
    }

    void FireProjectile()
    {
        if (isDead || projectilePrefab == null || playerMana == null) return;

        Transform spawnFirePoint = null;
        AdvancedPlayerController advanced = GetComponent<AdvancedPlayerController>();
        if (advanced != null)
        {
            spawnFirePoint = advanced.ActiveFirePoint;
        }
        if (spawnFirePoint == null)
        {
            spawnFirePoint = firePoint != null ? firePoint : transform;
        }

        // Use ray-plane intersection for perfect aiming
        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane gamePlane = new Plane(Vector3.forward, spawnFirePoint.position.z);

        if (gamePlane.Raycast(ray, out float enter))
        {
            Vector3 worldTouchPosition = ray.GetPoint(enter);
            Vector2 fireDirection = (worldTouchPosition - spawnFirePoint.position).normalized;

            var projectile = Instantiate(projectilePrefab, spawnFirePoint.position, Quaternion.identity);
            projectile.Launch(fireDirection, null, playerMana);

            // Debug visualization
            Debug.DrawRay(spawnFirePoint.position, fireDirection * 5f, Color.magenta, 2f);
        }
    }

    void HandlePlayerDeath()
    {
        Debug.Log("Player died!");
        isDead = true;
        rb.velocity = Vector2.zero;
        enabled = false;

        // Start the restart coroutine
        StartCoroutine(WaitForRestart());
    }

    private IEnumerator WaitForRestart()
    {
        // Wait 1 second after death
        yield return new WaitForSeconds(1f);

        // Wait for screen press (mouse click or touch)
        while (!Input.GetMouseButtonDown(0) && Input.touchCount == 0)
        {
            yield return null;
        }

        // Reload the current scene
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
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.OnDeath -= HandlePlayerDeath;
        }
        fire.performed -= ctx => FireProjectile();
    }
}