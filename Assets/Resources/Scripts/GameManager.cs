using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.IO;
using System.Collections.Generic;
using System.Collections;
using Newtonsoft.Json;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Configurações")]
    public string mainGameScene = "VisualNovelGame";
    public string menuScene = "MainMenu";

    [Header("Slots de Save")]
    public SaveData[] saveSlots = new SaveData[3];

    [Header("Slot Atual")]
    public int currentSlot = -1; // Slot atual em uso (-1 = nenhum)

    private string saveFilePath;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            saveFilePath = Path.Combine(Application.persistentDataPath, "savegame.json");
            InitializeSaveSystem();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    #region Save System
    private void InitializeSaveSystem()
    {
        if (File.Exists(saveFilePath))
        {
            try
            {
                string json = File.ReadAllText(saveFilePath);
                SaveData[] loadedSlots = JsonConvert.DeserializeObject<SaveData[]>(json);
                for (int i = 0; i < loadedSlots.Length && i < saveSlots.Length; i++)
                {
                    saveSlots[i] = loadedSlots[i];
                }
                Debug.Log("Dados de save carregados com sucesso");
            }
            catch (Exception e)
            {
                Debug.LogError($"Erro ao carregar save: {e.Message}");
                InitializeEmptySlots();
            }
        }
        else
        {
            InitializeEmptySlots();
        }
    }

    private void SaveAllSlots()
    {
        try
        {
            string json = JsonConvert.SerializeObject(saveSlots, Formatting.Indented);
            File.WriteAllText(saveFilePath, json);
            Debug.Log("Dados de save salvos com sucesso");
        }
        catch (Exception e)
        {
            Debug.LogError($"Erro ao salvar dados: {e.Message}");
        }
    }

    private void InitializeEmptySlots()
    {
        for (int i = 0; i < saveSlots.Length; i++)
        {
            if (saveSlots[i] == null)
            {
                saveSlots[i] = new SaveData { isEmpty = true };
            }
            else
            {
                saveSlots[i].isEmpty = true;
            }
        }
        SaveAllSlots();
    }
    #endregion

    #region Public API
    public void StartNewGame(int slotIndex = 0)
    {
        currentSlot = slotIndex;
        ClearTempState();
        SceneManager.LoadScene(mainGameScene);
        SceneManager.sceneLoaded += OnNewGameSceneLoaded;
    }

    public void OverwriteSlot(int slotIndex)
    {
        SaveToSlot(slotIndex);
    }

    public void SaveToSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= saveSlots.Length) return;

        var vnEngine = FindFirstObjectByType<VisualNovelEngine.VisualNovelEngine>();
        if (vnEngine == null)
        {
            Debug.LogError("VNEngine não encontrado para salvar");
            return;
        }

        saveSlots[slotIndex] = CreateSaveDataFromVNEngine(vnEngine);
        SaveAllSlots();
        Debug.Log($"Jogo salvo no slot {slotIndex + 1}");
    }

    public void LoadFromSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= saveSlots.Length || saveSlots[slotIndex].isEmpty)
        {
            Debug.LogWarning($"Slot {slotIndex} está vazio ou inválido");
            return;
        }

        currentSlot = slotIndex;
        string targetScene = string.IsNullOrEmpty(saveSlots[slotIndex].sceneName) ?
            mainGameScene : saveSlots[slotIndex].sceneName;

        SceneManager.LoadScene(targetScene);
        SceneManager.sceneLoaded += (scene, mode) => OnLoadGameSceneLoaded(scene, slotIndex);
    }

    public SaveData GetSlotData(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < saveSlots.Length)
        {
            if (saveSlots[slotIndex] == null)
            {
                saveSlots[slotIndex] = new SaveData { isEmpty = true };
            }
            return saveSlots[slotIndex];
        }
        return new SaveData { isEmpty = true };
    }

    public void ReturnToMainMenu()
    {
        currentSlot = -1;
        SceneManager.LoadScene(menuScene);
    }

    public void DeleteSaveSlot(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < saveSlots.Length)
        {
            saveSlots[slotIndex] = new SaveData { isEmpty = true };
            SaveAllSlots();
            Debug.Log($"Slot {slotIndex + 1} deletado");
        }
    }

    // Novo método: Obter slot atual
    public int GetCurrentSlot()
    {
        return currentSlot;
    }

    // Novo método: Verificar se há slot atual válido
    public bool HasCurrentSlot()
    {
        return currentSlot >= 0 && currentSlot < saveSlots.Length;
    }
    #endregion

    #region Private Methods
    private SaveData CreateSaveDataFromVNEngine(VisualNovelEngine.VisualNovelEngine vnEngine)
    {
        var stateField = typeof(VisualNovelEngine.VisualNovelEngine).GetField("state",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (stateField == null)
        {
            Debug.LogError("Campo 'state' não encontrado no VNEngine");
            return CreateEmptySaveData();
        }

        var state = stateField.GetValue(vnEngine) as VisualNovelEngine.EngineState;

        if (state == null)
        {
            Debug.LogError("Estado do VNEngine é null");
            return CreateEmptySaveData();
        }

        return new SaveData
        {
            isEmpty = false,
            previewText = GetPreviewText(state),
            backgroundSprite = GetCurrentBackground(vnEngine),
            playTime = state.totalPlayTime,
            saveDate = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
            sceneName = SceneManager.GetActiveScene().name,
            currentNodeIndex = state.currentNodeIndex,
            variablesJson = JsonConvert.SerializeObject(state.variables),
            flagsJson = JsonConvert.SerializeObject(state.flags),
            historyJson = JsonConvert.SerializeObject(state.history)
        };
    }

    private SaveData CreateEmptySaveData()
    {
        return new SaveData
        {
            isEmpty = false,
            previewText = "Novo Jogo",
            backgroundSprite = "",
            playTime = 0f,
            saveDate = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
            sceneName = mainGameScene,
            currentNodeIndex = 0,
            variablesJson = "{}",
            flagsJson = "[]",
            historyJson = "[]",
        };
    }

    private string GetPreviewText(VisualNovelEngine.EngineState state)
    {
        return state.history.Count > 0 ?
            state.history[state.history.Count - 1] : "Novo Jogo";
    }

    private string GetCurrentBackground(VisualNovelEngine.VisualNovelEngine vnEngine)
    {
        if (vnEngine.backgroundImage != null && vnEngine.backgroundImage.sprite != null)
        {
            return vnEngine.backgroundImage.sprite.name;
        }
        return "";
    }

    private void OnNewGameSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == mainGameScene)
        {
            SceneManager.sceneLoaded -= OnNewGameSceneLoaded;

            var vnEngine = FindFirstObjectByType<VisualNovelEngine.VisualNovelEngine>();
            if (vnEngine != null)
            {
                vnEngine.SetCurrentSlot(currentSlot);
                vnEngine.LoadScriptFromFile(vnEngine.defaultScriptPath);
                vnEngine.StartPlayTimeTracking();
            }
        }
    }

    private void OnLoadGameSceneLoaded(Scene scene, int slotIndex)
    {
        if (scene.name == mainGameScene)
        {
            var vnEngine = FindFirstObjectByType<VisualNovelEngine.VisualNovelEngine>();
            if (vnEngine != null)
            {
                vnEngine.SetCurrentSlot(currentSlot);
                vnEngine.LoadFromSaveData(saveSlots[slotIndex]);
                vnEngine.StartPlayTimeTracking();
            }
        }
    }

    private void ClearTempState()
    {
        // Limpa qualquer estado temporário se necessário
    }
    #endregion
}