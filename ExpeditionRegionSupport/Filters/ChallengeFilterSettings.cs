﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Expedition;
using ExpeditionRegionSupport.Filters.Utils;

namespace ExpeditionRegionSupport.Filters
{
    public static partial class ChallengeFilterSettings
    {
        public static Dictionary<string, List<ChallengeFilter>> Filters;

        public static FilterOptions CurrentFilter;

        /// <summary>
        /// The Expedition challenge that the filter is handling, or is about to handle
        /// </summary>
        public static Challenge FilterTarget;

        /// <summary>
        /// A flag that indicates that not all assignment requests could be processed successfully
        /// </summary>
        public static bool FailedToAssign;

        static ChallengeFilterSettings()
        {
            Filters = new Dictionary<string, List<ChallengeFilter>>();

            //Iterate through challenge types to populate challenge filters 
            foreach (string name in ExpeditionGame.challengeNames.Keys)
            {
                List<ChallengeFilter> filters = new List<ChallengeFilter>();

                ChallengeFilter f = processFilter(name);
                if (f != null)
                    filters.Add(f);

                Filters.Add(name, filters);
            }
        }

        private static ChallengeFilter processFilter(string name)
        {
            FilterOptions filterType = FilterOptions.VisitedRegions; //The only type managed by default

            switch (name)
            {
                case ExpeditionConsts.ChallengeNames.ECHO:
                case ExpeditionConsts.ChallengeNames.PEARL_HOARD:
                    return new ChallengeFilter(filterType);
                case ExpeditionConsts.ChallengeNames.VISTA:
                    return new ChallengeFilter(filterType)
                    {
                        ValueModifier = (v) => v.Split('_')[0] //This challenge stores room codes, which need underscore parsing
                    };
                case ExpeditionConsts.ChallengeNames.PEARL_DELIVERY:
                    return new PearlDeliveryChallengeFilter(filterType);
                case ExpeditionConsts.ChallengeNames.NEURON_DELIVERY:
                    return new NeuronDeliveryChallengeFilter(filterType);
            }
            return null;
        }

        /// <summary>
        /// Retrieves filters that are associated with the FilterTarget
        /// </summary>
        public static List<ChallengeFilter> GetFilters()
        {
            if (FilterTarget == null) return new List<ChallengeFilter>();

            return Filters[FilterTarget.GetTypeName()];
        }

        public static bool CheckConditions()
        {
            //Get the filters that apply to the target
            List<ChallengeFilter> availableFilters = GetFilters();

            return availableFilters.TrueForAll(f => f.ConditionMet());
        }

        public static void ApplyFilter(List<string> allowedRegions)
        {
            //Get the filters that apply to the target
            List<ChallengeFilter> availableFilters = GetFilters();

            foreach (ChallengeFilter filter in availableFilters)
                filter.Apply(allowedRegions);
        }
    }

    public enum FilterOptions
    {
        None,
        VisitedRegions
    }
}
