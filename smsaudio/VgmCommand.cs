namespace smsaudio
{
    static class VgmCommand
    {
        public const byte PSGWriteDD = 0x50; // 0x50 dd: PSG(SN76489 / SN76496) write value dd
        public const byte WaitNSamples = 0x61; //0x61 nn nn : Wait n samples, n can range from 0 to 65535
        public const byte Wait735Samples = 0x62; // 0x62: wait 735 samples(60th of a second), a shortcut for 0x61 0xdf 0x02
        public const byte Wait882Samples = 0x63; // 0x63: wait 882 samples(50th of a second), a shortcut for 0x61 0x72 0x03
        public const byte EndOfSoundData = 0x66; // 0x66: end of sound data

        // 0x7n: wait n+1 samples, n can range from 0 to 15
        public const byte Wait01Samples = 0x70;
        public const byte Wait02Samples = 0x71;
        public const byte Wait03Samples = 0x72;
        public const byte Wait04Samples = 0x73;
        public const byte Wait05Samples = 0x74;
        public const byte Wait06Samples = 0x75;
        public const byte Wait07Samples = 0x76;
        public const byte Wait08Samples = 0x77;
        public const byte Wait09Samples = 0x78;
        public const byte Wait10Samples = 0x79;
        public const byte Wait11Samples = 0x7A;
        public const byte Wait12Samples = 0x7B;
        public const byte Wait13Samples = 0x7C;
        public const byte Wait14Samples = 0x7D;
        public const byte Wait15Samples = 0x7E;
        public const byte Wait16Samples = 0x7F;
    }
}