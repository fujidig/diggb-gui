using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace diggb_gui
{
    public class Cartridge
    {
        byte[] rom;
        byte[] ram;
        bool mode;
        byte bank_no_lower;
        byte bank_no_upper;
        byte num_rom_banks;
        bool ram_enable;

        public Cartridge(byte[] rom_)
        {
            rom = rom_;
            mode = false;
            bank_no_lower = 0;
            bank_no_upper = 0;
            num_rom_banks = (byte)(2 << rom[0x0148]);
            int ram_size = 0;
            switch (rom[0x149])
            {
                case 0: ram_size = 0; break;
                case 1: ram_size = 2 * 1024; break;
                case 2: ram_size = 8 * 1024; break;
                case 3: ram_size = 32 * 1024; break;
                case 4: ram_size = 128 * 1024; break;
                case 5: ram_size = 64 * 1024; break;
            }
            ram = new byte[ram_size];
        }

        int rom_bank_no()
        {
            int bank_no = mode ? bank_no_lower : bank_no_upper << 5 | bank_no_lower;
            if (bank_no == 0 || bank_no == 0x20 || bank_no == 0x40 || bank_no == 0x60)
            {
                bank_no++;
            }
            return bank_no & (num_rom_banks - 1);
        }

        int ram_bank_no()
        {
            return mode ? bank_no_upper : 0;
        }

        public void write(ushort addr, byte val)
        {
            if (0x0000 <= addr && addr < 0x1fff)
            {
                ram_enable = (val & 0xf) == 0xa;
            }
            if (0x2000 <= addr && addr < 0x3fff)
            {
                bank_no_lower = (byte)(val & 0x1f);
            }
            else if (0x4000 <= addr && addr < 0x5fff)
            {
                bank_no_upper = (byte)(val & 3);
            }
            else if (0x6000 <= addr && addr < 0x7fff)
            {
                mode = (val & 1) > 0;
            }
            else if (0xa000 <= addr && addr <= 0xbfff)
            {
                if (ram_enable)
                {
                    int offset = (8 * 1024) * ram_bank_no();
                    ram[(addr & 0x1fff) + offset] = val;
                }
            }
        }

        public byte read(ushort addr)
        {
            if (addr <= 0x3fff)
            {
                return rom[addr];
            }
            else if (0x4000 <= addr && addr <= 0x7fff)
            {
                int offset = (16 * 1024) * rom_bank_no();
                return rom[(addr & 0x3fff) + offset];
            }
            else if (0xa000 <= addr && addr <= 0xbfff)
            {
                if (ram_enable)
                {
                    int offset = (8 * 1024) * ram_bank_no();
                    return ram[(addr & 0x1fff) + offset];
                }
                else
                {
                    return 0xff;
                }
            }
            else
            {
                throw new Exception("Unexpected address");
            }
        }
    }
}
