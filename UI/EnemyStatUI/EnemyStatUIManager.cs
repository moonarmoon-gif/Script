using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class EnemyStatUIManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform uiParent;
    [SerializeField] private EnemyStatUIPanel statUIPanelPrefab;

    [Header("Click Detection")]
    [SerializeField] private LayerMask enemyClickMask = ~0;
    [SerializeField] private string clickHitboxObjectName = "ClickHitbox";

    [Header("Panel Settings")]
    [SerializeField] private Vector2 panelOffsetPixels = new Vector2(60f, 0f);
    [SerializeField] private Vector2 flipPanelOffsetPixels = new Vector2(-60f, 0f);
    [SerializeField] private float panelScale = 1.5f;
    [SerializeField] private float panelUpdateIntervalSeconds = 0.05f;

    [Serializable]
    public class EnemyPanelOffsets
    {
        public string enemyName;
        public Vector2 panelOffsetPixels;
        public Vector2 flipPanelOffsetPixels;
        public float panelScale = 1.5f;
    }

    [Header("Per-Enemy Panel Offsets")]
    [SerializeField] private bool usePerEnemyPanelOffsets = true;
    [SerializeField] private List<EnemyPanelOffsets> perEnemyPanelOffsets = new List<EnemyPanelOffsets>();

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    private InputAction pointerPressAction;
    private InputAction pointerPositionAction;

    private readonly Dictionary<int, EnemyStatUIPanel> panelsByEnemyId = new Dictionary<int, EnemyStatUIPanel>();
    private int nextSortingOrder = 0;

    private Camera worldCamera;
    private bool lastSelectionActive;

    public Vector2 PanelOffsetPixels => panelOffsetPixels;
    public Vector2 FlipPanelOffsetPixels
    {
        get => flipPanelOffsetPixels;
        set => flipPanelOffsetPixels = value;
    }
    public float PanelScale => panelScale;
    public float PanelUpdateIntervalSeconds => panelUpdateIntervalSeconds;

    public Vector2 GetPanelOffsetPixelsForEnemy(EnemyHealth enemyHealth, bool flipped)
    {
        Vector2 fallback = flipped ? flipPanelOffsetPixels : panelOffsetPixels;
        if (!usePerEnemyPanelOffsets || enemyHealth == null)
        {
            return fallback;
        }

        string enemyName = enemyHealth.gameObject.name.Replace("(Clone)", "").Trim();
        for (int i = 0; i < perEnemyPanelOffsets.Count; i++)
        {
            EnemyPanelOffsets entry = perEnemyPanelOffsets[i];
            if (entry != null && entry.enemyName == enemyName)
            {
                return flipped ? entry.flipPanelOffsetPixels : entry.panelOffsetPixels;
            }
        }

        return fallback;
    }

    public float GetPanelScaleForEnemy(EnemyHealth enemyHealth)
    {
        float fallback = panelScale;
        if (!usePerEnemyPanelOffsets || enemyHealth == null)
        {
            return fallback;
        }

        string enemyName = enemyHealth.gameObject.name.Replace("(Clone)", "").Trim();
        for (int i = 0; i < perEnemyPanelOffsets.Count; i++)
        {
            EnemyPanelOffsets entry = perEnemyPanelOffsets[i];
            if (entry != null && entry.enemyName == enemyName)
            {
                if (entry.panelScale > 0f)
                {
                    return entry.panelScale;
                }
                return fallback;
            }
        }

        return fallback;
    }

    private void Awake()
    {
        worldCamera = Camera.main;

        if (worldCamera == null)
        {
            worldCamera = FindObjectOfType<Camera>();
        }

        if (uiParent == null)
        {
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas != null)
            {
                uiParent = parentCanvas.transform as RectTransform;
            }
        }

        if (pointerPressAction == null)
        {
            pointerPressAction = new InputAction("EnemyStatUI_Press", InputActionType.Button, "<Pointer>/press");
            pointerPressAction.Enable();
        }

        if (pointerPositionAction == null)
        {
            pointerPositionAction = new InputAction("EnemyStatUI_Position", InputActionType.Value, "<Pointer>/position");
            pointerPositionAction.Enable();
        }
    }

    private void Start()
    {
        if (debugLogging)
        {
            Debug.Log($"[EnemyStatUIManager] Started. enabled={enabled}, activeInHierarchy={gameObject.activeInHierarchy}, uiParent={(uiParent != null ? uiParent.name : "NULL")}, statUIPanelPrefab={(statUIPanelPrefab != null ? statUIPanelPrefab.name : "NULL")}, worldCamera={(worldCamera != null ? worldCamera.name : "NULL")}");
        }
    }

    private void OnDestroy()
    {
        if (pointerPressAction != null)
        {
            pointerPressAction.Disable();
            pointerPressAction.Dispose();
            pointerPressAction = null;
        }

        if (pointerPositionAction != null)
        {
            pointerPositionAction.Disable();
            pointerPositionAction.Dispose();
            pointerPositionAction = null;
        }
    }

    private void Update()
    {
        bool selectionActive = CardSelectionManager.Instance != null && CardSelectionManager.Instance.IsSelectionUIActive;
        if (selectionActive != lastSelectionActive)
        {
            lastSelectionActive = selectionActive;
            SetPanelsInteractionEnabled(!selectionActive);

            if (debugLogging)
            {
                Debug.Log($"[EnemyStatUIManager] selectionActive={selectionActive}");
            }
        }

        if (selectionActive)
        {
            return;
        }

        bool pressedThisFrame = false;
        Vector2 pointerPos = default;

        if (pointerPressAction != null && pointerPositionAction != null)
        {
            pressedThisFrame = pointerPressAction.WasPressedThisFrame();
            pointerPos = pointerPositionAction.ReadValue<Vector2>();
        }
        else if (Touchscreen.current != null)
        {
            var touch = Touchscreen.current.primaryTouch;
            pressedThisFrame = touch.press.wasPressedThisFrame;
            pointerPos = touch.position.ReadValue();
        }
        else if (Mouse.current != null)
        {
            pressedThisFrame = Mouse.current.leftButton.wasPressedThisFrame;
            pointerPos = Mouse.current.position.ReadValue();
        }

        if (!pressedThisFrame)
        {
            return;
        }

        if (EventSystem.current != null)
        {
            bool isOverUi = EventSystem.current.IsPointerOverGameObject();
            if (!isOverUi && Touchscreen.current != null)
            {
                var touch = Touchscreen.current.primaryTouch;
                if (touch != null)
                {
                    int touchId = touch.touchId.ReadValue();
                    isOverUi = EventSystem.current.IsPointerOverGameObject(touchId);
                }
            }

            if (isOverUi)
            {
                return;
            }
        }

        if (debugLogging)
        {
            Debug.Log($"[EnemyStatUIManager] Pointer press at screen={pointerPos}");
        }

        if (worldCamera == null)
        {
            worldCamera = Camera.main;
            if (worldCamera == null)
            {
                worldCamera = FindObjectOfType<Camera>();
                if (worldCamera == null)
                {
                    return;
                }
            }
        }

        Ray ray = worldCamera.ScreenPointToRay(pointerPos);
        Vector2 p2;
        if (Mathf.Abs(ray.direction.z) > 0.0001f)
        {
            float t = -ray.origin.z / ray.direction.z;
            Vector3 worldPoint = ray.origin + ray.direction * t;
            p2 = new Vector2(worldPoint.x, worldPoint.y);
        }
        else
        {
            Vector3 worldPoint = worldCamera.ScreenToWorldPoint(new Vector3(pointerPos.x, pointerPos.y, 0f));
            p2 = new Vector2(worldPoint.x, worldPoint.y);
        }

        EnemyHealth enemyHealth = null;

        RaycastHit2D[] hits2d = Physics2D.GetRayIntersectionAll(ray, 9999f, enemyClickMask);
        for (int i = 0; i < hits2d.Length; i++)
        {
            if (hits2d[i].collider == null)
            {
                continue;
            }

            if (TryGetEnemyFromClickHitbox(hits2d[i].collider, out enemyHealth))
            {
                break;
            }
        }

        if (enemyHealth == null)
        {
            Collider2D[] overlaps = Physics2D.OverlapPointAll(p2, enemyClickMask);
            for (int i = 0; i < overlaps.Length; i++)
            {
                if (overlaps[i] == null)
                {
                    continue;
                }

                if (TryGetEnemyFromClickHitbox(overlaps[i], out enemyHealth))
                {
                    break;
                }
            }
        }

        if (enemyHealth == null || !enemyHealth.IsAlive)
        {
            if (debugLogging)
            {
                Debug.Log("[EnemyStatUIManager] No EnemyHealth hit.");
            }
            return;
        }

        if (debugLogging)
        {
            Debug.Log($"[EnemyStatUIManager] Hit enemy: {enemyHealth.gameObject.name}");
        }
        OpenOrBringToFront(enemyHealth);
    }

    private bool TryGetEnemyFromClickHitbox(Collider2D hitCollider, out EnemyHealth enemyHealth)
    {
        enemyHealth = null;
        if (hitCollider == null)
        {
            return false;
        }

        if (!hitCollider.isTrigger)
        {
            return false;
        }

        Transform t = hitCollider.transform;
        bool matched = false;
        while (t != null)
        {
            if (t.name == clickHitboxObjectName)
            {
                matched = true;
                break;
            }
            t = t.parent;
        }

        if (!matched)
        {
            return false;
        }

        enemyHealth = hitCollider.GetComponentInParent<EnemyHealth>();
        return enemyHealth != null;
    }

    private void OpenOrBringToFront(EnemyHealth enemyHealth)
    {
        int key = enemyHealth.GetInstanceID();

        if (panelsByEnemyId.TryGetValue(key, out EnemyStatUIPanel existing) && existing != null)
        {
            existing.Close();
            return;
        }

        if (statUIPanelPrefab == null || uiParent == null)
        {
            if (debugLogging)
            {
                Debug.LogWarning($"[EnemyStatUIManager] Missing references. statUIPanelPrefab={(statUIPanelPrefab == null ? "NULL" : "OK")}, uiParent={(uiParent == null ? "NULL" : "OK")}");
            }
            return;
        }

        EnemyStatUIPanel panel = Instantiate(statUIPanelPrefab, uiParent);
        panelsByEnemyId[key] = panel;

        panel.Bind(enemyHealth, this, nextSortingOrder++, worldCamera);

        bool selectionActive = CardSelectionManager.Instance != null && CardSelectionManager.Instance.IsSelectionUIActive;
        panel.SetInteractionEnabled(!selectionActive);
     }

    internal void NotifyPanelClosed(EnemyHealth enemyHealth, EnemyStatUIPanel panel)
    {
        if (enemyHealth != null)
        {
            int key = enemyHealth.GetInstanceID();
            if (panelsByEnemyId.TryGetValue(key, out EnemyStatUIPanel existing) && existing == panel)
            {
                panelsByEnemyId.Remove(key);
            }
        }
        else if (panel != null)
        {
            int removeKey = 0;
            bool found = false;
            foreach (var kvp in panelsByEnemyId)
            {
                if (kvp.Value == panel)
                {
                    removeKey = kvp.Key;
                    found = true;
                    break;
                }
            }

            if (found)
            {
                panelsByEnemyId.Remove(removeKey);
            }
        }
    }

    private void SetPanelsInteractionEnabled(bool enabled)
    {
        foreach (var kvp in panelsByEnemyId)
        {
            if (kvp.Value != null)
            {
                kvp.Value.SetInteractionEnabled(enabled);
            }
        }
    }
}
