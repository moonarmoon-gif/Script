using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-10000)]
public class StartingScreen : MonoBehaviour
{
    private const string PrefKeyAutoOpenLevelUpUI = "StartingScreen.AutoOpenLevelUpUI";
    [Header("UI")]
    public GameObject Root;
    public CanvasGroup RootCanvasGroup;

    [Header("Level Up UI (optional)")]
    public LevelUpUI LevelUpUI;

    [Header("Player Stats Preview UI (optional)")]
    public LevelUpPlayerStatsPreviewUI PlayerStatsPreviewUI;

    [Header("Background")]
    public Image BackgroundImage;
    public Sprite BackgroundSprite;
    public RawImage BackgroundRawImage;
    public Texture BackgroundTexture;
    public Color BackgroundColor = Color.black;

    [Header("Texts (optional)")]
    public TMP_Text NewGameText;
    public TMP_Text LoadText;
    public TMP_Text SettingsText;
    public TMP_Text ExitText;

    [Header("Buttons (optional)")]
    public Button NewGameButton;
    public Button LoadButton;
    public Button SettingsButton;
    public Button ExitButton;

    [Header("Gameplay Gating")]
    public bool ShowOnStart = true;
    public bool DisableGameplaySystemsOnStart = false;
    public List<GameObject> ObjectsToDisableUntilNewGame = new List<GameObject>();

    [Header("Game UI (optional)")]
    public GameObject GameUI;
    public bool DisableGameUIWhileInMenu = true;
    public string GameUIName = "GameUI";

    [Header("Scene Switching")]
    public bool LoadGameSceneOnNewGame = true;
    public string GameSceneName = "Game";
    public LoadSceneMode GameSceneLoadMode = LoadSceneMode.Single;

    [Header("Menu Camera (optional)")]
    public Camera MenuCamera;
    public bool DisableMenuCameraOnNewGame = true;

    private EnemySpawner enemySpawner;
    private EnemyCardSpawner enemyCardSpawner;

    private bool enemySpawnerComponentEnabled;
    private bool enemySpawnerEnableSpawning;

    private bool enemyCardSpawnerComponentEnabled;
    private bool enemyCardSpawnerEnableSpawning;

    private bool cachedStates;
    private bool newGameStarted;

    private void Awake()
    {
        if (Time.timeScale == 0f)
        {
            Time.timeScale = 1f;
        }

        if (MenuCamera == null)
        {
            MenuCamera = Camera.main;
        }

        ResolveGameUIReference();

        if (!ShowOnStart)
        {
            DisableMenuVisualsImmediately();
            SetGameUIActive(true);

            if (DisableMenuCameraOnNewGame && MenuCamera != null)
            {
                AudioListener listener = MenuCamera.GetComponent<AudioListener>();
                if (listener != null)
                {
                    listener.enabled = false;
                }
                MenuCamera.clearFlags = CameraClearFlags.SolidColor;
                MenuCamera.backgroundColor = Color.black;
            }

            if (!Application.CanStreamedLevelBeLoaded(GameSceneName))
            {
                Debug.LogError($"StartingScreen: Can't load scene '{GameSceneName}'. Add it to Build Settings (Scenes In Build) and ensure the name matches.");
                return;
            }

            SceneManager.LoadSceneAsync(GameSceneName, GameSceneLoadMode);
            return;
        }

        if (DisableGameUIWhileInMenu)
        {
            SetGameUIActive(false);
        }

        if (DisableGameplaySystemsOnStart)
        {
            CacheAndDisableGameplaySystems();
        }
        ApplyBackground();
        SetPlayerStatsPreviewUIEnabled(false);
        AutoWireTextsFromButtonsIfMissing();
        ResolveLevelUpUIReference();
        RegisterButtonHandlers();
        SetVisible(true);

        SetTruePlayerLevelUIVisible(false);
        RefillPlayerVitalsIfPresent();

        if (PlayerPrefs.GetInt(PrefKeyAutoOpenLevelUpUI, 0) == 1)
        {
            PlayerPrefs.SetInt(PrefKeyAutoOpenLevelUpUI, 0);
            PlayerPrefs.Save();
            HandleLoadClicked();
        }
    }

    private static void RefillPlayerVitalsIfPresent()
    {
        PlayerHealth playerHealth = null;
        PlayerMana playerMana = null;

        if (AdvancedPlayerController.Instance != null)
        {
            playerHealth = AdvancedPlayerController.Instance.GetComponent<PlayerHealth>();
            playerMana = AdvancedPlayerController.Instance.GetComponent<PlayerMana>();
        }

        if (playerHealth == null)
        {
            playerHealth = FindObjectOfType<PlayerHealth>(true);
        }

        if (playerMana == null)
        {
            playerMana = FindObjectOfType<PlayerMana>(true);
        }

        if (playerHealth != null)
        {
            playerHealth.SetMaxHealth(playerHealth.MaxHealth, fillToMax: true);
        }

        if (playerMana != null)
        {
            playerMana.SetMaxMana(playerMana.MaxMana, refill: true);
        }
    }

    public static void RequestAutoOpenLevelUpUIOnNextMenuLoad()
    {
        PlayerPrefs.SetInt(PrefKeyAutoOpenLevelUpUI, 1);
        PlayerPrefs.Save();
    }

    private void SetTruePlayerLevelUIVisible(bool visible)
    {
        TruePlayerLevelUI ui = FindObjectOfType<TruePlayerLevelUI>(true);
        if (ui != null)
        {
            ui.SetRootVisible(visible);
        }
    }

    private void OnEnable()
    {
        if (!ShowOnStart)
        {
            return;
        }

        RegisterButtonHandlers();
    }

    private void OnDisable()
    {
        UnregisterButtonHandlers();
    }

    private void OnDestroy()
    {
        UnregisterButtonHandlers();
    }

    private void CacheAndDisableGameplaySystems()
    {
        if (cachedStates)
        {
            return;
        }

        enemySpawner = FindObjectOfType<EnemySpawner>(true);
        if (enemySpawner != null)
        {
            enemySpawnerComponentEnabled = enemySpawner.enabled;
            enemySpawnerEnableSpawning = enemySpawner.enableSpawning;
            enemySpawner.enabled = false;
        }

        enemyCardSpawner = FindObjectOfType<EnemyCardSpawner>(true);
        if (enemyCardSpawner != null)
        {
            enemyCardSpawnerComponentEnabled = enemyCardSpawner.enabled;
            enemyCardSpawnerEnableSpawning = enemyCardSpawner.enableSpawning;
            enemyCardSpawner.enabled = false;
        }

        if (ObjectsToDisableUntilNewGame != null)
        {
            for (int i = 0; i < ObjectsToDisableUntilNewGame.Count; i++)
            {
                GameObject obj = ObjectsToDisableUntilNewGame[i];
                if (obj != null)
                {
                    obj.SetActive(false);
                }
            }
        }

        cachedStates = true;
    }

    private void ApplyBackground()
    {
        if (MenuCamera != null)
        {
            MenuCamera.clearFlags = CameraClearFlags.SolidColor;
            MenuCamera.backgroundColor = BackgroundColor;
        }

        if (BackgroundRawImage != null && BackgroundTexture != null)
        {
            BackgroundRawImage.texture = BackgroundTexture;
            BackgroundRawImage.color = BackgroundColor;
            BackgroundRawImage.enabled = true;
            return;
        }

        if (BackgroundImage == null || BackgroundSprite == null)
        {
            return;
        }

        BackgroundImage.sprite = BackgroundSprite;
        BackgroundImage.preserveAspect = true;
        BackgroundImage.color = BackgroundColor;
        BackgroundImage.enabled = true;
    }

    private void SetPlayerStatsPreviewUIEnabled(bool enabled)
    {
        if (PlayerStatsPreviewUI == null)
        {
            PlayerStatsPreviewUI = FindObjectOfType<LevelUpPlayerStatsPreviewUI>(true);
        }

        if (PlayerStatsPreviewUI != null)
        {
            PlayerStatsPreviewUI.enabled = enabled;
        }
    }

    private void AutoWireTextsFromButtonsIfMissing()
    {
        if (NewGameText == null && NewGameButton != null)
        {
            NewGameText = NewGameButton.GetComponentInChildren<TMP_Text>(true);
        }

        if (LoadText == null && LoadButton != null)
        {
            LoadText = LoadButton.GetComponentInChildren<TMP_Text>(true);
        }

        if (SettingsText == null && SettingsButton != null)
        {
            SettingsText = SettingsButton.GetComponentInChildren<TMP_Text>(true);
        }

        if (ExitText == null && ExitButton != null)
        {
            ExitText = ExitButton.GetComponentInChildren<TMP_Text>(true);
        }
    }

    private void RegisterButtonHandlers()
    {
        UnregisterButtonHandlers();

        if (NewGameButton != null)
        {
            NewGameButton.onClick.AddListener(HandleNewGameClicked);
        }

        if (LoadButton != null)
        {
            LoadButton.onClick.AddListener(HandleLoadClicked);
        }
    }

    private void UnregisterButtonHandlers()
    {
        if (NewGameButton != null)
        {
            NewGameButton.onClick.RemoveListener(HandleNewGameClicked);
        }

        if (LoadButton != null)
        {
            LoadButton.onClick.RemoveListener(HandleLoadClicked);
        }
    }

    public void StartNewGame()
    {
        HandleNewGameClicked();
    }

    private void HandleLoadClicked()
    {
        if (Time.timeScale == 0f)
        {
            Time.timeScale = 1f;
        }

        if (LevelUpUI == null)
        {
            ResolveLevelUpUIReference();
        }

        Canvas[] canvases = FindObjectsOfType<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas c = canvases[i];
            if (c != null && c.gameObject != null && c.gameObject.name == "LevelUpCanvas")
            {
                if (!c.gameObject.activeSelf)
                {
                    c.gameObject.SetActive(true);
                }
                break;
            }
        }

        SetTruePlayerLevelUIVisible(true);

        DisableMenuVisualsImmediately();
        SetGameUIActive(false);

        if (LevelUpUI != null)
        {
            LevelUpUI.Show(this);
        }
        else
        {
            Debug.LogError("StartingScreen: Load clicked, but LevelUpUI reference is missing.");
        }
    }

    private void ResolveLevelUpUIReference()
    {
        if (LevelUpUI != null)
        {
            return;
        }

        LevelUpUI = FindObjectOfType<LevelUpUI>(true);
    }

    private void HandleNewGameClicked()
    {
        if (newGameStarted)
        {
            return;
        }

        newGameStarted = true;

        if (Time.timeScale == 0f)
        {
            Time.timeScale = 1f;
        }

        DisableMenuVisualsImmediately();
        SetGameUIActive(true);

        if (DisableMenuCameraOnNewGame && MenuCamera != null)
        {
            AudioListener listener = MenuCamera.GetComponent<AudioListener>();
            if (listener != null)
            {
                listener.enabled = false;
            }
            MenuCamera.clearFlags = CameraClearFlags.SolidColor;
            MenuCamera.backgroundColor = Color.black;
        }

        if (LoadGameSceneOnNewGame)
        {
            SetTruePlayerLevelUIVisible(false);
            if (!Application.CanStreamedLevelBeLoaded(GameSceneName))
            {
                Debug.LogError($"StartingScreen: Can't load scene '{GameSceneName}'. Add it to Build Settings (Scenes In Build) and ensure the name matches.");
                return;
            }

            SceneManager.LoadSceneAsync(GameSceneName, GameSceneLoadMode);
            return;
        }

        SetVisible(false);
        RestoreGameplaySystems();
    }

    private void ResolveGameUIReference()
    {
        if (GameUI != null)
        {
            return;
        }

        if (string.IsNullOrEmpty(GameUIName))
        {
            return;
        }

        GameUI = GameObject.Find(GameUIName);
    }

    private void SetGameUIActive(bool active)
    {
        ResolveGameUIReference();

        if (GameUI == null)
        {
            return;
        }

        GameUI.SetActive(active);
    }

    private void DisableMenuVisualsImmediately()
    {
        bool levelUpInsideRoot = LevelUpUI != null && Root != null && LevelUpUI.transform.IsChildOf(Root.transform);

        if (BackgroundImage != null)
        {
            BackgroundImage.enabled = false;
        }

        if (BackgroundRawImage != null)
        {
            BackgroundRawImage.enabled = false;
        }

        if (!levelUpInsideRoot)
        {
            if (RootCanvasGroup != null)
            {
                RootCanvasGroup.alpha = 0f;
                RootCanvasGroup.interactable = false;
                RootCanvasGroup.blocksRaycasts = false;
            }
        }

        GameObject root = Root != null ? Root : gameObject;
        if (root != null)
        {
            Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] != null)
                {
                    if (LevelUpUI != null && graphics[i].transform.IsChildOf(LevelUpUI.transform))
                    {
                        continue;
                    }

                    graphics[i].enabled = false;
                }
            }

            Selectable[] selectables = root.GetComponentsInChildren<Selectable>(true);
            for (int i = 0; i < selectables.Length; i++)
            {
                if (selectables[i] != null)
                {
                    if (LevelUpUI != null && selectables[i].transform.IsChildOf(LevelUpUI.transform))
                    {
                        continue;
                    }

                    selectables[i].interactable = false;
                }
            }
        }

        if (Root != null && !levelUpInsideRoot)
        {
            Root.SetActive(false);
        }

        Canvas.ForceUpdateCanvases();
    }

    private void RestoreGameplaySystems()
    {
        if (ObjectsToDisableUntilNewGame != null)
        {
            for (int i = 0; i < ObjectsToDisableUntilNewGame.Count; i++)
            {
                GameObject obj = ObjectsToDisableUntilNewGame[i];
                if (obj != null)
                {
                    obj.SetActive(true);
                }
            }
        }

        if (enemySpawner != null)
        {
            enemySpawner.enableSpawning = enemySpawnerEnableSpawning;
            enemySpawner.enabled = enemySpawnerComponentEnabled;
        }

        if (enemyCardSpawner != null)
        {
            enemyCardSpawner.enableSpawning = enemyCardSpawnerEnableSpawning;
            enemyCardSpawner.enabled = enemyCardSpawnerComponentEnabled;
        }
    }

    private void SetVisible(bool visible)
    {
        GameObject root = Root != null ? Root : gameObject;

        if (RootCanvasGroup != null)
        {
            RootCanvasGroup.alpha = visible ? 1f : 0f;
            RootCanvasGroup.interactable = visible;
            RootCanvasGroup.blocksRaycasts = visible;
        }

        if (root != null)
        {
            root.SetActive(visible);
        }
    }
}
