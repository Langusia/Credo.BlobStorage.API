using System;

namespace Credo.BlobStorage.Client
{
    public enum Channel
    {
        CSS,
        MyCredo
    }

    internal static class ChannelExtensions
    {
        internal static string ToBucketPrefix(this Channel channel)
        {
            switch (channel)
            {
                case Channel.CSS: return "css";
                case Channel.MyCredo: return "mycredo";
                default: throw new ArgumentOutOfRangeException(nameof(channel), channel, "Unknown channel.");
            }
        }
    }
}
