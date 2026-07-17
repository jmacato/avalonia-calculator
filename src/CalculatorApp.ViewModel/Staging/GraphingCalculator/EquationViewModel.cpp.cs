// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// #include "pch.h"
// #include "EquationViewModel.cpp.h"
// #include "CalcViewModel\Common\LocalizationSettings.h"
// #include "CalcViewModel\GraphingCalculatorEnums.h"

using CalculatorApp.ViewModel.Common;
using Graphing;
using System;
using Windows.ApplicationModel.Resources;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.Foundation.Collections;
using GraphControl;
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
    public partial class EquationViewModel
    {

        public EquationViewModel(Equation equation, int functionLabelIndex, Windows.UI.Color color, int colorIndex)

        {
            m_AnalysisErrorVisible = false;
            m_FunctionLabelIndex = functionLabelIndex;
            m_KeyGraphFeaturesItems = new();
            m_resourceLoader = ResourceLoader.GetForCurrentView();

            if (equation == null)
            {
                throw new ArgumentNullException(nameof(equation));
            }

            m_GraphEquation = equation;
            LineColor = color;
            LineColorIndex = colorIndex;
            IsLineEnabled = true;
        }

        public void PopulateKeyGraphFeatures(KeyGraphFeaturesInfo graphEquation)
        {
            if (graphEquation is null)
            {
                throw new ArgumentNullException(nameof(graphEquation));
            }

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

        void AddKeyGraphFeature(String title, String expression, String errorString)
        {
            KeyGraphFeaturesItem item = new KeyGraphFeaturesItem();
            item.Title = title;
            if (!string.IsNullOrEmpty(expression))
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

        void AddKeyGraphFeature(String title, IList<String> expressionVector, String errorString)
        {
            KeyGraphFeaturesItem item = new KeyGraphFeaturesItem();
            item.Title = title;
            if (expressionVector.Count != 0)
            {
                foreach (var expression in expressionVector)
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

        void AddParityKeyGraphFeature(KeyGraphFeaturesInfo graphEquation)
        {
            KeyGraphFeaturesItem parityItem = new KeyGraphFeaturesItem();
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

        void AddPeriodicityKeyGraphFeature(KeyGraphFeaturesInfo graphEquation)
        {
            KeyGraphFeaturesItem periodicityItem = new KeyGraphFeaturesItem();
            periodicityItem.Title = m_resourceLoader.GetString("Periodicity");
            switch (graphEquation.PeriodicityDirection)
            {
                case 0:
                    // Periodicity is not supported or is too complex to calculate.
                    // Return out of this function without adding periodicity to KeyGraphFeatureItems.
                    // SetTooComplexFeaturesErrorProperty will set the too complex error when periodicity is supported and unknown
                    return;
                case 1:
                    if (string.IsNullOrEmpty(graphEquation.PeriodicityExpression))
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

        void AddMonotoncityKeyGraphFeature(KeyGraphFeaturesInfo graphEquation)
        {
            KeyGraphFeaturesItem monotonicityItem = new KeyGraphFeaturesItem();
            monotonicityItem.Title = m_resourceLoader.GetString("Monotonicity");
            if (graphEquation.Monotonicity.Count != 0)
            {
                foreach (var item in graphEquation.Monotonicity)
                {
                    GridDisplayItems gridItem = new GridDisplayItems();
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

        void AddTooComplexKeyGraphFeature(KeyGraphFeaturesInfo graphEquation)
        {
            if (graphEquation.TooComplexFeatures <= 0)
            {
                return;
            }

            string separator = (LocalizationSettings.Instance.ListSeparator);

            StringBuilder error = new StringBuilder();
            if ((graphEquation.TooComplexFeatures & KeyGraphFeatures.Domain) == KeyGraphFeatures.Domain)
            {
                error.Append((m_resourceLoader.GetString("Domain") + separator + " "));
            }
            if ((graphEquation.TooComplexFeatures & KeyGraphFeatures.Range) == KeyGraphFeatures.Range)
            {
                error.Append((m_resourceLoader.GetString("Range") + separator + " "));
            }
            if ((graphEquation.TooComplexFeatures & KeyGraphFeatures.Zeros) == KeyGraphFeatures.Zeros)
            {
                error.Append((m_resourceLoader.GetString("XIntercept") + separator + " "));
            }
            if ((graphEquation.TooComplexFeatures & KeyGraphFeatures.YIntercept) == KeyGraphFeatures.YIntercept)
            {
                error.Append((m_resourceLoader.GetString("YIntercept") + separator + " "));
            }
            if ((graphEquation.TooComplexFeatures & KeyGraphFeatures.Parity) == KeyGraphFeatures.Parity)
            {
                error.Append((m_resourceLoader.GetString("Parity") + separator + " "));
            }
            if ((graphEquation.TooComplexFeatures & KeyGraphFeatures.Periodicity) == KeyGraphFeatures.Periodicity)
            {
                error.Append((m_resourceLoader.GetString("Periodicity") + separator + " "));
            }
            if ((graphEquation.TooComplexFeatures & KeyGraphFeatures.Minima) == KeyGraphFeatures.Minima)
            {
                error.Append((m_resourceLoader.GetString("Minima") + separator + " "));
            }
            if ((graphEquation.TooComplexFeatures & KeyGraphFeatures.Maxima) == KeyGraphFeatures.Maxima)
            {
                error.Append((m_resourceLoader.GetString("Maxima") + separator + " "));
            }
            if ((graphEquation.TooComplexFeatures & KeyGraphFeatures.InflectionPoints) == KeyGraphFeatures.InflectionPoints)
            {
                error.Append((m_resourceLoader.GetString("InflectionPoints") + separator + " "));
            }
            if ((graphEquation.TooComplexFeatures & KeyGraphFeatures.VerticalAsymptotes) == KeyGraphFeatures.VerticalAsymptotes)
            {
                error.Append((m_resourceLoader.GetString("VerticalAsymptotes") + separator + " "));
            }
            if ((graphEquation.TooComplexFeatures & KeyGraphFeatures.HorizontalAsymptotes) == KeyGraphFeatures.HorizontalAsymptotes)
            {
                error.Append((m_resourceLoader.GetString("HorizontalAsymptotes") + separator + " "));
            }
            if ((graphEquation.TooComplexFeatures & KeyGraphFeatures.ObliqueAsymptotes) == KeyGraphFeatures.ObliqueAsymptotes)
            {
                error.Append((m_resourceLoader.GetString("ObliqueAsymptotes") + separator + " "));
            }
            if ((graphEquation.TooComplexFeatures & KeyGraphFeatures.MonotoneIntervals) == KeyGraphFeatures.MonotoneIntervals)
            {
                error.Append((m_resourceLoader.GetString("Monotonicity") + separator + " "));
            }

            error.Remove(error.Length - (separator.Length + 1), 1);

            KeyGraphFeaturesItem tooComplexItem = new KeyGraphFeaturesItem();
            tooComplexItem.DisplayItems.Add(m_resourceLoader.GetString("KGFTooComplexFeaturesError"));
            tooComplexItem.DisplayItems.Add(error.ToString()); //new String(error.substr(0, (error.length() - (separator.Length() + 1)))));
            tooComplexItem.IsText = true;

            KeyGraphFeaturesItems.Add(tooComplexItem);
        }

        public static String EquationErrorText(ErrorType errorType, int errorCode)
        {
            var resLoader = ResourceLoader.GetForCurrentView();
            string resourceName = errorType switch
            {
                ErrorType.Evaluation => EvaluationErrorResourceName((EvaluationErrorCode)errorCode),
                ErrorType.Syntax => SyntaxErrorResourceName((SyntaxErrorCode)errorCode),
                _ => "GeneralError"
            };
            return resLoader.GetString(resourceName);
        }

        static string EvaluationErrorResourceName(EvaluationErrorCode errorCode)
        {
            return errorCode switch
            {
                EvaluationErrorCode.Overflow => "Overflow",
                EvaluationErrorCode.RequireRadiansMode => "RequireRadiansMode",
                EvaluationErrorCode.TooComplexToSolve => "TooComplexToSolve",
                EvaluationErrorCode.RequireDegreesMode => "RequireDegreesMode",
                EvaluationErrorCode.FactorialInvalidArgument or EvaluationErrorCode.Factorial2InvalidArgument
                    => "FactorialInvalidArgument",
                EvaluationErrorCode.FactorialCannotPerformOnLargeNumber => "FactorialCannotPerformOnLargeNumber",
                EvaluationErrorCode.ModuloCannotPerformOnFloat => "ModuloCannotPerformOnFloat",
                EvaluationErrorCode.EquationTooComplexToSolve
                    or EvaluationErrorCode.EquationTooComplexToSolveSymbolic
                    or EvaluationErrorCode.EquationTooComplexToPlot
                    or EvaluationErrorCode.InequalityTooComplexToSolve
                    or EvaluationErrorCode.GraphingEngineTooComplexToSolve => "TooComplexToSolve",
                EvaluationErrorCode.EquationHasNoSolution or EvaluationErrorCode.InequalityHasNoSolution
                    => "EquationHasNoSolution",
                EvaluationErrorCode.DivideByZero => "DivideByZero",
                EvaluationErrorCode.MutuallyExclusiveConditions => "MutuallyExclusiveConditions",
                EvaluationErrorCode.OutOfDomain => "OutOfDomain",
                EvaluationErrorCode.GraphingEngineNotSupported => "GE_NotSupported",
                _ => "GeneralError"
            };
        }

        static string SyntaxErrorResourceName(SyntaxErrorCode errorCode)
        {
            return BasicSyntaxErrorResourceName(errorCode)
                ?? AdvancedSyntaxErrorResourceName(errorCode)
                ?? "GeneralError";
        }

        static string? BasicSyntaxErrorResourceName(SyntaxErrorCode errorCode)
        {
            return errorCode switch
            {
                SyntaxErrorCode.ParenthesisMismatch => "ParenthesisMismatch",
                SyntaxErrorCode.UnmatchedParenthesis => "UnmatchedParenthesis",
                SyntaxErrorCode.TooManyDecimalPoints => "TooManyDecimalPoints",
                SyntaxErrorCode.DecimalPointWithoutDigits => "DecimalPointWithoutDigits",
                SyntaxErrorCode.UnexpectedEndOfExpression => "UnexpectedEndOfExpression",
                SyntaxErrorCode.UnexpectedToken => "UnexpectedToken",
                SyntaxErrorCode.InvalidToken => "InvalidToken",
                SyntaxErrorCode.TooManyEquals => "TooManyEquals",
                SyntaxErrorCode.EqualWithoutGraphVariable => "EqualWithoutGraphVariable",
                SyntaxErrorCode.InvalidEquationSyntax or SyntaxErrorCode.InvalidEquationFormat => "InvalidEquationSyntax",
                SyntaxErrorCode.EmptyExpression => "EmptyExpression",
                SyntaxErrorCode.EqualWithoutEquation => "EqualWithoutEquation",
                SyntaxErrorCode.ExpectParenthesisAfterFunctionName => "ExpectParenthesisAfterFunctionName",
                SyntaxErrorCode.IncorrectNumParameter => "IncorrectNumParameter",
                SyntaxErrorCode.InvalidVariableNameFormat => "InvalidVariableNameFormat",
                _ => null
            };
        }

        static string? AdvancedSyntaxErrorResourceName(SyntaxErrorCode errorCode)
        {
            return errorCode switch
            {
                SyntaxErrorCode.BracketMismatch => "BracketMismatch",
                SyntaxErrorCode.UnmatchedBracket => "UnmatchedBracket",
                SyntaxErrorCode.CannotUseIInReal => "CannotUseIInRea",
                SyntaxErrorCode.InvalidNumberDigit => "InvalidNumberDigit",
                SyntaxErrorCode.InvalidNumberBase => "InvalidNumberBase",
                SyntaxErrorCode.InvalidVariableSpecification => "InvalidVariableSpecification",
                SyntaxErrorCode.ExpectingLogicalOperands or SyntaxErrorCode.ExpectingScalarOperands
                    => "ExpectingLogicalOperands",
                SyntaxErrorCode.CannotUseIndexVarInOpLimits => "CannotUseIndexVarInOpLimits",
                SyntaxErrorCode.CannotUseIndexVarInLimPoint => "Overflow",
                SyntaxErrorCode.CannotUseComplexInfinityInReal => "CannotUseComplexInfinityInRea",
                SyntaxErrorCode.CannotUseIInInequalitySolving => "CannotUseIInInequalitySolving",
                _ => null
            };
        }
    }
}
