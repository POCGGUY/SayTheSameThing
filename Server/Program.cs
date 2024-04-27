using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Configuration;
using System.Threading;

namespace Server
{
    internal class Program
    {
        class ServerObject // Класс сервера
        {
            public List<ClientObject> clients = new List<ClientObject>();
            TcpListener tcpListener = new TcpListener(IPAddress.Any, 7000);
            protected internal void RemoveConnection(string id)  // Отключить клиента
            {
                ClientObject client = clients.FirstOrDefault(c => c.Id == id);
                if (client != null) clients.Remove(client);
                client.Close();
            }
            protected internal async Task ListenAsync()
            {
                try
                {
                    tcpListener.Start();
                    Console.WriteLine("Сервер запущен. Ожидание подключений...");

                    while (true)
                    {
                        TcpClient tcpClient = await tcpListener.AcceptTcpClientAsync();

                        ClientObject clientObject = new ClientObject(tcpClient, this);
                        clients.Add(clientObject);
                        Task.Run(clientObject.ProcessAsync);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
                finally
                {
                    Disconnect();
                }
            }

            protected internal async Task BroadcastPublicMessageAsync(string message)
            {
                foreach (var client in clients)
                {
                    if (!client.inGame)
                    {
                        await client.Writer.WriteLineAsync(message);
                        await client.Writer.FlushAsync();
                    }
                }
            }
            protected internal async Task BroadcastPrivateMessageAsync(string message, string id)
            {
                foreach (var client in clients)
                {
                    if (client.Id == id)
                    {
                        await client.Writer.WriteLineAsync(message);
                        await client.Writer.FlushAsync();
                        break;
                    }
                }
            }
            protected internal async Task BroadcastExceptSenderMessageAsync(string message, string id)
            {
                foreach (var client in clients)
                {
                    if (client.Id != id)
                    {
                        await client.Writer.WriteLineAsync(message);
                        await client.Writer.FlushAsync();
                    }
                }
            }
            protected internal async Task BroadcastToLobbyMessageAsync(string message, string firstId, string secondId)
            {
                foreach (var client in clients)
                {
                    if (client.Id == firstId || client.Id == secondId)
                    {
                        await client.Writer.WriteLineAsync(message);
                        await client.Writer.FlushAsync();
                    }
                }
            }



            protected internal void Disconnect()
            {
                foreach (var client in clients)
                {
                    client.Close();
                }
                tcpListener.Stop();
            }
        }
        class ClientObject // Класс клиента
        {
            protected internal string Id { get; } = Guid.NewGuid().ToString();
            protected internal StreamWriter Writer;
            protected internal StreamReader Reader;

            TcpClient client;
            ServerObject server;
            public string userName;
            public bool inGame = false;
            public bool isInvited = false;
            public bool wordEntered;
            public bool disconnected = false;
            public ClientObject inviter;
            public string word;

            public ClientObject(TcpClient tcpClient, ServerObject serverObject)
            {
                client = tcpClient;
                server = serverObject;

                var stream = client.GetStream();
                Reader = new StreamReader(stream);
                Writer = new StreamWriter(stream);
            }

            public async Task ProcessAsync()
            {
                try
                {
                    userName = await Reader.ReadLineAsync(); // Первым сообщением получает никнейм пользователя
                    foreach(var clientCompare in server.clients) // Проверка нет ли игрока с таким же ником на сервере
                    {
                        if (userName == clientCompare.userName && this != clientCompare)
                        {
                            string m = "Пользователь с таким именем уже есть на сервере";
                            await server.BroadcastPrivateMessageAsync(m, Id);
                            server.RemoveConnection(Id);
                            throw new Exception("Пользователь с таким именем уже есть на сервере");
                        }

                    }
                    string message = "Добро пожаловать! Чтобы узнать какие игроки есть на сервере напишите : \"!Получить список игроков\", чтобы пригласить игрока напишите в сообщении его имя";
                    await server.BroadcastPrivateMessageAsync(message, Id);
                    message = $"{userName} подключился к серверу";
                    await server.BroadcastPublicMessageAsync(message);
                    Console.WriteLine(message);
                    while (true)
                    {
                        try
                        {
                            bool skip = false;
                            message = await Reader.ReadLineAsync();
                            if (inGame) // Если пользователь в игре то сервер направляет все переменные в переменную word которая используется в классе LobbyObject
                            {
                                if (message != null && !wordEntered)
                                {
                                    word = message.ToLower(); // Перевод всех букв в нижний регистр чтобы избежать неравенства слов из-за чувствительности к регистру
                                    wordEntered = true;
                                    message = "Вы ввели слово: " + word;
                                    await server.BroadcastPrivateMessageAsync(message, Id);
                                }
                                else if(wordEntered) // Не даёт пользователю повторно ввести слово
                                {
                                    message = "Вы уже ввели слово";
                                    await server.BroadcastPrivateMessageAsync(message, Id);
                                }
                                continue;
                            }
                            if (message == null) continue;
                            else if (message == "+" && inviter != null) // Если пользователь принимает приглашение
                            {
                                LobbyObject lobby = new LobbyObject(this, inviter, server);
                                Task.Run(lobby.ProcessAsync);
                                skip = true;
                            }
                            else if (message == "-" && inviter != null) // Если пользователь отказался от приглашения
                            {
                                message = $"{userName} отказался от вашего приглашения";
                                await server.BroadcastPrivateMessageAsync(message, inviter.Id);
                                message = "Вы отказались от приглашения";
                                await server.BroadcastPrivateMessageAsync(message, Id);
                                isInvited = false;
                                inviter = null;
                                skip = true;
                            }
                            foreach (var clientCompare in server.clients)
                            {
                                if (message == clientCompare.userName && !clientCompare.isInvited && message != userName) // Пригласить пользователя если он не в игре и если его не пригласили ранее
                                {
                                    string broadcast = $"{userName} пригласил вас сыграть, чтобы принять приглашение отправьте \"+\", чтобы отказаться \"-\"";
                                    await server.BroadcastPrivateMessageAsync(broadcast, clientCompare.Id);
                                    clientCompare.isInvited = true;
                                    clientCompare.inviter = this;
                                    Console.WriteLine(broadcast);
                                    skip = true;
                                    break;
                                }
                                else if (message == clientCompare.userName && clientCompare.isInvited && message != userName)
                                {
                                    string broadcast = "Данный пользователь уже получил приглашение или находится в игре";
                                    await server.BroadcastPrivateMessageAsync(broadcast, Id);
                                    Console.WriteLine(broadcast);
                                    skip = true;
                                    break;
                                }
                                else if (message == userName)
                                {
                                    string broadcast = "Нельзя пригласить самого себя";
                                    await server.BroadcastPrivateMessageAsync(broadcast, Id);
                                    Console.WriteLine(broadcast);
                                    skip = true;
                                    break;
                                }
                                else if (message == "!Получить список игроков")
                                {
                                    int count = 1;
                                    foreach (var client in server.clients)
                                    {
                                        string broadcast = count + ". " + client.userName;
                                        await server.BroadcastPrivateMessageAsync(broadcast, Id);
                                        ++count;
                                    }
                                    skip = true;
                                    break;
                                }
                            }
                            if (skip) continue; // Пропускать текущую итерацию цикла чтобы не вывести сообщение приглашения в чат
                            message = $"{userName}: {message}"; // Вывод обычного сообщения в чат
                            Console.WriteLine(message);
                            await server.BroadcastPublicMessageAsync(message);
                        }
                        catch
                        {
                            message = $"{userName} отключился"; 
                            Console.WriteLine(message);
                            await server.BroadcastExceptSenderMessageAsync(message, Id);
                            disconnected = true;
                            inGame = false;
                            isInvited = false;
                            if (inviter != null)
                            {
                                inviter.inGame = false;
                                inviter.isInvited = false;
                            }
                            break;
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
                finally
                {
                    server.RemoveConnection(Id);
                }
            }
            protected internal void Close() // Освободить ресурсы после отключения пользователя
            {
                Writer.Close();
                Reader.Close();
                client.Close();
            }
        }

        class LobbyObject
        {
            ClientObject firstClient;
            ClientObject secondClient;
            ServerObject server;
            public LobbyObject(ClientObject firstInputClient, ClientObject secondInputClient, ServerObject inputServer)
            {
                firstClient = firstInputClient;
                secondClient = secondInputClient;
                server = inputServer;
                firstClient.inGame = true;
                secondClient.inGame = true;
            }
            public async Task ProcessAsync()
            {
                string message = "ef3b0e33877e8f8292346a022fee8d8b"; // Техническое сообщение, получив которое клиент очищает окно для вывода текста
                await server.BroadcastToLobbyMessageAsync(message, firstClient.Id, secondClient.Id);
                message = "Игра началась, введите начальную пару слов";
                Console.WriteLine(message);
                await server.BroadcastToLobbyMessageAsync(message, firstClient.Id, secondClient.Id);
                while (true)
                {
                    try
                    {
                        if(firstClient.disconnected || secondClient.disconnected) // Если в процессе игры кто-то из игроков отключился
                        {
                            message = "Игра прервана, поскольку было разорвано соединение с одним из игроков";
                            Console.WriteLine(message);
                            await server.BroadcastToLobbyMessageAsync(message, firstClient.Id, secondClient.Id);
                            firstClient.inGame = false;
                            secondClient.inGame = false;
                            firstClient.isInvited = false;
                            secondClient.isInvited = false;
                            firstClient.word = null;
                            secondClient.word = null;
                            firstClient.wordEntered = false;
                            secondClient.wordEntered = false;
                            firstClient.inviter = null;
                            break;

                        }
                        if (firstClient.word != null && secondClient.word != null) // Если оба игрока ввели слово
                        {
                            message = $"{firstClient.userName} назвал слово: " + firstClient.word;
                            await server.BroadcastToLobbyMessageAsync(message, firstClient.Id, secondClient.Id);
                            message = $"{secondClient.userName} назвал слово: " + secondClient.word;
                            await server.BroadcastToLobbyMessageAsync(message, firstClient.Id, secondClient.Id);
                            message = "Текущая пара слов: " + firstClient.word + " и " + secondClient.word;
                            Console.WriteLine(message);
                            await server.BroadcastToLobbyMessageAsync(message, firstClient.Id, secondClient.Id);

                            if (firstClient.word == secondClient.word) // Если слова обоих игроков совпали
                            {
                                message = "Вы нашли взаимопонимание! :)";
                                await server.BroadcastToLobbyMessageAsync(message, firstClient.Id, secondClient.Id);
                                Console.WriteLine(message);
                                message = "Финальное слово: " + firstClient.word;
                                await server.BroadcastToLobbyMessageAsync(message, firstClient.Id, secondClient.Id);
                                Console.WriteLine(message);
                                message = "Игра окончена! Лобби закрывается!";
                                await server.BroadcastToLobbyMessageAsync(message, firstClient.Id, secondClient.Id);
                                Console.WriteLine(message);
                                firstClient.inGame = false;
                                secondClient.inGame = false;
                                firstClient.isInvited = false;
                                secondClient.isInvited = false;
                                firstClient.word = null;
                                secondClient.word = null;
                                firstClient.wordEntered = false;
                                secondClient.wordEntered = false;
                                firstClient.inviter = null;
                                break;
                            }
                            firstClient.word = null;
                            secondClient.word = null;
                            firstClient.wordEntered = false;
                            secondClient.wordEntered = false;
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }

                }

            }
        }
        public static async Task Main(string[] args)
        {
            ServerObject server = new ServerObject(); // Запуск сервера
            await server.ListenAsync();
        }
    }
}