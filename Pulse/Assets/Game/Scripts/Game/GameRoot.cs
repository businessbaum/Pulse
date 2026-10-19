using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Pulse
{
    // Starts a run: creates the systems, the machine, the run director and the HUD.
    // The machine is the one the player chose in the lobby. It cannot be changed during a run.
    [RequireComponent(typeof(UIDocument))]
    public sealed class GameRoot : MonoBehaviour
    {
        // Arriving from the lobby: the machine starts ArriveDistance south of the starting point at
        // ArriveSpeed, the speed it left the lobby with, and brakes with ArriveBrake, so it is down to
        // its own top speed as it gets there. The dark opens around it within OpenTime.
        public const float ArriveSpeed = 42f;
        const float ArriveDistance = 30f, ArriveBrake = 28.5f, OpenTime = 0.9f;
        // A destroyed machine: the wreck is shown in slow motion for a moment before the end screen.
        const float SlowMotion = 0.3f, WreckSeconds = 1.1f;

        public GameConfig config;
        // Where the veins and caches of the next run stand. Tests set it; otherwise every run differs.
        public static int? FindSeed;

        Hud hud;
        float wreckClock;
        bool over;

        void Awake()
        {
            Time.timeScale = 1f;
            Rig.MoveOverride = null;
            Rig.BoostOverride = null;
            Profile.Load(config);
            Combat.Reset();
            Combat.Config = config;
            Combat.Enemies = gameObject.AddComponent<EnemySystem>();
            Combat.Zones = gameObject.AddComponent<ZoneSystem>();
            Combat.Projectiles = gameObject.AddComponent<ProjectileSystem>();
            Combat.Pickups = gameObject.AddComponent<PickupSystem>();
            Combat.Fx = gameObject.AddComponent<Fx>();
            Combat.Vfx = gameObject.AddComponent<Vfx>();
            Combat.Marks = gameObject.AddComponent<Marks>();
            Combat.Scenery = gameObject.AddComponent<Scenery>();
            Combat.Scenery.BuildWorld(config.world, config);
            Combat.Storm = gameObject.AddComponent<Storm>();
            Combat.Storm.Build(config.world);
            Combat.Finds = gameObject.AddComponent<Finds>();
            Combat.Finds.Build(config.world, config, FindSeed ?? System.Environment.TickCount);
            if (Camera.main != null)
            {
                Look.Apply(Camera.main);
                Combat.Camera = Camera.main.GetComponent<CameraRig>();
                if (Combat.Camera != null) Combat.Camera.offset = CameraRig.RunOffset;
            }

            Combat.Rig = new GameObject("Rig").AddComponent<Rig>();
            Combat.Rig.Build(Profile.Machine, config, refill: true);
            Combat.Director = gameObject.AddComponent<RunDirector>();
            Combat.Recall = gameObject.AddComponent<Recall>();
        }

        void Start()
        {
            hud = new Hud(GetComponent<UIDocument>().rootVisualElement, ToLobby, GoHome);
            if (Combat.Camera != null) Combat.Camera.Snap();
            if (RunSetup.Arriving) StartCoroutine(Arrive());
            RunSetup.Arriving = false;
        }

        // A run that starts through the lobby gate. The lobby ends on the machine racing north in
        // the dark, with the picture closed in around it. This starts from exactly that picture and
        // opens it: the open world appears around a machine that never left the screen.
        IEnumerator Arrive()
        {
            var rig = Combat.Rig;
            var camera = Combat.Camera;
            hud.Fade = 0f;
            rig.Scripted = true;
            rig.Teleport(Vector3.back * ArriveDistance);
            if (camera != null) { camera.iris = CameraRig.IrisTight; camera.pin = 1f; camera.Snap(); }
            // Loading leaves one very long frame behind. The race starts after it.
            yield return null;
            yield return null;
            rig.Scripted = false;
            rig.Launch(ArriveSpeed, ArriveBrake);
            // The air was already streaking past in the lobby. It still is.
            Combat.Vfx.Rush(rig.transform.position + Vector3.up * 0.9f, new Vector3(9f, 0.7f, 18f), Vector3.back * 3f, Tokens.Dust, 80, 3f, 0.4f);
            for (float t = 0f; t < OpenTime; t += Mathf.Min(Time.unscaledDeltaTime, 0.05f))
            {
                float u = t / OpenTime;
                if (camera != null)
                {
                    camera.iris = Mathf.Lerp(CameraRig.IrisTight, CameraRig.IrisWide, u * u);
                    // The camera lets go gradually, and the machine pulls ahead of it.
                    camera.pin = 1f - Mathf.SmoothStep(0f, 1f, u);
                }
                hud.Fade = Mathf.InverseLerp(0.35f, 1f, u);
                yield return null;
            }
            if (camera != null) { camera.iris = float.PositiveInfinity; camera.pin = 0f; }
            hud.Fade = 1f;
        }

        void Update()
        {
            Combat.Time += Time.deltaTime;

            if (over) hud.GameOver.Update();
            else if (!Combat.Rig.Alive)
            {
                Time.timeScale = SlowMotion;
                wreckClock += Time.unscaledDeltaTime;
                if (wreckClock >= WreckSeconds)
                {
                    over = true;
                    Time.timeScale = 0f;
                    // What this run added to the Codex is the player's, whatever became of the machine.
                    Profile.Settle();
                    hud.GameOver.Show();
                }
            }
            else if (hud.Echo.IsOpen) hud.Echo.Update();
            else if (hud.Pause.IsOpen) hud.Pause.Update();
            else if (Echoes.Owed > 0 && !Combat.Director.Busy && !Combat.Rig.Scripted) OfferEchoes();
            else if (PauseMenu.Asked()) OpenPause();

            // The pointer is only there while there is something to click.
            Pointer.Show(over || hud.Pause.IsOpen || hud.Echo.IsOpen);
            hud.Refresh();
        }

        // The machine has earned a choice of Echoes: the cards come up. With nothing left to offer,
        // it gets Beats.
        void OfferEchoes()
        {
            var offer = Echoes.Offer(config, Combat.Rig.Loadout, () => Random.value);
            if (offer.Count > 0)
            {
                hud.Echo.Open(offer, TakeEcho);
                return;
            }
            Echoes.Owed--;
            RunStats.Beats += Echoes.Spare;
            Loot.Beats(Echoes.Spare);
        }

        // The Echo that was chosen is in force from this moment. Public so a test can hand one over.
        public void TakeEcho(EchoDef echo)
        {
            Echoes.Owed = Mathf.Max(0, Echoes.Owed - 1);
            if (!Echoes.Take(echo)) return;
            Codex.Held(echo);
            RunStats.EchoCount++;
            var rig = Combat.Rig;
            if (echo.kind == EchoKind.Undertow) rig.Magnet = Echoes.MagnetReach;
            // A Long Wire changes what the systems reach, so the machine is wired anew.
            if (echo.kind == EchoKind.LongWire) rig.Build(rig.Loadout, config, refill: false);
            var color = echo.strand != StateType.None ? Tokens.Of(echo.strand) : Tokens.PulseGreen;
            Combat.Vfx.Shockwave(rig.transform.position, 6f, color, 44, 0.4f);
            Combat.Vfx.Sparks(rig.transform.position + Vector3.up, color, 36, 13f, 0.8f, 0.4f);
            Combat.Vfx.Rise(rig.transform.position, rig.Radius, 0f, color, 24, 4f, 0.16f, 0.8f);
        }

        // Opens the pause menu, which also says what the Echoes the machine holds do.
        public void OpenPause()
        {
            hud.ListEchoes();
            hud.Pause.Open(hud.Place);
        }

        // Public so a test can open and close the menu without a key press.
        public PauseMenu Pause => hud.Pause;
        public EchoScreen Echo => hud.Echo;
        public PickupLog Log => hud.Log;
        public bool Over => over;

        // The pause menu's way home: it closes and the teleporter starts to charge, as with the key.
        void GoHome()
        {
            hud.Pause.Close();
            Combat.Recall.Begin();
        }

        // Back to the Roundhouse after the machine is destroyed. Without a lobby in the build, the run restarts.
        void ToLobby()
        {
            Time.timeScale = 1f;
            if (Application.CanStreamedLevelBeLoaded("Lobby")) SceneManager.LoadScene("Lobby");
            else SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
