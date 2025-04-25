using System.Xml.Serialization;

namespace CrystalWebApp.Models
{
    [XmlRoot("ReportsMetadata")]
    public class ReportsMetadata
    {
        [XmlElement("Categories")]
        public Categories Categories { get; set; }
    }

    public class Categories
    {
        [XmlElement("Category")]
        public List<Category> CategoryList { get; set; }
    }

    public class Category
    {
        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlElement("Report")]
        public List<ReportInfo> Reports { get; set; }
    }

    public class ReportInfo
    {
        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlAttribute("displayName")]
        public string DisplayName { get; set; }

        [XmlAttribute("description")]
        public string Description { get; set; }

        [XmlElement("whereClauseTemplates")]
        public ReportWhereClauseTemplates WhereClauseTemplates { get; set; } = new ReportWhereClauseTemplates();
    }

    public class ReportWhereClauseTemplates
    {
        [XmlElement("template")]
        public List<ReportFilterTemplate> Templates { get; set; } = new List<ReportFilterTemplate>();
    }

    public class ReportFilterTemplate
    {
        [XmlAttribute("name")]
        public string Name { get; set; }

        [XmlText]
        public string Template { get; set; }

        public string GetFilterName()
        {
            return Name;
        }

        public string GetFilterType()
        {
            // Determine filter type based on template content
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