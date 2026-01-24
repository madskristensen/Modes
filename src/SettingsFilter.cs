using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace Modes
{
    /// <summary>
    /// Filters Visual Studio settings files to only include specific properties.
    /// Used to create minimal baseline backups that only contain settings affected by a mode.
    /// </summary>
    internal static class SettingsFilter
    {
        /// <summary>
        /// Filters a vssettings file to only keep the exact properties defined in the mode's settings.
        /// </summary>
        /// <param name="sourcePath">Path to the full exported settings file.</param>
        /// <param name="destPath">Path where the filtered settings will be saved.</param>
        /// <param name="modeSettings">The mode's settings XML document used as a template.</param>
        public static void FilterSettingsFile(string sourcePath, string destPath, XmlDocument modeSettings)
        {
            try
            {
                var exportDoc = new XmlDocument();
                exportDoc.Load(sourcePath);

                XmlNode exportUserSettings = exportDoc.SelectSingleNode("/UserSettings");
                XmlNode modeUserSettings = modeSettings.SelectSingleNode("/UserSettings");

                if (exportUserSettings == null || modeUserSettings == null)
                {
                    File.Copy(sourcePath, destPath, true);
                    return;
                }

                // Build lookup of properties to keep from mode settings
                HashSet<string> propertiesToKeep = BuildPropertiesToKeep(modeUserSettings);

                // Extract FontsAndColors category GUIDs to keep
                HashSet<string> fontCategoryGuidsToKeep = BuildFontCategoryGuidsToKeep(modeUserSettings);

                // Extract top-level Category names to keep from mode settings
                HashSet<string> topLevelCategoriesToKeep = BuildTopLevelCategoriesToKeep(modeUserSettings);

                // Filter ToolsOptions in the export to only keep matching properties
                FilterToolsOptions(exportUserSettings, propertiesToKeep);

                // Filter top-level Categories
                FilterTopLevelCategories(exportUserSettings, modeUserSettings, topLevelCategoriesToKeep, fontCategoryGuidsToKeep);

                exportDoc.Save(destPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to filter settings file: {ex.Message}");
                File.Copy(sourcePath, destPath, true);
            }
        }

        /// <summary>
        /// Builds a set of property paths to keep from the mode's ToolsOptions.
        /// </summary>
        private static HashSet<string> BuildPropertiesToKeep(XmlNode modeUserSettings)
        {
            var propertiesToKeep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            XmlNode modeToolsOptions = modeUserSettings.SelectSingleNode("ToolsOptions");
            if (modeToolsOptions != null)
            {
                foreach (XmlNode category in modeToolsOptions.SelectNodes("ToolsOptionsCategory"))
                {
                    var categoryName = category.Attributes?["name"]?.Value;
                    if (string.IsNullOrEmpty(categoryName)) continue;

                    foreach (XmlNode subCategory in category.SelectNodes("ToolsOptionsSubCategory"))
                    {
                        var subCategoryName = subCategory.Attributes?["name"]?.Value;
                        if (string.IsNullOrEmpty(subCategoryName)) continue;

                        foreach (XmlNode prop in subCategory.SelectNodes("PropertyValue"))
                        {
                            var propName = prop.Attributes?["name"]?.Value;
                            if (!string.IsNullOrEmpty(propName))
                            {
                                propertiesToKeep.Add($"{categoryName}/{subCategoryName}/{propName}");
                            }
                        }
                    }
                }
            }

            return propertiesToKeep;
        }

        /// <summary>
        /// Builds a set of FontsAndColors category GUIDs to keep.
        /// </summary>
        private static HashSet<string> BuildFontCategoryGuidsToKeep(XmlNode modeUserSettings)
        {
            var fontCategoryGuidsToKeep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // The structure can be: UserSettings/Category[@name='Environment_Group']/Category[@name='Environment_FontsAndColors']
            // or directly: UserSettings/Category[@name='Environment_FontsAndColors']
            XmlNode modeFontsCategory = modeUserSettings.SelectSingleNode(".//Category[@name='Environment_FontsAndColors']");
            if (modeFontsCategory != null)
            {
                foreach (XmlNode fontCategory in modeFontsCategory.SelectNodes(".//Category[@GUID or @Guid]"))
                {
                    // Try both GUID (uppercase) and Guid (mixed case) attributes
                    var guid = fontCategory.Attributes?["GUID"]?.Value
                               ?? fontCategory.Attributes?["Guid"]?.Value;
                    if (!string.IsNullOrEmpty(guid))
                    {
                        fontCategoryGuidsToKeep.Add(guid);
                    }
                }
            }

            return fontCategoryGuidsToKeep;
        }

        /// <summary>
        /// Builds a set of top-level category names to keep.
        /// </summary>
        private static HashSet<string> BuildTopLevelCategoriesToKeep(XmlNode modeUserSettings)
        {
            var topLevelCategoriesToKeep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (XmlNode modeCategory in modeUserSettings.SelectNodes("Category"))
            {
                var categoryName = modeCategory.Attributes?["name"]?.Value;
                if (!string.IsNullOrEmpty(categoryName))
                {
                    topLevelCategoriesToKeep.Add(categoryName);
                }
            }

            return topLevelCategoriesToKeep;
        }

        /// <summary>
        /// Filters the ToolsOptions section to only keep matching properties.
        /// </summary>
        private static void FilterToolsOptions(XmlNode exportUserSettings, HashSet<string> propertiesToKeep)
        {
            XmlNode exportToolsOptions = exportUserSettings.SelectSingleNode("ToolsOptions");
            if (exportToolsOptions == null) return;

            if (propertiesToKeep.Count == 0)
            {
                // Mode has no ToolsOptions settings, clear all ToolsOptionsCategory children
                exportToolsOptions.RemoveAll();
                return;
            }

            var categoriesToRemove = new List<XmlNode>();

            foreach (XmlNode category in exportToolsOptions.SelectNodes("ToolsOptionsCategory"))
            {
                var categoryName = category.Attributes?["name"]?.Value;
                if (string.IsNullOrEmpty(categoryName)) continue;

                var subCategoriesToRemove = new List<XmlNode>();

                foreach (XmlNode subCategory in category.SelectNodes("ToolsOptionsSubCategory"))
                {
                    var subCategoryName = subCategory.Attributes?["name"]?.Value;
                    if (string.IsNullOrEmpty(subCategoryName)) continue;

                    var propsToRemove = new List<XmlNode>();

                    foreach (XmlNode prop in subCategory.SelectNodes("PropertyValue"))
                    {
                        var propName = prop.Attributes?["name"]?.Value;
                        var fullPath = $"{categoryName}/{subCategoryName}/{propName}";

                        if (!propertiesToKeep.Contains(fullPath))
                        {
                            propsToRemove.Add(prop);
                        }
                    }

                    foreach (XmlNode node in propsToRemove)
                    {
                        subCategory.RemoveChild(node);
                    }

                    // Remove subcategory if no properties left
                    if (subCategory.SelectNodes("PropertyValue").Count == 0)
                    {
                        subCategoriesToRemove.Add(subCategory);
                    }
                }

                foreach (XmlNode node in subCategoriesToRemove)
                {
                    category.RemoveChild(node);
                }

                // Remove category if no subcategories left
                if (category.SelectNodes("ToolsOptionsSubCategory").Count == 0)
                {
                    categoriesToRemove.Add(category);
                }
            }

            foreach (XmlNode node in categoriesToRemove)
            {
                exportToolsOptions.RemoveChild(node);
            }
        }

        /// <summary>
        /// Filters top-level categories to only keep ones defined in mode settings.
        /// </summary>
        private static void FilterTopLevelCategories(
            XmlNode exportUserSettings,
            XmlNode modeUserSettings,
            HashSet<string> topLevelCategoriesToKeep,
            HashSet<string> fontCategoryGuidsToKeep)
        {
            var topLevelToRemove = new List<XmlNode>();

            foreach (XmlNode category in exportUserSettings.SelectNodes("Category"))
            {
                var categoryName = category.Attributes?["name"]?.Value;

                if (categoryName == "Environment_Group" && fontCategoryGuidsToKeep.Count > 0)
                {
                    FilterEnvironmentGroup(category, fontCategoryGuidsToKeep);
                }
                else if (topLevelCategoriesToKeep.Contains(categoryName))
                {
                    FilterCategoryProperties(category, modeUserSettings, categoryName);
                }
                else
                {
                    // Remove other top-level categories entirely
                    topLevelToRemove.Add(category);
                }
            }

            foreach (XmlNode node in topLevelToRemove)
            {
                exportUserSettings.RemoveChild(node);
            }
        }

        /// <summary>
        /// Filters the Environment_Group category to only keep matching FontsAndColors GUIDs.
        /// </summary>
        private static void FilterEnvironmentGroup(XmlNode category, HashSet<string> fontCategoryGuidsToKeep)
        {
            // Find Environment_FontsAndColors inside Environment_Group
            XmlNode fontsColorCategory = category.SelectSingleNode("Category[@name='Environment_FontsAndColors']");
            if (fontsColorCategory != null)
            {
                // Filter the FontsAndColors to only keep matching GUIDs
                XmlNode fontsAndColors = fontsColorCategory.SelectSingleNode("FontsAndColors");
                if (fontsAndColors != null)
                {
                    var fontCategoriesToRemove = new List<XmlNode>();
                    // Categories are inside Categories/Category elements
                    foreach (XmlNode fontCategory in fontsAndColors.SelectNodes(".//Category[@GUID or @Guid]"))
                    {
                        var guid = fontCategory.Attributes?["GUID"]?.Value
                                   ?? fontCategory.Attributes?["Guid"]?.Value;
                        if (!string.IsNullOrEmpty(guid) && !fontCategoryGuidsToKeep.Contains(guid))
                        {
                            fontCategoriesToRemove.Add(fontCategory);
                        }
                    }

                    foreach (XmlNode node in fontCategoriesToRemove)
                    {
                        node.ParentNode.RemoveChild(node);
                    }
                }
            }

            // Remove other categories inside Environment_Group (like Environment_Aliases, etc.)
            var otherCategoriesToRemove = new List<XmlNode>();
            foreach (XmlNode innerCategory in category.SelectNodes("Category"))
            {
                var innerName = innerCategory.Attributes?["name"]?.Value;
                if (innerName != "Environment_FontsAndColors")
                {
                    otherCategoriesToRemove.Add(innerCategory);
                }
            }

            foreach (XmlNode node in otherCategoriesToRemove)
            {
                category.RemoveChild(node);
            }
        }

        /// <summary>
        /// Filters properties within a category to only keep ones defined in the mode.
        /// </summary>
        private static void FilterCategoryProperties(XmlNode category, XmlNode modeUserSettings, string categoryName)
        {
            XmlNode modeCategory = modeUserSettings.SelectSingleNode($"Category[@name='{categoryName}']");
            if (modeCategory == null) return;

            var modePropertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (XmlNode prop in modeCategory.SelectNodes("PropertyValue"))
            {
                var propName = prop.Attributes?["name"]?.Value;
                if (!string.IsNullOrEmpty(propName))
                {
                    modePropertyNames.Add(propName);
                }
            }

            // Remove properties not in mode settings
            var propsToRemove = new List<XmlNode>();
            foreach (XmlNode prop in category.SelectNodes("PropertyValue"))
            {
                var propName = prop.Attributes?["name"]?.Value;
                if (!modePropertyNames.Contains(propName))
                {
                    propsToRemove.Add(prop);
                }
            }

            foreach (XmlNode node in propsToRemove)
            {
                category.RemoveChild(node);
            }
        }
    }
}
