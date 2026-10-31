using System.IO;
using Cardstorm.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cardstorm.EditorTools
{
    // Genera el greybox de forma reproducible e idempotente: sprites y el mapa de Gameplay.unity
    // (los obstáculos se instancian a partir de los prefabs Obstacle_Rock y Obstacle_Column).
    // En la escena solo se recrea el objeto raíz "Map"; el resto de objetos no se toca.
    public static class GreyboxBuilder
    {
        public const string ArtFolder = "Assets/Art/Greybox";
        public const string CircleSpritePath = ArtFolder + "/Greybox_Circle.png";
        public const string SquareSpritePath = ArtFolder + "/Greybox_Square.png";
        public const string TriangleSpritePath = ArtFolder + "/Greybox_Triangle.png";
        public const string Arc120SpritePath = ArtFolder + "/Greybox_Arc120.png";
        public const string DiamondSpritePath = ArtFolder + "/Greybox_Diamond.png";

        public const string PrefabFolder = "Assets/Prefabs";
        public const string RockPrefabPath = PrefabFolder + "/Obstacle_Rock.prefab";
        public const string ColumnPrefabPath = PrefabFolder + "/Obstacle_Column.prefab";

        public const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";

        const string UnlitSpriteMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

        const int SpriteSize = 64;

        // Mapa
        static readonly Vector2 MapSize = new(120f, 120f);
        const float BorderThickness = 2f;
        const float GridSpacing = 10f;
        static readonly Color FloorColor = new Color32(0x2B, 0x2B, 0x30, 0xFF);
        static readonly Color GridColor = new Color32(0x36, 0x36, 0x3C, 0xFF);
        static readonly Color BorderColor = new Color32(0x4A, 0x4A, 0x52, 0xFF);

        [MenuItem("Cardstorm/Greybox/Construir greybox")]
        public static void BuildAll()
        {
            CreateGreyboxSprites();
            AssetDatabase.SaveAssets();

            if (!OpenGameplayScene(out Scene scene))
            {
                Debug.LogWarning("[GreyboxBuilder] Construcción del mapa cancelada.");
                return;
            }
            BuildMap(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[GreyboxBuilder] Greybox construido.");
        }

        // ---------------------------------------------------------------------
        // Sprites greybox (blancos, 64 x 64 px, 64 PPU = 1 unidad; el arco, 32 PPU = radio 1)
        // ---------------------------------------------------------------------

        enum Shape { Circle, Square, Triangle, Arc120, Diamond }

        static void CreateGreyboxSprites()
        {
            Directory.CreateDirectory(ArtFolder);
            CreateSpriteIfMissing(CircleSpritePath, Shape.Circle);
            CreateSpriteIfMissing(SquareSpritePath, Shape.Square);
            CreateSpriteIfMissing(TriangleSpritePath, Shape.Triangle);
            CreateSpriteIfMissing(Arc120SpritePath, Shape.Arc120, SpriteSize / 2);
            CreateSpriteIfMissing(DiamondSpritePath, Shape.Diamond);
        }

        static void CreateSpriteIfMissing(string path, Shape shape, int pixelsPerUnit = SpriteSize)
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
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
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
                case Shape.Arc120:
                    // Sector de 120° con el vértice en el centro (pivote) apuntando hacia +X.
                    float cu = u * 2f - 1f, cv = v * 2f - 1f;
                    return cu * cu + cv * cv <= 1f && Mathf.Abs(Mathf.Atan2(cv, cu)) <= 60f * Mathf.Deg2Rad;
                case Shape.Diamond:
                    // Rombo con los vértices en el centro de cada lado.
                    return Mathf.Abs(u - 0.5f) + Mathf.Abs(v - 0.5f) <= 0.5f;
                default:
                    return true;
            }
        }

        // ---------------------------------------------------------------------
        // Mapa en Gameplay.unity
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

        static void BuildMap(Scene scene)
        {
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name == "Map")
                    Object.DestroyImmediate(go);
            }
            var map = new GameObject("Map");
            SceneManager.MoveGameObjectToScene(map, scene);
            map.AddComponent<MapBounds>();
            Rect rect = new(-MapSize * 0.5f, MapSize);

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
            if (rockPrefab == null || columnPrefab == null)
                Debug.LogWarning($"[GreyboxBuilder] Faltan los prefabs {RockPrefabPath} o {ColumnPrefabPath}: el mapa se construye sin obstáculos.");
            int index = 0;
            foreach (var o in MapLayout.GenerateObstacles(MapLayout.DefaultSettings(rect)))
            {
                bool isRock = o.Kind == MapLayout.ObstacleKind.Rock;
                var prefab = isRock ? rockPrefab : columnPrefab;
                if (prefab == null) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
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
        }

        static GameObject CreateChild(Transform parent, string name, int layer = 0)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(parent, false);
            return go;
        }

        static void CreateWall(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var wall = CreateChild(parent, name, Layers.Obstacle);
            wall.transform.localPosition = position;
            wall.transform.localScale = new Vector3(size.x, size.y, 1f);
            AddSprite(wall, SquareSpritePath, BorderColor, -10);
            wall.AddComponent<BoxCollider2D>().size = Vector2.one;
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
    }
}
