using System;
using CSharpMath.Structures;

namespace CSharpMath.Structures;
//For Result<string> where both implicit conversions fight over each other,
//use Err(string) there instead
public readonly record struct ResultImplicitError
{
    public ResultImplicitError(string error) =>
        Error = error ?? throw new ArgumentNullException(nameof(error));

    public string Error { get; }
}
