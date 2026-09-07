using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace RetailStorePOS.UI.Common.Services;

/// <summary>
/// Canonicalizes a <see cref="JsonElement"/> into the exact byte-for-byte form
/// the Edge Function (<c>supabase/functions/license-api/utils/crypto.ts</c>)
/// uses when computing the certificate signature.
///
/// The contract must stay identical to the TypeScript implementation:
/// <list type="bullet">
///   <item>String primitives use ECMAScript JSON.stringify escaping;
///         numbers, booleans, and nulls are emitted in their canonical JSON form.</item>
///   <item>Arrays preserve element order and concatenate with commas, no whitespace.</item>
///   <item>Objects drop entries whose value is <c>undefined</c> in JS terms — in
///         <see cref="JsonValueKind.Undefined"/> here — but keep <c>null</c>. Keys
///         are sorted with <see cref="StringComparison.Ordinal"/>.</item>
/// </list>
///
/// The current fixed certificate keys have the same ordering in the deployed
/// server's localeCompare sort. That is not true for arbitrary ASCII or Unicode
/// keys. This helper is a protocol serializer, not a general RFC 8785 implementation.
/// </summary>
internal static class CanonicalJson
{
    /// <summary>
    /// Returns the canonical UTF-8 string representation of <paramref name="element"/>.
    /// </summary>
    public static string Serialize(JsonElement element)
    {
        var writer = new StringWriter();
        WriteValue(writer, element);
        return writer.ToString();
    }

    private static void WriteValue(TextWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                WriteObject(writer, element);
                break;
            case JsonValueKind.Array:
                WriteArray(writer, element);
                break;
            case JsonValueKind.String:
                writer.Write(EscapeJsonString(element.GetString() ?? string.Empty));
                break;
            case JsonValueKind.Number:
                // Preserve the exact textual form the backend emitted. JSON.stringify
                // collapses things like 0e0 to 0 — the backend already wrote a clean
                // numeric so echoing the raw text is safe and lossless.
                writer.Write(element.GetRawText());
                break;
            case JsonValueKind.True:
                writer.Write("true");
                break;
            case JsonValueKind.False:
                writer.Write("false");
                break;
            case JsonValueKind.Null:
                writer.Write("null");
                break;
            case JsonValueKind.Undefined:
                // JS's JSON.stringify yields the empty string for undefined leaves.
                // The Edge Function only ever produces undefined inside object-entry
                // filters (see canonicalizeJson), never as a standalone value.
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported JSON value kind: {element.ValueKind}");
        }
    }

    private static void WriteObject(TextWriter writer, JsonElement obj)
    {
        // Sort keys ordinally and drop undefined values. Null values stay because
        // JS canonicalizer keeps them (only `undefined` is stripped).
        var entries = obj.EnumerateObject()
            .Where(p => p.Value.ValueKind != JsonValueKind.Undefined)
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToArray();

        writer.Write('{');
        for (var i = 0; i < entries.Length; i++)
        {
            if (i > 0) writer.Write(',');
            writer.Write(EscapeJsonString(entries[i].Name));
            writer.Write(':');
            WriteValue(writer, entries[i].Value);
        }
        writer.Write('}');
    }

    private static void WriteArray(TextWriter writer, JsonElement arr)
    {
        writer.Write('[');
        var first = true;
        foreach (var item in arr.EnumerateArray())
        {
            if (!first) writer.Write(',');
            first = false;
            WriteValue(writer, item);
        }
        writer.Write(']');
    }

    private static string EscapeJsonString(string value)
    {
        // Utf8JsonWriter's HTML-safe escaping changes signed bytes for characters
        // such as '+' and non-ASCII text. Match JSON.stringify's string form.
        var result = new StringBuilder(value.Length + 2).Append('"');
        for (var i = 0; i < value.Length; i++)
        {
            var character = value[i];
            switch (character)
            {
                case '"': result.Append("\\\""); break;
                case '\\': result.Append("\\\\"); break;
                case '\b': result.Append("\\b"); break;
                case '\f': result.Append("\\f"); break;
                case '\n': result.Append("\\n"); break;
                case '\r': result.Append("\\r"); break;
                case '\t': result.Append("\\t"); break;
                default:
                    if (char.IsHighSurrogate(character) && i + 1 < value.Length
                        && char.IsLowSurrogate(value[i + 1]))
                    {
                        result.Append(character).Append(value[++i]);
                    }
                    else if (character < ' ' || char.IsSurrogate(character))
                    {
                        result.Append("\\u").Append(((int)character).ToString("x4",
                            System.Globalization.CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        result.Append(character);
                    }
                    break;
            }
        }
        return result.Append('"').ToString();
    }
}
