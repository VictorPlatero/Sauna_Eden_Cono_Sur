using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;

namespace EdenPOS
{
    public sealed class Product
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public decimal Price { get; set; }
        public string ImageFile { get; set; }
        public string ColorHex { get; set; }
        public bool Active { get; set; }
        public int Stock { get; set; }
        public int InitialStock { get; set; }
        public bool TrackInventory { get; set; }
        public List<ProductOption> Options { get; set; }
    }

    public sealed class ReceiptLine
    {
        public string ProductId { get; set; }
        public string OptionId { get; set; }
        public string Name { get; set; }
        public string Comment { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }

        [ScriptIgnore]
        public decimal LineTotal { get { return UnitPrice * Quantity; } }
    }

    public sealed class Receipt
    {
        public string Id { get; set; }
        public int Number { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Employee { get; set; }
        public string CustomerName { get; set; }
        public string CustomerDni { get; set; }
        public string PaymentMethod { get; set; }
        public decimal AmountReceived { get; set; }
        public decimal CashAmount { get; set; }
        public decimal YapeAmount { get; set; }
        public decimal Total { get; set; }
        public string Status { get; set; }
        public List<ReceiptLine> Lines { get; set; } = new List<ReceiptLine>();
        public DateTime? RefundedAt { get; set; }
        public string RefundReason { get; set; }

        [ScriptIgnore]
        public decimal CashPaid { get { return PaymentMethod == "Mixto" ? CashAmount : PaymentMethod == "Efectivo" ? Total : 0m; } }

        [ScriptIgnore]
        public decimal YapePaid { get { return PaymentMethod == "Mixto" ? YapeAmount : PaymentMethod == "Yape" ? Total : 0m; } }
    }

    public sealed class SavedTicket
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string CustomerName { get; set; }
        public string CustomerDni { get; set; }
        public int LockerNumber { get; set; }
        public List<int> LockerNumbers { get; set; } = new List<int>();
        public int AdultCount { get; set; }
        public int ChildCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<ReceiptLine> Lines { get; set; } = new List<ReceiptLine>();

        [ScriptIgnore]
        public decimal Total { get { return Lines == null ? 0m : Lines.Sum(x => x.LineTotal); } }
    }

    public sealed class Customer
    {
        public string Dni { get; set; }
        public string FullName { get; set; }
        public string Source { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public sealed class Shift
    {
        public string Id { get; set; }
        public int Number { get; set; }
        public string Employee { get; set; }
        public DateTime OpenedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public decimal OpeningAmount { get; set; }
        public decimal ClosingAmount { get; set; }
        public decimal ExpectedAmount { get; set; }
        public bool IsOpen { get; set; }
    }

    public sealed class CashMovement
    {
        public string Id { get; set; }
        public string ShiftId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Employee { get; set; }
        public string Type { get; set; }
        public decimal Amount { get; set; }
        public string Comment { get; set; }
    }

    public sealed class InventoryMovement
    {
        public string Id { get; set; }
        public string ProductId { get; set; }
        public string ProductName { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Type { get; set; }
        public int Quantity { get; set; }
        public int PreviousStock { get; set; }
        public int NewStock { get; set; }
        public string Note { get; set; }
        public string ReferenceId { get; set; }
    }

    public sealed class AppUser
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Role { get; set; }
        public string PinSalt { get; set; }
        public string PinHash { get; set; }
        public bool Active { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public sealed class AppSettings
    {
        public string StoreName { get; set; }
        public string EmployeeName { get; set; }
        public string CurrencySymbol { get; set; }
        public int NextReceiptNumber { get; set; }
        public int NextShiftNumber { get; set; }
        public bool DniLookupEnabled { get; set; }
        public string DniApiTokenProtected { get; set; }
        public int InventorySchemaVersion { get; set; }
        public int CatalogSchemaVersion { get; set; }
        public int SecuritySchemaVersion { get; set; }
        public List<AppUser> Users { get; set; } = new List<AppUser>();
    }
}
