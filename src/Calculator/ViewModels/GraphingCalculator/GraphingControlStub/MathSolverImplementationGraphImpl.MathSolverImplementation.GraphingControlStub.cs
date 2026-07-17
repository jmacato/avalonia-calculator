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
    internal sealed class MathSolverImplementationGraphImpl : IGraph
    {
        private readonly MathSolverImplementationGraphOptionsImpl _options = new MathSolverImplementationGraphOptionsImpl();
        private readonly MathSolverImplementationGraphRendererImpl _renderer = new MathSolverImplementationGraphRendererImpl();
        private readonly MathSolverImplementationGraphAnalyzerImpl _analyzer = new MathSolverImplementationGraphAnalyzerImpl();
        public IReadOnlyList<IEquation> TryInitialize(IExpression? graphingExp = null)
        {
            return new List<IEquation>
            {
                new MathSolverImplementationEquationImpl()
            };
        }

        public int GetInitializationError() => 0;
        public IGraphingOptions GetOptions() => _options;
        public IReadOnlyList<IVariable> GetVariables() => new List<IVariable>();
        public void SetArgValue(string variableName, double value)
        {
        }

        public IGraphRenderer GetRenderer() => _renderer;
        public bool TryResetSelection() => true;
        public IGraphAnalyzer GetAnalyzer() => _analyzer;
    }
}
