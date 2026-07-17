using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace CSharpMath.Atom;

using Atoms;
using Space = Atoms.Space;
using Structures;
using static Structures.Result;
using InvalidCodePathException = Structures.InvalidCodePathException;

public sealed class LaTeXParserInnerEnvironment : LaTeXParserEnvironment
{
    public Boundary? RightBoundary { get; set; }
}
