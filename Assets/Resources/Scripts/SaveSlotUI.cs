using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SaveSlotUI : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI slotNumberText;
    public TextMeshProUGUI previewText;
    public TextMeshProUGUI playTimeText;
    public TextMeshProUGUI saveDateText;
    public Image backgroundImage;
    public GameObject emptySlotIndicator;
    public GameObject occupiedSlotIndicator;

    public void Initialize(int slotIndex, SaveData saveData, bool isLoadMenu = false)
    {
        slotNumberText.text = $"Slot {slotIndex + 1}";

        if (saveData == null || saveData.isEmpty)
        {
            SetEmptySlot(slotIndex, "Slot Vazio");
        }
        else
        {
            SetOccupiedSlot(slotIndex, saveData);
        }

        // Configurar visibilidade dos indicadores
        if (emptySlotIndicator != null)
            emptySlotIndicator.SetActive(saveData == null || saveData.isEmpty);

        if (occupiedSlotIndicator != null)
            occupiedSlotIndicator.SetActive(saveData != null && !saveData.isEmpty);

        // Configurar interatividade do botão para menu de load
        Button button = GetComponent<Button>();
        if (isLoadMenu && button != null)
        {
            button.interactable = (saveData != null && !saveData.isEmpty);
        }
    }

    public void SetEmptySlot(int slotIndex, string message)
    {
        slotNumberText.text = $"Slot {slotIndex + 1}";
        previewText.text = message;
        playTimeText.text = "";
        saveDateText.text = "";

        if (backgroundImage != null)
            backgroundImage.color = Color.gray;
    }

    public void SetOccupiedSlot(int slotIndex, SaveData saveData)
    {
        slotNumberText.text = $"Slot {slotIndex + 1}";
        previewText.text = string.IsNullOrEmpty(saveData.previewText) ?
            "Sem preview" : TruncateText(saveData.previewText, 50);
        playTimeText.text = FormatPlayTime(saveData.playTime);
        saveDateText.text = saveData.saveDate;

        if (backgroundImage != null)
            backgroundImage.color = Color.white;
    }

    private string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;

        return text.Substring(0, maxLength) + "...";
    }

    private string FormatPlayTime(float totalSeconds)
    {
        System.TimeSpan time = System.TimeSpan.FromSeconds(totalSeconds);
        return string.Format("{0:D2}:{1:D2}:{2:D2}", time.Hours, time.Minutes, time.Seconds);
    }
}