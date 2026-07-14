// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using CalcEngine;
using CalculationManager;

namespace CalcEngine;

// Unary operator Function Name table Element
// since unary operators button names aren't exactly friendly for history purpose,
// we have this separate table to get its localized name and for its Inv function if it exists.
// Unary operator Function Name table Element
// since unary operators button names aren't exactly friendly for history purpose,
// we have this separate table to get its localized name and for its Inv function if it exists.
public struct FunctionNameElement : IEquatable<FunctionNameElement>
{
    public string DegreeString { get; set; } // Used by default if there are no rad or grad specific strings.

    public string InverseDegreeString { get; set; } // Will fall back to DegreeString if empty

    public string RadString { get; set; }

    public string InverseRadString { get; set; } // Will fall back to RadString if empty

    public string GradString { get; set; }

    public string InverseGradString { get; set; } // Will fall back to GradString if empty

    public string ProgrammerModeString { get; set; }

    public bool hasAngleStrings => (!string.IsNullOrEmpty(RadString) || !string.IsNullOrEmpty(InverseRadString) ||
                                    !string.IsNullOrEmpty(GradString) || !string.IsNullOrEmpty(InverseGradString));

    public bool Equals(FunctionNameElement other)
    {
        return DegreeString == other.DegreeString &&
               InverseDegreeString == other.InverseDegreeString &&
               RadString == other.RadString &&
               InverseRadString == other.InverseRadString &&
               GradString == other.GradString &&
               InverseGradString == other.InverseGradString &&
               ProgrammerModeString == other.ProgrammerModeString;
    }

    public override bool Equals(object? obj)
    {
        return obj is FunctionNameElement other && Equals(other);
    }

    public override int GetHashCode()
    {
        return (DegreeString, InverseDegreeString, RadString, InverseRadString, GradString, InverseGradString,
            ProgrammerModeString).GetHashCode();
    }

    public static bool operator ==(FunctionNameElement left, FunctionNameElement right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(FunctionNameElement left, FunctionNameElement right)
    {
        return !left.Equals(right);
    }
}
