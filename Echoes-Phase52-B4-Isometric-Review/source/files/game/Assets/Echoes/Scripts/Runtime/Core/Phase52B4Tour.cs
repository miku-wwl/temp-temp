using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Echoes
{
    /// <summary>
    /// Opt-in Phase 5.2-B4 evidence run ("-echoes-b4-tour" with "-echoes-tour-output folder"). AUTOMATED input through
    /// PlayerController.InputOverride on the real controller under the high fixed camera; no teleport during the recorded route. The route
    /// stands idle on the plaza, zooms, walks eight directions and ten small circles (every facing ten times), talks to Mira, walks to the workshop door and round behind the painted
    /// workshop, circles under the willow, gathers a Dawn Blossom, walks the west path to the spring and inspects it, and walks back to the
    /// plaza; every frame records the player, the camera, the caretaker's drawing and the minimap. Afterwards (teleports allowed, labelled
    /// in the stills' names) it takes the stills of the visual gate: pitch 42/48/54, the caretaker's four directions idle and walking,
    /// matched B4/B3 pairs, occlusion, dialogue, satchel and pause menu, and stages S1-S3 set by this script. Presentation evidence only; not a human
    /// play session.
    /// </summary>
    public sealed class Phase52B4Tour : MonoBehaviour
    {
        [Serializable] sealed class Sample
        {
            public float t, x, y, z, speed, runPhase, cameraYaw, cameraPitch, cameraDistance, frameMs, mapX, mapY, objectiveX, objectiveY;
            public string step, dir, shown, objective;
            public int frame;
            public bool running, mirrored, objectiveOnEdge;
        }
        [Serializable] sealed class Event { public float t; public string what; }
        [Serializable] sealed class CapturedFrame { public string file; public float t; }
        [Serializable] sealed class Record
        {
            public string input = "AUTOMATED PlayerController.InputOverride and FixedFollowCamera25D.SetZoom; the real controller, gravity, collision, interaction detector, dialogue and minimap";
            public bool teleportsDuringRoute;
            public float routeSeconds, travelledMetres;
            public int width, height;
            public bool pass;
            public string camera = "";
            public string presentation = "";
            public List<Sample> samples = new List<Sample>();
            public List<Event> events = new List<Event>();
            public List<string> failures = new List<string>();
            public string videoCapture = "None";
            public List<CapturedFrame> capturedFrames = new List<CapturedFrame>();
            public List<string> stills = new List<string>();
        }

        PlayerController player;
        FixedFollowCamera25D follow;
        CharacterVisual2D5D presenter;
        InteractionDetector detector;
        DialogueBox dialogue;
        MinimapController minimap;
        Phase52B4Pilot b4;
        Camera view;
        Phase52Pilot pilot;
        Record record;
        string folder, step = "setup";
        bool recording, captureFrames, finished;
        float began, launched, nextFrame, defaultZoom = 14f;
        Vector3 last;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Begin()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-echoes-b4-tour") < 0) return;
            var root = new GameObject("AutomatedPhase52B4Evidence");
            DontDestroyOnLoad(root);
            root.AddComponent<Phase52B4Tour>();
        }

        void Start()
        {
            launched = Time.realtimeSinceStartup;
            StartCoroutine(Run());
        }

        void Update()
        {
            if (!finished && Time.realtimeSinceStartup - launched > 360f) { Debug.LogError("[Echoes][P52 B4 TOUR] TIMEOUT"); Application.Quit(1); }
        }

        void LateUpdate()
        {
            if (!recording) return;
            Vector3 p = player.transform.position;
            float d = Vector3.ProjectOnPlane(p - last, Vector3.up).magnitude;
            last = p;
            record.travelledMetres += d;
            var s = new Sample
            {
                t = Time.realtimeSinceStartup - began, x = p.x, y = p.y, z = p.z, speed = d / Mathf.Max(0.0001f, Time.deltaTime), frameMs = Time.unscaledDeltaTime * 1000f, step = step,
                cameraYaw = follow != null ? follow.Yaw : 0f, cameraPitch = follow != null ? follow.Pitch : 0f, cameraDistance = Vector3.Distance(view.transform.position, p),
            };
            if (presenter != null && presenter.isActiveAndEnabled)
            {
                s.dir = presenter.Direction; s.shown = presenter.ShownDirection; s.frame = presenter.FrameIndex; s.running = presenter.Running; s.runPhase = presenter.RunPhase; s.mirrored = presenter.Mirrored;
            }
            if (minimap != null && minimap.isActiveAndEnabled)
            {
                s.mapX = minimap.PlayerMapPoint.x; s.mapY = minimap.PlayerMapPoint.y;
                s.objective = minimap.Objective.ToString(); s.objectiveOnEdge = minimap.ObjectiveOnEdge;
                s.objectiveX = minimap.ObjectiveWindowPosition.x; s.objectiveY = minimap.ObjectiveWindowPosition.y;
            }
            record.samples.Add(s);
        }

        IEnumerator CaptureLoop()
        {
            Directory.CreateDirectory(Path.Combine(folder, "frames"));
            while (recording)
            {
                yield return new WaitForEndOfFrame();
                if (!recording) break;
                float now = Time.realtimeSinceStartup - began;
                if (now < nextFrame) continue;
                Texture2D frame = ScreenCapture.CaptureScreenshotAsTexture();
                string file = "frame-" + record.capturedFrames.Count.ToString("D5") + ".jpg";
                File.WriteAllBytes(Path.Combine(folder, "frames", file), frame.EncodeToJPG(94));
                Destroy(frame);
                record.capturedFrames.Add(new CapturedFrame { file = file, t = now });
                nextFrame = now + 1f / 30f;
            }
        }

        Vector3 Ground(Vector2 p)
        {
            var v = new Vector3(p.x, 0f, p.y);
            Terrain t = pilot != null ? pilot.Terrain : Terrain.activeTerrain;
            v.y = t.SampleHeight(v) + t.transform.position.y + 0.05f;
            return v;
        }

        void Mark(string what)
        {
            record.events.Add(new Event { t = Time.realtimeSinceStartup - began, what = what });
            Debug.Log("[Echoes][P52 B4 TOUR] " + what);
        }

        IEnumerator Run()
        {
            folder = Argument("-echoes-tour-output");
            if (string.IsNullOrEmpty(folder)) throw new IOException("Explicit fresh tour output required.");
            Directory.CreateDirectory(folder);
            if (File.Exists(Path.Combine(folder, "tour.json"))) throw new IOException("Tour output already exists.");
            yield return null;
            var menu = FindFirstObjectByType<MainMenuController>();
            if (menu != null) menu.PlayButton.onClick.Invoke();
            while ((player = FindFirstObjectByType<PlayerController>()) == null) yield return null;
            yield return null;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            view = Camera.main;
            follow = FindFirstObjectByType<FixedFollowCamera25D>();
            presenter = player.GetComponentInChildren<CharacterVisual2D5D>(true);
            detector = FindFirstObjectByType<InteractionDetector>();
            dialogue = FindFirstObjectByType<DialogueBox>();
            pilot = FindFirstObjectByType<Phase52Pilot>();
            b4 = FindFirstObjectByType<Phase52B4Pilot>();
            minimap = FindFirstObjectByType<MinimapController>(FindObjectsInactive.Include);
            if (follow != null) defaultZoom = follow.Distance;
            record = new Record { width = Screen.width, height = Screen.height };
            record.presentation = b4 == null ? "no B4 pilot in scene" : b4.BeforeMode ? "B3 presentation in the B4 scene (before mode)" : "B4 high-angle static 2.5D (painted props, B4 caretaker drawings, minimap)";
            record.camera = follow == null ? "none" : "FixedFollowCamera25D yaw=" + follow.Yaw + " pitch=" + follow.Pitch + " distance=" + follow.Distance + " (zoom " + follow.MinDistance + "-" + follow.MaxDistance + ") fov=" + view.fieldOfView;
            captureFrames = Array.IndexOf(Environment.GetCommandLineArgs(), "-echoes-b4-record-frames") >= 0;
            if (captureFrames) record.videoCapture = "Actual ScreenCapture.CaptureScreenshotAsTexture at WaitForEndOfFrame; original monotonic timestamps; no desktop capture or concept compositing";

            player.Teleport(Ground(new Vector2(6.6f, -2.2f)));
            player.transform.rotation = Quaternion.Euler(0f, (follow != null ? follow.Yaw : 0f) + 180f, 0f);
            if (follow != null) follow.Snap();
            yield return new WaitForSecondsRealtime(0.8f);
            FindFirstObjectByType<HudController>().HideBanner();
            File.WriteAllText(Path.Combine(folder, "ready.flag"), "Actual Windows player ready; the recorded route begins after three seconds.\n");
            Debug.Log("[Echoes][P52 B4 TOUR] READY; AUTOMATED_INPUT; " + record.presentation + "; " + record.camera);
            yield return new WaitForSecondsRealtime(3f);

            began = Time.realtimeSinceStartup;
            last = player.transform.position;
            recording = true;
            if (captureFrames) StartCoroutine(CaptureLoop());
            yield return Route();
            player.InputOverride = Vector2.zero;
            record.routeSeconds = Time.realtimeSinceStartup - began;
            yield return new WaitForSecondsRealtime(0.1f);
            recording = false;
            record.pass = record.failures.Count == 0 && record.travelledMetres > 90f && record.routeSeconds >= 30f;
            Debug.Log("[Echoes][P52 B4 TOUR] ROUTE " + (record.pass ? "PASS" : "FAIL") + " metres=" + record.travelledMetres.ToString("F1") + " seconds=" + record.routeSeconds.ToString("F1") + " failures=" + string.Join(" | ", record.failures));
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Stills();
            File.WriteAllText(Path.Combine(folder, "tour.json"), JsonUtility.ToJson(record, true));
            finished = true;
            bool pass = record.pass && record.failures.Count == 0;
            File.WriteAllText(Path.Combine(folder, "done.flag"), pass ? "PASS" : "FAIL");
            yield return new WaitForSecondsRealtime(1f);
            Application.Quit(pass ? 0 : 1);
        }

        IEnumerator Route()
        {
            step = "idle"; Mark("idle start on the plaza");
            yield return new WaitForSecondsRealtime(3.0f);
            step = "zoom";
            if (follow != null)
            {
                follow.SetZoom(follow.MinDistance); Mark("zoom in to " + follow.MinDistance); yield return new WaitForSecondsRealtime(2.0f);
                follow.SetZoom(follow.MaxDistance); Mark("zoom out to " + follow.MaxDistance); yield return new WaitForSecondsRealtime(2.0f);
                follow.SetZoom(defaultZoom); Mark("zoom back to " + defaultZoom); yield return new WaitForSecondsRealtime(1.2f);
            }
            step = "directions"; Mark("eight directions (W, W+D, D, S+D, S, S+A, A, W+A)");
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f * Mathf.Deg2Rad;
                player.InputOverride = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
                yield return new WaitForSecondsRealtime(0.6f);
            }
            yield return Stop("stop after the directions", 0.9f);
            step = "ten-turns";
            Vector2 centre = new Vector2(8.0f, -1.0f);   // open cobbles east of the dais, clear of the pillars
            var turns = new List<Vector2> { centre + new Vector2(1.6f, 0f) };
            for (int lap = 0; lap < 10; lap++)
                for (int i = 1; i <= 8; i++) { float a = i * Mathf.PI / 4f; turns.Add(centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 1.6f); }
            yield return Walk("ten full turns (small circles on the plaza, every facing ten times)", turns.ToArray());
            yield return Stop("stop after the ten turns", 0.9f);
            step = "to-mira";
            yield return Walk("walk to Mira", new Vector2(12.9f, 3.6f));
            yield return Stop("stop at Mira", 0.7f);
            yield return Use("talk to Mira", FindFirstObjectByType<NpcInteractable>(), true);
            step = "to-workshop";
            yield return Walk("walk to the workshop door", new Vector2(16.5f, 5.0f), new Vector2(20.6f, 7.2f));
            yield return Stop("stop at the workshop door", 1.2f);
            step = "behind-workshop";
            yield return Walk("walk round behind the painted workshop", new Vector2(21.6f, 3.4f), new Vector2(26.6f, 2.0f), new Vector2(29.4f, 5.4f), new Vector2(28.8f, 10.4f), new Vector2(26.6f, 12.6f));
            yield return Stop("stop behind the workshop (occluded)", 1.5f);
            yield return Walk("walk along the workshop's back", new Vector2(23.4f, 13.6f), new Vector2(20.4f, 12.4f));
            step = "under-willow";
            Vector2 willow = new Vector2(14.5f, 11.0f);
            var circle = new List<Vector2> { new Vector2(18.2f, 9.6f) };
            for (int i = 0; i <= 8; i++) { float a = -0.35f + i * Mathf.PI / 4.2f; circle.Add(willow + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.3f); }
            yield return Walk("circle under the willow canopy", circle.ToArray());
            step = "gather";
            ResourceNode flower = Nearest(new Vector2(10f, 10.5f));
            yield return Walk("walk to the Dawn Blossom", new Vector2(flower.Position.x + 1.1f, flower.Position.z - 0.6f));
            yield return Stop("stop at the Dawn Blossom", 0.5f);
            yield return Gather(flower);
            step = "to-spring";
            yield return Walk("walk the west path to the spring", new Vector2(4.0f, 9.4f), new Vector2(-2.0f, 8.6f), new Vector2(-8.0f, 5.6f), new Vector2(-14.0f, 11.0f), new Vector2(-18.2f, 13.4f));
            var springPoint = FindFirstObjectByType<SpringInteractable>();
            yield return Approach("step up to the spring until it is offered", springPoint);
            yield return Stop("stop at the spring", 1.2f);
            yield return Use("inspect the spring", springPoint, false);
            step = "back-to-plaza";
            yield return Walk("walk back to the plaza", new Vector2(-14.0f, 11.0f), new Vector2(-8.0f, 5.6f), new Vector2(-6.4f, 2.4f));
            yield return Stop("stop on the plaza", 2.5f);
        }

        IEnumerator Walk(string label, params Vector2[] points)
        {
            Mark(label);
            foreach (Vector2 target in points)
            {
                float started = Time.realtimeSinceStartup;
                while (true)
                {
                    Vector3 p = player.transform.position;
                    Vector2 delta = target - new Vector2(p.x, p.z);
                    if (delta.magnitude < 0.45f) break;
                    if (Time.realtimeSinceStartup - started > 9f) { record.failures.Add(label + ": did not reach " + target + " (stuck at " + p.ToString("F1") + ")"); break; }
                    Vector3 wanted = new Vector3(delta.x, 0f, delta.y).normalized;
                    Vector3 forward = Vector3.ProjectOnPlane(view.transform.forward, Vector3.up).normalized, right = Vector3.ProjectOnPlane(view.transform.right, Vector3.up).normalized;
                    player.InputOverride = new Vector2(Vector3.Dot(wanted, right), Vector3.Dot(wanted, forward));
                    yield return null;
                }
            }
        }

        /// <summary>Walks straight at an interactable until the real detector offers it (its range, not a guessed stop point, decides).</summary>
        IEnumerator Approach(string label, Interactable target)
        {
            Mark(label);
            float started = Time.realtimeSinceStartup;
            while (!ReferenceEquals(detector.Current, target))
            {
                if (Time.realtimeSinceStartup - started > 6f) { record.failures.Add(label + ": never offered (stopped at " + player.transform.position.ToString("F1") + ")"); break; }
                Vector3 wanted = Vector3.ProjectOnPlane(target.Position - player.transform.position, Vector3.up).normalized;
                Vector3 forward = Vector3.ProjectOnPlane(view.transform.forward, Vector3.up).normalized, right = Vector3.ProjectOnPlane(view.transform.right, Vector3.up).normalized;
                player.InputOverride = new Vector2(Vector3.Dot(wanted, right), Vector3.Dot(wanted, forward));
                yield return null;
            }
            player.InputOverride = Vector2.zero;
        }

        IEnumerator Stop(string label, float seconds)
        {
            Mark(label);
            step = "stop";
            player.InputOverride = Vector2.zero;
            yield return new WaitForSecondsRealtime(seconds);
        }

        /// <summary>Interacts with a target through the real detector; a dialogue it opens is read (the first two lines slowly) and closed.</summary>
        IEnumerator Use(string label, Interactable target, bool dialogueRequired)
        {
            step = "interact";
            float wait = 0f;
            while (!ReferenceEquals(detector.Current, target) && wait < 1f) { wait += Time.unscaledDeltaTime; yield return null; }
            if (!ReferenceEquals(detector.Current, target) || !detector.TryInteract()) { record.failures.Add(label + ": not in range or the interaction failed"); yield break; }
            if (!dialogue.IsOpen)
            {
                if (dialogueRequired) record.failures.Add(label + ": interaction did not open the dialogue");
                Mark(label + ": done (no dialogue)");
                yield return new WaitForSecondsRealtime(1.0f);
                yield break;
            }
            Mark(label + ": dialogue open (" + dialogue.Speaker + ")");
            int shown = 0;
            while (dialogue.IsOpen)
            {
                yield return new WaitForSecondsRealtime(shown < 2 ? 2.4f : 0.05f);
                shown++;
                dialogue.Advance();
            }
            yield return null;   // the minimap refreshes its objective in LateUpdate
            Mark(label + ": dialogue closed after " + shown + " lines; objective now " + (minimap != null ? minimap.Objective.ToString() : "?"));
            yield return new WaitForSecondsRealtime(0.4f);
        }

        IEnumerator Gather(ResourceNode node)
        {
            step = "gather";
            var session = GameSession.Current;
            int before = session.Inventory.Count(node.ItemId);
            float wait = 0f;
            while (!ReferenceEquals(detector.Current, node) && wait < 1f) { wait += Time.unscaledDeltaTime; yield return null; }
            bool ok = ReferenceEquals(detector.Current, node) && detector.TryInteract();
            int after = session.Inventory.Count(node.ItemId);
            if (!ok || after != before + 1) record.failures.Add("gather " + node.NodeId + ": interaction " + ok + ", count " + before + " -> " + after);
            Mark("gathered " + node.NodeId + " (" + before + " -> " + after + ")");
            yield return new WaitForSecondsRealtime(1.0f);
        }

        static ResourceNode Nearest(Vector2 xz)
        {
            ResourceNode best = null;
            float bestDistance = float.MaxValue;
            foreach (ResourceNode n in FindObjectsByType<ResourceNode>(FindObjectsSortMode.None))
            {
                float d = Vector2.Distance(new Vector2(n.Position.x, n.Position.z), xz);
                if (n.IsReady && d < bestDistance) { bestDistance = d; best = n; }
            }
            return best;
        }

        // ---- stills (after the recorded route; teleports set them up) ---------------------------------------------------------------

        static readonly (string name, Vector2 xz, float faceYaw)[] Spots =
        {
            ("workshop-front", new Vector2(20.4f, 7.4f), 90f),
            ("workshop-side", new Vector2(25.6f, 3.6f), 0f),
            ("plaza", new Vector2(4.0f, -3.5f), 225f),
            ("willow", new Vector2(12.6f, 8.6f), 45f),
            ("spring", new Vector2(-18.4f, 12.6f), 300f),
        };

        IEnumerator Stills()
        {
            step = "stills";
            HudController hud = FindFirstObjectByType<HudController>();
            if (b4 != null && b4.BeforeMode) b4.Apply(false);
            float cy = follow != null ? follow.Yaw : 0f;
            Phase52B4Pilot.CameraView v4 = b4 != null ? b4.B4View : default;

            // the three candidate pitches at the plaza and the workshop (the default is restored afterwards)
            foreach (float pitch in new[] { 42f, 48f, 54f })
            {
                if (follow != null && b4 != null) follow.Configure(v4.yaw, pitch, v4.distance, v4.minDistance, v4.maxDistance, v4.fieldOfView);
                foreach (var s in Spots.Where(s => s.name == "plaza" || s.name == "workshop-front"))
                {
                    yield return Place(Ground(s.xz), s.faceYaw, defaultZoom);
                    yield return Shot("pitch-" + pitch.ToString("F0") + "-" + s.name);
                }
            }
            if (b4 != null) b4.Apply(false);

            // the caretaker: four directions idle at the closest and the default zoom, then walking
            Vector3 spot = Ground(new Vector2(6.6f, -2.2f));
            var directions = new (string name, float yaw, Vector2 input)[] { ("S", cy + 180f, new Vector2(0f, -1f)), ("W", cy - 90f, new Vector2(-1f, 0f)), ("E", cy + 90f, new Vector2(1f, 0f)), ("N", cy, new Vector2(0f, 1f)) };
            foreach (var d in directions)
            {
                yield return Place(spot, d.yaw, follow != null ? follow.MinDistance : 10f);
                hud.HideBanner();
                yield return Shot("hero-idle-" + d.name + "-closest-zoom");
                yield return Place(spot, d.yaw, defaultZoom);
                yield return Shot("hero-idle-" + d.name + "-default-zoom");
            }
            foreach (var d in directions)
            {
                yield return Place(spot, d.yaw, follow != null ? follow.MinDistance : 10f);
                player.InputOverride = d.input;
                yield return new WaitForSecondsRealtime(0.55f);
                yield return Shot("hero-walk-" + d.name + "-closest-zoom");
                player.InputOverride = Vector2.zero;
            }

            // matched B4 / B3 pairs: the same build, the same scene, the same place; only the presentation switches
            foreach (var s in Spots)
            {
                yield return Place(Ground(s.xz), s.faceYaw, defaultZoom);
                yield return Shot("after-b4-" + s.name);
                if (b4 == null) continue;
                b4.Apply(true);
                yield return Place(Ground(s.xz), s.faceYaw, follow != null ? follow.Distance : 8f);
                yield return Shot("before-b3-" + s.name);
                b4.Apply(false);
            }

            // occlusion: behind the painted workshop and under the willow's canopy
            yield return Place(Ground(new Vector2(27.2f, 11.8f)), 225f, defaultZoom);
            yield return Shot("occlusion-behind-workshop");
            yield return Place(Ground(new Vector2(15.6f, 12.6f)), 225f, defaultZoom);
            yield return Shot("occlusion-behind-willow");

            // dialogue and satchel with the minimap on screen
            var mira = FindFirstObjectByType<NpcInteractable>();
            Vector3 front = mira.Position + new Vector3(0f, 0.3f, -1.6f);
            yield return Place(Ground(new Vector2(front.x, front.z)), 0f, defaultZoom);
            if (ReferenceEquals(detector.Current, mira) && detector.TryInteract() && dialogue.IsOpen)
            {
                yield return new WaitForSecondsRealtime(0.4f);
                yield return Shot("dialogue-mira");
                while (dialogue.IsOpen) dialogue.Advance();
            }
            else record.failures.Add("stills: Mira's dialogue did not open");
            var satchel = FindFirstObjectByType<InventoryPanel>();
            yield return Place(spot, cy + 180f, defaultZoom);
            satchel.Open();
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("inventory-portrait");
            satchel.Close();
            var pause = FindFirstObjectByType<PauseController>();
            pause.SetPaused(true);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("pause-menu");
            pause.SetPaused(false);
            yield return new WaitForSecondsRealtime(0.3f);

            // the later stages, SET BY THIS SCRIPT (not reached by play; the isolated evidence save folder is the only one touched), at the
            // places of the after-b4 stills (stage S0): the tone and the world must stay readable at every stage
            foreach (var stage in new[] { (ResonanceStage.AwakeningStream, "S1"), (ResonanceStage.BloomingGarden, "S2"), (ResonanceStage.RestoredValley, "S3") })
            {
                yield return ShowStage(stage.Item1);
                foreach (int i in new[] { 0, 2, 4 })
                {
                    yield return Place(Ground(Spots[i].xz), Spots[i].faceYaw, defaultZoom);
                    yield return Shot("stage-" + stage.Item2 + "-set-by-script-" + Spots[i].name);
                }
            }
        }

        IEnumerator ShowStage(ResonanceStage stage)
        {
            GameSession s = GameSession.Current;
            s.RestoreCore(s.Coins, s.Inventory.Snapshot(), s.Chicken, s.ChickenRemaining, s.ExplorationStep, s.StreamQuest, s.GardenQuest, s.WindmillQuest,
                s.EndingSeen, stage, s.NodeTimers().ToDictionary(kv => kv.Key, kv => kv.Value));
            Mark("stage set by the evidence script: " + stage);
            var resonance = FindFirstObjectByType<ResonanceView>();
            float waited = 0f;
            yield return new WaitForSecondsRealtime(0.5f);
            while (resonance != null && (resonance.IsTransitioning || resonance.ShownStage != stage) && waited < 12f) { waited += 0.25f; yield return new WaitForSecondsRealtime(0.25f); }
            if (resonance != null && resonance.ShownStage != stage) record.failures.Add("stills: the world did not reach " + stage);
            yield return new WaitForSecondsRealtime(1.5f);
        }

        IEnumerator Place(Vector3 at, float yaw, float zoom)
        {
            player.Teleport(at);
            player.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (follow != null) { follow.SetZoom(zoom); follow.Snap(); }
            yield return new WaitForSecondsRealtime(0.6f);
            if (follow != null) follow.Snap();
            yield return null;
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
            record.stills.Add(name);
            yield return new WaitForSecondsRealtime(0.3f);
        }

        static string Argument(string name) { string[] args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
    }
}
