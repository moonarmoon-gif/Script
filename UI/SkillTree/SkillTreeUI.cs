using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class SkillTreeUI : MonoBehaviour
{
    public static SkillTreeUI Instance { get; private set; }

    public bool IsOpen => root != null && root.activeSelf;

    private enum PendingAction
    {
        None,
        Purchase,
        Unpurchase
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureSkillTreeUIEnabledOnPlay()
    {
        SkillTreeUI[] all = Resources.FindObjectsOfTypeAll<SkillTreeUI>();
        for (int i = 0; i < all.Length; i++)
        {
            SkillTreeUI ui = all[i];
            if (ui == null)
            {
                continue;
            }

            if (!ui.gameObject.scene.IsValid() || !ui.gameObject.scene.isLoaded)
            {
                continue;
            }

            if (!ui.gameObject.activeSelf)
            {
                ui.gameObject.SetActive(true);
            }

            if (!ui.enabled)
            {
                ui.enabled = true;
            }
        }
    }

    [Header("Data")]
    [SerializeField] private SkillTreeData treeData;

    [Header("Runtime")]
    [SerializeField] private SkillTreeRuntimeState runtimeState;

    [Header("UI")]
    [SerializeField] private GameObject root;
    [SerializeField] private RectTransform content;
    [SerializeField] private RectTransform connectionsRoot;
    [SerializeField] private RectTransform nodesRoot;
    [SerializeField] private SkillTreeNodeView nodePrefab;
    [SerializeField] private SkillTreeConnectionView connectionPrefab;
    [SerializeField] private SkillTreeTooltip tooltip;

    [Header("Confirm UI")]
    [SerializeField] private GameObject confirmRoot;
    [SerializeField] private TMP_Text confirmText;
    [SerializeField] private Button confirmYesButton;
    [SerializeField] private Button confirmNoButton;

    [Header("Input")]
    [SerializeField] private KeyCode toggleKey = KeyCode.K;

    [SerializeField] private bool persistAcrossScenes = true;

    [Header("Gameplay Pause")]
    [SerializeField] private bool pauseGameplayWhenOpen = true;

    [SerializeField] private GameObject[] blockToggleIfAnyActive;
    [SerializeField] private GameObject[] blockResumeIfAnyActive;

    private float savedTimeScale = 1f;
    private bool pausedByThis;

    private PendingAction pendingAction;
    private string pendingNodeId;
    private bool confirmWired;

    private readonly Dictionary<string, SkillTreeNodeView> nodeViews = new Dictionary<string, SkillTreeNodeView>(System.StringComparer.Ordinal);
    private readonly List<SkillTreeConnectionView> connectionViews = new List<SkillTreeConnectionView>();

    private void Awake()
    {
        if (persistAcrossScenes)
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        if (root != null)
        {
            root.SetActive(false);
        }

        if (confirmRoot != null)
        {
            confirmRoot.SetActive(false);
        }

        WireConfirmUi();
    }

    private void OnEnable()
    {
        if (runtimeState != null)
        {
            runtimeState.OnChanged += RefreshVisuals;
        }
    }

    private void OnDisable()
    {
        if (runtimeState != null)
        {
            runtimeState.OnChanged -= RefreshVisuals;
        }
    }

    private void OnDestroy()
    {
        ResumeIfNeeded();
    }

    private void Update()
    {
        if (toggleKey != KeyCode.None && toggleKey != KeyCode.Space && WasKeyPressedThisFrame(toggleKey) && !IsAnyActive(blockToggleIfAnyActive))
        {
            Toggle();
        }

        if (root != null && root.activeSelf && WasKeyPressedThisFrame(KeyCode.Escape))
        {
            Close();
        }
    }

    private static bool WasKeyPressedThisFrame(KeyCode keyCode)
    {
#if ENABLE_INPUT_SYSTEM
        if (keyCode == KeyCode.None)
        {
            return false;
        }

        if (Keyboard.current == null)
        {
            return false;
        }

        if (TryConvertKeyCodeToInputSystemKey(keyCode, out Key key))
        {
            var control = Keyboard.current[key];
            return control != null && control.wasPressedThisFrame;
        }

        return false;
#else
        return false;
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private static bool TryConvertKeyCodeToInputSystemKey(KeyCode keyCode, out Key key)
    {
        key = Key.None;

        if (keyCode == KeyCode.None)
        {
            return false;
        }

        if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
        {
            key = (Key)((int)Key.A + (keyCode - KeyCode.A));
            return true;
        }

        if (keyCode >= KeyCode.Alpha0 && keyCode <= KeyCode.Alpha9)
        {
            key = (Key)((int)Key.Digit0 + (keyCode - KeyCode.Alpha0));
            return true;
        }

        if (keyCode == KeyCode.Return)
        {
            key = Key.Enter;
            return true;
        }

        string name = keyCode.ToString();
        return System.Enum.TryParse(name, out key) && key != Key.None;
    }
#endif

    public void Toggle()
    {
        if (root == null)
        {
            return;
        }

        if (root.activeSelf)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    public void Open()
    {
        if (root == null)
        {
            return;
        }

        if (runtimeState == null)
        {
            runtimeState = SkillTreeRuntimeState.Instance != null ? SkillTreeRuntimeState.Instance : FindObjectOfType<SkillTreeRuntimeState>();
        }

        if (runtimeState == null)
        {
            GameObject go = new GameObject("SkillTreeRuntimeState (Auto)");
            runtimeState = go.AddComponent<SkillTreeRuntimeState>();
        }

        runtimeState.OnChanged -= RefreshVisuals;
        runtimeState.OnChanged += RefreshVisuals;

        if (treeData != null && runtimeState != null)
        {
            runtimeState.TreeData = treeData;
            runtimeState.EnsureStartPurchased();
        }

        BuildIfNeeded();
        RefreshVisuals();

        PauseIfNeeded();

        root.SetActive(true);
    }

    public void Close()
    {
        if (root == null)
        {
            return;
        }

        if (tooltip != null)
        {
            tooltip.Hide();
        }

        HideConfirm();

        root.SetActive(false);

        ResumeIfNeeded();
    }

    private void PauseIfNeeded()
    {
        if (!pauseGameplayWhenOpen || pausedByThis)
        {
            return;
        }

        if (Time.timeScale <= 0.0001f)
        {
            pausedByThis = false;
            return;
        }

        savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        pausedByThis = true;
    }

    private void ResumeIfNeeded()
    {
        if (!pausedByThis)
        {
            return;
        }

        if (IsAnyActive(blockResumeIfAnyActive))
        {
            return;
        }

        if (GameStateManager.ManualPauseActive)
        {
            pausedByThis = false;
            return;
        }

        Time.timeScale = savedTimeScale;
        pausedByThis = false;
    }

    private static bool IsAnyActive(GameObject[] roots)
    {
        if (roots == null)
        {
            return false;
        }

        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] != null && roots[i].activeInHierarchy)
            {
                return true;
            }
        }

        return false;
    }

    public void HandleNodeClick(string nodeId, bool leftClick)
    {
        if (runtimeState == null || string.IsNullOrEmpty(nodeId))
        {
            return;
        }

        PendingAction action = leftClick ? PendingAction.Purchase : PendingAction.Unpurchase;

        if (pendingAction != PendingAction.None && pendingNodeId == nodeId)
        {
            if ((pendingAction == PendingAction.Purchase && !leftClick) || (pendingAction == PendingAction.Unpurchase && leftClick))
            {
                HideConfirm();
                return;
            }
        }

        bool canDo = action == PendingAction.Purchase ? runtimeState.CanPurchase(nodeId) : runtimeState.CanUnpurchase(nodeId);
        if (!canDo)
        {
            HideConfirm();
            return;
        }

        if (confirmRoot == null || confirmYesButton == null || confirmNoButton == null)
        {
            ApplyAction(action, nodeId);
            return;
        }

        pendingAction = action;
        pendingNodeId = nodeId;

        if (confirmText != null)
        {
            confirmText.text = action == PendingAction.Purchase ? "Confirm: " : "Confirm: ";
        }

        confirmRoot.SetActive(true);
    }

    private void ApplyAction(PendingAction action, string nodeId)
    {
        if (runtimeState == null || string.IsNullOrEmpty(nodeId))
        {
            return;
        }

        if (action == PendingAction.Purchase)
        {
            runtimeState.Purchase(nodeId);
        }
        else if (action == PendingAction.Unpurchase)
        {
            runtimeState.Unpurchase(nodeId);
        }
    }

    private void WireConfirmUi()
    {
        if (confirmWired)
        {
            return;
        }

        if (confirmYesButton != null)
        {
            confirmYesButton.onClick.RemoveListener(OnConfirmYes);
            confirmYesButton.onClick.AddListener(OnConfirmYes);
        }

        if (confirmNoButton != null)
        {
            confirmNoButton.onClick.RemoveListener(OnConfirmNo);
            confirmNoButton.onClick.AddListener(OnConfirmNo);
        }

        confirmWired = true;
    }

    private void OnConfirmYes()
    {
        if (pendingAction == PendingAction.None || string.IsNullOrEmpty(pendingNodeId))
        {
            HideConfirm();
            return;
        }

        ApplyAction(pendingAction, pendingNodeId);
        HideConfirm();
    }

    private void OnConfirmNo()
    {
        HideConfirm();
    }

    private void HideConfirm()
    {
        pendingAction = PendingAction.None;
        pendingNodeId = null;

        if (confirmRoot != null)
        {
            confirmRoot.SetActive(false);
        }
    }

    private void BuildIfNeeded()
    {
        if (treeData == null || nodePrefab == null || nodesRoot == null)
        {
            return;
        }

        if (nodeViews.Count > 0)
        {
            return;
        }

        for (int i = 0; i < treeData.nodes.Count; i++)
        {
            SkillTreeNodeData node = treeData.nodes[i];
            if (node == null || string.IsNullOrEmpty(node.id))
            {
                continue;
            }

            SkillTreeNodeView view = Instantiate(nodePrefab, nodesRoot);
            view.Initialize(node, runtimeState, tooltip);

            RectTransform rt = view.transform as RectTransform;
            if (rt != null)
            {
                rt.anchoredPosition = node.position;
            }

            nodeViews[node.id] = view;
        }

        BuildConnections();
    }

    private void BuildConnections()
    {
        if (treeData == null || connectionPrefab == null || connectionsRoot == null)
        {
            return;
        }

        Dictionary<string, SkillTreeNodeData> map = treeData.BuildLookup();

        HashSet<string> built = new HashSet<string>(System.StringComparer.Ordinal);

        for (int i = 0; i < treeData.nodes.Count; i++)
        {
            SkillTreeNodeData a = treeData.nodes[i];
            if (a == null || string.IsNullOrEmpty(a.id) || a.connections == null)
            {
                continue;
            }

            for (int j = 0; j < a.connections.Count; j++)
            {
                string bId = a.connections[j];
                if (string.IsNullOrEmpty(bId))
                {
                    continue;
                }

                string key = MakeUndirectedKey(a.id, bId);
                if (!built.Add(key))
                {
                    continue;
                }

                if (!map.TryGetValue(bId, out SkillTreeNodeData b) || b == null)
                {
                    continue;
                }

                if (!nodeViews.TryGetValue(a.id, out SkillTreeNodeView aView) || !nodeViews.TryGetValue(bId, out SkillTreeNodeView bView))
                {
                    continue;
                }

                SkillTreeConnectionView line = Instantiate(connectionPrefab, connectionsRoot);
                line.Initialize(aView, bView);
                connectionViews.Add(line);
            }
        }
    }

    private string MakeUndirectedKey(string a, string b)
    {
        if (string.CompareOrdinal(a, b) <= 0)
        {
            return a + "|" + b;
        }

        return b + "|" + a;
    }

    private void RefreshVisuals()
    {
        foreach (KeyValuePair<string, SkillTreeNodeView> kvp in nodeViews)
        {
            if (kvp.Value != null)
            {
                kvp.Value.Refresh();
            }
        }

        for (int i = 0; i < connectionViews.Count; i++)
        {
            SkillTreeConnectionView line = connectionViews[i];
            if (line != null)
            {
                line.Refresh(runtimeState);
            }
        }
    }
}
