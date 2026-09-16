using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VisualNovelEngine
{
    #region Log System Classes
    [System.Serializable]
    public class LogEntry
    {
        public string speaker;
        public string text;
        public string timestamp;
        public string backgroundSprite; // Para contexto visual
        public List<string> activeCharacters; // Personagens ativos no momento

        public LogEntry(string speaker, string text, string background = "")
        {
            this.speaker = speaker;
            this.text = text;
            this.backgroundSprite = background;
            this.activeCharacters = new List<string>();
        }
    }

    [System.Serializable]
    public class LogHistory
    {
        public List<LogEntry> entries = new List<LogEntry>();
        public int maxEntries = 500; // Limite para evitar memory leaks

        public void AddEntry(LogEntry entry)
        {
            entries.Add(entry);

            // Manter apenas os últimos maxEntries
            if (entries.Count > maxEntries)
            {
                entries.RemoveAt(0);
            }
        }

        public void Clear()
        {
            entries.Clear();
        }

        public List<LogEntry> GetEntriesBySpeaker(string speaker)
        {
            return entries.Where(e => e.speaker == speaker).ToList();
        }

        public List<LogEntry> SearchEntries(string keyword)
        {
            return entries.Where(e =>
                e.text.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                e.speaker.Contains(keyword, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }
    }
    #endregion

    #region Engine and UI
    public class EngineState
    {
        public int currentNodeIndex = 0;
        public Dictionary<string, string> variables = new Dictionary<string, string>();
        public HashSet<string> flags = new HashSet<string>();
        public List<string> history = new List<string>();
        public float totalPlayTime = 0f;
        public LogHistory logHistory = new LogHistory();
    }

    public class VisualNovelEngine : MonoBehaviour
    {
        [Header("UI")]
        public TextMeshProUGUI textBox; // caixa de texto (se não usar TMP, troque pelo tipo UnityEngine.UI.Text)
        public TextMeshProUGUI nameBox;
        public GameObject textBoxRoot; // para customização e animações
        public float defaultTextSpeed = 40f; // caracteres por segundo

        [Header("UI Toggle")]
        public bool isUIVisible = true;
        public KeyCode toggleUIKey = KeyCode.H; // Tecla para alternar UI
        public float uiFadeDuration = 0.3f;

        [Header("Menu UI")]
        public GameObject menuPanel; // Painel que contém os botões de escolha
        public GameObject choiceButtonPrefab; // Prefab do botão com TextMeshProUGUI
        public Transform[] choicePositions = new Transform[3];

        [Header("Sprites and backgrounds")]
        public Transform spriteContainer; // onde instanciar sprites (parece melhor com Image components)
        public SpriteRenderer backgroundImage;

        [Header("Audio")]
        public AudioSource musicSource;
        public AudioSource effectsSource;

        [Header("Vowel Sounds")]
        public AudioClip vowelA;
        public AudioClip vowelE;
        public AudioClip vowelI;
        public AudioClip vowelO;
        public AudioClip vowelU;
        [Range(0f, 1f)]
        public float vowelVolume = 0.7f;

        [Header("Save System")]
        public GameObject saveSlotPrefab;
        public Transform saveSlotsContainer;
        public GameObject saveLoadMenu;
        private bool isTrackingPlayTime = false;

        [Header("Other")]
        public bool autoStart = true;
        public string defaultScriptPath = "Resources/ScenesTXT/Teste";

        [Header("Auto Text Settings")]
        public bool AutoText = false; // Nova opção para texto automático
        public float autoTextDelay = 2f;

        [Header("Log System")]
        public GameObject logPanel;
        public Transform logContentParent;
        public GameObject logEntryPrefab;
        public TMP_InputField logSearchField;
        public Button logClearSearchButton;
        public ScrollRect logScrollRect;
        public KeyCode logToggleKey = KeyCode.L;
        public bool isLogOpen = false;

        [Header("Log Settings")]
        public int maxLogEntries = 500;
        public bool autoScrollToBottom = true;

        [Header("Screen Shake Settings")]
        public bool enableScreenShake = true;
        public float shakeDefaultDuration = 0.5f;
        public float shakeDefaultIntensity = 0.1f;

        [Header("Save Management")]
        public int currentSaveSlot = -1;

        // Internal shake variables
        private Coroutine shakeCoroutine;
        private Vector3 originalCameraPosition;
        private Transform mainCameraTransform;

        // internal
        private List<ScriptNode> nodes;
        private EngineState state = new EngineState();
        private ResourceManager resources = new ResourceManager();

        [Header("Pause System")]
        public bool IsPaused { get; private set; } = false;

        private bool isTyping = false;
        private bool isWaitingForInput = false;
        private bool isInMenu = false;
        private int pendingChoiceIndex = -1; // escolha clicada, consumida por HandleMenuNode
        private Coroutine typingCoroutine;
        private Coroutine autoAdvanceCoroutine;
        private Coroutine backgroundFadeCoroutine;
        private float textSpeed; // current speed
        private bool fastForward = false;
        private bool skipUnread = false; // pular texto não lido

        private Dictionary<string, int> labelIndices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, Image> activeCharacterSprites = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<char, AudioClip> vowelSounds = new Dictionary<char, AudioClip>();

        private string saveFilePath => Path.Combine(Application.persistentDataPath, "vn_save.json");

        private List<GameObject> currentChoiceButtons = new List<GameObject>();

        public static VisualNovelEngine Instance { get; private set; }

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
            textSpeed = defaultTextSpeed;
            if (textBox == null) Debug.LogWarning("textBox não atribuído no inspector.");
            if (nameBox == null) Debug.LogWarning("nameBox não atribuído no inspector.");
            if (menuPanel != null)
                menuPanel.SetActive(false);
            InitializeVowelSounds();
            InitializeScreenShake();
        }

        void Start()
        {
            if (autoStart)
            {
                LoadScriptFromFile(defaultScriptPath);
                StartPlayTimeTracking();
            }
        }

        private void InitializeScreenShake()
        {
            // Encontrar a câmera principal
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                mainCameraTransform = mainCamera.transform;
                originalCameraPosition = mainCameraTransform.localPosition;
            }
            else
            {
                Debug.LogWarning("Screen Shake: Câmera principal não encontrada!");
            }
        }

        void InitializeVowelSounds()
        {
            vowelSounds.Clear();

            // Mapear cada vogal ao seu AudioClip correspondente
            if (vowelA != null) vowelSounds['a'] = vowelA;
            if (vowelE != null) vowelSounds['e'] = vowelE;
            if (vowelI != null) vowelSounds['i'] = vowelI;
            if (vowelO != null) vowelSounds['o'] = vowelO;
            if (vowelU != null) vowelSounds['u'] = vowelU;

            // Também incluir vogais maiúsculas
            if (vowelA != null) vowelSounds['A'] = vowelA;
            if (vowelE != null) vowelSounds['E'] = vowelE;
            if (vowelI != null) vowelSounds['I'] = vowelI;
            if (vowelO != null) vowelSounds['O'] = vowelO;
            if (vowelU != null) vowelSounds['U'] = vowelU;

            Debug.Log($"Sons de vogais inicializados: {vowelSounds.Count} vogais configuradas");
        }

        public void SetPaused(bool paused)
        {
            IsPaused = paused;

            if (paused)
            {
                if (musicSource != null && musicSource.isPlaying)
                    musicSource.Pause();
            }
            else
            {
                if (musicSource != null && !musicSource.isPlaying)
                    musicSource.UnPause();
            }

            Debug.Log($"VisualNovelEngine {(paused ? "pausado" : "despausado")}");
        }

        void Update()
        {
            if (IsPaused || isLogOpen) return;
            if (isInMenu) return;

            HandleUIToggleInput();
            HandleLogInput();

            if (isTrackingPlayTime && !isLogOpen)
            {
                state.totalPlayTime += Time.deltaTime;
            }
            // Controls modificados:
            // Space / click -> advance apenas quando esperando por input
            if ((Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space)) && isWaitingForInput)
            {
                ContinueFromWait();
            }

            // Se estiver digitando, ainda permite pular a digitação
            if ((Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space)) && isTyping)
            {
                FinishTypingInstant();
            }

            // Fast-forward (Toggle) - agora só funciona durante digitação
            if (Input.GetKeyDown(KeyCode.F))
            {
                ToggleFastForward();
            }

            if (Input.GetKeyUp(KeyCode.LeftShift) || Input.GetKeyUp(KeyCode.RightShift))
            {
                fastForward = false;
                textSpeed = defaultTextSpeed;
            }

            // Skip unread toggle (press S)
            if (Input.GetKeyDown(KeyCode.S))
            {
                skipUnread = !skipUnread;
                Debug.Log("Skip unread: " + skipUnread);
            }

            // Jump to next choice with J
            if (Input.GetKeyDown(KeyCode.J))
            {
                JumpToNextChoice();
            }
        }

        public void ToggleFastForward()
        {
            fastForward = !fastForward;
            textSpeed = fastForward ? defaultTextSpeed * 3f : defaultTextSpeed;
            Debug.Log($"FastForward: {fastForward}");
        }

        public void StartPlayTimeTracking()
        {
            isTrackingPlayTime = true;
        }

        public void StopPlayTimeTracking()
        {
            isTrackingPlayTime = false;
        }

        void OnDestroy()
        {
            // Parar todas as coroutines de shake
            StopShake();

            // Parar outras coroutines se necessário
            if (typingCoroutine != null) StopCoroutine(typingCoroutine);
            if (autoAdvanceCoroutine != null) StopCoroutine(autoAdvanceCoroutine);
            if (backgroundFadeCoroutine != null) StopCoroutine(backgroundFadeCoroutine);
        }

        #region Script loading + indexing
        public void LoadScriptFromFile(string path)
        {
            string text;
            if (path.StartsWith("Resources/"))
            {
                string resPath = path.Substring("Resources/".Length);
                var t = Resources.Load<TextAsset>(resPath);
                if (t == null) { Debug.LogError($"Script não encontrado em Resources/{resPath}"); return; }
                text = t.text;
            }
            else if (File.Exists(path))
            {
                text = File.ReadAllText(path);
            }
            else
            {
                Debug.LogError($"Caminho de script desconhecido: {path}");
                return;
            }

            StopAllCoroutines();

            nodes = ScriptParser.Parse(text, out var diagnostics);
            ReportDiagnostics(path, diagnostics);
            IndexLabels();
            state = new EngineState();
            StartCoroutine(RunFromIndex(0));
        }

        /// <summary>
        /// Loga tudo que o parser encontrou de errado no carregamento, em vez de deixar
        /// o erro aparecer no meio da gameplay. Acrescenta as validações que só podem ser
        /// feitas aqui, porque dependem da cena (número de posições de escolha).
        /// </summary>
        void ReportDiagnostics(string path, List<ParseDiagnostic> diagnostics)
        {
            int capacity = choicePositions != null ? choicePositions.Length : 0;
            if (capacity > 0)
            {
                foreach (var node in nodes)
                {
                    if (node is MenuNode mn && mn.choices.Count > capacity)
                    {
                        diagnostics.Add(new ParseDiagnostic()
                        {
                            line = mn.lineNumber,
                            severity = DiagnosticSeverity.Error,
                            message = $"Menu com {mn.choices.Count} escolhas excede as {capacity} posições disponíveis na cena."
                        });
                    }
                }
            }

            if (diagnostics.Count == 0) return;

            int errors = 0;
            int warnings = 0;
            foreach (var d in diagnostics)
            {
                if (d.severity == DiagnosticSeverity.Error)
                {
                    errors++;
                    Debug.LogError($"[Script] {path} {d}");
                }
                else
                {
                    warnings++;
                    Debug.LogWarning($"[Script] {path} {d}");
                }
            }

            Debug.Log($"[Script] {path}: {errors} erro(s), {warnings} aviso(s).");
        }

        void IndexLabels()
        {
            labelIndices.Clear();
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] is LabelNode ln)
                {
                    labelIndices[ln.label] = i;
                }
            }
        }
        #endregion

        #region Execution
        IEnumerator RunFromIndex(int startIndex)
        {
            // Um recomeço (load, JumpToNextChoice) pode ter matado um HandleMenuNode no meio:
            // garante que não sobre menu aberto nem input travado.
            if (isInMenu)
            {
                isInMenu = false;
                HideMenuUI();
            }
            pendingChoiceIndex = -1;

            state.currentNodeIndex = startIndex;
            while (state.currentNodeIndex < nodes.Count)
            {
                var node = nodes[state.currentNodeIndex];
                // skip labels (they don't do anything at runtime)
                if (node is LabelNode) { state.currentNodeIndex++; continue; }

                if (node is DialogueNode dn)
                {
                    // Skip if unread-skipping and node is unvisited? we'll consider "history" to detect visited lines
                    string preview = (dn.speaker != null ? dn.speaker + ": " : "") + dn.text;
                    if (skipUnread && state.history.Contains(preview) == false)
                    {
                        state.history.Add(preview); // mark as read
                        state.currentNodeIndex++;
                        continue;
                    }
                    yield return StartCoroutine(HandleDialogueNode(dn));
                    state.currentNodeIndex++;
                }
                else if (node is CommandNode cn)
                {
                    bool jumped = HandleCommandNode(cn);
                    if (!jumped) state.currentNodeIndex++;
                    else { /* HandleCommandNode already set currentNodeIndex */ }
                    yield return null;
                }
                else if (node is MenuNode mn)
                {
                    yield return StartCoroutine(HandleMenuNode(mn));
                    // after menu, continue with currentNodeIndex (choices may have set it)
                }
                else
                {
                    state.currentNodeIndex++;
                }
            }

            Debug.Log("Fim do script.");
        }

        IEnumerator HandleDialogueNode(DialogueNode dn)
        {
            // Fill name box
            if (!string.IsNullOrEmpty(dn.speaker))
            {
                nameBox.text = dn.speaker;
            }
            else
            {
                nameBox.text = ""; // narrador
            }

            // convert our simple tags to TMP tags
            string formatted = ConvertTextTagsToTMP(dn.text);

            // apply inline parameters: speed, letterPause, color, style
            float speed = defaultTextSpeed;
            if (dn.parameters.TryGetValue("speed", out string sp))
            {
                if (float.TryParse(sp, out float spf)) speed = spf;
            }
            float charsPerSecond = speed;
            if (fastForward) charsPerSecond *= 3f;
            // allow per-line override: e.g., puppets: expression=happy -> show sprite
            if (dn.parameters.TryGetValue("expression", out string expr) && !string.IsNullOrEmpty(dn.speaker))
            {
                // change sprite expression (attempt to find sprite by "Character/expr")
                ShowCharacterExpression(dn.speaker, expr);
            }

            // Add to history
            state.history.Add((dn.speaker != null ? dn.speaker + ": " : "") + dn.text);

            AddToLog(dn);

            // Type the text - AGORA PASSANDO O SPEAKER
            isTyping = true;
            if (typingCoroutine != null) StopCoroutine(typingCoroutine);
            typingCoroutine = StartCoroutine(TypeTextCoroutine(formatted, charsPerSecond, dn.speaker));
            yield return new WaitUntil(() => isTyping == false);

            if (AutoText)
            {
                // Se AutoText estiver ativado, espera um delay e continua automaticamente
                isWaitingForInput = true;
                autoAdvanceCoroutine = StartCoroutine(AutoAdvanceCoroutine());
                yield return new WaitUntil(() => !isWaitingForInput);
            }
            else
            {
                // Se AutoText estiver desativado, espera por input do jogador
                isWaitingForInput = true;
                yield return new WaitUntil(() => !isWaitingForInput);
            }
        }

        IEnumerator AutoAdvanceCoroutine()
        {
            yield return new WaitForSeconds(autoTextDelay);
            if (isWaitingForInput)
            {
                ContinueFromWait();
            }
        }

        void ContinueFromWait()
        {
            isWaitingForInput = false;
            if (autoAdvanceCoroutine != null)
            {
                StopCoroutine(autoAdvanceCoroutine);
                autoAdvanceCoroutine = null;
            }
        }

        IEnumerator TypeTextCoroutine(string text, float charsPerSecond, string speaker)
        {
            textBox.text = "";
            int idx = 0;

            while (idx < text.Length)
            {
                float delay = 1f / Mathf.Max(1f, charsPerSecond);
                if (fastForward) delay *= 0.25f;

                if (text[idx] == '<')
                {
                    int close = text.IndexOf('>', idx);
                    if (close == -1)
                    {
                        textBox.text += text.Substring(idx);
                        idx = text.Length;
                    }
                    else
                    {
                        string tag = text.Substring(idx, close - idx + 1);
                        textBox.text += tag;
                        idx = close + 1;
                        continue;
                    }
                }
                else
                {
                    char currentChar = text[idx];
                    textBox.text += currentChar;

                    // Tocar som da vogal se for uma vogal
                    PlayVowelSound(currentChar, speaker);

                    idx++;
                    yield return new WaitForSeconds(delay);
                }
            }
            isTyping = false;
        }

        void PlayVowelSound(char character, string speaker)
        {
            // Só tocar som de vogal se o speaker for KuroNeko (case insensitive)
            if (!string.Equals(speaker, "KuroNeko", StringComparison.OrdinalIgnoreCase))
                return;

            // Verificar se o caractere é uma vogal e se temos um som para ela
            if (vowelSounds.ContainsKey(character))
            {
                AudioClip vowelClip = vowelSounds[character];
                if (vowelClip != null && effectsSource != null)
                {
                    // Usar o AudioSource de SFX para tocar o som da vogal
                    effectsSource.PlayOneShot(vowelClip, vowelVolume);
                }
            }
        }

        void FinishTypingInstant()
        {
            if (!isTyping) return;
            if (typingCoroutine != null) StopCoroutine(typingCoroutine);

            if (state.currentNodeIndex < nodes.Count && nodes[state.currentNodeIndex] is DialogueNode dn)
            {
                textBox.text = ConvertTextTagsToTMP(dn.text);
            }
            isTyping = false;

            if (AutoText && isWaitingForInput)
            {
                ContinueFromWait();
            }
        }

        bool HandleCommandNode(CommandNode cn)
        {
            string cmd = cn.command.ToLower();
            switch (cmd)
            {
                case "show":
                    // parameters: target (sprite/background), name, expression, pos (left,right,center), layer, fade=0.3
                    if (cn.parameters.TryGetValue("target", out string target))
                    {
                        if (target.Equals("bg", StringComparison.OrdinalIgnoreCase) || target.Equals("background", StringComparison.OrdinalIgnoreCase))
                        {
                            if (cn.parameters.TryGetValue("name", out string bgName))
                            {
                                string path = $"Sprites/Scenes/{bgName}";

                                if (bgName.StartsWith("Sprites/Scenes/"))
                                {
                                    path = bgName;
                                }
                                else if (bgName.StartsWith("Scenes/"))
                                {
                                    path = $"Sprites/{bgName}";
                                }

                                var spr = resources.LoadSprite(path);
                                if (spr != null)
                                {
                                    ShowBackGroundImage(spr, cn.parameters);
                                }
                                else
                                {
                                    string[] alternativePaths = {
                                        $"Sprites/Scenes/{bgName}",
                                        $"Sprites/{bgName}",
                                        bgName,
                                        Path.Combine("Assets", "Sprites", "Scenes", bgName)
                                    };

                                    foreach (string altPath in alternativePaths)
                                    {
                                        spr = resources.LoadSprite(altPath);
                                        if (spr != null)
                                        {
                                            Debug.Log($"Background encontrado em caminho alternativo: {altPath}");
                                            ShowBackGroundImage(spr, cn.parameters);
                                            break;
                                        }
                                    }

                                    if (spr == null)
                                    {
                                        Debug.LogWarning($"Show background falhou: sprite não encontrada. Tentou: {string.Join(", ", alternativePaths)}");
                                    }
                                }
                            }
                        }
                        if (target.Equals("textbox", StringComparison.OrdinalIgnoreCase))
                        {
                            if (textBoxRoot != null) textBoxRoot.SetActive(true);
                        }
                        else if (target.Equals("sprite", StringComparison.OrdinalIgnoreCase) || target.Equals("char", StringComparison.OrdinalIgnoreCase))
                        {
                            if (cn.parameters.TryGetValue("name", out string charName))
                            {
                                string expression = cn.parameters.ContainsKey("expression") ? cn.parameters["expression"] : null;
                                string path = $"Sprites/{charName}/{expression}";
                                var s = resources.LoadSprite(path);
                                if (s == null) // fallback: Sprites/<charName>
                                {
                                    s = resources.LoadSprite($"Sprites/{charName}");
                                }
                                ShowCharacterImage(charName, s, cn.parameters);
                            }
                        }
                    }
                    else
                    {
                        // legacy: 'show name=Alice expression=happy'
                        if (cn.parameters.TryGetValue("name", out string name))
                        {
                            string expression = cn.parameters.ContainsKey("expression") ? cn.parameters["expression"] : null;
                            var s = resources.LoadSprite($"Sprites/{name}/{expression}");
                            if (s == null) s = resources.LoadSprite($"Sprites/{name}");
                            ShowCharacterImage(name, s, cn.parameters);
                        }
                    }
                    return false;

                case "hide":
                    if (cn.parameters.TryGetValue("target", out string hideTarget))
                    {
                        if (hideTarget.Equals("textbox", StringComparison.OrdinalIgnoreCase))
                        {
                            HideTextBox(cn.parameters);
                        }
                        else if (hideTarget.Equals("bg", StringComparison.OrdinalIgnoreCase) || hideTarget.Equals("background", StringComparison.OrdinalIgnoreCase))
                        {
                            HideBackGroundImage(cn.parameters);
                        }
                        else if (hideTarget.Equals("sprite", StringComparison.OrdinalIgnoreCase))
                        {
                            if (cn.parameters.TryGetValue("name", out string hideName))
                            {
                                HideCharacter(hideName, cn.parameters);
                            }
                        }
                    }
                    else if (cn.parameters.TryGetValue("name", out string legacyHideName))
                    {
                        HideCharacter(legacyHideName);
                    }
                    return false;

                case "play":
                    if (cn.parameters.TryGetValue("type", out string type) && type == "bgm")
                    {
                        if (cn.parameters.TryGetValue("name", out string bgmName))
                        {
                            var clip = resources.LoadAudio($"Audio/BGM/{bgmName}");
                            if (clip != null && musicSource != null)
                            {
                                musicSource.clip = clip;
                                musicSource.loop = true;
                                musicSource.Play();
                            }
                        }
                    }
                    else if (cn.parameters.TryGetValue("type", out string sfxType) && sfxType == "sfx")
                    {
                        if (cn.parameters.TryGetValue("name", out string sfxName))
                        {
                            var clip = resources.LoadAudio($"Audio/SFX/{sfxName}");
                            if (clip != null && effectsSource != null)
                            {
                                effectsSource.PlayOneShot(clip);
                            }
                        }
                    }
                    else // legacy: play name=...
                    {
                        if (cn.parameters.TryGetValue("name", out string nm))
                        {
                            var clip = resources.LoadAudio($"Audio/{nm}");
                            if (clip != null && effectsSource != null) effectsSource.PlayOneShot(clip);
                        }
                    }
                    return false;

                case "stop":
                    if (cn.parameters.TryGetValue("type", out string stype) && stype == "bgm")
                    {
                        if (musicSource != null) musicSource.Stop();
                    }
                    else if (cn.parameters.TryGetValue("type", out string st) && st == "sfx")
                    {
                        if (effectsSource != null) effectsSource.Stop();
                    }
                    else
                    {
                        if (musicSource != null) musicSource.Stop();
                        if (effectsSource != null) effectsSource.Stop();
                    }
                    return false;

                case "jump":
                    if (cn.parameters.TryGetValue("to", out string label))
                    {
                        return JumpToLabel(label);
                    }
                    if (!string.IsNullOrEmpty(cn.raw))
                    {
                        return JumpToLabel(cn.raw); // allow '@jump labelname'
                    }
                    return false;

                case "scene":
                    if (cn.parameters.TryGetValue("name", out string sceneName))
                    {
                        // Here, just change background or call SceneManager if desired
                        Debug.Log($"Scene change requested: {sceneName}. Implement UnityEngine.SceneManagement.SceneManager.LoadScene if needed.");
                    }
                    return false;

                case "set":
                    // set key=value into variables
                    foreach (var kv in cn.parameters)
                    {
                        state.variables[kv.Key] = kv.Value;
                    }
                    return false;

                case "flag":
                    if (cn.parameters.TryGetValue("add", out string addf)) state.flags.Add(addf);
                    if (cn.parameters.TryGetValue("remove", out string rem)) state.flags.Remove(rem);
                    return false;

                case "if":
                    // conditional jump: if key==value goto label
                    // format: @if key=foo value=bar goto=label
                    if (cn.parameters.TryGetValue("key", out string keyk) &&
                        cn.parameters.TryGetValue("value", out string valv) &&
                        cn.parameters.TryGetValue("goto", out string gotoLabel))
                    {
                        if (state.variables.TryGetValue(keyk, out string cur) && cur == valv)
                        {
                            return JumpToLabel(gotoLabel);
                        }
                    }
                    return false;

                case "shake":
                    // Comando: @shake
                    // Parâmetros opcionais: duration, intensity
                    if (enableScreenShake)
                    {
                        ShakeScreen(cn.parameters);
                        Debug.Log($"Screen Shake ativado: {cn.parameters}");
                    }
                    return false;

                default:
                    Debug.LogWarning($"Comando não reconhecido: {cn.command} (raw: {cn.raw})");
                    return false;
            }
        }

        IEnumerator HandleMenuNode(MenuNode mn)
        {
            // Casos degenerados: avança o índice para não prender RunFromIndex neste mesmo nó.
            if (mn.choices.Count == 0)
            {
                Debug.LogError($"@menu sem escolhas (linha {mn.lineNumber}). Menu ignorado.");
                state.currentNodeIndex++;
                yield break;
            }

            if (mn.choices.Count > choicePositions.Length)
            {
                Debug.LogError($"Número de escolhas ({mn.choices.Count}) excede o número de posições disponíveis ({choicePositions.Length}) na linha {mn.lineNumber}. Menu ignorado.");
                state.currentNodeIndex++;
                yield break;
            }

            isInMenu = true;
            pendingChoiceIndex = -1;

            // Se há apenas uma escolha que pula imediatamente, escolhe automaticamente
            if (mn.choices.Count == 1 && !string.IsNullOrEmpty(mn.choices[0].jumpTo))
            {
                ApplyChoice(mn, 0);
                isInMenu = false;
                yield break;
            }

            // Mostrar menu na UI
            ShowMenuUI(mn.choices);

            // Esperar até que uma escolha seja feita. OnChoiceSelected preenche pendingChoiceIndex.
            while (pendingChoiceIndex == -1)
            {
                yield return null;
            }

            int selectedChoice = pendingChoiceIndex;
            pendingChoiceIndex = -1;

            HideMenuUI();
            ApplyChoice(mn, selectedChoice);
            isInMenu = false;
        }

        /// <summary>
        /// Aplica a escolha (variáveis + destino) e deixa state.currentNodeIndex pronto
        /// para RunFromIndex continuar. Sempre avança o índice, mesmo em erro, para não travar.
        /// </summary>
        void ApplyChoice(MenuNode mn, int choiceIndex)
        {
            if (choiceIndex < 0 || choiceIndex >= mn.choices.Count)
            {
                Debug.LogWarning($"Escolha inválida ({choiceIndex}) no menu da linha {mn.lineNumber}.");
                state.currentNodeIndex++;
                return;
            }

            var choice = mn.choices[choiceIndex];

            foreach (var kv in choice.parameters)
            {
                state.variables[kv.Key] = kv.Value;
            }

            if (!string.IsNullOrEmpty(choice.jumpTo) && JumpToLabel(choice.jumpTo))
            {
                return; // JumpToLabel já posicionou currentNodeIndex
            }

            state.currentNodeIndex++;
        }

        void ShowMenuUI(List<MenuChoice> choices)
        {
            // Ativar o painel do menu
            if (menuPanel != null)
                menuPanel.SetActive(true);

            ClearChoiceButtons();

            // Criar botões para cada escolha nas posições específicas
            for (int i = 0; i < choices.Count; i++)
            {
                if (i < choicePositions.Length && choicePositions[i] != null)
                {
                    CreateChoiceButton(choices[i], i, choicePositions[i]);
                }
                else
                {
                    Debug.LogError($"Posição {i} não atribuída para botão de escolha");
                }
            }
        }

        void HideMenuUI()
        {
            // Desativar o painel do menu
            if (menuPanel != null)
                menuPanel.SetActive(false);

            // Limpar todos os botões
            ClearChoiceButtons();
        }

        void CreateChoiceButton(MenuChoice choice, int index, Transform parentTransform)
        {
            if (choiceButtonPrefab == null)
            {
                Debug.LogError("ChoiceButtonPrefab não atribuído!");
                return;
            }

            if (parentTransform == null)
            {
                Debug.LogError($"ParentTransform não atribuído para botão {index}");
                return;
            }

            // Instanciar o botão no transform específico
            GameObject buttonGO = Instantiate(choiceButtonPrefab, parentTransform);
            currentChoiceButtons.Add(buttonGO);

            // Configurar o texto do botão
            TextMeshProUGUI buttonText = buttonGO.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = choice.text;
            }

            // Configurar o clique do botão
            Button button = buttonGO.GetComponent<Button>();
            if (button != null)
            {
                int choiceIndex = index;
                button.onClick.AddListener(() => OnChoiceSelected(choiceIndex));
            }
        }

        void ClearChoiceButtons()
        {
            foreach (GameObject button in currentChoiceButtons)
            {
                if (button != null)
                    Destroy(button);
            }
            currentChoiceButtons.Clear();
        }

        // Método chamado quando um botão de escolha é clicado.
        // Só registra a escolha: quem aplica é HandleMenuNode, que está esperando por ela.
        public void OnChoiceSelected(int choiceIndex)
        {
            if (!isInMenu) return; // clique fora de um menu ativo
            if (pendingChoiceIndex != -1) return; // menu já resolvido (clique duplo)

            pendingChoiceIndex = choiceIndex;
        }

        #endregion

        #region Helpers: text tags, sprites
        string ConvertTextTagsToTMP(string text)
        {
            // support some simple tags: [i] [/i], [b], [color=#RRGGBB]
            // We convert them to TMP-compatible tags: <i> <b> <color=#...>
            string outText = text;

            // [i] -> <i>
            outText = Regex.Replace(outText, @"\[(\/?)i\]", "<$1i>", RegexOptions.IgnoreCase);
            outText = Regex.Replace(outText, @"\[(\/?)b\]", "<$1b>", RegexOptions.IgnoreCase);
            // [color=#hex] -> <color=#hex>
            outText = Regex.Replace(outText, @"\[color=(#[0-9A-Fa-f]{3,6})\]", "<color=$1>", RegexOptions.IgnoreCase);
            outText = Regex.Replace(outText, @"\[/color\]", "</color>", RegexOptions.IgnoreCase);

            // Additional custom: {w=0.5} wait tags or {shake} can be implemented in a richer parser.
            // For now, remove braces not needed.
            outText = Regex.Replace(outText, @"\{.*?\}", "", RegexOptions.Singleline);

            return outText;
        }

        void ShowCharacterImage(string characterName, Sprite sprite, Dictionary<string, string> parameters)
        {
            if (sprite == null)
            {
                Debug.LogWarning($"ShowCharacterImage: sprite null para {characterName}");
                return;
            }

            // create a child Image under spriteContainer if not exists
            if (!activeCharacterSprites.ContainsKey(characterName))
            {
                var go = new GameObject("CHAR_" + characterName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(spriteContainer, false);
                var img = go.GetComponent<Image>();
                img.sprite = sprite;

                // CORREÇÃO: Preservar proporção da sprite
                img.preserveAspect = true; // Esta linha é crucial

                // Obter o RectTransform
                var rt = go.GetComponent<RectTransform>();

                // Configurar tamanho baseado na sprite original
                float spriteAspectRatio = sprite.rect.width / sprite.rect.height;

                // Definir tamanho apropriado (ajuste conforme necessário)
                float baseHeight = 600f; // Altura base em pixels
                float baseWidth = baseHeight * spriteAspectRatio;

                rt.sizeDelta = new Vector2(baseWidth, baseHeight);

                // Positioning melhorado
                if (parameters.TryGetValue("pos", out string pos))
                {
                    if (pos == "left")
                    {
                        rt.anchorMin = new Vector2(0f, 0f);
                        rt.anchorMax = new Vector2(0f, 0f);
                        rt.pivot = new Vector2(0f, 0f);
                        rt.anchoredPosition = new Vector2(50f, 50f); // Margem da esquerda e baixo
                    }
                    else if (pos == "right")
                    {
                        rt.anchorMin = new Vector2(1f, 0f);
                        rt.anchorMax = new Vector2(1f, 0f);
                        rt.pivot = new Vector2(1f, 0f);
                        rt.anchoredPosition = new Vector2(-50f, 50f); // Margem da direita e baixo
                    }
                    else // center
                    {
                        rt.anchorMin = new Vector2(0.5f, 0f);
                        rt.anchorMax = new Vector2(0.5f, 0f);
                        rt.pivot = new Vector2(0.5f, 0f);
                        rt.anchoredPosition = new Vector2(0f, 50f);
                    }
                }
                else
                {
                    // Default para center
                    rt.anchorMin = new Vector2(0.5f, 0f);
                    rt.anchorMax = new Vector2(0.5f, 0f);
                    rt.pivot = new Vector2(0.5f, 0f);
                    rt.anchoredPosition = new Vector2(0f, 50f);
                }

                activeCharacterSprites[characterName] = img;
            }
            else
            {
                // Atualizar sprite existente mantendo a proporção
                var img = activeCharacterSprites[characterName];
                img.sprite = sprite;
                img.preserveAspect = true; // Garantir que a proporção seja mantida

                // Opcional: Recalcular tamanho se necessário
                float spriteAspectRatio = sprite.rect.width / sprite.rect.height;
                float baseHeight = 600f;
                float baseWidth = baseHeight * spriteAspectRatio;

                var rt = img.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(baseWidth, baseHeight);
            }
        }

        void ShowCharacterExpression(string characterName, string expression)
        {
            // CORREÇÃO: Usar caminho relativo à pasta Resources
            string path = $"Sprites/{characterName}/{expression}";
            var s = resources.LoadSprite(path);
            if (s != null)
            {
                ShowCharacterImage(characterName, s, new Dictionary<string, string>());
            }
            else
            {
                Debug.Log($"Expressão não encontrada: {path}");
            }
        }

        void HideCharacter(string name, Dictionary<string, string> parameters = null)
        {
            if (activeCharacterSprites.TryGetValue(name, out Image img))
            {
                float fadeDuration = 0.5f;
                if (parameters != null && parameters.TryGetValue("fade", out string fadeValue))
                {
                    if (float.TryParse(fadeValue, out float customFade))
                    {
                        fadeDuration = customFade;
                    }
                }
                StartCoroutine(FadeOutCharacter(img, name, fadeDuration));
            }
        }

        IEnumerator FadeOutCharacter(Image img, string characterName, float duration = 0.5f)
        {
            Color originalColor = img.color;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Clamp01(1f - (elapsed / duration));
                img.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
                yield return null;
            }

            Debug.Log($"Tentando esconder personagem: {name}");

            if (activeCharacterSprites.TryGetValue(name, out Image img1))
            {
                Debug.Log($"Personagem {name} encontrado, destruindo...");
                Destroy(img1.gameObject);
                activeCharacterSprites.Remove(name);
                Debug.Log($"Personagem {name} escondido com sucesso");
            }
            else
            {
                Debug.LogWarning($"Personagem {name} não encontrado para esconder");
            }
        }

        void ShowBackGroundImage(Sprite spr, Dictionary<string, string> parameters = null)
        {
            if (spr == null)
            {
                Debug.LogWarning("ShowBackGroundImage: sprite nula");
                return;
            }

            if (backgroundImage == null)
            {
                Debug.LogWarning("ShowBackGroundImage: backgroundImage não está atribuído!");
                return;
            }

            if (backgroundFadeCoroutine != null)
            {
                StopCoroutine(backgroundFadeCoroutine);
                backgroundFadeCoroutine = null;
            }

            backgroundImage.sprite = spr;
            backgroundImage.color = new Color(1, 1, 1, 1);

            if (parameters != null && parameters.TryGetValue("fade", out string fadeValue))
            {
                if (float.TryParse(fadeValue, out float fadeTime))
                {
                    backgroundFadeCoroutine = StartCoroutine(FadeInBackground(fadeTime));
                }
            }

            if (parameters != null && parameters.TryGetValue("layer", out string layer))
            {
                var canvas = backgroundImage.GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    canvas.sortingOrder = int.TryParse(layer, out int order) ? order : 0;
                }
            }

            Debug.Log($"ShowBackGroundImage: exibindo background '{spr.name}'");
        }

        IEnumerator FadeInBackground(float duration)
        {
            if (backgroundImage == null) yield break;

            float elapsed = 0f;
            Color c = backgroundImage.color;
            c.a = 0f;
            backgroundImage.color = c;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Clamp01(elapsed / duration);
                backgroundImage.color = c;
                yield return null;
            }

            c.a = 1f;
            backgroundImage.color = c;
            backgroundFadeCoroutine = null;
        }

        void HideBackGroundImage(Dictionary<string, string> parameters = null)
        {
            if (backgroundImage == null || backgroundImage.sprite == null)
            {
                Debug.LogWarning("HideBackGroundImage: backgroundImage não está atribuído ou não tem sprite!");
                return;
            }

            // Interromper qualquer fade anterior
            if (backgroundFadeCoroutine != null)
            {
                StopCoroutine(backgroundFadeCoroutine);
                backgroundFadeCoroutine = null;
            }

            float fadeDuration = 0.5f; // Duração padrão do fade out
            if (parameters != null && parameters.TryGetValue("fade", out string fadeValue))
            {
                if (float.TryParse(fadeValue, out float customFade))
                {
                    fadeDuration = customFade;
                }
            }

            backgroundFadeCoroutine = StartCoroutine(FadeOutBackground(fadeDuration));
        }

        IEnumerator FadeOutBackground(float duration)
        {
            if (backgroundImage == null) yield break;

            float elapsed = 0f;
            Color originalColor = backgroundImage.color;
            Color targetColor = new Color(originalColor.r, originalColor.g, originalColor.b, 0f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Clamp01(1f - (elapsed / duration));
                backgroundImage.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
                yield return null;
            }

            // Opcional: Remover completamente o sprite após o fade out
            // backgroundImage.sprite = null;

            Debug.Log("Background escondido com fade out");
        }

        void HideTextBox(Dictionary<string, string> parameters = null)
        {
            if (textBoxRoot == null) return;

            float fadeDuration = 0.3f;
            if (parameters != null && parameters.TryGetValue("fade", out string fadeValue))
            {
                if (float.TryParse(fadeValue, out float customFade))
                {
                    fadeDuration = customFade;
                }
            }

            StartCoroutine(FadeOutTextBox(fadeDuration));
        }

        IEnumerator FadeOutTextBox(float duration)
        {
            // Se a textbox tiver CanvasGroup, usar alpha
            var canvasGroup = textBoxRoot.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                float elapsed = 0f;
                float startAlpha = canvasGroup.alpha;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / duration);
                    yield return null;
                }
            }

            textBoxRoot.SetActive(false);

            // Reset alpha se usou CanvasGroup
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }
        }
        #endregion

        #region Jumping and control
        bool JumpToLabel(string label)
        {
            if (string.IsNullOrEmpty(label)) return false;
            if (labelIndices.TryGetValue(label, out int idx))
            {
                state.currentNodeIndex = idx;
                return true;
            }
            else
            {
                Debug.LogWarning($"Label não encontrado: {label}");
                return false;
            }
        }

        public void Continue()
        {
            if (isWaitingForInput)
            {
                ContinueFromWait();
            }
            else if (isTyping)
            {
                FinishTypingInstant();
            }
        }

        void JumpToNextChoice()
        {
            // find next MenuNode
            for (int i = state.currentNodeIndex + 1; i < nodes.Count; i++)
            {
                if (nodes[i] is MenuNode)
                {
                    state.currentNodeIndex = i;
                    // interrupt current run and start from index
                    StopAllCoroutines();
                    StartCoroutine(RunFromIndex(state.currentNodeIndex));
                    return;
                }
            }
            Debug.Log("Nenhuma escolha futura encontrada.");
        }
        #endregion

        #region Save/Load

        // Método para definir o slot atual
        public void SetCurrentSlot(int slotIndex)
        {
            currentSaveSlot = slotIndex;
            Debug.Log($"Slot atual definido para: {currentSaveSlot}");
        }

        // Método para salvar no slot atual
        public void SaveToCurrentSlot()
        {
            if (currentSaveSlot < 0)
            {
                Debug.LogWarning("Nenhum slot atual definido. Usando slot 0 como padrão.");
                currentSaveSlot = 0;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SaveToSlot(currentSaveSlot);
                Debug.Log($"Jogo salvo no slot atual: {currentSaveSlot + 1}");
            }
            else
            {
                Debug.LogError("GameManager.Instance é nulo!");
            }
        }

        public void SaveToSlot(int slotIndex)
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SaveToSlot(slotIndex);
            }
            else
            {
                Debug.LogError("GameManager.Instance é nulo! Não foi possível salvar.");
            }
        }

        public void LoadFromSaveData(SaveData saveData)
        {
            if (saveData == null || saveData.isEmpty) return;

            state.currentNodeIndex = saveData.currentNodeIndex;
            state.totalPlayTime = saveData.playTime;

            // Desserializar variáveis, flags, histórico
            if (!string.IsNullOrEmpty(saveData.variablesJson))
            {
                state.variables = JsonConvert.DeserializeObject<Dictionary<string, string>>(saveData.variablesJson);
            }

            if (!string.IsNullOrEmpty(saveData.flagsJson))
            {
                state.flags = JsonConvert.DeserializeObject<HashSet<string>>(saveData.flagsJson);
            }

            if (!string.IsNullOrEmpty(saveData.historyJson))
            {
                state.history = JsonConvert.DeserializeObject<List<string>>(saveData.historyJson);
            }

            StopAllCoroutines();
            StartCoroutine(RunFromIndex(state.currentNodeIndex));

            Debug.Log($"Jogo carregado do slot. Node atual: {state.currentNodeIndex}");
        }
        #endregion

        #region Public methods for UI
        public void ToggleAutoText()
        {
            AutoText = !AutoText;
            if (AutoText && isWaitingForInput)
            {
                ContinueFromWait();
            }
        }

        public void SetAutoText(bool enabled)
        {
            AutoText = enabled;
            if (AutoText && isWaitingForInput)
            {
                ContinueFromWait();
            }
        }
        #endregion

        #region Public API for UI
        public void ShowSaveMenu()
        {
            if (saveLoadMenu != null)
            {
                saveLoadMenu.SetActive(true);
                var saveLoadUI = saveLoadMenu.GetComponent<SaveLoadUI>();
                if (saveLoadUI != null)
                {
                    saveLoadUI.ShowSaveMenu(); // Método agora existe
                }
                else
                {
                    Debug.LogWarning("SaveLoadUI component not found on saveLoadMenu!");
                }
            }
        }

        public void ShowLoadMenu()
        {
            if (saveLoadMenu != null)
            {
                saveLoadMenu.SetActive(true);
                var saveLoadUI = saveLoadMenu.GetComponent<SaveLoadUI>();
                if (saveLoadUI != null)
                {
                    saveLoadUI.ShowLoadMenu(); // Método agora existe
                }
                else
                {
                    Debug.LogWarning("SaveLoadUI component not found on saveLoadMenu!");
                }
            }
        }

        public void HideSaveLoadMenu()
        {
            if (saveLoadMenu != null)
            {
                saveLoadMenu.SetActive(false);
            }
        }

        public string GetCurrentPlayTimeFormatted()
        {
            return FormatPlayTime(state.totalPlayTime);
        }

        private string FormatPlayTime(float totalSeconds)
        {
            TimeSpan time = TimeSpan.FromSeconds(totalSeconds);
            return string.Format("{0:D2}:{1:D2}:{2:D2}",
                time.Hours, time.Minutes, time.Seconds);
        }
        #endregion

        #region UI Toggle Functions
        public void ToggleUI()
        {
            isUIVisible = !isUIVisible;
            UpdateUIVisibility();
        }

        public void ShowUI()
        {
            isUIVisible = true;
            UpdateUIVisibility();
        }

        public void HideUI()
        {
            isUIVisible = false;
            UpdateUIVisibility();
        }

        private void UpdateUIVisibility()
        {
            if (textBoxRoot != null)
            {
                // Usar CanvasGroup para fade suave se existir
                var canvasGroup = textBoxRoot.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    StartCoroutine(FadeUIElement(canvasGroup, isUIVisible));
                }
                else
                {
                    textBoxRoot.SetActive(isUIVisible);
                }
            }

            // Esconder nome também
            if (nameBox != null)
                nameBox.gameObject.SetActive(isUIVisible);

            // Esconder botões de escolha se estiverem visíveis
            if (!isUIVisible && isInMenu)
            {
                HideMenuUI();
            }

            Debug.Log($"UI {(isUIVisible ? "visível" : "escondida")}");
        }

        private IEnumerator FadeUIElement(CanvasGroup canvasGroup, bool show)
        {
            float targetAlpha = show ? 1f : 0f;
            float startAlpha = canvasGroup.alpha;
            float elapsed = 0f;

            while (elapsed < uiFadeDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / uiFadeDuration);
                yield return null;
            }

            canvasGroup.alpha = targetAlpha;

            // Se estiver escondendo, desativar completamente para não interferir com clicks
            if (!show)
            {
                canvasGroup.gameObject.SetActive(false);
            }
            else
            {
                canvasGroup.gameObject.SetActive(true);
            }
        }

        // Para controle por tecla (opcional)
        private void HandleUIToggleInput()
        {
            if (Input.GetKeyDown(toggleUIKey))
            {
                ToggleUI();
            }
        }
        #endregion

        #region Log System

        private void AddToLog(DialogueNode dn)
        {
            if (dn == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(dn.text))
            {
                return;
            }

            string currentBackground = backgroundImage != null && backgroundImage.sprite != null ?
                backgroundImage.sprite.name : "";

            string cleanText = CleanTextForLog(dn.text);

            var logEntry = new LogEntry(dn.speaker, cleanText, currentBackground);

            foreach (var character in activeCharacterSprites.Keys)
            {
                logEntry.activeCharacters.Add(character);
            }

            state.logHistory.AddEntry(logEntry);

            if (isLogOpen)
            {
                RefreshLogUI();
            }
        }

        public void ToggleLog()
        {
            isLogOpen = !isLogOpen;

            if (logPanel != null)
            {
                logPanel.SetActive(isLogOpen);

                if (isLogOpen)
                {
                    RefreshLogUI();

                    Time.timeScale = 0f;
                }
                else
                {
                    Time.timeScale = 1f;
                }
            }
        }

        public void ShowLog()
        {
            isLogOpen = true;
            if (logPanel != null)
            {
                logPanel.SetActive(true);
                RefreshLogUI();
                Time.timeScale = 0f;
            }
        }

        public void HideLog()
        {
            isLogOpen = false;
            if (logPanel != null)
            {
                logPanel.SetActive(false);
                Time.timeScale = 1f;
            }
        }

        private void RefreshLogUI()
        {
            if (logContentParent == null)
            {
                return;
            }

            if (logEntryPrefab == null)
            {
                return;
            }

            int childCount = logContentParent.childCount;

            foreach (Transform child in logContentParent)
            {
                Destroy(child.gameObject);
            }

            var entriesToShow = GetFilteredLogEntries();

            foreach (var entry in entriesToShow)
            {
                CreateLogEntryUI(entry);
            }

            if (autoScrollToBottom && logScrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                logScrollRect.verticalNormalizedPosition = 0f;
            }
        }

        private List<LogEntry> GetFilteredLogEntries()
        {
            if (string.IsNullOrEmpty(logSearchField?.text))
            {
                return state.logHistory.entries;
            }

            return state.logHistory.SearchEntries(logSearchField.text);
        }

        private void CreateLogEntryUI(LogEntry entry)
        {
            var logEntryGO = Instantiate(logEntryPrefab, logContentParent);
            var logEntryUI = logEntryGO.GetComponent<LogEntryUI>();

            if (logEntryUI != null)
            {
                logEntryUI.Initialize(entry);
            }
            else
            {
                var texts = logEntryGO.GetComponentsInChildren<TextMeshProUGUI>();
                foreach (var text in texts)
                {
                    if (text.name == "SpeakerText")
                        text.text = string.IsNullOrEmpty(entry.speaker) ? "Narrador" : entry.speaker;
                    else if (text.name == "DialogueText")
                        text.text = entry.text;
                }
            }
        }

        public void OnLogSearchValueChanged(string searchText)
        {
            RefreshLogUI();
        }

        public void ClearLogSearch()
        {
            if (logSearchField != null)
            {
                logSearchField.text = "";
                RefreshLogUI();
            }
        }

        public void ClearEntireLog()
        {
            state.logHistory.Clear();
            RefreshLogUI();
        }

        private string CleanTextForLog(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            string cleanText = text;

            var patternsToRemove = new List<string>
            {
                // Tags de cor
                @"\[color=#[0-9A-Fa-f]{3,6}\]",
                @"\[/color\]",
        
                // Tags de estilo básicas
                @"\[(\/?)i\]",
                @"\[(\/?)b\]",
                @"\[(\/?)u\]",
        
                // Parâmetros inline entre chaves
                @"\{[^}]+\}",
        
                // Tags TMP
                @"<color=#[0-9A-Fa-f]{3,6}>",
                @"</color>",
                @"<(\/?)i>",
                @"<(\/?)b>",
                @"<(\/?)u>",
        
                // Efeitos especiais TMP (caso use)
                @"<[^>]*>"
            };

            foreach (string pattern in patternsToRemove)
            {
                cleanText = Regex.Replace(cleanText, pattern, "", RegexOptions.IgnoreCase);
            }

            cleanText = cleanText.Trim();
            cleanText = Regex.Replace(cleanText, @"\s+", " "); 

            return cleanText;
        }

        private void HandleLogInput()
        {
            if (Input.GetKeyDown(logToggleKey) && !isInMenu)
            {
                ToggleLog();
            }
        }

        #endregion

        #region Screen Shake Functions
        public void ShakeScreen(float duration = 0.5f, float intensity = 0.1f)
        {
            if (!enableScreenShake) return;

            if (shakeCoroutine != null)
                StopCoroutine(shakeCoroutine);

            shakeCoroutine = StartCoroutine(ShakeCoroutine(duration, intensity));
        }

        public void ShakeScreen(Dictionary<string, string> parameters)
        {
            if (!enableScreenShake) return;

            float duration = shakeDefaultDuration;
            float intensity = shakeDefaultIntensity;

            if (parameters.TryGetValue("duration", out string durationStr))
            {
                if (float.TryParse(durationStr, out float customDuration))
                    duration = customDuration;
            }

            if (parameters.TryGetValue("intensity", out string intensityStr))
            {
                if (float.TryParse(intensityStr, out float customIntensity))
                    intensity = customIntensity;
            }

            ShakeScreen(duration, intensity);
        }

        private IEnumerator ShakeCoroutine(float duration, float intensity)
        {
            if (mainCameraTransform == null) yield break;

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                // Calcular progresso (0 a 1)
                float progress = elapsed / duration;

                // Reduzir intensidade ao longo do tempo
                float currentIntensity = intensity * (1f - progress);

                // Gerar offset aleatório
                float x = UnityEngine.Random.Range(-1f, 1f) * currentIntensity;
                float y = UnityEngine.Random.Range(-1f, 1f) * currentIntensity;

                // Aplicar shake
                mainCameraTransform.localPosition = originalCameraPosition + new Vector3(x, y, 0f);

                yield return null;
            }

            // Restaurar posição original
            mainCameraTransform.localPosition = originalCameraPosition;
            shakeCoroutine = null;
        }

        public void StopShake()
        {
            if (shakeCoroutine != null)
            {
                StopCoroutine(shakeCoroutine);
                shakeCoroutine = null;
            }

            if (mainCameraTransform != null)
            {
                mainCameraTransform.localPosition = originalCameraPosition;
            }
        }
        #endregion

        #region QuickSave to Pause Menu
        public int GetCurrentNodeIndex()
        {
            return state.currentNodeIndex;
        }

        public Dictionary<string, string> GetVariables()
        {
            return new Dictionary<string, string>(state.variables);
        }

        public HashSet<string> GetFlags()
        {
            return new HashSet<string>(state.flags);
        }

        public List<string> GetHistory()
        {
            return new List<string>(state.history);
        }

        public float GetTotalPlayTime()
        {
            return state.totalPlayTime;
        }

        #endregion
    }
    #endregion
}
