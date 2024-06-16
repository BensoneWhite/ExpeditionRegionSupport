﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExpeditionRegionSupport.Data.Logging
{
    public class LogID : ExtEnum<LogProperties>
    {
        public LogProperties Properties;

        public LogID(string modID, string name, string relativePathNoFile = null, bool register = false) : base(name, false)
        {
            if (register)
            {
                //Make sure properties are read from file before any ExtEnums are registered
                LogProperties.LoadProperties();

                values.AddEntry(value);
                index = values.Count - 1;
            }
        }

        private static string validateName(string modID, string name, string relativePath)
        {
            //Check if property already exists
            var propertyManager = LogProperties.PropertyManager;
            if (propertyManager != null)
            {
                if (propertyManager.TryGetData(PropertyDataController.FormatAccessString(name, nameof(LogProperties.Filename)), out string filename))
                {
                    if (string.Equals(name, filename, StringComparison.InvariantCultureIgnoreCase))
                    {
                        //This entry already exists
                        return filename;
                    }

                    string altFilename = propertyManager.GetData<string>(PropertyDataController.FormatAccessString(name, nameof(LogProperties.AltFilename)));

                    if (string.Equals(name, altFilename, StringComparison.InvariantCultureIgnoreCase))
                    {
                        //This entry already exists under an alternate name
                        return filename;
                    }

                    List<string> nameAliases = propertyManager.GetData<List<string>>(PropertyDataController.FormatAccessString(name, nameof(LogProperties.Aliases)));

                    if (nameAliases.Exists(alias => string.Equals(name, alias, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        //This entry already exists as an aliased name
                        return filename;
                    }
                }
            }

            //This name is at this point considered unique
            return name;
        }
    }
}
