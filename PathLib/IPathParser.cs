
using System.Collections.Immutable;

namespace PathLib
{
    public interface IPathParser
    {
        bool ReservedCharactersInPath(string path, out char reservedCharacter);
        string ParseDrive(string remainingPath);
        string ParseRoot(string remainingPath);
        ImmutableList<string> ParseTail(string remainingPath);
    }
}
