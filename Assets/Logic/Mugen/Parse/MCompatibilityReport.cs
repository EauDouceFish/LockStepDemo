using System.Collections.Generic;

namespace Lockstep.Mugen.Parse
{
    /// <summary>Import diagnostics. Unsupported syntax remains loadable but is never silently forgotten.</summary>
    public sealed class MCompatibilityReport
    {
        public readonly Dictionary<string, int> UnknownControllers = new Dictionary<string, int>();
        public readonly Dictionary<string, int> ParsedOnlyControllers = new Dictionary<string, int>();

        public bool IsClean => UnknownControllers.Count == 0 && ParsedOnlyControllers.Count == 0;

        public void AddUnknownController(string type)
        {
            Add(UnknownControllers, type);
        }

        public void AddParsedOnlyController(string type)
        {
            Add(ParsedOnlyControllers, type);
        }

        static void Add(Dictionary<string, int> target, string type)
        {
            string key = string.IsNullOrEmpty(type) ? "<missing>" : type;
            target.TryGetValue(key, out int count);
            target[key] = count + 1;
        }
    }
}
