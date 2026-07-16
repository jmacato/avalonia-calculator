using System.Buffers;
using System.Globalization;
using System.Text.Json;

using Avalonia.Platform;

namespace FluentAvalonia.UI;

/// <summary>
/// Helper class for storing localized string for FluentAvalonia/WinUI controls
/// </summary>
/// <remarks>
/// The string resources are taken from the WinUI repo. Not all resources in WinUI
/// may be available here, only those that are known to be used in a control
/// </remarks>
public class FALocalizationHelper
{
    private FALocalizationHelper()
    {
    }

    static FALocalizationHelper()
    {
        Instance = new FALocalizationHelper();
    }

    public static FALocalizationHelper Instance { get; }

    /// <summary>
    /// Gets a string resource by the specified name using the CurrentUICulture
    /// </summary>
    public string GetLocalizedStringResource(string resName) =>
        GetLocalizedStringResource(CultureInfo.CurrentUICulture, resName);

    /// <summary>
    /// Gets a string resource by the specified name and using the specified culture
    /// </summary>
    /// <remarks>
    /// InvariantCulture is not supported here and will default to en-US
    /// </remarks>
    public string GetLocalizedStringResource(CultureInfo ci, string resName)
    {
        // If running in globalization-invariant mode, always use "en-US" fallback
        var cultureName = ci == CultureInfo.InvariantCulture ? s_enUS : ci.Name;
        string[] cultureMap;
        string[] enUSMap;
        Dictionary<string, int> resourceIndices;

        lock (_cultureMapLock)
        {
            if (cultureName.Equals(s_enUS, StringComparison.OrdinalIgnoreCase))
            {
                if (_enUSMap is null)
                {
                    LoadCultureMaps(
                        s_enUS,
                        loadEnUS: false,
                        loadResourceNames: true,
                        out cultureMap,
                        out _,
                        out _resourceIndices);
                    _enUSMap = cultureMap;
                }

                cultureMap = _enUSMap;
            }
            else if (_cachedCultureName is not null &&
                _cachedCultureName.Equals(cultureName, StringComparison.OrdinalIgnoreCase))
            {
                cultureMap = _cachedCultureMap;
            }
            else
            {
                var loadFallbacks = _enUSMap is null;
                LoadCultureMaps(
                    cultureName,
                    loadFallbacks,
                    loadFallbacks,
                    out cultureMap,
                    out var loadedEnUSMap,
                    out var loadedResourceIndices);
                _enUSMap ??= loadedEnUSMap;
                _resourceIndices ??= loadedResourceIndices;
                _cachedCultureName = cultureName;
                _cachedCultureMap = cultureMap;
            }

            enUSMap = _enUSMap;
            resourceIndices = _resourceIndices;
        }

        if (!resourceIndices.TryGetValue(resName, out var resourceIndex))
            return string.Empty;

        if ((uint)resourceIndex < (uint)cultureMap.Length &&
            cultureMap[resourceIndex] is { } localizedValue)
            return localizedValue;

        return (uint)resourceIndex < (uint)enUSMap.Length
            ? enUSMap[resourceIndex] ?? string.Empty
            : string.Empty;
    }

    private static void LoadCultureMaps(
        string cultureName,
        bool loadEnUS,
        bool loadResourceNames,
        out string[] cultureMap,
        out string[] enUSMap,
        out Dictionary<string, int> resourceIndices)
    {
        cultureMap = null;
        enUSMap = null;
        resourceIndices = loadResourceNames
            ? new Dictionary<string, int>(s_initialResourceCapacity, StringComparer.OrdinalIgnoreCase)
            : null;

        using var stream = AssetLoader.Open(s_localizationAssetUri);
        var buffer = ArrayPool<byte>.Shared.Rent(s_initialBufferSize);
        var bufferedByteCount = 0;
        var isFirstBuffer = true;
        var readerState = new JsonReaderState();
        var resourceIndex = -1;
        var selectedCulture = SelectedCulture.None;

        try
        {
            while (true)
            {
                if (bufferedByteCount == buffer.Length)
                {
                    var largerBuffer = ArrayPool<byte>.Shared.Rent(buffer.Length * 2);
                    buffer.AsSpan(0, bufferedByteCount).CopyTo(largerBuffer);
                    ArrayPool<byte>.Shared.Return(buffer);
                    buffer = largerBuffer;
                }

                var bytesRead = stream.Read(buffer, bufferedByteCount, buffer.Length - bufferedByteCount);
                var isFinalBlock = bytesRead == 0;
                bufferedByteCount += bytesRead;

                if (isFirstBuffer && (bufferedByteCount >= 3 || isFinalBlock))
                {
                    isFirstBuffer = false;
                    if (bufferedByteCount >= 3 &&
                        buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
                    {
                        buffer.AsSpan(3, bufferedByteCount - 3).CopyTo(buffer);
                        bufferedByteCount -= 3;
                    }
                }

                // A stream is allowed to return fewer bytes without reaching EOF. Wait until
                // the possible UTF-8 BOM can be identified before handing bytes to the reader.
                if (isFirstBuffer)
                    continue;

                var reader = new Utf8JsonReader(
                    buffer.AsSpan(0, bufferedByteCount),
                    isFinalBlock,
                    readerState);

                while (reader.Read())
                {
                    switch (reader.TokenType)
                    {
                        case JsonTokenType.PropertyName when reader.CurrentDepth == 1:
                            resourceIndex++;
                            if (loadResourceNames)
                                resourceIndices[reader.GetString()] = resourceIndex;
                            selectedCulture = SelectedCulture.None;
                            break;

                        case JsonTokenType.PropertyName when reader.CurrentDepth == 2:
                            if (JsonPropertyEqualsCulture(ref reader, cultureName))
                                selectedCulture = SelectedCulture.Requested;
                            else if (loadEnUS && JsonPropertyEqualsCulture(ref reader, s_enUS))
                                selectedCulture = SelectedCulture.EnUS;
                            else
                                selectedCulture = SelectedCulture.None;
                            break;

                        case JsonTokenType.String when reader.CurrentDepth == 2:
                            if (selectedCulture == SelectedCulture.Requested)
                                SetCultureValue(ref cultureMap, resourceIndex, reader.GetString());
                            else if (selectedCulture == SelectedCulture.EnUS)
                                SetCultureValue(ref enUSMap, resourceIndex, reader.GetString());
                            break;
                    }
                }

                var consumedByteCount = checked((int)reader.BytesConsumed);
                var remainingByteCount = bufferedByteCount - consumedByteCount;
                if (remainingByteCount > 0)
                    buffer.AsSpan(consumedByteCount, remainingByteCount).CopyTo(buffer);

                bufferedByteCount = remainingByteCount;
                readerState = reader.CurrentState;

                if (isFinalBlock)
                    break;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        // An unsupported culture still gets a reusable empty map, without paying for
        // array storage that will never contain an entry.
        cultureMap ??= Array.Empty<string>();
        if (loadEnUS)
            enUSMap ??= Array.Empty<string>();
    }

    private static void SetCultureValue(ref string[] values, int resourceIndex, string value)
    {
        if (values is null)
            values = new string[Math.Max(s_initialResourceCapacity, resourceIndex + 1)];
        else if (resourceIndex >= values.Length)
            Array.Resize(ref values, Math.Max(values.Length * 2, resourceIndex + 1));

        values[resourceIndex] = value;
    }

    private static bool JsonPropertyEqualsCulture(ref Utf8JsonReader reader, string cultureName)
    {
        if (reader.ValueIsEscaped)
            return string.Equals(reader.GetString(), cultureName, StringComparison.OrdinalIgnoreCase);

        var utf8Name = reader.ValueSpan;
        if (utf8Name.Length != cultureName.Length)
            return false;

        for (var i = 0; i < utf8Name.Length; i++)
        {
            var expected = cultureName[i];
            if (expected > 0x7F)
                return false;

            var actual = utf8Name[i];
            var expectedByte = (byte)expected;
            if (actual == expectedByte)
                continue;

            // Culture names consist of ASCII letters, digits, and separators. Fold only
            // ASCII letters here so the 3,440 unselected property names allocate nothing.
            if (actual is >= (byte)'A' and <= (byte)'Z')
                actual += (byte)('a' - 'A');
            if (expectedByte is >= (byte)'A' and <= (byte)'Z')
                expectedByte += (byte)('a' - 'A');

            if (actual != expectedByte)
                return false;
        }

        return true;
    }

    private readonly object _cultureMapLock = new();
    private string _cachedCultureName;
    private string[] _cachedCultureMap;
    private string[] _enUSMap;
    private Dictionary<string, int> _resourceIndices;
    private static readonly Uri s_localizationAssetUri =
        new("avares://FluentAvalonia/Assets/ControlStrings.json");
    private const int s_initialBufferSize = 4 * 1024;
    private const int s_initialResourceCapacity = 40;
    private const string s_enUS = "en-US";

    private enum SelectedCulture : byte
    {
        None,
        Requested,
        EnUS
    }

    /// <summary>
    /// Dictionary of language entries for a resource name. &lt;language, value&gt; where
    /// language is the abbreviated name, e.g., en-US
    /// </summary>
    public class LocalizationEntry : Dictionary<string, string>
    {
        public LocalizationEntry()
            : base(StringComparer.InvariantCultureIgnoreCase)
        {

        }

    }

}
