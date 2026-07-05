using System.Globalization;
using MauiApp1.Models;

namespace MauiApp1.Helpers {
    // Maps a RequestStatus value to a Color, used for status badges in the UI.
    // Wire this up by adding a static instance to App.xaml as a resource, or
    // by adding it as a page-level resource where it's used.
    public class StatusToColorConverter : IValueConverter {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
            if (value is RequestStatus s) {
                return s switch {
                    RequestStatus.Approved => Color.FromArgb("#1B873F"),  // green
                    RequestStatus.Rejected => Color.FromArgb("#B42318"),  // red
                    RequestStatus.PendingApproval => Color.FromArgb("#B54708"),  // amber
                    RequestStatus.Submitted => Color.FromArgb("#175CD3"),  // blue
                    RequestStatus.Cancelled => Color.FromArgb("#667085"),  // grey
                    RequestStatus.Draft => Color.FromArgb("#667085"),  // grey
                    _ => Colors.Gray
                };
            }
            return Colors.Gray;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}
