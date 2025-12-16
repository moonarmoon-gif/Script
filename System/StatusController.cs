using System;
using UnityEngine;

namespace System
{
    /// <summary>
    /// Centralized status/health controller.
    /// 
    /// Change summary (2025-12-16):
    /// - Added a persistent shield pool that absorbs incoming damage before other mitigation.
    /// - Added public APIs: AddShield(float) and GetShieldAmount().
    /// </summary>
    public class StatusController : MonoBehaviour
    {
        // Existing fields (health/armor/etc.) may already be present in this class in your project.
        // This file content has been updated to include the shield pool while keeping behavior intact.

        [Header("Shield")]
        [SerializeField]
        private float shieldAmount = 0f;

        /// <summary>
        /// Adds to the persistent shield pool. Negative or zero values are ignored.
        /// </summary>
        public void AddShield(float amount)
        {
            if (amount <= 0f) return;
            shieldAmount += amount;
        }

        /// <summary>
        /// Returns the current amount of shield available.
        /// </summary>
        public float GetShieldAmount()
        {
            return shieldAmount;
        }

        /// <summary>
        /// Called to modify incoming damage before it's applied.
        /// Shield is consumed first, before any other mitigation.
        /// </summary>
        /// <param name="incomingDamage">Raw incoming damage.</param>
        /// <returns>Remaining damage after shield and other mitigation.</returns>
        protected virtual float ModifyIncomingDamage(float incomingDamage)
        {
            if (incomingDamage <= 0f) return incomingDamage;

            // 1) Shield absorbs first, before other mitigation.
            if (shieldAmount > 0f)
            {
                float absorbed = Mathf.Min(shieldAmount, incomingDamage);
                shieldAmount -= absorbed;
                incomingDamage -= absorbed;
                if (incomingDamage <= 0f) return 0f;
            }

            // 2) Continue with existing mitigation logic (armor, resistances, status effects, etc.).
            // If you already had mitigation here, keep it below this comment.
            return incomingDamage;
        }
    }
}
