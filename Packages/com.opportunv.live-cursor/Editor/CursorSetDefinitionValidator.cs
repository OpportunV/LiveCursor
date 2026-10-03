using System;
using System.Collections.Generic;

namespace Opportunv.LiveCursor.Editor
{
    internal static class CursorSetDefinitionValidator
    {
        public static bool Validate(CursorSetDefinition definition, CursorImportReport report)
        {
            if (definition == null)
            {
                report.Error("The cursor set definition is empty or not valid JSON.");
                return false;
            }

            if (definition.sizes is not { Length: > 0 })
            {
                report.Error("'sizes' must list at least one cursor size, for example [32, 48, 64].");
            }
            else
            {
                HashSet<int> seen = new();
                foreach (var size in definition.sizes)
                {
                    if (size <= 0)
                    {
                        report.Error($"Size {size} must be positive.");
                    }
                    else if (!seen.Add(size))
                    {
                        report.Error($"Size {size} is listed twice.");
                    }
                }
            }

            if (definition.hotspot is not { Length: 2 })
            {
                report.Error("'hotspot' must be [x, y] in source pixels from the top-left corner.");
            }

            if (definition.states is not { Length: > 0 })
            {
                report.Error("'states' must define at least one state.");
                return false;
            }

            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (var state in definition.states)
            {
                if (string.IsNullOrEmpty(state.name))
                {
                    report.Error("Every state needs a 'name'.");
                    continue;
                }

                if (!names.Add(state.name))
                {
                    report.Error($"State '{state.name}' is defined twice.");
                }

                if (state.frameDurationMs <= 0f)
                {
                    report.Error($"{StateLabel(state)}: 'frameDurationMs' must be positive.");
                }

                if (state.loopDelayMs < 0f)
                {
                    report.Error($"{StateLabel(state)}: 'loopDelayMs' cannot be negative.");
                }

                CheckOptionalHotspot(state.hotspot, StateLabel(state), report);
            }

            HashSet<string> pairs = new(StringComparer.Ordinal);
            foreach (var transition in definition.transitions ?? Array.Empty<CursorTransitionDefinition>())
            {
                var label = TransitionLabel(transition);
                if (!names.Contains(transition.from ?? string.Empty))
                {
                    report.Error($"{label}: unknown state '{transition.from}'.");
                }

                if (!names.Contains(transition.to ?? string.Empty))
                {
                    report.Error($"{label}: unknown state '{transition.to}'.");
                }

                if (transition.from == transition.to)
                {
                    report.Error($"{label}: 'from' and 'to' must differ.");
                }

                if (!pairs.Add($"{transition.from}>{transition.to}"))
                {
                    report.Error($"{label} is defined twice.");
                }

                if (transition.frameDurationMs <= 0f)
                {
                    report.Error($"{label}: 'frameDurationMs' must be positive.");
                }

                if (transition.reverseFrameDurationMs < 0f)
                {
                    report.Error($"{label}: 'reverseFrameDurationMs' cannot be negative.");
                }

                CheckOptionalHotspot(transition.hotspot, label, report);
            }

            return !report.HasErrors;
        }

        public static bool HasHotspot(int[] hotspot)
        {
            return hotspot is { Length: 2 };
        }

        public static string StateLabel(CursorStateDefinition state)
        {
            return $"State '{state.name}'";
        }

        public static string TransitionLabel(CursorTransitionDefinition transition)
        {
            return $"Transition '{transition.from}' -> '{transition.to}'";
        }

        private static void CheckOptionalHotspot(int[] hotspot, string label, CursorImportReport report)
        {
            if (hotspot is { Length: > 0 } && !HasHotspot(hotspot))
            {
                report.Error($"{label}: 'hotspot' must be [x, y] in source pixels, or left out to use the default.");
            }
        }
    }
}
