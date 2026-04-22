using UnityEngine;

public class FavourRarityChanceTracker : MonoBehaviour
{
    public static FavourRarityChanceTracker Instance { get; private set; }

    [SerializeField] private float commonFavourChance = 100f;
    [SerializeField] private float uncommonFavourChance = 0f;
    [SerializeField] private float rareFavourChance = 0f;
    [SerializeField] private float epicFavourChance = 0f;
    [SerializeField] private float legendaryFavourChance = 0f;
    [SerializeField] private float mythicFavourChance = 0f;

    [SerializeField] private int currentFavourLuck = 0;

    public float CommonFavourChance => commonFavourChance;
    public float UncommonFavourChance => uncommonFavourChance;
    public float RareFavourChance => rareFavourChance;
    public float EpicFavourChance => epicFavourChance;
    public float LegendaryFavourChance => legendaryFavourChance;
    public float MythicFavourChance => mythicFavourChance;

    public int CurrentFavourLuck => currentFavourLuck;

    private GameObject cachedPlayer;
    private PlayerStats cachedPlayerStats;

    private int lastAppliedFavourLuck = int.MinValue;
    private int nextPlayerSearchFrame;

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

        TryResolvePlayerStats(true);
        RefreshFromFavourLuck(GetPlayerFavourLuck());
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
        TryResolvePlayerStats(false);

        int favourLuck = GetPlayerFavourLuck();
        if (favourLuck != lastAppliedFavourLuck)
        {
            RefreshFromFavourLuck(favourLuck);
        }
    }

    private int GetPlayerFavourLuck()
    {
        if (cachedPlayerStats == null)
        {
            return 0;
        }

        return Mathf.Max(0, cachedPlayerStats.favourLuck);
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

    private void RefreshFromFavourLuck(int favourLuck)
    {
        currentFavourLuck = Mathf.Max(0, favourLuck);
        lastAppliedFavourLuck = currentFavourLuck;

        int luck = Mathf.Clamp(currentFavourLuck, 0, 300);

        float common = 0f;
        float uncommon = 0f;
        float rare = 0f;
        float epic = 0f;
        float legendary = 0f;
        float mythic = 0f;

        if (luck <= 50)
        {
            common = 100f - luck;
            uncommon = luck;
        }
        else if (luck <= 100)
        {
            common = 100f - luck;
            uncommon = 50f;
            rare = luck - 50f;
        }
        else if (luck <= 150)
        {
            uncommon = 150f - luck;
            rare = 50f;
            epic = luck - 100f;
        }
        else if (luck <= 200)
        {
            rare = 200f - luck;
            epic = 50f;
            legendary = luck - 150f;
        }
        else if (luck <= 250)
        {
            epic = 250f - luck;
            legendary = 50f;
            mythic = luck - 200f;
        }
        else
        {
            legendary = 300f - luck;
            mythic = 100f - legendary;
        }

        commonFavourChance = Mathf.Clamp(common, 0f, 100f);
        uncommonFavourChance = Mathf.Clamp(uncommon, 0f, 100f);
        rareFavourChance = Mathf.Clamp(rare, 0f, 100f);
        epicFavourChance = Mathf.Clamp(epic, 0f, 100f);
        legendaryFavourChance = Mathf.Clamp(legendary, 0f, 100f);
        mythicFavourChance = Mathf.Clamp(mythic, 0f, 100f);
    }
}
