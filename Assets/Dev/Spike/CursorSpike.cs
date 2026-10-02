using System;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Dev.Spike
{
    public sealed class CursorSpike : MonoBehaviour
    {
        [SerializeField] private CursorSpikeData _data;

        private const int TimingWindow = 240;
        private const float ProbeInterval = 0.25f;
        private const float LogInterval = 2f;
        private const float SweepStepDuration = 0.75f;

        private static readonly KeyCode[] _sizeKeys =
            { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4 };

        private static readonly KeyCode[] _sequenceKeys = { KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R };
        private readonly Stopwatch _stopwatch = new();
        private readonly double[] _callMilliseconds = new double[TimingWindow];
        private readonly StringBuilder _report = new(1024);
        private readonly StringBuilder _logLine = new(512);
        private int _callIndex;
        private int _callSamples;
        private int _callsThisSecond;
        private int _callsPerSecond;
        private float _secondTimer;
        private long _allocatedBytes;
        private long _totalCalls;
        private int _sizeIndex;
        private int _sequenceIndex;
        private bool _showGrid;
        private bool _paused;
        private bool _stress;
        private CursorMode _mode = CursorMode.Auto;
        private float _frameTimer;
        private int _frame;
        private int _direction = 1;
        private float _probeTimer;
        private CursorProbeSnapshot _probe;
        private CursorProbeSnapshot _baselineProbe;
        private bool _hasBaseline;
        private Texture2D _white;
        private GUIStyle _style;
        private string _reportText = string.Empty;
        private double _averageMilliseconds;
        private double _maxMilliseconds;
        private float _logTimer;
        private string _pendingLogReason;
        private int _sweepStep = -1;
        private float _sweepTimer;

        private void Awake()
        {
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            _white = Texture2D.whiteTexture;
            _pendingLogReason = "start";
            _sizeIndex = Mathf.Min(1, _data.Sizes.Length - 1);
        }

        private void Start()
        {
            StartSweep();
        }

        private void Update()
        {
            HandleInput();

            var advanced = false;
            if (_stress)
            {
                AdvanceFrame();
                advanced = true;
            }
            else if (!_paused)
            {
                var sequence = CurrentSequence();
                _frameTimer += Time.unscaledDeltaTime;
                while (_frameTimer >= sequence.FrameDuration)
                {
                    _frameTimer -= sequence.FrameDuration;
                    AdvanceFrame();
                    advanced = true;
                }
            }

            if (advanced)
            {
                Apply();
            }

            _secondTimer += Time.unscaledDeltaTime;
            if (_secondTimer >= 1f)
            {
                _secondTimer -= 1f;
                _callsPerSecond = _callsThisSecond;
                _callsThisSecond = 0;
            }

            _probeTimer += Time.unscaledDeltaTime;
            if (_probeTimer >= ProbeInterval)
            {
                _probeTimer = 0f;
                _probe = WindowsCursorProbe.Capture();
                if (!_hasBaseline)
                {
                    _baselineProbe = _probe;
                    _hasBaseline = true;
                }

                ComputeTiming();
                BuildReport();
                if (_pendingLogReason != null)
                {
                    WriteLog(_pendingLogReason);
                    _pendingLogReason = null;
                    _logTimer = 0f;
                }
            }

            UpdateSweep();

            _logTimer += Time.unscaledDeltaTime;
            if (_logTimer >= LogInterval)
            {
                _logTimer = 0f;
                WriteLog("tick");
            }
        }

        private void HandleInput()
        {
            for (var i = 0; i < _sizeKeys.Length && i < _data.Sizes.Length; i++)
            {
                if (Input.GetKeyDown(_sizeKeys[i]))
                {
                    _sizeIndex = i;
                    Apply();
                    _pendingLogReason = "size";
                }
            }

            for (var i = 0; i < _sequenceKeys.Length && i < CurrentSize().Sequences.Length; i++)
            {
                if (Input.GetKeyDown(_sequenceKeys[i]))
                {
                    _sequenceIndex = i;
                    _frame = 0;
                    _direction = 1;
                    _frameTimer = 0f;
                    Apply();
                    _pendingLogReason = "sequence";
                }
            }

            if (Input.GetKeyDown(KeyCode.G))
            {
                _showGrid = !_showGrid;
                Apply();
                _pendingLogReason = "grid";
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                _paused = !_paused;
                _pendingLogReason = "pause";
            }

            if (Input.GetKeyDown(KeyCode.F))
            {
                _stress = !_stress;
                _pendingLogReason = "stress";
            }

            if (Input.GetKeyDown(KeyCode.M))
            {
                _mode = _mode == CursorMode.Auto ? CursorMode.ForceSoftware : CursorMode.Auto;
                Apply();
                _pendingLogReason = "mode";
            }

            if (Input.GetKeyDown(KeyCode.A))
            {
                StartSweep();
            }

            if (Input.GetKeyDown(KeyCode.C))
            {
                ResetStats();
                _pendingLogReason = "reset";
            }
        }

        private void StartSweep()
        {
            _sweepStep = 0;
            _sweepTimer = 0f;
            _sizeIndex = 0;
            _showGrid = true;
            _mode = CursorMode.Auto;
            Apply();
        }

        private void UpdateSweep()
        {
            if (_sweepStep < 0)
            {
                return;
            }

            _sweepTimer += Time.unscaledDeltaTime;
            if (_sweepTimer < SweepStepDuration)
            {
                return;
            }

            _sweepTimer = 0f;
            _probe = WindowsCursorProbe.Capture();
            WriteLog("sweep");
            _sweepStep++;
            if (_sweepStep >= _data.Sizes.Length)
            {
                _sweepStep = -1;
                return;
            }

            _sizeIndex = _sweepStep;
            Apply();
        }

        private void AdvanceFrame()
        {
            var sequence = CurrentSequence();
            var count = sequence.Frames.Length;
            if (count <= 1)
            {
                _frame = 0;
                return;
            }

            if (!sequence.PingPong)
            {
                _frame = (_frame + 1) % count;
                return;
            }

            _frame += _direction;
            if (_frame >= count - 1)
            {
                _frame = count - 1;
                _direction = -1;
            }
            else if (_frame <= 0)
            {
                _frame = 0;
                _direction = 1;
            }
        }

        private void Apply()
        {
            var size = CurrentSize();
            var sequence = CurrentSequence();
            _frame = Mathf.Clamp(_frame, 0, sequence.Frames.Length - 1);
            var texture = _showGrid ? size.Grid : sequence.Frames[_frame];

            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            _stopwatch.Restart();
            Cursor.SetCursor(texture, size.Hotspot, _mode);
            _stopwatch.Stop();
            _allocatedBytes += GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

            _callMilliseconds[_callIndex] = _stopwatch.Elapsed.TotalMilliseconds;
            _callIndex = (_callIndex + 1) % TimingWindow;
            _callSamples = Mathf.Min(_callSamples + 1, TimingWindow);
            _callsThisSecond++;
            _totalCalls++;
        }

        private void ResetStats()
        {
            _callSamples = 0;
            _callIndex = 0;
            _allocatedBytes = 0;
            _totalCalls = 0;
            _hasBaseline = false;
        }

        private SpikeSize CurrentSize()
        {
            return _data.Sizes[_sizeIndex];
        }

        private SpikeSequence CurrentSequence()
        {
            var sequences = CurrentSize().Sequences;
            return sequences[Mathf.Clamp(_sequenceIndex, 0, sequences.Length - 1)];
        }

        private void ComputeTiming()
        {
            double average = 0;
            double max = 0;
            for (var i = 0; i < _callSamples; i++)
            {
                average += _callMilliseconds[i];
                max = Math.Max(max, _callMilliseconds[i]);
            }

            if (_callSamples > 0)
            {
                average /= _callSamples;
            }

            _averageMilliseconds = average;
            _maxMilliseconds = max;
        }

        private void WriteLog(string reason)
        {
            var size = CurrentSize();
            _logLine.Clear();
            _logLine.Append("[LiveCursorSpike] reason=").Append(reason)
                .Append(" texture=").Append(_showGrid ? "Grid" : CurrentSequence().Name.Replace(' ', '_'))
                .Append(" size=").Append(size.Size)
                .Append(" hotspot=").Append(size.Hotspot.x).Append(',').Append(size.Hotspot.y)
                .Append(" mode=").Append(_mode)
                .Append(" paused=").Append(_paused)
                .Append(" stress=").Append(_stress)
                .Append(" screen=").Append(Screen.width).Append('x').Append(Screen.height)
                .Append(" screenDpi=").Append(Screen.dpi)
                .Append(" windowDpi=").Append(_probe.WindowDpi)
                .Append(" smCxCursor=").Append(_probe.SystemMetricWidth)
                .Append(" smCxCursorForDpi=").Append(_probe.SystemMetricForDpiWidth)
                .Append(" cursorBaseSize=").Append(_probe.CursorBaseSize)
                .Append(" activeCursor=").Append(_probe.ActiveCursorWidth).Append('x').Append(_probe.ActiveCursorHeight)
                .Append(" activeHotspot=").Append(_probe.ActiveHotspotX).Append(',').Append(_probe.ActiveHotspotY)
                .Append(" setCursorAvgMs=").Append(_averageMilliseconds.ToString("0.0000"))
                .Append(" setCursorMaxMs=").Append(_maxMilliseconds.ToString("0.0000"))
                .Append(" callsPerSecond=").Append(_callsPerSecond)
                .Append(" totalCalls=").Append(_totalCalls)
                .Append(" allocBytes=").Append(_allocatedBytes)
                .Append(" gdi=").Append(_probe.GdiObjects).Append('/').Append(_baselineProbe.GdiObjects)
                .Append(" user=").Append(_probe.UserObjects).Append('/').Append(_baselineProbe.UserObjects);
            Debug.Log(_logLine.ToString());
        }

        private void BuildReport()
        {
            var average = _averageMilliseconds;
            var max = _maxMilliseconds;
            var size = CurrentSize();
            _report.Clear();
            _report.Append("LIVE CURSOR - WINDOWS SPIKE\n\n");
            _report.Append("Texture: ").Append(_showGrid ? "Grid" : CurrentSequence().Name).Append(" @ ")
                .Append(size.Size).Append(" px, hotspot ").Append(size.Hotspot.x).Append(',').Append(size.Hotspot.y)
                .Append('\n');
            _report.Append("Mode: ").Append(_mode).Append(_paused ? "  [paused]" : string.Empty)
                .Append(_stress ? "  [STRESS: every frame]" : string.Empty).Append('\n');
            _report.Append("Frame: ").Append(_frame).Append('\n');
            _report.Append('\n');
            _report.Append("Screen: ").Append(Screen.width).Append('x').Append(Screen.height).Append("  Screen.dpi: ")
                .Append(Screen.dpi).Append('\n');
            _report.Append("Window DPI: ").Append(_probe.WindowDpi).Append(" (scale ")
                .Append((_probe.WindowDpi / 96f * 100f).ToString("0")).Append("%)\n");
            _report.Append("SM_CXCURSOR: ").Append(_probe.SystemMetricWidth).Append("   ForDpi: ")
                .Append(_probe.SystemMetricForDpiWidth).Append('\n');
            _report.Append("CursorBaseSize (registry): ").Append(_probe.CursorBaseSize).Append('\n');
            _report.Append("ACTIVE OS CURSOR: ").Append(_probe.ActiveCursorWidth).Append('x')
                .Append(_probe.ActiveCursorHeight).Append("  hotspot ").Append(_probe.ActiveHotspotX).Append(',')
                .Append(_probe.ActiveHotspotY).Append('\n');
            _report.Append('\n');
            _report.Append("SetCursor avg: ").Append(average.ToString("0.000")).Append(" ms   max: ")
                .Append(max.ToString("0.000")).Append(" ms\n");
            _report.Append("Calls/s: ").Append(_callsPerSecond).Append("   total: ").Append(_totalCalls).Append('\n');
            _report.Append("Managed alloc in SetCursor: ").Append(_allocatedBytes).Append(" B total\n");
            _report.Append("GDI objects: ").Append(_probe.GdiObjects).Append(" (start ")
                .Append(_baselineProbe.GdiObjects).Append(")   USER objects: ").Append(_probe.UserObjects)
                .Append(" (start ").Append(_baselineProbe.UserObjects).Append(")\n");
            _report.Append('\n');
            _report.Append("1-4 size   Q/W/E/R sequence   G grid   Space pause\n");
            _report.Append("F stress   M Auto/Software   C reset stats   A size sweep\n");
            _report.Append("Hover the outlined boxes to compare on-screen size.\n");
            _report.Append("Red crosshair = where Unity thinks the mouse is.");
            _reportText = _report.ToString();
        }

        private void OnGUI()
        {
            _style ??= new(GUI.skin.label)
            {
                fontSize = 16,
                normal =
                {
                    textColor = Color.white
                }
            };

            GUI.color = new(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(new(10f, 10f, 560f, 470f), _white);
            GUI.color = Color.white;
            GUI.Label(new(20f, 18f, 545f, 460f), _reportText, _style);

            var x = 620f;
            var y = 40f;
            for (var i = 0; i < _data.Sizes.Length; i++)
            {
                float side = _data.Sizes[i].Size;
                DrawOutline(new(x, y, side, side), i == _sizeIndex ? Color.yellow : Color.gray);
                GUI.Label(new(x, y + side + 4f, 120f, 24f), _data.Sizes[i].Size + " px", _style);
                x += side + 40f;
            }

            var mouse = Input.mousePosition;
            var mouseX = mouse.x;
            var mouseY = Screen.height - mouse.y;
            GUI.color = Color.red;
            GUI.DrawTexture(new(mouseX - 12f, mouseY, 8f, 1f), _white);
            GUI.DrawTexture(new(mouseX + 5f, mouseY, 8f, 1f), _white);
            GUI.DrawTexture(new(mouseX, mouseY - 12f, 1f, 8f), _white);
            GUI.DrawTexture(new(mouseX, mouseY + 5f, 1f, 8f), _white);
            GUI.color = Color.white;
        }

        private void DrawOutline(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(new(rect.x, rect.y, rect.width, 1f), _white);
            GUI.DrawTexture(new(rect.x, rect.yMax - 1f, rect.width, 1f), _white);
            GUI.DrawTexture(new(rect.x, rect.y, 1f, rect.height), _white);
            GUI.DrawTexture(new(rect.xMax - 1f, rect.y, 1f, rect.height), _white);
            GUI.color = Color.white;
        }
    }
}