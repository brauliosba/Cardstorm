using System.Collections.Generic;
using System.IO;
using Cardstorm.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Cardstorm.EditorTools
{
    // Genera el greybox de forma reproducible e idempotente: sprites y el mapa de Gameplay.unity
    // (suelo y muros en tilemaps con los tiles de Tiny Dungeon; obstáculos según MapLayout).
    // En la escena solo se recrean los hijos de "Map"; Map (MapBounds) y CameraBounds se conservan
    // porque otros objetos los referencian. El resto de objetos no se toca.
    public static class GreyboxBuilder
    {
        public const string ArtFolder = "Assets/Art/Greybox";
        public const string CircleSpritePath = ArtFolder + "/Greybox_Circle.png";
        public const string SquareSpritePath = ArtFolder + "/Greybox_Square.png";
        public const string TriangleSpritePath = ArtFolder + "/Greybox_Triangle.png";
        public const string Arc120SpritePath = ArtFolder + "/Greybox_Arc120.png";
        public const string DiamondSpritePath = ArtFolder + "/Greybox_Diamond.png";
        public const string RingSpritePath = ArtFolder + "/Greybox_Ring.png";

        const string TileSpriteFolder = "Assets/Art/Kenney/TinyDungeon/Tiles";
        public const string MapTileFolder = "Assets/Art/Map";

        public const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";

        const string UnlitSpriteMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

        const int SpriteSize = 64;

        // Mapa (1 celda = 1 unidad = un tile de 16 px)
        static readonly Vector2Int MapSize = new(120, 120);
        const int BorderThickness = 2;
        const int FloorSeed = 2026;
        const float FloorVariantChance = 0.03f;
        // Suelo oscurecido para que personajes y proyectiles destaquen
        static readonly Color FloorTint = new(0.75f, 0.75f, 0.75f, 1f);
        const int FloorTile = 0;
        static readonly int[] FloorVariantTiles = { 12, 24 };
        const int WallTile = 40;

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
        // Sprites greybox (blancos, 64 x 64 px, 64 PPU = 1 unidad; el arco y el anillo, 32 PPU = radio 1)
        // ---------------------------------------------------------------------

        enum Shape { Circle, Square, Triangle, Arc120, Diamond, Ring }

        static void CreateGreyboxSprites()
        {
            Directory.CreateDirectory(ArtFolder);
            CreateSpriteIfMissing(CircleSpritePath, Shape.Circle);
            CreateSpriteIfMissing(SquareSpritePath, Shape.Square);
            CreateSpriteIfMissing(TriangleSpritePath, Shape.Triangle);
            CreateSpriteIfMissing(Arc120SpritePath, Shape.Arc120, SpriteSize / 2);
            CreateSpriteIfMissing(DiamondSpritePath, Shape.Diamond);
            CreateSpriteIfMissing(RingSpritePath, Shape.Ring, SpriteSize / 2);
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
                case Shape.Ring:
                    // Corona circular de radio exterior 1 e interior 0,7, centrada en el pivote.
                    float ru = u * 2f - 1f, rv = v * 2f - 1f;
                    float r2 = ru * ru + rv * rv;
                    return r2 <= 1f && r2 >= 0.49f;
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
            GameObject map = null;
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name == "Map") { map = go; break; }
            }
            if (map == null)
            {
                map = new GameObject("Map");
                SceneManager.MoveGameObjectToScene(map, scene);
            }
            if (!map.TryGetComponent<MapBounds>(out _))
                map.AddComponent<MapBounds>();

            for (int i = map.transform.childCount - 1; i >= 0; i--)
            {
                var child = map.transform.GetChild(i).gameObject;
                if (child.name != "CameraBounds")
                    Object.DestroyImmediate(child);
            }

            var floorTile = LoadOrCreateTile("Floor", FloorTile, Tile.ColliderType.None);
            var variantTiles = new TileBase[FloorVariantTiles.Length];
            for (int i = 0; i < variantTiles.Length; i++)
                variantTiles[i] = LoadOrCreateTile($"Floor_{i + 1}", FloorVariantTiles[i], Tile.ColliderType.None);
            var wallTile = LoadOrCreateTile("Wall", WallTile, Tile.ColliderType.Grid);

            var gridObject = CreateChild(map.transform, "Grid");
            gridObject.AddComponent<Grid>().cellSize = Vector3.one;

            // Área jugable: celdas de -60 a 59 en cada eje
            var rect = new RectInt(-MapSize / 2, MapSize);

            // Suelo con alguna variante suelta, siempre con la misma semilla
            var floor = CreateTilemap(gridObject.transform, "Floor", 0, -100, FloorTint);
            var random = new System.Random(FloorSeed);
            var floorCells = new List<Vector3Int>(rect.width * rect.height);
            var floorTiles = new List<TileBase>(rect.width * rect.height);
            foreach (var cell in rect.allPositionsWithin)
            {
                floorCells.Add((Vector3Int)cell);
                floorTiles.Add(random.NextDouble() < FloorVariantChance
                    ? variantTiles[random.Next(variantTiles.Length)]
                    : floorTile);
            }
            floor.SetTiles(floorCells.ToArray(), floorTiles.ToArray());

            // Muros: borde por fuera del área jugable y obstáculos con semilla fija, con un solo collider compuesto
            var walls = CreateTilemap(gridObject.transform, "Walls", Layers.Obstacle, -10, Color.white);
            var wallCells = new HashSet<Vector3Int>();
            for (int y = rect.yMin - BorderThickness; y < rect.yMax + BorderThickness; y++)
            for (int x = rect.xMin - BorderThickness; x < rect.xMax + BorderThickness; x++)
            {
                if (!rect.Contains(new Vector2Int(x, y)))
                    wallCells.Add(new Vector3Int(x, y, 0));
            }
            var obstacleCells = new List<Vector2Int>();
            var obstacleRect = new Rect(rect.position, rect.size);
            foreach (var o in MapLayout.GenerateObstacles(MapLayout.DefaultSettings(obstacleRect)))
            {
                MapLayout.GetCells(o, obstacleCells);
                foreach (var cell in obstacleCells)
                    wallCells.Add((Vector3Int)cell);
            }
            var wallArray = new Vector3Int[wallCells.Count];
            wallCells.CopyTo(wallArray);
            var wallTiles = new TileBase[wallArray.Length];
            for (int i = 0; i < wallTiles.Length; i++) wallTiles[i] = wallTile;
            walls.SetTiles(wallArray, wallTiles);

            var body = walls.gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            var tilemapCollider = walls.gameObject.AddComponent<TilemapCollider2D>();
            tilemapCollider.compositeOperation = Collider2D.CompositeOperation.Merge;
            // Polígonos y no contornos: OverlapCircle y OverlapBox tienen que detectar el interior
            var composite = walls.gameObject.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            tilemapCollider.ProcessTilemapChanges();
            composite.GenerateGeometry();

            // Forma para el Confiner2D de Cinemachine. Desactivada: no participa en la física.
            var cameraBounds = map.transform.Find("CameraBounds");
            if (cameraBounds == null)
            {
                cameraBounds = CreateChild(map.transform, "CameraBounds").transform;
                var boundsCollider = cameraBounds.gameObject.AddComponent<BoxCollider2D>();
                boundsCollider.size = MapSize;
                boundsCollider.isTrigger = true;
                boundsCollider.enabled = false;
            }
            cameraBounds.SetAsLastSibling();
        }

        // Crea el Tile del sprite tile_NNNN si no existe; si existe, lo reutiliza sin tocarlo.
        static Tile LoadOrCreateTile(string name, int spriteIndex, Tile.ColliderType colliderType)
        {
            string path = $"{MapTileFolder}/{name}.asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile != null) return tile;

            if (!AssetDatabase.IsValidFolder(MapTileFolder))
                AssetDatabase.CreateFolder("Assets/Art", "Map");
            string spritePath = $"{TileSpriteFolder}/tile_{spriteIndex:0000}.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite == null)
                Debug.LogWarning($"[GreyboxBuilder] Falta el sprite {spritePath}.");

            tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.colliderType = colliderType;
            AssetDatabase.CreateAsset(tile, path);
            return tile;
        }

        static Tilemap CreateTilemap(Transform grid, string name, int layer, int sortingOrder, Color color)
        {
            var go = CreateChild(grid, name, layer);
            var tilemap = go.AddComponent<Tilemap>();
            tilemap.color = color;
            var renderer = go.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = sortingOrder;
            var material = AssetDatabase.LoadAssetAtPath<Material>(UnlitSpriteMaterialPath);
            if (material != null)
                renderer.sharedMaterial = material;
            return tilemap;
        }

        static GameObject CreateChild(Transform parent, string name, int layer = 0)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(parent, false);
            return go;
        }
    }
}
