namespace Graphing.Symbolics;

internal sealed record PresburgerComparison(LinearIntegerExpression Expression, IntegerRelation Relation) : PresburgerFormula;
