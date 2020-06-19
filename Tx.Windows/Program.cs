using System;
using System.Diagnostics;
using Tx.Windows;

namespace wtrace
{
    class Program
    {
        static void Main(string[] args)
        {
            Process logman = Process.Start(
                "logman.exe",
                "create trace TCP-2 -rt -nb 2 2 -bs 1024 -p {7dd42a49-5329-4832-8dfd-43d979153a88} 0xffffffffffffffff -ets");
            logman.WaitForExit();

            IObservable<EtwNativeEvent> session = EtwObservable.FromSession("TCP-2");
            using (session.Subscribe(e => Console.WriteLine("{0} {1}", e.TimeStamp, e.Id)))
            {
                Console.ReadLine();
            }
            logman = Process.Start(
                "logman.exe",
                "stop TCP-2 -ets");
            logman.WaitForExit();
        }
    }
}
