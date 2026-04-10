using System.Globalization;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace RetailStorePOS.WinUiLogin.Common;

public static class NumericInputNormalization
{
    private const char ArabicDecimalSeparator = '\u066B';
    private const char ArabicGroupSeparator = '\u066C';
    private const char MinusSign = '\u2212';
    private const char FullWidthMinusSign = '\uFF0D';
    private const char SmallMinusSign = '\uFE63';
    private const char FullWidthPlusSign = '\uFF0B';
    private const char SmallPlusSign = '\uFE62';
    private const char FullWidthFullStop = '\uFF0E';
    private const char FullWidthComma = '\uFF0C';
    private const char NoBreakSpace = '\u00A0';
    private const char NarrowNoBreakSpace = '\u202F';

    public static readonly DependencyProperty NormalizeOnLostFocusProperty =
        DependencyProperty.RegisterAttached(
            "NormalizeOnLostFocus",
            typeof(bool),
            typeof(NumericInputNormalization),
            new PropertyMetadata(false, OnNormalizeOnLostFocusChanged));

    public static void SetNormalizeOnLostFocus(DependencyObject element, bool value)
    {
        element.SetValue(NormalizeOnLostFocusProperty, value);
    }

    public static bool GetNormalizeOnLostFocus(DependencyObject element)
    {
        return (bool)element.GetValue(NormalizeOnLostFocusProperty);
    }

    public static bool TryNormalize(string? text, out string normalizedText, out decimal value)
    {
        normalizedText = string.Empty;
        value = 0;

        var normalizedDigits = NormalizeCharacters(text);
        if (string.IsNullOrWhiteSpace(normalizedDigits))
        {
            return false;
        }

        var trimmed = normalizedDigits.Trim();
        if (!TrySplitSign(trimmed, out var sign, out var body))
        {
            return false;
        }

        sign = sign == "+" ? string.Empty : sign;

        var culture = CultureInfo.CurrentCulture;
        if (LooksLikeCurrentCultureGroupedInteger(body, culture)
            && TryParseNormalizedNumber(
                body,
                sign,
                decimalSeparator: null,
                BuildCurrentGroupSeparatorSet(culture),
                culture.NumberFormat.NumberGroupSizes,
                out normalizedText,
                out value))
        {
            return true;
        }

        foreach (var decimalSeparator in GetDecimalCandidates(body, culture))
        {
            if (TryParseNormalizedNumber(
                    body,
                    sign,
                    decimalSeparator,
                    BuildFlexibleGroupSeparatorSet(culture, decimalSeparator),
                    culture.NumberFormat.NumberGroupSizes,
                    out normalizedText,
                    out value))
            {
                return true;
            }
        }

        return TryParseNormalizedNumber(
            body,
            sign,
            decimalSeparator: null,
            BuildFlexibleGroupSeparatorSet(culture, decimalSeparator: null),
            culture.NumberFormat.NumberGroupSizes,
            out normalizedText,
            out value);
    }

    public static bool TryParseDecimal(string? text, out decimal value)
    {
        return TryNormalize(text, out _, out value);
    }

    public static bool NormalizeNumberBox(NumberBox numberBox)
    {
        if (!TryNormalize(numberBox.Text, out var normalizedText, out var value))
        {
            return false;
        }

        if (TryConvertDecimalToDouble(value, out var doubleValue)
            && (double.IsNaN(numberBox.Value) || numberBox.Value != doubleValue))
        {
            numberBox.Value = doubleValue;
        }

        if (!string.Equals(numberBox.Text, normalizedText, StringComparison.Ordinal))
        {
            numberBox.Text = normalizedText;
        }

        return true;
    }

    public static bool NormalizeTextBox(TextBox textBox)
    {
        if (!TryNormalize(textBox.Text, out var normalizedText, out _))
        {
            return false;
        }

        if (!string.Equals(textBox.Text, normalizedText, StringComparison.Ordinal))
        {
            textBox.Text = normalizedText;
        }

        return true;
    }

    private static void OnNormalizeOnLostFocusChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is NumberBox numberBox)
        {
            numberBox.LostFocus -= NormalizingControl_LostFocus;
            if ((bool)args.NewValue)
            {
                numberBox.LostFocus += NormalizingControl_LostFocus;
            }

            return;
        }

        if (dependencyObject is TextBox textBox)
        {
            textBox.LostFocus -= NormalizingControl_LostFocus;
            if ((bool)args.NewValue)
            {
                textBox.LostFocus += NormalizingControl_LostFocus;
            }
        }
    }

    private static void NormalizingControl_LostFocus(object sender, RoutedEventArgs e)
    {
        switch (sender)
        {
            case NumberBox numberBox:
                NormalizeNumberBox(numberBox);
                break;
            case TextBox textBox:
                NormalizeTextBox(textBox);
                break;
        }
    }

    private static string NormalizeCharacters(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        foreach (var character in text.Trim())
        {
            var digitValue = CharUnicodeInfo.GetDecimalDigitValue(character);
            if (digitValue >= 0)
            {
                builder.Append((char)('0' + digitValue));
                continue;
            }

            builder.Append(character switch
            {
                MinusSign or FullWidthMinusSign or SmallMinusSign => '-',
                FullWidthPlusSign or SmallPlusSign => '+',
                FullWidthFullStop => '.',
                FullWidthComma => ',',
                _ => character
            });
        }

        return builder.ToString();
    }

    private static bool TrySplitSign(string text, out string sign, out string body)
    {
        sign = string.Empty;
        body = text;

        if (text.Length == 0)
        {
            return false;
        }

        if (text[0] is '+' or '-')
        {
            sign = text[0].ToString();
            body = text[1..].TrimStart();
            if (body.Length == 0)
            {
                return false;
            }
        }

        return body.IndexOf('+') < 0 && body.IndexOf('-') < 0;
    }

    private static IEnumerable<char> GetDecimalCandidates(string body, CultureInfo culture)
    {
        var candidates = new List<char>();
        var currentDecimal = FirstSeparatorCharacter(culture.NumberFormat.NumberDecimalSeparator, '.');
        var currencyDecimal = FirstSeparatorCharacter(culture.NumberFormat.CurrencyDecimalSeparator, currentDecimal);

        AddCandidate(candidates, body, currentDecimal);
        AddCandidate(candidates, body, currencyDecimal);

        foreach (var separator in new[] { ArabicDecimalSeparator, '.', ',', currentDecimal, currencyDecimal }
                     .Distinct()
                     .Where(separator => body.IndexOf(separator) >= 0)
                     .OrderByDescending(separator => body.LastIndexOf(separator)))
        {
            AddCandidate(candidates, body, separator);
        }

        return candidates;
    }

    private static bool LooksLikeCurrentCultureGroupedInteger(string body, CultureInfo culture)
    {
        var groupSeparators = BuildCurrentGroupSeparatorSet(culture);
        if (!ContainsSeparator(body, groupSeparators))
        {
            return false;
        }

        return TryParseNormalizedNumber(
            body,
            sign: string.Empty,
            decimalSeparator: null,
            groupSeparators,
            culture.NumberFormat.NumberGroupSizes,
            out _,
            out _);
    }

    private static bool TryParseNormalizedNumber(
        string body,
        string sign,
        char? decimalSeparator,
        HashSet<char> groupSeparators,
        int[] cultureGroupSizes,
        out string normalizedText,
        out decimal value)
    {
        normalizedText = string.Empty;
        value = 0;

        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        var working = body.Trim();
        string integerRaw;
        string? fractionalRaw = null;

        if (decimalSeparator.HasValue)
        {
            var decimalIndex = working.IndexOf(decimalSeparator.Value);
            if (decimalIndex < 0 || working.LastIndexOf(decimalSeparator.Value) != decimalIndex)
            {
                return false;
            }

            integerRaw = working[..decimalIndex];
            fractionalRaw = working[(decimalIndex + 1)..];
        }
        else
        {
            integerRaw = working;
        }

        if (!TryNormalizeIntegerPart(integerRaw, groupSeparators, cultureGroupSizes, decimalSeparator.HasValue, out var integerDigits))
        {
            return false;
        }

        normalizedText = $"{sign}{integerDigits}";
        if (fractionalRaw is not null)
        {
            if (!TryNormalizeFractionalPart(fractionalRaw, out var fractionalDigits))
            {
                return false;
            }

            normalizedText = $"{normalizedText}.{fractionalDigits}";
        }

        return decimal.TryParse(
            normalizedText,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static bool TryNormalizeIntegerPart(
        string raw,
        HashSet<char> groupSeparators,
        int[] cultureGroupSizes,
        bool allowEmpty,
        out string digits)
    {
        digits = string.Empty;
        if (string.IsNullOrEmpty(raw))
        {
            if (!allowEmpty)
            {
                return false;
            }

            digits = "0";
            return true;
        }

        var segments = new List<string>();
        var currentSegment = new StringBuilder(raw.Length);
        var sawGroupSeparator = false;

        foreach (var character in raw)
        {
            if (char.IsDigit(character))
            {
                currentSegment.Append(character);
                continue;
            }

            if (!groupSeparators.Contains(character))
            {
                return false;
            }

            sawGroupSeparator = true;
            if (currentSegment.Length == 0)
            {
                return false;
            }

            segments.Add(currentSegment.ToString());
            currentSegment.Clear();
        }

        if (currentSegment.Length == 0)
        {
            return false;
        }

        segments.Add(currentSegment.ToString());

        if (sawGroupSeparator
            && !MatchesStandardThreeDigitGrouping(segments)
            && !MatchesCultureGroupingPattern(segments, cultureGroupSizes))
        {
            return false;
        }

        digits = CanonicalizeIntegerDigits(string.Concat(segments));
        return true;
    }

    private static bool TryNormalizeFractionalPart(string raw, out string digits)
    {
        digits = string.Empty;
        if (string.IsNullOrEmpty(raw))
        {
            return false;
        }

        var builder = new StringBuilder(raw.Length);
        foreach (var character in raw)
        {
            if (!char.IsDigit(character))
            {
                return false;
            }

            builder.Append(character);
        }

        if (builder.Length == 0)
        {
            return false;
        }

        digits = builder.ToString();
        return true;
    }

    private static string CanonicalizeIntegerDigits(string digits)
    {
        var trimmed = digits.TrimStart('0');
        return trimmed.Length == 0 ? "0" : trimmed;
    }

    private static bool MatchesStandardThreeDigitGrouping(IReadOnlyList<string> segments)
    {
        if (segments.Count == 0 || segments[0].Length is < 1 or > 3)
        {
            return false;
        }

        for (var index = 1; index < segments.Count; index++)
        {
            if (segments[index].Length != 3)
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesCultureGroupingPattern(IReadOnlyList<string> segments, int[] cultureGroupSizes)
    {
        var effectiveGroupSizes = cultureGroupSizes
            .Where(size => size > 0)
            .ToArray();

        if (segments.Count == 0 || effectiveGroupSizes.Length == 0)
        {
            return false;
        }

        var groupSizeIndex = 0;
        var repeatedSize = effectiveGroupSizes[^1];

        for (var segmentIndex = segments.Count - 1; segmentIndex >= 1; segmentIndex--)
        {
            var expectedSize = groupSizeIndex < effectiveGroupSizes.Length
                ? effectiveGroupSizes[groupSizeIndex]
                : repeatedSize;

            if (segments[segmentIndex].Length != expectedSize)
            {
                return false;
            }

            if (groupSizeIndex < effectiveGroupSizes.Length - 1)
            {
                groupSizeIndex++;
            }
        }

        var leftmostMaxLength = groupSizeIndex < effectiveGroupSizes.Length
            ? effectiveGroupSizes[groupSizeIndex]
            : repeatedSize;

        return segments[0].Length >= 1 && segments[0].Length <= leftmostMaxLength;
    }

    private static HashSet<char> BuildCurrentGroupSeparatorSet(CultureInfo culture)
    {
        return BuildGroupSeparatorSet(
            culture.NumberFormat.NumberGroupSeparator,
            culture.NumberFormat.CurrencyGroupSeparator,
            " ",
            NoBreakSpace.ToString(),
            NarrowNoBreakSpace.ToString());
    }

    private static HashSet<char> BuildFlexibleGroupSeparatorSet(CultureInfo culture, char? decimalSeparator)
    {
        var groupSeparators = BuildGroupSeparatorSet(
            culture.NumberFormat.NumberGroupSeparator,
            culture.NumberFormat.CurrencyGroupSeparator,
            " ",
            NoBreakSpace.ToString(),
            NarrowNoBreakSpace.ToString(),
            ArabicGroupSeparator.ToString(),
            ".",
            ",");

        if (decimalSeparator.HasValue)
        {
            groupSeparators.Remove(decimalSeparator.Value);
        }

        return groupSeparators;
    }

    private static HashSet<char> BuildGroupSeparatorSet(params string[] separators)
    {
        var characters = new HashSet<char>();
        foreach (var separator in separators.Where(value => !string.IsNullOrEmpty(value)))
        {
            foreach (var character in separator)
            {
                characters.Add(character);
            }
        }

        return characters;
    }

    private static bool ContainsSeparator(string text, HashSet<char> separators)
    {
        foreach (var character in text)
        {
            if (separators.Contains(character))
            {
                return true;
            }
        }

        return false;
    }

    private static char FirstSeparatorCharacter(string separator, char fallback)
    {
        foreach (var character in separator)
        {
            if (!char.IsWhiteSpace(character))
            {
                return character;
            }
        }

        return fallback;
    }

    private static void AddCandidate(ICollection<char> candidates, string body, char separator)
    {
        if (body.IndexOf(separator) >= 0 && !candidates.Contains(separator))
        {
            candidates.Add(separator);
        }
    }

    private static bool TryConvertDecimalToDouble(decimal value, out double converted)
    {
        converted = 0;

        try
        {
            converted = (double)value;
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }
}


