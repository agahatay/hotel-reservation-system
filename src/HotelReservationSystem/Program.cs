using System.Globalization;
using HotelReservationSystem.Core.Configuration;
using HotelReservationSystem.Forms;

namespace HotelReservationSystem;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // The interface is in English, so numbers are formatted the English way (1,234.50 / 5.3%).
        // Dates use an explicit dd.MM.yyyy format, and amount input accepts both "," and "." decimals.
        var culture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.CurrentCulture = culture;

        ApplicationConfiguration.Initialize();
        ErrorHandler.Install();

        while (true)
        {
            var settings = AppSettings.Load(AppContext.BaseDirectory);
            var services = new AppServices(settings.ConnectionString);

            if (!services.Database.TryConnect(out var error))
            {
                using var dbSettings = new FrmDatabaseSettings(settings, error);
                if (dbSettings.ShowDialog() == DialogResult.OK) continue; // saved new settings → retry
                return;
            }

            if (!services.Auth.AnyUsers())
            {
                using var setup = new FrmFirstRunSetup(services);
                if (setup.ShowDialog() != DialogResult.OK) return;
            }

            if (!RunSessions(services, settings)) return;
        }
    }

    /// <summary>Login → main window, repeated on sign-out. Returns true when the app should restart with new settings.</summary>
    private static bool RunSessions(AppServices services, AppSettings settings)
    {
        while (true)
        {
            AuthenticatedUser user;
            using (var login = new FrmLogin(services, settings))
            {
                var result = login.ShowDialog();
                if (result == DialogResult.Retry) return true;
                if (result != DialogResult.OK || login.SignedInUser is null) return false;
                user = login.SignedInUser;

                if (login.MustChangePassword && !ForcePasswordChange(services, user))
                    continue; // not changed → back to the sign-in screen
            }

            using var main = new FrmMain(services, user);
            Application.Run(main);
            if (!main.SignOutRequested) return false;
        }
    }

    /// <summary>Demo accounts, new accounts and admin-reset passwords must be replaced before use.</summary>
    private static bool ForcePasswordChange(AppServices services, AuthenticatedUser user)
    {
        using var dlg = new FrmPasswordDialog(
            "Choose a new password",
            $"The account \"{user.Username}\" is using a temporary or demonstration password. " +
            "Choose a new password to continue.",
            requireCurrent: true,
            save: (current, next) => services.Auth.ChangeOwnPassword(user, current, next));
        dlg.StartPosition = FormStartPosition.CenterScreen;
        dlg.ShowInTaskbar = true;
        return dlg.ShowDialog() == DialogResult.OK;
    }
}
