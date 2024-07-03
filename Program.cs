using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SimpleServerFTP
{
    using Newtonsoft.Json;
    using System.Threading;

    internal class ServerFTP
    {
        private int minPort;
        private int maxPort;
        private Socket serverSocket;
        private IPAddress ip;
        private int port;
        private readonly ConfigModel config;
        private bool isAnonymousUser;
        private string username;
        private bool isLoggedIn;
        private string currentDir;
        private Socket pasvSocket;
        private Dictionary<string, Action<Socket, string[]>> commandHandlers;

        public ServerFTP(string ipAddress, int portNr, ConfigModel config)
        {
            this.config = config;
            currentDir = config.FTPRootDir;
            minPort = config.TCPMin;
            maxPort = config.TCPMax;
            try
            {
                if (!IPAddress.TryParse(ipAddress, out ip))
                {
                    throw new ArgumentException("Invalid IP address.");
                }
                // Setting the IP address and port of the server
                ip = IPAddress.Parse(ipAddress);
                port = portNr;

                // Creating the server socket
                serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                // Binding the socket to the IP address and port
                serverSocket.Bind(new IPEndPoint(ip, portNr));
                CreateCommandHandler();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw;
            }
        }

        private void CreateCommandHandler()
        {
            // Creating a dictionary that maps command names to handler methods
            commandHandlers = new Dictionary<string, Action<Socket, string[]>> {
        { "USER", HandleUser }, // Login
        { "PASS", HandlePass }, // Password
        { "PWD", HandlePwd }, // Current directory
        { "CWD", HandleCwd }, // Change directory
        { "CDUP", HandleCdup }, // Move to parent directory
        { "MKD", HandleMkd }, // Create directory
        { "RMD", HandleRmd }, // Remove directory
        { "PASV", HandlePasv }, // Passive mode
        { "LIST", HandleList }, // List files/directories
        { "STOR", HandleStor }, // Upload file
        { "RETR", HandleRetr }, // Download file
        { "DELE", HandleDele }, // Delete file
        { "TYPE", HandleType }, // Transmission type
        { "QUIT", HandleQuit } // Logout
    };
        }

        public void Start()
        {
            // Starting to listen for connections
            serverSocket.Listen(10);
            Console.WriteLine($"FTP Server listening for connections. Configured IP:{config.IP} Port:{config.Port}");

            while (true)
            {
                try
                {
                    // Accepting a connection from a client
                    Socket clientSocket = serverSocket.Accept();
                    HandleResponse(clientSocket, $"220 FTP Server ready. Incoming connection.\r\n");
                    // Creating a new thread for each client connection
                    Thread clientThread = new Thread(() => HandleClient(clientSocket));
                    clientThread.Start();
                }
                catch (SocketException ex)
                {
                    // Logging socket-related exceptions
                    Console.WriteLine($"A socket-related error occurred while handling the client: {ex.Message}");
                }
                catch (ThreadStartException ex)
                {
                    // Logging thread-related exceptions
                    Console.WriteLine($"An error occurred while trying to start a thread for the client: {ex.Message}");
                }
                catch (Exception ex)
                {
                    // Logging exceptions not related to sockets or threads
                    Console.WriteLine($"An unexpected error occurred while handling the client: {ex.Message}");
                }
            }
        }

        private void HandleClient(Socket clientSocket)
        {
            while (true)
            {
                // Reading data from the client
                byte[] buffer = new byte[2048];
                int size = clientSocket.Receive(buffer);
                // Converting data to string and displaying it
                string command = Encoding.UTF8.GetString(buffer, 0, size);

                if (string.IsNullOrEmpty(command)) break; //!TODO: Information to the client

                HandleCommand(clientSocket, command);
            }
        }

        private void HandleCommand(Socket clientSocket, string command)
        {
            string[] splitCommand = command.Split(' ');
            string cmd = splitCommand[0].ToUpper();
            cmd = cmd.TrimEnd('\r', '\n');
            Console.WriteLine($"Received command: {cmd}");

            // Ignore AUTH TLS commands
            if (cmd == "AUTH" && splitCommand.Length > 1 && splitCommand[1].ToUpper() == "TLS")
            {
                HandleResponse(clientSocket, "502 Command not supported\r\n");
                return;
            }

            string[] args = new string[0];
            if (splitCommand.Length > 1)
            {
                args = new string[splitCommand.Length - 1];
                Array.Copy(splitCommand, 1, args, 0, args.Length);
                for (int i = 0; i < args.Length; i++)
                {
                    args[i] = args[i].TrimEnd('\r', '\n');
                }
            }

            if (!commandHandlers.ContainsKey(cmd))
            {
                HandleResponse(clientSocket, "502 Command does not exist\r\n");
                return;
            }

            // Checking if the command exists in the dictionary
            if (commandHandlers.ContainsKey(cmd))
            {
                // Calling the appropriate handler method
                commandHandlers[cmd](clientSocket, args);
            }
            else
            {
                HandleResponse(clientSocket, "502 Command does not exist\r\n");
            }
        }

        private void HandleResponse(Socket clientSocket, string message)
        {
            // Sending response to the client
            clientSocket.Send(Encoding.UTF8.GetBytes(message));
        }

        private void HandleUser(Socket clientSocket, string[] args)
        {
            // logging in anonymous and private user
            if (args[0] == "anonymous" && config.AnonymousUserEnabled)
            {
                isAnonymousUser = true;
                HandleResponse(clientSocket, "230 Logged in successfully\r\n");
            }
            else
            {
                username = args[0];
                HandleResponse(clientSocket, "331 Please enter password\r\n");
            }
        }

        private void HandlePass(Socket clientSocket, string[] args)
        {
            // Verifying user with "Database" user i.e., the config file
            if ((username == config.DBUser && args[0] == config.Password) || isAnonymousUser)
            {
                isLoggedIn = true;
                string user = isAnonymousUser ? "Anonymous user" : username;
                HandleResponse(clientSocket, $"230 Logged in as {user}\r\n");
            }
            else
            {
                HandleResponse(clientSocket, "530 Authentication error, login or password does not match the database user\r\n");
            }
        }

        private void HandleMkd(Socket clientSocket, string[] args)
        {
            if (!isLoggedIn)
            {
                HandleResponse(clientSocket, "530 Please log in\r\n");
                return;
            }

            string newDir = Path.Combine(config.FTPRootDir, args[0].TrimStart('/'));
            if (!Directory.Exists(newDir))
            {
                try
                {
                    Directory.CreateDirectory(newDir);
                    HandleResponse(clientSocket, $"257 \"{args[0]}\" Directory created\r\n");
                }
                catch (UnauthorizedAccessException)
                {
                    HandleResponse(clientSocket, $"550 No permission to create directory {args[0]}\r\n");
                }
            }
            else
            {
                HandleResponse(clientSocket, $"550 Directory {args[0]} already exists\r\n");
            }
        }

        private void HandleRmd(Socket clientSocket, string[] args)
        {
            if (!isLoggedIn)
            {
                HandleResponse(clientSocket, "530 Please log in\r\n");
                return;
            }

            // Remove directory from the main FTP directory
            string dirToRemove = Path.Combine(config.FTPRootDir, args[0].TrimStart('/'));
            if (Directory.Exists(dirToRemove))
            {
                if (Directory.GetFileSystemEntries(dirToRemove).Length == 0)
                {
                    // Directory is empty, remove it
                    try
                    {
                        Directory.Delete(dirToRemove);
                        HandleResponse(clientSocket, $"250 Directory {args[0]} removed\r\n");
                    }
                    catch (UnauthorizedAccessException)
                    {
                        HandleResponse(clientSocket, $"550 No permission to remove directory {args[0]}\r\n");
                    }
                }
                else
                {
                    // Directory is not empty, display error message
                    HandleResponse(clientSocket, $"550 Directory {args[0]} is not empty. Removal failed.\r\n");
                }
            }
            else
            {
                //Directory does not exist, display error message
                HandleResponse(clientSocket, "550 Failed to remove directory\r\n");
            }
        }

        private void HandlePwd(Socket clientSocket, string[] args)
        {
            if (isLoggedIn)
            {
                // Check if currentDir is the main directory
                string directory = currentDir == config.FTPRootDir ? "\\" : currentDir;

                string response = $"257 \"{directory}\" is the current directory\r\n";
                HandleResponse(clientSocket, response);
            }
            else
            {
                HandleResponse(clientSocket, "530 Please log in\r\n");
            }
        }

        private void HandleCwd(Socket clientSocket, string[] args)
        {
            if (!isLoggedIn)
            {
                HandleResponse(clientSocket, "530 Please log in\r\n");
                return;
            }

            // Convert relative path to absolute
            string newDirRelative = args[0].TrimStart('\\');
            // If the path is empty or equals "\", set it to the main directory
            if (string.IsNullOrEmpty(newDirRelative) || newDirRelative == "\\")
            {
                newDirRelative = ""; // Main directory
            }
            else
            {
                // Calculate new relative path in the context of the current directory
                newDirRelative = Path.Combine(currentDir, newDirRelative);
            }

            string newDirAbsolute = Path.Combine(config.FTPRootDir, newDirRelative);

            if (Directory.Exists(newDirAbsolute))
            {
                // Update currentDir as the new relative path
                currentDir = newDirRelative;
                HandleResponse(clientSocket, $"250 Directory changed to {currentDir}\r\n");
            }
            else
            {
                HandleResponse(clientSocket, "550 Failed to change directory. Directory does not exist or there was a problem with the provided path\r\n");
            }
        }

        private void HandleCdup(Socket clientSocket, string[] args)
        {
            if (!isLoggedIn)
            {
                HandleResponse(clientSocket, "530 Please log in\r\n");
                return;
            }

            // Using Path.GetDirectoryName instead of Directory.GetParent to avoid issues with full paths
            string parentDirRelative = Path.GetDirectoryName(currentDir);

            // Checking if we are not trying to go above the root directory
            if (!string.IsNullOrEmpty(parentDirRelative) && parentDirRelative != "\\")
            {
                string parentDirAbsolute = Path.Combine(config.FTPRootDir, parentDirRelative);
                if (Directory.Exists(parentDirAbsolute))
                {
                    currentDir = parentDirRelative;
                    HandleResponse(clientSocket, $"200 Directory changed to parent: {currentDir}\r\n");
                }
                else
                {
                    HandleResponse(clientSocket, "550 Failed to change directory to parent\r\n");
                }
            }
            else
            {
                // If we are already in the root directory, do not change currentDir
                HandleResponse(clientSocket, "200 You are already in the root directory\r\n");
            }
        }

        private void HandleDele(Socket clientSocket, string[] args)
        {
            if (!isLoggedIn)
            {
                HandleResponse(clientSocket, "530 Please log in\r\n");
                return;
            }

            // Deleting a file
            string fileToDelete = Path.Combine(currentDir, args[0].TrimStart('/'));
            if (File.Exists(fileToDelete))
            {
                try
                {
                    File.Delete(fileToDelete);
                    HandleResponse(clientSocket, $"250 File {args[0]} has been deleted\r\n");
                }
                catch (UnauthorizedAccessException)
                {
                    HandleResponse(clientSocket, $"550 No permission to delete file {args[0]}\r\n");
                }
            }
            else
            {
                HandleResponse(clientSocket, "550 Failed to delete file\r\n");
            }
        }

        private void HandlePasv(Socket clientSocket, string[] args)
        {
            if (!isLoggedIn)
            {
                HandleResponse(clientSocket, "530 Please log in\r\n");
                return;
            }

            // Attempting to find a free port
            int pasvPort = 0;
            // Small range of ports that can be used for passive connection, as this is a test application
            for (int port = minPort; port <= maxPort; port++)
            {
                try
                {
                    pasvSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    pasvSocket.Bind(new IPEndPoint(ip, port));
                    pasvSocket.Listen(1);
                    pasvPort = port;
                    break; // Success, break the loop
                }
                catch (SocketException ex)
                {
                    Console.WriteLine($"Port {port} was busy");
                    continue; // Failed, port was busy, so we try the next one
                }
            }

            if (pasvSocket != null)
            {
                // Here we should pass the new port information to the client
                string ipParts = ip.ToString();

                if (ipParts == config.DefaultIP) // Listening on all interfaces (not the best solution)
                {
                    ipParts = config.IP; // Set to the fixed IP from configuration
                }

                string[] splitIpParts = ipParts.Split('.');
                // pasvPort >> 8 divides pasvPort by 256.
                // pasvPort & 0xFF returns the last 8 bits of pasvPort.
                HandleResponse(clientSocket, $"227 Entering Passive Mode ({string.Join(",", splitIpParts)},{pasvPort >> 8},{pasvPort & 0xFF})\r\n");
            }
            else // In case of failure to find a free port, print information about it
            {
                HandleResponse(clientSocket, "425 Failed to find a free port\r\n");
            }
        }

        private void HandleList(Socket clientSocket, string[] args)
        {
            if (!isLoggedIn)
            {
                HandleResponse(clientSocket, "530 Please log in\r\n");
                return;
            }

            HandleResponse(clientSocket, "150 Opening connection for file listing\r\n");

            // Determining the path of the directory to be listed
            string pathRelative = currentDir; // Starting from the current directory

            if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
            {
                // If the first argument is the path to the root directory, we set pathRelative to an empty string
                if (args[0] == "\\")
                {
                    pathRelative = string.Empty;
                }
                else
                {
                    string additionalPath = args[0].TrimStart('/');
                    pathRelative = Path.Combine(pathRelative, additionalPath); // Adding relative path if provided
                }
            }

            string pathAbsolute = Path.Combine(config.FTPRootDir, pathRelative);

            // Retrieving the list of files and directories
            string[] files = Directory.GetFiles(pathAbsolute);
            string[] directories = Directory.GetDirectories(pathAbsolute);

            StringBuilder listData = new StringBuilder();
            foreach (string file in files)
            {
                FileInfo fi = new FileInfo(file);
                string line = $"-rw-r--r-- 1 owner group {fi.Length} {fi.LastWriteTime:MMM dd HH:mm} {fi.Name}\r\n";
                listData.Append(line);
            }
            foreach (string directory in directories)
            {
                DirectoryInfo di = new DirectoryInfo(directory);
                string line = $"drwxr-xr-x 1 owner group 0 {di.LastWriteTime:MMM dd HH:mm} {di.Name}\r\n";
                listData.Append(line);
            }

            try
            {
                using (Socket dataSocket = pasvSocket.Accept())
                {
                    byte[] listBytes = Encoding.UTF8.GetBytes(listData.ToString());
                    dataSocket.Send(listBytes, 0, listBytes.Length, SocketFlags.None);
                }
                HandleResponse(clientSocket, "226 Transfer completed successfully\r\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while sending file list: {ex.Message}");
                HandleResponse(clientSocket, "451 Action aborted. Local error in processing\r\n");
            }
            finally
            {
                pasvSocket?.Close();
                pasvSocket = null;
            }
        }
        private void HandleRetr(Socket clientSocket, string[] args)
        {
            if (!isLoggedIn)
            {
                HandleResponse(clientSocket, "530 Please log in\r\n");
                return;
            }

            // Check if in passive mode
            if (pasvSocket == null)
            {
                HandleResponse(clientSocket, "425 Server is not in passive mode! Use PASV command before RETR\r\n");
                return;
            }

            // Read file from disk and send it to the client through the data socket
            string fileToRetrieveRelative = args[0].TrimStart('/');
            string fileToRetrieveAbsolute = Path.Combine(config.FTPRootDir, currentDir, fileToRetrieveRelative);

            // Check if the path does not go outside the root directory
            if (!fileToRetrieveAbsolute.StartsWith(config.FTPRootDir))
            {
                HandleResponse(clientSocket, "550 Attempt to access outside the root directory is not allowed\r\n");
                return;
            }

            if (File.Exists(fileToRetrieveAbsolute))
            {
                HandleResponse(clientSocket, "150 Opening binary connection for file transfer\r\n");
                try
                {
                    using (Socket dataSocket = pasvSocket.Accept())
                    using (FileStream fs = File.OpenRead(fileToRetrieveAbsolute))
                    {
                        byte[] buffer = new byte[1024];
                        int bytesRead;
                        while ((bytesRead = fs.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            dataSocket.Send(buffer, 0, bytesRead, SocketFlags.None);
                        }
                    }
                    HandleResponse(clientSocket, "226 Transfer completed successfully\r\n");
                }
                catch (Exception ex)
                {
                    // Log the error
                    Console.WriteLine($"Error while reading file: {ex.Message}");
                    HandleResponse(clientSocket, "451 Action aborted. Local error in processing\r\n");
                }
                finally
                {
                    // Close the passive socket and set to null
                    pasvSocket?.Close();
                    pasvSocket = null;
                }
            }
            else
            {
                HandleResponse(clientSocket, "550 Failed to read file\r\n");
            }
        }

        private void HandleStor(Socket clientSocket, string[] args)
        {
            if (!isLoggedIn)
            {
                HandleResponse(clientSocket, "530 Please log in\r\n");
                return;
            }

            // Save file to disk reading from the data socket
            string fileToStoreRelative = args[0].TrimStart('/');
            string fileToStoreAbsolute = Path.Combine(config.FTPRootDir, currentDir, fileToStoreRelative);

            // Check if the path does not go outside the root directory
            if (!fileToStoreAbsolute.StartsWith(config.FTPRootDir))
            {
                HandleResponse(clientSocket, "550 Attempt to access outside the root directory is not allowed\r\n");
                return;
            }

            HandleResponse(clientSocket, "150 Opening binary connection for file upload\r\n");
            try
            {
                using (Socket dataSocket = pasvSocket.Accept())
                using (FileStream fs = File.Create(fileToStoreAbsolute))
                {
                    byte[] buffer = new byte[1024];
                    int bytesRead;
                    while ((bytesRead = dataSocket.Receive(buffer)) > 0)
                    {
                        fs.Write(buffer, 0, bytesRead);
                    }
                }
                HandleResponse(clientSocket, "226 Transfer completed successfully\r\n");
            }
            catch (Exception ex)
            {
                // Log the error
                Console.WriteLine($"Error while writing file: {ex.Message}");
                HandleResponse(clientSocket, "451 Action aborted. Local error in processing\r\n");
            }
            finally
            {
                // Close the passive socket and set to null
                pasvSocket?.Close();
                pasvSocket = null;
            }
        }

        private void HandleType(Socket clientSocket, string[] args)
        {
            if (!isLoggedIn)
            {
                HandleResponse(clientSocket, "530 Please log in\r\n");
                return;
            }

            // Handle data type
            string arg = args[0].Trim();
            if (arg == "I")
            {
                // Type "I" (Image) is used for binary data transfer.
                HandleResponse(clientSocket, "200 Type set to I.\r\n");
            }
            else if (arg == "A")
            {
                // Type "A" (ASCII) is used for text data transfer.
                HandleResponse(clientSocket, "200 Type set to A.\r\n");
            }
            else
            {
                HandleResponse(clientSocket, "504 Type not supported.\r\n");
            }
        }

        private void Quit(Socket clientSocket)
        {
            // Close connection with the client
            clientSocket.Shutdown(SocketShutdown.Both);
            clientSocket.Close();
        }

        private void HandleQuit(Socket clientSocket, string[] args)
        {
            // Send farewell message and disconnect the socket
            HandleResponse(clientSocket, "221 Successfully terminated the connection. See you again\r\n");
            isLoggedIn = false;
            isAnonymousUser = false;
            Quit(clientSocket);
        }
    }

    //dotnet run 127.0.0.1 21
    internal class Program
    {
        private static void Main()
        {
            // This could be an application setting, you need to change it to your own path, this path is just an example
            string configPath = "C:\\Users\\kamil\\source\\repos\\SimpleServerFTP\\FTPData.json";
            ConfigModel? config = null;

            try
            {
                string configJson = File.ReadAllText(configPath);
                config = JsonConvert.DeserializeObject<ConfigModel>(configJson);
            }
            catch
            {
                Console.WriteLine("Error while parsing config json.");
                return;
            }

            if (config != null)
            {
                string ipAddress = config.IP;
                int port = config.Port;

                ServerFTP server = new ServerFTP(ipAddress, port, config);
                server.Start();
            }
            else
            {
                Console.WriteLine("Your configuration file is incorrect");
            }
        }
    }

    public class ConfigModel
    {
        public string IP { get; set; }
        public string DefaultIP { get; set; }
        public int Port { get; set; }
        public bool AnonymousUserEnabled { get; set; }
        public string DBUser { get; set; }
        public string Password { get; set; }
        public string FTPRootDir { get; set; }
        public int TCPMin { get; set; }
        public int TCPMax { get; set; }
    }
}