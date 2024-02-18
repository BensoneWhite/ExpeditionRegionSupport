﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExpeditionRegionSupport.Filters
{
    public class Filter
    {
        public FilterCriteria Criteria = FilterCriteria.None;

        private bool _enabled = true;
        /// <summary>
        /// Flag that prevents filter logic from being applied
        /// </summary>
        public virtual bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        /// <summary>
        /// Applies any constraints necessary for comparing sets of data
        /// </summary>
        public virtual void Apply()
        {
        }

        /// <summary>
        /// Used when there is a need to check a certain game condition is true
        /// </summary>
        public virtual bool ConditionMet()
        {
            return true;
        }
    }

    public class ListFilter<T> : Filter
    {
        public List<T> CompareValues;

        public ListFilter(List<T> compareValues, FilterCriteria criteria = FilterCriteria.MustInclude)
        {
            CompareValues = compareValues ?? new List<T>();
            Criteria = criteria;
        }

        /// <summary>
        /// Checks a list against a criteria, and removes items that don't meet that criteria/>
        /// </summary>
        /// <param name="allowedItems">The list to check</param>
        /// <param name="valueModifier">A function used to apply formatting to a value</param>
        public virtual void Apply(List<T> allowedItems, Func<T, T> valueModifier = null)
        {
            //Determines if we check if Compare reference contains or does not contain an item
            bool compareCondition = Criteria != FilterCriteria.MustExclude;

            allowedItems.RemoveAll(item =>
            {
                return Evaluate(item, compareCondition, valueModifier);
            });
        }

        public virtual bool Evaluate(T item, bool condition, Func<T, T> valueModifier = null)
        {
            return CompareValues.Contains(valueModifier == null ? item : valueModifier(item)) == condition;
        }
    }

    public enum FilterCriteria
    {
        None,
        MustInclude,
        MustExclude
    }
}
