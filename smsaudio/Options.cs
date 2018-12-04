namespace smsaudio
{
    class Options
    {
        public bool PrintVgmInfo { get; private set; }
        public bool MultiChannelOutput { get; private set; }

        public string Filename { get; private set; }

        public static Options ParseCommandLine(string[] args)
        {
            var options = new Options();

            bool done = false;

            for (int i = 0; i < args.Length && !done; i++)
            {
                switch (args[i])
                {
                    case "--info":
                        options.PrintVgmInfo = true;
                        break;

                    case "--multichannel":
                        options.MultiChannelOutput = true;
                        break;

                    default:
                        options.Filename = args[i];
                        done = true;
                        break;
                }
            }

            return options;
        }
    }
}
