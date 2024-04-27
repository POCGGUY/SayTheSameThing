using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.Windows.Forms;
using System.IO;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace Client
{
    public partial class Form1 : Form
    {
        int port = 7000;
        TcpClient client = new TcpClient();
        string host;
        string userName;
        StreamReader Reader;
        StreamWriter Writer;
        async Task SendMessageAsync(StreamWriter writer)
        {
            try
            {
                if (textBox3.Text != null)
                {
                    string message = textBox3.Text;
                    textBox3.Text = null;
                    await writer.WriteLineAsync(message);
                    await writer.FlushAsync();
                }
            }
            catch (Exception e)
            {
                textBox4.Text += "Ошибка: " + e.Message + Environment.NewLine;
            }
        }
        async Task ReceiveMessageAsync(StreamReader reader)
        {
            while (true)
            {
                try
                {
                    string message = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(message)) continue;
                    if (message == "ef3b0e33877e8f8292346a022fee8d8b")
                    {
                        textBox4.Text = null;
                        continue;
                    }
                    Print(message);
                }
                catch
                {
                    break;
                }
            }
        }
        void Print(string message)
        {
            textBox4.Text += message + Environment.NewLine;
        }
        public Form1()
        {
            InitializeComponent();
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private async void button1_Click(object sender, EventArgs e)
        {
            if (textBox1.Text != "" && textBox2.Text != "")
            {
                textBox1.Enabled = false;
                textBox2.Enabled = false;
                label1.Enabled = false;
                label2.Enabled = false;
                button1.Enabled = false;
                host = textBox1.Text;
                userName = textBox2.Text;
                timer1.Enabled = true;
                await makeConnection();
            }
            else
            {
                Print("Ошибка, не введён ip адрес или имя пользователя");
            }
        }

        private void textBox3_TextChanged(object sender, EventArgs e)
        {

        }

        private void label3_Click(object sender, EventArgs e)
        {

        }

        private async void button2_Click(object sender, EventArgs e)
        {
            await SendMessageAsync(Writer);
        }
        async Task makeConnection()
        {
            try
            {
                client.Connect(host, port);
                Reader = new StreamReader(client.GetStream());
                Writer = new StreamWriter(client.GetStream());
                if (Writer is null || Reader is null) return;
                await Writer.WriteLineAsync(userName);
                await Writer.FlushAsync();
                await Task.Run(() => ReceiveMessageAsync(Reader));
            }
            catch (Exception e)
            {
                textBox4.Text += "Ошибка при подключении: " + e.Message + Environment.NewLine;
                textBox1.Enabled = true;
                textBox2.Enabled = true;
                label1.Enabled = true;
                label2.Enabled = true;
                button1.Enabled = true;
            }
        }
        private async void timer1_Tick(object sender, EventArgs e)
        {

        }

        private void textBox4_TextChanged(object sender, EventArgs e)
        {
            textBox4.SelectionStart = textBox4.Text.Length;
            textBox4.ScrollToCaret();
        }
        private void label4_Click(object sender, EventArgs e)
        {

        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private async void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyValue == (char)Keys.Enter)
            {
                await SendMessageAsync(Writer);
            }
        }
    }
}
