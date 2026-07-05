// ""

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MauiApp1.Helpers {
    public static class Constants {
        // Replace with your Firebase project settings
        public const string FirebaseApiKey = "AIzaSyCFV1AgR5apq0jyKt6z-YBBatzf0aSIkV0";
        public const string FirebaseProjectId = "kissiam";
        public const string FirebaseAuthDomain = "kissiam.firebaseapp.com";
        public const string FirebaseStorageBucket = "kissiam.firebasestorage.app";
        public const string FirebaseMessageSenderId = "546931489002";
        public const string FirebaseAppId = "1:546931489002:web:250f8a17fbf1f2b319ff93";
        public const string FirebaseMeasurementId = "G-0TBSTJ785C";

        // Firestore base
        public static string FirestoreBaseUrl => $"https://firestore.googleapis.com/v1/projects/{FirebaseProjectId}/databases/(default)/documents";
    }
}
