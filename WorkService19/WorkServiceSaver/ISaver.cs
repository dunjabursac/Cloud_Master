using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace WorkServiceSaver
{
    [ServiceContract]
    public interface ISaver
    {
        [OperationContract]
        Task<bool> AddCurrentWork(string idCurrentWork, string location, DateTime startDate, DateTime endDate, string description);

        [OperationContract]
        Task<List<CurrentWork>> GetAllData();

        [OperationContract]
        Task<bool> DeleteAllActiveData();

        [OperationContract]
        Task<List<CurrentWork>> GetAllHistoricalData();
    }
}
