﻿using BepInEx.Logging;
using LogUtils.Enums;
using LogUtils.Events;
using LogUtils.Helpers;
using LogUtils.Properties;
using Menu;
using RWCustom;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LogUtils
{
    public static class UtilityCore
    {
        public static Version AssemblyVersion = new Version(0, 8, 5);

        /// <summary>
        /// The assembly responsible for loading core resources for the utility
        /// </summary>
        public static bool IsControllingAssembly { get; private set; }

        /// <summary>
        /// The initialized state for the assembly. This does NOT indicate that another version of the assembly has initialized,
        /// and every assembly must go through the init process
        /// </summary>
        public static bool IsInitialized { get; private set; }

        /// <summary>
        /// The initialization process is in progress for the current assembly
        /// </summary>
        private static bool initializingInProgress;

        /// <summary>
        /// The ILogListener managed by the LogManager plugin. Null when LogManager isn't enabled.
        /// </summary>
        public static ILogListener ManagedLogListener;

        public static PersistenceManager PersistenceManager;

        public static PropertyDataController PropertyManager;

        /// <summary>
        /// Handles cross-mod data storage for the utility
        /// </summary>
        public static SharedDataHandler DataHandler;

        /// <summary>
        /// Handles log requests between different loggers
        /// </summary>
        public static LogRequestHandler RequestHandler;

        public static FrameTimer Scheduler;

        public static int ThreadID;

        internal static void Initialize()
        {
            if (IsInitialized || initializingInProgress) return; //Initialize may be called several times during the init process

            initializingInProgress = true;

            UtilityLogger.EnsureLogTypeCapacity(UtilityConsts.CUSTOM_LOGTYPE_LIMIT);
            UtilityLogger.Initialize();

            //This is before hooks are established. It is highly likely that the utility will load very early, and any mod could force it. Since we cannot control
            //this factor, we have to infer using specific game fields to tell which part of the initialization period we are in
            SetupPeriod startupPeriod = SetupPeriod.Pregame;

            if (Custom.rainWorld != null)
            {
                if (Menu.Remix.OptionalText.engText == null) //This is set in PreModsInIt
                {
                    startupPeriod = SetupPeriod.RWAwake;
                }
                else if (Custom.rainWorld.processManager?.currentMainLoop is InitializationScreen)
                {
                    //All ExtEnumTypes are forcefully updated as part of the OnModsInit run routine. Look for initialized types
                    if (ExtEnumBase.valueDictionary.Count() < 50) //Somewhere between PreModsInIt and OnModsInit, we don't know where exactly
                        startupPeriod = SetupPeriod.PreMods;
                    else
                        startupPeriod = SetupPeriod.PostMods;
                }
                else //It shouldn't be possible to be another period
                {
                    startupPeriod = SetupPeriod.PostMods;
                }
            }

            RWInfo.LatestSetupPeriodReached = startupPeriod;

            UtilityEvents.OnSetupPeriodReached += onSetupPeriodReached;

            LoadComponents();

            LogID.InitializeLogIDs(); //This should be called for every assembly that initializes

            LogFilterParser.ParseFile();

            if (RWInfo.LatestSetupPeriodReached < SetupPeriod.PostMods)
                LogFilter.ActivateKeyword(UtilityConsts.FilterKeywords.ACTIVATION_PERIOD_STARTUP);

            if (IsControllingAssembly)
            {
                PropertyManager.ProcessLogFiles();

                List<ILogListener> listenerInstances = (List<ILogListener>)BepInEx.Logging.Logger.Listeners;

                //Replace the first DiskLogListener we can find with a utility controlled instance
                int listenerIndex = listenerInstances.FindIndex(l => l is DiskLogListener);

                if (listenerIndex >= 0)
                    listenerInstances[listenerIndex] = new BepInExDiskLogListener(new TimedLogWriter());
                else
                    listenerInstances.Add(new BepInExDiskLogListener(new TimedLogWriter()));

                //Listen for Unity log requests while the log file is unavailable
                if (RWInfo.LatestSetupPeriodReached < LogID.Unity.Properties.AccessPeriod)
                    UtilityLogger.ReceiveUnityLogEvents = true;

                AppDomain.CurrentDomain.UnhandledException += (o, e) => RequestHandler.DumpRequestsToFile();
                GameHooks.Initialize();
            }

            initializingInProgress = false;
            IsInitialized = true;
        }

        /// <summary>
        /// Creates, or establishes a reference to an existing instance of necessary utility components
        /// </summary>
        internal static void LoadComponents()
        {
            Scheduler = ComponentUtils.GetOrCreate<FrameTimer>(UtilityConsts.ComponentTags.SCHEDULER, out _);
            PersistenceManager = ComponentUtils.GetOrCreate<PersistenceManager>(UtilityConsts.ComponentTags.PERSISTENCE_MANAGER, out _);
            DataHandler = ComponentUtils.GetOrCreate<SharedDataHandler>(UtilityConsts.ComponentTags.SHARED_DATA, out _);
            RequestHandler = ComponentUtils.GetOrCreate<LogRequestHandler>(UtilityConsts.ComponentTags.REQUEST_DATA, out _);

            PropertyManager = ComponentUtils.GetOrCreate<PropertyDataController>(UtilityConsts.ComponentTags.PROPERTY_DATA, out bool wasCreated);

            if (wasCreated)
            {
                IsControllingAssembly = true;
                PropertyManager.SetPropertiesFromFile();
            }
        }

        private static void onSetupPeriodReached(SetupPeriodEventArgs e)
        {
            if (e.CurrentPeriod > e.LastPeriod)
            {
                RWInfo.LatestSetupPeriodReached = e.CurrentPeriod;

                if (RWInfo.LatestSetupPeriodReached == SetupPeriod.RWAwake)
                {
                    //When the game starts, we need to clean up old log files. Any mod that wishes to access these files
                    //must do so in their plugin's OnEnable, or Awake method
                    PropertyManager.CompleteStartupRoutine();
                }
                else
                {
                    //In every other situation the period changes, we process requests that may have gone unhandled since the last setup period
                    RequestHandler.ProcessRequests();
                }
            }
        }

        /// <summary>
        /// Searches for an ILogListener that returns a signal sent through ToString().
        /// This listener belongs to the LogManager plugin.
        /// </summary>
        internal static void FindManagedListener()
        {
            IEnumerator<ILogListener> enumerator = BepInEx.Logging.Logger.Listeners.GetEnumerator();

            ILogListener managedListener = null;
            while (enumerator.MoveNext() && managedListener == null)
            {
                if (enumerator.Current.GetSignal() != null)
                    managedListener = enumerator.Current;
            }
        }

        internal static void HandleLogSignal()
        {
            if (ManagedLogListener != null)
                Logger.ProcessLogSignal(ManagedLogListener.GetSignal());
        }
    }
}
