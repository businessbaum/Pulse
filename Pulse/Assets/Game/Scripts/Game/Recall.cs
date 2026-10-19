using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Pulse
{
    // The way home. The machine carries a teleporter that takes a few seconds to charge. While it
    // charges, light is drawn into the machine and a beam builds over it. Any damage breaks the
    // charge, so it has to be started somewhere quiet. When it is full, the machine is pulled up
    // the beam and comes out on the turntable of the Roundhouse. This is how Beats get home: what
    // the machine carries becomes the player's when it arrives, and is lost if it is destroyed.
    //
    // R or the Y button starts it; pressing again stops it. The pause menu starts it too.
    public sealed class Recall : MonoBehaviour
    {
        // How long it charges, how long it stays dead after being broken, how long the note about that shows.
        const float ChargeTime = 4f, DeadTime = 1.5f, NoteTime = 1.1f;
        // Leaving: the machine is drawn out to a thin line and pulled upward while the dark closes in.
        const float LiftTime = 0.55f, LiftHeight = 9f, BeamHeight = 26f;
        static readonly Vector3 Stretched = new Vector3(0.12f, 7f, 0.12f);

        float lastHull, dead, note, owed;

        // From 0 to 1 while it charges.
        public float Progress { get; private set; }
        public bool Charging { get; private set; }
        // The charge is full and the machine is on its way.
        public bool Leaving { get; private set; }
        // True for a moment after a hit broke the charge.
        public bool Broken => note > 0f;

        bool Ready
        {
            get
            {
                var rig = Combat.Rig;
                return rig != null && rig.Alive && !rig.Scripted && !rig.Launching && !Combat.Director.Busy;
            }
        }

        // Starts charging. Returns false when it cannot right now.
        public bool Begin()
        {
            if (Charging || Leaving || dead > 0f || !Ready) return false;
            Charging = true;
            Progress = 0f;
            lastHull = Combat.Rig.Hull;
            return true;
        }

        public void Cancel()
        {
            Charging = false;
            Progress = 0f;
        }

        static bool Asked()
        {
            var keys = Keyboard.current;
            var pad = Gamepad.current;
            return (keys != null && keys.rKey.wasPressedThisFrame) || (pad != null && pad.buttonNorth.wasPressedThisFrame);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            dead -= dt;
            note -= Time.unscaledDeltaTime;
            if (Leaving || dt <= 0f) return;
            if (Asked())
            {
                if (Charging) Cancel();
                else Begin();
            }
            if (!Charging) return;

            var rig = Combat.Rig;
            if (!Ready) { Cancel(); return; }
            // Any hull lost since the last frame breaks it. Repairs do not.
            if (rig.Hull < lastHull - 0.01f) { Break(rig); return; }
            lastHull = rig.Hull;

            Progress += dt / ChargeTime;
            Charge(rig.transform.position, Progress, dt);
            if (Progress >= 1f) StartCoroutine(Leave());
        }

        // What charging looks like: light drawn into the machine from all round, streaks rising
        // around it, and a beam over it that grows wider and brighter. All of it gets stronger.
        void Charge(Vector3 pos, float charge, float dt)
        {
            owed += dt * (40f + 150f * charge);
            int count = (int)owed;
            owed -= count;
            if (count > 0)
            {
                Combat.Vfx.Gather(pos + Vector3.up * 0.9f, Mathf.Lerp(6f, 3.2f, charge), Tokens.PulseGreen, Vfx.Share(count * 0.5f), 0.4f);
                Combat.Vfx.Rush(pos + Vector3.up * 2.4f, new Vector3(1.6f, 2.4f, 1.6f), Vector3.up * (5f + 16f * charge), Tokens.PulseGreen,
                    count, 1.2f + charge, 0.3f);
            }
            var beam = Color.Lerp(Tokens.PulseGreen, Color.white, 0.5f * charge);
            beam.a = 0.25f + 0.6f * charge;
            Combat.Fx.Line(pos + Vector3.up * 1.2f, pos + Vector3.up * BeamHeight, beam, 0.05f, 0.05f + 0.5f * charge * charge);
            if (Combat.Camera != null) Combat.Camera.Shake(0.015f + 0.05f * charge);
        }

        // A hit: the charge spills out of the machine and the teleporter needs a moment.
        void Break(Rig rig)
        {
            Cancel();
            dead = DeadTime;
            note = NoteTime;
            Vector3 pos = rig.transform.position + Vector3.up;
            Combat.Vfx.Burst(pos, Tokens.PulseGreen, 26, 8f, 0.2f, 0.7f, falls: true);
            Combat.Vfx.Sparks(pos, Tokens.Chalk, 16, 10f, 0.5f, 0.3f);
            if (Combat.Camera != null) Combat.Camera.Shake(0.25f);
        }

        // The charge is full. The machine is drawn out into a line of light and pulled up the beam,
        // and the dark closes in on the place where it stood. The Roundhouse opens from that dark.
        IEnumerator Leave()
        {
            Leaving = true;
            Charging = false;
            var rig = Combat.Rig;
            var camera = Combat.Camera;
            rig.Scripted = true;
            Vector3 pos = rig.transform.position;
            if (camera != null) camera.pin = 1f;
            Combat.Vfx.Shockwave(pos, 6f, Tokens.PulseGreen, 50, 0.4f);
            Combat.Vfx.Sparks(pos + Vector3.up, Tokens.PulseGreen, 40, 14f, 0.9f, 0.4f);

            for (float t = 0f; t < LiftTime; t += Time.deltaTime)
            {
                float u = t / LiftTime, pull = u * u;
                rig.Visuals.localScale = Vector3.Lerp(Vector3.one, Stretched, pull);
                rig.Visuals.localPosition = Vector3.up * (pull * LiftHeight);
                Look.Exposure = Mathf.Sin(u * Mathf.PI) * 0.9f;
                var beam = Color.white;
                beam.a = 1f - 0.5f * u;
                Combat.Fx.Line(pos + Vector3.up * 0.2f, pos + Vector3.up * BeamHeight, beam, 0.05f, Mathf.Lerp(1.2f, 0.1f, u));
                Combat.Vfx.Rush(pos + Vector3.up * 5f, new Vector3(0.8f, 5f, 0.8f), Vector3.up * 30f, Tokens.PulseGreen, 4, 3f, 0.25f);
                if (camera != null) camera.iris = Mathf.Lerp(CameraRig.IrisWide, 0f, Mathf.SmoothStep(0f, 1f, u));
                yield return null;
            }
            if (camera != null) camera.iris = 0f;
            Look.Exposure = 0f;
            yield return null;

            Time.timeScale = 1f;
            // What it carried is the player's now.
            RunSetup.Brought = RunStats.Beats;
            Profile.Earn(RunStats.Beats);
            RunStats.Beats = 0;
            RunSetup.Returning = true;
            if (Application.CanStreamedLevelBeLoaded("Lobby")) SceneManager.LoadScene("Lobby");
            else SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
