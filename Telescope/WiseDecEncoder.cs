﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MccDaq;
using ASCOM.Wise40.Common;
using ASCOM.Wise40.Hardware;

namespace ASCOM.Wise40.Telescope
{
    public class WiseDecEncoder : WiseObject, IConnectable, IDisposable, IEncoder
    {
        private /*uint*/ double _daqsValue;

        private WiseEncoder axisEncoder, wormEncoder;

        private Astrometry.NOVAS.NOVAS31 Novas31;
        private Astrometry.AstroUtils.AstroUtils astroutils;

        private bool _connected = false;

        public Angle _angle;
        private const double halfPI = Math.PI / 2.0;
        private const double twoPI = Math.PI * 2.0;

        const double DecMultiplier = twoPI / 600 / 4096;
        const double DecCorrection = 0.35613322;                //20081231 SK: ActualDec-Encoder Dec [rad]

        private Common.Debugger debugger = Debugger.Instance;
        private Hardware.Hardware hw = Hardware.Hardware.Instance;
        private static WiseSite wisesite = WiseSite.Instance;

        private Object _lock = new Object();

        public WiseDecEncoder(string name)
        {
            Name = "DecEncoder";
            Novas31 = new Astrometry.NOVAS.NOVAS31();
            astroutils = new Astrometry.AstroUtils.AstroUtils();
            wisesite.init();

            axisEncoder = new WiseEncoder("DecAxis",
                1 << 16,
                new List<WiseEncSpec>() {
                    new WiseEncSpec() { brd = hw.teleboard, port = DigitalPortType.SecondPortA, mask = 0xff }, // [0]
                    new WiseEncSpec() { brd = hw.teleboard, port = DigitalPortType.SecondPortB, mask = 0xff }, // [1]
                }
            );

            wormEncoder = new WiseEncoder("DecWorm",
                1 << 12,
                new List<WiseEncSpec>() {
                    new WiseEncSpec() { brd = hw.teleboard, port = DigitalPortType.SecondPortCL, mask = 0x0f }, // [0]
                    new WiseEncSpec() { brd = hw.teleboard, port = DigitalPortType.FirstPortA,   mask = 0xff }, // [1]
                }
            );

            Name = name;

            _angle = Simulated ?
                Angle.FromDegrees(90.0, Angle.Type.Dec) - wisesite.Latitude :
                Angle.FromRadians((Value * DecMultiplier) + DecCorrection, Angle.Type.Dec);
        }

        public double Declination
        {
            get
            {
                return Degrees;
            }
        }

        public void Connect(bool connected)
        {
            axisEncoder.Connect(connected);
            wormEncoder.Connect(connected);

            _connected = connected;
        }

        public bool Connected
        {
            get
            {
                return _connected;
            }
        }

        public void Dispose() {
            axisEncoder.Dispose();
            wormEncoder.Dispose();
        }

        /// <summary>
        /// Reads the axis and worm encoders
        /// </summary>
        /// <returns>Combined Daq values</returns>
        public double Value
        {
            get
            {
                if (!Simulated)
                {
                    #region Delphi
                    ////DecWormDAC is 12bit: lower nibble of boardAport0+ boardAport1
                    //
                    // procedure GetCoord(var HA, RA, HA_Enc, Dec, Dec_Enc :extended [double];
                    // var HA_last, RA_last, Dec_last : extended [double];
                    // var BAP0, BAP1, BAP2, BAP3, BBP0, BBP1, BBP2, BBP3: integer [int];
                    // var HAWormEnc, HAAxisEnc, DecWormEnc, DecAxisEnc: longint [int]);
                    //
                    // DecWormEnc:= (BBP1 AND $000F)*$100 + BBP0;
                    // DecAxisEnc:= ((BBP2 AND $00FF) div $10) +($10 * BBP3); //missing 4140 div 16
                    // DecEnc:= ((DecAxisEnc * 600 + DecWormEnc) AND $FFF000) -DecWormEnc; //MASK lower 12 bits of DecAxisDAC
                    // Dec_Enc:= DecEnc * 2.5566346464760687161968126491532e-6;  //2*pi/600/4096
                    // Dec:= Dec_Enc + DecCorrection;
                    // if (Dec > pi) then
                    //   Dec := Dec - pi2;
                    #endregion

                    int worm, axis;

                    lock (_lock)
                    {
                        List<int> wormValues = wormEncoder.RawValuesInt;
                        List<int> axisValues = axisEncoder.RawValuesInt;

                        //worm = ((wormValues[0] & 0xf) << 8) | (wormValues[1] & 0xff);
                        worm = ((wormValues[0] & 0xf) * 0x100) + (wormValues[1] & 0xff);
                        //axis = (axisValues[1] >> 4) | (axisValues[0] << 4);
                        axis = (axisValues[1] / 0x10) + (axisValues[0] * 0x10);

                        _daqsValue = ((axis * 600 + worm) & 0xfff000) - worm;
                    }
                    #region debug
                    debugger.WriteLine(Debugger.DebugLevel.DebugAxes,
                        "{0}: _daqsValue: {1}, axis: {2}, worm: {3}", 
                        Name, _daqsValue, axis, worm);
                    #endregion
                }
                return _daqsValue;
            }

            set
            {
                if (Simulated)
                    _daqsValue = value;
            }
        }

        public Angle Angle
        {
            get
            {

                if(! Simulated)
                    _angle.Radians = (Value * DecMultiplier) + DecCorrection;

                Angle ret = _angle;

                if (FlippedOver90Degrees)
                    ret.Radians = Math.PI - ret.Radians;

                return ret;
            }

            set
            {
                if (Simulated)
                {
                    _angle.Radians = value.Radians;
                    Value = (uint) Math.Round((_angle.Radians - DecCorrection) / DecMultiplier);
                }
            }
        }

        public bool FlippedOver90Degrees
        {
            get
            {
                return false;
                
                //bool flipped = _angle.Radians > halfPI;

                //#region debug
                //debugger.WriteLine(Debugger.DebugLevel.DebugAxes,
                //    "FlippedOver90Degrees: radians: {0}, ret: {1}", _angle.Radians, flipped);
                //#endregion
                //return flipped;
            }
        }

        public double Degrees
        {
            get
            {
                Angle ret = _angle;

                if (!Simulated)
                {
                    double current_value = Value;
                    double radians = (current_value * DecMultiplier) + DecCorrection;

                    if (radians > Math.PI)
                        radians -= twoPI;
                    _angle.Radians = radians;

                    ret = _angle;
                    if (FlippedOver90Degrees)
                    {
                        #region debug
                        debugger.WriteLine(Debugger.DebugLevel.DebugAxes, "WiseDecEncoder: Flipped");
                        #endregion
                        ret.Radians = Math.PI - ret.Radians;
                    }

                    #region debug
                    debugger.WriteLine(Debugger.DebugLevel.DebugEncoders,
                        "[{0}] {1} Degrees - Value: {2}, deg: {3}", this.GetHashCode(), Name, current_value, ret);
                    #endregion
                }

                return ret.Degrees;
            }

            set
            {
                _angle.Degrees = value;
                if (Simulated)
                {
                    _daqsValue = /*(uint)*/ ((_angle.Radians - DecCorrection) / DecMultiplier);
                }
            }
        }
    }
}
