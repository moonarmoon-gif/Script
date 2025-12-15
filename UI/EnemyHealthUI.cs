using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Health UI that follows an enemy and displays their health bar
/// </summary>
public class EnemyHealthUI : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Enable/disable this enemy's health UI")]
    [SerializeField] private bool showHealthUI = true;
    
    [Header("References")]
    [SerializeField] private EnemyHealth enemyHealth;
    [SerializeField] private Transform enemyTransform;
    private Camera mainCamera;
    private Vector3 baseScale = new Vector3(0.01f, 0.01f, 0.01f); // Store the base scale from inspector
    
    [Header("UI Elements")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private Image fillImage;
    [SerializeField] private GameObject healthBarContainer;

    [Header("Position Settings")]
    [Tooltip("Offset from enemy position (X, Y)")]
    [SerializeField] private Vector2 offset = new Vector2(0f, 1.5f);
    
    [Header("Colors")]
    [SerializeField] private Color highHealthColor = Color.green;
    [SerializeField] private Color mediumHealthColor = Color.yellow;
    [SerializeField] private Color lowHealthColor = Color.red;
    [SerializeField] private float mediumHealthThreshold = 0.5f;
    [SerializeField] private float lowHealthThreshold = 0.25f;

    private void Awake()
    {
        mainCamera = Camera.main;
        
        // Auto-find references if not assigned
        if (enemyHealth == null)
        {
            enemyHealth = GetComponentInParent<EnemyHealth>();
        }
        
        if (enemyTransform == null)
        {
            enemyTransform = enemyHealth != null ? enemyHealth.transform : transform.parent;
        }
        
        // Setup canvas
        if (canvas == null)
        {
            canvas = GetComponent<Canvas>();
        }
        
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = mainCamera;
            
            // CRITICAL: Disable perspective scaling - keeps constant size regardless of distance
            canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 20);
            
            // CRITICAL: Always respect the scale set in inspector
            // Store the base scale for later use WITHOUT modifying it
            if (transform.localScale.x > 0.0001f && transform.localScale.y > 0.0001f) // Ensure it's not zero or near-zero
            {
                baseScale = transform.localScale;
                Debug.Log($"<color=cyan>EnemyHealthUI: Using scale from inspector: X={baseScale.x}, Y={baseScale.y}, Z={baseScale.z}</color>");
            }
            else
            {
                // Fallback if scale is zero or invalid
                baseScale = new Vector3(0.01f, 0.01f, 0.01f);
                transform.localScale = baseScale;
                Debug.LogWarning($"<color=yellow>EnemyHealthUI: Scale was zero/invalid, set to default: {baseScale}</color>");
            }
            
            // CRITICAL: Disable ALL raycasting to prevent blocking move tool in editor
            UnityEngine.UI.GraphicRaycaster raycaster = canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            if (raycaster != null)
            {
                Destroy(raycaster); // Remove it completely
                Debug.Log("<color=cyan>EnemyHealthUI: Removed GraphicRaycaster to allow move tool</color>");
            }
            
            // CRITICAL: Set canvas to ignore raycast layer
            int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
            if (ignoreRaycastLayer != -1)
            {
                gameObject.layer = ignoreRaycastLayer;
                
                // Set all children to ignore raycast too
                foreach (Transform child in transform.GetComponentsInChildren<Transform>(true))
                {
                    child.gameObject.layer = ignoreRaycastLayer;
                }
                
                Debug.Log("<color=cyan>EnemyHealthUI: Set canvas and children to Ignore Raycast layer</color>");
            }
            else
            {
                Debug.LogWarning("<color=yellow>EnemyHealthUI: 'Ignore Raycast' layer not found! Move tool may not work.</color>");
            }
        }
    }

    private void OnEnable()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnHealthChanged += Refresh;
            enemyHealth.OnDeath += OnEnemyDeath;
            InitializeUI();
        }
        else
        {
            Debug.LogWarning("EnemyHealthUI: No EnemyHealth source assigned!");
        }
        
        UpdateVisibility();
    }

    private void OnDisable()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnHealthChanged -= Refresh;
            enemyHealth.OnDeath -= OnEnemyDeath;
        }
    }

    private void Start()
    {
        InitializeUI();
        UpdateVisibility();
    }

    private void LateUpdate()
    {
        // Follow enemy position with offset
        if (enemyTransform != null && mainCamera != null)
        {
            Vector3 worldPos = enemyTransform.position + (Vector3)offset;
            transform.position = worldPos;
            
            // CRITICAL: Make health bar face camera WITHOUT inheriting enemy's rotation
            // Lock rotation to prevent scaling issues
            transform.rotation = Quaternion.identity;
            
            // CRITICAL: Maintain constant size - use stored baseScale
            // Do NOT apply distance-based scaling - keep it constant
            transform.localScale = new Vector3(
                Mathf.Abs(baseScale.x),
                Mathf.Abs(baseScale.y),
                Mathf.Abs(baseScale.z)
            );
        }
    }

    private void InitializeUI()
    {
        if (enemyHealth != null)
        {
            Refresh(enemyHealth.CurrentHealth, enemyHealth.MaxHealth);
        }
    }

    private void Refresh(float currentHealth, float maxHealth)
    {
        if (maxHealth <= 0) return;

        // Update slider
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }

        // Update text (optional - can be disabled for cleaner look)
        if (healthText != null)
        {
            healthText.text = $"{Mathf.CeilToInt(currentHealth)}/{Mathf.CeilToInt(maxHealth)}";
        }

        // Update color based on health percentage
        if (fillImage != null)
        {
            float healthPercent = currentHealth / maxHealth;

            if (healthPercent <= lowHealthThreshold)
            {
                fillImage.color = lowHealthColor;
            }
            else if (healthPercent <= mediumHealthThreshold)
            {
                fillImage.color = mediumHealthColor;
            }
            else
            {
                fillImage.color = highHealthColor;
            }
        }
    }

    private void OnEnemyDeath()
    {
        // Hide health bar when enemy dies
        if (healthBarContainer != null)
        {
            healthBarContainer.SetActive(false);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void UpdateVisibility()
    {
        if (healthBarContainer != null)
        {
            healthBarContainer.SetActive(showHealthUI);
        }
        else
        {
            gameObject.SetActive(showHealthUI);
        }
    }

    /// <summary>
    /// Enable or disable the health UI at runtime
    /// </summary>
    public void SetHealthUIEnabled(bool enabled)
    {
        showHealthUI = enabled;
        UpdateVisibility();
    }
}
