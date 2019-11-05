using System;
using System.Collections.Generic;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Windows.Forms;
using MSDAD.Shared;
using System.Threading;
using System.IO;
using System.Net.Sockets;

namespace Puppet_Master
{

    public partial class Form1 : Form
    {
        private List<PuppetRoom> Locations;
        public Dictionary<Puppet, IMSDADClientPuppet> Clients;
        public Dictionary<Puppet, IMSDADServerPuppet> Servers;
        private TcpChannel channel;
        private OpenFileDialog FolderBrowser = new OpenFileDialog();
        public delegate void RemoteAsyncDelegate();
        private int timeToSleep = 0;

        public Form1()
        {
            InitializeComponent();  
            this.channel = new TcpChannel(10001);
            ChannelServices.RegisterChannel(channel, false);
            this.Clients = new Dictionary<Puppet, IMSDADClientPuppet>();
            this.Servers = new Dictionary<Puppet, IMSDADServerPuppet>();
            this.Locations = new List<PuppetRoom>();
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }


        private void AddRoom(String location, uint capacity, String name)
        {
            safeSleep();
            this.Locations.Add(new PuppetRoom(location, capacity, name));

            foreach(IMSDADServerPuppet serverURL in this.Servers.Values)
            {
                serverURL.AddRoom(location, capacity, name);   
            }
        }

        private void CreateServer(String[] url, String serverId, String maxFaults, String minDelay, String maxDelay)
        {
            //Contact PCS
            safeSleep();
            String ip = url[0];
            IMSDADPCS pcs = (IMSDADPCS)Activator.GetObject(typeof(IMSDADPCS), "tcp://" + ip + ":10000/PCS");

            if (pcs != null)
            {
                String args = String.Format("{0} {1} {2} {3} {4} {5} {6}", url[0], serverId, url[2], url[1], maxFaults, minDelay, maxDelay);
                
                //Give Server URL of all Servers currently running
                String servers = "";
                foreach (Puppet server in this.Servers.Keys) {
                    servers += server.serverUrl + " ";
                }

                args += String.Format(" {0} {1}", this.Servers.Values.Count, servers);

                
                //Give Server All Locations
                String locals = "";
                foreach (PuppetRoom room in this.Locations)
                {
                    locals += room.ToString()  + " ";
                }
                args += String.Format("{0} {1}", this.Locations.Count, locals);

                //Contact Server and get Ref
                pcs.CreateProcess("Server", args);
                String fullUrl = "tcp://" + ip + ":" + url[1] + "/" + url[2];
                IMSDADServerPuppet remoteRef = (IMSDADServerPuppet)Activator.GetObject(typeof(IMSDADServerPuppet), fullUrl);
                if (remoteRef != null)
                {
                    this.Servers.Add(new Puppet(serverId , fullUrl), remoteRef);
                }
                else
                {
                    this.textBox1.Text += String.Format("Server at URL {0} was not created", fullUrl);
                }
            }
            else
            {
                this.textBox1.Text += String.Format("Unable to contact PCS at ip {0}", ip);
            }
        }

        private void CreateClient(String[] clientUrl, String clientId, String serverUrl, String scriptName)
        {
            String clientIp = clientUrl[0];
            IMSDADPCS pcs = (IMSDADPCS)Activator.GetObject(typeof(IMSDADPCS), "tcp://" + clientIp + ":10000/PCS");
            if (pcs != null)
            {
                String fullUrl = "tcp://" + clientIp + ":" + clientUrl[1] + "/" + clientUrl[2];
                
                String args = String.Format("{0} {1} {2} {3} {4} {5}", clientId, clientUrl[1], clientUrl[2], serverUrl, scriptName, clientIp);
                pcs.CreateProcess("Client", args);
                IMSDADClientPuppet remoteRef = (IMSDADClientPuppet)Activator.GetObject(typeof(IMSDADClientPuppet), fullUrl);
                if (remoteRef != null)
                {
                    this.Clients.Add(new Puppet(clientId, fullUrl), remoteRef);
                }
                else
                {
                    this.textBox1.Text += String.Format("Client at URL {0} was not created", fullUrl);
                }
            }
            else
            {
                this.textBox1.Text += String.Format("Unable to contact PCS at ip {0}", clientIp);
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
                    String[] clientUrl = ParseUrl(items[2]);
                    String clientId = items[1];
                    String serverUrl = items[3];
                    String scriptName = items[4];
                    CreateClient(clientUrl, clientId, serverUrl, scriptName);
                    break;
                case "AddRoom":
                    AddRoom(items[1], UInt32.Parse(items[2]), items[3]);
                    break;
                case "Status":
                    Status();
                    break;
                case "Crash":
                    Crash(items[1]);
                    break;
                case "Wait":
                    Wait(Int32.Parse(items[1]));
                    break;
                default:

                    break;
            }
        }

        //Testar
        private void Status()
        {

            try { 
            
                safeSleep();

                foreach (IMSDADServerPuppet server in Servers.Values)
                {
                    RemoteAsyncDelegate remDelegate = new RemoteAsyncDelegate(server.Status);
                    IAsyncResult result = remDelegate.BeginInvoke(null, null);
                    result.AsyncWaitHandle.WaitOne();
                    remDelegate.EndInvoke(result);
                }

            } catch(SocketException)
            {
                System.Console.WriteLine("Could not locate server");
            }

        }

        //Testar 
        private void Crash(string serverId)
        {
            try
            {
                safeSleep();
                Puppet p = new Puppet(serverId, null);
                RemoteAsyncDelegate remDelegate = new RemoteAsyncDelegate(Servers[p].Crash);
                //IAsyncResult RemAr = remDelegate.BeginInvoke(null, null);
                //RemAr.AsyncWaitHandle.WaitOne();
                //remDelegate.EndInvoke(RemAr);
                Servers.Remove(p);
            } catch (SocketException)
            {
                System.Console.WriteLine("Could not locate server");
            }

        }

        private void Wait(int time)
        {
            timeToSleep = time;
        }

        private void safeSleep()
        {
            if(timeToSleep != 0)
            {
                Thread.Sleep(timeToSleep);
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
            string folderPath;

            if (FolderBrowser.ShowDialog() == DialogResult.OK)
            {
                folderPath = FolderBrowser.FileName;
            } else
            {
                this.textBox1.Text += "Cannot open file given";
                return;
            }
            StreamReader reader = File.OpenText(folderPath);
            String line;
            while ((line = reader.ReadLine()) != null)
            {
                ParseCommand(line);
            }

        }


        private void button1_Click_3(object sender, EventArgs e)
        {

            try
            {
                foreach (IMSDADServerPuppet server in Servers.Values)
                {
                    RemoteAsyncDelegate remDelegate = new RemoteAsyncDelegate(server.ShutDown);
                    remDelegate.BeginInvoke(null, null);
                }

                foreach(IMSDADClientPuppet client in Clients.Values)
                {
                    RemoteAsyncDelegate remDelegate = new RemoteAsyncDelegate(client.ShutDown);
                    remDelegate.BeginInvoke(null, null);
                }

            } catch (SocketException)
            {
                System.Console.WriteLine("Could not locate server");
            }

            Servers.Clear();
            Clients.Clear();

        }
    }
    public class PuppetRoom
    {
        public String location { get; }
        public uint capacity { get; }
        public String name { get; }

        public PuppetRoom(String location, uint capacity, String name)
        {
            this.location = location;
            this.capacity = capacity;
            this.name = name;
        }

        public override string ToString()
        {
            return String.Format("{0} {1} {2}", this.location, this.capacity, this.name);
        }
    }

    public class Puppet
    {
        public String serverId { get; }
        public String serverUrl { get; }

        public Puppet(String serverId, String serverUrl)
        {
            this.serverId = serverId;
            this.serverUrl = serverUrl;
        }
        public override bool Equals(Object obj)
        {
            //Check for null and compare run-time types.
            if ((obj == null) || !this.GetType().Equals(obj.GetType()))
            {
                return false;
            }
            else
            {
                Puppet s = (Puppet)obj;
                return s.serverId == this.serverId;
            }
        }

        public override int GetHashCode()
        {
            return this.serverId.GetHashCode();
        }

    }
}


