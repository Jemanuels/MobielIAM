using MauiApp1.Services;
using MauiApp1.Views;
using Microsoft.Extensions.DependencyInjection;

namespace MauiApp1 {
    public partial class App : Application {
        public App() {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState) {
            var services = IPlatformApplication.Current!.Services;
            var auth = services.GetRequiredService<IFirebaseAuthService>();

            // If a token is in SecureStorage, try to restore the session
            // (re-fetches the Firestore user doc to populate role, etc.).
            // We block briefly here for MVP simplicity; this only runs at startup.
            var restored = auth.RestoreSessionAsync().GetAwaiter().GetResult();

            if (restored) {
                return new Window(new AppShell());
            }

            var loginPage = services.GetRequiredService<LoginPage>();
            return new Window(new NavigationPage(loginPage));
        }
    }
}
