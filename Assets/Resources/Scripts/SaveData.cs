using UnityEngine;
using System;

[System.Serializable]
public class SaveData
{
    public bool isEmpty = true;
    public string previewText;
    public string backgroundSprite;
    public float playTime;
    public string saveDate;
    public string sceneName;

    // Dados do estado do Visual Novel Engine
    public int currentNodeIndex;
    public string variablesJson; // Serializa o Dictionary como JSON
    public string flagsJson;    // Serializa o HashSet como JSON
    public string historyJson;  // Serializa a List como JSON
}