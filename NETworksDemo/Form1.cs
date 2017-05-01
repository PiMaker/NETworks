using System;
using System.IO;
using System.Text;
using System.Windows.Forms;
using NETworks;

namespace NETworksDemo
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            this.InitializeComponent();

            Control.CheckForIllegalCrossThreadCalls = false;

            Console.SetOut(new ControlWriter(this.textBox1));
            Network.EnableLog = true;
            Console.WriteLine("NETworks demo intialized");
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Network.Register("demo", this.RequestCallback);
        }

        private Response RequestCallback(string command, byte[] body)
        {
            Console.WriteLine("RequestAny received: " + command);
            return command == "ping"
                ? new Response(ResponseStatus.Ok, new byte[0], "pong")
                : new Response(ResponseStatus.ServerError, new byte[0], "not a ping");
        }

        private async void button2_Click(object sender, EventArgs e)
        {
            var response = await Network.RequestAny("demo", "ping", new byte[0]);
            Console.WriteLine("Response received: " + response.Message + " (Status: " + response.Status + ")");
        }

        private void button3_Click(object sender, EventArgs e)
        {
            Network.Shutdown();
        }

        public class ControlWriter : TextWriter
        {
            private readonly TextBox textbox;

            public ControlWriter(TextBox textbox)
            {
                this.textbox = textbox;
            }

            public override Encoding Encoding => Encoding.Unicode;

            public override void Write(char value)
            {
                this.textbox.Text += value;
                this.textbox.Select(this.textbox.Text.Length - 1, 0);
                this.textbox.ScrollToCaret();
            }

            public override void Write(string value)
            {
                this.textbox.Text += value;
                this.textbox.Select(this.textbox.Text.Length - 1, 0);
                this.textbox.ScrollToCaret();
            }

            public override void WriteLine(string value)
            {
                this.textbox.Text += value + this.NewLine;
                this.textbox.Select(this.textbox.Text.Length - 1, 0);
                this.textbox.ScrollToCaret();
            }
        }
    }
}