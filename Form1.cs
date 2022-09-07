using System.Diagnostics.Metrics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace diggb_gui
{
    public partial class Form1 : Form
    {
        static int WIDTH = 160;
        static int HEIGHT = 144;
        Interpreter intp;
        byte[] frame_buffer;
        public Form1()
        {
            InitializeComponent();
            frame_buffer = new byte[WIDTH * HEIGHT * 4];
            FileStream fs = new FileStream("red.gb", FileMode.Open, FileAccess.Read);
            int fileSize = (int)fs.Length;
            byte[] buf = new byte[fileSize];
            fs.Read(buf, 0, fileSize);
            intp = new Interpreter(buf);
        }
        private void timer1_Tick(object sender, EventArgs e)
        {
            int elapsed_tick = 0;
            while (elapsed_tick < 456 * (144 + 10))
            {
                elapsed_tick += intp.Step();
            }
            frame_buffer = intp.ppu.frame_buffer;
            Invalidate();
        }

        private void Form1_Paint(object sender, PaintEventArgs e)
        {
            Bitmap bitmap = new Bitmap(WIDTH, HEIGHT);
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            byte[] buf = new byte[bitmap.Width * bitmap.Height * 4];
            for (int i = 0; i < WIDTH * HEIGHT; i++)
            {
                buf[i * 4 + 0] = frame_buffer[i];
                buf[i * 4 + 1] = frame_buffer[i];
                buf[i * 4 + 2] = frame_buffer[i];
                buf[i * 4 + 3] = 255;
            }
            Marshal.Copy(buf, 0, data.Scan0, buf.Length);
            bitmap.UnlockBits(data);
            pictureBox1.Image = bitmap;

        }

        Nullable<Key> translate_keycode(Keys key)
        {
            switch (key)
            {
                case Keys.Down: return Key.Down;
                case Keys.Up: return Key.Up;
                case Keys.Left: return Key.Left;
                case Keys.Right: return Key.Right;
                case Keys.Return: return Key.Start;
                case Keys.RShiftKey: return Key.Select;
                case Keys.X: return Key.A;
                case Keys.Z: return Key.B;
                default: return null;
            }
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            Nullable<Key> key = translate_keycode(e.KeyCode);
            if (key != null)
            {
                intp.joypad.keydown((Key)key);
            }
        }

        private void Form1_KeyUp(object sender, KeyEventArgs e)
        {
            Nullable<Key> key = translate_keycode(e.KeyCode);
            if (key != null)
            {
                intp.joypad.keyup((Key)key);
            }
        }
    }
}