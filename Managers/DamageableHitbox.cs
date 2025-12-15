// DamageableHitbox.cs
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class DamageableHitbox : MonoBehaviour
{
    [SerializeField] private MonoBehaviour target; // must implement IDamageable
    private IDamageable damageable;

    private void Awake()
    {
        damageable = target as IDamageable;
        if (damageable == null && target != null)
        {
            Debug.LogError("Assigned target does not implement IDamageable.", target);
        }
        if (damageable == null)
        {
            damageable = GetComponentInParent<IDamageable>();
        }
    }

    public void ApplyDamage(float amount, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (damageable != null) damageable.TakeDamage(amount, hitPoint, hitNormal);
    }
}