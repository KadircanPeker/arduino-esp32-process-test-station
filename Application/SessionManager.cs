using System;
using ProcessTestApp.Domain;

namespace ProcessTestApp.Application
{
    public static class SessionManager
    {
        private static User _currentUser;
        private static readonly object _lock = new object();

        public static User CurrentUser
        {
            get
            {
                lock (_lock)
                {
                    return _currentUser;
                }
            }
        }

        public static string LoggedInUsername
        {
            get { return CurrentUser != null ? CurrentUser.Username : null; }
        }

        public static string LoggedInUserFullName
        {
            get { return CurrentUser != null ? CurrentUser.FullName : null; }
        }

        public static string LoggedInUserRole
        {
            get { return CurrentUser != null ? CurrentUser.Role : null; }
        }

        public static void SetSession(User user)
        {
            lock (_lock)
            {
                _currentUser = user;
            }
        }

        public static void ClearSession()
        {
            lock (_lock)
            {
                _currentUser = null;
            }
        }

        public static bool IsLoggedIn
        {
            get { return CurrentUser != null; }
        }
    }
}
