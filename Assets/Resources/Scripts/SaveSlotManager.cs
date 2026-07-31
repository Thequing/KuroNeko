using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

namespace VisualNovelEngine
{
    [Serializable]
    public class SaveSlotData
    {
        public int slotIndex;
        public bool isEmpty = true;
        public string saveTime;
        public string playTime;
        public string previewText;
        public string backgroundSprite;
        public string currentCharacter;
        public string currentScene;
        public EngineState gameState;
    }

    [Serializable]
    public class SaveSlotCollection
    {
        public List<SaveSlotData> slots = new List<SaveSlotData>();
        public const int MAX_SLOTS = 3;
    }

    public class SaveSlotManager : MonoBehaviour
    {
        private SaveSlotCollection saveSlots;
        private string saveFilePath => Path.Combine(Application.persistentDataPath, "save_slots.json");

        public static SaveSlotManager Instance { get; private set; }

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                LoadSaveSlots();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void LoadSaveSlots()
        {
            if (File.Exists(saveFilePath))
            {
                try
                {
                    string json = File.ReadAllText(saveFilePath);
                    saveSlots = JsonConvert.DeserializeObject<SaveSlotCollection>(json);

                    // Garantir que temos exatamente MAX_SLOTS slots
                    while (saveSlots.slots.Count < SaveSlotCollection.MAX_SLOTS)
                    {
                        saveSlots.slots.Add(new SaveSlotData
                        {
                            slotIndex = saveSlots.slots.Count,
                            isEmpty = true
                        });
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Erro ao carregar save slots: {ex.Message}");
                    CreateDefaultSlots();
                }
            }
            else
            {
                CreateDefaultSlots();
            }
        }

        private void CreateDefaultSlots()
        {
            saveSlots = new SaveSlotCollection();
            for (int i = 0; i < SaveSlotCollection.MAX_SLOTS; i++)
            {
                saveSlots.slots.Add(new SaveSlotData
                {
                    slotIndex = i,
                    isEmpty = true
                });
            }
            SaveSaveSlots();
        }

        public void SaveSaveSlots()
        {
            try
            {
                string json = JsonConvert.SerializeObject(saveSlots, Formatting.Indented);
                File.WriteAllText(saveFilePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Erro ao salvar save slots: {ex.Message}");
            }
        }

        public SaveSlotData GetSlotData(int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex < saveSlots.slots.Count)
                return saveSlots.slots[slotIndex];
            return null;
        }

        public void SaveToSlot(int slotIndex, EngineState gameState, string previewText, string backgroundSprite = "")
        {
            if (slotIndex < 0 || slotIndex >= saveSlots.slots.Count) return;

            var slot = saveSlots.slots[slotIndex];
            slot.isEmpty = false;
            slot.saveTime = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            slot.playTime = FormatPlayTime(gameState.totalPlayTime);
            slot.previewText = previewText.Length > 100 ? previewText.Substring(0, 100) + "..." : previewText;
            slot.backgroundSprite = backgroundSprite;
            slot.gameState = gameState;

            SaveSaveSlots();
        }

        public void DeleteSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= saveSlots.slots.Count) return;

            saveSlots.slots[slotIndex] = new SaveSlotData
            {
                slotIndex = slotIndex,
                isEmpty = true
            };

            SaveSaveSlots();
        }

        private string FormatPlayTime(float totalSeconds)
        {
            TimeSpan time = TimeSpan.FromSeconds(totalSeconds);
            return string.Format("{0:D2}:{1:D2}:{2:D2}",
                time.Hours, time.Minutes, time.Seconds);
        }

        public List<SaveSlotData> GetAllSlots()
        {
            return saveSlots.slots;
        }

        public string GetFormattedPlayTime(float totalSeconds)
        {
            return FormatPlayTime(totalSeconds);
        }
    }
}