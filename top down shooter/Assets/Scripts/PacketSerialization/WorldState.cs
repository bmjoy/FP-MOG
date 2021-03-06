﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public class WorldManager
{
    public WorldState snapshot;

    public WorldManager()
    {
        snapshot = new WorldState(0);
    }

    public void TakeSnapshot(int serverTick, List<Player> players, List<RayState> rayStates)
    {
        snapshot = new WorldState(serverTick);

        foreach (Player p in players)
        {
            if (p.playerGameobject != null)
                snapshot.AddState(p.GetState());
        }

        foreach (RayState ray in rayStates)
        {
            snapshot.AddState(ray);
        }
    }
}


public static class ServerPktSerializer
{
    public static byte[] Serialize(WorldState ws)
    {
        List<byte> pkt = new List<byte>();
        ws.AddBytesTo(pkt);
        return pkt.ToArray();
    }

    public static WorldState DeSerialize(byte[] bytes)
    {
        int offset = 0;
        return new WorldState(bytes, ref offset);
    }
}


public class WorldState
{
    // The current Server Tick 
    public int serverTickSeq;
    // On which client tick this message applies.
    public int clientTickAck;
    // the amount of ticks the packet was at the server
    public long timeSpentInServerInTicks;  

    public List<PlayerState> playersState = new List<PlayerState>();
    public List<RayState> raysState = new List<RayState>();

    public WorldState(int serverTickSeq)
    {
        this.serverTickSeq = serverTickSeq;
    }

    // Call This function just before send is being called
    public void UpdateStatistics(int clientTickAck, long timeSpentInServerInTicks)
    {
        this.clientTickAck = clientTickAck;
        this.timeSpentInServerInTicks = timeSpentInServerInTicks;
    }

    public void AddState(PlayerState state)
    {
        playersState.Add(state);
    }

    public void AddState(RayState state)
    {
        raysState.Add(state);
    }

    // Deserialize data received.
    public WorldState(byte[] data, ref int offset)
    {
        serverTickSeq = NetworkUtils.DeserializeInt(data, ref offset);
        clientTickAck = NetworkUtils.DeserializeInt(data, ref offset);
        timeSpentInServerInTicks = NetworkUtils.DeserializeLong(data, ref offset);

        ushort len;

        len = NetworkUtils.DeserializeUshort(data, ref offset);
        for (int i = 0; i < len; i++)
            AddState(new PlayerState(data, ref offset));

        len = NetworkUtils.DeserializeUshort(data, ref offset);
        for (int i = 0; i < len; i++)
            AddState(new RayState(data, ref offset));
    }

    // Serializes this object and add it as bytes to a given byte list.
    public void AddBytesTo(List<byte> byteList)
    {
        NetworkUtils.SerializeInt(byteList, serverTickSeq);
        NetworkUtils.SerializeInt(byteList, clientTickAck);
        NetworkUtils.SerializeLong(byteList, timeSpentInServerInTicks);

        NetworkUtils.SerializeUshort(byteList, (ushort) playersState.Count);
        foreach (var playerState in playersState)
            playerState.AddBytesTo(byteList);

        NetworkUtils.SerializeUshort(byteList, (ushort) raysState.Count);
        foreach (var rayState in raysState)
            rayState.AddBytesTo(byteList);
    }
}


public static class ClientPktSerializer
{
    public static byte[] Serialize(ClientInput ci)
    {
        List<byte> pkt = new List<byte>();
        ci.AddBytesTo(pkt);
        return pkt.ToArray();
    }

    public static ClientInput DeSerialize(byte[] bytes)
    {
        int offset = 0;
        return new ClientInput(bytes, ref offset);
    }
}


public class ClientInput
{
    // The current Client Tick 
    public int clientTickSeq;
    // On which server tick this message applies.
    public int serverTickAck;
    // the amount of ticks the packet was at the client
    public long timeSpentInClientInTicks;  

    public List<InputEvent> inputEvents = new List<InputEvent>();

    public ClientInput() { }

    // Call This function just before send is being called
    public void UpdateStatistics(int clientTickSeq, int serverTickAck, long timeSpentInClientInTicks)
    {
        this.clientTickSeq = clientTickSeq;
        this.serverTickAck = serverTickAck;
        this.timeSpentInClientInTicks = timeSpentInClientInTicks;
    }

    public void AddEvent(InputEvent ie)
    {
        inputEvents.Add(ie);
    }

    // Deserialize data received.
    public ClientInput(byte[] data, ref int offset)
    {
        clientTickSeq = NetworkUtils.DeserializeInt(data, ref offset);
        serverTickAck = NetworkUtils.DeserializeInt(data, ref offset);
        timeSpentInClientInTicks = NetworkUtils.DeserializeLong(data, ref offset);

        ushort len;

        len = NetworkUtils.DeserializeUshort(data, ref offset);
        for (int i = 0; i < len; i++)
            AddEvent(new InputEvent(data, ref offset));
    }

    // Serializes this object and add it as bytes to a given byte list.
    public void AddBytesTo(List<byte> byteList)
    {
        NetworkUtils.SerializeInt(byteList, clientTickSeq);
        NetworkUtils.SerializeInt(byteList, serverTickAck);
        NetworkUtils.SerializeLong(byteList, timeSpentInClientInTicks);

        NetworkUtils.SerializeUshort(byteList, (ushort) inputEvents.Count);
        foreach (var ie in inputEvents)
            ie.AddBytesTo(byteList);
    }
}


public struct InputEvent
{
    public int serverTick; // For Lag Compensation.
    public float deltaTime; // The delta time from the last Server Tick.
    // Player Inputs
    public byte keys; // A bit mask [1: W, 2: A, 4: S, 8: D] => [0001: W, 0010: A, 0100: S, 1000: D]
    public float zAngle; // The angle between the mouse and the player according to the x axis.
    public bool mouseDown; // True for Fire

    public InputEvent(int serverTick, float deltaTime, byte keys, float zAngle, bool mouseDown)
    {
        this.serverTick = serverTick;
        this.deltaTime = deltaTime;
        this.keys = keys;
        this.zAngle = zAngle;
        this.mouseDown = mouseDown;
    }

    // Deserialize data received.
    public InputEvent(byte[] data, ref int offset)
    {
        serverTick = NetworkUtils.DeserializeInt(data, ref offset);
        deltaTime = NetworkUtils.DeserializeFloat(data, ref offset);
        keys = NetworkUtils.DeserializeByte(data, ref offset);
        zAngle = NetworkUtils.DeserializeFloat(data, ref offset);
        mouseDown = NetworkUtils.DeserializeBool(data, ref offset);
    }

    // Serializes this object and add it as bytes to a given byte list.
    public void AddBytesTo(List<byte> byteList)
    {
        NetworkUtils.SerializeInt(byteList, serverTick);
        NetworkUtils.SerializeFloat(byteList, deltaTime);
        NetworkUtils.SerializeByte(byteList, keys);
        NetworkUtils.SerializeFloat(byteList, zAngle);
        NetworkUtils.SerializeBool(byteList, mouseDown);
    }
}


public struct StatsState
{
    public ushort playerId;
    public int kills;
    public long rtt;

    public StatsState(ushort playerId, int kills, long rtt)
    {
        this.playerId = playerId;
        this.kills = kills;
        this.rtt = rtt;
    }

    // Deserialize data received.
    public StatsState(byte[] data, ref int offset)
    {
        playerId = NetworkUtils.DeserializeUshort(data, ref offset);
        kills = NetworkUtils.DeserializeInt(data, ref offset);
        rtt = NetworkUtils.DeserializeLong(data, ref offset);
    }

    // Serializes this object and add it as bytes to a given byte list.
    public void AddBytesTo(List<byte> byteList)
    {
        NetworkUtils.SerializeUshort(byteList, playerId);
        NetworkUtils.SerializeInt(byteList, kills);
        NetworkUtils.SerializeLong(byteList, rtt);
    }
}


public struct PlayerState
{
    public ushort playerId;
    public float zAngle;
    public Vector2 pos;

    public PlayerState(ushort playerId, float zAngle, Vector2 pos)
    {
        this.playerId = playerId;
        this.zAngle = zAngle;
        this.pos = pos;
    }

    // Deserialize data received.
    public PlayerState(byte[] data, ref int offset)
    {
        playerId = NetworkUtils.DeserializeUshort(data, ref offset);
        zAngle = NetworkUtils.DeserializeFloat(data, ref offset);
        pos = NetworkUtils.DeserializeVector2(data, ref offset);
    }

    // Serializes this object and add it as bytes to a given byte list.
    public void AddBytesTo(List<byte> byteList)
    {
        NetworkUtils.SerializeUshort(byteList, playerId);
        NetworkUtils.SerializeFloat(byteList, zAngle);
        NetworkUtils.SerializeVector2(byteList, pos);
    }

    public static void Interp(List<PlayerState> prevStates, List<PlayerState> nextStates, float f,
                              ref List<PlayerState> playerStates)
    {
        playerStates.Clear();

        PlayerState temp;
        
        foreach (var nextState in nextStates)
        {
            var prevState = prevStates.FirstOrDefault(x => x.playerId == nextState.playerId);
            // Check whether the end result is also contained in the start snapshot
            // if so we can interpolate otherwise we simply set the value.
            if (!nextState.Equals(default(PlayerState))) {
                // interpolate
                var interpPosition = Vector2.Lerp(prevState.pos, nextState.pos, f);
                var interpRotation = Mathf.LerpAngle(prevState.zAngle, nextState.zAngle, f);

                temp = new PlayerState(nextState.playerId, interpRotation, interpPosition);
                playerStates.Add(temp);
            } 
            else
            {
                // set the value directly
                playerStates.Add(nextState);
            }
        }
    }
}


public struct RayState
{
    public ushort owner;
    public float zAngle;
    public Vector2 pos;

    public RayState(ushort owner, float zAngle, Vector2 pos)
    {
        this.owner = owner;
        this.zAngle = zAngle;
        this.pos = pos;
    }

    // Deserialize data received.
    public RayState(byte[] data, ref int offset)
    {
        owner = NetworkUtils.DeserializeUshort(data, ref offset);
        zAngle = NetworkUtils.DeserializeFloat(data, ref offset);
        pos = NetworkUtils.DeserializeVector2(data, ref offset);
    }

    // Serializes this object and add it as bytes to a given byte list.
    public void AddBytesTo(List<byte> byteList)
    {
        NetworkUtils.SerializeUshort(byteList, owner);
        NetworkUtils.SerializeFloat(byteList, zAngle);
        NetworkUtils.SerializeVector2(byteList, pos);
    }
}
