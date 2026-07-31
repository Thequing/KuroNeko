using UnityEngine;
using TMPro;

[ExecuteInEditMode]
[RequireComponent(typeof(TextMeshProUGUI))]
public class CurvedText : MonoBehaviour
{
    [SerializeField, Tooltip("Raio do arco de texto")]
    private float radius = 100f;

    [SerializeField, Tooltip("Fator de espaçamento entre letras (1 = normal)")]
    private float letterSpacing = 1f;

    private TextMeshProUGUI textComponent;

    void Awake()
    {
        // Obtém o componente TextMeshProUGUI associado
        textComponent = GetComponent<TextMeshProUGUI>();
    }

    void OnEnable()
    {
        // Inscreve no evento que ocorre antes de renderizar o texto
        textComponent = GetComponent<TextMeshProUGUI>();
        textComponent.OnPreRenderText += UpdateTextMesh;
    }

    void OnDisable()
    {
        // Limpa inscrição ao desabilitar o script
        if (textComponent != null)
            textComponent.OnPreRenderText -= UpdateTextMesh;
    }

    void OnValidate()
    {
        if (textComponent == null)
            textComponent = GetComponent<TextMeshProUGUI>();

        // Garante que o TextMeshPro atualize o textInfo antes de usar
        textComponent.ForceMeshUpdate();

        // Agora o textInfo está válido
        if (textComponent.textInfo != null && textComponent.textInfo.characterCount > 0)
            UpdateTextMesh(textComponent.textInfo);
    }

    private void UpdateTextMesh(TMP_TextInfo textInfo)
    {
        if (textInfo == null) return;

        // Itera por cada caractere do texto
        int characterCount = textInfo.characterCount;
        for (int i = 0; i < characterCount; i++)
        {
            if (!textInfo.characterInfo[i].isVisible)
                continue;

            // Índice dos vértices do caractere atual no mesh
            int vertexIndex = textInfo.characterInfo[i].vertexIndex;
            int materialIndex = textInfo.characterInfo[i].materialReferenceIndex;
            Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;

            // Calcula o ponto médio de base (baseline) do caractere
            Vector2 charMidBaseline = new Vector2(
                (vertices[vertexIndex + 0].x + vertices[vertexIndex + 2].x) / 2,
                textInfo.characterInfo[i].baseLine
            );

            // Centraliza os vértices em relação ao ponto médio
            vertices[vertexIndex + 0] -= (Vector3)charMidBaseline;
            vertices[vertexIndex + 1] -= (Vector3)charMidBaseline;
            vertices[vertexIndex + 2] -= (Vector3)charMidBaseline;
            vertices[vertexIndex + 3] -= (Vector3)charMidBaseline;

            // Calcula matriz de transformação (posição+rotação) do caractere
            Matrix4x4 matrix = ComputeTransformationMatrix(charMidBaseline, textInfo, i);

            // Aplica a transformação nos 4 vértices do caractere
            vertices[vertexIndex + 0] = matrix.MultiplyPoint3x4(vertices[vertexIndex + 0]);
            vertices[vertexIndex + 1] = matrix.MultiplyPoint3x4(vertices[vertexIndex + 1]);
            vertices[vertexIndex + 2] = matrix.MultiplyPoint3x4(vertices[vertexIndex + 2]);
            vertices[vertexIndex + 3] = matrix.MultiplyPoint3x4(vertices[vertexIndex + 3]);
        }
    }

    private Matrix4x4 ComputeTransformationMatrix(Vector3 charMidBaseline, TMP_TextInfo textInfo, int charIndex)
    {
        // Ajusta o raio para a linha atual (caso haja múltiplas linhas)
        float radiusLine = radius + textInfo.lineInfo[textInfo.characterInfo[charIndex].lineNumber].baseline;
        float circumference = 2 * Mathf.PI * radiusLine;

        // Aplica espaçamento extra ao deslocamento horizontal do caractere
        float adjustedX = charMidBaseline.x * letterSpacing;

        // Calcula ângulo em radianos para posicionar o caractere no arco
        float angle = ((adjustedX / circumference - 0.5f) * 360f + 90f) * Mathf.Deg2Rad;
        float cos = Mathf.Cos(angle);
        float sin = Mathf.Sin(angle);

        // Nova posição do ponto médio no arco circular
        Vector2 offsetPos = new Vector2(cos * radiusLine, -sin * radiusLine);

        // Rotação de acordo com a tangente do círculo
        float rotation = -Mathf.Atan2(sin, cos) * Mathf.Rad2Deg - 90f;

        // Retorna a matriz de transformação (translação + rotação)
        return Matrix4x4.TRS(
            new Vector3(offsetPos.x, offsetPos.y, 0),
            Quaternion.AngleAxis(rotation, Vector3.forward),
            Vector3.one
        );
    }
}
