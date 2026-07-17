using System;
using System.Collections.Generic;
using System.Collections;

namespace CSharpMath.Atom;

using Atoms;

internal sealed class DisabledMathList : MathList
{
    public override void Add(MathAtom? item) => throw new InvalidOperationException("Scripts are not allowed!");
    public override void Append(IEnumerable<MathAtom> list) => throw new InvalidOperationException("Scripts are not allowed!");
}
