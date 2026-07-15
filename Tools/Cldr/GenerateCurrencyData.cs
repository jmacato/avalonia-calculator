// Copyright (c) jmacato. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

return await CldrCurrencyGenerator.RunAsync(args);

internal static class CldrCurrencyGenerator
{
    private const string DefaultCldrReference = "release-48-2";
    private const string DefaultCldrVersion = "48.2";
    private const string DefaultOutputPath =
        "src/Calculator/ViewModels/DataLoaders/CldrCurrencyData.g.cs";
    private const string DefaultResourcesPath = "src/Calculator/Resources";
    private const string DefaultUnitDefinitionsPath =
        "src/Calculator/ViewModels/DataLoaders/UnitConverterDataConstants.h.cs";
    private const string GitHubApiLatestRelease =
        "https://api.github.com/repos/unicode-org/cldr/releases/latest";

    // CLDR does not define every Calculator-specific or whimsical unit. Those
    // entries deliberately use the nearest CLDR short-unit pattern because the
    // generated metadata only carries placement and spacing; the localized
    // Calculator resource remains the displayed abbreviation.
    private static readonly IReadOnlyList<UnitPatternSource> UnitPatternSources =
    [
        new("Area_Acre", "area-acre"),
        new("Area_Hectare", "area-hectare"),
        new("Area_SquareCentimeter", "area-square-centimeter"),
        new("Area_SquareFoot", "area-square-foot"),
        new("Area_SquareInch", "area-square-inch"),
        new("Area_SquareKilometer", "area-square-kilometer"),
        new("Area_SquareMeter", "area-square-meter"),
        new("Area_SquareMile", "area-square-mile"),
        new("Area_SquareMillimeter", "area-square-centimeter"),
        new("Area_SquareYard", "area-square-yard"),
        new("Data_Bit", "digital-bit"),
        new("Data_Byte", "digital-byte"),
        new("Data_Gigabit", "digital-gigabit"),
        new("Data_Gigabyte", "digital-gigabyte"),
        new("Data_Kilobit", "digital-kilobit"),
        new("Data_Kilobyte", "digital-kilobyte"),
        new("Data_Megabit", "digital-megabit"),
        new("Data_Megabyte", "digital-megabyte"),
        new("Data_Petabit", "digital-bit"),
        new("Data_Petabyte", "digital-petabyte"),
        new("Data_Terabit", "digital-terabit"),
        new("Data_Terabyte", "digital-terabyte"),
        new("Energy_BritishThermalUnit", "energy-british-thermal-unit"),
        new("Energy_Calorie", "energy-calorie"),
        new("Energy_ElectronVolt", "energy-electronvolt"),
        new("Energy_FootPound", "torque-pound-force-foot"),
        new("Energy_Joule", "energy-joule"),
        new("Energy_Kilocalorie", "energy-kilocalorie"),
        new("Energy_Kilojoule", "energy-kilojoule"),
        new("Length_Centimeter", "length-centimeter"),
        new("Length_Foot", "length-foot"),
        new("Length_Inch", "length-inch"),
        new("Length_Kilometer", "length-kilometer"),
        new("Length_Meter", "length-meter"),
        new("Length_Micron", "length-micrometer"),
        new("Length_Mile", "length-mile"),
        new("Length_Millimeter", "length-millimeter"),
        new("Length_Nanometer", "length-nanometer"),
        new("Length_NauticalMile", "length-nautical-mile"),
        new("Length_Yard", "length-yard"),
        new("Power_BritishThermalUnitPerMinute", "power-watt"),
        new("Power_FootPoundPerMinute", "power-watt"),
        new("Power_Horsepower", "power-horsepower"),
        new("Power_Kilowatt", "power-kilowatt"),
        new("Power_Watt", "power-watt"),
        new("Temperature_DegreesCelsius", "temperature-celsius"),
        new("Temperature_DegreesFahrenheit", "temperature-fahrenheit"),
        new("Temperature_Kelvin", "temperature-kelvin"),
        new("Time_Day", "duration-day"),
        new("Time_Hour", "duration-hour"),
        new("Time_Microsecond", "duration-microsecond"),
        new("Time_Millisecond", "duration-millisecond"),
        new("Time_Minute", "duration-minute"),
        new("Time_Second", "duration-second"),
        new("Time_Week", "duration-week"),
        new("Time_Year", "duration-year"),
        new("Speed_CentimetersPerSecond", "speed-meter-per-second"),
        new("Speed_FeetPerSecond", "speed-meter-per-second"),
        new("Speed_KilometersPerHour", "speed-kilometer-per-hour"),
        new("Speed_Knot", "speed-knot"),
        new("Speed_Mach", "speed-meter-per-second"),
        new("Speed_MetersPerSecond", "speed-meter-per-second"),
        new("Speed_MilesPerHour", "speed-mile-per-hour"),
        new("Volume_CubicCentimeter", "volume-cubic-centimeter"),
        new("Volume_CubicFoot", "volume-cubic-foot"),
        new("Volume_CubicInch", "volume-cubic-inch"),
        new("Volume_CubicMeter", "volume-cubic-meter"),
        new("Volume_CubicYard", "volume-cubic-yard"),
        new("Volume_CupUS", "volume-cup"),
        new("Volume_FluidOunceUK", "volume-fluid-ounce-imperial"),
        new("Volume_FluidOunceUS", "volume-fluid-ounce"),
        new("Volume_GallonUK", "volume-gallon-imperial"),
        new("Volume_GallonUS", "volume-gallon"),
        new("Volume_Liter", "volume-liter"),
        new("Volume_Milliliter", "volume-milliliter"),
        new("Volume_PintUK", "volume-pint-imperial"),
        new("Volume_PintUS", "volume-pint"),
        new("Volume_TablespoonUS", "volume-tablespoon"),
        new("Volume_TeaspoonUS", "volume-teaspoon"),
        new("Volume_QuartUK", "volume-quart-imperial"),
        new("Volume_QuartUS", "volume-quart"),
        new("Weight_Carat", "mass-carat"),
        new("Weight_Centigram", "mass-gram"),
        new("Weight_Decigram", "mass-gram"),
        new("Weight_Decagram", "mass-gram"),
        new("Weight_Gram", "mass-gram"),
        new("Weight_Hectogram", "mass-gram"),
        new("Weight_Kilogram", "mass-kilogram"),
        new("Weight_LongTon", "mass-ton"),
        new("Weight_Milligram", "mass-milligram"),
        new("Weight_Ounce", "mass-ounce"),
        new("Weight_Pound", "mass-pound"),
        new("Weight_ShortTon", "mass-ton"),
        new("Weight_Stone", "mass-stone"),
        new("Weight_Tonne", "mass-tonne"),
        new("Area_SoccerField", "area-square-meter"),
        new("Data_FloppyDisk", "digital-byte"),
        new("Data_CD", "digital-byte"),
        new("Data_DVD", "digital-byte"),
        new("Energy_Battery", "energy-joule"),
        new("Length_Paperclip", "length-meter"),
        new("Length_JumboJet", "length-meter"),
        new("Power_LightBulb", "power-watt"),
        new("Power_Horse", "power-watt"),
        new("Volume_Bathtub", "volume-liter"),
        new("Weight_Snowflake", "mass-kilogram"),
        new("Weight_Elephant", "mass-kilogram"),
        new("Volume_TeaspoonUK", "volume-teaspoon"),
        new("Volume_TablespoonUK", "volume-tablespoon"),
        new("Area_Hand", "area-square-meter"),
        new("Speed_Turtle", "speed-meter-per-second"),
        new("Speed_Jet", "speed-meter-per-second"),
        new("Weight_Whale", "mass-kilogram"),
        new("Volume_CoffeeCup", "volume-cup"),
        new("Volume_SwimmingPool", "volume-liter"),
        new("Speed_Horse", "speed-meter-per-second"),
        new("Area_Paper", "area-square-meter"),
        new("Area_Castle", "area-square-meter"),
        new("Energy_Banana", "energy-joule"),
        new("Energy_SliceOfCake", "energy-joule"),
        new("Length_Hand", "length-meter"),
        new("Power_TrainEngine", "power-watt"),
        new("Weight_SoccerBall", "mass-kilogram"),
        new("Angle_Degree", "angle-degree"),
        new("Angle_Radian", "angle-radian"),
        new("Angle_Gradian", "angle-degree"),
        new("Pressure_Atmosphere", "pressure-atmosphere"),
        new("Pressure_Bar", "pressure-bar"),
        new("Pressure_KiloPascal", "pressure-kilopascal"),
        new("Pressure_MillimeterOfMercury", "pressure-millimeter-ofhg"),
        new("Pressure_Pascal", "pressure-pascal"),
        new("Pressure_PSI", "pressure-pound-force-per-square-inch"),
        new("Data_Exabits", "digital-bit"),
        new("Data_Exabytes", "digital-byte"),
        new("Data_Exbibits", "digital-bit"),
        new("Data_Exbibytes", "digital-byte"),
        new("Data_Gibibits", "digital-gigabit"),
        new("Data_Gibibytes", "digital-gigabyte"),
        new("Data_Kibibits", "digital-kilobit"),
        new("Data_Kibibytes", "digital-kilobyte"),
        new("Data_Mebibits", "digital-megabit"),
        new("Data_Mebibytes", "digital-megabyte"),
        new("Data_Pebibits", "digital-bit"),
        new("Data_Pebibytes", "digital-petabyte"),
        new("Data_Tebibits", "digital-terabit"),
        new("Data_Tebibytes", "digital-terabyte"),
        new("Data_Yobibits", "digital-bit"),
        new("Data_Yobibytes", "digital-byte"),
        new("Data_Yottabit", "digital-bit"),
        new("Data_Yottabyte", "digital-byte"),
        new("Data_Zebibits", "digital-bit"),
        new("Data_Zebibytes", "digital-byte"),
        new("Data_Zetabits", "digital-bit"),
        new("Data_Zetabytes", "digital-byte"),
        new("Area_Pyeong", "area-square-meter"),
        new("Energy_Kilowatthour", "energy-kilowatt-hour"),
        new("Data_Nibble", "digital-byte"),
        new("Length_Angstrom", "length-nanometer")
    ];

    public static async Task<int> RunAsync(string[] args)
    {
        Options options;
        try
        {
            options = Options.Parse(args);
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            PrintUsage();
            return 2;
        }

        if (options.ShowHelp)
        {
            PrintUsage();
            return 0;
        }

        using HttpClient client = new();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("CalcNeo-CLDR-Generator/1.0");

        string cldrReference = options.CldrReference;
        string cldrVersion = options.CldrVersion;
        if (string.Equals(cldrReference, "latest", StringComparison.OrdinalIgnoreCase))
        {
            (cldrReference, cldrVersion) = await ResolveLatestReleaseAsync(client);
        }

        string sourceRoot =
            $"https://raw.githubusercontent.com/unicode-org/cldr/{cldrReference}";
        string supplementalUrl = $"{sourceRoot}/common/supplemental/supplementalData.xml";
        string likelySubtagsUrl = $"{sourceRoot}/common/supplemental/likelySubtags.xml";
        string rootUrl = $"{sourceRoot}/common/main/root.xml";
        string englishUrl = $"{sourceRoot}/common/main/en.xml";
        string localeSourceRoot = $"{sourceRoot}/common/main";

        Console.WriteLine($"Fetching Unicode CLDR {cldrVersion} ({cldrReference})...");
        byte[] supplementalBytes = await client.GetByteArrayAsync(supplementalUrl);
        byte[] likelySubtagsBytes = await client.GetByteArrayAsync(likelySubtagsUrl);
        byte[] rootBytes = await client.GetByteArrayAsync(rootUrl);
        byte[] englishBytes = await client.GetByteArrayAsync(englishUrl);

        XDocument supplemental = LoadXml(supplementalBytes, supplementalUrl);
        XDocument likelySubtags = LoadXml(likelySubtagsBytes, likelySubtagsUrl);
        XDocument root = LoadXml(rootBytes, rootUrl);
        XDocument english = LoadXml(englishBytes, englishUrl);
        FractionData fractions = ReadFractionData(supplemental);
        SortedDictionary<string, string> localeParents = ReadLocaleParents(supplemental);
        SortedDictionary<string, string> likelySubtagMappings = ReadLikelySubtags(likelySubtags);
        SortedDictionary<string, CurrencyDisplayData> currencies = ReadEnglishCurrencies(english);
        SortedDictionary<string, string> rootUnitPatterns = ReadUnitPatternPatches(root);
        SortedDictionary<string, string> englishUnitPatterns = ReadUnitPatternPatches(english);
        ValidateUnitPatternSources(Path.GetFullPath(
            options.UnitDefinitionsPath,
            Directory.GetCurrentDirectory()));
        string resourcesPath = Path.GetFullPath(
            options.ResourcesPath,
            Directory.GetCurrentDirectory());
        List<LocaleRequest> localeRequests = ReadShippedLocales(
            resourcesPath,
            likelySubtagMappings);
        LocaleResolution localeResolution = await ResolveLocaleSourcesAsync(
            client,
            localeSourceRoot,
            localeRequests,
            localeParents);
        SortedDictionary<string, SortedDictionary<string, CurrencyDisplayData>> localizedCurrencies =
            BuildLocalizedCurrencies(
                localeResolution.Locales,
                localeResolution.Sources,
                currencies);
        UnitDisplayTables unitDisplays = BuildUnitDisplays(
            localeResolution.Locales,
            localeResolution.Sources,
            rootUnitPatterns,
            englishUnitPatterns);

        string generated = RenderSource(
            cldrReference,
            cldrVersion,
            supplementalUrl,
            likelySubtagsUrl,
            rootUrl,
            englishUrl,
            localeSourceRoot,
            Convert.ToHexStringLower(SHA256.HashData(supplementalBytes)),
            Convert.ToHexStringLower(SHA256.HashData(likelySubtagsBytes)),
            Convert.ToHexStringLower(SHA256.HashData(rootBytes)),
            Convert.ToHexStringLower(SHA256.HashData(englishBytes)),
            localeResolution.Sources,
            fractions,
            currencies,
            localizedCurrencies,
            unitDisplays);

        string outputPath = Path.GetFullPath(options.OutputPath, Directory.GetCurrentDirectory());
        if (options.CheckOnly)
        {
            string existing = File.Exists(outputPath)
                ? await File.ReadAllTextAsync(outputPath)
                : string.Empty;
            if (string.Equals(existing, generated, StringComparison.Ordinal))
            {
                Console.WriteLine($"Up to date: {outputPath}");
                return 0;
            }

            Console.Error.WriteLine($"Generated CLDR source is stale: {outputPath}");
            return 1;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllTextAsync(
            outputPath,
            generated,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        Console.WriteLine(
            $"Wrote {currencies.Count} currency names/symbols and " +
            $"{unitDisplays.English.Count} unit display conventions for " +
            $"{localizedCurrencies.Count} shipped locale tables with " +
            $"{fractions.Exceptions.Count} fraction-digit exceptions to {outputPath}");
        return 0;
    }

    private static async Task<(string Reference, string Version)> ResolveLatestReleaseAsync(
        HttpClient client)
    {
        using Stream stream = await client.GetStreamAsync(GitHubApiLatestRelease);
        using JsonDocument release = await JsonDocument.ParseAsync(stream);
        string tag = release.RootElement.GetProperty("tag_name").GetString()
            ?? throw new InvalidDataException("The latest CLDR release has no tag_name.");
        string version = tag.StartsWith("release-", StringComparison.Ordinal)
            ? tag["release-".Length..].Replace('-', '.')
            : tag;
        return (tag, version);
    }

    private static XDocument LoadXml(byte[] bytes, string source)
    {
        try
        {
            using MemoryStream stream = new(bytes, writable: false);
            return XDocument.Load(stream, LoadOptions.None);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.Xml.XmlException)
        {
            throw new InvalidDataException($"Unable to parse {source}.", exception);
        }
    }

    private static FractionData ReadFractionData(XDocument document)
    {
        XElement fractions = document.Descendants("fractions").Single();
        var values = fractions.Elements("info")
            .Select(element => new
            {
                IsoCode = (string?)element.Attribute("iso4217"),
                Digits = ParseDigits(element)
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.IsoCode))
            .ToDictionary(
                item => item.IsoCode!,
                item => item.Digits,
                StringComparer.OrdinalIgnoreCase);

        if (!values.Remove("DEFAULT", out int defaultDigits))
        {
            throw new InvalidDataException("CLDR currency fractions do not define DEFAULT.");
        }

        return new FractionData(
            defaultDigits,
            new SortedDictionary<string, int>(
                values.Where(pair => pair.Value != defaultDigits)
                    .ToDictionary(pair => pair.Key, pair => pair.Value),
                StringComparer.Ordinal));
    }

    private static int ParseDigits(XElement element)
    {
        string value = (string?)element.Attribute("digits")
            ?? throw new InvalidDataException("A CLDR currency fraction is missing digits.");
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int digits) ||
            digits is < 0 or > 9)
        {
            throw new InvalidDataException($"Invalid CLDR currency fraction digits: {value}");
        }

        return digits;
    }

    private static void ValidateUnitPatternSources(string unitDefinitionsPath)
    {
        if (!File.Exists(unitDefinitionsPath))
        {
            throw new FileNotFoundException(
                "Calculator unit definitions were not found.",
                unitDefinitionsPath);
        }

        string source = File.ReadAllText(unitDefinitionsPath);
        string[] enumNames = Regex.Matches(
                source,
                @"^\s*(?<name>(?:Area|Data|Energy|Length|Power|Temperature|Time|Speed|Volume|Weight|Angle|Pressure)_[A-Za-z0-9]+)\s*=\s*UnitStart\s*\+\s*\d+",
                RegexOptions.Multiline | RegexOptions.CultureInvariant)
            .Select(match => match.Groups["name"].Value)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        string[] mappedNames = UnitPatternSources
            .Select(source => source.EnumName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        string[] duplicates = mappedNames
            .GroupBy(name => name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        string[] missing = enumNames.Except(mappedNames, StringComparer.Ordinal).ToArray();
        string[] unknown = mappedNames.Except(enumNames, StringComparer.Ordinal).ToArray();
        if (duplicates.Length != 0 || missing.Length != 0 || unknown.Length != 0)
        {
            throw new InvalidDataException(
                "CLDR unit pattern mappings do not match UnitConverterUnits. " +
                $"Duplicates: [{string.Join(", ", duplicates)}]; " +
                $"missing: [{string.Join(", ", missing)}]; " +
                $"unknown: [{string.Join(", ", unknown)}].");
        }
    }

    private static SortedDictionary<string, string> ReadLocaleParents(XDocument document)
    {
        XElement parentLocales = document.Descendants("parentLocales")
            .Single(element => element.Attribute("component") is null);
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (XElement parentLocale in parentLocales.Elements("parentLocale"))
        {
            string parent = (string?)parentLocale.Attribute("parent")
                ?? throw new InvalidDataException("A CLDR parentLocale is missing its parent.");
            string locales = (string?)parentLocale.Attribute("locales")
                ?? throw new InvalidDataException("A CLDR parentLocale is missing its locales.");
            foreach (string locale in locales.Split(
                         (char[]?)null,
                         StringSplitOptions.RemoveEmptyEntries))
            {
                result.Add(locale, parent);
            }
        }

        return result;
    }

    private static SortedDictionary<string, string> ReadLikelySubtags(XDocument document)
    {
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (XElement likelySubtag in document.Descendants("likelySubtag"))
        {
            string from = (string?)likelySubtag.Attribute("from")
                ?? throw new InvalidDataException("A CLDR likelySubtag is missing its source locale.");
            string to = (string?)likelySubtag.Attribute("to")
                ?? throw new InvalidDataException("A CLDR likelySubtag is missing its target locale.");
            result.Add(from, to);
        }

        if (result.Count == 0)
        {
            throw new InvalidDataException("CLDR likelySubtags.xml contains no mappings.");
        }

        return result;
    }

    private static SortedDictionary<string, CurrencyDisplayData> ReadEnglishCurrencies(
        XDocument document)
    {
        SortedDictionary<string, CurrencyDisplayData> result = new(StringComparer.Ordinal);
        foreach (XElement currency in document.Descendants("currencies").Elements("currency"))
        {
            string? isoCode = (string?)currency.Attribute("type");
            if (!IsIsoCurrencyCode(isoCode))
            {
                continue;
            }

            string? name = currency.Elements("displayName")
                .FirstOrDefault(element => element.Attribute("count") is null)?.Value;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            string symbol = currency.Elements("symbol")
                .FirstOrDefault(element => element.Attribute("alt") is null)?.Value ?? isoCode!;
            result.Add(isoCode!, new CurrencyDisplayData(name, symbol, false));
        }

        if (result.Count == 0)
        {
            throw new InvalidDataException("CLDR en.xml contains no currency metadata.");
        }

        return result;
    }

    private static List<LocaleRequest> ReadShippedLocales(
        string resourcesPath,
        IReadOnlyDictionary<string, string> likelySubtags)
    {
        if (!Directory.Exists(resourcesPath))
        {
            throw new DirectoryNotFoundException(
                $"Calculator resources directory does not exist: {resourcesPath}");
        }

        List<LocaleRequest> locales = Directory
            .EnumerateFiles(resourcesPath, "Resources.*.resx", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(fileName => fileName is not null && fileName.StartsWith("Resources.", StringComparison.Ordinal))
            .Select(fileName => fileName!["Resources.".Length..])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(cultureName => cultureName, StringComparer.Ordinal)
            .Select(cultureName => CreateLocaleRequest(cultureName, likelySubtags))
            .ToList();

        if (locales.Count == 0)
        {
            throw new InvalidDataException(
                $"No localized Resources.*.resx catalogs were found in {resourcesPath}.");
        }

        return locales;
    }

    private static LocaleRequest CreateLocaleRequest(
        string cultureName,
        IReadOnlyDictionary<string, string> likelySubtags)
    {
        string canonicalCultureName;
        try
        {
            canonicalCultureName = CultureInfo.GetCultureInfo(cultureName).Name;
        }
        catch (CultureNotFoundException exception)
        {
            throw new InvalidDataException(
                $"Resource catalog culture is invalid: {cultureName}",
                exception);
        }

        string sourceLocaleId = canonicalCultureName.Replace('-', '_');
        return new LocaleRequest(
            canonicalCultureName,
            sourceLocaleId,
            MaximizeLocaleId(sourceLocaleId, likelySubtags));
    }

    private static string MaximizeLocaleId(
        string localeId,
        IReadOnlyDictionary<string, string> likelySubtags)
    {
        LocaleIdParts source = ParseLocaleId(localeId);
        List<string> candidates = [];
        AddLikelySubtagCandidate(candidates, source.Language, source.Script, source.Region);
        AddLikelySubtagCandidate(candidates, source.Language, source.Script, null);
        AddLikelySubtagCandidate(candidates, source.Language, null, source.Region);
        AddLikelySubtagCandidate(candidates, source.Language, null, null);

        foreach (string candidate in candidates)
        {
            if (!likelySubtags.TryGetValue(candidate, out string? mappedLocale))
            {
                continue;
            }

            LocaleIdParts mapped = ParseLocaleId(mappedLocale);
            return new LocaleIdParts(
                source.Language.Equals("und", StringComparison.Ordinal) ? mapped.Language : source.Language,
                source.Script ?? mapped.Script,
                source.Region ?? mapped.Region).ToString();
        }

        return localeId;
    }

    private static void AddLikelySubtagCandidate(
        List<string> candidates,
        string language,
        string? script,
        string? region)
    {
        string candidate = new LocaleIdParts(language, script, region).ToString();
        if (!candidates.Contains(candidate, StringComparer.Ordinal))
        {
            candidates.Add(candidate);
        }
    }

    private static LocaleIdParts ParseLocaleId(string localeId)
    {
        string[] parts = localeId.Split('_');
        string language = parts[0];
        string? script = parts.Skip(1).FirstOrDefault(part => part.Length == 4);
        string? region = parts.Skip(1).FirstOrDefault(
            part => part.Length == 2 || part.Length == 3 && part.All(char.IsDigit));
        return new LocaleIdParts(language, script, region);
    }

    private static async Task<LocaleResolution> ResolveLocaleSourcesAsync(
        HttpClient client,
        string localeSourceRoot,
        IReadOnlyCollection<LocaleRequest> requests,
        IReadOnlyDictionary<string, string> localeParents)
    {
        Dictionary<string, IReadOnlyList<string>> chains = requests.ToDictionary(
            request => request.ResourceCulture,
            request => GetLocaleInheritanceChain(request.SourceLocaleId, localeParents),
            StringComparer.Ordinal);
        SortedDictionary<string, LocaleSource> sources = await FetchLocaleSourcesAsync(
            client,
            localeSourceRoot,
            chains.Values.SelectMany(chain => chain));

        foreach (LocaleRequest request in requests)
        {
            if (!sources.ContainsKey(request.SourceLocaleId) &&
                !request.MaximizedLocaleId.Equals(request.SourceLocaleId, StringComparison.Ordinal))
            {
                chains[request.ResourceCulture] = GetLocaleInheritanceChain(
                    request.MaximizedLocaleId,
                    localeParents);
            }
        }

        SortedDictionary<string, LocaleSource> maximizedSources = await FetchLocaleSourcesAsync(
            client,
            localeSourceRoot,
            chains.Values.SelectMany(chain => chain).Where(localeId => !sources.ContainsKey(localeId)));
        foreach ((string localeId, LocaleSource source) in maximizedSources)
        {
            sources.Add(localeId, source);
        }

        List<LocaleDefinition> locales = requests
            .Select(request => new LocaleDefinition(
                request.ResourceCulture,
                chains[request.ResourceCulture]))
            .ToList();

        foreach (LocaleDefinition locale in locales)
        {
            bool hasLocalizedSource = locale.CldrLocaleIds.Any(
                localeId => sources.TryGetValue(localeId, out LocaleSource? source) &&
                            source.Currencies.Count > 0);
            if (!hasLocalizedSource)
            {
                throw new InvalidDataException(
                    $"No CLDR locale source was found for shipped culture {locale.ResourceCulture} " +
                    $"(resolved chain: {string.Join(", ", locale.CldrLocaleIds)}).");
            }
        }

        return new LocaleResolution(locales, sources);
    }

    private static IReadOnlyList<string> GetLocaleInheritanceChain(
        string localeId,
        IReadOnlyDictionary<string, string> localeParents)
    {
        List<string> leafToRoot = [];
        HashSet<string> visited = new(StringComparer.Ordinal);
        string current = localeId;
        while (!current.Equals("root", StringComparison.Ordinal))
        {
            if (!visited.Add(current))
            {
                throw new InvalidDataException(
                    $"CLDR locale parent cycle detected at {current}.");
            }

            leafToRoot.Add(current);
            if (localeParents.TryGetValue(current, out string? explicitParent))
            {
                current = explicitParent;
                continue;
            }

            int separator = current.LastIndexOf('_');
            current = separator >= 0 ? current[..separator] : "root";
        }

        leafToRoot.Reverse();
        return leafToRoot;
    }

    private static async Task<SortedDictionary<string, LocaleSource>> FetchLocaleSourcesAsync(
        HttpClient client,
        string localeSourceRoot,
        IEnumerable<string> requestedLocaleIds)
    {
        string[] localeIds = requestedLocaleIds
            .Where(localeId =>
                !localeId.Equals("root", StringComparison.Ordinal) &&
                !localeId.Equals("en", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(localeId => localeId, StringComparer.Ordinal)
            .ToArray();

        Task<LocaleSource?>[] fetches = localeIds
            .Select(localeId => FetchLocaleSourceAsync(client, localeSourceRoot, localeId))
            .ToArray();
        LocaleSource?[] fetched = await Task.WhenAll(fetches);
        var sources = new SortedDictionary<string, LocaleSource>(StringComparer.Ordinal);
        foreach (LocaleSource source in fetched.OfType<LocaleSource>())
        {
            sources.Add(source.CldrLocaleId, source);
        }

        return sources;
    }

    private static async Task<LocaleSource?> FetchLocaleSourceAsync(
        HttpClient client,
        string localeSourceRoot,
        string localeId)
    {
        string url = $"{localeSourceRoot}/{localeId}.xml";
        using HttpResponseMessage response = await client.GetAsync(url);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        byte[] bytes = await response.Content.ReadAsByteArrayAsync();
        XDocument document = LoadXml(bytes, url);
        return new LocaleSource(
            localeId,
            url,
            Convert.ToHexStringLower(SHA256.HashData(bytes)),
            ReadCurrencyPatches(document),
            ReadUnitPatternPatches(document));
    }

    private static SortedDictionary<string, CurrencyDisplayPatch> ReadCurrencyPatches(
        XDocument document)
    {
        var result = new SortedDictionary<string, CurrencyDisplayPatch>(StringComparer.Ordinal);
        foreach (XElement currency in document.Descendants("currencies").Elements("currency"))
        {
            string? isoCode = (string?)currency.Attribute("type");
            if (!IsIsoCurrencyCode(isoCode))
            {
                continue;
            }

            string? name = currency.Elements("displayName")
                .FirstOrDefault(element => element.Attribute("count") is null)?.Value;
            string? symbol = currency.Elements("symbol")
                .FirstOrDefault(element => element.Attribute("alt") is null)?.Value;
            name = NormalizeInheritedValue(name);
            symbol = NormalizeInheritedValue(symbol);
            if (name is not null || symbol is not null)
            {
                result[isoCode!] = new CurrencyDisplayPatch(name, symbol);
            }
        }

        return result;
    }

    private static string? NormalizeInheritedValue(string? value) =>
        string.IsNullOrWhiteSpace(value) || value == "↑↑↑" ? null : value;

    private static SortedDictionary<string, string> ReadUnitPatternPatches(
        XDocument document)
    {
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        IEnumerable<XElement> shortUnits = document.Descendants("unitLength")
            .Where(element => (string?)element.Attribute("type") == "short")
            .Elements("unit");
        foreach (XElement unit in shortUnits)
        {
            string? unitType = (string?)unit.Attribute("type");
            string? pattern = unit.Elements("unitPattern")
                .FirstOrDefault(element =>
                    (string?)element.Attribute("count") == "other" &&
                    element.Attribute("alt") is null)?.Value;
            pattern = NormalizeInheritedValue(pattern);
            if (!string.IsNullOrWhiteSpace(unitType) && pattern is not null)
            {
                result[unitType] = pattern;
            }
        }

        return result;
    }

    private static SortedDictionary<string, SortedDictionary<string, CurrencyDisplayData>>
        BuildLocalizedCurrencies(
            IReadOnlyCollection<LocaleDefinition> locales,
            IReadOnlyDictionary<string, LocaleSource> localeSources,
            IReadOnlyDictionary<string, CurrencyDisplayData> english)
    {
        var localized = new SortedDictionary<string, SortedDictionary<string, CurrencyDisplayData>>(
            StringComparer.Ordinal);
        foreach (LocaleDefinition locale in locales)
        {
            var merged = new SortedDictionary<string, CurrencyDisplayData>(StringComparer.Ordinal);
            foreach ((string isoCode, CurrencyDisplayData display) in english)
            {
                merged.Add(isoCode, display);
            }
            foreach (string localeId in locale.CldrLocaleIds)
            {
                if (!localeSources.TryGetValue(localeId, out LocaleSource? source))
                {
                    continue;
                }

                foreach ((string isoCode, CurrencyDisplayPatch patch) in source.Currencies)
                {
                    CurrencyDisplayData current =
                        merged.TryGetValue(isoCode, out CurrencyDisplayData? value) && value is not null
                            ? value
                            : new CurrencyDisplayData(isoCode, isoCode, false);
                    merged[isoCode] = new CurrencyDisplayData(
                        patch.Name ?? current.Name,
                        patch.Symbol ?? current.Symbol,
                        patch.Symbol is not null || current.HasLocalizedSymbol);
                }
            }

            var differences = new SortedDictionary<string, CurrencyDisplayData>(StringComparer.Ordinal);
            foreach ((string isoCode, CurrencyDisplayData display) in merged)
            {
                if (!english.TryGetValue(isoCode, out CurrencyDisplayData? englishDisplay) ||
                    englishDisplay is null ||
                    display != englishDisplay)
                {
                    differences.Add(isoCode, display);
                }
            }

            localized.Add(locale.ResourceCulture, differences);
        }

        return localized;
    }

    private static UnitDisplayTables BuildUnitDisplays(
        IReadOnlyCollection<LocaleDefinition> locales,
        IReadOnlyDictionary<string, LocaleSource> localeSources,
        IReadOnlyDictionary<string, string> rootPatterns,
        IReadOnlyDictionary<string, string> englishPatches)
    {
        SortedDictionary<string, string> englishPatterns = MergeUnitPatterns(
            rootPatterns,
            englishPatches);
        SortedDictionary<string, UnitDisplayData> english = ResolveUnitDisplays(
            englishPatterns,
            "English");
        var localized =
            new SortedDictionary<string, SortedDictionary<string, UnitDisplayData>>(
                StringComparer.Ordinal);

        foreach (LocaleDefinition locale in locales)
        {
            var merged = new SortedDictionary<string, string>(StringComparer.Ordinal);
            ApplyUnitPatternPatches(merged, rootPatterns);
            foreach (string localeId in locale.CldrLocaleIds)
            {
                if (localeId.Equals("en", StringComparison.Ordinal))
                {
                    ApplyUnitPatternPatches(merged, englishPatches);
                }
                else if (localeSources.TryGetValue(localeId, out LocaleSource? source))
                {
                    ApplyUnitPatternPatches(merged, source.UnitPatterns);
                }
            }

            SortedDictionary<string, UnitDisplayData> resolved = ResolveUnitDisplays(
                merged,
                locale.ResourceCulture);
            var differences = new SortedDictionary<string, UnitDisplayData>(StringComparer.Ordinal);
            foreach ((string enumName, UnitDisplayData display) in resolved)
            {
                if (!english.TryGetValue(enumName, out UnitDisplayData englishDisplay) ||
                    display != englishDisplay)
                {
                    differences.Add(enumName, display);
                }
            }

            localized.Add(locale.ResourceCulture, differences);
        }

        return new UnitDisplayTables(english, localized);
    }

    private static SortedDictionary<string, string> MergeUnitPatterns(
        IReadOnlyDictionary<string, string> baseline,
        IReadOnlyDictionary<string, string> patches)
    {
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        ApplyUnitPatternPatches(result, baseline);
        ApplyUnitPatternPatches(result, patches);
        return result;
    }

    private static void ApplyUnitPatternPatches(
        IDictionary<string, string> destination,
        IReadOnlyDictionary<string, string> patches)
    {
        foreach ((string unitType, string pattern) in patches)
        {
            destination[unitType] = pattern;
        }
    }

    private static SortedDictionary<string, UnitDisplayData> ResolveUnitDisplays(
        IReadOnlyDictionary<string, string> patterns,
        string localeName)
    {
        var result = new SortedDictionary<string, UnitDisplayData>(StringComparer.Ordinal);
        foreach (UnitPatternSource source in UnitPatternSources)
        {
            if (!patterns.TryGetValue(source.CldrType, out string? pattern))
            {
                throw new InvalidDataException(
                    $"CLDR has no short pattern for {source.CldrType} " +
                    $"({source.EnumName}, {localeName}).");
            }

            result.Add(source.EnumName, ParseUnitDisplay(pattern, source, localeName));
        }

        return result;
    }

    private static UnitDisplayData ParseUnitDisplay(
        string pattern,
        UnitPatternSource source,
        string localeName)
    {
        const string placeholder = "{0}";
        int placeholderIndex = pattern.IndexOf(placeholder, StringComparison.Ordinal);
        if (placeholderIndex < 0 ||
            pattern.IndexOf(placeholder, placeholderIndex + placeholder.Length, StringComparison.Ordinal) >= 0)
        {
            throw new InvalidDataException(
                $"CLDR short pattern for {source.CldrType} must contain one {{0}} " +
                $"placeholder ({source.EnumName}, {localeName}): {pattern}");
        }

        string before = pattern[..placeholderIndex];
        string after = pattern[(placeholderIndex + placeholder.Length)..];
        bool unitBeforeValue = ContainsUnitText(before);
        bool unitAfterValue = ContainsUnitText(after);
        if (unitBeforeValue == unitAfterValue)
        {
            throw new InvalidDataException(
                $"Unable to locate the unit around {{0}} for {source.CldrType} " +
                $"({source.EnumName}, {localeName}): {pattern}");
        }

        bool useSpace = unitAfterValue
            ? LeadingLayoutCharacters(after).Any(char.IsWhiteSpace)
            : TrailingLayoutCharacters(before).Any(char.IsWhiteSpace);
        return new UnitDisplayData(unitAfterValue, useSpace);
    }

    private static bool ContainsUnitText(string value) =>
        value.Any(character =>
            !char.IsWhiteSpace(character) &&
            CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.Format);

    private static IEnumerable<char> LeadingLayoutCharacters(string value)
    {
        foreach (char character in value)
        {
            if (!char.IsWhiteSpace(character) &&
                CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.Format)
            {
                yield break;
            }

            yield return character;
        }
    }

    private static IEnumerable<char> TrailingLayoutCharacters(string value)
    {
        for (int index = value.Length - 1; index >= 0; index--)
        {
            char character = value[index];
            if (!char.IsWhiteSpace(character) &&
                CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.Format)
            {
                yield break;
            }

            yield return character;
        }
    }

    private static bool IsIsoCurrencyCode(string? value) =>
        value is { Length: 3 } && value.All(character => character is >= 'A' and <= 'Z');

    private static string RenderSource(
        string cldrReference,
        string cldrVersion,
        string supplementalUrl,
        string likelySubtagsUrl,
        string rootUrl,
        string englishUrl,
        string localeSourceRoot,
        string supplementalSha256,
        string likelySubtagsSha256,
        string rootSha256,
        string englishSha256,
        IReadOnlyDictionary<string, LocaleSource> localeSources,
        FractionData fractions,
        SortedDictionary<string, CurrencyDisplayData> currencies,
        SortedDictionary<string, SortedDictionary<string, CurrencyDisplayData>> localizedCurrencies,
        UnitDisplayTables unitDisplays)
    {
        StringBuilder source = new();
        source.AppendLine("// <auto-generated>");
        source.AppendLine("// Generated by Tools/Cldr/GenerateCurrencyData.cs. Do not edit by hand.");
        source.AppendLine($"// Unicode CLDR {cldrVersion} ({cldrReference}), Unicode License v3.");
        source.AppendLine($"// {supplementalUrl} SHA-256: {supplementalSha256}");
        source.AppendLine($"// {likelySubtagsUrl} SHA-256: {likelySubtagsSha256}");
        source.AppendLine($"// {rootUrl} SHA-256: {rootSha256}");
        source.AppendLine($"// {englishUrl} SHA-256: {englishSha256}");
        foreach (LocaleSource localeSource in localeSources.Values)
        {
            source.AppendLine(
                $"// {localeSource.Url} SHA-256: {localeSource.Sha256}");
        }
        source.AppendLine("// </auto-generated>");
        source.AppendLine();
        source.AppendLine("#nullable enable");
        source.AppendLine();
        source.AppendLine("using CalculatorApp.ViewModel.Common;");
        source.AppendLine();
        source.AppendLine("namespace CalculatorApp.ViewModel.DataLoaders;");
        source.AppendLine();
        source.AppendLine(
            "internal readonly record struct CldrCurrencyDisplayData(" +
            "string Name, string Symbol, bool HasLocalizedSymbol);");
        source.AppendLine(
            "internal readonly record struct CldrUnitDisplayData(" +
            "bool UnitAfterValue, bool UseSpace);");
        source.AppendLine();
        source.AppendLine("internal static class CldrCurrencyData");
        source.AppendLine("{");
        source.AppendLine($"    internal const string Version = {Quote(cldrVersion)};");
        source.AppendLine($"    internal const string SourceReference = {Quote(cldrReference)};");
        source.AppendLine($"    internal const string SupplementalSource = {Quote(supplementalUrl)};");
        source.AppendLine($"    internal const string LikelySubtagsSource = {Quote(likelySubtagsUrl)};");
        source.AppendLine($"    internal const string RootSource = {Quote(rootUrl)};");
        source.AppendLine($"    internal const string EnglishSource = {Quote(englishUrl)};");
        source.AppendLine($"    internal const string LocaleSourceRoot = {Quote(localeSourceRoot)};");
        source.AppendLine($"    internal const int DefaultFractionDigits = {fractions.DefaultDigits};");
        source.AppendLine();
        source.AppendLine("    private static readonly IReadOnlyDictionary<string, CldrCurrencyDisplayData> English =");
        source.AppendLine("        new Dictionary<string, CldrCurrencyDisplayData>(StringComparer.OrdinalIgnoreCase)");
        source.AppendLine("        {");
        foreach ((string isoCode, CurrencyDisplayData display) in currencies)
        {
            source.AppendLine(
                $"            [{Quote(isoCode)}] = new(" +
                $"{Quote(display.Name)}, {Quote(display.Symbol)}, " +
                $"{display.HasLocalizedSymbol.ToString().ToLowerInvariant()}),");
        }
        source.AppendLine("        };");
        source.AppendLine();
        source.AppendLine("    private static readonly IReadOnlyDictionary<int, CldrUnitDisplayData> EnglishUnits =");
        source.AppendLine("        new Dictionary<int, CldrUnitDisplayData>");
        source.AppendLine("        {");
        foreach ((string enumName, UnitDisplayData display) in unitDisplays.English)
        {
            source.AppendLine(
                $"            [(int)UnitConverterUnits.{enumName}] = new(" +
                $"{display.UnitAfterValue.ToString().ToLowerInvariant()}, " +
                $"{display.UseSpace.ToString().ToLowerInvariant()}),");
        }
        source.AppendLine("        };");
        source.AppendLine();
        foreach ((string cultureName, SortedDictionary<string, CurrencyDisplayData> displays) in localizedCurrencies)
        {
            string className = GetLocaleClassName(cultureName);
            source.AppendLine($"    private static class {className}");
            source.AppendLine("    {");
            source.AppendLine("        internal static readonly IReadOnlyDictionary<string, CldrCurrencyDisplayData> Values =");
            source.AppendLine("            new Dictionary<string, CldrCurrencyDisplayData>(StringComparer.OrdinalIgnoreCase)");
            source.AppendLine("            {");
            foreach ((string isoCode, CurrencyDisplayData display) in displays)
            {
                source.AppendLine(
                    $"                [{Quote(isoCode)}] = new(" +
                    $"{Quote(display.Name)}, {Quote(display.Symbol)}, " +
                    $"{display.HasLocalizedSymbol.ToString().ToLowerInvariant()}),");
            }
            source.AppendLine("            };");
            source.AppendLine();
            source.AppendLine("        internal static readonly IReadOnlyDictionary<int, CldrUnitDisplayData> Units =");
            source.AppendLine("            new Dictionary<int, CldrUnitDisplayData>");
            source.AppendLine("            {");
            foreach ((string enumName, UnitDisplayData display) in unitDisplays.Localized[cultureName])
            {
                source.AppendLine(
                    $"                [(int)UnitConverterUnits.{enumName}] = new(" +
                    $"{display.UnitAfterValue.ToString().ToLowerInvariant()}, " +
                    $"{display.UseSpace.ToString().ToLowerInvariant()}),");
            }
            source.AppendLine("            };");
            source.AppendLine("    }");
            source.AppendLine();
        }
        source.AppendLine("    private static readonly IReadOnlyDictionary<string, int> FractionDigitExceptions =");
        source.AppendLine("        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)");
        source.AppendLine("        {");
        foreach ((string isoCode, int digits) in fractions.Exceptions)
        {
            source.AppendLine($"            [{Quote(isoCode)}] = {digits},");
        }
        source.AppendLine("        };");
        source.AppendLine();
        source.AppendLine("    internal static bool TryGet(");
        source.AppendLine("        string cultureName,");
        source.AppendLine("        string isoCode,");
        source.AppendLine("        out CldrCurrencyDisplayData display)");
        source.AppendLine("    {");
        source.AppendLine("        display = default;");
        source.AppendLine("        bool found = cultureName switch");
        source.AppendLine("        {");
        foreach (string cultureName in localizedCurrencies.Keys)
        {
            source.AppendLine(
                $"            {Quote(cultureName)} => {GetLocaleClassName(cultureName)}.Values.TryGetValue(isoCode, out display),");
        }
        source.AppendLine("            _ => false");
        source.AppendLine("        };");
        source.AppendLine();
        source.AppendLine("        return found || English.TryGetValue(isoCode, out display);");
        source.AppendLine("    }");
        source.AppendLine();
        source.AppendLine("    internal static CldrUnitDisplayData GetUnitDisplay(");
        source.AppendLine("        string cultureName,");
        source.AppendLine("        int unitId)");
        source.AppendLine("    {");
        source.AppendLine("        IReadOnlyDictionary<int, CldrUnitDisplayData>? localized = cultureName switch");
        source.AppendLine("        {");
        foreach (string cultureName in localizedCurrencies.Keys)
        {
            source.AppendLine(
                $"            {Quote(cultureName)} => {GetLocaleClassName(cultureName)}.Units,");
        }
        source.AppendLine("            _ => null");
        source.AppendLine("        };");
        source.AppendLine();
        source.AppendLine("        if (localized is not null && localized.TryGetValue(unitId, out CldrUnitDisplayData display))");
        source.AppendLine("        {");
        source.AppendLine("            return display;");
        source.AppendLine("        }");
        source.AppendLine();
        source.AppendLine("        return EnglishUnits.TryGetValue(unitId, out display)");
        source.AppendLine("            ? display");
        source.AppendLine("            : new CldrUnitDisplayData(true, true);");
        source.AppendLine("    }");
        source.AppendLine();
        source.AppendLine("    internal static int GetFractionDigits(string isoCode) =>");
        source.AppendLine("        FractionDigitExceptions.TryGetValue(isoCode, out int digits)");
        source.AppendLine("            ? digits");
        source.AppendLine("            : DefaultFractionDigits;");
        source.AppendLine("}");
        return source.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static string GetLocaleClassName(string cultureName)
    {
        StringBuilder className = new("Locale");
        foreach (string part in cultureName.Split('-'))
        {
            className.Append(char.ToUpperInvariant(part[0]));
            className.Append(part.AsSpan(1));
        }

        return className.ToString();
    }

    private static string Quote(string value)
    {
        StringBuilder result = new(value.Length + 2);
        result.Append('"');
        foreach (char character in value)
        {
            result.Append(character switch
            {
                '\\' => "\\\\",
                '"' => "\\\"",
                '\r' => "\\r",
                '\n' => "\\n",
                '\t' => "\\t",
                _ => character.ToString()
            });
        }
        result.Append('"');
        return result.ToString();
    }

    private static void PrintUsage() => Console.WriteLine(
        """
        Usage:
          dotnet run Tools/Cldr/GenerateCurrencyData.cs -- [options]

        Options:
          --cldr-ref <git-ref>    CLDR release tag, or 'latest' (default: release-48-2)
          --cldr-version <value>  Version embedded in generated source (default: 48.2)
          --output <path>         Generated C# output path
          --resources <path>      Directory containing shipped Resources.*.resx catalogs
          --units <path>          Calculator UnitConverterUnits source path
          --check                 Fail if the checked-in generated source differs
          --help                  Show this help
        """);

    private sealed record Options(
        string CldrReference,
        string CldrVersion,
        string OutputPath,
        string ResourcesPath,
        string UnitDefinitionsPath,
        bool CheckOnly,
        bool ShowHelp)
    {
        internal static Options Parse(string[] args)
        {
            string cldrReference = DefaultCldrReference;
            string cldrVersion = DefaultCldrVersion;
            string outputPath = DefaultOutputPath;
            string resourcesPath = DefaultResourcesPath;
            string unitDefinitionsPath = DefaultUnitDefinitionsPath;
            bool checkOnly = false;
            bool showHelp = false;

            for (int index = 0; index < args.Length; index++)
            {
                switch (args[index])
                {
                    case "--cldr-ref":
                        cldrReference = ReadValue(args, ref index);
                        break;
                    case "--cldr-version":
                        cldrVersion = ReadValue(args, ref index);
                        break;
                    case "--output":
                        outputPath = ReadValue(args, ref index);
                        break;
                    case "--resources":
                        resourcesPath = ReadValue(args, ref index);
                        break;
                    case "--units":
                        unitDefinitionsPath = ReadValue(args, ref index);
                        break;
                    case "--check":
                        checkOnly = true;
                        break;
                    case "--help" or "-h":
                        showHelp = true;
                        break;
                    default:
                        throw new ArgumentException($"Unknown option: {args[index]}");
                }
            }

            if (string.Equals(cldrReference, "latest", StringComparison.OrdinalIgnoreCase) &&
                cldrVersion != DefaultCldrVersion)
            {
                throw new ArgumentException(
                    "--cldr-version cannot be combined with --cldr-ref latest.");
            }

            return new Options(
                cldrReference,
                cldrVersion,
                outputPath,
                resourcesPath,
                unitDefinitionsPath,
                checkOnly,
                showHelp);
        }

        private static string ReadValue(string[] args, ref int index)
        {
            if (++index >= args.Length || args[index].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Missing value for {args[index - 1]}.");
            }

            return args[index];
        }
    }

    private sealed record FractionData(
        int DefaultDigits,
        SortedDictionary<string, int> Exceptions);

    private sealed record CurrencyDisplayData(
        string Name,
        string Symbol,
        bool HasLocalizedSymbol);

    private sealed record CurrencyDisplayPatch(string? Name, string? Symbol);

    private sealed record UnitPatternSource(string EnumName, string CldrType);

    private readonly record struct UnitDisplayData(bool UnitAfterValue, bool UseSpace);

    private sealed record UnitDisplayTables(
        SortedDictionary<string, UnitDisplayData> English,
        SortedDictionary<string, SortedDictionary<string, UnitDisplayData>> Localized);

    private sealed record LocaleIdParts(
        string Language,
        string? Script,
        string? Region)
    {
        public override string ToString() => string.Join(
            '_',
            new[] { Language, Script, Region }.Where(part => part is not null));
    }

    private sealed record LocaleRequest(
        string ResourceCulture,
        string SourceLocaleId,
        string MaximizedLocaleId);

    private sealed record LocaleDefinition(
        string ResourceCulture,
        IReadOnlyList<string> CldrLocaleIds);

    private sealed record LocaleResolution(
        IReadOnlyList<LocaleDefinition> Locales,
        SortedDictionary<string, LocaleSource> Sources);

    private sealed record LocaleSource(
        string CldrLocaleId,
        string Url,
        string Sha256,
        SortedDictionary<string, CurrencyDisplayPatch> Currencies,
        SortedDictionary<string, string> UnitPatterns);
}
