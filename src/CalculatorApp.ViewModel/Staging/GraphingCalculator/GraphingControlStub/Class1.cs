using System; 
using System.Collections.Generic;
using Windows.UI; 
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI;
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
    public enum LocalizationType
    {
        Unknown,
        DecimalPointAndListComma,
        DecimalPointAndListSemicolon,
        DecimalCommaAndListSemicolon
    }

    public enum FormatType
    {
        Formula,
        InvariantFormula,
        FormuaWithoutAggregate,
        Linear,
        LinearInput,
        MathML,
        MathMLNoWrapper,
        MathRichEdit,
        InlineMathRichEdit,
        Binary,
        InvariantBinary,
        Base64,
        InvariantBase64,
        Latex
    }

    public enum EvalTrigUnitMode
    {
        Invalid,
        Radians,
        Degrees,
        Grads
    }

    public enum LineStyle
    {
        Solid,
        Dot,
        Dash,
        DashDot,
        DashDotDot
    }

    [Flags]
    public enum PerformAnalysisType : uint
    {
        Domain = 0x01,
        Range = 0x02,
        Parity = 0x04,
        InterceptionPointsWithXAndYAxis = 0x08,
        CriticalPoints = 0x10,
        Asymptotes = 0x20,
        Monotonicity = 0x40,
        Period = 0x80,
        All = 0xFF
    }

    public enum AnalysisType
    {
        Domain = 0,
        Range = 1,
        Parity = 2,
        Zeros = 3,
        YIntercept = 4,
        Minima = 5,
        Maxima = 6,
        InflectionPoints = 7,
        VerticalAsymptotes = 8,
        HorizontalAsymptotes = 9,
        ObliqueAsymptotes = 10,
        Monotonicity = 11,
        Period = 12
    }

    public enum FunctionParityType
    {
        Unknown = 0,
        Odd = 1,
        Even = 2,
        None = 3
    }

    public enum ChangeRangeAction
    {
        ZoomIn,
        ZoomOut,
        WidenX,
        ShrinkX,
        WidenY,
        ShrinkY,
        WidenZ,
        ShrinkZ,
        MoveNegativeX,
        MovePositiveX,
        MoveNegativeY,
        MovePositiveY,
        MoveNegativeZ,
        MovePositiveZ,
        SmoothZoomIn,
        SmoothZoomOut,
        PinchZoomIn,
        PinchZoomOut
    }

    public enum GraphAnalyzerMessage
    {
        None = 0,
        NoZeros = 1,
        NoYIntercept = 2,
        NoMinima = 3,
        NoMaxima = 4,
        NoInflectionPoints = 5,
        NoVerticalAsymptotes = 6,
        NoHorizontalAsymptotes = 7,
        NoObliqueAsymptotes = 8,
        NotAbleToCalculate = 9,
        NotAbleToMarkAllGraphFeatures = 10,
        TheseFeaturesAreTooComplexToCalculate = 11,
        ThisFeatureIsTooComplexToCalculate = 12
    }
}

namespace GraphControl
{
    public enum EquationLineStyle
    {
        Solid,
        Dot,
        Dash,
        DashDot,
        DashDotDot
    }

    public enum ErrorType
    {
        Evaluation,
        Syntax,
        Abort
    }

    public enum EvaluationErrorCode
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

    public enum GraphViewChangedReason
    {
        Manipulation,
        Reset
    }

    [Flags]
   public  enum KeyGraphFeaturesFlag
    {
        Domain = 1,
        Range = 2,
        Parity = 4,
        Periodicity = 8,
        Zeros = 16,
        YIntercept = 32,
        Minima = 64,
        Maxima = 128,
        InflectionPoints = 256,
        VerticalAsymptotes = 512,
        HorizontalAsymptotes = 1024,
        ObliqueAsymptotes = 2048,
        MonotoneIntervals = 4096
    };

    public  enum AnalysisErrorType
    {
        NoError,
        AnalysisCouldNotBePerformed,
        AnalysisNotSupported,
        VariableIsNotX
    };




    public enum   SyntaxErrorCode
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

namespace Graphing
{
    #region Common Base Interfaces

    public interface IExpression
    {
        uint ExpressionID { get; }
        bool IsEmptySet { get; }
    }

    public interface IVariable
    {
        int VariableID { get; }
        string VariableName { get; }
    }

    public interface IExpressible
    {
        IExpression GetExpression();
    }

    #endregion

    #region Options Interfaces

    public interface IParsingOptions
    {
        void SetFormatType(FormatType type);
        void SetLocalizationType(LocalizationType value);
    }

    public interface IEvalOptions
    {
        EvalTrigUnitMode TrigUnitMode { get; set; }
    }

    public interface IFormatOptions
    {
        void SetFormatType(FormatType type);
        void SetMathMLPrefix(string value);
        void SetLocalizationType(LocalizationType value);
    }

    public interface IGraphingOptions
    {
        bool MarkZeros { get; set; }
        bool MarkYIntercept { get; set; }
        bool MarkMinima { get; set; }
        bool MarkMaxima { get; set; }
        bool MarkInflectionPoints { get; set; }
        bool MarkVerticalAsymptotes { get; set; }
        bool MarkHorizontalAsymptotes { get; set; }
        bool MarkObliqueAsymptotes { get; set; }

        ulong MaxExecutionTime { get; set; }
        void ResetMaxExecutionTime();

        IReadOnlyList<Color> GetGraphColors();
        bool SetGraphColors(IReadOnlyList<Color> colors);
        void ResetGraphColors();

        Color BackColor { get; set; }
        void ResetBackColor();

        bool AllowKeyGraphFeaturesForFunctionsWithParameters { get; set; }
        void ResetAllowKeyGraphFeaturesForFunctionsWithParameters();

        Color ZerosColor { get; set; }
        void ResetZerosColor();

        Color ExtremaColor { get; set; }
        void ResetExtremaColor();

        Color InflectionPointsColor { get; set; }
        void ResetInflectionPointsColor();

        Color AsymptotesColor { get; set; }
        void ResetAsymptotesColor();

        Color AxisColor { get; set; }
        void ResetAxisColor();

        Color BoxColor { get; set; }
        void ResetBoxColor();

        Color GridColor { get; set; }
        void ResetGridColor();

        Color FontColor { get; set; }
        void ResetFontColor();

        bool ShowAxis { get; set; }
        void ResetShowAxis();

        bool ShowGrid { get; set; }
        void ResetShowGrid();

        bool ShowBox { get; set; }
        void ResetShowBox();

        bool ForceProportional { get; set; }
        void ResetForceProportional();

        string AliasX { get; set; }
        void ResetAliasX();

        string AliasY { get; set; }
        void ResetAliasY();

        LineStyle LineStyle { get; set; }
        void ResetLineStyle();

        Tuple<double, double> GetDefaultXRange();
        bool SetDefaultXRange(Tuple<double, double> minmax);
        void ResetDefaultXRange();

        Tuple<double, double> GetDefaultYRange();
        bool SetDefaultYRange(Tuple<double, double> minmax);
        void ResetDefaultYRange();
    }

    public interface IEquationOptions
    {
        Color GetGraphColor();
        void SetGraphColor(Color color);
        void ResetGraphColor();

        LineStyle GetLineStyle();
        void SetLineStyle(LineStyle value);
        void ResetLineStyle();

        float GetLineWidth();
        void SetLineWidth(float value);
        void ResetLineWidth();

        float GetSelectedEquationLineWidth();
        void SetSelectedEquationLineWidth(float value);
        void ResetSelectedEquationLineWidth();

        float GetPointRadius();
        void SetPointRadius(float value);
        void ResetPointRadius();

        float GetSelectedEquationPointRadius();
        void SetSelectedEquationPointRadius(float value);
        void ResetSelectedEquationPointRadius();
    }

    #endregion

    #region Math and Graph Interfaces

    public interface IMathSolver
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

    public interface IEquation
    {
        IEquationOptions GetGraphEquationOptions();
        uint GraphEquationID { get; }

        bool TrySelectEquation();
        bool IsEquationSelected { get; }
    }

    public interface IGraph
    {
        IReadOnlyList<IEquation> TryInitialize(IExpression graphingExp = null);
        int GetInitializationError();

        IGraphingOptions GetOptions();
        IReadOnlyList<IVariable> GetVariables();

        void SetArgValue(string variableName, double value);

        IGraphRenderer GetRenderer();

        bool TryResetSelection();

        IGraphAnalyzer GetAnalyzer();
    }

    public interface IGraphAnalyzer
    {
        bool CanFunctionAnalysisBePerformed(out bool variableIsNotX);
        int PerformFunctionAnalysis(uint analysisType);
        int GetAnalysisTypeCaption(AnalysisType type, out string captionOut);
        int GetMessage(GraphAnalyzerMessage msg, out string msgOut);
    }

    public interface IGraphRenderer
    {
        int SetGraphSize(uint width, uint height);
        int SetDpi(float dpiX, float dpiY);

        int DrawD2D1(object d2dFactory, object renderTarget, out bool hasSomeMissingDataOut);

        int GetClosePointData(
            double inScreenPointX,
            double inScreenPointY,
            double precision,
            out int formulaIdOut,
            out float xScreenPointOut,
            out float yScreenPointOut,
            out double xValueOut,
            out double yValueOut,
            out double rhoValueOut,
            out double thetaValueOut,
            out double tValueOut);

        int ScaleRange(double centerX, double centerY, double scale);
        int ChangeRange(ChangeRangeAction action);
        int MoveRangeByRatio(double ratioX, double ratioY);
        int ResetRange();
        int GetDisplayRanges(out double xMin, out double xMax, out double yMin, out double yMax);
        int SetDisplayRanges(double xMin, double xMax, double yMin, double yMax);
        int PrepareGraph();

        int GetBitmap(out IBitmap bitmapOut, out bool hasSomeMissingDataOut);
    }

    public interface IBitmap
    {
        byte[] GetData();
    }

    public struct IGraphFunctionAnalysisData
    {
        public string Domain { get; set; }
        public string Range { get; set; }
        public int Parity { get; set; }
        public int PeriodicityDirection { get; set; }
        public string PeriodicityExpression { get; set; }
        public string Zeros { get; set; }
        public string YIntercept { get; set; }
        public List<string> Minima { get; set; }
        public List<string> Maxima { get; set; }
        public List<string> InflectionPoints { get; set; }
        public List<string> VerticalAsymptotes { get; set; }
        public List<string> HorizontalAsymptotes { get; set; }
        public List<string> ObliqueAsymptotes { get; set; }
        public Dictionary<string, int> MonotoneIntervals { get; set; }
        public KeyGraphFeaturesFlag TooComplexFeatures { get; set; }
    }

    #endregion

    #region Implementation Classes (Stubs)

    // This would be implemented to connect to native C++ code
    internal class MathSolverImplementation : IMathSolver
    {
        private class ParsingOptionsImpl : IParsingOptions
        {
            public void SetFormatType(FormatType type) { }
            public void SetLocalizationType(LocalizationType value) { }
        }

        private class EvalOptionsImpl : IEvalOptions
        {
            public EvalTrigUnitMode TrigUnitMode { get; set; } = EvalTrigUnitMode.Radians;
        }

        private class FormatOptionsImpl : IFormatOptions
        {
            public void SetFormatType(FormatType type) { }
            public void SetMathMLPrefix(string value) { }
            public void SetLocalizationType(LocalizationType value) { }
        }

        private readonly ParsingOptionsImpl _parsingOptions = new ParsingOptionsImpl();
        private readonly EvalOptionsImpl _evalOptions = new EvalOptionsImpl();
        private readonly FormatOptionsImpl _formatOptions = new FormatOptionsImpl();

        public IParsingOptions ParsingOptions() => _parsingOptions;
        public IEvalOptions EvalOptions() => _evalOptions;
        public IFormatOptions FormatOptions() => _formatOptions;

        public IExpression ParseInput(string input, out int errorCode, out int errorType)
        {
            errorCode = 0;
            errorType = 0;
            return new ExpressionImpl();
        }

        public void HRErrorToErrorInfo(int hr, out int errorCode, out int errorType)
        {
            errorCode = 0;
            errorType = 0;
        }

        public IGraph CreateGrapher(IExpression expression) => new GraphImpl();
        public IGraph CreateGrapher() => new GraphImpl();

        public string Serialize(IExpression expression) => string.Empty;

        public IGraphFunctionAnalysisData Analyze(IGraphAnalyzer analyzer) => new IGraphFunctionAnalysisData();

        public IMathSolver CreateMathSolver()
        {
            return new MathSolverImplementation();
        }

        private class ExpressionImpl : IExpression
        {
            public uint ExpressionID => 1;
            public bool IsEmptySet => false;
        }

        private class GraphImpl : IGraph
        {
            private readonly GraphOptionsImpl _options = new GraphOptionsImpl();
            private readonly GraphRendererImpl _renderer = new GraphRendererImpl();
            private readonly GraphAnalyzerImpl _analyzer = new GraphAnalyzerImpl();

            public IReadOnlyList<IEquation> TryInitialize(IExpression graphingExp = null)
            {
                return new List<IEquation> { new EquationImpl() };
            }

            public int GetInitializationError() => 0;
            public IGraphingOptions GetOptions() => _options;
            public IReadOnlyList<IVariable> GetVariables() => new List<IVariable>();
            public void SetArgValue(string variableName, double value) { }
            public IGraphRenderer GetRenderer() => _renderer;
            public bool TryResetSelection() => true;
            public IGraphAnalyzer GetAnalyzer() => _analyzer;
        }

        private class EquationImpl : IEquation
        {
            private readonly EquationOptionsImpl _options = new EquationOptionsImpl();

            public IEquationOptions GetGraphEquationOptions() => _options;
            public uint GraphEquationID => 1;
            public bool TrySelectEquation() => true;
            public bool IsEquationSelected => false;
        }

        private class EquationOptionsImpl : IEquationOptions
        {
            public Color GetGraphColor() => new Color();
            public void SetGraphColor(Color color) { }
            public void ResetGraphColor() { }
            public LineStyle GetLineStyle() => LineStyle.Solid;
            public void SetLineStyle(LineStyle value) { }
            public void ResetLineStyle() { }
            public float GetLineWidth() => 2.0f;
            public void SetLineWidth(float value) { }
            public void ResetLineWidth() { }
            public float GetSelectedEquationLineWidth() => 3.0f;
            public void SetSelectedEquationLineWidth(float value) { }
            public void ResetSelectedEquationLineWidth() { }
            public float GetPointRadius() => 3.0f;
            public void SetPointRadius(float value) { }
            public void ResetPointRadius() { }
            public float GetSelectedEquationPointRadius() => 4.0f;
            public void SetSelectedEquationPointRadius(float value) { }
            public void ResetSelectedEquationPointRadius() { }
        }

        private class GraphOptionsImpl : IGraphingOptions
        {
            public bool MarkZeros { get; set; } = true;
            public bool MarkYIntercept { get; set; } = true;
            public bool MarkMinima { get; set; } = true;
            public bool MarkMaxima { get; set; } = true;
            public bool MarkInflectionPoints { get; set; } = true;
            public bool MarkVerticalAsymptotes { get; set; } = true;
            public bool MarkHorizontalAsymptotes { get; set; } = true;
            public bool MarkObliqueAsymptotes { get; set; } = true;
            public ulong MaxExecutionTime { get; set; } = 10000;
            public void ResetMaxExecutionTime() { }
            public IReadOnlyList<Color> GetGraphColors() => new List<Color>();
            public bool SetGraphColors(IReadOnlyList<Color> colors) => true;
            public void ResetGraphColors() { }
            public Color BackColor { get; set; }
            public void ResetBackColor() { }
            public bool AllowKeyGraphFeaturesForFunctionsWithParameters { get; set; } = true;
            public void ResetAllowKeyGraphFeaturesForFunctionsWithParameters() { }
            public Color ZerosColor { get; set; }
            public void ResetZerosColor() { }
            public Color ExtremaColor { get; set; }
            public void ResetExtremaColor() { }
            public Color InflectionPointsColor { get; set; }
            public void ResetInflectionPointsColor() { }
            public Color AsymptotesColor { get; set; }
            public void ResetAsymptotesColor() { }
            public Color AxisColor { get; set; }
            public void ResetAxisColor() { }
            public Color BoxColor { get; set; }
            public void ResetBoxColor() { }
            public Color GridColor { get; set; }
            public void ResetGridColor() { }
            public Color FontColor { get; set; }
            public void ResetFontColor() { }
            public bool ShowAxis { get; set; } = true;
            public void ResetShowAxis() { }
            public bool ShowGrid { get; set; } = true;
            public void ResetShowGrid() { }
            public bool ShowBox { get; set; } = true;
            public void ResetShowBox() { }
            public bool ForceProportional { get; set; } = true;
            public void ResetForceProportional() { }
            public string AliasX { get; set; } = "x";
            public void ResetAliasX() { }
            public string AliasY { get; set; } = "y";
            public void ResetAliasY() { }
            public LineStyle LineStyle { get; set; } = LineStyle.Solid;
            public void ResetLineStyle() { }
            public Tuple<double, double> GetDefaultXRange() => new Tuple<double, double>(-10, 10);
            public bool SetDefaultXRange(Tuple<double, double> minmax) => true;
            public void ResetDefaultXRange() { }
            public Tuple<double, double> GetDefaultYRange() => new Tuple<double, double>(-10, 10);
            public bool SetDefaultYRange(Tuple<double, double> minmax) => true;
            public void ResetDefaultYRange() { }
        }

        private class GraphRendererImpl : IGraphRenderer
        {
            public int SetGraphSize(uint width, uint height) => 0;
            public int SetDpi(float dpiX, float dpiY) => 0;
            public int DrawD2D1(object d2dFactory, object renderTarget, out bool hasSomeMissingDataOut)
            {
                hasSomeMissingDataOut = false;
                return 0;
            }
            public int GetClosePointData(double inScreenPointX, double inScreenPointY, double precision, out int formulaIdOut, out float xScreenPointOut, out float yScreenPointOut, out double xValueOut, out double yValueOut, out double rhoValueOut, out double thetaValueOut, out double tValueOut)
            {
                formulaIdOut = 0;
                xScreenPointOut = 0;
                yScreenPointOut = 0;
                xValueOut = 0;
                yValueOut = 0;
                rhoValueOut = 0;
                thetaValueOut = 0;
                tValueOut = 0;
                return 0;
            }
            public int ScaleRange(double centerX, double centerY, double scale) => 0;
            public int ChangeRange(ChangeRangeAction action) => 0;
            public int MoveRangeByRatio(double ratioX, double ratioY) => 0;
            public int ResetRange() => 0;
            public int GetDisplayRanges(out double xMin, out double xMax, out double yMin, out double yMax)
            {
                xMin = -10;
                xMax = 10;
                yMin = -10;
                yMax = 10;
                return 0;
            }
            public int SetDisplayRanges(double xMin, double xMax, double yMin, double yMax) => 0;
            public int PrepareGraph() => 0;
            public int GetBitmap(out IBitmap bitmapOut, out bool hasSomeMissingDataOut)
            {
                bitmapOut = new BitmapImpl();
                hasSomeMissingDataOut = false;
                return 0;
            }
        }

        private class BitmapImpl : IBitmap
        {
            public byte[] GetData() => new byte[0];
        }

        private class GraphAnalyzerImpl : IGraphAnalyzer
        {
            public bool CanFunctionAnalysisBePerformed(out bool variableIsNotX)
            {
                variableIsNotX = false;
                return true;
            }
            public int PerformFunctionAnalysis(uint analysisType) => 0;
            public int GetAnalysisTypeCaption(AnalysisType type, out string captionOut)
            {
                captionOut = string.Empty;
                return 0;
            }
            public int GetMessage(GraphAnalyzerMessage msg, out string msgOut)
            {
                msgOut = string.Empty;
                return 0;
            }
        }
    }

    #endregion
}

namespace GraphControl
{
    #region Data Classes

    public sealed class Variable : INotifyPropertyChanged
    {
        private double _value;
        private double _step = 0.1;
        private double _min = -5.0;
        private double _max = 5.0;

        public double Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value;
                    OnPropertyChanged();
                }
            }
        }

        public double Step
        {
            get => _step;
            set
            {
                if (_step != value)
                {
                    _step = value;
                    OnPropertyChanged();
                }
            }
        }

        public double Min
        {
            get => _min;
            set
            {
                if (_min != value)
                {
                    _min = value;
                    OnPropertyChanged();
                }
            }
        }

        public double Max
        {
            get => _max;
            set
            {
                if (_max != value)
                {
                    _max = value;
                    OnPropertyChanged();
                }
            }
        }

        public Variable(double value)
        {
            _value = value;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public partial class Equation : DependencyObject, INotifyPropertyChanged
    {
        // Dependency Properties
        public static readonly DependencyProperty ExpressionProperty =
            DependencyProperty.Register(nameof(Expression), typeof(string), typeof(Equation),
                new PropertyMetadata(string.Empty, OnExpressionChanged));

        public static readonly DependencyProperty IsLineEnabledProperty =
            DependencyProperty.Register(nameof(IsLineEnabled), typeof(bool), typeof(Equation),
                new PropertyMetadata(true, OnIsLineEnabledChanged));

        public static readonly DependencyProperty IsValidatedProperty =
            DependencyProperty.Register(nameof(IsValidated), typeof(bool), typeof(Equation),
                new PropertyMetadata(false));

        public static readonly DependencyProperty HasGraphErrorProperty =
            DependencyProperty.Register(nameof(HasGraphError), typeof(bool), typeof(Equation),
                new PropertyMetadata(false));

        public static readonly DependencyProperty IsInequalityProperty =
            DependencyProperty.Register(nameof(IsInequality), typeof(bool), typeof(Equation),
                new PropertyMetadata(false));

        public static readonly DependencyProperty IsSelectedProperty =
            DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(Equation),
                new PropertyMetadata(false, OnIsSelectedChanged));

        public static readonly DependencyProperty EquationStyleProperty =
            DependencyProperty.Register(nameof(EquationStyle), typeof(EquationLineStyle), typeof(Equation),
                new PropertyMetadata(EquationLineStyle.Solid, OnEquationStyleChanged));

        public static readonly DependencyProperty GraphErrorTypeProperty =
            DependencyProperty.Register(nameof(GraphErrorType), typeof(ErrorType), typeof(Equation),
                new PropertyMetadata(ErrorType.Syntax));

        public static readonly DependencyProperty GraphErrorCodeProperty =
            DependencyProperty.Register(nameof(GraphErrorCode), typeof(int), typeof(Equation),
                new PropertyMetadata(0));

        public static readonly DependencyProperty LineColorProperty =
            DependencyProperty.Register(nameof(LineColor), typeof(Color), typeof(Equation),
                new PropertyMetadata(Colors.Black, OnLineColorChanged));

        // Property wrappers
        public string Expression
        {
            get => (string)GetValue(ExpressionProperty);
            set => SetValue(ExpressionProperty, value);
        }

        public bool IsLineEnabled
        {
            get => (bool)GetValue(IsLineEnabledProperty);
            set => SetValue(IsLineEnabledProperty, value);
        }

        public bool IsValidated
        {
            get => (bool)GetValue(IsValidatedProperty);
            set => SetValue(IsValidatedProperty, value);
        }

        public bool HasGraphError
        {
            get => (bool)GetValue(HasGraphErrorProperty);
            set => SetValue(HasGraphErrorProperty, value);
        }

        public bool IsInequality
        {
            get => (bool)GetValue(IsInequalityProperty);
            set => SetValue(IsInequalityProperty, value);
        }

        public bool IsSelected
        {
            get => (bool)GetValue(IsSelectedProperty);
            set => SetValue(IsSelectedProperty, value);
        }

        public EquationLineStyle EquationStyle
        {
            get => (EquationLineStyle)GetValue(EquationStyleProperty);
            set => SetValue(EquationStyleProperty, value);
        }

        public ErrorType GraphErrorType
        {
            get => (ErrorType)GetValue(GraphErrorTypeProperty);
            set => SetValue(GraphErrorTypeProperty, value);
        }

        public int GraphErrorCode
        {
            get => (int)GetValue(GraphErrorCodeProperty);
            set => SetValue(GraphErrorCodeProperty, value);
        }

        public Color LineColor
        {
            get => (Color)GetValue(LineColorProperty);
            set => SetValue(LineColorProperty, value);
        }

        internal Graphing.IEquation GraphedEquation { get; set; }

        private static void OnExpressionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var equation = (Equation)d;
            equation.OnPropertyChanged(nameof(Expression));
        }

        private static void OnIsLineEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var equation = (Equation)d;
            equation.OnPropertyChanged(nameof(IsLineEnabled));
        }

        private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var equation = (Equation)d;
            equation.OnPropertyChanged(nameof(IsSelected));
        }

        private static void OnEquationStyleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var equation = (Equation)d;
            //equation.OnPropertyChanged(nameof(EquationStyle));
        }

        private static void OnLineColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var equation = (Equation)d;
            equation.OnPropertyChanged(nameof(LineColor));
        }

        public string GetRequest()
        {
            if (string.IsNullOrEmpty(Expression))
                return null;

            string request;
            IsInequality = false;

            // Check for inequality symbols
            if (Expression.Contains("&#x3E;") || Expression.Contains("&#x3C;") ||
                Expression.Contains("&#x2265;") || Expression.Contains("&#x2264;") ||
                Expression.Contains(">") || Expression.Contains("<") ||
                Expression.Contains("\u2265") || Expression.Contains("\u2264") ||
                Expression.Contains("&lt;") || Expression.Contains("&gt;"))
            {
                request = "<mrow><mi>plotIneq2D</mi><mfenced separators=\"\">";
                IsInequality = true;
                EquationStyle = EquationLineStyle.Dash;
            }
            else if (Expression.Contains(">=<"))
            {
                request = "<mrow><mi>plotEq2d</mi><mfenced separators=\"\">";
            }
            // If the expression contains both x and y but no equal or inequality sign
            else if (Expression.Contains(">x<") && Expression.Contains(">y<"))
            {
                return null;
            }
            else
            {
                request = "<mrow><mi>plot2d</mi><mfenced separators=\"\">";
            }

            request += GetCleanExpression();
            request += "</mfenced></mrow>";

            return request;
        }

        private string GetCleanExpression()
        {
            string mathML = Expression;

            // Remove "mml:" prefix
            return mathML.Replace("mml:", "");
        }

        public bool IsGraphableEquation()
        {
            return !string.IsNullOrEmpty(Expression) && IsLineEnabled && !HasGraphError;
        }

        public Equation()
        {
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public sealed class EquationCollection : ObservableCollection<Equation>
    {
        public event Action<Equation> EquationChanged;
        public event Action<Equation> EquationStyleChanged;
        public event Action<Equation> EquationLineEnabledChanged;

        public EquationCollection()
        {
        }

        protected override void InsertItem(int index, Equation item)
        {
            if (item != null)
            {
                item.PropertyChanged += OnEquationPropertyChanged;
            }
            base.InsertItem(index, item);
        }

        protected override void RemoveItem(int index)
        {
            var item = this[index];
            if (item != null)
            {
                item.PropertyChanged -= OnEquationPropertyChanged;
            }
            base.RemoveItem(index);
        }

        protected override void SetItem(int index, Equation item)
        {
            var oldItem = this[index];
            if (oldItem != null)
            {
                oldItem.PropertyChanged -= OnEquationPropertyChanged;
            }
            if (item != null)
            {
                item.PropertyChanged += OnEquationPropertyChanged;
            }
            base.SetItem(index, item);
        }

        protected override void ClearItems()
        {
            foreach (var item in this)
            {
                item.PropertyChanged -= OnEquationPropertyChanged;
            }
            base.ClearItems();
        }

        private void OnEquationPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var equation = sender as Equation;
            if (equation == null) return;

            switch (e.PropertyName)
            {
                case nameof(Equation.LineColor):
                case nameof(Equation.IsSelected):
                case nameof(Equation.EquationStyle):
                    EquationStyleChanged?.Invoke(equation);
                    break;
                case nameof(Equation.Expression):
                    EquationChanged?.Invoke(equation);
                    break;
                case nameof(Equation.IsLineEnabled):
                    EquationLineEnabledChanged?.Invoke(equation);
                    break;
            }
        }
    }

    public partial class KeyGraphFeaturesInfo : DependencyObject
    {
        // Dependency Properties
        public static readonly DependencyProperty XInterceptProperty =
            DependencyProperty.Register(nameof(XIntercept), typeof(string), typeof(KeyGraphFeaturesInfo), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty YInterceptProperty =
            DependencyProperty.Register(nameof(YIntercept), typeof(string), typeof(KeyGraphFeaturesInfo), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty ParityProperty =
            DependencyProperty.Register(nameof(Parity), typeof(int), typeof(KeyGraphFeaturesInfo), new PropertyMetadata(0));

        public static readonly DependencyProperty PeriodicityDirectionProperty =
            DependencyProperty.Register(nameof(PeriodicityDirection), typeof(int), typeof(KeyGraphFeaturesInfo), new PropertyMetadata(0));

        public static readonly DependencyProperty PeriodicityExpressionProperty =
            DependencyProperty.Register(nameof(PeriodicityExpression), typeof(string), typeof(KeyGraphFeaturesInfo), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty MinimaProperty =
            DependencyProperty.Register(nameof(Minima), typeof(IList<string>), typeof(KeyGraphFeaturesInfo),
                new PropertyMetadata(new ObservableCollection<string>()));

        public static readonly DependencyProperty MaximaProperty =
            DependencyProperty.Register(nameof(Maxima), typeof(IList<string>), typeof(KeyGraphFeaturesInfo),
                new PropertyMetadata(new ObservableCollection<string>()));

        public static readonly DependencyProperty DomainProperty =
            DependencyProperty.Register(nameof(Domain), typeof(string), typeof(KeyGraphFeaturesInfo), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty RangeProperty =
            DependencyProperty.Register(nameof(Range), typeof(string), typeof(KeyGraphFeaturesInfo), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty InflectionPointsProperty =
            DependencyProperty.Register(nameof(InflectionPoints), typeof(IList<string>), typeof(KeyGraphFeaturesInfo),
                new PropertyMetadata(new ObservableCollection<string>()));

        public static readonly DependencyProperty MonotonicityProperty =
            DependencyProperty.Register(nameof(Monotonicity), typeof(IDictionary<string, string>), typeof(KeyGraphFeaturesInfo),
                new PropertyMetadata(new Dictionary<string, string>()));

        public static readonly DependencyProperty VerticalAsymptotesProperty =
            DependencyProperty.Register(nameof(VerticalAsymptotes), typeof(IList<string>), typeof(KeyGraphFeaturesInfo),
                new PropertyMetadata(new ObservableCollection<string>()));

        public static readonly DependencyProperty HorizontalAsymptotesProperty =
            DependencyProperty.Register(nameof(HorizontalAsymptotes), typeof(IList<string>), typeof(KeyGraphFeaturesInfo),
                new PropertyMetadata(new ObservableCollection<string>()));

        public static readonly DependencyProperty ObliqueAsymptotesProperty =
            DependencyProperty.Register(nameof(ObliqueAsymptotes), typeof(IList<string>), typeof(KeyGraphFeaturesInfo),
                new PropertyMetadata(new ObservableCollection<string>()));

        public static readonly DependencyProperty TooComplexFeaturesProperty =
            DependencyProperty.Register(nameof(TooComplexFeatures), typeof(KeyGraphFeaturesFlag),
                typeof(KeyGraphFeaturesInfo), new PropertyMetadata((KeyGraphFeaturesFlag)0));

        public static readonly DependencyProperty AnalysisErrorProperty =
            DependencyProperty.Register(nameof(AnalysisError), typeof(AnalysisErrorType),
                typeof(KeyGraphFeaturesInfo), new PropertyMetadata((AnalysisErrorType)0));

        // Property wrappers
        public string XIntercept
        {
            get => (string)GetValue(XInterceptProperty);
            private set => SetValue(XInterceptProperty, value);
        }

        public string YIntercept
        {
            get => (string)GetValue(YInterceptProperty);
            private set => SetValue(YInterceptProperty, value);
        }

        public int Parity
        {
            get => (int)GetValue(ParityProperty);
            private set => SetValue(ParityProperty, value);
        }

        public int PeriodicityDirection
        {
            get => (int)GetValue(PeriodicityDirectionProperty);
            private set => SetValue(PeriodicityDirectionProperty, value);
        }

        public string PeriodicityExpression
        {
            get => (string)GetValue(PeriodicityExpressionProperty);
            private set => SetValue(PeriodicityExpressionProperty, value);
        }

        public IList<string> Minima
        {
            get => (IList<string>)GetValue(MinimaProperty);
            private set => SetValue(MinimaProperty, value);
        }

        public IList<string> Maxima
        {
            get => (IList<string>)GetValue(MaximaProperty);
            private set => SetValue(MaximaProperty, value);
        }

        public string Domain
        {
            get => (string)GetValue(DomainProperty);
            private set => SetValue(DomainProperty, value);
        }

        public string Range
        {
            get => (string)GetValue(RangeProperty);
            private set => SetValue(RangeProperty, value);
        }

        public IList<string> InflectionPoints
        {
            get => (IList<string>)GetValue(InflectionPointsProperty);
            private set => SetValue(InflectionPointsProperty, value);
        }

        public IDictionary<string, string> Monotonicity
        {
            get => (IDictionary<string, string>)GetValue(MonotonicityProperty);
            private set => SetValue(MonotonicityProperty, value);
        }

        public IList<string> VerticalAsymptotes
        {
            get => (IList<string>)GetValue(VerticalAsymptotesProperty);
            private set => SetValue(VerticalAsymptotesProperty, value);
        }

        public IList<string> HorizontalAsymptotes
        {
            get => (IList<string>)GetValue(HorizontalAsymptotesProperty);
            private set => SetValue(HorizontalAsymptotesProperty, value);
        }

        public IList<string> ObliqueAsymptotes
        {
            get => (IList<string>)GetValue(ObliqueAsymptotesProperty);
            private set => SetValue(ObliqueAsymptotesProperty, value);
        }

        public KeyGraphFeaturesFlag TooComplexFeatures
        {
            get => (KeyGraphFeaturesFlag)GetValue(TooComplexFeaturesProperty);
            private set => SetValue(TooComplexFeaturesProperty, value);
        }

        public AnalysisErrorType AnalysisError
        {
            get => (AnalysisErrorType) GetValue(AnalysisErrorProperty);
            private set => SetValue(AnalysisErrorProperty, value);
        }

        public static KeyGraphFeaturesInfo Create(Graphing.IGraphFunctionAnalysisData data)
        {
            var result = new KeyGraphFeaturesInfo
            {
                XIntercept = data.Zeros,
                YIntercept = data.YIntercept,
                Domain = data.Domain,
                Range = data.Range,
                Parity = data.Parity,
                PeriodicityDirection = data.PeriodicityDirection,
                PeriodicityExpression = data.PeriodicityExpression,
                Minima = new ObservableCollection<string>(data.Minima),
                Maxima = new ObservableCollection<string>(data.Maxima),
                InflectionPoints = new ObservableCollection<string>(data.InflectionPoints),
                Monotonicity = ConvertIntDictToStringDict(data.MonotoneIntervals),
                VerticalAsymptotes = new ObservableCollection<string>(data.VerticalAsymptotes),
                HorizontalAsymptotes = new ObservableCollection<string>(data.HorizontalAsymptotes),
                ObliqueAsymptotes = new ObservableCollection<string>(data.ObliqueAsymptotes),
                TooComplexFeatures = data.TooComplexFeatures,
                AnalysisError = 0 // No error
            };

            // Log analysis performance
            // TraceLogger.GetInstance().LogFunctionAnalysisPerformed(0, result.TooComplexFeatures);

            return result;
        }

        public static KeyGraphFeaturesInfo Create(AnalysisErrorType analysisErrorType)
        {
            var result = new KeyGraphFeaturesInfo
            {
                Minima = new ObservableCollection<string>(),
                Maxima = new ObservableCollection<string>(),
                InflectionPoints = new ObservableCollection<string>(),
                Monotonicity = new Dictionary<string, string>(),
                VerticalAsymptotes = new ObservableCollection<string>(),
                HorizontalAsymptotes = new ObservableCollection<string>(),
                ObliqueAsymptotes = new ObservableCollection<string>(),
                AnalysisError = analysisErrorType
            };

            // Log analysis performance
            // TraceLogger.GetInstance().LogFunctionAnalysisPerformed(analysisErrorType, 0);

            return result;
        }

        private static Dictionary<string, string> ConvertIntDictToStringDict(Dictionary<string, int> inMap)
        {
            var outMap = new Dictionary<string, string>();

            foreach (var kvp in inMap)
            {
                outMap[kvp.Key] = kvp.Value.ToString();
            }

            return outMap;
        }
    }

    #endregion

    #region DirectX Rendering Classes

    // These classes would normally use real DirectX functionality
    // For now they're just stubs
    internal class RenderMain
    {
        private Graphing.IGraph _graph;
        private float[] _backgroundColor = new float[4];
        private bool _drawNearestPoint;
        private Point _pointerLocation;
        private bool _drawActiveTracing;
        private Point _activeTracingPointerLocation;
        private double _xTraceValue;
        private double _yTraceValue;
        private Point _traceLocation;
        private bool _tracing;
        private int _renderError;

        public Graphing.IGraph Graph
        {
            get => _graph;
            set => _graph = value;
        }

        public Color BackgroundColor
        {
            set
            {
                // Convert color to normalized float array
                _backgroundColor[0] = value.R / 255.0f;
                _backgroundColor[1] = value.G / 255.0f;
                _backgroundColor[2] = value.B / 255.0f;
                _backgroundColor[3] = value.A / 255.0f;

                RunRenderPass();
            }
        }

        public bool DrawNearestPoint
        {
            get => _drawNearestPoint;
            set
            {
                if (_drawNearestPoint != value)
                {
                    _drawNearestPoint = value;
                    if (!_drawNearestPoint)
                    {
                        _tracing = false;
                    }
                }
            }
        }

        public Point PointerLocation
        {
            get => _pointerLocation;
            set
            {
                if (_pointerLocation != value)
                {
                    _pointerLocation = value;

                    bool wasPointRendered = _tracing;
                    if (CanRenderPoint() || wasPointRendered)
                    {
                        RunRenderPassAsync();
                    }
                }
            }
        }

        public bool ActiveTracing
        {
            get => _drawActiveTracing;
            set
            {
                if (_drawActiveTracing != value)
                {
                    _drawActiveTracing = value;

                    bool wasPointRendered = _tracing;
                    if (CanRenderPoint() || wasPointRendered)
                    {
                        RunRenderPassAsync();
                    }
                }
            }
        }

        public Point ActiveTraceCursorPosition
        {
            get => _activeTracingPointerLocation;
            set
            {
                if (_activeTracingPointerLocation != value)
                {
                    _activeTracingPointerLocation = value;

                    bool wasPointRendered = _tracing;
                    if (CanRenderPoint() || wasPointRendered)
                    {
                        RunRenderPassAsync();
                    }
                }
            }
        }

        public double XTraceValue => _xTraceValue;
        public double YTraceValue => _yTraceValue;
        public Point TraceLocation => _traceLocation;
        public bool Tracing => _tracing;
        public int RenderError => _renderError;

        public RenderMain(SwapChainPanel panel)
        {
            // Initialize the active tracing location to center of graph
            _activeTracingPointerLocation = new Point(200, 160);
        }

        public bool CanRenderPoint()
        {
            if (_graph != null && (_drawNearestPoint || _drawActiveTracing))
            {
                Point trackPoint = _pointerLocation;

                if (_drawActiveTracing)
                {
                    trackPoint = _activeTracingPointerLocation;
                }

                int formulaId;
                double outNearestPointValueX, outNearestPointValueY;
                float outNearestPointLocationX, outNearestPointLocationY;
                double rhoValueOut, thetaValueOut, tValueOut;

                double xAxisMin, xAxisMax, yAxisMin, yAxisMax;
                _graph.GetRenderer().GetDisplayRanges(out xAxisMin, out xAxisMax, out yAxisMin, out yAxisMax);
                double precision = GetPrecision(xAxisMax, xAxisMin);

                int result = _graph.GetRenderer().GetClosePointData(
                    trackPoint.X,
                    trackPoint.Y,
                    precision,
                    out formulaId,
                    out outNearestPointLocationX,
                    out outNearestPointLocationY,
                    out outNearestPointValueX,
                    out outNearestPointValueY,
                    out rhoValueOut,
                    out thetaValueOut,
                    out tValueOut);

                _tracing = result == 0 && !float.IsNaN(outNearestPointLocationX) && !float.IsNaN(outNearestPointLocationY);
            }
            else
            {
                _tracing = false;
            }

            return _tracing;
        }

        public void SetPointRadius(float radius)
        {
            // Would set radius in real implementation
        }

        public bool RunRenderPass()
        {
            return Render();
        }

        public async Task<bool> RunRenderPassAsync(bool allowCancel = true)
        {
            // In a real implementation, this would be async
            return await Task.FromResult(Render());
        }

        private bool Render()
        {
            if (_graph == null || _graph.GetRenderer() == null)
            {
                return false;
            }

            bool success = true;
            bool hasMissingData = false;

            // This would use actual DirectX in a real implementation
            _renderError = _graph.GetRenderer().DrawD2D1(null, null, out hasMissingData);
            success = _renderError == 0;

            if (success && (_drawNearestPoint || _drawActiveTracing))
            {
                Point trackPoint = _drawActiveTracing ? _activeTracingPointerLocation : _pointerLocation;

                int formulaId;
                double outNearestPointValueX, outNearestPointValueY;
                float outNearestPointLocationX, outNearestPointLocationY;
                double rhoValueOut, thetaValueOut, tValueOut;

                double xAxisMin, xAxisMax, yAxisMin, yAxisMax;
                _graph.GetRenderer().GetDisplayRanges(out xAxisMin, out xAxisMax, out yAxisMin, out yAxisMax);
                double precision = GetPrecision(xAxisMax, xAxisMin);

                if (_graph.GetRenderer().GetClosePointData(
                        trackPoint.X,
                        trackPoint.Y,
                        precision,
                        out formulaId,
                        out outNearestPointLocationX,
                        out outNearestPointLocationY,
                        out outNearestPointValueX,
                        out outNearestPointValueY,
                        out rhoValueOut,
                        out thetaValueOut,
                        out tValueOut) == 0)
                {
                    if (!float.IsNaN(outNearestPointLocationX) && !float.IsNaN(outNearestPointLocationY))
                    {
                        // In a real implementation, this would draw the point

                        _traceLocation = new Point(outNearestPointLocationX, outNearestPointLocationY);
                        _tracing = true;
                        _xTraceValue = outNearestPointValueX;
                        _yTraceValue = outNearestPointValueY;
                    }
                    else
                    {
                        _tracing = false;
                    }
                }
                else
                {
                    _tracing = false;
                }
            }

            return success;
        }

        private double GetPrecision(double maxAxis, double minAxis)
        {
            double exponent = Math.Floor(Math.Log10(maxAxis - minAxis)) - 3;
            return Math.Pow(10, exponent);
        }

        public void CreateWindowSizeDependentResources()
        {
            // In a real implementation, this would recreate DirectX resources
            RunRenderPass();
        }
    }

    #endregion

    #region Main Grapher Control

    // Delegates for events
    public delegate void TracingChangedEventHandler(bool newValue);
    public delegate void TracingValueChangedEventHandler(double xPointValue, double yPointValue);
    public delegate void PointerValueChangedEventHandler(Point value);

    public sealed class Grapher : UserControl
    {
        #region Dependency Properties

        public static readonly DependencyProperty ForceProportionalAxesProperty =
            DependencyProperty.Register(nameof(ForceProportionalAxes), typeof(bool), typeof(Grapher),
                new PropertyMetadata(true, OnForceProportionalAxesPropertyChanged));

        public static readonly DependencyProperty UseCommaDecimalSeperatorProperty =
            DependencyProperty.Register(nameof(UseCommaDecimalSeperator), typeof(bool), typeof(Grapher),
                new PropertyMetadata(false, OnUseCommaDecimalSeperatorPropertyChanged));

        public static readonly DependencyProperty VariablesProperty =
            DependencyProperty.Register(nameof(Variables), typeof(IDictionary<string, Variable>), typeof(Grapher),
                new PropertyMetadata(new Dictionary<string, Variable>()));

        public static readonly DependencyProperty EquationsProperty =
            DependencyProperty.Register(nameof(Equations), typeof(EquationCollection), typeof(Grapher),
                new PropertyMetadata(null, OnEquationsPropertyChanged));

        public static readonly DependencyProperty AxesColorProperty =
            DependencyProperty.Register(nameof(AxesColor), typeof(Color), typeof(Grapher),
                new PropertyMetadata(Colors.Transparent, OnAxesColorPropertyChanged));

        public static readonly DependencyProperty GraphBackgroundProperty =
            DependencyProperty.Register(nameof(GraphBackground), typeof(Color), typeof(Grapher),
                new PropertyMetadata(Colors.Transparent, OnGraphBackgroundPropertyChanged));

        public static readonly DependencyProperty GridLinesColorProperty =
            DependencyProperty.Register(nameof(GridLinesColor), typeof(Color), typeof(Grapher),
                new PropertyMetadata(Colors.Transparent, OnGridLinesColorPropertyChanged));

        public static readonly DependencyProperty LineWidthProperty =
            DependencyProperty.Register(nameof(LineWidth), typeof(double), typeof(Grapher),
                new PropertyMetadata(2.0, OnLineWidthPropertyChanged));

        public static readonly DependencyProperty IsKeepCurrentViewProperty =
            DependencyProperty.Register(nameof(IsKeepCurrentView), typeof(bool), typeof(Grapher),
                new PropertyMetadata(false));

        #endregion

        #region Properties

        public bool ForceProportionalAxes
        {
            get => (bool)GetValue(ForceProportionalAxesProperty);
            set => SetValue(ForceProportionalAxesProperty, value);
        }

        public bool UseCommaDecimalSeperator
        {
            get => (bool)GetValue(UseCommaDecimalSeperatorProperty);
            set => SetValue(UseCommaDecimalSeperatorProperty, value);
        }

        public IDictionary<string, Variable> Variables
        {
            get => (IDictionary<string, Variable>)GetValue(VariablesProperty);
            set => SetValue(VariablesProperty, value);
        }

        public EquationCollection Equations
        {
            get => (EquationCollection)GetValue(EquationsProperty);
            set => SetValue(EquationsProperty, value);
        }

        public Color AxesColor
        {
            get => (Color)GetValue(AxesColorProperty);
            set => SetValue(AxesColorProperty, value);
        }

        public Color GraphBackground
        {
            get => (Color)GetValue(GraphBackgroundProperty);
            set => SetValue(GraphBackgroundProperty, value);
        }

        public Color GridLinesColor
        {
            get => (Color)GetValue(GridLinesColorProperty);
            set => SetValue(GridLinesColorProperty, value);
        }

        public double LineWidth
        {
            get => (double)GetValue(LineWidthProperty);
            set => SetValue(LineWidthProperty, value);
        }

        public bool IsKeepCurrentView
        {
            get => (bool)GetValue(IsKeepCurrentViewProperty);
            set => SetValue(IsKeepCurrentViewProperty, value);
        }

        public bool ActiveTracing
        {
            get => _renderMain != null && _renderMain.ActiveTracing;
            set
            {
                if (_renderMain != null && _renderMain.ActiveTracing != value)
                {
                    _renderMain.ActiveTracing = value;
                    UpdateTracingChanged();
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActiveTracing)));
                }
            }
        }

        public Point TraceLocation
        {
            get => _renderMain != null ? _renderMain.TraceLocation : new Point();
        }

        public Point ActiveTraceCursorPosition
        {
            get => _renderMain != null ? _renderMain.ActiveTraceCursorPosition : new Point();
            set
            {
                if (_renderMain != null && _renderMain.ActiveTraceCursorPosition != value)
                {
                    _renderMain.ActiveTraceCursorPosition = value;
                    UpdateTracingChanged();
                }
            }
        }

        public int TrigUnitMode
        {
            get => (int)_solver.EvalOptions().TrigUnitMode;
            set
            {
                if (value != (int)_solver.EvalOptions().TrigUnitMode)
                {
                    _solver.EvalOptions().TrigUnitMode = (Graphing.EvalTrigUnitMode)value;
                    _trigUnitsChanged = true;
                    PlotGraph(true);
                }
            }
        }

        public double XAxisMin
        {
            get => _graph.GetOptions().GetDefaultXRange().Item1;
            set
            {
                var newValue = new Tuple<double, double>(value, XAxisMax);
                if (_graph != null)
                {
                    _graph.GetOptions().SetDefaultXRange(newValue);
                    if (_renderMain != null)
                    {
                        _renderMain.RunRenderPass();
                    }
                }
            }
        }

        public double XAxisMax
        {
            get => _graph.GetOptions().GetDefaultXRange().Item2;
            set
            {
                var newValue = new Tuple<double, double>(XAxisMin, value);
                if (_graph != null)
                {
                    _graph.GetOptions().SetDefaultXRange(newValue);
                    if (_renderMain != null)
                    {
                        _renderMain.RunRenderPass();
                    }
                }
            }
        }

        public double YAxisMin
        {
            get => _graph.GetOptions().GetDefaultYRange().Item1;
            set
            {
                var newValue = new Tuple<double, double>(value, YAxisMax);
                if (_graph != null)
                {
                    _graph.GetOptions().SetDefaultYRange(newValue);
                    if (_renderMain != null)
                    {
                        _renderMain.RunRenderPass();
                    }
                }
            }
        }

        public double YAxisMax
        {
            get => _graph.GetOptions().GetDefaultYRange().Item2;
            set
            {
                var newValue = new Tuple<double, double>(YAxisMin, value);
                if (_graph != null)
                {
                    _graph.GetOptions().SetDefaultYRange(newValue);
                    if (_renderMain != null)
                    {
                        _renderMain.RunRenderPass();
                    }
                }
            }
        }

        #endregion

        #region Events

        public event TracingValueChangedEventHandler TracingValueChangedEvent;
        public event PointerValueChangedEventHandler PointerValueChangedEvent;
        public event TracingChangedEventHandler TracingChangedEvent;
        public event EventHandler<GraphViewChangedReason> GraphViewChangedEvent;
        public event RoutedEventHandler GraphPlottedEvent;
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<IDictionary<string, Variable>> VariablesUpdated;

        #endregion

        #region Private Fields

        private readonly Graphing.IMathSolver _solver;
        private readonly Graphing.IGraph _graph;
        private RenderMain _renderMain;
        private bool _calculatedForceProportional;
        private bool _tracingTracking;
        private bool _trigUnitsChanged;
        private bool[] _keysPressed = new bool[5]; // Left, Right, Down, Up, Accelerator
        private bool _moving;
        private DispatcherTimer _tracingTrackingTimer;
        private CoreCursor _cachedCursor;
        private int _errorType;
        private int _errorCode;
        private bool _resetUsingInitialDisplayRange;
        private bool _rangeUpdatedBySettings;
        private double _initialDisplayRangeXMin;
        private double _initialDisplayRangeXMax;
        private double _initialDisplayRangeYMin;
        private double _initialDisplayRangeYMax;

        #endregion

        #region Constructor and Initialization

        public Grapher()
        {
            DefaultStyleKey = typeof(Grapher);

            // TODO: In real implementation, this would instantiate the native solver
            _solver = new MathSolverImplementation(); //Graphing.IMathSolver.CreateMathSolver();
            _graph = _solver.CreateGrapher();

            Equations = new EquationCollection();

            _solver.ParsingOptions().SetFormatType(Graphing.FormatType.MathML);
            _solver.FormatOptions().SetFormatType(Graphing.FormatType.MathML);
            _solver.FormatOptions().SetMathMLPrefix("mml");

            // Set up manipulation modes
            ManipulationMode = ManipulationModes.TranslateX | ManipulationModes.TranslateY |
                              ManipulationModes.TranslateInertia | ManipulationModes.Scale |
                              ManipulationModes.ScaleInertia;

            // Register for keyboard events
            var coreWindow = CoreWindow.GetForCurrentThread();
            coreWindow.KeyDown += OnCoreKeyDown;
            coreWindow.KeyUp += OnCoreKeyUp;

            // Register other events
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Get the SwapChainPanel from the template
            var swapChainPanel = GetTemplateChild("GraphSurface") as SwapChainPanel;
            if (swapChainPanel != null)
            {
                swapChainPanel.AllowFocusOnInteraction = true;
                _renderMain = new RenderMain(swapChainPanel);
                _renderMain.BackgroundColor = GraphBackground;
            }

            TryUpdateGraph(false);
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // The rest of initialization is done in OnLoaded
        }

        #endregion

        #region Public Methods

        public void ZoomFromCenter(double scale)
        {
            ScaleRange(0, 0, scale);
            GraphViewChangedEvent?.Invoke(this, GraphViewChangedReason.Manipulation);
        }

        public void ResetGrid()
        {
            if (_graph != null && _renderMain != null)
            {
                var renderer = _graph.GetRenderer();
                if (renderer is not null)
                {
                    if (_resetUsingInitialDisplayRange)
                    {
                        renderer.SetDisplayRanges(_initialDisplayRangeXMin, _initialDisplayRangeXMax,
                                               _initialDisplayRangeYMin, _initialDisplayRangeYMax);
                        _resetUsingInitialDisplayRange = false;
                    }
                    else if (_rangeUpdatedBySettings)
                    {
                        IsKeepCurrentView = false;
                        TryPlotGraph(false, false);
                        _rangeUpdatedBySettings = false;
                        GraphViewChangedEvent?.Invoke(this, GraphViewChangedReason.Reset);
                        return;
                    }
                    else
                    {
                        renderer.ResetRange();
                    }

                    _renderMain.RunRenderPass();
                    GraphViewChangedEvent?.Invoke(this, GraphViewChangedReason.Reset);
                }
            }
        }

        public void SetVariable(string variableName, double newValue)
        {
            if (!Variables.ContainsKey(variableName))
            {
                Variables[variableName] = new Variable(newValue);
            }

            if (_graph != null && _renderMain != null)
            {
                _graph.SetArgValue(variableName, newValue);
                _ = _renderMain.RunRenderPassAsync();
            }
        }

        public string ConvertToLinear(string mmlString)
        {
            _solver.FormatOptions().SetFormatType(Graphing.FormatType.LinearInput);

            int errorCode, errorType;
            var expression = _solver.ParseInput(mmlString, out errorCode, out errorType);
            var linearExpression = _solver.Serialize(expression);

            _solver.FormatOptions().SetFormatType(Graphing.FormatType.MathML);

            return linearExpression;
        }

        public string FormatMathML(string mmlString)
        {
            int errorCode, errorType;
            var expression = _solver.ParseInput(mmlString, out errorCode, out errorType);
            return _solver.Serialize(expression);
        }

        public void PlotGraph(bool keepCurrentView)
        {
            TryPlotGraph(keepCurrentView, false);
        }

        public KeyGraphFeaturesInfo AnalyzeEquation(Equation equation)
        {
            var graph = GetGraph(equation);
            if (graph == null) return null;

            SetGraphArgs(graph);

            var analyzer = graph.GetAnalyzer();
            if (analyzer == null) return null;

            List<Equation> equationVector = new List<Equation> { equation };
            UpdateGraphOptions(graph.GetOptions(), equationVector);

            bool variableIsNotX;
            if (analyzer.CanFunctionAnalysisBePerformed(out variableIsNotX) && !variableIsNotX)
            {
                if (analyzer.PerformFunctionAnalysis((uint)Graphing.PerformAnalysisType.All) == 0)
                {
                    var functionAnalysisData = _solver.Analyze(analyzer);
                    return KeyGraphFeaturesInfo.Create(functionAnalysisData);
                }
            }
            else if (variableIsNotX)
            {
                return KeyGraphFeaturesInfo.Create((AnalysisErrorType)1); // VariableIsNotX
            }
            else
            {
                return KeyGraphFeaturesInfo.Create((AnalysisErrorType)2); // AnalysisNotSupported
            }

            return KeyGraphFeaturesInfo.Create((AnalysisErrorType)3); // AnalysisCouldNotBePerformed
        }

        public void GetDisplayRanges(out double xMin, out double xMax, out double yMin, out double yMax)
        {
            xMin = xMax = yMin = yMax = 0;

            try
            {
                if (_graph != null && _renderMain != null)
                {
                    var render = _graph.GetRenderer();
                    if (render != null)
                    {
                        render.GetDisplayRanges(out xMin, out xMax, out yMin, out yMax);
                    }
                }
            }
            catch (Exception)
            {
                System.Diagnostics.Debug.WriteLine("GetDisplayRanges failed");
            }
        }

        public void SetDisplayRanges(double xMin, double xMax, double yMin, double yMax)
        {
            try
            {
                var render = _graph.GetRenderer();
                if (render != null)
                {
                    render.SetDisplayRanges(xMin, xMax, yMin, yMax);
                    _rangeUpdatedBySettings = true;

                    if (_renderMain != null)
                    {
                        _renderMain.RunRenderPass();
                        GraphViewChangedEvent?.Invoke(this, GraphViewChangedReason.Manipulation);
                    }
                }
            }
            catch (Exception)
            {
                System.Diagnostics.Debug.WriteLine("SetDisplayRanges failed");
            }
        }

        #endregion

        #region Private Methods

        private void ScaleRange(double centerX, double centerY, double scale)
        {
            if (_graph != null && _renderMain != null)
            {
                var renderer = _graph.GetRenderer();
                if (renderer != null && renderer.ScaleRange(centerX, centerY, scale) == 0)
                {
                    _renderMain.RunRenderPass();
                    GraphViewChangedEvent?.Invoke(this, GraphViewChangedReason.Manipulation);
                }
            }
        }

        private async void TryPlotGraph(bool keepCurrentView, bool shouldRetry)
        {
            if (await TryUpdateGraph(keepCurrentView))
            {
                SetEquationsAsValid();
            }
            else
            {
                SetEquationErrors();

                // If we failed to plot the graph, try again after the bad equations are flagged.
                if (shouldRetry)
                {
                    await TryUpdateGraph(keepCurrentView);
                }
            }

            int valid = 0;
            int invalid = 0;
            foreach (var eq in Equations)
            {
                if (eq.HasGraphError)
                {
                    invalid++;
                }
                if (eq.IsValidated)
                {
                    valid++;
                }
            }

            if (!_trigUnitsChanged)
            {
                // Log equation count changes
                // TraceLogger.GetInstance().LogEquationCountChanged(valid, invalid);
            }

            _trigUnitsChanged = false;
            GraphPlottedEvent?.Invoke(this, new RoutedEventArgs());
        }

        private async Task<bool> TryUpdateGraph(bool keepCurrentView)
        {
            bool successful = false;
            _errorCode = 0;
            _errorType = 0;

            if (_renderMain != null && _graph != null)
            {
                Graphing.IExpression graphExpression = null;
                string request = string.Empty;

                var validEqs = GetGraphableEquations();

                // Will be set to true if the previous graph should be kept in the event of an error
                bool shouldKeepPreviousGraph = false;

                if (validEqs.Count > 0)
                {
                    request = "<mrow><mi>show2d</mi><mfenced separators=\"\">";

                    int numValidEquations = 0;
                    foreach (var eq in validEqs)
                    {
                        if (eq.IsValidated)
                        {
                            shouldKeepPreviousGraph = true;
                        }

                        if (numValidEquations++ > 0)
                        {
                            if (!UseCommaDecimalSeperator)
                            {
                                request += "<mo>,</mo>";
                            }
                            else
                            {
                                request += "<mo>;</mo>";
                            }
                        }

                        var equationRequest = eq.GetRequest();

                        // If the equation request failed, then fail graphing.
                        if (equationRequest == null)
                        {
                            return false;
                        }

                        string parsableEquation = "<mrow><mi>show2d</mi><mfenced separators=\"\">" +
                                                 equationRequest +
                                                 "</mfenced></mrow>";

                        // Wire up the corresponding error to an error message in the UI
                        graphExpression = _solver.ParseInput(parsableEquation, out _errorCode, out _errorType);
                        if (graphExpression == null)
                        {
                            return false;
                        }

                        request += equationRequest;
                    }

                    request += "</mfenced></mrow>";
                }

                if (!string.IsNullOrEmpty(request))
                {
                    graphExpression = _solver.ParseInput(request, out _errorCode, out _errorType);
                }

                IReadOnlyList<Graphing.IEquation> initResult = null;

                if (graphExpression != null)
                {
                    initResult = TryInitializeGraph(keepCurrentView, graphExpression);

                    if (initResult != null)
                    {
                        for (int i = 0; i < validEqs.Count; i++)
                        {
                            validEqs[i].GraphedEquation = initResult[i];
                        }

                        UpdateGraphOptions(_graph.GetOptions(), validEqs);
                        SetGraphArgs(_graph);

                        _renderMain.Graph = _graph;

                        // It is possible that the render fails, in that case fall through to explicit empty initialization
                        bool renderSuccess = await _renderMain.RunRenderPassAsync();
                        if (renderSuccess)
                        {
                            UpdateVariables();
                            successful = true;
                        }
                        else
                        {
                            // If we failed to render then we have already lost the previous graph
                            shouldKeepPreviousGraph = false;
                            initResult = null;
                            _solver.HRErrorToErrorInfo(_renderMain.RenderError, out _errorCode, out _errorType);
                        }
                    }
                    else
                    {
                        _solver.HRErrorToErrorInfo(_graph.GetInitializationError(), out _errorCode, out _errorType);
                    }
                }

                // Do not re-initialize the graph to empty if there are still valid equations graphed
                if (initResult == null && !shouldKeepPreviousGraph)
                {
                    initResult = TryInitializeGraph(false, null);
                    if (initResult != null)
                    {
                        UpdateGraphOptions(_graph.GetOptions(), new List<Equation>());
                        SetGraphArgs(_graph);

                        _renderMain.Graph = _graph;
                        await _renderMain.RunRenderPassAsync();

                        UpdateVariables();

                        // Initializing an empty graph is only a success if there were no equations to graph.
                        successful = (validEqs.Count == 0);
                    }
                }
            }

            // Return true if we were able to graph and render all graphable equations
            return successful;
        }

        private void SetEquationsAsValid()
        {
            foreach (var eq in GetGraphableEquations())
            {
                eq.IsValidated = true;
            }
        }

        private void SetEquationErrors()
        {
            foreach (var eq in GetGraphableEquations())
            {
                if (!eq.IsValidated)
                {
                    eq.GraphErrorType = (ErrorType)_errorType;
                    eq.GraphErrorCode = _errorCode;
                    eq.HasGraphError = true;
                }
            }
        }

        private void SetGraphArgs(Graphing.IGraph graph)
        {
            if (graph != null && _renderMain != null)
            {
                foreach (var variablePair in Variables)
                {
                    graph.SetArgValue(variablePair.Key, variablePair.Value.Value);
                }
            }
        }

        private Graphing.IGraph GetGraph(Equation equation)
        {
            Graphing.IGraph graph = _solver.CreateGrapher();

            string request = "<mrow><mi>show2d</mi><mfenced separators=\"\">" +
                             equation.GetRequest() +
                             "</mfenced></mrow>";

            int errorCode, errorType;
            var expr = _solver.ParseInput(request, out errorCode, out errorType);
            if (expr != null)
            {
                var eqs = graph.TryInitialize(expr);
                if (eqs != null && eqs.Count > 0)
                {
                    return graph;
                }
            }

            return null;
        }

        private void UpdateVariables()
        {
            var updatedVariables = new Dictionary<string, Variable>();

            if (_graph != null)
            {
                var graphVariables = _graph.GetVariables();

                foreach (var graphVar in graphVariables)
                {
                    string name = graphVar.VariableName;
                    if (name != "x" && name != "y")
                    {
                        Variable variable = null;

                        if (Variables.ContainsKey(name))
                        {
                            variable = Variables[name];
                        }

                        if (variable == null)
                        {
                            variable = new Variable(1.0);
                        }

                        updatedVariables[name] = variable;
                    }
                }
            }

            if (Variables.Count != updatedVariables.Count)
            {
                // TraceLogger.GetInstance().LogVariableCountChanged(updatedVariables.Count);
            }

            Variables = updatedVariables;
            VariablesUpdated?.Invoke(this, Variables);
        }

        private void UpdateGraphOptions(Graphing.IGraphingOptions options, List<Equation> validEqs)
        {
            options.ForceProportional = ForceProportionalAxes;

            if (!options.AllowKeyGraphFeaturesForFunctionsWithParameters)
            {
                options.AllowKeyGraphFeaturesForFunctionsWithParameters = true;
            }

            if (validEqs.Count > 0)
            {
                var graphColors = validEqs.Select(eq => eq.LineColor).ToList();
                options.SetGraphColors(graphColors);

                foreach (var eq in validEqs)
                {
                    if (eq.GraphedEquation != null)
                    {
                        if (!eq.HasGraphError && eq.IsSelected)
                        {
                            eq.GraphedEquation.TrySelectEquation();
                        }

                        eq.GraphedEquation.GetGraphEquationOptions().SetLineStyle((Graphing.LineStyle)eq.EquationStyle);
                        eq.GraphedEquation.GetGraphEquationOptions().SetLineWidth((float)LineWidth);
                        eq.GraphedEquation.GetGraphEquationOptions().SetSelectedEquationLineWidth((float)(LineWidth + ((LineWidth <= 2) ? 1 : 2)));
                    }
                }
            }
        }

        private List<Equation> GetGraphableEquations()
        {
            var validEqs = new List<Equation>();

            foreach (var eq in Equations)
            {
                if (eq.IsGraphableEquation())
                {
                    validEqs.Add(eq);
                }
            }

            return validEqs;
        }

        private IReadOnlyList<Graphing.IEquation> TryInitializeGraph(bool keepCurrentView, Graphing.IExpression graphingExp)
        {
            if (keepCurrentView || IsKeepCurrentView)
            {
                var renderer = _graph.GetRenderer();
                double xMin, xMax, yMin, yMax;
                renderer.GetDisplayRanges(out xMin, out xMax, out yMin, out yMax);

                var initResult = _graph.TryInitialize(graphingExp);
                if (initResult != null)
                {
                    if (IsKeepCurrentView)
                    {
                        // PrepareGraph() populates the values of the graph after TryInitialize but before rendering.
                        if (renderer.PrepareGraph() == 0)
                        {
                            // Get the initial display ranges from the graph that was just initialized to be used in ResetGrid
                            renderer.GetDisplayRanges(
                                out _initialDisplayRangeXMin,
                                out _initialDisplayRangeXMax,
                                out _initialDisplayRangeYMin,
                                out _initialDisplayRangeYMax);
                            _resetUsingInitialDisplayRange = true;
                        }
                    }

                    renderer.SetDisplayRanges(xMin, xMax, yMin, yMax);
                }

                return initResult;
            }
            else
            {
                _resetUsingInitialDisplayRange = false;
                return _graph.TryInitialize(graphingExp);
            }
        }

        private void UpdateTracingChanged()
        {
            if (_renderMain.Tracing)
            {
                TracingChangedEvent?.Invoke(true);
                TracingValueChangedEvent?.Invoke(_renderMain.XTraceValue, _renderMain.YTraceValue);
            }
            else
            {
                TracingChangedEvent?.Invoke(false);
            }
        }

        private static void OnEquationsPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var grapher = (Grapher)d;
            var oldValue = e.OldValue as EquationCollection;
            var newValue = e.NewValue as EquationCollection;

            if (oldValue != null)
            {
                oldValue.EquationChanged -= grapher.OnEquationChanged;
                oldValue.EquationStyleChanged -= grapher.OnEquationStyleChanged;
                oldValue.EquationLineEnabledChanged -= grapher.OnEquationLineEnabledChanged;
            }

            if (newValue != null)
            {
                newValue.EquationChanged += grapher.OnEquationChanged;
                newValue.EquationStyleChanged += grapher.OnEquationStyleChanged;
                newValue.EquationLineEnabledChanged += grapher.OnEquationLineEnabledChanged;
            }

            grapher.PlotGraph(false);
        }

        private void OnEquationChanged(Equation equation)
        {
            // Reset error properties
            equation.HasGraphError = false;
            equation.IsValidated = false;

            TryPlotGraph(false, true);
        }

        private void OnEquationStyleChanged(Equation equation)
        {
            if (_graph != null)
            {
                _graph.TryResetSelection();
                UpdateGraphOptions(_graph.GetOptions(), GetGraphableEquations());
            }

            if (_renderMain != null)
            {
                _renderMain.RunRenderPass();
            }
        }

        private void OnEquationLineEnabledChanged(Equation equation)
        {
            // If the equation is in an error state or is empty, it should not be graphed anyway
            if (equation.HasGraphError || string.IsNullOrEmpty(equation.Expression))
                return;

            bool keepCurrentView = true;

            // If the equation has changed, the IsLineEnabled state is reset.
            // This checks if the equation has been reset and sets keepCurrentView to false in this case.
            if (!equation.HasGraphError && !equation.IsValidated && equation.IsLineEnabled)
            {
                keepCurrentView = false;
            }

            PlotGraph(keepCurrentView);
        }

        private static void OnForceProportionalAxesPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var grapher = (Grapher)d;
            var oldValue = (bool)e.OldValue;
            var newValue = (bool)e.NewValue;

            grapher._calculatedForceProportional = newValue;
            grapher.TryUpdateGraph(false);
        }

        private static void OnUseCommaDecimalSeperatorPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var grapher = (Grapher)d;
            var oldValue = (bool)e.OldValue;
            var newValue = (bool)e.NewValue;

            if (newValue)
            {
                grapher._solver.ParsingOptions().SetLocalizationType(Graphing.LocalizationType.DecimalCommaAndListSemicolon);
                grapher._solver.FormatOptions().SetLocalizationType(Graphing.LocalizationType.DecimalCommaAndListSemicolon);
            }
            else
            {
                grapher._solver.ParsingOptions().SetLocalizationType(Graphing.LocalizationType.DecimalPointAndListComma);
                grapher._solver.FormatOptions().SetLocalizationType(Graphing.LocalizationType.DecimalPointAndListComma);
            }
        }

        private static void OnAxesColorPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var grapher = (Grapher)d;
            if (grapher._graph != null)
            {
                var axesColor = (Color)e.NewValue;
                grapher._graph.GetOptions().AxisColor = (axesColor);
                grapher._graph.GetOptions().FontColor = (axesColor);
            }
        }

        private static void OnGraphBackgroundPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var grapher = (Grapher)d;
            if (grapher._renderMain != null)
            {
                grapher._renderMain.BackgroundColor = (Color)e.NewValue;
            }

            if (grapher._graph != null)
            {
                var color = (Color)e.NewValue;
                grapher._graph.GetOptions().BackColor = (color);
                grapher._graph.GetOptions().BoxColor = (color);
            }
        }

        private static async void OnGridLinesColorPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var grapher = (Grapher)d;
            if (grapher._renderMain != null && grapher._graph != null)
            {
                var gridLinesColor = (Color)e.NewValue;
                grapher._graph.GetOptions().GridColor = (gridLinesColor);
                await grapher._renderMain.RunRenderPassAsync();
            }
        }

        private static void OnLineWidthPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var grapher = (Grapher)d;
            if (grapher._graph != null)
            {
                grapher.UpdateGraphOptions(grapher._graph.GetOptions(), grapher.GetGraphableEquations());
                if (grapher._renderMain != null)
                {
                    grapher._renderMain.SetPointRadius((float)(grapher.LineWidth + 1));
                    grapher._renderMain.RunRenderPass();

                    // TraceLogger.GetInstance().LogLineWidthChanged();
                }
            }
        }

        private void OnCoreKeyDown(CoreWindow sender, KeyEventArgs e)
        {
            // We don't want to react to keyboard input unless the graph control has the focus
            var gcHasFocus = FocusManager.GetFocusedElement() as Grapher;
            if (gcHasFocus == null || gcHasFocus != this)
                return;

            switch (e.VirtualKey)
            {
                case VirtualKey.Left:
                case VirtualKey.Right:
                case VirtualKey.Down:
                case VirtualKey.Up:
                case VirtualKey.Shift:
                    HandleKey(true, e.VirtualKey);
                    break;
            }
        }

        private void OnCoreKeyUp(CoreWindow sender, KeyEventArgs e)
        {
            // We don't want to react to any keys when we are not in the graph control
            var gcHasFocus = FocusManager.GetFocusedElement() as Grapher;
            if (gcHasFocus == null || gcHasFocus != this)
                return;

            switch (e.VirtualKey)
            {
                case VirtualKey.Left:
                case VirtualKey.Right:
                case VirtualKey.Down:
                case VirtualKey.Up:
                case VirtualKey.Shift:
                    HandleKey(false, e.VirtualKey);
                    break;
            }
        }

        private void HandleKey(bool keyDown, VirtualKey key)
        {
            int pressedKeys = 0;
            if (key == VirtualKey.Left)
            {
                _keysPressed[0] = keyDown;
                if (keyDown) pressedKeys++;
            }
            if (key == VirtualKey.Right)
            {
                _keysPressed[1] = keyDown;
                if (keyDown) pressedKeys++;
            }
            if (key == VirtualKey.Down)
            {
                _keysPressed[2] = keyDown;
                if (keyDown) pressedKeys++;
            }
            if (key == VirtualKey.Up)
            {
                _keysPressed[3] = keyDown;
                if (keyDown) pressedKeys++;
            }
            if (key == VirtualKey.Shift)
            {
                _keysPressed[4] = keyDown;
            }

            if (pressedKeys > 0 && !_moving)
            {
                _moving = true;
                // Key(s) we care about, so ensure we are ticking our timer (and that we have one to tick)
                if (_tracingTrackingTimer == null)
                {
                    _tracingTrackingTimer = new DispatcherTimer();
                    _tracingTrackingTimer.Tick += HandleTracingMovementTick;
                    _tracingTrackingTimer.Interval = TimeSpan.FromMilliseconds(100);
                }
                _tracingTrackingTimer.Start();
            }
        }

        private void HandleTracingMovementTick(object sender, object e)
        {
            int delta = 5;
            int liveKeys = 0;

            if (_keysPressed[4]) // Accelerator (Shift)
            {
                delta = 1;
            }

            var curPos = ActiveTraceCursorPosition;

            if (_keysPressed[0]) // Left
            {
                liveKeys++;
                curPos.X -= delta;
                if (curPos.X < 0)
                {
                    curPos.X = 0;
                }
            }

            if (_keysPressed[1]) // Right
            {
                liveKeys++;
                curPos.X += delta;
                if (curPos.X > ActualWidth - delta)
                {
                    curPos.X = ActualWidth - delta;
                }
            }

            if (_keysPressed[3]) // Up
            {
                liveKeys++;
                curPos.Y -= delta;
                if (curPos.Y < 0)
                {
                    curPos.Y = 0;
                }
            }

            if (_keysPressed[2]) // Down
            {
                liveKeys++;
                curPos.Y += delta;
                if (curPos.Y > ActualHeight - delta)
                {
                    curPos.Y = ActualHeight - delta;
                }
            }

            if (liveKeys == 0)
            {
                _moving = false;

                // None of the keys we care about are being hit any longer so shut down our timer
                _tracingTrackingTimer.Stop();
            }
            else
            {
                ActiveTraceCursorPosition = curPos;
                PointerValueChangedEvent?.Invoke(curPos);
            }
        }

        #endregion

        #region Pointer and Manipulation Event Handlers

        protected override void OnPointerEntered(PointerRoutedEventArgs e)
        {
            if (_renderMain != null)
            {
                OnPointerMoved(e);
                e.Handled = true;
            }
            base.OnPointerEntered(e);
        }

        protected override void OnPointerMoved(PointerRoutedEventArgs e)
        {
            if (_renderMain != null)
            {
                _renderMain.DrawNearestPoint = true;
                Point currPosition = e.GetCurrentPoint(this).Position;

                if (_renderMain.ActiveTracing)
                {
                    PointerValueChangedEvent?.Invoke(currPosition);
                    ActiveTraceCursorPosition = currPosition;

                    if (_cachedCursor == null)
                    {
                        _cachedCursor = CoreWindow.GetForCurrentThread().PointerCursor;
                        CoreWindow.GetForCurrentThread().PointerCursor = null;
                    }
                }
                else if (_cachedCursor != null)
                {
                    _renderMain.PointerLocation = currPosition;

                    CoreWindow.GetForCurrentThread().PointerCursor = _cachedCursor;
                    _cachedCursor = null;

                    UpdateTracingChanged();
                }
                else
                {
                    _renderMain.PointerLocation = currPosition;
                    UpdateTracingChanged();
                }

                e.Handled = true;
            }
            base.OnPointerMoved(e);
        }

        protected override void OnPointerExited(PointerRoutedEventArgs e)
        {
            if (_renderMain != null)
            {
                _renderMain.DrawNearestPoint = false;
                TracingChangedEvent?.Invoke(false);
                e.Handled = true;
            }

            if (_cachedCursor != null)
            {
                CoreWindow.GetForCurrentThread().PointerCursor = _cachedCursor;
                _cachedCursor = null;
            }

            base.OnPointerExited(e);
        }

        protected override void OnPointerWheelChanged(PointerRoutedEventArgs e)
        {
            var currentPointer = e.GetCurrentPoint(this);

            double delta = currentPointer.Properties.MouseWheelDelta;

            // The maximum delta is 120 according to Windows documentation
            // Apply a dampening effect so that small mouse movements have a smoother zoom
            const double scrollDamper = 0.15;
            double scale = 1.0 + (Math.Abs(delta) / 120.0) * scrollDamper;

            // positive delta if wheel scrolled away from the user
            if (delta >= 0)
            {
                scale = 1.0 / scale;
            }

            // For scaling, the graphing engine interprets x,y position between the range [-1, 1]
            // Translate the pointer position to the [-1, 1] bounds
            var pos = currentPointer.Position;
            var centerX = (2 * pos.X / ActualWidth - 1);
            var centerY = (1 - 2 * pos.Y / ActualHeight);

            ScaleRange(centerX, centerY, scale);
            GraphViewChangedEvent?.Invoke(this, GraphViewChangedReason.Manipulation);

            e.Handled = true;
            base.OnPointerWheelChanged(e);
        }

        protected override void OnPointerPressed(PointerRoutedEventArgs e)
        {
            // Set the pointer capture to the element being interacted with so that only it
            // will fire pointer-related events
            CapturePointer(e.Pointer);
            base.OnPointerPressed(e);
        }

        protected override void OnPointerReleased(PointerRoutedEventArgs e)
        {
            ReleasePointerCapture(e.Pointer);
            base.OnPointerReleased(e);
        }

        protected override void OnPointerCanceled(PointerRoutedEventArgs e)
        {
            ReleasePointerCapture(e.Pointer);
            base.OnPointerCanceled(e);
        }

        protected override void OnManipulationDelta(ManipulationDeltaRoutedEventArgs e)
        {
            if (_renderMain != null && _graph != null)
            {
                var renderer = _graph.GetRenderer();
                if (renderer != null)
                {
                    // Only call for a render pass if we actually scaled or translated
                    bool needsRenderPass = false;

                    double width = ActualWidth;
                    double height = ActualHeight;

                    // Handle translation
                    var translation = e.Delta.Translation;
                    double translationX = translation.X;
                    double translationY = translation.Y;
                    if (translationX != 0 || translationY != 0)
                    {
                        // The graphing engine pans the graph according to a ratio for x and y
                        // A value of +1 means move a half screen in the positive direction for the given axis
                        // Convert the manipulation's translation values to ratios for the engine
                        translationX /= -width;
                        translationY /= height;

                        if (renderer.MoveRangeByRatio(translationX, translationY) != 0)
                        {
                            return;
                        }
                        needsRenderPass = true;
                    }

                    // Handle scaling
                    double scale = e.Delta.Scale;
                    if (scale != 1.0)
                    {
                        // The graphing engine interprets scale amounts as the inverse of the value retrieved
                        // from the ManipulationUpdatedEventArgs. Invert the scale amount for the engine
                        scale = 1.0 / scale;

                        // Convert from PointerPosition to graph position (-1 to 1 range)
                        var pos = e.Position;
                        double centerX = (2 * pos.X / width - 1);
                        double centerY = (1 - 2 * pos.Y / height);

                        if (renderer.ScaleRange(centerX, centerY, scale) != 0)
                        {
                            return;
                        }
                        needsRenderPass = true;
                    }

                    if (needsRenderPass)
                    {
                        _renderMain.RunRenderPass();
                        GraphViewChangedEvent?.Invoke(this, GraphViewChangedReason.Manipulation);
                    }
                }
            }
            base.OnManipulationDelta(e);
        }

        #endregion

        #region Stream/Export Methods

        public async Task<IRandomAccessStream> GetGraphBitmapStreamAsync()
        {
            if (_renderMain != null && _graph != null)
            {
                var renderer = _graph.GetRenderer();
                if (renderer != null)
                {
                    Graphing.IBitmap bitmapOut;
                    bool hasSomeMissingDataOut;
                    int hr = renderer.GetBitmap(out bitmapOut, out hasSomeMissingDataOut);

                    if (hr == 0 && bitmapOut != null)
                    {
                        // Get the raw data
                        byte[] byteVector = bitmapOut.GetData();

                        // Create a memory stream
                        var stream = new InMemoryRandomAccessStream();

                        // Get a writer to transfer the data
                        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
                        {
                            writer.WriteBytes(byteVector);
                            await writer.StoreAsync();
                            await writer.FlushAsync();
                        }

                        // Reset the stream position
                        stream.Seek(0);
                        return stream;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Grapher::GetGraphBitmapStream() unable to get graph image from renderer");
                        throw new Exception($"Failed to get bitmap: {hr}");
                    }
                }
            }
            return null;
        }

        #endregion
    }

    #endregion

    #region Native Interop

    // These classes would normally use P/Invoke to connect to the native DLL
    internal static class NativeMethods
    {
        private const string GraphingEngineDll = "GraphingImpl.dll";

        // Example P/Invoke declarations would go here
        // [DllImport(GraphingEngineDll)]
        // public static extern IntPtr CreateMathSolver();
        // etc.
    }

    #endregion
}
