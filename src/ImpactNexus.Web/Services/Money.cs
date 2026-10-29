using System.Globalization;

namespace ImpactNexus.Web.Services;

/// <summary>
/// All monetary values in this application are South African Rand (ZAR).
/// Formatting is pinned to en-ZA so the symbol and grouping do not depend on the
/// culture of whichever machine the application happens to run on.
/// </summary>
public static class Money
{
    /// <summary>
    /// en-ZA with an explicit symbol: renders <c>R1 850 000,00</c>.
    /// </summary>
    public static readonly CultureInfo Zar = CreateZar();

    /// <summary>Currency symbol used in labels and exports.</summary>
    public const string Symbol = "R";

    private static CultureInfo CreateZar()
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("en-ZA").Clone();
        culture.NumberFormat.CurrencySymbol = Symbol;
        culture.NumberFormat.CurrencyDecimalDigits = 2;
        culture.NumberFormat.CurrencyPositivePattern = 2;   // R n
        culture.NumberFormat.CurrencyNegativePattern = 12;  // R -n
        return culture;
    }
}
