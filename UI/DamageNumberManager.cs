using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Singleton manager for spawning damage numbers
/// </summary>
public class DamageNumberManager : MonoBehaviour
{
    public static DamageNumberManager Instance { get; private set; }

    [Header("Prefab")]
    [SerializeField] private GameObject damageNumberPrefab;

    [Header("Canvas")]
    [SerializeField] private Canvas damageCanvas;
    [Tooltip("The sorting layer name for damage numbers (should be below UI)")]
    [SerializeField] private string sortingLayerName = "UI";

    [Header("Sorting")]
    [Tooltip("Sorting order for damage numbers (should be lower than UI popups)")]
    [SerializeField] private int sortingOrder = 0;

    [Header("Damage Colors")]
    [SerializeField] private Color fireDamageColor = new Color(1f, 0.5f, 0f);
    [SerializeField] private Color iceDamageColor = new Color(0.5f, 0.8f, 1f);
    [SerializeField] private Color thunderDamageColor = new Color(1f, 1f, 0f);
    [SerializeField] private Color playerDamageColor = Color.red;
    [SerializeField] private Color shieldDamageColor = new Color(0.6f, 0.9f, 1f);
    [SerializeField] private Color poisonDamageColor = new Color(0.5f, 1f, 0.5f);
    [SerializeField] private Color thornDamageColor = new Color(0.6f, 1f, 0.6f);
    [SerializeField] private Color reflectDamageColor = new Color(1f, 1f, 0.6f);
    [SerializeField] private Color woundDamageColor = new Color(1f, 0.7f, 0.4f);

    [Header("Status Popup Colors")]
    [SerializeField] private Color immuneStatusColor = Color.red;
    [SerializeField] private Color burnStatusColor = new Color(1f, 0.4f, 0f);
    [SerializeField] private Color slowStatusColor = new Color(0.4f, 0.8f, 1f);
    [SerializeField] private Color executeStatusColor = Color.red;
    [SerializeField] private Color poisonStatusColor = new Color(0.5f, 1f, 0.5f);
    [SerializeField] private Color bleedStatusColor = new Color(0.9f, 0.1f, 0.1f);
    [SerializeField] private Color woundStatusColor = new Color(1f, 0.6f, 0.2f);
    [SerializeField] private Color nullifyStatusColor = new Color(0.6f, 0.9f, 1f);
    [SerializeField] private Color reflectStatusColor = new Color(1f, 1f, 0.6f);

    [Header("Status Popup Offsets (world space)")]
    [SerializeField] private Vector3 immuneStatusOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private Vector3 burnStatusOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private Vector3 slowStatusOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private Vector3 executeStatusOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private Vector3 poisonStatusOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private Vector3 bleedStatusOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private Vector3 woundStatusOffset = new Vector3(0f, 1.5f, 0f);
    [SerializeField] private Vector3 weakStatusOffset = new Vector3(0f, 1.5f, 0f);
    
    [Header("Wound Damage Number Offset (world space)")]
    [SerializeField] private Vector3 woundDamageNumberOffset = new Vector3(0f, 1.5f, 0f);
    
    [Header("Damage Number Settings")]
    [Tooltip("Base font size for normal damage numbers")]
    [SerializeField] private float damageNumberFontSize = 36f;
    [Tooltip("Font size for CRITICAL damage numbers (0 = use base font size)")]
    [SerializeField] private float criticalFontSize = 0f;
    [Tooltip("Font size for BURN damage numbers (0 = use base font size)")]
    [SerializeField] private float statusDamageNumberFontSize = 0f;
    [Tooltip("Font size for generic status popups like Burn/Slow/etc. (0 = use base font size)")]
    [SerializeField] private float statusFontSize = 0f;
    [Tooltip("Optional override font size for the 'Executed' status text (0 = use StatusFontSize)")]
    [SerializeField] private float executeFontSize = 0f;
    [Tooltip("Optional override font size for the 'Immune' status text (0 = use StatusFontSize)")]
    [SerializeField] private float immuneFontSize = 14f;
    [Tooltip("Optional override font size for the 'Nullify' status text (0 = use StatusFontSize)")]
    [SerializeField] private float nullifyFontSize = 14f;
    [Tooltip("Optional override font size for the 'Reflect' status text (0 = use StatusFontSize)")]
    [SerializeField] private float reflectFontSize = 14f;
    
    [Tooltip("Speed at which damage numbers float upward")]
    [SerializeField] private float floatSpeed = 2f;
    
    [Tooltip("How long damage numbers stay visible")]
    [SerializeField] private float duration = 1.5f;
    
    [Tooltip("Horizontal spread range for damage numbers")]
    [SerializeField] private float horizontalSpread = 0.5f;
    
    [Tooltip("Scale multiplier for critical hits")]
    [SerializeField] private float criticalSizeMultiplier = 1.5f;
    
    [Tooltip("Enable outline on damage numbers")]
    [SerializeField] private bool enableOutline = true;
    
    [Tooltip("Outline color")]
    [SerializeField] private Color outlineColor = Color.black;
    
    [Tooltip("Outline width")]
    [Range(0f, 1f)]
    [SerializeField] private float outlineWidth = 0.2f;
    
    [Tooltip("Fade out animation curve")]
    [SerializeField] private AnimationCurve fadeOutCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
    
    [Tooltip("Scale animation curve")]
    [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0, 0.5f, 1, 1);
    
    [Header("Performance")]
    [Tooltip("Maximum number of damage numbers that can be spawned per frame. 0 or negative = unlimited.")]
    public int DamageNumbersPerFrame = 60;

    [Tooltip("Maximum number of Thunderbird V2 strike hits processed per frame. 0 or negative = unlimited.")]
    public int ThunderbirdV2MaxStrikesPerFrame = 0;

    [Header("Camera Scaling")]
    [Tooltip("If true, damage number font size will be scaled inversely with Camera.main.orthographicSize so they keep a consistent apparent size as the camera zooms.")]
    [SerializeField] private bool scaleWithCameraSize = true;

    // Cached reference camera size used as the "baseline" for damage-number
    // sizing. When scaleWithCameraSize is enabled, all damage-number font
    // sizes are multiplied by (referenceCameraSize / currentCameraSize).
    private float referenceCameraSize = 0f;

    private struct PendingDamageNumber
    {
        public float damage;
        public Vector3 worldPosition;
        public DamageType damageType;
        public bool isCrit;
        public bool isBurn;
    }

    private readonly Queue<PendingDamageNumber> pendingDamageNumbers = new Queue<PendingDamageNumber>();

    // Public accessors
    public float DamageNumberFontSize => damageNumberFontSize;
    public float CriticalFontSize => criticalFontSize > 0f ? criticalFontSize : damageNumberFontSize;
    public float StatusDamageNumberFontSize => statusDamageNumberFontSize > 0f ? statusDamageNumberFontSize : damageNumberFontSize;
    public float StatusFontSize => statusFontSize > 0f ? statusFontSize : damageNumberFontSize;
    public float ExecuteFontSize => executeFontSize > 0f ? executeFontSize : StatusFontSize;
    public float ImmuneFontSize => immuneFontSize > 0f ? immuneFontSize : StatusFontSize;
    public float NullifyFontSize => nullifyFontSize > 0f ? nullifyFontSize : StatusFontSize;
    public float ReflectFontSize => reflectFontSize > 0f ? reflectFontSize : StatusFontSize;
    public float FloatSpeed => floatSpeed;
    public float Duration => duration;

    public Vector3 GetAnchorWorldPosition(GameObject target, Vector3 fallback)
    {
        if (target == null)
        {
            return fallback;
        }

        Collider2D best = null;
        float bestArea = -1f;

        Collider2D[] childColliders = target.GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < childColliders.Length; i++)
        {
            Collider2D c = childColliders[i];
            if (c == null || !c.enabled || !c.gameObject.activeInHierarchy)
            {
                continue;
            }
            if (c.isTrigger)
            {
                continue;
            }

            Vector3 size = c.bounds.size;
            float area = size.x * size.y;
            if (area > bestArea)
            {
                best = c;
                bestArea = area;
            }
        }

        if (best == null)
        {
            Collider2D[] parentColliders = target.GetComponentsInParent<Collider2D>();
            for (int i = 0; i < parentColliders.Length; i++)
            {
                Collider2D c = parentColliders[i];
                if (c == null || !c.enabled || !c.gameObject.activeInHierarchy)
                {
                    continue;
                }
                if (c.isTrigger)
                {
                    continue;
                }

                Vector3 size = c.bounds.size;
                float area = size.x * size.y;
                if (area > bestArea)
                {
                    best = c;
                    bestArea = area;
                }
            }
        }

        if (best != null)
        {
            return best.bounds.center;
        }

        best = null;
        bestArea = -1f;

        for (int i = 0; i < childColliders.Length; i++)
        {
            Collider2D c = childColliders[i];
            if (c == null || !c.enabled || !c.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector3 size = c.bounds.size;
            float area = size.x * size.y;
            if (area > bestArea)
            {
                best = c;
                bestArea = area;
            }
        }

        if (best == null)
        {
            Collider2D[] parentColliders = target.GetComponentsInParent<Collider2D>();
            for (int i = 0; i < parentColliders.Length; i++)
            {
                Collider2D c = parentColliders[i];
                if (c == null || !c.enabled || !c.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector3 size = c.bounds.size;
                float area = size.x * size.y;
                if (area > bestArea)
                {
                    best = c;
                    bestArea = area;
                }
            }
        }

        if (best != null)
        {
            return best.bounds.center;
        }

        SpriteRenderer sr = target.GetComponent<SpriteRenderer>() ?? target.GetComponentInChildren<SpriteRenderer>() ?? target.GetComponentInParent<SpriteRenderer>();
        if (sr != null)
        {
            return sr.bounds.center;
        }

        return target.transform.position;
    }
    public float HorizontalSpread => horizontalSpread;
    public float CriticalSizeMultiplier => criticalSizeMultiplier;
    public bool EnableOutline => enableOutline;
    public Color OutlineColor => outlineColor;
    public float OutlineWidth => outlineWidth;
    public AnimationCurve FadeOutCurve => fadeOutCurve;
    public AnimationCurve ScaleCurve => scaleCurve;
    public bool ScaleWithCameraSize => scaleWithCameraSize;

    public enum DamageType
    {
        Fire,
        Ice,
        Thunder,
        Player,
        Shield,
        Poison,
        Thorn,
        Reflect,
        Wound
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Create a dedicated canvas if not assigned
        if (damageCanvas == null)
        {
            // Try to find an existing DamageNumbers canvas
            GameObject canvasGO = GameObject.Find("DamageNumbersCanvas");

            if (canvasGO == null)
            {
                // Create a new canvas for damage numbers
                canvasGO = new GameObject("DamageNumbersCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                damageCanvas = canvasGO.GetComponent<Canvas>();

                // Set up the canvas
                damageCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                damageCanvas.pixelPerfect = false;
                damageCanvas.sortingLayerName = sortingLayerName;
                damageCanvas.sortingOrder = sortingOrder;

                // Make it persistent
                DontDestroyOnLoad(canvasGO);

                Debug.Log("<color=cyan>Created new DamageNumbers canvas</color>");
            }
            else
            {
                damageCanvas = canvasGO.GetComponent<Canvas>();
            }
        }

        // Ensure the canvas is properly configured
        if (damageCanvas != null)
        {
            // Set sorting properties
            damageCanvas.sortingLayerName = sortingLayerName;
            damageCanvas.sortingOrder = sortingOrder;
            damageCanvas.overrideSorting = true; // This ensures our sorting order is respected

            Debug.Log($"<color=cyan>DamageNumberManager: Canvas '{damageCanvas.name}' - Layer: {damageCanvas.sortingLayerName}, Order: {damageCanvas.sortingOrder}</color>");
        }
        else
        {
            Debug.LogError("<color=red>Failed to initialize DamageNumberManager canvas!</color>");
        }

        // Cache a baseline orthographic size so that damage numbers can be
        // scaled relative to the initial camera zoom level.
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            referenceCameraSize = mainCam.orthographicSize;
        }
    }

    /// <summary>
    /// Get the scale factor that should be applied to damage-number font sizes
    /// based on the current camera zoom. 1.0 means no change; values below 1
    /// will shrink the numbers when the camera zooms out.
    /// </summary>
    public float GetCameraFontScale()
    {
        if (!scaleWithCameraSize)
        {
            return 1f;
        }

        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            return 1f;
        }

        if (referenceCameraSize <= 0f)
        {
            referenceCameraSize = mainCam.orthographicSize;
        }

        if (mainCam.orthographicSize <= 0f)
        {
            return 1f;
        }

        return referenceCameraSize / mainCam.orthographicSize;
    }

    private void Update()
    {
        int maxPerFrame = DamageNumbersPerFrame <= 0 ? int.MaxValue : DamageNumbersPerFrame;
        ProcessPendingDamageNumbers(maxPerFrame);
    }

    private void EnqueueDamageNumber(float damage, Vector3 worldPosition, DamageType damageType, bool isCrit, bool isBurn)
    {
        PendingDamageNumber entry;
        entry.damage = damage;
        entry.worldPosition = worldPosition;
        entry.damageType = damageType;
        entry.isCrit = isCrit;
        entry.isBurn = isBurn;
        pendingDamageNumbers.Enqueue(entry);
    }

    private void ProcessPendingDamageNumbers(int maxToProcess)
    {
        int processed = 0;
        while (processed < maxToProcess && pendingDamageNumbers.Count > 0)
        {
            PendingDamageNumber entry = pendingDamageNumbers.Dequeue();
            SpawnDamageNumberNow(entry.damage, entry.worldPosition, entry.damageType, entry.isCrit, entry.isBurn);
            processed++;
        }
    }

    private void SpawnDamageNumberNow(float damage, Vector3 worldPosition, DamageType damageType, bool isCrit, bool isBurn)
    {
        if (damageNumberPrefab == null || damageCanvas == null)
        {
            Debug.LogWarning("DamageNumberManager: Missing prefab or canvas!");
            return;
        }

        // Get color based on damage type
        Color color = GetColorForDamageType(damageType);

        // Instantiate damage number
        GameObject damageObj = Instantiate(damageNumberPrefab, damageCanvas.transform);

        if (!damageObj.activeSelf)
        {
            damageObj.SetActive(true);
        }
        DamageNumber damageNumber = damageObj.GetComponent<DamageNumber>();

        if (damageNumber != null)
        {
            if (isCrit)
            {
                damageNumber.InitializeCrit(damage, color, worldPosition);
            }
            else if (isBurn)
            {
                damageNumber.InitializeBurn(damage, color, worldPosition);
            }
            else
            {
                damageNumber.Initialize(damage, color, worldPosition);
            }
        }
        else
        {
            Debug.LogError("DamageNumberManager: Prefab missing DamageNumber component!");
            Destroy(damageObj);
        }
    }

    /// <summary>
    /// Spawn a damage number at the given world position.
    /// This now enqueues the request so that a per-frame cap can be applied
    /// when actually instantiating the UI objects.
    /// </summary>
    public void ShowDamage(float damage, Vector3 worldPosition, DamageType damageType, bool isCrit = false, bool isBurn = false)
    {
        // Auto-detect crits for projectile-style damage when caller has not
        // explicitly specified crit/burn flags.
        if (!isBurn && !isCrit && damageType != DamageType.Player && damageType != DamageType.Shield)
        {
            PlayerStats stats = Object.FindObjectOfType<PlayerStats>();
            if (stats != null && stats.lastHitWasCrit)
            {
                isCrit = true;
            }
        }

        EnqueueDamageNumber(damage, worldPosition, damageType, isCrit, isBurn);
    }

    private Color GetColorForDamageType(DamageType damageType)
    {
        switch (damageType)
        {
            case DamageType.Fire:
                return fireDamageColor;
            case DamageType.Ice:
                return iceDamageColor;
            case DamageType.Thunder:
                return thunderDamageColor;
            case DamageType.Player:
                return playerDamageColor;
            case DamageType.Shield:
                return shieldDamageColor;
            case DamageType.Poison:
                return poisonDamageColor;
            case DamageType.Thorn:
                return thornDamageColor;
            case DamageType.Reflect:
                return reflectDamageColor;
            case DamageType.Wound:
                return woundDamageColor;
            default:
                return Color.white;
        }
    }

    /// <summary>
    /// Set custom colors at runtime
    /// </summary>
    public void SetDamageColor(DamageType damageType, Color color)
    {
        switch (damageType)
        {
            case DamageType.Fire:
                fireDamageColor = color;
                break;
            case DamageType.Ice:
                iceDamageColor = color;
                break;
            case DamageType.Thunder:
                thunderDamageColor = color;
                break;
            case DamageType.Player:
                playerDamageColor = color;
                break;
            case DamageType.Shield:
                shieldDamageColor = color;
                break;
            case DamageType.Poison:
                poisonDamageColor = color;
                break;
            case DamageType.Thorn:
                thornDamageColor = color;
                break;
            case DamageType.Reflect:
                reflectDamageColor = color;
                break;
            case DamageType.Wound:
                woundDamageColor = color;
                break;
        }
    }

    public void ShowImmune(Vector3 worldPosition)
    {
        ShowStatusInternal("Immune", worldPosition + immuneStatusOffset, immuneStatusColor);
    }

    public void ShowBurn(Vector3 worldPosition)
    {
        ShowStatusInternal("Burn", worldPosition + burnStatusOffset, burnStatusColor);
    }

    public void ShowSlow(Vector3 worldPosition)
    {
        ShowStatusInternal("Slow", worldPosition + slowStatusOffset, slowStatusColor);
    }

    public void ShowExecuted(Vector3 worldPosition)
    {
        ShowStatusInternal("Executed", worldPosition + executeStatusOffset, executeStatusColor);
    }

    public void ShowPoison(Vector3 worldPosition)
    {
        ShowStatusInternal("Poison", worldPosition + poisonStatusOffset, poisonStatusColor);
    }

    public void ShowBleed(Vector3 worldPosition)
    {
        ShowStatusInternal("Bleed", worldPosition + bleedStatusOffset, bleedStatusColor);
    }

    public void ShowWound(Vector3 worldPosition)
    {
        ShowStatusInternal("Wound", worldPosition + woundStatusOffset, woundStatusColor);
    }

    public void ShowWeak(Vector3 worldPosition)
    {
        ShowStatusInternal("Weak", worldPosition + weakStatusOffset, woundStatusColor);
    }

    public void ShowNullify(Vector3 worldPosition)
    {
        ShowStatusInternal("Nullify", worldPosition + immuneStatusOffset, nullifyStatusColor);
    }

    public void ShowReflect(Vector3 worldPosition)
    {
        ShowStatusInternal("Reflect", worldPosition + immuneStatusOffset, reflectStatusColor);
    }

    private void ShowStatusInternal(string text, Vector3 worldPosition, Color color)
    {
        if (damageNumberPrefab == null || damageCanvas == null)
        {
            return;
        }

        GameObject damageObj = Instantiate(damageNumberPrefab, damageCanvas.transform);
        DamageNumber damageNumber = damageObj.GetComponent<DamageNumber>();

        if (damageNumber != null)
        {
            damageNumber.InitializeStatus(text, color, worldPosition);
        }
        else
        {
            Destroy(damageObj);
        }
    }
}
