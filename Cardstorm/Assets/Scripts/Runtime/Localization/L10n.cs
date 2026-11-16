using System;
using Cardstorm.Cards;
using Cardstorm.Enemies;
using Cardstorm.Progression;
using Cardstorm.UI;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace Cardstorm.Localization
{
    // Textos del idioma activo. Si falta una entrada, devuelve el texto de respaldo y nunca el aviso del paquete.
    public static class L10n
    {
        // Se lanza al cambiar de idioma y al terminar de cargar el primero.
        public static event Action Changed;

        public static StatTexts StatTexts { get; private set; } = StatTexts.Spanish;

        // Sube con cada cambio de idioma: las vistas que guardan lo último dibujado lo comparan.
        public static int Version { get; private set; }

        static StringTable uiTable;
        static StringTable contentTable;
        static int request;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Changed = null;
            StatTexts = StatTexts.Spanish;
            Version = 0;
            uiTable = null;
            contentTable = null;
            request = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook()
        {
            if (!LocalizationSettings.HasSettings) return;
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            LoadTables();
        }

        static void OnLocaleChanged(UnityEngine.Localization.Locale _) => LoadTables();

        // Nunca se espera a una carga: estos avisos pueden llegar mientras el paquete ya está esperando
        // otra. Se piden las dos tablas y los textos se reescriben cuando están.
        static void LoadTables()
        {
            uiTable = null;
            contentTable = null;
            int current = ++request;
            var database = LocalizationSettings.StringDatabase;
            var ui = database.GetTableAsync(L10nKeys.UiTable);
            var content = database.GetTableAsync(L10nKeys.ContentTable);
            ui.Completed += _ => content.Completed += _ =>
            {
                if (current == request) Refresh();
            };
        }

        static void Refresh()
        {
            uiTable = null;
            contentTable = null;
            var es = StatTexts.Spanish;
            StatTexts = new StatTexts(
                Get(L10nKeys.DecimalSeparator, es.DecimalSeparator),
                Get(L10nKeys.StatPercent, es.Percent),
                Get(L10nKeys.StatPerTen, es.PerTen));
            Version++;
            Changed?.Invoke();
        }

        // Texto de la tabla UI. Sin respaldo, devuelve la clave para que se vea qué falta.
        public static string Get(string key, string fallback = null) =>
            Lookup(ref uiTable, L10nKeys.UiTable, key, fallback);

        public static string GetContent(string key, string fallback) =>
            Lookup(ref contentTable, L10nKeys.ContentTable, key, fallback);

        // Para las entradas con nombres: «Sustituye a {0}».
        public static string Format(string key, params object[] args)
        {
            string format = Get(key);
            try { return string.Format(format, args); }
            catch (FormatException) { return format; }
        }

        public static string CardName(CardDefinition card) =>
            card != null ? GetContent(L10nKeys.CardName(card.id), card.displayName) : string.Empty;

        public static string CardDescription(CardDefinition card) =>
            card != null ? GetContent(L10nKeys.CardDescription(card.id), card.description) : string.Empty;

        public static string GemName(GemDefinition gem) =>
            gem != null ? GetContent(L10nKeys.GemName(gem.id), gem.displayName) : string.Empty;

        public static string GemDescription(GemDefinition gem) =>
            gem != null ? GetContent(L10nKeys.GemDescription(gem.id), gem.description) : string.Empty;

        public static string EnemyName(EnemyDefinition enemy) =>
            enemy != null ? GetContent(L10nKeys.EnemyName(enemy.id), enemy.displayName) : string.Empty;

        public static string TypeName(CardType type) => Get(L10nKeys.Type(type));

        public static string RarityName(CardRarity rarity) => Get(L10nKeys.Rarity(rarity));

        public static string StatLabel(StatType stat) => Get(L10nKeys.Stat(stat), stat.ToString());

        static string Lookup(ref StringTable table, string tableName, string key, string fallback)
        {
            if (string.IsNullOrEmpty(key)) return fallback ?? string.Empty;
            if (!LocalizationSettings.HasSettings) return fallback ?? key;

            // Mientras la tabla carga (los primeros fotogramas o tras cambiar de idioma) no hay texto;
            // Changed avisa cuando llega.
            if (table == null)
            {
                var load = LocalizationSettings.StringDatabase.GetTableAsync(tableName);
                if (!load.IsValid() || !load.IsDone) return fallback ?? string.Empty;
                table = load.Result;
            }
            string value = table != null ? table.GetEntry(key)?.Value : null;
            return !string.IsNullOrEmpty(value) ? value : fallback ?? key;
        }
    }
}
