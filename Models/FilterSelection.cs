using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace CrystalWebApp.Models
{
    [Serializable]
    public class FilterSelection
    {
        public List<string> Companies { get; set; } = new List<string>();
        public List<string> Branches { get; set; } = new List<string>();
        public List<string> Lines { get; set; } = new List<string>();
        public List<string> BuyMonths { get; set; } = new List<string>();
        public List<string> WorkOrders { get; set; } = new List<string>();
        public List<string> CutNos { get; set; } = new List<string>();
        public List<string> POs { get; set; } = new List<string>();
        public List<string> StyleNos { get; set; } = new List<string>();
        public List<string> Colors { get; set; } = new List<string>();
        public List<string> Sizes { get; set; } = new List<string>();
        private string? _startDate;
        [DefaultValue("2025-03-01")]
        public string? StartDate 
        { 
            get => _startDate ?? "2025-03-01";
            set => _startDate = value;
        }
        private string? _endDate;
        [DefaultValue("2025-03-25")]
        public string? EndDate 
        { 
            get => _endDate ?? "2025-03-25";
            set => _endDate = value;
        }
        [DefaultValue("")]
        public string? ExportFormat { get; set; }
        [DefaultValue("")]
        public string? LastHour { get; set; }
        [DefaultValue("")]
        public string? PrePackingListNames { get; set; }
        
        public List<string> ColorCodes { get; set; } = new List<string>();
        public List<string> Customers { get; set; } = new List<string>();
        public List<string> PackStations { get; set; } = new List<string>();
        public List<string> SectionCodes { get; set; } = new List<string>();
        public List<string> WorkerCodes { get; set; } = new List<string>();
        public List<string> MachineCodes { get; set; } = new List<string>();
        public List<string> OperationDescriptions { get; set; } = new List<string>();
        public List<string> OperationCodes { get; set; } = new List<string>();
        public List<int> MachineRounds { get; set; } = new List<int>();
        public List<string> FaultCodes { get; set; } = new List<string>();
        public List<string> Warehouses { get; set; } = new List<string>();
        public List<string> CustomerNames { get; set; } = new List<string>();
        public List<string> DepartmentDescriptions { get; set; } = new List<string>();
        public List<string> DepartmentNames { get; set; } = new List<string>();
    }
} 