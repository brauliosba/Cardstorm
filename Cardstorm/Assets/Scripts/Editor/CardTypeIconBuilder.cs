using Cardstorm.Cards;
using Cardstorm.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Cardstorm.EditorTools
{
    // Añade el icono de tipo a las cartas del HUD, de la tienda y de la recompensa.
    // Solo crea lo que falta y no toca lo que ya está en los prefabs; se puede borrar después.
    public static class CardTypeIconBuilder
    {
        const string AssetPath = "Assets/Data/Cards/CardTypeIcons.asset";
        const string SpriteFolder = "Assets/Art/Icons/Types";
        const string HudPath = "Assets/Prefabs/UI/HUD.prefab";
        const string ShopSlotPath = "Assets/Prefabs/UI/ShopSlot.prefab";
        const string RewardPath = "Assets/Prefabs/UI/RewardCanvas.prefab";

        // 11 px a ×3. El icono de carta pasa de 56 a 64 (×4) y el de la tienda de 40 a 48 (×3), para que los píxeles sean iguales.
        const float TypeIconSize = 33f;
        const float HudIconOld = 56f, HudIconNew = 64f;
        const float ShopIconOld = 40f, ShopIconNew = 48f;

        [MenuItem("Cardstorm/UI/Añadir iconos de tipo")]
        public static void Build()
        {
            var icons = LoadOrCreateIcons();
            EditPrefab(HudPath, root =>
            {
                foreach (var view in root.GetComponentsInChildren<CardSlotView>(true))
                    Wire(view, icons, "art", new Vector2(0f, 1f), new Vector2(6f, -6f), HudIconOld, HudIconNew);
            });
            EditPrefab(ShopSlotPath, root =>
            {
                foreach (var view in root.GetComponentsInChildren<ShopSlotView>(true))
                    Wire(view, icons, null, new Vector2(0f, 1f), new Vector2(56f, -14f), ShopIconOld, ShopIconNew);
            });
            EditPrefab(RewardPath, root =>
            {
                foreach (var view in root.GetComponentsInChildren<ShopSlotView>(true))
                    Wire(view, icons, null, new Vector2(0f, 1f), new Vector2(56f, -14f), ShopIconOld, ShopIconNew);
            });
            AssetDatabase.SaveAssets();
            Debug.Log("[CardTypeIconBuilder] Iconos de tipo añadidos.");
        }

        static CardTypeIcons LoadOrCreateIcons()
        {
            var icons = AssetDatabase.LoadAssetAtPath<CardTypeIcons>(AssetPath);
            if (icons == null)
            {
                icons = ScriptableObject.CreateInstance<CardTypeIcons>();
                AssetDatabase.CreateAsset(icons, AssetPath);
            }
            if (icons.attack == null) icons.attack = LoadSprite("Attack");
            if (icons.defense == null) icons.defense = LoadSprite("Defense");
            if (icons.mobility == null) icons.mobility = LoadSprite("Mobility");
            if (icons.summon == null) icons.summon = LoadSprite("Summon");
            if (icons.power == null) icons.power = LoadSprite("Power");
            EditorUtility.SetDirty(icons);
            return icons;
        }

        static Sprite LoadSprite(string type)
        {
            string path = $"{SpriteFolder}/Type_{type}.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) Debug.LogWarning($"[CardTypeIconBuilder] Falta el sprite {path}.");
            return sprite;
        }

        static void EditPrefab(string path, System.Action<GameObject> edit)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
            {
                Debug.LogWarning($"[CardTypeIconBuilder] No se encuentra {path}.");
                return;
            }
            try
            {
                edit(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // parentField: campo con la Image bajo la que va el icono; null, el padre del icono de carta.
        static void Wire(Component view, CardTypeIcons icons, string parentField, Vector2 corner, Vector2 offset,
            float oldIconSize, float newIconSize)
        {
            var so = new SerializedObject(view);
            var iconsProp = so.FindProperty("typeIcons");
            if (iconsProp.objectReferenceValue == null)
                iconsProp.objectReferenceValue = icons;

            var cardIcon = so.FindProperty("icon").objectReferenceValue as Image;
            if (cardIcon != null)
            {
                cardIcon.preserveAspect = true;
                var r = cardIcon.rectTransform;
                if (Mathf.Approximately(r.sizeDelta.x, oldIconSize) && Mathf.Approximately(r.sizeDelta.y, oldIconSize))
                    r.sizeDelta = new Vector2(newIconSize, newIconSize);
            }

            var typeProp = so.FindProperty("typeIcon");
            if (typeProp.objectReferenceValue == null)
            {
                Transform parent = parentField != null
                    ? (so.FindProperty(parentField).objectReferenceValue as Component)?.transform
                    : cardIcon != null ? cardIcon.transform.parent : null;
                if (parent == null)
                {
                    Debug.LogWarning($"[CardTypeIconBuilder] {view.name}: no hay dónde poner el icono de tipo.");
                }
                else
                {
                    var existing = parent.Find("TypeIcon");
                    var image = existing != null ? existing.GetComponent<Image>() : CreateTypeIcon(parent, corner, offset);
                    typeProp.objectReferenceValue = image;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Image CreateTypeIcon(Transform parent, Vector2 corner, Vector2 offset)
        {
            var go = new GameObject("TypeIcon", typeof(RectTransform), typeof(Image));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = corner;
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(TypeIconSize, TypeIconSize);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.enabled = false;
            return image;
        }
    }
}
