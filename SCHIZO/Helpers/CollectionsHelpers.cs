using System;
using System.Collections.Generic;
using System.Linq;

namespace SCHIZO.Helpers;
internal static class CollectionsHelpers
{
    /// <summary>
    /// Search the given <see cref="string"/>-keyed <see cref="Dictionary{TKey, TValue}"/> for (part of) the given key.
    /// </summary>
    /// <remarks>
    /// If more than one key matches the given <paramref name="key"/>, which one gets returned is undefined (due to dictionary ordering).
    /// </remarks>
    /// <typeparam name="T">Dictionary value type. Value types are not supported.</typeparam>
    /// <param name="key">Full or partial key.</param>
    /// <param name="ignoreCase">Whether to compare keys case-insensitively.</param>
    /// <returns>The value, if any key matched; otherwise, <see langword="default"/>.</returns>
    public static T PartialSearch<T>(this IDictionary<string, T> dict, string key, bool ignoreCase = false)
        where T : class // value types are unsupported :^)
    {
        // full match
        if (dict.TryGetValue(key, out T signal))
            return signal;
        // partial (case-insensitive Contains but netfx doesn't have a Contains(string, StringComparison) overload)
        StringComparison comp = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        key = dict.Keys.FirstOrDefault(k => k.IndexOf(key, comp) >= 0);
        return key is null ? null
            : dict[key];
    }

    public static void PushRange<T>(this Stack<T> stack, IEnumerable<T> range)
    {
        foreach (T item in range)
            stack.Push(item);
    }

    public static TValue GetOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, TValue defaultValue = default)
        => dict.TryGetValue(key, out TValue value) ? value : defaultValue;
}
