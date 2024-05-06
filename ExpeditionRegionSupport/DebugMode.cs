﻿using ExpeditionRegionSupport.Regions;
using ExpeditionRegionSupport.Regions.Data;
using ExpeditionRegionSupport.Tools;
using System;
using System.Collections.Generic;
using System.Text;

namespace ExpeditionRegionSupport
{
    public static class DebugMode
    {
        /// <summary>
        /// Set to true when running some type of debug event in order to make the mod aware that the process is running
        /// </summary>
        public static bool RunningDebugProcess;

        /// <summary>
        /// Set to true to minimize logging, and other processes triggered by DebugMode
        /// </summary>
        public static bool EmulateReleaseConditions;

        public static List<DebugTimer> RegisteredTimers = new List<DebugTimer>();

        public static DebugTimer CreateTimer(bool registerTimer, bool allowResultLogging = true)
        {
            DebugTimer timer = new DebugTimer(allowResultLogging);

            if (registerTimer)
                RegisteredTimers.Add(timer);
            return timer;
        }

        public static MultiUseTimer CreateMultiUseTimer(bool registerTimer, TimerOutput outputFormat, bool allowResultLogging = true)
        {
            MultiUseTimer timer = new MultiUseTimer(outputFormat, allowResultLogging);

            if (registerTimer)
                RegisteredTimers.Add(timer);
            return timer;
        }

        public static void StartDebugProcess()
        {
            RunningDebugProcess = true;
            RegisteredTimers.Clear();
        }

        public static void FinishDebugProcess()
        {
            RunningDebugProcess = false;
            RegisteredTimers.Clear();
        }

        public static void ProcessAllAccessibleRegions(SlugcatStats.Name slugcat)
        {
            StartDebugProcess();

            DebugTimer mainProcessTimer;
            mainProcessTimer = CreateTimer(true, false);
            mainProcessTimer.Start();

            string[] regions = RegionUtils.GetAllRegions();
            RegionsCache[] regionLists = new RegionsCache[regions.Length];

            RegionsCache largestRegionCache = null; //Stores the biggest cache by size
            for (int regionIndex = 0; regionIndex < regions.Length; regionIndex++)
            {
                string regionCode = regions[regionIndex];

                if (largestRegionCache != null)
                    RegionUtils.RegionAccessibilityCache = largestRegionCache;

                DebugTimer processTimer;
                processTimer = CreateTimer(true, false);
                processTimer.ID = "Region Access";
                processTimer.Start();

                List<string> accessibleRegions = RegionUtils.GetAccessibleRegions(regionCode, slugcat);

                if (largestRegionCache == null || largestRegionCache.Regions.Count < RegionUtils.RegionAccessibilityCache.Regions.Count)
                    largestRegionCache = RegionUtils.RegionAccessibilityCache;

                regionLists[regionIndex] = new RegionsCache(regionCode, accessibleRegions);

                processTimer.ReportTime("Finding accessible regions");
                processTimer.Stop();
            }

            mainProcessTimer.ReportTime("Entire process");
            mainProcessTimer.Stop();

            StringBuilder sb = new StringBuilder();

            sb.AppendLine("Process results")
              .AppendLine($"{RegisteredTimers.Count} registered timers")
              .AppendLine(mainProcessTimer.ToString());

            RegisteredTimers.Remove(mainProcessTimer); //Easier to process the results if we remove the main timer here

            bool allTimersProcessed = false;

            var timers = RegisteredTimers.GetEnumerator();
            DebugTimer timer = null;
            foreach (RegionsCache regionAccessList in regionLists)
            {
                sb.AppendLine("REGION " + regionAccessList.RegionCode)
                  .AppendLine("ACCESSIBILITY LIST")
                  .AppendLine(regionAccessList.Regions.FormatToString(','));

                if (allTimersProcessed)
                {
                    sb.AppendLine("All timers processed before every region cache could be checked");
                    continue;
                }

                bool regionHandled = false;
                while (!regionHandled)
                {
                    if (timer == null || timer.ID != "Region Access")
                        timer = findNextIdentifiedTimer();

                    if (timer == null)
                    {
                        allTimersProcessed = true;
                        sb.AppendLine("End of data reached");
                        break;
                    }

                    if (timer != null)
                    {
                        if (timer.ID != null)
                            sb.AppendLine(timer.ID);

                        switch (timer.ID)
                        {
                            case "Region Access":
                                sb.AppendLine(timer.ToString()) //Total process time for a single region
                                  .AppendLine("BREAKDOWN");
                                timer = null; //Set to null, so Region Connections may be found
                                break;
                            case "Region Connections":
                                processResultBreakdown();
                                regionHandled = true;
                                break;
                        }
                    }
                }
            }

            //foreach (DebugTimer timer in RegisteredTimers)
            //    sb.AppendLine(timer.ToString());

            Plugin.Logger.LogDebug(sb.ToString());
            FinishDebugProcess();

            DebugTimer findNextIdentifiedTimer()
            {
                while (timers.MoveNext() && string.IsNullOrEmpty(timers.Current.ID))
                    sb.AppendLine(timers.Current.ToString());

                return timers.Current;
            }

            void processResultBreakdown()
            {
                DebugTimer timer = timers.Current;

                if (timer.ID == "Region Connections")
                {
                    MultiUseTimer regionConnectionTimer = (MultiUseTimer)timer;
                    sb.AppendLine(regionConnectionTimer.ToString());

                    //Handle any recursively processed regions
                    int expectedSubTimers = regionConnectionTimer.AllResults.Count;
                    sb.AppendLine("Checking sub-timers: " + expectedSubTimers);
                    for (int i = 0; i < expectedSubTimers; i++)
                    {
                        timer = findNextIdentifiedTimer();

                        if (timer == null)
                        {
                            sb.AppendLine("End of data reached");
                            break;
                        }

                        if (timer.ID != "Region Connections")
                        {
                            sb.AppendLine("Unexpected ID detected");
                            break;
                        }

                        sb.AppendLine(timer.ID);
                        processResultBreakdown();
                    }
                }
            }
        }

        public static void TestRegionMiner()
        {
            StartDebugProcess();

            var logger = Plugin.Logger;

            RegionDataMiner regionMiner = new RegionDataMiner();

            IEnumerable<string> roomData_SI = regionMiner.GetRoomLines("SI");

            logger.LogInfo(string.Empty);
            logger.LogInfo("Showing ROOM LINES");
            logger.LogInfo(string.Empty);

            var enumerator = roomData_SI.GetEnumerator();

            enumerator.MoveNext();
            string line1 = enumerator.Current;
            enumerator.MoveNext();
            string line2 = enumerator.Current;
            enumerator.MoveNext();
            string line3 = enumerator.Current;

            logger.LogInfo(line1 + " " + line2 + " " + line3);

            //foreach (string roomData in roomData_SI)
            //    Logger.LogInfo(roomData);

            IEnumerable<string> roomData_HI = regionMiner.GetRoomLines("HI");

            logger.LogInfo(string.Empty);
            logger.LogInfo("Showing ROOM LINES");
            logger.LogInfo(string.Empty);
            foreach (string roomData in roomData_HI)
                logger.LogInfo(roomData);

            //Print again to confirm that stream closes after use
            logger.LogInfo(string.Empty);
            logger.LogInfo("Showing ROOM LINES");
            logger.LogInfo(string.Empty);
            roomData_SI = regionMiner.GetRoomLines("SI");

            foreach (string roomData in roomData_SI)
                logger.LogInfo(roomData);
            FinishDebugProcess();
        }
    }
}
