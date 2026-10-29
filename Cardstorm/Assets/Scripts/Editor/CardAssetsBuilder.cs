using System.Collections.Generic;
using Cardstorm.Cards;
using Cardstorm.Cards.Effects;
using UnityEditor;
using UnityEngine;

namespace Cardstorm.EditorTools
{
    // Crea las cartas y el mazo inicial. Solo crea los assets que faltan, para no pisar valores ya ajustados.
    public static class CardAssetsBuilder
    {
        public const string CardsFolder = "Assets/Data/Cards";
        public const string DisparoPath = CardsFolder + "/Card_Disparo.asset";
        public const string TajoPath = CardsFolder + "/Card_Tajo.asset";
        public const string EsquivarPath = CardsFolder + "/Card_Esquivar.asset";
        public const string StartingDeckPath = CardsFolder + "/Deck_Inicial.asset";

        [MenuItem("Cardstorm/Datos/Crear mazo inicial")]
        public static void BuildStartingDeck()
        {
            if (!AssetDatabase.IsValidFolder(CardsFolder))
                AssetDatabase.CreateFolder("Assets/Data", "Cards");

            var created = new List<string>();

            var disparo = LoadOrCreate(DisparoPath, created, card =>
            {
                Fill(card, "disparo", "Disparo", CardType.Attack, 1f, TargetPriority.Nearest, 12f, 10f,
                    "Lanza un proyectil al enemigo más cercano que inflige 10 de daño.");
                card.effects.Add(new ProjectileEffect { damage = 10f });
            });

            var tajo = LoadOrCreate(TajoPath, created, card =>
            {
                Fill(card, "tajo", "Tajo", CardType.Attack, 2f, TargetPriority.Nearest, 12f, 22.9f,
                    "Golpe en arco de 120° hacia el enemigo más cercano: 18 de daño a todos los alcanzados.");
                card.effects.Add(new ArcEffect { damage = 18f, range = 2.5f, arcDegrees = 120f });
            });

            var esquivar = LoadOrCreate(EsquivarPath, created, card =>
            {
                Fill(card, "esquivar", "Esquivar", CardType.Mobility, 1f, TargetPriority.MoveDirection, 0f, 10f,
                    "Desplazamiento rápido de 4 unidades en la dirección del movimiento, con 0,3 s de invulnerabilidad.");
                card.effects.Add(new DashEffect { distance = 4f, duration = 0.15f, invulnerability = 0.3f });
            });

            if (AssetDatabase.LoadAssetAtPath<DeckDefinition>(StartingDeckPath) == null)
            {
                var deck = ScriptableObject.CreateInstance<DeckDefinition>();
                deck.cards.Add(new DeckDefinition.Entry { card = disparo, copies = 4 });
                deck.cards.Add(new DeckDefinition.Entry { card = tajo, copies = 2 });
                deck.cards.Add(new DeckDefinition.Entry { card = esquivar, copies = 2 });
                AssetDatabase.CreateAsset(deck, StartingDeckPath);
                created.Add(StartingDeckPath);
            }

            AssetDatabase.SaveAssets();

            string report = created.Count > 0
                ? "Creados:\n" + string.Join("\n", created)
                : "Ya existían todos los assets; no se ha cambiado nada.";
            Debug.Log("[CardAssetsBuilder] " + report);
            EditorUtility.DisplayDialog("Mazo inicial", report, "Vale");
        }

        static CardDefinition LoadOrCreate(string path, List<string> created, System.Action<CardDefinition> fill)
        {
            var existing = AssetDatabase.LoadAssetAtPath<CardDefinition>(path);
            if (existing != null) return existing;

            var card = ScriptableObject.CreateInstance<CardDefinition>();
            fill(card);
            AssetDatabase.CreateAsset(card, path);
            created.Add(path);
            return card;
        }

        static void Fill(CardDefinition card, string id, string displayName, CardType type, float cost,
            TargetPriority targeting, float targetRange, float powerValue, string description)
        {
            card.id = id;
            card.displayName = displayName;
            card.description = description;
            card.type = type;
            card.rarity = CardRarity.Basic;
            card.cost = cost;
            card.targeting = targeting;
            card.targetRange = targetRange;
            card.powerValue = powerValue;
            card.icon = AssetDatabase.LoadAssetAtPath<Sprite>(type == CardType.Mobility
                ? GreyboxBuilder.CircleSpritePath
                : GreyboxBuilder.TriangleSpritePath);
        }
    }
}
