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
    internal enum EvaluationErrorCode
    {
        Overflow = 2,
        RequireRadiansMode = 3,
        TooComplexToSolve = 4,
        RequireDegreesMode = 5,
        FactorialInvalidArgument = -1,
        Factorial2InvalidArgument = -2,
        FactorialCannotPerformOnLargeNumber = -3,
        ModuloCannotPerformOnFloat = -5,
        EquationTooComplexToSolveSymbolic = -7,
        EquationHasNoSolution = -8,
        EquationTooComplexToSolve = -9,
        EquationTooComplexToPlot = -10,
        DivideByZero = -15,
        InequalityTooComplexToSolve = -41,
        InequalityHasNoSolution = -42,
        MutuallyExclusiveConditions = -43,
        OutOfDomain = -101,
        GE_NotSupported = -503,
        GE_GeneralError = -504,
        GE_TooComplexToSolve = -506
    }
}
