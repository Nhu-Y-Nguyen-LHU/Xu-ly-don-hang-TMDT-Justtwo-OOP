using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ECommerceApp
{
    #region 1. Cấu trúc Interfaces (Giai đoạn 1)

    public interface IShippingStrategy
    {
        double CalculateShipping(Order order);
        string GetStrategyName();
    }

    public interface IPaymentStrategy
    {
        bool Pay(double amount);
        string GetMethodName();
    }

    public interface IDiscount
    {
        double ApplyDiscount(double currentPrice);
        string GetDescription();
    }

    #endregion

    #region 2. Các lớp Item & Đa hình (Giai đoạn 2)

    public abstract class Item
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public double Price { get; set; }
        public int Quantity { get; set; }

        protected Item(int id, string name, double price, int quantity)
        {
            Id = id;
            Name = name;
            Price = price;
            Quantity = quantity;
        }

        public virtual double GetSubTotal() => Price * Quantity;
        public abstract double GetExtraFee(); // Phí phụ thu đặc thù
    }

    public class NormalItem : Item
    {
        public NormalItem(int id, string name, double price, int quantity) : base(id, name, price, quantity) { }
        public override double GetExtraFee() => 0;
    }

    public class FragileItem : Item
    {
        public FragileItem(int id, string name, double price, int quantity) : base(id, name, price, quantity) { }
        public override double GetExtraFee() => Price * 0.05; // 5% bảo hiểm hàng dễ vỡ
    }

    public class BulkyItem : Item
    {
        public BulkyItem(int id, string name, double price, int quantity) : base(id, name, price, quantity) { }
        public override double GetExtraFee() => 50000 * Quantity; // Phí bốc xếp hàng cồng kềnh
    }

    #endregion

    #region 3. Triển khai Design Patterns (Giai đoạn 3)

    // --- Strategy Pattern cho Phí Ship ---
    public class StandardShipping : IShippingStrategy
    {
        public double CalculateShipping(Order order) => 30000;
        public string GetStrategyName() => "Giao hàng tiêu chuẩn";
    }

    public class ExpressShipping : IShippingStrategy
    {
        public double CalculateShipping(Order order) => 60000;
        public string GetStrategyName() => "Giao hàng hỏa tốc";
    }

    // --- Strategy Pattern cho Thanh toán ---
    public class CODPayment : IPaymentStrategy
    {
        public bool Pay(double amount) => true;
        public string GetMethodName() => "Tiền mặt (COD)";
    }

    public class MomoPayment : IPaymentStrategy
    {
        public bool Pay(double amount)
        {
            Console.WriteLine($"[Momo] Thanh toán {amount:N0}đ thành công.");
            return true;
        }
        public string GetMethodName() => "Ví Momo";
    }

    // --- Decorator Pattern cho Chiết khấu đa tầng ---
    public class BasePrice : IDiscount
    {
        public double ApplyDiscount(double price) => price;
        public string GetDescription() => "Giá gốc";
    }

    public abstract class DiscountDecorator : IDiscount
    {
        protected IDiscount _wrapped;
        public DiscountDecorator(IDiscount wrapped) => _wrapped = wrapped;
        public virtual double ApplyDiscount(double price) => _wrapped.ApplyDiscount(price);
        public virtual string GetDescription() => _wrapped.GetDescription();
    }

    public class VoucherDiscount : DiscountDecorator
    {
        private double _percent;
        public VoucherDiscount(IDiscount wrapped, double percent) : base(wrapped) => _percent = percent;
        public override double ApplyDiscount(double price) => base.ApplyDiscount(price) * (1 - _percent / 100);
        public override string GetDescription() => base.GetDescription() + $" + Voucher {_percent}%";
    }

    public class FreeshipDiscount : DiscountDecorator
    {
        private double _shipFee;
        public FreeshipDiscount(IDiscount wrapped, double shipFee) : base(wrapped) => _shipFee = shipFee;
        public override double ApplyDiscount(double price) => base.ApplyDiscount(price) - _shipFee;
        public override string GetDescription() => base.GetDescription() + " + Freeship";
    }

    #endregion

    #region 4. Logic Đơn hàng & Quản lý File (Giai đoạn 2 & 4)

    public class Order
    {
        public List<Item> Items { get; set; } = new List<Item>();
        public IShippingStrategy Shipping { get; set; } = new StandardShipping();
        public IPaymentStrategy Payment { get; set; } = new CODPayment();
        public IDiscount DiscountProcessor { get; set; } = new BasePrice();

        public void AddItem(Item item) => Items.Add(item);

        public double GetSubTotal() => Items.Sum(i => i.GetSubTotal() + i.GetExtraFee());
        public double GetShippingFee() => Shipping.CalculateShipping(this);
        public double GetFinalTotal() => DiscountProcessor.ApplyDiscount(GetSubTotal() + GetShippingFee());

        public void ExportInvoice()
        {
            string path = "Invoice.txt";
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("==========================================");
            sb.AppendLine("             HÓA ĐƠN MUA HÀNG             ");
            sb.AppendLine("==========================================");
            sb.AppendLine($"Ngày đặt   : {DateTime.Now}");
            sb.AppendLine("------------------------------------------");
            foreach (var item in Items)
            {
                sb.AppendLine($"{item.Name,-20} x{item.Quantity,-3} {item.Price,12:N0}đ");
            }
            sb.AppendLine("------------------------------------------");
            sb.AppendLine($"Tạm tính: {GetSubTotal(),25:N0}đ");
            sb.AppendLine($"Phí ship: {GetShippingFee(),25:N0}đ");
            sb.AppendLine($"Giảm giá: {DiscountProcessor.GetDescription()}");
            sb.AppendLine($"TỔNG CỘNG: {GetFinalTotal(),24:N0}đ");
            sb.AppendLine("==========================================");
            sb.AppendLine($"Thanh toán: {Payment.GetMethodName()}");

            File.WriteAllText(path, sb.ToString());
            Console.WriteLine(sb.ToString());
        }
    }

    public static class Database
    {
        private const string FilePath = "Products.csv";

        public static void Initialize()
        {
            if (!File.Exists(FilePath))
            {
                string content = "id,name,price,type\n" +
                                 "1,Binh hoa thuy tinh,150000,FRAGILE\n" +
                                 "2,Tu go,2500000,BULKY\n" +
                                 "3,Ao thun,200000,NORMAL";
                File.WriteAllText(FilePath, content);
            }
        }

        public static List<Item> LoadProducts()
        {
            var list = new List<Item>();
            var lines = File.ReadAllLines(FilePath).Skip(1);
            foreach (var line in lines)
            {
                var p = line.Split(',');
                int id = int.Parse(p[0]);
                string name = p[1];
                double price = double.Parse(p[2]);
                string type = p[3];

                if (type == "FRAGILE") list.Add(new FragileItem(id, name, price, 0));
                else if (type == "BULKY") list.Add(new BulkyItem(id, name, price, 0));
                else list.Add(new NormalItem(id, name, price, 0));
            }
            return list;
        }
    }

    #endregion

    #region 5. Chương trình chính (Giai đoạn 4)

    class Program
    {
        static Order currentOrder = new Order();
        static List<Item> inventory = new List<Item>();

        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Database.Initialize(); // Tự động tạo file nếu thiếu
            inventory = Database.LoadProducts();

            while (true)
            {
                try
                {
                    Console.Clear();
                    Console.WriteLine("=== MENU CỬA HÀNG TMĐT ===");
                    Console.WriteLine("1. Xem sản phẩm");
                    Console.WriteLine("2. Mua hàng");
                    Console.WriteLine("3. Áp dụng Giảm giá đa tầng (Decorator)");
                    Console.WriteLine("4. Chọn Vận chuyển & Thanh toán (Strategy)");
                    Console.WriteLine("5. Thanh toán & Xuất hóa đơn");
                    Console.WriteLine("0. Thoát");
                    Console.Write("Chọn: ");

                    string choice = Console.ReadLine();
                    switch (choice)
                    {
                        case "1":
                            foreach (var p in inventory) Console.WriteLine($"{p.Id}. {p.Name} - {p.Price:N0}đ ({p.GetType().Name})");
                            break;
                        case "2":
                            Console.Write("Nhập ID: "); int id = int.Parse(Console.ReadLine());
                            Console.Write("Số lượng: "); int q = int.Parse(Console.ReadLine());
                            if (q <= 0) throw new Exception("Số lượng phải dương.");
                            var item = inventory.Find(x => x.Id == id);
                            if (item != null)
                            {
                                var newItem = (Item)Activator.CreateInstance(item.GetType(), item.Id, item.Name, item.Price, q);
                                currentOrder.AddItem(newItem);
                                Console.WriteLine("Đã thêm!");
                            }
                            break;
                        case "3":
                            currentOrder.DiscountProcessor = new VoucherDiscount(new FreeshipDiscount(new BasePrice(), currentOrder.GetShippingFee()), 10);
                            Console.WriteLine("Đã áp dụng Voucher 10% + Freeship!");
                            break;
                        case "4":
                            Console.WriteLine("1. Giao hỏa tốc | 2. Thanh toán Momo");
                            string sub = Console.ReadLine();
                            if (sub == "1") currentOrder.Shipping = new ExpressShipping();
                            if (sub == "2") currentOrder.Payment = new MomoPayment();
                            break;
                        case "5":
                            currentOrder.ExportInvoice();
                            currentOrder = new Order(); // Reset
                            break;
                        case "0": return;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Lỗi: " + ex.Message);
                }
                Console.WriteLine("\nNhấn phím bất kỳ...");
                Console.ReadKey();
            }
        }
    }

    #endregion
}