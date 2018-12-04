using System.Diagnostics;
using System.IO;

using static System.Console;

namespace smsaudio
{
    class Program
    {
        static double _updateClock;
        static uint _sampleRate = 44100;
        static double _sampleFrequency;

        static Options _options;

        static void Main(string[] args)
        {
            _options = Options.ParseCommandLine(args);

            string vgmPath = _options.Filename;

            if (!File.Exists(vgmPath))
            {
                WriteLine($"File {vgmPath} not found.");
                return;
            }

            string vgmFileName = Path.GetFileNameWithoutExtension(vgmPath);

            VgmFile vgmFile;

            using (var fileStream = File.Open(vgmPath, FileMode.Open, FileAccess.Read))
            {
                vgmFile = VgmFile.Load(fileStream);
            }

            if (_options.PrintVgmInfo)
            {
                WriteLine($"Filename: {Path.GetFileName(vgmPath)}");
                WriteLine();
                WriteLine($"Track:    {vgmFile.Gd3.English.TrackName}");
                WriteLine($"Game:     {vgmFile.Gd3.English.GameName}");
                WriteLine($"System:   {vgmFile.Gd3.English.SystemName}");
                WriteLine($"Composer: {vgmFile.Gd3.English.TrackAuthor}");

                WriteLine($"Release:  {vgmFile.Gd3.ReleaseDate}");
                WriteLine($"VGM By:   {vgmFile.Gd3.VgmAuthor}");
                WriteLine();
                WriteLine("Notes:");
                WriteLine(vgmFile.Gd3.Notes);
                WriteLine();
            }

            // generate the audio samples into a memory stream
            using (WaveFileWriter writer = new WaveFileWriter(File.Open($"{vgmFileName}.wav", FileMode.Create), new WaveFormat(_sampleRate, 16, 2)))
            {
                SN76489 psg = new SN76489(vgmFile.Header.SN76489ShiftRegisterWidth, vgmFile.Header.SN76489Feedback);

                _sampleFrequency = vgmFile.Header.SN76489Clock / (double)_sampleRate;

                bool playing = true;

                int loopLimit = 0;
                int loopCount = 0;

                uint playCursor = 0;

                while (playing)
                {
                    int command = vgmFile.VgmData[playCursor++];

                    switch (command)
                    {
                        // 0x50 dd: PSG(SN76489 / SN76496) write value dd
                        case 0x50:
                            psg.WriteData(vgmFile.VgmData[playCursor++]);
                            break;

                        //0x61 nn nn : Wait n samples, n can range from 0 to 65535 (approx 1.49
                        //             seconds). Longer pauses than this are represented by multiple
                        //             wait commands.
                        case 0x61:
                            int sampleCount = vgmFile.VgmData[playCursor++];
                            sampleCount |= vgmFile.VgmData[playCursor++] << 8;

                            OutputSamples(psg, writer, sampleCount);
                            break;

                        // 0x62: wait 735 samples(60th of a second), a shortcut for 0x61 0xdf 0x02
                        case 0x62:
                            OutputSamples(psg, writer, 735);
                            break;

                        // 0x63: wait 882 samples(50th of a second), a shortcut for 0x61 0x72 0x03
                        case 0x63:
                            OutputSamples(psg, writer, 882);
                            break;

                        // 0x66: end of sound data
                        case 0x66:
                            if (vgmFile.LoopOffset != 0 && loopCount < loopLimit)
                            {
                                playCursor = vgmFile.LoopOffset;
                                loopCount++;
                            }
                            else
                            {
                                playing = false;
                            }
                            break;

                        // 0x7n: wait n+1 samples, n can range from 0 to 15.
                        case 0x70: case 0x71: case 0x72: case 0x73:
                        case 0x74: case 0x75: case 0x76: case 0x77:
                        case 0x78: case 0x79: case 0x7A: case 0x7B:
                        case 0x7C: case 0x7D: case 0x7E: case 0x7F:
                            OutputSamples(psg, writer, (command & 0x0F) + 1);
                            break;

                        // game gear stereo shift, ignore for now
                        case 0x4F:
                            int stereoByte = vgmFile.VgmData[playCursor++];
                            WriteLine($"GG Stereo Write: 0x{stereoByte:X2}");
                            break;

                        default:
                            Error.WriteLine("Unknown command: 0x{0:X2} at 0x{1:X4}", command, playCursor - 1);
                            playing = false;
                            break;
                    }
                }
            }


            if (Debugger.IsAttached)
            {
                Write("Press any key to continue...");
                ReadKey(true);
            }
        }

        static void OutputSamples(SN76489 psg, WaveFileWriter writer, int sampleCount)
        {
            for (int i = 0; i < sampleCount; i++)
            {
                _updateClock += _sampleFrequency;

                uint updateCycles = (uint)_updateClock; // truncate to get integer clock value, keeping the decimal part to accumulate

                psg.Update(updateCycles);

                var (left, right) = psg.Output;
                writer.WriteSample(left);
                writer.WriteSample(right);

                _updateClock -= updateCycles;
            }
        }
    }
}
