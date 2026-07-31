using UnityEngine;
using VisualNovelEngine;

public class GameInitializer : MonoBehaviour
{
    [Header("UI References")]
    public GameObject saveLoadMenuPrefab;
    public GameObject saveSlotUIPrefab;

    void Start()
    {
        // Garantir que o SaveSlotManager está pronto
        if (SaveSlotManager.Instance == null)
        {
            GameObject saveManager = new GameObject("SaveSlotManager");
            saveManager.AddComponent<SaveSlotManager>();
        }

        // Configurar o VNEngine se existir
        /*
        VNEngine vnEngine = VNEngine.Instance;
        if (vnEngine != null)
        {
            // Configurar referências de UI se necessário
            if (vnEngine.saveLoadMenu == null && saveLoadMenuPrefab != null)
            {
                GameObject menu = Instantiate(saveLoadMenuPrefab);
                vnEngine.saveLoadMenu = menu;
                menu.SetActive(false);
            }

            vnEngine.StartPlayTimeTracking();
        }
        else
        {
            Debug.LogError("VNEngine não encontrado na cena!");
        }*/
    }
}