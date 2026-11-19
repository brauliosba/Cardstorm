using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

namespace Cardstorm.EditorTools
{
    // Crea el font asset SDF de Kenney Mini Square (admite contorno, sombra y negrita) y lo pone en
    // los textos de los prefabs de UI y de Gameplay.unity que aún usan Liberation Sans o la versión
    // ráster anterior, que se borra. Idempotente: el resto de textos no se toca.
    public static class PixelFontApplier
    {
        const string FontPath = "Assets/Art/Kenney/Fonts/KenneyMiniSquare.ttf";
        const string FontAssetPath = "Assets/Art/Kenney/Fonts/KenneyMiniSquare SDF.asset";
        const string RasterAssetPath = "Assets/Art/Kenney/Fonts/KenneyMiniSquare.asset";
        const string LiberationPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        const string PrefabFolder = "Assets/Prefabs";
        const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";

        // La fuente está dibujada a 8 px: los tamaños múltiplos de 8 quedan nítidos a 1080p.
        const int PixelSize = 8;
        // 8 texels por píxel de la fuente; el padding limita el grosor máximo del contorno.
        const int SamplingSize = PixelSize * 8;
        const int Padding = 8;
        const int AtlasSize = 1024;
        const string Prepopulate =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
            "áéíóúüñÁÉÍÓÚÜÑ¿¡«»°…·×";

        [MenuItem("Cardstorm/UI/Aplicar fuente pixel")]
        public static void Apply()
        {
            var liberation = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LiberationPath);
            var raster = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RasterAssetPath);
            var pixel = GetOrCreateFontAsset(liberation);
            if (pixel == null) return;

            var oldFonts = new HashSet<TMP_FontAsset> { liberation };
            if (raster != null) oldFonts.Add(raster);

            SetDefaultFont(pixel);

            int changed = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                int n = Swap(root, oldFonts, pixel);
                if (n > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
                changed += n;
            }

            var scene = SceneManager.GetSceneByPath(GameplayScenePath);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Additive);
            int inScene = 0;
            foreach (var go in scene.GetRootGameObjects()) inScene += Swap(go, oldFonts, pixel);
            if (inScene > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            if (opened) EditorSceneManager.CloseScene(scene, true);

            if (raster != null) AssetDatabase.DeleteAsset(RasterAssetPath);

            AssetDatabase.SaveAssets();
            Debug.Log($"Fuente pixel aplicada a {changed} textos en prefabs y {inScene} en la escena.");
        }

        static TMP_FontAsset GetOrCreateFontAsset(TMP_FontAsset fallback)
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null) return existing;

            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (font == null)
            {
                Debug.LogError($"No se encuentra {FontPath}");
                return null;
            }

            var asset = TMP_FontAsset.CreateFontAsset(font, SamplingSize, Padding, GlyphRenderMode.SDFAA,
                AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic, false);
            asset.name = "KenneyMiniSquare SDF";
            AssetDatabase.CreateAsset(asset, FontAssetPath);

            var atlas = asset.atlasTexture;
            atlas.name = "KenneyMiniSquare SDF Atlas";
            AssetDatabase.AddObjectToAsset(atlas, asset);

            // CreateFontAsset pone el shader Mobile; el completo tiene contorno, sombra y brillo.
            var material = asset.material;
            material.name = "KenneyMiniSquare SDF Material";
            material.shader = Shader.Find("TextMeshPro/Distance Field");
            AssetDatabase.AddObjectToAsset(material, asset);

            asset.TryAddCharacters(Prepopulate, out _);

            // Para los glifos que no tiene (p. ej. el signo menos)
            if (fallback != null) asset.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        static void SetDefaultFont(TMP_FontAsset pixel)
        {
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
            if (settings == null) return;
            var so = new SerializedObject(settings);
            so.FindProperty("m_defaultFontAsset").objectReferenceValue = pixel;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Los textos de prefabs anidados se cambian en su propio prefab, no aquí.
        static int Swap(GameObject root, HashSet<TMP_FontAsset> oldFonts, TMP_FontAsset pixel)
        {
            int n = 0;
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.font == null || !oldFonts.Contains(text.font)) continue;
                if (PrefabUtility.IsPartOfPrefabInstance(text)) continue;

                text.font = pixel;
                text.fontSharedMaterial = pixel.material;
                // La cursiva inclina los píxeles
                text.fontStyle &= ~FontStyles.Italic;
                text.fontSize = Snap(text.fontSize);
                if (text.enableAutoSizing)
                {
                    text.fontSizeMin = Snap(text.fontSizeMin);
                    text.fontSizeMax = Snap(text.fontSizeMax);
                }
                EditorUtility.SetDirty(text);
                n++;
            }
            return n;
        }

        // Al múltiplo de 8 más cercano; en empate, hacia abajo para no desbordar.
        static float Snap(float size)
        {
            float snapped = Mathf.Ceil(size / PixelSize - 0.5f) * PixelSize;
            return Mathf.Max(PixelSize * 2, snapped);
        }
    }
}
