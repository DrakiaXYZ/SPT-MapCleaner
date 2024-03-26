namespace DrakiaXYZ.SPTMapCleaner
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Missing argument exported project path");
                WaitForExit();
                return;
            }

            MapCleaner cleaner = new MapCleaner(args[0]);
            if (!cleaner.ValidateFolder())
            {
                foreach (var error in cleaner.GetErrors())
                {
                    Console.WriteLine(error);
                }
                WaitForExit();
                return;
            }

            cleaner.RunCleaner();
            WaitForExit();
        }

        private static void WaitForExit()
        {
            Console.WriteLine("Press any key to quit");
            Console.ReadLine();
        }
    }
}
