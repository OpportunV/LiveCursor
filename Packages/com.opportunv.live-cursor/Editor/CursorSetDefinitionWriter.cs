using System;
using System.Globalization;
using System.Text;

namespace Opportunv.LiveCursor.Editor
{
    internal static class CursorSetDefinitionWriter
    {
        public static string Write(CursorSetDefinition definition)
        {
            StringBuilder builder = new();
            builder.Append("{\n");
            builder.Append("  \"sizes\": ").Append(Numbers(definition.sizes)).Append(",\n");
            builder.Append("  \"hotspot\": ").Append(Numbers(definition.hotspot)).Append(",\n");

            builder.Append("  \"states\": [");
            var states = definition.states ?? Array.Empty<CursorStateDefinition>();
            for (var i = 0; i < states.Length; i++)
            {
                var state = states[i];
                builder.Append(i == 0 ? "\n" : ",\n");
                builder.Append("    {\n");
                builder.Append("      \"name\": ").Append(Quote(state.name)).Append(",\n");
                builder.Append("      \"frames\": ").Append(Frames(state.frames)).Append(",\n");
                builder.Append("      \"frameDurationMs\": ").Append(Number(state.frameDurationMs));
                if (state.loopDelayMs > 0f)
                {
                    builder.Append(",\n      \"loopDelayMs\": ").Append(Number(state.loopDelayMs));
                }

                if (CursorSetDefinitionValidator.HasHotspot(state.hotspot))
                {
                    builder.Append(",\n      \"hotspot\": ").Append(Numbers(state.hotspot));
                }

                builder.Append("\n    }");
            }

            builder.Append(states.Length > 0 ? "\n  ],\n" : "],\n");

            builder.Append("  \"transitions\": [");
            var transitions = definition.transitions ?? Array.Empty<CursorTransitionDefinition>();
            for (var i = 0; i < transitions.Length; i++)
            {
                var transition = transitions[i];
                builder.Append(i == 0 ? "\n" : ",\n");
                builder.Append("    {\n");
                builder.Append("      \"from\": ").Append(Quote(transition.from)).Append(",\n");
                builder.Append("      \"to\": ").Append(Quote(transition.to)).Append(",\n");
                builder.Append("      \"frames\": ").Append(Frames(transition.frames)).Append(",\n");
                builder.Append("      \"frameDurationMs\": ").Append(Number(transition.frameDurationMs)).Append(",\n");
                builder.Append("      \"includesEndpoints\": ")
                    .Append(Bool(transition.includesEndpoints))
                    .Append(",\n");
                builder.Append("      \"reversible\": ").Append(Bool(transition.reversible));
                if (transition.reverseFrameDurationMs > 0f)
                {
                    builder.Append(",\n      \"reverseFrameDurationMs\": ")
                        .Append(Number(transition.reverseFrameDurationMs));
                }

                if (CursorSetDefinitionValidator.HasHotspot(transition.hotspot))
                {
                    builder.Append(",\n      \"hotspot\": ").Append(Numbers(transition.hotspot));
                }

                builder.Append("\n    }");
            }

            builder.Append(transitions.Length > 0 ? "\n  ]" : "]");

            var code = definition.code;
            if (code != null && !string.IsNullOrEmpty(code.path))
            {
                builder.Append(",\n  \"code\": {\n");
                builder.Append("    \"className\": ").Append(Quote(code.className)).Append(",\n");
                builder.Append("    \"namespace\": ").Append(Quote(code.@namespace ?? string.Empty)).Append(",\n");
                builder.Append("    \"path\": ").Append(Quote(code.path)).Append('\n');
                builder.Append("  }");
            }

            builder.Append("\n}\n");
            return builder.ToString();
        }

        private static string Frames(CursorFramesDefinition frames)
        {
            if (frames == null)
            {
                return "{}";
            }

            if (!string.IsNullOrEmpty(frames.folder))
            {
                return $"{{ \"folder\": {Quote(frames.folder)} }}";
            }

            if (frames.files is { Length: > 0 })
            {
                StringBuilder builder = new("{ \"files\": [");
                for (var i = 0; i < frames.files.Length; i++)
                {
                    builder.Append(i == 0 ? string.Empty : ", ").Append(Quote(frames.files[i]));
                }

                return builder.Append("] }").ToString();
            }

            return $"{{ \"sheet\": {Quote(frames.sheet)}, \"columns\": {frames.columns}, " +
                   $"\"rows\": {frames.rows}, \"count\": {frames.count} }}";
        }

        private static string Numbers(int[] values)
        {
            if (values == null)
            {
                return "[]";
            }

            return $"[{string.Join(", ", values)}]";
        }

        private static string Number(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string Bool(bool value)
        {
            return value ? "true" : "false";
        }

        private static string Quote(string value)
        {
            StringBuilder builder = new("\"");
            foreach (var chr in value ?? string.Empty)
            {
                switch (chr)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        builder.Append(chr);
                        break;
                }
            }

            return builder.Append('"').ToString();
        }
    }
}
