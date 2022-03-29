using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public class CurrentWork
    {
        public CurrentWork()
        {

        }

        public CurrentWork(string idCurrentWork, string location, DateTime startDate, DateTime endDate, string description, string weatherDescription, double temp, double windSpeed, double clouds)
        {
            IdCurrentWork = idCurrentWork;
            Location = location;
            StartDate = startDate;
            EndDate = endDate;
            Description = description;

            WeatherDescription = weatherDescription;
            Temp = temp;
            WindSpeed = windSpeed;
            Clouds = clouds;
        }

        public string IdCurrentWork { get; set; }
        public string Location { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Description { get; set; }


        public string WeatherDescription { get; set; }
        public double Temp { get; set; }
        public double WindSpeed { get; set; }
        public double Clouds { get; set; }

    }
}
