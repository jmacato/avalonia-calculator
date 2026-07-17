namespace Graphing
{
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
    }
}
