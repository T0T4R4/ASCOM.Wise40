﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ASCOM.Wise40.Common;

namespace ASCOM.Wise40
{
    public abstract class WeatherStation: WiseObject
    {
        public enum WeatherStationVendor { DavisInstruments, Boltwood };
        public enum WeatherStationModel { VantagePro2, CloudSensorII };
        public enum WeatherStationInputMethod
        {
            ClarityII,
            WeatherLink_HtmlReport,
            Weizmann_TBD,
            Korean_TBD
        };

        public int _unitId;

        public abstract WeatherStationVendor Vendor
        {
            get;
        }

        public abstract WeatherStationModel Model
        {
            get;
        }

        //public abstract string RawData
        //{
        //    get;
        //}

        public abstract bool Enabled
        {
            get;
            set;
        }

        public abstract WeatherStationInputMethod InputMethod
        {
            get;
            set;
        }
    }
}
