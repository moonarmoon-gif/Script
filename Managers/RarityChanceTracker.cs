using UnityEngine;

public class RarityChanceTracker : MonoBehaviour
{
    public static RarityChanceTracker Instance { get; private set; }

    [SerializeField] private float commonChance = 30f;
    [SerializeField] private float uncommonChance = 29f;
    [SerializeField] private float rareChance = 20f;
    [SerializeField] private float epicChance = 15f;
    [SerializeField] private float legendaryChance = 5f;
    [SerializeField] private float mythicChance = 1f;

    [SerializeField] private float currentLuck = 0f;

    public float CommonChance => commonChance;
    public float UncommonChance => uncommonChance;
    public float RareChance => rareChance;
    public float EpicChance => epicChance;
    public float LegendaryChance => legendaryChance;
    public float MythicChance => mythicChance;

    public float CurrentLuck => currentLuck;

    private GameObject cachedPlayer;
    private PlayerStats cachedPlayerStats;

    private CardSelectionManager cachedCardSelectionManager;

    private float baseCommon;
    private float baseUncommon;
    private float baseRare;
    private float baseEpic;
    private float baseLegendary;
    private float baseMythic;

    private float lastAppliedLuck = float.NaN;
    private int nextPlayerSearchFrame;
    private int nextManagerSearchFrame;

    private bool IsAttachedToPlayerHierarchy()
    {
        if (CompareTag("Player"))
        {
            return true;
        }

        if (GetComponent<PlayerController>() != null ||
            GetComponent<AdvancedPlayerController>() != null ||
            GetComponent<PlayerStats>() != null)
        {
            return true;
        }

        if (GetComponentInParent<PlayerController>() != null ||
            GetComponentInParent<AdvancedPlayerController>() != null ||
            GetComponentInParent<PlayerStats>() != null)
        {
            return true;
        }

        return false;
    }

    private void Awake()
    {
        bool attachedToPlayer = IsAttachedToPlayerHierarchy();
        if (Instance != null && Instance != this)
        {
            if (attachedToPlayer)
            {
                Destroy(this);
            }
            else
            {
                Destroy(gameObject);
            }
            return;
        }

        Instance = this;
        if (!attachedToPlayer)
        {
            DontDestroyOnLoad(gameObject);
        }

        TryResolveCardSelectionManager(true);
        TryResolvePlayerStats(true);

        LoadBaseOddsFromManagerIfNeeded(force: true);
        RefreshFromLuck(GetPlayerLuck());
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        TryResolveCardSelectionManager(false);
        TryResolvePlayerStats(false);

        bool baseOddsChanged = LoadBaseOddsFromManagerIfNeeded(force: false);

        float luck = GetPlayerLuck();
        if (baseOddsChanged || !Mathf.Approximately(luck, lastAppliedLuck))
        {
            RefreshFromLuck(luck);
        }
    }

    private float GetPlayerLuck()
    {
        if (cachedPlayerStats == null)
        {
            return 0f;
        }

        return Mathf.Max(0f, cachedPlayerStats.luck);
    }

    private void TryResolvePlayerStats(bool force)
    {
        if (!force && cachedPlayerStats != null)
        {
            return;
        }

        if (!force && Time.frameCount < nextPlayerSearchFrame)
        {
            return;
        }

        nextPlayerSearchFrame = Time.frameCount + 30;

        if (cachedPlayer == null)
        {
            cachedPlayer = GameObject.FindGameObjectWithTag("Player");
        }

        if (cachedPlayer != null)
        {
            cachedPlayerStats = cachedPlayer.GetComponent<PlayerStats>();
        }
    }

    private void TryResolveCardSelectionManager(bool force)
    {
        if (!force && cachedCardSelectionManager != null)
        {
            return;
        }

        if (!force && Time.frameCount < nextManagerSearchFrame)
        {
            return;
        }

        nextManagerSearchFrame = Time.frameCount + 60;

        cachedCardSelectionManager = CardSelectionManager.Instance;
        if (cachedCardSelectionManager == null)
        {
            cachedCardSelectionManager = FindObjectOfType<CardSelectionManager>();
        }
    }

    private bool LoadBaseOddsFromManagerIfNeeded(bool force)
    {
        float nextCommon = baseCommon;
        float nextUncommon = baseUncommon;
        float nextRare = baseRare;
        float nextEpic = baseEpic;
        float nextLegendary = baseLegendary;
        float nextMythic = baseMythic;

        if (cachedCardSelectionManager != null)
        {
            nextCommon = cachedCardSelectionManager.commonOdds;
            nextUncommon = cachedCardSelectionManager.uncommonOdds;
            nextRare = cachedCardSelectionManager.rareOdds;
            nextEpic = cachedCardSelectionManager.epicOdds;
            nextLegendary = cachedCardSelectionManager.legendaryOdds;
            nextMythic = cachedCardSelectionManager.mythicOdds;
        }
        else if (force)
        {
            nextCommon = commonChance;
            nextUncommon = uncommonChance;
            nextRare = rareChance;
            nextEpic = epicChance;
            nextLegendary = legendaryChance;
            nextMythic = mythicChance;
        }

        bool changed = force
                       || !Mathf.Approximately(nextCommon, baseCommon)
                       || !Mathf.Approximately(nextUncommon, baseUncommon)
                       || !Mathf.Approximately(nextRare, baseRare)
                       || !Mathf.Approximately(nextEpic, baseEpic)
                       || !Mathf.Approximately(nextLegendary, baseLegendary)
                       || !Mathf.Approximately(nextMythic, baseMythic);

        baseCommon = nextCommon;
        baseUncommon = nextUncommon;
        baseRare = nextRare;
        baseEpic = nextEpic;
        baseLegendary = nextLegendary;
        baseMythic = nextMythic;

        return changed;
    }

    private void RefreshFromLuck(float playerLuck)
    {
        currentLuck = Mathf.Max(0f, playerLuck);
        lastAppliedLuck = currentLuck;

        float luck = Mathf.Max(0f, currentLuck) * 0.5f;

        float pCommon;
        float pUncommon;
        float pRare;
        float pEpic;
        float pLegendary;
        float pMythic;

        const float w0CommonToUncommon = 5f;
        const float w0CommonToRare = 4f;
        const float w0CommonToEpic = 3f;
        const float w0CommonToLegendary = 2f;
        const float w0CommonToMythic = 1f;
        const float sumStage0 = w0CommonToUncommon + w0CommonToRare + w0CommonToEpic + w0CommonToLegendary + w0CommonToMythic;

        const float w1UncommonToRare = 4f;
        const float w1UncommonToEpic = 3f;
        const float w1UncommonToLegendary = 2f;
        const float w1UncommonToMythic = 1f;
        const float sumStage1 = w1UncommonToRare + w1UncommonToEpic + w1UncommonToLegendary + w1UncommonToMythic;

        const float w2RareToEpic = 3f;
        const float w2RareToLegendary = 2f;
        const float w2RareToMythic = 1f;
        const float sumStage2 = w2RareToEpic + w2RareToLegendary + w2RareToMythic;

        if (luck <= 80f)
        {
            float t0 = luck / 80f;
            float removedCommon = baseCommon * t0;

            pCommon = baseCommon - removedCommon;
            pUncommon = baseUncommon + removedCommon * (w0CommonToUncommon / sumStage0);
            pRare = baseRare + removedCommon * (w0CommonToRare / sumStage0);
            pEpic = baseEpic + removedCommon * (w0CommonToEpic / sumStage0);
            pLegendary = baseLegendary + removedCommon * (w0CommonToLegendary / sumStage0);
            pMythic = baseMythic + removedCommon * (w0CommonToMythic / sumStage0);
        }
        else if (luck <= 160f)
        {
            float removedCommon = baseCommon;

            float uStart = baseUncommon + removedCommon * (w0CommonToUncommon / sumStage0);
            float rStart = baseRare + removedCommon * (w0CommonToRare / sumStage0);
            float eStart = baseEpic + removedCommon * (w0CommonToEpic / sumStage0);
            float lStart = baseLegendary + removedCommon * (w0CommonToLegendary / sumStage0);
            float mStart = baseMythic + removedCommon * (w0CommonToMythic / sumStage0);

            float t1 = (luck - 80f) / 80f;
            float removedUncommon = uStart * t1;

            pCommon = 0f;
            pUncommon = uStart - removedUncommon;
            pRare = rStart + removedUncommon * (w1UncommonToRare / sumStage1);
            pEpic = eStart + removedUncommon * (w1UncommonToEpic / sumStage1);
            pLegendary = lStart + removedUncommon * (w1UncommonToLegendary / sumStage1);
            pMythic = mStart + removedUncommon * (w1UncommonToMythic / sumStage1);
        }
        else if (luck <= 240f)
        {
            float removedCommon = baseCommon;

            float u1 = baseUncommon + removedCommon * (w0CommonToUncommon / sumStage0);
            float r1 = baseRare + removedCommon * (w0CommonToRare / sumStage0);
            float e1 = baseEpic + removedCommon * (w0CommonToEpic / sumStage0);
            float l1 = baseLegendary + removedCommon * (w0CommonToLegendary / sumStage0);
            float m1 = baseMythic + removedCommon * (w0CommonToMythic / sumStage0);

            float removedUncommon = u1;
            float r2 = r1 + removedUncommon * (w1UncommonToRare / sumStage1);
            float e2 = e1 + removedUncommon * (w1UncommonToEpic / sumStage1);
            float l2 = l1 + removedUncommon * (w1UncommonToLegendary / sumStage1);
            float m2 = m1 + removedUncommon * (w1UncommonToMythic / sumStage1);

            float t2 = (luck - 160f) / 80f;
            float removedRare = r2 * t2;

            pCommon = 0f;
            pUncommon = 0f;
            pRare = r2 - removedRare;
            pEpic = e2 + removedRare * (w2RareToEpic / sumStage2);
            pLegendary = l2 + removedRare * (w2RareToLegendary / sumStage2);
            pMythic = m2 + removedRare * (w2RareToMythic / sumStage2);
        }
        else if (luck <= 320f)
        {
            float removedCommon = baseCommon;

            float u1 = baseUncommon + removedCommon * (w0CommonToUncommon / sumStage0);
            float r1 = baseRare + removedCommon * (w0CommonToRare / sumStage0);
            float e1 = baseEpic + removedCommon * (w0CommonToEpic / sumStage0);
            float l1 = baseLegendary + removedCommon * (w0CommonToLegendary / sumStage0);
            float m1 = baseMythic + removedCommon * (w0CommonToMythic / sumStage0);

            float removedUncommon = u1;
            float r2 = r1 + removedUncommon * (w1UncommonToRare / sumStage1);
            float e2 = e1 + removedUncommon * (w1UncommonToEpic / sumStage1);
            float l2 = l1 + removedUncommon * (w1UncommonToLegendary / sumStage1);
            float m2 = m1 + removedUncommon * (w1UncommonToMythic / sumStage1);

            float removedRare = r2;
            float e3 = e2 + removedRare * (w2RareToEpic / sumStage2);
            float l3 = l2 + removedRare * (w2RareToLegendary / sumStage2);
            float m3 = m2 + removedRare * (w2RareToMythic / sumStage2);

            float t3 = (luck - 240f) / 80f;

            float epicEnd = e3 * 0.2f;
            float deltaEpic = (e3 - epicEnd) * t3;
            float currentEpic = e3 - deltaEpic;

            pCommon = 0f;
            pUncommon = 0f;
            pRare = 0f;
            pEpic = currentEpic;
            pLegendary = l3 + deltaEpic * (1f / 3f);
            pMythic = m3 + deltaEpic * (2f / 3f);
        }
        else
        {
            float removedCommon = baseCommon;

            float u1 = baseUncommon + removedCommon * (w0CommonToUncommon / sumStage0);
            float r1 = baseRare + removedCommon * (w0CommonToRare / sumStage0);
            float e1 = baseEpic + removedCommon * (w0CommonToEpic / sumStage0);
            float l1 = baseLegendary + removedCommon * (w0CommonToLegendary / sumStage0);
            float m1 = baseMythic + removedCommon * (w0CommonToMythic / sumStage0);

            float removedUncommon = u1;
            float r2 = r1 + removedUncommon * (w1UncommonToRare / sumStage1);
            float e2 = e1 + removedUncommon * (w1UncommonToEpic / sumStage1);
            float l2 = l1 + removedUncommon * (w1UncommonToLegendary / sumStage1);
            float m2 = m1 + removedUncommon * (w1UncommonToMythic / sumStage1);

            float removedRare = r2;
            float e3 = e2 + removedRare * (w2RareToEpic / sumStage2);
            float l3 = l2 + removedRare * (w2RareToLegendary / sumStage2);
            float m3 = m2 + removedRare * (w2RareToMythic / sumStage2);

            float epicStart4 = e3 * 0.2f;
            float deltaEpicStage3 = e3 - epicStart4;
            float legendStart4 = l3 + deltaEpicStage3 * (1f / 3f);
            float mythicStart4 = m3 + deltaEpicStage3 * (2f / 3f);

            float clampedLuck = Mathf.Min(luck, 400f);
            float t4 = (clampedLuck - 320f) / 80f;
            t4 = Mathf.Clamp01(t4);

            float epicEnd4 = epicStart4 * 0.6f;
            float legendEnd4 = legendStart4 * 0.65f;

            float currentEpic = Mathf.Lerp(epicStart4, epicEnd4, t4);
            float currentLegend = Mathf.Lerp(legendStart4, legendEnd4, t4);

            float deltaEpic4 = epicStart4 - currentEpic;
            float deltaLegend4 = legendStart4 - currentLegend;
            float currentMythic = mythicStart4 + deltaEpic4 + deltaLegend4;

            pCommon = 0f;
            pUncommon = 0f;
            pRare = 0f;
            pEpic = currentEpic;
            pLegendary = currentLegend;
            pMythic = currentMythic;
        }

        commonChance = Mathf.Max(0f, pCommon);
        uncommonChance = Mathf.Max(0f, pUncommon);
        rareChance = Mathf.Max(0f, pRare);
        epicChance = Mathf.Max(0f, pEpic);
        legendaryChance = Mathf.Max(0f, pLegendary);
        mythicChance = Mathf.Max(0f, pMythic);
    }
}
