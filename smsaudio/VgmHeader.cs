using System;

namespace smsaudio
{
    // convenience aliases for the header
    using u8 = Byte;
    using u16 = UInt16;
    using u32 = UInt32;
    
    class VgmHeader
    {
        // version 1.00
        public u32 EofOffset;
        public u32 Version;
        public u32 SN76489Clock;
        public u32 YM2413Clock;
        public u32 GD3Offset;
        public u32 TotalSamples;
        public u32 LoopOffset;
        public u32 LoopSamples;

        // version 1.01
        public u32 Rate;

        // version 1.10
        public u16 SN76489Feedback;
        public u8  SN76489ShiftRegisterWidth;

        // version 1.10
        public u32 YM2612Clock;
        public u32 YM2151Clock;

        // version 1.5
        public u32 VGMDataOffset;
    }
}