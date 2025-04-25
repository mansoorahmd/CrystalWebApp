using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

namespace CrystalWebApp.Utilities
{
    /// <summary>
    /// Service for executing SQL queries and filling DataAdapters/DataTables
    /// </summary>
    public class SqlDataService
    {
        private readonly IConfiguration _configuration;
        private readonly Serilog.ILogger _logger;
        private readonly string _connectionString;

        public SqlDataService(IConfiguration configuration, Serilog.ILogger logger)
        {
            _configuration = configuration;
            _logger = logger;
            _connectionString = _configuration.GetConnectionString("CrystalReports") ?? throw new InvalidOperationException("Database connection string 'CrystalReports' not found in configuration.");
            
        }

        /// <summary>
        /// Executes a SQL query and returns the results as a DataTable
        /// </summary>
        /// <param name="sqlQuery">The SQL query to execute</param>
        /// <returns>DataTable containing the query results</returns>
        public DataTable ExecuteQuery(string sqlQuery)
        {
            if (string.IsNullOrEmpty(sqlQuery))
            {
                throw new ArgumentException("SQL query cannot be null or empty", nameof(sqlQuery));
            }

            _logger.Information("Executing SQL query: {QueryStart}", sqlQuery);

            var dataTable = new DataTable();
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    _logger.Information("Database connection opened");

                    using (var command = new SqlCommand(sqlQuery, connection))
                    {
                        command.CommandTimeout = _configuration.GetValue<int>("SqlCommandTimeout", 120);
                        
                        using (var adapter = new SqlDataAdapter(command))
                        {
                            adapter.Fill(dataTable);
                        }
                    }
                }
                
                _logger.Information("Query executed successfully. Rows returned: {RowCount}", dataTable.Rows.Count);
                return dataTable;
                    // log row count
            }
            catch (Exception ex) when (ex is SqlException)
            {
                _logger.Error(ex, "SQL exception occurred while executing query");
                throw new ApplicationException($"Database error: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error executing SQL query");
                throw new ApplicationException($"Error executing query: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Asynchronously executes a SQL query and returns the results as a DataTable
        /// </summary>
        /// <param name="sqlQuery">The SQL query to execute</param>
        /// <returns>Task containing DataTable with the query results</returns>
        public async Task<DataTable> ExecuteQueryAsync(string sqlQuery)
        {
            if (string.IsNullOrEmpty(sqlQuery))
            {
                throw new ArgumentException("SQL query cannot be null or empty", nameof(sqlQuery));
            }

            _logger.Information("Executing SQL query asynchronously: {QueryStart}...", sqlQuery.Substring(0, Math.Min(100, sqlQuery.Length)));

            var dataTable = new DataTable();
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    _logger.Information("Database connection opened asynchronously");

                    using (var command = new SqlCommand(sqlQuery, connection))
                    {
                        command.CommandTimeout = _configuration.GetValue<int>("SqlCommandTimeout", 120); // Default 2 minutes
                        
                        using (var adapter = new SqlDataAdapter(command))
                        {
                            adapter.Fill(dataTable); // SqlDataAdapter doesn't have async methods, so using standard Fill
                            _logger.Information("Query executed successfully. Rows returned: {RowCount}", dataTable.Rows.Count);
                        }
                    }
                }
                return dataTable;
            }
            catch (Exception ex) when (ex is SqlException)
            {
                _logger.Error(ex, "SQL exception occurred while executing query asynchronously");
                throw new ApplicationException($"Database error: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error executing SQL query asynchronously");
                throw new ApplicationException($"Error executing query: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Executes a SQL query and fills an existing DataTable
        /// </summary>
        /// <param name="sqlQuery">The SQL query to execute</param>
        /// <param name="dataTable">The DataTable to fill with results</param>
        public void FillDataTable(string sqlQuery, DataTable dataTable)
        {
            if (string.IsNullOrEmpty(sqlQuery))
            {
                throw new ArgumentException("SQL query cannot be null or empty", nameof(sqlQuery));
            }

            if (dataTable == null)
            {
                throw new ArgumentNullException(nameof(dataTable), "DataTable cannot be null");
            }

            _logger.Information("Filling DataTable with SQL query: {QueryStart}...", sqlQuery.Substring(0, Math.Min(100, sqlQuery.Length)));

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    _logger.Information("Database connection opened");

                    using (var command = new SqlCommand(sqlQuery, connection))
                    {
                        command.CommandTimeout = _configuration.GetValue<int>("SqlCommandTimeout", 120); // Default 2 minutes
                        
                        using (var adapter = new SqlDataAdapter(command))
                        {
                            adapter.Fill(dataTable);
                            _logger.Information("DataTable filled successfully. Rows: {RowCount}", dataTable.Rows.Count);
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is SqlException)
            {
                _logger.Error(ex, "SQL exception occurred while filling DataTable");
                throw new ApplicationException($"Database error while filling DataTable: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error filling DataTable");
                throw new ApplicationException($"Error filling DataTable: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets a SqlDataAdapter for the given query
        /// </summary>
        /// <param name="sqlQuery">The SQL query to execute</param>
        /// <returns>Configured SqlDataAdapter</returns>
        public SqlDataAdapter GetDataAdapter(string sqlQuery)
        {
            if (string.IsNullOrEmpty(sqlQuery))
            {
                throw new ArgumentException("SQL query cannot be null or empty", nameof(sqlQuery));
            }

            _logger.Information("Creating SqlDataAdapter for query: {QueryStart}...", sqlQuery.Substring(0, Math.Min(100, sqlQuery.Length)));

            try
            {
                var connection = new SqlConnection(_connectionString);
                connection.Open();
                _logger.Information("Database connection opened");

                var command = new SqlCommand(sqlQuery, connection);
                command.CommandTimeout = _configuration.GetValue<int>("SqlCommandTimeout", 120); // Default 2 minutes
                
                var adapter = new SqlDataAdapter(command);
                
                // Set cleanup behavior - will close the connection when disposed
                adapter.Disposed += (sender, args) => 
                {
                    command.Dispose();
                    connection.Dispose();
                    _logger.Information("SqlDataAdapter and associated resources cleaned up");
                };
                
                return adapter;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error creating SqlDataAdapter");
                throw new ApplicationException($"Error creating SqlDataAdapter: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Generates a report from a SQL query using Crystal Reports
        /// </summary>
        /// <param name="sqlQuery">The SQL query to execute</param>
        /// <param name="reportPath">Path to the Crystal Report file (.rpt)</param>
        /// <param name="reportName">Name of the report for file naming</param>
        /// <param name="friendlyName">Display name of the report</param>
        /// <param name="exportFormat">Format to export (PDF, EXCEL, etc.)</param>
        /// <param name="filterParams">Optional filter parameters for display in report header</param>
        /// <returns>ActionResult containing the exported report</returns>
        public ActionResult GenerateReportFromQuery(string sqlQuery, string reportPath, string reportName, 
            string friendlyName, string exportFormat, Dictionary<string, List<string>> filterParams = null)
        {
            try
            {
                _logger.Information("Generating report from query: {ReportName}", reportName);
                
                // Execute the query to get the data
                var dataTable = ExecuteQuery(sqlQuery);

                return new ContentResult
                {
                    Content = "Report generation not implemented yet.",
                    ContentType = "text/plain"
                };


            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error generating report from query: {ReportName}", reportName);
                return new ContentResult 
                { 
                    Content = $"Error generating report: {ex.Message}",
                    ContentType = "text/plain"
                };
            }
        }

        /// <summary>
        /// Asynchronously generates a report from a SQL query using Crystal Reports
        /// </summary>
        /// <param name="sqlQuery">The SQL query to execute</param>
        /// <param name="reportPath">Path to the Crystal Report file (.rpt)</param>
        /// <param name="reportName">Name of the report for file naming</param>
        /// <param name="friendlyName">Display name of the report</param>
        /// <param name="exportFormat">Format to export (PDF, EXCEL, etc.)</param>
        /// <param name="filterParams">Optional filter parameters for display in report header</param>
        /// <returns>Task containing ActionResult with the exported report</returns>
        public async Task<ActionResult> GenerateReportFromQueryAsync(string sqlQuery, string reportPath, string reportName, 
            string friendlyName, string exportFormat, Dictionary<string, List<string>> filterParams = null)
        {
            try
            {
                _logger.Information("Generating report from query asynchronously: {ReportName}", reportName);
                
                // Execute the query to get the data
                var dataTable = await ExecuteQueryAsync(sqlQuery);

                // Generate the report using the ReportService
                return new ContentResult
                {
                    Content = "Report generation not implemented yet.",
                    ContentType = "text/plain"
                };

                // Note: The actual report generation logic would go here, using the ReportService
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error generating report from query asynchronously: {ReportName}", reportName);
                return new ContentResult 
                { 
                    Content = $"Error generating report: {ex.Message}",
                    ContentType = "text/plain"
                };
            }
        }
    }
} 