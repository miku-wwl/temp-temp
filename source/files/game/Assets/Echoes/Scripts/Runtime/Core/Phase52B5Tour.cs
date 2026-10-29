using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Echoes
{
    /// <summary>
    /// Opt-in Phase 5.2-B5 evidence run ("-echoes-b5-tour" with "-echoes-tour-output folder"; "-echoes-b5-record-frames" records the
    /// framebuffer). AUTOMATED input through PlayerController.InputOverride on the real controller under the high fixed camera; no teleport
    /// during the recorded route. The route idles and zooms on the plaza, walks eight directions and three small circles, talks to Mira,
    /// walks round the painted workshop and under the willow, gathers a Dawn Blossom, walks under the broadleaf by the hen yard, into the
    /// garden between the raised beds and out again, along the west path to the spring (inspected when the detector offers it), down the
    /// stream bank under the trees and back to the plaza; every frame records the player, the camera, the caretaker's drawing and the
    /// minimap. Afterwards (teleports allowed, labelled in the stills' names) it takes the stills of the visual gate: B5 and B4 (before
    /// mode, same build, same place) pairs, pitch 42/48/54, occlusion, the caretaker's four directions, dialogue, satchel, pause, and stages
    /// S1-S3 set by this script. Presentation evidence only; not a human play session.
    /// </summary>
    public sealed class Phase52B5Tour : MonoBehaviour
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
        [Serializable] sealed class Occlusion { public string still, presentation; public float zoom, visibleShare = -1f, visibleShareWithoutOpening = -1f, markedShare = -1f; public int referencePixels; }
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
            public List<Occlusion> occlusion = new List<Occlusion>();
        }

        PlayerController player;
        FixedFollowCamera25D follow;
        CharacterVisual2D5D presenter;
        InteractionDetector detector;
        DialogueBox dialogue;
        MinimapController minimap;
        Phase52B4Pilot b4;
        Phase52B5Pilot b5;
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
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-echoes-b5-tour") < 0) return;
            var root = new GameObject("AutomatedPhase52B5Evidence");
            DontDestroyOnLoad(root);
            root.AddComponent<Phase52B5Tour>();
        }

        void Start()
        {
            launched = Time.realtimeSinceStartup;
            StartCoroutine(Run());
        }

        void Update()
        {
            if (!finished && Time.realtimeSinceStartup - launched > 420f) { Debug.LogError("[Echoes][P52 B5 TOUR] TIMEOUT"); Application.Quit(1); }
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
            Debug.Log("[Echoes][P52 B5 TOUR] " + what);
        }

        string Presentation => b5 == null ? "no B5 pilot in scene" : b5.BeforeMode ? "B4 presentation in the B5 scene (before mode)" : "B5 painted environment (stage-painted spring, painted trees, garden beds, meadow and soil, minimap re-baked)";

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
            b5 = FindFirstObjectByType<Phase52B5Pilot>();
            minimap = FindFirstObjectByType<MinimapController>(FindObjectsInactive.Include);
            if (follow != null) defaultZoom = follow.Distance;
            record = new Record { width = Screen.width, height = Screen.height, presentation = Presentation };
            record.camera = follow == null ? "none" : "FixedFollowCamera25D yaw=" + follow.Yaw + " pitch=" + follow.Pitch + " distance=" + follow.Distance + " (zoom " + follow.MinDistance + "-" + follow.MaxDistance + ") fov=" + view.fieldOfView;
            captureFrames = Array.IndexOf(Environment.GetCommandLineArgs(), "-echoes-b5-record-frames") >= 0;
            if (captureFrames) record.videoCapture = "Actual ScreenCapture.CaptureScreenshotAsTexture at WaitForEndOfFrame; original monotonic timestamps; no desktop capture or concept compositing";

            player.Teleport(Ground(new Vector2(6.6f, -2.2f)));
            player.transform.rotation = Quaternion.Euler(0f, (follow != null ? follow.Yaw : 0f) + 180f, 0f);
            if (follow != null) follow.Snap();
            yield return new WaitForSecondsRealtime(0.8f);
            FindFirstObjectByType<HudController>().HideBanner();
            File.WriteAllText(Path.Combine(folder, "ready.flag"), "Actual Windows player ready; the recorded route begins after three seconds.\n");
            Debug.Log("[Echoes][P52 B5 TOUR] READY; AUTOMATED_INPUT; " + record.presentation + "; " + record.camera);
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
            record.pass = record.failures.Count == 0 && record.travelledMetres > 120f && record.routeSeconds >= 30f;
            Debug.Log("[Echoes][P52 B5 TOUR] ROUTE " + (record.pass ? "PASS" : "FAIL") + " metres=" + record.travelledMetres.ToString("F1") + " seconds=" + record.routeSeconds.ToString("F1") + " failures=" + string.Join(" | ", record.failures));
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
            yield return new WaitForSecondsRealtime(2.5f);
            step = "zoom";
            if (follow != null)
            {
                follow.SetZoom(follow.MinDistance); Mark("zoom in to " + follow.MinDistance); yield return new WaitForSecondsRealtime(1.6f);
                follow.SetZoom(follow.MaxDistance); Mark("zoom out to " + follow.MaxDistance); yield return new WaitForSecondsRealtime(1.6f);
                follow.SetZoom(defaultZoom); Mark("zoom back to " + defaultZoom); yield return new WaitForSecondsRealtime(1.0f);
            }
            step = "directions"; Mark("eight directions (W, W+D, D, S+D, S, S+A, A, W+A)");
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f * Mathf.Deg2Rad;
                player.InputOverride = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
                yield return new WaitForSecondsRealtime(0.6f);
            }
            yield return Stop("stop after the directions", 0.8f);
            step = "turns";
            Vector2 centre = new Vector2(8.0f, -1.0f);
            var turns = new List<Vector2> { centre + new Vector2(1.6f, 0f) };
            for (int lap = 0; lap < 3; lap++)
                for (int i = 1; i <= 8; i++) { float a = i * Mathf.PI / 4f; turns.Add(centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 1.6f); }
            yield return Walk("three small circles on the plaza (every facing three times)", turns.ToArray());
            yield return Stop("stop after the circles", 0.6f);
            step = "to-mira";
            yield return Walk("walk to Mira", new Vector2(12.9f, 3.6f));
            yield return Stop("stop at Mira", 0.5f);
            yield return Use("talk to Mira", FindFirstObjectByType<NpcInteractable>(), true);
            step = "to-workshop";
            yield return Walk("walk to the workshop door", new Vector2(16.5f, 5.0f), new Vector2(20.6f, 7.2f));
            yield return Stop("stop at the workshop door", 0.8f);
            step = "behind-workshop";
            yield return Walk("walk round behind the painted workshop", new Vector2(21.6f, 3.4f), new Vector2(26.6f, 2.0f), new Vector2(29.4f, 5.4f), new Vector2(28.8f, 10.4f), new Vector2(26.6f, 12.6f));
            yield return Stop("stop behind the workshop (occluded)", 1.2f);
            yield return Walk("walk along the workshop's back", new Vector2(23.4f, 13.6f), new Vector2(20.4f, 12.4f));
            step = "under-willow";
            Vector2 willow = new Vector2(14.5f, 11.0f);
            var circle = new List<Vector2> { new Vector2(18.2f, 9.6f) };
            for (int i = 0; i <= 8; i++) { float a = -0.35f + i * Mathf.PI / 4.2f; circle.Add(willow + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.3f); }
            yield return Walk("circle under the willow canopy", circle.ToArray());
            step = "gather";
            ResourceNode flower = Nearest(new Vector2(10f, 10.5f));
            yield return Walk("walk to the Dawn Blossom", new Vector2(flower.Position.x + 1.1f, flower.Position.z - 0.6f));
            yield return Stop("stop at the Dawn Blossom", 0.4f);
            yield return Gather(flower);
            step = "under-tree";
            yield return Walk("walk south and under the broadleaf by the hen yard", new Vector2(9.0f, 1.0f), new Vector2(8.4f, -4.5f), new Vector2(12.6f, -3.6f), new Vector2(15.8f, -4.0f));
            yield return Stop("stop under the broadleaf's canopy (behind its trunk)", 1.0f);
            step = "garden";
            yield return Walk("walk to the garden's west gate", new Vector2(17.0f, -6.6f), new Vector2(13.5f, -9.5f), new Vector2(10.8f, -12.6f), new Vector2(10.9f, -14.0f));
            yield return Walk("walk into the garden between the raised beds", new Vector2(12.6f, -14.0f), new Vector2(15.0f, -13.62f), new Vector2(17.0f, -13.62f), new Vector2(18.7f, -14.0f));
            yield return Stop("stop at the garden's east end", 0.8f);
            yield return Walk("walk back out of the garden", new Vector2(17.0f, -14.38f), new Vector2(15.0f, -14.38f), new Vector2(12.6f, -14.0f), new Vector2(10.9f, -14.0f));
            step = "to-spring";
            yield return Walk("walk round the plaza to the west path and the spring", new Vector2(9.5f, -9.5f), new Vector2(7.0f, -6.0f), new Vector2(2.0f, -7.6f), new Vector2(-5.5f, -5.0f),
                new Vector2(-7.6f, 0.5f), new Vector2(-8.0f, 5.6f), new Vector2(-14.0f, 11.0f), new Vector2(-18.2f, 13.4f));
            var springPoint = FindFirstObjectByType<SpringInteractable>();
            yield return Approach("step up to the spring until it is offered", springPoint);
            yield return Stop("stop at the spring", 1.0f);
            yield return Use("inspect the spring", springPoint, false);
            step = "stream";
            yield return Walk("walk down the stream bank under the trees", new Vector2(-21.4f, 11.2f), new Vector2(-22.4f, 8.0f), new Vector2(-24.0f, 5.0f));
            yield return Stop("stop on the stream bank", 0.8f);
            step = "back-to-plaza";
            yield return Walk("walk back to the plaza", new Vector2(-17.0f, 4.6f), new Vector2(-11.0f, 3.6f), new Vector2(-6.4f, 2.4f));
            yield return Stop("stop on the plaza", 2.0f);
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
                yield return new WaitForSecondsRealtime(shown < 2 ? 2.2f : 0.05f);
                shown++;
                dialogue.Advance();
            }
            yield return null;   // the minimap refreshes its objective in LateUpdate
            Mark(label + ": dialogue closed after " + shown + " lines; objective now " + (minimap != null ? minimap.Objective.ToString() : "?"));
            yield return new WaitForSecondsRealtime(0.3f);
        }

        IEnumerator Gather(ResourceNode node)
        {
            step = "gather";
            var session = GameSession.Current;
            int before = session.Inventory.Count(node.ItemId);
            float wait = 0f;
            while (!ReferenceEquals(detector.Current, node) && wait < 1f) { wait += Time.unscaledDeltaTime; yield return null; }
            bool ok = ReferenceEquals(detector.Current, node) && detector.TryInteract();
            yield return null;
            int after = session.Inventory.Count(node.ItemId);
            if (!ok || after != before + 1) record.failures.Add("gather " + node.NodeId + ": interaction " + ok + ", count " + before + " -> " + after);
            if (node.ReadyVisualShown) record.failures.Add("gather " + node.NodeId + ": the gathered plant is still shown as ready");
            Mark("gathered " + node.NodeId + " (" + before + " -> " + after + "); ready visual shown " + node.ReadyVisualShown);
            yield return new WaitForSecondsRealtime(0.8f);
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
            ("plaza", new Vector2(4.0f, -3.5f), 225f),
            ("workshop-front", new Vector2(20.4f, 7.4f), 90f),
            ("workshop-side", new Vector2(25.6f, 3.6f), 0f),
            ("willow", new Vector2(12.6f, 8.6f), 45f),
            ("spring", new Vector2(-18.4f, 12.6f), 300f),
            ("garden", new Vector2(10.2f, -14.2f), 90f),
            ("hen-yard", new Vector2(19.0f, -3.0f), 90f),
            ("stream", new Vector2(-24.6f, 6.4f), 300f),
            ("windmill", new Vector2(3.6f, 22.0f), 0f),
        };

        IEnumerator Stills()
        {
            step = "stills";
            HudController hud = FindFirstObjectByType<HudController>();
            bool startedBefore = b5 != null && b5.BeforeMode;
            if (startedBefore) b5.Apply(false);
            float cy = follow != null ? follow.Yaw : 0f;
            Phase52B4Pilot.CameraView v4 = b4 != null ? b4.B4View : default;

            // matched B5 / B4 pairs: the same build, the same scene, the same place; only the presentation switches (stage S0)
            foreach (var s in Spots)
            {
                yield return Place(Ground(s.xz), s.faceYaw, defaultZoom);
                hud.HideBanner();
                yield return Shot("after-b5-" + s.name);
                if (b5 == null) continue;
                b5.Apply(true);
                yield return Place(Ground(s.xz), s.faceYaw, defaultZoom);
                yield return Shot("before-b4-" + s.name);
                b5.Apply(false);
            }

            // the three candidate pitches (the default is restored afterwards)
            foreach (float pitch in new[] { 42f, 48f, 54f })
            {
                if (follow != null && b4 != null) follow.Configure(v4.yaw, pitch, v4.distance, v4.minDistance, v4.maxDistance, v4.fieldOfView);
                foreach (var s in Spots.Where(s => s.name == "plaza" || s.name == "workshop-front" || s.name == "garden"))
                {
                    yield return Place(Ground(s.xz), s.faceYaw, defaultZoom);
                    yield return Shot("pitch-" + pitch.ToString("F0") + "-" + s.name);
                }
            }
            if (follow != null && b4 != null) follow.Configure(v4.yaw, v4.pitch, v4.distance, v4.minDistance, v4.maxDistance, v4.fieldOfView);

            // occlusion: behind the workshop, the willow, the broadleaf by the hen yard and a tree on the stream bank, at the default and the
            // closest zoom (the closest zoom is the worst case: the canopy fills more of the screen)
            var occlusion = new (string name, Vector2 xz)[]
            {
                ("behind-workshop", new Vector2(27.2f, 11.8f)), ("behind-willow", new Vector2(15.6f, 12.6f)),
                ("behind-broadleaf-henyard", new Vector2(16.3f, -3.7f)), ("behind-broadleaf-stream", new Vector2(-19.6f, 7.2f)),
            };
            foreach (var o in occlusion)
                foreach (bool before in new[] { false, true })
                {
                    if (b5 != null) b5.Apply(before);
                    string label = before ? "-before-b4" : "";
                    foreach (float zoom in new[] { defaultZoom, follow != null ? follow.MinDistance : 10f })
                    {
                        yield return Place(Ground(o.xz), 225f, zoom);
                        string name = "occlusion-" + o.name + label + (zoom < defaultZoom ? "-closest-zoom" : "");
                        yield return Shot(name);
                        var measured = new Occlusion { still = name, presentation = before ? "B4" : "B5", zoom = zoom };
                        yield return MeasureVisibility(measured, !before);
                        record.occlusion.Add(measured);
                    }
                    if (b5 != null) b5.Apply(false);
                }

            // the ground at the farthest zoom over open meadow: repeats or seams of the ground textures would show here first
            foreach (bool before in new[] { false, true })
            {
                if (b5 != null) b5.Apply(before);
                yield return Place(Ground(new Vector2(-13.0f, -27.0f)), 225f, follow != null ? follow.MaxDistance : 17f);
                yield return Shot("ground-tiling-farthest-zoom" + (before ? "-before-b4" : ""));
                if (b5 != null) b5.Apply(false);
            }

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

            // the later stages, SET BY THIS SCRIPT (not reached by play; the isolated evidence save folder is the only one touched)
            foreach (var stage in new[] { (ResonanceStage.AwakeningStream, "S1"), (ResonanceStage.BloomingGarden, "S2"), (ResonanceStage.RestoredValley, "S3") })
            {
                yield return ShowStage(stage.Item1);
                foreach (string name in new[] { "spring", "garden", "plaza", "workshop-front", "stream", "windmill" })
                {
                    var s = Spots.First(x => x.name == name);
                    yield return Place(Ground(s.xz), s.faceYaw, defaultZoom);
                    yield return Shot("stage-" + stage.Item2 + "-set-by-script-" + s.name);
                    // B4 at the same place and stage: every place at S3, and the spring at S1 (where B4's bloom tree hid the caretaker)
                    if (b5 != null && (stage.Item2 == "S3" || stage.Item2 == "S1" && name == "spring"))
                    {
                        b5.Apply(true);
                        yield return Place(Ground(s.xz), s.faceYaw, defaultZoom);
                        yield return Shot("stage-" + stage.Item2 + "-set-by-script-before-b4-" + s.name);
                        b5.Apply(false);
                    }
                }
            }
            if (startedBefore) b5.Apply(true);
        }

        static readonly int OccludedTintId = Shader.PropertyToID("_OccludedTint");
        static readonly int FadeStrengthId = Shader.PropertyToID("_FadeStrength");

        /// <summary>
        /// How much of the caretaker the player sees, measured on the real framebuffer (the frames the player shows, captured at the end of
        /// consecutive frames while she stands still): the pixels her card changes inside her screen box in the full scene, divided by the
        /// pixels it changes when every other renderer is switched off (only the terrain and her). 1 = nothing hides her, 0 = fully hidden.
        /// The hidden-silhouette pass is switched off for this (it marks where she is but is not her); "marked" repeats the count with it
        /// on and a lower threshold. For B5 the count is also made with the painted trees' opening switched off.
        /// </summary>
        IEnumerator MeasureVisibility(Occlusion o, bool painted)
        {
            if (b4 == null || b4.HeroCard == null) yield break;
            Renderer hero = b4.HeroCard;
            Material material = hero.sharedMaterial;
            Color tint = material.GetColor(OccludedTintId);
            Vector3 feet = player.transform.position;
            Vector3 a = view.WorldToScreenPoint(feet), b = view.WorldToScreenPoint(feet + Vector3.up * 1.75f);
            float tall = Mathf.Abs(b.y - a.y);
            var box = new RectInt((int)(a.x - 0.32f * tall), (int)(Mathf.Min(a.y, b.y) - 2), (int)(0.64f * tall), (int)(tall + 4));
            Color32[] on = null, off = null, refOn = null, refOff = null, marked = null, closedOn = null, closedOff = null;
            material.SetColor(OccludedTintId, new Color(tint.r, tint.g, tint.b, 0f));
            yield return Frame(p => on = p);
            hero.enabled = false;
            yield return Frame(p => off = p);
            hero.enabled = true;
            if (painted)
            {
                var cards = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r => r.sharedMaterial != null && r.sharedMaterial.shader.name == "Echoes/PaintedCard25D").ToList();
                var block = new MaterialPropertyBlock();
                foreach (var r in cards) { r.GetPropertyBlock(block); block.SetFloat(FadeStrengthId, 0f); r.SetPropertyBlock(block); }
                yield return Frame(p => closedOn = p);
                hero.enabled = false;
                yield return Frame(p => closedOff = p);
                hero.enabled = true;
                foreach (var r in cards) r.SetPropertyBlock(null);
            }
            var others = FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r => r.enabled && r != hero).ToList();
            foreach (Renderer r in others) r.enabled = false;
            yield return Frame(p => refOn = p);
            hero.enabled = false;
            yield return Frame(p => refOff = p);
            hero.enabled = true;
            foreach (Renderer r in others) r.enabled = true;
            material.SetColor(OccludedTintId, tint);
            yield return Frame(p => marked = p);
            int w = Screen.width, h = Screen.height;
            int reference = Changed(refOn, refOff, w, h, box, 64);
            o.referencePixels = reference;
            if (reference < 50) yield break;
            o.visibleShare = Mathf.Clamp01(Changed(on, off, w, h, box, 64) / (float)reference);
            if (painted) o.visibleShareWithoutOpening = Mathf.Clamp01(Changed(closedOn, closedOff, w, h, box, 64) / (float)reference);
            o.markedShare = Mathf.Clamp01(Changed(marked, off, w, h, box, 24) / (float)Mathf.Max(1, Changed(refOn, refOff, w, h, box, 24)));
            Debug.Log("[Echoes][P52 B5 TOUR] visibility " + o.still + ": visible " + o.visibleShare.ToString("F3") + ", without the opening " + o.visibleShareWithoutOpening.ToString("F3")
                + ", marked " + o.markedShare.ToString("F3") + " (" + reference + " reference pixels)");
        }

        static IEnumerator Frame(Action<Color32[]> store)
        {
            yield return new WaitForEndOfFrame();
            Texture2D frame = ScreenCapture.CaptureScreenshotAsTexture();
            store(frame.GetPixels32());
            Destroy(frame);
        }

        static int Changed(Color32[] p, Color32[] q, int w, int h, RectInt box, int threshold)
        {
            if (p == null || q == null || p.Length != w * h || q.Length != w * h) return 0;
            int changed = 0;
            for (int y = Mathf.Max(0, box.yMin); y < Mathf.Min(h, box.yMax); y++)
                for (int x = Mathf.Max(0, box.xMin); x < Mathf.Min(w, box.xMax); x++)
                {
                    Color32 c = p[y * w + x], d = q[y * w + x];
                    if (Mathf.Max(Mathf.Abs(c.r - d.r), Mathf.Abs(c.g - d.g), Mathf.Abs(c.b - d.b)) > threshold) changed++;
                }
            return changed;
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
            while (resonance != null && (resonance.IsTransitioning || resonance.ShownStage != stage) && waited < 14f) { waited += 0.25f; yield return new WaitForSecondsRealtime(0.25f); }
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
