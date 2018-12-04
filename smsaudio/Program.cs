using System;
using System.Diagnostics;
using System.IO;

namespace smsaudio
{
    class Program
    {
        const uint _sampleRate = 44100;

        static double _psgUpdateClock;
        static double _psgClocksPerSample;

        static Options _options;

        static void Main(string[] args)
        {
            _options = Options.ParseCommandLine(args);

            string vgmPath = _options.Filename;

            if (!File.Exists(vgmPath))
            {
                Console.WriteLine($"File {vgmPath} not found.");
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
                Console.WriteLine($"Filename: {Path.GetFileName(vgmPath)}");
                Console.WriteLine();
                Console.WriteLine($"Track:    {vgmFile.Gd3.English.TrackName}");
                Console.WriteLine($"Game:     {vgmFile.Gd3.English.GameName}");
                Console.WriteLine($"System:   {vgmFile.Gd3.English.SystemName}");
                Console.WriteLine($"Composer: {vgmFile.Gd3.English.TrackAuthor}");

                Console.WriteLine($"Release:  {vgmFile.Gd3.ReleaseDate}");
                Console.WriteLine($"VGM By:   {vgmFile.Gd3.VgmAuthor}");
                Console.WriteLine();
                Console.WriteLine("Notes:");
                Console.WriteLine(vgmFile.Gd3.Notes);
                Console.WriteLine();
            }

            // generate the audio samples into a memory stream
            using (var writer = new WaveFileWriter(File.Open($"{vgmFileName}.wav", FileMode.Create), 
                                                    new WaveFormat(_sampleRate, 16, (ushort)(_options.MultiChannelOutput ? 4 : 2))))
            {
                var psg = new SN76489(vgmFile.Header.SN76489ShiftRegisterWidth, vgmFile.Header.SN76489Feedback);

                _psgClocksPerSample = vgmFile.Header.SN76489Clock / (double)_sampleRate;

                bool playing = true;

                int loopLimit = 0;
                int loopCount = 0;

                uint playCursor = 0;

                while (playing)
                {
                    byte command = vgmFile.VgmData[playCursor++];

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

                            OutputPSGSamples(psg, writer, sampleCount);
                            break;

                        // 0x62: wait 735 samples(60th of a second), a shortcut for 0x61 0xdf 0x02
                        case 0x62:
                            OutputPSGSamples(psg, writer, 735);
                            break;

                        // 0x63: wait 882 samples(50th of a second), a shortcut for 0x61 0x72 0x03
                        case 0x63:
                            OutputPSGSamples(psg, writer, 882);
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
                            OutputPSGSamples(psg, writer, (command & 0x0F) + 1);
                            break;

                        // game gear stereo shift, ignore for now
                        case 0x4F:
                            int stereoByte = vgmFile.VgmData[playCursor++];
                            Console.WriteLine($"GG Stereo Write: 0x{stereoByte:X2}");
                            break;

                        default:
                            Console.Error.WriteLine("Unknown command: 0x{0:X2} at 0x{1:X4}", command, playCursor - 1);
                            playing = false;
                            break;
                    }
                }
            }

            if (Debugger.IsAttached)
            {
                Console.Write("Press any key to continue...");
                Console.ReadKey(true);
            }
        }

        static void OutputPSGSamples(SN76489 psg, WaveFileWriter writer, int sampleCount)
        {
            for (int i = 0; i < sampleCount; i++)
            {
                _psgUpdateClock += _psgClocksPerSample;

                uint updateCycles = (uint)_psgUpdateClock; // truncate to get integer clock value, keeping the decimal part to accumulate

                psg.Update(updateCycles);

                if (!_options.MultiChannelOutput)
                {
                    var (left, right) = psg.Output;
                    writer.WriteSample(left);
                    writer.WriteSample(right);
                }
                else
                {
                    writer.WriteSample(psg.Channel0Output);
                    writer.WriteSample(psg.Channel1Output);
                    writer.WriteSample(psg.Channel2Output);
                    writer.WriteSample(psg.Channel3Output);
                }

                _psgUpdateClock -= updateCycles;
            }
        }
    }
}
