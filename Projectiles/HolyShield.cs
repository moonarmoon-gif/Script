using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class HolyShield : MonoBehaviour, IDamageable
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;

    [Header("Spawn Offset")]
    [SerializeField] private Vector2 spawnOffset = Vector2.zero;

    [Header("Regeneration")]
    [SerializeField] private float regenDelay = 5f;
    [SerializeField, Tooltip("Percent of current max shield health regenerated per second (5 = 5% per second).")]
    private float regenPerSecond = 5f;

    [Header("Respawn")]
    [SerializeField] private float respawnDelay = 30f;

    [Header("Fade Out")]
    [SerializeField] private float fadeOutDuration = 1f;

    [Header("Fade In")]
    [SerializeField] private float fadeInDuration = 0.5f;

    [SerializeField] private GameObject reflectShieldPrefab;
    [SerializeField] private GameObject nullifyShieldPrefab;
    [SerializeField] private Animator animator;

    private float currentHealth;
    private float lastDamageTime;
    private bool isBroken;
    private SpriteRenderer[] spriteRenderers;
    private Collider2D shieldCollider;
    private Collider2D ownerCollider;
    private float baseMaxHealth;
    private float baseRespawnDelay;
    private Vector3 baseScale;
    private Color[] originalColors;

    private int activeVariantMask;

    private static HolyShield activeShield;
    private static float lastDestroyedTime = -1f;
    private static bool hasEverSpawned = false;

    public static HolyShield ActiveShield => activeShield;
    public static bool HasEverSpawned => hasEverSpawned;
    public bool IsAlive => !isBroken && currentHealth > 0f;

    private void Awake()
    {
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
        shieldCollider = GetComponent<Collider2D>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        baseMaxHealth = maxHealth;
        baseRespawnDelay = respawnDelay;
        baseScale = transform.localScale;
        currentHealth = maxHealth;

        if (spriteRenderers != null && spriteRenderers.Length > 0)
        {
            originalColors = new Color[spriteRenderers.Length];
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] != null)
                {
                    originalColors[i] = spriteRenderers[i].color;
                }
            }
        }
    }

    public void Initialize(Vector3 spawnPosition, Collider2D playerCollider, bool skipCooldownCheck = false)
    {
        ProjectileCards card = ProjectileCardModifiers.Instance != null
            ? ProjectileCardModifiers.Instance.GetCardFromProjectile(gameObject)
            : null;

        CardModifierStats modifiers = new CardModifierStats();
        if (card != null && ProjectileCardModifiers.Instance != null)
        {
            modifiers = ProjectileCardModifiers.Instance.GetCardModifiers(card);
        }

        float oldMax = maxHealth;
        float oldCurrent = currentHealth;
        float oldRatio = oldMax > 0f ? Mathf.Clamp01(oldCurrent / oldMax) : 1f;

        float healthMult = modifiers.damageMultiplier;
        if (healthMult <= 0f)
        {
            healthMult = 1f;
        }
        maxHealth = baseMaxHealth * healthMult;

        if (!hasEverSpawned)
        {
            currentHealth = maxHealth;
        }
        else
        {
            currentHealth = Mathf.Max(1f, maxHealth * oldRatio);
        }

        // Apply flat Shield Health bonus from projectile modifiers (only relevant for HolyShield)
        if (modifiers.shieldHealthBonus != 0f)
        {
            maxHealth += modifiers.shieldHealthBonus;
            currentHealth = Mathf.Min(maxHealth, currentHealth + modifiers.shieldHealthBonus);
        }

        respawnDelay = Mathf.Max(0.1f, baseRespawnDelay * (1f - modifiers.cooldownReductionPercent / 100f));
        if (MinCooldownManager.Instance != null)
        {
            respawnDelay = MinCooldownManager.Instance.ClampCooldown(card, Mathf.Max(0.1f, baseRespawnDelay * (1f - modifiers.cooldownReductionPercent / 100f)));
        }

        if (modifiers.sizeMultiplier != 1f)
        {
            transform.localScale = baseScale * modifiers.sizeMultiplier;
        }

        Vector3 targetPos = spawnPosition + (Vector3)spawnOffset;
        transform.position = targetPos;

        if (playerCollider != null)
        {
            transform.SetParent(playerCollider.transform);
        }

        ownerCollider = playerCollider;

        activeVariantMask = 0;
        if (card != null && ProjectileCardLevelSystem.Instance != null)
        {
            int enhancedVariant = ProjectileCardLevelSystem.Instance.GetEnhancedVariant(card);

            int level = ProjectileCardLevelSystem.Instance.GetLevel(card);
            if (level < 1)
            {
                level = 1;
            }

            ApplyVariantFromIndex(enhancedVariant, level);
        }

        isBroken = false;
        lastDamageTime = Time.time;
        activeShield = this;

        hasEverSpawned = true;

        if (fadeInDuration > 0f && spriteRenderers != null && spriteRenderers.Length > 0)
        {
            StartCoroutine(FadeIn());
        }

        if (spriteRenderers != null && spriteRenderers.Length > 0)
        {
            // No variant visuals remain on HolyShield; ReflectShield/NullifyShield
            // handle their own visuals independently.
        }
    }

    /// <summary>
    /// Apply a selected variant index (0 = base, 1 = Reflect, 2 = Nullify)
    /// to this active shield instance at runtime. This is invoked when the
    /// player picks a HolyShield variant in the UI so that the already-
    /// spawned shield upgrades immediately without needing a new spawn.
    /// </summary>
    public void ApplyVariantFromIndex(int variantIndex)
    {
        int level = 1;
        ProjectileCards card = ProjectileCardModifiers.Instance != null
            ? ProjectileCardModifiers.Instance.GetCardFromProjectile(gameObject)
            : null;
        if (card != null && ProjectileCardLevelSystem.Instance != null)
        {
            level = Mathf.Max(1, ProjectileCardLevelSystem.Instance.GetLevel(card));
        }

        ApplyVariantFromIndex(variantIndex, level);
    }

    private void ApplyVariantFromIndex(int variantIndex, int level)
    {
        int newMask = Mathf.Max(0, variantIndex);
        bool wantsReflect = (newMask & 1) != 0;
        bool wantsNullify = (newMask & 2) != 0;

        bool hadReflect = (activeVariantMask & 1) != 0;
        bool hadNullify = (activeVariantMask & 2) != 0;

        if (wantsReflect)
        {
            EnsureReflectShield(level, hadReflect);
        }
        else
        {
            DestroyReflectShield();
        }

        if (wantsNullify)
        {
            EnsureNullifyShield(level, hadNullify);
        }
        else
        {
            DestroyNullifyShield();
        }

        activeVariantMask = newMask;
    }

    private void EnsureReflectShield(int level, bool alreadyHad)
    {
        if (reflectShieldPrefab == null)
        {
            return;
        }

        if (ReflectShield.ActiveShield == null)
        {
            GameObject obj = Instantiate(reflectShieldPrefab, transform.position, Quaternion.identity);
            ReflectShield shield = obj.GetComponent<ReflectShield>();
            if (shield != null)
            {
                shield.Initialize(transform.position, ownerCollider, Mathf.Max(1, level));
            }
            return;
        }

        if (!alreadyHad || !ReflectShield.ActiveShield.IsAlive)
        {
            ReflectShield.ActiveShield.Initialize(transform.position, ownerCollider, Mathf.Max(1, level));
        }
        else
        {
            ReflectShield.ActiveShield.SetMaxCharges(Mathf.Max(1, level));
        }
    }

    private void EnsureNullifyShield(int level, bool alreadyHad)
    {
        if (nullifyShieldPrefab == null)
        {
            return;
        }

        if (NullifyShield.ActiveShield == null)
        {
            GameObject obj = Instantiate(nullifyShieldPrefab, transform.position, Quaternion.identity);
            NullifyShield shield = obj.GetComponent<NullifyShield>();
            if (shield != null)
            {
                shield.Initialize(transform.position, ownerCollider, Mathf.Max(1, level));
            }
            return;
        }

        if (!alreadyHad || !NullifyShield.ActiveShield.IsAlive)
        {
            NullifyShield.ActiveShield.Initialize(transform.position, ownerCollider, Mathf.Max(1, level));
        }
        else
        {
            NullifyShield.ActiveShield.SetMaxCharges(Mathf.Max(1, level));
        }
    }

    private void DestroyReflectShield()
    {
        if (ReflectShield.ActiveShield != null)
        {
            Destroy(ReflectShield.ActiveShield.gameObject);
        }
    }

    private void DestroyNullifyShield()
    {
        if (NullifyShield.ActiveShield != null)
        {
            Destroy(NullifyShield.ActiveShield.gameObject);
        }
    }

    public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (isBroken || amount <= 0f)
        {
            return;
        }

        float finalAmount = amount;

        if (StatusControllerManager.Instance != null)
        {
            PlayerHealth ownerHealth = GetComponentInParent<PlayerHealth>();
            if (ownerHealth != null)
            {
                StatusController statusController = ownerHealth.GetComponent<StatusController>();
                if (statusController != null)
                {
                    int strengthStacks = statusController.GetStacks(StatusId.ShieldStrength);
                    if (strengthStacks > 0)
                    {
                        float perStack = StatusControllerManager.Instance.ShieldStrengthDamageReductionPercentPerStack;
                        float total = Mathf.Max(0f, perStack * strengthStacks);
                        float mul = Mathf.Max(0f, 1f - total / 100f);
                        finalAmount *= mul;
                    }

                    int shatterStacks = statusController.GetStacks(StatusId.Shatter);
                    if (shatterStacks > 0)
                    {
                        float bonus = StatusControllerManager.Instance.ShatterShieldBonusPercent;
                        finalAmount *= 1f + Mathf.Max(0f, bonus) / 100f;
                        statusController.ConsumeStacks(StatusId.Shatter, 1);
                    }
                }
            }
        }

        lastDamageTime = Time.time;
        currentHealth = Mathf.Max(0f, currentHealth - finalAmount);

        if (DamageNumberManager.Instance != null)
        {
            DamageNumberManager.Instance.ShowDamage(finalAmount, hitPoint, DamageNumberManager.DamageType.Shield);
        }

        if (currentHealth <= 0f)
        {
            BreakShield();
        }
    }

    private void Update()
    {
        if (isBroken)
        {
            return;
        }

        if (currentHealth < maxHealth && Time.time - lastDamageTime >= regenDelay)
        {
            if (maxHealth > 0f && regenPerSecond > 0f)
            {
                // regenPerSecond is treated as a percentage value (5 = 5% of maxHealth per second)
                float healFractionPerSecond = regenPerSecond / 100f;
                float heal = maxHealth * healFractionPerSecond * Time.deltaTime;
                currentHealth = Mathf.Min(maxHealth, currentHealth + heal);
            }
        }

    }

    private void BreakShield()
    {
        if (isBroken)
        {
            return;
        }

        isBroken = true;
        lastDestroyedTime = Time.time;
        if (activeShield == this)
        {
            activeShield = null;
        }

        if (shieldCollider != null)
        {
            shieldCollider.enabled = false;
        }

        if (spriteRenderers != null && spriteRenderers.Length > 0)
        {
            if (fadeOutDuration > 0f)
            {
                StartCoroutine(FadeAndDestroy());
            }
            else
            {
                int count = spriteRenderers.Length;
                for (int i = 0; i < count; i++)
                {
                    if (spriteRenderers[i] != null)
                    {
                        Color c = spriteRenderers[i].color;
                        c.a = 0f;
                        spriteRenderers[i].color = c;
                    }
                }
            }
        }

        StartCoroutine(RespawnRoutine());
    }

    private System.Collections.IEnumerator FadeAndDestroy()
    {
        float elapsed = 0f;
        int count = spriteRenderers.Length;
        Color[] startColors = new Color[count];
        for (int i = 0; i < count; i++)
        {
            if (spriteRenderers[i] != null)
            {
                startColors[i] = spriteRenderers[i].color;
            }
        }

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeOutDuration);
            float alphaMul = 1f - t;

            for (int i = 0; i < count; i++)
            {
                if (spriteRenderers[i] != null)
                {
                    Color c = startColors[i];
                    c.a *= alphaMul;
                    spriteRenderers[i].color = c;
                }
            }

            yield return null;
        }
    }

    private System.Collections.IEnumerator FadeIn()
    {
        if (spriteRenderers == null || originalColors == null)
        {
            yield break;
        }

        int count = Mathf.Min(spriteRenderers.Length, originalColors.Length);
        if (count == 0)
        {
            yield break;
        }

        // Start fully transparent
        for (int i = 0; i < count; i++)
        {
            if (spriteRenderers[i] != null)
            {
                Color c = originalColors[i];
                c.a = 0f;
                spriteRenderers[i].color = c;
            }
        }

        // If duration is zero or negative, snap to original colors
        if (fadeInDuration <= 0f)
        {
            for (int i = 0; i < count; i++)
            {
                if (spriteRenderers[i] != null)
                {
                    spriteRenderers[i].color = originalColors[i];
                }
            }
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeInDuration);

            for (int i = 0; i < count; i++)
            {
                if (spriteRenderers[i] != null)
                {
                    Color c = originalColors[i];
                    c.a *= t;
                    spriteRenderers[i].color = c;
                }
            }

            yield return null;
        }

        // Ensure we end exactly at original colors
        for (int i = 0; i < count; i++)
        {
            if (spriteRenderers[i] != null)
            {
                spriteRenderers[i].color = originalColors[i];
            }
        }
    }

    private System.Collections.IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnDelay);

        currentHealth = maxHealth;
        isBroken = false;
        lastDamageTime = Time.time;

        if (shieldCollider != null)
        {
            shieldCollider.enabled = true;
        }

        if (spriteRenderers != null && originalColors != null)
        {
            int count = Mathf.Min(spriteRenderers.Length, originalColors.Length);
            for (int i = 0; i < count; i++)
            {
                if (spriteRenderers[i] != null)
                {
                    spriteRenderers[i].color = originalColors[i];
                }
            }
        }

        activeShield = this;

        ProjectileCards card = ProjectileCardModifiers.Instance != null
            ? ProjectileCardModifiers.Instance.GetCardFromProjectile(gameObject)
            : null;
        if (card != null && ProjectileCardLevelSystem.Instance != null)
        {
            int enhancedVariant = ProjectileCardLevelSystem.Instance.GetEnhancedVariant(card);
            int level = Mathf.Max(1, ProjectileCardLevelSystem.Instance.GetLevel(card));
            ApplyVariantFromIndex(enhancedVariant, level);
        }

        if (fadeInDuration > 0f && spriteRenderers != null && spriteRenderers.Length > 0)
        {
            StartCoroutine(FadeIn());
        }
    }

    public static void ResetRunState()
    {
        HolyShield[] shields = UnityEngine.Object.FindObjectsOfType<HolyShield>();
        if (shields != null)
        {
            for (int i = 0; i < shields.Length; i++)
            {
                if (shields[i] != null)
                {
                    UnityEngine.Object.Destroy(shields[i].gameObject);
                }
            }
        }

        activeShield = null;
        lastDestroyedTime = -1f;
        hasEverSpawned = false;
        ReflectShield.ResetRunState();
        NullifyShield.ResetRunState();
    }
}
