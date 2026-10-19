using UnityEngine;

namespace Pulse
{
    // The mouse pointer. It is only there while there is something to click: a menu or a screen.
    // While the machine drives or the character walks it would only lie on the picture.
    public static class Pointer
    {
        public static bool Shown { get; private set; } = true;

        // Every start of the game begins with the pointer, and leaving the game gives it back.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Begin()
        {
            Show(true);
            Application.quitting -= Restore;
            Application.quitting += Restore;
        }

        static void Restore() => Show(true);

        public static void Show(bool shown)
        {
            Shown = shown;
            if (Cursor.visible != shown) Cursor.visible = shown;
        }
    }
}
