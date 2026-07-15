namespace Graphing
{

public enum LocalizationType
{
    Unknown = 0,
    DecimalPointAndListComma = 1,
    DecimalPointAndListSemicolon = 2,
    DecimalCommaAndListSemicolon = 3
}

public enum EquationParsingMode
{
    SolveEquation = 0,
    GraphEquation = 1,
    NonEquation = 2,
    DoNotCare = 3
}

public enum FormatType
{
    Formula = 0,
    InvariantFormula = 1,
    FormulaWithoutAggregate = 2,
    Linear = 3,
    LinearInput = 4,
    MathML = 5,
    MathMLNoWrapper = 6,
    MathRichEdit = 7,
    InlineMathRichEdit = 8,
    Binary = 9,
    InvariantBinary = 10,
    Base64 = 11,
    InvariantBase64 = 12,
    Latex = 13
}

public enum EvalNumberField
{
    Invalid = 0,
    Real = 1,
    Complex = 2
}

public enum EvalExpandMode
{
    Neutral = 0,
    Expand = 1,
    Factor = 2
}

public enum EvalTrigUnitMode
{
    Invalid = 0,
    Radians = 1,
    Degrees = 2,
    Grads = 3
}

public enum ContextualActionType
{
    None = 0,
    SolveEquation = 1,
    Compare = 2,
    Expand = 3,
    Graph2D = 4,
    ListGraph2D = 5,
    GraphBothSides2D = 6,
    Graph3D = 7,
    GraphBothSides3D = 8,
    GraphInequality = 9,
    Assign = 10,
    Factor = 11,
    Deriv = 12,
    IndefiniteIntegral = 13,
    Graph2DExpression = 14,
    Graph3DExpression = 15,
    SolveInequality = 16,
    Calculate = 17,
    Round = 18,
    Floor = 19,
    Ceiling = 20,
    MatrixMask = 21,
    MatrixDeterminant = 22,
    MatrixInverse = 23,
    MatrixTrace = 24,
    MatrixTranspose = 25,
    MatrixSize = 26,
    MatrixReduce = 27,
    ListMask = 28,
    ListSort = 29,
    ListMean = 30,
    ListMedian = 31,
    ListMode = 32,
    ListLcm = 33,
    ListGcf = 34,
    ListSum = 35,
    ListProduct = 36,
    ListMax = 37,
    ListMin = 38,
    ListVariance = 39,
    ListStdDev = 40,
    ShowVerboseSolution = 41,
    TypeMask = 42,
    Informational = 43
}

public enum MathActionCategoryType
{
    Unknown = 0,
    Calculate = 1,
    Solve = 2,
    Integrate = 3,
    Differentiate = 4,
    Algebra = 5,
    Matrix = 6,
    List = 7,
    Graph = 8
}

public enum StepSequenceType
{
    None = 0,
    Text = 1,
    Expression = 2,
    NewLine = 3,
    NewStep = 4,
    Conditional = 5,
    Composite = 6,
    Goto = 7,
    Call = 8,
    Return = 9,
    Stop = 10,
    Error = 11,
    GotoTemp = 12
}

public enum FormatVerbosityMode
{
    Verbose = 0,
    Simple = 1
}

namespace Renderer
{
    public enum ChangeRangeAction
    {
        ZoomIn = 0,
        ZoomOut = 1,
        WidenX = 2,
        ShrinkX = 3,
        WidenY = 4,
        ShrinkY = 5,
        WidenZ = 6,
        ShrinkZ = 7,
        MoveNegativeX = 8,
        MovePositiveX = 9,
        MoveNegativeY = 10,
        MovePositiveY = 11,
        MoveNegativeZ = 12,
        MovePositiveZ = 13,
        SmoothZoomIn = 14,
        SmoothZoomOut = 15,
        PinchZoomIn = 16,
        PinchZoomOut = 17
    }

    public enum LineStyle
    {
        Solid = 0,
        Dot = 1,
        Dash = 2,
        DashDot = 3,
        DashDotDot = 4
    }
}

namespace Analyzer
{
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

    [Flags]
    public enum PerformAnalysisType : uint
    {
        None = 0,
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

    public enum FunctionParityType
    {
        Unknown = 0,
        Odd = 1,
        Even = 2,
        None = 3
    }

    public enum FunctionMonotonicityType
    {
        Unknown = 0,
        Ascending = 1,
        Descending = 2,
        Constant = 3
    }

    public enum AsymptoteType
    {
        Unknown = 0,
        PositiveInfinity = 1,
        NegativeInfinity = 2,
        AnyInfinity = 3
    }

    public enum FunctionPeriodicityType
    {
        Unknown = 0,
        Periodic = 1,
        NotPeriodic = 2
    }
}
}
