using Infrastructure.Excel;
using Infrastructure.Pdf;
using Infrastructure.Settings;
using Infrastructure.Update;

namespace App
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            var settingsStore = new JsonSettingsStore();
            Application.Run(new MainForm(settingsStore, new ExcelPassengerReader(),
                new EVisaPdfExtractor(), new ExcelPassengerWriter(), new GitHubUpdateService(settingsStore.Load().UpdateFeedUrl)));
        }
    }
}
