# Graphing.Symbolics provenance

`Graphing.Symbolics` is an independent implementation written for this repository. It contains no copied CAS source, generated third-party source, binary dependency, or package reference. The implementation uses only .NET BCL types, principally `System.Numerics.BigInteger` and immutable collections.

The following publications are specifications for theorem selection and independently derived algorithms, not sources of implementation text:

- Daniel Richardson, “Some undecidable problems involving elementary functions of a real variable” — the reason every obligation has a proved/disproved/unknown result and unsupported elementary-function questions remain unknown.
- David Stoutemyer, “Simplifying Fractions of Powers” — domain-preserving rewrite requirements. The value DAG and definedness formula are deliberately separate, so cancellation and zero-product rewrites cannot erase holes.
- Basu, Pollack, and Roy, *Algorithms in Real Algebraic Geometry* — polynomial remainder sequences, Sturm sign determination, square-free decomposition, algebraic isolating intervals, and cell decomposition.
- Daniel Perrucci and Marie-Françoise Roy, “Elementary recursive quantifier elimination based on Thom encoding” — derivative-sign (Thom) encodings attached to isolated algebraic roots.
- Chen, Ge, Li, and Xia, “Real root isolation of regular trigonometric polynomials” and the associated ISSAC 2023 decision-procedure slides — tangent half-angle reduction, projective endpoint handling, and periodic root families. No LGPL implementation was inspected or copied.
- Cooper’s Presburger elimination theorem — integer-preimage obligations and the primitive inverse metadata used for `IsInteger(tan(x))`.
- Tucker, “A validated real function calculus,” and Dominik Gruntz’s limit framework are reserved specifications for a later validated analytic backend. The current engine does not convert an unimplemented analytic argument into a result.

Every published result crosses the independent `CertificateChecker`. Polynomial certificates replay their Sturm chains and cell signs. Theorem certificates re-derive the normalized obligation and result from the input semantic graph. Mutation or replay failure is converted to `Unknown(CertificateRejected)` by the production engine.
