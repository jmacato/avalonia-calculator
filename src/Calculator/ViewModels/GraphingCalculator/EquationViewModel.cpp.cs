// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// #include "pch.h"
// #include "EquationViewModel.cpp.h"
// #include "CalcViewModel\Common\LocalizationSettings.h"
// #include "CalcViewModel\GraphingCalculatorEnums.h"

using  CalculatorApp.ViewModel.Common;
using  Graphing;
using  System; 
using  Windows.ApplicationModel.Resources;
using  Windows.UI;
using  Microsoft.UI.Xaml;
using  Windows.Foundation.Collections;
using  GraphControl;
using System.Collections.Generic;
using System.Text;

namespace CalculatorApp.ViewModel
{
    //GridDisplayItems.GridDisplayItems()
    //    : m_Expression{ "" }
    //    , m_Direction{ "" }
    //{
    //}

    //KeyGraphFeaturesItem.KeyGraphFeaturesItem()
    //    : m_Title{ "" }
    //    , m_DisplayItems{ new List<String ^>() }
    //    , m_GridItems{ new List<GridDisplayItems ^>() }
    //    , m_IsText{ false }
    //{
    //}
    public partial class EquationViewModel { 

        public EquationViewModel(Equation  equation, int functionLabelIndex, Windows.UI.Color color, int colorIndex)

        {
            m_AnalysisErrorVisible = false;
            m_FunctionLabelIndex = functionLabelIndex; 
          m_KeyGraphFeaturesItems = new ();
         m_resourceLoader = ResourceLoader.GetForViewIndependentUse();

            if (equation == null)
        {
                throw new Exception("Equation cannot be nul");
        }

        GraphEquation = equation;
        LineColor = color;
        LineColorIndex = colorIndex;
        IsLineEnabled = true;
    }

    public void PopulateKeyGraphFeatures(KeyGraphFeaturesInfo  graphEquation)
    {
        if (graphEquation.AnalysisError != 0)
        {
            AnalysisErrorVisible = true;
            if (graphEquation.AnalysisError == (AnalysisErrorType.AnalysisCouldNotBePerformed))
            {
                AnalysisErrorString = m_resourceLoader.GetString("KGFAnalysisCouldNotBePerformed");
            }
            else if (graphEquation.AnalysisError == (AnalysisErrorType.AnalysisNotSupported))
            {
                AnalysisErrorString = m_resourceLoader.GetString("KGFAnalysisNotSupported");
            }
            else if (graphEquation.AnalysisError == (AnalysisErrorType.VariableIsNotX))
            {
                AnalysisErrorString = m_resourceLoader.GetString("KGFVariableIsNotX");
            }
            return;
        }

        KeyGraphFeaturesItems.Clear();

        AddKeyGraphFeature(m_resourceLoader.GetString("Domain"), graphEquation.Domain, m_resourceLoader.GetString("KGFDomainNone"));
        AddKeyGraphFeature(m_resourceLoader.GetString("Range"), graphEquation.Range, m_resourceLoader.GetString("KGFRangeNone"));
        AddKeyGraphFeature(m_resourceLoader.GetString("XIntercept"), graphEquation.XIntercept, m_resourceLoader.GetString("KGFXInterceptNone"));
        AddKeyGraphFeature(m_resourceLoader.GetString("YIntercept"), graphEquation.YIntercept, m_resourceLoader.GetString("KGFYInterceptNone"));
        AddKeyGraphFeature(m_resourceLoader.GetString("Minima"), graphEquation.Minima, m_resourceLoader.GetString("KGFMinimaNone"));
        AddKeyGraphFeature(m_resourceLoader.GetString("Maxima"), graphEquation.Maxima, m_resourceLoader.GetString("KGFMaximaNone"));
        AddKeyGraphFeature(
            m_resourceLoader.GetString("InflectionPoints"), graphEquation.InflectionPoints, m_resourceLoader.GetString("KGFInflectionPointsNone"));
        AddKeyGraphFeature(
            m_resourceLoader.GetString("VerticalAsymptotes"), graphEquation.VerticalAsymptotes, m_resourceLoader.GetString("KGFVerticalAsymptotesNone"));
        AddKeyGraphFeature(
            m_resourceLoader.GetString("HorizontalAsymptotes"),
            graphEquation.HorizontalAsymptotes,
            m_resourceLoader.GetString("KGFHorizontalAsymptotesNone"));
        AddKeyGraphFeature(
            m_resourceLoader.GetString("ObliqueAsymptotes"), graphEquation.ObliqueAsymptotes, m_resourceLoader.GetString("KGFObliqueAsymptotesNone"));
        AddParityKeyGraphFeature(graphEquation);
        AddPeriodicityKeyGraphFeature(graphEquation);
        AddMonotoncityKeyGraphFeature(graphEquation);
        AddTooComplexKeyGraphFeature(graphEquation);

        AnalysisErrorVisible = false;
    }

    void AddKeyGraphFeature(String  title, String  expression, String  errorString)
    {
        KeyGraphFeaturesItem  item = new KeyGraphFeaturesItem();
        item.Title = title;
        if (expression != "")
        {
            item.DisplayItems.Add(expression);
            item.IsText = false;
        }
        else
        {
            item.DisplayItems.Add(errorString);
            item.IsText = true;
        }
        KeyGraphFeaturesItems.Add(item);
    }

    void AddKeyGraphFeature(String  title, IList<String>  expressionVector, String  errorString)
    {
        KeyGraphFeaturesItem  item = new KeyGraphFeaturesItem();
        item.Title = title;
        if (expressionVector.Count != 0)
        {
            foreach(var expression in  expressionVector)
            {
                item.DisplayItems.Add(expression);
            }
            item.IsText = false;
        }
        else
        {
            item.DisplayItems.Add(errorString);
            item.IsText = true;
        }
        KeyGraphFeaturesItems.Add(item);
    }

    void AddParityKeyGraphFeature(KeyGraphFeaturesInfo  graphEquation)
    {
        KeyGraphFeaturesItem  parityItem = new KeyGraphFeaturesItem();
        parityItem.Title = m_resourceLoader.GetString("Parity");
        switch (graphEquation.Parity)
        {
        case 0:
            parityItem.DisplayItems.Add(m_resourceLoader.GetString("KGFParityUnknown"));
            break;
        case 1:
            parityItem.DisplayItems.Add(m_resourceLoader.GetString("KGFParityOdd"));
            break;
        case 2:
            parityItem.DisplayItems.Add(m_resourceLoader.GetString("KGFParityEven"));
            break;
        case 3:
            parityItem.DisplayItems.Add(m_resourceLoader.GetString("KGFParityNeither"));
            break;
        default:
            parityItem.DisplayItems.Add(m_resourceLoader.GetString("KGFParityUnknown"));
                    break;
        }
        parityItem.IsText = true;

        KeyGraphFeaturesItems.Add(parityItem);
    }

    void AddPeriodicityKeyGraphFeature(KeyGraphFeaturesInfo  graphEquation)
    {
        KeyGraphFeaturesItem  periodicityItem = new KeyGraphFeaturesItem();
        periodicityItem.Title = m_resourceLoader.GetString("Periodicity");
        switch (graphEquation.PeriodicityDirection)
        {
        case 0:
            // Periodicity is not supported or is too complex to calculate.
            // Return out of this function without adding periodicity to KeyGraphFeatureItems.
            // SetTooComplexFeaturesErrorProperty will set the too complex error when periodicity is supported and unknown
            return;
        case 1:
            if (graphEquation.PeriodicityExpression == "")
            {
                periodicityItem.DisplayItems.Add(m_resourceLoader.GetString("KGFPeriodicityUnknown"));
                periodicityItem.IsText = true;
            }
            else
            {
                periodicityItem.DisplayItems.Add(graphEquation.PeriodicityExpression);
                periodicityItem.IsText = false;
            }
            break;
        case 2:
            periodicityItem.DisplayItems.Add(m_resourceLoader.GetString("KGFPeriodicityNotPeriodic"));
            periodicityItem.IsText = false;
            break;
        default:
            periodicityItem.DisplayItems.Add(m_resourceLoader.GetString("KGFPeriodicityError"));
            periodicityItem.IsText = true;
                    break;
        }

        KeyGraphFeaturesItems.Add(periodicityItem);
    }

    void AddMonotoncityKeyGraphFeature(KeyGraphFeaturesInfo  graphEquation)
    {
        KeyGraphFeaturesItem  monotonicityItem = new KeyGraphFeaturesItem();
        monotonicityItem.Title = m_resourceLoader.GetString("Monotonicity");
        if (graphEquation.Monotonicity.Count != 0)
        {
            foreach(var item in  graphEquation.Monotonicity)
            {
                GridDisplayItems  gridItem = new GridDisplayItems();
                gridItem.Expression = item.Key;

                var monotonicityType = item.Value;
                switch (monotonicityType)
                {
                case "0":
                    gridItem.Direction = m_resourceLoader.GetString("KGFMonotonicityUnknown");
                    break;
                case "1":
                    gridItem.Direction = m_resourceLoader.GetString("KGFMonotonicityIncreasing");
                    break;
                case "2":
                    gridItem.Direction = m_resourceLoader.GetString("KGFMonotonicityDecreasing");
                    break;
                case "3":
                    gridItem.Direction = m_resourceLoader.GetString("KGFMonotonicityConstant");
                    break;
                default:
                    gridItem.Direction = m_resourceLoader.GetString("KGFMonotonicityError");
                    break;
                }

                monotonicityItem.GridItems.Add(gridItem);
            }
            monotonicityItem.IsText = false;
        }
        else
        {
            monotonicityItem.DisplayItems.Add(m_resourceLoader.GetString("KGFMonotonicityError"));
            monotonicityItem.IsText = true;
        }

        KeyGraphFeaturesItems.Add(monotonicityItem);
    }

    void AddTooComplexKeyGraphFeature(KeyGraphFeaturesInfo  graphEquation)
    {
        if (graphEquation.TooComplexFeatures <= 0)
        {
            return;
        }

        string  separator = (LocalizationSettings.GetInstance().GetListSeparator());

        StringBuilder error = new StringBuilder();
        if ((graphEquation.TooComplexFeatures & KeyGraphFeaturesFlag.Domain) == KeyGraphFeaturesFlag.Domain)
        {
            error.Append((m_resourceLoader.GetString("Domain") + separator + " "));
        }
        if ((graphEquation.TooComplexFeatures & KeyGraphFeaturesFlag.Range) == KeyGraphFeaturesFlag.Range)
        {
            error.Append((m_resourceLoader.GetString("Range") + separator + " "));
        }
        if ((graphEquation.TooComplexFeatures & KeyGraphFeaturesFlag.Zeros) == KeyGraphFeaturesFlag.Zeros)
        {
            error.Append((m_resourceLoader.GetString("XIntercept") + separator + " "));
        }
        if ((graphEquation.TooComplexFeatures & KeyGraphFeaturesFlag.YIntercept) == KeyGraphFeaturesFlag.YIntercept)
        {
            error.Append((m_resourceLoader.GetString("YIntercept") + separator + " "));
        }
        if ((graphEquation.TooComplexFeatures & KeyGraphFeaturesFlag.Parity) == KeyGraphFeaturesFlag.Parity)
        {
            error.Append((m_resourceLoader.GetString("Parity") + separator + " "));
        }
        if ((graphEquation.TooComplexFeatures & KeyGraphFeaturesFlag.Periodicity) == KeyGraphFeaturesFlag.Periodicity)
        {
            error.Append((m_resourceLoader.GetString("Periodicity") + separator + " "));
        }
        if ((graphEquation.TooComplexFeatures & KeyGraphFeaturesFlag.Minima) == KeyGraphFeaturesFlag.Minima)
        {
            error.Append((m_resourceLoader.GetString("Minima") + separator + " "));
        }
        if ((graphEquation.TooComplexFeatures & KeyGraphFeaturesFlag.Maxima) == KeyGraphFeaturesFlag.Maxima)
        {
            error.Append((m_resourceLoader.GetString("Maxima") + separator + " "));
        }
        if ((graphEquation.TooComplexFeatures & KeyGraphFeaturesFlag.InflectionPoints) == KeyGraphFeaturesFlag.InflectionPoints)
        {
            error.Append((m_resourceLoader.GetString("InflectionPoints") + separator + " "));
        }
        if ((graphEquation.TooComplexFeatures & KeyGraphFeaturesFlag.VerticalAsymptotes) == KeyGraphFeaturesFlag.VerticalAsymptotes)
        {
            error.Append((m_resourceLoader.GetString("VerticalAsymptotes") + separator + " "));
        }
        if ((graphEquation.TooComplexFeatures & KeyGraphFeaturesFlag.HorizontalAsymptotes) == KeyGraphFeaturesFlag.HorizontalAsymptotes)
        {
            error.Append((m_resourceLoader.GetString("HorizontalAsymptotes") + separator + " "));
        }
        if ((graphEquation.TooComplexFeatures & KeyGraphFeaturesFlag.ObliqueAsymptotes) == KeyGraphFeaturesFlag.ObliqueAsymptotes)
        {
            error.Append((m_resourceLoader.GetString("ObliqueAsymptotes") + separator + " "));
        }
        if ((graphEquation.TooComplexFeatures & KeyGraphFeaturesFlag.MonotoneIntervals) == KeyGraphFeaturesFlag.MonotoneIntervals)
        {
            error.Append((m_resourceLoader.GetString("Monotonicity") + separator + " "));
        }

            error.Remove(error.Length - (separator.Length + 1), 1);

        KeyGraphFeaturesItem  tooComplexItem = new KeyGraphFeaturesItem();
        tooComplexItem.DisplayItems.Add(m_resourceLoader.GetString("KGFTooComplexFeaturesError"));
            tooComplexItem.DisplayItems.Add(error.ToString()); //new String(error.substr(0, (error.length() - (separator.Length() + 1)))));
        tooComplexItem.IsText = true;

        KeyGraphFeaturesItems.Add(tooComplexItem);
    }

    public static String  EquationErrorText(ErrorType errorType, int errorCode)
    {
        var resLoader = ResourceLoader.GetForViewIndependentUse();
        if (errorType == ErrorType.Evaluation)
        {
            switch ((EvaluationErrorCode)(errorCode))
            {
            case (EvaluationErrorCode.Overflow):
                return resLoader.GetString("Overflow");
                break;
            case (EvaluationErrorCode.RequireRadiansMode):
                return resLoader.GetString("RequireRadiansMode");
                break;
            case (EvaluationErrorCode.TooComplexToSolve):
                return resLoader.GetString("TooComplexToSolve");
                break;
            case (EvaluationErrorCode.RequireDegreesMode):
                return resLoader.GetString("RequireDegreesMode");
                break;
            case (EvaluationErrorCode.FactorialInvalidArgument):
            case (EvaluationErrorCode.Factorial2InvalidArgument):
                return resLoader.GetString("FactorialInvalidArgument");
                break;
            case (EvaluationErrorCode.FactorialCannotPerformOnLargeNumber):
                return resLoader.GetString("FactorialCannotPerformOnLargeNumber");
                break;
            case (EvaluationErrorCode.ModuloCannotPerformOnFloat):
                return resLoader.GetString("ModuloCannotPerformOnFloat");
                break;
            case (EvaluationErrorCode.EquationTooComplexToSolve):
            case (EvaluationErrorCode.EquationTooComplexToSolveSymbolic):
            case (EvaluationErrorCode.EquationTooComplexToPlot):
            case (EvaluationErrorCode.InequalityTooComplexToSolve):
            case (EvaluationErrorCode.GE_TooComplexToSolve):
                return resLoader.GetString("TooComplexToSolve");
                break;
            case (EvaluationErrorCode.EquationHasNoSolution):
            case (EvaluationErrorCode.InequalityHasNoSolution):
                return resLoader.GetString("EquationHasNoSolution");
                break;
            case (EvaluationErrorCode.DivideByZero):
                return resLoader.GetString("DivideByZero");
                break;
            case (EvaluationErrorCode.MutuallyExclusiveConditions):
                return resLoader.GetString("MutuallyExclusiveConditions");
                break;
            case (EvaluationErrorCode.OutOfDomain):
                return resLoader.GetString("OutOfDomain");
                break;
            case (EvaluationErrorCode.GE_NotSupported):
                return resLoader.GetString("GE_NotSupported");
                break;
            default:
                return resLoader.GetString("GeneralError");
                break;
            }
        }
        else if (errorType == ErrorType.Syntax)
        {
            switch ((SyntaxErrorCode)(errorCode))
            {
            case (SyntaxErrorCode.ParenthesisMismatch):
                return resLoader.GetString("ParenthesisMismatch");
                break;
            case (SyntaxErrorCode.UnmatchedParenthesis):
                return resLoader.GetString("UnmatchedParenthesis");
                break;
            case (SyntaxErrorCode.TooManyDecimalPoints):
                return resLoader.GetString("TooManyDecimalPoints");
                break;
            case (SyntaxErrorCode.DecimalPointWithoutDigits):
                return resLoader.GetString("DecimalPointWithoutDigits");
                break;
            case (SyntaxErrorCode.UnexpectedEndOfExpression):
                return resLoader.GetString("UnexpectedEndOfExpression");
                break;
            case (SyntaxErrorCode.UnexpectedToken):
                return resLoader.GetString("UnexpectedToken");
                break;
            case (SyntaxErrorCode.InvalidToken):
                return resLoader.GetString("InvalidToken");
                break;
            case (SyntaxErrorCode.TooManyEquals):
                return resLoader.GetString("TooManyEquals");
                break;
            case (SyntaxErrorCode.EqualWithoutGraphVariable):
                return resLoader.GetString("EqualWithoutGraphVariable");
                break;
            case (SyntaxErrorCode.InvalidEquationSyntax):
            case (SyntaxErrorCode.InvalidEquationFormat):
                return resLoader.GetString("InvalidEquationSyntax");
                break;
            case (SyntaxErrorCode.EmptyExpression):
                return resLoader.GetString("EmptyExpression");
                break;
            case (SyntaxErrorCode.EqualWithoutEquation):
                return resLoader.GetString("EqualWithoutEquation");
                break;
            case (SyntaxErrorCode.ExpectParenthesisAfterFunctionName):
                return resLoader.GetString("ExpectParenthesisAfterFunctionName");
                break;
            case (SyntaxErrorCode.IncorrectNumParameter):
                return resLoader.GetString("IncorrectNumParameter");
                break;
            case (SyntaxErrorCode.InvalidVariableNameFormat):
                return resLoader.GetString("InvalidVariableNameFormat");
                break;
            case (SyntaxErrorCode.BracketMismatch):
                return resLoader.GetString("BracketMismatch");
                break;
            case (SyntaxErrorCode.UnmatchedBracket):
                return resLoader.GetString("UnmatchedBracket");
                break;
            case (SyntaxErrorCode.CannotUseIInReal):
                return resLoader.GetString("CannotUseIInRea");
                break;
            case (SyntaxErrorCode.InvalidNumberDigit):
                return resLoader.GetString("InvalidNumberDigit");
                break;
            case (SyntaxErrorCode.InvalidNumberBase):
                return resLoader.GetString("InvalidNumberBase");
                break;
            case (SyntaxErrorCode.InvalidVariableSpecification):
                return resLoader.GetString("InvalidVariableSpecification");
                break;
            case (SyntaxErrorCode.ExpectingLogicalOperands):
            case (SyntaxErrorCode.ExpectingScalarOperands):
                return resLoader.GetString("ExpectingLogicalOperands");
                break;
            case (SyntaxErrorCode.CannotUseIndexVarInOpLimits):
                return resLoader.GetString("CannotUseIndexVarInOpLimits");
                break;
            case (SyntaxErrorCode.CannotUseIndexVarInLimPoint):
                return resLoader.GetString("Overflow");
                break;
            case (SyntaxErrorCode.CannotUseComplexInfinityInReal):
                return resLoader.GetString("CannotUseComplexInfinityInRea");
                break;
            case (SyntaxErrorCode.CannotUseIInInequalitySolving):
                return resLoader.GetString("CannotUseIInInequalitySolving");
                break;
            default:
                return resLoader.GetString("GeneralError");
                break;
            }
        }

        return resLoader.GetString("GeneralError");
}
}
}
