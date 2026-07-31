using UnityEngine;

namespace VisualNovelEngine
{
    using UnityEngine;

    public class ResourceManager
    {
        // Strategy: resources should be in Resources folders or assigned by name.
        // For sprites: "Sprites/Personagem_Alice/happy"
        // Backgrounds: "Backgrounds/park"
        // Audio: "Audio/BGM/peaceful"

        public Sprite LoadSprite(string path)
        {
            var s = Resources.Load<Sprite>(path);
            if (s == null) Debug.LogWarning($"Sprite not found: {path}");
            return s;
        }

        public AudioClip LoadAudio(string path)
        {
            var a = Resources.Load<AudioClip>(path);
            if (a == null) Debug.LogWarning($"AudioClip not found: {path}");
            return a;
        }

        public Sprite LoadCG(string path)
        {
            var x = Resources.Load<Sprite>(path);
            if (x == null) Debug.LogWarning($"Sprite not found: {path}");
            return x;
        }

        public GameObject LoadPrefab(string path)
        {
            return Resources.Load<GameObject>(path);
        }
    }
}