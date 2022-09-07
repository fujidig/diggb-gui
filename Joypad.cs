using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace diggb_gui
{
    enum Key
    {
        Down, Up, Left, Right, Start, Select, B, A
    }

    internal class Joypad
    {
        byte joyp;
        byte key_state;
        public bool irq;

        public Joypad()
        {
            joyp = 0xff;
            key_state = 0xff;
            irq = false;
        }

        public void keydown(Key key)
        {
            switch (key)
            {
                case Key.Down: key_state = (byte)(key_state & ~0x80); break;
                case Key.Up: key_state = (byte)(key_state & ~0x40); break;
                case Key.Left: key_state = (byte)(key_state & ~0x20); break;
                case Key.Right: key_state = (byte)(key_state & ~0x10); break;
                case Key.Start: key_state = (byte)(key_state & ~0x8); break;
                case Key.Select: key_state = (byte)(key_state & ~0x4); break;
                case Key.B: key_state = (byte)(key_state & ~0x2); break;
                case Key.A: key_state = (byte)(key_state & ~0x1); break;
            }
            irq = true;
        }

        public void keyup(Key key)
        {
            switch (key)
            {
                case Key.Down: key_state = (byte)(key_state | 0x80); break;
                case Key.Up: key_state = (byte)(key_state | 0x40); break;
                case Key.Left: key_state = (byte)(key_state | 0x20); break;
                case Key.Right: key_state = (byte)(key_state | 0x10); break;
                case Key.Start: key_state = (byte)(key_state | 0x8); break;
                case Key.Select: key_state = (byte)(key_state | 0x4); break;
                case Key.B: key_state = (byte)(key_state | 0x2); break;
                case Key.A: key_state = (byte)(key_state | 0x1); break;
            }
        }

        public byte read(ushort addr)
        {
            if (addr == 0xff00)
            {
                if ((joyp & 0x10) == 0)
                {
                    return (byte)((joyp & 0xf0) | (key_state >> 4) & 0xf);
                }
                else if ((joyp & 0x20) == 0)
                {
                    return (byte)((joyp & 0xf0) | (key_state & 0xf));
                }
                else
                {
                    return joyp;
                }
            }
            else
            {
                throw new Exception("Unexpected address");
            }
        }

        public void write(ushort addr, byte val)
        {
            if (addr == 0xff00)
            {
                joyp = (byte)((joyp & 0xcf) | (val & 0x30));
            }
            else
            {
                throw new Exception("Unexpected address");
            }
        }
    }
}
