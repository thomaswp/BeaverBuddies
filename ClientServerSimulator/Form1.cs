using System.Diagnostics;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using Timer = System.Windows.Forms.Timer;

namespace ClientServerSimulator
{
    public partial class Form1 : Form
    {
        Client client;
        Server server;

        public Form1()
        {
            InitializeComponent();

            client = new Client();
            server = new Server();

            client.OnLog += message => this.textBoxClient.Text += message + "\r\n";
            server.OnLog += message => this.textBoxServer.Text += message + "\r\n";
        }

        private void Log(string message)
        {
            Debug.WriteLine(message);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            
        }

        private void buttonStartServer_Click(object sender, EventArgs e)
        {
            server.Start();
        }

        private void buttonStartClient_Click(object sender, EventArgs e)
        {
            client.Start();
        }

        private void timerServer_Tick(object sender, EventArgs e)
        {
            server.TryTick();
        }

        private void timerClient_Tick(object sender, EventArgs e)
        {
            client.TryTick();
        }

        private void nudServerSpeed_ValueChanged(object sender, EventArgs e)
        {
            int speed = (int)nudServerSpeed.Value;
            UpdateSpeed(speed, timerServer);
        }

        private void nudClientSpeed_ValueChanged(object sender, EventArgs e)
        {
            int speed = (int)nudClientSpeed.Value;
            UpdateSpeed(speed, timerClient);
        }

        private void UpdateSpeed(int speed, Timer timer)
        {
            timer.Enabled = speed > 0;
            if (speed != 0)
            {
                timer.Interval = 100 / speed;
            }
        }
    }
}