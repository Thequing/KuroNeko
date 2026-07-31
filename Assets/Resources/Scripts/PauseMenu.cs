using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace VisualNovelEngine
{
    public class PauseMenu : MonoBehaviour
    {
        [Header("Pause Menu UI")]
        public GameObject pauseMenuPanel;
        public Button resumeButton;
        public Button saveButton;
        public Button exitButton;
        public TextMeshProUGUI saveStatusText;

        [Header("Settings")]
        public KeyCode pauseKey = KeyCode.Escape;
        public bool pauseWhenMenuOpen = true;

        private VisualNovelEngine vnEngine;
        private bool isPaused = false;

        void Start()
        {
            vnEngine = FindFirstObjectByType<VisualNovelEngine>();
            if (vnEngine == null)
            {
                Debug.LogError("VisualNovelEngine não encontrado na cena!");
                return;
            }

            if (resumeButton != null)
                resumeButton.onClick.AddListener(ResumeGame);

            if (saveButton != null)
                saveButton.onClick.AddListener(QuickSave);

            if (exitButton != null)
                exitButton.onClick.AddListener(ExitToMainMenu);

            if (pauseMenuPanel != null)
                pauseMenuPanel.SetActive(false);

            if (saveStatusText != null)
                saveStatusText.gameObject.SetActive(false);
        }

        void Update()
        {
            if (Input.GetKeyDown(pauseKey) && !IsOtherMenuOpen())
            {
                if (isPaused)
                {
                    ResumeGame();
                }
                else
                {
                    PauseGame();
                }
            }
        }

        public void PauseGame()
        {
            if (isPaused || vnEngine == null) return;

            isPaused = true;

            if (pauseWhenMenuOpen)
            {
                Time.timeScale = 0f;
            }

            if (pauseMenuPanel != null)
            {
                pauseMenuPanel.SetActive(true);
            }

            vnEngine.SetPaused(true);

            Debug.Log("Jogo pausado");
        }

        public void ResumeGame()
        {
            if (!isPaused || vnEngine == null) return;

            isPaused = false;

            if (pauseWhenMenuOpen)
            {
                Time.timeScale = 1f;
            }

            if (pauseMenuPanel != null)
            {
                pauseMenuPanel.SetActive(false);
            }

            vnEngine.SetPaused(false);

            Debug.Log("Jogo despausado");
        }

        private bool IsOtherMenuOpen()
        {
            if (vnEngine == null) return false;

            var saveLoadMenuField = vnEngine.GetType().GetField("saveLoadMenu", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (saveLoadMenuField != null)
            {
                GameObject saveLoadMenuObj = (GameObject)saveLoadMenuField.GetValue(vnEngine);
                if (saveLoadMenuObj != null && saveLoadMenuObj.activeInHierarchy)
                    return true;
            }

            var logPanelField = vnEngine.GetType().GetField("logPanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (logPanelField != null)
            {
                GameObject logPanelObj = (GameObject)logPanelField.GetValue(vnEngine);
                if (logPanelObj != null && logPanelObj.activeInHierarchy)
                    return true;
            }

            return false;
        }

        #region Button Handlers
        private void QuickSave()
        {
            if (vnEngine == null) return;

            // Salva diretamente no slot atual do VisualNovelEngine
            vnEngine.SaveToCurrentSlot();

            // Mostrar feedback visual
            ShowSaveStatus("Jogo salvo com sucesso!");

            Debug.Log("QuickSave realizado no slot atual");
        }

        private void ShowSaveStatus(string message)
        {
            if (saveStatusText != null)
            {
                saveStatusText.text = message;
                saveStatusText.gameObject.SetActive(true);
                StartCoroutine(HideSaveStatusAfterDelay(2f));
            }
        }

        private IEnumerator HideSaveStatusAfterDelay(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (saveStatusText != null)
                saveStatusText.gameObject.SetActive(false);
        }

        private void ExitToMainMenu()
        {
            Time.timeScale = 1f;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.ReturnToMainMenu();
            }
            else
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }
        #endregion

        #region Public Methods
        public bool IsGamePaused()
        {
            return isPaused;
        }

        public void TogglePause()
        {
            if (isPaused)
                ResumeGame();
            else
                PauseGame();
        }
        #endregion

        void OnDestroy()
        {
            if (isPaused)
            {
                Time.timeScale = 1f;
                if (vnEngine != null)
                    vnEngine.SetPaused(false);
            }
        }
    }
}