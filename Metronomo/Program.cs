namespace Metronomo;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var form = new MainForm();

        // "Metronomo.exe --snap ruta.png" guarda una captura de la ventana y sale (para revisar el diseño).
        if (args.Length == 2 && args[0] == "--snap")
        {
            form.Shown += async (_, _) =>
            {
                await Task.Delay(300);
                using var bmp = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(bmp, new Rectangle(Point.Empty, form.Size));
                bmp.Save(args[1]);
                form.Close();
            };
        }
        Application.Run(form);
    }
}
