using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using SharedLib.Communication;
using SharedLib.AI;
using SharedLib.GameEngine;
using System.Net;

namespace WorkerServer;

public class WorkerServer
{
    private TcpClient? tcpClient;
    private NetworkStream? stream;
    private readonly int mainServerPort = 5001;
    private readonly string workerId;
    private bool isRunning = false;
    private bool isConnected = false;
    private bool isRegistered = false;
    private IPAddress[] iPAddresses =
    {
        new IPAddress(new byte[] { 192, 168, 195, 69 }),
        new IPAddress(new byte[] { 192, 168, 195, 126 }),
    };

    // Events for UI
    public event Action<string>? OnLogMessage;
    public event Action<bool, string>? OnConnectionChanged; // isConnected, serverAddress
    public event Action<bool, string>? OnRegistrationChanged; // isRegistered, workerId
    public event Action<string, double, bool>? OnRequestProcessed; // requestType, processingTime, success

    public WorkerServer()
    {
        this.workerId = Environment.MachineName + "-" + Guid.NewGuid().ToString()[..8];
    }

    private void Log(string message)
    {
        Console.WriteLine(message);
        OnLogMessage?.Invoke(message);
    }

    public async Task StartAsync()
    {
        isRunning = true;
        Log($"Worker {workerId} starting...");

        while (isRunning)
        {
            try
            {
                if (!isConnected)
                {
                    await ConnectToMainServerAsync();
                }

                if (isConnected && stream != null)
                {
                    await ListenForRequestsAsync();
                }
            }
            catch (Exception ex)
            {
                Log($"Connection error: {ex.Message}");
                isConnected = false;
                isRegistered = false;
                OnConnectionChanged?.Invoke(false, "");
                OnRegistrationChanged?.Invoke(false, workerId);
                
                if (isRunning)
                {
                    Log("Attempting to reconnect in 5 seconds...");
                    await Task.Delay(5000);
                }
            }
        }
    }

    private async Task ConnectToMainServerAsync()
    {
        try
        {
            tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(iPAddresses, mainServerPort);
            stream = tcpClient.GetStream();
            isConnected = true;

            string serverAddress = $"{iPAddresses[0]} | {iPAddresses[1]}:{mainServerPort}";
            Log($"Worker {workerId} connected to MainServer at {serverAddress}");
            OnConnectionChanged?.Invoke(true, serverAddress);

            // Send registration message
            await RegisterWithMainServerAsync();
        }
        catch (Exception ex)
        {
            Log($"Failed to connect to MainServer: {ex.Message}");
            isConnected = false;
            isRegistered = false;
            OnConnectionChanged?.Invoke(false, "");
            OnRegistrationChanged?.Invoke(false, workerId);
            tcpClient?.Close();
            tcpClient = null;
            stream = null;
        }
    }

    private async Task RegisterWithMainServerAsync()
    {
        try
        {
            var registrationRequest = new WorkerRequest
            {
                Type = WorkerProtocol.WORKER_REGISTRATION,
                Data = JsonSerializer.Serialize(new { WorkerId = workerId, Capabilities = new[] { "AI_PROCESSING", "MOVE_VALIDATION" } })
            };

            await SendMessage(registrationRequest);
            Log($"Worker {workerId} is trying to register with MainServer");
        }
        catch (Exception ex)
        {
            Log($"Failed to register with MainServer: {ex.Message}");
        }
    }

    private async Task ListenForRequestsAsync()
    {
        var buffer = new byte[4096];
        var messageBuilder = new StringBuilder();

        try
        {
            while (isRunning && isConnected && stream != null)
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                    Log("MainServer disconnected");
                    break;
                }

                string data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                messageBuilder.Append(data);

                string messages = messageBuilder.ToString();
                string[] lines = messages.Split('\n');

                for (int i = 0; i < lines.Length - 1; i++)
                {
                    string message = lines[i].Trim();
                    if (!string.IsNullOrEmpty(message))
                    {
                        await ProcessRequest(message);
                    }
                }

                messageBuilder.Clear();
                if (lines.Length > 0)
                {
                    messageBuilder.Append(lines[lines.Length - 1]);
                }
            }
        }
        catch (Exception ex)
        {
            Log($"Error listening for requests: {ex.Message}");
        }
        finally
        {
            isConnected = false;
            isRegistered = false;
            OnConnectionChanged?.Invoke(false, "");
            OnRegistrationChanged?.Invoke(false, workerId);
        }
    }

    private async Task ProcessRequest(string message)
    {
        Log($"Received: {message.Substring(0, Math.Min(100, message.Length))}...");

        try
        {
            var request = JsonSerializer.Deserialize<WorkerRequest>(message);
            if (request == null)
            {
                await SendErrorResponse("", "Invalid request format");
                return;
            }

            WorkerResponse response;

            switch (request.Type)
            {
                case WorkerProtocol.AI_MOVE_REQUEST:
                    response = await ProcessAIMoveRequest(request);
                    break;

                case WorkerProtocol.VALIDATE_MOVE_REQUEST:
                    response = await ProcessMoveValidationRequest(request);
                    break;

                case WorkerProtocol.HEALTH_CHECK:
                    response = new WorkerResponse
                    {
                        RequestId = request.RequestId,
                        Type = WorkerProtocol.HEALTH_CHECK_RESPONSE,
                        Status = WorkerProtocol.SUCCESS,
                        Data = JsonSerializer.Serialize(new { 
                            WorkerId = workerId, 
                            Status = "Healthy", 
                            IsRegistered = isRegistered,
                            IsConnected = isConnected,
                            Timestamp = DateTime.UtcNow 
                        })
                    };
                    break;

                case WorkerProtocol.WORKER_REGISTRATION_ACK:
                    await ProcessRegistrationAck(request);
                    return;

                case WorkerProtocol.PING:
                    response = new WorkerResponse
                    {
                        RequestId = request.RequestId,
                        Type = WorkerProtocol.PONG,
                        Status = WorkerProtocol.SUCCESS,
                        Data = JsonSerializer.Serialize(new { WorkerId = workerId })
                    };
                    break;

                default:
                    response = new WorkerResponse
                    {
                        RequestId = request.RequestId,
                        Type = WorkerProtocol.ERROR_RESPONSE,
                        Status = WorkerProtocol.ERROR,
                        ErrorMessage = $"Unknown request type: {request.Type}"
                    };
                    break;
            }

            await SendResponse(response);
        }
        catch (Exception ex)
        {
            Log($"Error processing request: {ex.Message}");
            await SendErrorResponse("", $"Processing error: {ex.Message}");
        }
    }

    private async Task ProcessRegistrationAck(WorkerRequest request)
    {
        try
        {
            var ackData = JsonSerializer.Deserialize<JsonElement>(request.Data);
            var acknowledgedWorkerId = ackData.GetProperty("WorkerId").GetString();
            
            if (acknowledgedWorkerId == workerId)
            {
                isRegistered = true;
                Log($"[Worker {workerId}] Registration acknowledged by MainServer");
                Log($"[Worker {workerId}] Status: Connected and Registered - Ready to process requests");
                OnRegistrationChanged?.Invoke(true, workerId);
            }
            else
            {
                Log($"[Worker {workerId}] Warning: Received acknowledgment for different worker ID: {acknowledgedWorkerId}");
            }
        }
        catch (Exception ex)
        {
            Log($"[Worker {workerId}] Error processing registration acknowledgment: {ex.Message}");
        }
    }

    private async Task<WorkerResponse> ProcessAIMoveRequest(WorkerRequest request)
    {
        var startTime = DateTime.Now;
        bool success = false;
        try
        {
            Log($"[Worker {workerId}] Processing AI request {request.RequestId}");
            
            var aiRequest = JsonSerializer.Deserialize<AIRequest>(request.Data);
            if (aiRequest == null)
            {
                OnRequestProcessed?.Invoke("AI_MOVE_REQUEST", (DateTime.Now - startTime).TotalMilliseconds, false);
                return CreateErrorResponse(request.RequestId, "Invalid AI request data");
            }

            // Convert jagged array to 2D array
            var board2D = ConvertToRectangularArray(aiRequest.Board);
            
            Log($"[Worker {workerId}] Starting AI calculation for {aiRequest.AISymbol}");
            var gomokuAI = new GomokuAI(aiRequest.AISymbol);
            var (row, col) = gomokuAI.GetBestMove(board2D);
            
            var elapsed = DateTime.Now - startTime;
            Log($"[Worker {workerId}] AI calculation completed in {elapsed.TotalMilliseconds:F0}ms");

            var aiResponse = new AIResponse
            {
                Row = row,
                Col = col,
                IsValid = row != -1 && col != -1
            };

            if (!aiResponse.IsValid)
            {
                aiResponse.ErrorMessage = "No valid moves available";
            }

            success = aiResponse.IsValid;
            OnRequestProcessed?.Invoke("AI_MOVE_REQUEST", elapsed.TotalMilliseconds, success);

            return new WorkerResponse
            {
                RequestId = request.RequestId,
                Type = WorkerProtocol.AI_MOVE_RESPONSE,
                Status = WorkerProtocol.SUCCESS,
                Data = JsonSerializer.Serialize(aiResponse)
            };
        }
        catch (Exception ex)
        {
            var elapsed = DateTime.Now - startTime;
            Log($"[Worker {workerId}] AI request {request.RequestId} failed after {elapsed.TotalMilliseconds}ms: {ex.Message}");
            OnRequestProcessed?.Invoke("AI_MOVE_REQUEST", elapsed.TotalMilliseconds, false);
            return CreateErrorResponse(request.RequestId, $"AI processing error: {ex.Message}");
        }
    }

    private async Task<WorkerResponse> ProcessMoveValidationRequest(WorkerRequest request)
    {
        var startTime = DateTime.Now;
        try
        {
            var validationRequest = JsonSerializer.Deserialize<MoveValidationRequest>(request.Data);
            if (validationRequest == null)
            {
                OnRequestProcessed?.Invoke("VALIDATE_MOVE_REQUEST", (DateTime.Now - startTime).TotalMilliseconds, false);
                return CreateErrorResponse(request.RequestId, "Invalid validation request data");
            }

            // Convert jagged array to 2D array
            var board2D = ConvertToRectangularArray(validationRequest.Board);

            var validationResponse = new MoveValidationResponse
            {
                IsValid = GameLogic.IsValidMove(board2D, validationRequest.Row, validationRequest.Col)
            };

            if (validationResponse.IsValid)
            {
                // Create a copy and apply the move to check win condition
                var tempBoard = GameLogic.CopyBoard(board2D);
                tempBoard[validationRequest.Row, validationRequest.Col] = validationRequest.PlayerSymbol;

                validationResponse.IsWinning = GameLogic.CheckWin(tempBoard, validationRequest.Row, validationRequest.Col, validationRequest.PlayerSymbol);
                validationResponse.IsDraw = !validationResponse.IsWinning && GameLogic.IsBoardFull(tempBoard);
            }
            else
            {
                validationResponse.ErrorMessage = "Invalid move: position is occupied or out of bounds";
            }

            var elapsed = DateTime.Now - startTime;
            OnRequestProcessed?.Invoke("VALIDATE_MOVE_REQUEST", elapsed.TotalMilliseconds, true);

            return new WorkerResponse
            {
                RequestId = request.RequestId,
                Type = WorkerProtocol.VALIDATE_MOVE_RESPONSE,
                Status = WorkerProtocol.SUCCESS,
                Data = JsonSerializer.Serialize(validationResponse)
            };
        }
        catch (Exception ex)
        {
            var elapsed = DateTime.Now - startTime;
            OnRequestProcessed?.Invoke("VALIDATE_MOVE_REQUEST", elapsed.TotalMilliseconds, false);
            return CreateErrorResponse(request.RequestId, $"Validation error: {ex.Message}");
        }
    }

    private WorkerResponse CreateErrorResponse(string requestId, string errorMessage)
    {
        return new WorkerResponse
        {
            RequestId = requestId,
            Type = WorkerProtocol.ERROR_RESPONSE,
            Status = WorkerProtocol.ERROR,
            ErrorMessage = errorMessage
        };
    }

    private async Task SendResponse(WorkerResponse response)
    {
        try
        {
            await SendMessage(response);
            Log($"Sent response: {response.Type} - {response.Status}");
        }
        catch (Exception ex)
        {
            Log($"Failed to send response: {ex.Message}");
            isConnected = false;
        }
    }

    private async Task SendMessage(object message)
    {
        if (stream == null) return;

        try
        {
            string json = JsonSerializer.Serialize(message);
            byte[] data = Encoding.UTF8.GetBytes(json + "\n");
            await stream.WriteAsync(data, 0, data.Length);
            await stream.FlushAsync();
        }
        catch (Exception ex)
        {
            Log($"Failed to send message: {ex.Message}");
            isConnected = false;
            throw;
        }
    }

    private async Task SendErrorResponse(string requestId, string errorMessage)
    {
        var errorResponse = CreateErrorResponse(requestId, errorMessage);
        await SendResponse(errorResponse);
    }

    private string[,] ConvertToRectangularArray(string[][] jaggedArray)
    {
        var result = new string[15, 15];
        for (int i = 0; i < 15; i++)
        {
            for (int j = 0; j < 15; j++)
            {
                result[i, j] = jaggedArray[i][j] ?? string.Empty;
            }
        }
        return result;
    }

    public bool IsReadyToProcess()
    {
        return isRunning && isConnected && isRegistered;
    }

    public void Stop()
    {
        Log($"Worker {workerId} stopping...");
        isRunning = false;
        isConnected = false;
        isRegistered = false;
        OnConnectionChanged?.Invoke(false, "");
        OnRegistrationChanged?.Invoke(false, workerId);
        stream?.Close();
        tcpClient?.Close();
        Log($"Worker {workerId} stopped");
    }
}