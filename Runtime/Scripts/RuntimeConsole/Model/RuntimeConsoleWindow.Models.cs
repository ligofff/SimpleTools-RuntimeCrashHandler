using System;
using UnityEngine;

namespace Ligofff.RuntimeExceptionsHandler.RuntimeConsole
{
    public partial class RuntimeConsoleWindow
    {
        protected readonly struct PendingLog
        {
            public readonly string Condition;
            public readonly string Stacktrace;
            public readonly LogType Type;

            public PendingLog(string condition, string stacktrace, LogType type)
            {
                Condition = condition;
                Stacktrace = stacktrace;
                Type = type;
            }
        }

        protected readonly struct BottomActionButton
        {
            public readonly string Id;
            public readonly string Label;
            public readonly Action Callback;

            public BottomActionButton(string id, string label, Action callback)
            {
                Id = id;
                Label = label;
                Callback = callback;
            }
        }

        protected readonly struct VirtualizedRow
        {
            public readonly int EntryIndex;
            public readonly int RowIndex;
            public readonly float Top;
            public readonly float Height;

            public VirtualizedRow(int entryIndex, int rowIndex, float top, float height)
            {
                EntryIndex = entryIndex;
                RowIndex = rowIndex;
                Top = top;
                Height = height;
            }
        }

        public struct LogEntry
        {
            public readonly int Id;
            public readonly string Condition;
            public readonly string Stacktrace;
            public readonly LogType Type;
            public readonly string DisplayLine;
            public int Count;

            public LogEntry(int id, string condition, string stacktrace, LogType type, string displayLine)
            {
                Id = id;
                Condition = condition ?? string.Empty;
                Stacktrace = NormalizeLineBreaks(stacktrace ?? string.Empty);
                Type = type;
                Count = 1;
                DisplayLine = NormalizeLineBreaks(displayLine ?? string.Empty);
            }
        }

        protected struct ButtonVisualState
        {
            public float HoverAmount;
            public float ClickPulse;
            public int LastSeenFrame;
        }
    }
}
