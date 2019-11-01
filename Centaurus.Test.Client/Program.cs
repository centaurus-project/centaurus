using Centaurus.Domain;
using Centaurus.Models;
using NUnit.Framework;
using stellar_dotnet_sdk;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Test.Client
{
    class Program
    {

        static void Test()
        {
            try
            {
                var sockets = new List<UserWebSocketConnection>();

                foreach (string seed in new string[] { "SCR6C6STGV7RKURFGV7XA76WRUZYAKRYFICLWBHTI7NAW3VXVRA5T75E", "SBMZHCOQF2SANK2HSCMEZTOCJKBXV6CYRLAEE66BWSQBLKOZXLNMQN3T" })
                {
                    UserWebSocketConnection ws = new UserWebSocketConnection();
                    ws.EstablishConnection().Wait();
                    sockets.Add(ws);
                }

                Stopwatch sw = new Stopwatch();
                sw.Start();

                var counter = 0;
                var failed = 0;

                var runCount = 1_000_000;

                for (var i = 0; i < runCount; i++)
                    sockets[0].PlaceOrder(0, 1 * 10_000_000, 1);


                //Parallel.For(0, runCount, async (i) =>
                //{
                //    try
                //    {
                //        var res = await sockets[0].PlaceOrder(0, 1 * 10_000_000, 1);
                //        if (!(res is OrderResult))
                //            Console.WriteLine("Order result was not received");
                //    }
                //    catch (Exception e)
                //    {
                //        Interlocked.Increment(ref failed);
                //    }
                //    Interlocked.Increment(ref counter);
                //});


                //for (var i = 0; i < runCount; i++)
                //{
                //    Task.Factory.StartNew(async () =>
                //    {
                //        var lockedIndex = i;
                //        try
                //        {
                //            //var tasks = new Task<Message>[2];

                //            //var index = (lockedIndex % 2 == 0 ? 0 : 1);

                //            //tasks[index] = sockets[0].PlaceOrder(index, 1 * 10_000_000, 1);
                //            ////tasks[1] = sockets[1].PlaceOrder((lockedIndex % 2 != 0 ? 0 : 1), 1 * 10_000_000, 1);

                //            //await Task.WhenAll(tasks);

                //            await sockets[0].PlaceOrder(0, 1 * 10_000_000, 1);
                //        }
                //        catch (Exception e)
                //        {
                //            Console.WriteLine(e);
                //        }

                //        var current = counter + 1;

                //        Interlocked.Increment(ref counter);
                //        if (current % 10000 == 0)
                //            Console.WriteLine(current);
                //    });
                //}

                sw.Stop();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"{sw.ElapsedMilliseconds}ms");

                Console.ForegroundColor = ConsoleColor.White;

                while (counter < runCount)
                {
                    Thread.Sleep(1000);
                }

                Console.WriteLine($"Failed: {failed}");
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc);
            }
        }

        static void Client()
        {
            MessageHandlers.Init();

            var settings = new AlphaSettings 
            { 
                    HorizonUrl = "https://horizon-testnet.stellar.org", 
                    NetworkPassphrase = "Test SDF Network ; September 2015" 
            };

            Global.Init(settings, new FileSystem());

            UserWebSocketConnection ws = new UserWebSocketConnection();
            ws.EstablishConnection().Wait();

            Console.WriteLine("Type 'q' to close...");
            Console.WriteLine("Place order format: po {order-direction} {amount (will be multiplied by 10 000 000)} {price}");
            Console.WriteLine("Example: po 0 1 2");

            while (true)
            {
                try
                {
                    var line = Console.ReadLine();
                    if (line == "q")
                        break;

                    if (line.IndexOf("po") == 0)
                    {
                        var poArgs = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        var res = ws.PlaceOrder(int.Parse(poArgs[1]), long.Parse(poArgs[2]) * 10_000_000, double.Parse(poArgs[3])).Result;
                        Console.WriteLine(res.Status.ToString());
                    }
                }
                catch (Exception exc)
                {
                    Console.WriteLine(exc);
                }
            }
        }

        static void Main(string[] args)
        {
            try
            {
                Client();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            Console.ReadLine();
        }
    }
}
