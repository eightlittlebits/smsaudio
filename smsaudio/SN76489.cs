﻿using System;
using System.IO;

namespace smsaudio
{
    class SN76489
    {
        const int ChannelCount = 4;

        const int Tone2 = 2;
        const int Noise = 3;

        readonly double[] _volumeTable;

        uint _updateClock;
        uint _updateInterval = 16;

        int _latchedChannel;
        bool _volumeRegisterLatched;

        readonly int[] _channelControl = new int[ChannelCount];
        readonly double[] _channelVolume = new double[ChannelCount];

        readonly int[] _channelCounter = new int[ChannelCount];
        readonly int[] _channelOutput = new int[ChannelCount];

        readonly int _shiftRegisterWidth;
        readonly int _tappedBits;

        ushort _lsfr = 0x8000;

        BinaryWriter _outputStream;

        public SN76489(BinaryWriter outputStream, int shiftRegisterWidth, int tappedBits)
        {
            _outputStream = outputStream;

            _shiftRegisterWidth = shiftRegisterWidth;
            _tappedBits = tappedBits;

            _latchedChannel = 0;
            _volumeRegisterLatched = false;

            _volumeTable = ComputeVolumeLookup();
        }

        private static double[] ComputeVolumeLookup()
        {
            int maxVolume = short.MaxValue / 2;
            double attenuation = Math.Pow(10, -0.1); // 2dB attenuation

            double[] volumeTable = new double[16];

            for (int i = 0; i < volumeTable.Length; i++)
            {
                // attenuate 2dB for each entry in table
                volumeTable[i] = maxVolume * Math.Pow(attenuation, i);
            }

            // 0x0F is silence
            volumeTable[15] = 0;

            return volumeTable;
        }

        public void WriteData(byte data)
        {
            bool latchByte = false;

            if ((data & 0x80) == 0x80)
            {
                latchByte = true;

                _latchedChannel = (data >> 5) & 0x03;
                _volumeRegisterLatched = (data & 0x10) == 0x10;
            }

            if (_volumeRegisterLatched)
            {
                _channelVolume[_latchedChannel] = _volumeTable[data & 0x0F];
            }
            else if (_latchedChannel < 3)
            {
                // writing to a tone control register?
                if (latchByte)
                {
                    // latch byte sets the lower 4 bits of the register
                    _channelControl[_latchedChannel] = (_channelControl[_latchedChannel] & ~0x0F) | (data & 0x0F);
                }
                else
                {
                    // data byte sets the upper 6 bits of the register
                    _channelControl[_latchedChannel] = (_channelControl[_latchedChannel] & 0x0F) | ((data & 0x3F) << 4);
                }
            }
            else
            {
                // noise register
                _channelControl[_latchedChannel] = data & 0x07;
                _lsfr = 0x8000;
            }
        }

        public void Update(uint cycleCount)
        {
            _updateClock += cycleCount;

            while (_updateClock >= _updateInterval)
            {
                _updateClock -= _updateInterval;

                // update tone channels
                for (int i = 0; i < 3; i++)
                {
                    // if the tone register period is 0 then skip
                    if (_channelControl[i] == 0)
                        continue;

                    // update channel counter, if zero flip output and reload from tone register
                    if (--_channelCounter[i] <= 0)
                    {
                        _channelCounter[i] = _channelControl[i];
                        _channelOutput[i] ^= 1;
                    }
                }

                // update noise channel
                if (--_channelCounter[Noise] <= 0)
                {
                    // reset counter
                    switch (_channelControl[Noise] & 0x03)
                    {
                        case 0: _channelCounter[Noise] = 0x20; break;
                        case 1: _channelCounter[Noise] = 0x40; break;
                        case 2: _channelCounter[Noise] = 0x80; break;
                        case 3: _channelCounter[Noise] = _channelControl[Tone2]; break;
                    }

                    int feedback;

                    if ((_channelControl[Noise] & 0x04) == 0x00)
                    {
                        // periodic noise
                        feedback = _lsfr & 1;
                    }
                    else
                    {
                        // white noise
                        feedback = parity(_lsfr & _tappedBits);
                    }

                    _lsfr = (ushort)((_lsfr >> 1) | (feedback << (_shiftRegisterWidth - 1)));
                    _channelOutput[Noise] = _lsfr & 1;
                }

                // mix channel output and output sample
                double sample = 0;

                for (int i = 0; i < ChannelCount; i++)
                {
                    sample += _channelVolume[i] * (_channelOutput[i] - 0.5);
                }

                _outputStream.Write((short)sample);
            }
        }

        // TODO(david): make this a local function when C#7 comes along
        int parity(int v)
        {
            v ^= v >> 8;
            v ^= v >> 4;
            v ^= v >> 2;
            v ^= v >> 1;

            return v;
        }
    }
}
