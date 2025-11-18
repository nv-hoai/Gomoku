using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SharedLib.Models;

namespace MainServer;

public class MatchmakingService
{
    private readonly ConcurrentDictionary<string, GameRoom> gameRooms = new();
    private readonly Queue<ClientHandler> waitingPlayers = new();
    private readonly object waitingLock = new object();

    public async Task<GameRoom> FindOrCreateRoom(ClientHandler client)
    {
        GameRoom? room = null;
        ClientHandler? waitingPlayer = null;

        // Do the synchronous work inside the lock
        lock (waitingLock)
        {
            if (waitingPlayers.Count > 0)
            {
                waitingPlayer = waitingPlayers.Dequeue();
                if (waitingPlayer.IsConnected && waitingPlayer.CurrentRoom != null)
                {
                    if (waitingPlayer.CurrentRoom.AddPlayer(client))
                    {
                        room = waitingPlayer.CurrentRoom;
                    }
                }
            }

            if (room == null)
            {
                var roomId = Guid.NewGuid().ToString();
                room = new GameRoom(roomId);
                gameRooms[roomId] = room;

                if (room.AddPlayer(client))
                {
                    waitingPlayers.Enqueue(client);
                }
            }
        }

        await client.SendMessage($"PLAYER_SYMBOL:{client.PlayerSymbol}");

        return room;
    }

    public async Task<GameRoom> CreateAIRoom(ClientHandler client)
    {
        var roomId = Guid.NewGuid().ToString();
        var room = new GameRoom(roomId)
        {
            IsAIGame = true,
            AISymbol = "O"
        };

        if (room.AddPlayer(client))
        {
            gameRooms[roomId] = room;
            await client.SendMessage($"PLAYER_SYMBOL:{client.PlayerSymbol}");
            return room;
        }

        throw new InvalidOperationException("Failed to add player to AI room");
    }

    public void LeaveRoom(IGamePlayer client, GameRoom room)
    {
        room.RemovePlayer(client);

        if (room.IsEmpty)
        {
            gameRooms.TryRemove(room.RoomId, out _);
            Console.WriteLine($"Room {room.RoomId} removed");
        }
    }

    public async Task CleanupRoomsAsync()
    {
        var cutoffTime = DateTime.Now.AddMinutes(-10);
        var roomsToRemove = new List<string>();

        foreach (var kvp in gameRooms)
        {
            var room = kvp.Value;
            if (room.LastActivity < cutoffTime || room.IsEmpty)
            {
                roomsToRemove.Add(kvp.Key);
            }
        }

        foreach (var roomId in roomsToRemove)
        {
            if (gameRooms.TryRemove(roomId, out _))
            {
                Console.WriteLine($"Cleaned up inactive room: {roomId}");
            }
        }
    }

    public int GetActiveGameCount()
    {
        return gameRooms.Count(kvp => kvp.Value.IsGameActive);
    }

    public int GetTotalRoomCount()
    {
        return gameRooms.Count;
    }

    public IEnumerable<GameRoom> GetActiveRooms()
    {
        return gameRooms.Values.Where(r => r.IsGameActive).ToList();
    }

    public IEnumerable<GameRoom> GetAllRooms()
    {
        return gameRooms.Values.ToList();
    }
}
