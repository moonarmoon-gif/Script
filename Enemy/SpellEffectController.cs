using System.Collections;
using UnityEngine;

/// <summary>
/// Independent controller for DeathBringer's spell effect
/// Handles damage timing and cleanup independently of DeathBringer's state
/// </summary>
public class SpellEffectController : MonoBehaviour
{
    private float damage;
    private float damageDelay;
    private float effectDuration;
    private IDamageable targetDamageable;
    private Vector3 casterPosition;
    private bool hasDealtDamage = false;
    private GameObject attacker;

    private EnemyHealth casterHealth;
    private DeathBringerEnemy casterEnemy;
    private int casterSpellToken;

    public void Initialize(float spellDamage, float spellDamageDelay, float spellEffectDuration, IDamageable playerDamageable, GameObject attacker, Vector3 deathBringerPosition, EnemyHealth casterHealth, DeathBringerEnemy casterEnemy, int casterSpellToken)
    {
        damage = spellDamage;
        damageDelay = spellDamageDelay;
        effectDuration = spellEffectDuration;
        targetDamageable = playerDamageable;
        this.attacker = attacker;
        casterPosition = deathBringerPosition;

        this.casterHealth = casterHealth;
        this.casterEnemy = casterEnemy;
        this.casterSpellToken = casterSpellToken;

        // Start independent coroutine
        StartCoroutine(SpellEffectRoutine());
    }

    IEnumerator SpellEffectRoutine()
    {
        // Wait for damage delay
        yield return new WaitForSeconds(damageDelay);

        if (casterHealth == null || !casterHealth.IsAlive || casterEnemy == null || !casterEnemy.IsSpellActionTokenValid(casterSpellToken))
        {
            Destroy(gameObject);
            yield break;
        }

        // Deal damage
        if (!hasDealtDamage && targetDamageable != null && targetDamageable.IsAlive && AdvancedPlayerController.Instance != null)
        {
            Vector3 playerPos = AdvancedPlayerController.Instance.transform.position;
            Vector3 hitNormal = (playerPos - casterPosition).normalized;

            if (attacker != null)
            {
                PlayerHealth.RegisterPendingAttacker(attacker);
            }

            targetDamageable.TakeDamage(damage, playerPos, hitNormal);
            hasDealtDamage = true;
            Debug.Log($"<color=cyan>Spell effect dealt {damage} damage (independent timing)</color>");
        }

        // Wait for remaining effect duration
        float remainingDuration = effectDuration - damageDelay;
        if (remainingDuration > 0)
        {
            yield return new WaitForSeconds(remainingDuration);
        }

        // Destroy spell effect after duration completes
        Debug.Log("<color=cyan>Spell effect duration complete - destroying</color>");
        Destroy(gameObject);
    }
}
