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

    [Header("Variant Settings")]
    [SerializeField] private float reflectRechargeDuration = 30f;
    [SerializeField] private float nullifyRechargeDuration = 30f;
    [SerializeField] private float colorTransitionDelay = 1f;

    [Header("Variant Colors")]
    [SerializeField] private Color reflectVariantColor = Color.cyan;
    [SerializeField] private Color nullifyVariantColor = Color.magenta;

    [Header("Variant Visual Options")]
    [SerializeField] private bool useReflectVariantColor = true;
    [SerializeField] private bool useNullifyVariantColor = true;
    [SerializeField] private Animator animator;

    private float currentHealth;
    private float lastDamageTime;
    private bool isBroken;
    private SpriteRenderer[] spriteRenderers;
    private Collider2D shieldCollider;
    private float baseMaxHealth;
    private float baseRespawnDelay;
    private Vector3 baseScale;
    private Color[] originalColors;

    private enum ShieldVariantMode
    {
        Base,
        Reflect,
        Nullify
    }

    private ShieldVariantMode variantMode = ShieldVariantMode.Base;
    private bool variantPropertyActive;
    private float variantPropertyReenableTime;
    private Coroutine colorTransitionRoutine;

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

        variantMode = ShieldVariantMode.Base;
        variantPropertyActive = false;
        variantPropertyReenableTime = 0f;

        if (card != null && ProjectileCardLevelSystem.Instance != null)
        {
            int enhancedVariant = ProjectileCardLevelSystem.Instance.GetEnhancedVariant(card);
            if (enhancedVariant == 1)
            {
                variantMode = ShieldVariantMode.Reflect;
                variantPropertyActive = true;
            }
            else if (enhancedVariant == 2)
            {
                variantMode = ShieldVariantMode.Nullify;
                variantPropertyActive = true;
            }
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
            StartCoroutine(ApplyVariantVisualStateAfterFadeIn());
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
        ShieldVariantMode newMode = ShieldVariantMode.Base;
        bool newPropertyActive = false;

        if (variantIndex == 1)
        {
            newMode = ShieldVariantMode.Reflect;
            newPropertyActive = true;
        }
        else if (variantIndex == 2)
        {
            newMode = ShieldVariantMode.Nullify;
            newPropertyActive = true;
        }

        variantMode = newMode;
        variantPropertyActive = newPropertyActive;
        variantPropertyReenableTime = 0f;

        if (spriteRenderers != null && spriteRenderers.Length > 0)
        {
            if (variantMode == ShieldVariantMode.Reflect || variantMode == ShieldVariantMode.Nullify)
            {
                if (variantPropertyActive)
                {
                    ApplyVariantActiveVisual();
                }
                else
                {
                    ApplyVariantInactiveVisual();
                }
            }
            else
            {
                ApplyVariantInactiveVisual();
            }
        }
    }

    public void HandleIncomingHit(float amount, GameObject attacker, Vector3 hitPoint, Vector3 hitNormal, bool isMeleeLikeHit, bool isRangedLikeHit)
    {
        if (isBroken || amount <= 0f)
        {
            return;
        }

        bool isAoeDamage = DamageAoeScope.IsAoeDamage;

        bool handled = false;

        if (!isAoeDamage && variantMode == ShieldVariantMode.Reflect && variantPropertyActive && isMeleeLikeHit)
        {
            if (attacker != null)
            {
                IDamageable attackerDamageable = attacker.GetComponent<IDamageable>() ?? attacker.GetComponentInParent<IDamageable>();
                if (attackerDamageable != null && attackerDamageable.IsAlive)
                {
                    float reflectDamage = amount;
                    Vector3 attackerPos = attacker.transform.position;
                    Vector3 normal = (attackerPos - transform.position).normalized;

                    // Tag the attacker EnemyHealth so its own damage pipeline
                    // renders this hit using the Reflect damage color instead
                    // of whatever elemental color was last applied.
                    EnemyHealth enemyHealth = attacker.GetComponent<EnemyHealth>() ?? attacker.GetComponentInParent<EnemyHealth>();
                    if (enemyHealth != null)
                    {
                        enemyHealth.SetLastIncomingDamageType(DamageNumberManager.DamageType.Reflect);
                    }

                    attackerDamageable.TakeDamage(reflectDamage, attackerPos, normal);

                    if (DamageNumberManager.Instance != null)
                    {
                        // Show only the "Reflect" status text at the shield;
                        // the numeric damage popup comes from EnemyHealth.
                        DamageNumberManager.Instance.ShowReflect(transform.position);
                    }
                }
            }

            handled = true;
        }
        else if (!isAoeDamage && variantMode == ShieldVariantMode.Nullify && variantPropertyActive && isRangedLikeHit)
        {
            if (DamageNumberManager.Instance != null)
            {
                DamageNumberManager.Instance.ShowNullify(transform.position);
            }

            handled = true;
        }

        if (variantMode == ShieldVariantMode.Nullify && variantPropertyActive && (isMeleeLikeHit || isAoeDamage))
        {
            variantPropertyActive = false;
            variantPropertyReenableTime = Time.time + nullifyRechargeDuration;
            ApplyVariantInactiveVisual();
        }
        else if (variantMode == ShieldVariantMode.Reflect && variantPropertyActive && (isRangedLikeHit || isAoeDamage))
        {
            variantPropertyActive = false;
            variantPropertyReenableTime = Time.time + reflectRechargeDuration;
            ApplyVariantInactiveVisual();
        }

        if (handled)
        {
            return;
        }

        TakeDamage(amount, hitPoint, hitNormal);
    }

    public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (isBroken || amount <= 0f)
        {
            return;
        }

        if (DamageAoeScope.IsAoeDamage && variantMode == ShieldVariantMode.Nullify && variantPropertyActive)
        {
            variantPropertyActive = false;
            variantPropertyReenableTime = Time.time + nullifyRechargeDuration;
            ApplyVariantInactiveVisual();
        }

        if (!DamageAoeScope.IsAoeDamage && variantMode == ShieldVariantMode.Nullify && variantPropertyActive)
        {
            if (DamageNumberManager.Instance != null)
            {
                DamageNumberManager.Instance.ShowNullify(transform.position);
            }

            return;
        }

        if (variantMode == ShieldVariantMode.Reflect && variantPropertyActive)
        {
            variantPropertyActive = false;
            variantPropertyReenableTime = Time.time + reflectRechargeDuration;
            ApplyVariantInactiveVisual();
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

        if ((variantMode == ShieldVariantMode.Reflect || variantMode == ShieldVariantMode.Nullify) &&
            !variantPropertyActive && variantPropertyReenableTime > 0f && Time.time >= variantPropertyReenableTime)
        {
            variantPropertyActive = true;
            ApplyVariantActiveVisual();
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

        if (fadeInDuration > 0f && spriteRenderers != null && spriteRenderers.Length > 0)
        {
            StartCoroutine(FadeIn());
        }

        if (spriteRenderers != null && spriteRenderers.Length > 0)
        {
            StartCoroutine(ApplyVariantVisualStateAfterFadeIn());
        }
    }

    public static void ResetRunState()
    {
        activeShield = null;
        lastDestroyedTime = -1f;
        hasEverSpawned = false;
    }

    private void ApplyVariantActiveVisual()
    {
        if (variantMode == ShieldVariantMode.Reflect)
        {
            if (useReflectVariantColor)
            {
                StartVariantColorTransitionToVariant();
            }
            else if (animator != null)
            {
                animator.SetBool("IsHoly", false);
                animator.SetBool("IsVoid", true);
                animator.SetBool("IsIce", false);
            }
        }
        else if (variantMode == ShieldVariantMode.Nullify)
        {
            if (useNullifyVariantColor)
            {
                StartVariantColorTransitionToVariant();
            }
            else if (animator != null)
            {
                animator.SetBool("IsHoly", false);
                animator.SetBool("IsIce", true);
                animator.SetBool("IsVoid", false);
            }
        }
    }

    private void ApplyVariantInactiveVisual()
    {
        if (variantMode == ShieldVariantMode.Reflect)
        {
            if (useReflectVariantColor)
            {
                StartVariantColorTransitionToOriginal();
            }
            else if (animator != null)
            {
                animator.SetBool("IsHoly", true);
                animator.SetBool("IsVoid", false);
                animator.SetBool("IsIce", false);
            }
        }
        else if (variantMode == ShieldVariantMode.Nullify)
        {
            if (useNullifyVariantColor)
            {
                StartVariantColorTransitionToOriginal();
            }
            else if (animator != null)
            {
                animator.SetBool("IsHoly", true);
                animator.SetBool("IsIce", false);
                animator.SetBool("IsVoid", false);
            }
        }
        else
        {
            if (animator != null)
            {
                animator.SetBool("IsHoly", true);
                animator.SetBool("IsVoid", false);
                animator.SetBool("IsIce", false);
            }
        }
    }

    private void StartVariantColorTransitionToVariant()
    {
        if (spriteRenderers == null || spriteRenderers.Length == 0)
        {
            return;
        }

        Color targetTint = variantMode == ShieldVariantMode.Reflect ? reflectVariantColor : nullifyVariantColor;
        StartVariantColorTransition(targetTint, true);
    }

    private void StartVariantColorTransitionToOriginal()
    {
        if (spriteRenderers == null || spriteRenderers.Length == 0)
        {
            return;
        }

        StartVariantColorTransition(Color.white, false);
    }

    private void StartVariantColorTransition(Color targetVariantColor, bool toVariant)
    {
        if (colorTransitionRoutine != null)
        {
            StopCoroutine(colorTransitionRoutine);
        }

        colorTransitionRoutine = StartCoroutine(VariantColorTransitionCoroutine(targetVariantColor, toVariant));
    }

    private System.Collections.IEnumerator VariantColorTransitionCoroutine(Color targetVariantColor, bool toVariant)
    {
        if (spriteRenderers == null || spriteRenderers.Length == 0)
        {
            yield break;
        }

        int count = spriteRenderers.Length;
        Color[] startColors = new Color[count];
        Color[] endColors = new Color[count];

        for (int i = 0; i < count; i++)
        {
            if (spriteRenderers[i] == null)
            {
                continue;
            }

            Color current = spriteRenderers[i].color;
            startColors[i] = current;

            if (toVariant)
            {
                Color baseColor = (originalColors != null && i < originalColors.Length) ? originalColors[i] : current;
                Color target = Color.Lerp(baseColor, targetVariantColor, 1f);
                target.a = current.a;
                endColors[i] = target;
            }
            else
            {
                if (originalColors != null && i < originalColors.Length)
                {
                    Color target = originalColors[i];
                    target.a = current.a;
                    endColors[i] = target;
                }
                else
                {
                    endColors[i] = current;
                }
            }
        }

        float duration = Mathf.Max(0f, colorTransitionDelay);
        if (duration <= 0f)
        {
            for (int i = 0; i < count; i++)
            {
                if (spriteRenderers[i] != null)
                {
                    spriteRenderers[i].color = endColors[i];
                }
            }
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            for (int i = 0; i < count; i++)
            {
                if (spriteRenderers[i] != null)
                {
                    Color c = Color.Lerp(startColors[i], endColors[i], t);
                    spriteRenderers[i].color = c;
                }
            }

            yield return null;
        }

        for (int i = 0; i < count; i++)
        {
            if (spriteRenderers[i] != null)
            {
                spriteRenderers[i].color = endColors[i];
            }
        }
    }

    private System.Collections.IEnumerator ApplyVariantVisualStateAfterFadeIn()
    {
        if (spriteRenderers == null || spriteRenderers.Length == 0)
        {
            yield break;
        }

        if (fadeInDuration > 0f)
        {
            yield return new WaitForSeconds(fadeInDuration);
        }

        if (isBroken)
        {
            yield break;
        }

        if (variantMode == ShieldVariantMode.Reflect || variantMode == ShieldVariantMode.Nullify)
        {
            if (variantPropertyActive)
            {
                ApplyVariantActiveVisual();
            }
            else
            {
                ApplyVariantInactiveVisual();
            }
        }
    }
}
