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

    private StaticStatus casterStaticStatus;

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

        if (casterHealth != null)
        {
            casterStaticStatus = casterHealth.GetComponent<StaticStatus>();
        }

        StartCoroutine(SpellEffectRoutine());
    }

    IEnumerator SpellEffectRoutine()
    {
        yield return StaticPauseHelper.WaitForSecondsPauseSafeAndStatic(
            damageDelay,
            () => casterHealth == null || !casterHealth.IsAlive || casterEnemy == null || !casterEnemy.IsSpellActionTokenValid(casterSpellToken),
            () => casterStaticStatus != null && casterStaticStatus.IsInStaticPeriod);

        if (casterHealth == null || !casterHealth.IsAlive || casterEnemy == null || !casterEnemy.IsSpellActionTokenValid(casterSpellToken))
        {
            Destroy(gameObject);
            yield break;
        }

        if (!hasDealtDamage && targetDamageable != null && targetDamageable.IsAlive && AdvancedPlayerController.Instance != null)
        {
            yield return StaticPauseHelper.WaitWhileStatic(
                () => casterHealth == null || !casterHealth.IsAlive || casterEnemy == null || !casterEnemy.IsSpellActionTokenValid(casterSpellToken),
                () => casterStaticStatus != null && casterStaticStatus.IsInStaticPeriod);

            Vector3 playerPos = AdvancedPlayerController.Instance.transform.position;
            Vector3 hitNormal = (playerPos - casterPosition).normalized;

            if (attacker != null)
            {
                PlayerHealth.RegisterPendingAttacker(attacker);
            }

            // IMPORTANT: This goes through PlayerHealth.TakeDamage pipeline (armor etc.)
            targetDamageable.TakeDamage(damage, playerPos, hitNormal);

            hasDealtDamage = true;
            Debug.Log($"<color=cyan>Spell effect dealt {damage} damage (independent timing)</color>");
        }

        float remainingDuration = effectDuration - damageDelay;
        if (remainingDuration > 0)
        {
            yield return StaticPauseHelper.WaitForSecondsPauseSafeAndStatic(
                remainingDuration,
                () => casterHealth == null || !casterHealth.IsAlive || casterEnemy == null || !casterEnemy.IsSpellActionTokenValid(casterSpellToken),
                () => casterStaticStatus != null && casterStaticStatus.IsInStaticPeriod);
        }

        Destroy(gameObject);
    }
}