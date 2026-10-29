using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace EdenPOS
{
    public static class Validation
    {
        public static string NormalizeDni(string value)
        {
            return new string((value ?? "").Where(char.IsDigit).Take(8).ToArray());
        }

        public static bool IsValidDni(string value)
        {
            var normalized = NormalizeDni(value);
            return normalized.Length == 8 && normalized.Any(x => x != '0');
        }

        public static string NormalizeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            return Regex.Replace(value.Trim(), @"\s+", " ").ToUpperInvariant();
        }

        public static bool IsValidPersonName(string value)
        {
            var normalized = NormalizeName(value);
            return normalized.Length >= 3 && normalized.Length <= 120 && normalized.Any(char.IsLetter);
        }

        public static bool IsValidRequiredText(string value, int minimum = 3, int maximum = 160)
        {
            var text = (value ?? "").Trim();
            return text.Length >= minimum && text.Length <= maximum;
        }

        public static bool IsValidMoney(decimal value, bool allowZero = true)
        {
            return value >= (allowZero ? 0m : 0.01m) && value <= 1000000m;
        }
    }
}
