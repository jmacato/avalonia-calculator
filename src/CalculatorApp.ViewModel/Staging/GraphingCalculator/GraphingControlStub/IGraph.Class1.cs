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

namespace Graphing
{
    public interface IGraph
    {
        IReadOnlyList<IEquation> TryInitialize(IExpression? graphingExp = null);
        int GetInitializationError();
        IGraphingOptions GetOptions();
        IReadOnlyList<IVariable> GetVariables();
        void SetArgValue(string variableName, double value);
        IGraphRenderer GetRenderer();
        bool TryResetSelection();
        IGraphAnalyzer GetAnalyzer();
    }
}
