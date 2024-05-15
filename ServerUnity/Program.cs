
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Data.SqlClient;
using System.Collections.Generic;

namespace ServerUnity
{
    class Server
    {
        static readonly IPAddress ipAddress = IPAddress.Parse("192.168.0.104");

        private static SqlConnection? connection;

        static List<KeyValuePair<string, DateTime>>? AuthList;

        static async Task Main()
        {
            try
            {
                AuthList = new List<KeyValuePair<string, DateTime>>();

                Task t = Task.Factory.StartNew(CheckAuthList);

                string connString = @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=C:\Users\dmitr\source\repos\ServerUnity\ServerUnity\Database.mdf;Integrated Security=True;MultipleActiveResultSets=True";
                connection = new SqlConnection(connString);
                try
                {
                    connection.Open();

                    string command = "UPDATE AuthTBL SET isActive = 0";
                    var myCommand = new SqlCommand(command, connection);
                    SqlDataReader sqlDataReader = myCommand.ExecuteReader();

                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }

                await GenerateKeys(connection, 20);

                IPEndPoint ipEndPoint = new(ipAddress, 11000);

                using Socket listener = new(ipEndPoint.AddressFamily,
                    SocketType.Stream, ProtocolType.Tcp);

                listener.Bind(ipEndPoint);
                listener.Listen(100);

                while (true)
                {
                    Socket handler = await listener.AcceptAsync();

                    // Receive message.
                    byte[] buffer = new byte[1024];
                    int received = await handler.ReceiveAsync(buffer, SocketFlags.None);
                    string response = Encoding.UTF8.GetString(buffer, 0, received);

                    string[] data = response.Split('|');

                    Console.WriteLine($"Socket server received message: \"{response}\"");

                    // Данные для авторизации поступают строкой и разбиваются по символу '|'
                    // data[0] - Authorization или Registration
                    // data[1] - AuthName
                    // data[2] - AuthPassword
                    // data[3] - LicenceKey
                    // data[4] - DeviceNumber
                    switch (data[0])
                    {
                        case "Authorization":
                            {
                                CheckAuthList();

                                string command = "SELECT * FROM AuthTBL WHERE AuthName LIKE '" + data[1] + "%' AND AuthPassword LIKE '" + data[2] + "%' AND isActive LIKE '0'";
                                var myCommand = new SqlCommand(command, connection);
                                SqlDataReader sqlDataReader = myCommand.ExecuteReader();
                                if (sqlDataReader.HasRows)
                                {
                                    string command3 = "UPDATE AuthTBL SET isActive=1 WHERE DeviceNumber='" + data[4] + "'";
                                    var myCommand3 = new SqlCommand(command3, connection);
                                    _ = myCommand3.ExecuteReader();

                                    sqlDataReader.Read();

                                    response = "true" + "|" + sqlDataReader.GetBoolean(6);

                                    AuthList.Add(new KeyValuePair<string, DateTime>(data[4], DateTime.Now));
                                }
                                else
                                    response = "false";
                                sqlDataReader.Close();
                            }
                            break;
                        case "Registration":
                            {
                                string command = "SELECT * FROM AuthTBL WHERE AuthName LIKE '" + data[1] + "%' AND AuthPassword LIKE '" + data[2] + "%'";
                                var myCommand = new SqlCommand(command, connection);
                                SqlDataReader sqlDataReader = myCommand.ExecuteReader();

                                string checkKey = "SELECT * FROM LicenceKeyTBL WHERE LicenceKey LIKE '" + data[3] + "' AND isActivated LIKE '0'";
                                var myCommand2 = new SqlCommand(checkKey, connection);
                                SqlDataReader sqlDataReader1 = myCommand2.ExecuteReader();
                                if (sqlDataReader.HasRows == true || sqlDataReader1.HasRows == false)
                                {
                                    response = "false";
                                }
                                else
                                {
                                    string command1 = "INSERT INTO AuthTBL ([AuthName], [AuthPassword], [LicenceKey], [isActive], [DeviceNumber]) VALUES ('" +
                                        data[1] + "', '" + data[2] + "', '" + data[3] + "' , " + "1" + ", '" + data[4] + "')";
                                    var myCommand1 = new SqlCommand(command1, connection);
                                    _ = myCommand1.ExecuteReader();

                                    string command3 = "UPDATE LicenceKeyTBL SET isActivated=1 WHERE LicenceKey='" + data[3] + "'";
                                    var myCommand3 = new SqlCommand(command3, connection);
                                    _ = myCommand3.ExecuteReader();

                                    response = "true";

                                    AuthList.Add(new KeyValuePair<string, DateTime>(data[4], DateTime.Now));
                                }
                                sqlDataReader.Close();

                            }
                            break;
                    }

                    var ackMessage = response;
                    if (response == "true")
                    {

                    }
                    var echoBytes = Encoding.UTF8.GetBytes(ackMessage);
                    await handler.SendAsync(echoBytes, 0);
                    Console.WriteLine(
                        $"Socket server sent acknowledgment: \"{ackMessage}\"");

                    // Sample output:
                    //    Socket server received message: "Hi friends 👋!"
                    //    Socket server sent acknowledgment: "<|ACK|>"
                }
            }
            catch
            (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        static async Task GenerateKeys(SqlConnection sqlConnection, int keysCount)
        {
            char[] chars = new char[] { 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K',
            'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z'};
            char[] numbers = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };

            var rand = new Random();
            for (int i = 0; i < keysCount; i++)
            {
                string key = string.Empty;
                for (int j = 0; j < 20; j++)
                {
                    if (j < 2)
                        key += numbers[rand.Next(numbers.Length)];
                    else if (j < 16)
                        key += chars[rand.Next(chars.Length)];
                    else
                        key += numbers[rand.Next(numbers.Length)];
                }

                string command1 = "INSERT INTO LicenceKeyTBL ([LicenceKey], [isActivated]) VALUES ('" +
                                       key + "', '" + '0' + "')";
                var myCommand1 = new SqlCommand(command1, connection);
                _ = myCommand1.ExecuteReader();
            }
        }

        static void CheckAuthList()
        {
            try
            {
                foreach (KeyValuePair<string, DateTime> user in AuthList)
                {
                    if (DateTime.Now - user.Value > new TimeSpan(0, 0, 5))
                    {
                        string command3 = "UPDATE AuthTBL SET isActive=0 WHERE DeviceNumber='" + user.Key + "'";
                        var myCommand3 = new SqlCommand(command3, connection);
                        _ = myCommand3.ExecuteReader();

                        AuthList.Remove(user);

                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            Thread.Sleep(1000);
        }
    }
}