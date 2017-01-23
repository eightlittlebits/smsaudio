namespace smsaudio
{
    class Gd3Tag
    {
        internal class TrackInfo
        {
            public string TrackName;
            public string GameName;
            public string SystemName;
            public string TrackAuthor;
        }

        public TrackInfo English = new TrackInfo();
        public TrackInfo Japanese = new TrackInfo();

        public string ReleaseDate;
        public string VgmAuthor;
        public string Notes;
    }
}