using UnityEngine;

public interface IDamageable
{
    /// <summary>
    /// True while this object can still take damage (i.e., not dead/broken).
    /// </summary>
    bool IsAlive { get; }

    /// <summary>
    /// Apply damage at a specific world-space point and normal.
    /// </summary>
    void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal);
}