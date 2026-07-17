using System;
using System.Collections.Generic;
using Windows.UI;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.System;
using Windows.Storage.Streams;
using Windows.System;
using Graphing;
using GraphControl;
using Microsoft.UI;
using Microsoft.UI.Windowing;

namespace GraphControl
{
    internal enum SyntaxErrorCode
    {
        // found ) without matching (
        ParenthesisMismatch = 1,
        // found ( without matching )
        UnmatchedParenthesis = 2,
        // more than 1 decimal point in a number. Example: 7.3.2
        TooManyDecimalPoints = 3,
        // decimal point on its own without any digits surrounding it. Example: 3+.+4
        DecimalPointWithoutDigits = 4,
        // example: 3-4*
        UnexpectedEndOfExpression = 5,
        // example: 3-*4
        UnexpectedToken = 6,
        // example: [    (or many other special characters), another example: "3,5" (comma is invalid here)
        InvalidToken = 7,
        // example: solve(x+3=8=x)
        TooManyEquals = 8,
        // example: ploteq(4+83=9)
        EqualWithoutGraphVariable = 10,
        // <para>example: ploteq(x+y)        (expecting "=" in equation ploting)</para>
        // <para>example2: Solve(5*x+9)    (expecting = in the equation solving)</para>
        InvalidEquationSyntax = 11,
        // there is nothing in the expression
        EmptyExpression = 12,
        // example: factor(x=3) (expecting solve(x=3)).
        EqualWithoutEquation = 14,
        // example: solve( (x=3)*2 )
        InvalidEquationFormat = 15,
        // This error only occurs when CasContext.ParsingOptions.AllowImplicitParentheses == false.
        // example: sin a    (expecting sin(a))
        ExpectParenthesisAfterFunctionName = 25,
        // example: root(a)    (expecting 2 parameters)
        IncorrectNumParameter = 26,
        // exmaple: "x_", "x_@", "x__1"
        InvalidVariableNameFormat = 32,
        // found } without matching {
        BracketMismatch = 34,
        // found { without matching }
        UnmatchedBracket = 35,
        // syntax error in MathML format. Used only if CasContext.ParsingOptions.FormatType is MathML or MathMLNoWrapper
        InvalidMathMLFormat = 40,
        // The input has an unknown MathML entity. Used only if CasContext.ParsingOptions.FormatType is MathML or MathMLNoWrapper
        UnknownMathMLEntity = 41,
        // The input has an unknown MathML element. Used only if CasContext.ParsingOptions.FormatType is MathML or MathMLNoWrapper
        UnknownMathMLElement = 42,
        // "i" and "I" cannot be used as variable names in real number field
        CannotUseIInReal = 48,
        // General error
        GeneralError = 52,
        // used in parsing numbers with arbitrary bases. example: base(2, 1020), base(16, 1AG)
        InvalidNumberDigit = 55,
        // a valid number base must be an integer >=2 and &lt;=36
        InvalidNumberBase = 56,
        // some functions require a variable in certain argument position. e.g. 2nd argument of deriv, integral, limit, etc.
        // this error code is used if the argument at the position is not a variable
        InvalidVariableSpecification = 57,
        // all operands of logical operators must be logical. example: "true and 1"
        ExpectingLogicalOperands = 58,
        // all operands of a non-logical operator must not be logical. example: "sin(true)"
        ExpectingScalarOperands = 59,
        // a list can contain logicals or scalars, but not both.
        CannotMixLogicalScalarInList = 60,
        // in definite integral, seriesSum and seriesProduct, the index variable is used in the lower/upper limits.
        // example: integral(sin(x), x, 0, x)
        CannotUseIndexVarInOpLimits = 61,
        // in limit, the index variable is used in the limit point
        // example: limit(sin(x), x, x-1)
        CannotUseIndexVarInLimPoint = 62,
        /// ComplexInfinity cannot be used in real number field
        CannotUseComplexInfinityInReal = 72,
        // complex numbers are not allowed in inequality solving
        CannotUseIInInequalitySolving = 123,
        // Indicate a bug in the MathRichEdit serializer
        RichEditSerializationError = 201,
        // can't initialize math zone in richedit, meaning it's the wrong version richedit dll, need reinstall
        RichEditInitialization = 202,
        // indicate bug in either richedit or richedit wrapper
        RichEditInlineObjectStructure = 203,
        // in a structure like integral, sum, product, one of the boxes is not filled
        RichEditMissingArgument = 204,
        // errors in richedit wrapper that are not specifically handled for
        RichEditGeneralError = 210,
    }
}
