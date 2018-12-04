namespace smsaudio
{
    struct AudioFrame<T> where T : struct
    {
        public readonly T Left;
        public readonly T Right;

        public AudioFrame(T left, T right)
        {
            Left = left;
            Right = right;
        }

        public void Deconstruct(out T left, out T right)
        {
            left = Left;
            right = Right;
        }
    }
}
