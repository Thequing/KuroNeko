using UnityEngine;
using VisualNovelEngine;

public class MainMenu : MonoBehaviour
{
    [Header("References")]
    public string gameSceneName = "GameScene";

    public void OnNewGameClicked()
    {
        // Carrega a cena do jogo
        UnityEngine.SceneManagement.SceneManager.LoadScene(gameSceneName);
    }

    public void OnLoadGameClicked()
    {
        // Mostra o menu de load diretamente pelo VNEngine
        /*if (VisualNovelEngine.Instance != null)
        {
            VisualNovelEngine.Instance.ShowLoadMenu();
        }
        else
        {
            Debug.LogError("VNEngine.Instance não encontrada!");
        }*/
    }

    // Este método deve ser chamado DENTRO da cena do jogo
    public void OnSaveGameClicked()
    {
        /*if (VisualNovelEngine.Instance != null)
        {
            VisualNovelEngine.Instance.ShowSaveMenu();
        }
        else
        {
            Debug.LogError("VNEngine.Instance não encontrada!");
        }
        */
    }

    public void OnQuitClicked()
    {
        Application.Quit();
    }
}