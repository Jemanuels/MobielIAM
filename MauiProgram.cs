using MauiApp1.Services;
using MauiApp1.ViewModels;
using MauiApp1.Views;
using Microsoft.Extensions.Logging;

namespace MauiApp1 {
    public static class MauiProgram {
        public static MauiApp CreateMauiApp() {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts => {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            builder.Services.AddSingleton<ICurrentUserService, CurrentUserService>();

            builder.Services.AddHttpClient<IFirebaseAuthService, FirebaseAuthService>();
            builder.Services.AddHttpClient<IAccessRequestService, AccessRequestService>();
            builder.Services.AddHttpClient<IBusinessSystemService, BusinessSystemService>();

            // ---- ViewModels ----
            builder.Services.AddTransient<LoginViewModel>();
            builder.Services.AddTransient<RegisterViewModel>();
            builder.Services.AddTransient<DashboardViewModel>();
            builder.Services.AddTransient<NewAccessRequestViewModel>();
            builder.Services.AddTransient<MyRequestsViewModel>();
            builder.Services.AddTransient<RequestDetailsViewModel>();
            builder.Services.AddTransient<ApprovalQueueViewModel>();
            builder.Services.AddTransient<ApprovalDetailsViewModel>();
            builder.Services.AddTransient<AdminSystemsViewModel>();
            builder.Services.AddTransient<EditBusinessSystemViewModel>();

            // ---- Pages ----
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<RegisterPage>();
            builder.Services.AddTransient<DashboardPage>();
            builder.Services.AddTransient<NewAccessRequestPage>();
            builder.Services.AddTransient<MyRequestsPage>();
            builder.Services.AddTransient<RequestDetailsPage>();
            builder.Services.AddTransient<ApprovalQueuePage>();
            builder.Services.AddTransient<ApprovalDetailsPage>();
            builder.Services.AddTransient<AdminSystemsPage>();
            builder.Services.AddTransient<EditBusinessSystemPage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
