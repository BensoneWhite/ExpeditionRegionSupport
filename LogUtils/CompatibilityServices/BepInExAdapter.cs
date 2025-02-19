using BepInEx.Logging;
using LogUtils.Helpers.Console;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

namespace LogUtils.CompatibilityServices
{
    /// <summary>
    /// Adapter service for converting the BepInEx logging system to the system that LogUtils operates
    /// </summary>
    internal static class BepInExAdapter
    {
        private static BepInExDiskLogListener _listener;

        private static BepInExConsoleLogListener _consoleListener;

        public static Logger ManagedLog;

        public static void DebugFatal(object ex) => ManagedLog.LogFatal(ex);

        public static void Run()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                ConsoleVirtualizationHelper.EnableVirtualTerminalProcessing();

            _listener = new BepInExDiskLogListener(new TimedLogWriter());

            //_consoleListener = new BepInExConsoleLogListener();

            AdaptLoggingSystem();
            TransferData();
        }

        /// <summary>
        /// Transitions the BepInEx logging system, and data over to the system operated by LogUtils
        /// </summary>
        internal static void AdaptLoggingSystem()
        {
            ICollection<ILogListener> listeners = GetListeners();

            //Find the LogListener that writes to the BepInEx root directory
            ILogListener found = listeners.FirstOrDefault(l => l is DiskLogListener);

            //This listener is incompatible with LogUtils, and must be replaced
            if (found != null)
            {
                found.Dispose(); //This will flush any messages held by the original listener
                listeners.Remove(found);
            }

            listeners.Add(_listener);

            //Probably this shouln't be a thing
            //ICollection<ILogListener> consoleListeners = GetListeners();

            //ILogListener consoleFound = consoleListeners.FirstOrDefault(l => l is ConsoleLogListener);

            //if(consoleFound != null)
            //{
            //    consoleFound.Dispose();
            //    consoleListeners.Remove(consoleFound);
            //}

            //consoleListeners.Add(_consoleListener);
        }

        /// <summary>
        /// Migrates existing log file over to new file when necessary
        /// </summary>
        internal static void TransferData()
        {
            /*
            LogProperties logProperties = LogID.BepInEx.Properties;

            //This code wont support changes to only the file extension
            bool hasDefaultPath = PathUtils.PathsAreEqual(logProperties.CurrentFolderPath, logProperties.OriginalFolderPath);
            bool hasDefaultFileName = ComparerUtils.FilenameComparer.Equals(logProperties.CurrentFilename, UtilityConsts.LogNames.BepInEx);

            bool fileMoveRequired = !hasDefaultPath || !hasDefaultFileName;

            if (fileMoveRequired)
            {
                string originalLogPath = Path.Combine(logProperties.OriginalFolderPath, UtilityConsts.LogNames.BepInEx + FileExt.LOG);

                //Due to BepInEx log file already existing by the time this assembly is loaded
                LogFile.Move(Path.Combine(originalLogPath, LogID.BepInEx.Properties.CurrentFilePath), logProperties.CurrentFilePath);
            }
            */
        }

        internal static ICollection<ILogListener> GetListeners()
        {
            return BepInEx.Logging.Logger.Listeners;
        }
    }
}
