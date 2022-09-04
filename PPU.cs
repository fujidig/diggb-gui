using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrackBar;

namespace diggb_gui
{
    public class PPU
    {
        static int SCREEN_W = 160;
        static int SCREEN_H = 144;

        enum BGPriority
        {
            Color0, Color123
        }

        byte[] vram;
        byte[] oam;
        byte lcdc, stat, scy, scx, ly, lyc, dma, bgp, obp0, obp1, wy, wx;
        public bool irq_vblank, irq_lcdc;
        ushort counter;
        public byte[] frame_buffer;
        byte[] scanline;
        BGPriority[] bg_prio;

        public PPU()
        {
            vram = new byte[0x2000];
            oam = new byte[0xa0];
            frame_buffer = new byte[SCREEN_W * SCREEN_H];
            scanline = new byte[SCREEN_W];
            bg_prio = new BGPriority[SCREEN_W];
            lcdc = 0x80;
            stat = 0x02;
        }

        public void write(ushort addr, byte val)
        {
            if (0x8000 <= addr && addr <= 0x9fff)
            {
                if ((stat & 0x3) != 3)
                {
                    vram[addr & 0x1fff] = val;
                    return;
                }
                else
                {
                    return;
                }
            }
            if (0xfe00 <= addr && addr <= 0xfe9f)
            {
                if ((stat & 0x3) == 0 || (stat & 0x3) == 1)
                {
                    oam[addr & 0x00ff] = val;
                    return;
                }
                else
                {
                    return;
                }
            }
            switch (addr)
            {
                case 0xff40:
                    {
                        if ((lcdc & 0x80) != (val & 0x80))
                        {
                            ly = 0;
                            counter = 0;
                            int mode = ((val & 0x80) > 0) ? 2 : 0;
                            stat = (byte)((stat & 0xf8) | mode);
                            update_mode_interrupt();
                        }
                        lcdc = val;
                        return;
                    }
                case 0xff41: stat = (byte)((val & 0xf8) | (stat & 3)); return;
                case 0xff42: scy = val; return;
                case 0xff43: scx = val; return;
                case 0xff44: return;
                case 0xff45:
                    {
                        if (lyc != val)
                        {
                            lyc = val;
                            update_lyc_interrupt();
                        }
                        return;
                    }
                case 0xff47: bgp = val; return;
                case 0xff48: obp0 = val; return;
                case 0xff49: obp1 = val; return;
                case 0xff4a: wy = val; return;
                case 0xff4b: wx = val; return;
                default: throw new Exception("Unexpected address");
            }
        }

        public byte read(ushort addr)
        {
            if (0x8000 <= addr && addr <= 0x9fff)
            {
                if ((stat & 0x3) != 3)
                {
                    return vram[addr & 0x1fff];
                }
                else
                {
                    return 0xff;
                }
            }

            if (0xfe00 <= addr && addr <= 0xfe9f)
            {
                if ((stat & 0x3) == 0 || (stat & 0x3) == 1)
                {
                    return oam[addr & 0x00ff];
                }
                else
                {
                    return 0xff;
                }
            }
            switch (addr)
            {
                case 0xff40: return lcdc;
                case 0xff41: return stat;
                case 0xff42: return scy;
                case 0xff43: return scx;
                case 0xff44: return ly;
                case 0xff45: return lyc;
                case 0xff46: return dma;
                case 0xff47: return bgp;
                case 0xff48: return obp0;
                case 0xff49: return obp1;
                case 0xff4a: return wy;
                case 0xff4b: return wx;
                default: throw new Exception("Unexpected address");
            }
        }

        public void update(byte tick)
        {
            if ((lcdc & 0x80) == 0)
            {
                return;
            }
            counter = (ushort)(counter + tick);
            switch (stat & 3)
            {
                case 2:
                    {
                        if (counter >= 80)
                        {
                            counter -= 80;
                            stat = (byte)((stat & 0xf8) | 3);
                            render_scanline();
                        }
                        break;
                    }
                case 3:
                    {
                        if (counter >= 172)
                        {
                            counter -= 172;
                            stat = (byte)(stat & 0xf8);
                            update_mode_interrupt();
                        }
                        break;
                    }
                case 0:
                    {
                        if (counter >= 204)
                        {
                            counter -= 204;
                            ly += 1;
                            if (ly >= SCREEN_H)
                            {
                                stat = (byte)((stat & 0xf8) | 1);
                                irq_vblank = true;
                            }
                            else
                            {
                                stat = (byte)((stat & 0xf8) | 2);
                            }
                            update_lyc_interrupt();
                            update_mode_interrupt();
                        }
                        break;
                    }
                case 1:
                    {
                        if (counter >= 456)
                        {
                            counter -= 456;
                            ly += 1;
                            if (ly >= 154)
                            {
                                stat = (byte)((stat & 0xf8) | 2);
                                ly = 0;
                                update_mode_interrupt();
                            }
                            update_lyc_interrupt();
                        }
                        break;
                    }
            }
        }

        void update_mode_interrupt()
        {
            switch (stat & 0x3)
            {
                case 0:
                    if ((stat & 0x8) > 0) irq_lcdc = true; return;
                case 1:
                    if ((stat & 0x10) > 0) irq_lcdc = true; return;
                case 2:
                    if ((stat & 0x20) > 0) irq_lcdc = true; return;
            }
        }

        void update_lyc_interrupt()
        {
            if (ly == lyc)
            {
                stat |= 0x4;
                if ((stat & 0x40) > 0)
                {
                    irq_lcdc = true;
                }
            }
            else
            {
                stat = (byte)(stat & ~0x4);
            }
        }

        void render_scanline()
        {
            if ((lcdc & 1) > 0)
            {
                render_bg();
            }
            if ((lcdc & 2) > 0)
            {
                render_sprites();
            }
            for (int x = 0; x < SCREEN_W; x++)
            {
                int ix = x + ly * SCREEN_W;
                frame_buffer[ix] = scanline[x];
            }
        }

        void render_bg()
        {
            byte tile_x = (byte)(scx >> 3);
            byte tile_y = (byte)((scy + ly) >> 3);
            byte offset_x = (byte)(scx & 7);
            byte offset_y = (byte)((scy + ly) & 7);
            (byte, byte) tile = fetch_bg_tile(tile_x, tile_y, offset_y);
            bool window = false;

            for (int x = 0; x < SCREEN_W; x ++) {
                if ((lcdc & 0x20) > 0) {
                    if (wy <= ly && wx == x + 7) {
                        tile_x = 0;
                        tile_y = (byte)((ly - wy) >> 3);
                        offset_x = 0;
                        offset_y = (byte)((ly - wy) & 0x7);
                        tile = fetch_window_tile(tile_x, tile_y, offset_y);
                        window = true;
                    }
                }

                byte color_no = get_color_no(tile, (byte)(7 - offset_x));
                byte color = map_color(color_no, bgp);

                bg_prio[x] = color_no == 0 ? BGPriority.Color0 : BGPriority.Color123;
                scanline[x] = color;

                offset_x += 1;

                if (offset_x >= 8) {
                    offset_x = 0;
                    tile_x += 1;

                    if (window) {
                        tile = fetch_window_tile(tile_x, tile_y, offset_y);
                    }
                    else
                    {
                        tile = fetch_bg_tile(tile_x, tile_y, offset_y);
                    }
                }
            }
        }

        void render_sprites()
        {
            // todo
        }

        byte map_color(byte color_no, byte palette)
        {
            switch ((palette >> (color_no << 1)) & 3) {
                case 0: return 0xff;
                case 1: return 0xaa;
                case 2: return 0x55;
                case 3: default:  return 0;
            }
        }

        byte get_color_no((byte, byte) tile, byte bitpos) {
            byte lo_bit = (byte)((tile.Item1 >> bitpos) & 1);
            byte hi_bit = (byte)((tile.Item2 >> bitpos) & 1);
            return (byte)(hi_bit << 1 | lo_bit);
       }

        (byte, byte) fetch_bg_tile(byte tile_x, byte tile_y, byte offset_y)
        {
            ushort tile_map_base = ((lcdc & 0x8) > 0) ? (ushort)0x1c00 : (ushort)0x1800;
            return fetch_bg_window_tile(tile_x, tile_y, offset_y, tile_map_base);
        }

        (byte, byte) fetch_window_tile(byte tile_x, byte tile_y, byte offset_y)
        {
            ushort tile_map_base = ((lcdc & 0x40) > 0) ? (ushort)0x1c00 : (ushort)0x1800;
            return fetch_bg_window_tile(tile_x, tile_y, offset_y, tile_map_base);
        }

        (byte, byte) fetch_bg_window_tile(byte tile_x, byte tile_y, byte offset_y, ushort tile_map_base)
        {
            ushort tile_map_addr = (ushort)(tile_map_base | ((tile_x & 0x1f) + (tile_y << 5)));
            byte tile_no = vram[tile_map_addr];
            return fetch_tile(tile_no, offset_y, (lcdc & 0x10) > 0);
        }

        (byte, byte) fetch_tile(byte tile_no, byte offset_y, bool tile_data_sel)
        {
            ushort tile_data_addr = tile_data_sel ?
                (ushort)(tile_no << 4) :
                (ushort)(0x1000 + ((sbyte)tile_no << 4));
            ushort row_addr = (ushort)(tile_data_addr + (offset_y << 1));
            byte tile0 = vram[row_addr];
            byte tile1 = vram[row_addr + 1];
            return (tile0, tile1);
        }

    }
}
