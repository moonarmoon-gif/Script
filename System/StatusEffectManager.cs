using UnityEngine;
using System.Collections;

/// <summary>
/// Manages status effects like Burn and Slow on enemies
/// </summary>
public class StatusEffectManager : MonoBehaviour
{
    // Burn effect
    private bool isBurning = false;
    private float burnDamagePercent = 0.1f; // 10% of total damage
    private float burnTickInterval = 0.5f; // Damage every 0.5s
    private float burnDuration = 5f;
    private float burnTotalDamage = 0f;
    private Coroutine burnCoroutine;
    
    // Slow effect
    private bool isSlowed = false;
    private float slowMultiplier = 0.5f; // 50% speed
    private float slowDuration = 5f;
    private float originalSpeed = 0f;
    private Coroutine slowCoroutine;
    
    private MonoBehaviour enemyScript;
    
    private void Awake()
    {
        // Try to find enemy script with speed field
        enemyScript = GetComponent<MonoBehaviour>();
    }
    
    /// <summary>
    /// Apply burn effect to enemy
    /// </summary>
    public void ApplyBurn(float totalDamage, float damagePercent, float tickInterval, float duration)
    {
        if (isBurning)
        {
            // Refresh burn duration
            if (burnCoroutine != null)
            {
                StopCoroutine(burnCoroutine);
            }
        }

        if (DamageNumberManager.Instance != null)
        {
            Vector3 anchor = DamageNumberManager.Instance.GetAnchorWorldPosition(gameObject, transform.position);
            DamageNumberManager.Instance.ShowBurn(anchor);
        }

        PlayerStats stats = Object.FindObjectOfType<PlayerStats>();
        if (stats != null)
        {
            float totalMultiplier = 1f + Mathf.Max(0f, stats.burnTotalDamageMultiplier);
            if (!Mathf.Approximately(totalMultiplier, 1f))
            {
                totalDamage *= totalMultiplier;
            }

            if (!Mathf.Approximately(stats.burnDurationBonus, 0f))
            {
                duration = Mathf.Max(0f, duration + stats.burnDurationBonus);
            }
        }

        burnTotalDamage = totalDamage;
        burnDamagePercent = damagePercent;
        burnTickInterval = tickInterval;
        burnDuration = duration;
        
        burnCoroutine = StartCoroutine(BurnEffect());
    }
    
    /// <summary>
    /// Apply slow effect to enemy
    /// </summary>
    public void ApplySlow(float speedMultiplier, float duration)
    {
        if (isSlowed)
        {
            // Refresh slow duration
            if (slowCoroutine != null)
            {
                StopCoroutine(slowCoroutine);
            }
        }
        else
        {
            // Store original speed
            originalSpeed = GetEnemySpeed();
        }

        if (DamageNumberManager.Instance != null)
        {
            DamageNumberManager.Instance.ShowSlow(transform.position);
        }

        float finalMultiplier = speedMultiplier;
        float finalDuration = duration;

        PlayerStats stats = Object.FindObjectOfType<PlayerStats>();
        if (stats != null)
        {
            // Convert speed multiplier to a 0-1 "slow strength" (0 = no slow, 1 = full stop)
            float strength = Mathf.Clamp01(1f - finalMultiplier);

            if (!Mathf.Approximately(stats.slowStrengthBonus, 0f))
            {
                strength = Mathf.Clamp01(strength + stats.slowStrengthBonus);
                finalMultiplier = 1f - strength;
            }

            if (!Mathf.Approximately(stats.slowDurationBonus, 0f))
            {
                finalDuration = Mathf.Max(0f, finalDuration + stats.slowDurationBonus);
            }
        }

        slowMultiplier = finalMultiplier;
        slowDuration = finalDuration;
        
        slowCoroutine = StartCoroutine(SlowEffect());
    }
    
    private IEnumerator BurnEffect()
    {
        isBurning = true;
        float elapsed = 0f;
        float damagePerTick = burnTotalDamage * burnDamagePercent;
        
        Debug.Log($"<color=orange>BURN started on {gameObject.name}: {damagePerTick:F1} damage every {burnTickInterval}s for {burnDuration}s</color>");
        
        while (elapsed < burnDuration)
        {
            // Deal burn damage
            IDamageable damageable = GetComponent<IDamageable>();
            if (damageable != null && damageable.IsAlive)
            {
                Vector3 hitPoint = transform.position;
                Vector3 hitNormal = Vector3.up;

                Vector3 anchor = DamageNumberManager.Instance != null
                    ? DamageNumberManager.Instance.GetAnchorWorldPosition(gameObject, hitPoint)
                    : hitPoint;

                damageable.TakeDamage(damagePerTick, anchor, hitNormal);
                
                // Show burn damage number
                if (DamageNumberManager.Instance != null)
                {
                    DamageNumberManager.Instance.ShowDamage(damagePerTick, anchor, DamageNumberManager.DamageType.Fire, false, true);
                }
            }
            else
            {
                // Enemy died, stop burn
                break;
            }
            
            yield return new WaitForSeconds(burnTickInterval);
            elapsed += burnTickInterval;
        }
        
        isBurning = false;
        Debug.Log($"<color=orange>BURN ended on {gameObject.name}</color>");
    }
    
    private IEnumerator SlowEffect()
    {
        isSlowed = true;
        
        // Apply slow
        SetEnemySpeed(originalSpeed * slowMultiplier);
        Debug.Log($"<color=cyan>SLOW started on {gameObject.name}: Speed reduced to {slowMultiplier * 100}% for {slowDuration}s</color>");
        
        yield return new WaitForSeconds(slowDuration);
        
        // Restore original speed
        SetEnemySpeed(originalSpeed);
        isSlowed = false;
        Debug.Log($"<color=cyan>SLOW ended on {gameObject.name}</color>");
    }
    
    private float GetEnemySpeed()
    {
        // Try to get speed from common enemy script patterns
        var type = enemyScript.GetType();
        var speedField = type.GetField("moveSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (speedField != null)
        {
            return (float)speedField.GetValue(enemyScript);
        }
        
        return 2f; // Default speed
    }
    
    private void SetEnemySpeed(float newSpeed)
    {
        // Try to set speed on common enemy script patterns
        var type = enemyScript.GetType();
        var speedField = type.GetField("moveSpeed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (speedField != null)
        {
            speedField.SetValue(enemyScript, newSpeed);
            Debug.Log($"<color=cyan>Set {gameObject.name} speed to {newSpeed}</color>");
        }
    }
    
    private void OnDestroy()
    {
        // Clean up coroutines
        if (burnCoroutine != null) StopCoroutine(burnCoroutine);
        if (slowCoroutine != null) StopCoroutine(slowCoroutine);
    }
}
