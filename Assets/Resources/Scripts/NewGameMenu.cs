using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NewGameMenu : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject slotButtonPrefab;
    public Transform slotsContainer;
    public Button backButton;

    private void Start()
    {
        RefreshSlots();
        backButton.onClick.AddListener(() => gameObject.SetActive(false));
    }

    public void RefreshSlots()
    {
        // Limpa slots existentes
        foreach (Transform child in slotsContainer)
        {
            Destroy(child.gameObject);
        }

        // Cria botões para cada slot
        for (int i = 0; i < 3; i++)
        {
            GameObject slotGO = Instantiate(slotButtonPrefab, slotsContainer);
            SaveSlotUI slotUI = slotGO.GetComponent<SaveSlotUI>();
            SaveData slotData = GameManager.Instance.GetSlotData(i);

            if (slotData.isEmpty)
            {
                slotUI.SetEmptySlot(i, "Slot Vazio");
            }
            else
            {
                slotUI.SetOccupiedSlot(i, slotData);
            }

            // Configura o botão para sobrescrever
            Button slotButton = slotGO.GetComponent<Button>();
            int slotIndex = i;
            slotButton.onClick.AddListener(() => OnSlotSelected(slotIndex));
        }
    }

    private void OnSlotSelected(int slotIndex)
    {
        // CORREÇÃO: Usando OverwriteSlot que agora existe
        GameManager.Instance.OverwriteSlot(slotIndex);
        GameManager.Instance.StartNewGame();
    }
}