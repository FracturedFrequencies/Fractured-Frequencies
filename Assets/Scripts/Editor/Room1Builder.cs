#if UNITY_EDITOR
using System;
using System.Linq;
using FracturedFrequencies.Gravity;
using FracturedFrequencies.Player;
using FracturedFrequencies.Tools;
using FracturedFrequencies.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FracturedFrequencies.EditorTools
{
    /// <summary>
    /// Builds Room 1 (Tuning Workshop) as three small isometric diorama rooms.
    /// Menu: Tools > Fractured Frequencies > Build Room 1 (Tuning Workshop)
    ///
    /// Every room is a 10 x 10 floor with two back walls (right wall at +X, back wall at +Z), 8 m high,
    /// seen by a fixed isometric camera. Gravity can point at the floor or at the right wall.
    ///   Room A  Gravity:  click the right-wall crystal, fall onto the wall, walk into the crack.
    ///   Room B  Resonate: hold right click on the glass wall until it shatters, walk into the crack.
    ///   Room C  Combine:  shatter the glass barrier in front of the iron crate, make the right wall the floor;
    ///                     the crate falls 8 m and smashes the cracked stone that hides the exit.
    /// </summary>
    public static class Room1Builder
    {
        public const string ScenePath = "Assets/Scenes/Room1_TuningWorkshop.unity";
        private const string MatFolder = "Assets/Art/Materials/Room1";
        private const float W = 10f;  // room width (x) and depth (z)
        private const float H = 8f;   // wall height

        private static readonly Color Background = new Color(0.10f, 0.10f, 0.14f);

        private static Material _floor, _wall, _rim, _stone, _iron, _player, _crystal, _halo, _glass, _exit, _crack, _fx;

        [MenuItem("Tools/Fractured Frequencies/Build Room 1 (Tuning Workshop)")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (AssetDatabase.IsValidFolder(MatFolder)) AssetDatabase.DeleteAsset(MatFolder);
            EnsureFolder(MatFolder);
            EnsureFolder("Assets/Scenes");
            CreateMaterials();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---------------- World ----------------
            var world = new GameObject("World");
            var gravity = world.AddComponent<DirectionalGravity>();
            Set(gravity, "direction", Vector3.down);
            var room = world.AddComponent<RoomController>();

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.88f);
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.6f;
            sun.transform.rotation = Quaternion.Euler(50f, 30f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.62f, 0.7f);

            var level = new GameObject("Level").transform;
            var props = new GameObject("Interactive").transform;
            var chambers = new GameObject("Rooms").transform;

            // ================= Room A: gravity =================
            var oA = Vector3.zero;
            BuildRoom("Room A", level, oA, false);
            var aWallCrystal = Crystal("Crystal_A_RightWall", props, oA + new Vector3(W, 2.2f, 3f), Vector3.right, gravity);
            Crystal("Crystal_A_Floor", props, oA + new Vector3(6f, 0f, 8f), Vector3.down, gravity);

            // ================= Room B: resonate =================
            var oB = new Vector3(40f, 0f, 0f);
            BuildRoom("Room B", level, oB, false);
            var glassWall = Breakable("GlassWall", props, oB + new Vector3(5.15f, H / 2f, W / 2f), new Vector3(0.3f, H, W), _glass,
                new Vector3Int(1, 4, 5), 40f, 8f);
            AddResonant(glassWall, Resonant.ResonantMaterial.Glass, 660f);

            // ================= Room C: combine =================
            var oC = new Vector3(80f, 0f, 0f);
            BuildRoom("Room C", level, oC, true);
            var cWallCrystal = Crystal("Crystal_C_RightWall", props, oC + new Vector3(W, 4.5f, 2.5f), Vector3.right, gravity);
            Crystal("Crystal_C_Floor", props, oC + new Vector3(6f, 0f, 2f), Vector3.down, gravity);

            var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crate.name = "IronCrate_80kg";
            crate.transform.SetParent(props);
            crate.transform.position = oC + new Vector3(2f, 0.6f, 7f);
            crate.transform.localScale = Vector3.one * 1.2f;
            crate.GetComponent<MeshRenderer>().sharedMaterial = _iron;
            var crateBody = crate.AddComponent<Rigidbody>();
            crateBody.mass = 80f;
            crateBody.interpolation = RigidbodyInterpolation.Interpolate;
            crateBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            crate.AddComponent<GravityBody>();

            var glassBarrier = Breakable("GlassBarrier", props, oC + new Vector3(3.65f, 1.1f, 7f), new Vector3(0.3f, 2.2f, 2f), _glass,
                new Vector3Int(1, 2, 2), 10f, 6f);
            AddResonant(glassBarrier, Resonant.ResonantMaterial.Glass, 740f);

            var slab = Breakable("CrackedStone", props, oC + new Vector3(W + 0.25f, 1f, 7f), new Vector3(0.5f, 2f, 2f), _stone,
                new Vector3Int(1, 2, 2), 60f, 4f);
            Visual("Crack1", slab.transform, PrimitiveType.Cube, new Vector3(-0.26f, 0.2f, 0.1f), new Vector3(0.02f, 1.5f, 0.06f), _crack)
                .transform.localRotation = Quaternion.Euler(35f, 0f, 0f);
            Visual("Crack2", slab.transform, PrimitiveType.Cube, new Vector3(-0.26f, -0.3f, -0.3f), new Vector3(0.02f, 1.0f, 0.06f), _crack)
                .transform.localRotation = Quaternion.Euler(-50f, 0f, 0f);
            var impact = slab.AddComponent<ImpactBreakable>();
            Set(impact, "energyThreshold", 2500f);
            Set(impact, "displayName", "Cracked stone");

            // Exit: the glowing recess behind the cracked stone
            var exit = new GameObject("Exit");
            exit.transform.SetParent(props);
            exit.transform.position = oC + new Vector3(11.3f, 1f, 7f);
            var exitBox = exit.AddComponent<BoxCollider>();
            exitBox.isTrigger = true;
            exitBox.size = new Vector3(2.4f, 2f, 2f);
            var exitGlow = Box("ExitGlow", exit.transform, oC + new Vector3(12.5f, 0f, 6f), oC + new Vector3(12.8f, 2f, 8f), _exit);
            GameObjectUtility.SetStaticEditorFlags(exitGlow, 0); // it pulses at runtime
            var roomExit = exit.AddComponent<RoomExit>();
            Set(roomExit, "roomName", "Room 1");
            Set(roomExit, "nextSceneName", "");
            Set(roomExit, "glow", exitGlow.transform);

            // ---------------- Rooms (checkpoints) ----------------
            var chA = MakeChamber(chambers, "Room A", oA, new Vector3(2f, 0.95f, 2f), null, null);
            var chB = MakeChamber(chambers, "Room B", oB, new Vector3(2f, 0.95f, 5f), null, new[] { glassWall });
            var chC = MakeChamber(chambers, "Room C", oC, new Vector3(2f, 0.95f, 2.5f), new[] { crateBody }, new[] { glassBarrier, slab });

            // Portals (glowing cracks) between the rooms
            var portalA = MakePortal("Crack_A_to_B", props, oA + new Vector3(W - 0.05f, 6f, 7f), new Vector3(-0.75f, 0f, 0f),
                new Vector3(1.6f, 2.2f, 1.6f), new Vector3(0.08f, 2.2f, 0.5f), chB);
            var portalB = MakePortal("Crack_B_to_C", props, oB + new Vector3(8f, 0.03f, 5f), new Vector3(0f, 0.7f, 0f),
                new Vector3(1.6f, 1.4f, 2.4f), new Vector3(0.5f, 0.06f, 2.2f), chC);

            // ---------------- Player ----------------
            var player = new GameObject("Player");
            player.tag = "Player";
            player.transform.position = oA + new Vector3(2f, 0.95f, 2f);
            var body = player.AddComponent<Rigidbody>();
            body.mass = 70f;
            var capsule = player.AddComponent<CapsuleCollider>();
            capsule.height = 1.8f;
            capsule.radius = 0.35f;
            var input = player.AddComponent<PlayerInputReader>();
            var motor = player.AddComponent<PlayerMotor>();
            Visual("Body", player.transform, PrimitiveType.Capsule, Vector3.zero, new Vector3(0.7f, 0.9f, 0.7f), _player);
            Visual("Scarf", player.transform, PrimitiveType.Cube, new Vector3(0f, 0.45f, -0.1f), new Vector3(0.75f, 0.12f, 0.75f), _crystal);
            Visual("Eye", player.transform, PrimitiveType.Sphere, new Vector3(0f, 0.6f, 0.3f), Vector3.one * 0.16f, _halo);

            // ---------------- Camera ----------------
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Background;
            cam.orthographic = true;
            cam.orthographicSize = 9.5f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            camGO.AddComponent<AudioListener>();
            var iso = camGO.AddComponent<IsoCamera>();
            Set(iso, "startChamber", chA);
            Set(iso, "orthographicSize", 9.5f);
            camGO.transform.rotation = Quaternion.Euler(30f, 45f, 0f);
            camGO.transform.position = new Vector3(5f, 4f, 5f) - camGO.transform.forward * 50f;
            var pointer = camGO.AddComponent<PointerInteractor>();
            Set(pointer, "viewCamera", cam);
            Set(pointer, "input", input);
            Set(pointer, "player", motor);
            Set(pointer, "beamMaterial", _fx);
            Set(motor, "cameraTransform", camGO.transform);

            // ---------------- Tutorial + HUD ----------------
            var markerGO = new GameObject("TargetMarker");
            markerGO.AddComponent<LineRenderer>().sharedMaterial = _fx;
            var marker = markerGO.AddComponent<TargetMarker>();
            Set(marker, "viewCamera", camGO.transform);

            var tutorial = new GameObject("Tutorial").AddComponent<TutorialDirector>();
            Set(tutorial, "player", motor);
            Set(tutorial, "gravity", gravity);
            Set(tutorial, "marker", marker);
            Set(tutorial, "completeText", "Room 1 complete!\nGravity, resonance and impact: you know all three.");
            var so = new SerializedObject(tutorial);
            var steps = so.FindProperty("steps");
            steps.arraySize = 0;
            AddStep(steps, "Move with  W A S D", null, 0f, null, 1f, TutorialStep.Goal.MoveDistance, 2f);
            AddStep(steps, "Pink crystals change gravity.\nPoint at the crystal on the RIGHT WALL and LEFT CLICK it.",
                "It is the pink diamond inside the yellow ring.", 8f,
                aWallCrystal.transform, 1f, TutorialStep.Goal.GravityDirection, 0f, Vector3.right);
            AddStep(steps, "You are standing on the wall!\nWalk to the glowing crack and step into it.",
                "W walks up the wall. The crystal on the floor turns gravity back.", 12f,
                portalA, 1.4f, TutorialStep.Goal.EnterChamber, 0f, default, chB);
            AddStep(steps, "A glass wall blocks the way.\nPoint at it and HOLD the RIGHT mouse button until it shatters.",
                "Keep holding. The bar above the cursor fills up.", 6f,
                glassWall.transform, 1.6f, TutorialStep.Goal.ObjectGone, 0f, default, null, glassWall);
            AddStep(steps, "Step into the glowing crack.", null, 0f,
                portalB, 1.4f, TutorialStep.Goal.EnterChamber, 0f, default, chC);
            AddStep(steps, "The exit is behind the CRACKED STONE in the right wall.\nStone is too thick to resonate. Only something HEAVY falling hard breaks it.",
                "Shatter the glass in front of the iron crate (hold RIGHT click), then LEFT CLICK the crystal on the right wall. Let the crate fall!", 15f,
                slab.transform, 1.5f, TutorialStep.Goal.ObjectGone, 0f, default, null, slab);
            AddStep(steps, "The stone broke!\nMake the right wall the floor and walk into the glowing opening.",
                "Click the crystal on the right wall if you are still on the floor.", 8f,
                exit.transform, 1.4f, TutorialStep.Goal.RoomComplete, 0f);
            so.ApplyModifiedPropertiesWithoutUndo();

            var hud = new GameObject("HUD").AddComponent<GameHUD>();
            Set(hud, "tutorial", tutorial);
            Set(hud, "pointer", pointer);

            Set(room, "player", motor);
            Set(room, "gravity", gravity);
            Set(room, "startChamber", chA);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = player;
            Debug.Log("[FF] Room 1 (isometric) built and saved to " + ScenePath + ". Press Play.");
        }

        // =================================================================== rooms

        /// <summary>Floor, right wall (+X), back wall (+Z), low rims on the open sides and lips on the wall edges.</summary>
        private static void BuildRoom(string name, Transform parent, Vector3 o, bool rightWallHole)
        {
            var r = new GameObject(name).transform;
            r.SetParent(parent);

            Box("Floor", r, o + new Vector3(0f, -0.5f, 0f), o + new Vector3(W, 0f, W), _floor);
            Box("BackWall", r, o + new Vector3(0f, -0.5f, W), o + new Vector3(W + 0.5f, H, W + 0.5f), _wall);

            if (!rightWallHole)
            {
                Box("RightWall", r, o + new Vector3(W, -0.5f, 0f), o + new Vector3(W + 0.5f, H, W + 0.5f), _wall);
            }
            else
            {
                // Hole at y 0..2, z 6..8 (closed by the cracked stone), with a recess behind it.
                Box("RightWall_A", r, o + new Vector3(W, -0.5f, 0f), o + new Vector3(W + 0.5f, H, 6f), _wall);
                Box("RightWall_B", r, o + new Vector3(W, -0.5f, 8f), o + new Vector3(W + 0.5f, H, W + 0.5f), _wall);
                Box("RightWall_Top", r, o + new Vector3(W, 2f, 6f), o + new Vector3(W + 0.5f, H, 8f), _wall);
                Box("RightWall_Bottom", r, o + new Vector3(W, -0.5f, 6f), o + new Vector3(W + 0.5f, 0f, 8f), _wall);
                Box("Recess_Bottom", r, o + new Vector3(W + 0.5f, -0.3f, 5.7f), o + new Vector3(W + 2.8f, 0f, 8.3f), _rim);
                Box("Recess_Top", r, o + new Vector3(W + 0.5f, 2f, 5.7f), o + new Vector3(W + 2.8f, 2.3f, 8.3f), _rim);
                Box("Recess_Left", r, o + new Vector3(W + 0.5f, 0f, 5.7f), o + new Vector3(W + 2.8f, 2f, 6f), _rim);
                Box("Recess_Right", r, o + new Vector3(W + 0.5f, 0f, 8f), o + new Vector3(W + 2.8f, 2f, 8.3f), _rim);
            }

            // Low rims along the open front sides
            Box("Rim_Left", r, o + new Vector3(-0.3f, -0.5f, 0f), o + new Vector3(0f, 0.4f, W), _rim);
            Box("Rim_Front", r, o + new Vector3(-0.3f, -0.5f, -0.3f), o + new Vector3(W, 0.4f, 0f), _rim);
            // Lips on the open edges of the walls (they keep you on a wall when it is the floor)
            Box("RightWall_LipTop", r, o + new Vector3(W - 0.4f, H - 0.3f, 0f), o + new Vector3(W, H, W), _rim);
            Box("RightWall_LipFront", r, o + new Vector3(W - 0.4f, 0f, 0f), o + new Vector3(W, H, 0.3f), _rim);
            Box("BackWall_LipTop", r, o + new Vector3(0f, H - 0.3f, W - 0.4f), o + new Vector3(W, H, W), _rim);
            Box("BackWall_LipLeft", r, o + new Vector3(0f, 0f, W - 0.4f), o + new Vector3(0.3f, H, W), _rim);
        }

        private static GameObject Box(string name, Transform parent, Vector3 min, Vector3 max, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, true);
            go.transform.position = (min + max) / 2f;
            go.transform.localScale = max - min;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        private static GameObject Visual(string name, Transform parent, PrimitiveType type, Vector3 localPos, Vector3 localScale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        /// <summary>Static box that can shatter: collider on the root, mesh as first child, BoxShatter attached.</summary>
        private static GameObject Breakable(string name, Transform parent, Vector3 center, Vector3 size, Material mat,
            Vector3Int pieces, float mass, float lifetime)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = center;
            go.AddComponent<BoxCollider>().size = size;
            Visual("Visual", go.transform, PrimitiveType.Cube, Vector3.zero, size, mat);
            var shatter = go.AddComponent<BoxShatter>();
            Set(shatter, "pieces", pieces);
            Set(shatter, "totalMass", mass);
            Set(shatter, "pieceLifetime", lifetime);
            return go;
        }

        private static void AddResonant(GameObject go, Resonant.ResonantMaterial material, float frequency)
        {
            var res = go.AddComponent<Resonant>();
            Set(res, "material", material);
            Set(res, "naturalFrequency", frequency);
        }

        /// <summary>Crystal on a surface. `surfacePoint` lies on the surface, `gravityDir` points into it.</summary>
        private static GravitySwitchCrystal Crystal(string name, Transform parent, Vector3 surfacePoint, Vector3 gravityDir, DirectionalGravity gravity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = surfacePoint - gravityDir.normalized * 0.45f;
            go.transform.rotation = Quaternion.FromToRotation(Vector3.up, -gravityDir.normalized);
            var trigger = go.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.8f;

            var visual = Visual("Diamond", go.transform, PrimitiveType.Cube, Vector3.zero, new Vector3(0.45f, 0.8f, 0.45f), _crystal);
            visual.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            var halo = Visual("ActiveHalo", go.transform, PrimitiveType.Cylinder, new Vector3(0f, -0.4f, 0f), new Vector3(1.3f, 0.02f, 1.3f), _halo);

            var crystal = go.AddComponent<GravitySwitchCrystal>();
            Set(crystal, "targetGravity", gravity);
            Set(crystal, "gravityDirection", gravityDir.normalized);
            Set(crystal, "visual", visual.transform);
            Set(crystal, "activeHalo", halo);
            Set(crystal, "fxMaterial", _fx);
            return crystal;
        }

        private static Transform MakePortal(string name, Transform parent, Vector3 position, Vector3 triggerCenter,
            Vector3 triggerSize, Vector3 glowSize, Chamber target)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = position;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = triggerCenter;
            box.size = triggerSize;
            var glow = Visual("Glow", go.transform, PrimitiveType.Cube, Vector3.zero, glowSize, _exit);
            var portal = go.AddComponent<Portal>();
            Set(portal, "target", target);
            Set(portal, "glow", glow.transform);
            return glow.transform;
        }

        private static Chamber MakeChamber(Transform parent, string name, Vector3 origin, Vector3 spawn,
            Rigidbody[] resetBodies, GameObject[] resetObjects)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = origin + new Vector3(W / 2f, H / 2f, W / 2f);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(W, H, W);
            var chamber = go.AddComponent<Chamber>();

            var spawnPoint = new GameObject("Spawn").transform;
            spawnPoint.SetParent(go.transform);
            spawnPoint.position = origin + spawn;

            Set(chamber, "displayName", name);
            Set(chamber, "spawnPoint", spawnPoint);
            Set(chamber, "gravityDirection", Vector3.down);
            var so = new SerializedObject(chamber);
            var bodies = so.FindProperty("resetBodies");
            bodies.arraySize = resetBodies?.Length ?? 0;
            for (int i = 0; i < bodies.arraySize; i++) bodies.GetArrayElementAtIndex(i).objectReferenceValue = resetBodies[i];
            var objects = so.FindProperty("resetObjects");
            objects.arraySize = resetObjects?.Length ?? 0;
            for (int i = 0; i < objects.arraySize; i++) objects.GetArrayElementAtIndex(i).objectReferenceValue = resetObjects[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return chamber;
        }

        private static void AddStep(SerializedProperty steps, string instruction, string hint, float hintDelay,
            Transform highlight, float radius, TutorialStep.Goal goal, float distance,
            Vector3 gravityDirection = default, Chamber chamber = null, GameObject breakTarget = null)
        {
            int i = steps.arraySize;
            steps.arraySize = i + 1;
            var s = steps.GetArrayElementAtIndex(i);
            s.FindPropertyRelative("instruction").stringValue = instruction;
            s.FindPropertyRelative("hint").stringValue = hint ?? "";
            s.FindPropertyRelative("hintDelay").floatValue = hintDelay;
            s.FindPropertyRelative("highlight").objectReferenceValue = highlight;
            s.FindPropertyRelative("highlightRadius").floatValue = radius;
            s.FindPropertyRelative("goal").enumValueIndex = (int)goal;
            s.FindPropertyRelative("distance").floatValue = distance;
            s.FindPropertyRelative("gravityDirection").vector3Value = gravityDirection;
            s.FindPropertyRelative("chamber").objectReferenceValue = chamber;
            s.FindPropertyRelative("door").objectReferenceValue = null;
            s.FindPropertyRelative("breakTarget").objectReferenceValue = breakTarget;
        }

        // =================================================================== materials

        private static void CreateMaterials()
        {
            _floor   = Watercolor("Floor", new Color(0.86f, 0.82f, 0.74f), new Color(0.74f, 0.70f, 0.62f));
            _wall    = Watercolor("Wall", new Color(0.74f, 0.74f, 0.78f), new Color(0.58f, 0.58f, 0.66f));
            _rim     = Watercolor("Rim", new Color(0.46f, 0.44f, 0.50f), new Color(0.34f, 0.32f, 0.40f));
            _stone   = Watercolor("CrackedStone", new Color(0.58f, 0.50f, 0.44f), new Color(0.44f, 0.37f, 0.32f));
            _iron    = Watercolor("IronCrate", new Color(0.36f, 0.40f, 0.50f), new Color(0.22f, 0.25f, 0.33f));
            _player  = Watercolor("Player", new Color(0.24f, 0.22f, 0.32f), new Color(0.14f, 0.12f, 0.22f));
            _crystal = Unlit("Crystal", new Color(1.00f, 0.38f, 0.78f));
            _halo    = Unlit("CrystalHalo", new Color(1.00f, 0.88f, 0.96f));
            _glass   = Unlit("Glass", new Color(0.62f, 0.88f, 1.00f));
            _exit    = Unlit("ExitGlow", new Color(1.00f, 0.97f, 0.80f));
            _crack   = Unlit("CrackLine", new Color(0.16f, 0.12f, 0.12f));
            _fx      = SaveMaterial("Fx", Shader.Find("Sprites/Default"));
        }

        private static Material Watercolor(string name, Color top, Color bottom)
        {
            var shader = Shader.Find("Watercolor/Watercolor Lit");
            if (shader == null) return Unlit(name, Color.Lerp(bottom, top, 0.6f));
            var mat = SaveMaterial(name, shader);
            mat.SetColor("_TopColor", top);
            mat.SetColor("_BottomColor", bottom);
            mat.SetFloat("_GRADSPACE", 0f);
            mat.EnableKeyword("_GRADSPACE_LOCAL");
            mat.DisableKeyword("_GRADSPACE_WORLD");
            mat.SetFloat("_FadeAmount", 0f);
            mat.SetFloat("_WashStrength", 0.3f);
            mat.SetFloat("_EdgePool", 0.3f);
            mat.SetFloat("_LightColorInfluence", 0.3f);
            mat.SetColor("_ShadowColor", new Color(0.70f, 0.68f, 0.80f));
            mat.SetFloat("_ShadowDensity", 0.1f);
            mat.SetFloat("_ReceiveShadows", 0.6f);
            mat.SetFloat("_RimStrength", 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material Unlit(string name, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var mat = SaveMaterial(name, shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material SaveMaterial(string name, Shader shader)
        {
            var mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, $"{MatFolder}/M_{name}.mat");
            return mat;
        }

        // =================================================================== utilities

        /// <summary>Sets a (possibly private) serialized field.</summary>
        private static void Set(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null)
            {
                Debug.LogWarning($"[FF] Room1Builder: field '{field}' not found on {target.GetType().Name}");
                return;
            }
            switch (value)
            {
                case Object o:     p.objectReferenceValue = o; break;
                case Enum e:       p.enumValueIndex = Convert.ToInt32(e); break;
                case float f:      p.floatValue = f; break;
                case int i:        p.intValue = i; break;
                case bool b:       p.boolValue = b; break;
                case string s:     p.stringValue = s; break;
                case Vector3 v:    p.vector3Value = v; break;
                case Vector3Int v: p.vector3IntValue = v; break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        private static void AddToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path)) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
#endif
