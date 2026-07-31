using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class SaveLoadUI : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject slotButtonPrefab;
    public Transform slotsContainer;
    public Button backButton;
    public TextMeshProUGUI menuTitle;

    [Header("Anchors de Slots (ordem 0–2)")]
    public RectTransform[] slotAnchors = new RectTransform[3];

    [Header("Configuração Visual")]
    public Vector2 slotSize = new Vector2(400, 100);

    [Header("Menu Type")]
    public bool isLoadMenu = false;

    // Cache interno
    private GameObject cachedSlotButtonPrefab;
    private Transform cachedSlotsContainer;
    private Button cachedBackButton;
    private TextMeshProUGUI cachedMenuTitle;

    private void Start()
    {
        if (slotAnchors == null || slotAnchors.Length == 0)
            slotAnchors = slotsContainer.GetComponentsInChildren<RectTransform>();

        if (cachedBackButton != null)
        {
            cachedBackButton.onClick.RemoveAllListeners();
            cachedBackButton.onClick.AddListener(HideMenu);
        }
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        if (cachedSlotsContainer != null && slotAnchors.Length > 0)
            StartCoroutine(RefreshAfterFrame());
    }

    private void CacheReferences()
    {
        cachedSlotButtonPrefab = slotButtonPrefab;
        cachedSlotsContainer = slotsContainer;
        cachedBackButton = backButton;
        cachedMenuTitle = menuTitle;
    }

    // ================================
    // MÉTODOS PRINCIPAIS
    // ================================

    public void ShowSaveMenu()
    {
        isLoadMenu = false;
        ShowMenu();
    }

    public void ShowLoadMenu()
    {
        isLoadMenu = true;
        ShowMenu();
    }

    public void ShowMenu()
    {
        CacheReferences();

        gameObject.SetActive(true);

        if (cachedMenuTitle != null)
            cachedMenuTitle.text = isLoadMenu ? "Carregar Jogo" : "Novo Jogo";

        StartCoroutine(RefreshAfterFrame());
    }

    private IEnumerator RefreshAfterFrame()
    {
        yield return null; // espera 1 frame
        yield return null; // garante que o layout está montado
        RefreshSlots();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(cachedSlotsContainer as RectTransform);
    }

    public void HideMenu()
    {
        gameObject.SetActive(false);
    }

    // ================================
    // GERENCIAMENTO DE SLOTS
    // ================================

    public void RefreshSlots()
    {
        if (cachedSlotsContainer == null || cachedSlotButtonPrefab == null)
        {
            Debug.LogError("Referências de container ou prefab não definidas!");
            return;
        }

        // Limpa slots antigos
        for (int i = cachedSlotsContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(cachedSlotsContainer.GetChild(i).gameObject);
        }

        // Cria os 3 slots
        for (int i = 0; i < 3; i++)
        {
            CreateSlotButton(i);
        }
    }

    private void CreateSlotButton(int slotIndex)
    {
        GameObject slotGO = Instantiate(cachedSlotButtonPrefab, cachedSlotsContainer);
        slotGO.name = $"Slot_{slotIndex + 1}";

        RectTransform rt = slotGO.GetComponent<RectTransform>();

        if (rt != null)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = slotSize;
            rt.localScale = Vector3.one;

            // Se houver âncora correspondente, posiciona nela
            if (slotAnchors != null && slotIndex < slotAnchors.Length && slotAnchors[slotIndex] != null)
            {
                RectTransform anchor = slotAnchors[slotIndex];
                rt.position = anchor.position;
            }
            else
            {
                rt.anchoredPosition = new Vector2(0, -slotIndex * (slotSize.y + 20)); // fallback
            }
        }

        // Dados e comportamento
        SaveSlotUI slotUI = slotGO.GetComponent<SaveSlotUI>();
        SaveData slotData = GameManager.Instance.GetSlotData(slotIndex);

        if (slotUI != null)
            slotUI.Initialize(slotIndex, slotData, isLoadMenu);

        Button slotButton = slotGO.GetComponent<Button>();
        if (slotButton != null)
        {
            slotButton.onClick.RemoveAllListeners();

            int currentIndex = slotIndex;
            if (isLoadMenu)
            {
                if (slotData != null && !slotData.isEmpty)
                    slotButton.onClick.AddListener(() => OnLoadSlotSelected(currentIndex));
                else
                    slotButton.interactable = false;
            }
            else
            {
                slotButton.onClick.AddListener(() => OnSaveSlotSelected(currentIndex));
            }
        }
    }

    // ================================
    // AÇÕES DE SLOT
    // ================================

    private void OnSaveSlotSelected(int slotIndex)
    {
        SaveData existingData = GameManager.Instance.GetSlotData(slotIndex);
        if (existingData != null && !existingData.isEmpty)
            ShowOverwriteConfirmation(slotIndex);
        else
            StartNewGameAndSave(slotIndex);
    }

    private void OnLoadSlotSelected(int slotIndex)
    {
        GameManager.Instance.LoadFromSlot(slotIndex);
        HideMenu();
    }

    private void ShowOverwriteConfirmation(int slotIndex)
    {
        StartNewGameAndSave(slotIndex);
    }

    private void StartNewGameAndSave(int slotIndex)
    {
        GameManager.Instance.SaveToSlot(slotIndex);
        GameManager.Instance.StartNewGame();
        HideMenu();
    }

    public void ForceUpdateReferences()
    {
        CacheReferences();
    }
}
