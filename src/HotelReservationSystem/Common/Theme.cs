namespace HotelReservationSystem.Common;

/// <summary>Colors and fonts used across the application.</summary>
internal static class Theme
{
    public static readonly Color Primary = Color.FromArgb(30, 77, 123);
    public static readonly Color PrimaryDark = Color.FromArgb(22, 58, 94);
    public static readonly Color PrimaryLight = Color.FromArgb(220, 232, 245);

    public static readonly Color Sidebar = Color.FromArgb(21, 43, 66);
    public static readonly Color SidebarHover = Color.FromArgb(33, 61, 90);
    public static readonly Color SidebarActive = Color.FromArgb(44, 80, 117);
    public static readonly Color SidebarText = Color.FromArgb(214, 224, 235);

    public static readonly Color Background = Color.FromArgb(243, 245, 248);
    public static readonly Color Surface = Color.White;
    public static readonly Color Border = Color.FromArgb(221, 226, 232);
    public static readonly Color GridHeader = Color.FromArgb(246, 248, 250);
    public static readonly Color GridAlternate = Color.FromArgb(250, 251, 252);

    public static readonly Color Text = Color.FromArgb(31, 41, 55);
    public static readonly Color MutedText = Color.FromArgb(107, 114, 128);

    public static readonly Color Danger = Color.FromArgb(180, 35, 24);
    public static readonly Color DangerLight = Color.FromArgb(253, 236, 234);
    public static readonly Color Success = Color.FromArgb(18, 128, 92);
    public static readonly Color Warning = Color.FromArgb(181, 71, 8);

    // Reservation status tints (same meaning as the original application's row colors).
    public static readonly Color BookedTint = Color.FromArgb(228, 238, 255);
    public static readonly Color CheckedInTint = Color.FromArgb(223, 245, 230);
    public static readonly Color CompletedTint = Color.FromArgb(238, 239, 241);
    public static readonly Color CancelledTint = Color.FromArgb(253, 228, 226);

    public static readonly Color Occupied = Color.FromArgb(232, 104, 92);
    public static readonly Color Weekend = Color.FromArgb(240, 243, 247);
    public static readonly Color Today = Color.FromArgb(255, 241, 204);

    public const string FontFamily = "Segoe UI";
    public static readonly Font Base = new(FontFamily, 9.75f);
    public static readonly Font Bold = new(FontFamily, 9.75f, FontStyle.Bold);
    public static readonly Font Small = new(FontFamily, 8.75f);
    public static readonly Font Section = new("Segoe UI Semibold", 11f);
    public static readonly Font Title = new("Segoe UI Semibold", 16f);
    public static readonly Font Kpi = new("Segoe UI Semibold", 20f);
    public static readonly Font Brand = new("Segoe UI Semibold", 13f);

    public static Color StatusTint(ReservationStatus status) => status switch
    {
        ReservationStatus.Booked => BookedTint,
        ReservationStatus.CheckedIn => CheckedInTint,
        ReservationStatus.Completed => CompletedTint,
        ReservationStatus.Cancelled => CancelledTint,
        _ => Surface
    };

    public static string StatusText(ReservationStatus status) => status switch
    {
        ReservationStatus.CheckedIn => "Checked in",
        _ => status.ToString()
    };
}
