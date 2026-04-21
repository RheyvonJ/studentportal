using System;

namespace StudentPortal.Services
{
    internal static class JoinMeetFlowTests
    {
        public static bool AdminGetsJwt(Func<string, string, string, TimeSpan?, string> generator)
        {
            var token = generator("room123", "Professor X", "admin@example.com", TimeSpan.FromMinutes(10));
            return !string.IsNullOrWhiteSpace(token);
        }

        public static bool NonAdminNoJwt(Func<string, string, string, TimeSpan?, string> generator)
        {
            try
            {
                var _ = generator("room123", "Student Y", "student@example.com", TimeSpan.FromMinutes(10));
                return false;
            }
            catch
            {
                return true;
            }
        }
    }
}
