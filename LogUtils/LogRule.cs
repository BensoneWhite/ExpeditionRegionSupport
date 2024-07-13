﻿using System;
using System.Collections.Generic;

namespace LogUtils
{
    public abstract class LogRule
    {
        public bool ReadOnly => !IsTemporary || Owner?.ReadOnly == true;

        /// <summary>
        /// An unique string that identifies a particular LogRule. LogRules with the same value in this field will be treated as interchangable within the LogRuleCollection class
        /// </summary>
        public string Name = "Unknown";

        /// <summary>
        /// The default priority of a LogRule. Rules are applied in order of priority from lowest to highest
        /// </summary>
        public float Priority
        {
            get
            {
                if (TemporaryOverride != null)
                    return TemporaryOverride.GetPriority();
                return GetPriority();
            }
        }

        private LogRule _temporaryOverride;

        /// <summary>
        /// The instance stored in this field takes priority over the LogRule that contains it
        /// </summary>
        public LogRule TemporaryOverride
        {
            get => _temporaryOverride;
            set
            {
                if (value == this) return; //A reference cannot override itself

                if (_temporaryOverride != null)
                {
                    _temporaryOverride.IsTemporary = false;
                }

                if (value != null)
                {
                    value.IsTemporary = true;
                    value.TemporaryOverride = null; //Temporary rules should not be allowed to have temporary rules
                }
                _temporaryOverride = value;
            }
        }

        public bool IsTemporary { get; private set; }

        private bool _enabled = true;
        public bool IsEnabled
        {
            get
            {
                if (TemporaryOverride != null)
                    return TemporaryOverride.IsEnabled;
                return _enabled;
            }
            set
            {
                if (TemporaryOverride != null)
                {
                    TemporaryOverride.IsEnabled = value;
                    return;
                }

                if (ReadOnly || value == _enabled) return;

                _enabled = value;
            }
        }

        {
            {

            }
        }

        public string PropertyString => ToPropertyString();

        public LogRule(bool enabled)
        {
            _enabled = enabled;
        }

        public LogRule(string name, bool enabled) : this(enabled)
        {
            Name = name;
        }

        public void Apply(ref string message)
        {
            if (TemporaryOverride != null)
            {
                message = TemporaryOverride.ApplyRule(message); //Apply temporary rule instead
                return;
            }
            message = ApplyRule(message);
        }

        protected virtual string ApplyRule(string message)
        {
            return message;
        }

        protected virtual float GetPriority()
        {
            return 0.7f;
        }

        public virtual string ToPropertyString()
        {
            return LogProperties.ToPropertyString(Name.ToLower(), _enabled.ToString());
        }

        public override string ToString()
        {
            return ToPropertyString();
        }

        public delegate string ApplyDelegate(string message);
    }

    public class ShowCategoryRule : LogRule
    {
        public ShowCategoryRule(bool enabled) : base("ShowCategory", enabled)
        {
        }

        protected override float GetPriority()
        {
            return 0.995f; //This rule has the second highest defined priority
        }
    }

    public class ShowLineCountRule : LogRule
    {
        public ShowLineCountRule(bool enabled) : base("ShowLineCount", enabled)
        {
        }

        protected override float GetPriority()
        {
            return 1.0f; //This rule has the highest defined priority
        }
    }
}
