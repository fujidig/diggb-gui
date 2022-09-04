using diggb_gui;
using System;

namespace diggb_gui
{
    class Interpreter
    {
        ushort af;
        ushort bc;
        ushort de;
        ushort hl;
        ushort pc;
        ushort sp;
        bool ime;
        byte tick;
        bool halted;
        Cartridge cartridge;
        byte[] ram;
        byte[] hram;
        Timer timer;
        byte int_flag;
        byte int_enable;
        public PPU ppu;

        static string[] REGNAME = { "B", "C", "D", "E", "H", "L", "(HL)", "A" };
        static string[] REGNAME2 = { "BC", "DE", "HL", "AF", "SP" };

        public Interpreter(byte[] mem_)
        {
            cartridge = new Cartridge(mem_);
            af = bc = de = hl = 0;
            sp = 0;
            pc = 0x100;
            ime = false;
            tick = 0;
            halted = false;
            timer = new Timer();
            ram = new byte[0x2000];
            hram = new byte[0x7f];
            ppu = new PPU();
        }

        public void Run()
        {
            while (true)
            {
                int elapsed_tick = 0;
                while (elapsed_tick < 456 * (144 + 10))
                {
                    elapsed_tick += Step();
                }
            }
        }

        public int Step()
        {
            int total_tick = 0;
            tick = 0;
            if (halted)
            {
                tick += 4;
            }
            else
            {
                FetchAndExec();
            }
            total_tick += tick;
            update(tick);
            if (ime)
            {
                tick = 0;
                check_irqs();
                update(tick);
                total_tick += tick;
            }
            return total_tick;
        }

        void update(byte tick)
        {
            //Console.WriteLine("update: {0:d5} {1:x4}", (ushort)(timer.counter + tick), pc);
            timer.update(tick);
            ppu.update(tick);

            if (ppu.irq_vblank) {
                int_flag |= 0x1;
                ppu.irq_vblank = false;
            }

            if (ppu.irq_lcdc) {
                int_flag |= 0x2;
                ppu.irq_lcdc = false;
            }

            if (timer.irq)
            {
                int_flag |= 0x4;
                timer.irq = false;
            }
        }

        void check_irqs()
        {
            for (int i = 0; i <= 4; i++)
            {
                bool irq = (int_flag & (1 << i)) > 0;
                bool ie = (int_enable & (1 << i)) > 0;
                if (irq && ie)
                {
                    call_isr(i);
                    break;
                }
            }
        }

        void call_isr(int id)
        {
            int_flag &= (byte)~(1 << id);
            ime = false;
            halted = false;
            ushort isr = 0;
            switch (id)
            {
                case 0: isr = 0x40; break;
                case 1: isr = 0x48; break;
                case 2: isr = 0x50; break;
                case 3: isr = 0x80; break;
                case 4: isr = 0x70; break;
            }
            tick += 8;
            //debugPrint(String.Format("call_isr {0}, {1:x4}: ", id, isr));
            call(isr);
        }

        void FetchAndExec()
        {
            //Console.WriteLine("{0:x4} {1:x4} {2:x4} {3:x4} {4:x4} {5:x4}", pc, sp, af, bc, de, hl);
            //Console.Error.Write("{0:x4}: ", pc);
            byte insn = read(pc++);
            switch (insn)
            {
                case 0x00:
                    debugPrint("nop");
                    break;
                case 0xc2:
                    {
                        ushort nn = read_16bit_from_pc();
                        debugPrint(String.Format("jp nz, {0:x}", nn));
                        if (((af >> 7) & 1) == 0)
                        {
                            pc = nn;
                            tick += 4;
                        }
                        break;
                    }
                case 0xca:
                    {
                        ushort nn = read_16bit_from_pc();
                        debugPrint(String.Format("jp z, {0:x}", nn));
                        if (getFlagZ())
                        {
                            pc = nn;
                            tick += 4;
                        }
                        break;
                    }
                case 0xd2:
                    {
                        ushort nn = read_16bit_from_pc();
                        debugPrint(String.Format("jp nc, {0:x}", nn));
                        if (!getFlagC())
                        {
                            pc = nn;
                            tick += 4;
                        }
                        break;
                    }
                case 0xda:
                    {
                        ushort nn = read_16bit_from_pc();
                        debugPrint(String.Format("jp c, {0:x}", nn));
                        if (getFlagC())
                        {
                            pc = nn;
                            tick += 4;
                        }
                        break;
                    }
                case 0xc3:
                    {
                        ushort nn = read_16bit_from_pc();
                        debugPrint(String.Format("jp {0:x}", nn));
                        pc = nn;
                        tick += 4;
                        break;
                    }
                case 0xf3:
                    debugPrint("di");
                    ime = false;
                    break;
                case 0xea:
                    {
                        ushort nn = read_16bit_from_pc();
                        debugPrint(String.Format("ld ({0:x}), A", nn));
                        write(nn, (byte)(af >> 8));
                        break;
                    }
                case 0xe0:
                    {
                        byte n = read(pc++);
                        debugPrint(String.Format("ldh ({0:x}), A", n));
                        write((ushort)(0xff00 | n), (byte)(af >> 8));
                        break;
                    }
                case 0xcd:
                    {
                        ushort nn = read_16bit_from_pc();
                        debugPrint(String.Format("call ({0:x})", nn));
                        tick += 4;
                        write(--sp, (byte)(pc >> 8));
                        write(--sp, (byte)(pc & 0xff));
                        pc = nn;
                        break;
                    }
                case 0x40:
                case 0x41:
                case 0x42:
                case 0x43:
                case 0x44:
                case 0x45:
                case 0x46:
                case 0x47:
                case 0x48:
                case 0x49:
                case 0x4a:
                case 0x4b:
                case 0x4c:
                case 0x4d:
                case 0x4e:
                case 0x4f:
                case 0x50:
                case 0x51:
                case 0x52:
                case 0x53:
                case 0x54:
                case 0x55:
                case 0x56:
                case 0x57:
                case 0x58:
                case 0x59:
                case 0x5a:
                case 0x5b:
                case 0x5c:
                case 0x5d:
                case 0x5e:
                case 0x5f:
                case 0x60:
                case 0x61:
                case 0x62:
                case 0x63:
                case 0x64:
                case 0x65:
                case 0x66:
                case 0x67:
                case 0x68:
                case 0x69:
                case 0x6a:
                case 0x6b:
                case 0x6c:
                case 0x6d:
                case 0x6e:
                case 0x6f:
                case 0x70:
                case 0x71:
                case 0x72:
                case 0x73:
                case 0x74:
                case 0x75: /********/
                case 0x77:
                case 0x78:
                case 0x79:
                case 0x7a:
                case 0x7b:
                case 0x7c:
                case 0x7d:
                case 0x7e:
                case 0x7f:
                    {
                        int lhs = (insn >> 3) - 8;
                        int rhs = insn & 7;
                        debugPrint(String.Format("ld {0} {1}", REGNAME[lhs], REGNAME[rhs]));
                        writereg(lhs, readreg(rhs));
                        break;
                    }
                case 0x18:
                    {
                        sbyte e = (sbyte)read(pc++);
                        debugPrint(String.Format("jr {0}", e));
                        pc = (ushort)(pc + e);
                        tick += 4;
                        break;
                    }
                case 0xc9:
                    {
                        debugPrint("ret");
                        ret();
                        break;
                    }
                case 0xd9:
                    {
                        debugPrint("reti");
                        ime = true;
                        ret();
                        break;
                    }
                case 0xc5:
                case 0xd5:
                case 0xe5:
                case 0xf5:
                    {
                        int i = (insn >> 4) - 0xc;
                        debugPrint(String.Format("push {0}", REGNAME2[i]));
                        ushort v = readreg2(i);
                        sp -= 2;
                        tick += 4;
                        write(sp, (byte)(v & 0xff));
                        write((ushort)(sp + 1), (byte)(v >> 8));
                        break;
                    }
                case 0xc1:
                case 0xd1:
                case 0xe1:
                case 0xf1:
                    {
                        int i = (insn >> 4) - 0xc;
                        debugPrint(String.Format("pop {0}", REGNAME2[i]));
                        byte lsb = read(sp);
                        byte msb = read((ushort)(sp + 1));
                        sp += 2;
                        ushort addr = (ushort)(lsb | (msb << 8));
                        if (insn == 0xf1) addr &= 0xfff0;
                        writereg2(i, addr);

                        break;
                    }
                case 0x03:
                case 0x13:
                case 0x23:
                case 0x33:
                    {
                        int i = (insn >> 4);
                        if (i == 3) i = 4;
                        debugPrint(String.Format("inc {0}", REGNAME2[i]));
                        writereg2(i, (ushort)(readreg2(i) + 1));
                        tick += 4;
                        break;
                    }
                case 0x01:
                case 0x11:
                case 0x21:
                case 0x31:
                    {
                        int i = (insn >> 4);
                        if (i == 3) i = 4;
                        ushort nn = read_16bit_from_pc();
                        debugPrint(String.Format("ld {0}, {1:x}", REGNAME2[i], nn));
                        writereg2(i, nn);
                        break;
                    }
                case 0xb0:
                case 0xb1:
                case 0xb2:
                case 0xb3:
                case 0xb4:
                case 0xb5:
                case 0xb6:
                case 0xb7:
                    {
                        int i = insn & 0x7;
                        debugPrint(String.Format("or {0}", REGNAME[i]));
                        byte result = (byte)(readreg(7) | readreg(i));
                        writereg(7, result);
                        setflags(result == 0, false, false, false);
                        break;
                    }
                case 0xf6:
                    {
                        byte n = read(pc++);
                        debugPrint(String.Format("or {0:x}", n));
                        byte result = (byte)(readreg(7) | n);
                        writereg(7, result);
                        setflags(result == 0, false, false, false);
                        break;
                    }
                case 0xa8:
                case 0xa9:
                case 0xaa:
                case 0xab:
                case 0xac:
                case 0xad:
                case 0xae:
                case 0xaf:

                    {
                        int i = insn & 0x7;
                        debugPrint(String.Format("xor {0}", REGNAME[i]));
                        byte result = (byte)(readreg(7) ^ readreg(i));
                        writereg(7, result);
                        setflags(result == 0, false, false, false);
                        break;
                    }
                case 0xc8:
                    {
                        debugPrint("ret Z");
                        tick += 4;
                        if (((af >> 7) & 1) != 0)
                        {
                            ret();
                        }
                        break;
                    }
                case 0xd8:
                    {
                        debugPrint("ret C");
                        tick += 4;
                        if (((af >> 4) & 1) != 0)
                        {
                            ret();
                        }
                        break;
                    }
                case 0xc0:
                    {
                        debugPrint("ret NZ");
                        tick += 4;
                        if (((af >> 7) & 1) == 0)
                        {
                            ret();
                        }
                        break;
                    }
                case 0xd0:
                    {
                        debugPrint("ret NC");
                        tick += 4;
                        if (((af >> 4) & 1) == 0)
                        {
                            ret();
                        }
                        break;
                    }
                case 0x0e:
                case 0x1e:
                case 0x2e:
                case 0x3e:
                    {
                        int i = (insn >> 4) * 2 + 1;
                        byte n = read(pc++);
                        debugPrint(String.Format("ld {0}, {1:x}", REGNAME[i], n));
                        writereg(i, n);
                        break;
                    }
                case 0xc7:
                case 0xd7:
                case 0xe7:
                case 0xf7:
                case 0xcf:
                case 0xdf:
                case 0xef:
                case 0xff:
                    {
                        int n = insn - 0xc7;
                        debugPrint(String.Format("rst {0:x}", n));
                        call((ushort)n);
                        break;
                    }
                case 0x02:
                case 0x12:
                    {
                        int i = insn >> 4;
                        debugPrint(String.Format("ld ({0}), A", REGNAME2[i]));
                        write(readreg2(i), readreg(7));
                        break;
                    }
                case 0x22:
                    {
                        debugPrint("ld (HL+), A");
                        write(readreg2(2), readreg(7));
                        writereg2(2, (ushort)(readreg2(2) + 1));
                        break;
                    }
                case 0x32:
                    {
                        debugPrint("ld (HL-), A");
                        write(readreg2(2), readreg(7));
                        writereg2(2, (ushort)(readreg2(2) - 1));
                        break;
                    }
                case 0x05:
                case 0x15:
                case 0x25:
                case 0x35:
                case 0x0d:
                case 0x1d:
                case 0x2d:
                case 0x3d:
                    {
                        int i = (insn >> 3);
                        debugPrint(String.Format("dec {0}", REGNAME[i]));
                        byte orig = readreg(i);
                        byte result = (byte)(orig - 1);
                        writereg(i, result);
                        setflags(result == 0, true, (orig & 0xf) == 0, ((af >> 4) & 1) != 0);
                        break;
                    }
                case 0x28:
                    {
                        sbyte e = (sbyte)read(pc++);
                        debugPrint(String.Format("jr Z, {0}", e));
                        if (((af >> 7) & 1) != 0)
                        {
                            pc = (ushort)(pc + e);
                            tick += 4;
                        }
                        break;
                    }
                case 0x38:
                    {
                        sbyte e = (sbyte)read(pc++);
                        debugPrint(String.Format("jr C, {0}", e));
                        if (((af >> 4) & 1) != 0)
                        {
                            pc = (ushort)(pc + e);
                            tick += 4;
                        }
                        break;
                    }
                case 0x20:
                    {
                        sbyte e = (sbyte)read(pc++);
                        debugPrint(String.Format("jr NZ, {0}", e));
                        if (((af >> 7) & 1) == 0)
                        {
                            pc = (ushort)(pc + e);
                            tick += 4;
                        }
                        break;
                    }
                case 0x30:
                    {
                        sbyte e = (sbyte)read(pc++);
                        debugPrint(String.Format("jr NC, {0}", e));
                        if (((af >> 4) & 1) == 0)
                        {
                            pc = (ushort)(pc + e);
                            tick += 4;
                        }
                        break;
                    }
                case 0xf0:
                    {
                        byte n = read(pc++);
                        debugPrint(String.Format("ldh A, ({0:x})", n));
                        writereg(7, read((ushort)(0xff00 | n)));
                        break;
                    }
                case 0xfe:
                    {
                        byte n = read(pc++);
                        debugPrint(String.Format("cp {0}", n));
                        byte input = readreg(7);
                        byte result = (byte)(input - n);
                        setflags(result == 0, true, (input & 0xf) < (n & 0xf), input < n);
                        break;
                    }
                case 0xfa:
                    {
                        ushort nn = read_16bit_from_pc();
                        debugPrint(String.Format("ld A, ({0:x})", nn));
                        writereg(7, read(nn));
                        break;
                    }
                case 0x76:
                    {
                        debugPrint("halt");
                        if (ime)
                        {
                            halted = true;
                        }
                        break;
                    }
                case 0x04:
                case 0x14:
                case 0x24:
                case 0x34:
                case 0x0c:
                case 0x1c:
                case 0x2c:
                case 0x3c:
                    {
                        int i = (insn >> 3);
                        debugPrint(String.Format("inc {0}", REGNAME[i]));
                        byte input = readreg(i);
                        byte result = (byte)(input + 1);
                        bool h = (input & 0xf) == 0xf;
                        writereg(i, result);
                        setflags(result == 0, false, h, ((af >> 4) & 1) != 0);
                        break;
                    }
                case 0x06:
                case 0x16:
                case 0x26:
                case 0x36:
                    {
                        int i = (insn >> 4) * 2;
                        byte n = read(pc++);
                        debugPrint(String.Format("ld {0}, {1}", REGNAME[i], n));
                        writereg(i, n);
                        break;
                    }
                case 0x80:
                case 0x81:
                case 0x82:
                case 0x83:
                case 0x84:
                case 0x85:
                case 0x86:
                case 0x87:
                    {
                        int i = insn & 7;
                        debugPrint(String.Format("add {0}", REGNAME[i]));
                        byte input = readreg(7);
                        byte n = readreg(i);
                        byte result = (byte)(input + n);
                        bool h = (byte)((input & 0xf) + (n & 0xf)) >= 0x10;
                        writereg(7, result);
                        setflags(result == 0, false, h, input + n >= 256);
                        break;
                    }
                case 0x88:
                case 0x89:
                case 0x8a:
                case 0x8b:
                case 0x8c:
                case 0x8d:
                case 0x8e:
                case 0x8f:
                    {
                        int i = insn & 7;
                        debugPrint(String.Format("adc {0}", REGNAME[i]));
                        byte input = readreg(7);
                        byte n = readreg(i);
                        int c = (af >> 4) & 1;
                        byte result = (byte)(input + n + c);
                        bool h = (byte)((input & 0xf) + (n & 0xf) + c) >= 0x10;
                        writereg(7, result);
                        setflags(result == 0, false, h, input + n + c >= 256);
                        break;
                    }
                case 0xc6:
                    {
                        byte n = read(pc++);
                        debugPrint(String.Format("add {0}", n));
                        byte input = readreg(7);
                        byte result = (byte)(input + n);
                        bool h = (byte)((input & 0xf) + (n & 0xf)) >= 0x10;
                        writereg(7, result);
                        setflags(result == 0, false, h, input + n >= 256);
                        break;
                    }
                case 0x90:
                case 0x91:
                case 0x92:
                case 0x93:
                case 0x94:
                case 0x95:
                case 0x96:
                case 0x97:
                    {
                        int i = insn & 7;
                        debugPrint(String.Format("sub {0}", REGNAME[i]));
                        byte a = readreg(7);
                        byte b = readreg(i);
                        byte result = (byte)(a - b);
                        bool h = ((a & 0xf) - (b & 0xf)) < 0;
                        writereg(7, result);
                        setflags(result == 0, true, h, (a - b) < 0);
                        break;
                    }
                case 0xd6:
                    {
                        byte n = read(pc++);
                        debugPrint(String.Format("sub {0}", n));
                        byte a = readreg(7);
                        byte b = n;
                        byte result = (byte)(a - b);
                        bool h = ((a & 0xf) - (b & 0xf)) < 0;
                        writereg(7, result);
                        setflags(result == 0, true, h, (a - b) < 0);
                        break;
                    }
                case 0xe6:
                    {
                        byte n = read(pc++);
                        debugPrint(String.Format("and {0}", n));
                        byte input = readreg(7);
                        byte result = (byte)(input & n);
                        writereg(7, result);
                        setflags(result == 0, false, true, false);
                        break;
                    }
                case 0xc4:
                    {
                        ushort nn = read_16bit_from_pc();
                        debugPrint(String.Format("call NZ, ({0:x})", nn));
                        if (((af >> 7) & 1) == 0)
                        {
                            call(nn);
                        }
                        break;
                    }
                case 0xd4:
                    {
                        ushort nn = read_16bit_from_pc();
                        debugPrint(String.Format("call NC, ({0:x})", nn));
                        if (((af >> 4) & 1) == 0)
                        {
                            call(nn);
                        }
                        break;
                    }
                case 0xcc:
                    {
                        ushort nn = read_16bit_from_pc();
                        debugPrint(String.Format("call Z, ({0:x})", nn));
                        if (getFlagZ())
                        {
                            call(nn);
                        }
                        break;
                    }
                case 0xdc:
                    {
                        ushort nn = read_16bit_from_pc();
                        debugPrint(String.Format("call C, ({0:x})", nn));
                        if (getFlagC())
                        {
                            call(nn);
                        }
                        break;
                    }
                case 0x0a:
                case 0x1a:
                    {
                        int i = insn >> 4;
                        debugPrint(String.Format("ld A, ({0})", REGNAME2[i]));
                        writereg(7, read(readreg2(i)));
                        break;
                    }
                case 0x2a:
                    {
                        debugPrint("ld A, (HL+)");
                        writereg(7, read(readreg2(2)));
                        hl++;
                        break;
                    }
                case 0x3a:
                    {
                        debugPrint("ld A, (HL-)");
                        writereg(7, read(readreg2(2)));
                        hl--;
                        break;
                    }
                case 0xcb:
                    {
                        byte n = read(pc++);
                        int i = n & 7;
                        int pos = (n >> 3) & 7;
                        if (n <= 7)
                        {
                            debugPrint(String.Format("rlc {0}", REGNAME[i]));
                            rlc(i);
                        }
                        else if (0x08 <= n && n <= 0x0f)
                        {
                            debugPrint(String.Format("rrc {0}", REGNAME[i]));
                            rrc(i);
                        }
                        else if (0x10 <= n && n <= 0x17)
                        {
                            debugPrint(String.Format("rl {0}", REGNAME[i]));
                            rl(i);
                        }
                        else if (0x18 <= n && n <= 0x1f)
                        {
                            debugPrint(String.Format("rr {0}", REGNAME[i]));
                            rr(i);
                        }
                        else if (0x20 <= n && n <= 0x27)
                        {
                            debugPrint(String.Format("sla {0}", REGNAME[i]));
                            sla(i);
                        }
                        else if (0x28 <= n && n <= 0x2f)
                        {
                            debugPrint(String.Format("sra {0}", REGNAME[i]));
                            sra(i);
                        }
                        else if (0x30 <= n && n <= 0x37)
                        {
                            debugPrint(String.Format("swap {0}", REGNAME[i]));
                            swap(i);
                        }
                        else if (0x38 <= n && n <= 0x3f)
                        {
                            debugPrint(String.Format("srl {0}", REGNAME[i]));
                            srl(i);
                        }
                        else if (0x40 <= n && n <= 0x7f)
                        {
                            debugPrint(String.Format("bit {0}, {1}", pos, REGNAME[i]));
                            bit(pos, i);
                        }
                        else if (0x80 <= n && n <= 0xbf)
                        {
                            debugPrint(String.Format("res {0}, {1}", pos, REGNAME[i]));
                            res(pos, i);
                        }
                        else if (0xc0 <= n && n <= 0xff)
                        {
                            debugPrint(String.Format("set {0}, {1}", pos, REGNAME[i]));
                            set(pos, i);
                        }
                        else
                        {
                            debugPrint(String.Format("unimplemented insn: cb {0:x}", n));
                            throw new Exception();
                        }
                        break;
                    }
                case 0x0b:
                case 0x1b:
                case 0x2b:
                case 0x3b:
                    {
                        int i = (insn >> 4);
                        if (i == 3) i = 4;
                        debugPrint(String.Format("dec {0}", REGNAME2[i]));
                        ushort orig = readreg2(i);
                        ushort result = (ushort)(orig - 1);
                        writereg2(i, result);
                        tick += 4;
                        break;
                    }
                case 0x1f:
                    {
                        debugPrint("rra");
                        byte v = readreg(7);
                        byte v2 = (byte)(v >> 1 | (getFlagC() ? 1 : 0) << 7);
                        writereg(7, v2);
                        setflags(false, false, false, (v & 1) == 1);
                        break;
                    }
                case 0xee:
                    {
                        byte n = read(pc++);
                        debugPrint(String.Format("xor {0:x}", n));
                        byte v = readreg(7);
                        byte v2 = (byte)(v ^ n);
                        writereg(7, v2);
                        setflags(v2 == 0, false, false, false);
                        break;
                    }
                case 0xce:
                    {
                        byte n = read(pc++);
                        debugPrint(String.Format("adc {0:x}", n));
                        int c = ((af >> 4) & 1);
                        byte v = readreg(7);
                        byte v2 = (byte)(v + n + c);
                        bool half_carry = (v & 0xf) + (n & 0xf) + c > 0xf;
                        bool carry = v + n + c > 0xff;
                        writereg(7, v2);
                        setflags(v2 == 0, false, half_carry, carry);
                        break;
                    }
                case 0x09:
                case 0x19:
                case 0x29:
                case 0x39:
                    {
                        int i = insn >> 4;
                        if (i == 3)
                        {
                            i = 4;
                        }
                        debugPrint(String.Format("add HL {0}", REGNAME2[i]));
                        ushort v = readreg2(2);
                        ushort v2 = readreg2(i);
                        bool half_carry = (v & 0xfff) + (v2 & 0xfff) > 0xfff;
                        bool carry = v + v2 > 0xffff;
                        writereg2(2, (ushort)(v + v2));
                        tick += 4;
                        setflags(((af >> 7) & 1) == 1, false, half_carry, carry);
                        break;
                    }
                case 0xe9:
                    {
                        ushort nn = readreg2(2);
                        debugPrint("jp (HL)");
                        pc = nn;
                        break;
                    }
                case 0xf8:
                    {
                        sbyte n = (sbyte)read(pc++);
                        debugPrint(String.Format("ld hl,sp+  {0:x}", n));
                        tick += 4;
                        bool half_carry = (sp & 0x0f) + (n & 0x0f) > 0x0f;
                        bool carry = (sp & 0xff) + (n & 0xff) > 0xff;
                        writereg2(2, (ushort)(sp + n));
                        setflags(false, false, half_carry, carry);
                        break;
                    }
                case 0xf9:
                    {
                        debugPrint("ld sp, hl");
                        tick += 4;
                        writereg2(4, readreg2(2));
                        break;
                    }
                case 0xb8:
                case 0xb9:
                case 0xba:
                case 0xbb:
                case 0xbc:
                case 0xbd:
                case 0xbe:
                case 0xbf:
                    {
                        int i = insn & 7;
                        debugPrint(String.Format("cp {0}", REGNAME[i]));
                        int a = (int)readreg(7);
                        int val = (int)readreg(i);
                        int v = a - val;
                        setflags(v == 0, true, (a & 0x0f) < (val & 0x0f), v < 0);
                        break;
                    }
                case 0x07:
                    {
                        debugPrint("rlca");
                        byte v = readreg(7);
                        byte v2 = (byte)((v << 1) | (v >> 7));
                        writereg(7, v2);
                        setflags(false, false, false, (v >> 7) == 1);
                        break;
                    }
                case 0x17:
                    {
                        debugPrint("rla");
                        byte v = readreg(7);
                        byte v2 = (byte)(v << 1 | (getFlagC() ? 1 : 0));
                        writereg(7, v2);
                        setflags(false, false, false, (v >> 7) == 1);
                        break;
                    }
                case 0x0f:
                    {
                        debugPrint("rrca");
                        byte v = readreg(7);
                        byte v2 = (byte)(v >> 1 | (v & 1) << 7);
                        writereg(7, v2);
                        setflags(false, false, false, (v & 1) == 1);
                        break;
                    }
                case 0xfb:
                    {
                        debugPrint("ei");
                        ime = true;
                        break;
                    }
                case 0x10:
                    {
                        if (read(pc++) == 0)
                        {
                            debugPrint("stop 0");
                            break;
                        }
                        else
                        {
                            throw new Exception("invalid argument of stop");
                        }
                    }
                case 0x08:
                    {
                        ushort nn = read_16bit_from_pc();
                        debugPrint("ld {0:x}, sp");
                        write(nn, (byte)(sp & 0xff));
                        write((ushort)(nn + 1), (byte)(sp >> 8));
                        break;
                    }
                case 0x2f:
                    {
                        debugPrint("cpl");
                        writereg(7, (byte)(readreg(7) ^ 0xff));
                        setflags(((af >> 7) & 1) == 1, true, true, ((af >> 4) & 1) == 1);
                        break;
                    }
                case 0x27:
                    {
                        debugPrint("daa");
                        byte a = readreg(7);
                        if (!getFlagN())
                        {
                            if (getFlagC() || a > 0x99)
                            {
                                a = (byte)(a + 0x60);
                                setFlagC(true);
                            }
                            if (getFlagH() || (a & 0xf) > 0x9)
                            {
                                a = (byte)(a + 6);
                            }
                        }
                        else
                        {
                            if (getFlagC())
                            {
                                a = (byte)(a - 0x60);
                            }
                            if (getFlagH())
                            {
                                a = (byte)(a - 6);
                            }
                        }
                        writereg(7, a);
                        setFlagZ(a == 0);
                        setFlagH(false);
                        break;
                    }
                case 0xe8:
                    {
                        sbyte n = (sbyte)read(pc++);
                        debugPrint(String.Format("add sp, {0}", n));
                        int a = this.sp;
                        int b = (ushort)n;
                        bool half_carry = (a & 0x0f) + (b & 0x0f) > 0x0f;
                        bool carry = (a & 0xff) + (b & 0xff) > 0xff;
                        setFlagZ(false);
                        setFlagN(false);
                        setFlagH(half_carry);
                        setFlagC(carry);
                        sp = (ushort)(sp + n);
                        tick += 8;
                        break;
                    }
                case 0xde:
                    {
                        byte n = read(pc++);
                        debugPrint(String.Format("sbc A, {0}", n));
                        int c = getFlagC() ? 1 : 0;
                        int a = readreg(7);
                        byte res = (byte)(a - n - c);
                        bool half_carry = (a & 0xf) < (n & 0xf) + c;
                        bool carry = a < n + c;
                        writereg(7, res);
                        setFlagZ(res == 0);
                        setFlagN(true);
                        setFlagH(half_carry);
                        setFlagC(carry);
                        break;
                    }
                case 0x98:
                case 0x99:
                case 0x9a:
                case 0x9b:
                case 0x9c:
                case 0x9d:
                case 0x9e:
                case 0x9f:
                    {
                        int i = (insn & 0xf) - 8;
                        debugPrint(String.Format("sbc A, {0}", REGNAME[i]));
                        int c = getFlagC() ? 1 : 0;
                        int a = readreg(7);
                        int n = readreg(i);
                        byte res = (byte)(a - n - c);
                        bool half_carry = (a & 0xf) < (n & 0xf) + c;
                        bool carry = a < n + c;
                        writereg(7, res);
                        setFlagZ(res == 0);
                        setFlagN(true);
                        setFlagH(half_carry);
                        setFlagC(carry);
                        break;
                    }
                case 0xf2:
                    {
                        debugPrint("ld a, (0xff00+c)");
                        ushort addr = (ushort)(0xff00 | readreg(1));
                        writereg(7, read(addr));
                        break;
                    }
                case 0xe2:
                    {
                        debugPrint("ld (0xff00+c), a");
                        ushort addr = (ushort)(0xff00 | readreg(1));
                        write(addr, readreg(7));
                        break;
                    }
                case 0x37:
                    {
                        debugPrint("scf");
                        setFlagN(false);
                        setFlagH(false);
                        setFlagC(true);
                        break;
                    }
                case 0x3f:
                    {
                        debugPrint("scf");
                        setFlagN(false);
                        setFlagH(false);
                        setFlagC(!getFlagC());
                        break;
                    }
                case 0xa0:
                case 0xa1:
                case 0xa2:
                case 0xa3:
                case 0xa4:
                case 0xa5:
                case 0xa6:
                case 0xa7:
                    {
                        int i = insn & 0x7;
                        debugPrint(String.Format("and {0}", REGNAME[i]));
                        byte result = (byte)(readreg(7) & readreg(i));
                        writereg(7, result);
                        setflags(result == 0, false, true, false);
                        break;
                    }
                default:
                    debugPrint(String.Format("unimplemented insn: {0:x}", insn));
                    throw new Exception();
            }
        }

        void debugPrint(string str)
        {
            //Console.Error.WriteLine(str));
        }

        ushort read_16bit_from_pc()
        {
            byte lsb = read(pc);
            byte msb = read((ushort)(pc + 1));
            pc += 2;
            return (ushort)(lsb | (msb << 8));
        }

        void call(ushort addr)
        {
            sp -= 2;
            tick += 4;
            write(sp, (byte)(pc & 0xff));
            write((ushort)(sp + 1), (byte)(pc >> 8));
            pc = addr;
        }

        void ret()
        {
            byte lsb = read(sp);
            byte msb = read((ushort)(sp + 1));
            sp += 2;
            pc = (ushort)(msb << 8 | lsb);
            tick += 4;
        }

        void rlc(int reg)
        {
            byte orig = readreg(reg);
            byte res = (byte)((orig << 1) | (orig >> 7));
            writereg(reg, res);
            setFlagZ(res == 0);
            setFlagN(false);
            setFlagH(false);
            setFlagC(((orig >> 7) & 1) == 1);
        }

        void rrc(int reg)
        {
            byte orig = readreg(reg);
            byte res = (byte)((orig >> 1) | (orig << 7));
            writereg(reg, res);
            setFlagZ(res == 0);
            setFlagN(false);
            setFlagH(false);
            setFlagC((orig & 1) == 1);
        }

        void rl(int reg)
        {
            byte orig = readreg(reg);
            byte res = (byte)((orig << 1) | (getFlagC() ? 1 : 0));
            writereg(reg, res);
            setFlagZ(res == 0);
            setFlagN(false);
            setFlagH(false);
            setFlagC(((orig >> 7) & 1) == 1);
        }

        void rr(int reg)
        {
            byte orig = readreg(reg);
            byte res = (byte)((orig >> 1) | (getFlagC() ? 1 : 0) << 7);
            writereg(reg, res);
            setFlagZ(res == 0);
            setFlagN(false);
            setFlagH(false);
            setFlagC((orig & 1) == 1);
        }

        void sla(int reg)
        {
            byte orig = readreg(reg);
            byte res = (byte)(orig << 1);
            writereg(reg, res);
            setFlagZ(res == 0);
            setFlagN(false);
            setFlagH(false);
            setFlagC((orig & 0x80) > 0);
        }

        void sra(int reg)
        {
            byte orig = readreg(reg);
            byte res = (byte)((orig >> 1) | (orig & 0x80));
            writereg(reg, res);
            setFlagZ(res == 0);
            setFlagN(false);
            setFlagH(false);
            setFlagC((orig & 1) > 0);
        }

        void swap(int reg)
        {
            byte v = readreg(reg);
            byte res = (byte)(v >> 4 | v << 4);
            writereg(reg, res);
            setFlagZ(res == 0);
            setFlagN(false);
            setFlagH(false);
            setFlagC(false);
        }

        void srl(int reg)
        {
            byte orig = readreg(reg);
            byte res = (byte)(orig >> 1);
            writereg(reg, res);
            setFlagZ(res == 0);
            setFlagN(false);
            setFlagH(false);
            setFlagC((orig & 1) > 0);
        }

        void bit(int pos, int reg)
        {
            bool z = ((readreg(reg) >> pos) & 1) == 0;
            setFlagZ(z);
            setFlagN(false);
            setFlagH(true);
        }

        void res(int pos, int reg)
        {
            writereg(reg, (byte)(readreg(reg) & ~(1 << pos)));
        }

        void set(int pos, int reg)
        {
            writereg(reg, (byte)(readreg(reg) | (1 << pos)));
        }

        void setflags(bool z, bool n, bool h, bool c)
        {
            int f = (z ? 1 : 0) << 7 | (n ? 1 : 0) << 6 | (h ? 1 : 0) << 5 | (c ? 1 : 0) << 4;
            af = (ushort)((af & 0xff00) | f);
        }

        void setFlagZ(bool z)
        {
            af = (ushort)((af & ~(1 << 7)) | (z ? 1 : 0) << 7);
        }

        void setFlagN(bool n)
        {
            af = (ushort)((af & ~(1 << 6)) | (n ? 1 : 0) << 6);
        }

        void setFlagH(bool h)
        {
            af = (ushort)((af & ~(1 << 5)) | (h ? 1 : 0) << 5);
        }

        void setFlagC(bool c)
        {
            af = (ushort)((af & ~(1 << 4)) | (c ? 1 : 0) << 4);
        }

        bool getFlagZ()
        {
            return ((af >> 7) & 1) == 1;
        }

        bool getFlagN()
        {
            return ((af >> 6) & 1) == 1;
        }

        bool getFlagH()
        {
            return ((af >> 5) & 1) == 1;
        }

        bool getFlagC()
        {
            return ((af >> 4) & 1) == 1;
        }

        byte read(ushort addr)
        {
            byte res;
            if (addr <= 0x7fff)
            {
                res = cartridge.read(addr);
            }
            else if (0xc000 <= addr && addr <= 0xdfff)
            {
                res = ram[addr & 0x1fff];
            }
            else if (0xff80 <= addr && addr <= 0xfffe)
            {
                res = hram[addr & 0x7f];
            }
            else if (0xff04 <= addr && addr <= 0xff07)
            {
                res = timer.read(addr);
            }
            else if ((0x8000 <= addr && addr <= 0x9fff) ||
                     (0xfe00 <= addr && addr <= 0xfe9f) ||
                     (0xff40 <= addr && addr <= 0xff45) ||
                     (0xff47 <= addr && addr <= 0xff4b))
            {
                res = ppu.read(addr);
            }
            else if (addr == 0xff0f)
            {
                res = int_flag;
            }
            else if (addr == 0xffff)
            {
                res = int_enable;
            }
            else
            {
                res = 0;
            }
            tick += 4;
            return res;
        }

        void write(ushort addr, byte val)
        {
            //debugPrint(String.Format("({0:x}) <- {1:x}", addr, val));
            if (addr <= 0x7fff)
            {
                cartridge.write(addr, val);
            }
            else if (0xc000 <= addr && addr <= 0xdfff)
            {
                ram[addr & 0x1fff] = val;
            }
            else if (0xff80 <= addr && addr <= 0xfffe)
            {
                hram[addr & 0x7f] = val;
            }
            else if (addr == 0xff01)
            {
                Console.Write("{0}", Convert.ToChar(val).ToString());
                //Console.Error.Write("<{0}>", Convert.ToChar(val).ToString());
            }
            else if (0xff04 <= addr && addr <= 0xff07)
            {
                timer.write(addr, val);
            }
            else if ((0x8000 <= addr && addr <= 0x9fff) ||
                     (0xfe00 <= addr && addr <= 0xfe9f) ||
                     (0xff40 <= addr && addr <= 0xff45) ||
                     (0xff47 <= addr && addr <= 0xff4b))
            {
                ppu.write(addr, val);
            }
            else if (addr == 0xff46)
            {
                do_dma(val);
            }
            else if (addr == 0xff0f)
            {
                int_flag = val;
            }
            else if (addr == 0xffff)
            {
                int_enable = val;
            }
            else if (0xff00 <= addr && addr < 0xff80)
            {
                // do nothing
            }
            tick += 4;
        }

        void do_dma(byte val)
        {
            if (val < 0x80 || 0xdf < val)
            {
                throw new Exception("invalid DMA source address");
            }

            ushort src_base = (ushort)(val << 8);
            ushort dst_base = 0xfe00;

            for (int i = 0; i < 0xa0; i ++)
            {
                byte tmp = read((ushort)(src_base | i));
                write((ushort)(dst_base | i), tmp);
            }
        }

        byte readreg(int i)
        {
            switch (i)
            {
                case 0: return (byte)(bc >> 8);
                case 1: return (byte)(bc & 0xff);
                case 2: return (byte)(de >> 8);
                case 3: return (byte)(de & 0xff);
                case 4: return (byte)(hl >> 8);
                case 5: return (byte)(hl & 0xff);
                case 6: return read(hl);
                case 7: return (byte)(af >> 8);
                default: throw new Exception("readreg: unknown i");
            }
        }

        void writereg(int i, byte v)
        {
            switch (i)
            {
                case 0: bc = (ushort)(v << 8 | (bc & 0xff)); break;
                case 1: bc = (ushort)((bc & 0xff00) | v); break;
                case 2: de = (ushort)(v << 8 | (de & 0xff)); break;
                case 3: de = (ushort)((de & 0xff00) | v); break;
                case 4: hl = (ushort)(v << 8 | (hl & 0xff)); break;
                case 5: hl = (ushort)((hl & 0xff00) | v); break;
                case 6: write(hl, v); break;
                case 7: af = (ushort)(v << 8 | (af & 0xff)); break;
                default: throw new Exception("writereg: unknown i");
            }
        }

        ushort readreg2(int i)
        {
            switch (i)
            {
                case 0: return bc;
                case 1: return de;
                case 2: return hl;
                case 3: return af;
                case 4: return sp;
                default: throw new Exception("readreg2: unknwon i");
            }
        }

        void writereg2(int i, ushort v)
        {
            switch (i)
            {
                case 0: bc = v; break;
                case 1: de = v; break;
                case 2: hl = v; break;
                case 3: af = v; break;
                case 4: sp = v; break;
                default: throw new Exception("writereg2: unknown i");
            }
        }
    }
}
