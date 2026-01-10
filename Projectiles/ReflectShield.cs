using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ReflectShield : MonoBehaviour
{
    [Header("Spawn Offset")]
    public Vector2 spawnOffset = Vector2.zero;

    [Header("Charges")]
    public int chargesMax = 1;

    [Header("Recharge")]
    public float ReflectRechargeDuration = 60f;

    [Header("Fade")]
    public float FadeInDuration = 0.5f;
    public float FadeOutDuration = 1f;

    private int chargesCurrent;
    private float lastChargeConsumedTime = -999f;
    private bool isDisabled;

    private SpriteRenderer[] spriteRenderers;
    private Color[] originalColors;
    private Collider2D shieldCollider;

    private static ReflectShield activeShield;

    public static ReflectShield ActiveShield => activeShield;

    public bool IsAlive => !isDisabled && chargesCurrent > 0;

    public int ChargesCurrent => chargesCurrent;

    private void Awake()
    {
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
        shieldCollider = GetComponent<Collider2D>();

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

    public void Initialize(Vector3 spawnPosition, Collider2D playerCollider, int maxCharges)
    {
        chargesMax = Mathf.Max(1, maxCharges);
        chargesCurrent = chargesMax;
        lastChargeConsumedTime = -999f;

        transform.position = spawnPosition + (Vector3)spawnOffset;
        if (playerCollider != null)
        {
            transform.SetParent(playerCollider.transform);
        }

        isDisabled = false;
        if (shieldCollider != null)
        {
            shieldCollider.enabled = true;
        }

        activeShield = this;

        if (FadeInDuration > 0f)
        {
            StartCoroutine(FadeIn());
        }
    }

    public void SetMaxCharges(int maxCharges)
    {
        int oldMax = chargesMax;
        chargesMax = Mathf.Max(1, maxCharges);

        int delta = chargesMax - oldMax;
        if (delta > 0)
        {
            chargesCurrent += delta;
        }

        chargesCurrent = Mathf.Clamp(chargesCurrent, 0, chargesMax);
    }

    private void Update()
    {
        if (isDisabled)
        {
            return;
        }

        if (chargesCurrent < chargesMax && ReflectRechargeDuration > 0f)
        {
            if (Time.time - lastChargeConsumedTime >= ReflectRechargeDuration)
            {
                chargesCurrent = chargesMax;
            }
        }
    }

    public bool TryHandleMeleeHit(float amount, GameObject attacker)
    {
        if (!IsAlive)
        {
            return false;
        }

        if (amount <= 0f)
        {
            return true;
        }

        if (attacker != null)
        {
            IDamageable attackerDamageable = attacker.GetComponent<IDamageable>() ?? attacker.GetComponentInParent<IDamageable>();
            if (attackerDamageable != null && attackerDamageable.IsAlive)
            {
                Vector3 attackerPos = attacker.transform.position;
                Vector3 normal = (attackerPos - transform.position).normalized;

                EnemyHealth enemyHealth = attacker.GetComponent<EnemyHealth>() ?? attacker.GetComponentInParent<EnemyHealth>();
                if (enemyHealth != null)
                {
                    enemyHealth.SetLastIncomingDamageType(DamageNumberManager.DamageType.Reflect);
                }

                attackerDamageable.TakeDamage(amount, attackerPos, normal);

                if (DamageNumberManager.Instance != null)
                {
                    DamageNumberManager.Instance.ShowReflect(transform.position);
                }
            }
        }

        ConsumeCharge();
        return true;
    }

    private void ConsumeCharge()
    {
        chargesCurrent = Mathf.Max(0, chargesCurrent - 1);
        lastChargeConsumedTime = Time.time;

        if (chargesCurrent <= 0)
        {
            DisableShield();
        }
    }

    private void DisableShield()
    {
        if (isDisabled)
        {
            return;
        }

        isDisabled = true;

        if (shieldCollider != null)
        {
            shieldCollider.enabled = false;
        }

        if (FadeOutDuration > 0f)
        {
            StartCoroutine(FadeOutAndRespawn());
        }
        else
        {
            SetAlpha(0f);
            StartCoroutine(RespawnRoutine());
        }
    }

    private IEnumerator FadeOutAndRespawn()
    {
        float elapsed = 0f;
        while (elapsed < FadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / FadeOutDuration);
            SetAlpha(1f - t);
            yield return null;
        }

        SetAlpha(0f);
        yield return RespawnRoutine();
    }

    private IEnumerator RespawnRoutine()
    {
        float wait = Mathf.Max(0f, ReflectRechargeDuration);
        if (wait > 0f)
        {
            yield return new WaitForSeconds(wait);
        }

        chargesCurrent = chargesMax;
        isDisabled = false;

        if (shieldCollider != null)
        {
            shieldCollider.enabled = true;
        }

        if (FadeInDuration > 0f)
        {
            yield return FadeIn();
        }
        else
        {
            RestoreOriginalColors();
        }
    }

    private IEnumerator FadeIn()
    {
        if (spriteRenderers == null || originalColors == null)
        {
            yield break;
        }

        RestoreOriginalColors();
        SetAlpha(0f);

        float elapsed = 0f;
        while (elapsed < FadeInDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / FadeInDuration);
            SetAlpha(t);
            yield return null;
        }

        SetAlpha(1f);
    }

    private void RestoreOriginalColors()
    {
        if (spriteRenderers == null || originalColors == null)
        {
            return;
        }

        int count = Mathf.Min(spriteRenderers.Length, originalColors.Length);
        for (int i = 0; i < count; i++)
        {
            if (spriteRenderers[i] != null)
            {
                spriteRenderers[i].color = originalColors[i];
            }
        }
    }

    private void SetAlpha(float alpha)
    {
        if (spriteRenderers == null)
        {
            return;
        }

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
            {
                Color c = spriteRenderers[i].color;
                float baseAlpha = c.a;
                if (originalColors != null && i >= 0 && i < originalColors.Length)
                {
                    baseAlpha = originalColors[i].a;
                }
                c.a = baseAlpha * Mathf.Clamp01(alpha);
                spriteRenderers[i].color = c;
            }
        }
    }

    public static void ResetRunState()
    {
        if (activeShield != null)
        {
            UnityEngine.Object.Destroy(activeShield.gameObject);
        }
        activeShield = null;
    }

    private void OnDestroy()
    {
        if (activeShield == this)
        {
            activeShield = null;
        }
    }
}
