using Common;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HistoryWorkSaver
{
    public class HistoryService : IHistoryService
    {
        public HistoryService()
        {

        }

        public List<CurrentWork> GetAllHistoricalDataFromStorage()
        {
            List<CurrentWork> currentWorks = new List<CurrentWork>();
            CloudStorageAccount _storageAccount;
            CloudTable _table;
            string a = ConfigurationManager.AppSettings["DataConnectionString"];
            _storageAccount = CloudStorageAccount.Parse(a);
            CloudTableClient tableClient = new CloudTableClient(new Uri(_storageAccount.TableEndpoint.AbsoluteUri), _storageAccount.Credentials);
            _table = tableClient.GetTableReference("CurrentWorkDataStorage");
            var results = from g in _table.CreateQuery<CurrentWorkTable>() where g.PartitionKey == "CurrentWorkData" && g.HistoryData select g;
            foreach (CurrentWorkTable currentWorkEntity in results.ToList())
            {
                currentWorks.Add(new CurrentWork(currentWorkEntity.IdCurrentWork, currentWorkEntity.Location, currentWorkEntity.StartDate, currentWorkEntity.EndDate, currentWorkEntity.Description, currentWorkEntity.WeatherDescription, currentWorkEntity.Temp, currentWorkEntity.WindSpeed, currentWorkEntity.Clouds));
            }
            return currentWorks;
        }
    }
}
