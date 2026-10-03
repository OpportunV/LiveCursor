using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Opportunv.LiveCursor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Dev.Diagnostics
{
    public sealed class CursorDiagnostics : MonoBehaviour
    {
        [SerializeField] private CursorSet[] _sets;
        [SerializeField] private float _playbackSeconds = 10f;
        [SerializeField] private float _stepSeconds = 0.2f;

        private const string LogPrefix = "[LiveCursorPerf] ";
        private const float EnvironmentInterval = 1f;
        private const float TargetSpacing = 90f;
        private const float TargetRadius = 18f;

        private static readonly int[] _patternSizes = { 32, 48, 64, 96, 128 };

        private readonly TimingCursorOutput _output = new();
        private readonly StringBuilder _report = new();
        private readonly Stopwatch _stopwatch = new();
        private Texture2D[] _patterns;
        private Texture2D _dot;
        private CursorPlayer _player;
        private bool _running;
        private int _patternIndex = -1;
        private int _setIndex;
        private int _stateIndex;
        private float _environmentTimer;
        private string _environment = string.Empty;
        private string _loggedEnvironment;
        private string _lastClick = "Click a target centre with the cursor's click point.";
        private int _marks;
        private Vector2 _scroll;

        private IEnumerator Start()
        {
            _player = new(_output);
            _patterns = new Texture2D[_patternSizes.Length];
            for (var i = 0; i < _patterns.Length; i++)
            {
                _patterns[i] = CursorPatternTextures.Create(_patternSizes[i]);
            }

            _dot = Texture2D.whiteTexture;
            yield return null;
            yield return RunRealistic();
        }

        private void Update()
        {
            _environmentTimer -= Time.unscaledDeltaTime;
            if (_environmentTimer <= 0f)
            {
                _environmentTimer = EnvironmentInterval;
                if (!_running)
                {
                    _player.SetSystemCursorSize(SystemCursorSize.Get());
                }

                _environment = DescribeEnvironment();
                if (_environment != _loggedEnvironment)
                {
                    _loggedEnvironment = _environment;
                    Debug.Log(LogPrefix + "Environment: " + _environment);
                }
            }

            if (!_running && _patternIndex < 0)
            {
                _player.Tick(Time.unscaledDeltaTime);
            }
        }

        private void OnGUI()
        {
            GUI.skin.label.fontSize = 15;
            GUI.skin.button.fontSize = 15;
            var width = Screen.width - 32f;
            var panelHeight = Screen.height * 0.55f;
            GUILayout.BeginArea(new(16f, 16f, width, panelHeight));
            GUILayout.Label(_environment);
            DrawButtons();
            DrawPatternButtons();
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Label(_running ? "Running..." : _report.ToString());
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            DrawTargets(new(16f, 32f + panelHeight, width, Screen.height - panelHeight - 48f));
        }

        private void DrawButtons()
        {
            GUILayout.BeginHorizontal();
            GUI.enabled = !_running;
            if (GUILayout.Button("Benchmark (realistic)", GUILayout.Width(200f)))
            {
                StartCoroutine(RunRealistic());
            }

            if (GUILayout.Button("Full matrix (fills Unity's cache)", GUILayout.Width(270f)))
            {
                StartCoroutine(RunMatrix());
            }

            if (GUILayout.Button("Next state", GUILayout.Width(120f)))
            {
                ShowCursor();
                NextState(true);
            }

            if (GUILayout.Button("Next set", GUILayout.Width(120f)))
            {
                ShowCursor();
                _setIndex = (_setIndex + 1) % _sets.Length;
                _player.SetSet(_sets[_setIndex]);
            }

            GUI.enabled = true;
            if (GUILayout.Button($"Mark {_marks + 1}", GUILayout.Width(100f)))
            {
                _marks++;
                Debug.Log($"{LogPrefix}Mark {_marks}: {DescribeEnvironment()}");
            }

            GUILayout.EndHorizontal();
        }

        private void DrawPatternButtons()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Checker cursor:", GUILayout.Width(130f));
            GUI.enabled = !_running;
            for (var i = 0; i < _patterns.Length; i++)
            {
                if (GUILayout.Button($"{_patternSizes[i]} px", GUILayout.Width(80f)))
                {
                    ShowPattern(i);
                }
            }

            if (GUILayout.Button("Back to cursor set", GUILayout.Width(170f)))
            {
                ShowCursor();
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();

            if (_patternIndex < 0)
            {
                return;
            }

            var pattern = _patterns[_patternIndex];
            GUILayout.BeginHorizontal();
            GUILayout.Label("On-screen copy at 1:1 pixels:", GUILayout.Width(220f));
            var rect = GUILayoutUtility.GetRect(pattern.width, pattern.height, GUILayout.Width(pattern.width),
                GUILayout.Height(pattern.height));
            rect.x = Mathf.Round(rect.x);
            rect.y = Mathf.Round(rect.y);
            GUI.DrawTexture(rect, pattern);
            GUILayout.EndHorizontal();
        }

        private void DrawTargets(Rect area)
        {
            GUI.Box(area, GUIContent.none);
            GUI.Label(new(area.x + 8f, area.y + 4f, area.width - 16f, 24f), _lastClick);
            var columns = Mathf.Max(1, Mathf.FloorToInt((area.width - TargetSpacing) / TargetSpacing));
            var rows = Mathf.Max(1, Mathf.FloorToInt((area.height - TargetSpacing) / TargetSpacing));
            var current = Event.current;
            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    var center = new Vector2(Mathf.Round(area.x + TargetSpacing * (column + 1)),
                        Mathf.Round(area.y + 30f + TargetSpacing * (row + 0.5f)));
                    DrawTarget(center);
                    if (current.type == EventType.MouseDown &&
                        Vector2.Distance(current.mousePosition, center) <= TargetRadius)
                    {
                        RecordClick(current.mousePosition, center);
                        current.Use();
                    }
                }
            }
        }

        private void DrawTarget(Vector2 center)
        {
            var previous = GUI.color;
            GUI.color = new(1f, 1f, 1f, 0.35f);
            GUI.DrawTexture(new(center.x - TargetRadius, center.y, TargetRadius * 2f, 1f), _dot);
            GUI.DrawTexture(new(center.x, center.y - TargetRadius, 1f, TargetRadius * 2f), _dot);
            GUI.color = Color.red;
            GUI.DrawTexture(new(center.x, center.y, 1f, 1f), _dot);
            GUI.color = previous;
        }

        private void RecordClick(Vector2 position, Vector2 center)
        {
            var offset = position - center;
            var state = _patternIndex >= 0 ? $"checker {_patternSizes[_patternIndex]}" : _player.CurrentState.Name;
            _lastClick = $"Last click with {state} @{_player.CursorSize}px: offset {offset.x:0}, {offset.y:0} px from the centre.";
            Debug.Log($"{LogPrefix}Click: {_lastClick}");
        }

        private void ShowPattern(int index)
        {
            _patternIndex = index;
            var pattern = _patterns[index];
            Cursor.SetCursor(pattern, Vector2.zero, CursorMode.Auto);
            Debug.Log($"{LogPrefix}Checker cursor {pattern.width} px: {DescribeEnvironment()}");
        }

        private void ShowCursor()
        {
            if (_patternIndex < 0)
            {
                return;
            }

            _patternIndex = -1;
            _player.Refresh();
        }

        private IEnumerator RunRealistic()
        {
            _running = true;
            ShowCursor();
            _report.Clear();
            Line("Realistic: " + DescribeEnvironment());
            var set = _sets[_setIndex];
            var size = SystemCursorSize.Get();
            MeasureWarm(set, size);
            yield return null;
            yield return MeasurePlayback(set, size);
            FinishRun();
        }

        private IEnumerator RunMatrix()
        {
            _running = true;
            ShowCursor();
            _report.Clear();
            Line("Matrix: " + DescribeEnvironment());
            foreach (var set in _sets)
            {
                for (var sizeIndex = 0; sizeIndex < set.SizeCount; sizeIndex++)
                {
                    MeasureWarm(set, set.GetSize(sizeIndex));
                    yield return null;
                }
            }

            yield return MeasurePlayback(_sets[0], SystemCursorSize.Get());
            FinishRun();
        }

        private void FinishRun()
        {
            _player.SetSet(_sets[_setIndex]);
            _player.SetSystemCursorSize(SystemCursorSize.Get());
            _running = false;
        }

        private void MeasureWarm(CursorSet set, int size)
        {
            CursorPlayer player = new(_output);
            player.SetSystemCursorSize(size);
            player.SetSet(set);

            _output.ResetStats();
            _stopwatch.Restart();
            player.Warm();
            _stopwatch.Stop();
            var firstCount = _output.Count;
            var firstTotal = _stopwatch.Elapsed.TotalMilliseconds;
            var firstAverage = _output.AverageMs;
            var firstMax = _output.MaxMs;

            _output.ResetStats();
            player.Warm();
            Line($"Warm {set.name} @{player.CursorSize}px: {firstCount} textures, first use {Ms(firstTotal)} total, " +
                 $"{Ms(firstAverage)} avg, {Ms(firstMax)} max; again {Ms(_output.AverageMs)} avg, {Ms(_output.MaxMs)} max");
        }

        private IEnumerator MeasurePlayback(CursorSet set, int size)
        {
            _player.SetSet(set);
            _player.SetSystemCursorSize(size);
            var probeBefore = WindowsCursorProbe.Capture();
            _output.ResetStats();

            var frames = 0;
            var tickTotal = 0d;
            var tickMax = 0d;
            var stepTimer = 0f;
            var elapsed = 0f;
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            while (elapsed < _playbackSeconds)
            {
                var delta = Time.unscaledDeltaTime;
                elapsed += delta;
                stepTimer += delta;
                _stopwatch.Restart();
                if (stepTimer >= _stepSeconds)
                {
                    stepTimer = 0f;
                    NextState((frames & 1) == 0);
                }

                _player.Tick(delta);
                _stopwatch.Stop();
                var tick = _stopwatch.Elapsed.TotalMilliseconds;
                tickTotal += tick;
                tickMax = Math.Max(tickMax, tick);
                frames++;
                yield return null;
            }

            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            var probeAfter = WindowsCursorProbe.Capture();
            Line($"Playback {set.name} @{_player.CursorSize}px for {_playbackSeconds:0}s: {frames} frames, " +
                 $"{_output.Count} SetCursor calls, {Ms(_output.AverageMs)} avg, {Ms(_output.MaxMs)} max");
            Line($"Tick incl. SetCursor: {Ms(tickTotal / Math.Max(frames, 1))} avg, {Ms(tickMax)} max; " +
                 $"allocated {allocated} bytes on the main thread (includes the coroutine)");
            Line($"GDI objects {probeBefore.GdiObjects} -> {probeAfter.GdiObjects}, " +
                 $"USER objects {probeBefore.UserObjects} -> {probeAfter.UserObjects}");
        }

        private void NextState(bool immediate)
        {
            var set = _player.Set;
            if (!set || set.StateCount == 0)
            {
                return;
            }

            _stateIndex = (_stateIndex + 1) % set.StateCount;
            _player.SetState(set.GetStateId(_stateIndex), immediate);
        }

        private string DescribeEnvironment()
        {
            var probe = WindowsCursorProbe.Capture();
            var mouse = Input.mousePosition;
            var over = Application.isFocused && mouse.x >= 0f && mouse.y >= 0f && mouse.x < Screen.width &&
                       mouse.y < Screen.height;
            return $"Screen {Screen.width}x{Screen.height} @{Screen.dpi:0} dpi, window DPI {probe.WindowDpi}, " +
                   $"SM_CXCURSOR {probe.SystemMetricWidth}, for DPI {probe.SystemMetricForDpiWidth}, " +
                   $"CursorBaseSize {probe.CursorBaseSize}, SystemCursorSize.Get() {SystemCursorSize.Get()}, " +
                   $"baked size in use {_player?.CursorSize}, pointer over window {(over ? "yes" : "no")}, " +
                   $"OS cursor {probe.ActiveCursorWidth}x{probe.ActiveCursorHeight} " +
                   $"hotspot {probe.ActiveHotspotX},{probe.ActiveHotspotY}";
        }

        private void Line(string text)
        {
            _report.AppendLine(text);
            Debug.Log(LogPrefix + text);
        }

        private static string Ms(double value)
        {
            return value.ToString("0.000", CultureInfo.InvariantCulture) + " ms";
        }
    }
}
