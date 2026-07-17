using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;

namespace CSharpMath;

using Display;
using Display.FrontEnd;

internal enum ExtensionsNullHandling
{
    ///<summary>the string "null", without the quotes</summary>
    LiteralNull,
    /// <summary>Change the null to the empty string, then wrap.</summary>
    EmptyContent,
    /// <summary>Return the empty string. Do not wrap.</summary>
    EmptyString,
}
