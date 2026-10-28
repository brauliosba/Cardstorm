using System.IO;
using Cardstorm.Core;
using UnityEditor;
using UnityEngine;

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

        const string UnlitSpriteMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

        const int SpriteSize = 64;

        [MenuItem("Cardstorm/Greybox/Construir greybox")]
        public static void BuildAll()
        {
            ApplyLayersAndPhysics();
            CreateGreyboxSprites();
            BuildPlayerPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("[GreyboxBuilder] Greybox construido.");
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
