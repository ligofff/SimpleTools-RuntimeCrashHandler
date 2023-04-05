using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ligofff.RuntimeExceptionsHandler
{
    public class RuntimeExceptionHandler : MonoBehaviour
    {
        [SerializeField]
        private List<string> ignoredLogMessages;

        [SerializeField]
        private ExceptionWindow window;

        [SerializeField]
        private bool handleErrorMessagesAsNonCritical;
        
        [SerializeField]
        private bool timeScaleStopOnExceptionWindowOpen = true;

        [SerializeField]
        private bool disableCrashHandlingAfterFirstCrash = true;

        [SerializeField]
        private ErrorNotification notificationPrefab;

        [SerializeField]
        private Transform notificationsParent;
        
        private bool _isGameCrashedAlready;
        private float _timeScaleBeforeCrash;

        protected virtual string ExceptionPrefix =>
            $"<color=#ebdd89>Hey! Sorry, but game is broken a little :C\nPlay time: {Time.time}s\n</color>";

        private void Awake()
        {
            Application.logMessageReceivedThreaded += OnLogMessageReceived;
            window.Hide();
        }

        private void OnDestroy()
        {
            Application.logMessageReceivedThreaded -= OnLogMessageReceived;
        }

        public virtual void CancelException()
        {
            if (timeScaleStopOnExceptionWindowOpen)
                Time.timeScale = _timeScaleBeforeCrash;
            
            window.Hide();
        }

        protected virtual void OnGameError(string condition, string stacktrace)
        {
            var notification = Instantiate(notificationPrefab, notificationsParent);
            notification.Setup("Game error!", condition, stacktrace,  () => OnErrorNotificationButtonClick(notification, condition, stacktrace));
        }

        protected virtual void OnGameCrash(string condition, string stacktrace)
        {
            OpenExceptionWindow(condition, stacktrace);
            
            _isGameCrashedAlready = true;
        }

        protected virtual void OnErrorNotificationButtonClick(ErrorNotification notification, string condition, string stacktrace)
        {
            OpenExceptionWindow(condition, stacktrace);
            notification.Close();
        }

        protected virtual void OpenExceptionWindow(string condition, string stacktrace)
        {
            if (timeScaleStopOnExceptionWindowOpen)
            {
                _timeScaleBeforeCrash = Time.timeScale;
                Time.timeScale = 0f;
            }
            
            var exceptionText = $"{ExceptionPrefix}\n<color=#bf45eb>{condition}</color>\n{StackTraceToRichText(stacktrace)}";

            window.Setup("Game error!", exceptionText, new (string, Action)[]
            {
                ("Cancel", CancelException),
                ("Close game", Application.Quit)
            });
            
            window.Show();
        }

        private void OnLogMessageReceived(string condition, string stacktrace, LogType type)
        {                
            if (!Application.isPlaying) return;
            if (IsIgnoredLogMessage(stacktrace)) return;

            if (type == LogType.Error && handleErrorMessagesAsNonCritical)
            {
                OnGameError(condition, stacktrace);
                return;
            }

            if (type == LogType.Exception || (type == LogType.Error && !handleErrorMessagesAsNonCritical))
            { 
                if (_isGameCrashedAlready && disableCrashHandlingAfterFirstCrash) return;
                
                OnGameCrash(condition, stacktrace);
            }

        }
        
        protected virtual bool IsIgnoredLogMessage(string stacktrace)
        {
            return ignoredLogMessages.Any(stacktrace.Contains);
        }

        private string StackTraceToRichText(string stacktrace)
        {
            var threeEntryColor = "#e8c0c0";

            var lines = stacktrace.Split(new char[]{ '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            var outString = "";

            try
            {
                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];

                    var startOfFirsyEntry = 0;
                    var endOfFirstEntry = line.IndexOf('(') - 1;
                    var startOfTwoEntry = line.IndexOf('(', endOfFirstEntry);
                    var endOfTwoEntry = line.IndexOf(')', startOfTwoEntry);
                    var startOfThreeEntry = line.IndexOf('(', endOfTwoEntry);
                    var endOfThreeEntry = line.IndexOf(')', startOfThreeEntry);

                    var newLine =
                        $"<size=10>{i}: {line.Substring(startOfFirsyEntry, endOfFirstEntry - startOfFirsyEntry)} " +
                        $"<i><u>{line.Substring(startOfTwoEntry, endOfTwoEntry - startOfTwoEntry)})</i></u> " +
                        $"<color={threeEntryColor}>{line.Substring(startOfThreeEntry, line.Length - startOfThreeEntry)}</color></size>";
                    outString += newLine;
                    outString += "\n";
                }
            }
            catch (Exception)
            {
                return stacktrace;
            }

            return outString;
        }
    }
}