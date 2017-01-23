using System;
using System.IO;

namespace smsaudio
{
    class SN76489
    {
        const int ChannelCount = 4;

        double[] _volumeTable;

        uint _updateClock;
        uint _updateInterval = 16;

        ushort[] _registers;
        int _latchedRegister;
        bool _toneRegisterLatched;

        int[] _channelCounter;
        int[] _channelOutput;

        readonly int _shiftRegisterWidth;
        readonly int _tappedBits;

        ushort _lsfr = 0x8000;

        BinaryWriter _outputStream;

        public SN76489(BinaryWriter outputStream, int shiftRegisterWidth, int tappedBits)
        {
            _outputStream = outputStream;

            _shiftRegisterWidth = shiftRegisterWidth;
            _tappedBits = tappedBits;

            _registers = new ushort[8];
            _channelCounter = new int[ChannelCount];
            _channelOutput = new int[ChannelCount];

            // initialise registers
            for (int i = 0; i < ChannelCount; i++)
            {
                _registers[i * 2] = 0;      // tone/noise
                _registers[i * 2 + 1] = 0x0F; // volume

                _channelOutput[i] = -1;  // start output high
            }

            _latchedRegister = 0;
            _toneRegisterLatched = true;

            _volumeTable = ComputeVolumeLookup();
        }

        private static double[] ComputeVolumeLookup()
        {
            int maxVolume = 8000;
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
            // if bit 7 is set it's a latch/data byte
            if ((data & 0x80) == 0x80)
            {
                // update the latched register from the data byte
                //   %1cctdddd
                //     |||````--Data
                //     ||`------Type
                //     ``-------Channel

                _latchedRegister = (data >> 4) & 0x07;
                _toneRegisterLatched = (data & 0x10) == 0x00;

                // set the low 4 bits of the latched register
                _registers[_latchedRegister] = (ushort)((_registers[_latchedRegister] & ~0x0F) | (data & 0x0F));

            }
            // otherwise it's a data byte
            else
            {
                // if a tone register is latched the lower 6 bits are placed
                // into the high 6 bits of the register
                if (_toneRegisterLatched)
                {
                    _registers[_latchedRegister] = (ushort)((_registers[_latchedRegister] & 0x0F) | ((data & 0x3F) << 4));
                }
                // for a volume register set the low 4 bits of the latched register
                else
                {
                    _registers[_latchedRegister] = (ushort)((_registers[_latchedRegister] & ~0x0F) | (data & 0x0F));
                }
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
                    if (_registers[i * 2] == 0)
                        continue;

                    // update channel counter, if zero flip output and reload from tone register
                    if (--_channelCounter[i] <= 0)
                    {
                        _channelCounter[i] += _registers[i * 2];
                        _channelOutput[i] *= -1;
                    }
                }

                // update noise channel
                if (--_channelCounter[3] <= 0)
                {
                    // reset counter
                    switch (_registers[6] & 0x03)
                    {
                        case 0: _channelCounter[3] += 0x20; break;
                        case 1: _channelCounter[3] += 0x40; break;
                        case 2: _channelCounter[3] += 0x80; break;
                        case 3: _channelCounter[3] += _registers[4]; break;
                    }

                    int feedback;

                    if ((_registers[6] & 0x04) == 0x00)
                    {
                        // periodic noise
                        feedback = _lsfr & 1;
                    }
                    else
                    {
                        // white noise
                        feedback  = parity(_lsfr & _tappedBits);
                    }

                    _lsfr = (ushort)((_lsfr >> 1) | (feedback << (_shiftRegisterWidth - 1)));
                    _channelOutput[3] = _lsfr & 1;
                }

                // mix channel output and output sample
                int sample = 0;

                for (int i = 0; i < ChannelCount; i++)
                {
                    sample += (int)(_volumeTable[_registers[(i * 2) + 1]] * _channelOutput[i]);
                }

                _outputStream.Write((short)sample);
            }
        }

        private int parity(int v)
        {
            v ^= v >> 8;
            v ^= v >> 4;
            v ^= v >> 2;
            v ^= v >> 1;

            return v;
        }
    }
}
