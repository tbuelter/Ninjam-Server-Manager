using System;
using System.Configuration;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace Ninjam_Server_Manager
{
    class Node
    {

        public string Path { get; set; }
        public string Ip { get; set; }
        public string Port { get; set; }
        
        public Node(string _path, string _ip, string _port) {
            Path = _path;
            Ip = _ip;
            Port = _port;
        }
    }

    class Program
    {
        public static Node[] GetNodeList()
        {
            //string str = System.Reflection.Assembly.GetEntryAssembly().Location
            string path = "C:\\Users\\tobib\\serverlist.txt";
            string[] arrLines = File.ReadAllLines(path);


            Node[] nodes = new Node[arrLines.Length];
            for (int i = 0; i < arrLines.Length; i++)
            {
                string[] tokens = arrLines[i].Split(";");
                nodes[i] = new Node(tokens[0], tokens[1], tokens[2]);
            }
            return nodes;
        }

        // Get all the Nodes with Path, IP and Port
        public Node[] nodes = GetNodeList();

        // Reset Config
        public void ResetConfigs()
        {
            //string[] pathList = GetNodeList();
            int[] status = GetStatus();

        }

        // Get Login Names of specified server
        public static void GetLogins(int id)
        {

        }
        // Gets list of the Paths were the config files are

        // Returns an Integer Array, Index is server ID and the content is the number of active users. (-1 being Server Dead)
        public int[] GetStatus()
        {
            //string[] pathList = GetNodeList();
            //Node[] nodes = GetNodeList();
            int[] activeList = new int[nodes.Length];

            for (int i = 0; i < nodes.Length; i++)
            {
                if (IsAlive(nodes[i].Path)){
                    string[] files = Directory.GetFiles(nodes[i].Path);
                    foreach (string file in files)
                    {
                        if (file.Contains("User "))
                        {
                            string[] tokens = file.Split(" ");

                            activeList[i] += Convert.ToInt32(tokens[1]);

                        }
                    }
                }
                else
                {
                    activeList[i] = -1;
                }
            }
            return activeList;
        }
        // Returns ID of an empty server
        public int findEmptyServer()
        {
            int[] list = GetStatus();
            for (int i = 0; i < list.Length; i++)
            {
                if(list[i] == 0)
                {
                    return list[i];
                }
            }
            return 99;
        }

        // Adds the Login Data to the specified Server config
        public int AddLogin(string name, string pw, int id)
        {
           
            // If no ID is specified from the User, find an Empty server.
            if (id == -1)
            {
                id = findEmptyServer();
            }
            try
            {
                //Node[] nodes = GetNodeList();

                //string[] pathList = GetNodeList();
                string path = nodes[id].Path + "\\example.cfg";
                string[] arrLines = File.ReadAllLines(path);
                string[] lines = new string[arrLines.Length + 1];
                int index = 0;
                for (int i = 0; i < arrLines.Length; i++)
                {
                    // TODO: Filter Double usernames
                    if (index == 0 && i >= 30 && !arrLines[i].Contains("User"))
                    {
                        lines[i] = "User " + name + " " + pw + " CBTKRMHVP";
                        index = 1;
                    }
                    lines[i + index] = arrLines[i];
                }
                File.WriteAllLines(path, lines);
                return id;
            }
            catch
            {
                Console.WriteLine("AddLogin Fail");
                return -1;
            }
        }
        //Check if server is Alive with 3 retries
        public static bool IsAlive(string path)
        {
            File.Create(path + "\\isalive");
            for (int i = 0; i < 3; i++)
            {
                if (File.Exists(path + "\\alive"))
                {
                    File.Delete(path + "\\alive");
                    return true;
                }
                System.Threading.Thread.Sleep(10);
            }
            return false;
        }

        // To add userdata to a config send via TCP: "Login NAME PASSWORD ID"
        // Get Status: "Status"
        // If ID is -1 it gets an empty server
        public void Main()
        {

            string managerId = ConfigurationManager.AppSettings["ManagerID"];
            TcpListener server = null;
            try
            {
                // Set the TcpListener on port 13000.
                Int32 port = 13000;
                IPAddress localAddr = IPAddress.Parse("127.0.0.1");

                // TcpListener server = new TcpListener(port);
                server = new TcpListener(localAddr, port);

                // Start listening for client requests.
                server.Start();

                // Buffer for reading data
                Byte[] bytes = new Byte[256];
                String data = null;

                // Enter the listening loop.
                while (true)
                {
                    Console.Write("Waiting for a connection... ");

                    // Perform a blocking call to accept requests.
                    // You could also use server.AcceptSocket() here.
                    TcpClient client = server.AcceptTcpClient();
                    Console.WriteLine("Connected!");

                    data = null;

                    // Get a stream object for reading and writing
                    NetworkStream stream = client.GetStream();

                    int i;

                    // Loop to receive all the data sent by the client.
                    while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                    {
                        // Translate data bytes to a ASCII string.
                        data = System.Text.Encoding.ASCII.GetString(bytes, 0, i);
                        Console.WriteLine("Received: {0}", data);

                        // Add Login to Config
                        if (data.Contains("Login "))
                        {
                            string[] tokens = data.Split(" ");
                            if (tokens.Length == 4) {
                                string name = tokens[1];
                                string pw = tokens[2];
                                int serverId = Convert.ToInt32(tokens[3]);
                                int id = AddLogin(name, pw, serverId);
                                if(id != -1)
                                {
                                    // Send back a response.
                                    byte[] response = System.Text.Encoding.ASCII.GetBytes("Login Added: " + name + " " + pw + ".\nOn Server: " + nodes[id].Ip + ":" + nodes[id].Port );
                                    stream.Write(response, 0, response.Length);
                                    Console.WriteLine("Sent: {0}", data);    
                                }
                            }
                            else
                            {
                                Console.WriteLine("Invalid Logindata");
                            }
                        }

                        // Get Server/Node Status
                        if (data.Contains("Status"))
                        {
                            int[] status = GetStatus();
                            string statusList = string.Empty;
                            for (int j = 0; j < status.Length; j++)
                            {
                                if (status[i] != -1)
                                {
                                    statusList += ("Alive Server " + i + ";users " + status[i] + nodes[i].Ip + ":" + nodes[i].Port + "\n");
                                }
                                else
                                {
                                    statusList += ("Dead Server " + nodes[i].Ip + ":" + nodes[i].Port + ";manager " + managerId + "\n");
                                }
                            }
                            // Send back a response.
                            byte[] response = System.Text.Encoding.ASCII.GetBytes(statusList);
                            stream.Write(response, 0, response.Length);
                            Console.WriteLine("Sent: {0}", data);
                        }

                    }

                    // Shutdown and end connection
                    client.Close();
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException: {0}", e);
            }
            finally
            {
                // Stop listening for new clients.
                server.Stop();
            }

            Console.WriteLine("\nHit enter to continue...");
            Console.Read();
        }
    }
}
