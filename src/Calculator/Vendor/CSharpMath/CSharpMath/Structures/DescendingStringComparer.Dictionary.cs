using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace CSharpMath.Structures;
/// <summary>Ensures that longer <see cref = "string "/>s with same beginnings are listed first, to be matched first.</summary>
sealed class DescendingStringComparer<TValue> : IComparer<(string NonCommand, TValue Value)>
{
    public int Compare((string NonCommand, TValue Value) x, (string NonCommand, TValue Value) y) => string.CompareOrdinal(y.NonCommand, x.NonCommand);
}
