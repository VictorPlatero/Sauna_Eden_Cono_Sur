using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace EdenPOS
{
    public static class AppData
    {
        private static readonly object Sync = new object();
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        public static string DataDirectory
        {
            get
            {
                var overridePath = Environment.GetEnvironmentVariable("EDENPOS_DATA_PATH");
                if (!string.IsNullOrWhiteSpace(overridePath)) return overridePath;
                var pathFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data.path");
                if (File.Exists(pathFile))
                {
                    var configuredPath = File.ReadAllText(pathFile).Trim();
                    if (!string.IsNullOrWhiteSpace(configuredPath))
                        return Path.GetFullPath(Path.IsPathRooted(configuredPath) ? configuredPath : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configuredPath));
                }
                if (IsPortableMode)
                {
                    var visibleDatabase = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BaseDatos");
                    var legacyPortable = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
                    if (!Directory.Exists(visibleDatabase) && Directory.Exists(legacyPortable)) return legacyPortable;
                    return visibleDatabase;
                }
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EdenPOS", "Data");
            }
        }

        public static bool IsPortableMode
        {
            get { return File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "portable.mode")); }
        }

        public static string AssetDirectory
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Catalog"); }
        }

        public static event EventHandler DataChanged;

        public static void Initialize()
        {
            Directory.CreateDirectory(DataDirectory);
            if (!File.Exists(PathFor("settings"))) Save("settings", CreateDefaultSettings());
            if (!File.Exists(PathFor("catalog"))) Save("catalog", CreateDefaultProducts());
            if (!File.Exists(PathFor("receipts"))) Save("receipts", new List<Receipt>());
            if (!File.Exists(PathFor("saved_tickets"))) Save("saved_tickets", new List<SavedTicket>());
            if (!File.Exists(PathFor("customers"))) Save("customers", new List<Customer>());
            if (!File.Exists(PathFor("shifts"))) Save("shifts", new List<Shift>());
            if (!File.Exists(PathFor("cash_movements"))) Save("cash_movements", new List<CashMovement>());
            if (!File.Exists(PathFor("inventory_movements"))) Save("inventory_movements", new List<InventoryMovement>());
            MigrateDefaults();
        }

        public static AppSettings Settings
        {
            get { return Load("settings", CreateDefaultSettings); }
        }

        public static List<Product> Products
        {
            get { return Load("catalog", CreateDefaultProducts); }
        }

        public static List<Receipt> Receipts
        {
            get { return Load("receipts", () => new List<Receipt>()); }
        }

        public static List<SavedTicket> SavedTickets
        {
            get { return Load("saved_tickets", () => new List<SavedTicket>()); }
        }

        public static List<Customer> Customers
        {
            get { return Load("customers", () => new List<Customer>()); }
        }

        public static List<AppUser> Users
        {
            get
            {
                var settings = Settings;
                return settings.Users == null ? new List<AppUser>() : settings.Users;
            }
        }

        public static List<Shift> Shifts
        {
            get { return Load("shifts", () => new List<Shift>()); }
        }

        public static List<CashMovement> CashMovements
        {
            get { return Load("cash_movements", () => new List<CashMovement>()); }
        }

        public static List<InventoryMovement> InventoryMovements
        {
            get { return Load("inventory_movements", () => new List<InventoryMovement>()); }
        }

        public static Shift OpenShift
        {
            get { return Shifts.OrderByDescending(x => x.OpenedAt).FirstOrDefault(x => x.IsOpen); }
        }

        public static void SaveSettings(AppSettings value)
        {
            Save("settings", value);
            RaiseChanged();
        }

        public static AppUser AuthenticateUser(string userId, string pin)
        {
            var user = Users.FirstOrDefault(x => x.Id == userId && x.Active);
            return UserSecurity.VerifyPin(user, pin) ? user : null;
        }

        public static AppUser SaveUser(string id, string name, string pin, string role)
        {
            lock (Sync)
            {
                if (!Validation.IsValidRequiredText(name, 3, 60))
                    throw new ArgumentException("El nombre debe tener entre 3 y 60 caracteres.");
                if (!UserRoles.IsValid(role))
                    throw new ArgumentException("Seleccione un rol válido.");

                var normalizedName = Validation.NormalizeName(name);
                var settings = Settings;
                if (settings.Users == null) settings.Users = new List<AppUser>();
                if (settings.Users.Any(x => x.Id != id && string.Equals(x.Name, normalizedName, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("Ya existe un usuario con ese nombre.");

                var user = settings.Users.FirstOrDefault(x => x.Id == id);
                if (user == null)
                {
                    user = UserSecurity.CreateUser(normalizedName, pin, role);
                    settings.Users.Add(user);
                }
                else
                {
                    if (AppSession.CurrentUser != null && AppSession.CurrentUser.Id == user.Id && user.Role != role)
                        throw new InvalidOperationException("No puede cambiar su propio rol durante la sesión actual.");
                    if (user.Role == UserRoles.Owner && role != UserRoles.Owner &&
                        settings.Users.Count(x => x.Active && x.Role == UserRoles.Owner) <= 1)
                        throw new InvalidOperationException("Debe existir al menos una dueña activa.");
                    if (!string.IsNullOrWhiteSpace(pin)) UserSecurity.SetPin(user, pin);
                    else if (string.IsNullOrWhiteSpace(user.PinHash))
                        throw new ArgumentException("Ingrese un PIN de 4 a 8 dígitos.");
                    user.Name = normalizedName;
                    user.Role = role;
                    user.UpdatedAt = DateTime.Now;
                }

                settings.SecuritySchemaVersion = Math.Max(settings.SecuritySchemaVersion, 1);
                Save("settings", settings);
                if (AppSession.CurrentUser != null && AppSession.CurrentUser.Id == user.Id) AppSession.SignIn(user);
                RaiseChanged();
                return user;
            }
        }

        public static void SetUserActive(string id, bool active)
        {
            lock (Sync)
            {
                var settings = Settings;
                if (settings.Users == null) settings.Users = new List<AppUser>();
                var user = settings.Users.FirstOrDefault(x => x.Id == id);
                if (user == null) throw new InvalidOperationException("El usuario ya no existe.");
                if (AppSession.CurrentUser != null && AppSession.CurrentUser.Id == id)
                    throw new InvalidOperationException("No puede desactivar al usuario que tiene la sesión abierta.");
                if (!active && user.Role == UserRoles.Owner &&
                    settings.Users.Count(x => x.Active && x.Role == UserRoles.Owner) <= 1)
                    throw new InvalidOperationException("Debe existir al menos una dueña activa.");
                user.Active = active;
                user.UpdatedAt = DateTime.Now;
                Save("settings", settings);
                RaiseChanged();
            }
        }

        public static void DeleteUser(string id)
        {
            lock (Sync)
            {
                var settings = Settings;
                if (settings.Users == null) settings.Users = new List<AppUser>();
                var user = settings.Users.FirstOrDefault(x => x.Id == id);
                if (user == null) return;
                if (AppSession.CurrentUser != null && AppSession.CurrentUser.Id == id)
                    throw new InvalidOperationException("No puede eliminar al usuario que tiene la sesión abierta.");
                if (user.Role == UserRoles.Owner && user.Active &&
                    settings.Users.Count(x => x.Active && x.Role == UserRoles.Owner) <= 1)
                    throw new InvalidOperationException("Debe existir al menos una dueña activa.");
                settings.Users.Remove(user);
                Save("settings", settings);
                RaiseChanged();
            }
        }

        public static void SaveProducts(List<Product> value)
        {
            Save("catalog", value);
            RaiseChanged();
        }

        public static void AddInventoryEntry(string productId, int quantity, string note)
        {
            AddInventoryEntry(productId, null, quantity, note);
        }

        public static void AddInventoryEntry(string productId, string optionId, int quantity, string note)
        {
            if (quantity <= 0 || quantity > 1000000) throw new ArgumentException("La entrada debe ser mayor que cero.");
            lock (Sync)
            {
                var products = Products;
                var product = products.FirstOrDefault(x => x.Id == productId);
                if (product == null) throw new InvalidOperationException("El artículo ya no existe.");
                var movements = InventoryMovements;
                if (!string.IsNullOrWhiteSpace(optionId))
                {
                    var option = (product.Options ?? new List<ProductOption>()).FirstOrDefault(x => x.Id == optionId);
                    if (option == null) throw new InvalidOperationException("La variante ya no existe.");
                    var previous = option.Stock;
                    option.TrackInventory = true;
                    option.Stock += quantity;
                    option.InitialStock += quantity;
                    movements.Add(NewInventoryMovement(product, option, "Entrada", quantity, previous, note, null));
                }
                else
                {
                    var previous = product.Stock;
                    product.TrackInventory = true;
                    product.Stock += quantity;
                    product.InitialStock += quantity;
                    movements.Add(NewInventoryMovement(product, "Entrada", quantity, previous, note, null));
                }
                Save("catalog", products);
                Save("inventory_movements", movements);
                RaiseChanged();
            }
        }

        public static void DeleteProduct(string productId)
        {
            lock (Sync)
            {
                var products = Products;
                var product = products.FirstOrDefault(x => x.Id == productId);
                if (product == null) return;
                if (Receipts.Any(x => x.Lines != null && x.Lines.Any(l => l.ProductId == productId)))
                    throw new InvalidOperationException("Este artículo ya tiene ventas registradas. Puede editarlo u ocultarlo, pero no eliminarlo para conservar el historial.");
                products.Remove(product);
                Save("catalog", products);
                RaiseChanged();
            }
        }

        public static Receipt AddReceipt(List<ReceiptLine> lines, string customer, string customerDni, string method, decimal received, decimal cashAmount = -1m, decimal yapeAmount = -1m)
        {
            lock (Sync)
            {
                var settings = Settings;
                var receipts = Receipts;
                var products = Products;
                var movements = InventoryMovements;
                foreach (var line in lines)
                {
                    var product = products.FirstOrDefault(x => x.Id == line.ProductId);
                    if (product == null) continue;
                    var option = FindLineOption(product, line);
                    if (option != null && option.TrackInventory && option.Stock < line.Quantity)
                        throw new InvalidOperationException("No hay saldo suficiente de " + option.Name + ". Disponible: " + option.Stock + ".");
                    if (option == null && product.TrackInventory && product.Stock < line.Quantity)
                        throw new InvalidOperationException("No hay saldo suficiente de " + product.Name + ". Disponible: " + product.Stock + ".");
                }
                var receipt = new Receipt
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Number = settings.NextReceiptNumber,
                    CreatedAt = DateTime.Now,
                    Employee = string.IsNullOrWhiteSpace(AppSession.UserName) ? settings.EmployeeName : AppSession.UserName,
                    CustomerName = string.IsNullOrWhiteSpace(customer) ? "Público general" : customer.Trim(),
                    CustomerDni = Validation.NormalizeDni(customerDni),
                    PaymentMethod = method,
                    AmountReceived = received,
                    Total = lines.Sum(x => x.LineTotal),
                    Status = "Pagado",
                    Lines = lines.Select(x => new ReceiptLine
                    {
                        ProductId = x.ProductId,
                        OptionId = x.OptionId,
                        Name = x.Name,
                        Comment = x.Comment,
                        UnitPrice = x.UnitPrice,
                        Quantity = x.Quantity
                    }).ToList()
                };
                receipt.CashAmount = cashAmount >= 0m ? cashAmount : method == "Efectivo" ? receipt.Total : 0m;
                receipt.YapeAmount = yapeAmount >= 0m ? yapeAmount : method == "Yape" ? receipt.Total : 0m;
                receipts.Add(receipt);
                foreach (var line in receipt.Lines)
                {
                    var product = products.FirstOrDefault(x => x.Id == line.ProductId);
                    if (product == null) continue;
                    var option = FindLineOption(product, line);
                    if (option != null)
                    {
                        if (!option.TrackInventory) continue;
                        var previous = option.Stock;
                        option.Stock -= line.Quantity;
                        movements.Add(NewInventoryMovement(product, option, "Venta", line.Quantity, previous, "Venta #" + receipt.Number, receipt.Id));
                    }
                    else
                    {
                        if (!product.TrackInventory) continue;
                        var previous = product.Stock;
                        product.Stock -= line.Quantity;
                        movements.Add(NewInventoryMovement(product, "Venta", line.Quantity, previous, "Venta #" + receipt.Number, receipt.Id));
                    }
                }
                settings.NextReceiptNumber++;
                Save("receipts", receipts);
                Save("settings", settings);
                Save("catalog", products);
                Save("inventory_movements", movements);
                RaiseChanged();
                return receipt;
            }
        }

        public static SavedTicket SaveTicket(List<ReceiptLine> lines, string customer, string customerDni, string name, int lockerNumber = 0)
        {
            return SaveTicket(lines, customer, customerDni, name, lockerNumber > 0 ? new[] { lockerNumber } : new int[0], 0, 0);
        }

        public static SavedTicket SaveTicket(
            List<ReceiptLine> lines,
            string customer,
            string customerDni,
            string name,
            IEnumerable<int> lockerNumbers,
            int adultCount = 0,
            int childCount = 0)
        {
            if (lines == null || lines.Count == 0) throw new InvalidOperationException("No hay artículos para guardar.");
            var selectedLockers = (lockerNumbers ?? new int[0]).Distinct().OrderBy(x => x).ToList();
            if (selectedLockers.Count == 0) throw new ArgumentException("Seleccione al menos una llave.");
            if (selectedLockers.Any(x => x < 1 || x > 35)) throw new ArgumentException("Los candados deben estar entre 1 y 35.");
            if (adultCount < 0 || adultCount > 999 || childCount < 0 || childCount > 999)
                throw new ArgumentException("La cantidad de personas no es válida.");
            lock (Sync)
            {
                var tickets = SavedTickets;
                var occupied = tickets.SelectMany(GetTicketLockers).ToList();
                var duplicated = selectedLockers.FirstOrDefault(x => occupied.Contains(x));
                if (duplicated > 0) throw new InvalidOperationException("El candado " + duplicated + " ya está ocupado. Seleccione otro.");
                var now = DateTime.Now;
                var ticket = new SavedTicket
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = string.IsNullOrWhiteSpace(name) ? "Ticket " + now.ToString("HH:mm") : name.Trim(),
                    CustomerName = string.IsNullOrWhiteSpace(customer) ? "Público general" : customer.Trim(),
                    CustomerDni = Validation.NormalizeDni(customerDni),
                    LockerNumber = selectedLockers.First(),
                    LockerNumbers = selectedLockers,
                    AdultCount = adultCount,
                    ChildCount = childCount,
                    CreatedAt = now,
                    UpdatedAt = now,
                    Lines = CopyLines(lines)
                };
                tickets.Add(ticket);
                Save("saved_tickets", tickets);
                RaiseChanged();
                return ticket;
            }
        }

        public static SavedTicket UpdateSavedTicket(
            string id,
            List<ReceiptLine> lines,
            string customer,
            string customerDni,
            string name,
            IEnumerable<int> lockerNumbers,
            int adultCount,
            int childCount)
        {
            if (lines == null || lines.Count == 0) throw new InvalidOperationException("No hay artículos para guardar.");
            var selectedLockers = (lockerNumbers ?? new int[0]).Distinct().OrderBy(x => x).ToList();
            if (selectedLockers.Count == 0) throw new ArgumentException("Seleccione al menos una llave.");
            if (selectedLockers.Any(x => x < 1 || x > 35)) throw new ArgumentException("Las llaves deben estar entre 1 y 35.");
            if (adultCount < 0 || adultCount > 999 || childCount < 0 || childCount > 999)
                throw new ArgumentException("La cantidad de personas no es válida.");
            lock (Sync)
            {
                var tickets = SavedTickets;
                var ticket = tickets.FirstOrDefault(x => x.Id == id);
                if (ticket == null) throw new InvalidOperationException("El ticket guardado ya no existe.");
                var occupied = tickets.Where(x => x.Id != id).SelectMany(GetTicketLockers).ToList();
                var duplicated = selectedLockers.FirstOrDefault(x => occupied.Contains(x));
                if (duplicated > 0) throw new InvalidOperationException("La llave " + duplicated + " ya está ocupada. Seleccione otra.");

                ticket.Name = string.IsNullOrWhiteSpace(name) ? ticket.Name : name.Trim();
                ticket.CustomerName = string.IsNullOrWhiteSpace(customer) ? "Público general" : customer.Trim();
                ticket.CustomerDni = Validation.NormalizeDni(customerDni);
                ticket.LockerNumber = selectedLockers.First();
                ticket.LockerNumbers = selectedLockers;
                ticket.AdultCount = adultCount;
                ticket.ChildCount = childCount;
                ticket.UpdatedAt = DateTime.Now;
                ticket.Lines = CopyLines(lines);
                Save("saved_tickets", tickets);
                RaiseChanged();
                return ticket;
            }
        }

        public static List<int> GetTicketLockers(SavedTicket ticket)
        {
            if (ticket == null) return new List<int>();
            if (ticket.LockerNumbers != null && ticket.LockerNumbers.Count > 0) return ticket.LockerNumbers.Distinct().OrderBy(x => x).ToList();
            return ticket.LockerNumber > 0 ? new List<int> { ticket.LockerNumber } : new List<int>();
        }

        public static int GetTicketAdultCount(SavedTicket ticket)
        {
            if (ticket == null) return 0;
            if (ticket.AdultCount > 0 || ticket.ChildCount > 0) return Math.Max(0, ticket.AdultCount);
            int adults;
            int children;
            OccupancyCalculator.Count(ticket.Lines, out adults, out children);
            return adults;
        }

        public static int GetTicketChildCount(SavedTicket ticket)
        {
            if (ticket == null) return 0;
            if (ticket.AdultCount > 0 || ticket.ChildCount > 0) return Math.Max(0, ticket.ChildCount);
            int adults;
            int children;
            OccupancyCalculator.Count(ticket.Lines, out adults, out children);
            return children;
        }

        public static void UpdateSavedTicketLockers(string id, IEnumerable<int> lockerNumbers)
        {
            lock (Sync)
            {
                var selectedLockers = (lockerNumbers ?? new int[0]).Distinct().OrderBy(x => x).ToList();
                if (selectedLockers.Count == 0) throw new ArgumentException("Seleccione al menos una llave.");
                if (selectedLockers.Any(x => x < 1 || x > 35)) throw new ArgumentException("Las llaves deben estar entre 1 y 35.");

                var tickets = SavedTickets;
                var ticket = tickets.FirstOrDefault(x => x.Id == id);
                if (ticket == null) throw new InvalidOperationException("El ticket guardado ya no existe.");

                var occupied = tickets.Where(x => x.Id != id).SelectMany(GetTicketLockers).ToList();
                var duplicated = selectedLockers.FirstOrDefault(x => occupied.Contains(x));
                if (duplicated > 0) throw new InvalidOperationException("La llave " + duplicated + " ya está ocupada. Seleccione otra.");

                ticket.LockerNumber = selectedLockers.First();
                ticket.LockerNumbers = selectedLockers;
                ticket.UpdatedAt = DateTime.Now;
                Save("saved_tickets", tickets);
                RaiseChanged();
            }
        }

        public static void DeleteSavedTicket(string id)
        {
            var tickets = SavedTickets;
            tickets.RemoveAll(x => x.Id == id);
            Save("saved_tickets", tickets);
            RaiseChanged();
        }

        private static List<ReceiptLine> CopyLines(IEnumerable<ReceiptLine> lines)
        {
            return (lines ?? Enumerable.Empty<ReceiptLine>()).Select(x => new ReceiptLine
            {
                ProductId = x.ProductId,
                OptionId = x.OptionId,
                Name = x.Name,
                Comment = x.Comment,
                UnitPrice = x.UnitPrice,
                Quantity = x.Quantity
            }).ToList();
        }

        public static Customer FindCustomer(string dni)
        {
            var normalized = Validation.NormalizeDni(dni);
            return Customers.FirstOrDefault(x => x.Dni == normalized);
        }

        public static Customer SaveCustomer(string dni, string fullName, string source)
        {
            var normalized = Validation.NormalizeDni(dni);
            if (!Validation.IsValidDni(normalized)) throw new ArgumentException("El DNI debe tener 8 dígitos.");
            if (!Validation.IsValidPersonName(fullName)) throw new ArgumentException("El nombre completo no es válido.");
            var customers = Customers;
            var customer = customers.FirstOrDefault(x => x.Dni == normalized);
            if (customer == null)
            {
                customer = new Customer { Dni = normalized, CreatedAt = DateTime.Now };
                customers.Add(customer);
            }
            customer.FullName = Validation.NormalizeName(fullName);
            customer.Source = string.IsNullOrWhiteSpace(source) ? "Manual" : source;
            customer.UpdatedAt = DateTime.Now;
            Save("customers", customers);
            RaiseChanged();
            return customer;
        }

        public static void ClearCustomers()
        {
            Save("customers", new List<Customer>());
            RaiseChanged();
        }

        public static string ProtectSecret(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            var bytes = Encoding.UTF8.GetBytes(value.Trim());
            var protectedBytes = ProtectedData.Protect(bytes, Encoding.UTF8.GetBytes("EdenPOS-DNI-v1"), DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(protectedBytes);
        }

        public static string UnprotectSecret(string protectedValue)
        {
            var environmentToken = Environment.GetEnvironmentVariable("EDENPOS_DNI_TOKEN");
            if (!string.IsNullOrWhiteSpace(environmentToken)) return environmentToken.Trim();
            if (string.IsNullOrWhiteSpace(protectedValue)) return "";
            try
            {
                var bytes = Convert.FromBase64String(protectedValue);
                var clearBytes = ProtectedData.Unprotect(bytes, Encoding.UTF8.GetBytes("EdenPOS-DNI-v1"), DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(clearBytes);
            }
            catch { return ""; }
        }

        public static void RefundReceipt(Receipt receipt, string reason)
        {
            lock (Sync)
            {
                var receipts = Receipts;
                var stored = receipts.FirstOrDefault(x => x.Id == receipt.Id);
                if (stored == null || stored.Status == "Reembolsado") return;
                var products = Products;
                var movements = InventoryMovements;
                stored.Status = "Reembolsado";
                stored.RefundedAt = DateTime.Now;
                stored.RefundReason = reason;
                foreach (var line in stored.Lines ?? new List<ReceiptLine>())
                {
                    var product = products.FirstOrDefault(x => x.Id == line.ProductId);
                    if (product == null) continue;
                    var option = FindLineOption(product, line);
                    if (option != null)
                    {
                        if (!option.TrackInventory) continue;
                        var previous = option.Stock;
                        option.Stock += line.Quantity;
                        movements.Add(NewInventoryMovement(product, option, "Reembolso", line.Quantity, previous, "Reembolso #" + stored.Number, stored.Id));
                    }
                    else
                    {
                        if (!product.TrackInventory) continue;
                        var previous = product.Stock;
                        product.Stock += line.Quantity;
                        movements.Add(NewInventoryMovement(product, "Reembolso", line.Quantity, previous, "Reembolso #" + stored.Number, stored.Id));
                    }
                }
                Save("receipts", receipts);
                Save("catalog", products);
                Save("inventory_movements", movements);
                RaiseChanged();
            }
        }

        private static InventoryMovement NewInventoryMovement(Product product, string type, int quantity, int previous, string note, string referenceId)
        {
            return new InventoryMovement
            {
                Id = Guid.NewGuid().ToString("N"), ProductId = product.Id, ProductName = product.Name,
                CreatedAt = DateTime.Now, Type = type, Quantity = quantity, PreviousStock = previous,
                NewStock = product.Stock, Note = string.IsNullOrWhiteSpace(note) ? "Sin comentario" : note.Trim(), ReferenceId = referenceId
            };
        }

        private static InventoryMovement NewInventoryMovement(Product product, ProductOption option, string type, int quantity, int previous, string note, string referenceId)
        {
            return new InventoryMovement
            {
                Id = Guid.NewGuid().ToString("N"), ProductId = option.Id, ProductName = product.Name + " · " + option.Name,
                CreatedAt = DateTime.Now, Type = type, Quantity = quantity, PreviousStock = previous,
                NewStock = option.Stock, Note = string.IsNullOrWhiteSpace(note) ? "Sin comentario" : note.Trim(), ReferenceId = referenceId
            };
        }

        private static ProductOption FindLineOption(Product product, ReceiptLine line)
        {
            if (product == null || line == null || product.Options == null) return null;
            if (!string.IsNullOrWhiteSpace(line.OptionId))
                return product.Options.FirstOrDefault(x => x.Id == line.OptionId);
            return product.Options.FirstOrDefault(x =>
                string.Equals(product.Name + " · " + x.Name, line.Name, StringComparison.CurrentCultureIgnoreCase));
        }

        public static Shift OpenNewShift(decimal openingAmount)
        {
            lock (Sync)
            {
                if (OpenShift != null) throw new InvalidOperationException("Ya existe un turno abierto.");
                var settings = Settings;
                var shifts = Shifts;
                var shift = new Shift
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Number = settings.NextShiftNumber,
                    Employee = string.IsNullOrWhiteSpace(AppSession.UserName) ? settings.EmployeeName : AppSession.UserName,
                    OpenedAt = DateTime.Now,
                    OpeningAmount = openingAmount,
                    IsOpen = true
                };
                shifts.Add(shift);
                settings.NextShiftNumber++;
                Save("shifts", shifts);
                Save("settings", settings);
                RaiseChanged();
                return shift;
            }
        }

        public static void CloseShift(string id, decimal closingAmount)
        {
            var shifts = Shifts;
            var shift = shifts.FirstOrDefault(x => x.Id == id && x.IsOpen);
            if (shift == null) return;
            shift.ExpectedAmount = CalculateExpectedCash(shift);
            shift.ClosingAmount = closingAmount;
            shift.ClosedAt = DateTime.Now;
            shift.IsOpen = false;
            Save("shifts", shifts);
            RaiseChanged();
        }

        public static CashMovement AddCashMovement(string type, decimal amount, string comment)
        {
            var shift = OpenShift;
            if (shift == null) throw new InvalidOperationException("Abra un turno antes de registrar movimientos de tesorería.");
            var movement = new CashMovement
            {
                Id = Guid.NewGuid().ToString("N"),
                ShiftId = shift.Id,
                CreatedAt = DateTime.Now,
                Employee = string.IsNullOrWhiteSpace(AppSession.UserName) ? Settings.EmployeeName : AppSession.UserName,
                Type = type,
                Amount = type == "Salida" ? -Math.Abs(amount) : Math.Abs(amount),
                Comment = string.IsNullOrWhiteSpace(comment) ? "Sin comentario" : comment.Trim()
            };
            var movements = CashMovements;
            movements.Add(movement);
            Save("cash_movements", movements);
            RaiseChanged();
            return movement;
        }

        public static decimal CalculateExpectedCash(Shift shift)
        {
            if (shift == null) return 0m;
            var end = shift.ClosedAt ?? DateTime.MaxValue;
            var cashSales = Receipts.Where(x => x.CreatedAt >= shift.OpenedAt && x.CreatedAt <= end &&
                x.Status == "Pagado").Sum(x => x.CashPaid);
            var movementTotal = CashMovements.Where(x => x.ShiftId == shift.Id).Sum(x => x.Amount);
            return shift.OpeningAmount + cashSales + movementTotal;
        }

        public static string Money(decimal value)
        {
            return Settings.CurrencySymbol + value.ToString("N2", CultureInfo.GetCultureInfo("es-PE"));
        }

        public static string ResolveAsset(string imageFile)
        {
            if (string.IsNullOrWhiteSpace(imageFile)) return null;
            if (imageFile.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                return Path.Combine(DataDirectory, "Images", imageFile.Substring(5));
            if (Path.IsPathRooted(imageFile)) return imageFile;
            return Path.Combine(AssetDirectory, imageFile);
        }

        public static Image LoadImage(string imageFile)
        {
            try
            {
                var path = ResolveAsset(imageFile);
                if (!File.Exists(path)) return null;
                using (var stream = File.OpenRead(path))
                using (var source = Image.FromStream(stream))
                    return new Bitmap(source);
            }
            catch { return null; }
        }

        public static void OpenDataFolder()
        {
            Directory.CreateDirectory(DataDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe", "\"" + DataDirectory + "\"") { UseShellExecute = true });
        }

        public static void SetPortableMode(bool enabled)
        {
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EDENPOS_DATA_PATH")))
                throw new InvalidOperationException("No se puede cambiar el modo portable mientras EDENPOS_DATA_PATH está configurado.");
            var marker = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "portable.mode");
            var portableData = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BaseDatos");
            var localData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EdenPOS", "Data");
            if (enabled)
            {
                if (IsPortableMode) return;
                var currentData = DataDirectory;
                EnsureDirectoryWritable(AppDomain.CurrentDomain.BaseDirectory);
                CopyDirectory(currentData, portableData);
                File.WriteAllText(marker, "Edén POS portable mode", Encoding.UTF8);
            }
            else
            {
                if (!IsPortableMode) return;
                CopyDirectory(portableData, localData);
                File.Delete(marker);
            }
            Directory.CreateDirectory(DataDirectory);
            Initialize();
            RaiseChanged();
        }

        public static void ExportBackup(string destinationPath)
        {
            if (string.IsNullOrWhiteSpace(destinationPath)) throw new ArgumentException("Seleccione un archivo de destino.");
            Initialize();
            var destination = Path.GetFullPath(destinationPath);
            var dataRoot = Path.GetFullPath(DataDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (destination.StartsWith(dataRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Guarde la copia fuera de la carpeta de datos de Edén POS.");

            using (var file = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(file, ZipArchiveMode.Create))
            {
                foreach (var source in Directory.GetFiles(DataDirectory, "*", SearchOption.AllDirectories))
                {
                    var relative = source.Substring(dataRoot.Length).Replace('\\', '/');
                    if (relative.Equals("settings.json", StringComparison.OrdinalIgnoreCase) ||
                        relative.Equals("errors.log", StringComparison.OrdinalIgnoreCase) ||
                        relative.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) continue;
                    archive.CreateEntryFromFile(source, relative, CompressionLevel.Optimal);
                }

                var settings = Settings;
                var portableSettings = new AppSettings
                {
                    StoreName = settings.StoreName,
                    EmployeeName = settings.EmployeeName,
                    CurrencySymbol = settings.CurrencySymbol,
                    NextReceiptNumber = settings.NextReceiptNumber,
                    NextShiftNumber = settings.NextShiftNumber,
                    DniLookupEnabled = false,
                    DniApiTokenProtected = "",
                    InventorySchemaVersion = settings.InventorySchemaVersion,
                    SecuritySchemaVersion = settings.SecuritySchemaVersion,
                    Users = (settings.Users ?? new List<AppUser>()).Select(x => new AppUser
                    {
                        Id = x.Id,
                        Name = x.Name,
                        Role = x.Role,
                        PinSalt = x.PinSalt,
                        PinHash = x.PinHash,
                        Active = x.Active,
                        CreatedAt = x.CreatedAt,
                        UpdatedAt = x.UpdatedAt
                    }).ToList()
                };
                WriteZipText(archive, "settings.json", PrettyJson(Json.Serialize(portableSettings)));
                WriteZipText(archive, "backup_manifest.json", PrettyJson(Json.Serialize(new Dictionary<string, object>
                {
                    { "application", "EdenPOS" }, { "version", "1.3" }, { "created_at", DateTime.Now },
                    { "token_included", false }, { "portable_images", true }
                })));
            }
        }

        public static void ImportBackup(string sourcePath)
        {
            if (!File.Exists(sourcePath)) throw new FileNotFoundException("No se encontró la copia seleccionada.", sourcePath);
            var currentSettings = Settings;
            var currentToken = currentSettings.DniApiTokenProtected;
            var currentLookupEnabled = currentSettings.DniLookupEnabled && !string.IsNullOrWhiteSpace(UnprotectSecret(currentToken));
            var extracted = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            long totalSize = 0;
            using (var file = File.OpenRead(sourcePath))
            using (var archive = new ZipArchive(file, ZipArchiveMode.Read))
            {
                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrWhiteSpace(entry.Name)) continue;
                    var relative = entry.FullName.Replace('\\', '/');
                    if (relative.StartsWith("/", StringComparison.Ordinal) || relative.Contains("../") || Path.IsPathRooted(relative))
                        throw new InvalidDataException("La copia contiene una ruta no segura.");
                    if (entry.Length > 20 * 1024 * 1024) throw new InvalidDataException("La copia contiene un archivo demasiado grande.");
                    totalSize += entry.Length;
                    if (totalSize > 100 * 1024 * 1024) throw new InvalidDataException("La copia supera el tamaño permitido.");
                    using (var input = entry.Open())
                    using (var memory = new MemoryStream()) { input.CopyTo(memory); extracted[relative] = memory.ToArray(); }
                }
            }
            if (!extracted.ContainsKey("catalog.json") || !extracted.ContainsKey("settings.json"))
                throw new InvalidDataException("El archivo no es una copia válida de Edén POS.");

            Directory.CreateDirectory(DataDirectory);
            foreach (var item in extracted)
            {
                if (item.Key.Equals("settings.json", StringComparison.OrdinalIgnoreCase) || item.Key.Equals("backup_manifest.json", StringComparison.OrdinalIgnoreCase)) continue;
                var destination = Path.GetFullPath(Path.Combine(DataDirectory, item.Key.Replace('/', Path.DirectorySeparatorChar)));
                var root = Path.GetFullPath(DataDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Ruta inválida dentro de la copia.");
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.WriteAllBytes(destination, item.Value);
            }

            var importedSettings = Json.Deserialize<AppSettings>(Encoding.UTF8.GetString(extracted["settings.json"]));
            if (importedSettings == null) throw new InvalidDataException("La configuración de la copia no es válida.");
            importedSettings.DniApiTokenProtected = currentToken;
            importedSettings.DniLookupEnabled = currentLookupEnabled;
            Save("settings", importedSettings);
            Initialize();
            RaiseChanged();
        }

        public static void LogError(Exception exception, string context)
        {
            try
            {
                Directory.CreateDirectory(DataDirectory);
                var text = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + context + Environment.NewLine +
                           (exception == null ? "Error desconocido" : exception.ToString()) + Environment.NewLine + new string('-', 80) + Environment.NewLine;
                File.AppendAllText(Path.Combine(DataDirectory, "errors.log"), text, Encoding.UTF8);
            }
            catch { }
        }

        private static string PathFor(string name) { return Path.Combine(DataDirectory, name + ".json"); }

        private static T Load<T>(string name, Func<T> fallback)
        {
            lock (Sync)
            {
                try
                {
                    var path = PathFor(name);
                    if (!File.Exists(path)) return fallback();
                    var text = File.ReadAllText(path, Encoding.UTF8);
                    var result = Json.Deserialize<T>(text);
                    return result == null ? fallback() : result;
                }
                catch
                {
                    var path = PathFor(name);
                    if (File.Exists(path)) File.Copy(path, path + ".invalid-" + DateTime.Now.ToString("yyyyMMddHHmmss"), true);
                    return fallback();
                }
            }
        }

        private static void Save<T>(string name, T value)
        {
            lock (Sync)
            {
                Directory.CreateDirectory(DataDirectory);
                var path = PathFor(name);
                var temp = path + ".tmp";
                var json = PrettyJson(Json.Serialize(value));
                File.WriteAllText(temp, json, new UTF8Encoding(false));
                File.Copy(temp, path, true);
                File.Delete(temp);
            }
        }

        private static void RaiseChanged()
        {
            var handler = DataChanged;
            if (handler != null) handler(null, EventArgs.Empty);
        }

        private static void MigrateDefaults()
        {
            var products = Products;
            var settings = Settings;
            var changed = false;
            if (settings.Users == null) settings.Users = new List<AppUser>();
            if (settings.SecuritySchemaVersion < 1 || !settings.Users.Any(x => x.Active && x.Role == UserRoles.Owner))
            {
                if (!settings.Users.Any(x => x.Active && x.Role == UserRoles.Owner))
                    settings.Users.Add(UserSecurity.CreateUser("DUEÑA", "0000", UserRoles.Owner));
                settings.SecuritySchemaVersion = 1;
                Save("settings", settings);
            }
            if (settings.InventorySchemaVersion < 1)
            {
                foreach (var product in products)
                    product.TrackInventory = product.Category == "ASEO & VENTAS" || product.Category == "CAFETERÍA";
                settings.InventorySchemaVersion = 1;
                Save("settings", settings);
                changed = true;
            }
            if (settings.InventorySchemaVersion < 2)
            {
                foreach (var product in products.Where(x => x.TrackInventory && x.InitialStock == 0 && x.Stock == 0))
                    product.TrackInventory = false;
                settings.InventorySchemaVersion = 2;
                Save("settings", settings);
                changed = true;
            }
            if (settings.CatalogSchemaVersion < 1)
            {
                foreach (var product in products.Where(ProductOptionCatalog.HasOptions))
                {
                    var options = ProductOptionCatalog.GetOptions(product);
                    if (options.Count > 0) product.Price = options.Min(x => x.Price);
                    product.TrackInventory = false;
                }
                settings.CatalogSchemaVersion = 1;
                Save("settings", settings);
                changed = true;
            }
            var productsMissingOptions = products
                .Where(x => x.Options == null && ProductOptionCatalog.HasTemplate(x.Name))
                .ToList();
            if (settings.CatalogSchemaVersion < 2 || productsMissingOptions.Count > 0)
            {
                foreach (var product in productsMissingOptions)
                    product.Options = ProductOptionCatalog.CreateDefaultOptions(product.Name);
                settings.CatalogSchemaVersion = 2;
                Save("settings", settings);
                changed = true;
            }
            foreach (var product in products.Where(x => x.Name == "ADULTO MAYOR" && x.Price == 12m))
            {
                product.Price = 15m;
                changed = true;
            }
            var imageRoot = Path.GetFullPath(Path.Combine(DataDirectory, "Images")).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            foreach (var product in products.Where(x => !string.IsNullOrWhiteSpace(x.ImageFile) && Path.IsPathRooted(x.ImageFile)))
            {
                try
                {
                    var imagePath = Path.GetFullPath(product.ImageFile);
                    if (imagePath.StartsWith(imageRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        product.ImageFile = "data:" + Path.GetFileName(imagePath);
                        changed = true;
                    }
                }
                catch { }
            }
            if (changed) Save("catalog", products);
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            if (!Directory.Exists(source)) return;
            foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            {
                var relative = directory.Substring(source.TrimEnd(Path.DirectorySeparatorChar).Length).TrimStart(Path.DirectorySeparatorChar);
                Directory.CreateDirectory(Path.Combine(destination, relative));
            }
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                var relative = file.Substring(source.TrimEnd(Path.DirectorySeparatorChar).Length).TrimStart(Path.DirectorySeparatorChar);
                var target = Path.Combine(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target, true);
            }
        }

        private static void EnsureDirectoryWritable(string directory)
        {
            var test = Path.Combine(directory, ".edenpos-write-test-" + Guid.NewGuid().ToString("N"));
            try { File.WriteAllText(test, "ok"); File.Delete(test); }
            catch { throw new UnauthorizedAccessException("La carpeta del programa no permite escritura. Mueva Edén POS a Documentos o a una memoria USB antes de activar el modo portable."); }
        }

        private static void WriteZipText(ZipArchive archive, string name, string text)
        {
            var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
            using (var output = new StreamWriter(entry.Open(), new UTF8Encoding(false))) output.Write(text);
        }

        private static string PrettyJson(string json)
        {
            var result = new StringBuilder();
            var quoted = false;
            var escaped = false;
            var indent = 0;
            foreach (var ch in json)
            {
                if (escaped) { result.Append(ch); escaped = false; continue; }
                if (ch == '\\' && quoted) { result.Append(ch); escaped = true; continue; }
                if (ch == '"') { quoted = !quoted; result.Append(ch); continue; }
                if (quoted) { result.Append(ch); continue; }
                switch (ch)
                {
                    case '{':
                    case '[': result.Append(ch).AppendLine(); indent++; result.Append(new string(' ', indent * 2)); break;
                    case '}':
                    case ']': result.AppendLine(); indent--; result.Append(new string(' ', indent * 2)).Append(ch); break;
                    case ',': result.Append(ch).AppendLine().Append(new string(' ', indent * 2)); break;
                    case ':': result.Append(": "); break;
                    default: if (!char.IsWhiteSpace(ch)) result.Append(ch); break;
                }
            }
            return result.ToString();
        }

        private static AppSettings CreateDefaultSettings()
        {
            return new AppSettings
            {
                StoreName = "BAÑOS SAUNA EDÉN",
                EmployeeName = "VICTOR",
                CurrencySymbol = "S/ ",
                NextReceiptNumber = 1,
                NextShiftNumber = 1,
                DniLookupEnabled = false,
                DniApiTokenProtected = "",
                InventorySchemaVersion = 2,
                CatalogSchemaVersion = 2,
                SecuritySchemaVersion = 1,
                Users = new List<AppUser> { UserSecurity.CreateUser("DUEÑA", "0000", UserRoles.Owner) }
            };
        }

        private static List<Product> CreateDefaultProducts()
        {
            var products = new List<Product>();
            Action<string, string, decimal, string, string> add = (category, name, price, image, color) => products.Add(new Product
            {
                Id = Guid.NewGuid().ToString("N"), Category = category, Name = name, Price = price,
                ImageFile = image, ColorHex = color, Active = true,
                TrackInventory = false
            });

            add("ALQUILERES", "ALQ BIKINI MUJER", 5m, "swimsuit.png", "#1EA7E1");
            add("ALQUILERES", "ALQ FLOTADOR", 5m, "float.png", "#00A7C8");
            add("ALQUILERES", "ALQ GORRO", 3m, "cap.png", "#36A9E1");
            add("ALQUILERES", "ALQ LENTES", 4m, "goggles.png", "#3F51B5");
            add("ALQUILERES", "ALQ SANDALIA", 3m, "sandal.png", "#00A8D8");
            add("ALQUILERES", "ALQ SHORT", 5m, "shorts.png", "#29B6F6");
            add("ALQUILERES", "ALQ TOALLA", 3m, "towel.png", "#42A5F5");
            add("ALQUILERES", "PAREO", 4m, "pareo.png", "#039BE5");

            add("ASEO & VENTAS", "ACONDICIONADOR", 2m, "lotion.png", "#FFB74D");
            add("ASEO & VENTAS", "AFEITADOR", 2m, "razor.png", "#78909C");
            add("ASEO & VENTAS", "BICARBONATO", 2m, "salt.png", "#039BE5");
            add("ASEO & VENTAS", "BLOQUEADOR", 5m, "sun.png", "#FFB300");
            add("ASEO & VENTAS", "DESODORANTE", 4m, "spray.png", "#8E24AA");
            add("ASEO & VENTAS", "ESPONJA DE BAÑO", 3m, "sponge.png", "#F48FB1");
            add("ASEO & VENTAS", "EXFOLIANTE SAUNA", 5m, "towel.png", "#26C6DA");
            add("ASEO & VENTAS", "GANCHOS CABELLO", 2m, "hair.png", "#6D4C41");
            add("ASEO & VENTAS", "GORROS", 3m, "cap.png", "#E64A19");
            add("ASEO & VENTAS", "JABONES", 1m, "soap.png", "#EC407A");
            add("ASEO & VENTAS", "LENTES", 4m, "glasses.png", "#EF5350");
            add("ASEO & VENTAS", "MANO SAUNA", 5m, "glove.png", "#AB47BC");
            add("ASEO & VENTAS", "SHAMPOOS", 1m, "shampoo.png", "#42A5F5");

            add("CAFETERÍA", "GASEOSAS", 4m, "soda.png", "#6D4C41");
            add("CAFETERÍA", "HIDRATANTE", 1.50m, "water.png", "#29B6F6");
            add("CAFETERÍA", "JARRAS", 5m, "pitcher.png", "#FDD835");
            add("CAFETERÍA", "MARCIANOS", 2m, "alien.png", "#7E57C2");
            add("CAFETERÍA", "SANDWICHES", 2.50m, "sandwich.png", "#8D6E63");
            add("CAFETERÍA", "SNACKS", 1.50m, "cookie.png", "#795548");

            add("SAUNA", "ADULTO MAYOR", 15m, "elder.png", "#7CB342");
            add("SAUNA", "AMIGOS X3", 48m, "friends.png", "#43A047");
            add("SAUNA", "AMIGOS X4", 60m, "friends.png", "#43A047");
            add("SAUNA", "AMIGOS X5", 72m, "friends.png", "#43A047");
            add("SAUNA", "FAMILIA 1", 35m, "family.png", "#F39C12");
            add("SAUNA", "FAMILIA 2", 45m, "family.png", "#F39C12");
            add("SAUNA", "GENERAL", 18m, "sauna.png", "#7CB342");
            add("SAUNA", "MUJER 2", 30m, "woman.png", "#E91E63");
            add("SAUNA", "MUJER 3", 42m, "woman.png", "#E91E63");
            add("SAUNA", "MUJER 4", 52m, "woman.png", "#E91E63");
            add("SAUNA", "PAREJA", 32m, "couple.png", "#9C27B0");
            add("SAUNA", "NIÑO", 10m, "child.png", "#26A69A");
            add("SAUNA", "PASE CORTESÍA", 0m, "gift.png", "#FBC02D");
            add("SAUNA", "PRÉSTAMO GORRO", 3m, "cap.png", "#F4511E");
            add("SAUNA", "VIP", 20m, "crown.png", "#F9A825");
            foreach (var product in products.Where(x => ProductOptionCatalog.HasTemplate(x.Name)))
                product.Options = ProductOptionCatalog.CreateDefaultOptions(product.Name);
            return products;
        }
    }
}
