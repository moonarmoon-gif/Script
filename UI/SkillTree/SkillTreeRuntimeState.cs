using System;
using System.Collections.Generic;
using UnityEngine;

public class SkillTreeRuntimeState : MonoBehaviour
{
    public static SkillTreeRuntimeState Instance { get; private set; }

    [SerializeField] private SkillTreeData treeData;
    [SerializeField] private List<string> purchasedNodeIds = new List<string>();

    private readonly HashSet<string> purchasedSet = new HashSet<string>(StringComparer.Ordinal);

    public event Action OnChanged;

    public SkillTreeData TreeData
    {
        get => treeData;
        set
        {
            treeData = value;
            RebuildFromList();
            NotifyChanged();
        }
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

        RebuildFromList();
    }

    public bool IsPurchased(string nodeId) => !string.IsNullOrEmpty(nodeId) && purchasedSet.Contains(nodeId);

    public IReadOnlyCollection<string> GetPurchasedIds() => purchasedSet;

    public bool CanPurchase(string nodeId)
    {
        if (treeData == null || string.IsNullOrEmpty(nodeId))
        {
            return false;
        }

        if (IsPurchased(nodeId))
        {
            return false;
        }

        if (nodeId == treeData.startNodeId)
        {
            return true;
        }

        SkillTreeNodeData node = treeData.GetNode(nodeId);
        if (node == null || node.connections == null)
        {
            return false;
        }

        for (int i = 0; i < node.connections.Count; i++)
        {
            string neighborId = node.connections[i];
            if (IsPurchased(neighborId))
            {
                return true;
            }
        }

        return false;
    }

    public bool CanUnpurchase(string nodeId)
    {
        if (treeData == null || string.IsNullOrEmpty(nodeId))
        {
            return false;
        }

        if (nodeId == treeData.startNodeId)
        {
            return false;
        }

        if (!IsPurchased(nodeId))
        {
            return false;
        }

        string startId = treeData.startNodeId;
        if (string.IsNullOrEmpty(startId) || !purchasedSet.Contains(startId))
        {
            return false;
        }

        Dictionary<string, SkillTreeNodeData> map = treeData.BuildLookup();
        Queue<string> q = new Queue<string>();
        HashSet<string> reachable = new HashSet<string>(StringComparer.Ordinal);

        q.Enqueue(startId);
        reachable.Add(startId);

        while (q.Count > 0)
        {
            string cur = q.Dequeue();
            if (!map.TryGetValue(cur, out SkillTreeNodeData node) || node == null || node.connections == null)
            {
                continue;
            }

            for (int i = 0; i < node.connections.Count; i++)
            {
                string nId = node.connections[i];
                if (string.IsNullOrEmpty(nId) || nId == nodeId)
                {
                    continue;
                }

                if (!purchasedSet.Contains(nId))
                {
                    continue;
                }

                if (reachable.Add(nId))
                {
                    q.Enqueue(nId);
                }
            }
        }

        foreach (string id in purchasedSet)
        {
            if (id == nodeId)
            {
                continue;
            }

            if (!reachable.Contains(id))
            {
                return false;
            }
        }

        return true;
    }

    public bool Purchase(string nodeId)
    {
        if (!CanPurchase(nodeId))
        {
            return false;
        }

        purchasedSet.Add(nodeId);
        SyncListFromSet();
        NotifyChanged();
        return true;
    }

    public bool Unpurchase(string nodeId)
    {
        if (treeData == null || string.IsNullOrEmpty(nodeId))
        {
            return false;
        }

        if (!CanUnpurchase(nodeId))
        {
            return false;
        }

        if (!purchasedSet.Remove(nodeId))
        {
            return false;
        }

        SyncListFromSet();
        NotifyChanged();
        return true;
    }

    public void EnsureStartPurchased()
    {
        if (treeData == null || string.IsNullOrEmpty(treeData.startNodeId))
        {
            return;
        }

        if (!IsPurchased(treeData.startNodeId))
        {
            purchasedSet.Add(treeData.startNodeId);
            SyncListFromSet();
            NotifyChanged();
        }
    }

    private void PruneDisconnected()
    {
        if (treeData == null || string.IsNullOrEmpty(treeData.startNodeId))
        {
            return;
        }

        if (purchasedSet.Count == 0)
        {
            return;
        }

        string startId = treeData.startNodeId;
        if (!purchasedSet.Contains(startId))
        {
            purchasedSet.Clear();
            purchasedSet.Add(startId);
            return;
        }

        Dictionary<string, SkillTreeNodeData> map = treeData.BuildLookup();
        Queue<string> q = new Queue<string>();
        HashSet<string> reachable = new HashSet<string>(StringComparer.Ordinal);

        q.Enqueue(startId);
        reachable.Add(startId);

        while (q.Count > 0)
        {
            string cur = q.Dequeue();
            if (!map.TryGetValue(cur, out SkillTreeNodeData node) || node == null || node.connections == null)
            {
                continue;
            }

            for (int i = 0; i < node.connections.Count; i++)
            {
                string nId = node.connections[i];
                if (!purchasedSet.Contains(nId))
                {
                    continue;
                }

                if (reachable.Add(nId))
                {
                    q.Enqueue(nId);
                }
            }
        }

        List<string> toRemove = null;
        foreach (string id in purchasedSet)
        {
            if (!reachable.Contains(id))
            {
                if (toRemove == null) toRemove = new List<string>();
                toRemove.Add(id);
            }
        }

        if (toRemove != null)
        {
            for (int i = 0; i < toRemove.Count; i++)
            {
                purchasedSet.Remove(toRemove[i]);
            }
        }
    }

    private void RebuildFromList()
    {
        purchasedSet.Clear();
        if (purchasedNodeIds == null)
        {
            purchasedNodeIds = new List<string>();
        }

        Dictionary<string, SkillTreeNodeData> map = treeData != null ? treeData.BuildLookup() : null;

        for (int i = 0; i < purchasedNodeIds.Count; i++)
        {
            string id = purchasedNodeIds[i];
            if (!string.IsNullOrEmpty(id))
            {
                if (map == null || map.ContainsKey(id))
                {
                    purchasedSet.Add(id);
                }
            }
        }

        PruneDisconnected();
        SyncListFromSet();
    }

    private void SyncListFromSet()
    {
        if (purchasedNodeIds == null)
        {
            purchasedNodeIds = new List<string>();
        }

        purchasedNodeIds.Clear();
        foreach (string id in purchasedSet)
        {
            purchasedNodeIds.Add(id);
        }
    }

    private void NotifyChanged() => OnChanged?.Invoke();
}
