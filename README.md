# Gomoku Online Game - Hướng Dẫn Cài Đặt và Chạy

![.NET](https://img.shields.io/badge/.NET-8.0-blue)
![C#](https://img.shields.io/badge/C%23-12.0-purple)
![WPF](https://img.shields.io/badge/WPF-Windows-blue)
![License](https://img.shields.io/badge/license-MIT-green)

Game Gomoku (Cờ Caro) online với kiến trúc phân tán Server-Worker, hỗ trợ chơi với AI sử dụng thuật toán Minimax.

---

##  Mục Lục

1. [Yêu Cầu Hệ Thống](#-yêu-cầu-hệ-thống)
2. [Cấu Trúc Project](#-cấu-trúc-project)
3. [Cài Đặt](#-cài-đặt)
4. [Chạy Hệ Thống](#-chạy-hệ-thống)
5. [Sử Dụng](#-sử-dụng)
6. [Troubleshooting](#-troubleshooting)
7. [Tài Liệu Kỹ Thuật](#-tài-liệu-kỹ-thuật)

---

##  Yêu Cầu Hệ Thống

### Phần Mềm Bắt Buộc

- **Operating System**: Windows 10/11 (64-bit)
- **.NET SDK**: Version 8.0 trở lên
  - Download: https://dotnet.microsoft.com/download/dotnet/8.0
- **SQL Server**: LocalDB hoặc SQL Server Express
  - Đã tích hợp sẵn trong Visual Studio
  - Hoặc download: https://www.microsoft.com/sql-server/sql-server-downloads

### Phần Mềm Khuyến Nghị

- **Visual Studio 2022** (Community/Professional/Enterprise)
  - Workloads: `.NET desktop development`
  - Hoặc **Visual Studio Code** với C# Extension
- **Git**: Để clone repository

### Yêu Cầu Phần Cứng

- **CPU**: 2 cores trở lên
- **RAM**: 4GB trở lên (khuyến nghị 8GB)
- **Disk**: 500MB trống
- **Network**: Cổng 5000, 5001 không bị chặn bởi firewall

---


##  Cài Đặt

### Bước 1: Clone Repository

```bash
git clone https://github.com/nv-hoai/Gomoku.git
cd Gomoku
```

### Bước 2: Kiểm Tra .NET SDK

Mở **Command Prompt** hoặc **PowerShell** và chạy:

```bash
dotnet --version
```

Phải hiển thị version `8.0.x` trở lên. Nếu chưa có, tải về từ link trên.

### Bước 3: Khôi Phục Dependencies

```bash
dotnet restore Gomuku.sln
```

Hoặc sử dụng Visual Studio:
- Mở `Gomuku.sln`
- Visual Studio sẽ tự động restore NuGet packages

### Bước 4: Cấu Hình Database

#### Option 1: Sử dụng LocalDB (Khuyến nghị cho dev)

Database sẽ tự động tạo khi chạy lần đầu với connection string mặc định:

```
Server=(localdb)\mssqllocaldb;Database=GomokuGameDB;Trusted_Connection=True;MultipleActiveResultSets=true
```

#### Option 2: Sử dụng SQL Server

Nếu dùng SQL Server, sửa connection string trong `MainServer/MainServer.cs`:

```csharp
optionsBuilder.UseSqlServer("Server=YOUR_SERVER;Database=GomokuGameDB;Trusted_Connection=True;");
```

#### Chạy Migrations (Tạo Database Schema)

```bash
cd SharedLib
dotnet ef database update
```

Hoặc trong Visual Studio Package Manager Console:

```powershell
Update-Database -Project SharedLib
```

### Bước 5: Build Projects

#### Option A: Sử dụng Build Script (Khuyến nghị)

Double-click vào `build-all.bat` hoặc chạy trong terminal:

```bash
build-all.bat
```

#### Option B: Build Từng Project

```bash
# Build SharedLib trước
cd SharedLib
dotnet build

# Build MainServer
cd ../MainServer
dotnet build

# Build WorkerServer
cd ../WorkerServer
dotnet build
```

#### Option C: Visual Studio

- Mở `Gomuku.sln`
- Nhấn `Ctrl + Shift + B` hoặc `Build > Build Solution`

---

## Chạy Hệ Thống

### Option 1: Chạy Tất Cả Cùng Lúc (Khuyến nghị)

Double-click vào `start-all.bat`:

```bash
start-all.bat
```

Script này sẽ:
1. Mở MainServer (GUI Window)
2. Mở WorkerServer (Console Window)

### Option 2: Chạy Từng Thành Phần

#### 1. Chạy MainServer (Server Chính)

**GUI Mode** (có giao diện):

```bash
run-server-gui.bat
```

Hoặc:

```bash
cd MainServer
dotnet run
```

MainServer sẽ:
- Mở cửa sổ WPF Dashboard
- Lắng nghe clients trên **port 5000**
- Lắng nghe workers trên **port 5001**
- Hiển thị logs, workers, clients, active rooms

#### 2. Chạy WorkerServer (Xử Lý AI)

```bash
run-worker.bat
```

Hoặc:

```bash
cd WorkerServer
dotnet run
```

Worker sẽ:
- Kết nối đến MainServer (port 5001)
- Đăng ký khả năng xử lý AI
- Chờ nhận AI requests

**Lưu ý**: Có thể chạy nhiều Workers song song để tăng performance.

### Option 3: Debug Trong Visual Studio

1. Mở `Gomuku.sln`
2. Set Multiple Startup Projects:
   - Right-click Solution → Properties
   - Common Properties → Startup Project
   - Chọn "Multiple startup projects"
   - Set `MainServer` và `WorkerServer` thành **Start**
3. Nhấn `F5` để chạy

---

## 🐛 Troubleshooting

### Lỗi Thường Gặp

#### 1. "Could not load file or assembly 'System.Diagnostics.PerformanceCounter'"

**Nguyên nhân**: Thiếu NuGet package

**Giải pháp**:
```bash
cd MainServer
dotnet add package System.Diagnostics.PerformanceCounter
dotnet restore
```

#### 2. "Unable to connect to SQL Server"

**Nguyên nhân**: LocalDB chưa được cài đặt

**Giải pháp**:
- Cài đặt SQL Server Express: https://www.microsoft.com/sql-server/sql-server-downloads
- Hoặc cài Visual Studio với SQL Server Data Tools

#### 3. "Port 5000 is already in use"

**Nguyên nhân**: Port bị chiếm dụng

**Giải pháp**:
```powershell
# Tìm process đang dùng port
netstat -ano | findstr ":5000"

# Kill process (thay <PID> bằng process ID)
taskkill /PID <PID> /F
```

#### 4. "Worker cannot connect to MainServer"

**Nguyên nhân**: Firewall hoặc IP sai

**Giải pháp**:
- Kiểm tra firewall (xem mục 3.3 trên)
- Kiểm tra IP trong `WorkerServer.cs`:
  ```csharp
  IPAddress[] iPAddresses = {
      IPAddress.Parse("192.168.x.x"),  // Thay đúng IP của MainServer
  };
  ```
- Nếu test local, dùng `127.0.0.1`:
  ```csharp
  IPAddress[] iPAddresses = { IPAddress.Loopback };
  ```

#### 5. "The type initializer for 'System.Diagnostics.PerformanceCounter' threw an exception"

**Nguyên nhân**: Chạy trên non-Windows hoặc permissions

**Giải pháp**:
- LoadBalancer sẽ tự động fallback sang phương thức khác
- Hoặc comment out PerformanceCounter trong `LoadBalancer.cs`

#### 6. Database Migration Lỗi

**Giải pháp**:
```bash
# Xóa database cũ và tạo lại
cd SharedLib
dotnet ef database drop
dotnet ef database update
```

### Logs và Debugging

#### Bật Verbose Logging

Trong `MainServer.cs`, thêm:

```csharp
private void Log(string message)
{
    Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");
    OnLogMessage?.Invoke(message);
}
```

#### Kiểm Tra Database Connection

```bash
# Package Manager Console trong Visual Studio
Get-Migrations -Project SharedLib
```

---


### Technologies

- **.NET 8.0**: Framework
- **C# 12**: Programming language
- **WPF**: Windows Presentation Foundation (GUI)
- **Entity Framework Core 8.0**: ORM
- **SQL Server LocalDB**: Database
- **TCP/IP Sockets**: Network communication
- **RSA + AES**: Hybrid encryption
- **Minimax + Alpha-Beta Pruning**: AI algorithm

---

