using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static System.Console;

namespace smsaudio
{
    class Program
    {
        const int ClockFrequencyNTSC = 3579545; // Hz
        const int ClockFrequencyPAL = 3546893; // Hz

        static void Main(string[] args)
        {
            using (BinaryWriter writer = new BinaryWriter(File.Open("output.raw", FileMode.Create)))
            {
                SN76489 psg = new SN76489(writer);

                //SetTone(psg, 0, 440);
                //SetTone(psg, 1, 329.6);
                //SetTone(psg, 2, 392);

                //byte volume = 0;

                //SetVolume(psg, 0, volume);
                //SetVolume(psg, 1, volume);
                //SetVolume(psg, 2, volume);

                //SetTone(psg, 2, 10000);
                //SetVolume(psg, 2, 0);

                psg.WriteData(0xE4);
                SetVolume(psg, 3, 0);

                // generate 5 seconds worth of audio
                for (int i = 0; i < 5; i++)
                {
                    psg.Update(ClockFrequencyNTSC);
                }
            }
        }

        static int GetPeriodForFrequency(int clockFrequency, double frequency)
        {
            // f = clock / (32 x reg)
            // reg = (clock / f) / 32
            return (int)(clockFrequency / frequency) / 32;
        }

        static void SetTone(SN76489 psg, int channel, double frequency)
        {
            // get required period for the frequency
            int period = GetPeriodForFrequency(ClockFrequencyNTSC, frequency);

            // split into lower 4 and upper 6 bits
            int loData = period & 0x0F;
            int hiData = (period >> 4) & 0x3F;

            // generate data bytes for psg
            byte latch = (byte)(0x80 | (channel * 2) << 4 | loData);
            byte data = (byte)hiData;

            psg.WriteData(latch);
            psg.WriteData(data);
        }

        static void SetVolume(SN76489 psg, int channel, byte volume)
        {
            byte latch = (byte)(0x80 | ((channel * 2 + 1) << 4) | (volume & 0x0F));

            psg.WriteData(latch);
        }
    }
}
