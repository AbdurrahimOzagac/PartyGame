#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using ShooterParty.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace ShooterParty.Tests
{
    // The local player's controls, driven through virtual devices: camera-relative WASD, hold-to-fire at the
    // cooldown, nothing while the mouse is free, and the click that closed a menu never spits. And when a real
    // Practice session lets the game take the mouse.
    public class ControlsTests
    {
        InputSettings savedSettings, testSettings;
        readonly System.Collections.Generic.List<InputDevice> realDevices = new System.Collections.Generic.List<InputDevice>();
        Keyboard kb;
        Mouse mouse;
        GameObject floor, critter, camGo;
        readonly System.Collections.Generic.HashSet<int> blobs = new System.Collections.Generic.HashSet<int>();

        [SetUp]
        public void SetUp()
        {
            savedSettings = InputSystem.settings;
            testSettings = ScriptableObject.CreateInstance<InputSettings>();   // batchmode has no focused Game view
            testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings = testSettings;
            realDevices.Clear();
            blobs.Clear();
            foreach (var d in InputSystem.devices) if (d.enabled) realDevices.Add(d);
            foreach (var d in realDevices) InputSystem.DisableDevice(d);   // a real mouse moving mid-test would become Mouse.current
            kb = InputSystem.AddDevice<Keyboard>(); kb.MakeCurrent();
            mouse = InputSystem.AddDevice<Mouse>(); mouse.MakeCurrent();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            InputSystem.RemoveDevice(kb);
            InputSystem.RemoveDevice(mouse);
            foreach (var d in realDevices) if (d.added) InputSystem.EnableDevice(d);
            InputSystem.settings = savedSettings;
            Object.Destroy(testSettings);
            foreach (var go in new[] { floor, critter, camGo }) if (go) Object.Destroy(go);
            SessionFlow.TimeScaleForTests = 1f;
            yield return NetTestRig.Teardown();
        }

        void Keys(params Key[] keys) => InputSystem.QueueStateEvent(kb, new KeyboardState(keys));
        void Click(bool down) => InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left, down));

        // Spits that left the mouth: every blob lives through its flight, so each one is seen at least once.
        IEnumerator CountShots(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                yield return null;
                foreach (var b in Object.FindObjectsByType<SpitBlob>(FindObjectsSortMode.None)) blobs.Add(b.GetInstanceID());
            }
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        [UnityTest]
        public IEnumerator WasdFollowsTheCameraAndHoldingSpitKeepsFiring()
        {
            var at = new Vector3(0f, 0f, -60f);
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = at + Vector3.down * 0.5f; floor.transform.localScale = new Vector3(40f, 1f, 40f);
            critter = Object.Instantiate(NetTestRig.Load(NetTestRig.OfflineCritterPrefab), at + Vector3.up * 0.05f, Quaternion.identity);
            camGo = new GameObject("Cam", typeof(Camera));
            var cam = camGo.AddComponent<OverShoulderCamera>();
            cam.SetTarget(critter.transform);   // looks along +Z
            cam.allowCursorLock = true;
            var input = critter.AddComponent<LocalCritterInput>();
            input.cam = cam;
            var motor = critter.GetComponent<CritterMotor>();
            yield return new WaitForSeconds(0.2f);
            motor.FaceTowards(Vector3.right, true);   // the critter faces +X, the camera still looks along +Z
            yield return new WaitForFixedUpdate();

            var start = critter.transform.position;
            Keys(Key.W);
            yield return new WaitForSeconds(0.5f);
            var moved = Flat(critter.transform.position - start);
            Assert.Greater(moved.z, 0.5f, "W walks where the camera looks");
            Assert.Less(Mathf.Abs(moved.x), 0.15f, "not where the critter faced");
            start = critter.transform.position;
            Keys(Key.D);
            yield return new WaitForSeconds(0.4f);
            Keys();
            yield return new WaitForSeconds(0.4f);
            moved = Flat(critter.transform.position - start);
            Assert.Greater(moved.x, 0.4f, "D strafes camera-right");
            Assert.Greater(Vector3.Dot(critter.transform.forward, Vector3.right), 0.9f, "it turned to its movement");

            // a menu is open: holding keys and the button does nothing
            cam.allowCursorLock = false;
            start = critter.transform.position;
            Keys(Key.W);
            Click(true);
            yield return CountShots(0.3f);
            Assert.AreEqual(0, blobs.Count, "no spit while the mouse is free");
            Assert.Less(Flat(critter.transform.position - start).magnitude, 0.05f, "no walking while the mouse is free");
            Keys();
            // the menu closes with that click still down: it never spits
            cam.allowCursorLock = true;
            yield return CountShots(0.3f);
            Assert.AreEqual(0, blobs.Count, "the click that closed the menu never spits");
            Click(false);
            yield return null;
            Click(true);
            yield return CountShots(1.05f);
            Click(false);
            Assert.GreaterOrEqual(blobs.Count, 3, "holding spits every cooldown (1/3 s)");
            Assert.LessOrEqual(blobs.Count, 4, "and never faster");
            Assert.Greater(Vector3.Dot(critter.transform.forward, Vector3.forward), 0.9f, "it turned to the crosshair");
        }

        static IEnumerator WaitFor(System.Func<bool> done, float timeout, string what)
        {
            for (float t = 0f; t < timeout && !done(); t += Time.unscaledDeltaTime) yield return null;
            Assert.IsTrue(done(), "timed out waiting for " + what);
        }

        [UnityTest]
        public IEnumerator PracticeRoundTakesTheMouseOnlyForPlay()
        {
            SessionFlow.TimeScaleForTests = 0.25f;
            yield return NetTestRig.BootToPracticeLobby();
            var ui = UiRoot.Instance;
            var s = SessionManager.Instance;
            Assert.IsFalse(OverShoulderCamera.Main && OverShoulderCamera.Main.allowCursorLock, "the lobby keeps the cursor");
            s.RequestPlaylist("Test", new[] { "knockback_arena" });
            yield return WaitFor(() => s.PlaylistIds.Count == 1 && s.PlaylistName.Value == "Test", 3f, "the playlist");
            s.RequestStart();
            yield return WaitFor(() => s.Phase.Value == SessionPhase.Intro && OverShoulderCamera.Main, 15f, "the intro");
            yield return null;
            var cam = OverShoulderCamera.Main;
            Assert.IsFalse(cam.allowCursorLock, "the intro card keeps the cursor");
            yield return WaitFor(() => s.Phase.Value == SessionPhase.Playing, 20f, "the round");
            yield return null;
            Assert.IsTrue(cam.allowCursorLock, "play takes the mouse without a click");
            ui.pause.Show();
            yield return null;
            Assert.IsFalse(cam.allowCursorLock, "pause frees it");
            ui.pause.Hide();
            yield return null;
            Assert.IsTrue(cam.allowCursorLock, "resume takes it back");
            ui.SendMessage("OnApplicationFocus", false);
            yield return null;
            Assert.IsFalse(cam.allowCursorLock, "losing focus frees it");
            ui.SendMessage("OnApplicationFocus", true);
            yield return null;
            Assert.IsTrue(cam.allowCursorLock, "focus back takes it again");
        }
    }
}
#endif
