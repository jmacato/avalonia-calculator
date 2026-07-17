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
    // This would be implemented to connect to native C++ code
    internal sealed class MathSolverImplementation : IMathSolver
    {
        private readonly MathSolverImplementationParsingOptionsImpl _parsingOptions = new MathSolverImplementationParsingOptionsImpl();
        private readonly MathSolverImplementationEvalOptionsImpl _evalOptions = new MathSolverImplementationEvalOptionsImpl();
        private readonly MathSolverImplementationFormatOptionsImpl _formatOptions = new MathSolverImplementationFormatOptionsImpl();
        public IParsingOptions ParsingOptions() => _parsingOptions;
        public IEvalOptions EvalOptions() => _evalOptions;
        public IFormatOptions FormatOptions() => _formatOptions;
        public IExpression ParseInput(string input, out int errorCode, out int errorType)
        {
            errorCode = 0;
            errorType = 0;
            return new MathSolverImplementationExpressionImpl();
        }

        public void HRErrorToErrorInfo(int hr, out int errorCode, out int errorType)
        {
            errorCode = 0;
            errorType = 0;
        }

        public IGraph CreateGrapher(IExpression expression) => new MathSolverImplementationGraphImpl();
        public IGraph CreateGrapher() => new MathSolverImplementationGraphImpl();
        public string Serialize(IExpression expression) => string.Empty;
        public IGraphFunctionAnalysisData Analyze(IGraphAnalyzer analyzer) => new IGraphFunctionAnalysisData();
        public IMathSolver CreateMathSolver()
        {
            return new MathSolverImplementation();
        }
    }
}
