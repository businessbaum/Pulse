using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pulse
{
    // What the player has set in the options menu. It belongs to this computer, not to a save, so it
    // is kept apart from the profile and survives starting over. Every setting is one of a few named
    // choices; the menu shows them without knowing what they do.
    public static class Options
    {
        // One line of the options menu.
        public sealed class Setting
        {
            public string name;
            public string[] choices;
            public Func<int> get;
            public Action<int> set;
        }

        [Serializable]
        sealed class Stored
        {
            public int shake = 2, distance = 1;
            public bool flashes = true, fullscreen = true, vsync = true;
        }

        const string Key = "pulse.options";
        static readonly float[] Shakes = { 0f, 0.45f, 1f }, Distances = { 0.88f, 1f, 1.16f };

        static Stored stored = new Stored();

        // How strongly the camera rattles: 0 is not at all, 1 is as designed.
        public static float Shake => Shakes[Mathf.Clamp(stored.shake, 0, Shakes.Length - 1)];
        // Whether the whole picture may flash, as it does for lightning and explosions.
        public static bool Flashes => stored.flashes;
        // How far away the camera stands: 1 is as designed.
        public static float Distance => Distances[Mathf.Clamp(stored.distance, 0, Distances.Length - 1)];

        public static readonly IReadOnlyList<Setting> All = new[]
        {
            new Setting { name = "Screen shake", choices = new[] { "Off", "Reduced", "Full" }, get = () => stored.shake, set = v => stored.shake = v },
            new Setting { name = "Flashes", choices = new[] { "Off", "On" }, get = () => stored.flashes ? 1 : 0, set = v => stored.flashes = v == 1 },
            new Setting { name = "Camera", choices = new[] { "Close", "Normal", "Far" }, get = () => stored.distance, set = v => stored.distance = v },
            new Setting { name = "Fullscreen", choices = new[] { "Off", "On" }, get = () => stored.fullscreen ? 1 : 0, set = v => stored.fullscreen = v == 1 },
            new Setting { name = "V-Sync", choices = new[] { "Off", "On" }, get = () => stored.vsync ? 1 : 0, set = v => stored.vsync = v == 1 },
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Load()
        {
            stored = PlayerPrefs.HasKey(Key) ? JsonUtility.FromJson<Stored>(PlayerPrefs.GetString(Key)) ?? new Stored() : new Stored();
            Apply();
        }

        // Moves a setting one choice on or back, saves it and makes it take effect.
        public static void Step(Setting setting, int by)
        {
            int count = setting.choices.Length;
            setting.set((setting.get() + by + count) % count);
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(stored));
            PlayerPrefs.Save();
            Apply();
        }

        // The settings the engine itself has to be told about. The others are read where they are used.
        static void Apply()
        {
            QualitySettings.vSyncCount = stored.vsync ? 1 : 0;
#if !UNITY_EDITOR
            if (Screen.fullScreen != stored.fullscreen) Screen.fullScreen = stored.fullscreen;
#endif
        }
    }
}
