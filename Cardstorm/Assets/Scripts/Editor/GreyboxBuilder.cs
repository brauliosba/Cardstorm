using System.IO;
using Cardstorm.CameraRig;
using Cardstorm.Core;
using Cardstorm.Enemies;
using Cardstorm.Spawning;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cardstorm.EditorTools
{
    // Construye el greybox de forma reproducible e idempotente.
    // Se ejecuta desde el menú
    public static class GreyboxBuilder
    {
        public const string ArtFolder = "Assets/Art/Greybox";
        public const string CircleSpritePath = ArtFolder + "/Greybox_Circle.png";
        public const string SquareSpritePath = ArtFolder + "/Greybox_Square.png";
        public const string TriangleSpritePath = ArtFolder + "/Greybox_Triangle.png";

        public const string PrefabFolder = "Assets/Prefabs";
        public const string PlayerPrefabPath = PrefabFolder + "/Player.prefab";
        public const string RockPrefabPath = PrefabFolder + "/Obstacle_Rock.prefab";
        public const string EnemyPrefabPath = PrefabFolder + "/Enemy.prefab";
        public const string ColumnPrefabPath = PrefabFolder + "/Obstacle_Column.prefab";

        public const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";

        public const string EnemyDataFolder = "Assets/Data/Enemies";
        public const string SpawningDataFolder = "Assets/Data/Spawning";
        public const string SpawnCurvePath = SpawningDataFolder + "/SpawnCurve_Partida.asset";

        const string UnlitSpriteMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

        const int SpriteSize = 64;

        // Mapa
        static readonly Vector2 MapSize = new Vector2(120f, 120f);
        const float BorderThickness = 2f;
        const float GridSpacing = 10f;
        static readonly Color FloorColor = new Color32(0x2B, 0x2B, 0x30, 0xFF);
        static readonly Color GridColor = new Color32(0x36, 0x36, 0x3C, 0xFF);
        static readonly Color BorderColor = new Color32(0x4A, 0x4A, 0x52, 0xFF);
        static readonly Color ObstacleColor = new Color32(0x80, 0x80, 0x86, 0xFF);

        // Cámara (valores provisionales)
        const float OrthoSize = 9f;
        const float FollowDamping = 0.4f;
        const float LookaheadTime = 0.3f;      // 0,3 s × 5 u/s = 1,5 unidades de adelanto
        const float LookaheadSmoothing = 10f;

        [MenuItem("Cardstorm/Greybox/Construir greybox")]
        public static void BuildAll()
        {
            ApplyLayersAndPhysics();
            CreateGreyboxSprites();
            BuildPlayerPrefab();
            BuildObstaclePrefabs();
            CreateEnemyDefinitions(overwrite: false);
            CreateSpawnCurve(overwrite: false);
            BuildEnemyPrefab();
            AssetDatabase.SaveAssets();

            if (!OpenGameplayScene(out Scene scene))
            {
                Debug.LogWarning("[GreyboxBuilder] Construcción de la escena cancelada.");
                return;
            }
            var mapBounds = BuildMap(scene);
            var player = EnsurePlayer(scene);
            BuildCameraRig(scene, mapBounds, player);
            BuildEnemySystems(scene, mapBounds, player);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[GreyboxBuilder] Greybox construido.");
        }

        [MenuItem("Cardstorm/Greybox/Restablecer datos a la memoria")]
        public static void ResetData()
        {
            if (!Application.isBatchMode && !EditorUtility.DisplayDialog(
                    "Restablecer datos",
                    "Se sobrescribirán las definiciones de enemigos y la curva de aparición con los valores de la memoria. Los cambios hechos en el inspector se perderán.",
                    "Restablecer", "Cancelar"))
                return;

            CreateEnemyDefinitions(overwrite: true);
            CreateSpawnCurve(overwrite: true);
            AssetDatabase.SaveAssets();
            Debug.Log("[GreyboxBuilder] Datos restablecidos.");
        }

        // ---------------------------------------------------------------------
        // 3. Datos: definiciones de enemigos (solo se crean si no existen)
        // ---------------------------------------------------------------------

        static void CreateEnemyDefinitions(bool overwrite)
        {
            Directory.CreateDirectory(EnemyDataFolder);

            // Vida, XP y oro de la memoria; velocidad, daño, radio y masa provisionales.
            CreateEnemyDefinition(overwrite, "Rondador", "rondador", "Rondador",
                maxHealth: 10f, xp: 1, goldAverage: 0.3f, goldChance: 0.3f,
                moveSpeed: 3f, contactDamage: 10f, radius: 0.4f, mass: 1f,
                EnemyShape.Circle, new Color32(0xD9, 0x53, 0x4F, 0xFF), poolPrewarm: 250);
            CreateEnemyDefinition(overwrite, "Corredor", "corredor", "Corredor",
                maxHealth: 6f, xp: 1, goldAverage: 0.3f, goldChance: 0.3f,
                moveSpeed: 7f, contactDamage: 10f, radius: 0.3f, mass: 0.7f,
                EnemyShape.Triangle, new Color32(0xF0, 0xAD, 0x4E, 0xFF), poolPrewarm: 80);
            CreateEnemyDefinition(overwrite, "Tirador", "tirador", "Tirador",
                maxHealth: 20f, xp: 2, goldAverage: 0.5f, goldChance: 0.5f,
                moveSpeed: 2.5f, contactDamage: 10f, radius: 0.45f, mass: 1f,
                EnemyShape.Square, new Color32(0x9B, 0x59, 0xB6, 0xFF), poolPrewarm: 40);
            CreateEnemyDefinition(overwrite, "Coloso", "coloso", "Coloso",
                maxHealth: 120f, xp: 10, goldAverage: 3f, goldChance: 1f,
                moveSpeed: 1.8f, contactDamage: 20f, radius: 1f, mass: 20f,
                EnemyShape.Circle, new Color32(0x8B, 0x5A, 0x2B, 0xFF), poolPrewarm: 10);
        }

        public static string EnemyDefinitionPath(string suffix) => $"{EnemyDataFolder}/EnemyDefinition_{suffix}.asset";

        // Curva de la partida (hojas Tramos y Oleadas). Solo se crea si no existe, salvo al restablecer.
        static void CreateSpawnCurve(bool overwrite)
        {
            Directory.CreateDirectory(SpawningDataFolder);

            var curve = AssetDatabase.LoadAssetAtPath<SpawnCurve>(SpawnCurvePath);
            if (curve != null && !overwrite)
                return;

            bool isNew = curve == null;
            if (isNew)
                curve = ScriptableObject.CreateInstance<SpawnCurve>();

            curve.enemyTypes = new[]
            {
                AssetDatabase.LoadAssetAtPath<EnemyDefinition>(EnemyDefinitionPath("Rondador")),
                AssetDatabase.LoadAssetAtPath<EnemyDefinition>(EnemyDefinitionPath("Corredor")),
                AssetDatabase.LoadAssetAtPath<EnemyDefinition>(EnemyDefinitionPath("Tirador")),
                AssetDatabase.LoadAssetAtPath<EnemyDefinition>(EnemyDefinitionPath("Coloso")),
            };
            SpawnCurveDefaults.ApplyTo(curve);

            if (isNew)
                AssetDatabase.CreateAsset(curve, SpawnCurvePath);
            else
                EditorUtility.SetDirty(curve);

            foreach (var issue in curve.Validate())
                Debug.LogWarning($"[GreyboxBuilder] SpawnCurve: {issue}");
        }

        static void CreateEnemyDefinition(bool overwrite, string suffix, string id, string displayName,
            float maxHealth, int xp, float goldAverage, float goldChance,
            float moveSpeed, float contactDamage, float radius, float mass,
            EnemyShape shape, Color color, int poolPrewarm)
        {
            string path = EnemyDefinitionPath(suffix);
            var def = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (def != null && !overwrite)
                return;

            bool isNew = def == null;
            if (isNew)
                def = ScriptableObject.CreateInstance<EnemyDefinition>();

            def.id = id;
            def.displayName = displayName;
            def.maxHealth = maxHealth;
            def.xp = xp;
            def.goldAverage = goldAverage;
            def.goldChance = goldChance;
            def.moveSpeed = moveSpeed;
            def.contactDamage = contactDamage;
            def.radius = radius;
            def.mass = mass;
            def.behaviour = EnemyBehaviour.Chase;
            def.shape = shape;
            def.color = color;
            def.poolPrewarm = poolPrewarm;

            if (isNew)
                AssetDatabase.CreateAsset(def, path);
            else
                EditorUtility.SetDirty(def);
        }

        // ---------------------------------------------------------------------
        // 1. Capas, matriz de colisiones y física 2D
        // ---------------------------------------------------------------------

        static readonly (int index, string name)[] ProjectLayers =
        {
            (Layers.Player, Layers.PlayerName),
            (Layers.Enemy, Layers.EnemyName),
            (Layers.Obstacle, Layers.ObstacleName),
            (Layers.PlayerAttack, Layers.PlayerAttackName),
            (Layers.EnemyAttack, Layers.EnemyAttackName),
            (Layers.Pickup, Layers.PickupName),
        };

        static void ApplyLayersAndPhysics()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layersProp = tagManager.FindProperty("layers");
            foreach (var (index, name) in ProjectLayers)
            {
                var element = layersProp.GetArrayElementAtIndex(index);
                if (!string.IsNullOrEmpty(element.stringValue) && element.stringValue != name)
                    Debug.LogWarning($"[GreyboxBuilder] La capa {index} se llamaba '{element.stringValue}'; se renombra a '{name}'.");
                element.stringValue = name;
            }
            tagManager.ApplyModifiedPropertiesWithoutUndo();

            Physics2D.gravity = Vector2.zero;
            Physics2D.simulationMode = SimulationMode2D.FixedUpdate;

            // Las capas propias solo chocan con lo que se indica; las de Unity (0-5) no se tocan.
            int[] own = { Layers.Player, Layers.Enemy, Layers.Obstacle, Layers.PlayerAttack, Layers.EnemyAttack, Layers.Pickup };
            int[] reserved = { Layers.PlayerAttack, Layers.EnemyAttack, Layers.Pickup };
            for (int a = 0; a < 32; a++)
            {
                foreach (int b in reserved)
                    Physics2D.IgnoreLayerCollision(a, b, a != Layers.Obstacle);
            }
            foreach (int a in own)
            foreach (int b in own)
            {
                if (System.Array.IndexOf(reserved, a) >= 0 || System.Array.IndexOf(reserved, b) >= 0)
                    continue;
                Physics2D.IgnoreLayerCollision(a, b, false);
            }

            // Fuerza la escritura de Physics2DSettings.asset.
            var physicsSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/Physics2DSettings.asset")[0];
            EditorUtility.SetDirty(physicsSettings);
            EditorUtility.SetDirty(tagManager.targetObject);
        }

        // ---------------------------------------------------------------------
        // 2. Sprites greybox (blancos, 64 x 64 px, 64 PPU = 1 unidad)
        // ---------------------------------------------------------------------

        enum Shape { Circle, Square, Triangle }

        static void CreateGreyboxSprites()
        {
            Directory.CreateDirectory(ArtFolder);
            CreateSpriteIfMissing(CircleSpritePath, Shape.Circle);
            CreateSpriteIfMissing(SquareSpritePath, Shape.Square);
            CreateSpriteIfMissing(TriangleSpritePath, Shape.Triangle);
        }

        static void CreateSpriteIfMissing(string path, Shape shape)
        {
            if (!File.Exists(path))
            {
                var tex = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false);
                var pixels = new Color32[SpriteSize * SpriteSize];
                for (int y = 0; y < SpriteSize; y++)
                for (int x = 0; x < SpriteSize; x++)
                {
                    byte alpha = (byte)Mathf.RoundToInt(Coverage(shape, x, y) * 255f);
                    pixels[y * SpriteSize + x] = new Color32(255, 255, 255, alpha);
                }
                tex.SetPixels32(pixels);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = SpriteSize;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        // ---------------------------------------------------------------------
        // 4. Prefabs
        // ---------------------------------------------------------------------

        static void BuildPlayerPrefab()
        {
            Directory.CreateDirectory(PrefabFolder);

            // Círculo blanco de 0,5 de radio (diámetro 1 = tamaño del sprite).
            var root = new GameObject("Player") { layer = Layers.Player };
            AddSprite(root, CircleSpritePath, Color.white, 0);

            var collider = root.AddComponent<CircleCollider2D>();
            collider.radius = 0.5f;
            root.AddComponent<Rigidbody2D>();
            root.AddComponent<Player.PlayerInputReader>();
            var movement = root.AddComponent<Player.PlayerMovement>();
            root.AddComponent<Player.PlayerHealth>();

            // Triángulo hijo que apunta en LastMoveDirection: el pivote gira y la flecha está desplazada hacia +Y.
            var facing = new GameObject("Facing") { layer = Layers.Player };
            facing.transform.SetParent(root.transform, false);
            var arrow = new GameObject("Arrow") { layer = Layers.Player };
            arrow.transform.SetParent(facing.transform, false);
            arrow.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            arrow.transform.localScale = new Vector3(0.3f, 0.3f, 1f);
            AddSprite(arrow, TriangleSpritePath, new Color(0.25f, 0.25f, 0.3f), 1);

            var so = new SerializedObject(movement);
            so.FindProperty("facingIndicator").objectReferenceValue = facing.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);
        }

        static void BuildObstaclePrefabs()
        {
            var rock = new GameObject("Obstacle_Rock") { layer = Layers.Obstacle };
            AddSprite(rock, CircleSpritePath, ObstacleColor, -10);
            rock.AddComponent<CircleCollider2D>().radius = 0.5f;
            PrefabUtility.SaveAsPrefabAsset(rock, RockPrefabPath);
            Object.DestroyImmediate(rock);

            var column = new GameObject("Obstacle_Column") { layer = Layers.Obstacle };
            AddSprite(column, SquareSpritePath, ObstacleColor, -10);
            column.AddComponent<BoxCollider2D>().size = Vector2.one;
            PrefabUtility.SaveAsPrefabAsset(column, ColumnPrefabPath);
            Object.DestroyImmediate(column);
        }

        // ---------------------------------------------------------------------
        // 5. Escena: mapa, jugador y cámara (sobre Gameplay.unity, sin tocar el resto de objetos)
        // ---------------------------------------------------------------------

        static bool OpenGameplayScene(out Scene scene)
        {
            scene = SceneManager.GetActiveScene();
            if (scene.path == GameplayScenePath)
                return true;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return false;
            scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            return scene.IsValid();
        }

        static GameObject RecreateRoot(Scene scene, string name)
        {
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name == name)
                    Object.DestroyImmediate(go);
            }
            var root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, scene);
            return root;
        }

        static GameObject CreateChild(Transform parent, string name, int layer = 0)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(parent, false);
            return go;
        }

        static MapBounds BuildMap(Scene scene)
        {
            var map = RecreateRoot(scene, "Map");
            var bounds = map.AddComponent<MapBounds>();
            Rect rect = new Rect(-MapSize * 0.5f, MapSize);

            // Suelo y cuadrícula para percibir el desplazamiento
            var floor = CreateChild(map.transform, "Floor");
            floor.transform.localScale = new Vector3(MapSize.x, MapSize.y, 1f);
            AddSprite(floor, SquareSpritePath, FloorColor, -100);

            var grid = CreateChild(map.transform, "Grid");
            for (float x = rect.xMin; x <= rect.xMax + 0.01f; x += GridSpacing)
            {
                var line = CreateChild(grid.transform, $"V {x:0}");
                line.transform.localPosition = new Vector3(x, 0f, 0f);
                line.transform.localScale = new Vector3(0.06f, MapSize.y, 1f);
                AddSprite(line, SquareSpritePath, GridColor, -99);
            }
            for (float y = rect.yMin; y <= rect.yMax + 0.01f; y += GridSpacing)
            {
                var line = CreateChild(grid.transform, $"H {y:0}");
                line.transform.localPosition = new Vector3(0f, y, 0f);
                line.transform.localScale = new Vector3(MapSize.x, 0.06f, 1f);
                AddSprite(line, SquareSpritePath, GridColor, -99);
            }

            // Muros del borde, por fuera del área jugable
            var borders = CreateChild(map.transform, "Borders");
            float t = BorderThickness;
            CreateWall(borders.transform, "North", new Vector2(0f, rect.yMax + t * 0.5f), new Vector2(MapSize.x + 2f * t, t));
            CreateWall(borders.transform, "South", new Vector2(0f, rect.yMin - t * 0.5f), new Vector2(MapSize.x + 2f * t, t));
            CreateWall(borders.transform, "East", new Vector2(rect.xMax + t * 0.5f, 0f), new Vector2(t, MapSize.y));
            CreateWall(borders.transform, "West", new Vector2(rect.xMin - t * 0.5f, 0f), new Vector2(t, MapSize.y));

            // Obstáculos con semilla fija
            var obstacles = CreateChild(map.transform, "Obstacles");
            var rockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RockPrefabPath);
            var columnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ColumnPrefabPath);
            int index = 0;
            foreach (var o in MapLayout.GenerateObstacles(MapLayout.DefaultSettings(rect)))
            {
                bool isRock = o.Kind == MapLayout.ObstacleKind.Rock;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(isRock ? rockPrefab : columnPrefab, scene);
                go.name = $"{(isRock ? "Rock" : "Column")} {index++:00}";
                go.transform.SetParent(obstacles.transform, false);
                go.transform.localPosition = o.Position;
                float scale = isRock ? o.Size * 2f : o.Size;
                go.transform.localScale = new Vector3(scale, scale, 1f);
            }

            // Forma para el Confiner2D de Cinemachine. Desactivada: no participa en la física.
            var cameraBounds = CreateChild(map.transform, "CameraBounds");
            var boundsCollider = cameraBounds.AddComponent<BoxCollider2D>();
            boundsCollider.size = MapSize;
            boundsCollider.isTrigger = true;
            boundsCollider.enabled = false;

            return bounds;
        }

        static void CreateWall(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var wall = CreateChild(parent, name, Layers.Obstacle);
            wall.transform.localPosition = position;
            wall.transform.localScale = new Vector3(size.x, size.y, 1f);
            AddSprite(wall, SquareSpritePath, BorderColor, -10);
            wall.AddComponent<BoxCollider2D>().size = Vector2.one;
        }

        static Transform EnsurePlayer(Scene scene)
        {
            foreach (var go in scene.GetRootGameObjects())
            {
                var movement = go.GetComponentInChildren<Player.PlayerMovement>(true);
                if (movement != null)
                    return movement.transform;
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            player.transform.position = Vector3.zero;
            return player.transform;
        }

        // Prefab genérico: Enemy.Activate le da forma, color, tamaño y masa según la definición.
        static void BuildEnemyPrefab()
        {
            var root = new GameObject("Enemy") { layer = Layers.Enemy };
            AddSprite(root, CircleSpritePath, Color.white, 0);
            root.AddComponent<Rigidbody2D>();
            root.AddComponent<CircleCollider2D>().radius = 0.5f;
            var enemy = root.AddComponent<Enemy>();

            var so = new SerializedObject(enemy);
            so.FindProperty("circleSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(CircleSpritePath);
            so.FindProperty("squareSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(SquareSpritePath);
            so.FindProperty("triangleSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(TriangleSpritePath);
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
            Object.DestroyImmediate(root);
        }

        static void BuildEnemySystems(Scene scene, MapBounds mapBounds, Transform player)
        {
            var root = RecreateRoot(scene, "Enemies");

            var definitions = new[]
            {
                AssetDatabase.LoadAssetAtPath<EnemyDefinition>(EnemyDefinitionPath("Rondador")),
                AssetDatabase.LoadAssetAtPath<EnemyDefinition>(EnemyDefinitionPath("Corredor")),
                AssetDatabase.LoadAssetAtPath<EnemyDefinition>(EnemyDefinitionPath("Tirador")),
                AssetDatabase.LoadAssetAtPath<EnemyDefinition>(EnemyDefinitionPath("Coloso")),
            };

            var pool = root.AddComponent<Pooling.EnemyPool>();
            var poolSo = new SerializedObject(pool);
            poolSo.FindProperty("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Enemy>(EnemyPrefabPath);
            var defsProp = poolSo.FindProperty("definitions");
            defsProp.arraySize = definitions.Length;
            for (int i = 0; i < definitions.Length; i++)
                defsProp.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];
            poolSo.ApplyModifiedPropertiesWithoutUndo();

            var flowField = root.AddComponent<Navigation.FlowFieldProvider>();
            var flowSo = new SerializedObject(flowField);
            flowSo.FindProperty("map").objectReferenceValue = mapBounds;
            flowSo.FindProperty("target").objectReferenceValue = player;
            flowSo.ApplyModifiedPropertiesWithoutUndo();

            var manager = root.AddComponent<EnemyManager>();
            var managerSo = new SerializedObject(manager);
            managerSo.FindProperty("player").objectReferenceValue = player;
            managerSo.FindProperty("pool").objectReferenceValue = pool;
            managerSo.FindProperty("flowField").objectReferenceValue = flowField;
            managerSo.ApplyModifiedPropertiesWithoutUndo();

            BuildGameSystems(scene, mapBounds, manager, definitions[2]);
        }

        // Reloj de la partida y spawner de oleadas.
        static void BuildGameSystems(Scene scene, MapBounds mapBounds, EnemyManager manager, EnemyDefinition tirador)
        {
            var root = RecreateRoot(scene, "Game");
            var clock = root.AddComponent<GameClock>();

            var spawner = root.AddComponent<WaveSpawner>();
            var so = new SerializedObject(spawner);
            so.FindProperty("curve").objectReferenceValue = AssetDatabase.LoadAssetAtPath<SpawnCurve>(SpawnCurvePath);
            so.FindProperty("clock").objectReferenceValue = clock;
            so.FindProperty("enemies").objectReferenceValue = manager;
            so.FindProperty("map").objectReferenceValue = mapBounds;
            var lineTypes = so.FindProperty("pincerLineTypes");
            lineTypes.arraySize = 1;
            lineTypes.GetArrayElementAtIndex(0).objectReferenceValue = tirador;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildCameraRig(Scene scene, MapBounds mapBounds, Transform player)
        {
            // Cámara real: ortográfica, con el Brain de Cinemachine
            Camera mainCamera = null;
            foreach (var go in scene.GetRootGameObjects())
            {
                mainCamera = go.GetComponentInChildren<Camera>(true);
                if (mainCamera != null && mainCamera.CompareTag("MainCamera")) break;
                mainCamera = null;
            }
            if (mainCamera == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                SceneManager.MoveGameObjectToScene(go, scene);
                mainCamera = go.AddComponent<Camera>();
            }
            mainCamera.orthographic = true;
            mainCamera.orthographicSize = OrthoSize;
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = new Color32(0x15, 0x15, 0x18, 0xFF);
            mainCamera.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);
            if (!mainCamera.TryGetComponent(out CinemachineBrain _))
                mainCamera.gameObject.AddComponent<CinemachineBrain>();

            // Cámara virtual que sigue al jugador
            var rig = RecreateRoot(scene, "CM PlayerCamera");
            rig.transform.position = new Vector3(player.position.x, player.position.y, -10f);

            var cmCamera = rig.AddComponent<CinemachineCamera>();
            cmCamera.Target.TrackingTarget = player;
            var lens = LensSettings.Default;
            lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
            lens.OrthographicSize = OrthoSize;
            cmCamera.Lens = lens;

            var composer = rig.AddComponent<CinemachinePositionComposer>();
            composer.CameraDistance = 10f;
            composer.Damping = new Vector3(FollowDamping, FollowDamping, 0f);
            composer.Lookahead = new LookaheadSettings
            {
                Enabled = true,
                Time = LookaheadTime,
                Smoothing = LookaheadSmoothing,
                IgnoreY = false,
            };

            var confiner = rig.AddComponent<CinemachineConfiner2D>();
            confiner.BoundingShape2D = mapBounds.transform.Find("CameraBounds").GetComponent<BoxCollider2D>();
            confiner.Damping = 0f;

            // Sacudida: impulso uniforme que recibe el listener de esta cámara
            var listener = rig.AddComponent<CinemachineImpulseListener>();
            listener.UseCameraSpace = true;
            var source = rig.AddComponent<CinemachineImpulseSource>();
            source.ImpulseDefinition.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
            source.ImpulseDefinition.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump;
            var shake = rig.AddComponent<CameraShake>();
            var so = new SerializedObject(shake);
            so.FindProperty("player").objectReferenceValue = player.GetComponent<Player.PlayerHealth>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static SpriteRenderer AddSprite(GameObject go, string spritePath, Color color, int sortingOrder)
        {
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            var material = AssetDatabase.LoadAssetAtPath<Material>(UnlitSpriteMaterialPath);
            if (material != null)
                renderer.sharedMaterial = material;
            return renderer;
        }

        // Fracción del píxel cubierta por la forma (4 x 4 submuestras para suavizar los bordes).
        static float Coverage(Shape shape, int px, int py)
        {
            const int sub = 4;
            int inside = 0;
            for (int sy = 0; sy < sub; sy++)
            for (int sx = 0; sx < sub; sx++)
            {
                float u = (px + (sx + 0.5f) / sub) / SpriteSize;
                float v = (py + (sy + 0.5f) / sub) / SpriteSize;
                if (Inside(shape, u, v)) inside++;
            }
            return inside / (float)(sub * sub);
        }

        static bool Inside(Shape shape, float u, float v)
        {
            switch (shape)
            {
                case Shape.Circle:
                    float dx = u - 0.5f, dy = v - 0.5f;
                    return dx * dx + dy * dy <= 0.25f;
                case Shape.Triangle:
                    // Apunta hacia +Y: base abajo, vértice arriba en el centro.
                    float halfWidth = 0.5f * (1f - v);
                    return Mathf.Abs(u - 0.5f) <= halfWidth;
                default:
                    return true;
            }
        }
    }
}
