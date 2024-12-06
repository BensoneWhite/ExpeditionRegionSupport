﻿using BepInEx.Logging;

namespace LogUtils.CompatibilityServices
{
    public interface IExtendedLogSource : ILogSource
    {
        public void Log(LogLevel level, object data);

        public void LogFatal(object data);

        public void LogError(object data);

        public void LogWarning(object data);

        public void LogMessage(object data);

        public void LogInfo(object data);

        public void LogDebug(object data);
    }
}
