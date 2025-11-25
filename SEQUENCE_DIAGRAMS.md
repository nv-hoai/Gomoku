# Gomoku Server - Sequence Diagrams

This document contains detailed sequence diagrams for the Gomoku game server architecture, including the new UI monitoring features and worker load balancing system.

---

## 1. Server Startup with UI Monitoring

```mermaid
sequenceDiagram
    participant UI as MainWindow (WPF)
    participant Server as MainServer
    participant LB as LoadBalancer
    participant DB as Database
    participant TCP as TCP Listeners

    UI->>Server: StartAsync()
    
    Server->>Server: Check if already running
    Server->>TCP: Dispose old listeners
    Server->>TCP: Create new TcpListener (Port 5000)
    Server->>TCP: Create worker TcpListener (Port 5001)
    
    Server->>Server: isRunning = true
    Server->>UI: OnLogMessage("Server started")
    
    par Start Background Tasks
        Server->>Server: ListenForWorkersAsync()
        Server->>Server: CleanupRoomsAsync()
        Server->>LB: MonitorLoadAsync()
    end
    
    Server->>TCP: Start listening for clients
    Server->>UI: Update UI (Status: Running, Button: Stop)
    
    Note over UI,Server: Server ready to accept connections
```

---

## 2. Worker Connection & Status Tracking

```mermaid
sequenceDiagram
    participant Worker as Worker Server
    participant Server as MainServer
    participant UI as MainWindow UI
    participant LB as LoadBalancer

    Worker->>Server: TCP Connect (Port 5001)
    Server->>Server: AcceptTcpClientAsync()
    Server->>Server: Create WorkerConnection
    
    Server->>Worker: Request WORKER_INIT
    Worker->>Server: WORKER_INIT:{WorkerId, Capabilities}
    
    Server->>Server: Add to workers dictionary
    Server->>Server: Set Status = Idle
    Server->>UI: OnWorkerConnected(workerId, endpoint)
    
    UI->>UI: Add to WorkersDataGrid
    UI->>UI: Update WorkersCountText
    
    Note over Server,LB: Worker ready for AI tasks
    
    loop Worker Status Updates
        Server->>UI: OnWorkerStatusChanged(workerId, status, currentTask)
        UI->>UI: Update worker row in grid
        UI->>UI: Refresh DataGrid display
    end
```

---

## 3. Client Authentication with UI Update

```mermaid
sequenceDiagram
    participant Client as Unity Client
    participant Handler as ClientHandler
    participant Server as MainServer
    participant UI as MainWindow UI
    participant DB as Database

    Client->>Server: TCP Connect (Port 5000)
    Server->>Handler: Create ClientHandler
    Server->>UI: OnClientConnected(clientId, totalClients)
    UI->>UI: Add client with PlayerName="Unknown"
    
    Client->>Handler: LOGIN:{Username, Password}
    Handler->>DB: Query user credentials
    DB->>Handler: User data + Profile
    
    Handler->>Handler: Validate password hash
    Handler->>Handler: Set AuthenticatedProfile
    Handler->>Server: NotifyClientAuthenticated(clientId, playerName)
    
    Server->>UI: OnClientAuthenticated(clientId, playerName)
    UI->>UI: Update ClientViewModel.PlayerName
    UI->>UI: Refresh ClientsDataGrid
    
    Handler->>Client: LOGIN_SUCCESS:{Profile data}
    
    Note over Client,UI: Client now shows actual player name in UI
```

---

## 4. Client Logout with UI Reset

```mermaid
sequenceDiagram
    participant Client as Unity Client
    participant Handler as ClientHandler
    participant Server as MainServer
    participant UI as MainWindow UI
    participant DB as Database

    Client->>Handler: LOGOUT
    Handler->>DB: UpdateStatusAsync(profileId, isOnline=false)
    DB->>Handler: Status updated
    
    Handler->>Handler: Clear AuthenticatedProfile
    Handler->>Handler: Clear PlayerInfo
    Handler->>Server: NotifyClientAuthenticated(clientId, "Unknown")
    
    Server->>UI: OnClientAuthenticated(clientId, "Unknown")
    UI->>UI: Update ClientViewModel.PlayerName = "Unknown"
    UI->>UI: Refresh ClientsDataGrid
    
    Handler->>Client: LOGOUT_SUCCESS
    
    Note over Client,UI: Client now shows "Unknown" again in UI
```

---

## 5. PvP Matchmaking with Room Tracking

```mermaid
sequenceDiagram
    participant P1 as Player 1 Client
    participant Server as MainServer
    participant Match as MatchmakingService
    participant UI as MainWindow UI
    participant P2 as Player 2 Client

    P1->>Server: FIND_MATCH
    Server->>Match: AddToQueue(player1)
    Match->>Match: Create new room for P1
    Server->>UI: OnRoomUpdated()
    UI->>UI: UpdateRoomsList()
    
    Server->>P1: JOIN_ROOM:{roomId}
    Server->>P1: WAITING_FOR_OPPONENT
    
    P2->>Server: FIND_MATCH
    Server->>Match: AddToQueue(player2)
    Match->>Match: Find waiting room
    Match->>Match: Add P2 to room
    Match->>Match: Set room.StartTime = DateTime.Now
    
    Server->>UI: OnRoomUpdated()
    UI->>UI: Add RoomViewModel with:
    UI->>UI: - Player1Name from profile
    UI->>UI: - Player2Name from profile
    UI->>UI: - Duration = "00:00"
    UI->>UI: Start DispatcherTimer (1 sec updates)
    
    Server->>P1: MATCH_FOUND
    Server->>P2: MATCH_FOUND
    Server->>P1: OPPONENT_INFO:{Player2 data}
    Server->>P2: OPPONENT_INFO:{Player1 data}
    
    Server->>P1: BOTH_READY
    Server->>P2: BOTH_READY
    Server->>P1: GAME_START
    Server->>P2: GAME_START
    
    loop Every Second
        UI->>UI: Calculate duration = Now - StartTime
        UI->>UI: Update Duration column in grid
    end
    
    Note over P1,UI: Active game shows in UI with live duration
```

---

## 6. AI Game with Worker Load Balancing

```mermaid
sequenceDiagram
    participant Client as Human Player
    participant Server as MainServer
    participant LB as LoadBalancer
    participant Worker as Worker Server
    participant Match as MatchmakingService
    participant UI as MainWindow UI

    Client->>Server: PLAY_WITH_AI
    Server->>Match: CreateAIGame(player)
    Match->>Match: Create room with IsAIGame=true
    Match->>Match: Set StartTime
    
    Server->>UI: OnRoomUpdated()
    UI->>UI: Add room with Player2Name="AI"
    
    Server->>Client: MATCH_FOUND
    Server->>Client: OPPONENT_INFO:{AI player}
    Server->>Client: GAME_START
    
    Client->>Server: GAME_MOVE:{row, col}
    Server->>Server: Validate & apply move
    Server->>Client: GAME_MOVE (echo)
    Server->>Client: TURN_CHANGE
    
    Note over Server,LB: AI's turn - need to calculate move
    
    Server->>LB: ShouldUseWorker(AIMove)
    LB->>LB: Check system load
    LB->>LB: Check game load
    
    alt High Load - Use Worker
        LB->>Server: return true
        Server->>Server: Set worker.Status = ProcessingAI
        Server->>UI: OnWorkerStatusChanged(workerId, ProcessingAI, "AI Move (Room: X)")
        UI->>UI: Update worker status in grid
        
        Server->>Worker: AI_MOVE_REQUEST:{board, symbol, roomId}
        Worker->>Worker: Calculate best move
        Worker->>Server: AI_RESPONSE:{row, col}
        
        Server->>Server: worker.TasksCompleted++
        Server->>Server: Set worker.Status = Idle
        Server->>UI: OnWorkerStatusChanged(workerId, Idle, "Idle")
        UI->>UI: Update worker grid
    else Low Load - Process Locally
        LB->>Server: return false
        Server->>Server: Calculate AI move locally
    end
    
    Server->>Server: Apply AI move
    Server->>Client: GAME_MOVE:{AI move}
    Server->>Client: TURN_CHANGE
    
    loop Game continues
        UI->>UI: Update room duration every second
    end
```

---

## 7. Game End with Statistics Recording

```mermaid
sequenceDiagram
    participant P1 as Player 1
    participant Server as MainServer
    participant Match as MatchmakingService
    participant DB as Database
    participant UI as MainWindow UI
    participant P2 as Player 2

    P1->>Server: GAME_MOVE:{winning move}
    Server->>Server: Validate move
    Server->>Server: Check win condition
    Server->>Server: Winner detected!
    
    Server->>Match: EndGame(room, winner)
    Match->>Match: Calculate ELO changes
    Match->>Match: Calculate game stats
    
    Match->>DB: Update PlayerProfile (P1):
    DB->>DB: Wins++, TotalGames++, ELO += change
    
    Match->>DB: Update PlayerProfile (P2):
    DB->>DB: Losses++, TotalGames++, ELO -= change
    
    Match->>DB: Insert GameHistory:
    DB->>DB: {Player1Id, Player2Id, WinnerId, Duration, Moves, EloChanges}
    
    Server->>UI: OnRoomUpdated()
    UI->>UI: Remove room from ActiveRooms list
    UI->>UI: Update ActiveGamesText count
    
    Server->>P1: GAME_END:{winner:"Player1", eloChange:+25}
    Server->>P2: GAME_END:{winner:"Player1", eloChange:-25}
    
    Server->>UI: OnLogMessage("Game ended: P1 wins")
    
    Note over Server,DB: Stats recorded, room closed
```

---

## 8. Server Stop with Cleanup

```mermaid
sequenceDiagram
    participant UI as MainWindow UI
    participant Server as MainServer
    participant Clients as All Clients
    participant Workers as All Workers
    participant TCP as TCP Listeners

    UI->>UI: User clicks Stop button
    UI->>UI: Show confirmation dialog
    
    UI->>Server: Stop()
    Server->>Server: Check if running
    Server->>Server: isRunning = false
    
    par Disconnect All Clients
        loop For each client
            Server->>Clients: Disconnect()
            Clients->>Clients: Close streams
            Clients->>Clients: Update DB status
        end
    end
    
    par Disconnect All Workers
        loop For each worker
            Server->>Workers: Close Client & Stream
        end
    end
    
    Server->>TCP: Stop client listener
    Server->>TCP: Stop worker listener
    
    Server->>UI: OnLogMessage("Server stopped")
    
    UI->>UI: Wait 100ms
    UI->>UI: Update StatusText = "Stopped"
    UI->>UI: StatusIndicator color = Red
    UI->>UI: Button = "▶ Start Server" (Green)
    
    Note over UI,Server: Server fully stopped, ready to restart
```

---

## 9. Server Restart with Data Clear

```mermaid
sequenceDiagram
    participant UI as MainWindow UI
    participant Server as MainServer
    participant Collections as ObservableCollections

    UI->>UI: User clicks Start button
    UI->>UI: ClearAllData()
    
    UI->>Collections: logs.Clear()
    UI->>Collections: workers.Clear()
    UI->>Collections: clients.Clear()
    UI->>Collections: rooms.Clear()
    
    UI->>UI: Reset counters to "0"
    UI->>Server: ClearAllData()
    
    Server->>Server: clients.Clear()
    Server->>Server: workers.Clear()
    Server->>Server: pendingRequests.Clear()
    
    UI->>UI: Update StatusText = "Running"
    UI->>UI: StatusIndicator color = Green
    UI->>UI: Button = "⏹ Stop Server" (Red)
    
    UI->>Server: StartAsync()
    Server->>Server: Dispose old listeners
    Server->>Server: Create new listeners
    Server->>Server: Start accepting connections
    
    Note over UI,Server: Clean restart complete
```

---

## 10. Real-time UI Monitoring

```mermaid
sequenceDiagram
    participant Events as Server Events
    participant Dispatcher as WPF Dispatcher
    participant UI as MainWindow UI
    participant Timer as DispatcherTimer

    Note over Events,Timer: Multiple event streams running concurrently

    par Worker Status Updates
        Events->>Dispatcher: OnWorkerStatusChanged
        Dispatcher->>UI: Invoke on UI thread
        UI->>UI: Find worker in collection
        UI->>UI: Update Status, CurrentTask, TasksCompleted
        UI->>UI: Refresh WorkersDataGrid
    end

    par Client Authentication
        Events->>Dispatcher: OnClientAuthenticated
        Dispatcher->>UI: Invoke on UI thread
        UI->>UI: Find client in collection
        UI->>UI: Update PlayerName
        UI->>UI: Refresh ClientsDataGrid
    end

    par Room Updates
        Events->>Dispatcher: OnRoomUpdated
        Dispatcher->>UI: Invoke on UI thread
        UI->>UI: UpdateRoomsList()
        UI->>UI: Clear rooms collection
        UI->>UI: Query server.ActiveRooms
        UI->>UI: Create RoomViewModel for each
        UI->>UI: Populate with Player1Name, Player2Name
    end

    par Timer Tick (Every Second)
        Timer->>UI: RoomUpdateTimer_Tick
        loop For each room
            UI->>UI: Calculate duration = Now - StartTime
            UI->>UI: Update Duration property
        end
        Note over UI: Grid auto-refreshes via binding
    end

    Note over Events,Timer: All updates happen on UI thread via Dispatcher
```

---

## 11. Load Balancing Decision Flow

```mermaid
sequenceDiagram
    participant Server as MainServer
    participant LB as LoadBalancer
    participant PC as Performance Counters
    participant Logic as Decision Logic

    Server->>LB: ShouldUseWorker(AIMove)
    
    LB->>LB: GetCurrentLoadLevel()
    
    LB->>PC: Get CPU usage %
    PC->>LB: cpuUsage = 45%
    
    LB->>PC: Get RAM available
    PC->>LB: availableRAM = 2048 MB
    
    LB->>LB: Calculate system load %
    LB->>LB: systemLoad = max(cpu, ram_used)
    
    LB->>LB: Get active games count
    LB->>LB: Calculate game load %
    LB->>LB: gameLoad = (activeGames / MAX_GAMES) * 100
    
    LB->>LB: overallLoad = max(systemLoad, gameLoad)
    
    alt overallLoad >= 80 (High)
        LB->>Logic: LoadLevel = High
        Logic->>Server: return true (Use Worker)
    else overallLoad >= 50 (Medium)
        LB->>Logic: LoadLevel = Medium
        alt operationType = AIMove
            Logic->>Server: return true (Use Worker for AI)
        else operationType = other
            Logic->>Server: return false (Process locally)
        end
    else overallLoad < 50 (Low)
        LB->>Logic: LoadLevel = Low
        Logic->>Server: return false (Process locally)
    end
    
    Note over Server,Logic: Decision affects worker assignment
```

---

## 12. Friend System with Real-time Updates

```mermaid
sequenceDiagram
    participant P1 as Player 1 Client
    participant Server as MainServer
    participant DB as Database
    participant FService as FriendshipService
    participant P2 as Player 2 Client

    P1->>Server: SEND_FRIEND_REQUEST:"Player2"
    Server->>DB: Check if Player2 exists
    DB->>Server: Player found
    
    Server->>FService: CreateFriendRequest(P1.Id, P2.Id)
    FService->>DB: Insert Friendship (Status=Pending)
    DB->>FService: Request created
    
    Server->>P1: FRIEND_REQUEST_SENT
    
    Note over P2: Player 2 logs in later
    
    P2->>Server: LOGIN:{credentials}
    Server->>P2: LOGIN_SUCCESS
    
    P2->>Server: GET_FRIENDS
    Server->>FService: GetFriendships(P2.Id)
    FService->>DB: Query friendships
    DB->>FService: Return pending requests
    
    Server->>P2: FRIENDS_DATA:[{from:Player1, status:Pending}]
    
    P2->>Server: ACCEPT_FRIEND_REQUEST:{requestId}
    Server->>FService: AcceptRequest(requestId)
    FService->>DB: Update Status = Accepted
    DB->>FService: Updated
    
    Server->>P1: FRIEND_REQUEST_ACCEPTED:{Player2}
    Server->>P2: FRIEND_REQUEST_ACCEPTED:{Player1}
    
    Note over P1,P2: Now friends - can see online status
```

---

## 13. Leaderboard & Statistics Query

```mermaid
sequenceDiagram
    participant Client as Unity Client
    participant Server as MainServer
    participant Profile as PlayerProfileService
    participant History as GameHistoryService
    participant DB as Database

    Client->>Server: GET_LEADERBOARD
    Server->>Profile: GetTopPlayers(100)
    Profile->>DB: SELECT TOP 100 ORDER BY Elo DESC
    DB->>Profile: Player profiles data
    
    Profile->>Profile: Calculate ranks
    Profile->>Server: Ranked player list
    
    Server->>Client: LEADERBOARD_DATA:[{rank, name, elo, wins}...]
    
    Client->>Server: GET_PLAYER_STATS:"SomePlayer"
    Server->>Profile: GetByPlayerName("SomePlayer")
    Profile->>DB: SELECT WHERE PlayerName = ?
    DB->>Profile: Profile data
    
    Profile->>Profile: Calculate win rate
    Profile->>Profile: Calculate rank position
    Profile->>Server: Complete stats
    
    Server->>Client: PLAYER_STATS_DATA:{profile, stats}
    
    Client->>Server: GET_GAME_HISTORY
    Server->>History: GetRecentGames(clientProfile.Id, 20)
    History->>DB: SELECT TOP 20 ORDER BY PlayedAt DESC
    DB->>History: Game records
    
    History->>History: Calculate ELO changes for each
    History->>Server: Game history list
    
    Server->>Client: GAME_HISTORY_DATA:[{opponent, result, elo, date}...]
    
    Note over Client,DB: Statistics cached for performance
```

---

## Notes

### Key Improvements in New Architecture:

1. **Real-time UI Monitoring**
   - Event-driven updates via Dispatcher
   - Live worker status tracking
   - Active game room monitoring with duration
   - Client authentication state updates

2. **Worker Load Balancing**
   - Performance counter monitoring
   - Intelligent task distribution
   - Worker status tracking (Idle/ProcessingAI/Busy)
   - Task completion metrics

3. **Clean Server Lifecycle**
   - Proper connection cleanup on Stop
   - Full data reset on Start
   - Toggle Start/Stop button functionality
   - Graceful client/worker disconnection

4. **Enhanced Game Tracking**
   - Room creation timestamps
   - Live duration calculation
   - Player name resolution
   - Active game statistics

5. **Thread-Safe UI Updates**
   - All UI updates via Dispatcher.Invoke
   - ObservableCollection for data binding
   - DispatcherTimer for periodic updates
   - Event-based notification system

### Technologies Used:
- **.NET 8.0** with WPF for UI
- **TCP Sockets** for client/worker communication
- **SQL Server LocalDB** with EF Core
- **ConcurrentDictionary** for thread-safe collections
- **Performance Counters** for load monitoring
- **Event-driven architecture** for real-time updates
