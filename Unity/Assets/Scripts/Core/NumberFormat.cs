using System;
using System.Globalization;

namespace AgentClicker.Core
{
    public enum NumberStyle { Short = 0, Scientific = 1 }

    public static class NumberFormat
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Short (1.23 Qa) or scientific (1.23e15). Set from the settings.</summary>
        public static NumberStyle Style = NumberStyle.Short;

        // Short-scale suffixes for every power of 1000 up to a centillion (1e303): K, M, B, T, Qa ... Dc, UDc ... Vg ... Ce.
        static readonly string[] Suffixes = BuildSuffixes();
        static readonly string[] LongNames = BuildLongNames();

        static string[] BuildSuffixes()
        {
            string[] first = { "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No" };
            string[] units = { "", "U", "D", "T", "Qa", "Qi", "Sx", "Sp", "O", "N" };
            string[] tens = { "", "Dc", "Vg", "Tg", "Qag", "Qig", "Sxg", "Spg", "Ocg", "Nog" };
            var s = new string[102];
            for (int n = 0; n < s.Length; n++)
            {
                int illion = n - 1; // 1 = million, 2 = billion ...
                if (n < first.Length) s[n] = first[n];
                else if (illion < 100) s[n] = units[illion % 10] + tens[illion / 10];
                else s[n] = "Ce";
            }
            return s;
        }

        static string[] BuildLongNames()
        {
            string[] first = { "", "thousand", "million", "billion", "trillion", "quadrillion", "quintillion", "sextillion", "septillion", "octillion", "nonillion" };
            string[] units = { "", "un", "duo", "tre", "quattuor", "quin", "sex", "septen", "octo", "novem" };
            string[] tens = { "", "decillion", "vigintillion", "trigintillion", "quadragintillion", "quinquagintillion", "sexagintillion",
                              "septuagintillion", "octogintillion", "nonagintillion" };
            var s = new string[102];
            for (int n = 0; n < s.Length; n++)
            {
                int illion = n - 1;
                if (n < first.Length) s[n] = first[n];
                else if (illion < 100) s[n] = units[illion % 10] + tens[illion / 10];
                else s[n] = "centillion";
            }
            return s;
        }

        /// <summary>Number of named powers of 1000 (the largest is a centillion, 1e303).</summary>
        public static int NamedTiers => Suffixes.Length;

        /// <summary>"million" for 2, "quattuordecillion" for 15 ... (power of 1000).</summary>
        public static string LongName(int thousandsPower) => LongNames[Math.Max(0, Math.Min(thousandsPower, LongNames.Length - 1))];

        /// <summary>Compact number: 999, 1.23K, 45.6M, 789B ... 3 significant digits above 1000.</summary>
        public static string Short(double v)
        {
            if (double.IsNaN(v)) return "0";
            if (double.IsInfinity(v)) return v > 0 ? "∞" : "-∞";
            if (v < 0) return "-" + Short(-v);
            if (v < 10 && Math.Abs(v - Math.Round(v)) > 0.05) return v.ToString("0.0", Inv);
            if (v < 1000) return Math.Floor(v).ToString("0", Inv);
            int tier = (int)Math.Floor(Math.Log10(v) / 3 + 1e-12);
            if (Style == NumberStyle.Scientific || tier >= Suffixes.Length) return Scientific(v);
            double scaled = v / Math.Pow(1000, tier);
            if (scaled >= 1000) { scaled /= 1000; tier++; } // floating-point edge right below a power of 1000
            if (tier >= Suffixes.Length) return Scientific(v);
            string fmt = scaled < 10 ? "0.00" : scaled < 100 ? "0.0" : "0";
            return Truncate(scaled, fmt) + Suffixes[tier];
        }

        /// <summary>1.23e45, with the mantissa floored like <see cref="Short"/>.</summary>
        public static string Scientific(double v)
        {
            if (v < 1000) return Short(v);
            int exp = (int)Math.Floor(Math.Log10(v));
            double mantissa = v / Math.Pow(10, exp);
            if (mantissa >= 10) { mantissa /= 10; exp++; }
            if (mantissa < 1) { mantissa *= 10; exp--; }
            return Truncate(mantissa, "0.00") + "e" + exp.ToString(Inv);
        }

        static string Truncate(double v, string fmt)
        {
            // Floor rather than round so "1.999K" never shows as more credits than you have.
            int decimals = fmt.Length > 2 ? fmt.Length - 2 : 0;
            double p = Math.Pow(10, decimals);
            return (Math.Floor(v * p + 1e-9) / p).ToString(fmt, Inv);
        }

        public static string Credits(double v) => Short(v) + " credits";
        public static string Rate(double v) => Short(v) + "/s";

        /// <summary>Grouped integer, e.g. 1,234,567. Falls back to <see cref="Short"/> beyond 1e15.</summary>
        public static string Grouped(double v) => v >= 1e15 ? Short(v) : Math.Floor(v).ToString("#,0", Inv);

        /// <summary>Multiplier text: x1.25, x37.5, x1.20K.</summary>
        public static string Mult(double v) => v < 1000 ? "x" + v.ToString(v < 10 ? "0.00" : "0.0", Inv) : "x" + Short(v);

        public static double RoundSignificant(double v, int digits)
        {
            if (v <= 0 || double.IsInfinity(v) || double.IsNaN(v)) return v > 0 ? v : 0;
            double scale = Math.Pow(10, Math.Floor(Math.Log10(v)) + 1 - digits);
            double r = Math.Round(v / scale) * scale;
            return double.IsInfinity(r) ? v : r;
        }

        /// <summary>13.75 -> "1:45 PM".</summary>
        public static string Clock(float hours)
        {
            int totalMinutes = (int)Math.Floor(hours * 60);
            int h24 = (totalMinutes / 60) % 24;
            int m = totalMinutes % 60;
            int h12 = h24 % 12 == 0 ? 12 : h24 % 12;
            return $"{h12}:{m:00} {(h24 < 12 ? "AM" : "PM")}";
        }

        public static string Duration(double seconds)
        {
            if (double.IsInfinity(seconds) || double.IsNaN(seconds) || seconds > 3.15e9) return "forever";
            var t = TimeSpan.FromSeconds(Math.Max(0, seconds));
            if (t.TotalDays >= 2) return $"{(int)t.TotalDays}d {t.Hours}h";
            if (t.TotalHours >= 1) return $"{(int)t.TotalHours}h {t.Minutes:00}m";
            if (t.TotalMinutes >= 1) return $"{t.Minutes}m {t.Seconds:00}s";
            return $"{t.Seconds}s";
        }

        public static string Percent(double fraction)
        {
            if (fraction * 100 >= 10000) return Short(fraction * 100) + "%";
            return (fraction * 100).ToString(fraction < 0.1 ? "0.#" : "0", Inv) + "%";
        }
    }
}
