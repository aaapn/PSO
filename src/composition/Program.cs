using System;
using System.Text;
using Pos.Infrastructure;

namespace Pos.Composition;

internal static class Program
{
    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        if (OperatingSystem.IsWindows())
        {
            Console.InputEncoding = Encoding.UTF8;
        }
        try
        {
            Container container = new();
            ConsoleUi ui = new(container.Pos);
            if (args.Length > 0 && args[0] == "--demo")
            {
                return ui.Demo();
            }
            ui.Run();
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("Ошибка: " + error.Message);
            return 1;
        }
    }
}
