using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Lux.Management.Console.Modules.Broadcasting.QuickConfig.Helpers
{
    /// <summary>
    /// A self-contained, lightweight QR Code generator in pure C#.
    /// Ported from the compact QR Code generator algorithm (Nayuki's design).
    /// </summary>
    public static class QrCodeGenerator
    {
        public static WriteableBitmap GenerateQrCode(string text, int scale = 6)
        {
            // Simple QR code implementation (handles Version 5, 37x37 modules, ECC Level M)
            // Suitable for strings around 80-100 characters (e.g., Customer Name + HWID)
            byte[] qrBytes = EncodeText(text, out int size);
            
            int imgSize = size * scale;
            var bitmap = new WriteableBitmap(imgSize, imgSize, 96, 96, PixelFormats.Bgra32, null);
            
            int stride = imgSize * 4;
            byte[] pixels = new byte[imgSize * stride];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isBlack = qrBytes[y * size + x] != 0;
                    byte color = isBlack ? (byte)0 : (byte)255; // Black module or White background

                    // Draw a scaled block of pixels
                    for (int dy = 0; dy < scale; dy++)
                    {
                        for (int dx = 0; dx < scale; dx++)
                        {
                            int px = x * scale + dx;
                            int py = y * scale + dy;
                            int index = (py * stride) + (px * 4);
                            
                            pixels[index] = color;     // B
                            pixels[index + 1] = color; // G
                            pixels[index + 2] = color; // R
                            pixels[index + 3] = 255;   // A
                        }
                    }
                }
            }

            bitmap.WritePixels(new Int32Rect(0, 0, imgSize, imgSize), pixels, stride, 0);
            return bitmap;
        }

        private static byte[] EncodeText(string text, out int size)
        {
            // Version 6 (41x41), ECC Level M (handles up to 108 alphanumeric or 134 bytes)
            int version = 6;
            size = 17 + version * 4; // 41 modules
            byte[] grid = new byte[size * size];
            bool[] isFunction = new bool[size * size];

            // 1. Draw Function Patterns (Finders, Alignments, Timings)
            DrawFinderPatterns(grid, isFunction, size);
            DrawAlignmentPatterns(grid, isFunction, size, version);
            DrawTimingPatterns(grid, isFunction, size);
            DrawFormatAndVersionInfoPlaceholders(isFunction, size);

            // 2. Encode and Interleave Data & Error Correction
            byte[] dataCodewords = GetCodewords(text);
            byte[] eccCodewords = GetErrorCorrection(dataCodewords);
            byte[] allCodewords = CombineDataAndEcc(dataCodewords, eccCodewords);

            // 3. Populate Data Modules
            PopulateData(grid, isFunction, size, allCodewords);

            // 4. Apply Mask (Standard Mask 1: (x+y)%2 == 0 or Mask 3: (x+y)%3 == 0, let's use Mask 0: (x+y)%2 == 0)
            ApplyMask(grid, isFunction, size, 0);

            // 5. Draw actual Format and Version Info
            DrawFormatInfo(grid, size, 0); // Mask 0, ECC M (ECC M code is 00)

            return grid;
        }

        private static void DrawFinderPatterns(byte[] grid, bool[] isFunction, int size)
        {
            DrawFinder(grid, isFunction, size, 0, 0);
            DrawFinder(grid, isFunction, size, size - 7, 0);
            DrawFinder(grid, isFunction, size, 0, size - 7);
        }

        private static void DrawFinder(byte[] grid, bool[] isFunction, int size, int x, int y)
        {
            for (int dy = 0; dy < 7; dy++)
            {
                for (int dx = 0; dx < 7; dx++)
                {
                    int px = x + dx;
                    int py = y + dy;
                    bool val = (dx == 0 || dx == 6 || dy == 0 || dy == 6 || (dx >= 2 && dx <= 4 && dy >= 2 && dy <= 4));
                    grid[py * size + px] = val ? (byte)1 : (byte)0;
                    isFunction[py * size + px] = true;
                }
            }

            // Separators
            for (int i = 0; i < 8; i++)
            {
                SetFunction(grid, isFunction, size, x + i, y + 7, false);
                SetFunction(grid, isFunction, size, x + 7, y + i, false);
                SetFunction(grid, isFunction, size, x + i, y - 1, false);
                SetFunction(grid, isFunction, size, x - 1, y + i, false);
            }
        }

        private static void SetFunction(byte[] grid, bool[] isFunction, int size, int x, int y, bool val)
        {
            if (x >= 0 && x < size && y >= 0 && y < size)
            {
                grid[y * size + x] = val ? (byte)1 : (byte)0;
                isFunction[y * size + x] = true;
            }
        }

        private static void DrawAlignmentPatterns(byte[] grid, bool[] isFunction, int size, int version)
        {
            // Position for Version 6 is 6, 34
            int[] pos = { 6, 34 };
            foreach (int y in pos)
            {
                foreach (int x in pos)
                {
                    if (isFunction[y * size + x]) continue; // Skip if overlapping finder separator

                    for (int dy = -2; dy <= 2; dy++)
                    {
                        for (int dx = -2; dx <= 2; dx++)
                        {
                            bool val = (dx == -2 || dx == 2 || dy == -2 || dy == 2 || (dx == 0 && dy == 0));
                            grid[(y + dy) * size + (x + dx)] = val ? (byte)1 : (byte)0;
                            isFunction[(y + dy) * size + (x + dx)] = true;
                        }
                    }
                }
            }
        }

        private static void DrawTimingPatterns(byte[] grid, bool[] isFunction, int size)
        {
            for (int i = 8; i < size - 8; i++)
            {
                bool val = (i % 2 == 0);
                SetFunction(grid, isFunction, size, i, 6, val);
                SetFunction(grid, isFunction, size, 6, i, val);
            }
        }

        private static void DrawFormatAndVersionInfoPlaceholders(bool[] isFunction, int size)
        {
            for (int i = 0; i < 9; i++)
            {
                isFunction[6 * size + i] = true;
                isFunction[i * size + 6] = true;
                isFunction[(size - 1 - i) * size + 6] = true;
                isFunction[6 * size + (size - 1 - i)] = true;
            }
            isFunction[6 * size + 6] = true;
        }

        private static byte[] GetCodewords(string text)
        {
            // Version 6-M capacity is 136 byte codewords.
            byte[] rawBytes = System.Text.Encoding.UTF8.GetBytes(text);
            List<byte> list = new List<byte>();

            // Mode indicator: Byte mode (0100) -> 4 bits
            list.Add(0x40);

            // Character count indicator: 8 bits for Version 1-9 byte mode
            int count = Math.Min(rawBytes.Length, 255);
            list[0] |= (byte)(count >> 4);
            list.Add((byte)(count << 4));

            // Populate data
            int bitPos = 12;
            foreach (byte b in rawBytes)
            {
                WriteBits(list, b, 8, ref bitPos);
            }

            // Terminator (4 bits of 0)
            WriteBits(list, 0, 4, ref bitPos);

            // Align to byte boundary
            while (bitPos % 8 != 0)
            {
                WriteBits(list, 0, 1, ref bitPos);
            }

            // Pad with alternating bytes (0xEC, 0x11) until length is 136
            bool pad = true;
            while (list.Count < 136)
            {
                list.Add(pad ? (byte)0xEC : (byte)0x11);
                pad = !pad;
            }

            return list.ToArray();
        }

        private static void WriteBits(List<byte> list, int val, int len, ref int bitPos)
        {
            for (int i = len - 1; i >= 0; i--)
            {
                int bit = (val >> i) & 1;
                int byteIdx = bitPos / 8;
                int bitOffset = 7 - (bitPos % 8);

                if (byteIdx >= list.Count)
                {
                    list.Add(0);
                }

                if (bit != 0)
                {
                    list[byteIdx] |= (byte)(1 << bitOffset);
                }
                bitPos++;
            }
        }

        private static byte[] GetErrorCorrection(byte[] data)
        {
            // Version 6-M has 1 block, 136 data codewords, 34 ECC codewords
            // Generator polynomial for 34 ECC is derived using standard Reed-Solomon arithmetic
            int eccCount = 34;
            byte[] ecc = new byte[eccCount];

            // Generator polynomial roots
            byte[] poly = GetGeneratorPolynomial(eccCount);

            // Division in Galois Field 256
            for (int i = 0; i < data.Length; i++)
            {
                byte feedback = (byte)(data[i] ^ ecc[0]);
                Array.Copy(ecc, 1, ecc, 0, eccCount - 1);
                ecc[eccCount - 1] = 0;

                if (feedback != 0)
                {
                    for (int j = 0; j < eccCount; j++)
                    {
                        ecc[j] ^= GfMultiply(poly[j], feedback);
                    }
                }
            }

            return ecc;
        }

        private static byte[] CombineDataAndEcc(byte[] data, byte[] ecc)
        {
            byte[] combined = new byte[data.Length + ecc.Length];
            Array.Copy(data, 0, combined, 0, data.Length);
            Array.Copy(ecc, 0, combined, data.Length, ecc.Length);
            return combined;
        }

        private static void PopulateData(byte[] grid, bool[] isFunction, int size, byte[] data)
        {
            int byteIdx = 0;
            int bitIdx = 7;
            bool upward = true;

            for (int col = size - 1; col > 0; col -= 2)
            {
                if (col == 6) col--; // Skip timing column

                for (int row = 0; row < size; row++)
                {
                    int r = upward ? (size - 1 - row) : row;
                    for (int c = col; c >= col - 1; c--)
                    {
                        if (isFunction[r * size + c]) continue;

                        bool bit = false;
                        if (byteIdx < data.Length)
                        {
                            bit = ((data[byteIdx] >> bitIdx) & 1) != 0;
                            bitIdx--;
                            if (bitIdx < 0)
                            {
                                bitIdx = 7;
                                byteIdx++;
                            }
                        }

                        grid[r * size + c] = bit ? (byte)1 : (byte)0;
                    }
                }
                upward = !upward;
            }
        }

        private static void ApplyMask(byte[] grid, bool[] isFunction, int size, int mask)
        {
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (isFunction[y * size + x]) continue;

                    // Mask 0: (x + y) % 2 == 0
                    bool invert = ((x + y) % 2 == 0);
                    if (invert)
                    {
                        grid[y * size + x] = (byte)(grid[y * size + x] ^ 1);
                    }
                }
            }
        }

        private static void DrawFormatInfo(byte[] grid, int size, int mask)
        {
            // ECC M = 00, Mask 0 = 000. Combined raw: 00000.
            // Under BCH(15, 5) code, generator polynomial x^10 + x^8 + x^5 + x^4 + x^2 + x + 1 (10100110111)
            // Raw 00000 results in remainder 0000000000.
            // Formatted 15 bits = 000000000000000.
            // XORed with mask 101010000010010.
            // Result format code: 101010000010010.
            int format = 0x5412; // 101010000010010 in binary

            // Place format info bits
            for (int i = 0; i < 15; i++)
            {
                bool bit = ((format >> i) & 1) != 0;
                
                // Draw first part (near top left finder)
                int x = (i < 6) ? i : (i < 8 ? i + 1 : 14 - i);
                int y = (i < 6) ? 8 : (i < 7 ? 7 : (i < 9 ? 8 - (i - 7) : 14 - i));
                if (i >= 6 && i <= 8)
                {
                    x = i == 6 ? 5 : (i == 7 ? 7 : 8);
                    y = i == 6 ? 8 : (i == 7 ? 8 : 7);
                }

                // Wait, simplify standard layout placements:
                // Left-to-right/top-to-bottom indices:
                // Format info bits:
                // 0 to 5 -> (8, 0) to (8, 5)
                // 6 -> (8, 7)
                // 7 -> (8, 8)
                // 8 -> (7, 8)
                // 9 to 14 -> (5, 8) to (0, 8)
            }

            // Standard layout fallback placement:
            // Let's hardcode format bits exactly.
            // Format bits list:
            // bits 0..5 -> (8,0)..(8,5)
            SetGrid(grid, size, 8, 0, true);
            SetGrid(grid, size, 8, 1, false);
            SetGrid(grid, size, 8, 2, true);
            SetGrid(grid, size, 8, 3, false);
            SetGrid(grid, size, 8, 4, true);
            SetGrid(grid, size, 8, 5, false);

            SetGrid(grid, size, 8, 7, false); // bit 6
            SetGrid(grid, size, 8, 8, false); // bit 7
            SetGrid(grid, size, 7, 8, false); // bit 8

            SetGrid(grid, size, 5, 8, false); // bit 9
            SetGrid(grid, size, 4, 8, true);  // bit 10
            SetGrid(grid, size, 3, 8, false); // bit 11
            SetGrid(grid, size, 2, 8, false); // bit 12
            SetGrid(grid, size, 1, 8, true);  // bit 13
            SetGrid(grid, size, 0, 8, false); // bit 14

            // Second part (bottom left and top right finders)
            // bits 0..7 -> (size-1, 8)..(size-8, 8)
            for (int i = 0; i < 8; i++)
            {
                bool bit = ((format >> i) & 1) != 0;
                SetGrid(grid, size, 8, size - 1 - i, bit);
            }
            // bits 8..14 -> (8, size-7)..(8, size-1)
            for (int i = 8; i < 15; i++)
            {
                bool bit = ((format >> i) & 1) != 0;
                SetGrid(grid, size, size - 15 + i, 8, bit);
            }

            // Dark module (always at x=8, y=4*version+9)
            grid[(4 * 6 + 9) * size + 8] = 1;
        }

        private static void SetGrid(byte[] grid, int size, int y, int x, bool val)
        {
            grid[y * size + x] = val ? (byte)1 : (byte)0;
        }

        // --- Galois Field 256 & Reed Solomon Helpers ---
        private static readonly byte[] GfExp = new byte[512];
        private static readonly byte[] GfLog = new byte[256];

        static QrCodeGenerator()
        {
            // Initialize GF(256) log and exp tables
            int val = 1;
            for (int i = 0; i < 255; i++)
            {
                GfExp[i] = (byte)val;
                GfLog[val] = (byte)i;
                val <<= 1;
                if ((val & 0x100) != 0)
                {
                    val ^= 0x11D; // Generator polynomial x^8 + x^4 + x^3 + x^2 + 1
                }
            }
            for (int i = 255; i < 512; i++)
            {
                GfExp[i] = GfExp[i - 255];
            }
        }

        private static byte GfMultiply(byte a, byte b)
        {
            if (a == 0 || b == 0) return 0;
            return GfExp[GfLog[a] + GfLog[b]];
        }

        private static byte[] GetGeneratorPolynomial(int degree)
        {
            byte[] poly = new byte[degree + 1];
            poly[degree] = 1;

            int root = 1;
            for (int i = 0; i < degree; i++)
            {
                // Multiply poly by (x - root)
                byte rootVal = GfExp[i];
                for (int j = 0; j < degree; j++)
                {
                    poly[j] = (byte)(GfMultiply(poly[j], rootVal) ^ poly[j + 1]);
                }
                poly[degree] = GfMultiply(poly[degree], rootVal);
                root <<= 1;
            }

            // Exclude the leading 1 (coefficient for x^degree) to match division poly
            byte[] result = new byte[degree];
            Array.Copy(poly, 0, result, 0, degree);
            return result;
        }
    }
}
