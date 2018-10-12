﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

using ASCOM.Wise40.Common;
using System.IO;

namespace ASCOM.Wise40
{
    public sealed class ActivityMonitor : WiseObject
    {
        //private static volatile ActivityMonitor _instance = new ActivityMonitor(); // Singleton
        //private static object syncObject = new object();

        // start Singleton
        private static readonly Lazy<ActivityMonitor> lazy = 
            new Lazy<ActivityMonitor>(() => new ActivityMonitor()); // Singleton

        public static ActivityMonitor Instance
        {
            get
            {
                lazy.Value.init();
                return lazy.Value;
            }
        }

        private ActivityMonitor() { }
        // end Singleton

        private Timer inactivityTimer;
        private readonly int realMillisToInactivity = (int)TimeSpan.FromMinutes(15).TotalMilliseconds;
        private readonly int simulatedlMillisToInactivity = (int)TimeSpan.FromMinutes(3).TotalMilliseconds;
        private Debugger debugger = Debugger.Instance;
        private bool _shuttingDown = false;
        private DateTime _due = DateTime.MinValue;                  // not set
        private WiseSite wisesite = WiseSite.Instance;

        [FlagsAttribute]
        public enum Activity
        {
            None = 0,
            Tracking = (1 << 0),
            Slewing = (1 << 1),
            Pulsing = (1 << 2),
            Dome = (1 << 3),
            Handpad = (1 << 4),
            GoingIdle = (1 << 5),
            Parking = (1 << 6),
            Shutter = (1 << 7),
            ShuttingDown = (1 << 8),
            Focuser = (1 << 9),
            FilterWheel = (1 << 10),
        };
        private static Activity _currentlyActive = Activity.None;
        private static List<Activity> _activities = new List<Activity> {
            Activity.Tracking,
            Activity.Slewing,
            Activity.Pulsing,
            Activity.Dome,
            Activity.Handpad,
            Activity.GoingIdle,
            Activity.Parking,
            Activity.Shutter,
            Activity.ShuttingDown,
        };

        public void BecomeIdle(object StateObject)
        {
            EndActivity(Activity.GoingIdle);
        }

        //public static ActivityMonitor Instance
        //{
        //    get
        //    {
        //        if (_instance == null)
        //        {
        //            lock (syncObject)
        //            {
        //                if (_instance == null)
        //                    _instance = new ActivityMonitor();
        //            }
        //        }
        //        _instance.init();
        //        return _instance;
        //    }
        //}

        //public ActivityMonitor() { }
        //static ActivityMonitor() { }

        public void init()
        {
            wisesite.init();
            inactivityTimer = new System.Threading.Timer(BecomeIdle);
            _currentlyActive = Activity.None;
            RestartGoindIdleTimer("init");
        }

        public void StartActivity(Activity act)
        {
            if (_shuttingDown)
                return;

            if (act == Activity.GoingIdle && _currentlyActive != Activity.None)
                return;

            ActivityMonitor._currentlyActive |= act;
            if (act != Activity.GoingIdle)      // Any activity ends GoingIdle
                EndActivity(Activity.GoingIdle);
            #region debug
            debugger.WriteLine(Common.Debugger.DebugLevel.DebugLogic,
                "ActivityMonitor:StartActivity: started {0} (currentlyActive: {1})", act.ToString(), ObservatoryActivities);
            #endregion
            if (act != Activity.GoingIdle)
                StopGoindIdleTimer();
        }

        public void EndActivity(Activity act)
        {
            if (_shuttingDown)
            {
                return;
            }

            _currentlyActive &= ~act;
            #region debug
            debugger.WriteLine(Common.Debugger.DebugLevel.DebugLogic,
                "ActivityMonitor:EndActivity: ended {0} (currentlyActive: {1})", act.ToString(), ObservatoryActivities);
            #endregion

            if (act == Activity.ShuttingDown)
                StopGoindIdleTimer();
            else if (_currentlyActive == Activity.None)
                RestartGoindIdleTimer("no activities");
        }

        public bool Active(Activity a)
        {
            return (_currentlyActive & a) != 0;
        }

        public void StopGoindIdleTimer()
        {
            inactivityTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _due = DateTime.MinValue;
        }

        public void RestartGoindIdleTimer(string reason)
        {
            if (_shuttingDown)
                return;

            // The file's creation time is used in case we crashed after starting to idle.
            string filename = Const.topWise40Directory + "Observatory/ActivityMonitorRestart";
            int dueMillis = Simulated ? simulatedlMillisToInactivity : realMillisToInactivity;

            if (reason == "init")
            {
                if (File.Exists(filename))
                {
                    double fileAgeMillis = DateTime.Now.Subtract(File.GetCreationTime(filename)).TotalMilliseconds;

                    if (fileAgeMillis < dueMillis)
                        dueMillis = Math.Min(dueMillis, (int)fileAgeMillis);
                    File.Delete(filename);
                }
                else
                    Directory.CreateDirectory(Path.GetDirectoryName(filename));
            }
            #region debug
            debugger.WriteLine(Common.Debugger.DebugLevel.DebugLogic, "ActivityMonitor:RestartGoindIdleTimer (reason = {0}, due = {1} millis).",
                reason, dueMillis);
            #endregion

            StartActivity(Activity.GoingIdle);
            inactivityTimer.Change(dueMillis, Timeout.Infinite);
            File.Create(filename).Close();

            _due = DateTime.Now.AddMilliseconds(dueMillis);
        }

        public TimeSpan RemainingTime
        {
            get
            {
                if (_due == DateTime.MinValue)
                    return TimeSpan.MaxValue;
                return _due.Subtract(DateTime.Now);
            }
        }

        public bool ObservatoryIsActive()
        {
            #region debug
            debugger.WriteLine(Debugger.DebugLevel.DebugLogic, "ObservatoryIsActive: {0}", _currentlyActive.ToString());
            #endregion
            return _currentlyActive != Activity.None;
        }

        public string ObservatoryActivities
        {
            get
            {
                List<string> ret = new List<string>();

                foreach (Activity a in _activities)
                {
                    if (!Active(a))
                        continue;

                    if (a == Activity.GoingIdle)
                    {
                        TimeSpan ts = RemainingTime;

                        string s = a.ToString();
                        if (ts != TimeSpan.MaxValue)
                        {
                            s += " in ";
                            if (ts.TotalMinutes > 0)
                                s += string.Format("{0}m", (int)ts.TotalMinutes);
                            s += string.Format("{0}s", ts.Seconds);
                        }
                        ret.Add(s);
                    }
                    else
                        ret.Add(a.ToString());
                }

                return string.Join(", ", ret);
            }
        }
    }
}
