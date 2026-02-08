using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Laser beam projectile that fires continuously while holding the screen
/// Follows the finger/mouse position and stops when released
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class LaserBeamProjectile : MonoBehaviour
{
    [Header("Motion")]
    [Tooltip("Speed of the laser projectile")]
    [SerializeField] private float speed = 25f;
    
    [Tooltip("Lifetime of the laser projectile in seconds")]
    [SerializeField] private float lifetime = 2.0f;
    
    [Tooltip("Custom spawn offset for this projectile")]
    [SerializeField] private Vector2 spawnOffset = Vector2.zero;
    
    [Tooltip("Enable collider for collision detection")]
    [SerializeField] private bool enableCollider = true;
    
    [Header("Trail Settings")]
    [Tooltip("Number of trail renderers to create")]
    public int numberOfTrails = 1;
    
    [Tooltip("Enable trail renderer")]
    [SerializeField] private bool enableTrail = true;
    
    [Tooltip("Trail lifetime in seconds")]
    [SerializeField] private float trailLifetime = 0.5f;
    
    [Tooltip("Trail width")]
    [SerializeField] private float trailWidth = 0.3f;
    
    [Tooltip("Trail start color")]
    [SerializeField] private Color trailStartColor = new Color(1f, 0.5f, 0f, 1f); // Orange
    
    [Tooltip("Trail end color")]
    [SerializeField] private Color trailEndColor = new Color(1f, 0.2f, 0f, 0f); // Transparent orange
    
    [Header("Damage Settings")]
    [Tooltip("Damage dealt to enemies")]
    [SerializeField] private float damage = 10f;
    
    [Tooltip("Damage interval (time between damage ticks)")]
    [SerializeField] private float damageInterval = 0.1f;
    
    [Tooltip("Enemy layer mask")]
    [SerializeField] private LayerMask enemyLayer;
    
    // Removed unused field
    // [Tooltip("Projectile type (Fire or Ice)")]
    // [SerializeField] private ProjectileType projectileType = ProjectileType.Fire;
    
    public enum SpriteFacing2D { Right = 0, Up = 90, Left = 180, Down = 270 }
    
    [Header("Rotation")]
    [SerializeField] private SpriteFacing2D spriteFacing = SpriteFacing2D.Right;
    [SerializeField] private float additionalRotationOffsetDeg = 0f;
    [SerializeField] private bool rotateToVelocity = true;
    [SerializeField] private float maxRotationDegreesPerSecond = 1080f;
    [SerializeField] private float minRotateVelocity = 0.01f;
    [SerializeField] private bool keepInitialRotation = false;
    
    [Header("Visual Settings")]
    [Tooltip("Scale multiplier")]
    [SerializeField] private float scaleMultiplier = 1f;
    
    [Header("Impact VFX")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private float hitEffectDuration = 1.0f;
    
    [Header("Impact Orientation")]
    [SerializeField] private ImpactOrientationMode impactOrientation = ImpactOrientationMode.SurfaceNormal;
    [SerializeField] private float impactZOffset = 0f;
    [SerializeField] private bool parentImpactToHit = false;
    
    [Header("Audio - Impact")]
    [SerializeField] private AudioClip impactClip;
    [Range(0f, 1f)][SerializeField] private float impactVolume = 1f;
    
    [Header("Audio - Trail")]
    [SerializeField] public AudioClip trailClip;
    [Range(0f, 1f)][SerializeField] public float trailVolume = 0.85f;
    [SerializeField] public float trailPitch = 1.0f;
    [SerializeField] public bool trailLoop = true;
    [Tooltip("1 = fully 3D, 0 = 2D UI-like.")]
    [Range(0f, 1f)][SerializeField] public float trailSpatialBlend = 1f;
    [Tooltip("Reduce for arcade feel; 0 turns off Doppler effect.")]
    [SerializeField] public float trailDopplerLevel = 0f;
    [Tooltip("Fade-out time when the projectile ends.")]
    [SerializeField] public float trailFadeOutSeconds = 0.12f;
    
    public enum ImpactOrientationMode
    {
        SurfaceNormal,
        Opposite,
        ProjectileVelocity,
        None
    }
    
    // Private variables
    private Rigidbody2D rb;
    private List<TrailRenderer> trailRenderers = new List<TrailRenderer>();
    private Collider2D projectileCollider;
    private Vector2 direction;
    private float lifeTimer = 0f;
    private Dictionary<Collider2D, float> lastDamageTimes = new Dictionary<Collider2D, float>();
    private bool isActive = false;
    private AudioSource _trailSource;
    private Coroutine _fadeOutRoutine;
    private PlayerStats cachedPlayerStats;
    
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        projectileCollider = GetComponent<Collider2D>();
        
        // Configure Rigidbody2D
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        
        // Setup collider
        if (projectileCollider != null)
        {
            projectileCollider.isTrigger = true;
            projectileCollider.enabled = enableCollider;
        }

        cachedPlayerStats = FindObjectOfType<PlayerStats>();
        
        // Setup trail renderer
        if (enableTrail)
        {
            SetupTrailRenderer();
        }
        
        // Apply scale
        transform.localScale = Vector3.one * scaleMultiplier;
    }
    
    private void SetupTrailRenderer()
    {
        // Clear existing trails
        trailRenderers.Clear();
        
        // Create multiple trail renderers if needed
        for (int i = 0; i < numberOfTrails; i++)
        {
            GameObject trailObj = new GameObject($"Trail_{i}");
            trailObj.transform.SetParent(transform);
            trailObj.transform.localPosition = Vector3.zero;
            
            TrailRenderer trail = trailObj.AddComponent<TrailRenderer>();
            trail.time = trailLifetime;
            trail.startWidth = trailWidth;
            trail.endWidth = trailWidth * 0.1f;
            trail.startColor = trailStartColor;
            trail.endColor = trailEndColor;
            trail.material = new Material(Shader.Find("Sprites/Default"));
            trail.sortingOrder = -1; // Behind the projectile
            trail.numCornerVertices = 5;
            trail.numCapVertices = 5;
            trail.minVertexDistance = 0.1f;
            
            trailRenderers.Add(trail);
        }
        
        // Setup audio
        EnsureTrailAudioSource();
    }
    
    private void Update()
    {
        if (!isActive) return;
        
        // Update lifetime
        lifeTimer += GameStateManager.GetPauseSafeDeltaTime();
        if (lifeTimer >= lifetime)
        {
            DestroyLaser();
            return;
        }
        
        // Stick to player position (follow player)
        if (AdvancedPlayerController.Instance != null)
        {
            transform.position = AdvancedPlayerController.Instance.transform.position;
        }
        
        // Handle rotation to face direction
        if (!keepInitialRotation && rotateToVelocity)
        {
            float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float facingCorrection = (int)spriteFacing;
            float desired = targetAngle + facingCorrection + additionalRotationOffsetDeg;
            
            float current = transform.eulerAngles.z;
            float step = maxRotationDegreesPerSecond * GameStateManager.GetPauseSafeDeltaTime();
            float newAngle = Mathf.MoveTowardsAngle(current, desired, step);
            transform.rotation = Quaternion.Euler(0f, 0f, newAngle);
        }
    }
    
    /// <summary>
    /// Get the spawn offset for this projectile type
    /// </summary>
    public Vector2 GetSpawnOffset()
    {
        return spawnOffset;
    }
    
    /// <summary>
    /// Initialize the laser with direction (sticks to player position)
    /// </summary>
    public void Initialize(Vector2 fireDirection)
    {
        direction = fireDirection.normalized;
        isActive = true;
        
        // Position at player (will follow in Update)
        if (AdvancedPlayerController.Instance != null)
        {
            transform.position = AdvancedPlayerController.Instance.transform.position;
        }
        
        // Set rotation to face direction
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
        
        // Start trail audio
        StartTrailSfx();
        
        Debug.Log($"<color=orange>Laser initialized! Direction: {direction}, Angle: {angle:F1}° (follows player)</color>");
    }
    
    /// <summary>
    /// Update the laser direction (for following finger movement)
    /// </summary>
    public void UpdateDirection(Vector2 newDirection)
    {
        direction = newDirection.normalized;
        
        // Update rotation
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }
    
    /// <summary>
    /// Stop the laser and destroy it
    /// </summary>
    public void StopLaser()
    {
        isActive = false;
        DestroyLaser();
    }
    
    private void DestroyLaser()
    {
        // Disable trails so they fade out naturally
        foreach (var trail in trailRenderers)
        {
            if (trail != null)
            {
                trail.emitting = false;
            }
        }
        
        StopTrailSfx(false);
        PauseSafeSelfDestruct.Schedule(gameObject, trailLifetime); // Wait for trail to fade
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isActive) return;
        Transform t = other != null ? other.transform : null;
        while (t != null)
        {
            if (t.name == "ClickHitbox")
            {
                return;
            }
            t = t.parent;
        }
        ApplyDamage(other);
    }
    
    private void OnTriggerStay2D(Collider2D other)
    {
        if (!isActive) return;
        Transform t = other != null ? other.transform : null;
        while (t != null)
        {
            if (t.name == "ClickHitbox")
            {
                return;
            }
            t = t.parent;
        }
        ApplyDamage(other);
    }
    
    private void ApplyDamage(Collider2D other)
    {
        // Check if it's an enemy
        if (((1 << other.gameObject.layer) & enemyLayer) == 0)
        {
            return; // Not an enemy
        }
        
        // Check damage cooldown for this specific enemy
        if (lastDamageTimes.ContainsKey(other))
        {
            if (GameStateManager.PauseSafeTime - lastDamageTimes[other] < damageInterval)
            {
                return; // Too soon to damage this enemy again
            }
        }
        
        // Try to damage the enemy
        IDamageable damageable = other.GetComponent<IDamageable>();
        if (damageable != null)
        {
            Vector2 hitPoint = other.ClosestPoint(transform.position);
            Vector2 hitNormal = (hitPoint - (Vector2)transform.position).normalized;

            Component damageableComponent = damageable as Component;
            GameObject enemyObject = damageableComponent != null ? damageableComponent.gameObject : other.gameObject;

            float finalDamage = damage;

            if (cachedPlayerStats != null)
            {
                finalDamage = PlayerDamageHelper.ComputeProjectileDamage(cachedPlayerStats, enemyObject, damage, gameObject);
            }

            // Tag EnemyHealth so laser hits always use a consistent, generic
            // Player damage color instead of inheriting whatever elemental
            // color last hit this enemy.
            if (enemyObject != null)
            {
                EnemyHealth enemyHealth = enemyObject.GetComponent<EnemyHealth>() ?? enemyObject.GetComponentInParent<EnemyHealth>();
                if (enemyHealth != null)
                {
                    enemyHealth.SetLastIncomingDamageType(DamageNumberManager.DamageType.Player);
                }
            }

            damageable.TakeDamage(finalDamage, hitPoint, hitNormal);

            // Update last damage time for this enemy
            lastDamageTimes[other] = GameStateManager.PauseSafeTime;

            Debug.Log($"<color=orange>Laser dealt {finalDamage} damage to {other.gameObject.name}</color>");
        }
    }
    
    private void HandleImpact(Vector3 point, Vector3 normal, Transform hitParent)
    {
        if (hitEffectPrefab != null)
        {
            Quaternion rotation = ComputeImpactRotation(normal);
            Vector3 position = new Vector3(point.x, point.y, impactZOffset);
            GameObject vfx = Instantiate(hitEffectPrefab, position, rotation);
            
            if (parentImpactToHit && hitParent != null)
            {
                vfx.transform.SetParent(hitParent, true);
            }
            
            if (hitEffectDuration > 0f)
            {
                PauseSafeSelfDestruct.Schedule(vfx, hitEffectDuration);
            }
        }
        
        if (impactClip != null)
        {
            AudioSource.PlayClipAtPoint(impactClip, point, impactVolume);
        }
        
        StopTrailSfx(false);
    }
    
    private Quaternion ComputeImpactRotation(Vector3 surfaceNormal)
    {
        switch (impactOrientation)
        {
            case ImpactOrientationMode.SurfaceNormal:
                return Quaternion.LookRotation(Vector3.forward, surfaceNormal);
            case ImpactOrientationMode.Opposite:
                return Quaternion.LookRotation(Vector3.forward, -surfaceNormal);
            case ImpactOrientationMode.ProjectileVelocity:
                Vector2 v = rb != null ? rb.velocity : Vector2.right;
                if (v.sqrMagnitude < 0.0001f) v = Vector2.right;
                return Quaternion.LookRotation(Vector3.forward, v.normalized);
            case ImpactOrientationMode.None:
            default:
                return Quaternion.identity;
        }
    }
    
    private void EnsureTrailAudioSource()
    {
        if (_trailSource == null)
        {
            _trailSource = GetComponent<AudioSource>();
            if (_trailSource == null)
            {
                _trailSource = gameObject.AddComponent<AudioSource>();
            }
        }
        
        _trailSource.playOnAwake = false;
        _trailSource.loop = trailLoop;
        _trailSource.spatialBlend = trailSpatialBlend;
        _trailSource.dopplerLevel = trailDopplerLevel;
        _trailSource.rolloffMode = AudioRolloffMode.Linear;
        _trailSource.minDistance = 1f;
        _trailSource.maxDistance = 30f;
    }
    
    private void StartTrailSfx()
    {
        if (trailClip == null) return;
        
        EnsureTrailAudioSource();
        
        if (_fadeOutRoutine != null)
        {
            StopCoroutine(_fadeOutRoutine);
            _fadeOutRoutine = null;
        }
        
        _trailSource.clip = trailClip;
        _trailSource.volume = trailVolume;
        _trailSource.pitch = trailPitch;
        _trailSource.loop = trailLoop;
        
        if (!_trailSource.isPlaying)
        {
            _trailSource.Play();
        }
    }
    
    private void StopTrailSfx(bool instant)
    {
        if (_trailSource == null) return;
        
        if (instant || trailFadeOutSeconds <= 0f || !_trailSource.isPlaying)
        {
            _trailSource.Stop();
            _trailSource.clip = null;
            return;
        }
        
        if (_fadeOutRoutine != null)
        {
            StopCoroutine(_fadeOutRoutine);
        }
        _fadeOutRoutine = StartCoroutine(FadeOutAndStop(_trailSource, trailFadeOutSeconds));
    }
    
    private IEnumerator FadeOutAndStop(AudioSource source, float duration)
    {
        float startVolume = source.volume;
        float t = 0f;
        
        while (t < duration && source != null)
        {
            t += GameStateManager.GetPauseSafeDeltaTime();
            float k = Mathf.Clamp01(1f - (t / duration));
            source.volume = startVolume * k;
            yield return null;
        }
        
        if (source != null)
        {
            source.Stop();
            source.clip = null;
            source.volume = startVolume;
        }
        
        _fadeOutRoutine = null;
    }
    
    private void OnDestroy()
    {
        lastDamageTimes.Clear();
    }
}
