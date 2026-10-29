using System;
using System.IO;
using UnityEngine;

namespace Echoes
{
    /// <summary>Only attached to the pilot menu. Keeps experimental art and saves separate from RC2.</summary>
    public sealed class Phase52PilotRuntime : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void SaveIsolation()
        {
            if (!Application.version.StartsWith("0.5.2-pilot", StringComparison.Ordinal)) return;
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args,"-echoes-smoke") >= 0) return; // Existing smoke runner owns its own isolated folder.
            int index = Array.IndexOf(args,"-echoes-save-dir");
            string folder = index >= 0 && index+1 < args.Length ? args[index+1] : Path.Combine(Application.persistentDataPath,"phase52-pilot");
            Directory.CreateDirectory(folder);
            SaveSystem.UseFolder(folder);
            Debug.Log("[Echoes][P52] pilot saves isolated: "+folder);
        }

        void Start()
        {
            var menu = GetComponent<MainMenuController>();
            if (menu == null || menu.CreditsLabel == null) return;
            const string old = "No external art, music, sound or font files are used. Music and sounds are original and synthesised procedurally. The interface uses the font and sprites built into Unity.";
            bool b3 = Application.version.StartsWith("0.5.2-pilot.3", StringComparison.Ordinal);
            bool b4 = Application.version.StartsWith("0.5.2-pilot.4", StringComparison.Ordinal);
            bool b5 = Application.version.StartsWith("0.5.2-pilot.5", StringComparison.Ordinal);
            menu.CreditsLabel.text = GameCredits.Text.Replace(old, b5
                ? "Pilot B5 (painted environment): the spring, the broadleaf trees, the garden plants and stones, and the meadow and soil textures are original AI-generated illustrations supplied by the owner (fitted, registered and recoloured by script); the caretaker drawings, the workshop, the willow, the ground plants and the minimap frame are the owner's B4 illustrations; the villager sprites and portraits are cut from original illustrations made with OpenAI image generation and supplied by the owner. Pipelines, shaders, camera, minimap and scene builder were made with Anthropic Claude Code. Owner visual review pending. Original synthesised audio and Unity built-in font retained."
                : b4
                ? "Pilot B4 (high-angle 2.5D): the caretaker drawings, the painted workshop, willow and spring, the ground plants, the plaza stone and the minimap frame are original AI-generated illustrations supplied by the owner, cleaned and fitted by script; the villager sprites and portraits are cut from original illustrations made with OpenAI image generation and supplied by the owner. Pipelines, shaders, camera, minimap and scene builder were made with Anthropic Claude Code. Owner visual review pending. Original synthesised audio and Unity built-in font retained."
                : b3
                ? "Pilot B3 (2.5D): the caretaker and villager sprites and portraits are animated and cut from original illustrations made with OpenAI image generation and supplied by the owner; the sprite pipeline, shaders, fixed camera, foliage and path art were made with Anthropic Claude Code. Owner visual review pending. Original synthesised audio and Unity built-in font retained."
                : "Pilot B2: the original Blender-authored caretaker, its code-painted textures, toon shader, animation, foliage and path art were made with Anthropic Claude Code (first pilot: OpenAI Codex). Caretaker portrait: original OpenAI image generation supplied by the owner. Owner visual review pending. Original synthesised audio and Unity built-in font retained.");
        }
    }
}
