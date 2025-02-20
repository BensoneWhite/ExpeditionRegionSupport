﻿using BepInEx.Logging;
using LogUtils.Helpers.Console;
using System;
using System.Linq;
using UnityEngine;

namespace LogUtils.Enums
{
    public partial class LogCategory : SharedExtEnum<LogCategory>
    {
        /// <summary>
        /// The default conversion type for LogLevel enum
        /// </summary>
        public const LogLevel LOG_LEVEL_DEFAULT = LogLevel.Info;

        /// <summary>
        /// The default conversion type for LogType enum
        /// </summary>
        public const LogType LOG_TYPE_DEFAULT = LogType.Log;

        private LogLevel _bepInExConversion = LOG_LEVEL_DEFAULT;
        private LogType _unityConversion = LOG_TYPE_DEFAULT;

        /// <summary>
        /// A flag that indicates that conversion fields need to be updated
        /// </summary>
        private bool conversionFieldsNeedUpdating;

        /// <summary>
        /// The category value translated to the category enum used for BepInEx logging
        /// </summary>
        public LogLevel BepInExCategory
        {
            get
            {
                if (!ReferenceEquals(ManagedReference, this))
                    return ManagedReference.BepInExCategory;
                return _bepInExConversion;
            }
        }

        /// <summary>
        /// The category value translated to the category enum used for Unity logging
        /// </summary>
        public LogType UnityCategory
        {
            get
            {
                if (!ReferenceEquals(ManagedReference, this))
                    return ManagedReference.UnityCategory;
                return _unityConversion;
            }
        }

        /// <summary>
        /// The bitflag translation representing this LogCategory
        /// TODO: Implement for composites
        /// </summary>
        public virtual int FlagValue => indexToConversionValue();

        private Color _consoleColor;

        /// <summary>
        /// The color that will be used in the console, or other write implementation that supports text coloration  
        /// </summary>
        public virtual Color _ConsoleColor
        {
            get
            {
                //TODO: Override for composites
                if (!ReferenceEquals(ManagedReference, this))
                    return ManagedReference._ConsoleColor;
                return _consoleColor;
            }
            set
            {
                //TODO: Override for composites
                if (!ReferenceEquals(ManagedReference, this))
                    ManagedReference._ConsoleColor = value;
                _consoleColor = value;
            }
        }

        public static LogCategory[] RegisteredEntries => values.entries.Select(entry => new LogCategory(entry)).ToArray();

        public static LogCategoryCombiner Combiner = new LogCategoryCombiner();

        /// <summary>
        /// Constructs a registered LogCategory instance
        /// </summary>
        /// <param name="value">The ExtEnum value associated with this instance</param>
        /// <param name="bepInExEquivalent">
        /// The enum value to be used when this category is used by the BepInEx logger
        /// Set to null assigns a custom LogLevel, otherwise will take the value of the LogLevel provided
        /// </param>
        /// <param name="unityEquivalent">
        /// The enum value to be used when this category is used by the Unity logger
        /// Set to null assigns a custom LogType, otherwise will take the value of the LogType provided
        /// </param>
        public LogCategory(string value, LogLevel? bepInExEquivalent, LogType? unityEquivalent) : base(value, true)
        {
            if (!ReferenceEquals(ManagedReference, this))
            {
                //When the managed reference is registered at the same time as this instance, propagate constructor parameters to the managed reference
                if (ManagedReference.conversionFieldsNeedUpdating)
                {
                    int customValue = indexToConversionValue();
                    ManagedReference.updateConversionFields(bepInExEquivalent ?? (LogLevel)customValue, unityEquivalent ?? (LogType)customValue);
                }

                //Make sure that backing fields always have the same values as the managed reference
                updateConversionFields(ManagedReference._bepInExConversion, ManagedReference._unityConversion);
            }
            else
            {
                int customValue = indexToConversionValue();
                updateConversionFields(bepInExEquivalent ?? (LogLevel)customValue, unityEquivalent ?? (LogType)customValue);
            }
        }

        ///This implementarion looks silly, consoleColor is not getting the color from any source
        //public LogCategory(string value, LogLevel? bepInExEquivalent, LogType? unityEquivalent, Color consoleColor) : base(value, true)
        //{
        //    var BepInExLogLevel = bepInExEquivalent.Value.GetHighestLevel();
        //    switch (BepInExLogLevel)
        //    {
        //        case LogLevel.Debug:
        //            _ConsoleColor = ConsoleColorUnity.GetUnityColor(ConsoleColor.Gray);
        //            break;
        //        case LogLevel.Message:
        //            _ConsoleColor = ConsoleColorUnity.GetUnityColor(ConsoleColor.White);
        //            break;
        //        case LogLevel.Warning:
        //            _ConsoleColor = ConsoleColorUnity.GetUnityColor(ConsoleColor.Yellow);
        //            break;
        //        case LogLevel.Error:
        //            _ConsoleColor = ConsoleColorUnity.GetUnityColor(ConsoleColor.DarkRed);
        //            break;
        //        case LogLevel.Fatal:
        //            _ConsoleColor = ConsoleColorUnity.GetUnityColor(ConsoleColor.Red);
        //            break;
        //        default:
        //            _ConsoleColor = ConsoleColorUnity.GetUnityColor(ConsoleColor.Gray);
        //            break;
        //    }

        //    if (consoleColor == null)
        //    {
        //        _consoleColor = Color.white;
        //    }

        //}

        /// <summary>
        /// Constructs a LogCategory instance
        /// </summary>
        /// <param name="value">The ExtEnum value associated with this instance</param>
        /// <param name="register">Whether or not this instance should be registered as a unique ExtEnum entry</param>
        public LogCategory(string value, bool register = false) : base(value, register)
        {
            //We can only give custom conversions to registered instances because these instances have a valid index assigned
            if (Registered)
            {
                if (!ReferenceEquals(ManagedReference, this))
                {
                    if (ManagedReference.conversionFieldsNeedUpdating)
                    {
                        int customValue = indexToConversionValue();
                        ManagedReference.updateConversionFields((LogLevel)customValue, (LogType)customValue);
                    }

                    //Make sure that backing fields always have the same values as the managed reference
                    updateConversionFields(ManagedReference._bepInExConversion, ManagedReference._unityConversion);
                }
                else
                {
                    int customValue = indexToConversionValue();
                    updateConversionFields((LogLevel)customValue, (LogType)customValue);
                }
            }
        }

        public override void Register()
        {
            //When a registration is applied, set a flag that allows conversion fields to be overwritten
            if (!ManagedReference.Registered || index < 0)
                conversionFieldsNeedUpdating = true;

            base.Register();
        }

        private int indexToConversionValue()
        {
            if (index < 0) return -1;

            //Map the registration index to an unmapped region of values designated for custom enum values
            //Assigned value will be a valid bit flag position
            return (int)Math.Min(CONVERSION_OFFSET * Math.Pow(index, 2), int.MaxValue);
        }

        private void updateConversionFields(LogLevel logLevel, LogType logType)
        {
            _bepInExConversion = logLevel;
            _unityConversion = logType;

            conversionFieldsNeedUpdating = false;
        }

        public static LogID GetUnityLogID(LogType logType)
        {
            return !IsErrorCategory(logType) ? LogID.Unity : LogID.Exception;
        }

        public static bool IsErrorCategory(LogCategory category)
        {
            var composite = category as CompositeLogCategory;

            if (composite != null)
            {
                //Exclude the All flag here - not relevant to error handling
                return !composite.Contains(All) && composite.HasAny(Error | Fatal);
            }
            return category == Error || category == Fatal;
        }

        public static bool IsErrorCategory(LogType category)
        {
            return category == LogType.Error || category == LogType.Exception;
        }

        public static bool IsErrorCategory(LogLevel category)
        {
            return (category & (LogLevel.Error | LogLevel.Fatal)) != 0;
        }

        public static CompositeLogCategory operator |(LogCategory a, LogCategory b)
        {
            return Combiner.Combine(a, b);
        }

        public static CompositeLogCategory operator &(LogCategory a, LogCategory b)
        {
            return Combiner.Intersect(a, b);
        }

        public static CompositeLogCategory operator ^(LogCategory a, LogCategory b)
        {
            return Combiner.Distinct(a, b);
        }

        public static LogCategory operator ~(LogCategory target)
        {
            return Combiner.GetComplement(target);
        }

        static LogCategory()
        {
            Default = Info;
        }

        public static readonly LogCategory All = new LogCategory("All", LogLevel.All, null);
        public static readonly LogCategory None = new LogCategory("None", LogLevel.None, LogType.Log);
        public static readonly LogCategory Assert = new LogCategory("Assert", null, LogType.Assert);
        public static readonly LogCategory Debug = new LogCategory("Debug", LogLevel.Debug, null);
        public static readonly LogCategory Info = new LogCategory("Info", LogLevel.Info, LogType.Log);
        public static readonly LogCategory Message = new LogCategory("Message", LogLevel.Message, LogType.Log);
        public static readonly LogCategory Important = new LogCategory("Important", null, null);
        public static readonly LogCategory Warning = new LogCategory("Warning", LogLevel.Warning, LogType.Warning);
        public static readonly LogCategory Error = new LogCategory("Error", LogLevel.Error, LogType.Error);
        public static readonly LogCategory Fatal = new LogCategory("Fatal", LogLevel.Fatal, LogType.Error);

        public static readonly LogCategory Default;
    }
}
