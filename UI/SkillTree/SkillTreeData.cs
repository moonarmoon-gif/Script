using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SkillTreeData", menuName = "Skill Tree/Skill Tree Data")]
public class SkillTreeData : ScriptableObject
{
    public string startNodeId = "START";

    public List<SkillTreeNodeData> nodes = new List<SkillTreeNodeData>();

    public SkillTreeNodeData GetNode(string id)
    {
        if (string.IsNullOrEmpty(id) || nodes == null)
        {
            return null;
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            SkillTreeNodeData n = nodes[i];
            if (n != null && n.id == id)
            {
                return n;
            }
        }

        return null;
    }

    public Dictionary<string, SkillTreeNodeData> BuildLookup()
    {
        Dictionary<string, SkillTreeNodeData> map = new Dictionary<string, SkillTreeNodeData>(StringComparer.Ordinal);
        if (nodes == null)
        {
            return map;
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            SkillTreeNodeData n = nodes[i];
            if (n == null || string.IsNullOrEmpty(n.id))
            {
                continue;
            }

            if (!map.ContainsKey(n.id))
            {
                map.Add(n.id, n);
            }
        }

        return map;
    }

#if UNITY_EDITOR
    [ContextMenu("Generate Debug Tree (100)")]
    private void GenerateDebugTree100()
    {
        nodes = new List<SkillTreeNodeData>();

        SkillTreeNodeData start = new SkillTreeNodeData
        {
            id = startNodeId,
            title = "Start",
            description = "",
            position = Vector2.zero,
            effects = new List<SkillTreeEffect>()
        };
        nodes.Add(start);

        int total = 100;
        float radiusStep = 220f;
        int perRing = 20;

        int created = 1;
        int ringIndex = 1;
        while (created < total)
        {
            int ringCount = Mathf.Min(perRing, total - created);
            float radius = ringIndex * radiusStep;

            for (int i = 0; i < ringCount; i++)
            {
                float t = (float)i / Mathf.Max(1, ringCount);
                float angle = t * Mathf.PI * 2f;

                SkillTreeNodeData n = new SkillTreeNodeData
                {
                    id = "N" + created,
                    title = "Node " + created,
                    description = "",
                    position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius,
                    effects = new List<SkillTreeEffect>(),
                    connections = new List<string>()
                };

                if (created % 5 == 0)
                {
                    n.effects.Add(new SkillTreeEffect { stat = SkillTreeStat.AttackFlat, floatValue = 1f });
                }
                else if (created % 5 == 1)
                {
                    n.effects.Add(new SkillTreeEffect { stat = SkillTreeStat.ManaRegenFlat, floatValue = 0.25f });
                }
                else if (created % 5 == 2)
                {
                    n.effects.Add(new SkillTreeEffect { stat = SkillTreeStat.AttackSpeedPercent, floatValue = 2f });
                }
                else if (created % 5 == 3)
                {
                    n.effects.Add(new SkillTreeEffect { stat = SkillTreeStat.MaxHealthFlat, floatValue = 5f });
                }
                else
                {
                    n.effects.Add(new SkillTreeEffect { stat = SkillTreeStat.FocusStacks, intValue = 1 });
                }

                nodes.Add(n);
                created++;
            }

            ringIndex++;
        }

        BuildUndirectedRingConnections(perRing);

        UnityEditor.EditorUtility.SetDirty(this);
    }

    private void BuildUndirectedRingConnections(int perRing)
    {
        if (nodes == null || nodes.Count <= 1)
        {
            return;
        }

        SkillTreeNodeData start = nodes[0];
        if (start.connections == null)
        {
            start.connections = new List<string>();
        }

        int index = 1;
        int ringIndex = 1;
        while (index < nodes.Count)
        {
            int ringCount = Mathf.Min(perRing, nodes.Count - index);

            for (int i = 0; i < ringCount; i++)
            {
                SkillTreeNodeData cur = nodes[index + i];
                if (cur.connections == null)
                {
                    cur.connections = new List<string>();
                }

                int nextI = (i + 1) % ringCount;
                SkillTreeNodeData next = nodes[index + nextI];

                AddEdge(cur, next.id);
                AddEdge(next, cur.id);

                if (ringIndex == 1)
                {
                    AddEdge(cur, start.id);
                    AddEdge(start, cur.id);
                }
            }

            index += ringCount;
            ringIndex++;
        }
    }

    private void AddEdge(SkillTreeNodeData node, string otherId)
    {
        if (node == null || string.IsNullOrEmpty(otherId))
        {
            return;
        }

        if (node.connections == null)
        {
            node.connections = new List<string>();
        }

        if (!node.connections.Contains(otherId))
        {
            node.connections.Add(otherId);
        }
    }
#endif
}

[Serializable]
public class SkillTreeNodeData
{
    public string id;
    public string title;
    [TextArea] public string description;

    public Vector2 position;
    public Sprite icon;

    public List<string> connections = new List<string>();
    public List<SkillTreeEffect> effects = new List<SkillTreeEffect>();
}

public enum SkillTreeStat
{
    AttackFlat,
    ManaRegenFlat,
    AttackSpeedPercent,
    DamageMultiplierPercent,
    MaxHealthFlat,
    MaxManaFlat,
    FocusStacks
}

[Serializable]
public class SkillTreeEffect
{
    public SkillTreeStat stat;
    public float floatValue;
    public int intValue;
}
