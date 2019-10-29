﻿using System;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.IO;
using System.Text;
using System.Linq;
using System.Threading;

// Rough Idea
/*
StartServer();

{
    
    select(); // NON BLOCKING


    for reads
        if server:
            accept 
            add to writes
            add to reads
        else:
            Game.InputFromPlayer(Player, data)

    if newTick
        for clients:
            message_queues.put(new List<Byte[]> {len, Game.GetSnapshot(Player)} );
            sock.BeginSend(meassage_queues[s]);


    for writes
        try:
            next_msg = message_queues[s].get_nowait()
        except Exception:
            pass
        else:
            s.send(next_msg)
       
    for errors

}
*/

public class User
{
    public Player player;
    public Socket sock = null;
    // Size of receive buffer.
    protected const int BufferSize = 512;
    // Receive buffer.  
    public byte[] buffer = new byte[BufferSize];
    // Received data string.
    protected MemoryStream ms = new MemoryStream();

    public int bytesRec = 0;
    protected int offset = 0;
    protected int len = 0;

    protected int cut;
    protected string str;

    public User(Socket sock)
    {
        this.sock = sock;
        player = new Player();
    }

    public void ReceiveOnce()
    {
        // From one receive try to get as many messages as possible.
        offset = 0;
        if (len == 0)
        {
            len = Globals.DeSerializePrefix(buffer, offset);
            offset = 4;
        }

        while (offset < bytesRec)
        {
            cut = Math.Min(len, bytesRec - offset);
            ms.Write(buffer, offset, cut);
            len -= cut;
            offset += cut;

            if (len == 0)
            {
                // Process message in stream.
                str = string.Format("Echoed test = {0}", Encoding.ASCII.GetString(ms.ToArray()));
                Console.WriteLine(str);
                // Clean the MemoryStream.
                ms.SetLength(0);

                // For the next message within this recevie.
                if (offset < bytesRec)
                {
                    len = Globals.DeSerializePrefix(buffer, offset);
                    offset += 4;
                }
            }
        }
    }
}


public class Server : MonoBehaviour
{
    [SerializeField]
    public GameObject playerPrefab;

    private ServerLoop serverLoop;
    private static bool isRunning;
    private readonly int MaximumPlayers = 10;
    private static Socket listenerSocket;

    private List<User> instantiateJobs = new List<User>();

    private static List<Socket> InputsOG = new List<Socket>();
    private static List<Socket> OutputsOG = new List<Socket>();
    private static List<Socket> ErrorsOG = new List<Socket>();

    private static Dictionary<Socket, User> clients = new Dictionary<Socket, User>();

    private void Start()
    {
        serverLoop = new ServerLoop(playerPrefab);
        StartServer();
    }

    private void FixedUpdate()
    {
        lock (instantiateJobs)
        {
            Player p;
            for (int i = 0; i < instantiateJobs.Count; i++)
            {
                p = instantiateJobs[i].player;
                p.obj = serverLoop.AddPlayer(p.playerId);
                p.rb = p.obj.GetComponent<Rigidbody2D>();
            }

            instantiateJobs.Clear();
        }

        // Every three ticks the update function will return true.
        if (serverLoop.Update(clients.Values.Select(x => x.player).ToList()))
        {
            byte[] snapshot = serverLoop.GetSnapshot();
            foreach (Socket sock in OutputsOG)
            {
                try
                {
                    SendReply(clients[sock], snapshot);
                }
                catch
                {
                    OnUserDisconnect(sock);
                }
            }
        }
    }

    private void StartServer()
    {
        // Establish the local endpoint for the socket. 
        IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
        IPAddress ipAddress = ipHostInfo.AddressList[0];
        IPEndPoint localEndPoint = new IPEndPoint(ipAddress, Globals.port);

        Debug.Log("The server is running  on: " + localEndPoint.Address.ToString() + " : " + localEndPoint.Port.ToString());
        // Create a TCP/IP socket.  
        listenerSocket = new Socket(ipAddress.AddressFamily,
            SocketType.Stream, ProtocolType.Tcp);

        // Bind the socket to the local endpoint and listen for incoming connections.  
        try
        {
            listenerSocket.Bind(localEndPoint);
            listenerSocket.Listen(100);

            Thread selectThr = new Thread(StartListening);
            selectThr.Start();
        }
        catch (Exception e)
        {
            Debug.Log(e.ToString());
            Application.Quit();
        }
    }

    public void StartListening()
    {
        Console.WriteLine("Main Loop");

        InputsOG.Add(listenerSocket);

        List<Socket> Inputs = new List<Socket>();
        List<Socket> Outputs = new List<Socket>();
        List<Socket> Errors = new List<Socket>();

        Socket tmp;
        User usr;

        isRunning = true;
        while (isRunning)
        {

            Inputs = InputsOG.ToList();

            Socket.Select(Inputs, null, null, -1);

            foreach (Socket sock in Inputs)
            {
                if (sock == listenerSocket)
                {
                    tmp = sock.Accept();
                    usr = new User(tmp);

                    // Send to the connected client his ID.
                    SendReply(usr, BitConverter.GetBytes(usr.player.playerId));

                    // Instantiate at the main thread, not here.
                    lock (instantiateJobs)
                    {
                        instantiateJobs.Add(usr);
                    }

                    Console.WriteLine("Client connected");
                    Console.WriteLine(string.Format("Connected: {0}", tmp.Connected));

                    clients.Add(tmp, usr);

                    InputsOG.Add(tmp);
                    OutputsOG.Add(tmp);
                }
                else
                {
                    usr = clients[sock];
                    usr.bytesRec = sock.Receive(usr.buffer, 0, usr.buffer.Length, 0);

                    if (usr.bytesRec <= 0)
                        OnUserDisconnect(sock);
                    else
                        // Receive the data.
                        clients[sock].ReceiveOnce();
                }
            }
        }

        Debug.Log("Stop Listening");
    }

    void SendReply(User user, byte[] msgArray)
    {
        byte[] wrapped = Globals.Serializer(msgArray);
        user.sock.BeginSend(wrapped, 0, wrapped.Length, SocketFlags.None, EndSend, user);
    }

    void EndSend(IAsyncResult iar)
    {
        User user = (iar.AsyncState as User);
        user.sock.EndSend(iar);
    }

    void OnApplicationQuit()
    {
        Debug.Log("Application Quit\nClosing Socket");
        CloseServer();
    }

    static void CloseServer()
    {
        isRunning = false;
        listenerSocket.Shutdown(SocketShutdown.Both);
        listenerSocket.Close();
    }

    static void OnUserDisconnect(Socket sock)
    {
        try
        {
            sock.Close();
            clients.Remove(sock);

            InputsOG.Remove(sock);
            OutputsOG.Remove(sock);

            Console.WriteLine("Client Disconnected");
        } 
        catch
        {
        }
    }
}