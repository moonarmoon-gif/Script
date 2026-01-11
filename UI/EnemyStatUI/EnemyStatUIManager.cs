using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class EnemyStatUIManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform uiParent;
    [SerializeField] private EnemyStatUIPanel statUIPanelPrefab;

    [Header("Click Detection")]
    [SerializeField] private LayerMask enemyClickMask = ~0;

    [Header("Panel Settings")]
    [SerializeField] private Vector2 panelOffsetPixels = new Vector2(60f, 0f);
    [SerializeField] private float panelScale = 1f;
    [SerializeField] private float panelUpdateIntervalSeconds = 0.05f;

    [Header("Debug")]
    [SerializeField] private bool debugLogging = false;

    private InputAction pointerPressAction;
    private InputAction pointerPositionAction;

    private readonly Dictionary<int, EnemyStatUIPanel> panelsByEnemyId = new Dictionary<int, EnemyStatUIPanel>();
    private int nextSortingOrder = 0;

    private Camera worldCamera;
    private bool lastSelectionActive;

    public Vector2 PanelOffsetPixels => panelOffsetPixels;
    public float PanelScale => panelScale;
    public float PanelUpdateIntervalSeconds => panelUpdateIntervalSeconds;

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

        RaycastHit2D hit2d = Physics2D.GetRayIntersection(ray, 9999f, enemyClickMask);
        if (hit2d.collider != null)
        {
            enemyHealth = hit2d.collider.GetComponent<EnemyHealth>() ?? hit2d.collider.GetComponentInParent<EnemyHealth>();
        }

        if (enemyHealth == null)
        {
            Collider2D hit = Physics2D.OverlapPoint(p2, enemyClickMask);
            if (hit != null)
            {
                enemyHealth = hit.GetComponent<EnemyHealth>() ?? hit.GetComponentInParent<EnemyHealth>();
            }
        }

        if (enemyHealth == null)
        {
            Collider2D[] hits = Physics2D.OverlapPointAll(p2);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i] == null)
                {
                    continue;
                }

                enemyHealth = hits[i].GetComponent<EnemyHealth>() ?? hits[i].GetComponentInParent<EnemyHealth>();
                if (enemyHealth != null)
                {
                    break;
                }
            }
        }

        if (enemyHealth == null)
        {
            if (Physics.Raycast(ray, out RaycastHit hit3d, 9999f, enemyClickMask))
            {
                enemyHealth = hit3d.collider.GetComponent<EnemyHealth>() ?? hit3d.collider.GetComponentInParent<EnemyHealth>();
            }
            else if (Physics.Raycast(ray, out hit3d, 9999f))
            {
                enemyHealth = hit3d.collider.GetComponent<EnemyHealth>() ?? hit3d.collider.GetComponentInParent<EnemyHealth>();
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

    private void OpenOrBringToFront(EnemyHealth enemyHealth)
    {
        int key = enemyHealth.GetInstanceID();

        if (panelsByEnemyId.TryGetValue(key, out EnemyStatUIPanel existing) && existing != null)
        {
            existing.BringToFront(nextSortingOrder++);
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
