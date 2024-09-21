namespace Assets.Metater.MetaVoiceChat.VcImpls
{
    //public interface IVcImpl<TFrame> where TFrame : IVcFrame
    //{
    //    MetaVc MetaVc { get; set; }

    //    TFrame MakeEncodedFrame(double timestamp, ReadOnlySpan<byte> data);
    //    TFrame MakeDecodedFrame(double timestamp, ReadOnlySpan<short> samples);
    //}

    //public static class IVcImplExtensions
    //{
    //    public static TFrame EncodeFrame<TFrame>(this IVcImpl<TFrame> impl, TFrame decodedFrame) where TFrame : IVcFrame
    //    {
    //        var data = impl.MetaVc.Encoder.Encode(decodedFrame.Samples.AsSpan());
    //        return impl.MakeEncodedFrame(decodedFrame.Timestamp, data);
    //    }

    //    public static TFrame DecodeFrame<TFrame>(this IVcImpl<TFrame> impl, TFrame encodedFrame) where TFrame : IVcFrame
    //    {
    //        var samples = impl.MetaVc.Decoder.Decode(encodedFrame.Data.AsSpan());
    //        return impl.MakeDecodedFrame(encodedFrame.Timestamp, samples);
    //    }
    //}
}