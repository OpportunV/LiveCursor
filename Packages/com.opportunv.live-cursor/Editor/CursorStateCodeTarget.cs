using System.Collections.Generic;
using System.Linq;

namespace Opportunv.LiveCursor.Editor
{
    internal sealed class CursorStateCodeTarget
    {
        public string CodePath { get; }

        public string ClassName { get; }

        public string Namespace { get; }

        public List<string> SetPaths { get; } = new();

        public List<List<string>> SetStates { get; } = new();

        public List<string> StateNames { get; } = new();

        public string FullName => string.IsNullOrEmpty(Namespace) ? ClassName : $"{Namespace}.{ClassName}";

        public CursorStateCodeTarget(string codePath, string className, string @namespace)
        {
            CodePath = codePath;
            ClassName = className;
            Namespace = @namespace ?? string.Empty;
        }

        public void Add(string setPath, CursorSetDefinition definition)
        {
            List<string> states = new();
            foreach (var state in definition.states ?? System.Array.Empty<CursorStateDefinition>())
            {
                if (string.IsNullOrEmpty(state.name) || states.Contains(state.name))
                {
                    continue;
                }

                states.Add(state.name);
                if (!StateNames.Contains(state.name))
                {
                    StateNames.Add(state.name);
                }
            }

            SetPaths.Add(setPath);
            SetStates.Add(states);
        }

        public List<string> MissingStates(int setIndex)
        {
            return StateNames.Where(stateName => !SetStates[setIndex].Contains(stateName)).ToList();
        }
    }
}
