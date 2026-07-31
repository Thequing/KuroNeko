using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VisualNovelEngine;

public class LogEntryUI : MonoBehaviour
{
    public TextMeshProUGUI speakerText;
    public TextMeshProUGUI dialogueText;

    public void Initialize(LogEntry entry)
    {
        if (speakerText != null)
        {
            speakerText.text = string.IsNullOrEmpty(entry.speaker) ? "Narrador" : entry.speaker;
        }
        else
        {
            Debug.LogError("SpeakerText não atribuído no LogEntryUI prefab!");
        }

        if (dialogueText != null)
        {
            dialogueText.text = entry.text;
        }
        else
        {
            Debug.LogError("DialogueText não atribuído no LogEntryUI prefab!");
        }
    }
}