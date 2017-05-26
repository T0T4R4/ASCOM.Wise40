﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ASCOM.Wise40.Hardware;
using ASCOM.Wise40.Common;
using MccDaq;
using System.Threading;


namespace ASCOM.Wise40
{
    public class WiseFocuserEnc : WiseEncoder, IDisposable
    {
        private static readonly WiseFocuserEnc instance = new WiseFocuserEnc();
        private bool _initialized = false;

        private WisePin pinLatch;
        private Hardware.Hardware hardware = Hardware.Hardware.Instance;
        private static uint _simulatedValue;
        private static Const.Direction _simulatedDirection;
        private static Timer _simulationTimer = new Timer(new TimerCallback(simulateMovement));
        private static uint _simulatedStep = 1;

        //
        // 17 Jan 2017 - Arie Blumenzweig
        //  The code below is for the Posital/Fraba multi-turn parallel encoder, model OCD-PPA1B-0812-C060-PRT we have purchassed in Jan 2017.
        //
        //   NOTES:
        //     - The encoder family has an optional Preset pin that can be used to set the Zero point.  We have mistakenly ordered
        //         the PP (push-pull) version without the P1 (preset option).  The code still implements pinZero, which is not really used.
        //         In the future we may either:
        //              - get another encoder (not really likely to happen) or
        //              - use the wire for one more turn-bit.
        //
        //     - The encoder's family allows for up to 12 position bits and 13 turn bits.
        //     - This specific model has 12 position bits and 8 turn bits.
        //     - We use all the 12 position bits but only 4 turn-bits, due to wires-in-the-cable and DIO24 pins availability.
        //
        //   Ultimately we get 4096 positions in a turn and up to 16 turns (we estimate only about 7-8 turns to be physically required)
        //
        //  The upper and lower limits are maintained via software.  They have default natural values but these are overriden by values in the 
        //  focuser's ASCOM profile.
        //

        //
        // 7 Mar 2017 -  Arie Blumenzweig
        //
        //  NOTES:
        //    - We found that the new encoder increases/decreases in the oposite direction compared to the old one.
        //      The encoder has a pin that, when strapped to 5V, inverses the counting direction. We may do that in
        //       the future, till then we reverse the counter in software (with reversedDirection = true)
        //
        
        //
        // 23 May, 2017 - Arie Blumenzweig
        //
        //   - We have too much jitter in the position values.  We'll discard some of the least-significant position bits.
        //   - Seems that the current 4 turn-bits are not enough.  The values wrap-around at about 5mm from the upper limit-switch.
        //     The wire that was supposed to be used for zeroing the encoder (reminder: the purchassedencoder does not have this capability)
        //      will be re-used for an additional turn-bit, so we'll have 5 of them.
        //
        private static readonly bool reversedDirection = true;          // The encoder value decreases when focusing up

        private BitExtractor positionBits = new BitExtractor(9, 3);
        private BitExtractor turnsBits = new BitExtractor(6, 12);

        private uint _daqsValue;
        private bool _connected = false;
        private bool _multiTurn = false;
        private uint _upperLimit, _lowerLimit;
        private uint _maxValue;

        List<IConnectable> connectables = new List<IConnectable>();
        List<IDisposable> disposables = new List<IDisposable>();

        public WiseFocuserEnc() { }

        public static WiseFocuserEnc Instance
        {
            get
            {
                return instance;
            }
        }

        public void init(bool multiTurn = false)
        {
            if (_initialized)
                return;

            Name = "FocusEnc";
            this._multiTurn = multiTurn;

            if (this._multiTurn)
            {
                _maxValue = turnsBits.MaxValue * positionBits.MaxValue;
                pinLatch = new WisePin("FocusLatch", hardware.miscboard, DigitalPortType.FirstPortCH, 3, DigitalPortDirection.DigitalOut);
                base.init("FocusEnc",
                    (int)_maxValue,
                    new List<WiseEncSpec>() {
                        new WiseEncSpec() { brd = hardware.miscboard, port = DigitalPortType.FirstPortCL,  mask = 0x03 },
                        new WiseEncSpec() { brd = hardware.miscboard, port = DigitalPortType.FirstPortA,  mask = 0xff },
                        new WiseEncSpec() { brd = hardware.miscboard, port = DigitalPortType.FirstPortB,  mask = 0xff },
                        }
                );
                connectables.Add(pinLatch);
                disposables.Add(pinLatch);
                
                UpperLimit = _maxValue;
                LowerLimit = 0;
            }
            else
            {
                base.init("FocusEnc",
                    1 << 7,
                    new List<WiseEncSpec>() {
                        new WiseEncSpec() { brd = hardware.miscboard, port = DigitalPortType.FirstPortB,  mask = 0x7f },
                        },
                    true
                    );

                UpperLimit = 128;
                LowerLimit = 0;
            }

            _initialized = true;
        }

        public new uint Value
        {

            get
            {
                uint ret;

                if (Simulated)
                {
                    ret = _simulatedValue;
                }
                else
                {
                    uint turns, pos;

                    if (_multiTurn)
                    {
                        pinLatch.SetOn();
                        Thread.Sleep(1);
                    }
                    _daqsValue = base.Value;
                    if (_multiTurn)
                        pinLatch.SetOff();

                    if (_multiTurn)
                    {
                        pos = positionBits.Extract(_daqsValue);
                        turns = turnsBits.Extract(_daqsValue);
                    }
                    else
                    {
                        pos = _daqsValue;
                        turns = 0;
                    }

                    ret = (turns * positionBits.MaxValue) + pos;
                    if (reversedDirection)
                        ret = _maxValue - ret;
                    #region debug
                    debugger.WriteLine(Debugger.DebugLevel.DebugEncoders, "FocusEnc get: pos: {0}, turn: {1} => {2}", pos, turns, ret);
                    #endregion
                }
                return ret;
            }

            set
            {

            }
        }

        public new void Dispose()
        {
            foreach (var disposable in disposables)
                disposable.Dispose();

            base.Dispose();
        }

        public new bool Connected
        {
            get
            {
                return _connected;
            }

            set
            {
                if (value == _connected)
                    return;

                foreach (var connectable in connectables)
                    connectable.Connect(value);
                base.Connect(value);

                if (Simulated && value == true)
                    _simulatedValue = 0;

                _connected = value;
            }
        }

        public uint UpperLimit
        {
            get
            {
                return _upperLimit;
            }

            set
            {
                _upperLimit = value;
            }
        }

        public uint LowerLimit
        {
            get
            {
                return _lowerLimit;
            }

            set
            {
                _lowerLimit = value;
            }
        }

        public void startMoving(Const.Direction dir)
        {
            if (!Simulated)
                return;
            _simulatedDirection = dir;
            _simulationTimer.Change(100, 100);
        }

        public void stopMoving()
        {
            _simulationTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private static void simulateMovement(object o)
        {
            if (!instance.Simulated)
            {
                _simulationTimer.Change(Timeout.Infinite, Timeout.Infinite);
                return;
            }

            if (_simulatedDirection == Const.Direction.Increasing)
                _simulatedValue += _simulatedStep;
            else
                _simulatedValue -= _simulatedStep;
        }
    }
}
