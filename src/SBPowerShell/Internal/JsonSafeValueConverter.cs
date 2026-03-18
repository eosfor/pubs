using System.Collections;

namespace SBPowerShell.Internal;

internal static class JsonSafeValueConverter
{
    public static object? Convert(object? value)
    {
        if (value is null)
        {
            return null;
        }

        return value switch
        {
            byte[] bytes => System.Convert.ToBase64String(bytes),
            DateTime dt => dt.ToUniversalTime().ToString("O"),
            DateTimeOffset dto => dto.ToUniversalTime().ToString("O"),
            TimeSpan ts => ts.ToString(),
            Guid guid => guid.ToString(),
            Uri uri => uri.ToString(),
            string s => s,
            bool b => b,
            byte b8 => b8,
            sbyte sb8 => sb8,
            short s16 => s16,
            ushort u16 => u16,
            int i32 => i32,
            uint u32 => u32,
            long i64 => i64,
            ulong u64 => u64,
            float f32 => f32,
            double f64 => f64,
            decimal dec => dec,
            IDictionary dict => ConvertDictionary(dict),
            IEnumerable enumerable => ConvertEnumerable(enumerable),
            _ => value.ToString()
        };
    }

    private static IReadOnlyDictionary<string, object?> ConvertDictionary(IDictionary dictionary)
    {
        var result = new Dictionary<string, object?>(dictionary.Count, StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in dictionary)
        {
            var key = entry.Key?.ToString() ?? string.Empty;
            result[key] = Convert(entry.Value);
        }

        return result;
    }

    private static IReadOnlyList<object?> ConvertEnumerable(IEnumerable enumerable)
    {
        var result = new List<object?>();
        foreach (var item in enumerable)
        {
            result.Add(Convert(item));
        }

        return result;
    }
}
