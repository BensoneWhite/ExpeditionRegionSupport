﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LogUtils.Helpers;
using UnityEngine;

namespace LogUtils
{
    public class PropertyDataController : UtilityComponent
    {
        public List<LogProperties> Properties = new List<LogProperties>();
        public CustomLogPropertyCollection CustomLogProperties = new CustomLogPropertyCollection();
        public Dictionary<LogProperties, StringDictionary> UnrecognizedFields = new Dictionary<LogProperties, StringDictionary>();

        static PropertyDataController()
        {
            //Initialize the utility when this class is accessed
            if (!UtilityCore.IsInitialized)
                UtilityCore.Initialize();
        }

        public PropertyDataController()
        {
            Tag = "Log Properties";

            CustomLogProperties.OnPropertyAdded += onCustomPropertyAdded;
            CustomLogProperties.OnPropertyRemoved += onCustomPropertyRemoved;
        }

        private void onCustomPropertyAdded(CustomLogProperty property)
        {
            foreach (LogProperties properties in Properties)
            {
                CustomLogProperty customProperty = property.Clone(); //Create an instance of the custom property for each item in the property list

                //Search for unrecognized fields that match the custom property
                if (UnrecognizedFields.TryGetValue(properties, out StringDictionary fieldDictionary))
                {
                    if (fieldDictionary.ContainsKey(customProperty.Name))
                    {
                        customProperty.Value = fieldDictionary[customProperty.Name]; //Overwrites default value with the value taken from file
                        fieldDictionary.Remove(customProperty.Name); //Field is no longer unrecognized

                        if (fieldDictionary.Count == 0)
                            UnrecognizedFields.Remove(properties); //Remove the dictionary after it is empty
                    }
                }

                //Register the custom property with the associated properties instance
                properties.CustomProperties.AddProperty(customProperty);
            }
        }

        private void onCustomPropertyRemoved(CustomLogProperty property)
        {
            //Remove the custom property reference from each properties instance
            foreach (LogProperties properties in Properties)
                properties.CustomProperties.RemoveProperty(p => p.Name == property.Name);
        }

        public List<LogProperties> GetProperties(LogID logID)
        {
            return Properties.FindAll(p => p.Filename == logID.value);
        }

        /// <summary>
        /// Finds the first detected LogProperties instance associated with the given LogID, and relative filepath
        /// </summary>
        /// <param name="logID">The LogID to search for</param>
        /// <param name="relativePathNoFile">The filepath to search for. When set to null, the first LogID match will be returned</param>
        public LogProperties GetProperties(LogID logID, string relativePathNoFile)
        {
            if (relativePathNoFile == null)
                return Properties.Find(p => p.Filename == logID.value);

            return Properties.Find(p => p.Filename == logID.value && p.HasPath(relativePathNoFile));
        }

        public LogProperties SetProperties(LogID logID, string relativePathNoFile)
        {
            LogProperties properties = new LogProperties(logID.value, relativePathNoFile);

            Properties.Add(properties);
            return properties;
        }

        public void ReadFromFile()
        {
            LogPropertyReader reader = new LogPropertyReader("logs.txt");

            var enumerator = reader.GetEnumerator();

            while (enumerator.MoveNext())
            {
                StringDictionary dataFields = enumerator.Current;
                LogProperties properties = null;
                try
                {
                    properties = new LogProperties(dataFields["filename"], dataFields["path"])
                    {
                        Version = dataFields["version"],
                        AltFilename = dataFields["altfilename"],
                        Tags = dataFields["tags"].Split(',')
                    };

                    bool showCategories = bool.Parse(dataFields["showcategories"]);
                    bool showLineCount = bool.Parse(dataFields["showlinecount"]);

                    LogRule showCategoryRule = new ShowCategoryRule(showCategories);
                    LogRule showLineCountRule = new ShowLineCountRule(showLineCount);

                    properties.Rules.Add(showCategoryRule);
                    properties.Rules.Add(showLineCountRule);

                    const int expected_field_total = 8;

                    int unprocessedFieldTotal = dataFields.Count - expected_field_total;

                    if (unprocessedFieldTotal > 0)
                    {
                        var unrecognizedFields = UnrecognizedFields[properties] = new StringDictionary();

                        //Handle unrecognized, and custom fields by storing them in a list that other mods will be able to access
                        IDictionaryEnumerator fieldEnumerator = (IDictionaryEnumerator)dataFields.GetEnumerator();
                        while (unprocessedFieldTotal > 0)
                        {
                            fieldEnumerator.MoveNext();

                            DictionaryEntry fieldEntry = fieldEnumerator.Key switch
                            {
                                "filename" => default,
                                "altfilename" => default,
                                "aliases" => default,
                                "version" => default,
                                "path" => default,
                                "logrules" => default,
                                "showlinecount" => default,
                                "showcategories" => default,
                                _ => fieldEnumerator.Entry
                            };

                            if (!fieldEntry.Equals(default))
                            {
                                unrecognizedFields[(string)fieldEntry.Key] = (string)fieldEntry.Value;
                                unprocessedFieldTotal--;
                            }
                        }
                    }
                }
                catch (KeyNotFoundException)
                {
                    throw new KeyNotFoundException(string.Format("{0}.log is missing a required property. Check logs.txt for issues", dataFields["filename"]));
                }
                finally
                {
                    if (properties != null)
                        properties.ReadOnly = true;
                }
            }
        }

        public void SaveToFile()
        {
            StringBuilder sb = new StringBuilder();

            foreach (LogProperties properties in Properties)
            {
                sb.AppendLine(properties.ToString());

                if (UnrecognizedFields.TryGetValue(properties, out StringDictionary unrecognizedPropertyLines) && unrecognizedPropertyLines.Count > 0)
                {
                    if (!properties.CustomProperties.Any()) //Ensure that custom field header is only added once
                        sb.AppendPropertyString("custom");

                    foreach (string key in unrecognizedPropertyLines)
                        sb.AppendPropertyString(key, unrecognizedPropertyLines[key]);
                }
            }

            File.WriteAllText(Path.Combine(Application.streamingAssetsPath, "logs.txt"), sb.ToString());
        }

        public override Dictionary<string, object> GetFields()
        {
            Dictionary<string, object> fields = base.GetFields();

            fields[nameof(Properties)] = Properties;
            fields[nameof(CustomLogProperties)] = CustomLogProperties;
            fields[nameof(UnrecognizedFields)] = UnrecognizedFields;
            return fields;
        }

        public static string FormatAccessString(string logName, string propertyName)
        {
            return logName + ',' + propertyName;
        }
    }
}
