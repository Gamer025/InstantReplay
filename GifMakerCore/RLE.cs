using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GifMaker
{
    internal class RLE
    {
        public unsafe static void DecompressRGB(byte[] input, byte[] output)
        {

            fixed (byte* pInput = input)
            {
                fixed (byte* pOutput = output)
                {
                    byte* pCur = pInput;
                    byte* pEnd = pInput + input.Length;
                    byte* pOutCur = pOutput;
                    byte* pOutEnd = pOutCur + output.Length;
                    while (pCur + 3 < pEnd && pOutCur < pOutEnd)
                    {
                        int amt = *pCur;
                        if (pOutCur + 3 * amt > pOutEnd)
                        {
                            Console.WriteLine("DecompressRGB got too small output array!");
                            return;
                        }
                        for (int repeat = 0; repeat < amt; repeat++)
                        {
                            *pOutCur = *(pCur + 1);
                            pOutCur += 1;
                            *pOutCur = *(pCur + 2);
                            pOutCur += 1;
                            *pOutCur = *(pCur + 3);
                            pOutCur += 1;
                        }
                        pCur += 4;
                    }
                }
            }
        }

        public unsafe static void DecompressRGBandDownscale(byte[] input, byte[] output, int width)
        {
            int row = 0;
            int col = 0;
            fixed (byte* pInput = input)
            {
                fixed (byte* pOutput = output)
                {
                    byte* pCur = pInput;
                    byte* pEnd = pInput + input.Length;
                    byte* pOutCur = pOutput;
                    byte* pOutEnd = pOutCur + output.Length;
                    while (pCur + 3 < pEnd && pOutCur < pOutEnd)
                    {
                        int amt = *pCur;
                        pCur++;
                        byte r = *pCur;
                        pCur++;
                        byte g = *pCur;
                        pCur++;
                        byte b = *pCur;
                        pCur++;
                        if (pOutCur + 3 * amt / 2 > pOutEnd)
                        {
                            Console.WriteLine("DecompressRGB got too small output array!");
                            return;
                        }
                        for (int repeat = 0; repeat < amt; repeat++)
                        {
                            if (row % 2 == 0 && col % 2 == 0)
                            {
                                *pOutCur++ = r;
                                *pOutCur++ = g;
                                *pOutCur++ = b;
                            }
                            col++;
                            if (col >= width)
                            {
                                col = 0;
                                row++;
                            }
                        }
                    }
                }
            }
        }
    }
}
