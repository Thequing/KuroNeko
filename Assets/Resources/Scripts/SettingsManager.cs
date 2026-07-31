using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using System.IO;

[System.Serializable]
public class GameSettings
{
    [Range(0f, 1f)] public float Master = 1f;
    [Range(0f, 1f)] public float Music = 1f;
    [Range(0f, 1f)] public float Effects = 1f;

    public bool fullscreen = true;
    public int resolutionIndex = 0;
    public string language = "pt-BR";
}

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    [Header("Audio Mixer")]
    public AudioMixer audioMixer;

    [Header("Sliders de Áudio")]
    public Slider masterSlider;
    public Slider musicSlider;
    public Slider sfxSlider;

    [Header("Configurações atuais")]
    public GameSettings settings = new GameSettings();

    private string settingsFilePath;
    private Resolution[] availableResolutions;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            settingsFilePath = Path.Combine(Application.persistentDataPath, "settings.json");
            availableResolutions = Screen.resolutions;

            LoadSettings();

            SetupSliders();

            Debug.Log("Configurações carregadas e aplicadas do JSON com sucesso.");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        StartCoroutine(ApplyAudioNextFrame());
    }

    private System.Collections.IEnumerator ApplyAudioNextFrame()
    {
        yield return null; // espera 1 frame
        ApplySettings();
    }

    // ============================================================
    // ==== SALVAR E CARREGAR ====================================
    // ============================================================

    public void SaveSettings()
    {
        string json = JsonUtility.ToJson(settings, true);
        File.WriteAllText(settingsFilePath, json);
    }

    public void LoadSettings()
    {
        if (File.Exists(settingsFilePath))
        {
            string json = File.ReadAllText(settingsFilePath);
            settings = JsonUtility.FromJson<GameSettings>(json);
        }
        else
        {
            SaveSettings(); // cria arquivo inicial
        }

        ApplySettings();
    }

    // ============================================================
    // ==== APLICAR CONFIGURAÇÕES GERAIS ==========================
    // ============================================================

    public void ApplySettings()
    {
        // AUDIO
        SetMixerVolume("Master", settings.Master);
        SetMixerVolume("Music", settings.Music);
        SetMixerVolume("Effects", settings.Effects);

        // TELA
        Screen.fullScreen = settings.fullscreen;

        // RESOLUÇÃO
        if (availableResolutions.Length > 0 &&
            settings.resolutionIndex >= 0 &&
            settings.resolutionIndex < availableResolutions.Length)
        {
            Resolution res = availableResolutions[settings.resolutionIndex];
            Screen.SetResolution(res.width, res.height, settings.fullscreen);
        }

        // Atualiza sliders se existirem
        if (masterSlider) masterSlider.value = settings.Master;
        if (musicSlider) musicSlider.value = settings.Music;
        if (sfxSlider) sfxSlider.value = settings.Effects;
    }

    // ============================================================
    // ==== LIGAÇÃO COM SLIDERS ==================================
    // ============================================================

    private void SetupSliders()
    {
        if (masterSlider)
        {
            masterSlider.onValueChanged.AddListener(SetMasterVolume);
            masterSlider.value = settings.Master;
        }

        if (musicSlider)
        {
            musicSlider.onValueChanged.AddListener(SetMusicVolume);
            musicSlider.value = settings.Music;
        }

        if (sfxSlider)
        {
            sfxSlider.onValueChanged.AddListener(SetSFXVolume);
            sfxSlider.value = settings.Effects;
        }
    }

    // ============================================================
    // ==== AJUSTES DE ÁUDIO =====================================
    // ============================================================

    private void SetMixerVolume(string parameterName, float value)
    {
        float dB = Mathf.Log10(Mathf.Clamp(value, 0.0001f, 1f)) * 20f;
        audioMixer.SetFloat(parameterName, dB);
    }

    public void SetMasterVolume(float value)
    {
        settings.Master = value;
        SetMixerVolume("Master", value);
        SaveSettings();
    }

    public void SetMusicVolume(float value)
    {
        settings.Music = value;
        SetMixerVolume("Music", value);
        SaveSettings();
    }

    public void SetSFXVolume(float value)
    {
        settings.Effects = value;
        SetMixerVolume("Effects", value);
        SaveSettings();
    }

    // ============================================================
    // ==== VÍDEO E IDIOMA =======================================
    // ============================================================

    public void SetFullscreen(bool value)
    {
        settings.fullscreen = value;
        Screen.fullScreen = value;
        SaveSettings();
    }

    public void SetResolution(int index)
    {
        if (index < 0 || index >= availableResolutions.Length) return;
        settings.resolutionIndex = index;

        Resolution res = availableResolutions[index];
        Screen.SetResolution(res.width, res.height, settings.fullscreen);
        SaveSettings();
    }

    public void SetLanguage(string lang)
    {
        settings.language = lang;
        SaveSettings();
    }

    // ============================================================
    // ==== UTILIDADES PARA UI ===================================
    // ============================================================

    public Resolution[] GetAvailableResolutions() => availableResolutions;
    public int GetCurrentResolutionIndex() => settings.resolutionIndex;
}
