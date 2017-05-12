using System;
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

        public AudioFrame<short> Output
        {
            get
            {
                double sample = 0;

                sample += _channelVolume[0] * (_channelOutput[0] - 0.5);
                sample += _channelVolume[1] * (_channelOutput[1] - 0.5);
                sample += _channelVolume[2] * (_channelOutput[2] - 0.5);
                sample += _channelVolume[3] * (_channelOutput[3] - 0.5);

                return new AudioFrame<short>((short)sample, (short)sample);
            }
        }

        public SN76489(int shiftRegisterWidth, int tappedBits)
        {
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
                    UpdateToneChannel(i);
                }

                UpdateNoiseChannel();
            }
        }

        private void UpdateToneChannel(int toneChannel)
        {
            // if the tone register period is 0 then skip
            if (_channelControl[toneChannel] == 0)
                return;

            // update channel counter, if zero flip output and reload from tone register
            if (--_channelCounter[toneChannel] <= 0)
            {
                _channelCounter[toneChannel] = _channelControl[toneChannel];
                _channelOutput[toneChannel] ^= 1;
            }
        }

        private void UpdateNoiseChannel()
        {
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
                    int HasOddParity(int v)
                    {
                        v ^= v >> 8;
                        v ^= v >> 4;
                        v ^= v >> 2;
                        v ^= v >> 1;

                        return v;
                    }

                    // white noise
                    feedback = HasOddParity(_lsfr & _tappedBits);
                }

                _lsfr = (ushort)((_lsfr >> 1) | (feedback << (_shiftRegisterWidth - 1)));
                _channelOutput[Noise] = _lsfr & 1;
            }
        }
    }
}
