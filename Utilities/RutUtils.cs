using System.Linq;

namespace ReqSaaS_1.Utilities
{
    public static class RutUtils
    {
        // Devuelve RUT para BD: solo dígitos + DV (sin puntos ni guion), DV mayúscula.
        // Ej: "12.345.678-k" -> "12345678K". Si es inválido, devuelve null.
        public static string? Normalize(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;

            var s = new string(input.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
            if (s.Length < 2) return null;

            var body = s[..^1];
            var dv = s[^1];

            if (!body.All(char.IsDigit)) return null;
            return Validate(body, dv) ? body + dv : null;
        }

        public static bool IsValid(string? any) => Normalize(any) != null;

        private static bool Validate(string bodyDigits, char dv)
        {
            int factor = 2, sum = 0;
            for (int i = bodyDigits.Length - 1; i >= 0; i--)
            {
                sum += (bodyDigits[i] - '0') * factor;
                factor = factor == 7 ? 2 : factor + 1;
            }
            int rest = 11 - (sum % 11);
            char dvCalc = rest switch { 11 => '0', 10 => 'K', _ => (char)('0' + rest) };
            return dvCalc == char.ToUpperInvariant(dv);
        }
    }
}
