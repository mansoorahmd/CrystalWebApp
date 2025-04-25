using Microsoft.AspNetCore.Mvc;
using System.Xml.Serialization;
using System.IO;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Serilog;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using CrystalWebApp.Utilities;
using System.Data;
using SharedServices.Models;

namespace CrystalWebApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReportListController : ControllerBase
    {
        private readonly IWebHostEnvironment _environment;
        private ReportsMetadata _reportsMetadata;
        private ReportQueries _reportQueries;
        private readonly ILogger<ReportListController> _logger;
        private readonly SqlDataService _sqlDataService;

        public ReportListController(
            IWebHostEnvironment environment, 
            ILogger<ReportListController> logger,
            SqlDataService sqlDataService)
        {
            _environment = environment;
            _logger = logger;
            _sqlDataService = sqlDataService;
            LoadReportsMetadata();
            LoadReportQueries();
        }

        private void LoadReportsMetadata()
        {
            string xmlPath = Path.Combine(_environment.ContentRootPath, "Resources", "ReportsMeta.xml");
            _logger.LogInformation("Loading reports metadata from {XmlPath}", xmlPath);
            var serializer = new XmlSerializer(typeof(ReportsMetadata));
            using (var reader = new StreamReader(xmlPath))
            {
                _reportsMetadata = (ReportsMetadata)serializer.Deserialize(reader);
            }
            _logger.LogInformation("Loaded {CategoryCount} categories with {ReportCount} reports", 
                _reportsMetadata.Categories.CategoryList.Count,
                _reportsMetadata.Categories.CategoryList.Sum(c => c.Reports?.Count ?? 0));
        }

        private void LoadReportQueries()
        {
            string xmlPath = Path.Combine(_environment.ContentRootPath, "Resources", "ReportQueries.xml");
            _logger.LogInformation("Loading report queries from {XmlPath}", xmlPath);
            var serializer = new XmlSerializer(typeof(ReportQueries));
            using (var reader = new StreamReader(xmlPath))
            {
                _reportQueries = (ReportQueries)serializer.Deserialize(reader);
            }
            _logger.LogInformation("Loaded {QueryCount} report queries", _reportQueries.Reports.Count);
        }

        private List<object> GetReportFiltersList(string reportName)
        {
            var query = _reportQueries.Reports
                .FirstOrDefault(q => q.Name.Equals(reportName, StringComparison.OrdinalIgnoreCase));

            if (query?.WhereClauseTemplates?.Templates == null)
            {
                return new List<object>();
            }

            return query.WhereClauseTemplates.Templates.Select(t => new
            {
                name = t.Name,
                type = t.GetFilterType(),
                template = t.Template
            }).ToList<object>();
        }

        [HttpGet]
        public ActionResult<object> GetReports([FromQuery] bool includeFilters = false)
        {
            var reports = _reportsMetadata.Categories.CategoryList
                .SelectMany(c => c.Reports)
                .Select(r => new
                {
                    name = r.Name,
                    displayName = r.DisplayName,
                    description = r.Description,
                    category = _reportsMetadata.Categories.CategoryList
                        .FirstOrDefault(c => c.Reports.Contains(r))?.Name,
                    filters = includeFilters ? _reportQueries.Reports
                        .FirstOrDefault(q => q.Name.Equals(r.Name, StringComparison.OrdinalIgnoreCase))
                        ?.WhereClauseTemplates?.Templates?.Select(t => new
                        {
                            name = t.Name,
                            type = t.GetFilterType(),
                            template = t.Template
                        })?.ToList<object>() ?? new List<object>() : null
                })
                .ToList();

            return Ok(new { reports });
        }

        [HttpGet("categories")]
        public ActionResult<IEnumerable<string>> GetCategories()
        {
            return Ok(_reportsMetadata.Categories.CategoryList.Select(c => c.Name));
        }

        [HttpGet("category/{categoryName}")]
        public ActionResult<IEnumerable<ReportInfo>> GetReportsByCategory(string categoryName)
        {
            var category = _reportsMetadata.Categories.CategoryList
                .FirstOrDefault(c => c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase));
            
            if (category == null)
            {
                return NotFound($"Category '{categoryName}' not found");
            }

            return Ok(category.Reports);
        }

        [HttpGet("report/{reportName}/filters")]
        public IActionResult GetReportFilters(string reportName = "QualityCheckReport")
        {
            try
            {
                var report = _reportQueries.Reports.FirstOrDefault(r => r.Name.Equals(reportName, StringComparison.OrdinalIgnoreCase));
                if (report == null)
                {
                    return NotFound($"Report '{reportName}' not found");
                }

                var filters = report.WhereClauseTemplates.Templates.Select(t => new
                {
                    name = t.GetFilterName(),
                    type = t.GetFilterType(),
                    template = t.Template
                }).ToList();

                return Ok(filters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting report filters");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("report/{reportName?}/query")]
        public ActionResult<object> GetReportQuery(string reportName = "BoxMaintenanceReport", [FromBody] FilterSelection filters = null)
        {
            _logger.LogInformation("Executing query for report {ReportName}", reportName);
            
            var query = _reportQueries.Reports.FirstOrDefault(r => r.Name.Equals(reportName, StringComparison.OrdinalIgnoreCase));
            
            if (query == null)
            {
                _logger.LogWarning("Query for report '{ReportName}' not found", reportName);
                return NotFound($"Query for report '{reportName}' not found");
            }

            var filterParams = ReportQuery.ConvertFilterSelectionToParams(filters);
            _logger.LogDebug("Applied {FilterCount} filters to report {ReportName}", filterParams.Count, reportName);
            
            string generatedQuery = query.BuildQuery(filterParams);
            _logger.LogInformation("Successfully generated query for report {ReportName}", reportName);
            _logger.LogInformation("Generated query: \n{GeneratedQuery}", generatedQuery);
            // Clean up any excessive whitespace for the clean version
            string cleanQuery = Regex.Replace(generatedQuery, @"\s+", " ").Trim();

            var availableFilters = query.WhereClauseTemplates?.Templates?.Select(t => new
            {
                name = t.Name,
                type = t.GetFilterType(),
                template = t.Template
            }) ?? Enumerable.Empty<object>();

            
            return Ok(new
            {
                GeneratedQuery = cleanQuery, // Return a clean, single-line version without excess whitespace
                GroupBy = query.GroupBy,
                ReportPath = query.ReportPath,
                AvailableFilters = availableFilters,
                SelectedFilters = filters
            });
        }

        /// <summary>
        /// Loads a report with the given parameters
        /// </summary>
        [HttpPost("report/{reportName?}/load")]
        public IActionResult LoadReport(
            string reportName = "BoxMaintenanceReport", 
            [FromBody] FilterSelection filters = null,
            [FromQuery] string exportFormat = "PDF")
        {
            _logger.LogInformation("LoadReport called for {ReportName} in {Format} format", reportName, exportFormat);
            
            // Find the report definition
            var query = _reportQueries.Reports.FirstOrDefault(r => r.Name.Equals(reportName, StringComparison.OrdinalIgnoreCase));
            
            if (query == null)
            {
                _logger.LogWarning("Report '{ReportName}' not found", reportName);
                return NotFound($"Report '{reportName}' not found");
            }

            try
            {
                // Convert filters and build the query
                var filterParams = ReportQuery.ConvertFilterSelectionToParams(filters);
                string generatedQuery = query.BuildQuery(filterParams);
                _logger.LogInformation("Generated query for report {ReportName}", reportName);

                // Get the report metadata
                var reportMetadata = _reportsMetadata.Categories.CategoryList
                    .SelectMany(c => c.Reports)
                    .FirstOrDefault(r => r.Name.Equals(reportName, StringComparison.OrdinalIgnoreCase));

                if (reportMetadata == null)
                {
                    _logger.LogWarning("Report metadata for '{ReportName}' not found", reportName);
                    return NotFound($"Report metadata for '{reportName}' not found");
                }

                // Generate the report using SqlDataService
                var result = _sqlDataService.GenerateReportFromQuery(
                    generatedQuery,
                    query.ReportPath,
                    reportName,
                    reportMetadata.DisplayName,
                    exportFormat,
                    filterParams
                );

                _logger.LogInformation("Successfully generated report for {ReportName}", reportName);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating report for {ReportName}", reportName);
                return StatusCode(500, $"Error generating report: {ex.Message}");
            }
        }
    }
} 