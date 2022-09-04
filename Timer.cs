using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace diggb_gui
{
    public class Timer
    {
        byte tima;
        byte tma;
        byte tac;
        public ushort counter;
        public bool irq;

        public Timer()
        {
            tima = 0;
            tma = 0;
            tac = 0;
            counter = 0;
            irq = false;
        }

        public void write(ushort addr, byte val)
        {
            switch (addr)
            {
                case 0xff04: counter = 0; break;
                case 0xff05: tima = val; break;
                case 0xff06: tma = val; break;
                case 0xff07: tac = (byte)(val & 7); break;
                default: throw new Exception("Unexpected address");
            }
        }

        public byte read(ushort addr)
        {
            switch (addr)
            {
                case 0xff04: return (byte)(counter >> 8);
                case 0xff05: return tima;
                case 0xff06: return tma;
                case 0xff07: return tac;
                default: throw new Exception("Unexpected address");
            }
        }

        public void update(byte tick)
        {
            ushort counter_prev = counter;
            counter = (ushort)(counter + tick);
            if ((tac & 4) > 0)
            {
                int divider = 0;
                switch (tac & 3)
                {
                    case 0: divider = 10; break;
                    case 1: divider = 4; break;
                    case 2: divider = 6; break;
                    case 3: divider = 8; break;
                }
                int x = counter >> divider;
                int y = counter_prev >> divider;
                int mask = (1 << (16 - divider)) - 1;
                ushort diff = (ushort)((x - y) & mask);

                if (diff > 0)
                {
                    byte res = (byte)(tima + diff);
                    bool overflow = (tima + (byte)diff) > 0xff;
                    if (overflow)
                    {
                        tima = (byte)(tma + ((byte)diff - 1));
                        irq = true;
                    }
                    else
                    {
                        tima = res;
                    }
                }
                //Console.WriteLine("tac={0:x2} tima={1:x2} tma={2:x2} irq={3} counter={4} counter_prev={5} x={6} y={7} mask={8} diff={9}", tac, tima, tma, irq ? 1 : 0, counter, counter_prev, x, y, mask, diff);
            }
        }
    }
}
