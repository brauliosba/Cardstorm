using Cardstorm.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Cardstorm.EditorTools
{
    // Menú temporal: monta RemovalScreen dentro de ShopCanvas.prefab copiando la ventana de la tienda
    // y una ranura, para que tenga el mismo aspecto. Se borra una vez montada la pantalla.
    public static class RemovalScreenBuilder
    {
        const string ShopCanvasPath = "Assets/Prefabs/UI/ShopCanvas.prefab";
        const string ShopSlotPath = "Assets/Prefabs/UI/ShopSlot.prefab";

        static readonly Vector2 EntrySize = new(260f, 90f);
        const float EntriesWidth = 4 * 260f + 3 * 16f;

        [MenuItem("Cardstorm/UI/Montar pantalla de eliminar carta")]
        public static void Build()
        {
            var slotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShopSlotPath);
            if (slotPrefab == null)
            {
                Debug.LogError($"No se encuentra {ShopSlotPath}.");
                return;
            }

            var contents = PrefabUtility.LoadPrefabContents(ShopCanvasPath);
            try
            {
                if (!BuildInside(contents, slotPrefab)) return;
                PrefabUtility.SaveAsPrefabAsset(contents, ShopCanvasPath);
                Debug.Log("Pantalla de eliminar carta montada en ShopCanvas.prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        static bool BuildInside(GameObject canvas, GameObject slotPrefab)
        {
            var shop = canvas.GetComponentInChildren<ShopScreen>(true);
            var shopRoot = shop != null ? new SerializedObject(shop).FindProperty("root").objectReferenceValue as GameObject : null;
            if (shopRoot == null)
            {
                Debug.LogError("ShopCanvas no tiene ShopScreen con Root asignado.");
                return false;
            }

            // El objeto RemovalScreen, hermano de Root y por detrás en la jerarquía para dibujarse encima.
            var screenTf = canvas.transform.Find("RemovalScreen");
            var screenGo = screenTf != null ? screenTf.gameObject : new GameObject("RemovalScreen", typeof(RectTransform));
            screenGo.transform.SetParent(canvas.transform, false);
            screenGo.transform.SetAsLastSibling();
            for (int i = screenGo.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(screenGo.transform.GetChild(i).gameObject);
            Stretch((RectTransform)screenGo.transform);

            // Root y Window copiados de la tienda.
            var root = Object.Instantiate(shopRoot, screenGo.transform);
            root.name = "Root";
            var window = root.transform.Find("Window");

            DestroyChild(window, "Buttons/RefreshButton");
            DestroyChild(window, "Buttons/RemoveButton");

            var title = Text(window, "Title");
            title.text = "Eliminar carta";

            var price = Text(window, "Queue");
            price.gameObject.name = "Price";
            price.gameObject.SetActive(true);
            price.text = "Cuesta 5 oro";

            var note = Object.Instantiate(price.gameObject, window).GetComponent<TMP_Text>();
            note.gameObject.name = "Note";
            note.text = "Deben quedar al menos 5 cartas de acción";
            note.color = HudColors.NotEnough;
            note.fontSize = price.fontSize * 0.85f;
            var noteRt = note.rectTransform;
            noteRt.sizeDelta = new Vector2(900f, noteRt.sizeDelta.y);
            noteRt.anchoredPosition = price.rectTransform.anchoredPosition + new Vector2(0f, -42f);

            var gold = Text(window, "Gold");

            var back = window.Find("Buttons/ContinueButton").GetComponent<Button>();
            back.gameObject.name = "BackButton";
            back.GetComponentInChildren<TMP_Text>(true).text = "Volver";

            // Lista: la rejilla de las ranuras de la tienda, sin ranuras y más ancha.
            var entries = window.Find("Slots");
            entries.name = "Entries";
            for (int i = entries.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(entries.GetChild(i).gameObject);
            if (entries.TryGetComponent(out LayoutElement entriesLayout)) entriesLayout.preferredWidth = EntriesWidth;
            if (entries.TryGetComponent(out WrapLayoutGroup wrap)) wrap.childAlignment = TextAnchor.UpperCenter;
            var entriesRt = (RectTransform)entries;
            entriesRt.sizeDelta = new Vector2(EntriesWidth, entriesRt.sizeDelta.y);
            entriesRt.anchoredPosition = new Vector2(0f, -10f);

            var entry = BuildEntry(slotPrefab, entries);

            if (!screenGo.TryGetComponent(out RemovalScreen screen)) screen = screenGo.AddComponent<RemovalScreen>();
            var so = new SerializedObject(screen);
            so.FindProperty("root").objectReferenceValue = root;
            so.FindProperty("priceText").objectReferenceValue = price;
            so.FindProperty("goldText").objectReferenceValue = gold;
            so.FindProperty("noteText").objectReferenceValue = note;
            so.FindProperty("entriesParent").objectReferenceValue = entries;
            so.FindProperty("entryTemplate").objectReferenceValue = entry;
            so.FindProperty("backButton").objectReferenceValue = back;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Texto del botón Eliminar de la tienda.
            var removeButton = new SerializedObject(shop).FindProperty("removeButton").objectReferenceValue as Button;
            if (removeButton != null)
            {
                var shopSo = new SerializedObject(shop);
                shopSo.FindProperty("removeText").objectReferenceValue = removeButton.GetComponentInChildren<TMP_Text>(true);
                shopSo.ApplyModifiedPropertiesWithoutUndo();
            }
            return true;
        }

        // Una ranura de la tienda, más baja y sin descripción, rareza ni precio.
        static RemovalEntryView BuildEntry(GameObject slotPrefab, Transform parent)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(slotPrefab, parent);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = "Entry";

            var slotView = go.GetComponent<ShopSlotView>();
            var slotSo = new SerializedObject(slotView);
            Object Ref(string field) => slotSo.FindProperty(field).objectReferenceValue;
            var border = Ref("border");
            var icon = Ref("icon") as Image;
            var nameText = Ref("nameText") as TMP_Text;
            var typeText = Ref("typeText") as TMP_Text;
            var costText = Ref("costText") as TMP_Text;
            var count = Ref("priceText") as TMP_Text;
            var veil = Ref("veil");
            DestroyRef(Ref("descriptionText"));
            DestroyRef(Ref("rarityText"));
            DestroyRef(Ref("boughtLabel"));
            Object.DestroyImmediate(slotView);

            ((RectTransform)go.transform).sizeDelta = EntrySize;
            if (go.TryGetComponent(out LayoutElement layout))
            {
                layout.preferredWidth = EntrySize.x;
                layout.preferredHeight = EntrySize.y;
            }

            // Coste a la izquierda, nombre y tipo en el centro, icono arriba a la derecha y copias abajo.
            if (costText != null)
                Place(costText.transform.parent as RectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(40f, 40f));
            if (nameText != null)
            {
                Place(nameText.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 0f), new Vector2(60f, 0f), new Vector2(-110f, 30f));
                nameText.alignment = TextAlignmentOptions.BottomLeft;
            }
            if (typeText != null)
            {
                Place(typeText.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 1f), new Vector2(60f, -2f), new Vector2(-110f, 22f));
                typeText.alignment = TextAlignmentOptions.TopLeft;
            }
            if (icon != null)
                Place(icon.rectTransform, Vector2.one, Vector2.one, Vector2.one, new Vector2(-10f, -8f), new Vector2(28f, 28f));
            if (count != null)
            {
                count.gameObject.name = "Count";
                count.gameObject.SetActive(true);
                count.text = "×4";
                count.alignment = TextAlignmentOptions.BottomRight;
                Place(count.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-10f, 8f), new Vector2(70f, 30f));
            }

            var view = go.AddComponent<RemovalEntryView>();
            var so = new SerializedObject(view);
            so.FindProperty("border").objectReferenceValue = border;
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("nameText").objectReferenceValue = nameText;
            so.FindProperty("countText").objectReferenceValue = count;
            so.FindProperty("typeText").objectReferenceValue = typeText;
            so.FindProperty("costText").objectReferenceValue = costText;
            so.FindProperty("veil").objectReferenceValue = veil;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            if (rt == null) return;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
        }

        static TMP_Text Text(Transform parent, string path) => parent.Find(path).GetComponent<TMP_Text>();

        static void DestroyChild(Transform parent, string path)
        {
            var child = parent.Find(path);
            if (child != null) Object.DestroyImmediate(child.gameObject);
        }

        static void DestroyRef(Object o)
        {
            var go = o is Component c ? c.gameObject : o as GameObject;
            if (go != null) Object.DestroyImmediate(go);
        }
    }
}
