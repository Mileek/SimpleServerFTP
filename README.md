# SimpleServerFTP

SimpleServerFTP is a lightweight, C#-based FTP server application designed to support basic file transfer operations. 
It includes functionality for handling user authentication, file uploads, downloads, directory listings, and other standard FTP commands.
FTPServer is based on .NET Sockets.

## Features

- Supports both anonymous and authenticated users.
- Handles basic FTP commands such as USER, PASS, PWD, CWD, CDUP, MKD, RMD, PASV, LIST, STOR, RETR, DELE, TYPE, QUIT.
- Uses passive mode for data transfers.
- Configurable via a JSON configuration file.

## Requirements

- .NET Core 3.1 or higher
- Newtonsoft.Json library

## Installation

1. Clone the repository:
    ```sh
    git clone https://github.com/Mileek/SimpleServerFTP.git
    ```
2. Navigate to the project directory:
    ```sh
    cd SimpleServerFTP
    ```
3. Install dependencies:
    ```sh
    dotnet restore
    ```

## Configuration

The server configuration is managed through a JSON file. Below is an example configuration (`FTPData.json`):

```json
{
  "IP": "127.0.0.1",
  "DefaultIP": "0.0.0.0",
  "Port": 21,
  "AnonymousUserEnabled": true,
  "DBUser": "admin",
  "Password": "password",
  "FTPRootDir": "C:\\FTPServer\\Root",
  "TCPMin": 1024,
  "TCPMax": 65535
}
```

- `IP`: The IP address the server will bind to.
- `DefaultIP`: The default IP address for passive mode.
- `Port`: The port number the server will listen on.
- `AnonymousUserEnabled`: Allow or disallow anonymous logins.
- `DBUser`: Username for authenticated access.
- `Password`: Password for authenticated access.
- `FTPRootDir`: The root directory for the FTP server.
- `TCPMin` and `TCPMax`: Range of ports to use for passive mode connections.

## Running the Server

1. Make sure your configuration file is properly set up and accessible.
2. Run the server using the .NET CLI:
    ```sh
    dotnet run
    ```
3. The server will start listening for connections on the configured IP address and port.

## Usage

### FTP Commands Supported

- **USER**: Login username.
- **PASS**: Login password.
- **PWD**: Print working directory.
- **CWD**: Change working directory.
- **CDUP**: Change to parent directory.
- **MKD**: Make directory.
- **RMD**: Remove directory.
- **PASV**: Enter passive mode.
- **LIST**: List files/directories.
- **STOR**: Upload file.
- **RETR**: Download file.
- **DELE**: Delete file.
- **TYPE**: Set transfer type (binary or ASCII).
- **QUIT**: Logout and close connection.

### Example Commands

```sh
USER anonymous
PASS anypassword
PWD
CWD /foldername
LIST
STOR filename
RETR filename
DELE filename
QUIT
```

## Error Handling

The server logs errors related to socket connections, threads, and general exceptions to the console for debugging purposes.

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

## Contributing

Contributions are welcome! Please submit pull requests to the repository for any enhancements or bug fixes.

## Contact

For any questions or feedback, please contact me at [Mileek](https://github.com/Mileek).
