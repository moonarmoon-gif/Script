using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;

/// <summary>
/// Manages card selection when player levels up
/// </summary>
public class CardSelectionManager : MonoBehaviour
{
    public static CardSelectionManager Instance;

    [Header("Card Pools")]
    [SerializeField] private List<CoreCards> coreCardPool = new List<CoreCards>();
    [SerializeField] private List<ProjectileCards> projectileCardPool = new List<ProjectileCards>();
    [SerializeField] private List<FavourCards> favourCardPool = new List<FavourCards>();
    [Tooltip("All ProjectileVariantSet assets, one per projectile type, used to populate the variant selector UI.")]
    [SerializeField] private ProjectileVariantSet[] projectileVariantSets;

    [Header("Projectile Initial Cooldown")]
    [Tooltip("Multiplier for initial cooldown when first picking a projectile card (0.5 = half cooldown, 1.0 = full cooldown)")]
    [Range(0f, 1f)]
    public float initialCooldownMultiplier = 0.5f;

    [Header("Selection Settings")]
    [SerializeField] private int cardsToShow = 3;
    [SerializeField] public bool pauseGameOnSelection = true;
    [Tooltip("Delay between card selection stages (Core \u2192 Projectile). TOTAL delay = delayBetweenStages + cardDisplayDelay")]
    [SerializeField] private float delayBetweenStages = 0.5f;
    [Tooltip("Enable two-stage selection: Core cards first, then Projectile cards")]
    [SerializeField] private bool enableTwoStageSelection = true;

    // Track which projectile cards have been picked at least once
    private HashSet<ProjectileCards> pickedProjectileCards = new HashSet<ProjectileCards>();

    // Track which ACTIVE projectile system card has been chosen (by name)
    private string activeProjectileSystemCardName = null;

    // Track the very first ACTIVE projectile chosen at game start (initial free unlock)
    private string initialActiveProjectileSystemCardName = null;

    [Header("UI Timing")]
    [Tooltip("Delay after game freezes before showing card UI")]
    public float cardDisplayDelay = 0.5f;

    public float EnemyCardDisplayDelay = 0.1f;
    public float FavourCardDisplayDelay = 0.1f;

    [Header("Variant Selection Settings")]
    [Tooltip("Delay before showing the variant selection UI after an enhancement tier is reached (seconds, unscaled time)")]
    [SerializeField] private float variantSelectionDelay = 0.1f;

    [Header("Collapse")]
    [SerializeField] private int collapseGraceLevel = 5;

    [Header("Rarity Odds (Must total 100%)")]
    [Range(0f, 100f)] public float commonOdds = 30f;
    [Range(0f, 100f)] public float uncommonOdds = 29f;
    [Range(0f, 100f)] public float rareOdds = 20f;
    [Range(0f, 100f)] public float epicOdds = 15f;
    [Range(0f, 100f)] public float legendaryOdds = 5f;
    [Range(0f, 100f)] public float mythicOdds = 1f;

    [Header("Projectile Base Rarity Weighting")]
    [Tooltip("Chance (percent) to pick a projectile whose BASE rarity exactly matches the rolled rarity when both exact and upgraded options exist. The remainder of the probability goes to upgraded (lower-base-rarity) projectiles.")]
    [Range(0f, 100f)] public float exactBaseRarityWeightPercent = 50f;

    [Header("Favour Soul System")] 
    [Tooltip("When enabled, Favour cards are offered via the Soul-based system (AskForFavourButton) instead of the legacy automatic flow.")]
    private bool useFavourSoulSystem = false;

    [Tooltip("When enabled, Soul level-ups automatically trigger a Favour selection using the soul-level-based rarity odds.")]
    [SerializeField] private bool automaticLevelingFavourSystem = false;

    [Tooltip("Default number of Favour choices when requesting a favour via the Soul-based Soul system.")]
    [SerializeField] private int soulFavourChoices = 3;

    [Tooltip("Delay before showing Favour cards when requested via the Soul-based system (seconds, unscaled time).")]
    public float favourSelectionDelay = 0.1f;

    [Header("Rarity Colors")]
    public Color commonColor = new Color(0.7f, 0.7f, 0.7f);
    public Color uncommonColor = new Color(0.2f, 1f, 0.2f);
    public Color rareColor = new Color(0.3f, 0.5f, 1f);
    public Color epicColor = new Color(0.7f, 0.3f, 1f);
    public Color legendaryColor = new Color(1f, 0.6f, 0f);
    public Color mythicColor = new Color(1f, 0.2f, 0.2f);
    public Color bossColor = new Color(1f, 0.84f, 0f); // Gold color for Boss rarity

    [Header("UI References")]
    [SerializeField] private GameObject cardSelectionUI;
    [SerializeField] private Transform cardContainer;
    [SerializeField] private GameObject projectileCardButtonPrefab;
    [SerializeField] private GameObject coreCardButtonPrefab;
    [SerializeField] private GameObject enemyCardButtonPrefab; // Separate prefab for enemy cards
    [SerializeField] private GameObject favourCardButtonPrefab; // Separate prefab for favour cards
    [SerializeField] private GameObject variantSelectorCardButtonPrefab;
    [SerializeField] private GameObject fireMineCardButtonPrefab;
    [SerializeField] private GameObject frostMineCardButtonPrefab;

    [Header("Mine Element Selection")]
    [SerializeField] private bool enableMineElementSelection = true;

    private GameObject player;
    private bool isSelectionActive = false;
    private bool isFirstStage = true; // True = Core cards, False = Projectile cards
    private bool waitingForSecondStage = false;
    
    // Queue for multiple level-ups
    private Queue<int> pendingLevelUps = new Queue<int>();
    private bool processingLevelUpQueue = false;

    // Queue for pending variant selections so they don't conflict with
    // core/projectile/enemy card selection UIs.
    private struct PendingVariantSelection
    {
        public ProjectileCards card;
        public ProjectileVariantSet set;
        public int tier;
    }

    private Queue<PendingVariantSelection> pendingVariantSelections = new Queue<PendingVariantSelection>();
    private bool processingVariantQueue = false;

    private bool deferVariantSelections = false;

    private Queue<List<BaseCard>> pendingExternalCardSelections = new Queue<List<BaseCard>>();
    private bool processingExternalCardQueue = false;

    private Queue<List<CombinedCard>> pendingExternalCombinedSelections = new Queue<List<CombinedCard>>();
    private bool processingExternalCombinedQueue = false;

    // Ensure we only subscribe once to the enhancement tier event, even if
    // ProjectileCardLevelSystem is created after CardSelectionManager.
    private bool subscribedToTierEvents = false;

    private int pendingNoCommonLevelUpStages = 0;
    private HashSet<string> usedOneTimeFavourCardNames = new HashSet<string>();
    private bool mineElementChoiceMade = false;
    private bool mineUsesAlternateElement = false;
    private bool pendingMineElementSelection = false;
    private ProjectileCards pendingMineCard = null;

    private enum ExternalSelectionPriority
    {
        Core = 0,
        Projectile = 1,
        Favour = 2,
        Enemy = 3,
        Other = 4
    }

    private ExternalSelectionPriority GetExternalSelectionPriority(List<BaseCard> cards)
    {
        if (cards == null || cards.Count == 0)
        {
            return ExternalSelectionPriority.Other;
        }

        bool hasCore = false;
        bool hasProjectile = false;
        bool hasFavour = false;
        bool hasEnemy = false;

        for (int i = 0; i < cards.Count; i++)
        {
            BaseCard c = cards[i];
            if (c == null) continue;

            if (c is CoreCards)
            {
                hasCore = true;
            }
            else if (c is ProjectileCards)
            {
                hasProjectile = true;
            }
            else if (c is FavourCards)
            {
                hasFavour = true;
            }
            else if (c is EnemyCards)
            {
                hasEnemy = true;
            }
        }

        if (hasCore) return ExternalSelectionPriority.Core;
        if (hasProjectile) return ExternalSelectionPriority.Projectile;
        if (hasFavour) return ExternalSelectionPriority.Favour;
        if (hasEnemy) return ExternalSelectionPriority.Enemy;
        return ExternalSelectionPriority.Other;
    }

    private List<BaseCard> DequeueNextExternalSelectionByPriority()
    {
        if (pendingExternalCardSelections == null || pendingExternalCardSelections.Count == 0)
        {
            return null;
        }

        List<BaseCard>[] batches = pendingExternalCardSelections.ToArray();

        int bestIndex = -1;
        int bestPriority = int.MaxValue;
        for (int i = 0; i < batches.Length; i++)
        {
            int priority = (int)GetExternalSelectionPriority(batches[i]);
            if (priority < bestPriority)
            {
                bestPriority = priority;
                bestIndex = i;
                if (bestPriority == (int)ExternalSelectionPriority.Core)
                {
                    break;
                }
            }
        }

        if (bestIndex < 0)
        {
            return pendingExternalCardSelections.Dequeue();
        }

        List<BaseCard> selected = batches[bestIndex];
        pendingExternalCardSelections.Clear();
        for (int i = 0; i < batches.Length; i++)
        {
            if (i == bestIndex) continue;
            pendingExternalCardSelections.Enqueue(batches[i]);
        }

        return selected;
    }

    private int GetPlayerLevelForGating()
    {
        if (player == null)
        {
            return 1;
        }

        PlayerLevel playerLevel = player.GetComponent<PlayerLevel>();
        if (playerLevel != null)
        {
            return playerLevel.CurrentLevel;
        }

        PlayerStats playerStats = player.GetComponent<PlayerStats>();
        if (playerStats != null && playerStats.currentLevel > 0)
        {
            return playerStats.currentLevel;
        }

        return 1;
    }

    private bool IsProjectileCardAllowedByLevel(ProjectileCards card)
    {
        if (card == null)
        {
            return false;
        }

        if (card.projectileType == ProjectileCards.ProjectileType.Collapse)
        {
            return GetPlayerLevelForGating() >= collapseGraceLevel;
        }

        return true;
    }

    /// <summary>
    /// Check if card selection is currently active
    /// </summary>
    public bool IsSelectionActive()
    {
        return isSelectionActive ||
               processingLevelUpQueue || pendingLevelUps.Count > 0 ||
               processingVariantQueue || pendingVariantSelections.Count > 0 ||
               processingExternalCombinedQueue || pendingExternalCombinedSelections.Count > 0 ||
               processingExternalCardQueue || pendingExternalCardSelections.Count > 0;
    }

    public bool UseFavourSoulSystem
    {
        get { return useFavourSoulSystem; }
    }

    public bool AutomaticLevelingFavourSystem
    {
        get { return automaticLevelingFavourSystem; }
    }

    public int SoulFavourChoices
    {
        get { return soulFavourChoices; }
    }

    public void RegisterNoCommonLevelUpStages(int stages)
    {
        if (stages <= 0)
        {
            return;
        }

        pendingNoCommonLevelUpStages += stages;
    }

    public void RegisterOneTimeFavourUsed(FavourCards card)
    {
        if (card == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(card.cardName))
        {
            return;
        }

        if (usedOneTimeFavourCardNames == null)
        {
            usedOneTimeFavourCardNames = new HashSet<string>();
        }

        usedOneTimeFavourCardNames.Add(card.cardName);
    }

    private bool IsOneTimeFavourUsed(FavourCards card)
    {
        if (card == null || usedOneTimeFavourCardNames == null || usedOneTimeFavourCardNames.Count == 0)
        {
            return false;
        }

        return usedOneTimeFavourCardNames.Contains(card.cardName);
    }

    public bool HasPendingLevelUpStages()
    {
        return processingLevelUpQueue || pendingLevelUps.Count > 0 ||
               processingVariantQueue || pendingVariantSelections.Count > 0 ||
               processingExternalCombinedQueue || pendingExternalCombinedSelections.Count > 0 ||
               processingExternalCardQueue || pendingExternalCardSelections.Count > 0 ||
               isSelectionActive;
    }

    public Color GetRarityColor(CardRarity rarity)
    {
        switch (rarity)
        {
            case CardRarity.Common:
                return commonColor;
            case CardRarity.Uncommon:
                return uncommonColor;
            case CardRarity.Rare:
                return rareColor;
            case CardRarity.Epic:
                return epicColor;
            case CardRarity.Legendary:
                return legendaryColor;
            case CardRarity.Mythic:
                return mythicColor;
            case CardRarity.Boss:
                return bossColor;
            default:
                return commonColor;
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

        player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogError("Player not found! Make sure Player has 'Player' tag!");
        }

        // Validate UI references
        if (cardSelectionUI == null)
        {
            Debug.LogError("Card Selection UI not assigned in CardSelectionManager!");
        }
        else
        {
            cardSelectionUI.SetActive(false);
        }

        if (cardContainer == null)
        {
            Debug.LogError("Card Container not assigned in CardSelectionManager!");
        }

        if (projectileCardButtonPrefab == null)
        {
            Debug.LogError("Projectile Card Button Prefab not assigned in CardSelectionManager!");
        }

        // Subscribe to enhancement tier events so we can show variant selection
        if (ProjectileCardLevelSystem.Instance != null)
        {
            ProjectileCardLevelSystem.Instance.OnCardTierIncreased += HandleCardTierIncreased;
            subscribedToTierEvents = true;
        }
    }

    private void Update()
    {
        // Lazy subscription in case ProjectileCardLevelSystem awakens AFTER
        // CardSelectionManager. This guarantees the variant selection UI will
        // still receive tier events regardless of script execution order.
        if (!subscribedToTierEvents && ProjectileCardLevelSystem.Instance != null)
        {
            ProjectileCardLevelSystem.Instance.OnCardTierIncreased += HandleCardTierIncreased;
            subscribedToTierEvents = true;
        }
    }

    public void ForceCloseSelectionUI()
    {
        StopAllCoroutines();

        if (cardSelectionUI != null)
        {
            cardSelectionUI.SetActive(false);
        }

        if (pauseGameOnSelection)
        {
            Time.timeScale = 1f;
        }

        isSelectionActive = false;
        isFirstStage = false;
        waitingForSecondStage = false;

        pendingLevelUps.Clear();
        pendingVariantSelections.Clear();
        pendingExternalCardSelections.Clear();
        pendingExternalCombinedSelections.Clear();

        processingLevelUpQueue = false;
        processingVariantQueue = false;
        processingExternalCardQueue = false;
        processingExternalCombinedQueue = false;

        pendingMineElementSelection = false;
        pendingMineCard = null;
    }

    /// <summary>
    /// Show card selection UI - queues multiple level-ups
    /// </summary>
    public void ShowCardSelection()
    {
        if (GameStateManager.Instance != null && GameStateManager.Instance.PlayerIsDead)
        {
            return;
        }

        // Add to queue
        pendingLevelUps.Enqueue(1);
        
        // Start processing if not already processing
        if (!processingLevelUpQueue && !isSelectionActive)
        {
            // Reset the two-stage selection state
            isFirstStage = true;
            waitingForSecondStage = false;
            StartCoroutine(ProcessLevelUpQueue());
        }
    }
    
    /// <summary>
    /// Process queued level-ups in batches.
    /// For each batch, all CORE stages are shown first, then all PROJECTILE stages.
    /// New level-ups that arrive while a batch is processing are handled in the
    /// next batch once the current one completes.
    /// </summary>
    private IEnumerator ProcessLevelUpQueue()
    {
        processingLevelUpQueue = true;

        while (pendingLevelUps.Count > 0)
        {
            deferVariantSelections = true;

            // Snapshot how many level-ups are currently pending and clear them
            int totalLevels = pendingLevelUps.Count;
            for (int i = 0; i < totalLevels; i++)
            {
                if (pendingLevelUps.Count > 0)
                {
                    pendingLevelUps.Dequeue();
                }
            }

            // Process all CORE card stages for this batch
            for (int i = 0; i < totalLevels; i++)
            {
                // Wait for any variant selections to complete
                while (processingVariantQueue)
                {
                    yield return null;
                }

                yield return StartCoroutine(ShowSingleLevelUpStage(true));

                // Small delay between core card selections
                if (i < totalLevels - 1)
                {
                    yield return new WaitForSecondsRealtime(delayBetweenStages);
                }
            }

            deferVariantSelections = false;
            if (!processingVariantQueue && pendingVariantSelections.Count > 0)
            {
                StartCoroutine(ProcessVariantSelectionQueue());
            }

            while (processingVariantQueue || pendingVariantSelections.Count > 0)
            {
                yield return null;
            }

            // Now process all PROJECTILE card stages for this batch, if enabled
            if (enableTwoStageSelection && totalLevels > 0)
            {
                // Small gap between finishing all cores and starting projectiles
                yield return new WaitForSecondsRealtime(delayBetweenStages * 1.5f);

                for (int i = 0; i < totalLevels; i++)
                {
                    // Wait for any variant selections to complete
                    while (processingVariantQueue)
                    {
                        yield return null;
                    }

                    yield return StartCoroutine(ShowSingleLevelUpStage(false));

                    if (!processingVariantQueue && pendingVariantSelections.Count > 0)
                    {
                        StartCoroutine(ProcessVariantSelectionQueue());
                    }

                    while (processingVariantQueue || pendingVariantSelections.Count > 0)
                    {
                        yield return null;
                    }

                    // Small delay between projectile card selections
                    if (i < totalLevels - 1)
                    {
                        yield return new WaitForSecondsRealtime(delayBetweenStages);
                    }
                }
            }

            if (!processingVariantQueue && pendingVariantSelections.Count > 0)
            {
                StartCoroutine(ProcessVariantSelectionQueue());
            }

            while (processingVariantQueue || pendingVariantSelections.Count > 0)
            {
                yield return null;
            }

            // If more level-ups accumulated during this batch, add a short
            // delay before starting the next batch so the UI feels sequential.
            if (pendingLevelUps.Count > 0)
            {
                yield return new WaitForSecondsRealtime(delayBetweenStages);
            }
        }

        processingLevelUpQueue = false;
    }
    
    /// <summary>
    /// Show a single stage (core or projectile cards)
    /// </summary>
    private IEnumerator ShowSingleLevelUpStage(bool isCoreStage)
    {
        if (GameStateManager.Instance != null && GameStateManager.Instance.PlayerIsDead)
        {
            yield break;
        }

        isSelectionActive = true;
        isFirstStage = isCoreStage;
        waitingForSecondStage = false;

        if (pauseGameOnSelection)
        {
            Time.timeScale = 0f;
        }

        yield return new WaitForSecondsRealtime(cardDisplayDelay);

        if (GameStateManager.Instance != null && GameStateManager.Instance.PlayerIsDead)
        {
            ForceCloseSelectionUI();
            yield break;
        }

        bool excludeCommon = pendingNoCommonLevelUpStages > 0;

        List<CombinedCard> selectedCards;
        if (isCoreStage)
        {
            selectedCards = GenerateRandomCombinedCards(cardsToShow, excludeCommon);
        }
        else
        {
            selectedCards = GenerateRandomProjectileCards(cardsToShow, excludeCommon);
        }

        if (cardSelectionUI != null)
        {
            cardSelectionUI.SetActive(true);
            if (isCoreStage)
            {
                DisplayCombinedCards(selectedCards);
            }
            else
            {
                DisplayProjectileCards(selectedCards);
            }
        }
        
        if (excludeCommon && pendingNoCommonLevelUpStages > 0)
        {
            pendingNoCommonLevelUpStages--;
        }
        
        // Wait until player selects a card
        while (isSelectionActive)
        {
            yield return null;
        }
    }

    private IEnumerator ShowCardsAfterDelay()
    {
        if (GameStateManager.Instance != null && GameStateManager.Instance.PlayerIsDead)
        {
            yield break;
        }

        // Use the same delay as other card displays
        yield return new WaitForSecondsRealtime(cardDisplayDelay);

        if (GameStateManager.Instance != null && GameStateManager.Instance.PlayerIsDead)
        {
            yield break;
        }

        List<CombinedCard> selectedCards = GenerateRandomCombinedCards(cardsToShow, false);

        if (cardSelectionUI != null)
        {
            cardSelectionUI.SetActive(true);
            
            // Ensure the cards are visible
            CanvasGroup canvasGroup = cardSelectionUI.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }
            
            DisplayCombinedCards(selectedCards);
        }
    }

    public IEnumerator ShowInitialActiveProjectileSelection(int count)
    {
        if (GameStateManager.Instance != null && GameStateManager.Instance.PlayerIsDead)
        {
            yield break;
        }

        while (isSelectionActive)
        {
            yield return null;
        }

        isSelectionActive = true;
        isFirstStage = false;
        waitingForSecondStage = false;

        if (pauseGameOnSelection)
        {
            Time.timeScale = 0f;
        }

        yield return new WaitForSecondsRealtime(cardDisplayDelay);

        if (GameStateManager.Instance != null && GameStateManager.Instance.PlayerIsDead)
        {
            ForceCloseSelectionUI();
            yield break;
        }

        List<CombinedCard> cards = GenerateRandomActiveProjectileCards(count, true);
        if (cards.Count == 0)
        {
            if (pauseGameOnSelection)
            {
                Time.timeScale = 1f;
            }
            isSelectionActive = false;
            yield break;
        }

        if (cardSelectionUI != null)
        {
            cardSelectionUI.SetActive(true);
            DisplayCombinedCards(cards);
        }

        while (isSelectionActive)
        {
            yield return null;
        }
    }

    /// <summary>
    /// Generate random CORE cards only
    /// </summary>
    private List<CombinedCard> GenerateRandomCombinedCards(int count, bool excludeCommon)
    {
        List<CombinedCard> cards = new List<CombinedCard>();
        HashSet<CoreCards> usedCoreCards = new HashSet<CoreCards>();

        // Filter out any core cards with spawnChance <= 0
        List<CoreCards> eligibleCoreCards = coreCardPool.Where(c => c != null && c.spawnChance > 0f).ToList();

        if (eligibleCoreCards.Count == 0)
        {
            return cards;
        }

        int targetCount = Mathf.Min(count, eligibleCoreCards.Count);
        int maxAttempts = 1000;
        int attempts = 0;

        while (cards.Count < targetCount && attempts < maxAttempts)
        {
            attempts++;
            CardRarity targetRarity = GetRandomRarity();
            if (excludeCommon && targetRarity == CardRarity.Common)
            {
                targetRarity = CardRarity.Uncommon;
            }

            CoreCards basicCard = GetRandomCoreCardOfRarity(targetRarity, usedCoreCards);
            if (basicCard == null) continue;

            usedCoreCards.Add(basicCard);

            // Core stage now only produces a single CoreCards-based CombinedCard.
            // Projectile modifiers are handled elsewhere and no longer come from a pool.
            cards.Add(new CombinedCard(basicCard, null, targetRarity));
        }

        return cards;
    }

    private List<CombinedCard> GenerateRandomActiveProjectileCards(int count, bool forceCommonRarity = false)
    {
        List<CombinedCard> cards = new List<CombinedCard>();
        HashSet<ProjectileCards> usedProjectileCards = new HashSet<ProjectileCards>();

        if (projectileCardPool.Count == 0)
        {
            return cards;
        }

        var activeCards = projectileCardPool.Where(c => c != null && c.spawnChance > 0f && c.projectileSystem == ProjectileCards.ProjectileSystemType.Active).ToList();
        if (activeCards.Count == 0)
        {
            return cards;
        }

        for (int i = 0; i < count; i++)
        {
            var available = activeCards.Where(c => !usedProjectileCards.Contains(c)).ToList();
            if (available.Count == 0)
            {
                break;
            }
            ProjectileCards projCard = available[Random.Range(0, available.Count)];
            // For the very first active projectile selection at game start, we force Common rarity
            // so the player always starts from a basic version.
            CardRarity baseRarity = projCard.rarity;
            CardRarity targetRarity = forceCommonRarity ? baseRarity : GetRandomRarity();
            if (targetRarity < baseRarity)
            {
                targetRarity = baseRarity;
            }
            CardRarity assignedRarity = targetRarity;

            // Create a runtime copy to avoid modifying the ScriptableObject
            ProjectileCards runtimeCopy = Instantiate(projCard);
            runtimeCopy.runtimeBaseRarity = projCard.rarity;
            runtimeCopy.rarity = assignedRarity;

            // For the initial active projectile selection at game start, treat this
            // runtime instance as a "free" unlock that does not grant levels once.
            if (forceCommonRarity)
            {
                runtimeCopy.suppressLevelGainOnce = true;
            }

            // Select modifiers based on the assigned rarity
            runtimeCopy.OnRarityAssigned();

            usedProjectileCards.Add(projCard);
            pickedProjectileCards.Add(projCard);

            CombinedCard combinedCard = new CombinedCard(null, null, assignedRarity) { projectileCard = runtimeCopy };
            cards.Add(combinedCard);

            if (projCard.projectileSystem == ProjectileCards.ProjectileSystemType.Active)
            {
                // Remember the first ACTIVE projectile OFFERED at game start as the "free" unlock
                // so it can suppress level gain/modifiers once. The ACTUAL active projectile system
                // card that governs all future draws is bound WHEN THE PLAYER SELECTS a card
                // (in SelectCombinedCard), not here.
                if (forceCommonRarity && string.IsNullOrEmpty(initialActiveProjectileSystemCardName))
                {
                    initialActiveProjectileSystemCardName = projCard.cardName;
                }
            }
        }

        return cards;
    }

    private ProjectileCards GetRandomProjectileCard(HashSet<ProjectileCards> usedCards, out CardRarity assignedRarity)
    {
        // Get a random rarity based on odds
        CardRarity targetRarity = GetRandomRarity();

        // Get available cards (not used in this selection, spawnChance > 0)
        var availableCards = projectileCardPool
            .Where(c => c != null && c.spawnChance > 0f && !usedCards.Contains(c))
            .ToList();

        availableCards = availableCards.Where(IsProjectileCardAllowedByLevel).ToList();

        // If an active projectile system card is already chosen, block all OTHER active-system cards
        if (!string.IsNullOrEmpty(activeProjectileSystemCardName))
        {
            availableCards = availableCards.Where(c =>
                c.projectileSystem != ProjectileCards.ProjectileSystemType.Active ||
                c.cardName == activeProjectileSystemCardName).ToList();
        }

        if (availableCards.Count == 0)
        {
            assignedRarity = CardRarity.Common;
            return null;
        }

        // Only cards whose BASE rarity is <= target rarity can appear at this rolled rarity
        var eligibleCards = availableCards.Where(c => c.rarity <= targetRarity).ToList();
        if (eligibleCards.Count == 0)
        {
            // Try a lower target rarity, same as previous behaviour
            if (targetRarity > CardRarity.Common)
            {
                return GetRandomProjectileCardOfRarity(targetRarity - 1, usedCards, out assignedRarity);
            }

            assignedRarity = CardRarity.Common;
            return null;
        }

        // Separate into first-time and already-picked for preference
        var firstTimeEligible = eligibleCards.Where(c => !pickedProjectileCards.Contains(c)).ToList();
        var repeatEligible = eligibleCards.Where(c => pickedProjectileCards.Contains(c)).ToList();

        // Helper local function: apply configurable weighting between exact and upgraded groups
        ProjectileCards SelectWeighted(List<ProjectileCards> exact, List<ProjectileCards> upgraded, string debugPrefix)
        {
            bool hasExact = exact.Count > 0;
            bool hasUpgraded = upgraded.Count > 0;

            if (!hasExact && !hasUpgraded)
            {
                return null;
            }

            if (hasExact && hasUpgraded)
            {
                float roll = Random.value;
                float exactWeight = Mathf.Clamp01(exactBaseRarityWeightPercent / 100f);
                float upgradedWeight = 1f - exactWeight;

                if (roll <= exactWeight)
                {
                    ProjectileCards c = exact[Random.Range(0, exact.Count)];
                    float exactPct = exactWeight * 100f;
                    float upgradedPct = upgradedWeight * 100f;
                    Debug.Log($"<color=cyan>{debugPrefix} EXACT ({exactPct:F1}% group): {c.cardName} | Base Rarity: {c.rarity} | Assigned: {targetRarity}</color>");
                    return c;
                }
                else
                {
                    ProjectileCards c = upgraded[Random.Range(0, upgraded.Count)];
                    float exactPct = exactWeight * 100f;
                    float upgradedPct = upgradedWeight * 100f;
                    Debug.Log($"<color=yellow>{debugPrefix} UPGRADED ({upgradedPct:F1}% group, exact={exactPct:F1}%): {c.cardName} | Base Rarity: {c.rarity} | Assigned: {targetRarity}</color>");
                    return c;
                }
            }

            if (hasExact)
            {
                ProjectileCards c = exact[Random.Range(0, exact.Count)];
                Debug.Log($"<color=cyan>{debugPrefix} EXACT (100% group): {c.cardName} | Base Rarity: {c.rarity} | Assigned: {targetRarity}</color>");
                return c;
            }
            else
            {
                ProjectileCards c = upgraded[Random.Range(0, upgraded.Count)];
                Debug.Log($"<color=yellow>{debugPrefix} UPGRADED (100% group): {c.cardName} | Base Rarity: {c.rarity} | Assigned: {targetRarity}</color>");
                return c;
            }
        }

        // 1) Prefer first-time cards if any are available
        if (firstTimeEligible.Count > 0)
        {
            var firstTimeExact = firstTimeEligible.Where(c => c.rarity == targetRarity).ToList();
            var firstTimeUpgraded = firstTimeEligible.Where(c => c.rarity < targetRarity).ToList();

            ProjectileCards selected = SelectWeighted(firstTimeExact, firstTimeUpgraded, "✓ FIRST PICK");
            if (selected != null)
            {
                assignedRarity = targetRarity;
                pickedProjectileCards.Add(selected);

                if (selected.projectileSystem == ProjectileCards.ProjectileSystemType.Active && string.IsNullOrEmpty(activeProjectileSystemCardName))
                {
                    activeProjectileSystemCardName = selected.cardName;
                }

                return selected;
            }
        }

        // 2) Fallback to repeat cards, still using the same exact vs upgraded weighting
        if (repeatEligible.Count > 0)
        {
            var repeatExact = repeatEligible.Where(c => c.rarity == targetRarity).ToList();
            var repeatUpgraded = repeatEligible.Where(c => c.rarity < targetRarity).ToList();

            ProjectileCards selected = SelectWeighted(repeatExact, repeatUpgraded, "↻ REPEAT PICK");
            if (selected != null)
            {
                assignedRarity = targetRarity;

                if (selected.projectileSystem == ProjectileCards.ProjectileSystemType.Active && string.IsNullOrEmpty(activeProjectileSystemCardName))
                {
                    activeProjectileSystemCardName = selected.cardName;
                }

                return selected;
            }
        }

        // If we somehow reach here, fall back to a lower rarity as before
        if (targetRarity > CardRarity.Common)
        {
            return GetRandomProjectileCardOfRarity(targetRarity - 1, usedCards, out assignedRarity);
        }

        assignedRarity = CardRarity.Common;
        return null;
    }
    
    private ProjectileCards GetRandomProjectileCardOfRarity(CardRarity targetRarity, HashSet<ProjectileCards> usedCards, out CardRarity assignedRarity)
    {
        // Cards can spawn at their assigned rarity OR ANY HIGHER rarity
        // e.g., Common card (rarity=0) can spawn as any rarity (0-6)
        // e.g., Legendary card (rarity=5) can only spawn as Legendary (5) or Mythic (6)
        var matchingCards = projectileCardPool.Where(c => c != null && c.spawnChance > 0f && c.rarity <= targetRarity && !usedCards.Contains(c)).ToList();

        matchingCards = matchingCards.Where(IsProjectileCardAllowedByLevel).ToList();

        // Respect single ACTIVE projectile system rule
        if (!string.IsNullOrEmpty(activeProjectileSystemCardName))
        {
            matchingCards = matchingCards.Where(c =>
                c.projectileSystem != ProjectileCards.ProjectileSystemType.Active ||
                c.cardName == activeProjectileSystemCardName).ToList();
        }

        if (matchingCards.Count == 0)
        {
            if (targetRarity > CardRarity.Common)
            {
                return GetRandomProjectileCardOfRarity(targetRarity - 1, usedCards, out assignedRarity);
            }
            else
            {
                var unusedCards = projectileCardPool.Where(c => c != null && c.spawnChance > 0f && !usedCards.Contains(c)).ToList();
                unusedCards = unusedCards.Where(IsProjectileCardAllowedByLevel).ToList();
                if (unusedCards.Count > 0)
                {
                    ProjectileCards fallbackCard = unusedCards[Random.Range(0, unusedCards.Count)];
                    assignedRarity = targetRarity;
                    if (assignedRarity < fallbackCard.rarity)
                    {
                        assignedRarity = fallbackCard.rarity;
                    }
                    return fallbackCard;
                }
                assignedRarity = CardRarity.Common;
                return null;
            }
        }

        ProjectileCards selectedCard = matchingCards[Random.Range(0, matchingCards.Count)];
        assignedRarity = targetRarity;
        if (selectedCard.projectileSystem == ProjectileCards.ProjectileSystemType.Active && string.IsNullOrEmpty(activeProjectileSystemCardName))
        {
            activeProjectileSystemCardName = selectedCard.cardName;
        }
        return selectedCard;
    }

    private ProjectileCards GetRandomPassiveProjectileCardOfRarity(CardRarity targetRarity, HashSet<ProjectileCards> usedCards)
    {
        var matchingCards = projectileCardPool
            .Where(c => c != null && c.spawnChance > 0f && c.projectileSystem == ProjectileCards.ProjectileSystemType.Passive && c.rarity <= targetRarity && !usedCards.Contains(c))
            .ToList();

        matchingCards = matchingCards.Where(IsProjectileCardAllowedByLevel).ToList();

        if (matchingCards.Count == 0)
        {
            if (targetRarity > CardRarity.Common)
            {
                return GetRandomPassiveProjectileCardOfRarity(targetRarity - 1, usedCards);
            }
            else
            {
                var unusedCards = projectileCardPool
                    .Where(c => c != null && c.spawnChance > 0f && c.projectileSystem == ProjectileCards.ProjectileSystemType.Passive && !usedCards.Contains(c))
                    .ToList();
                unusedCards = unusedCards.Where(IsProjectileCardAllowedByLevel).ToList();
                if (unusedCards.Count > 0)
                {
                    return unusedCards[Random.Range(0, unusedCards.Count)];
                }
                return null;
            }
        }

        return matchingCards[Random.Range(0, matchingCards.Count)];
    }

    private CoreCards GetRandomCoreCardOfRarity(CardRarity targetRarity, HashSet<CoreCards> usedCards)
    {
        // Cards can spawn at their assigned rarity OR ANY HIGHER rarity
        // e.g., Common card can spawn as Common, Uncommon, Rare, Epic, Legendary, Mythic
        // e.g., Epic card can only spawn as Epic, Legendary, or Mythic
        var matchingCards = coreCardPool.Where(c => c != null && c.spawnChance > 0f && c.rarity <= targetRarity && !usedCards.Contains(c)).ToList();

        if (matchingCards.Count == 0)
        {
            if (targetRarity > CardRarity.Common)
            {
                return GetRandomCoreCardOfRarity(targetRarity - 1, usedCards);
            }
            else
            {
                var unusedCards = coreCardPool.Where(c => c != null && c.spawnChance > 0f && !usedCards.Contains(c)).ToList();
                if (unusedCards.Count > 0)
                {
                    return unusedCards[Random.Range(0, unusedCards.Count)];
                }
                return null;
            }
        }

        return matchingCards[Random.Range(0, matchingCards.Count)];
    }

    private ProjectileModifierCoreCards GetRandomModifierCardOfRarity(CardRarity targetRarity, HashSet<ProjectileModifierCoreCards> usedCards)
    {
        // Projectile modifier cards are no longer drawn from a pool in CardSelectionManager.
        // This method is kept for compatibility but always returns null.
        return null;
    }

    /// <summary>
    /// Generate random PROJECTILE MODIFIER cards only (renamed from GenerateRandomProjectileCards)
    /// </summary>
    private List<BaseCard> GenerateRandomProjectileModifierCards(int count)
    {
        // Projectile modifier cards are now handled directly and no longer drawn via this pool.
        // Keep method for backwards compatibility, but return an empty list.
        return new List<BaseCard>();
    }

    /// <summary>
    /// Generate random cards based on rarity odds (all types mixed)
    /// </summary>
    private List<BaseCard> GenerateRandomCards(int count)
    {
        List<BaseCard> cards = new List<BaseCard>();
        HashSet<BaseCard> usedCards = new HashSet<BaseCard>();
        List<BaseCard> allCards = new List<BaseCard>();

        // Legacy mixed-card generator: now only uses coreCardPool.
        allCards.AddRange(coreCardPool.Where(c => c != null && c.spawnChance > 0f).Cast<BaseCard>());

        if (allCards.Count == 0)
        {
            return cards;
        }
        HashSet<CardRarity> allowedRarities = new HashSet<CardRarity>(allCards.Select(c => c.rarity));

        int targetCount = Mathf.Min(count, allCards.Count);
        int maxAttempts = 1000;
        int attempts = 0;

        while (cards.Count < targetCount && attempts < maxAttempts)
        {
            attempts++;
            CardRarity targetRarity = GetRandomRarity(allowedRarities);
            BaseCard card = GetRandomCardOfRarity(allCards, targetRarity, usedCards);

            if (card != null)
            {
                cards.Add(card);
                usedCards.Add(card);
            }
        }

        return cards;
    }

    /// <summary>
    /// Generate FAVOUR cards based on enemy card rarity history.
    /// When forceBossRarity is true, we generate Boss-rarity favour cards
    /// only. Otherwise we build a one-to-one mapping from the recent
    /// non-boss enemy rarities in the history to favour card rarities,
    /// with no duplicate favour cards within a single selection.
    /// </summary>
    private List<BaseCard> GenerateFavourCards(int count, List<CardRarity> enemyRarityHistory, bool forceBossRarity)
    {
        List<BaseCard> result = new List<BaseCard>();

        if (favourCardPool == null || favourCardPool.Count == 0)
        {
            Debug.LogWarning("<color=yellow>CardSelectionManager: No Favour cards configured in favourCardPool.</color>");
            return result;
        }

        HashSet<FavourCards> usedCards = new HashSet<FavourCards>();

        // Special post-boss case: show ONLY Boss-rarity favour cards.
        if (forceBossRarity)
        {
            var bossCandidates = favourCardPool
                .Where(c => c != null && c.spawnChance > 0f && c.rarity == CardRarity.Boss && !usedCards.Contains(c) && !IsOneTimeFavourUsed(c))
                .ToList();

            int targetCount = Mathf.Min(count, bossCandidates.Count);

            for (int i = 0; i < targetCount; i++)
            {
                if (bossCandidates.Count == 0)
                {
                    break;
                }

                int index = Random.Range(0, bossCandidates.Count);
                FavourCards baseCard = bossCandidates[index];
                bossCandidates.RemoveAt(index);
                usedCards.Add(baseCard);

                FavourCards instance = Instantiate(baseCard);
                result.Add(instance);
            }

            if (result.Count == 0)
            {
                Debug.LogWarning("<color=yellow>CardSelectionManager: No Boss-rarity Favour cards available for post-boss selection.</color>");
            }

            return result;
        }

        // Regular case: build a one-to-one mapping from NON-BOSS enemy
        // rarities in the history to favour rarities.
        if (enemyRarityHistory == null || enemyRarityHistory.Count == 0)
        {
            Debug.Log("<color=yellow>CardSelectionManager: No enemy rarity history available for Favour selection, skipping.</color>");
            return result;
        }

        // Filter out Boss entries - they do not contribute to regular
        // favour odds.
        List<CardRarity> nonBossHistory = enemyRarityHistory
            .Where(r => r != CardRarity.Boss)
            .ToList();

        if (nonBossHistory.Count == 0)
        {
            Debug.Log("<color=yellow>CardSelectionManager: Enemy history contained only Boss rarities, skipping Favour selection.</color>");
            return result;
        }

        // Map from the MOST RECENT non-boss enemy picks, up to the
        // requested count. This creates a per-enemy one-to-one mapping
        // between recent enemy-card rarities and favour-card rarities.
        int maxCards = Mathf.Min(count, nonBossHistory.Count);
        int startIndex = Mathf.Max(0, nonBossHistory.Count - maxCards);

        for (int i = startIndex; i < nonBossHistory.Count; i++)
        {
            CardRarity targetRarity = nonBossHistory[i];

            FavourCards baseCard = GetRandomFavourCardOfRarity(targetRarity, usedCards);
            if (baseCard == null)
            {
                Debug.LogWarning($"<color=yellow>CardSelectionManager: No Favour card found for rarity {targetRarity}.");
                continue;
            }

            usedCards.Add(baseCard);

            FavourCards instance = Instantiate(baseCard);
            result.Add(instance);
        }

        return result;
    }

    /// <summary>
    /// Helper for selecting a Favour card of a given rarity from favourCardPool,
    /// respecting per-card spawnChance, one-time flags, already-used cards, and
    /// any internal pick limits declared on the favour effect itself.
    /// </summary>
    private FavourCards GetRandomFavourCardOfRarity(CardRarity targetRarity, HashSet<FavourCards> usedCards)
    {
        if (favourCardPool == null || favourCardPool.Count == 0)
        {
            return null;
        }

        List<FavourCards> candidates = favourCardPool
            .Where(c => c != null
                        && c.spawnChance > 0f
                        && c.rarity == targetRarity
                        && !usedCards.Contains(c)
                        && !IsOneTimeFavourUsed(c))
            .ToList();

        // Enforce per-effect pick limits for ANY favour effect that implements
        // IFavourPickLimit. This replaces the previous special case for
        // ProjectileCooldownReductionFavour.
        candidates.RemoveAll(card =>
        {
            if (card == null || card.favourEffect == null) return false;

            IFavourPickLimit limited = card.favourEffect as IFavourPickLimit;
            if (limited == null) return false;

            return limited.IsAtPickLimit();
        });

        if (candidates.Count == 0)
        {
            return null;
        }

        // Weighted random selection based on spawnChance
        float totalWeight = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            totalWeight += Mathf.Max(0f, candidates[i].spawnChance);
        }

        if (totalWeight <= 0f)
        {
            return candidates[Random.Range(0, candidates.Count)];
        }

        float roll = Random.Range(0f, totalWeight);
        float accumulator = 0f;

        for (int i = 0; i < candidates.Count; i++)
        {
            float weight = Mathf.Max(0f, candidates[i].spawnChance);
            accumulator += weight;
            if (roll <= accumulator)
            {
                return candidates[i];
            }
        }

        return candidates[candidates.Count - 1];
    }

    /// <summary>
    /// Get random rarity based on odds (modified by player luck)
    /// </summary>
    private CardRarity GetRandomRarity()
    {
        return GetRandomRarity(null);
    }

    /// <summary>
    /// Get random rarity based on odds (modified by player luck), limited to an allowed set.
    /// Luck behaviour (piecewise):
    ///  - 0–80   : drain Common into higher tiers (Uncommon gets most, Mythic least)
    ///  - 80–160 : drain Uncommon into higher tiers (Rare gets most, Mythic least)
    ///  - 160–240: drain Rare into higher tiers (Epic gets most, Mythic least)
    ///  - 240–320: drain Epic into Legendary/Mythic (both increase)
    ///  - 320+   : slowly drain Epic and Legendary to feed Mythic, keeping Epic/Legendary > 0
    /// This function computes a full rarity distribution, then optionally restricts it
    /// to allowedRarities by re-normalizing.
    /// </summary>
    private CardRarity GetRandomRarity(HashSet<CardRarity> allowedRarities)
    {
        // Get player luck stat (clamped to 0+)
        float playerLuck = 0f;
        if (player != null)
        {
            PlayerStats stats = player.GetComponent<PlayerStats>();
            if (stats != null)
            {
                playerLuck = Mathf.Max(0f, stats.luck);
            }
        }

        // Effective luck is scaled down so that achieving the same rarity
        // shift now requires roughly twice as much PlayerStats.luck as before.
        float luck = Mathf.Max(0f, playerLuck) * 0.5f;

        // Base odds from inspector (should sum to ~100 via OnValidate)
        float baseCommon = commonOdds;
        float baseUncommon = uncommonOdds;
        float baseRare = rareOdds;
        float baseEpic = epicOdds;
        float baseLegendary = legendaryOdds;
        float baseMythic = mythicOdds;

        float pCommon;
        float pUncommon;
        float pRare;
        float pEpic;
        float pLegendary;
        float pMythic;

        // Constants for redistribution weights
        const float w0CommonToUncommon = 5f;
        const float w0CommonToRare = 4f;
        const float w0CommonToEpic = 3f;
        const float w0CommonToLegendary = 2f;
        const float w0CommonToMythic = 1f;
        const float sumStage0 = w0CommonToUncommon + w0CommonToRare + w0CommonToEpic + w0CommonToLegendary + w0CommonToMythic; // 15

        const float w1UncommonToRare = 4f;
        const float w1UncommonToEpic = 3f;
        const float w1UncommonToLegendary = 2f;
        const float w1UncommonToMythic = 1f;
        const float sumStage1 = w1UncommonToRare + w1UncommonToEpic + w1UncommonToLegendary + w1UncommonToMythic; // 10

        const float w2RareToEpic = 3f;
        const float w2RareToLegendary = 2f;
        const float w2RareToMythic = 1f;
        const float sumStage2 = w2RareToEpic + w2RareToLegendary + w2RareToMythic; // 6

        // Piecewise luck stages
        if (luck <= 80f)
        {
            // Stage 0: drain Common only, over 0–80 luck
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
            // Stage 0 complete, Stage 1 partial (drain Uncommon over next 80 luck)
            float removedCommon = baseCommon; // fully drained

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
            // Stage 0 and 1 complete, Stage 2 partial (drain Rare over next 80 luck)
            float removedCommon = baseCommon; // fully drained

            // After Stage 0
            float u1 = baseUncommon + removedCommon * (w0CommonToUncommon / sumStage0);
            float r1 = baseRare + removedCommon * (w0CommonToRare / sumStage0);
            float e1 = baseEpic + removedCommon * (w0CommonToEpic / sumStage0);
            float l1 = baseLegendary + removedCommon * (w0CommonToLegendary / sumStage0);
            float m1 = baseMythic + removedCommon * (w0CommonToMythic / sumStage0);

            // After Stage 1 complete (Uncommon fully drained)
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
            // Stage 0,1,2 complete, Stage 3 partial (drain Epic into Legendary/Mythic)
            float removedCommon = baseCommon; // fully drained

            // After Stage 0
            float u1 = baseUncommon + removedCommon * (w0CommonToUncommon / sumStage0);
            float r1 = baseRare + removedCommon * (w0CommonToRare / sumStage0);
            float e1 = baseEpic + removedCommon * (w0CommonToEpic / sumStage0);
            float l1 = baseLegendary + removedCommon * (w0CommonToLegendary / sumStage0);
            float m1 = baseMythic + removedCommon * (w0CommonToMythic / sumStage0);

            // After Stage 1 complete
            float removedUncommon = u1;
            float r2 = r1 + removedUncommon * (w1UncommonToRare / sumStage1);
            float e2 = e1 + removedUncommon * (w1UncommonToEpic / sumStage1);
            float l2 = l1 + removedUncommon * (w1UncommonToLegendary / sumStage1);
            float m2 = m1 + removedUncommon * (w1UncommonToMythic / sumStage1);

            // After Stage 2 complete (Rare fully drained)
            float removedRare = r2;
            float e3 = e2 + removedRare * (w2RareToEpic / sumStage2);
            float l3 = l2 + removedRare * (w2RareToLegendary / sumStage2);
            float m3 = m2 + removedRare * (w2RareToMythic / sumStage2);

            float t3 = (luck - 240f) / 80f;

            // Epic drains from e3 down to ~20% of its Stage-2-complete value over this range
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
            // Stage 0,1,2,3 complete, Stage 4 partial (slowly drain Epic & Legendary to feed Mythic)
            float removedCommon = baseCommon; // fully drained

            // After Stage 0
            float u1 = baseUncommon + removedCommon * (w0CommonToUncommon / sumStage0);
            float r1 = baseRare + removedCommon * (w0CommonToRare / sumStage0);
            float e1 = baseEpic + removedCommon * (w0CommonToEpic / sumStage0);
            float l1 = baseLegendary + removedCommon * (w0CommonToLegendary / sumStage0);
            float m1 = baseMythic + removedCommon * (w0CommonToMythic / sumStage0);

            // After Stage 1 complete
            float removedUncommon = u1;
            float r2 = r1 + removedUncommon * (w1UncommonToRare / sumStage1);
            float e2 = e1 + removedUncommon * (w1UncommonToEpic / sumStage1);
            float l2 = l1 + removedUncommon * (w1UncommonToLegendary / sumStage1);
            float m2 = m1 + removedUncommon * (w1UncommonToMythic / sumStage1);

            // After Stage 2 complete
            float removedRare = r2;
            float e3 = e2 + removedRare * (w2RareToEpic / sumStage2);
            float l3 = l2 + removedRare * (w2RareToLegendary / sumStage2);
            float m3 = m2 + removedRare * (w2RareToMythic / sumStage2);

            // After Stage 3 complete (Epic drained to 20% of e3)
            float epicStart4 = e3 * 0.2f;
            float deltaEpicStage3 = e3 - epicStart4;
            float legendStart4 = l3 + deltaEpicStage3 * (1f / 3f);
            float mythicStart4 = m3 + deltaEpicStage3 * (2f / 3f);

            // Stage 4 (320–400): slowly drain Epic and Legendary, all of it goes to Mythic.
            // Beyond 400 luck, treat as saturated at t4 = 1.
            float clampedLuck = Mathf.Min(luck, 400f);
            float t4 = (clampedLuck - 320f) / 80f;
            t4 = Mathf.Clamp01(t4);

            float epicEnd4 = epicStart4 * 0.6f;      // keep Epic above 0
            float legendEnd4 = legendStart4 * 0.65f; // keep Legendary above 0

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

        // Clamp any tiny negatives due to floating point
        pCommon = Mathf.Max(0f, pCommon);
        pUncommon = Mathf.Max(0f, pUncommon);
        pRare = Mathf.Max(0f, pRare);
        pEpic = Mathf.Max(0f, pEpic);
        pLegendary = Mathf.Max(0f, pLegendary);
        pMythic = Mathf.Max(0f, pMythic);

        // Now sample from the distribution, optionally restricted to allowedRarities
        if (allowedRarities != null && allowedRarities.Count > 0)
        {
            float total = 0f;
            if (allowedRarities.Contains(CardRarity.Mythic)) total += pMythic;
            if (allowedRarities.Contains(CardRarity.Legendary)) total += pLegendary;
            if (allowedRarities.Contains(CardRarity.Epic)) total += pEpic;
            if (allowedRarities.Contains(CardRarity.Rare)) total += pRare;
            if (allowedRarities.Contains(CardRarity.Uncommon)) total += pUncommon;
            if (allowedRarities.Contains(CardRarity.Common)) total += pCommon;

            if (total <= 0f)
            {
                // Fallback to global distribution if subset has no weight
                return GetRandomRarity(null);
            }

            float roll = Random.Range(0f, total);
            float cumulative = 0f;

            if (allowedRarities.Contains(CardRarity.Mythic))
            {
                cumulative += pMythic;
                if (roll < cumulative)
                {
                    Debug.Log($"<color=gold>★ MYTHIC card rolled! (Luck: {playerLuck})</color>");
                    return CardRarity.Mythic;
                }
            }

            if (allowedRarities.Contains(CardRarity.Legendary))
            {
                cumulative += pLegendary;
                if (roll < cumulative)
                {
                    Debug.Log($"<color=orange>★ LEGENDARY card rolled! (Luck: {playerLuck})</color>");
                    return CardRarity.Legendary;
                }
            }

            if (allowedRarities.Contains(CardRarity.Epic))
            {
                cumulative += pEpic;
                if (roll < cumulative)
                {
                    Debug.Log($"<color=purple>★ EPIC card rolled! (Luck: {playerLuck})</color>");
                    return CardRarity.Epic;
                }
            }

            if (allowedRarities.Contains(CardRarity.Rare))
            {
                cumulative += pRare;
                if (roll < cumulative)
                {
                    Debug.Log($"<color=blue>RARE card rolled (Luck: {playerLuck})</color>");
                    return CardRarity.Rare;
                }
            }

            if (allowedRarities.Contains(CardRarity.Uncommon))
            {
                cumulative += pUncommon;
                if (roll < cumulative)
                {
                    Debug.Log($"<color=green>Uncommon card rolled (Luck: {playerLuck})</color>");
                    return CardRarity.Uncommon;
                }
            }

            if (allowedRarities.Contains(CardRarity.Common))
            {
                cumulative += pCommon;
                if (roll < cumulative)
                {
                    Debug.Log($"Common card rolled (Luck: {playerLuck})");
                    return CardRarity.Common;
                }
            }

            // Fallback: pick the lowest allowed rarity
            CardRarity minAllowed = allowedRarities.Min();
            return minAllowed;
        }

        // Global distribution across all rarities
        float totalAll = pCommon + pUncommon + pRare + pEpic + pLegendary + pMythic;
        if (totalAll <= 0f)
        {
            // If odds are somehow all zero, default to Common
            Debug.LogWarning("GetRandomRarity: Total odds were zero, defaulting to Common.");
            return CardRarity.Common;
        }

        float rollAll = Random.Range(0f, totalAll);
        float cumulativeAll = 0f;

        cumulativeAll += pMythic;
        if (rollAll < cumulativeAll)
        {
            Debug.Log($"<color=gold>★ MYTHIC card rolled! (Luck: {playerLuck})</color>");
            return CardRarity.Mythic;
        }

        cumulativeAll += pLegendary;
        if (rollAll < cumulativeAll)
        {
            Debug.Log($"<color=orange>★ LEGENDARY card rolled! (Luck: {playerLuck})</color>");
            return CardRarity.Legendary;
        }

        cumulativeAll += pEpic;
        if (rollAll < cumulativeAll)
        {
            Debug.Log($"<color=purple>★ EPIC card rolled! (Luck: {playerLuck})</color>");
            return CardRarity.Epic;
        }

        cumulativeAll += pRare;
        if (rollAll < cumulativeAll)
        {
            Debug.Log($"<color=blue>RARE card rolled (Luck: {playerLuck})</color>");
            return CardRarity.Rare;
        }

        cumulativeAll += pUncommon;
        if (rollAll < cumulativeAll)
        {
            Debug.Log($"<color=green>Uncommon card rolled (Luck: {playerLuck})</color>");
            return CardRarity.Uncommon;
        }

        Debug.Log($"Common card rolled (Luck: {playerLuck})");
        return CardRarity.Common;
    }

    /// <summary>
    /// Get random card of specific rarity
    /// </summary>
    private BaseCard GetRandomCardOfRarity(List<BaseCard> pool, CardRarity targetRarity, HashSet<BaseCard> usedCards)
    {
        List<BaseCard> matchingCards = pool.Where(c => c != null && c.rarity == targetRarity && !usedCards.Contains(c)).ToList();

        if (matchingCards.Count == 0)
        {
            if (targetRarity > CardRarity.Common)
            {
                return GetRandomCardOfRarity(pool, targetRarity - 1, usedCards);
            }
            else
            {
                List<BaseCard> unusedCards = pool.Where(c => !usedCards.Contains(c)).ToList();
                if (unusedCards.Count > 0)
                {
                    return unusedCards[Random.Range(0, unusedCards.Count)];
                }
                return null;
            }
        }

        return matchingCards[Random.Range(0, matchingCards.Count)];
    }

    private void DisplayCombinedCards(List<CombinedCard> cards)
    {
        if (cardContainer == null)
        {
            return;
        }

        foreach (Transform child in cardContainer)
        {
            if (child != null && child.gameObject.activeSelf)
            {
                child.gameObject.SetActive(false);
            }
            Destroy(child.gameObject);
        }

        foreach (CombinedCard card in cards)
        {
            GameObject prefabToUse = null;

            if (card.projectileCard != null && projectileCardButtonPrefab != null)
            {
                prefabToUse = projectileCardButtonPrefab;
            }
            else if (card.basicCard != null && coreCardButtonPrefab != null)
            {
                prefabToUse = coreCardButtonPrefab;
            }
            else if (projectileCardButtonPrefab != null)
            {
                prefabToUse = projectileCardButtonPrefab;
            }
            else
            {
                prefabToUse = coreCardButtonPrefab;
            }

            if (prefabToUse == null)
            {
                continue;
            }

            GameObject cardButton = Instantiate(prefabToUse, cardContainer);

            CardButton buttonScript = cardButton.GetComponent<CardButton>();
            if (buttonScript != null)
            {
                buttonScript.SetCombinedCard(card, this);
            }
        }
    }

    private void HandleCardTierIncreased(ProjectileCards card, int tier)
    {
        if (card == null) return;

        ProjectileVariantSet set = GetVariantSetForCard(card);
        if (set == null || set.variants == null || set.variants.Length == 0)
        {
            return;
        }

        int clampedTier = Mathf.Clamp(tier, 1, 3);
        EnqueueVariantSelection(card, set, clampedTier);
    }

    private void EnqueueVariantSelection(ProjectileCards card, ProjectileVariantSet set, int tier)
    {
        PendingVariantSelection pending;
        pending.card = card;
        pending.set = set;
        pending.tier = tier;

        pendingVariantSelections.Enqueue(pending);

        if (!processingVariantQueue && !deferVariantSelections)
        {
            StartCoroutine(ProcessVariantSelectionQueue());
        }
    }

    private IEnumerator ProcessVariantSelectionQueue()
    {
        processingVariantQueue = true;

        while (pendingVariantSelections.Count > 0)
        {
            var next = pendingVariantSelections.Dequeue();

            // Wait until no other card selection UI is currently open. Variant
            // selections have priority over normal level-up cards, so we do
            // NOT wait for the level-up queue itself to finish; instead, the
            // level-up queue yields while variants are pending.
            while (isSelectionActive)
            {
                yield return null;
            }

            yield return StartCoroutine(ShowVariantSelectionAfterDelay(next.card, next.set, next.tier));
        }

        processingVariantQueue = false;
    }

    private ProjectileVariantSet GetVariantSetForCard(ProjectileCards card)
    {
        if (card == null || projectileVariantSets == null) return null;

        foreach (var set in projectileVariantSets)
        {
            if (set == null) continue;
            if (set.projectileType == card.projectileType)
            {
                return set;
            }
        }

        return null;
    }

    private IEnumerator ShowVariantSelectionAfterDelay(ProjectileCards card, ProjectileVariantSet set, int tier)
    {
        if (GameStateManager.Instance != null && GameStateManager.Instance.PlayerIsDead)
        {
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < variantSelectionDelay)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (GameStateManager.Instance != null && GameStateManager.Instance.PlayerIsDead)
        {
            ForceCloseSelectionUI();
            yield break;
        }

        if (cardSelectionUI == null || cardContainer == null || variantSelectorCardButtonPrefab == null)
        {
            Debug.LogError("Variant selector UI references not set correctly.");
            yield break;
        }

        if (pauseGameOnSelection)
        {
            Time.timeScale = 0f;
        }

        foreach (Transform child in cardContainer)
        {
            if (child != null && child.gameObject.activeSelf)
            {
                child.gameObject.SetActive(false);
            }
            Destroy(child.gameObject);
        }

        cardSelectionUI.SetActive(true);
        isSelectionActive = true;
        isFirstStage = false;
        waitingForSecondStage = false;

        bool anyButtons = false;

        bool isMineProjectile = false;
        if (card != null && card.projectilePrefab != null)
        {
            if (card.projectilePrefab.GetComponent<FireMine>() != null ||
                card.projectilePrefab.GetComponent<FrostMine>() != null)
            {
                isMineProjectile = true;
            }
        }

        foreach (var info in set.variants)
        {
            if (info == null) continue;

            if (isMineProjectile)
            {
                if (tier == 1 && info.variantIndex != 1)
                {
                    continue;
                }
                if (tier == 2 && info.variantIndex != 2)
                {
                    continue;
                }
            }

            // Filter out variants that have ALREADY been chosen for this card on
            // previous enhancement tiers. This ensures, for example, that once
            // FireTalon has taken Variant 1, Variant 1 will not be offered again
            // on later enhancement selections for that same card.
            if (ProjectileCardLevelSystem.Instance != null && info.variantIndex > 0)
            {
                if (ProjectileCardLevelSystem.Instance.HasChosenVariant(card, info.variantIndex))
                {
                    continue;
                }
            }

            GameObject buttonObj = Instantiate(variantSelectorCardButtonPrefab, cardContainer);
            VariantSelectorButton selector = buttonObj.GetComponent<VariantSelectorButton>();
            if (selector != null)
            {
                selector.Initialize(this, card, info, tier);
            }

            anyButtons = true;
        }

        // If every variant was filtered out (all previously chosen), skip the
        // selection UI entirely to avoid a soft-lock with no buttons.
        if (!anyButtons)
        {
            if (cardSelectionUI != null)
            {
                cardSelectionUI.SetActive(false);
            }

            if (pauseGameOnSelection)
            {
                Time.timeScale = 1f;
            }

            isSelectionActive = false;
            yield break;
        }

        while (isSelectionActive)
        {
            yield return null;
        }
    }

    public void OnVariantSelected()
    {
        if (cardSelectionUI != null)
        {
            cardSelectionUI.SetActive(false);
        }

        if (pauseGameOnSelection)
        {
            Time.timeScale = 1f;
        }

        isSelectionActive = false;
    }
    
    /// <summary>
    /// Display projectile cards (alias for DisplayCombinedCards)
    /// </summary>
    private void DisplayProjectileCards(List<CombinedCard> cards)
    {
        DisplayCombinedCards(cards);
    }

    public void SelectCombinedCard(CombinedCard card)
    {
        if (player == null)
        {
            return;
        }

        // Special flow: If we are currently resolving the Mine element selection,
        // interpret this click as choosing Fire or Frost for the previously picked
        // Mine card, then apply that card's effect exactly once.
        if (enableMineElementSelection && pendingMineElementSelection && pendingMineCard != null && card != null && card.projectileCard != null)
        {
            ProjectileCards choiceCard = card.projectileCard;

            bool useAlternate = false;
            if (pendingMineCard.alternateProjectilePrefab != null && choiceCard.projectilePrefab == pendingMineCard.alternateProjectilePrefab)
            {
                useAlternate = true;
            }

            mineElementChoiceMade = true;
            mineUsesAlternateElement = useAlternate;

            if (mineUsesAlternateElement && pendingMineCard.alternateProjectilePrefab != null)
            {
                pendingMineCard.projectilePrefab = pendingMineCard.alternateProjectilePrefab;
            }

            pendingMineElementSelection = false;

            pendingMineCard.ApplyEffect(player);
            pendingMineCard = null;

            if (cardSelectionUI != null)
            {
                cardSelectionUI.SetActive(false);
            }

            if (pauseGameOnSelection)
            {
                Time.timeScale = 1f;
            }

            isSelectionActive = false;
            waitingForSecondStage = false;
            return;
        }

        if (card != null && card.projectileCard != null)
        {
            ProjectileCards projCard = card.projectileCard;
            if (projCard.projectileSystem == ProjectileCards.ProjectileSystemType.Active && string.IsNullOrEmpty(activeProjectileSystemCardName))
            {
                activeProjectileSystemCardName = projCard.cardName;
            }

            // FIRST-TIME MINE PICK: Defer applying the card until after the player
            // chooses Fire vs Frost in a dedicated follow-up selection.
            if (enableMineElementSelection &&
                projCard.projectileType == ProjectileCards.ProjectileType.FireMine &&
                projCard.alternateProjectilePrefab != null &&
                !mineElementChoiceMade &&
                !pendingMineElementSelection)
            {
                pendingMineElementSelection = true;
                pendingMineCard = projCard;

                if (cardSelectionUI != null)
                {
                    cardSelectionUI.SetActive(false);
                }

                isSelectionActive = false;
                waitingForSecondStage = false;

                StartCoroutine(ShowMineElementSelectionAfterDelay(projCard));
                return;
            }
        }

        card.ApplyAllEffects(player);
        
        // Note: Projectile level tracking is now handled in ProjectileCards.ApplyEffect()
        // Removed duplicate AddLevels() call that was causing double level upgrades

        // CRITICAL: Don't trigger second stage manually - the queue system handles it
        // Just close UI and mark selection as complete
        if (cardSelectionUI != null)
        {
            cardSelectionUI.SetActive(false);
        }

        // Only unpause if this is the last stage (projectile cards or two-stage disabled)
        if (!isFirstStage || !enableTwoStageSelection)
        {
            if (pauseGameOnSelection)
            {
                Time.timeScale = 1f;
            }
        }

        isSelectionActive = false;
        waitingForSecondStage = false;
    }

    /// <summary>
    /// Get the very first ACTIVE projectile system card name chosen at game start.
    /// Used so the initial free active projectile does not advance levels/enhancement.
    /// </summary>
    public string GetInitialActiveProjectileName()
    {
        return initialActiveProjectileSystemCardName;
    }

    private IEnumerator ShowProjectileCardsAfterDelay()
    {
        yield return new WaitForSecondsRealtime(delayBetweenStages);

        List<CombinedCard> projectileCards = GenerateRandomProjectileCards(cardsToShow);

        if (cardSelectionUI != null)
        {
            cardSelectionUI.SetActive(true);
            DisplayCombinedCards(projectileCards);
        }
    }

    private IEnumerator ShowMineElementSelectionAfterDelay(ProjectileCards baseMineCard)
    {
        if (baseMineCard == null || baseMineCard.alternateProjectilePrefab == null)
        {
            pendingMineElementSelection = false;
            pendingMineCard = null;
            yield break;
        }

        yield return new WaitForSecondsRealtime(cardDisplayDelay);

        if (GameStateManager.Instance != null && GameStateManager.Instance.PlayerIsDead)
        {
            pendingMineElementSelection = false;
            pendingMineCard = null;
            ForceCloseSelectionUI();
            yield break;
        }

        if (cardSelectionUI == null || cardContainer == null)
        {
            pendingMineElementSelection = false;
            pendingMineCard = null;
            yield break;
        }

        foreach (Transform child in cardContainer)
        {
            if (child != null && child.gameObject.activeSelf)
            {
                child.gameObject.SetActive(false);
            }
            Destroy(child.gameObject);
        }

        if (pauseGameOnSelection)
        {
            Time.timeScale = 0f;
        }

        cardSelectionUI.SetActive(true);
        isSelectionActive = true;

        // FIRE option (base prefab)
        ProjectileCards fireCopy = Instantiate(baseMineCard);
        fireCopy.rarity = baseMineCard.rarity;
        CombinedCard fireCombined = new CombinedCard(null, null, fireCopy.rarity) { projectileCard = fireCopy };
        GameObject fireButtonPrefab = fireMineCardButtonPrefab != null ? fireMineCardButtonPrefab : projectileCardButtonPrefab;
        GameObject fireButton = Instantiate(fireButtonPrefab, cardContainer);
        CardButton fireCardButton = fireButton.GetComponent<CardButton>();
        if (fireCardButton != null)
        {
            fireCardButton.SetCombinedCard(fireCombined, this);
        }

        // FROST option (alternate prefab)
        ProjectileCards frostCopy = Instantiate(baseMineCard);
        frostCopy.rarity = baseMineCard.rarity;
        frostCopy.projectilePrefab = baseMineCard.alternateProjectilePrefab;
        CombinedCard frostCombined = new CombinedCard(null, null, frostCopy.rarity) { projectileCard = frostCopy };
        GameObject frostButtonPrefab = frostMineCardButtonPrefab != null ? frostMineCardButtonPrefab : projectileCardButtonPrefab;
        GameObject frostButton = Instantiate(frostButtonPrefab, cardContainer);
        CardButton frostCardButton = frostButton.GetComponent<CardButton>();
        if (frostCardButton != null)
        {
            frostCardButton.SetCombinedCard(frostCombined, this);
        }

        while (isSelectionActive)
        {
            yield return null;
        }
    }

    /// <summary>
    /// Generate random PROJECTILE cards only.
    /// </summary>
    private List<CombinedCard> GenerateRandomProjectileCards(int count)
    {
        return GenerateRandomProjectileCards(count, false);
    }

    /// <summary>
    /// Generate random PROJECTILE cards only, with optional exclusion of Common rarity
    /// (used by NoCommonNext favour during specific level-up stages).
    /// </summary>
    private List<CombinedCard> GenerateRandomProjectileCards(int count, bool excludeCommon)
    {
        List<CombinedCard> cards = new List<CombinedCard>();
        HashSet<ProjectileCards> usedProjectileCards = new HashSet<ProjectileCards>();

        if (projectileCardPool.Count == 0)
        {
            return cards;
        }

        // Determine how many UNIQUE projectile cards are actually ELIGIBLE for this selection.
        // This must match the top-level availability rules used by GetRandomProjectileCard:
        //  - spawnChance > 0
        //  - respect the single ACTIVE projectile system rule
        List<ProjectileCards> eligibleCards = projectileCardPool
            .Where(c => c != null && c.spawnChance > 0f)
            .ToList();

        eligibleCards = eligibleCards.Where(IsProjectileCardAllowedByLevel).ToList();

        if (!string.IsNullOrEmpty(activeProjectileSystemCardName))
        {
            eligibleCards = eligibleCards.Where(c =>
                c.projectileSystem != ProjectileCards.ProjectileSystemType.Active ||
                c.cardName == activeProjectileSystemCardName).ToList();
        }

        if (eligibleCards.Count == 0)
        {
            return cards;
        }

        // Target count cannot exceed the number of distinct ELIGIBLE projectile cards
        int targetCount = Mathf.Min(count, eligibleCards.Count);
        int maxAttempts = 1000;
        int attempts = 0;

        while (cards.Count < targetCount && attempts < maxAttempts)
        {
            attempts++;

            CardRarity assignedRarity;
            ProjectileCards projCard = GetRandomProjectileCard(usedProjectileCards, out assignedRarity);
            if (projCard == null)
            {
                // No more unique projectile cards available for this selection via the main path
                break;
            }

            if (excludeCommon && assignedRarity == CardRarity.Common)
            {
                assignedRarity = CardRarity.Uncommon;
            }

            bool isMineCard = enableMineElementSelection && projCard.projectileType == ProjectileCards.ProjectileType.FireMine && projCard.alternateProjectilePrefab != null;

            ProjectileCards runtimeCopy = Instantiate(projCard);

            if (isMineCard && mineElementChoiceMade && projCard.alternateProjectilePrefab != null)
            {
                if (mineUsesAlternateElement)
                {
                    runtimeCopy.projectilePrefab = projCard.alternateProjectilePrefab;
                }
            }

            runtimeCopy.rarity = assignedRarity;
            runtimeCopy.OnRarityAssigned();

            usedProjectileCards.Add(projCard);
            CombinedCard combinedCard = new CombinedCard(null, null, assignedRarity) { projectileCard = runtimeCopy };
            cards.Add(combinedCard);

            Debug.Log($"<color=cyan>Selected {projCard.cardName} - Inspector Rarity: {projCard.rarity}, Spawned as: {assignedRarity}</color>");
        }

        // FINAL SAFETY: If we still have fewer cards than targetCount, but there are
        // eligible projectile cards left, fill the remaining slots directly from
        // the eligible pool. This guarantees we always show `count` cards whenever
        // at least `count` unique eligible cards exist.
        if (cards.Count < targetCount)
        {
            Debug.LogWarning($"[CardSelectionManager] Projectile selection under-filled by main path: target={targetCount}, got={cards.Count}. Using fallback to fill remaining slots.");

            List<ProjectileCards> remainingCandidates = eligibleCards
                .Where(c => c != null && c.spawnChance > 0f && !usedProjectileCards.Contains(c))
                .ToList();

            int safety = 0;
            while (cards.Count < targetCount && remainingCandidates.Count > 0 && safety < 100)
            {
                safety++;

                int index = Random.Range(0, remainingCandidates.Count);
                ProjectileCards projCard = remainingCandidates[index];
                remainingCandidates.RemoveAt(index);
                if (projCard == null)
                {
                    continue;
                }

                // Assign a safe rarity: never below the card's base rarity
                CardRarity fallbackRarity = GetRandomRarity();
                if (excludeCommon && fallbackRarity == CardRarity.Common)
                {
                    fallbackRarity = CardRarity.Uncommon;
                }
                if (fallbackRarity < projCard.rarity)
                {
                    fallbackRarity = projCard.rarity;
                }

                ProjectileCards runtimeCopy = Instantiate(projCard);
                runtimeCopy.runtimeBaseRarity = projCard.rarity;
                runtimeCopy.rarity = fallbackRarity;
                runtimeCopy.OnRarityAssigned();

                usedProjectileCards.Add(projCard);
                CombinedCard combinedCard = new CombinedCard(null, null, fallbackRarity) { projectileCard = runtimeCopy };
                cards.Add(combinedCard);

                Debug.Log($"<color=cyan>[Fallback] Selected {projCard.cardName} - Inspector Rarity: {projCard.rarity}, Spawned as: {fallbackRarity}</color>");
            }
        }

        return cards;
    }

    /// <summary>
    /// Show second stage (projectile cards) after delay
    /// </summary>
    private IEnumerator ShowSecondStageAfterDelay()
    {
        // Wait for delay (using unscaled time since game is paused)
        yield return new WaitForSecondsRealtime(delayBetweenStages);

        if (!waitingForSecondStage)
        {
            yield break; // Safety check
        }

        Debug.Log("<color=cyan>Stage 2: Showing Projectile Cards</color>");

        // Generate projectile modifier cards (currently returns an empty list but
        // kept for backwards compatibility).
        List<BaseCard> projectileCards = GenerateRandomProjectileModifierCards(cardsToShow);

        // Show UI with projectile cards
        if (cardSelectionUI != null)
        {
            cardSelectionUI.SetActive(true);
        }
        else
        {
            Debug.LogError("Card Selection UI not assigned!");

            // Fallback: just select first card and resume
            if (projectileCards.Count > 0 && player != null)
            {
                projectileCards[0].ApplyEffect(player);
            }

            if (pauseGameOnSelection)
            {
                Time.timeScale = 1f;
            }

            isSelectionActive = false;
            waitingForSecondStage = false;
        }
    }

    /// <summary>
    /// Show a list of BaseCard instances directly (used by EnemyCardSpawner and
    /// Favour card flow). Calls are queued so they do not conflict with level-up
    /// or variant selections and are processed sequentially.
    /// </summary>
    public void ShowCards(List<BaseCard> cards)
    {
        if (cards == null || cards.Count == 0)
        {
            return;
        }

        if (GameStateManager.Instance != null && GameStateManager.Instance.PlayerIsDead)
        {
            return;
        }

        // Enqueue a copy so callers can safely reuse/modify their lists.
        pendingExternalCardSelections.Enqueue(new List<BaseCard>(cards));

        if (!processingExternalCardQueue)
        {
            StartCoroutine(ProcessExternalCardSelectionQueue());
        }
    }

    private IEnumerator ProcessExternalCardSelectionQueue()
    {
        processingExternalCardQueue = true;

        bool showedAny = false;

        while (pendingExternalCardSelections.Count > 0)
        {
            // Wait for any level-up or variant selections, or any current card
            // selection, to finish before showing the next external batch.
            while (processingLevelUpQueue || pendingLevelUps.Count > 0 ||
                   processingVariantQueue || pendingVariantSelections.Count > 0 ||
                   processingExternalCombinedQueue || pendingExternalCombinedSelections.Count > 0 ||
                   isSelectionActive)
            {
                if (GameStateManager.Instance != null && GameStateManager.Instance.PlayerIsDead)
                {
                    pendingExternalCardSelections.Clear();
                    processingExternalCardQueue = false;
                    yield break;
                }

                yield return null;
            }

            List<BaseCard> cards = DequeueNextExternalSelectionByPriority();
            if (cards == null || cards.Count == 0)
            {
                continue;
            }

            if (GameStateManager.Instance != null && GameStateManager.Instance.PlayerIsDead)
            {
                continue;
            }

            if (showedAny && delayBetweenStages > 0f)
            {
                yield return new WaitForSecondsRealtime(delayBetweenStages);
            }

            ShowCardsImmediate(cards);
            showedAny = true;

            // Wait for the player to finish this selection.
            while (isSelectionActive)
            {
                if (GameStateManager.Instance != null && GameStateManager.Instance.PlayerIsDead)
                {
                    ForceCloseSelectionUI();
                    pendingExternalCardSelections.Clear();
                    processingExternalCardQueue = false;
                    yield break;
                }

                yield return null;
            }
        }

        processingExternalCardQueue = false;
    }

    private void ShowCardsImmediate(List<BaseCard> cards)
    {
        if (cards == null || cards.Count == 0)
        {
            return;
        }

        if (GameStateManager.Instance != null && GameStateManager.Instance.PlayerIsDead)
        {
            return;
        }

        if (cardSelectionUI == null || cardContainer == null)
        {
            Debug.LogError("<color=red>CardSelectionManager.ShowCards: UI references not assigned!</color>");
            return;
        }

        foreach (Transform child in cardContainer)
        {
            Destroy(child.gameObject);
        }

        if (pauseGameOnSelection)
        {
            Time.timeScale = 0f;
        }

        if (cardSelectionUI != null)
        {
            cardSelectionUI.SetActive(false);
        }
        isSelectionActive = true;
        isFirstStage = false;
        waitingForSecondStage = false;

        float perCardDelay = Mathf.Max(0f, cardDisplayDelay);
        for (int i = 0; i < cards.Count; i++)
        {
            BaseCard c = cards[i];
            if (c is EnemyCards)
            {
                perCardDelay = Mathf.Max(perCardDelay, EnemyCardDisplayDelay);
            }
            else if (c is FavourCards)
            {
                perCardDelay = Mathf.Max(perCardDelay, FavourCardDisplayDelay);
            }
        }

        StartCoroutine(DisplayExternalCardsWithOptionalDelay(cards, perCardDelay));

        Debug.Log($"<color=cyan>CardSelectionManager: Showing {cards.Count} cards</color>");
    }

    private IEnumerator DisplayExternalCardsWithOptionalDelay(List<BaseCard> cards, float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSecondsRealtime(delay);
        }

        if (!isSelectionActive)
        {
            yield break;
        }

        if (cardSelectionUI != null)
        {
            cardSelectionUI.SetActive(true);
        }

        for (int i = 0; i < cards.Count; i++)
        {
            BaseCard card = cards[i];
            if (card == null)
            {
                continue;
            }

            GameObject prefabToUse = null;

            if (card is EnemyCards && enemyCardButtonPrefab != null)
            {
                prefabToUse = enemyCardButtonPrefab;
            }
            else if (card is ProjectileCards && projectileCardButtonPrefab != null)
            {
                prefabToUse = projectileCardButtonPrefab;
            }
            else if (card is FavourCards && favourCardButtonPrefab != null)
            {
                prefabToUse = favourCardButtonPrefab;
            }
            else if (coreCardButtonPrefab != null)
            {
                prefabToUse = coreCardButtonPrefab;
            }
            else
            {
                prefabToUse = projectileCardButtonPrefab;
            }

            if (prefabToUse == null)
            {
                Debug.LogError("<color=red>Card button prefab not assigned!</color>");
                continue;
            }

            GameObject cardButton = Instantiate(prefabToUse, cardContainer);
            CardButton buttonScript = cardButton.GetComponent<CardButton>();
            if (buttonScript != null)
            {
                buttonScript.SetCard(card, this);
            }
        }
    }

    private void ShowCombinedCardsImmediate(List<CombinedCard> cards)
    {
        if (cards == null || cards.Count == 0)
        {
            return;
        }

        if (GameStateManager.Instance != null && GameStateManager.Instance.PlayerIsDead)
        {
            return;
        }

        if (cardSelectionUI == null || cardContainer == null)
        {
            return;
        }

        foreach (Transform child in cardContainer)
        {
            Destroy(child.gameObject);
        }

        if (pauseGameOnSelection)
        {
            Time.timeScale = 0f;
        }

        cardSelectionUI.SetActive(true);
        isSelectionActive = true;
        isFirstStage = false;
        waitingForSecondStage = false;

        DisplayCombinedCards(cards);
    }

    private IEnumerator ProcessExternalCombinedSelectionQueue()
    {
        processingExternalCombinedQueue = true;

        while (pendingExternalCombinedSelections.Count > 0)
        {
            while (processingLevelUpQueue || pendingLevelUps.Count > 0 ||
                   processingVariantQueue || pendingVariantSelections.Count > 0 ||
                   processingExternalCardQueue || pendingExternalCardSelections.Count > 0 ||
                   isSelectionActive)
            {
                if (GameStateManager.Instance != null && GameStateManager.Instance.PlayerIsDead)
                {
                    pendingExternalCombinedSelections.Clear();
                    processingExternalCombinedQueue = false;
                    yield break;
                }

                yield return null;
            }

            List<CombinedCard> cards = pendingExternalCombinedSelections.Dequeue();
            if (cards == null || cards.Count == 0)
            {
                continue;
            }

            if (GameStateManager.Instance != null && GameStateManager.Instance.PlayerIsDead)
            {
                continue;
            }

            ShowCombinedCardsImmediate(cards);

            while (isSelectionActive)
            {
                if (GameStateManager.Instance != null && GameStateManager.Instance.PlayerIsDead)
                {
                    ForceCloseSelectionUI();
                    pendingExternalCombinedSelections.Clear();
                    processingExternalCombinedQueue = false;
                    yield break;
                }

                yield return null;
            }
        }

        processingExternalCombinedQueue = false;
    }

    public void SelectExternalCard(BaseCard card)
    {
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player");
        }

        if (player == null)
        {
            return;
        }

        if (card != null)
        {
            card.ApplyEffect(player);
        }

        if (cardSelectionUI != null)
        {
            cardSelectionUI.SetActive(false);
        }

        if (pauseGameOnSelection)
        {
            Time.timeScale = 1f;
        }

        isSelectionActive = false;
        isFirstStage = false;
        waitingForSecondStage = false;
    }

    /// <summary>
    /// Show a Favour-card selection built from enemy rarity history. Intended to
    /// be called by EnemyCardSpawner before showing enemy cards or boss cards.
    /// </summary>
    public void ShowFavourCards(List<CardRarity> enemyRarityHistory, bool forceBossRarity, int count)
    {
        List<BaseCard> favourCards = GenerateFavourCards(count, enemyRarityHistory, forceBossRarity);
        if (favourCards == null || favourCards.Count == 0)
        {
            Debug.Log("<color=yellow>CardSelectionManager: No favour cards generated for this selection.</color>");
            return;
        }

        ShowCards(favourCards);
    }

    public void ShowPassiveProjectileChoiceWithMinRarity(int count, CardRarity minRarity)
    {
        if (GameStateManager.Instance != null && GameStateManager.Instance.PlayerIsDead)
        {
            return;
        }

        if (projectileCardPool == null || projectileCardPool.Count == 0)
        {
            return;
        }

        if (minRarity < CardRarity.Common)
        {
            minRarity = CardRarity.Common;
        }

        List<ProjectileCards> passiveCards = projectileCardPool
            .Where(c => c != null && c.spawnChance > 0f && c.projectileSystem == ProjectileCards.ProjectileSystemType.Passive)
            .ToList();
        if (passiveCards.Count == 0)
        {
            return;
        }

        var filteredByBaseRarity = passiveCards.Where(c => c.rarity >= minRarity).ToList();
        if (filteredByBaseRarity.Count > 0)
        {
            passiveCards = filteredByBaseRarity;
        }

        HashSet<CardRarity> allowedRarities = new HashSet<CardRarity>();
        for (int i = 0; i < passiveCards.Count; i++)
        {
            ProjectileCards card = passiveCards[i];
            for (CardRarity r = minRarity; r <= CardRarity.Mythic; r++)
            {
                if (card.rarity <= r)
                {
                    allowedRarities.Add(r);
                }
            }
        }

        if (allowedRarities.Count == 0)
        {
            allowedRarities.Add(minRarity);
        }

        int targetCount = Mathf.Max(1, count);
        List<CombinedCard> combinedCards = new List<CombinedCard>();
        HashSet<ProjectileCards> usedCards = new HashSet<ProjectileCards>();
        int attempts = 0;
        int maxAttempts = 1000;

        while (combinedCards.Count < targetCount && attempts < maxAttempts)
        {
            attempts++;
            CardRarity targetRarity = GetRandomRarity(allowedRarities);
            ProjectileCards baseCard = GetRandomPassiveProjectileCardOfRarity(targetRarity, usedCards);
            if (baseCard == null)
            {
                break;
            }

            usedCards.Add(baseCard);

            CardRarity assignedRarity = targetRarity;
            if (assignedRarity < minRarity)
            {
                assignedRarity = minRarity;
            }
            if (assignedRarity < baseCard.rarity)
            {
                assignedRarity = baseCard.rarity;
            }

            ProjectileCards runtimeCopy = Instantiate(baseCard);
            runtimeCopy.rarity = assignedRarity;
            runtimeCopy.OnRarityAssigned();

            CombinedCard combined = new CombinedCard(null, null, assignedRarity) { projectileCard = runtimeCopy };
            combinedCards.Add(combined);
        }

        if (combinedCards.Count == 0)
        {
            return;
        }

        pendingExternalCombinedSelections.Enqueue(combinedCards);
        if (!processingExternalCombinedQueue)
        {
            StartCoroutine(ProcessExternalCombinedSelectionQueue());
        }
    }

    // Replace your existing GetRarityForSoulLevel(int soulLevel) with this entire method.

    private static readonly int[][] SoulLevelFavourOdds = new int[][]
    {
    // SoulLevel 1..5
    new[] {100, 0, 0, 0, 0, 0},
    new[] {100, 0, 0, 0, 0, 0},
    new[] {100, 0, 0, 0, 0, 0},
    new[] {100, 0, 0, 0, 0, 0},
    new[] {100, 0, 0, 0, 0, 0},

    // 6..15
    new[] {95, 5, 0, 0, 0, 0},
    new[] {90, 10, 0, 0, 0, 0},
    new[] {85, 15, 0, 0, 0, 0},
    new[] {80, 20, 0, 0, 0, 0},
    new[] {75, 25, 0, 0, 0, 0},
    new[] {65, 35, 0, 0, 0, 0},
    new[] {55, 45, 0, 0, 0, 0},
    new[] {45, 55, 0, 0, 0, 0},
    new[] {35, 65, 0, 0, 0, 0},
    new[] {25, 75, 0, 0, 0, 0},

    // 16..23
    new[] {15, 80, 5, 0, 0, 0},
    new[] {5, 85, 10, 0, 0, 0},
    new[] {5, 80, 15, 0, 0, 0},
    new[] {5, 75, 20, 0, 0, 0},
    new[] {5, 70, 25, 0, 0, 0},
    new[] {5, 65, 30, 0, 0, 0},
    new[] {5, 60, 35, 0, 0, 0},
    new[] {5, 55, 40, 0, 0, 0},

    // 24..27
    new[] {0, 50, 50, 0, 0, 0},
    new[] {0, 40, 60, 0, 0, 0},
    new[] {0, 30, 70, 0, 0, 0},
    new[] {0, 20, 80, 0, 0, 0},

    // 28..35
    new[] {0, 10, 80, 5, 0, 0},
    new[] {0, 5, 80, 10, 0, 0},
    new[] {0, 5, 75, 15, 0, 0},
    new[] {0, 5, 70, 20, 0, 0},
    new[] {0, 5, 65, 25, 0, 0},
    new[] {0, 5, 60, 30, 0, 0},
    new[] {0, 5, 55, 35, 0, 0},
    new[] {0, 5, 50, 40, 0, 0},

    // 36..39
    new[] {0, 0, 45, 50, 0, 0},
    new[] {0, 0, 40, 60, 0, 0},
    new[] {0, 0, 30, 70, 0, 0},
    };

    private CardRarity GetRarityForSoulLevel(int soulLevel)
    {
        // SoulLevel is 1-based.
        if (soulLevel < 1) soulLevel = 1;

        int index = soulLevel - 1;

        // Clamp to last row for testing, as requested.
        if (index >= SoulLevelFavourOdds.Length)
        {
            index = SoulLevelFavourOdds.Length - 1;
        }

        int[] row = SoulLevelFavourOdds[index];

        int common = Mathf.Max(0, row.Length > 0 ? row[0] : 0);
        int uncommon = Mathf.Max(0, row.Length > 1 ? row[1] : 0);
        int rare = Mathf.Max(0, row.Length > 2 ? row[2] : 0);
        int epic = Mathf.Max(0, row.Length > 3 ? row[3] : 0);
        int legendary = Mathf.Max(0, row.Length > 4 ? row[4] : 0);
        int mythic = Mathf.Max(0, row.Length > 5 ? row[5] : 0);

        int total = common + uncommon + rare + epic + legendary + mythic;
        if (total <= 0)
        {
            return CardRarity.Common;
        }

        int roll = Random.Range(0, total);
        int cumulative = 0;

        cumulative += common;
        if (roll < cumulative) return CardRarity.Common;

        cumulative += uncommon;
        if (roll < cumulative) return CardRarity.Uncommon;

        cumulative += rare;
        if (roll < cumulative) return CardRarity.Rare;

        cumulative += epic;
        if (roll < cumulative) return CardRarity.Epic;

        cumulative += legendary;
        if (roll < cumulative) return CardRarity.Legendary;

        return CardRarity.Mythic;
    }

    public void ShowSoulFavourCardsForSoulLevel(int soulLevel, int count)
    {
        if (GameStateManager.Instance != null && GameStateManager.Instance.PlayerIsDead)
        {
            return;
        }

        if (soulLevel < 1)
        {
            return;
        }

        if (favourCardPool == null || favourCardPool.Count == 0)
        {
            return;
        }

        int targetCount = Mathf.Max(1, count);

        List<BaseCard> result = new List<BaseCard>();
        HashSet<FavourCards> usedCards = new HashSet<FavourCards>();

        for (int i = 0; i < targetCount; i++)
        {
            CardRarity targetRarity = GetRarityForSoulLevel(soulLevel);

            FavourCards baseCard = GetRandomFavourCardOfRarity(targetRarity, usedCards);
            if (baseCard == null)
            {
                break;
            }

            usedCards.Add(baseCard);

            FavourCards instance = Instantiate(baseCard);
            result.Add(instance);
        }

        if (result.Count == 0)
        {
            return;
        }

        ShowCards(result);
    }

    /// <summary>
    /// Handle selection of a single Favour card (called from CardButton).
    /// </summary>
    public void SelectFavourCard(BaseCard card)
    {
        if (player == null)
        {
            Debug.LogError("<color=red>Player not found!</color>");
            return;
        }

        if (card != null)
        {
            card.ApplyEffect(player);
        }

        if (cardSelectionUI != null)
        {
            cardSelectionUI.SetActive(false);
        }

        if (pauseGameOnSelection)
        {
            Time.timeScale = 1f;
        }

        isSelectionActive = false;
    }

    private void OnValidate()
    {
        float total = commonOdds + uncommonOdds + rareOdds + epicOdds + legendaryOdds + mythicOdds;
        if (Mathf.Abs(total - 100f) > 0.01f)
        {
            Debug.LogWarning($"Rarity odds don't total 100%! Current total: {total}%");
        }
    }
}