using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Cardstorm.Spawning;
using UnityEditor;
using UnityEngine;

namespace Cardstorm.EditorTools
{
    // Rellena SpawnCurve_Partida.asset con los CSV exportados de las hojas Tramos y Oleadas de la hoja de equilibrado.
    // Si hay errores o la curva resultante no es válida, no se toca el asset.
    public static class SpawnCurveCsvImporter
    {
        public const string SpawnCurvePath = "Assets/Data/Spawning/SpawnCurve_Partida.asset";
        const string LastFolderKey = "Cardstorm.SpawnCurveCsvImporter.LastFolder";

        [MenuItem("Cardstorm/Datos/Importar curva desde CSV")]
        public static void ImportFromMenu()
        {
            var curve = AssetDatabase.LoadAssetAtPath<SpawnCurve>(SpawnCurvePath);
            if (curve == null)
            {
                EditorUtility.DisplayDialog("Importar curva", $"No existe {SpawnCurvePath}. Crea antes el asset (Create > Cardstorm > Spawn Curve).", "Vale");
                return;
            }

            string folder = EditorPrefs.GetString(LastFolderKey, Application.dataPath);
            string tramosPath = EditorUtility.OpenFilePanel("Tramos.csv", folder, "csv");
            if (string.IsNullOrEmpty(tramosPath)) return;
            folder = Path.GetDirectoryName(tramosPath);
            EditorPrefs.SetString(LastFolderKey, folder);

            string oleadasPath = EditorUtility.OpenFilePanel("Oleadas.csv (Cancelar para no cambiar las oleadas)", folder, "csv");

            bool ok = Import(curve, tramosPath, string.IsNullOrEmpty(oleadasPath) ? null : oleadasPath, out string report);
            EditorUtility.DisplayDialog(ok ? "Curva importada" : "No se ha importado la curva", report, "Vale");
        }

        // También sirve para importar desde código o batchmode. oleadasPath puede ser null para conservar las oleadas.
        public static bool Import(SpawnCurve curve, string tramosPath, string oleadasPath, out string report)
        {
            var typeNames = curve.enemyTypes
                .Select(t => t == null ? string.Empty : string.IsNullOrEmpty(t.displayName) ? t.name : t.displayName)
                .ToArray();
            var errors = new List<string>();
            var warnings = new List<string>();

            var segments = SpawnCurveCsv.ParseSegments(ReadText(tramosPath), typeNames, errors);
            var waves = oleadasPath != null
                ? SpawnCurveCsv.ParseWaves(ReadText(oleadasPath), typeNames, curve.waves, errors, warnings)
                : curve.waves;

            if (errors.Count == 0)
                errors.AddRange(SpawnCurve.Validate(typeNames.Length, curve.groupInterval, segments, waves));

            if (errors.Count > 0)
            {
                report = "Errores:\n• " + string.Join("\n• ", errors.Take(15)) + (errors.Count > 15 ? $"\n… y {errors.Count - 15} más" : "");
                Debug.LogError($"[SpawnCurveCsvImporter] {report}");
                return false;
            }

            Undo.RecordObject(curve, "Importar curva desde CSV");
            curve.segments = segments;
            if (oleadasPath != null) curve.waves = waves;
            EditorUtility.SetDirty(curve);
            AssetDatabase.SaveAssets();

            var sb = new StringBuilder();
            sb.AppendLine($"{segments.Count} tramos ({segments[0].startTime:0}–{segments[^1].endTime:0} s).");
            sb.AppendLine(oleadasPath != null ? $"{waves.Count} oleadas." : "Oleadas sin cambios.");
            if (warnings.Count > 0)
                sb.AppendLine("\nAvisos:\n• " + string.Join("\n• ", warnings));
            report = sb.ToString();
            Debug.Log($"[SpawnCurveCsvImporter] {report}", curve);
            return true;
        }

        // Excel guarda los CSV en UTF-8 o en la página de códigos de Windows (tildes como «Construcción»).
        static string ReadText(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            try
            {
                return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                try { return Encoding.GetEncoding(1252).GetString(bytes); }
                catch (System.Exception) { return Encoding.GetEncoding("iso-8859-1").GetString(bytes); }
            }
        }
    }
}
