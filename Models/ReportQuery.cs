using System.Xml.Serialization;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System;
using System.Reflection;
using Serilog;

namespace CrystalWebApp.Models
{
    [XmlRoot("ReportQueries")]
    public class ReportQueries
    {
        [XmlElement("Report")]
        public List<ReportQuery> Reports { get; set; }
    }

    public class ReportQuery
    {
        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlElement("query")]
        public string Query { get; set; }

        [XmlElement("whereClauseTemplates")]
        public WhereClauseTemplates WhereClauseTemplates { get; set; }

        [XmlElement("groupBy")]
        public string GroupBy { get; set; }

        [XmlElement("reportPath")]
        public string ReportPath { get; set; }

        public string BuildQuery(Dictionary<string, List<string>> filterParams)
        {
            string finalQuery = this.Query;
            var whereClauses = new List<string>();

            if (this.WhereClauseTemplates?.Templates != null && filterParams != null)
            {
                foreach (var param in filterParams)
                {
                    string paramName = param.Key;
                    List<string> paramValues = param.Value;

                    var template = this.WhereClauseTemplates.Templates.FirstOrDefault(t => t.Name == paramName);

                    if (template != null && paramValues?.Any(v => !string.IsNullOrEmpty(v)) == true)
                    {
                        // Flatten potentially comma-separated values and trim whitespace
                        var allValues = paramValues
                            .SelectMany(v => v?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries) ?? Enumerable.Empty<string>())
                            .Select(v => v.Trim())
                            .Where(v => !string.IsNullOrEmpty(v))
                            .Distinct()
                            .ToList();

                        if (allValues.Any())
                        {
                            string formattedValues;
                            string filterType = template.GetFilterType(); // Reuse existing logic to determine type

                            // Format values based on filter type (add quotes for strings/multiselect)
                            bool isNumeric = allValues.All(v => decimal.TryParse(v.Replace("'", "''"), out _)); // Handle potential single quotes in values

                            if (!isNumeric && (filterType == "MultiSelect" || filterType == "Text" || filterType == "SingleSelect"))
                            {
                                formattedValues = string.Join(",", allValues.Select(v => $"'{v.Replace("'", "''")}'")); // Quote non-numeric values and escape inner quotes
                            }
                            else
                            {
                                formattedValues = string.Join(",", allValues); // Numeric or other types don't need quotes
                            }

                            // Apply the template formatting
                            if (template.Template.Contains("{0}")) // Ensure template has a placeholder
                            {
                                // Special handling for IN clause - requires parentheses around values
                                if (template.Template.Contains("IN ({0})"))
                                {
                                    whereClauses.Add(string.Format(template.Template, formattedValues)); // Pass comma-separated list
                                }
                                // Handling for comparison operators like >=, <=, = - usually take one value
                                else if (template.Template.Contains("= {0}") || template.Template.Contains(">= {0}") || template.Template.Contains("<= {0}") || template.Template.Contains("LIKE {0}"))
                                {
                                    // Use the first value, assuming single value comparison
                                     // Ensure the single value is formatted correctly (quoted if needed)
                                    string singleFormattedValue = isNumeric ? allValues.First() : $"'{allValues.First().Replace("'", "''")}'";
                                    whereClauses.Add(string.Format(template.Template, singleFormattedValue));
                                }
                                else
                                {
                                    // Default case for other templates with {0}
                                    whereClauses.Add(string.Format(template.Template, formattedValues));
                                }
                            } else {
                                // Template might not use {0}, could be a fixed clause
                                whereClauses.Add(template.Template);
                            }
                        }
                    }
                }
            }

            // Combine where clauses and integrate into the main query
            if (whereClauses.Any())
            {
                // Join clauses with a space, assuming each clause starts with " AND " from the template
                string combinedWhere = string.Join(" ", whereClauses);
                
                // Find the position after the first WHERE clause (case-insensitive)
                Match whereMatch = Regex.Match(finalQuery, @"\sWHERE\s", RegexOptions.IgnoreCase);
                if (whereMatch.Success)
                {
                    // Find the end of the existing WHERE condition (e.g., end of "1=1" or other base condition)
                    // This is tricky, let's simplify: insert directly after the WHERE keyword + space.
                    // This assumes the base query has *something* after WHERE, like "WHERE 1=1".
                    int insertPosition = whereMatch.Index + whereMatch.Length; 
                    
                    // Check if the base query already ends with WHERE 1=1 or similar, adjust insertion if needed
                    string textAfterWhere = finalQuery.Substring(insertPosition).TrimStart();
                    if (textAfterWhere.StartsWith("1=1")) {
                         insertPosition = finalQuery.IndexOf("1=1", insertPosition) + "1=1".Length;
                    }
                    // Insert the combined clauses with just a leading space
                    finalQuery = finalQuery.Insert(insertPosition, $" {combinedWhere}");
                }
                // Else: If no WHERE clause found in base query (unexpected?), this won't add filters.
                // Consider adding a full WHERE clause if necessary.
            }

            // Append GroupBy clause if present
            if (!string.IsNullOrWhiteSpace(this.GroupBy))
            {
                 // Ensure GroupBy isn't added if the query ends with WHERE
                 if (finalQuery.EndsWith(" WHERE", StringComparison.OrdinalIgnoreCase)) {
                     finalQuery = finalQuery.Substring(0, finalQuery.Length - 6).Trim(); // Remove trailing WHERE
                 }
                finalQuery += " " + this.GroupBy;
            }

            // Simple cleanup for potentially redundant 'AND' near WHERE
            finalQuery = Regex.Replace(finalQuery, @"WHERE\s+AND\s+", "WHERE ", RegexOptions.IgnoreCase);

            // Log the query with Serilog - preserving SQL formatting
            // Don't normalize whitespace here to maintain SQL formatting in logs
            Log.ForContext("SqlQuery", finalQuery)
               .Information("Generated SQL query for report {ReportName}", this.Name);

            // Return the query with original formatting (newlines, tabs from XML)
            return finalQuery.Trim(); // Trim leading/trailing whitespace only
        }

        public static Dictionary<string, List<string>> ConvertFilterSelectionToParams(FilterSelection filterSelection)
        {
            var filterParamsDict = new Dictionary<string, List<string>>();
            if (filterSelection == null) return filterParamsDict;

            // Helper function to add list parameters if they have non-empty values
            void AddListParam(string key, List<string> values)
            {
                if (values?.Any(v => !string.IsNullOrEmpty(v)) == true)
                {
                    filterParamsDict.Add(key, values.Where(v => !string.IsNullOrEmpty(v)).ToList());
                }
            }

            // Helper function to add string parameters if they are not empty
            void AddStringParam(string key, string value)
            {
                if (!string.IsNullOrEmpty(value))
                {
                    filterParamsDict.Add(key, new List<string> { value });
                }
            }

            // Using reflection to iterate through FilterSelection properties
            // Ensure System.Reflection is included via 'using'
            var properties = typeof(FilterSelection).GetProperties(); 
            foreach (var prop in properties)
            {
                var value = prop.GetValue(filterSelection);
                if (value != null)
                {
                    if (prop.PropertyType == typeof(List<string>))
                    {
                        AddListParam(prop.Name, (List<string>)value);
                    }
                    else if (prop.PropertyType == typeof(string))
                    {
                        AddStringParam(prop.Name, (string)value);
                    }
                    else if (prop.PropertyType == typeof(List<int>))
                    {
                        // Handle List<int> specifically
                        var intList = (List<int>)value;
                        if (intList.Any())
                        {
                            filterParamsDict.Add(prop.Name, intList.Select(i => i.ToString()).ToList());
                        }
                    }
                    // Add more type handlers if needed (e.g., List<DateTime>, etc.)
                }
            }

            return filterParamsDict;
        }
    }

    public class WhereClauseTemplates
    {
        [XmlElement("template")]
        public List<FilterTemplate> Templates { get; set; }
    }

    public class FilterTemplate
    {
        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlText]
        public string Template { get; set; }

        public string GetFilterName()
        {
            return string.Join(" ", Regex.Split(Name, @"(?<!^)(?=[A-Z])"));
        }

        public string GetFilterType()
        {
            if (Template.Contains("IN ({0})"))
                return "MultiSelect";
            else if (Template.Contains("= {0}"))
                return "SingleSelect";
            else if (Template.Contains(">= {0}") || Template.Contains("<= {0}"))
                return "Date";
            else
                return "Text";
        }
    }
} 