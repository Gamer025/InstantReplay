using System;

namespace InstantReplay
{
    internal class RLE
    {
        //Too avoid unecessary allocs and garbage when compressing a lot
        byte[] outputCompress;

        //Typical size for a semi complex image
        public RLE(int compress = 100000)
        {
            outputCompress = new byte[compress];
        }

        //Input is a byte Array in format: r,g,b,r,g,b,...
        //Output is c,r,g,b,c,r,g,b with the c being the amount of repeats of that pixel
        public unsafe byte[] CompressRGB(byte[] input)
        {
            byte[] returnArray;
            //Worst case: Every pixel different so we effectively add 1 byte more, will 99% never happen
            if (outputCompress.Length < input.Length + input.Length / 3)
                Array.Resize(ref outputCompress, input.Length + input.Length / 3);
            fixed (byte* pInput = input)
            {
                fixed (byte* pOutput = outputCompress)
                {
                    byte* pCur = pInput;
                    byte* pEnd = pInput + input.Length;
                    byte* pOutCur = pOutput;
                    while (pCur < pEnd)
                    {
                        byte cur0 = *pCur;
                        byte cur1 = *(pCur + 1);
                        byte cur2 = *(pCur + 2);
                        int count = 1;
                        while (pCur + count * 3 < pEnd && count < 255 &&
                            *(pCur + count * 3) >> 3 == cur0 >> 3 && *(pCur + count * 3 + 1) >> 3 == cur1 >> 3 && *(pCur + count * 3 + 2) >> 3 == cur2 >> 3)
                        {
                            count++;
                        }
                        *pOutCur = (byte)count;
                        pOutCur += 1;
                        *pOutCur = cur0;
                        pOutCur += 1;
                        *pOutCur = cur1;
                        pOutCur += 1;
                        *pOutCur = cur2;
                        pOutCur += 1;
                        pCur += count * 3;
                    }
                    returnArray = new byte[pOutCur - pOutput];
                }
            }
            Array.Copy(outputCompress, returnArray, returnArray.Length);
            return returnArray;
        }

        //Yes this is an exact copy and paste of the above method only with the bit shift increased to 4
        //No this can't just be a variable because in testing that reduced perf from around 4ms to 6ms
        public unsafe byte[] CompressRGBHighCompress(byte[] input)
        {
            byte[] returnArray;
            //Worst case: Every pixel different so we effectively add 1 byte more, will 99% never happen
            if (outputCompress.Length < input.Length + input.Length / 3)
                Array.Resize(ref outputCompress, input.Length + input.Length / 3);
            fixed (byte* pInput = input)
            {
                fixed (byte* pOutput = outputCompress)
                {
                    byte* pCur = pInput;
                    byte* pEnd = pInput + input.Length;
                    byte* pOutCur = pOutput;
                    while (pCur < pEnd)
                    {
                        byte cur0 = *pCur;
                        byte cur1 = *(pCur + 1);
                        byte cur2 = *(pCur + 2);
                        int count = 1;
                        while (pCur + count * 3 < pEnd && count < 255 &&
                            *(pCur + count * 3) >> 4 == cur0 >> 4 && *(pCur + count * 3 + 1) >> 4 == cur1 >> 4 && *(pCur + count * 3 + 2) >> 4 == cur2 >> 4)
                        {
                            count++;
                        }
                        *pOutCur = (byte)count;
                        pOutCur += 1;
                        *pOutCur = cur0;
                        pOutCur += 1;
                        *pOutCur = cur1;
                        pOutCur += 1;
                        *pOutCur = cur2;
                        pOutCur += 1;
                        pCur += count * 3;
                    }
                    returnArray = new byte[pOutCur - pOutput];
                }
            }
            Array.Copy(outputCompress, returnArray, returnArray.Length);
            return returnArray;
        }

        public unsafe void DecompressRGB(byte[] input, byte[] output)
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
                            InstantReplay.ME.Logger_p.LogError("DecompressRGB got too small output array!");
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

        public unsafe void DecompressRGBandDownscale(byte[] input, byte[] output, int width)
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
                            UnityEngine.Debug.LogError("DecompressRGB got too small output array!");
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
