using Unity.Collections;

public static class NativeArrayExtensions
{
    public static NativeArray<byte> Zeros;

    static NativeArrayExtensions()
    {
        Zeros = new NativeArray<byte>(256, Allocator.Persistent, NativeArrayOptions.ClearMemory);
    }

    //public static void CopyAndZero<T>(this NativeArray<T> dst, NativeArray<T> src) where T : struct
    //{
    //    NativeArray<T>.Copy(src, dst, src.Length);
    //    NativeArray<T>.Copy(Zeros, 0, dst, src.Length, dst.Length - src.Length);
    //}
    public static void CopyFromAndZero(this NativeArray<byte> dst, NativeArray<byte> src)
    {
        NativeArray<byte>.Copy(src, dst, src.Length);
        NativeArray<byte>.Copy(Zeros, 0, dst, src.Length, dst.Length - src.Length);
    }

    public static void CopyFromAndZero(this NativeArray<byte> dst, byte[] src)
    {
        NativeArray<byte>.Copy(src, dst, src.Length);
        if (dst.Length > src.Length)
            NativeArray<byte>.Copy(Zeros, 0, dst, src.Length, dst.Length - src.Length);
    }
}