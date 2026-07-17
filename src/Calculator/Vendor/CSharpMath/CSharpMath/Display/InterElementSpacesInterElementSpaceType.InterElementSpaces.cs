namespace CSharpMath.Display;

using Atom;
using Atoms = Atom.Atoms;
internal enum InterElementSpacesInterElementSpaceType
{
    Invalid = -1,
    None = 0,
    Thin,
    ///<summary>Thin if not in script mode, else none</summary>
    NsThin,
    ///<summary>Medium if not in script mode, else none</summary>
    NsMedium,
    ///<summary>Thick if not in script mode, else none</summary>
    NsThick
}
