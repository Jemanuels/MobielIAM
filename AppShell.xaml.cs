using MauiApp1.Views;

namespace MauiApp1 {
    public partial class AppShell : Shell {
        public AppShell() {
            InitializeComponent();

            Routing.RegisterRoute(nameof(NewAccessRequestPage), typeof(NewAccessRequestPage));
            Routing.RegisterRoute(nameof(MyRequestsPage), typeof(MyRequestsPage));
            Routing.RegisterRoute(nameof(RequestDetailsPage), typeof(RequestDetailsPage));
            Routing.RegisterRoute(nameof(ApprovalQueuePage), typeof(ApprovalQueuePage));
            Routing.RegisterRoute(nameof(ApprovalDetailsPage), typeof(ApprovalDetailsPage));
            Routing.RegisterRoute(nameof(AdminSystemsPage), typeof(AdminSystemsPage));
            Routing.RegisterRoute(nameof(EditBusinessSystemPage), typeof(EditBusinessSystemPage));
        }
    }
}
