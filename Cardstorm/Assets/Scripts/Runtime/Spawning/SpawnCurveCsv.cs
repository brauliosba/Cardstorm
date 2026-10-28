using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Cardstorm.Spawning
{
    // Lee los CSV exportados de las hojas Tramos y Oleadas de la hoja de equilibrado.
    // - Separador ';' con coma decimal (Excel en español) o ',' con punto decimal (Excel en inglés).
    // - Las columnas se buscan por su cabecera, en cualquier orden. Las de cada tipo de enemigo,
    //   por el nombre del tipo ("Rondadores/min" empieza por "Rondador").
    // - Las filas cuyo inicio no es un número (vacías, notas) se ignoran.
    public static class SpawnCurveCsv
    {
        public static List<SpawnSegment> ParseSegments(string text, IReadOnlyList<string> typeNames, List<string> errors)
        {
            var result = new List<SpawnSegment>();
            if (!TryReadTable(text, "Tramos", errors, out var rows, out char delimiter)) return result;

            var header = rows[0].Fields;
            int start = Column(header, "inicio", "Inicio (s)", errors);
            int end = Column(header, "fin", "Fin (s)", errors);
            int reinforcement = Column(header, "refuerzo", "Refuerzo f", errors);
            int name = OptionalColumn(header, "nombre");
            int[] types = TypeColumns(header, typeNames, errors);
            if (errors.Count > 0) return result;

            for (int r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                if (!IsDataRow(row, start, delimiter)) continue;

                var segment = new SpawnSegment
                {
                    name = Field(row, name),
                    startTime = Number(row, start, delimiter, errors),
                    endTime = Number(row, end, delimiter, errors),
                    reinforcement = Number(row, reinforcement, delimiter, errors),
                    ratePerMinute = new float[types.Length],
                };
                for (int t = 0; t < types.Length; t++)
                    segment.ratePerMinute[t] = Number(row, types[t], delimiter, errors);
                result.Add(segment);
            }
            return result;
        }

        // previous: oleadas actuales del asset, para conservar lo que el CSV no trae (duración, pulsos y,
        // si no se reconoce por el nombre, la formación).
        public static List<WaveEvent> ParseWaves(string text, IReadOnlyList<string> typeNames, IReadOnlyList<WaveEvent> previous,
            List<string> errors, List<string> warnings)
        {
            var result = new List<WaveEvent>();
            if (!TryReadTable(text, "Oleadas", errors, out var rows, out char delimiter)) return result;

            var header = rows[0].Fields;
            int start = Column(header, "inicio", "Inicio (s)", errors);
            int chestGold = Column(header, "cofre", "Cofre al terminar (oro)", errors);
            int chestTime = Column(header, "hora", "Hora del cofre (s)", errors);
            int name = OptionalColumn(header, "nombre");
            int formation = OptionalColumn(header, "formacion");
            int[] types = TypeColumns(header, typeNames, errors);
            if (errors.Count > 0) return result;

            for (int r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                if (!IsDataRow(row, start, delimiter)) continue;

                var old = previous != null && result.Count < previous.Count ? previous[result.Count] : null;
                var wave = new WaveEvent
                {
                    name = Field(row, name),
                    startTime = Number(row, start, delimiter, errors),
                    chestGold = (int)Math.Round(Number(row, chestGold, delimiter, errors)),
                    chestTime = Number(row, chestTime, delimiter, errors),
                    duration = old?.duration ?? 30f,
                    pulses = old?.pulses ?? 2,
                    extraCount = new int[types.Length],
                };
                for (int t = 0; t < types.Length; t++)
                    wave.extraCount[t] = (int)Math.Round(Number(row, types[t], delimiter, errors));

                if (TryParseFormation(Field(row, formation), out var f) || TryParseFormation(wave.name, out f))
                {
                    wave.formation = f;
                }
                else
                {
                    wave.formation = old?.formation ?? WaveFormation.Ring;
                    warnings.Add($"Oleadas, línea {row.Line}: no se reconoce la formación de «{wave.name}»; se usa {wave.formation}.");
                }
                result.Add(wave);
            }
            return result;
        }

        public static bool TryParseFormation(string text, out WaveFormation formation)
        {
            string n = Normalize(text);
            formation = WaveFormation.Ring;
            if (n.Length == 0) return false;
            if (n.Contains("cerco") || n.Contains("ring")) { formation = WaveFormation.Ring; return true; }
            if (n.Contains("pinza") || n.Contains("pincer")) { formation = WaveFormation.Pincer; return true; }
            if (n.Contains("estampida") || n.Contains("stampede")) { formation = WaveFormation.Stampede; return true; }
            if (n.Contains("tormenta") || n.Contains("mixta") || n.Contains("mixed")) { formation = WaveFormation.Mixed; return true; }
            return false;
        }

        // ---------------------------------------------------------------------
        // Lectura de la tabla
        // ---------------------------------------------------------------------

        readonly struct Row
        {
            public readonly int Line;
            public readonly List<string> Fields;

            public Row(int line, List<string> fields)
            {
                Line = line;
                Fields = fields;
            }
        }

        static bool TryReadTable(string text, string label, List<string> errors, out List<Row> rows, out char delimiter)
        {
            rows = new List<Row>();
            delimiter = ';';
            if (string.IsNullOrWhiteSpace(text))
            {
                errors.Add($"{label}: el archivo está vacío.");
                return false;
            }

            text = text.TrimStart('﻿');
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

            int headerIndex = Array.FindIndex(lines, l => !string.IsNullOrWhiteSpace(l));
            delimiter = DetectDelimiter(lines[headerIndex]);
            for (int i = headerIndex; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                rows.Add(new Row(i + 1, SplitFields(lines[i], delimiter)));
            }
            return true;
        }

        static char DetectDelimiter(string header)
        {
            int semicolons = 0, commas = 0;
            bool quoted = false;
            foreach (char c in header)
            {
                if (c == '"') quoted = !quoted;
                else if (!quoted && c == ';') semicolons++;
                else if (!quoted && c == ',') commas++;
            }
            return commas > semicolons ? ',' : ';';
        }

        static List<string> SplitFields(string line, char delimiter)
        {
            var fields = new List<string>();
            var current = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = !quoted;
                    }
                }
                else if (c == delimiter && !quoted)
                {
                    fields.Add(current.ToString().Trim());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            fields.Add(current.ToString().Trim());
            return fields;
        }

        static bool IsDataRow(Row row, int startColumn, char delimiter) =>
            TryParseNumber(Field(row, startColumn), delimiter, out _);

        // ---------------------------------------------------------------------
        // Columnas y valores
        // ---------------------------------------------------------------------

        static int Column(List<string> header, string prefix, string label, List<string> errors)
        {
            int index = OptionalColumn(header, prefix);
            if (index < 0) errors.Add($"Falta la columna «{label}».");
            return index;
        }

        static int OptionalColumn(List<string> header, string prefix)
        {
            for (int i = 0; i < header.Count; i++)
            {
                if (Normalize(header[i]).StartsWith(prefix, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        static int[] TypeColumns(List<string> header, IReadOnlyList<string> typeNames, List<string> errors)
        {
            var columns = new int[typeNames.Count];
            for (int t = 0; t < typeNames.Count; t++)
            {
                columns[t] = OptionalColumn(header, Normalize(typeNames[t]));
                if (columns[t] < 0) errors.Add($"Falta la columna del tipo «{typeNames[t]}».");
            }
            return columns;
        }

        static string Field(Row row, int column) =>
            column >= 0 && column < row.Fields.Count ? row.Fields[column] : string.Empty;

        static float Number(Row row, int column, char delimiter, List<string> errors)
        {
            string text = Field(row, column);
            if (TryParseNumber(text, delimiter, out float value)) return value;
            errors.Add($"línea {row.Line}, columna {column + 1}: «{text}» no es un número.");
            return 0f;
        }

        static bool TryParseNumber(string text, char delimiter, out float value)
        {
            text = text.Trim();
            // Con ';' como separador, la coma es la marca decimal.
            if (delimiter != ',') text = text.Replace(',', '.');
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        // Minúsculas y sin tildes, para comparar cabeceras.
        static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var decomposed = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposed.Length);
            foreach (char c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
