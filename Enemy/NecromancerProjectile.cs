using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class NecromancerProjectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    public float speed = 8f;
    public float lifetime = 5f;
    public bool rotateTowardsDirection = true;
    public float rotationSpeed = 720f;

    public bool destroyOnPlayerContact = true;

    public float[] DamageTimings;

    [Header("Visual Effects")]
    public GameObject hitEffectPrefab;
    public float hitEffectLifetime = 0.5f;
    
    [Tooltip("Hit effect offset when projectile is moving left")]
    public Vector2 hitEffectOffsetRight = Vector2.zero;
    
    [Tooltip("Hit effect offset when projectile is moving right")]
    public Vector2 hitEffectOffsetLeft = Vector2.zero;

    private float damage;
    private Vector2 moveDirection;
    private Rigidbody2D rb;
    private Collider2D projectileCollider;
    private Collider2D spawnerCollider;
    private GameObject owner;
    private float spawnTime;
    private bool hasProcessedPlayerHit;
    private Coroutine timedDamageRoutine;
    private DamageableHitbox cachedHitbox;
    private PlayerHealth cachedPlayerHealth;
    private IDamageable cachedDamageable;
    private Transform cachedPlayerTransform;
    private bool isInTimedDamageMode;
    private bool isDestroying;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        projectileCollider = GetComponent<Collider2D>();
        spawnTime = Time.time;

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.bodyType = RigidbodyType2D.Dynamic;
        }

    }

    public void Initialize(float projectileDamage, Vector2 direction, Collider2D spawner = null)
    {
        damage = projectileDamage;
        moveDirection = direction.normalized;
        spawnerCollider = spawner;
        owner = spawner != null ? spawner.gameObject : null;

        if (spawnerCollider != null && projectileCollider != null)
        {
            Physics2D.IgnoreCollision(projectileCollider, spawnerCollider, true);
        }

        // Ignore collision with other projectiles
        NecromancerProjectile[] otherProjectiles = FindObjectsOfType<NecromancerProjectile>();
        foreach (NecromancerProjectile other in otherProjectiles)
        {
            if (other != this && other.projectileCollider != null && projectileCollider != null)
            {
                Physics2D.IgnoreCollision(projectileCollider, other.projectileCollider, true);
            }
        }

        if (rb != null)
        {
            rb.velocity = moveDirection * speed;
        }

        if (rotateTowardsDirection)
        {
            float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    void Update()
    {
        if (!isDestroying && Time.time - spawnTime >= lifetime)
        {
            DestroyProjectile(false);
            return;
        }
    }

    void FixedUpdate()
    {
        if (isInTimedDamageMode)
        {
            return;
        }

        if (rb != null && moveDirection.sqrMagnitude > 0.01f)
        {
            rb.velocity = moveDirection * speed;

            if (rotateTowardsDirection)
            {
                float targetAngle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
                float currentAngle = transform.eulerAngles.z;
                float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, rotationSpeed * Time.fixedDeltaTime);
                transform.rotation = Quaternion.Euler(0f, 0f, newAngle);
            }
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        Vector3 hitPoint = collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position;
        HandleContact(collision.gameObject, collision.collider, hitPoint);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        Vector3 hitPoint = other != null ? other.ClosestPoint(transform.position) : transform.position;
        HandleContact(other != null ? other.gameObject : null, other, hitPoint);
    }

    void HandleContact(GameObject hitObject, Collider2D hitCollider, Vector3 hitPoint)
    {
        if (isDestroying)
        {
            return;
        }

        if (hitObject == null)
        {
            return;
        }

        if (spawnerCollider != null && hitCollider == spawnerCollider)
        {
            return;
        }

        if (hasProcessedPlayerHit)
        {
            if (hitObject.CompareTag("Player"))
            {
                return;
            }

            DamageableHitbox candidateHitbox = hitObject.GetComponent<DamageableHitbox>();
            if (candidateHitbox != null)
            {
                PlayerHealth playerOwner = candidateHitbox.GetComponentInParent<PlayerHealth>();
                if (playerOwner != null)
                {
                    return;
                }
            }
        }

        if (hitObject.CompareTag("Player"))
        {
            HandlePlayerHit(hitObject, hitPoint);
            return;
        }

        DamageableHitbox hitbox = hitObject.GetComponent<DamageableHitbox>();
        if (hitbox != null)
        {
            HandlePlayerHit(hitObject, hitPoint);
            return;
        }

        if (hitObject.GetComponent<EnemyHealth>() != null)
        {
            return;
        }

        if (hitObject.CompareTag("EnemyProjectile") || hitObject.GetComponent<NecromancerProjectile>() != null)
        {
            return;
        }

        if (IsObstacle(hitObject))
        {
            DestroyProjectile(false);
            return;
        }
    }

    void HandlePlayerHit(GameObject playerObject, Vector3 hitPoint)
    {
        hasProcessedPlayerHit = true;

        cachedHitbox = playerObject.GetComponent<DamageableHitbox>();
        cachedPlayerHealth = cachedHitbox != null ? cachedHitbox.GetComponentInParent<PlayerHealth>() : playerObject.GetComponent<PlayerHealth>();
        cachedDamageable = playerObject.GetComponentInParent<IDamageable>();

        if (cachedPlayerHealth != null)
        {
            cachedPlayerTransform = cachedPlayerHealth.transform;
        }
        else if (cachedDamageable is Component damageableComponent)
        {
            cachedPlayerTransform = damageableComponent.transform;
        }
        else
        {
            cachedPlayerTransform = playerObject.transform;
        }

        if (projectileCollider != null)
        {
            GameObject playerRoot = cachedPlayerHealth != null ? cachedPlayerHealth.gameObject : (cachedDamageable is Component damageableRoot ? damageableRoot.gameObject : playerObject);
            Collider2D[] playerColliders = playerRoot.GetComponentsInChildren<Collider2D>();
            foreach (Collider2D col in playerColliders)
            {
                if (col != null)
                {
                    Physics2D.IgnoreCollision(projectileCollider, col, true);
                }
            }
        }

        bool hasTimings = DamageTimings != null && DamageTimings.Length > 0;

        if (!hasTimings)
        {
            ApplyDamageTick();

            if (destroyOnPlayerContact)
            {
                DestroyProjectile(true, hitPoint);
            }

            return;
        }

        if (destroyOnPlayerContact)
        {
            ApplyDamageTick();
            DestroyProjectile(true, hitPoint);
            return;
        }

        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.simulated = false;
        }

        if (projectileCollider != null)
        {
            projectileCollider.enabled = false;
        }

        if (timedDamageRoutine != null)
        {
            StopCoroutine(timedDamageRoutine);
        }

        isInTimedDamageMode = true;
        timedDamageRoutine = StartCoroutine(ApplyTimedDamageRoutine());
    }

    IEnumerator ApplyTimedDamageRoutine()
    {
        float[] timingsCopy = (float[])DamageTimings.Clone();
        System.Array.Sort(timingsCopy);

        float previousTime = 0f;
        for (int i = 0; i < timingsCopy.Length; i++)
        {
            float targetTime = timingsCopy[i];
            if (targetTime < 0f)
            {
                targetTime = 0f;
            }

            float wait = targetTime - previousTime;
            if (wait > 0f)
            {
                yield return new WaitForSeconds(wait);
            }

            previousTime = targetTime;

            if (isDestroying)
            {
                break;
            }

            if (!ApplyDamageTick())
            {
                break;
            }
        }

        timedDamageRoutine = null;
    }

    bool ApplyDamageTick()
    {
        if (isDestroying)
        {
            return false;
        }

        Transform t = cachedPlayerTransform;
        if (t == null)
        {
            return false;
        }

        Vector3 hitPoint = t.position;
        Vector3 hitNormal = (t.position - transform.position).normalized;

        if (cachedHitbox != null)
        {
            if (owner != null)
            {
                PlayerHealth playerHealthOnParent = cachedHitbox.GetComponentInParent<PlayerHealth>();
                if (playerHealthOnParent != null && playerHealthOnParent.IsAlive)
                {
                    PlayerHealth.RegisterPendingAttacker(owner);
                }
            }

            cachedHitbox.ApplyDamage(damage, hitPoint, hitNormal);
            return true;
        }

        if (cachedPlayerHealth != null)
        {
            if (!cachedPlayerHealth.IsAlive)
            {
                return false;
            }

            if (owner != null)
            {
                PlayerHealth.RegisterPendingAttacker(owner);
            }

            cachedPlayerHealth.TakeDamage(damage, hitPoint, hitNormal);
            return true;
        }

        if (cachedDamageable != null)
        {
            if (!cachedDamageable.IsAlive)
            {
                return false;
            }

            cachedDamageable.TakeDamage(damage, hitPoint, hitNormal);
            return true;
        }

        return false;
    }

    bool IsObstacle(GameObject obj)
    {
        int obstacleLayer = LayerMask.NameToLayer("Obstacle");
        int wallLayer = LayerMask.NameToLayer("Wall");
        return (obstacleLayer != -1 && obj.layer == obstacleLayer) ||
               (wallLayer != -1 && obj.layer == wallLayer);
    }

    void DestroyProjectile(bool hitPlayer, Vector3 hitPoint = default)
    {
        if (isDestroying)
        {
            return;
        }

        isDestroying = true;
        isInTimedDamageMode = false;

        if (timedDamageRoutine != null)
        {
            StopCoroutine(timedDamageRoutine);
            timedDamageRoutine = null;
        }

        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.simulated = false;
        }

        if (projectileCollider != null)
        {
            projectileCollider.enabled = false;
        }

        if (hitPlayer && hitEffectPrefab != null)
        {
            Vector3 effectPosition = hitPoint != default ? hitPoint : transform.position;
            
            // Determine offset based on projectile direction
            Vector2 offset = moveDirection.x < 0 ? hitEffectOffsetLeft : hitEffectOffsetRight;
            effectPosition += (Vector3)offset;
            
            GameObject effect = Instantiate(hitEffectPrefab, effectPosition, Quaternion.identity);
            Destroy(effect, hitEffectLifetime);
        }

        Destroy(gameObject, 0.05f);
    }
}