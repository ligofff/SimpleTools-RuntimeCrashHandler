using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Ligofff.RuntimeExceptionsHandler.RuntimeConsole
{
    public static class RuntimeConsoleCopyHeaderData
    {
        private const string UnknownValue = "unknown";
        private static readonly object SyncRoot = new object();
        private static readonly List<CustomProvider> CustomProviders = new List<CustomProvider>(8);

        public static void SetDataProvider(string label, Func<string> valueGetter)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                throw new ArgumentException("Data label cannot be null or whitespace.", nameof(label));
            }

            if (valueGetter == null)
            {
                throw new ArgumentNullException(nameof(valueGetter));
            }

            var normalizedLabel = label.Trim();
            lock (SyncRoot)
            {
                for (var i = 0; i < CustomProviders.Count; i++)
                {
                    if (!string.Equals(CustomProviders[i].Label, normalizedLabel, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    CustomProviders[i] = new CustomProvider(normalizedLabel, valueGetter);
                    return;
                }

                CustomProviders.Add(new CustomProvider(normalizedLabel, valueGetter));
            }
        }

        public static void SetDataValue(string label, string value)
        {
            var capturedValue = value ?? string.Empty;
            SetDataProvider(label, () => capturedValue);
        }

        public static bool RemoveDataProvider(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return false;
            }

            var normalizedLabel = label.Trim();
            lock (SyncRoot)
            {
                for (var i = 0; i < CustomProviders.Count; i++)
                {
                    if (!string.Equals(CustomProviders[i].Label, normalizedLabel, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    CustomProviders.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        public static void ClearDataProviders()
        {
            lock (SyncRoot)
            {
                CustomProviders.Clear();
            }
        }

        internal static void AppendHeader(StringBuilder builder, string reportKind)
        {
            if (builder == null)
            {
                return;
            }

            builder.Append("=== Runtime Console Report");
            if (!string.IsNullOrWhiteSpace(reportKind))
            {
                builder.Append(" (");
                builder.Append(reportKind.Trim());
                builder.Append(')');
            }

            builder.AppendLine(" ===");
            AppendLine(builder, "Current time", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"));
            AppendLine(builder, "Build version", SafeValue(Application.version));
            AppendLine(builder, "Unity version", SafeValue(Application.unityVersion));
            AppendLine(builder, "Product", SafeValue(Application.productName));
            AppendLine(builder, "Platform", Application.platform.ToString());
            AppendLine(builder, "Hardware", BuildHardwareSummary());
            AppendCustomData(builder);
            builder.AppendLine("==============================================");
            builder.AppendLine();
        }

        private static void AppendCustomData(StringBuilder builder)
        {
            CustomProvider[] providers;
            lock (SyncRoot)
            {
                providers = CustomProviders.ToArray();
            }

            for (var i = 0; i < providers.Length; i++)
            {
                var provider = providers[i];
                string value;

                try
                {
                    value = provider.ValueGetter.Invoke();
                }
                catch (Exception ex)
                {
                    value = $"<provider failed: {ex.GetType().Name}>";
                }

                AppendLine(builder, provider.Label, value);
            }
        }

        private static string BuildHardwareSummary()
        {
            var builder = new StringBuilder(160);
            builder.Append(SafeValue(SystemInfo.deviceModel));
            builder.Append("; ");
            builder.Append(SafeValue(SystemInfo.operatingSystem));
            builder.Append("; CPU: ");
            builder.Append(SafeValue(SystemInfo.processorType));
            builder.Append(" (");
            builder.Append(Mathf.Max(1, SystemInfo.processorCount));
            builder.Append(" cores)");
            builder.Append("; RAM: ");
            builder.Append(Mathf.Max(0, SystemInfo.systemMemorySize));
            builder.Append(" MB");
            builder.Append("; GPU: ");
            builder.Append(SafeValue(SystemInfo.graphicsDeviceName));
            return builder.ToString();
        }

        private static void AppendLine(StringBuilder builder, string key, string value)
        {
            builder.Append(key);
            builder.Append(": ");
            builder.AppendLine(SafeValue(value));
        }

        private static string SafeValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? UnknownValue : value.Trim();
        }

        private readonly struct CustomProvider
        {
            public readonly string Label;
            public readonly Func<string> ValueGetter;

            public CustomProvider(string label, Func<string> valueGetter)
            {
                Label = label;
                ValueGetter = valueGetter;
            }
        }
    }
}
