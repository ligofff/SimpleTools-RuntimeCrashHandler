using System;
using System.Collections;
using UnityEngine;

namespace Ligofff.RuntimeExceptionsHandler.RuntimeConsole
{
    [DisallowMultipleComponent]
    public class TestLogGenerator : MonoBehaviour
    {
        private enum SimulatedLogKind
        {
            Info,
            Warning,
            Error,
            Exception
        }

        [Header("Run mode")]
        [SerializeField]
        private bool runOnEnable = true;

        [SerializeField]
        [Min(0.05f)]
        private float intervalSeconds = 1f;

        [SerializeField]
        private bool useUnscaledTime = true;

        [SerializeField]
        private int randomSeed;

        [Header("Emission")]
        [SerializeField]
        private bool includeInfo = true;

        [SerializeField]
        private bool includeWarnings = true;

        [SerializeField]
        private bool includeErrors = true;

        [SerializeField]
        private bool includeExceptions = true;

        [SerializeField]
        [Range(1, 12)]
        private int minStackDepth = 2;

        [SerializeField]
        [Range(1, 16)]
        private int maxStackDepth = 6;

        [SerializeField]
        private bool attachContextObject = true;

        private readonly string[] _systems =
        {
            "Inventory",
            "SaveGame",
            "QuestTracker",
            "CombatAI",
            "UIFlow",
            "InputRouter",
            "NetSync",
            "Addressables"
        };

        private readonly string[] _operations =
        {
            "LoadState",
            "BuildWidget",
            "ResolveDependency",
            "ValidatePayload",
            "Initialize",
            "Deserialize",
            "ApplyDelta",
            "Commit"
        };

        private readonly string[] _errorReasons =
        {
            "object reference was null",
            "resource handle is invalid",
            "payload checksum mismatch",
            "collection index out of range",
            "cached value expired",
            "configuration entry missing"
        };

        private System.Random _random;
        private Coroutine _emitCoroutine;
        private int _emitCounter;
        private readonly SimulatedLogKind[] _kindPool = new SimulatedLogKind[4];

        private void OnEnable()
        {
            _random = randomSeed == 0 ? new System.Random() : new System.Random(randomSeed);

            if (runOnEnable)
            {
                StartGenerating();
            }
        }

        private void OnDisable()
        {
            StopGenerating();
        }

        [ContextMenu("Start Generating")]
        public void StartGenerating()
        {
            if (_emitCoroutine != null)
            {
                return;
            }

            _emitCoroutine = StartCoroutine(GenerateLoop());
        }

        [ContextMenu("Stop Generating")]
        public void StopGenerating()
        {
            if (_emitCoroutine == null)
            {
                return;
            }

            StopCoroutine(_emitCoroutine);
            _emitCoroutine = null;
        }

        [ContextMenu("Emit Once")]
        public void EmitOnce()
        {
            var kind = PickKind();
            var route = _random.Next(0, 3);
            var depth = Mathf.Max(minStackDepth, 1);
            depth = _random.Next(depth, Mathf.Max(depth + 1, maxStackDepth + 1));

            if (kind == SimulatedLogKind.Exception)
            {
                EmitException(depth, route);
                return;
            }

            EmitCommonLog(kind, depth, route);
        }

        private IEnumerator GenerateLoop()
        {
            while (true)
            {
                EmitOnce();
                yield return useUnscaledTime
                    ? new WaitForSecondsRealtime(intervalSeconds)
                    : new WaitForSeconds(intervalSeconds);
            }
        }

        private SimulatedLogKind PickKind()
        {
            var count = 0;

            if (includeInfo)
            {
                _kindPool[count++] = SimulatedLogKind.Info;
            }

            if (includeWarnings)
            {
                _kindPool[count++] = SimulatedLogKind.Warning;
            }

            if (includeErrors)
            {
                _kindPool[count++] = SimulatedLogKind.Error;
            }

            if (includeExceptions)
            {
                _kindPool[count++] = SimulatedLogKind.Exception;
            }

            if (count == 0)
            {
                return SimulatedLogKind.Info;
            }

            return _kindPool[_random.Next(0, count)];
        }

        private void EmitCommonLog(SimulatedLogKind kind, int depth, int route)
        {
            switch (route)
            {
                case 0:
                    RouteA(kind, depth);
                    break;
                case 1:
                    RouteB(kind, depth);
                    break;
                default:
                    RouteC(kind, depth);
                    break;
            }
        }

        private void EmitException(int depth, int route)
        {
            try
            {
                switch (route)
                {
                    case 0:
                        ThrowRouteA(depth);
                        break;
                    case 1:
                        ThrowRouteB(depth);
                        break;
                    default:
                        ThrowRouteC(depth);
                        break;
                }
            }
            catch (Exception ex)
            {
                if (attachContextObject)
                {
                    Debug.LogException(ex, this);
                }
                else
                {
                    Debug.LogException(ex);
                }
            }
        }

        private void RouteA(SimulatedLogKind kind, int depth)
        {
            if (depth <= 0)
            {
                WriteCommonLog(kind);
                return;
            }

            RouteA(kind, depth - 1);
        }

        private void RouteB(SimulatedLogKind kind, int depth)
        {
            if (depth <= 0)
            {
                WriteCommonLog(kind);
                return;
            }

            RouteB_Sub(kind, depth - 1);
        }

        private void RouteB_Sub(SimulatedLogKind kind, int depth)
        {
            if (depth % 2 == 0)
            {
                RouteA(kind, depth - 1);
            }
            else
            {
                RouteC(kind, depth - 1);
            }
        }

        private void RouteC(SimulatedLogKind kind, int depth)
        {
            if (depth <= 0)
            {
                WriteCommonLog(kind);
                return;
            }

            RouteC_Sub(kind, depth - 1);
        }

        private void RouteC_Sub(SimulatedLogKind kind, int depth)
        {
            RouteB(kind, depth - 1);
        }

        private void ThrowRouteA(int depth)
        {
            if (depth <= 0)
            {
                throw BuildException("A");
            }

            ThrowRouteA(depth - 1);
        }

        private void ThrowRouteB(int depth)
        {
            if (depth <= 0)
            {
                throw BuildException("B");
            }

            ThrowRouteB_Sub(depth - 1);
        }

        private void ThrowRouteB_Sub(int depth)
        {
            if (depth <= 0)
            {
                throw BuildException("B-sub");
            }

            ThrowRouteC(depth - 1);
        }

        private void ThrowRouteC(int depth)
        {
            if (depth <= 0)
            {
                throw BuildException("C");
            }

            ThrowRouteA(depth - 1);
        }

        private Exception BuildException(string routeId)
        {
            var message = $"{BuildHeader()} Simulated exception in route {routeId}: {PickRandom(_errorReasons)}.";
            var kind = _random.Next(0, 3);

            switch (kind)
            {
                case 0:
                    return new NullReferenceException(message);
                case 1:
                    return new InvalidOperationException(message);
                default:
                    return new ArgumentOutOfRangeException(nameof(routeId), routeId, message);
            }
        }

        private void WriteCommonLog(SimulatedLogKind kind)
        {
            var message = $"{BuildHeader()} {PickRandom(_operations)} -> {PickRandom(_errorReasons)}.";
            var context = attachContextObject ? this : null;

            switch (kind)
            {
                case SimulatedLogKind.Warning:
                    Debug.LogWarning(message, context);
                    break;
                case SimulatedLogKind.Error:
                    Debug.LogError(message, context);
                    break;
                default:
                    Debug.Log(message, context);
                    break;
            }
        }

        private string BuildHeader()
        {
            _emitCounter++;
            return $"[TestLog {_emitCounter:0000}] {PickRandom(_systems)}";
        }

        private string PickRandom(string[] values)
        {
            return values[_random.Next(0, values.Length)];
        }
    }
}
