using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EnemyStatUIPanel : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image healthIcon;
    [SerializeField] private Image attackIcon;
    [SerializeField] private Image defenseIcon;
    [SerializeField] private Image moveSpeedIcon;

    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private TextMeshProUGUI attackText;
    [SerializeField] private TextMeshProUGUI defenseText;
    [SerializeField] private TextMeshProUGUI moveSpeedText;

    [SerializeField] private Button closeButton;

    private EnemyHealth enemyHealth;
    private StatusController statusController;
    private SpriteRenderer spriteRenderer;
    private Collider2D targetCollider;

    private EnemyStatUIManager manager;
    private Camera worldCamera;

    private RectTransform rectTransform;
    private Canvas canvas;
    private CanvasGroup canvasGroup;

    private float cachedBaseAttack;
    private float cachedBaseMoveSpeed;
    private bool hasBaseAttack;
    private bool hasBaseMoveSpeed;

    private float nextUpdateTime;
    private bool isBound;

    public void Bind(EnemyHealth target, EnemyStatUIManager owner, int sortingOrder, Camera cam)
    {
        enemyHealth = target;
        manager = owner;
        worldCamera = cam;

        rectTransform = transform as RectTransform;
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }

        canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
        }
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;

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

        statusController = enemyHealth != null ? (enemyHealth.GetComponent<StatusController>() ?? enemyHealth.GetComponentInParent<StatusController>()) : null;

        spriteRenderer = enemyHealth != null ? (enemyHealth.GetComponent<SpriteRenderer>() ?? enemyHealth.GetComponentInChildren<SpriteRenderer>()) : null;
        targetCollider = enemyHealth != null ? (enemyHealth.GetComponent<Collider2D>() ?? enemyHealth.GetComponentInChildren<Collider2D>()) : null;

        CacheBaseStats();
        ForceRefresh();

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

        if (manager != null)
        {
            float scale = manager.PanelScale;
            if (scale > 0f)
            {
                transform.localScale = Vector3.one * scale;
            }
        }

        isBound = true;
    }

    public void BringToFront(int sortingOrder)
    {
        if (canvas != null)
        {
            canvas.sortingOrder = sortingOrder;
        }
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
        RefreshPosition();
        RefreshStats();
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

        Vector3 anchorWorld = enemyHealth.transform.position;
        if (targetCollider != null)
        {
            anchorWorld = targetCollider.bounds.center;
        }

        Vector3 screenPoint = worldCamera.WorldToScreenPoint(anchorWorld);

        Vector2 offset = manager != null ? manager.PanelOffsetPixels : Vector2.zero;
        bool flipped = IsEnemyFlipped();
        if (flipped)
        {
            offset.x = -offset.x;
        }

        rectTransform.position = new Vector3(screenPoint.x + offset.x, screenPoint.y + offset.y, 0f);
    }

    private void RefreshStats()
    {
        float currentHealth = enemyHealth != null ? enemyHealth.CurrentHealth : 0f;
        if (healthText != null)
        {
            healthText.text = currentHealth.ToString("0");
        }

        float attack = hasBaseAttack ? cachedBaseAttack : 0f;
        if (attackText != null)
        {
            attackText.text = attack.ToString("0");
        }

        if (defenseText != null)
        {
            defenseText.text = "0";
        }

        float baseMove = hasBaseMoveSpeed ? cachedBaseMoveSpeed : 0f;
        float effectiveMove = baseMove;
        if (statusController != null)
        {
            effectiveMove *= statusController.GetEnemyMoveSpeedMultiplier();
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
            return spriteRenderer.flipX;
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
