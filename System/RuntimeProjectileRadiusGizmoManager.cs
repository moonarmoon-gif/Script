using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-100)]
public class RuntimeProjectileRadiusGizmoManager : MonoBehaviour
{
    public static RuntimeProjectileRadiusGizmoManager Instance { get; private set; }

    [SerializeField] private LayerMask projectileClickMask = ~0;

    private InputAction pointerPressAction;
    private InputAction pointerPositionAction;

    private Camera worldCamera;

    private static int handledClickFrame = -1;

    private static bool novaStarGizmosEnabled = false;
    private static bool dwarfStarGizmosEnabled = false;
    private float nextStarGizmoSyncTime = 0f;

    public static bool WasClickHandledThisFrame => handledClickFrame == Time.frameCount;

    public static void ResetGlobalStarGizmoState()
    {
        novaStarGizmosEnabled = false;
        dwarfStarGizmosEnabled = false;

        if (Instance != null)
        {
            Instance.ApplyGlobalStarGizmos(RuntimeProjectileRadiusGizmo.GizmoSourceKind.NovaStar, false);
            Instance.ApplyGlobalStarGizmos(RuntimeProjectileRadiusGizmo.GizmoSourceKind.DwarfStar, false);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureExists()
    {
        if (Instance != null)
        {
            return;
        }

        RuntimeProjectileRadiusGizmoManager existing = FindObjectOfType<RuntimeProjectileRadiusGizmoManager>();
        if (existing != null)
        {
            Instance = existing;
            return;
        }

        GameObject go = new GameObject("RuntimeProjectileRadiusGizmoManager");
        RuntimeProjectileRadiusGizmoManager mgr = go.AddComponent<RuntimeProjectileRadiusGizmoManager>();
        Instance = mgr;
        DontDestroyOnLoad(go);
    }

    public static bool TryHandleClick(Vector2 screenPosition)
    {
        if (WasClickHandledThisFrame)
        {
            return true;
        }

        if (Instance == null)
        {
            EnsureExists();
            if (Instance == null)
            {
                return false;
            }
        }

        bool allowToggle = Instance.ShouldToggleOnThisCall();
        bool handled = Instance.TryHandleClickInternal(screenPosition, allowToggle);
        if (handled)
        {
            handledClickFrame = Time.frameCount;
        }

        return handled;
    }

    private bool ShouldToggleOnThisCall()
    {
        if (pointerPressAction != null)
        {
            return pointerPressAction.WasPressedThisFrame();
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            return true;
        }

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            return true;
        }

        return false;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        worldCamera = Camera.main;
        if (worldCamera == null)
        {
            worldCamera = FindObjectOfType<Camera>();
        }

        if (pointerPressAction == null)
        {
            pointerPressAction = new InputAction("ProjectileGizmo_Press", InputActionType.Button, "<Pointer>/press");
            pointerPressAction.Enable();
        }

        if (pointerPositionAction == null)
        {
            pointerPositionAction = new InputAction("ProjectileGizmo_Position", InputActionType.Value, "<Pointer>/position");
            pointerPositionAction.Enable();
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

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (handledClickFrame == Time.frameCount)
        {
            SyncStarGizmosIfNeeded();
            return;
        }

        bool pressedThisFrame = false;
        Vector2 pointerPos = default;

        if (pointerPressAction != null && pointerPositionAction != null)
        {
            pressedThisFrame = pointerPressAction.WasPressedThisFrame();
            pointerPos = pointerPositionAction.ReadValue<Vector2>();
        }
        else if (Mouse.current != null)
        {
            pressedThisFrame = Mouse.current.leftButton.wasPressedThisFrame;
            pointerPos = Mouse.current.position.ReadValue();
        }

        if (!pressedThisFrame)
        {
            SyncStarGizmosIfNeeded();
            return;
        }

        if (TryHandleClickInternal(pointerPos, true))
        {
            handledClickFrame = Time.frameCount;
        }

        SyncStarGizmosIfNeeded();
    }

    private void SyncStarGizmosIfNeeded()
    {
        if (!novaStarGizmosEnabled && !dwarfStarGizmosEnabled)
        {
            return;
        }

        if (Time.unscaledTime < nextStarGizmoSyncTime)
        {
            return;
        }

        nextStarGizmoSyncTime = Time.unscaledTime + 0.5f;

        if (novaStarGizmosEnabled)
        {
            ApplyGlobalStarGizmos(RuntimeProjectileRadiusGizmo.GizmoSourceKind.NovaStar, true);
        }

        if (dwarfStarGizmosEnabled)
        {
            ApplyGlobalStarGizmos(RuntimeProjectileRadiusGizmo.GizmoSourceKind.DwarfStar, true);
        }
    }

    private bool TryHandleClickInternal(Vector2 screenPosition, bool allowToggle)
    {
        bool prevQueriesHitTriggers = Physics2D.queriesHitTriggers;
        Physics2D.queriesHitTriggers = true;

        if (CardSelectionManager.Instance != null && CardSelectionManager.Instance.IsSelectionUIActive)
        {
            Physics2D.queriesHitTriggers = prevQueriesHitTriggers;
            return false;
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
                Physics2D.queriesHitTriggers = prevQueriesHitTriggers;
                return false;
            }
        }

        if (worldCamera == null)
        {
            worldCamera = Camera.main;
            if (worldCamera == null)
            {
                worldCamera = FindObjectOfType<Camera>();
                if (worldCamera == null)
                {
                    Physics2D.queriesHitTriggers = prevQueriesHitTriggers;
                    return false;
                }
            }
        }

        Ray ray = worldCamera.ScreenPointToRay(screenPosition);

        RaycastHit2D[] hits2d = Physics2D.GetRayIntersectionAll(ray, 9999f, projectileClickMask);
        for (int i = 0; i < hits2d.Length; i++)
        {
            Collider2D c = hits2d[i].collider;
            if (c == null)
            {
                continue;
            }

            if (TryHandleCollider(c, allowToggle))
            {
                Physics2D.queriesHitTriggers = prevQueriesHitTriggers;
                return true;
            }
        }

        Vector2 p2;
        if (Mathf.Abs(ray.direction.z) > 0.0001f)
        {
            float t = -ray.origin.z / ray.direction.z;
            Vector3 worldPoint = ray.origin + ray.direction * t;
            p2 = new Vector2(worldPoint.x, worldPoint.y);
        }
        else
        {
            Vector3 worldPoint = worldCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, 0f));
            p2 = new Vector2(worldPoint.x, worldPoint.y);
        }

        Collider2D[] overlaps = Physics2D.OverlapPointAll(p2, projectileClickMask);
        for (int i = 0; i < overlaps.Length; i++)
        {
            Collider2D c = overlaps[i];
            if (c == null)
            {
                continue;
            }

            if (TryHandleCollider(c, allowToggle))
            {
                Physics2D.queriesHitTriggers = prevQueriesHitTriggers;
                return true;
            }
        }

        Physics2D.queriesHitTriggers = prevQueriesHitTriggers;
        return false;
    }

    private bool TryHandleCollider(Collider2D hitCollider, bool allowToggle)
    {
        if (hitCollider == null)
        {
            return false;
        }

        if (!TryGetRootAndKindFromCollider(hitCollider, out GameObject root, out RuntimeProjectileRadiusGizmo.GizmoSourceKind kind))
        {
            return false;
        }

        if (!allowToggle)
        {
            return true;
        }

        if (kind == RuntimeProjectileRadiusGizmo.GizmoSourceKind.NovaStar || kind == RuntimeProjectileRadiusGizmo.GizmoSourceKind.DwarfStar)
        {
            ToggleGlobalStarGizmos(kind);
            return true;
        }

        return ToggleGizmoOnRoot(root, kind);
    }

    private void ToggleGlobalStarGizmos(RuntimeProjectileRadiusGizmo.GizmoSourceKind kind)
    {
        bool enabled;
        if (kind == RuntimeProjectileRadiusGizmo.GizmoSourceKind.NovaStar)
        {
            novaStarGizmosEnabled = !novaStarGizmosEnabled;
            enabled = novaStarGizmosEnabled;
        }
        else
        {
            dwarfStarGizmosEnabled = !dwarfStarGizmosEnabled;
            enabled = dwarfStarGizmosEnabled;
        }

        ApplyGlobalStarGizmos(kind, enabled);
    }

    private void ApplyGlobalStarGizmos(RuntimeProjectileRadiusGizmo.GizmoSourceKind kind, bool enabled)
    {
        if (kind == RuntimeProjectileRadiusGizmo.GizmoSourceKind.NovaStar)
        {
            NovaStar[] stars = FindObjectsOfType<NovaStar>();
            for (int i = 0; i < stars.Length; i++)
            {
                NovaStar s = stars[i];
                if (s == null) continue;
                SetGizmoEnabledOnRoot(s.gameObject, kind, enabled);
            }
        }
        else if (kind == RuntimeProjectileRadiusGizmo.GizmoSourceKind.DwarfStar)
        {
            DwarfStar[] stars = FindObjectsOfType<DwarfStar>();
            for (int i = 0; i < stars.Length; i++)
            {
                DwarfStar s = stars[i];
                if (s == null) continue;
                SetGizmoEnabledOnRoot(s.gameObject, kind, enabled);
            }
        }
    }

    private void SetGizmoEnabledOnRoot(GameObject root, RuntimeProjectileRadiusGizmo.GizmoSourceKind kind, bool enabled)
    {
        if (root == null) return;

        RuntimeProjectileRadiusGizmo existing = root.GetComponent<RuntimeProjectileRadiusGizmo>();
        if (!enabled)
        {
            if (existing != null)
            {
                Destroy(existing);
            }
            return;
        }

        if (existing != null)
        {
            return;
        }

        RuntimeProjectileRadiusGizmo gizmo = root.AddComponent<RuntimeProjectileRadiusGizmo>();
        gizmo.Initialize(kind);
    }

    private bool TryGetRootAndKindFromCollider(Collider2D hitCollider, out GameObject root, out RuntimeProjectileRadiusGizmo.GizmoSourceKind kind)
    {
        root = null;
        kind = default;

        FireMine fireMine = hitCollider.GetComponentInParent<FireMine>();
        if (fireMine != null)
        {
            root = fireMine.gameObject;
            kind = RuntimeProjectileRadiusGizmo.GizmoSourceKind.FireMine;
            return true;
        }

        NovaStar novaStar = hitCollider.GetComponentInParent<NovaStar>();
        if (novaStar != null)
        {
            root = novaStar.gameObject;
            kind = RuntimeProjectileRadiusGizmo.GizmoSourceKind.NovaStar;
            return true;
        }

        DwarfStar dwarfStar = hitCollider.GetComponentInParent<DwarfStar>();
        if (dwarfStar != null)
        {
            root = dwarfStar.gameObject;
            kind = RuntimeProjectileRadiusGizmo.GizmoSourceKind.DwarfStar;
            return true;
        }

        ElectroBall electroBall = hitCollider.GetComponentInParent<ElectroBall>();
        if (electroBall != null)
        {
            root = electroBall.gameObject;
            kind = RuntimeProjectileRadiusGizmo.GizmoSourceKind.ElectroBall;
            return true;
        }

        NuclearStrike nuclearStrike = hitCollider.GetComponentInParent<NuclearStrike>();
        if (nuclearStrike != null)
        {
            root = nuclearStrike.gameObject;
            kind = RuntimeProjectileRadiusGizmo.GizmoSourceKind.NuclearStrike;
            return true;
        }

        ThunderBird thunderBird = hitCollider.GetComponentInParent<ThunderBird>();
        if (thunderBird != null)
        {
            root = thunderBird.gameObject;
            kind = RuntimeProjectileRadiusGizmo.GizmoSourceKind.ThunderBird;
            return true;
        }

        return false;
    }

    private bool ToggleGizmoOnRoot(GameObject root, RuntimeProjectileRadiusGizmo.GizmoSourceKind kind)
    {
        if (root == null)
        {
            return false;
        }

        RuntimeProjectileRadiusGizmo existing = root.GetComponent<RuntimeProjectileRadiusGizmo>();
        if (existing != null)
        {
            Destroy(existing);
            return true;
        }

        RuntimeProjectileRadiusGizmo gizmo = root.AddComponent<RuntimeProjectileRadiusGizmo>();
        gizmo.Initialize(kind);
        return true;
    }
}
