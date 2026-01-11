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

    [Header("Panel Settings")]
    [SerializeField] private Vector2 panelOffsetPixels = new Vector2(60f, 0f);
    [SerializeField] private float panelScale = 1f;
    [SerializeField] private float panelUpdateIntervalSeconds = 0.05f;

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

        if (uiParent == null)
        {
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas != null)
            {
                uiParent = parentCanvas.transform as RectTransform;
            }
        }
    }

    private void Update()
    {
        bool selectionActive = CardSelectionManager.Instance != null && CardSelectionManager.Instance.IsSelectionActive();
        if (selectionActive != lastSelectionActive)
        {
            lastSelectionActive = selectionActive;
            SetPanelsInteractionEnabled(!selectionActive);
        }

        if (Mouse.current == null)
        {
            return;
        }

        if (!Mouse.current.leftButton.wasPressedThisFrame)
        {
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (worldCamera == null)
        {
            worldCamera = Camera.main;
            if (worldCamera == null)
            {
                return;
            }
        }

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Vector3 worldPoint = worldCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, 0f));
        Vector2 p2 = new Vector2(worldPoint.x, worldPoint.y);

        Collider2D hit = Physics2D.OverlapPoint(p2, enemyClickMask);
        if (hit == null)
        {
            return;
        }

        EnemyHealth enemyHealth = hit.GetComponent<EnemyHealth>() ?? hit.GetComponentInParent<EnemyHealth>();
        if (enemyHealth == null || !enemyHealth.IsAlive)
        {
            return;
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
            return;
        }

        EnemyStatUIPanel panel = Instantiate(statUIPanelPrefab, uiParent);
        panelsByEnemyId[key] = panel;

        panel.Bind(enemyHealth, this, nextSortingOrder++, worldCamera);

        bool selectionActive = CardSelectionManager.Instance != null && CardSelectionManager.Instance.IsSelectionActive();
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
