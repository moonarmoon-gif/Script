using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class HellBeam : MonoBehaviour, IInstantModifiable
{
    [Header("Animation")]
    public float StartAnimationTime = 0.5f;
    public float EndAnimationTime = 2f;

    [Header("Damage")]
    public float DamageTickInterval = 0.25f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private ProjectileType projectileType = ProjectileType.Nuclear;

    [Header("Spawn Area - 6 Point System")]
    [SerializeField] private string pointATag = "HellBeam_PointA";
    [SerializeField] private string pointBTag = "HellBeam_PointB";
    [SerializeField] private string pointCTag = "HellBeam_PointC";
    [SerializeField] private string pointDTag = "HellBeam_PointD";
    [SerializeField] private string pointETag = "HellBeam_PointE";
    [SerializeField] private string pointFTag = "HellBeam_PointF";

    [Header("Collider Scaling")]
    [SerializeField] private float colliderSizeOffset = 0f;

    [Header("Mana")]
    [SerializeField] private int manaCost = 30;

    public int MinOrderInlayer = 201;
    public int MaxOrderInLayer = 300;

    private Transform pointA;
    private Transform pointB;
    private Transform pointC;
    private Transform pointD;
    private Transform pointE;
    private Transform pointF;

    private Collider2D _collider2D;
    private Animator[] _animators;
    private float[] _animatorDefaultSpeeds;
    private bool _animatorsPaused;
    private GameObject fireBlossomObject;

    private float baseDamage;
    private float baseLifetime;
    private Vector3 baseScale;

    private float currentDamage;
    private float currentLifetime;

    private bool isLooping;
    private HashSet<Collider2D> overlapped = new HashSet<Collider2D>();

    private PlayerStats cachedPlayerStats;

    private Coroutine lifecycleCoroutine;

    private static Dictionary<string, float> lastFireTimes = new Dictionary<string, float>();
    private string prefabKey;

    private static int activeSortingBeamCount = 0;

    private void Awake()
    {
        _collider2D = GetComponent<Collider2D>();
        _animators = GetComponentsInChildren<Animator>(true);
        _animatorDefaultSpeeds = null;
        if (_animators != null && _animators.Length > 0)
        {
            _animatorDefaultSpeeds = new float[_animators.Length];
            for (int i = 0; i < _animators.Length; i++)
            {
                Animator a = _animators[i];
                _animatorDefaultSpeeds[i] = a != null ? a.speed : 1f;
            }
        }
        CacheFireBlossom();
        baseScale = transform.localScale;

        Collider2D[] allColliders = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < allColliders.Length; i++)
        {
            if (allColliders[i] != null)
            {
                allColliders[i].isTrigger = true;
            }
        }

        if (_collider2D != null)
        {
            _collider2D.enabled = false;
        }

        SetAnimatorBoolAll("Loop", false);
        SetAnimatorBoolAll("End", false);
        SetFireBlossomActive(false);

        SyncChildSpriteSortPoints();
    }

    private void OnEnable()
    {
        activeSortingBeamCount += 1;
        ForceStopLoopingState();
        SetAnimatorBoolAll("End", false);
        UpdateAnimatorPauseState();

        SyncChildSpriteSortPoints();
    }

    private void Update()
    {
        UpdateAnimatorPauseState();
    }

    private void OnDisable()
    {
        activeSortingBeamCount = Mathf.Max(0, activeSortingBeamCount - 1);

        if (lifecycleCoroutine != null)
        {
            StopCoroutine(lifecycleCoroutine);
            lifecycleCoroutine = null;
        }

        ForceStopLoopingState();
        SetAnimatorBoolAll("End", false);
    }

    private void UpdateAnimatorPauseState()
    {
        bool shouldPause = GameStateManager.GetPauseSafeDeltaTime() <= 0f;
        if (shouldPause == _animatorsPaused)
        {
            return;
        }
        _animatorsPaused = shouldPause;

        if (_animators == null)
        {
            return;
        }

        for (int i = 0; i < _animators.Length; i++)
        {
            Animator a = _animators[i];
            if (a == null)
            {
                continue;
            }

            if (shouldPause)
            {
                a.speed = 0f;
            }
            else
            {
                float defaultSpeed = 1f;
                if (_animatorDefaultSpeeds != null && i >= 0 && i < _animatorDefaultSpeeds.Length)
                {
                    defaultSpeed = _animatorDefaultSpeeds[i];
                }
                a.speed = defaultSpeed;
            }
        }
    }

    private void ForceStopLoopingState()
    {
        isLooping = false;
        SetFireBlossomActive(false);

        if (_collider2D != null)
        {
            _collider2D.enabled = false;
        }

        SetAnimatorBoolAll("Loop", false);
    }

    private void ApplySortingOrderForBeam()
    {
        SpriteRenderer[] sprites = GetComponentsInChildren<SpriteRenderer>(true);
        if (sprites == null || sprites.Length == 0)
        {
            return;
        }

        int instanceBaseOrder = sprites[0].sortingOrder;
        for (int i = 1; i < sprites.Length; i++)
        {
            if (sprites[i] != null && sprites[i].sortingOrder < instanceBaseOrder)
            {
                instanceBaseOrder = sprites[i].sortingOrder;
            }
        }

        int minOrder = Mathf.Min(MinOrderInlayer, MaxOrderInLayer);
        int maxOrder = Mathf.Max(MinOrderInlayer, MaxOrderInLayer);

        int desiredBaseOrder = Mathf.Clamp(minOrder + Mathf.Max(0, activeSortingBeamCount - 1), minOrder, maxOrder);

        int delta = desiredBaseOrder - instanceBaseOrder;
        if (delta == 0)
        {
            return;
        }

        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null)
            {
                sprites[i].sortingOrder += delta;
            }
        }
    }

    public void Initialize(Vector3 spawnPosition, Collider2D playerCollider, float configuredBaseDamage, float configuredBaseLifetime, float configuredTickInterval, bool skipCooldownCheck = false)
    {
        baseDamage = configuredBaseDamage;
        baseLifetime = configuredBaseLifetime;
        DamageTickInterval = configuredTickInterval;

        ForceStopLoopingState();
        SetAnimatorBoolAll("End", false);

        ProjectileCards card = ProjectileCardModifiers.Instance != null
            ? ProjectileCardModifiers.Instance.GetCardFromProjectile(gameObject)
            : null;

        CardModifierStats modifiers = new CardModifierStats();
        if (card != null && ProjectileCardModifiers.Instance != null)
        {
            modifiers = ProjectileCardModifiers.Instance.GetCardModifiers(card);
        }

        float baseCooldown = card != null
            ? (card.runtimeSpawnInterval > 0f ? card.runtimeSpawnInterval : card.spawnInterval)
            : 0f;

        if (card != null)
        {
            card.runtimeSpawnInterval = Mathf.Max(0.0001f, baseCooldown);
        }

        float finalCooldown = baseCooldown > 0f
            ? baseCooldown * (1f - modifiers.cooldownReductionPercent / 100f)
            : 0f;

        if (card != null)
        {
            if (MinCooldownManager.Instance != null)
            {
                finalCooldown = MinCooldownManager.Instance.ClampCooldown(card, finalCooldown);
            }
            else
            {
                finalCooldown = Mathf.Max(0.1f, finalCooldown);
            }
        }

        int finalManaCost = Mathf.Max(1, Mathf.CeilToInt(manaCost * (1f - modifiers.manaCostReduction)));

        cachedPlayerStats = null;
        if (playerCollider != null)
        {
            cachedPlayerStats = playerCollider.GetComponent<PlayerStats>();
        }
        if (cachedPlayerStats == null)
        {
            cachedPlayerStats = FindObjectOfType<PlayerStats>();
        }

        float effectiveCooldown = finalCooldown;
        if (cachedPlayerStats != null && cachedPlayerStats.projectileCooldownReduction > 0f)
        {
            float totalCdr = Mathf.Max(0f, cachedPlayerStats.projectileCooldownReduction);
            effectiveCooldown = finalCooldown / (1f + totalCdr);
            if (MinCooldownManager.Instance != null && card != null)
            {
                effectiveCooldown = MinCooldownManager.Instance.ClampCooldown(card, effectiveCooldown);
            }
            else
            {
                effectiveCooldown = Mathf.Max(0.1f, effectiveCooldown);
            }
        }

        bool bypassEnhancedFirstSpawnCooldown = false;
        if (!skipCooldownCheck && card != null && card.applyEnhancedFirstSpawnReduction && card.pendingEnhancedFirstSpawn)
        {
            if (card.projectileSystem == ProjectileCards.ProjectileSystemType.Passive)
            {
                bypassEnhancedFirstSpawnCooldown = true;
                card.pendingEnhancedFirstSpawn = false;
            }
        }

        prefabKey = "HellBeam";

        if (!skipCooldownCheck && baseCooldown > 0f)
        {
            if (!bypassEnhancedFirstSpawnCooldown && lastFireTimes.ContainsKey(prefabKey))
            {
                if (GameStateManager.PauseSafeTime - lastFireTimes[prefabKey] < effectiveCooldown)
                {
                    Destroy(gameObject);
                    return;
                }
            }

            PlayerMana playerMana = FindObjectOfType<PlayerMana>();
            if (playerMana != null && !playerMana.Spend(finalManaCost))
            {
                Destroy(gameObject);
                return;
            }

            lastFireTimes[prefabKey] = GameStateManager.PauseSafeTime;
        }

        FindSpawnAreaPoints();
        ApplySpawnPosition(spawnPosition);

        ApplySortingOrderForBeam();
        SyncChildSpriteSortPoints();

        currentDamage = (baseDamage + modifiers.damageFlat) * modifiers.damageMultiplier;
        currentLifetime = Mathf.Max(0f, baseLifetime + modifiers.lifetimeIncrease);

        if (modifiers.sizeMultiplier != 1f)
        {
            transform.localScale = baseScale * modifiers.sizeMultiplier;
            ColliderScaler.ScaleCollider(_collider2D, modifiers.sizeMultiplier, colliderSizeOffset);
        }

        if (playerCollider != null)
        {
            Collider2D[] cols = GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null)
                {
                    Physics2D.IgnoreCollision(cols[i], playerCollider, true);
                }
            }
        }

        float totalLifetime = Mathf.Max(0f, StartAnimationTime) + Mathf.Max(0f, currentLifetime) + Mathf.Max(0f, EndAnimationTime) + 0.1f;
        PauseSafeSelfDestruct.Schedule(gameObject, totalLifetime);

        if (lifecycleCoroutine != null)
        {
            StopCoroutine(lifecycleCoroutine);
            lifecycleCoroutine = null;
        }

        lifecycleCoroutine = StartCoroutine(LifecycleRoutine());
    }

    private void SyncChildSpriteSortPoints()
    {
        SpriteRenderer parentRenderer = GetComponent<SpriteRenderer>();
        if (parentRenderer == null)
        {
            return;
        }

        SpriteSortPoint desired = parentRenderer.spriteSortPoint;
        SpriteRenderer[] sprites = GetComponentsInChildren<SpriteRenderer>(true);
        if (sprites == null)
        {
            return;
        }

        for (int i = 0; i < sprites.Length; i++)
        {
            SpriteRenderer sr = sprites[i];
            if (sr == null || sr == parentRenderer)
            {
                continue;
            }

            sr.spriteSortPoint = desired;
        }
    }

    private IEnumerator LifecycleRoutine()
    {
        float startTime = Mathf.Max(0f, StartAnimationTime);
        if (startTime > 0f)
        {
            yield return GameStateManager.WaitForPauseSafeSeconds(startTime);
        }

        isLooping = true;
        SetAnimatorBoolAll("Loop", true);
        SetFireBlossomActive(true);

        if (_collider2D != null)
        {
            _collider2D.enabled = true;
        }

        PopulateInitialOverlaps();

        float tickInterval = Mathf.Max(0.01f, DamageTickInterval);
        float now = GameStateManager.PauseSafeTime;
        float endAt = now + Mathf.Max(0f, currentLifetime);
        float nextTickAt = now;

        while (GameStateManager.PauseSafeTime < endAt)
        {
            float t = GameStateManager.PauseSafeTime;
            if (t >= nextTickAt)
            {
                DealTickDamage();
                nextTickAt = t + tickInterval;
            }

            yield return null;
        }

        isLooping = false;
        SetFireBlossomActive(false);

        if (_collider2D != null)
        {
            _collider2D.enabled = false;
        }

        SetAnimatorBoolAll("Loop", false);
        SetAnimatorBoolAll("End", true);

        float endTime = Mathf.Max(0f, EndAnimationTime);
        if (endTime > 0f)
        {
            yield return GameStateManager.WaitForPauseSafeSeconds(endTime);
        }

        lifecycleCoroutine = null;
        Destroy(gameObject);
    }

    private void DealTickDamage()
    {
        if (!isLooping)
        {
            return;
        }

        if (overlapped.Count == 0)
        {
            return;
        }

        List<Collider2D> toRemove = null;

        foreach (Collider2D col in overlapped)
        {
            if (col == null)
            {
                if (toRemove == null)
                {
                    toRemove = new List<Collider2D>();
                }
                toRemove.Add(col);
                continue;
            }

            if (((1 << col.gameObject.layer) & enemyLayer) == 0)
            {
                continue;
            }

            if (!OffscreenDamageChecker.CanTakeDamage(col.transform.position))
            {
                continue;
            }

            IDamageable damageable = col.GetComponent<IDamageable>() ?? col.GetComponentInParent<IDamageable>();
            if (damageable == null || !damageable.IsAlive)
            {
                continue;
            }

            Component dmgComp = damageable as Component;
            GameObject enemyObject = dmgComp != null ? dmgComp.gameObject : col.gameObject;

            float finalDamage = currentDamage;
            if (cachedPlayerStats != null && enemyObject != null)
            {
                finalDamage = PlayerDamageHelper.ComputeProjectileDamage(cachedPlayerStats, enemyObject, currentDamage, gameObject);
            }

            if (enemyObject != null)
            {
                EnemyHealth enemyHealth = enemyObject.GetComponent<EnemyHealth>() ?? enemyObject.GetComponentInParent<EnemyHealth>();
                if (enemyHealth != null)
                {
                    DamageNumberManager.DamageType damageType = projectileType == ProjectileType.Fire
                        ? DamageNumberManager.DamageType.Fire
                        : (projectileType == ProjectileType.Thunder || projectileType == ProjectileType.ThunderDisc
                            ? DamageNumberManager.DamageType.Thunder
                            : DamageNumberManager.DamageType.Ice);
                    enemyHealth.SetLastIncomingDamageType(damageType);
                }
            }

            Vector3 hitPoint = col.ClosestPoint(transform.position);
            Vector3 hitNormal = (col.transform.position - transform.position).normalized;

            damageable.TakeDamage(finalDamage, hitPoint, hitNormal);
        }

        if (toRemove != null)
        {
            for (int i = 0; i < toRemove.Count; i++)
            {
                overlapped.Remove(toRemove[i]);
            }
        }
    }

    private void PopulateInitialOverlaps()
    {
        if (_collider2D == null)
        {
            return;
        }

        ContactFilter2D filter = new ContactFilter2D();
        filter.useLayerMask = true;
        filter.layerMask = enemyLayer;
        filter.useTriggers = true;

        List<Collider2D> results = new List<Collider2D>(32);
        int count = _collider2D.OverlapCollider(filter, results);
        for (int i = 0; i < count; i++)
        {
            Collider2D c = results[i];
            if (c != null)
            {
                overlapped.Add(c);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isLooping)
        {
            return;
        }

        if (other == null)
        {
            return;
        }

        overlapped.Add(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        overlapped.Remove(other);
    }

    public void ApplyInstantModifiers(CardModifierStats modifiers)
    {
        currentDamage = (baseDamage + modifiers.damageFlat) * modifiers.damageMultiplier;
        currentLifetime = Mathf.Max(0f, baseLifetime + modifiers.lifetimeIncrease);

        transform.localScale = baseScale;

        if (modifiers.sizeMultiplier != 1f)
        {
            transform.localScale = baseScale * modifiers.sizeMultiplier;
            ColliderScaler.ScaleCollider(_collider2D, modifiers.sizeMultiplier, colliderSizeOffset);
        }
    }

    private void SetAnimatorBoolAll(string param, bool value)
    {
        if (_animators == null)
        {
            return;
        }

        for (int i = 0; i < _animators.Length; i++)
        {
            Animator a = _animators[i];
            if (a != null)
            {
                a.SetBool(param, value);
            }
        }
    }

    private void FindSpawnAreaPoints()
    {
        if (!string.IsNullOrEmpty(pointATag))
        {
            GameObject o = GameObject.FindGameObjectWithTag(pointATag);
            if (o != null) pointA = o.transform;
        }

        if (!string.IsNullOrEmpty(pointBTag))
        {
            GameObject o = GameObject.FindGameObjectWithTag(pointBTag);
            if (o != null) pointB = o.transform;
        }

        if (!string.IsNullOrEmpty(pointCTag))
        {
            GameObject o = GameObject.FindGameObjectWithTag(pointCTag);
            if (o != null) pointC = o.transform;
        }

        if (!string.IsNullOrEmpty(pointDTag))
        {
            GameObject o = GameObject.FindGameObjectWithTag(pointDTag);
            if (o != null) pointD = o.transform;
        }

        if (!string.IsNullOrEmpty(pointETag))
        {
            GameObject o = GameObject.FindGameObjectWithTag(pointETag);
            if (o != null) pointE = o.transform;
        }

        if (!string.IsNullOrEmpty(pointFTag))
        {
            GameObject o = GameObject.FindGameObjectWithTag(pointFTag);
            if (o != null) pointF = o.transform;
        }
    }

    private void ApplySpawnPosition(Vector3 fallback)
    {
        List<Vector2> poly = new List<Vector2>(6);
        if (pointA != null) poly.Add(pointA.position);
        if (pointB != null) poly.Add(pointB.position);
        if (pointC != null) poly.Add(pointC.position);
        if (pointD != null) poly.Add(pointD.position);
        if (pointE != null) poly.Add(pointE.position);
        if (pointF != null) poly.Add(pointF.position);

        if (poly.Count < 3)
        {
            transform.position = fallback;
            return;
        }

        float minX = poly[0].x;
        float maxX = poly[0].x;
        float minY = poly[0].y;
        float maxY = poly[0].y;

        for (int i = 1; i < poly.Count; i++)
        {
            Vector2 v = poly[i];
            if (v.x < minX) minX = v.x;
            if (v.x > maxX) maxX = v.x;
            if (v.y < minY) minY = v.y;
            if (v.y > maxY) maxY = v.y;
        }

        Vector3 finalPosition = fallback;
        bool found = false;
        int maxAttempts = 20;

        HellBeam[] allBeams = FindObjectsOfType<HellBeam>();

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            float x = Random.Range(minX, maxX);
            float y = Random.Range(minY, maxY);
            Vector2 p = new Vector2(x, y);

            if (!IsPointInsidePolygon(p, poly))
            {
                continue;
            }

            finalPosition = new Vector3(x, y, fallback.z);

            Vector3 previousPosition = transform.position;
            transform.position = finalPosition;

            bool overlapsOtherBeam = false;
            Bounds myBounds = GetWorldSpriteBounds();
            for (int i = 0; i < allBeams.Length; i++)
            {
                HellBeam other = allBeams[i];
                if (other == null || other == this)
                {
                    continue;
                }

                Bounds otherBounds = other.GetWorldSpriteBounds();
                if (myBounds.Intersects(otherBounds))
                {
                    overlapsOtherBeam = true;
                    break;
                }
            }

            if (overlapsOtherBeam)
            {
                transform.position = previousPosition;
                continue;
            }

            transform.position = previousPosition;
            found = true;
            break;
        }

        if (!found)
        {
            int relaxedAttempts = 30;
            for (int attempt = 0; attempt < relaxedAttempts; attempt++)
            {
                float x = Random.Range(minX, maxX);
                float y = Random.Range(minY, maxY);
                Vector2 p = new Vector2(x, y);

                if (!IsPointInsidePolygon(p, poly))
                {
                    continue;
                }

                finalPosition = new Vector3(x, y, fallback.z);
                found = true;
                break;
            }
        }

        if (!found)
        {
            finalPosition = fallback;
        }

        transform.position = finalPosition;
    }

    private Bounds GetWorldSpriteBounds()
    {
        SpriteRenderer[] sprites = GetComponentsInChildren<SpriteRenderer>(true);
        if (sprites != null && sprites.Length > 0)
        {
            Bounds b = sprites[0].bounds;
            for (int i = 1; i < sprites.Length; i++)
            {
                if (sprites[i] != null)
                {
                    b.Encapsulate(sprites[i].bounds);
                }
            }
            return b;
        }

        if (_collider2D != null)
        {
            return _collider2D.bounds;
        }

        return new Bounds(transform.position, Vector3.one * 0.01f);
    }

    private bool IsPointInsidePolygon(Vector2 point, List<Vector2> polygon)
    {
        int count = polygon.Count;
        if (count < 3)
        {
            return false;
        }

        bool inside = false;
        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            Vector2 pi = polygon[i];
            Vector2 pj = polygon[j];

            bool intersect = ((pi.y > point.y) != (pj.y > point.y)) &&
                             (point.x < (pj.x - pi.x) * (point.y - pi.y) / (((pj.y - pi.y) == 0f) ? 0.0001f : (pj.y - pi.y)) + pi.x);

            if (intersect)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private void OnDrawGizmosSelected()
    {
        GameObject aObj = GameObject.FindGameObjectWithTag(pointATag);
        GameObject bObj = GameObject.FindGameObjectWithTag(pointBTag);
        GameObject cObj = GameObject.FindGameObjectWithTag(pointCTag);
        GameObject dObj = GameObject.FindGameObjectWithTag(pointDTag);
        GameObject eObj = null;
        GameObject fObj = null;

        if (!string.IsNullOrEmpty(pointETag))
        {
            eObj = GameObject.FindGameObjectWithTag(pointETag);
        }

        if (!string.IsNullOrEmpty(pointFTag))
        {
            fObj = GameObject.FindGameObjectWithTag(pointFTag);
        }

        List<Vector3> spawnPoints = new List<Vector3>(6);
        if (aObj != null) spawnPoints.Add(aObj.transform.position);
        if (bObj != null) spawnPoints.Add(bObj.transform.position);
        if (cObj != null) spawnPoints.Add(cObj.transform.position);
        if (dObj != null) spawnPoints.Add(dObj.transform.position);
        if (eObj != null) spawnPoints.Add(eObj.transform.position);
        if (fObj != null) spawnPoints.Add(fObj.transform.position);

        if (spawnPoints.Count < 3)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.9f);
        for (int i = 0; i < spawnPoints.Count; i++)
        {
            Gizmos.DrawSphere(spawnPoints[i], 0.25f);
        }

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.7f);
        for (int i = 0; i < spawnPoints.Count; i++)
        {
            Vector3 from = spawnPoints[i];
            Vector3 to = spawnPoints[(i + 1) % spawnPoints.Count];
            Gizmos.DrawLine(from, to);
        }

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.25f);
        if (spawnPoints.Count >= 4)
        {
            Gizmos.DrawLine(spawnPoints[0], spawnPoints[2 % spawnPoints.Count]);
            Gizmos.DrawLine(spawnPoints[1 % spawnPoints.Count], spawnPoints[3 % spawnPoints.Count]);
        }
    }

    private void CacheFireBlossom()
    {
        fireBlossomObject = null;
        Transform[] all = GetComponentsInChildren<Transform>(true);
        if (all == null)
        {
            return;
        }
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == "FireBlossom")
            {
                fireBlossomObject = all[i].gameObject;
                return;
            }
        }
    }

    private void SetFireBlossomActive(bool active)
    {
        if (fireBlossomObject == null)
        {
            return;
        }
        if (fireBlossomObject.activeSelf != active)
        {
            fireBlossomObject.SetActive(active);
        }
    }
}
