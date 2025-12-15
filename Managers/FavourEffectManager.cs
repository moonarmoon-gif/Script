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
        float deltaTime = Time.deltaTime;
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
    /// Notify all active effects that the player is about to be hit.
    /// Effects can modify the incoming damage value.
    /// This will be wired into the player damage pipeline later.
    /// </summary>
    public void NotifyPlayerHit(GameObject attacker, ref float damage)
    {
        for (int i = 0; i < activeEffects.Count; i++)
        {
            if (activeEffects[i] != null)
            {
                activeEffects[i].effectInstance.OnPlayerHit(gameObject, attacker, ref damage, this);
            }
        }
    }
}
