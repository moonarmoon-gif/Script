using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attached to the player to manage all active Favour effects.
/// It instantiates runtime copies of FavourEffect assets so each effect
/// instance can safely track its own state without modifying the asset.
/// </summary>
public class FavourEffectManager : MonoBehaviour
{
    private class ActiveEffect
    {
        public FavourEffect effectInstance;
        public FavourEffect sourceAsset;
    }

    private readonly List<ActiveEffect> activeEffects = new List<ActiveEffect>();
    public ProjectileCards CurrentProjectileCard { get; internal set; }

    /// <summary>
    /// Apply or upgrade a FavourEffect asset on this player. When the same
    /// favour is selected again, we reuse the existing runtime instance and
    /// call OnUpgrade instead of creating a new one.
    /// </summary>
    public void AddEffect(FavourEffect effectAsset, FavourCards sourceCard)
    {
        if (effectAsset == null)
        {
            return;
        }

        // Track how many times this favour has been picked so that
        // MaxPickLimit can be enforced globally during card selection.
        effectAsset.RegisterPick();

        // Look for an existing runtime instance created from this asset
        for (int i = 0; i < activeEffects.Count; i++)
        {
            if (activeEffects[i].sourceAsset == effectAsset && activeEffects[i].effectInstance != null)
            {
                activeEffects[i].effectInstance.OnUpgrade(gameObject, this, sourceCard);
                return;
            }
        }

        // No existing instance, create a new one
        FavourEffect runtimeInstance = ScriptableObject.Instantiate(effectAsset);
        var entry = new ActiveEffect
        {
            effectInstance = runtimeInstance,
            sourceAsset = effectAsset
        };
        activeEffects.Add(entry);

        runtimeInstance.OnApply(gameObject, this, sourceCard);
    }

    /// <summary>
    /// Remove a specific runtime effect instance from this manager.
    /// </summary>
    public void RemoveEffect(FavourEffect effectInstance)
    {
        if (effectInstance == null)
        {
            return;
        }

        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            if (activeEffects[i].effectInstance == effectInstance)
            {
                activeEffects[i].effectInstance.OnRemove(gameObject, this);
                activeEffects.RemoveAt(i);
                break;
            }
        }
    }

    /// <summary>
    /// Notify all active effects that the player has successfully applied a
    /// status effect to an enemy (e.g., Burn, Slow, Static). This allows
    /// favours to react immediately to status inflictions.
    /// </summary>
    public void NotifyStatusApplied(GameObject enemy, StatusId statusId)
    {
        for (int i = 0; i < activeEffects.Count; i++)
        {
            FavourEffect effect = activeEffects[i].effectInstance;
            if (effect != null)
            {
                effect.OnStatusApplied(gameObject, enemy, statusId, this);
            }
        }
    }

    private void Update()
    {
        float deltaTime = GameStateManager.GetPauseSafeDeltaTime();
        for (int i = 0; i < activeEffects.Count; i++)
        {
            FavourEffect effect = activeEffects[i].effectInstance;
            if (effect != null)
            {
                effect.OnUpdate(gameObject, this, deltaTime);
            }
        }
    }

    /// <summary>
    /// Notify all active effects that an enemy has been killed.
    /// This will be wired into the enemy death pipeline later.
    /// </summary>
    public void NotifyEnemyKilled(GameObject enemy)
    {
        for (int i = 0; i < activeEffects.Count; i++)
        {
            FavourEffect effect = activeEffects[i].effectInstance;
            if (effect != null)
            {
                effect.OnEnemyKilled(gameObject, enemy, this);
            }
        }
    }

    public void NotifyBeforeDealDamage(GameObject enemy, ref float damage)
    {
        for (int i = 0; i < activeEffects.Count; i++)
        {
            FavourEffect effect = activeEffects[i].effectInstance;
            if (effect != null)
            {
                effect.OnBeforeDealDamage(gameObject, enemy, ref damage, this);
            }
        }
    }

    public void NotifyEnemyDamageFinalized(GameObject enemy, float finalDamage, bool isStatusTick)
    {
        for (int i = 0; i < activeEffects.Count; i++)
        {
            FavourEffect effect = activeEffects[i].effectInstance;
            if (effect != null)
            {
                effect.OnEnemyDamageFinalized(gameObject, enemy, finalDamage, isStatusTick, this);
            }
        }
    }

    public float GetExecuteThresholdPercentForEnemy(GameObject enemy)
    {
        float threshold = 0f;

        for (int i = 0; i < activeEffects.Count; i++)
        {
            FavourEffect effect = activeEffects[i].effectInstance;
            if (effect == null)
            {
                continue;
            }

            float t = effect.GetExecuteThresholdPercent(gameObject, enemy, this);
            if (t > threshold)
            {
                threshold = t;
            }
        }

        return Mathf.Clamp(threshold, 0f, 100f);
    }

    public void NotifyCritResolved(ProjectileCards sourceCard, bool canCrit, bool didCrit)
    {
        for (int i = 0; i < activeEffects.Count; i++)
        {
            FavourEffect effect = activeEffects[i].effectInstance;
            if (effect != null)
            {
                effect.OnCritResolved(gameObject, sourceCard, canCrit, didCrit, this);
            }
        }
    }

    public float PreviewBeforeDealDamage(GameObject enemy, float damage)
    {
        for (int i = 0; i < activeEffects.Count; i++)
        {
            FavourEffect effect = activeEffects[i].effectInstance;
            if (effect != null)
            {
                damage = effect.PreviewBeforeDealDamage(gameObject, enemy, damage, this);
            }
        }

        return damage;
    }

    /// <summary>
    /// Remove and clean up all active favour effects. Used when fully
    /// resetting the run so no runtime state is carried over.
    /// </summary>
    public void ClearAllEffects()
    {
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            var entry = activeEffects[i];
            if (entry != null && entry.effectInstance != null)
            {
                entry.effectInstance.OnRemove(gameObject, this);
            }
        }

        activeEffects.Clear();
        CurrentProjectileCard = null;
    }

    /// <summary>
    /// Notify all active effects that the player is about to be hit.
    /// Effects can modify the incoming damage value.
    ///
    /// Shield-like favours have a defined priority order:
    /// 1) ShieldOnLowHealthFavour absorbs first
    /// 2) All other favours modify damage
    /// 3) ManaShieldFavour absorbs last (and can choose to skip if HolyShield is active)
    /// </summary>
    public void NotifyPlayerHit(GameObject attacker, ref float damage)
    {
        // Pass 1: run low-health shield first so it always gets the first
        // chance to absorb incoming damage.
        for (int i = 0; i < activeEffects.Count; i++)
        {
            var entry = activeEffects[i];
            if (entry == null || entry.effectInstance == null) continue;

            if (entry.effectInstance is ShieldOnLowHealthFavour)
            {
                entry.effectInstance.OnPlayerHit(gameObject, attacker, ref damage, this);
            }
        }

        // Pass 2: run all other favours EXCEPT the shield-specific ones so
        // they can react to the partially absorbed damage.
        for (int i = 0; i < activeEffects.Count; i++)
        {
            var entry = activeEffects[i];
            if (entry == null || entry.effectInstance == null) continue;

            if (entry.effectInstance is ShieldOnLowHealthFavour) continue;
            if (entry.effectInstance is ManaShieldFavour) continue;

            entry.effectInstance.OnPlayerHit(gameObject, attacker, ref damage, this);
        }

        // Pass 3: run ManaShield last so it only ever sees the remaining
        // damage after all other reactions (and can optionally skip if
        // HolyShield is currently active).
        for (int i = 0; i < activeEffects.Count; i++)
        {
            var entry = activeEffects[i];
            if (entry == null || entry.effectInstance == null) continue;

            ManaShieldFavour manaShield = entry.effectInstance as ManaShieldFavour;
            if (manaShield != null)
            {
                manaShield.OnPlayerHit(gameObject, attacker, ref damage, this);
            }
        }
    }

    public void NotifyPlayerDamageFinalized(GameObject attacker, float finalDamage, bool isStatusTick, bool isAoeDamage)
    {
        for (int i = 0; i < activeEffects.Count; i++)
        {
            var entry = activeEffects[i];
            if (entry == null || entry.effectInstance == null) continue;
            entry.effectInstance.OnPlayerDamageFinalized(gameObject, attacker, finalDamage, isStatusTick, isAoeDamage, this);
        }
    }
}
