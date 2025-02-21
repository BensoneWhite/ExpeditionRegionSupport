using BepInEx.Logging;
using LogUtils.Enums;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using LogUtils.Helpers.Console;

namespace LogUtils.CompatibilityServices
{
    public class BepInExConsoleLogListener : ILogListener, IDisposable
    {
        private readonly TextWriter _consoleStream;

        private bool MatchConsoleManager(Type type) => type.Namespace == "BepInEx" && type.Name == "ConsoleManager";

        public BepInExConsoleLogListener()
        {
            // Locate the ConsoleManager type from all loaded assemblies.
            var consoleManagerType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .FirstOrDefault(MatchConsoleManager) ??
                throw new Exception("ConsoleManager type not found in loaded assemblies.");

            // Retrieve the static ConsoleStream property.
            var propertyInfo = consoleManagerType.GetProperty(
                "ConsoleStream",
                BindingFlags.Static | BindingFlags.Public) ??
                throw new Exception("ConsoleStream property not found on ConsoleManager type.");

            _consoleStream = propertyInfo.GetValue(null) as TextWriter;
            if (_consoleStream == null)
                throw new Exception("ConsoleStream is null.");
        }

        /// <summary>
        /// Called when a log event occurs. Converts the LogLevel to a LogCategory,
        /// builds an ANSI escape code string from its Unity color, and writes the log message.
        /// </summary>
        public void LogEvent(object sender, LogEventArgs eventArgs)
        {
            LogCategory category = LogCategory.ToCategory(eventArgs.Level);

            // Convert the category's Unity color to an ANSI escape code.
            string ansiForeground = AnsiColorConverter.AnsiToForeground(category._ConsoleColor);

            // Build the final log line with the ANSI code prepended and a reset at the end.
            string logLine = ansiForeground + eventArgs.ToStringLine() + AnsiColorConverter.AnsiReset;

            // Write the log line to the console.
            _consoleStream?.Write(logLine);
        }

        /// <summary>
        /// Cleanup any resources if necessary.
        /// </summary>
        public void Dispose()
        {
            // Nothing to dispose in this implementation.
        }
    }
}