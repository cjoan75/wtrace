using System;
using Utilities;

namespace LowLevelDesign.WTrace
{
    class Shim
    {
        [STAThread()]
        static void Main(string[] args)
        {
            Unpack();

            DoMain(args);
        }

        /// <summary>
        /// Unpacks all the support files associated with this program.   
        /// </summary>
        public static bool Unpack()
        {
            return SupportFiles.UnpackResourcesIfNeeded();
        }


        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static void DoMain(string[] args)
        {
            Program.main(args);
        }
    }
}
