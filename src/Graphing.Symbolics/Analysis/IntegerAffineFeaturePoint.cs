namespace Graphing.Symbolics;

internal sealed record IntegerAffineFeaturePoint(ExactReal XOffset, ExactReal XStep, ExactReal YOffset, ExactReal YStep, string Parameter, IntegerConstraint Constraint) : FeaturePoint;
