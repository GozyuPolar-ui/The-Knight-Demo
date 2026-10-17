using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

// Singleton class that manages the data persistence
public class DataPersistenceManager : MonoBehaviour
{
    [Header("Debugging")]
    [SerializeField] private bool initializeDataIfNull = false;

    [SerializeField] private bool disableDataPersistence = false;


    [Header("File Storage Configuration")]
    [SerializeField] private string fileName;

    [SerializeField] private bool useEncryption;

    private GameData gameData;

    private List<IDataPersistance> dataPersistanceObjects;

    private FileDataHandler dataHandler;

    private string selectedProfileId = "";

    private const string MenuSceneName = "MainMenu";
    private string loadedSceneName = "";
    public static DataPersistenceManager instance { get; private set; }

    private void Awake()
    {
        if (instance != null)
        {
            Debug.Log("There is more than one DataPersistenceManager in the scene. Also destroying the newest one");
            Destroy(this.gameObject);
            return;
        }
        instance = this;

        DontDestroyOnLoad(this.gameObject);

        this.dataHandler = new FileDataHandler(Application.persistentDataPath, this.fileName, this.useEncryption);

        this.selectedProfileId = dataHandler.GetMostRecentProfile();
    }

    public void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    public void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    public void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        this.loadedSceneName = scene.name;
        this.dataPersistanceObjects = FindAllDataPersistanceObjects();
        LoadGame();
    }

    public void OnSceneUnloaded(Scene scene)
    {
        SaveGame();
    }

    private void Start()
    {
    }

    public void ChangeSelectedProfileId(string newProfileId)
    {
        if (newProfileId != this.selectedProfileId)
        {
            // do not carry the data of another profile over
            this.gameData = null;
        }

        this.selectedProfileId = newProfileId;
        LoadGame();
    }

    public void NewGame()
    {
        this.gameData = new GameData();
    }

    public void LoadGame()
    {
        if (disableDataPersistence)
        {
            return;
        }

        // Load the game data from the file using the data handler
        GameData loadedData = this.dataHandler.Load(selectedProfileId);

        // When there is nothing on disk keep the data that is already in memory. Otherwise the
        // inventory is lost between scenes whenever the game was not started through the menu
        // (no profile selected) or the file could not be written.
        if (loadedData != null)
        {
            this.gameData = loadedData;
        }

        // Starting directly from a gameplay scene: create fresh data so that saving works.
        bool inGameplayScene = !string.IsNullOrEmpty(this.loadedSceneName) && this.loadedSceneName != MenuSceneName;

        if(this.gameData == null && (this.initializeDataIfNull || inGameplayScene))
        {
            NewGame();
        }

        if (this.gameData == null)
        {
            Debug.Log("No data to load. Creating a new game");
            return;
        }

        // Loading data to all data persistance objects in the game
        foreach (IDataPersistance dataPersistanceObject in this.dataPersistanceObjects)
        {
            dataPersistanceObject.LoadData(this.gameData);
        }
    }

    public void SaveGame()
    {
        if (disableDataPersistence)
        {
            return;
        }   

        if (this.gameData == null)
        {
            return;
        }

        // Passing the data to other scripts to save their data
        foreach (IDataPersistance dataPersistanceObject in this.dataPersistanceObjects)
        {
            dataPersistanceObject.SaveData(ref this.gameData);
        }

        // adding a timestamp to the data
        this.gameData.lastUpdated = System.DateTime.Now.ToBinary();

        //Save the game data to the file using the data handler
        dataHandler.Save(this.gameData,selectedProfileId);
    }

    // Saves only the inventory slots. Used by the portals: the items are kept when changing scene
    // without carrying the player / camera position of this scene over to the next one.
    public void SaveInventory()
    {
        if (disableDataPersistence || this.dataPersistanceObjects == null)
        {
            return;
        }

        if (this.gameData == null)
        {
            NewGame();
        }

        foreach (IDataPersistance dataPersistanceObject in this.dataPersistanceObjects)
        {
            if (dataPersistanceObject is Slot slot && slot != null)
            {
                slot.SaveData(ref this.gameData);
            }
        }

        this.gameData.lastUpdated = System.DateTime.Now.ToBinary();
        dataHandler.Save(this.gameData, selectedProfileId);
    }

    private void OnApplicationQuit()
    {
        if (SceneManager.GetActiveScene().name == "MainLevel")
        {
            Debug.Log("SaveGame skipped: current scene is MainLevel.");
            return;
        }

        SaveGame();
    }

    private List<IDataPersistance> FindAllDataPersistanceObjects()
    {
        IEnumerable<IDataPersistance> dataPersistanceObjects = FindObjectsOfType<MonoBehaviour>().OfType<IDataPersistance>();

        return new List<IDataPersistance>(dataPersistanceObjects);
    }

    public bool HasGameData()
    {
        return this.gameData != null;
    }

    public Dictionary<string, GameData> GetAllProfiles()
    {
        return this.dataHandler.LoadAllProfiles();
    }
}
