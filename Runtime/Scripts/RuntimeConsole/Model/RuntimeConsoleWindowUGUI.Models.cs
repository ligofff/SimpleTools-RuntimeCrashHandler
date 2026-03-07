using System;
using UnityEngine;

namespace Ligofff.RuntimeExceptionsHandler.RuntimeConsole
{
    public partial class RuntimeConsoleWindowUGUI
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

        protected readonly struct RowLayout
        {
            public readonly int EntryIndex;
            public readonly int RowIndex;
            public readonly float Top;
            public readonly float Height;
            public readonly bool Expanded;

            public RowLayout(int entryIndex, int rowIndex, float top, float height, bool expanded)
            {
                EntryIndex = entryIndex;
                RowIndex = rowIndex;
                Top = top;
                Height = height;
                Expanded = expanded;
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
                Stacktrace = stacktrace ?? string.Empty;
                Type = type;
                DisplayLine = displayLine ?? string.Empty;
                Count = 1;
            }
        }
    }
}
