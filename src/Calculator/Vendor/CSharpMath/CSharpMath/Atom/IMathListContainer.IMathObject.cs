namespace CSharpMath.Atom
{
    public interface IMathListContainer : IMathObject
    {
        System.Collections.Generic.IEnumerable<MathList> InnerLists { get; }
    }
}
