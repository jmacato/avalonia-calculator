using System;
using System.Collections.Generic;
using System.Linq;

namespace CSharpMath.Structures;

/// <summary>
/// Maps LaTeX commands and literal prefixes to parsed values.
/// </summary>
public sealed class LaTeXCommandMap<TValue> : InitializerCollection<string, TValue>
{
    private readonly LaTeXCommandDefaultParser<TValue> _defaultParser;
    private readonly LaTeXCommandDefaultParser<TValue> _defaultParserForCommands;
    private readonly SortedSet<(string NonCommand, TValue Value)> _nonCommands =
        new(new DescendingStringComparer<TValue>());
    private readonly Dictionary<string, TValue> _commands = new();

    public LaTeXCommandMap(
        LaTeXCommandDefaultParser<TValue> defaultParser,
        LaTeXCommandDefaultParser<TValue> defaultParserForCommands,
        Action<string, TValue>? extraAddAction = null) : base(extraAddAction)
    {
        _defaultParser = defaultParser;
        _defaultParserForCommands = defaultParserForCommands;
    }

    protected override void OnAdded(string key, TValue value)
    {
        if (key.AsSpan().StartsWithInvariant(@"\"))
        {
            if (SplitCommand(key.AsSpan()) == key.Length - 1)
                throw new ArgumentException("Key is unreachable: " + key, nameof(key));
            _commands.Add(key, value);
        }
        else
        {
            _nonCommands.Add((key, value));
        }
    }

    public override IEnumerator<KeyValuePair<string, TValue>> GetEnumerator() =>
        _nonCommands
            .Select(entry => new KeyValuePair<string, TValue>(entry.NonCommand, entry.Value))
            .Concat(_commands)
            .GetEnumerator();

    /// <summary>Finds the number of characters in a LaTeX command at the start of <paramref name="characters"/>.</summary>
    private static int SplitCommand(ReadOnlySpan<char> characters)
    {
        System.Diagnostics.Debug.Assert(characters[0] == '\\');
        var splitIndex = 1;
        if (splitIndex >= characters.Length)
            return splitIndex;
        if (IsEnglishAlphabetOrAt(characters[splitIndex]))
        {
            do
            {
                splitIndex++;
            }
            while (splitIndex < characters.Length && IsEnglishAlphabetOrAt(characters[splitIndex]));

            if (splitIndex >= characters.Length)
                return splitIndex;
            if (characters[splitIndex] is '*' or '=' or '\'')
                splitIndex++;
        }
        else
        {
            splitIndex++;
        }

        return splitIndex;

        static bool IsEnglishAlphabetOrAt(char value) =>
            value is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or '@';
    }

    /// <summary>Looks up the command or literal prefix at the start of <paramref name="characters"/>.</summary>
    public Result<(TValue Result, int SplitIndex)> TryLookup(ReadOnlySpan<char> characters)
    {
        Result<(TValue Result, int SplitIndex)> TryLookupCommand(ReadOnlySpan<char> input)
        {
            var splitIndex = SplitCommand(input);
            var lookup = input[..splitIndex];
            while (splitIndex < input.Length && char.IsWhiteSpace(input[splitIndex]))
                splitIndex++;
            return _commands.TryGetValue(lookup.ToString(), out var result)
                ? Result.Ok((result, splitIndex))
                : _defaultParserForCommands(lookup);
        }

        Result<(TValue Result, int SplitIndex)> TryLookupNonCommand(ReadOnlySpan<char> input)
        {
            foreach (var (nonCommand, value) in _nonCommands)
            {
                if (input.StartsWith(nonCommand.AsSpan(), StringComparison.Ordinal))
                    return Result.Ok((value, nonCommand.Length));
            }

            return _defaultParser(input);
        }

        if (characters.IsEmpty)
            throw new ArgumentException("There are no characters to read.", nameof(characters));
        return characters.StartsWithInvariant(@"\")
            ? TryLookupCommand(characters)
            : TryLookupNonCommand(characters);
    }
}
