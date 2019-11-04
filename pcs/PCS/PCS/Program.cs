using MSDAD.Shared;
using System;
using System.Diagnostics;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;

namespace MSDAD
{
    namespace PCS
    {
        class PCS : MarshalByRefObject, IMSDADPCS
        {
            void IMSDADPCS.CreateProcess(String type, String args)
            {
                Process proc = new Process();
                proc.StartInfo.Arguments = args;
                switch (type)
                {
                    case "Server":
                        proc.StartInfo.FileName = AppDomain.CurrentDomain.BaseDirectory + "Server.exe";
                        break;

                    case "Client:":
                        proc.StartInfo.FileName = AppDomain.CurrentDomain.BaseDirectory + "Client.exe";
                        break;
                }
                Process.Start(AppDomain.CurrentDomain.BaseDirectory + "Server.exe", args);
                proc.Dispose();
            }
            static void Main()
            {
                TcpChannel channel = new TcpChannel(10000);
                ChannelServices.RegisterChannel(channel, false);
                RemotingConfiguration.RegisterWellKnownServiceType(typeof(IMSDADPCS), "PCS", WellKnownObjectMode.Singleton);
                Console.WriteLine(" < enter > para sair...");
                Console.ReadLine();
            }
        }
    }
}
