using System;
using System.Collections;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class EnemyStatUIPanel : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("UI")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image healthIcon;
    [SerializeField] private Image attackIcon;
    [SerializeField] private Image defenseIcon;
    [SerializeField] private Image moveSpeedIcon;

    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private TextMeshProUGUI attackText;
    [SerializeField] private TextMeshProUGUI defenseText;
    [SerializeField] private TextMeshProUGUI moveSpeedText;

    [SerializeField] private Button closeButton;

    [Header("Rarity Background Colors")]
    [SerializeField] private Color commonBackgroundColor = new Color(0.7f, 0.7f, 0.7f);
    [SerializeField] private Color uncommonBackgroundColor = new Color(0.2f, 1f, 0.2f);
    [SerializeField] private Color rareBackgroundColor = new Color(0.3f, 0.5f, 1f);
    [SerializeField] private Color epicBackgroundColor = new Color(0.7f, 0.3f, 1f);
    [SerializeField] private Color legendaryBackgroundColor = new Color(1f, 0.6f, 0f);
    [SerializeField] private Color mythicBackgroundColor = new Color(1f, 0.2f, 0.2f);
    [SerializeField] private Color bossBackgroundColor = new Color(1f, 0.84f, 0f);

    private EnemyHealth enemyHealth;
    private StatusController statusController;
    private SpriteRenderer spriteRenderer;
    private Collider2D targetCollider;
    private MonoBehaviour[] cachedBehaviours;
    private Rigidbody2D enemyRb;
    private Animator[] enemyAnimators;

    private EnemyStatUIManager manager;
    private Camera worldCamera;

    private RectTransform rectTransform;
    private Canvas canvas;
    private CanvasGroup canvasGroup;

    private Coroutine revealRoutine;

    private float cachedBaseAttack;
    private float cachedBaseMoveSpeed;
    private bool hasBaseAttack;
    private bool hasBaseMoveSpeed;

    private bool isManuallyUnbound;
    private bool isDragging;
    private Vector2 dragStartAnchoredPosition;
    private Vector2 dragStartLocalPointerPosition;
    private bool hasPositionedOnce;

    private int defaultCanvasSortingOrder;
    private int defaultCanvasSortingLayerId;
    private bool hasDefaultCanvasSorting;
    private bool isForcedBehindPlayer;

    private float nextUpdateTime;
    private bool isBound;

    private bool isTeleportHidden;
    private float teleportHiddenPrevAlpha = 1f;
    private bool teleportHiddenPrevInteractable;
    private bool teleportHiddenPrevBlocksRaycasts;

    public void Bind(EnemyHealth target, EnemyStatUIManager owner, int sortingOrder, Camera cam)
    {
        enemyHealth = target;
        manager = owner;
        worldCamera = cam;

        isManuallyUnbound = false;
        isDragging = false;
        isForcedBehindPlayer = false;
        hasPositionedOnce = false;

        isTeleportHidden = false;
        teleportHiddenPrevAlpha = 1f;
        teleportHiddenPrevInteractable = false;
        teleportHiddenPrevBlocksRaycasts = false;

        rectTransform = transform as RectTransform;
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }

        if (rectTransform != null)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }

        canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
        }
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;

        defaultCanvasSortingOrder = sortingOrder;
        defaultCanvasSortingLayerId = canvas.sortingLayerID;
        hasDefaultCanvasSorting = true;

        GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();
        if (raycaster == null)
        {
            raycaster = gameObject.AddComponent<GraphicRaycaster>();
        }

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        teleportHiddenPrevAlpha = 1f;
        teleportHiddenPrevInteractable = canvasGroup.interactable;
        teleportHiddenPrevBlocksRaycasts = canvasGroup.blocksRaycasts;

        canvasGroup.alpha = 0f;

        statusController = enemyHealth != null ? (enemyHealth.GetComponent<StatusController>() ?? enemyHealth.GetComponentInParent<StatusController>()) : null;

        cachedBehaviours = enemyHealth != null ? enemyHealth.GetComponentsInChildren<MonoBehaviour>(true) : null;

        spriteRenderer = enemyHealth != null ? (enemyHealth.GetComponent<SpriteRenderer>() ?? enemyHealth.GetComponentInChildren<SpriteRenderer>()) : null;
        targetCollider = enemyHealth != null ? (enemyHealth.GetComponent<Collider2D>() ?? enemyHealth.GetComponentInChildren<Collider2D>()) : null;
        enemyRb = enemyHealth != null ? (enemyHealth.GetComponent<Rigidbody2D>() ?? enemyHealth.GetComponentInParent<Rigidbody2D>()) : null;
        enemyAnimators = enemyHealth != null ? enemyHealth.GetComponentsInChildren<Animator>(true) : null;

        if (backgroundImage != null && enemyHealth != null)
        {
            EnemyExpData expData = enemyHealth.GetComponent<EnemyExpData>() ?? enemyHealth.GetComponentInParent<EnemyExpData>();
            if (expData == null)
            {
                Transform root = enemyHealth.transform.root;
                if (root != null)
                {
                    expData = root.GetComponentInChildren<EnemyExpData>(true);
                }
            }
            CardRarity rarity = expData != null ? expData.EnemyRarity : CardRarity.Common;
            backgroundImage.color = GetBackgroundColorForRarity(rarity);
        }

        if (manager != null)
        {
            float scale = manager.GetPanelScaleForEnemy(enemyHealth);
            if (scale > 0f)
            {
                transform.localScale = Vector3.one * scale;
            }
        }

        if (rectTransform != null)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
            Canvas.ForceUpdateCanvases();
        }

        CacheBaseStats();
        ForceRefresh();

        if (revealRoutine != null)
        {
            StopCoroutine(revealRoutine);
        }
        revealRoutine = StartCoroutine(RevealNextFrame());

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.AddListener(Close);
        }

        DisableNonCloseRaycasts();

        if (enemyHealth != null)
        {
            enemyHealth.OnDeath -= HandleEnemyDeath;
            enemyHealth.OnDeath += HandleEnemyDeath;
        }

        float interval = manager != null ? manager.PanelUpdateIntervalSeconds : 0.05f;
        nextUpdateTime = Time.unscaledTime + Mathf.Max(0f, interval);

        isBound = true;
    }

    private IEnumerator RevealNextFrame()
    {
        yield return null;

        if (rectTransform != null)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
            Canvas.ForceUpdateCanvases();
        }

        ForceRefresh();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            SetTeleportHidden(ShouldHidePanelForTeleport());
        }

        revealRoutine = null;
    }

    public void BringToFront(int sortingOrder)
    {
        if (canvas != null)
        {
            canvas.sortingOrder = sortingOrder;
        }

        defaultCanvasSortingOrder = sortingOrder;
        if (!isForcedBehindPlayer && canvas != null)
        {
            defaultCanvasSortingLayerId = canvas.sortingLayerID;
        }
        hasDefaultCanvasSorting = true;

        RefreshSortingAgainstPlayer();
        transform.SetAsLastSibling();
    }

    public void SetInteractionEnabled(bool enabled)
    {
        if (canvasGroup != null)
        {
            canvasGroup.interactable = enabled;
            canvasGroup.blocksRaycasts = enabled;
        }

        if (closeButton != null)
        {
            closeButton.interactable = enabled;
        }
    }

    private void Update()
    {
        if (!isBound)
        {
            return;
        }

        if (enemyHealth == null || !enemyHealth.IsAlive)
        {
            Close();
            return;
        }

        bool shouldHideForTeleport = ShouldHidePanelForTeleport();
        if (shouldHideForTeleport)
        {
            SetTeleportHidden(true);
            return;
        }

        if (isTeleportHidden)
        {
            SetTeleportHidden(false);
        }

        float interval = manager != null ? manager.PanelUpdateIntervalSeconds : 0.05f;
        if (interval <= 0f)
        {
            RefreshPositionAndStats();
            return;
        }

        if (Time.unscaledTime >= nextUpdateTime)
        {
            nextUpdateTime = Time.unscaledTime + Mathf.Max(0.01f, interval);
            RefreshPositionAndStats();
        }
    }

    private void RefreshPositionAndStats()
    {
        RefreshStats();
        RefreshSortingAgainstPlayer();

        if (!hasPositionedOnce)
        {
            if (!isDragging)
            {
                RefreshPosition();
                hasPositionedOnce = true;
            }
            return;
        }

        if (ShouldFollowEnemyPosition())
        {
            RefreshPosition();
        }
    }

    private bool ShouldFollowEnemyPosition()
    {
        if (isManuallyUnbound || isDragging)
        {
            return false;
        }

        if (enemyHealth == null || !enemyHealth.IsAlive)
        {
            return false;
        }

        if (enemyRb != null && enemyRb.simulated)
        {
            if (enemyRb.velocity.sqrMagnitude > 0.0001f)
            {
                return true;
            }
        }

        if (enemyAnimators != null)
        {
            for (int i = 0; i < enemyAnimators.Length; i++)
            {
                Animator anim = enemyAnimators[i];
                if (anim == null || !anim.isActiveAndEnabled)
                {
                    continue;
                }

                if (IsAnimatorMovementLikeActive(anim))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsAnimatorMovementLikeActive(Animator anim)
    {
        if (anim == null)
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = anim.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter p = parameters[i];
            if (p == null || p.type != AnimatorControllerParameterType.Bool)
            {
                continue;
            }

            string n = p.name;
            if (string.IsNullOrEmpty(n))
            {
                continue;
            }

            bool relevant =
                string.Equals(n, "moving", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(n, "movingflip", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(n, "running", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(n, "runningflip", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(n, "walk", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(n, "walkflip", StringComparison.OrdinalIgnoreCase) ||
                n.IndexOf("charge", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("teleport", StringComparison.OrdinalIgnoreCase) >= 0 ||
                string.Equals(n, "arrival", StringComparison.OrdinalIgnoreCase) ||
                n.IndexOf("arrival", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!relevant)
            {
                continue;
            }

            if (anim.GetBool(n))
            {
                return true;
            }
        }

        return false;
    }

    private bool ShouldHidePanelForTeleport()
    {
        if (isManuallyUnbound || isDragging)
        {
            return false;
        }

        if (enemyHealth == null || !enemyHealth.IsAlive)
        {
            return false;
        }

        if (spriteRenderer != null && !spriteRenderer.enabled)
        {
            return true;
        }

        if (targetCollider != null && !targetCollider.enabled)
        {
            return true;
        }

        if (enemyAnimators == null)
        {
            return false;
        }

        for (int i = 0; i < enemyAnimators.Length; i++)
        {
            Animator anim = enemyAnimators[i];
            if (anim == null || !anim.isActiveAndEnabled)
            {
                continue;
            }

            AnimatorControllerParameter[] parameters = anim.parameters;
            for (int p = 0; p < parameters.Length; p++)
            {
                AnimatorControllerParameter param = parameters[p];
                if (param == null || param.type != AnimatorControllerParameterType.Bool)
                {
                    continue;
                }

                string n = param.name;
                if (string.IsNullOrEmpty(n) || n.IndexOf("teleport", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (anim.GetBool(n))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void SetTeleportHidden(bool hidden)
    {
        if (canvasGroup == null)
        {
            return;
        }

        if (hidden)
        {
            if (!isTeleportHidden)
            {
                if (canvasGroup.alpha > 0.0001f)
                {
                    teleportHiddenPrevAlpha = canvasGroup.alpha;
                }
                teleportHiddenPrevInteractable = canvasGroup.interactable;
                teleportHiddenPrevBlocksRaycasts = canvasGroup.blocksRaycasts;
            }

            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            isTeleportHidden = true;
            return;
        }

        if (!isTeleportHidden)
        {
            return;
        }

        canvasGroup.alpha = teleportHiddenPrevAlpha;
        canvasGroup.interactable = teleportHiddenPrevInteractable;
        canvasGroup.blocksRaycasts = teleportHiddenPrevBlocksRaycasts;
        isTeleportHidden = false;

        hasPositionedOnce = false;
        nextUpdateTime = Time.unscaledTime;
    }

    private void RefreshSortingAgainstPlayer()
    {
        if (canvas == null || !hasDefaultCanvasSorting)
        {
            return;
        }

        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }

        if (worldCamera == null || rectTransform == null)
        {
            return;
        }

        AdvancedPlayerController player = AdvancedPlayerController.Instance;
        if (player == null)
        {
            RestoreDefaultSorting();
            return;
        }

        SpriteRenderer[] playerSprites = player.GetComponentsInChildren<SpriteRenderer>(true);
        if (playerSprites == null || playerSprites.Length == 0)
        {
            RestoreDefaultSorting();
            return;
        }

        bool hasPlayerBounds = false;
        Bounds playerBounds = default;
        bool hasPlayerSorting = false;
        int playerBackLayerValue = int.MinValue;
        int playerBackOrder = int.MinValue;
        int playerBackLayerId = 0;

        SortingGroup playerSortingGroup = player.GetComponent<SortingGroup>();
        if (playerSortingGroup != null)
        {
            hasPlayerSorting = true;
            playerBackLayerId = playerSortingGroup.sortingLayerID;
            playerBackOrder = playerSortingGroup.sortingOrder;
            playerBackLayerValue = SortingLayer.GetLayerValueFromID(playerBackLayerId);
        }

        for (int i = 0; i < playerSprites.Length; i++)
        {
            SpriteRenderer sr = playerSprites[i];
            if (sr == null || !sr.enabled || sr.sprite == null)
            {
                continue;
            }

            if (!hasPlayerBounds)
            {
                playerBounds = sr.bounds;
                hasPlayerBounds = true;
            }
            else
            {
                playerBounds.Encapsulate(sr.bounds);
            }

            if (playerSortingGroup == null)
            {
                int layerValue = SortingLayer.GetLayerValueFromID(sr.sortingLayerID);
                if (!hasPlayerSorting || layerValue > playerBackLayerValue || (layerValue == playerBackLayerValue && sr.sortingOrder > playerBackOrder))
                {
                    hasPlayerSorting = true;
                    playerBackLayerValue = layerValue;
                    playerBackOrder = sr.sortingOrder;
                    playerBackLayerId = sr.sortingLayerID;
                }
            }
        }

        if (!hasPlayerBounds)
        {
            RestoreDefaultSorting();
            return;
        }

        Rect panelRect = GetPanelScreenRect();
        Rect playerRect = WorldBoundsToScreenRect(playerBounds, worldCamera);
        bool overlaps = panelRect.Overlaps(playerRect, true);

        if (overlaps)
        {
            if (!hasPlayerSorting)
            {
                RestoreDefaultSorting();
                return;
            }

            canvas.sortingLayerID = playerBackLayerId;
            canvas.sortingOrder = playerBackOrder > int.MinValue ? (playerBackOrder - 1) : playerBackOrder;
            isForcedBehindPlayer = true;
        }
        else
        {
            RestoreDefaultSorting();
        }
    }

    private Rect GetPanelScreenRect()
    {
        Canvas parentCanvas = rectTransform != null ? rectTransform.GetComponentInParent<Canvas>() : null;
        Camera uiCam = null;
        if (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCam = parentCanvas.worldCamera != null ? parentCanvas.worldCamera : worldCamera;
        }

        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);

        Vector2 p0 = RectTransformUtility.WorldToScreenPoint(uiCam, corners[0]);
        float minX = p0.x;
        float maxX = p0.x;
        float minY = p0.y;
        float maxY = p0.y;

        for (int i = 1; i < 4; i++)
        {
            Vector2 p = RectTransformUtility.WorldToScreenPoint(uiCam, corners[i]);
            if (p.x < minX) minX = p.x;
            if (p.x > maxX) maxX = p.x;
            if (p.y < minY) minY = p.y;
            if (p.y > maxY) maxY = p.y;
        }

        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    private static Rect WorldBoundsToScreenRect(Bounds b, Camera cam)
    {
        Vector3 p0 = cam.WorldToScreenPoint(new Vector3(b.min.x, b.min.y, b.center.z));
        float minX = p0.x;
        float maxX = p0.x;
        float minY = p0.y;
        float maxY = p0.y;

        Vector3 p1 = cam.WorldToScreenPoint(new Vector3(b.min.x, b.max.y, b.center.z));
        if (p1.x < minX) minX = p1.x;
        if (p1.x > maxX) maxX = p1.x;
        if (p1.y < minY) minY = p1.y;
        if (p1.y > maxY) maxY = p1.y;

        Vector3 p2 = cam.WorldToScreenPoint(new Vector3(b.max.x, b.min.y, b.center.z));
        if (p2.x < minX) minX = p2.x;
        if (p2.x > maxX) maxX = p2.x;
        if (p2.y < minY) minY = p2.y;
        if (p2.y > maxY) maxY = p2.y;

        Vector3 p3 = cam.WorldToScreenPoint(new Vector3(b.max.x, b.max.y, b.center.z));
        if (p3.x < minX) minX = p3.x;
        if (p3.x > maxX) maxX = p3.x;
        if (p3.y < minY) minY = p3.y;
        if (p3.y > maxY) maxY = p3.y;

        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    private void RestoreDefaultSorting()
    {
        if (!isForcedBehindPlayer)
        {
            return;
        }

        if (canvas != null)
        {
            canvas.sortingLayerID = defaultCanvasSortingLayerId;
            canvas.sortingOrder = defaultCanvasSortingOrder;
        }

        isForcedBehindPlayer = false;
    }

    private void RefreshPosition()
    {
        if (rectTransform == null)
        {
            rectTransform = transform as RectTransform;
            if (rectTransform == null)
            {
                return;
            }
        }

        if (worldCamera == null)
        {
            worldCamera = Camera.main;
            if (worldCamera == null)
            {
                return;
            }
        }

        bool flipped = IsEnemyFlipped();
        Vector2 offsetPixels = Vector2.zero;
        if (manager != null)
        {
            offsetPixels = manager.GetPanelOffsetPixelsForEnemy(enemyHealth, flipped);
        }
        float orientedX = offsetPixels.x;

        Bounds bounds;
        if (targetCollider != null)
        {
            bounds = targetCollider.bounds;
        }
        else if (spriteRenderer != null)
        {
            bounds = spriteRenderer.bounds;
        }
        else
        {
            bounds = new Bounds(enemyHealth.transform.position, Vector3.zero);
        }

        Vector3 centerWorld = bounds.center;
        Vector3 centerScreen = worldCamera.WorldToScreenPoint(centerWorld);

        Canvas parentCanvasFallback = rectTransform.GetComponentInParent<Canvas>();
        float scaleFactorFallback = 1f;
        if (parentCanvasFallback != null)
        {
            scaleFactorFallback = Mathf.Max(0.0001f, parentCanvasFallback.scaleFactor);
        }

        Camera uiCameraFallback = null;
        if (parentCanvasFallback != null && parentCanvasFallback.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCameraFallback = parentCanvasFallback.worldCamera != null ? parentCanvasFallback.worldCamera : worldCamera;
        }

        RectTransform parentRect = transform.parent as RectTransform;
        if (parentRect != null)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, centerScreen, uiCameraFallback, out Vector2 localCenter))
            {
                float xLocal = orientedX / scaleFactorFallback;
                float yLocal = offsetPixels.y / scaleFactorFallback;

                Vector2 anchorRefLocal = new Vector2(
                    parentRect.rect.xMin + (parentRect.rect.width * rectTransform.anchorMin.x),
                    parentRect.rect.yMin + (parentRect.rect.height * rectTransform.anchorMin.y)
                );

                Vector2 anchored = (localCenter + new Vector2(xLocal, yLocal)) - anchorRefLocal;
                rectTransform.anchoredPosition = anchored;
                return;
            }
        }

        Vector2 pivotScreen = new Vector2(centerScreen.x + orientedX, centerScreen.y + offsetPixels.y);
        RectTransform worldRef = parentRect != null ? parentRect : rectTransform;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(worldRef, pivotScreen, uiCameraFallback, out Vector3 worldPoint))
        {
            rectTransform.position = worldPoint;
        }
    }

    private Color GetBackgroundColorForRarity(CardRarity rarity)
    {
        switch (rarity)
        {
            case CardRarity.Common:
                return commonBackgroundColor;
            case CardRarity.Uncommon:
                return uncommonBackgroundColor;
            case CardRarity.Rare:
                return rareBackgroundColor;
            case CardRarity.Epic:
                return epicBackgroundColor;
            case CardRarity.Legendary:
                return legendaryBackgroundColor;
            case CardRarity.Mythic:
                return mythicBackgroundColor;
            case CardRarity.Boss:
                return bossBackgroundColor;
            default:
                return commonBackgroundColor;
        }
    }

    private void RefreshStats()
    {
        float currentHealth = enemyHealth != null ? enemyHealth.CurrentHealth : 0f;
        if (healthText != null)
        {
            healthText.text = currentHealth.ToString("0");
        }

        float attack = hasBaseAttack ? cachedBaseAttack : 0f;

        if (EnemyScalingSystem.Instance != null)
        {
            float damageMultiplier = EnemyScalingSystem.Instance.GetDamageMultiplier();
            if (damageMultiplier > 0f && !Mathf.Approximately(damageMultiplier, 1f))
            {
                attack *= damageMultiplier;
            }
        }
        if (attackText != null)
        {
            attackText.text = attack.ToString("0");
        }

        if (defenseText != null)
        {
            defenseText.text = "0";
        }

        float baseMove = hasBaseMoveSpeed ? cachedBaseMoveSpeed : 0f;

        if (cachedBehaviours != null && cachedBehaviours.Length > 0)
        {
            if (TryGetFirstNumericValue(cachedBehaviours, new[] { "currentMoveSpeed", "moveSpeed", "walkSpeed" }, out float runtimeMove))
            {
                baseMove = runtimeMove;
            }
        }
        float effectiveMove = baseMove;
        if (statusController != null)
        {
            effectiveMove *= statusController.GetEnemyMoveSpeedMultiplier();
        }

        if (ProjectileFreezeManager.Instance != null && ProjectileFreezeManager.Instance.IsFrozen)
        {
            effectiveMove = 0f;
        }

        if (moveSpeedText != null)
        {
            moveSpeedText.text = effectiveMove.ToString("0.##");
        }
    }

    private void ForceRefresh()
    {
        RefreshPositionAndStats();
    }

    private bool IsEnemyFlipped()
    {
        if (spriteRenderer != null)
        {
            bool flipX = spriteRenderer.flipX;
            bool invert = false;

            MonoBehaviour[] parents = spriteRenderer.GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < parents.Length; i++)
            {
                MonoBehaviour b = parents[i];
                if (b == null)
                {
                    continue;
                }

                FieldInfo fi = b.GetType().GetField("invertFlip", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (fi != null && fi.FieldType == typeof(bool))
                {
                    invert = (bool)fi.GetValue(b);
                    break;
                }
            }

            return invert ? !flipX : flipX;
        }

        float sx = enemyHealth != null ? enemyHealth.transform.lossyScale.x : 1f;
        return sx < 0f;
    }

    private void CacheBaseStats()
    {
        cachedBaseAttack = 0f;
        cachedBaseMoveSpeed = 0f;
        hasBaseAttack = false;
        hasBaseMoveSpeed = false;

        if (enemyHealth == null)
        {
            return;
        }

        MonoBehaviour[] behaviours = enemyHealth.GetComponentsInChildren<MonoBehaviour>(true);

        if (TryGetFirstNumericValue(behaviours, new[] { "attackDamage", "rangedAttackDamage", "projectileDamage" }, out float attack))
        {
            cachedBaseAttack = attack;
            hasBaseAttack = true;
        }

        if (TryGetFirstNumericValue(behaviours, new[] { "moveSpeed", "walkSpeed" }, out float move))
        {
            cachedBaseMoveSpeed = move;
            hasBaseMoveSpeed = true;
        }
    }

    private static bool TryGetFirstNumericValue(MonoBehaviour[] behaviours, string[] memberNames, out float value)
    {
        value = 0f;
        if (behaviours == null || memberNames == null)
        {
            return false;
        }

        for (int n = 0; n < memberNames.Length; n++)
        {
            string name = memberNames[n];
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour b = behaviours[i];
                if (b == null)
                {
                    continue;
                }

                if (TryReadNumericMember(b, name, out value))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryReadNumericMember(object obj, string memberName, out float value)
    {
        value = 0f;
        if (obj == null || string.IsNullOrEmpty(memberName))
        {
            return false;
        }

        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Type type = obj.GetType();

        FieldInfo field = type.GetField(memberName, flags);
        if (field != null)
        {
            object raw = field.GetValue(obj);
            return TryConvertNumeric(raw, out value);
        }

        PropertyInfo prop = type.GetProperty(memberName, flags);
        if (prop != null && prop.CanRead)
        {
            object raw = prop.GetValue(obj, null);
            return TryConvertNumeric(raw, out value);
        }

        return false;
    }

    private static bool TryConvertNumeric(object raw, out float value)
    {
        value = 0f;
        if (raw == null)
        {
            return false;
        }

        if (raw is float f)
        {
            value = f;
            return true;
        }

        if (raw is int i)
        {
            value = i;
            return true;
        }

        if (raw is double d)
        {
            value = (float)d;
            return true;
        }

        if (raw is long l)
        {
            value = l;
            return true;
        }

        return false;
    }

    private void DisableNonCloseRaycasts()
    {
        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
            {
                graphics[i].raycastTarget = false;
            }
        }

        if (backgroundImage != null)
        {
            backgroundImage.raycastTarget = true;
        }

        if (closeButton != null)
        {
            Graphic[] closeGraphics = closeButton.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < closeGraphics.Length; i++)
            {
                if (closeGraphics[i] != null)
                {
                    closeGraphics[i].raycastTarget = true;
                }
            }
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData == null || rectTransform == null)
        {
            return;
        }

        if (closeButton != null && eventData.pointerPressRaycast.gameObject != null)
        {
            Transform pressTransform = eventData.pointerPressRaycast.gameObject.transform;
            if (pressTransform != null && pressTransform.IsChildOf(closeButton.transform))
            {
                return;
            }
        }

        RectTransform parentRect = rectTransform.parent as RectTransform;
        if (parentRect == null)
        {
            return;
        }

        Canvas parentCanvas = rectTransform.GetComponentInParent<Canvas>();
        Camera uiCam = null;
        if (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCam = parentCanvas.worldCamera != null ? parentCanvas.worldCamera : worldCamera;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, eventData.position, uiCam, out Vector2 localPointer))
        {
            return;
        }

        isManuallyUnbound = true;
        isDragging = true;
        dragStartAnchoredPosition = rectTransform.anchoredPosition;
        dragStartLocalPointerPosition = localPointer;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || eventData == null || rectTransform == null)
        {
            return;
        }

        RectTransform parentRect = rectTransform.parent as RectTransform;
        if (parentRect == null)
        {
            return;
        }

        Canvas parentCanvas = rectTransform.GetComponentInParent<Canvas>();
        Camera uiCam = null;
        if (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCam = parentCanvas.worldCamera != null ? parentCanvas.worldCamera : worldCamera;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, eventData.position, uiCam, out Vector2 localPointer))
        {
            return;
        }

        Vector2 delta = localPointer - dragStartLocalPointerPosition;
        rectTransform.anchoredPosition = dragStartAnchoredPosition + delta;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;
    }

    private void HandleEnemyDeath()
    {
        Close();
    }

    public void Close()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath -= HandleEnemyDeath;
        }

        if (manager != null)
        {
            manager.NotifyPanelClosed(enemyHealth, this);
        }

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath -= HandleEnemyDeath;
        }
    }
}
