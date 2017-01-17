﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ASCOM.Wise40.Hardware;
using ASCOM.Wise40.Common;
using ASCOM.Astrometry.AstroUtils;
using System.Threading;

namespace ASCOM.Wise40
{
    public class DomeSlaveDriver: IConnectable
    {
        private static WiseDome wisedome = WiseDome.Instance;
        private static WiseTele wisetele = WiseTele.Instance;
        private bool _connected = false;
        private ASCOM.Astrometry.NOVAS.NOVAS31 novas31;
        private AstroUtils astroutils;
        AutoResetEvent _arrivedAtAz = new AutoResetEvent(false);
        private Debugger debugger;

        public static readonly DomeSlaveDriver instance = new DomeSlaveDriver(); // Singleton
        private static WiseSite wisesite = WiseSite.Instance;

        private bool _initialized = false;

        // Explicit static constructor to tell C# compiler
        // not to mark type as beforefieldinit
        static DomeSlaveDriver()
        {
        }

        public DomeSlaveDriver()
        {
        }

        public static DomeSlaveDriver Instance
        {
            get
            {
                return instance;
            }
        }

        public void init()
        {
            if (_initialized)
                return;

            debugger = Debugger.Instance;
            instance.novas31 = new Astrometry.NOVAS.NOVAS31();
            instance.astroutils = new AstroUtils();
            _arrivedAtAz = new AutoResetEvent(false);
            wisedome.init();
            wisedome.SetArrivedAtAzEvent(_arrivedAtAz);
            wisesite.init();

            _initialized = true;
            #region debug
            debugger.WriteLine(Debugger.DebugLevel.DebugAxes, "DomeSlaveDriver: init() done.");
            #endregion
        }

        public bool Connected
        {
            get
            {
                return _connected;
            }
        }

        public void Connect(bool value)
        {
            init();
            wisedome.Connect(value);
            _connected = value;
        }

        public void SlewToAz(double az)
        {
            if (wisetele == null)
                wisetele = WiseTele.Instance;

            #region debug
            debugger.WriteLine(Debugger.DebugLevel.DebugAxes, "DomeSlaveDriver: Asking dome to SlewToAzimuth({0})", new Angle(az, Angle.Type.Az));
            #endregion
            try
            {
                wisedome.SlewToAzimuth(az);
                #region debug
                debugger.WriteLine(Debugger.DebugLevel.DebugAxes, "DomeSlaveDriver: Waiting for dome to arrive to target az");
                #endregion
                _arrivedAtAz.WaitOne();
            }
            catch (Exception ex)
            {
                wisedome.AbortSlew();
                throw ex;
            }
            #region debug
            debugger.WriteLine(Debugger.DebugLevel.DebugAxes, "DomeSlaveDriver: Dome arrived to {0}", new Angle(az, Angle.Type.Az));
            #endregion
        }

        public void Park()
        {
            if (wisetele == null)
                wisetele = WiseTele.Instance;

            #region debug
            debugger.WriteLine(Debugger.DebugLevel.DebugAxes, "DomeSlaveDriver: Asking dome to Park");
            #endregion
            try
            {
                wisedome.Park();
                #region debug
                debugger.WriteLine(Debugger.DebugLevel.DebugAxes, "DomeSlaveDriver: Waiting for dome to arrive to target az");
                #endregion
                _arrivedAtAz.WaitOne();
            }
            catch (Exception ex)
            {
                wisedome.AbortSlew();
                throw ex;
            }

            #region debug
            debugger.WriteLine(Debugger.DebugLevel.DebugAxes, "DomeSlaveDriver: Dome is Parked");
            #endregion
        }

        public void Calibrate()
        {
            if (wisetele == null)
                wisetele = WiseTele.Instance;

            #region debug
            debugger.WriteLine(Debugger.DebugLevel.DebugAxes, "DomeSlaveDriver: Asking dome to findHome");
            #endregion
            try
            {
                wisedome.FindHome();
                #region debug
                debugger.WriteLine(Debugger.DebugLevel.DebugAxes, "DomeSlaveDriver: Waiting for dome to findHome");
                #endregion
            }
            catch (Exception ex)
            {
                wisedome.AbortSlew();
                throw ex;
            }

            #region debug
            debugger.WriteLine(Debugger.DebugLevel.DebugAxes, "DomeSlaveDriver: Dome found Home");
            #endregion
        }

        public void SlewToAz(Angle ra, Angle dec)
        {
            Angle ha = wisesite.LocalSiderealTime - ra; 
            double haRadians = ha.Radians;
            double decRadians = dec.Radians;
            double domeAz = Angle.FromRadians(ScopeCoordToDomeAz(haRadians, decRadians), Angle.Type.Az).Degrees;

            #region debug
            debugger.WriteLine(Debugger.DebugLevel.DebugAxes,
                "DomeSlaveDriver: SlewToAz ra: {0}, dec: {1} => ha: {2}, dec: {1} => {3}",
                ra.ToString(), dec.ToString(), ha.ToString(), domeAz.ToString());
            #endregion
            SlewToAz(domeAz);
        }

        public bool Slewing
        {
            get
            {
                return wisetele.slewers.Active(Slewers.Type.Dome);
            }
        }

        public void AbortSlew()
        {
            wisedome.AbortSlew();
            wisetele.slewers.Delete(Slewers.Type.Dome);
        }

        public string Azimuth
        {
            get
            {
                if (!wisedome.Connected)
                    return "Not connected";
                if (!wisedome.Calibrated)
                    return "Not calibrated";

                return wisedome.Azimuth.ToNiceString();
            }
        }

        public string Status
        {
            get
            {
                if (!Connected)
                    return "Not connected";

                return wisedome.Status;
            }
        }

        public void OpenShutter(bool bypassSafety = false)
        {
            wisedome.OpenShutter(bypassSafety);
        }

        public void CloseShutter()
        {
            wisedome.CloseShutter();
        }

        public void StopShutter()
        {
            wisedome.ShutterStop();
        }

        public string ShutterStatus
        {
            get
            {
                if (!Connected)
                    return "Not connected";

                string state = wisedome.ShutterState.ToString();
                return (state == "shutterError" ? String.Empty : wisedome.ShutterState.ToString().Substring("shutter".Length));
            }
        }

        /// <summary>
        /// Calculates the Dome Azimuth for a GEM scope's (HA, Dec)
        /// Adapted from VisualBasic (see Documents\DomeSync.txt)
        /// </summary>
        /// <param name="HA">Scope's HourAngle</param>
        /// <param name="Dec">Scope's Declination</param>
        /// <returns></returns>
        public double ScopeCoordToDomeAz(double HA, double Dec)
        {
            double phi = wisesite.Latitude.Radians;
            double Xdome0 = 0.0;    // shift of scope pivot point from dome center - X axis
            double Ydome0 = 0.0;    // shift of scope pivot point from dome center - Y axis
            double Zdome0 = 0.0;    // shift of scope pivot point from dome center - Z axis
            double rDecAxis = 1200; // optical axis offset from RA axis
            double rDome = 5000;    // dome radius
            double pi = Math.PI;

            HA += Math.PI;  // From -12..12 to 0..24 hours
            double A = Xdome0 + rDecAxis * Math.Cos(phi - pi / 2.0) * Math.Sin(HA - pi);
            double B = Ydome0 + rDecAxis * Math.Cos(HA - pi);
            double C = Zdome0 - rDecAxis * Math.Sin(phi - pi / 2.0) * Math.Sin(HA - pi);
            double D = Math.Cos(phi - pi / 2.0) * Math.Cos(Dec) * Math.Cos(-HA) + Math.Sin(phi - pi / 2.0) * Math.Sin(Dec);
            double E = Math.Cos(Dec) * Math.Sin(-HA);
            double F = -Math.Sin(phi - pi / 2.0) * Math.Cos(Dec) * Math.Cos(-HA) + Math.Cos(phi - pi / 2.0) * Math.Sin(Dec);

            double knum = -(A * D + B * E + C * F) +
                        Math.Sqrt(Math.Pow(A * D + B * E + C * F, 2) +
                        (Math.Pow(D, 2) + Math.Pow(E, 2) + Math.Pow(F, 2)) * (Math.Pow(rDome, 2) - Math.Pow(A, 2) - Math.Pow(B, 2) - Math.Pow(C, 2)));

            double k = knum / (Math.Pow(D, 2) + Math.Pow(E, 2) + Math.Pow(F, 2));
            double Xdome = A + D * k;
            Xdome = -Xdome;
            double Ydome = B + E * k;
            double Zdome = C + F * k;
            double dome_A = -Math.Atan2(Ydome, Xdome);
            //dome_A = -dome_A;

            if (dome_A < 0)
                dome_A = 2 * Math.PI + dome_A;
            #region debug            
            debugger.WriteLine(Debugger.DebugLevel.DebugAxes, string.Format("ScopeCoordToDomeAz: ha: {0} ({1}), dec: {2} ({3}) => az: {4} ({5})",
                HA.ToString(), Angle.FromRadians(HA, Angle.Type.HA).ToString(),
                Dec.ToString(), Angle.FromRadians(Dec, Angle.Type.Dec).ToString(),
                dome_A.ToString(), Angle.FromRadians(dome_A, Angle.Type.Az).ToNiceString()));
            #endregion
            #region trace
            //wisetele.traceLogger.LogMessage("ScopeCoordToDomeAz", string.Format("ha: {0}, dec: {1} => az: {2}", HA.ToString(), Dec.ToString(), dome_A.ToString()));
            #endregion

            return dome_A;
        }
    }
}
