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

namespace Graphing
{
    internal interface IMathSolver
    {
        IParsingOptions ParsingOptions();
        IEvalOptions EvalOptions();
        IFormatOptions FormatOptions();
        IExpression ParseInput(string input, out int errorCode, out int errorType);
        void HRErrorToErrorInfo(int hr, out int errorCode, out int errorType);
        IGraph CreateGrapher(IExpression expression);
        IGraph CreateGrapher();
        string Serialize(IExpression expression);
        IGraphFunctionAnalysisData Analyze(IGraphAnalyzer analyzer);
        IMathSolver CreateMathSolver();
    }
}
