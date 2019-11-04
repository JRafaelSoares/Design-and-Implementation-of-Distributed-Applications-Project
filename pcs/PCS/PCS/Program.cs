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
                switch (type)
                {
                    case "Server":
                        Process.Start(AppDomain.CurrentDomain.BaseDirectory + "Server.exe", args);
                        break;

                    case "Client:":
                        Process.Start(AppDomain.CurrentDomain.BaseDirectory + "Client.exe", args);
                        break;
                }
               
            }
            static void Main()
            {
                TcpChannel channel = new TcpChannel(10000);
                ChannelServices.RegisterChannel(channel, false);
                RemotingConfiguration.RegisterWellKnownServiceType(typeof(PCS), "PCS", WellKnownObjectMode.Singleton);
                Console.WriteLine(" < enter > para sair...");
                Console.ReadLine();
            }
        }
    }
}
