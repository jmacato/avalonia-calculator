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
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.System;
using Windows.Storage.Streams;
using Graphing;
using GraphControl;

namespace GraphControl
{
    public enum EvaluationErrorCode
    {
        None = 0,
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
        GraphingEngineNotSupported = -503,
        GraphingEngineGeneralError = -504,
        GraphingEngineTooComplexToSolve = -506
    }
}
