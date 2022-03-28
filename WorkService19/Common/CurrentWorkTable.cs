using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public class CurrentWorkTable : TableEntity
    {
        public CurrentWorkTable()
        {

        }

        public CurrentWorkTable(string idCurrentWork, string location, DateTime startDate, DateTime endDate, string description, bool historyData)
        {
            RowKey = idCurrentWork;
            PartitionKey = "CurrentWorkData";

            IdCurrentWork = idCurrentWork;
            Location = location;
            StartDate = startDate;
            EndDate = endDate;
            Description = description;

            HistoryData = historyData;
        }

        public string IdCurrentWork { get; set; }
        public string Location { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Description { get; set; }
        public bool HistoryData { get; set; }
    }
}
