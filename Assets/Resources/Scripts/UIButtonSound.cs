using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UIButtonSound : MonoBehaviour, ISelectHandler, IPointerEnterHandler
{
    [Header("Referências de Áudio")]
    [Tooltip("Fonte de áudio por onde o som será reproduzido (geralmente o canal 'Effects').")]
    public AudioSource effectsSource;

    [Tooltip("Som a ser tocado quando o botão for destacado ou selecionado.")]
    public AudioClip highlightClip;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();

        if (effectsSource == null)
        {
            Debug.LogWarning($"{name}: Nenhum AudioSource definido no campo 'Effects'.");
        }
    }

    // Chamado quando o botão é destacado com o mouse
    public void OnPointerEnter(PointerEventData eventData)
    {
        PlaySound();
    }

    // Chamado quando o botão é selecionado (por teclado, controle, etc)
    public void OnSelect(BaseEventData eventData)
    {
        PlaySound();
    }

    private void PlaySound()
    {
        if (effectsSource != null && highlightClip != null)
        {
            effectsSource.PlayOneShot(highlightClip);
        }
    }
}
