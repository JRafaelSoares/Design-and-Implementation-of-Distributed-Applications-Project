using System;
using System.Collections.Generic;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Windows.Forms;
using MSDAD.Shared;

namespace Puppet_Master
{
    public class PuppetRoom
    {
        public String location { get; }
        public uint capacity { get; }
        public String name { get; }
    }
    public partial class Form1 : Form
    {
        private List<PuppetRoom> Locations;
        public Dictionary<String, String> Clients;
        public Dictionary<String, String> Servers;
        private TcpChannel channel;
        private FolderBrowserDialog FolderBrowser = new FolderBrowserDialog();
        public Form1()
        {
            InitializeComponent();
            this.channel = new TcpChannel(10001);
            ChannelServices.RegisterChannel(channel, false);
            this.Clients = new Dictionary<string, string>();
            this.Servers = new Dictionary<string, string>();
            this.Locations = new List<PuppetRoom>();
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void CreateServer(String[] url, String serverId, String maxFaults, String minDelay, String maxDelay)
        {
            String ip = url[0];
            IMSDADPCS pcs = (IMSDADPCS)Activator.GetObject(typeof(IMSDADPCS), "tcp://" + ip + ":10000/PCS");
            if (pcs != null)
            {
                String args = String.Format("{0} {1} {2} {3} {4} {5}", serverId, url[2], url[1], maxFaults, minDelay, maxDelay);
                pcs.CreateProcess("Server", args);
            }
            else
            {
                this.textBox1.Text += String.Format("Unable to contact PCS at ip {0}", ip);
            }
        }

        private String[] ParseUrl(String url)
        {
            String[] Items = url.Split(':');
            String ip = Items[1].Substring(2);
            String port = Items[2].Split('/')[0];
            String networkName = Items[2].Split('/')[1];
            return new string[] { ip, port, networkName };
        }
        private void ParseCommand(String command)
        {
            String[] items = command.Split();
            switch (items[0])
            {
                case "Server":
                    String[] url = ParseUrl(items[2]);
                    String serverId = items[1];
                    String maxFaults = items[3];
                    String minDelay = items[4];
                    String maxDelay = items[5];
                    CreateServer(url, serverId, maxFaults, minDelay, maxDelay);
                    break;
                case "Client":
                    break;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            ParseCommand(this.CommandBox.Text);
            //Do method Async
            this.textBox1.Text += this.CommandBox.Text + "\r\n";
            this.CommandBox.Text = "";
        }

        private void CommandBox_TextChanged(object sender, EventArgs e)
        {

        }

        private void button1_Click_1(object sender, EventArgs e)
        {
            if (FolderBrowser.ShowDialog() == DialogResult.OK)
            {

            }
        }

        private void button1_Click_2(object sender, EventArgs e)
        {

        }

        private void button1_Click_3(object sender, EventArgs e)
        {

        }
    }
}
