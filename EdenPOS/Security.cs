using System;
using System.Linq;
using System.Security.Cryptography;

namespace EdenPOS
{
    public static class UserRoles
    {
        public const string Owner = "Owner";
        public const string Worker = "Worker";

        public static bool IsValid(string role)
        {
            return role == Owner || role == Worker;
        }

        public static string DisplayName(string role)
        {
            return role == Owner ? "Dueña" : "Trabajador";
        }
    }

    public static class UserSecurity
    {
        private const int Iterations = 100000;
        private const int HashSize = 32;

        public static bool IsValidPin(string pin)
        {
            return !string.IsNullOrWhiteSpace(pin) &&
                   pin.Length >= 4 &&
                   pin.Length <= 8 &&
                   pin.All(char.IsDigit);
        }

        public static AppUser CreateUser(string name, string pin, string role)
        {
            if (!Validation.IsValidRequiredText(name, 3, 60))
                throw new ArgumentException("El nombre debe tener entre 3 y 60 caracteres.");
            if (!IsValidPin(pin))
                throw new ArgumentException("El PIN debe contener entre 4 y 8 dígitos.");
            if (!UserRoles.IsValid(role))
                throw new ArgumentException("Seleccione un rol válido.");

            var user = new AppUser
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = Validation.NormalizeName(name),
                Role = role,
                Active = true,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            SetPin(user, pin);
            return user;
        }

        public static void SetPin(AppUser user, string pin)
        {
            if (user == null) throw new ArgumentNullException("user");
            if (!IsValidPin(pin))
                throw new ArgumentException("El PIN debe contener entre 4 y 8 dígitos.");

            var salt = new byte[16];
            using (var random = RandomNumberGenerator.Create()) random.GetBytes(salt);
            using (var derive = new Rfc2898DeriveBytes(pin, salt, Iterations))
            {
                user.PinSalt = Convert.ToBase64String(salt);
                user.PinHash = Convert.ToBase64String(derive.GetBytes(HashSize));
            }
            user.UpdatedAt = DateTime.Now;
        }

        public static bool VerifyPin(AppUser user, string pin)
        {
            if (user == null || !user.Active || !IsValidPin(pin) ||
                string.IsNullOrWhiteSpace(user.PinSalt) || string.IsNullOrWhiteSpace(user.PinHash)) return false;
            try
            {
                var salt = Convert.FromBase64String(user.PinSalt);
                var expected = Convert.FromBase64String(user.PinHash);
                using (var derive = new Rfc2898DeriveBytes(pin, salt, Iterations))
                    return FixedTimeEquals(expected, derive.GetBytes(expected.Length));
            }
            catch
            {
                return false;
            }
        }

        private static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            var difference = 0;
            for (var index = 0; index < left.Length; index++)
                difference |= left[index] ^ right[index];
            return difference == 0;
        }
    }

    public static class AppSession
    {
        public static AppUser CurrentUser { get; private set; }
        public static bool IsOwner { get { return CurrentUser != null && CurrentUser.Role == UserRoles.Owner; } }
        public static string UserName { get { return CurrentUser == null ? "" : CurrentUser.Name; } }
        public static string RoleName { get { return CurrentUser == null ? "" : UserRoles.DisplayName(CurrentUser.Role); } }

        public static void SignIn(AppUser user)
        {
            if (user == null || !user.Active) throw new InvalidOperationException("El usuario no está disponible.");
            CurrentUser = user;
        }

        public static void SignOut()
        {
            CurrentUser = null;
        }
    }
}
