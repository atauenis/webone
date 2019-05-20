using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebOne
{
/// <summary>
/// HTTP Server (listener)
/// </summary>
	class HTTPServer
	{
		//Based on https://habr.com/ru/post/120157/

		TcpListener Listener; 

		/// <summary>
		/// Start the HTTP Server
		/// </summary>
		/// <param name="Port">TCP port which the Server should listen</param>
		public HTTPServer(int Port)
		{
			Console.WriteLine("Starting server...");
			Listener = new TcpListener(IPAddress.Any, Port); 
			Listener.Start();
			Console.WriteLine("Listening for HTTP 1.x on port {0}.", Port);

			while (true)
			{
				// Принимаем новых клиентов. После того, как клиент был принят, он передается в новый поток (ClientThread)
				// с использованием пула потоков.
				//ThreadPool.QueueUserWorkItem(new WaitCallback(ClientThread), Listener.AcceptTcpClient());

				
                // Принимаем нового клиента
                TcpClient Client = Listener.AcceptTcpClient();
                // Создаем поток
                Thread Thread = new Thread(new ParameterizedThreadStart(ClientThread));
                // И запускаем этот поток, передавая ему принятого клиента
                Thread.Start(Client);
			}
		}

		/// <summary>
		/// Запуск обработчика запроса
		/// </summary>
		/// <param name="StateInfo">Приведенный к классу TcpClient объект StateInfo</param>
		static void ClientThread(Object StateInfo)
		{
			// Просто создаем новый экземпляр класса Client и передаем ему приведенный к классу TcpClient объект StateInfo
			new Transit((TcpClient)StateInfo);
		}

		~HTTPServer()
		{
			if (Listener != null)
			{
				Console.WriteLine("Stopping server...");
				Listener.Stop();
			}
		}

		static void Main(string[] args)
		{
			//probably should be put into trash.
			// Определим нужное максимальное количество потоков
			// Пусть будет по 4 на каждый процессор
			int MaxThreadsCount = Environment.ProcessorCount * 4;
			// Установим максимальное количество рабочих потоков
			ThreadPool.SetMaxThreads(MaxThreadsCount, MaxThreadsCount);
			// Установим минимальное количество рабочих потоков
			ThreadPool.SetMinThreads(2, 2);
			// Создадим новый сервер на порту 80
			new HTTPServer(80);
		}
	}

}
