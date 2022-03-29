using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace HistoryWorkSaver
{
    [ServiceContract]
    public interface IHistoryService
    {
        [OperationContract]
        List<CurrentWork> GetAllHistoricalDataFromStorage();

    }
}
