using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tx.Windows.Etw;

namespace wtrace_3
{
    class Program
    {
        static void Main(string[] args)
        {
            var etwSession = EtwRecorder.StartSession("test");

            Console.ReadLine();

            EtwRecorder.StopSession(etwSession);
        }
    }
}
