using System.Diagnostics;

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

            client.OnLog += message => Log("Client: " + message);
            server.OnLog += message => Log("Server: " + message);
        }

        private void Log(string message)
        {
            Debug.WriteLine(message);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            server.TryTick();
            client.TryTick();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            server.Start();
            client.Start();
            timer1.Enabled = true;

        }
    }
}