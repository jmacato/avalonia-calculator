// Copyright (c) jmacato. All rights reserved.
// Licensed under the MIT License.

internal sealed record CldrCurrencyGeneratorOptions(string CldrReference, string CldrVersion, string OutputPath, string ResourcesPath, string UnitDefinitionsPath, bool CheckOnly, bool ShowHelp)
{
    internal static CldrCurrencyGeneratorOptions Parse(string[] args)
    {
        string cldrReference = CldrCurrencyGenerator.DefaultCldrReference;
        string cldrVersion = CldrCurrencyGenerator.DefaultCldrVersion;
        string outputPath = CldrCurrencyGenerator.DefaultOutputPath;
        string resourcesPath = CldrCurrencyGenerator.DefaultResourcesPath;
        string unitDefinitionsPath = CldrCurrencyGenerator.DefaultUnitDefinitionsPath;
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
            cldrVersion != CldrCurrencyGenerator.DefaultCldrVersion)
        {
            throw new ArgumentException("--cldr-version cannot be combined with --cldr-ref latest.");
        }

        return new CldrCurrencyGeneratorOptions(cldrReference, cldrVersion, outputPath, resourcesPath, unitDefinitionsPath, checkOnly, showHelp);
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
