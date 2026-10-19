using UnityEngine;

namespace Pulse
{
    // Writes the profile to disk by itself. It is there from the moment the game starts and lives
    // through every change of scene. Whatever changed is on disk within a second, and at once when
    // the game loses the screen or is closed, so there is nothing for the player to save.
    public sealed class Autosave : MonoBehaviour
    {
        const float Every = 1f;

        float clock;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Begin()
        {
            var go = new GameObject("Autosave");
            DontDestroyOnLoad(go);
            go.AddComponent<Autosave>();
            Profile.Deferred = true;
        }

        void Update()
        {
            clock += Time.unscaledDeltaTime;
            if (clock < Every) return;
            clock = 0f;
            Profile.Flush();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) Profile.Flush();
        }

        void OnApplicationQuit() => Profile.Flush();

        void OnDestroy()
        {
            Profile.Flush();
            Profile.Deferred = false;
        }
    }
}
