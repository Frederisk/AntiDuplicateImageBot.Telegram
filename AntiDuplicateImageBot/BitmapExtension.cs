using Shipwreck.Phash.Imaging;

using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace AntiDuplicateImageBot;

public static class BitmapExtensions {
    internal static Bitmap ToRgb24(this Bitmap bitmap) {
        if (bitmap.PixelFormat == PixelFormat.Format24bppRgb) {
            return bitmap;
        }

        Bitmap? bitmap2 = null;
        try {
            bitmap2 = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format24bppRgb);
            ////bitmap2.SetResolution(bitmap.HorizontalResolution, bitmap.VerticalResolution);
            using (Graphics graphics = Graphics.FromImage(bitmap2)) {
                //    graphics.CompositingMode = CompositingMode.SourceCopy;
                //    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                //    graphics.DrawImage(bitmap, 0, 0);
                //graphics.DrawImage(bitmap,  new Rectangle(0, 0, bitmap2.Width, bitmap2.Height));
            }

            return bitmap2;
        } catch (Exception) {
            bitmap2?.Dispose();
            throw;
        }
    }

    public static ByteImage ToLuminanceImage(this Bitmap bitmap) {
        Bitmap? bitmap2 = null;
        try {
            bitmap2 = bitmap.ToRgb24();
            Byte[] array = bitmap2.ToBytes();
            ByteImage byteImage = new ByteImage(bitmap2.Width, bitmap2.Height);
            Int32 num = (Image.GetPixelFormatSize(bitmap2.PixelFormat) + 7) / 8;
            Int32 num2 = bitmap2.GetStride() % (bitmap2.Width * num);
            Vector3 vector = new Vector3(66f, 129f, 25f);
            Int32 num3 = 0;
            Vector3 vector2 = default;
            for (Int32 i = 0; i < byteImage.Height; i++) {
                for (Int32 j = 0; j < byteImage.Width; j++) {
                    vector2.Z = (Int32)array[num3++];
                    vector2.Y = (Int32)array[num3++];
                    vector2.X = (Int32)array[num3++];
                    byteImage[j, i] = (Byte)(((Int32)(Vector3.Dot(vector, vector2) + 128f) >> 8) + 16);
                }

                num3 += num2;
            }

            return byteImage;
        } finally {
            if (bitmap != bitmap2) {
                bitmap2?.Dispose();
            }
        }
    }

    public static Int32 GetStride(this Bitmap bitmap) {
        Int32 bitsPerPixel = ((Int32)bitmap.PixelFormat & 0xff00) >> 8;
        Int32 stride = 4 * ((bitmap.Width * bitsPerPixel + 31) / 32);
        return stride;
    }


    /// <summary>
    /// Copies the bitmap to its raw bytes format with stride bytes.
    /// </summary>
    /// <param name="bitmap">bitmap to convert</param>
    /// <returns>Raw byte array with stride bytes</returns>
    public static Byte[] ToBytes(this Bitmap bitmap) {
        BitmapData? lockedBits = null;
        try {
            lockedBits = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, bitmap.PixelFormat);
            Int32 sizeInBytes = lockedBits.Stride * lockedBits.Height;
            Byte[] rawPixelByteData = new Byte[sizeInBytes];
            Marshal.Copy(lockedBits.Scan0, rawPixelByteData, 0, sizeInBytes);

            return rawPixelByteData;
        } finally {
            if (lockedBits != null)
                bitmap.UnlockBits(lockedBits);
        }
    }
}