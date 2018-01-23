﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;

using ASCOM.Wise40.Common;
using ASCOM.Wise40.Hardware;
using ASCOM.DeviceInterface;

namespace ASCOM.Wise40.Telescope
{
    /// <summary>
    /// A PulserTask performs an Asynchronous PulseGuide on a WiseTelescope
    ///  axis.
    /// </summary>
    public class PulserTask
    {

        public TelescopeAxes _axis;
        public int _duration;
        public WiseVirtualMotor _motor;
        public Task task;
        private Common.Debugger debugger;

        public PulserTask()
        {
            debugger = Common.Debugger.Instance;
        }

        public override string ToString()
        {
            return _axis.ToString() + "_PulseGuideTask";
        }

        public void Run()
        {
            Stopwatch sw = new Stopwatch();

            sw.Start();
            _motor.SetOn(Const.rateGuide);
            while (sw.ElapsedMilliseconds < _duration)
            {
                if (Pulsing.pulseGuideCTS.IsCancellationRequested)
                {
                    #region debug
                    debugger.WriteLine(Common.Debugger.DebugLevel.DebugLogic,
                        "PulserTask on {0} aborted after {1} millis.", _axis.ToString(), sw.ElapsedMilliseconds);
                    #endregion
                    break;
                }
                Thread.Sleep(10);
            }
            _motor.SetOff();
            sw.Stop();
        }
    }

    public class Pulsing
    {

        public Common.Debugger debugger;
        private Object _lock = new object();
        private static volatile Pulsing _instance;
        private static object syncObject = new object();
        private static List<PulserTask> _active = new List<PulserTask>();
        private static WiseTele wisetele;

        public static Dictionary<GuideDirections, TelescopeAxes> guideDirection2Axis = new Dictionary<GuideDirections, TelescopeAxes>
        {
            {GuideDirections.guideEast, TelescopeAxes.axisPrimary },
            {GuideDirections.guideWest, TelescopeAxes.axisPrimary },
            {GuideDirections.guideNorth, TelescopeAxes.axisSecondary },
            {GuideDirections.guideSouth, TelescopeAxes.axisSecondary },
        };

        private static Dictionary<GuideDirections, WiseVirtualMotor> guideDirection2Motor;

        public void Init()
        {
            debugger = Common.Debugger.Instance;
            wisetele = WiseTele.Instance;
            guideDirection2Motor = new Dictionary<GuideDirections, WiseVirtualMotor>();
            guideDirection2Motor[GuideDirections.guideEast] = wisetele.EastMotor;
            guideDirection2Motor[GuideDirections.guideWest] = wisetele.WestMotor;
            guideDirection2Motor[GuideDirections.guideNorth] = wisetele.NorthMotor;
            guideDirection2Motor[GuideDirections.guideSouth] = wisetele.SouthMotor;
        }

        public static CancellationTokenSource pulseGuideCTS = new CancellationTokenSource();
        private static CancellationToken pulseGuideCT;

        public Pulsing() { }

        static Pulsing() { }

        public static Pulsing Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (syncObject)
                    {
                        if (_instance == null)
                            _instance = new Pulsing();
                    }
                }
                return _instance;
            }
        }

        public void Abort()
        {
            pulseGuideCTS.Cancel();
            pulseGuideCTS = new CancellationTokenSource();
            #region debug
            debugger.WriteLine(Common.Debugger.DebugLevel.DebugLogic, "Aborted PulseGuiding");
            #endregion
        }

        public void Start(GuideDirections direction, int duration)
        {
            PulserTask pulserTask;

            pulserTask = new PulserTask() {
                _axis = guideDirection2Axis[direction],
                _duration = duration,
                _motor = guideDirection2Motor[direction],
                task = null,
            };
            pulseGuideCT = pulseGuideCTS.Token;
            string before = _active.ToString();

            _active.Add(pulserTask);
            pulserTask.task = Task.Run(() =>
            {
                try
                {
                    pulserTask.Run();
                }
                catch (Exception ex)
                {
                    #region debug
                    debugger.WriteLine(Common.Debugger.DebugLevel.DebugLogic,
                        "Caught exception: {0}, aborting pulse guiding", ex.Message);
                    #endregion
                    Abort();
                    _active.Remove(pulserTask);
                }
            }, pulseGuideCT).ContinueWith((t) =>
            {
                #region debug
                debugger.WriteLine(Common.Debugger.DebugLevel.DebugLogic,
                    "pulser \"{0}\" on {1} completed with status: {2}",
                    t.ToString(), pulserTask._axis.ToString(), t.Status.ToString());
                #endregion
                _active.Remove(pulserTask);
            }, TaskContinuationOptions.ExecuteSynchronously);

            #region debug
            debugger.WriteLine(Common.Debugger.DebugLevel.DebugAxes,
                "Pulsers:Add: added {0}, \"{1}\" => \"{2}\"", pulserTask.ToString(), before, this.ToString());
            #endregion
        }

        public void Delete(TelescopeAxes axis)
        {
            string before;
            PulserTask pulserTask;

            lock (_lock)
            {
                before = ToString();

                pulserTask = _active.Find((s) => s._axis == axis);
                _active.Remove(pulserTask);
            }
            #region debug
            debugger.WriteLine(Common.Debugger.DebugLevel.DebugAxes,
                "ActiveSlewers: deleted {0}, \"{1}\" => \"{2}\" ({3})", axis.ToString(), before, this.ToString(), _active.GetHashCode());
            #endregion
        }

        public override string ToString()
        {
            List<string> s = new List<string>();
            lock (_lock)
            {
                foreach (var a in _active)
                    s.Add(a.ToString());
            }

            return string.Join(",", s.ToArray());
        }

        public bool Active(TelescopeAxes axis)
        {
            bool ret = false;

            lock (_lock)
                foreach (var a in _active)
                {
                    if (a._axis == axis)
                    {
                        ret = true;
                        break;
                    }
                }
            return ret;
        }

        public bool Active(GuideDirections direction)
        {
            return Active(guideDirection2Axis[direction]);
        }

        public bool Active()
        {
            return _active.Count != 0;
        }

        public void Clear()
        {
            lock (_lock)
                _active.Clear();
        }

        public Task[] ToArray()
        {
            List<Task> tasks = new List<Task>();

            foreach (var a in _active)
                tasks.Add(a.task);
            return tasks.ToArray();
        }

        public static bool IsPulseGuising
        {
            get
            {
                return _active.Count != 0;
            }
        }
    }
}