// Game Studio Kit (MIT). Counts console errors, exceptions and warnings on any thread while a QA run is active.
// Any exception or error fails a QA run (WoO rule: zero console errors in the gold path and smoke).
using System;
using System.Threading;
using UnityEngine;

namespace GameStudioKit.Qa
{
    public sealed class ConsoleCounter : IDisposable
    {
        int _errors;
        int _exceptions;
        int _warnings;
        string _firstException;
        bool _listening;
        readonly object _gate = new object();

        public void Start()
        {
            if (_listening) return;
            Application.logMessageReceivedThreaded += OnLog;
            _listening = true;
        }

        public void Stop()
        {
            if (!_listening) return;
            Application.logMessageReceivedThreaded -= OnLog;
            _listening = false;
        }

        public void Dispose() => Stop();

        public void Reset()
        {
            lock (_gate)
            {
                _errors = 0;
                _exceptions = 0;
                _warnings = 0;
                _firstException = null;
            }
        }

        public int Errors => Volatile.Read(ref _errors);
        public int Exceptions => Volatile.Read(ref _exceptions);

        void OnLog(string condition, string stackTrace, LogType type)
        {
            switch (type)
            {
                case LogType.Exception:
                    Interlocked.Increment(ref _exceptions);
                    lock (_gate)
                    {
                        if (_firstException == null) _firstException = Describe(condition, stackTrace);
                    }
                    break;
                case LogType.Error:
                case LogType.Assert:
                    Interlocked.Increment(ref _errors);
                    break;
                case LogType.Warning:
                    Interlocked.Increment(ref _warnings);
                    break;
            }
        }

        static string Describe(string condition, string stackTrace)
        {
            string first = condition ?? "exception";
            if (!string.IsNullOrEmpty(stackTrace))
            {
                int nl = stackTrace.IndexOf('\n');
                string top = (nl >= 0 ? stackTrace.Substring(0, nl) : stackTrace).Trim();
                if (top.Length > 0) first += " @ " + top;
            }
            return first.Length > 500 ? first.Substring(0, 500) : first;
        }

        public QaConsoleSummary Snapshot()
        {
            lock (_gate)
            {
                return new QaConsoleSummary
                {
                    Errors = Volatile.Read(ref _errors),
                    Exceptions = Volatile.Read(ref _exceptions),
                    Warnings = Volatile.Read(ref _warnings),
                    FirstException = _firstException,
                };
            }
        }
    }
}
