using System;
using SharedLib.GameEngine;

namespace MainServer;

public class GameRoom
{
    public string RoomId { get; set; }
    public IGamePlayer? Player1 { get; set; }
    public IGamePlayer? Player2 { get; set; }
    public bool Player1Ready { get; set; } = false;
    public bool Player2Ready { get; set; } = false;
    public string[,] Board { get; set; } = new string[GameLogic.BOARD_SIZE, GameLogic.BOARD_SIZE];
    public string CurrentPlayer { get; set; } = "X";
    public bool IsGameActive { get; set; } = false;
    public DateTime LastActivity { get; set; } = DateTime.Now;
    public DateTime StartTime { get; set; } = DateTime.Now;
    public bool IsAIGame { get; set; } = false;
    public string AISymbol { get; set; } = "O";

    public GameRoom(string roomId)
    {
        RoomId = roomId;
        InitializeBoard();
    }

    private void InitializeBoard()
    {
        Board = GameLogic.CreateEmptyBoard();
    }

    public bool IsFull => IsAIGame ? Player1 != null : (Player1 != null && Player2 != null);
    public bool IsEmpty => Player1 == null && Player2 == null;
    public bool BothPlayersReady => IsAIGame ? Player1Ready : (Player1Ready && Player2Ready && IsFull);

    public IGamePlayer? GetOpponent(IGamePlayer player)
    {
        return player == Player1 ? Player2 : Player1;
    }

    public bool AddPlayer(IGamePlayer client)
    {
        if (Player1 == null)
        {
            Player1 = client;
            client.PlayerSymbol = "X";
            return true;
        }
        else if (Player2 == null)
        {
            Player2 = client;
            client.PlayerSymbol = "O";
            return true;
        }
        return false;
    }

    public bool IsCurrentPlayerAI()
    {
        return IsAIGame && CurrentPlayer == AISymbol;
    }

    public IGamePlayer? GetHumanPlayer()
    {
        return IsAIGame ? Player1 : null;
    }

    public void RemovePlayer(IGamePlayer client)
    {
        if (Player1 == client)
            Player1 = null;
        else if (Player2 == client)
            Player2 = null;

        IsGameActive = false;
    }

    public string GetPlayer1Name()
    {
        if (Player1 is ClientHandler client)
            return client.AuthenticatedProfile?.PlayerName ?? "Player 1";
        return "Player 1";
    }

    public string GetPlayer2Name()
    {
        if (IsAIGame)
            return "AI";
        if (Player2 is ClientHandler client)
            return client.AuthenticatedProfile?.PlayerName ?? "Player 2";
        return "Player 2";
    }

    public TimeSpan GetGameDuration()
    {
        return DateTime.Now - StartTime;
    }
}
