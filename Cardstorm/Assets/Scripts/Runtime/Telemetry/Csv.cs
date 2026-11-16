using System.Globalization;
using System.Text;

namespace Cardstorm.Telemetry
{
    // Separador ';' y punto decimal, como el resto de CSV del proyecto.
    public static class Csv
    {
        public const char Separator = ';';

        public static string Num(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        // Sin separadores ni saltos de línea dentro de una celda.
        public static string Text(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace(Separator, ' ').Replace('\r', ' ').Replace('\n', ' ');
        }

        public static string Line(params string[] cells)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < cells.Length; i++)
            {
                if (i > 0) sb.Append(Separator);
                sb.Append(Text(cells[i]));
            }
            return sb.ToString();
        }
    }
}
